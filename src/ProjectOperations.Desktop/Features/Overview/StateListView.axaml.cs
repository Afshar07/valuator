using Avalonia.Controls;
using Avalonia.Threading;

namespace ProjectOperations.Desktop.Features.Overview;

public partial class StateListView : UserControl
{
    public StateListView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not StateListViewModel model) return;
            Name = model.CardName;
            model.PropertyChanged += (_, e) =>
            {
                // The add line is already in the tree (only hidden), so focus the input once it is shown.
                if (e.PropertyName == nameof(StateListViewModel.IsAdding) && model.IsAdding)
                    Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("StateItemInput")?.Focus());
            };
        };
    }
}
