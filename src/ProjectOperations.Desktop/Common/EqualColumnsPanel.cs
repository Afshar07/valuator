using Avalonia;
using Avalonia.Controls;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Lays children out in equal columns that never grow past the available width, so a long title ellipsizes instead of widening every
/// column (a <c>UniformGrid</c> measures against infinity and does not). One row; the calendar uses one per week.
/// </summary>
internal sealed class EqualColumnsPanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<EqualColumnsPanel, int>(nameof(Columns), 7);

    static EqualColumnsPanel() => AffectsMeasure<EqualColumnsPanel>(ColumnsProperty);

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);
        var cell = double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width / columns;
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(cell, double.PositiveInfinity));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Snap each edge to a whole pixel, as a grid does, so the rules between columns stay crisp.
        var cell = finalSize.Width / Math.Max(1, Columns);
        for (var index = 0; index < Children.Count; index++)
        {
            var start = Math.Round(index * cell);
            Children[index].Arrange(new Rect(start, 0, Math.Round((index + 1) * cell) - start, finalSize.Height));
        }
        return finalSize;
    }
}
