using Avalonia.Controls;

namespace ProjectOperations.Desktop.Features.Tasks;

public partial class TasksView : UserControl
{
    public TasksView()
    {
        InitializeComponent();
        Columns.Weights = [2, 1];
    }
}
