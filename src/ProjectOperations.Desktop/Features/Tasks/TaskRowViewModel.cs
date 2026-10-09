using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Tasks;

/// <summary>One task in the list: a checkbox-style toggle that completes or reopens it, and the rest of the row opens the edit dialog.</summary>
internal sealed partial class TaskRowViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectTask _task;
    private readonly ProjectScreenServices _services;
    private readonly DateTimeOffset _now;

    public TaskRowViewModel(Project project, ProjectTask task, ProjectScreenServices services)
    {
        _project = project; _task = task; _services = services; L = services.Strings;
        _now = services.Clock.GetUtcNow();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string Title => _task.Title;
    public ProjectTaskStatus Status => _task.Status;
    public bool IsActive => TaskUrgency.IsActive(_task);
    public bool IsOverdue => TaskUrgency.BucketOf(_task, _now) == TaskBucket.Overdue;
    public bool IsInProgress => _task.Status == ProjectTaskStatus.InProgress;
    public bool IsCancelled => _task.Status == ProjectTaskStatus.Cancelled;

    public string StatusText => L.Enum(_task.Status);
    public string ToggleLabel => L[IsActive ? "task.complete" : "v3.reopen"];
    /// <summary>The title, status and due date in one phrase: the row's accessible name.</summary>
    public string AccessibleName => $"{_task.Title} · {StatusText} · {L.Due(_task.DueAt)}";

    /// <summary>The requirement this task follows up on, or null.</summary>
    public string? RequirementTitle => _task.RequirementId is { } id && _project.Requirements.FirstOrDefault(item => item.Id == id) is { } requirement ? L.Requirement(requirement) : null;
    public bool HasRequirement => RequirementTitle is not null;

    public string DueText => _task.DueAt is null ? "—" : L.ShortDate(_task.DueAt);
    /// <summary>"in 3 days" / "2 days late" for open work; the status for closed work.</summary>
    public string RelativeText => IsActive ? L.Relative(_task.DueAt, true, _services.Clock.GetLocalNow().Date) : StatusText;

    [RelayCommand]
    private Task ToggleAsync() => _services.Host.RunAsync(async () =>
    {
        _task.Status = IsActive ? ProjectTaskStatus.Done : ProjectTaskStatus.Todo;
        _task.UpdatedAt = _services.Clock.GetUtcNow();
        await _services.Projects.SaveAsync(_project);
        await _services.Host.RefreshProjectAsync();
    });

    [RelayCommand]
    private void Edit() => _services.Dialogs.Show(new TaskDialogViewModel(_project, _task, isNew: false, _services));
}

/// <summary>One milestone: flag, title, date with how far away it is; the row opens the edit dialog.</summary>
internal sealed partial class MilestoneRowViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly Milestone _milestone;
    private readonly ProjectScreenServices _services;

    public MilestoneRowViewModel(Project project, Milestone milestone, ProjectScreenServices services)
    {
        _project = project; _milestone = milestone; _services = services; L = services.Strings;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string Title => _milestone.Title;
    public bool IsComplete => _milestone.IsComplete;
    public bool HasDate => _milestone.DueAt is not null;
    /// <summary>The short date, or "No date".</summary>
    public string DateText => L.ShortDate(_milestone.DueAt);
    /// <summary>"Reached" once complete, otherwise how far away the date is.</summary>
    public string RelativeText => _milestone.IsComplete ? L["v3.reached"] : L.Relative(_milestone.DueAt, false, _services.Clock.GetLocalNow().Date);
    public string AccessibleName => $"{_milestone.Title} · {L[_milestone.IsComplete ? "milestone.complete" : "milestone.open"]} · {L.Due(_milestone.DueAt)}";

    [RelayCommand]
    private void Edit() => _services.Dialogs.Show(new MilestoneDialogViewModel(_project, _milestone, isNew: false, _services));
}
