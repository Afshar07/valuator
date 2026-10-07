using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Responsive equal- or weighted-column panel. It wraps by available width instead of by breakpoint classes.</summary>
internal sealed class AdaptiveGrid : Panel
{
    public double MinItemWidth { get; set; } = 220;
    public double Gap { get; set; } = 16;
    /// <summary>Optional relative column widths, used only when every child fits on a single row.</summary>
    public double[]? Weights { get; set; }
    /// <summary>Stretch each child to its row height (cards in a row line up) or keep their own height.</summary>
    public bool StretchRows { get; set; }

    private List<Control> Items => Children.Where(child => child.IsVisible).ToList();
    private int Columns(double width, int count)
    {
        if (count == 0) return 1;
        if (double.IsInfinity(width)) return count;
        return Math.Clamp((int)Math.Floor((width + Gap) / (MinItemWidth + Gap)), 1, count);
    }
    private double[] ColumnWidths(double width, int columns, int count)
    {
        if (double.IsInfinity(width)) return Enumerable.Repeat(MinItemWidth, columns).ToArray();
        var available = Math.Max(0, width - Gap * (columns - 1));
        var weights = Weights is not null && columns == count && Weights.Length == columns ? Weights : Enumerable.Repeat(1d, columns).ToArray();
        return weights.Select(weight => available * weight / weights.Sum()).ToArray();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items; var columns = Columns(availableSize.Width, items.Count); var widths = ColumnWidths(availableSize.Width, columns, items.Count);
        double height = 0, width = 0;
        for (var start = 0; start < items.Count; start += columns)
        {
            double row = 0, rowWidth = 0;
            for (var column = 0; column < columns && start + column < items.Count; column++)
            {
                items[start + column].Measure(new Size(widths[column], double.PositiveInfinity));
                row = Math.Max(row, items[start + column].DesiredSize.Height); rowWidth += widths[column] + (column > 0 ? Gap : 0);
            }
            height += row + (start > 0 ? Gap : 0); width = Math.Max(width, rowWidth);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var items = Items; var columns = Columns(finalSize.Width, items.Count); var widths = ColumnWidths(finalSize.Width, columns, items.Count);
        double y = 0;
        for (var start = 0; start < items.Count; start += columns)
        {
            double x = 0, row = 0;
            for (var column = 0; column < columns && start + column < items.Count; column++)
                row = Math.Max(row, items[start + column].DesiredSize.Height);
            for (var column = 0; column < columns && start + column < items.Count; column++)
            {
                var item = items[start + column];
                item.Arrange(new Rect(x, y, widths[column], StretchRows ? row : item.DesiredSize.Height)); x += widths[column] + Gap;
            }
            y += row + Gap;
        }
        return finalSize;
    }
}

/// <summary>Small layout and decoration factories shared by every screen.</summary>
internal static class Ui
{
    public static Border Card(Control? child = null, Thickness? padding = null) => new Border
    {
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(PresentationTheme.RadiusCard),
        Padding = padding ?? new Thickness(16, 14),
        ClipToBounds = true,
        Child = child
    }.Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault").CardShadowed();

    /// <summary>A row that sits inside a card list: full-bleed, separated from the previous row by a subtle top rule.</summary>
    public static Border Separated(Control child, bool first = false) => new Border
    {
        BorderThickness = new Thickness(0, first ? 0 : 1, 0, 0),
        Child = child
    }.Paint(Border.BorderBrushProperty, "BorderSubtle");

    public static Border Rule() => new Border { Height = 1 }.Paint(Border.BackgroundProperty, "BorderSubtle");

    public static Border Dot(double size, string color, bool square = false) => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(square ? 1.5 : size / 2),
        VerticalAlignment = VerticalAlignment.Center
    }.Paint(Border.BackgroundProperty, color);

    public static Border Pill(PresentationContext context, Func<string> caption, Tone tone, string? icon = null)
    {
        var text = context.Label(caption, "Micro", tone.Foreground); text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis;
        Control content = text;
        if (icon is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            row.Children.Add(Icons.Glyph(icon, 11, tone.Foreground, IconWeight.Bold)); row.Children.Add(text); content = row;
        }
        return new Border
        {
            CornerRadius = new CornerRadius(PresentationTheme.RadiusPill),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = content
        }.Paint(Border.BackgroundProperty, tone.Background);
    }

    /// <summary>Thin rounded progress bar; the fill grows from the start edge in either flow direction.</summary>
    public static Control Progress(double percent, double height = 5)
    {
        var value = Math.Clamp(percent, 0, 100);
        var grid = new Grid { Height = height, VerticalAlignment = VerticalAlignment.Center, ColumnDefinitions = new ColumnDefinitions($"{Math.Max(value, 0.0001):0.####}*,{Math.Max(100 - value, 0.0001):0.####}*") };
        var track = new Border { CornerRadius = new CornerRadius(height / 2) }.Paint(Border.BackgroundProperty, "BackgroundTrack");
        Grid.SetColumnSpan(track, 2); grid.Children.Add(track);
        if (value > 0) grid.Children.Add(new Border { CornerRadius = new CornerRadius(height / 2) }.Paint(Border.BackgroundProperty, "Accent"));
        return grid;
    }

    /// <summary>Banner strip (warning, AI pending, info) with a leading icon.</summary>
    public static Control Banner(Control content, string icon, string iconColor, string background, string? border = null, bool dashed = false)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
        var glyph = Icons.Glyph(icon, 17, iconColor); glyph.VerticalAlignment = VerticalAlignment.Top; glyph.Margin = new Thickness(0, 1, 0, 0);
        grid.Children.Add(glyph); Grid.SetColumn(content, 1); content.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(content);
        if (dashed) return new DashedFrame(grid, border ?? "BorderDefault", background, PresentationTheme.RadiusMedium, new Thickness(14, 10));
        var banner = new Border { CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), Padding = new Thickness(14, 10), Child = grid }
            .Paint(Border.BackgroundProperty, background);
        if (border is not null) { banner.BorderThickness = new Thickness(1); banner.Paint(Border.BorderBrushProperty, border); }
        return banner;
    }

    /// <summary>Icon square used for project initials and file types.</summary>
    public static Border Tile(Control content, double size = 30, double radius = 8, string background = "BackgroundTrack") => new Border
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(radius),
        VerticalAlignment = VerticalAlignment.Center,
        Child = content
    }.Paint(Border.BackgroundProperty, background);

    public static Border Initial(string name, double size = 30)
    {
        var initial = string.IsNullOrWhiteSpace(name) ? "·" : name.Trim().EnumerateRunes().First().ToString();
        var text = PresentationTheme.Typeset(new TextBlock { Text = initial, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, "BodyStrong", "TextSecondary");
        return Tile(text, size);
    }

    /// <summary>Card header: title, optional muted count and optional trailing action.</summary>
    public static Grid Header(PresentationContext context, Func<string> title, Control? action = null, Func<string>? count = null)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, MinHeight = 26 };
        var heading = context.Label(title, "Heading"); heading.VerticalAlignment = VerticalAlignment.Center;
        if (count is not null)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            line.Children.Add(heading); var muted = context.Label(count, "MetaMedium", "TextTertiary"); muted.FontSize = 14; muted.VerticalAlignment = VerticalAlignment.Center; line.Children.Add(muted);
            header.Children.Add(line);
        }
        else header.Children.Add(heading);
        if (action is not null) { action.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(action, 1); header.Children.Add(action); }
        return header;
    }
}

/// <summary>Rounded frame with a dashed outline: marks AI proposals and not-yet-built concepts.</summary>
internal sealed class DashedFrame : Panel
{
    public DashedFrame(Control child, string stroke, string? background, double radius, Thickness padding, double thickness = 1)
    {
        var outline = new Avalonia.Controls.Shapes.Rectangle { RadiusX = radius, RadiusY = radius, StrokeThickness = thickness, StrokeDashArray = [4, 3], IsHitTestVisible = false };
        outline.Paint(Avalonia.Controls.Shapes.Shape.StrokeProperty, stroke);
        var body = new Border { CornerRadius = new CornerRadius(radius), Padding = padding, Child = child };
        if (background is not null) body.Paint(Border.BackgroundProperty, background);
        Children.Add(body); Children.Add(outline);
    }
}

/// <summary>Padded card with a header and a body stack.</summary>
internal sealed class SectionCard : Border
{
    public StackPanel Body { get; } = new() { Spacing = 10 };
    public SectionCard(PresentationContext context, string title, Control? action = null, Func<string>? count = null) : this(context, () => context.Text.Get(title), action, count) { }
    public SectionCard(PresentationContext context, Func<string> title, Control? action = null, Func<string>? count = null)
    {
        Classes.Add("sectionCard"); BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(PresentationTheme.RadiusCard); Padding = new Thickness(16, 14);
        this.Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault").CardShadowed();
        Body.Children.Add(Ui.Header(context, title, action, count)); Child = Body;
    }
}

/// <summary>Card whose rows run edge to edge, separated by rules (lists and tables).</summary>
internal sealed class ListCard : Border
{
    private readonly StackPanel _rows = new();
    public ListCard(PresentationContext context, Func<string>? title = null, Control? action = null, Func<string>? count = null, Control? columnHeader = null)
    {
        BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(PresentationTheme.RadiusCard); ClipToBounds = true;
        this.Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault").CardShadowed();
        var stack = new StackPanel();
        if (title is not null) stack.Children.Add(new Border { Padding = new Thickness(16, 12, 16, 10), Child = Ui.Header(context, title, action, count) });
        if (columnHeader is not null) stack.Children.Add(columnHeader);
        stack.Children.Add(_rows); Child = stack;
        _hasHeading = title is not null && columnHeader is null;
    }
    private readonly bool _hasHeading;
    public int Count => _rows.Children.Count;
    public void Add(Control row) => _rows.Children.Add(Ui.Separated(row, first: _rows.Children.Count == 0 && !_hasHeading));
    /// <summary>Muted band that groups the rows below it (e.g. Active / Done).</summary>
    public void AddGroup(PresentationContext context, Func<string> label)
    {
        var text = context.Label(label, "MetaMedium", "TextSecondary");
        _rows.Children.Add(new Border { Padding = new Thickness(16, 6), BorderThickness = new Thickness(0, 1, 0, 0), Child = text }
            .Paint(BackgroundProperty, "BackgroundMuted").Paint(BorderBrushProperty, "BorderSubtle"));
    }
    public void Clear() => _rows.Children.Clear();
}

/// <summary>Column header band for table-like list cards.</summary>
internal sealed class TableHeader : Border
{
    public TableHeader(PresentationContext context, string columns, params string[] keys)
    {
        Padding = new Thickness(16, 9); BorderThickness = new Thickness(0, 0, 0, 1);
        this.Paint(BackgroundProperty, "BackgroundMuted").Paint(BorderBrushProperty, "BorderDefault");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 16 };
        for (var index = 0; index < keys.Length; index++)
        {
            if (keys[index].Length == 0) continue;
            var key = keys[index]; var label = context.Label(() => context.Text.Get(key), "MetaMedium", "TextSecondary"); label.TextWrapping = TextWrapping.NoWrap;
            label.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetColumn(label, index); grid.Children.Add(label);
        }
        Child = grid;
    }
}

internal sealed class StatCard : Border
{
    public StatCard(PresentationContext context, string name, string labelKey, int value, string icon, string iconColor, string numberColor = "TextPrimary")
    {
        Name = name; BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(PresentationTheme.RadiusCard); Padding = new Thickness(16, 14);
        this.Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault").CardShadowed();
        var stack = new StackPanel { Spacing = 6 };
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        line.Children.Add(Icons.Glyph(icon, 15, iconColor)); var label = context.Label(labelKey, "Caption", "TextSecondary"); label.VerticalAlignment = VerticalAlignment.Center; line.Children.Add(label);
        stack.Children.Add(line);
        var number = context.Label(() => value.ToString("N0", context.Locale.Culture), "Stat", numberColor); number.Name = "StatValue"; stack.Children.Add(number);
        Child = stack;
    }
}

/// <summary>Whole-row button inside a list card; the accessible name describes the row for assistive technology and tests.</summary>
internal class ListRow : Button
{
    protected override Type StyleKeyOverride => typeof(Button);
    public ListRow(PresentationContext context, Control content, Func<string> accessibleName, Func<Task> action)
    {
        Classes.Add("row"); Content = content;
        context.Localized.Bind(this, control => AutomationProperties.SetName(control, accessibleName()));
        Click += async (_, _) => await context.ActAsync(this, action);
    }

    /// <summary>Leading visual, title/subtitle stack, optional pill and trailing text.</summary>
    public static Grid Layout(PresentationContext context, Control? leading, Func<string> title, Func<string>? subtitle, Control? pill, Func<string>? trailing, string trailingColor = "TextSecondary")
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        if (leading is not null) grid.Children.Add(leading);
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(context.Label(title, "BodyMedium"));
        if (subtitle is not null) labels.Children.Add(context.Label(subtitle, "Caption", "TextSecondary"));
        Grid.SetColumn(labels, 1); grid.Children.Add(labels);
        if (pill is not null) { Grid.SetColumn(pill, 2); grid.Children.Add(pill); }
        if (trailing is not null)
        {
            var value = context.Label(trailing, "Caption", trailingColor); value.TextWrapping = TextWrapping.NoWrap; value.VerticalAlignment = VerticalAlignment.Center; value.TextAlignment = TextAlignment.End;
            Grid.SetColumn(value, 3); grid.Children.Add(value);
        }
        return grid;
    }
}

internal static class StatusVisuals
{
    public static (string Icon, IconWeight Weight, Tone Tone) Requirement(RequirementStatus status) => status switch
    {
        RequirementStatus.Complete => (Icons.CheckCircle, IconWeight.Fill, Tone.Success),
        RequirementStatus.Provided => (Icons.CircleHalf, IconWeight.Regular, Tone.Accent),
        RequirementStatus.NeedsReview => (Icons.WarningCircle, IconWeight.Regular, Tone.Warning),
        _ => (Icons.CircleDashed, IconWeight.Regular, Tone.Error)
    };

    public static Tone Project(ProjectStatus status) => status switch
    {
        ProjectStatus.Active => Tone.Success,
        ProjectStatus.OnHold => Tone.Neutral,
        ProjectStatus.Completed => Tone.Accent,
        _ => Tone.Neutral
    };

    public static (string Icon, IconWeight Weight, string Color) Task(ProjectTaskStatus status) => status switch
    {
        ProjectTaskStatus.Done => (Icons.CheckCircle, IconWeight.Fill, "Success"),
        ProjectTaskStatus.Cancelled => (Icons.Prohibit, IconWeight.Regular, "TextTertiary"),
        ProjectTaskStatus.InProgress => (Icons.CircleHalf, IconWeight.Regular, "Accent"),
        _ => (Icons.Circle, IconWeight.Regular, "TextTertiary")
    };
}

/// <summary>Two-to-four option segmented control (theme, language).</summary>
internal sealed class Segmented : Border
{
    private readonly List<(string Id, Button Button)> _options = [];
    public Segmented(PresentationContext context, IEnumerable<(string Id, Func<string> Label)> options, Func<string> selected, Action<string> select, double optionWidth = double.NaN, string? name = null)
    {
        if (name is not null) Name = name;
        CornerRadius = new CornerRadius(9); Padding = new Thickness(3); this.Paint(BackgroundProperty, "BackgroundTrack");
        var grid = new UniformGrid { Rows = 1 };
        foreach (var (id, label) in options)
        {
            var button = new Button { MinWidth = double.IsNaN(optionWidth) ? 0 : optionWidth, Margin = new Thickness(1, 0) }; button.Classes.Add("segment");
            context.Localized.Bind(button, control => { control.Content = label(); AutomationProperties.SetName(control, label()); });
            button.Click += (_, _) => select(id);
            _options.Add((id, button)); grid.Children.Add(button);
        }
        Child = grid;
        context.Localized.Bind(this, _ => Refresh(selected()));
    }
    public void Refresh(string selected)
    {
        foreach (var (id, button) in _options)
        {
            button.Classes.Set("selected", id == selected);
            if (id == selected) button.CardShadowedButton(); else button.ClearValue(Button.EffectProperty);
        }
    }
}

internal static class ButtonDecorations
{
    /// <summary>Selected segment lift; Avalonia buttons have no box shadow of their own, so a subtle drop shadow effect stands in.</summary>
    public static void CardShadowedButton(this Button button) => button.Effect = new DropShadowEffect { BlurRadius = 2, OffsetX = 0, OffsetY = 1, Opacity = 0.08 };
}

/// <summary>Application sidebar: brand, navigation, storage note, theme and language controls.</summary>
internal sealed class AppSidebar : Border
{
    private readonly Dictionary<string, Button> _entries = [];
    private readonly TextBlock _badge;
    private readonly Border _badgeHost;
    public Segmented ThemeSelector { get; }
    public Button Language { get; }

    public AppSidebar(PresentationContext context, IReadOnlyList<(string Page, string Key, string Name, string Icon)> pages, Action<string> navigate)
    {
        Name = "AppSidebar"; Width = 232; Padding = new Thickness(12, 18, 12, 14); BorderThickness = new Thickness(0, 0, 1, 0);
        this.Paint(BackgroundProperty, "BackgroundSidebar").Paint(BorderBrushProperty, "BorderDefault");
        var dock = new DockPanel();

        var brand = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Margin = new Thickness(8, 2, 8, 18) };
        brand.Children.Add(Ui.Tile(Icons.Glyph(Icons.Scales, 18, "OnAccent", IconWeight.Bold), 34, 9, "Accent"));
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = context.Label("app.title", "BodyStrong"); title.FontSize = 14; title.TextWrapping = TextWrapping.NoWrap; names.Children.Add(title);
        var subtitle = context.Label("presentation.workspace", "Meta", "TextSecondary"); subtitle.TextWrapping = TextWrapping.NoWrap; names.Children.Add(subtitle);
        Grid.SetColumn(names, 1); brand.Children.Add(names);
        DockPanel.SetDock(brand, Dock.Top); dock.Children.Add(brand);

        var footer = new StackPanel { Spacing = 8 };
        var stored = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(10, 0, 10, 4) };
        stored.Children.Add(Icons.Glyph(Icons.HardDrives, 14)); var storedText = context.Label("presentation.localMvp", "Meta", "TextSecondary"); storedText.VerticalAlignment = VerticalAlignment.Center; stored.Children.Add(storedText);
        footer.Children.Add(stored);
        ThemeSelector = new Segmented(context, AppearanceContext.Themes.Select(id => (id, (Func<string>)(() => context.Text.Get("theme." + id)))), () => context.Appearance.Theme,
            id => context.SetTheme(id), name: "ThemeSelector");
        footer.Children.Add(ThemeSelector);
        Language = new Button { Name = "LanguageSelector", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 36, Padding = new Thickness(10, 0), CornerRadius = new CornerRadius(9) };
        var language = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        language.Children.Add(Icons.Glyph(Icons.Translate, 15, "TextPrimary"));
        var current = context.Label(() => context.Locale.LanguageCode == "fa" ? "فارسی" : "English", "BodyMedium"); current.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(current, 1); language.Children.Add(current);
        var other = context.Label(() => context.Locale.LanguageCode == "fa" ? "English" : "فارسی", "Micro", "TextTertiary"); other.FontWeight = FontWeight.Normal; other.VerticalAlignment = VerticalAlignment.Center;
        other.FontFamily = context.Locale.LanguageCode == "fa" ? PresentationTheme.LatinFontFamily : PresentationTheme.PersianFontFamily;
        context.Localized.Bind(other, control => control.FontFamily = context.Locale.LanguageCode == "fa" ? PresentationTheme.LatinFontFamily : PresentationTheme.PersianFontFamily);
        Grid.SetColumn(other, 2); language.Children.Add(other);
        Language.Content = language;
        context.Localized.Bind(Language, control => AutomationProperties.SetName(control, context.Text.Get("settings.language")));
        Language.Click += (_, _) => context.SetLanguage(context.Locale.LanguageCode == "fa" ? "en" : "fa");
        footer.Children.Add(Language);
        DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);

        var entries = new StackPanel { Spacing = 4 };
        _badge = PresentationTheme.Typeset(new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, "Micro", "Error");
        _badge.FontWeight = FontWeight.SemiBold;
        _badgeHost = new Border { MinWidth = 18, Height = 18, Padding = new Thickness(5, 0), CornerRadius = new CornerRadius(9), IsVisible = false, Child = _badge, VerticalAlignment = VerticalAlignment.Center }
            .Paint(BackgroundProperty, "ErrorSoft");
        foreach (var (page, key, name, icon) in pages)
        {
            var button = new Button { Name = name }; button.Classes.Add("nav");
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            row.Children.Add(Icons.Glyph(icon, 16, "TextSecondary"));
            var caption = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(caption, 1); row.Children.Add(caption);
            if (page == "dashboard") { Grid.SetColumn(_badgeHost, 2); row.Children.Add(_badgeHost); }
            button.Content = row;
            context.Localized.Bind(button, control => { caption.Text = context.Text.Get(key); AutomationProperties.SetName(control, context.Text.Get(key)); });
            var target = page;
            button.Click += (_, _) => navigate(target);
            _entries[page] = button; entries.Children.Add(button);
        }
        dock.Children.Add(entries); Child = dock;
        context.Localized.Bind(this, _ => _badge.Text = _count.ToString("N0", context.Locale.Culture));
        _context = context;
    }
    private readonly PresentationContext _context;
    private int _count;

    public void Select(string page)
    {
        foreach (var (id, button) in _entries)
        {
            button.Classes.Set("selected", id == page);
            if (button.Content is Grid grid && grid.Children[0] is TextBlock icon) icon.Paint(TextBlock.ForegroundProperty, id == page ? "TextPrimary" : "TextSecondary");
        }
    }

    public void SetAttentionCount(int count)
    {
        _count = count; _badge.Text = count.ToString("N0", _context.Locale.Culture); _badgeHost.IsVisible = count > 0;
    }
}

/// <summary>Keeps screens at a readable maximum width, anchored to the start edge in both flow directions.</summary>
internal sealed class ReadableColumn : Decorator
{
    public double MaxContentWidth { get; set; } = 1120;
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(availableSize.Width, MaxContentWidth);
        Child?.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, Child?.DesiredSize.Height ?? 0);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, MaxContentWidth), finalSize.Height));
        return finalSize;
    }
}
