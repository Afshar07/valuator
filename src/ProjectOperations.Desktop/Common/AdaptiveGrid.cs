using Avalonia;
using Avalonia.Controls;

namespace ProjectOperations.Desktop.Common;

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
