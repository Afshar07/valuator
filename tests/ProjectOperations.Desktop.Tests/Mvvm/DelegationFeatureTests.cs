using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.Delegation;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The Delegate tab as a plain view-model: the assistant's status, and the project's job history with one expandable row per job.</summary>
public sealed class DelegationFeatureTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 8, 9, 30, 0, TimeSpan.Zero);

    private static AgentJob Job(string prompt, AgentJobStatus status, int dayOffset = 0, string result = "", string error = "", bool finished = true, params TaskProposal[] proposals) => new()
    {
        Prompt = prompt, Status = status, CreatedAt = Started.AddDays(dayOffset), FinishedAt = finished ? Started.AddDays(dayOffset).AddMinutes(4) : null,
        ResultText = result, Error = error, Proposals = [.. proposals]
    };

    private static TaskProposal Proposal(string title, ProposalReviewStatus status = ProposalReviewStatus.Pending) =>
        new() { Title = title, DueAt = Started.AddDays(5), ReviewStatus = status };

    private static async Task<(ProjectScenario Scenario, DelegationViewModel Delegation)> OpenAsync(IReadOnlyList<AgentJob> jobs, bool configured = true)
    {
        var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        return (scenario, new DelegationViewModel(project, jobs, configured, scenario.Services));
    }

    [Fact]
    public async Task The_intro_card_reports_whether_the_assistant_is_ready_and_opens_it()
    {
        var (scenario, ready) = await OpenAsync([]);
        using (scenario)
        {
            Assert.Equal("Ready", ready.StatusText);
            Assert.Equal("Accent", ready.StatusToken);

            ready.OpenAssistantCommand.Execute(null);

            Assert.Equal(1, scenario.Host.AssistantOpens);
        }

        var (offScenario, off) = await OpenAsync([], configured: false);
        using (offScenario)
        {
            Assert.Equal("Not configured", off.StatusText);
            Assert.Equal("TextTertiary", off.StatusToken);
        }
    }

    [Fact]
    public async Task With_no_jobs_the_history_says_so()
    {
        var (scenario, delegation) = await OpenAsync([]);
        using (scenario)
        {
            Assert.True(delegation.IsEmpty);
            Assert.Empty(delegation.Rows);
        }
    }

    [Fact]
    public async Task The_history_lists_the_newest_job_first_with_status_start_and_a_truthful_outcome()
    {
        var jobs = new[]
        {
            Job("Oldest", AgentJobStatus.Completed, -3),
            Job("Newest", AgentJobStatus.Completed, 0, proposals: [Proposal("a"), Proposal("b", ProposalReviewStatus.Approved)]),
            Job("Middle", AgentJobStatus.Failed, -1, error: "boom")
        };
        var (scenario, delegation) = await OpenAsync(jobs);
        using (scenario)
        {
            var L = scenario.L;
            Assert.False(delegation.IsEmpty);
            Assert.Equal(["Newest", "Middle", "Oldest"], delegation.Rows.Select(row => row.Request));
            var newest = delegation.Rows[0];
            Assert.Equal("Completed", newest.StatusText);
            Assert.Equal(L.Due(Started), newest.StartedText);
            Assert.Equal("2 tasks proposed · 1 pending review", newest.OutcomeText);
            Assert.Equal($"Newest · Completed · {L.Due(Started)}", newest.AccessibleName);
            Assert.Equal("Failed · see details", delegation.Rows[1].OutcomeText);
            Assert.Equal("No task proposals", delegation.Rows[2].OutcomeText);
        }
    }

    [Theory]
    [InlineData(AgentJobStatus.Completed, PillKind.Success, "Completed", "No task proposals")]
    [InlineData(AgentJobStatus.Cancelled, PillKind.Neutral, "Cancelled", "Stopped on request · nothing was added")]
    [InlineData(AgentJobStatus.Failed, PillKind.Error, "Failed", "Failed · see details")]
    [InlineData(AgentJobStatus.Interrupted, PillKind.Warning, "Interrupted", "App closed before finishing · success not assumed")]
    [InlineData(AgentJobStatus.Running, PillKind.Ai, "Running", "No final outcome recorded yet")]
    public async Task Each_status_has_its_own_pill_and_outcome_and_none_is_shown_as_success_by_default(AgentJobStatus status, PillKind kind, string text, string outcome)
    {
        var (scenario, delegation) = await OpenAsync([Job("Request", status)]);
        using (scenario)
        {
            var row = delegation.Rows.Single();
            Assert.Equal(kind, row.StatusKind);
            Assert.Equal(text, row.StatusText);
            Assert.Equal(outcome, row.OutcomeText);
            Assert.False(string.IsNullOrEmpty(row.StatusIcon));
        }
    }

    [Fact]
    public async Task A_completed_job_with_every_proposal_decided_reports_how_many_were_approved_and_rejected()
    {
        var job = Job("Reviewed", AgentJobStatus.Completed, proposals: [Proposal("a", ProposalReviewStatus.Approved), Proposal("b", ProposalReviewStatus.Approved), Proposal("c", ProposalReviewStatus.Rejected)]);
        var (scenario, delegation) = await OpenAsync([job]);
        using (scenario)
        {
            var row = delegation.Rows.Single();
            Assert.Equal("2 approved · 1 rejected", row.OutcomeText);
            Assert.False(row.CanReview);
        }
    }

    [Fact]
    public async Task A_row_expands_in_place_to_the_request_result_error_and_proposals_of_its_job()
    {
        var job = Job("Find gaps", AgentJobStatus.Completed, result: "Two gaps found", proposals: [Proposal("Request deck")]);
        var (scenario, delegation) = await OpenAsync([job]);
        using (scenario)
        {
            var row = delegation.Rows.Single();
            Assert.False(row.IsExpanded);

            row.ToggleCommand.Execute(null);

            Assert.True(row.IsExpanded);
            Assert.Equal($"Finished: {scenario.L.Due(job.FinishedAt)}", row.FinishedText);
            Assert.True(row.HasResult);
            Assert.Equal("Two gaps found", row.ResultText);
            Assert.False(row.HasError);
            Assert.False(row.ShowInterrupted);
            Assert.Equal([$"• Request deck · {scenario.L.Due(Started.AddDays(5))} · Pending"], row.ProposalLines);
            Assert.True(row.CanReview);

            row.ToggleCommand.Execute(null);
            Assert.False(row.IsExpanded);
        }
    }

    [Fact]
    public async Task A_failed_job_keeps_its_error_as_technical_details_and_an_interrupted_one_says_success_is_not_assumed()
    {
        var jobs = new[]
        {
            Job("Failing", AgentJobStatus.Failed, error: "Synthetic transport unavailable"),
            Job("Cut off", AgentJobStatus.Interrupted, -1, finished: false)
        };
        var (scenario, delegation) = await OpenAsync(jobs);
        using (scenario)
        {
            var failed = delegation.Rows[0];
            Assert.True(failed.HasError);
            Assert.Equal("Synthetic transport unavailable", failed.ErrorText);
            Assert.False(failed.HasResult);
            Assert.False(failed.ShowInterrupted);

            var interrupted = delegation.Rows[1];
            Assert.True(interrupted.ShowInterrupted);
            Assert.Equal("No final outcome recorded yet", interrupted.FinishedText);
        }
    }

    [Fact]
    public async Task Only_a_completed_job_with_pending_proposals_offers_review_and_it_opens_the_assistant_on_that_job()
    {
        var pending = Job("Pending", AgentJobStatus.Completed, 0, proposals: [Proposal("a")]);
        var failed = Job("Failed with leftovers", AgentJobStatus.Failed, -1, proposals: [Proposal("b")]);
        var (scenario, delegation) = await OpenAsync([pending, failed]);
        using (scenario)
        {
            Assert.True(delegation.Rows[0].CanReview);
            Assert.False(delegation.Rows[1].CanReview);

            await delegation.Rows[0].ReviewCommand.ExecuteAsync(null);

            Assert.Equal([pending.Id], scenario.Host.Reviewed);
            Assert.Equal(1, scenario.Host.Runs);
        }
    }

    [Fact]
    public async Task The_history_follows_the_language_in_place()
    {
        var (scenario, delegation) = await OpenAsync([Job("Request", AgentJobStatus.Cancelled)]);
        using (scenario)
        {
            var row = delegation.Rows.Single();
            var changes = new List<string?>();
            row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            var english = row.StatusText;

            scenario.Locale.SetLanguage("fa");

            Assert.Contains(string.Empty, changes);
            Assert.NotEqual(english, row.StatusText);
            Assert.Equal(scenario.L["history.outcome.cancelled"], row.OutcomeText);
            Assert.Equal("Request", row.Request);
        }
    }
}
