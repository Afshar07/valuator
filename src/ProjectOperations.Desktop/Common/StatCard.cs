using Avalonia;
using Avalonia.Controls;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// A count with an icon and a caption, for the attention home. <c>Value</c> is already formatted text; <c>ValueToken</c> is a
/// colour token such as <c>Error</c>, so a count can turn red.
/// </summary>
internal sealed class StatCard : Border
{
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<StatCard, string?>(nameof(Label));
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<StatCard, string?>(nameof(Value));
    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<StatCard, string?>(nameof(Icon));
    public static readonly StyledProperty<string?> IconTokenProperty = AvaloniaProperty.Register<StatCard, string?>(nameof(IconToken), "TextSecondary");
    public static readonly StyledProperty<string?> ValueTokenProperty = AvaloniaProperty.Register<StatCard, string?>(nameof(ValueToken), "TextPrimary");

    private readonly IconGlyph _glyph = new() { Size = 15 };
    private readonly TextBlock _label = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly TextBlock _value = new() { Name = "StatValue" };

    static StatCard()
    {
        LabelProperty.Changed.AddClassHandler<StatCard>((card, _) => card._label.Text = card.Label);
        ValueProperty.Changed.AddClassHandler<StatCard>((card, _) => card._value.Text = card.Value);
        IconProperty.Changed.AddClassHandler<StatCard>((card, _) => card._glyph.Icon = card.Icon);
        IconTokenProperty.Changed.AddClassHandler<StatCard>((card, _) => card._glyph.Token = card.IconToken);
        ValueTokenProperty.Changed.AddClassHandler<StatCard>((card, _) => card._value.Paint(TextBlock.ForegroundProperty, card.ValueToken ?? "TextPrimary"));
    }

    public StatCard()
    {
        Classes.Add("card");
        Padding = new Thickness(16, 14);
        _label.Classes.Add("Caption"); _label.Paint(TextBlock.ForegroundProperty, "TextSecondary");
        _value.Classes.Add("Stat");
        _glyph.Token = IconToken;
        var line = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { _glyph, _label } };
        Child = new StackPanel { Spacing = 6, Children = { line, _value } };
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? IconToken { get => GetValue(IconTokenProperty); set => SetValue(IconTokenProperty, value); }
    public string? ValueToken { get => GetValue(ValueTokenProperty); set => SetValue(ValueTokenProperty, value); }
}
