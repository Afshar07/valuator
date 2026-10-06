using System.Text.Json;
using Microsoft.Data.Sqlite;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Infrastructure.Persistence;

public sealed class SqliteAgentJobRepository(string databasePath) : IAgentJobRepository
{
    private readonly SqliteDatabase database = new(databasePath);

    public Task SaveAsync(AgentJob job, CancellationToken cancellationToken = default) =>
        Task.Run(() => SaveCoreAsync(job, cancellationToken), cancellationToken);

    private async Task SaveCoreAsync(AgentJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.Id);
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await SaveSnapshotAsync(connection, transaction, job, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveReviewAsync(Project project, AgentJob job, CancellationToken cancellationToken = default) =>
        Task.Run(() => SaveReviewCoreAsync(project, job, cancellationToken), cancellationToken);

    private async Task SaveReviewCoreAsync(Project project, AgentJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentException.ThrowIfNullOrWhiteSpace(job.Id);
        if (project.Id != job.ProjectId)
            throw new ArgumentException("The reviewed job must belong to the project.", nameof(job));
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        using (var exists = SqliteDatabase.Command(connection, transaction,
            "SELECT COUNT(*) FROM agent_jobs WHERE id=$id AND project_id=$project;", ("$id", job.Id), ("$project", project.Id)))
            if (Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new InvalidOperationException("The reviewed job is no longer available for this project.");
        var previousUpdatedAt = project.UpdatedAt;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            var revision = await SqliteProjectRepository.SaveAggregateAsync(connection, transaction, project, cancellationToken);
            await SaveSnapshotAsync(connection, transaction, job, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            project.Revision = revision;
        }
        catch
        {
            project.UpdatedAt = previousUpdatedAt;
            throw;
        }
    }

    private static async Task SaveSnapshotAsync(SqliteConnection connection, SqliteTransaction transaction,
        AgentJob job, CancellationToken cancellationToken)
    {
        using var command = SqliteDatabase.Command(connection, transaction, """
            INSERT INTO agent_jobs(id,project_id,payload) VALUES($id,$project,$payload)
            ON CONFLICT(id) DO UPDATE SET payload=$payload WHERE agent_jobs.project_id=$project;
            """, ("$id", job.Id), ("$project", job.ProjectId), ("$payload", JsonSerializer.Serialize(job)));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("An agent job cannot move to another project.");
    }

    public Task<IReadOnlyList<AgentJob>> ListAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.Run(() => ListCoreAsync(projectId, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<AgentJob>> ListCoreAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: true);
        using var command = SqliteDatabase.Command(connection, transaction,
            "SELECT payload FROM agent_jobs WHERE project_id=$project;", ("$project", projectId));
        var jobs = new List<AgentJob>();
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                jobs.Add(JsonSerializer.Deserialize<AgentJob>(reader.GetString(0))
                    ?? throw new InvalidDataException("The stored agent job is invalid."));
            }
        await transaction.CommitAsync(cancellationToken);
        return jobs.OrderByDescending(job => job.CreatedAt).ThenBy(job => job.Id, StringComparer.Ordinal).ToList();
    }
}
