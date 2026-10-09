using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Agents;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Delegation;

/// <summary>
/// One delegated job: what was asked, how it ended and when. The row expands in place to the request, the result, the technical error and the
/// proposals. Reviewing proposals happens in the assistant, so each proposal has exactly one approval control.
/// </summary>
internal sealed partial class HistoryRowViewModel : ViewModelBase
{
    private readonly AgentJob _job;
    private readonly ProjectScreenServices _services;

    [ObservableProperty] private bool _isExpanded;

    public HistoryRowViewModel(AgentJob job, ProjectScreenServices services)
    {
        _job = job; _services = services; L = services.Strings;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string Request => _job.Prompt;
    public string StatusText => L.Enum(_job.Status);
    public PillKind StatusKind => _job.Status switch
    {
        AgentJobStatus.Completed => PillKind.Success,
        AgentJobStatus.Cancelled => PillKind.Neutral,
        AgentJobStatus.Failed => PillKind.Error,
        AgentJobStatus.Interrupted => PillKind.Warning,
        _ => PillKind.Ai
    };

    public string StatusIcon => _job.Status switch
    {
        AgentJobStatus.Completed => Icons.Check,
        AgentJobStatus.Cancelled => Icons.Stop,
        AgentJobStatus.Failed => Icons.X,
        AgentJobStatus.Interrupted => Icons.Plugs,
        _ => Icons.CircleNotch
    };

    public string StartedText => L.Due(_job.CreatedAt);
    public string AccessibleName => $"{_job.Prompt} · {StatusText} · {StartedText}";

    /// <summary>How the job ended in terms of what it left to review; a job without a final outcome never reads as success.</summary>
    public string OutcomeText
    {
        get
        {
            var pending = _job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending);
            return _job.Status switch
            {
                AgentJobStatus.Completed when _job.Proposals.Count == 0 => L["history.outcome.noProposals"],
                AgentJobStatus.Completed when pending > 0 => L.Format("history.outcome.pending", _job.Proposals.Count, pending),
                AgentJobStatus.Completed => L.Format("history.outcome.reviewed", _job.Proposals.Count(p => p.ReviewStatus == ProposalReviewStatus.Approved),
                    _job.Proposals.Count(p => p.ReviewStatus == ProposalReviewStatus.Rejected)),
                AgentJobStatus.Cancelled => L["history.outcome.cancelled"],
                AgentJobStatus.Failed => L["history.outcome.failed"],
                AgentJobStatus.Interrupted => L["history.outcome.interrupted"],
                _ => L["history.noOutcome"]
            };
        }
    }

    // ---- the expanded row ----------------------------------------------------------------------------------------------------------

    public string FinishedText => _job.FinishedAt.HasValue ? L.Format("history.finished", L.Due(_job.FinishedAt)) : L["history.noOutcome"];
    public bool ShowInterrupted => _job.Status == AgentJobStatus.Interrupted;
    public bool HasResult => !string.IsNullOrWhiteSpace(_job.ResultText);
    public string ResultText => _job.ResultText;
    public bool HasError => !string.IsNullOrWhiteSpace(_job.Error);
    public string ErrorText => _job.Error;
    public IReadOnlyList<string> ProposalLines => _job.Proposals.Select(proposal => $"• {proposal.Title} · {L.Due(proposal.DueAt)} · {L.Enum(proposal.ReviewStatus)}").ToList();
    /// <summary>Only a completed job with undecided proposals can be reviewed.</summary>
    public bool CanReview => _job.Status == AgentJobStatus.Completed && _job.Proposals.Any(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending);

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private Task ReviewAsync() => _services.Host.RunAsync(() => _services.Host.ReviewInAssistantAsync(_job.Id));
}
