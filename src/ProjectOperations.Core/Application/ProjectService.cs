using ProjectOperations.Core.Domain;

namespace ProjectOperations.Core.Application;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(Project project, CancellationToken cancellationToken = default);
}

public sealed class ProjectService(IProjectRepository repository)
{
    public async Task<Project> CreateAsync(string name, string company, ProjectStage stage,
        ProjectStatus status, string owner, string notes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var template = VcTemplate.Create();
        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Name = name.Trim(),
            CompanyName = company.Trim(),
            Stage = stage,
            Status = status,
            Owner = owner,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now,
            TemplateId = template.Id,
            Requirements = template.InstantiateRequirements()
        };
        await repository.SaveAsync(project, cancellationToken);
        return project;
    }

    /// <summary>
    /// Creates a project without the built-in checklist. Requirements are added by the user; an optional first one is created here.
    /// </summary>
    public async Task<Project> CreateBlankAsync(string name, string company, ProjectStage stage, ProjectStatus status, string owner, string notes,
        string? firstRequirement = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Name = name.Trim(),
            CompanyName = company.Trim(),
            Stage = stage,
            Status = status,
            Owner = owner,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now,
            TemplateId = BlankTemplateId
        };
        if (!string.IsNullOrWhiteSpace(firstRequirement)) project.Requirements.Add(NewCustomRequirement(firstRequirement));
        await repository.SaveAsync(project, cancellationToken);
        return project;
    }

    public const string BlankTemplateId = "blank";

    /// <summary>A user-defined requirement; it lives in the single custom group and starts as a missing document.</summary>
    public static ProjectRequirement NewCustomRequirement(string title) => new()
    {
        DefinitionId = "custom-" + Guid.NewGuid().ToString("N"),
        GroupId = "custom",
        Title = title.Trim(),
        Type = RequirementType.Document,
        Status = RequirementStatus.Missing
    };

    public async Task SaveAsync(Project project, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Name);
        var previous = project.UpdatedAt;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        try { await repository.SaveAsync(project, cancellationToken); }
        catch { project.UpdatedAt = previous; throw; }
    }

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(cancellationToken);

    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetAsync(id, cancellationToken);
}
