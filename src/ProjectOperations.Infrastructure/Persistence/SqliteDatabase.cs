using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ProjectOperations.Infrastructure.Persistence;

internal sealed class SqliteDatabase(string path)
{
    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            ForeignKeys = true
        }.ToString());
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        Task.Run(() => InitializeCoreAsync(cancellationToken), cancellationToken);

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using (var journal = connection.CreateCommand())
        {
            journal.CommandText = "PRAGMA journal_mode=WAL;";
            await journal.ExecuteScalarAsync(cancellationToken);
        }
        using var transaction = connection.BeginTransaction();
        using var version = Command(connection, transaction, "PRAGMA user_version;");
        var current = Convert.ToInt32(await version.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (current > 3) throw new InvalidOperationException("This database was created by a newer application version.");
        if (current is 1 or 2)
        {
            // Configurable stages replaced the stage enum without a migration: older data is discarded.
            using var discard = Command(connection, transaction, """
                DROP TABLE IF EXISTS agent_jobs; DROP TABLE IF EXISTS state_items; DROP TABLE IF EXISTS project_state;
                DROP TABLE IF EXISTS milestones; DROP TABLE IF EXISTS tasks; DROP TABLE IF EXISTS files;
                DROP TABLE IF EXISTS requirements; DROP TABLE IF EXISTS projects; DROP TABLE IF EXISTS stages;
                """);
            await discard.ExecuteNonQueryAsync(cancellationToken);
            current = 0;
        }
        if (current == 0)
        {
            using var schema = Command(connection, transaction, """
                CREATE TABLE stages (
                    id TEXT PRIMARY KEY, template_id TEXT NOT NULL, position INTEGER NOT NULL,
                    title TEXT NOT NULL, color TEXT NOT NULL);
                CREATE INDEX stages_template ON stages(template_id);
                CREATE TABLE projects (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL, company_name TEXT NOT NULL,
                    stage_id TEXT NOT NULL REFERENCES stages(id) ON DELETE RESTRICT, status INTEGER NOT NULL, owner TEXT NOT NULL, notes TEXT NOT NULL,
                    created_at TEXT NOT NULL, updated_at TEXT NOT NULL, template_id TEXT NOT NULL, revision INTEGER NOT NULL);
                CREATE TABLE requirements (
                    id TEXT PRIMARY KEY, project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                    position INTEGER NOT NULL, definition_id TEXT NOT NULL, group_id TEXT NOT NULL,
                    title TEXT NOT NULL, type INTEGER NOT NULL, status INTEGER NOT NULL, notes TEXT NOT NULL,
                    value TEXT NOT NULL, last_reviewed_at TEXT);
                CREATE TABLE files (
                    id TEXT PRIMARY KEY, requirement_id TEXT NOT NULL REFERENCES requirements(id) ON DELETE CASCADE,
                    position INTEGER NOT NULL, path TEXT NOT NULL, name TEXT NOT NULL, size INTEGER NOT NULL CHECK(size >= 0), added_at TEXT NOT NULL);
                CREATE TABLE tasks (
                    id TEXT PRIMARY KEY, project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                    position INTEGER NOT NULL, title TEXT NOT NULL, description TEXT NOT NULL, status INTEGER NOT NULL, due_at TEXT,
                    created_at TEXT NOT NULL, updated_at TEXT NOT NULL, requirement_id TEXT);
                CREATE TABLE milestones (
                    id TEXT PRIMARY KEY, project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                    position INTEGER NOT NULL, title TEXT NOT NULL, notes TEXT NOT NULL, due_at TEXT, is_complete INTEGER NOT NULL);
                CREATE TABLE project_state (
                    project_id TEXT PRIMARY KEY REFERENCES projects(id) ON DELETE CASCADE, summary TEXT NOT NULL);
                CREATE TABLE state_items (
                    project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                    kind INTEGER NOT NULL, position INTEGER NOT NULL, text TEXT NOT NULL,
                    PRIMARY KEY(project_id, kind, position));
                CREATE TABLE agent_jobs (
                    id TEXT PRIMARY KEY, project_id TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                    payload TEXT NOT NULL);
                CREATE INDEX requirements_project ON requirements(project_id);
                CREATE INDEX files_requirement ON files(requirement_id);
                CREATE INDEX tasks_project ON tasks(project_id);
                CREATE INDEX milestones_project ON milestones(project_id);
                CREATE INDEX agent_jobs_project ON agent_jobs(project_id);
                PRAGMA user_version = 3;
                """);
            await schema.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction,
        string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value switch
            {
                null => DBNull.Value,
                Guid id => id.ToString("D"),
                DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
                Enum enumeration => Convert.ToInt32(enumeration, CultureInfo.InvariantCulture),
                _ => value
            });
        return command;
    }

    public static DateTimeOffset Date(SqliteDataReader reader, int ordinal) =>
        DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTimeOffset? NullableDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Date(reader, ordinal);
}
