using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Tasks;

/// <summary>
/// Tasks &amp; dates tab: open tasks grouped by urgency, milestones, and a pointer to proposals still awaiting review.
/// Editing happens in dialogs. Urgency comes from <see cref="TaskUrgency"/>; this view-model only presents it.
/// </summary>
internal sealed partial class TasksViewModel : ViewModelBase
{
    private static readonly IReadOnlyDictionary<TaskBucket, string> GroupTitles = new Dictionary<TaskBucket, string>
    {
        [TaskBucket.Overdue] = "v3.grpOverdue",
        [TaskBucket.ThisWeek] = "v3.grpWeek",
        [TaskBucket.Later] = "v3.grpLater",
        [TaskBucket.NoDate] = "v3.grpNoDate",
        [TaskBucket.Closed] = "tasks.closed"
    };

    private readonly Project _project;
    private readonly ProjectScreenServices _services;
    private readonly string? _reviewJobId;
    private readonly int _waitingProposals;

    public TasksViewModel(Project project, IReadOnlyList<AgentJob> jobs, ProjectScreenServices services)
    {
        _project = project; _services = services; L = services.Strings;
        var pending = jobs.Where(job => job.Status == AgentJobStatus.Completed && job.Proposals.Any(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending))
            .OrderByDescending(job => job.CreatedAt).ToList();
        _waitingProposals = pending.Sum(job => job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending));
        _reviewJobId = pending.FirstOrDefault()?.Id;

        var now = services.Clock.GetUtcNow();
        Groups = TaskUrgency.Group(project.Tasks, now)
            .Select(group => new TaskGroupViewModel(group, services.Strings, group.Tasks.Select(task => new TaskRowViewModel(project, task, services)).ToList()))
            .ToList();
        Milestones = project.Milestones.OrderBy(milestone => milestone.DueAt ?? DateTimeOffset.MaxValue)
            .Select(milestone => new MilestoneRowViewModel(project, milestone, services)).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public bool HasPendingProposals => _waitingProposals > 0;
    public string PendingProposalsText => L.Format("tasks.pendingProposals", _waitingProposals);

    public IReadOnlyList<TaskGroupViewModel> Groups { get; }
    public bool IsEmpty => _project.Tasks.Count == 0;
    public IReadOnlyList<MilestoneRowViewModel> Milestones { get; }
    public bool HasMilestones => Milestones.Count > 0;

    [RelayCommand]
    private Task ReviewAsync() => _reviewJobId is { } id ? _services.Host.RunAsync(() => _services.Host.ReviewInAssistantAsync(id)) : Task.CompletedTask;

    [RelayCommand]
    private void NewTask() => _services.Dialogs.Show(new TaskDialogViewModel(_project, new ProjectTask { ProjectId = _project.Id }, isNew: true, _services));

    [RelayCommand]
    private void NewMilestone() => _services.Dialogs.Show(new MilestoneDialogViewModel(_project, new Milestone { ProjectId = _project.Id }, isNew: true, _services));

    internal static string GroupTitleKey(TaskBucket bucket) => GroupTitles[bucket];
}

/// <summary>A muted band over the tasks of one urgency bucket.</summary>
internal sealed class TaskGroupViewModel : ViewModelBase
{
    private readonly TaskGroup _group;
    private readonly LocalizedStrings _strings;

    public TaskGroupViewModel(TaskGroup group, LocalizedStrings strings, IReadOnlyList<TaskRowViewModel> rows)
    {
        _group = group; _strings = strings; Rows = rows;
        RefreshOnLanguageChange(strings);
    }

    public IReadOnlyList<TaskRowViewModel> Rows { get; }
    public string Title => _strings[TasksViewModel.GroupTitleKey(_group.Bucket)];
    public string CountText => _strings.Number(_group.Tasks.Count);
    public bool IsOverdue => _group.Bucket == TaskBucket.Overdue;
    public bool IsClosed => _group.Bucket == TaskBucket.Closed;
}
