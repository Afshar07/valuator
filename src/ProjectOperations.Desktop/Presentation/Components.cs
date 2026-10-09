using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

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

    /// <summary>Pill in the stage's own color: title in the color on a soft tint of it (the title is user text, never translated).</summary>
    public static Border StagePill(PresentationContext context, ProjectStage stage)
    {
        var color = Color.Parse(stage.Color);
        var text = context.Label(() => stage.Title, "Micro"); text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.Foreground = new SolidColorBrush(color);
        return new Border
        {
            CornerRadius = new CornerRadius(PresentationTheme.RadiusPill),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(color, 0.16),
            Child = text
        };
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

    private static readonly Lazy<Bitmap> LogoBitmap = new(() => new Bitmap(AssetLoader.Open(new Uri("avares://ProjectOperations.Desktop/Assets/Icons/app-icon-512.png"))));

    /// <summary>Application logo (app icon tile) at the given square size.</summary>
    public static Image Logo(double size)
    {
        var image = new Image { Width = size, Height = size, Source = LogoBitmap.Value, VerticalAlignment = VerticalAlignment.Center, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
        AutomationProperties.SetAccessibilityView(image, AccessibilityView.Raw);
        return image;
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
    /// <summary>Muted band that groups the rows below it (e.g. Overdue / Next 7 days), with an optional muted count.</summary>
    public void AddGroup(PresentationContext context, Func<string> label, Func<string>? count = null, string color = "TextSecondary")
    {
        var text = context.Label(label, "MetaMedium", color); text.FontWeight = FontWeight.SemiBold;
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        line.Children.Add(text);
        if (count is not null) { var number = context.Label(count, "MetaMedium", "TextTertiary"); number.VerticalAlignment = VerticalAlignment.Center; line.Children.Add(number); }
        _rows.Children.Add(new Border { Padding = new Thickness(16, 6), BorderThickness = new Thickness(0, 1, 0, 0), Child = line }
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
    private readonly Dictionary<AppPage, Button> _entries = [];
    private readonly Dictionary<AppPage, Border> _newBadges = [];
    private readonly TextBlock _badge;
    private readonly Border _badgeHost;
    private readonly Border _gettingStarted = new() { Name = "GettingStarted", IsVisible = false, Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(12), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium) };
    private readonly PresentationContext _owner;
    public Segmented ThemeSelector { get; }
    public Button Language { get; }

    public AppSidebar(PresentationContext context, IReadOnlyList<(AppPage Page, string Key, string Name, string Icon)> pages, Action<AppPage> navigate)
    {
        Name = "AppSidebar"; Width = 232; Padding = new Thickness(12, 18, 12, 14); BorderThickness = new Thickness(0, 0, 1, 0);
        this.Paint(BackgroundProperty, "BackgroundSidebar").Paint(BorderBrushProperty, "BorderDefault");
        var dock = new DockPanel();

        var brand = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Margin = new Thickness(8, 2, 8, 18) };
        brand.Children.Add(Ui.Logo(36));
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
            var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            var freshText = context.Label("v3.newBadge", "Micro", "AccentText"); freshText.FontWeight = FontWeight.SemiBold; freshText.TextWrapping = TextWrapping.NoWrap;
            var fresh = new Border { IsVisible = false, Padding = new Thickness(7, 1), CornerRadius = new CornerRadius(PresentationTheme.RadiusPill), Name = "NewBadge", Child = freshText }.Paint(BackgroundProperty, "AccentSoft");
            _newBadges[page] = fresh; badges.Children.Add(fresh);
            if (page == AppPage.Dashboard) badges.Children.Add(_badgeHost);
            Grid.SetColumn(badges, 2); row.Children.Add(badges);
            button.Content = row;
            context.Localized.Bind(button, control => { caption.Text = context.Text.Get(key); AutomationProperties.SetName(control, context.Text.Get(key)); });
            var target = page;
            button.Click += (_, _) => navigate(target);
            _entries[page] = button; entries.Children.Add(button);
        }
        _gettingStarted.Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault");
        DockPanel.SetDock(_gettingStarted, Dock.Bottom); dock.Children.Add(_gettingStarted);
        dock.Children.Add(entries); Child = dock;
        context.Localized.Bind(this, _ => _badge.Text = _count.ToString("N0", context.Locale.Culture));
        _context = context; _owner = context;
    }
    private readonly PresentationContext _context;
    private int _count;

    public void Select(AppPage page)
    {
        foreach (var (id, button) in _entries)
        {
            button.Classes.Set("selected", id == page);
            if (button.Content is Grid grid && grid.Children[0] is TextBlock icon) icon.Paint(TextBlock.ForegroundProperty, id == page ? "TextPrimary" : "TextSecondary");
        }
    }

    /// <summary>Shows only the sections that have something to show; Projects and Settings are always available.</summary>
    public void SetVisible(AppPage page, bool visible) { if (_entries.TryGetValue(page, out var button)) button.IsVisible = visible; }
    public bool IsPageVisible(AppPage page) => _entries.TryGetValue(page, out var button) && button.IsVisible;
    /// <summary>Marks a section that appeared since the app started and has not been opened yet.</summary>
    public void SetNew(AppPage page, bool isNew) { if (_newBadges.TryGetValue(page, out var badge)) badge.IsVisible = isNew; }

    /// <summary>One getting-started step: caption key, completion, optional flag and the action that moves it forward.</summary>
    public sealed record GettingStartedItem(string Key, bool Done, bool Optional, Func<Task>? Go);

    /// <summary>Shows the checklist card under the navigation, or hides it when <paramref name="items"/> is null.</summary>
    public void SetGettingStarted(IReadOnlyList<GettingStartedItem>? items, Func<Task> hide)
    {
        _gettingStarted.IsVisible = items is not null;
        if (items is null) { _gettingStarted.Child = null; return; }
        var context = _owner; var done = items.Count(item => item.Done);
        var stack = new StackPanel { Spacing = 8 };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(context.Label("v3.gsTitle", "SmallStrong"));
        var count = context.Label(() => $"{context.Number(done)}/{context.Number(items.Count)}", "Meta", "TextSecondary"); count.TextWrapping = TextWrapping.NoWrap; Grid.SetColumn(count, 1); head.Children.Add(count);
        stack.Children.Add(head);
        var bar = Ui.Progress(100.0 * done / items.Count, 4); bar.Name = "GettingStartedProgress"; stack.Children.Add(bar);
        var list = new StackPanel { Spacing = 1 };
        foreach (var item in items)
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
            line.Children.Add(Icons.Glyph(item.Done ? Icons.CheckCircle : Icons.Circle, 15, item.Done ? "Success" : "TextTertiary", item.Done ? IconWeight.Fill : IconWeight.Regular));
            var label = context.Label(item.Key, "Caption", item.Done ? "TextTertiary" : "TextPrimary"); label.VerticalAlignment = VerticalAlignment.Center;
            if (item.Done) label.TextDecorations = TextDecorations.Strikethrough;
            Grid.SetColumn(label, 1); line.Children.Add(label);
            if (item.Optional) { var optional = context.Label("v3.optional", "Micro", "TextTertiary"); optional.FontWeight = FontWeight.Normal; optional.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(optional, 2); line.Children.Add(optional); }
            var button = new Button { Content = line, Name = "GettingStartedItem", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(4, 5), MinHeight = 0, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(PresentationTheme.RadiusSmall) };
            button.IsHitTestVisible = !item.Done && item.Go is not null;
            button.Classes.Add("row"); button.Padding = new Thickness(4, 5);
            context.Localized.Bind(button, control => Avalonia.Automation.AutomationProperties.SetName(control, context.Text.Get(item.Key)));
            if (!item.Done && item.Go is { } go) button.Click += async (_, _) => await context.ActAsync(button, go);
            list.Children.Add(button);
        }
        stack.Children.Add(list);
        var hideButton = context.Action("v3.hide", hide, "link"); hideButton.Name = "GettingStartedHide"; hideButton.Padding = new Thickness(4, 0); hideButton.FontSize = 11.5;
        stack.Children.Add(hideButton);
        _gettingStarted.Child = stack;
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

/// <summary>Form building blocks for modal dialogs (project, task, milestone, wizard). Sizes follow the v3 design: 36px inputs, 28px chips.</summary>
internal static class Forms
{
    public static TextBox Input(string value = "", double height = 36) => new()
    {
        Text = value,
        TextWrapping = TextWrapping.NoWrap,
        TextAlignment = TextAlignment.Start,
        MinHeight = height,
        VerticalContentAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public static ComboBox Choice<T>(PresentationContext context, T selected, Func<T, string> text) where T : struct, Enum => new()
    {
        ItemsSource = Enum.GetValues<T>(),
        SelectedItem = selected,
        MinHeight = 36,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<T>((value, _) => context.Label(() => text(value), "Body"))
    };

    /// <summary>Caption above an input; the label is directly followed by its input in one stack (an accessibility and test contract).</summary>
    public static StackPanel Field(PresentationContext context, string labelKey, Control input) => Field(context, () => context.Text.Get(labelKey), input);
    public static StackPanel Field(PresentationContext context, Func<string> label, Control input)
    {
        var group = new StackPanel { Spacing = 6 };
        group.Children.Add(context.Label(label, "CaptionMedium", "TextSecondary")); group.Children.Add(input);
        return group;
    }

    /// <summary>One-of-many pill chips. <paramref name="select"/> runs after the selection moved.</summary>
    public static WrapPanel Chips<T>(PresentationContext context, IEnumerable<(T Value, Func<string> Label)> options, T selected, Action<T> select, string? name = null) where T : notnull
    {
        var panel = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        if (name is not null) panel.Name = name;
        var buttons = new List<(T Value, Button Button)>();
        foreach (var (value, label) in options)
        {
            var chip = new Button { Height = 28, MinHeight = 28, Padding = new Thickness(11, 0) }; chip.Classes.Add("chip");
            context.Localized.Bind(chip, control => { control.Content = label(); Avalonia.Automation.AutomationProperties.SetName(control, label()); });
            chip.Classes.Set("selected", EqualityComparer<T>.Default.Equals(value, selected));
            var captured = value;
            chip.Click += (_, _) =>
            {
                foreach (var (other, button) in buttons) button.Classes.Set("selected", EqualityComparer<T>.Default.Equals(other, captured));
                select(captured);
            };
            buttons.Add((value, chip)); panel.Children.Add(chip);
        }
        return panel;
    }
}

/// <summary>Modal dialog chrome: title, close button, body and a footer with optional delete, cancel and save.</summary>
internal class DialogFrame : Border
{
    public StackPanel Body { get; } = new() { Spacing = 16, Margin = new Thickness(18, 12, 18, 18) };
    private readonly StackPanel _stack = new();

    public DialogFrame(PresentationContext context, string name, Func<string> title, double width = 480)
    {
        Name = name; Width = width; MaxWidth = width; Margin = new Thickness(16);
        BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(PresentationTheme.RadiusDialog);
        this.Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault").RaisedShadowed();
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(18, 16, 18, 4) };
        header.Children.Add(context.Label(title, "Dialog"));
        var close = context.IconAction("action.close", Icons.X, () => { context.Shell.CloseModal(); return Task.CompletedTask; }, "icon", iconOnly: true);
        Grid.SetColumn(close, 1); header.Children.Add(close);
        _stack.Children.Add(header); _stack.Children.Add(Body);
        Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = _stack };
    }

    public void Footer(Button? delete, Button cancel, Button save)
    {
        var footer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(18, 12) }.Paint(BorderBrushProperty, "BorderSubtle");
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        if (delete is not null) row.Children.Add(delete);
        Grid.SetColumn(cancel, 2); row.Children.Add(cancel); Grid.SetColumn(save, 3); row.Children.Add(save);
        footer.Child = row; _stack.Children.Add(footer);
    }
}
