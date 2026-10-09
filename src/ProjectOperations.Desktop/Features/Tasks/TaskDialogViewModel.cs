using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Tasks;

/// <summary>A requirement a task can follow up on, or "none".</summary>
internal sealed class RequirementOptionViewModel : ViewModelBase
{
    private readonly Func<string> _title;

    public RequirementOptionViewModel(LocalizedStrings strings, Guid? id, Func<string> title)
    {
        Id = id; _title = title;
        RefreshOnLanguageChange(strings);
    }

    public Guid? Id { get; }
    public string Title => _title();
}

/// <summary>New / edit task dialog: title, due date, status and the requirement it follows up on.</summary>
internal sealed partial class TaskDialogViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectTask _task;
    private readonly bool _isNew;
    private readonly ProjectScreenServices _services;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title;

    [ObservableProperty] private RequirementOptionViewModel _selectedRequirement;

    public TaskDialogViewModel(Project project, ProjectTask task, bool isNew, ProjectScreenServices services)
    {
        _project = project; _task = task; _isNew = isNew; _services = services; L = services.Strings;
        _title = task.Title;
        Date = isNew ? DateFieldViewModel.Ahead(L, 1, allowNone: true, services.Clock) : DateFieldViewModel.Existing(L, task.DueAt, allowNone: true, services.Clock);
        Status = new ChipGroupViewModel<ProjectTaskStatus>(L, Enum.GetValues<ProjectTaskStatus>().Select(item => (item, (Func<string>)(() => L.Enum(item)))),
            isNew ? ProjectTaskStatus.Todo : task.Status);
        Requirements = [new RequirementOptionViewModel(L, null, () => L["v3.noneL"]),
            .. project.Requirements.Select(requirement => new RequirementOptionViewModel(L, requirement.Id, () => L.Requirement(requirement)))];
        _selectedRequirement = Requirements.FirstOrDefault(option => option.Id == task.RequirementId) ?? Requirements[0];
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public DateFieldViewModel Date { get; }
    public ChipGroupViewModel<ProjectTaskStatus> Status { get; }
    public IReadOnlyList<RequirementOptionViewModel> Requirements { get; }

    public string Heading => L[_isNew ? "v3.newTaskT" : "v3.editTaskT"];
    public string SaveText => L[_isNew ? "v3.createTask" : "v3.saveB"];
    public bool CanDelete => !_isNew;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => _services.Host.RunAsync(async () =>
    {
        _task.Title = Title.Trim(); _task.DueAt = Date.Value; _task.Status = Status.Selected; _task.RequirementId = SelectedRequirement.Id;
        _task.UpdatedAt = _services.Clock.GetUtcNow();
        if (_isNew && !_project.Tasks.Contains(_task)) _project.Tasks.Add(_task);
        await _services.Projects.SaveAsync(_project);
        _services.Dialogs.Close();
        await _services.Host.RefreshProjectAsync();
    });

    [RelayCommand]
    private void Cancel() => _services.Dialogs.Close();

    [RelayCommand]
    private Task DeleteAsync() => _services.Host.RunAsync(async () =>
    {
        _project.Tasks.Remove(_task);
        await _services.Projects.SaveAsync(_project);
        _services.Dialogs.Close();
        await _services.Host.RefreshProjectAsync();
    });
}

/// <summary>New / edit milestone dialog: title, date and whether it has been reached.</summary>
internal sealed partial class MilestoneDialogViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly Milestone _milestone;
    private readonly bool _isNew;
    private readonly ProjectScreenServices _services;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title;

    [ObservableProperty] private bool _isReached;

    public MilestoneDialogViewModel(Project project, Milestone milestone, bool isNew, ProjectScreenServices services)
    {
        _project = project; _milestone = milestone; _isNew = isNew; _services = services; L = services.Strings;
        _title = milestone.Title; _isReached = milestone.IsComplete;
        Date = isNew ? DateFieldViewModel.Ahead(L, 7, allowNone: false, services.Clock) : DateFieldViewModel.Existing(L, milestone.DueAt, allowNone: false, services.Clock);
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public DateFieldViewModel Date { get; }

    public string Heading => L[_isNew ? "v3.newMsT" : "v3.editMsT"];
    public string SaveText => L[_isNew ? "v3.createMs" : "v3.saveB"];
    public bool CanDelete => !_isNew;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => _services.Host.RunAsync(async () =>
    {
        _milestone.Title = Title.Trim(); _milestone.DueAt = Date.Value; _milestone.IsComplete = IsReached;
        if (_isNew && !_project.Milestones.Contains(_milestone)) _project.Milestones.Add(_milestone);
        await _services.Projects.SaveAsync(_project);
        _services.Dialogs.Close();
        await _services.Host.RefreshProjectAsync();
    });

    [RelayCommand]
    private void Cancel() => _services.Dialogs.Close();

    [RelayCommand]
    private Task DeleteAsync() => _services.Host.RunAsync(async () =>
    {
        _project.Milestones.Remove(_milestone);
        await _services.Projects.SaveAsync(_project);
        _services.Dialogs.Close();
        await _services.Host.RefreshProjectAsync();
    });
}
