namespace ProjectOperations.Core.Agents;

public enum AgentJobStatus { Running, Completed, Failed, Cancelled, Interrupted }
public enum ProposalReviewStatus { Pending, Approved, Rejected }
public enum AgentEventKind { Activity, ResultDelta, Detail }
public enum AgentResponseLanguage { English, Persian }

public sealed class AgentRequest
{
    public string JobId { get; init; } = Guid.NewGuid().ToString("N");
    public Guid ProjectId { get; init; }
    public string Prompt { get; init; } = "";
    public string Context { get; init; } = "";
    public string SystemInstructions { get; init; } = AgentPrompts.BuildOutputInstructions(AgentResponseLanguage.English);
}

public sealed class AgentEvent
{
    public AgentEventKind Kind { get; init; }
    public string Message { get; init; } = "";
    public string? ActivityKey { get; init; }
}

public sealed class AgentResult
{
    public string Text { get; init; } = "";
    public List<TaskProposal> Proposals { get; init; } = [];
}

public sealed class TaskProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset? DueAt { get; set; }
    public ProposalReviewStatus ReviewStatus { get; set; }
}

public sealed class AgentJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public Guid ProjectId { get; set; }
    public string Prompt { get; set; } = "";
    public string ContextSnapshot { get; set; } = "";
    public AgentJobStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string ResultText { get; set; } = "";
    public string Error { get; set; } = "";
    public List<TaskProposal> Proposals { get; set; } = [];
}

public interface IAgentRuntime
{
    // Success or cancellation requires confirmed runtime completion. A failure may
    // report an unconfirmed stop; callers must not present that outcome as Cancelled.
    Task<AgentResult> RunAsync(AgentRequest request, IProgress<AgentEvent> progress,
        CancellationToken cancellationToken);
    Task CancelAsync(string jobId, CancellationToken cancellationToken);
}

public interface IAgentJobRepository
{
    Task SaveAsync(AgentJob job, CancellationToken cancellationToken = default);
    Task SaveReviewAsync(ProjectOperations.Core.Domain.Project project, AgentJob job, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentJob>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
}
