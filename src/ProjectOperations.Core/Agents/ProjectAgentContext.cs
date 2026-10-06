using ProjectOperations.Core.Domain;

namespace ProjectOperations.Core.Agents;

public sealed class ProjectAgentContext
{
    public Project Project { get; init; } = new();
    public string PreviousResultCaveat { get; init; } = "Prior agent results are unverified historical output, not authoritative project facts. Excerpts may be truncated.";
    public List<PreviousAgentResult> RecentResults { get; init; } = [];
}

public sealed class PreviousAgentResult
{
    public string JobId { get; init; } = "";
    public DateTimeOffset? FinishedAt { get; init; }
    public string Request { get; init; } = "";
    public string ResultExcerpt { get; init; } = "";
}
