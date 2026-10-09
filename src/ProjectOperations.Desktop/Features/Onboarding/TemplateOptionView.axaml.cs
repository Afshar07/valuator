using Avalonia.Controls;

namespace ProjectOperations.Desktop.Features.Onboarding;

public partial class TemplateOptionView : UserControl
{
    public TemplateOptionView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => { if (DataContext is TemplateOptionViewModel option) TemplateButton.Name = option.ControlName; };
    }
}
