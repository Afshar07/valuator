using System.Text.Json;
using Microsoft.Data.Sqlite;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Infrastructure.Persistence;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class SqlitePersistenceTests : IAsyncLifetime
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProjectOperations.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => System.IO.Path.Combine(directory, "projects.db");
    private SqliteProjectRepository Repository => new(DatabasePath);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 14, 20, 30, TimeSpan.FromHours(3.5));

    public Task InitializeAsync() => Repository.InitializeAsync();

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        return Task.CompletedTask;
    }

    private static Project CreateProject()
    {
        var template = VcTemplate.Create();
        var project = new Project
        {
            Name = "بررسی 'Atlas'",
            CompanyName = "شرکت Atlas",
            Owner = "VC analyst",
            Stage = ProjectStage.DueDiligence,
            Status = ProjectStatus.OnHold,
            Notes = "Notes\nline 2",
            CreatedAt = Now.AddDays(-2),
            UpdatedAt = Now,
            TemplateId = template.Id,
            Requirements = template.InstantiateRequirements(),
            State = new() { Summary = "Awaiting forecast", OpenQuestions = ["Runway?", "Margins?"], FollowUps = ["Founder response"] }
        };
        project.Requirements[0].Status = RequirementStatus.NeedsReview;
        project.Requirements[0].Notes = "Unverified figures";
        project.Requirements[0].Value = "Data 'quoted'";
        project.Requirements[0].LastReviewedAt = Now.AddHours(-1);
        project.Requirements[0].Files.Add(new()
        {
            FileName = "pitch.pdf",
            Path = "C:\\VC\\Atlas\\pitch.pdf",
            SizeBytes = 15342,
            AddedAt = Now
        });
        project.Tasks =
        [
            new() { ProjectId = project.Id, Title = "Review forecast", Description = "Check assumptions", DueAt = Now.AddDays(1), Status = ProjectTaskStatus.InProgress },
            new() { ProjectId = project.Id, Title = "No deadline", Status = ProjectTaskStatus.Todo }
        ];
        project.Milestones = [new() { ProjectId = project.Id, Title = "IC meeting", Notes = "Prepare brief", DueAt = Now.AddDays(3) }];
        return project;
    }

    [Fact]
    public async Task InitializationEnablesWriteAheadLogging()
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();
        using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode;";
        Assert.Equal("wal", await journal.ExecuteScalarAsync());
    }

    [Fact]
    public async Task AggregateSurvivesNewRepositoryWithMetadataFilesTasksDatesAndState()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var restarted = new SqliteProjectRepository(DatabasePath);
        await restarted.InitializeAsync();
        var loaded = Assert.IsType<Project>(await restarted.GetAsync(project.Id));
        Assert.Equal(JsonSerializer.Serialize(project), JsonSerializer.Serialize(loaded));
        Assert.Equal(1, loaded.Revision);
        Assert.Null(await restarted.GetAsync(Guid.NewGuid()));
        Assert.Equal(project.Id, Assert.Single(await restarted.ListAsync()).Id);
    }

    [Fact]
    public async Task SavesReplaceRemovedChildrenWithoutDeletingOtherProjectsOrJobs()
    {
        var project = CreateProject();
        var other = CreateProject();
        await Repository.SaveAsync(project);
        await Repository.SaveAsync(other);
        var job = new AgentJob { ProjectId = project.Id, ResultText = "Existing analysis" };
        await new SqliteAgentJobRepository(DatabasePath).SaveAsync(job);
        project.Requirements.RemoveAt(0);
        project.Tasks.Clear();
        project.Milestones.Clear();
        project.State.OpenQuestions.Clear();
        await Repository.SaveAsync(project);
        var loaded = Assert.IsType<Project>(await Repository.GetAsync(project.Id));
        Assert.Equal(JsonSerializer.Serialize(project), JsonSerializer.Serialize(loaded));
        Assert.Equal(JsonSerializer.Serialize(other), JsonSerializer.Serialize(await Repository.GetAsync(other.Id)));
        Assert.Equal(job.Id, Assert.Single(await new SqliteAgentJobRepository(DatabasePath).ListAsync(project.Id)).Id);
    }

    [Fact]
    public async Task ChildConstraintFailureRollsBackEntireAggregateAndRevision()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var before = JsonSerializer.Serialize(project);
        project.Name = "Should roll back";
        project.Requirements[0].Files[0].SizeBytes = -1;
        await Assert.ThrowsAsync<SqliteException>(() => Repository.SaveAsync(project));
        Assert.Equal(1, project.Revision);
        Assert.Equal(before, JsonSerializer.Serialize(await Repository.GetAsync(project.Id)));
    }

    [Fact]
    public async Task DuplicateChildIdCannotOverwriteAnotherProject()
    {
        var first = CreateProject();
        await Repository.SaveAsync(first);
        var second = CreateProject();
        second.Requirements[0].Id = first.Requirements[0].Id;
        await Assert.ThrowsAsync<SqliteException>(() => Repository.SaveAsync(second));
        Assert.Null(await Repository.GetAsync(second.Id));
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(await Repository.GetAsync(first.Id)));
    }

    [Fact]
    public async Task StaleSnapshotCannotOverwriteMoreRecentUserEdits()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var stale = Assert.IsType<Project>(await Repository.GetAsync(project.Id));
        project.Notes = "Latest user edits";
        await Repository.SaveAsync(project);
        stale.Notes = "Old view changes";
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.SaveAsync(stale));
        Assert.Equal(JsonSerializer.Serialize(project), JsonSerializer.Serialize(await Repository.GetAsync(project.Id)));
    }

    [Fact]
    public async Task TaskBelongingToAnotherProjectIsRejected()
    {
        var project = CreateProject();
        project.Tasks[0].ProjectId = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentException>(() => Repository.SaveAsync(project));
        Assert.Empty(await Repository.ListAsync());
    }

    [Fact]
    public async Task CancelledSaveDoesNotPersistChanges()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        project.Name = "Cancelled change";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository.SaveAsync(project, cancellation.Token));
        Assert.Equal("بررسی 'Atlas'", (await Repository.GetAsync(project.Id))!.Name);
    }

    [Fact]
    public async Task ServiceCreatesVcRequirementsAndUpdatesTimestamps()
    {
        var service = new ProjectService(Repository);
        var project = await service.CreateAsync("  Atlas  ", "  Atlas company  ", ProjectStage.Screening,
            ProjectStatus.Active, "Analyst", "Notes");
        Assert.Equal("Atlas", project.Name);
        Assert.Equal("Atlas company", project.CompanyName);
        Assert.Equal(16, project.Requirements.Count);
        Assert.All(project.Requirements, requirement => Assert.Equal(RequirementStatus.Missing, requirement.Status));
        project.UpdatedAt = DateTimeOffset.UnixEpoch;
        var beforeSave = DateTimeOffset.UtcNow;
        await service.SaveAsync(project);
        Assert.InRange(project.UpdatedAt, beforeSave, DateTimeOffset.UtcNow);
        Assert.Equal(2, project.Revision);
        Assert.Equal(project.Id, Assert.Single(await service.ListAsync()).Id);
        Assert.NotNull(await service.GetAsync(project.Id));
    }

    [Fact]
    public async Task AgentJobHistoryRetainsResultProposalReviewAndOffsetsAcrossRestart()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var jobs = new SqliteAgentJobRepository(DatabasePath);
        var job = new AgentJob
        {
            ProjectId = project.Id,
            Prompt = "Prepare brief",
            ContextSnapshot = "As supplied",
            Status = AgentJobStatus.Completed,
            CreatedAt = Now,
            FinishedAt = Now.AddMinutes(2),
            ResultText = "Brief",
            Error = "",
            Proposals =
            [new() { Title = "Request forecast", Description = "Missing data", DueAt = Now.AddDays(2), ReviewStatus = ProposalReviewStatus.Approved }]
        };
        await jobs.SaveAsync(job);
        var earlier = new AgentJob { ProjectId = project.Id, CreatedAt = Now.AddDays(-1), Status = AgentJobStatus.Interrupted };
        await jobs.SaveAsync(earlier);
        var loaded = await new SqliteAgentJobRepository(DatabasePath).ListAsync(project.Id);
        Assert.Equal([job.Id, earlier.Id], loaded.Select(item => item.Id));
        Assert.Equal(JsonSerializer.Serialize(job), JsonSerializer.Serialize(loaded[0]));
        job.Proposals[0].ReviewStatus = ProposalReviewStatus.Rejected;
        await jobs.SaveAsync(job);
        Assert.Equal(ProposalReviewStatus.Rejected, (await jobs.ListAsync(project.Id))[0].Proposals[0].ReviewStatus);
        Assert.Empty(await jobs.ListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AgentJobRequiresExistingProjectAndCannotMoveProjects()
    {
        var jobs = new SqliteAgentJobRepository(DatabasePath);
        var job = new AgentJob { ProjectId = Guid.NewGuid() };
        await Assert.ThrowsAsync<SqliteException>(() => jobs.SaveAsync(job));
        var first = CreateProject();
        var second = CreateProject();
        await Repository.SaveAsync(first);
        await Repository.SaveAsync(second);
        job.ProjectId = first.Id;
        await jobs.SaveAsync(job);
        job.ProjectId = second.Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() => jobs.SaveAsync(job));
        Assert.Single(await jobs.ListAsync(first.Id));
        Assert.Empty(await jobs.ListAsync(second.Id));
    }

    [Fact]
    public async Task ApprovalCommitsTaskAndProposalReviewTogether()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var jobs = new SqliteAgentJobRepository(DatabasePath);
        var proposal = new TaskProposal { Title = "Request forecast", Description = "Missing data", DueAt = Now.AddDays(1) };
        var job = new AgentJob { ProjectId = project.Id, Status = AgentJobStatus.Completed, Proposals = [proposal] };
        await jobs.SaveAsync(job);
        project.Tasks.Add(new() { Id = proposal.Id, ProjectId = project.Id, Title = proposal.Title, Description = proposal.Description, DueAt = proposal.DueAt });
        proposal.ReviewStatus = ProposalReviewStatus.Approved;
        var beforeReview = DateTimeOffset.UtcNow;
        await jobs.SaveReviewAsync(project, job);
        var restartedProject = Assert.IsType<Project>(await Repository.GetAsync(project.Id));
        Assert.Contains(restartedProject.Tasks, task => task.Id == proposal.Id);
        Assert.InRange(restartedProject.UpdatedAt, beforeReview, DateTimeOffset.UtcNow);
        Assert.Equal(2, restartedProject.Revision);
        var restartedJob = Assert.Single(await new SqliteAgentJobRepository(DatabasePath).ListAsync(project.Id));
        Assert.Equal(ProposalReviewStatus.Approved, Assert.Single(restartedJob.Proposals).ReviewStatus);
    }

    [Fact]
    public async Task InvalidApprovalRollsBackTaskAndReviewTogether()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var before = JsonSerializer.Serialize(project);
        var jobs = new SqliteAgentJobRepository(DatabasePath);
        var proposal = new TaskProposal { Title = "Request forecast" };
        var job = new AgentJob { ProjectId = project.Id, Status = AgentJobStatus.Completed, Proposals = [proposal] };
        await jobs.SaveAsync(job);
        project.Tasks.Add(new() { Id = proposal.Id, ProjectId = project.Id, Title = proposal.Title });
        proposal.ReviewStatus = ProposalReviewStatus.Approved;
        project.Requirements[0].Files[0].SizeBytes = -1;
        await Assert.ThrowsAsync<SqliteException>(() => jobs.SaveReviewAsync(project, job));
        Assert.Equal(before, JsonSerializer.Serialize(await Repository.GetAsync(project.Id)));
        Assert.Equal(1, project.Revision);
        Assert.Equal(Now, project.UpdatedAt);
        Assert.Equal(ProposalReviewStatus.Pending, Assert.Single(Assert.Single(await jobs.ListAsync(project.Id)).Proposals).ReviewStatus);
    }

    [Fact]
    public async Task JobWriteFailureRollsBackAlreadyWrittenProject()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var before = JsonSerializer.Serialize(project);
        var jobs = new SqliteAgentJobRepository(DatabasePath);
        var job = new AgentJob { ProjectId = project.Id, Status = AgentJobStatus.Completed };
        await jobs.SaveAsync(job);
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await connection.OpenAsync();
            using var trigger = connection.CreateCommand();
            trigger.CommandText = "CREATE TRIGGER reject_job_update BEFORE UPDATE ON agent_jobs BEGIN SELECT RAISE(ABORT, 'simulated write failure'); END;";
            await trigger.ExecuteNonQueryAsync();
        }
        project.Tasks.Add(new() { ProjectId = project.Id, Title = "Should roll back" });
        await Assert.ThrowsAsync<SqliteException>(() => jobs.SaveReviewAsync(project, job));
        Assert.Equal(before, JsonSerializer.Serialize(await Repository.GetAsync(project.Id)));
        Assert.Equal(1, project.Revision);
    }

    [Fact]
    public async Task ReviewRejectsMismatchedProjectAndJobIdentity()
    {
        var project = CreateProject();
        await Repository.SaveAsync(project);
        var jobs = new SqliteAgentJobRepository(DatabasePath);
        var job = new AgentJob { ProjectId = Guid.NewGuid() };
        await Assert.ThrowsAsync<ArgumentException>(() => jobs.SaveReviewAsync(project, job));
        Assert.Equal(1, project.Revision);
    }

    [Fact]
    public async Task NewerDatabaseVersionIsRejectedWithoutChangingIt()
    {
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 2;";
            await command.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repository.InitializeAsync());
        await using var verify = new SqliteConnection($"Data Source={DatabasePath}");
        await verify.OpenAsync();
        using var version = verify.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.Equal(2L, await version.ExecuteScalarAsync());
    }
}
