using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// A coloured dot beside a short status: <c>&lt;c:StatusLine Text="{Binding StatusText}" Token="{Binding StatusToken}"/&gt;</c>.
/// <see cref="Token"/> is a colour token name (for example <c>Warning</c>) followed live across theme switches.
/// </summary>
internal sealed class StatusLine : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<StatusLine, string?>(nameof(Text));
    public static readonly StyledProperty<string> TokenProperty = AvaloniaProperty.Register<StatusLine, string>(nameof(Token), "TextSecondary");

    private readonly Border _dot = new() { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _label = new() { TextWrapping = Avalonia.Media.TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };

    static StatusLine()
    {
        TextProperty.Changed.AddClassHandler<StatusLine>((line, _) => line._label.Text = line.Text);
        TokenProperty.Changed.AddClassHandler<StatusLine>((line, _) => line.Apply());
    }

    public StatusLine()
    {
        Orientation = Orientation.Horizontal; Spacing = 6; VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_dot); Children.Add(_label);
        Apply();
    }

    protected override Type StyleKeyOverride => typeof(StackPanel);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Token { get => GetValue(TokenProperty); set => SetValue(TokenProperty, value); }

    private void Apply()
    {
        PresentationTheme.Typeset(_label, "Meta", Token);
        _dot.Paint(Border.BackgroundProperty, Token);
    }
}
