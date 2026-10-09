using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Assistant;

/// <summary>A project the assistant can work on, offered while none is open. Choosing it opens the project on its Delegate tab.</summary>
internal sealed partial class AssistantProjectRowViewModel(Project project, AssistantServices services) : ViewModelBase
{
    public string Name => project.Name;
    public string Company => project.CompanyName;
    public bool HasCompany => !string.IsNullOrWhiteSpace(project.CompanyName);

    [RelayCommand]
    private Task OpenAsync() => services.Host.RunAsync(() => services.Navigator.GoToAsync(new ProjectRoute(project.Id, ProjectTab.Delegate)));
}
