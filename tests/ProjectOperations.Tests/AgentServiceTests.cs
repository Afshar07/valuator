using System.Text.Json;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Infrastructure.Persistence;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class AgentServiceTests
{
    [Theory]
    [InlineData(AgentResponseLanguage.English)]
    [InlineData(AgentResponseLanguage.Persian)]
    public async Task ResponseLanguageOnlyChangesInstructionsNotTheConsentedContextOrPrompt(AgentResponseLanguage language)
    {
        await using var fixture = await Fixture.CreateAsync();
        var project = await fixture.Projects.CreateAsync("Atlas", "Atlas", default, default, "Owner", "یادداشت");
        AgentRequest? sent = null;
        var service = new AgentService(fixture.Projects, new TestRuntime((request, _, _) =>
        {
            sent = request;
            return Task.FromResult(new AgentResult());
        }), new MemoryJobs());
        var history = new List<AgentJob>();
        var consentedContext = AgentService.BuildContext(project, history);
        var job = await service.RunAsync(project, "Prepare brief", new NoProgress(), CancellationToken.None,
            history, language);
        Assert.Equal(AgentJobStatus.Completed, job.Status);
        Assert.NotNull(sent);
        Assert.Equal(AgentPrompts.BuildOutputInstructions(language), sent.SystemInstructions);
        Assert.Equal(consentedContext, sent.Context);
        Assert.Equal(consentedContext, job.ContextSnapshot);
        Assert.Equal("Prepare brief", sent.Prompt);
    }

    [Fact]
    public void PreviousResultsAreBoundedUnverifiedAndScopedToTheSelectedProject()
    {
        var project = new Project { Name = "Atlas" };
        var history = Enumerable.Range(0, 5).Select(index => new AgentJob
        {
            ProjectId = project.Id,
            Status = AgentJobStatus.Completed,
            CreatedAt = DateTimeOffset.UnixEpoch.AddDays(index),
            ResultText = new string('x', 15_000)
        }).ToList();
        history.Add(new AgentJob { ProjectId = Guid.NewGuid(), Status = AgentJobStatus.Completed, ResultText = "OTHER_PROJECT_SECRET" });
        history.Add(new AgentJob { ProjectId = project.Id, Status = AgentJobStatus.Failed, ResultText = "FAILED_UNCONFIRMED_RESULT" });
        var text = AgentService.BuildContext(project, history);
        using var context = JsonDocument.Parse(text);
        var results = context.RootElement.GetProperty("RecentResults").EnumerateArray().ToList();
        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.Equal(12_000, result.GetProperty("ResultExcerpt").GetString()!.Length));
        Assert.Equal(history[4].Id, results[0].GetProperty("JobId").GetString());
        Assert.Contains("unverified historical", context.RootElement.GetProperty("PreviousResultCaveat").GetString());
        Assert.DoesNotContain("OTHER_PROJECT_SECRET", text);
        Assert.DoesNotContain("FAILED_UNCONFIRMED_RESULT", text);
    }

    [Fact]
    public async Task ContextContainsOnlySelectedProjectAndNoAutomaticTasksAreCommitted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var other = await fixture.Projects.CreateAsync("Private other project", "Other company", default, default, "", "OTHER_SECRET");
        var selected = await fixture.Projects.CreateAsync("Atlas", "Atlas company", default, default, "Owner", "ATLAS_NOTE");
        AgentRequest? sent = null;
        var runtime = new TestRuntime((request, _, _) =>
        {
            sent = request;
            return Task.FromResult(new AgentResult { Text = "Review forecast", Proposals = [new() { Title = "Request forecast" }] });
        });
        var jobs = new MemoryJobs(fixture.Projects);
        var service = new AgentService(fixture.Projects, runtime, jobs);
        var job = await service.RunAsync(selected, "Summarize", new NoProgress(), CancellationToken.None);
        Assert.Equal(AgentJobStatus.Completed, job.Status);
        Assert.NotNull(sent);
        Assert.Equal(AgentPrompts.BuildOutputInstructions(AgentResponseLanguage.English), sent.SystemInstructions);
        Assert.Contains("ATLAS_NOTE", sent.Context);
        Assert.DoesNotContain("OTHER_SECRET", sent.Context);
        Assert.Equal(selected.Id, sent.ProjectId);
        Assert.Empty((await fixture.Projects.GetAsync(selected.Id))!.Tasks);
        Assert.Empty((await fixture.Projects.GetAsync(other.Id))!.Tasks);
        Assert.Single((await service.HistoryAsync(selected.Id)).Single().Proposals);
    }

    [Fact]
    public async Task ApprovalReloadsFreshProjectAndRetriesDoNotDuplicateTasks()
    {
        await using var fixture = await Fixture.CreateAsync();
        var project = await fixture.Projects.CreateAsync("Atlas", "Atlas", default, default, "Before", "");
        var proposal = new TaskProposal { Title = "Request forecast" };
        var jobs = new MemoryJobs(fixture.Projects);
        var service = new AgentService(fixture.Projects,
            new TestRuntime((_, _, _) => Task.FromResult(new AgentResult { Proposals = [proposal] })), jobs);
        var job = await service.RunAsync(project, "Actions", new NoProgress(), CancellationToken.None);
        var edited = (await fixture.Projects.GetAsync(project.Id))!;
        edited.Owner = "Updated after agent started";
        await fixture.Projects.SaveAsync(edited);
        await service.ApproveAsync(job, [proposal.Id]);
        var approvedRevision = (await fixture.Projects.GetAsync(project.Id))!.Revision;
        await service.ApproveAsync(job, [proposal.Id]);
        var saved = (await fixture.Projects.GetAsync(project.Id))!;
        Assert.Equal(approvedRevision, saved.Revision);
        Assert.Equal("Updated after agent started", saved.Owner);
        Assert.Equal(proposal.Id, Assert.Single(saved.Tasks).Id);
        Assert.Equal(ProposalReviewStatus.Approved, job.Proposals.Single().ReviewStatus);
    }

    [Fact]
    public async Task RejectionDoesNotCreateTasks()
    {
        await using var fixture = await Fixture.CreateAsync();
        var project = await fixture.Projects.CreateAsync("Atlas", "Atlas", default, default, "", "");
        var proposal = new TaskProposal { Title = "Not approved" };
        var service = new AgentService(fixture.Projects,
            new TestRuntime((_, _, _) => Task.FromResult(new AgentResult { Proposals = [proposal] })), new MemoryJobs(fixture.Projects));
        var job = await service.RunAsync(project, "Actions", new NoProgress(), CancellationToken.None);
        await service.RejectAsync(job, [proposal.Id]);
        await service.ApproveAsync(job, [proposal.Id]);
        Assert.Empty((await fixture.Projects.GetAsync(project.Id))!.Tasks);
        Assert.Equal(ProposalReviewStatus.Rejected, job.Proposals.Single().ReviewStatus);
    }

    [Fact]
    public async Task ConfirmedCancellationPersistsPartialResultAndNoProposals()
    {
        await using var fixture = await Fixture.CreateAsync();
        var project = await fixture.Projects.CreateAsync("Atlas", "Atlas", default, default, "", "");
        using var stop = new CancellationTokenSource();
        var runtime = new TestRuntime((_, progress, token) =>
        {
            progress.Report(new AgentEvent { Kind = AgentEventKind.ResultDelta, Message = "Partial analysis" });
            stop.Cancel();
            return Task.FromCanceled<AgentResult>(token);
        });
        var service = new AgentService(fixture.Projects, runtime, new MemoryJobs());
        var job = await service.RunAsync(project, "Summary", new NoProgress(), stop.Token);
        Assert.Equal(AgentJobStatus.Cancelled, job.Status);
        Assert.Equal("Partial analysis", job.ResultText);
        Assert.Empty(job.Proposals);
        Assert.Equal(AgentJobStatus.Cancelled, (await service.HistoryAsync(project.Id)).Single().Status);
    }

    [Fact]
    public async Task FailedStopIsNotRecordedAsCancelled()
    {
        await using var fixture = await Fixture.CreateAsync();
        var project = await fixture.Projects.CreateAsync("Atlas", "Atlas", default, default, "", "");
        using var stop = new CancellationTokenSource();
        var runtime = new TestRuntime((_, _, _) =>
        {
            stop.Cancel();
            throw new InvalidOperationException("Runtime stop could not be confirmed.");
        });
        var service = new AgentService(fixture.Projects, runtime, new MemoryJobs());
        var job = await service.RunAsync(project, "Summary", new NoProgress(), stop.Token);
        Assert.Equal(AgentJobStatus.Failed, job.Status);
        Assert.Contains("not be confirmed", job.Error);
    }

    [Fact]
    public async Task StaleRunningHistoryIsInterruptedNotSuccessfulOrConfirmedStopped()
    {
        await using var fixture = await Fixture.CreateAsync();
        var project = await fixture.Projects.CreateAsync("Atlas", "Atlas", default, default, "", "");
        var jobs = new MemoryJobs();
        await jobs.SaveAsync(new AgentJob { ProjectId = project.Id, Status = AgentJobStatus.Running });
        var service = new AgentService(fixture.Projects,
            new TestRuntime((_, _, _) => throw new InvalidOperationException()), jobs);
        var history = Assert.Single(await service.HistoryAsync(project.Id));
        Assert.Equal(AgentJobStatus.Interrupted, history.Status);
        Assert.Contains("not confirmed", history.Error);
    }

    private sealed class NoProgress : IProgress<AgentEvent>
    {
        public void Report(AgentEvent value) { }
    }

    private sealed class TestRuntime(Func<AgentRequest, IProgress<AgentEvent>, CancellationToken, Task<AgentResult>> run) : IAgentRuntime
    {
        public Task<AgentResult> RunAsync(AgentRequest request, IProgress<AgentEvent> progress, CancellationToken cancellationToken)
            => run(request, progress, cancellationToken);
        public Task CancelAsync(string jobId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MemoryJobs(ProjectService? projects = null) : IAgentJobRepository
    {
        private readonly Dictionary<string, string> _jobs = [];
        public Task SaveAsync(AgentJob job, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _jobs[job.Id] = JsonSerializer.Serialize(job);
            return Task.CompletedTask;
        }
        public async Task SaveReviewAsync(Project project, AgentJob job, CancellationToken cancellationToken = default)
        {
            await projects!.SaveAsync(project, cancellationToken);
            await SaveAsync(job, cancellationToken);
        }
        public Task<IReadOnlyList<AgentJob>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<AgentJob> result = _jobs.Values.Select(json => JsonSerializer.Deserialize<AgentJob>(json)!)
                .Where(job => job.ProjectId == projectId).ToList();
            return Task.FromResult(result);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "projectops-agent-tests-" + Guid.NewGuid().ToString("N"));
        public ProjectService Projects { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            Directory.CreateDirectory(fixture._directory);
            var repository = new SqliteProjectRepository(Path.Combine(fixture._directory, "projects.db"));
            await repository.InitializeAsync();
            fixture.Projects = new ProjectService(repository);
            return fixture;
        }

        public ValueTask DisposeAsync()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(_directory, true);
            return ValueTask.CompletedTask;
        }
    }
}
