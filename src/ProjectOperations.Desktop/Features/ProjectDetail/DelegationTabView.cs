using Avalonia.Controls;

namespace ProjectOperations.Desktop.Features.ProjectDetail;

/// <summary>Hosts the code-built <see cref="DelegationView"/> inside the XAML project tabs (phase 5 replaces it).</summary>
public sealed class DelegationTabView : ContentControl
{
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Content = DataContext is DelegationTabViewModel model ? new DelegationView(model.Context, model.Project, model.Jobs) : null;
    }
}
