using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.Overview;
using ProjectOperations.Desktop.Features.Requirements;
using ProjectOperations.Desktop.Features.Tasks;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.ProjectDetail;

/// <summary>
/// The open project: a header (name, stage, readiness, edit, assistant) over the Overview, Requirements, Tasks and Delegate tabs.
/// The Delegate tab is still code-built; <paramref name="delegation"/> is its host until the assistant moves to MVVM.
/// </summary>
internal sealed partial class ProjectDetailViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectScreenServices _services;
    private readonly ProjectSummary _summary;

    [ObservableProperty] private int _selectedIndex;
    [ObservableProperty] private bool _isLocked;

    public ProjectDetailViewModel(Project project, IReadOnlyList<AgentJob> jobs, ProjectTab tab, ProjectScreenServices services, ViewModelBase delegation)
    {
        _project = project; _services = services; L = services.Strings;
        _summary = ProjectSummaries.Summarize(project, services.Clock.GetUtcNow());
        Overview = new OverviewViewModel(project, services);
        Requirements = new RequirementsViewModel(project, services);
        Tasks = new TasksViewModel(project, jobs, services);
        Delegation = delegation;
        _selectedIndex = (int)tab;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public OverviewViewModel Overview { get; }
    public RequirementsViewModel Requirements { get; }
    public TasksViewModel Tasks { get; }
    public ViewModelBase Delegation { get; }

    public static async Task<ProjectDetailViewModel> LoadAsync(Project project, ProjectTab tab, ProjectScreenServices services, AgentService agents,
        Func<IReadOnlyList<AgentJob>, ViewModelBase> delegation)
    {
        var jobs = await agents.HistoryAsync(project.Id);
        return new ProjectDetailViewModel(project, jobs, tab, services, delegation(jobs));
    }

    public string Title => _project.Name;
    public string Subtitle => string.Join(" · ", new[] { _project.CompanyName, _project.Owner }.Where(part => !string.IsNullOrWhiteSpace(part)));
    public ProjectStage Stage => _project.Stage;
    public bool ShowStatus => _project.Status != ProjectStatus.Active;
    public string StatusText => L.Enum(_project.Status);

    public string BackText => L["navigation.projects"];
    public string BackIcon => L.IsRightToLeft ? Icons.ArrowRight : Icons.ArrowLeft;

    public string ReadinessText => $"{L.Number(_summary.CompleteRequirements)}/{L.Number(_summary.TotalRequirements)}";
    public string ReadinessTip => L.Format("overview.readiness", _summary.CompleteRequirements, _summary.TotalRequirements, _summary.CompletionPercentage);
    public double ReadinessPercent => _summary.CompletionPercentage;

    partial void OnSelectedIndexChanged(int value)
    {
        if (value is < 0 or > (int)ProjectTab.Delegate) return;
        var tab = (ProjectTab)value;
        _services.Host.SelectTab(tab);
        if (tab == ProjectTab.Delegate) _services.Host.OpenAssistant();
    }

    [RelayCommand]
    private Task BackAsync() => _services.Host.RunAsync(() => _services.Navigator.GoToAsync(new PageRoute(AppPage.Projects)));

    [RelayCommand]
    private Task EditAsync() => _services.Host.RunAsync(async () =>
    {
        var stages = await _services.Projects.ListStagesAsync(_project.TemplateId);
        _services.Dialogs.Show(new EditProjectDialogViewModel(_project, stages, _services));
    });

    [RelayCommand]
    private void ToggleAssistant() => _services.Host.ToggleAssistant();
}
