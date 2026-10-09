using Avalonia.Controls;
using Avalonia.Threading;

namespace ProjectOperations.Desktop.Features.Requirements;

public partial class RequirementsView : UserControl
{
    public RequirementsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not RequirementsViewModel model) return;
            model.PropertyChanged += (_, e) =>
            {
                // The input is already in the tree (only hidden), so focus it once the add line is shown.
                if (e.PropertyName == nameof(RequirementsViewModel.IsAdding) && model.IsAdding)
                    Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("NewRequirementTitle")?.Focus());
            };
        };
    }
}
