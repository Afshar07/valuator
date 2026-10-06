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
