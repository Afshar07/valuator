using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ProjectOperations.Desktop.Common;

/// <summary>The colour family of a <see cref="Pill"/>. View-models pick a kind; the control maps it to theme tokens.</summary>
public enum PillKind { Neutral, Success, Warning, Error, Accent, Ai }

/// <summary>
/// A small rounded status label: <c>&lt;c:Pill Text="{Binding StatusText}" Kind="{Binding StatusKind}"/&gt;</c>, optionally with a leading
/// <see cref="Icon"/>. Colours are theme tokens, so Light/Dark switch live.
/// </summary>
internal sealed class Pill : Border
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Pill, string?>(nameof(Text));
    public static readonly StyledProperty<PillKind> KindProperty = AvaloniaProperty.Register<Pill, PillKind>(nameof(Kind));
    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<Pill, string?>(nameof(Icon));

    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly IconGlyph _icon = new() { Size = 11, Weight = IconWeight.Bold };
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = 5 };

    static Pill()
    {
        TextProperty.Changed.AddClassHandler<Pill>((pill, _) => pill._text.Text = pill.Text);
        KindProperty.Changed.AddClassHandler<Pill>((pill, _) => pill.Apply());
        IconProperty.Changed.AddClassHandler<Pill>((pill, _) => pill.ShowIcon());
    }

    public Pill()
    {
        CornerRadius = new CornerRadius(PresentationTheme.RadiusPill);
        Padding = new Thickness(8, 2);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        _row.Children.Add(_icon);
        Child = _text;
        Apply();
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public PillKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    /// <summary>A Phosphor glyph (<c>Icons.Check</c>) shown before the text; null shows the text alone.</summary>
    public string? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    private void ShowIcon()
    {
        _icon.Icon = Icon;
        // The text moves between the pill and the row, so detach it from one before the other adopts it.
        Child = null; _row.Children.Remove(_text);
        if (Icon is null) Child = _text;
        else { _row.Children.Add(_text); Child = _row; }
    }

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
        _icon.Token = tone.Foreground;
        this.Paint(BackgroundProperty, tone.Background);
    }
}
