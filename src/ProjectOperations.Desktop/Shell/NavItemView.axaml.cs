using Avalonia.Controls;

namespace ProjectOperations.Desktop.Shell;

public partial class NavItemView : UserControl
{
    public NavItemView()
    {
        InitializeComponent();
        // The entry is found by its name (tests and assistive technology), which the view-model supplies.
        DataContextChanged += (_, _) => { if (DataContext is NavItemViewModel item) NavButton.Name = item.ControlName; };
    }
}
