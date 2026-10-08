using ProjectOperations.Core.Domain;

namespace ProjectOperations.Core.Application;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(Project project, CancellationToken cancellationToken = default);
    /// <summary>The template's ordered stages; the defaults are seeded the first time a template is asked for.</summary>
    Task<IReadOnlyList<ProjectStage>> ListStagesAsync(string templateId, CancellationToken cancellationToken = default);
    /// <summary>Replaces the template's ordered stages. Fails when a removed stage is still used by a project.</summary>
    Task SaveStagesAsync(string templateId, IReadOnlyList<ProjectStage> stages, CancellationToken cancellationToken = default);
    /// <summary>Number of projects on each stage (stages without projects are absent).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountProjectsByStageAsync(CancellationToken cancellationToken = default);
}

public sealed class StageInUseException(int projects) : InvalidOperationException("The stage is used by projects.")
{
    public int Projects { get; } = projects;
}

public sealed class ProjectService(IProjectRepository repository)
{
    public async Task<Project> CreateAsync(string name, string company,
        ProjectStatus status, string owner, string notes, Guid? stageId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var template = VcTemplate.Create();
        var stage = await ResolveStageAsync(template.Id, stageId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Name = name.Trim(),
            CompanyName = company.Trim(),
            Stage = stage,
            StageId = stage.Id,
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
    public async Task<Project> CreateBlankAsync(string name, string company, ProjectStatus status, string owner, string notes,
        string? firstRequirement = null, Guid? stageId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var stage = await ResolveStageAsync(BlankTemplateId, stageId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Name = name.Trim(),
            CompanyName = company.Trim(),
            Stage = stage,
            StageId = stage.Id,
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

    private async Task<ProjectStage> ResolveStageAsync(string templateId, Guid? stageId, CancellationToken cancellationToken)
    {
        var stages = await repository.ListStagesAsync(templateId, cancellationToken);
        if (stageId is null) return stages.FirstOrDefault() ?? throw new InvalidOperationException("The template has no stages.");
        return stages.FirstOrDefault(stage => stage.Id == stageId) ?? throw new ArgumentException("The stage does not belong to the template.", nameof(stageId));
    }

    public Task<IReadOnlyList<ProjectStage>> ListStagesAsync(string templateId, CancellationToken cancellationToken = default) =>
        repository.ListStagesAsync(templateId, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, int>> CountProjectsByStageAsync(CancellationToken cancellationToken = default) =>
        repository.CountProjectsByStageAsync(cancellationToken);

    /// <summary>Saves the template's complete ordered list (add, rename, recolor and reorder all go through here).</summary>
    public Task SaveStagesAsync(string templateId, IReadOnlyList<ProjectStage> stages, CancellationToken cancellationToken = default)
    {
        if (stages.Count == 0) throw new ArgumentException("A template needs at least one stage.", nameof(stages));
        if (stages.Any(stage => string.IsNullOrWhiteSpace(stage.Title))) throw new ArgumentException("A stage needs a title.", nameof(stages));
        return repository.SaveStagesAsync(templateId, stages, cancellationToken);
    }

    /// <summary>Removes a stage unless a project uses it (<see cref="StageInUseException"/>).</summary>
    public async Task DeleteStageAsync(string templateId, Guid stageId, CancellationToken cancellationToken = default)
    {
        var usage = await repository.CountProjectsByStageAsync(cancellationToken);
        if (usage.TryGetValue(stageId, out var projects) && projects > 0) throw new StageInUseException(projects);
        var stages = (await repository.ListStagesAsync(templateId, cancellationToken)).Where(stage => stage.Id != stageId).ToList();
        await SaveStagesAsync(templateId, stages, cancellationToken);
    }

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
