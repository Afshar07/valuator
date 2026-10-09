using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Metadata;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Page title row: heading, optional subtitle, and trailing controls as the content.
/// <c>&lt;c:PageHeader Title="{Binding L[dashboard.title]}"&gt;&lt;Button .../&gt;&lt;/c:PageHeader&gt;</c>
/// The title and subtitle are real children rather than a control template, so they stay in the logical tree where tests look for them.
/// </summary>
internal sealed class PageHeader : Grid
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<PageHeader, string?>(nameof(Title));
    public static readonly StyledProperty<string?> SubtitleProperty = AvaloniaProperty.Register<PageHeader, string?>(nameof(Subtitle));

    private readonly TextBlock _title = new();
    private readonly TextBlock _subtitle = new() { IsVisible = false };
    private Control? _trailing;

    static PageHeader()
    {
        TitleProperty.Changed.AddClassHandler<PageHeader>((header, _) => header._title.Text = header.Title);
        SubtitleProperty.Changed.AddClassHandler<PageHeader>((header, _) => header.ShowSubtitle());
    }

    public PageHeader()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ColumnSpacing = 16;
        _title.Classes.Add("Title");
        _subtitle.Classes.Add("Body");
        _subtitle.Paint(TextBlock.ForegroundProperty, "TextSecondary");
        Children.Add(new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Bottom, Children = { _title, _subtitle } });
    }

    protected override Type StyleKeyOverride => typeof(Grid);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <summary>The controls at the end edge: buttons, a search box.</summary>
    [Content]
    public Control? Trailing
    {
        get => _trailing;
        set
        {
            if (_trailing is not null) Children.Remove(_trailing);
            _trailing = value;
            if (value is null) return;
            SetColumn(value, 1);
            Children.Add(value);
            ShowSubtitle();
        }
    }

    private void ShowSubtitle()
    {
        _subtitle.Text = Subtitle;
        _subtitle.IsVisible = !string.IsNullOrEmpty(Subtitle);
        // Beside a tall title block the controls sit on its baseline; beside a lone title they are centred.
        if (_trailing is not null) _trailing.VerticalAlignment = _subtitle.IsVisible ? VerticalAlignment.Bottom : VerticalAlignment.Center;
    }
}
