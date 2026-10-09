using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// A project stage in its own colour: the title in the colour on a soft tint of it. The title is the user's own text, never translated.
/// <c>&lt;c:StagePill Stage="{Binding Stage}"/&gt;</c>
/// </summary>
internal sealed class StagePill : Border
{
    public static readonly StyledProperty<ProjectStage?> StageProperty = AvaloniaProperty.Register<StagePill, ProjectStage?>(nameof(Stage));

    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };

    static StagePill() => StageProperty.Changed.AddClassHandler<StagePill>((pill, _) => pill.Apply());

    public StagePill()
    {
        CornerRadius = new CornerRadius(PresentationTheme.RadiusPill);
        Padding = new Thickness(8, 2);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        PresentationTheme.Typeset(_text, "Micro");
        Child = _text;
    }

    protected override Type StyleKeyOverride => typeof(Border);

    public ProjectStage? Stage { get => GetValue(StageProperty); set => SetValue(StageProperty, value); }

    private void Apply()
    {
        if (Stage is not { } stage) { _text.Text = ""; ClearValue(BackgroundProperty); return; }
        var color = Color.Parse(stage.Color);
        _text.Text = stage.Title;
        _text.Foreground = new SolidColorBrush(color);
        Background = new SolidColorBrush(color, 0.16);
    }
}
