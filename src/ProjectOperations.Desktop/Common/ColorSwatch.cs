using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// A filled circle in a <c>#RRGGBB</c> colour: <c>&lt;c:ColorSwatch Color="{Binding Color}" Size="18"/&gt;</c>.
/// The colour is the user's own data (a stage colour), so it is not a theme token.
/// </summary>
internal sealed class ColorSwatch : Border
{
    public static readonly StyledProperty<string?> ColorProperty = AvaloniaProperty.Register<ColorSwatch, string?>(nameof(Color));
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<ColorSwatch, double>(nameof(Size), 16);

    static ColorSwatch()
    {
        ColorProperty.Changed.AddClassHandler<ColorSwatch>((swatch, _) => swatch.Apply());
        SizeProperty.Changed.AddClassHandler<ColorSwatch>((swatch, _) => swatch.Apply());
    }

    public ColorSwatch()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Apply();
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public string? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private void Apply()
    {
        Width = Height = Size;
        CornerRadius = new CornerRadius(Size / 2);
        if (Color is { } text && Avalonia.Media.Color.TryParse(text, out var color)) Background = new SolidColorBrush(color);
        else ClearValue(BackgroundProperty);
    }
}
