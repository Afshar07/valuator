using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Features.Assistant;

/// <summary>
/// The tray in which the agent's task proposals are reviewed, over the project's committed tasks. A proposal is only a suggestion: nothing becomes
/// a task until it is selected and approved here, and a decided proposal cannot be decided again.
/// </summary>
internal sealed partial class ProposalReviewViewModel : ViewModelBase
{
    private readonly AgentJob _job;
    private readonly AssistantServices _services;
    private readonly int _pending;

    public ProposalReviewViewModel(AgentJob job, Project project, AssistantServices services)
    {
        _job = job; _services = services; L = services.Strings;
        _pending = job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending);
        Items = job.Proposals.Select(proposal => new ProposalItemViewModel(proposal, job.Status == AgentJobStatus.Completed, L)).ToList();
        var now = services.Clock.GetUtcNow();
        CommittedTasks = project.Tasks.Where(TaskUrgency.IsActive).Select(task => new CommittedTaskViewModel(task, now, L)).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public IReadOnlyList<ProposalItemViewModel> Items { get; }
    public IReadOnlyList<CommittedTaskViewModel> CommittedTasks { get; }
    public bool HasNoCommittedTasks => CommittedTasks.Count == 0;
    public string TrayTitle => L.Format("proposal.trayTitle", _pending);
    /// <summary>True while any proposal of the job is still undecided.</summary>
    public bool CanReview => _pending > 0;

    [RelayCommand]
    private Task ApproveAsync() => DecideAsync((job, ids) => _services.Workspace.Agents.ApproveAsync(job, ids));

    [RelayCommand]
    private Task RejectAsync() => DecideAsync((job, ids) => _services.Workspace.Agents.RejectAsync(job, ids));

    /// <summary>Applies the decision to the selected proposals, then reloads the project so the tray and the committed tasks show what was stored.</summary>
    private Task DecideAsync(Func<AgentJob, IEnumerable<Guid>, Task> decide) => _services.Host.RunAsync(async () =>
    {
        await decide(_job, Items.Where(item => item.IsSelected).Select(item => item.Id).ToList());
        await _services.Host.RefreshProjectAsync();
    });
}

/// <summary>One proposed task with its selection box. Only a pending proposal of a completed job can be selected.</summary>
internal sealed partial class ProposalItemViewModel : ViewModelBase
{
    private readonly TaskProposal _proposal;
    private readonly bool _jobCompleted;
    private readonly LocalizedStrings _strings;

    [ObservableProperty] private bool _isSelected;

    public ProposalItemViewModel(TaskProposal proposal, bool jobCompleted, LocalizedStrings strings)
    {
        _proposal = proposal; _jobCompleted = jobCompleted; _strings = strings;
        RefreshOnLanguageChange(strings);
    }

    public Guid Id => _proposal.Id;
    public bool IsEnabled => _jobCompleted && _proposal.ReviewStatus == ProposalReviewStatus.Pending;

    /// <summary>Title, due date and review status, with the agent's description below.</summary>
    public string Text => $"{_proposal.Title} · {_strings["proposal.due"]} {_strings.Due(_proposal.DueAt)} · {_strings.Enum(_proposal.ReviewStatus)}"
        + (string.IsNullOrWhiteSpace(_proposal.Description) ? "" : "\n" + _proposal.Description);
}

/// <summary>One of the project's own open tasks, listed under the proposals so what is committed is never confused with what is only proposed.</summary>
internal sealed class CommittedTaskViewModel(ProjectTask task, DateTimeOffset now, LocalizedStrings strings) : ViewModelBase
{
    public string Title => task.Title;
    public string DueText => task.DueAt is null ? "" : strings.ShortDate(task.DueAt);
    public bool IsLate => task.DueAt < now;
}
