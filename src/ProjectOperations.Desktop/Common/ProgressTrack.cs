using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ProjectOperations.Desktop.Common;

/// <summary>Thin rounded progress bar, 0 to 100. The fill grows from the start edge in either flow direction.</summary>
internal sealed class ProgressTrack : Grid
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<ProgressTrack, double>(nameof(Value));

    private readonly Border _fill = new();

    static ProgressTrack()
    {
        ValueProperty.Changed.AddClassHandler<ProgressTrack>((track, _) => track.Apply());
        HeightProperty.Changed.AddClassHandler<ProgressTrack>((track, _) => track.Apply());
    }

    public ProgressTrack()
    {
        Height = 5;
        VerticalAlignment = VerticalAlignment.Center;
        var track = new Border().Paint(Border.BackgroundProperty, "BackgroundTrack");
        SetColumnSpan(track, 2);
        Children.Add(track);
        _fill.Paint(Border.BackgroundProperty, "Accent");
        Children.Add(_fill);
        Apply();
    }

    protected override Type StyleKeyOverride => typeof(Grid);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    private void Apply()
    {
        if (Children.Count < 2) return; // still being constructed
        var value = Math.Clamp(double.IsNaN(Value) ? 0 : Value, 0, 100);
        var radius = new CornerRadius((double.IsNaN(Height) ? 5 : Height) / 2);
        ColumnDefinitions = new ColumnDefinitions($"{Math.Max(value, 0.0001):0.####}*,{Math.Max(100 - value, 0.0001):0.####}*");
        _fill.IsVisible = value > 0;
        _fill.CornerRadius = radius;
        ((Border)Children[0]).CornerRadius = radius;
    }
}
