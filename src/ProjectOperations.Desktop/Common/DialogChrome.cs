using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Modal dialog chrome for XAML dialog views: title, close button and the content below them. The content is the whole dialog
/// below the header: a padded body and, usually, a footer <c>Border</c> with the <c>dialogFooter</c> class holding the delete, cancel
/// and save buttons. Its look lives in <c>Styles.axaml</c>.
/// </summary>
internal sealed class DialogChrome : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DialogChrome, string?>(nameof(Title));
    public static readonly StyledProperty<ICommand?> CloseCommandProperty = AvaloniaProperty.Register<DialogChrome, ICommand?>(nameof(CloseCommand));
    public static readonly StyledProperty<string?> CloseLabelProperty = AvaloniaProperty.Register<DialogChrome, string?>(nameof(CloseLabel));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public ICommand? CloseCommand { get => GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }
    /// <summary>Accessible name and tooltip of the close button.</summary>
    public string? CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }
}
