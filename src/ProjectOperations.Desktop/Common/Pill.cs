using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProjectOperations.Desktop.Common;

/// <summary>The colour family of a <see cref="Pill"/>. View-models pick a kind; the control maps it to theme tokens.</summary>
public enum PillKind { Neutral, Success, Warning, Error, Accent, Ai }

/// <summary>
/// A small rounded status label: <c>&lt;c:Pill Text="{Binding StatusText}" Kind="{Binding StatusKind}"/&gt;</c>.
/// Colours are theme tokens, so Light/Dark switch live.
/// </summary>
internal sealed class Pill : Border
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Pill, string?>(nameof(Text));
    public static readonly StyledProperty<PillKind> KindProperty = AvaloniaProperty.Register<Pill, PillKind>(nameof(Kind));

    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };

    static Pill()
    {
        TextProperty.Changed.AddClassHandler<Pill>((pill, _) => pill._text.Text = pill.Text);
        KindProperty.Changed.AddClassHandler<Pill>((pill, _) => pill.Apply());
    }

    public Pill()
    {
        CornerRadius = new CornerRadius(PresentationTheme.RadiusPill);
        Padding = new Thickness(8, 2);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        Child = _text;
        Apply();
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public PillKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    private void Apply()
    {
        var tone = Kind switch
        {
            PillKind.Success => Tone.Success,
            PillKind.Warning => Tone.Warning,
            PillKind.Error => Tone.Error,
            PillKind.Accent => Tone.Accent,
            PillKind.Ai => Tone.Ai,
            _ => Tone.Neutral
        };
        PresentationTheme.Typeset(_text, "Micro", tone.Foreground);
        this.Paint(BackgroundProperty, tone.Background);
    }
}
