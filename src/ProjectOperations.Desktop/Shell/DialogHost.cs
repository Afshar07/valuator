using Avalonia.Controls;
using Avalonia.Layout;

namespace ProjectOperations.Desktop.Shell;

/// <summary>Shows one modal at a time over the shell.</summary>
internal interface IDialogService
{
    bool IsOpen { get; }
    /// <summary>Shows a dialog control, or a view-model whose view the view locator supplies.</summary>
    void Show(object content);
    void Close();
}

/// <summary>Modal layer: a scrim (click closes) under a centered dialog.</summary>
internal sealed class DialogHost : Panel, IDialogService
{
    public DialogHost()
    {
        IsVisible = false; ZIndex = 10;
    }

    public bool IsOpen => IsVisible;

    public void Show(object content)
    {
        Children.Clear();
        var scrim = new Border().Paint(Border.BackgroundProperty, "Scrim");
        scrim.PointerPressed += (_, _) => Close();
        var dialog = content as Control ?? new ContentControl { Content = content };
        dialog.HorizontalAlignment = HorizontalAlignment.Center; dialog.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(scrim); Children.Add(dialog); IsVisible = true;
    }

    public void Close() { IsVisible = false; Children.Clear(); }
}
