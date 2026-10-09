using Avalonia;
using Avalonia.Controls;

namespace ProjectOperations.Desktop.Common;

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
