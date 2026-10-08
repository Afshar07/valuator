namespace ProjectOperations.Core.Domain;

public enum ProjectStage { Screening, DueDiligence, InvestmentCommittee, Investment, Portfolio, Exit }
public enum ProjectStatus { Active, OnHold, Completed, Archived }
public enum RequirementType { Document, Text, Number, Money, Structured }
public enum RequirementStatus { Missing, Provided, NeedsReview, Complete }
public enum ProjectTaskStatus { Todo, InProgress, Done, Cancelled }

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public ProjectStage Stage { get; set; }
    public ProjectStatus Status { get; set; }
    public string Owner { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string TemplateId { get; set; } = "";
    public long Revision { get; set; }
    public List<ProjectRequirement> Requirements { get; set; } = [];
    public List<ProjectTask> Tasks { get; set; } = [];
    public List<Milestone> Milestones { get; set; } = [];
    public ProjectState State { get; set; } = new();
}

public sealed class ProjectState
{
    public string Summary { get; set; } = "";
    public List<string> OpenQuestions { get; set; } = [];
    public List<string> FollowUps { get; set; } = [];
}

public sealed class ProjectRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DefinitionId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Title { get; set; } = "";
    public RequirementType Type { get; set; }
    public RequirementStatus Status { get; set; }
    public string Notes { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset? LastReviewedAt { get; set; }
    public List<ProjectFile> Files { get; set; } = [];
}

public sealed class ProjectFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProjectTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ProjectTaskStatus Status { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    /// <summary>The requirement this task follows up on, when it was created from one.</summary>
    public Guid? RequirementId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Milestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTimeOffset? DueAt { get; set; }
    public bool IsComplete { get; set; }
}
