using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// A Phosphor icon for XAML: <c>&lt;c:IconGlyph Icon="{x:Static app:Icons.Plus}" Size="14" Token="TextSecondary"/&gt;</c>.
/// Without a <see cref="Token"/> the glyph takes the inherited foreground, so icons in buttons follow the button's colour.
/// Icons are decorative and never carry accessible names.
/// </summary>
internal sealed class IconGlyph : TextBlock
{
    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<IconGlyph, string?>(nameof(Icon));
    public static readonly StyledProperty<IconWeight> WeightProperty = AvaloniaProperty.Register<IconGlyph, IconWeight>(nameof(Weight));
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<IconGlyph, double>(nameof(Size), 16);
    public static readonly StyledProperty<string?> TokenProperty = AvaloniaProperty.Register<IconGlyph, string?>(nameof(Token));

    static IconGlyph()
    {
        IconProperty.Changed.AddClassHandler<IconGlyph>((glyph, _) => glyph.Text = glyph.Icon);
        WeightProperty.Changed.AddClassHandler<IconGlyph>((glyph, _) => glyph.FontFamily = Icons.Family(glyph.Weight));
        SizeProperty.Changed.AddClassHandler<IconGlyph>((glyph, _) => glyph.Apply());
        TokenProperty.Changed.AddClassHandler<IconGlyph>((glyph, _) =>
        {
            if (glyph.Token is { } token) glyph.Paint(ForegroundProperty, token); else glyph.ClearValue(ForegroundProperty);
        });
    }

    public IconGlyph()
    {
        FontWeight = Avalonia.Media.FontWeight.Normal; TextAlignment = TextAlignment.Center; FlowDirection = FlowDirection.LeftToRight;
        VerticalAlignment = VerticalAlignment.Center; HorizontalAlignment = HorizontalAlignment.Center;
        FontFamily = Icons.Family(IconWeight.Regular);
        Apply();
    }

    protected override Type StyleKeyOverride => typeof(TextBlock);

    public string? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public IconWeight Weight { get => GetValue(WeightProperty); set => SetValue(WeightProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    /// <summary>Colour token name (for example <c>TextSecondary</c>), followed live across theme switches; null inherits the foreground.</summary>
    public string? Token { get => GetValue(TokenProperty); set => SetValue(TokenProperty, value); }

    private void Apply() { FontSize = Size; LineHeight = Size; }
}
