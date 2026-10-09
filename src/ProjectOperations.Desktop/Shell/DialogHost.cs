using CommunityToolkit.Mvvm.ComponentModel;
using ProjectOperations.Desktop.Common;

namespace ProjectOperations.Desktop.Shell;

/// <summary>Shows one modal at a time over the shell.</summary>
internal interface IDialogService
{
    bool IsOpen { get; }
    /// <summary>Shows a dialog: a view-model whose view the view locator supplies.</summary>
    void Show(object content);
    void Close();
}

/// <summary>The modal layer's state: what is shown, or nothing. <c>MainWindow.axaml</c> draws a scrim under it; a click on the scrim or Escape closes it.</summary>
internal sealed partial class DialogService : ViewModelBase, IDialogService
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    private object? _content;

    public bool IsOpen => Content is not null;

    public void Show(object content) => Content = content;
    public void Close() => Content = null;
}
