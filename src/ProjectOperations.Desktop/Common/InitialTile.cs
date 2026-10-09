using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ProjectOperations.Desktop.Common;

/// <summary>A rounded square holding the first letter of a name: the project avatar. <c>&lt;c:InitialTile Text="{Binding Name}" Size="32"/&gt;</c></summary>
internal sealed class InitialTile : Border
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<InitialTile, string?>(nameof(Text));
    public static readonly StyledProperty<double> SizeProperty = AvaloniaProperty.Register<InitialTile, double>(nameof(Size), 30);

    private readonly TextBlock _initial = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    static InitialTile()
    {
        TextProperty.Changed.AddClassHandler<InitialTile>((tile, _) => tile.ShowInitial());
        SizeProperty.Changed.AddClassHandler<InitialTile>((tile, _) => tile.Width = tile.Height = tile.Size);
    }

    public InitialTile()
    {
        Width = Height = Size;
        CornerRadius = new CornerRadius(8);
        VerticalAlignment = VerticalAlignment.Center;
        this.Paint(BackgroundProperty, "BackgroundTrack");
        PresentationTheme.Typeset(_initial, "BodyStrong", "TextSecondary");
        Child = _initial;
        ShowInitial();
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private void ShowInitial() => _initial.Text = string.IsNullOrWhiteSpace(Text) ? "·" : Text.Trim().EnumerateRunes().First().ToString();
}
