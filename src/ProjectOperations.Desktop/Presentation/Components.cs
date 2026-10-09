using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Domain;

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
