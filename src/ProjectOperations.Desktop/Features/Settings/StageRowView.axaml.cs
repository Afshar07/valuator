using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ProjectOperations.Desktop.Features.Settings;

public partial class StageRowView : UserControl
{
    public StageRowView()
    {
        InitializeComponent();
        // Enter commits through a key binding; leaving the box commits too. The view-model decides whether anything changed.
        StageTitle.LostFocus += OnTitleLostFocus;
    }

    private void OnTitleLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StageRowViewModel row) row.CommitTitleCommand.Execute(null);
    }
}
