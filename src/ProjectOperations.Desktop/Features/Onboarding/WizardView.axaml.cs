using Avalonia.Controls;
using Avalonia.Threading;

namespace ProjectOperations.Desktop.Features.Onboarding;

public partial class WizardView : UserControl
{
    public WizardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not WizardViewModel wizard) return;
            FocusName();
            wizard.PropertyChanged += (_, e) =>
            {
                // Coming back to the first step puts the cursor in the name again.
                if (e.PropertyName == nameof(WizardViewModel.IsNameStep) && wizard.IsNameStep) FocusName();
            };
        };
        AttachedToVisualTree += (_, _) => FocusName();
    }

    private void FocusName() => Dispatcher.UIThread.Post(() => { if (DataContext is WizardViewModel { IsNameStep: true }) WizardName.Focus(); });
}
