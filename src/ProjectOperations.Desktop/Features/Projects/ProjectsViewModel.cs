using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Projects;

/// <summary>
/// All projects as a table: stage, readiness (Complete only), next open deadline, owner and status, with a text filter over name,
/// company and owner. Opening and deleting happen through the rows; the figures come from <see cref="ProjectSummaries"/>.
/// </summary>
internal sealed partial class ProjectsViewModel : ViewModelBase
{
    private readonly PageServices _services;

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private IReadOnlyList<ProjectRowViewModel> _visibleRows;

    public ProjectsViewModel(IReadOnlyList<Project> projects, PageServices services)
    {
        _services = services; L = services.Strings;
        var now = services.Clock.GetLocalNow();
        AllRows = projects.Select(project => new ProjectRowViewModel(project, now, services)).ToList();
        _visibleRows = ApplyFilter();
        RefreshOnLanguageChange(L);
    }

    public static async Task<ProjectsViewModel> LoadAsync(PageServices services) => new(await services.Projects.ListAsync(), services);

    public LocalizedStrings L { get; }
    public IReadOnlyList<ProjectRowViewModel> AllRows { get; }
    public bool HasRows => AllRows.Count > 0;
    public bool IsEmpty => AllRows.Count == 0;

    partial void OnFilterChanged(string value) => VisibleRows = ApplyFilter();

    private IReadOnlyList<ProjectRowViewModel> ApplyFilter()
    {
        var query = Filter.Trim();
        var rows = AllRows.Where(row => row.Matches(query)).ToList();
        for (var index = 0; index < rows.Count; index++) rows[index].IsFirst = index == 0;
        return rows;
    }

    [RelayCommand]
    private Task NewProjectAsync() => _services.Host.RunAsync(() => { _services.Host.ShowWizard(); return Task.CompletedTask; });
}

/// <summary>One project in the table. The row opens the project; the trash icon asks for confirmation before deleting it.</summary>
internal sealed partial class ProjectRowViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectSummary _summary;
    private readonly ScheduleItem? _next;
    private readonly PageServices _services;

    [ObservableProperty] private bool _isFirst;

    public ProjectRowViewModel(Project project, DateTimeOffset now, PageServices services)
    {
        _project = project; _services = services; L = services.Strings;
        _summary = ProjectSummaries.Summarize(project, now);
        _next = ProjectSummaries.NextDeadline(project, now);
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string Name => _project.Name;
    public string Company => _project.CompanyName;
    public bool HasCompany => !string.IsNullOrWhiteSpace(_project.CompanyName);
    public ProjectStage Stage => _project.Stage;

    /// <summary>Share of requirements that are Complete, 0 to 100.</summary>
    public double ReadinessPercent => _summary.CompletionPercentage;
    public string ReadinessText => $"{L.Number(_summary.CompleteRequirements)}/{L.Number(_summary.TotalRequirements)}";

    public string NextText => _next is null ? L["date.notSet"] : L.ShortDate(_next.DueAt);
    public bool IsNextOverdue => _next?.IsOverdue == true;

    public string OwnerText => string.IsNullOrWhiteSpace(_project.Owner) ? "—" : _project.Owner;
    public string StatusText => L.Enum(_project.Status);
    public PillKind StatusKind => _project.Status switch
    {
        ProjectStatus.Active => PillKind.Success,
        ProjectStatus.Completed => PillKind.Accent,
        _ => PillKind.Neutral
    };

    /// <summary>Name, company, stage, status and owner in one phrase: the row's accessible name.</summary>
    public string AccessibleName => $"{_project.Name} · {_project.CompanyName} · {_project.Stage.Title} · {StatusText} · {_project.Owner}";

    public bool Matches(string query) => query.Length == 0
        || _project.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || _project.CompanyName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || _project.Owner.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    [RelayCommand]
    private Task OpenAsync() => _services.GoAsync(new ProjectRoute(_project.Id));

    [RelayCommand]
    private void Delete() => _services.Dialogs.Show(new DeleteProjectDialogViewModel(_project, _services));
}

/// <summary>Asks for explicit confirmation before a project and everything inside it is permanently deleted. Linked files are never touched.</summary>
internal sealed partial class DeleteProjectDialogViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly PageServices _services;

    public DeleteProjectDialogViewModel(Project project, PageServices services)
    {
        _project = project; _services = services; L = services.Strings;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string Heading => L["v3.delProjectT"];
    public string Body => L.Format("v3.delProjectBody", _project.Name);

    [RelayCommand]
    private void Cancel() => _services.Dialogs.Close();

    [RelayCommand]
    private Task ConfirmAsync() => _services.Host.RunAsync(async () =>
    {
        await _services.Projects.DeleteAsync(_project.Id);
        _services.Dialogs.Close();
        await _services.Navigator.GoToAsync(new PageRoute(AppPage.Projects));
    });
}
