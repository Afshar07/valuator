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

/// <summary>Form building blocks shared by code-built controls.</summary>
internal static class Forms
{
    public static TextBox Input(string value = "", double height = 36) => new()
    {
        Text = value,
        TextWrapping = TextWrapping.NoWrap,
        MinHeight = height,
        VerticalContentAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
}
