using Microsoft.Data.Sqlite;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Infrastructure.Persistence;

public sealed class SqliteProjectRepository(string databasePath) : IProjectRepository
{
    private readonly SqliteDatabase database = new(databasePath);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => database.InitializeAsync(cancellationToken);

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => ListCoreAsync(cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<Project>> ListCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: true);
        var projects = await ReadProjectsAsync(connection, transaction, null, cancellationToken);
        foreach (var project in projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ReadChildrenAsync(connection, transaction, project, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return projects.OrderByDescending(project => project.UpdatedAt).ThenBy(project => project.Id).ToList();
    }

    public Task<IReadOnlyList<ProjectStage>> ListStagesAsync(string templateId, CancellationToken cancellationToken = default) =>
        Task.Run(() => ListStagesCoreAsync(templateId, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<ProjectStage>> ListStagesCoreAsync(string templateId, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var stages = await ReadStagesAsync(connection, transaction, templateId, cancellationToken);
        if (stages.Count == 0)
        {
            stages = ProjectStage.Defaults();
            await WriteStagesAsync(connection, transaction, templateId, stages, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return stages;
    }

    public Task SaveStagesAsync(string templateId, IReadOnlyList<ProjectStage> stages, CancellationToken cancellationToken = default) =>
        Task.Run(() => SaveStagesCoreAsync(templateId, stages, cancellationToken), cancellationToken);

    private async Task SaveStagesCoreAsync(string templateId, IReadOnlyList<ProjectStage> stages, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await WriteStagesAsync(connection, transaction, templateId, stages, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<List<ProjectStage>> ReadStagesAsync(SqliteConnection connection, SqliteTransaction transaction,
        string templateId, CancellationToken cancellationToken)
    {
        using var command = SqliteDatabase.Command(connection, transaction,
            "SELECT id,title,color FROM stages WHERE template_id=$template ORDER BY position;", ("$template", templateId));
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var stages = new List<ProjectStage>();
        while (await reader.ReadAsync(cancellationToken))
            stages.Add(new ProjectStage { Id = Guid.Parse(reader.GetString(0)), Title = reader.GetString(1), Color = reader.GetString(2) });
        return stages;
    }

    private static async Task WriteStagesAsync(SqliteConnection connection, SqliteTransaction transaction,
        string templateId, IReadOnlyList<ProjectStage> stages, CancellationToken cancellationToken)
    {
        for (var position = 0; position < stages.Count; position++)
        {
            using var upsert = SqliteDatabase.Command(connection, transaction, """
                INSERT INTO stages(id,template_id,position,title,color) VALUES($id,$template,$position,$title,$color)
                ON CONFLICT(id) DO UPDATE SET position=$position,title=$title,color=$color WHERE stages.template_id=$template;
                """, ("$id", stages[position].Id), ("$template", templateId), ("$position", position),
                ("$title", stages[position].Title.Trim()), ("$color", stages[position].Color));
            await upsert.ExecuteNonQueryAsync(cancellationToken);
        }
        var keep = string.Join(",", stages.Select((_, index) => "$k" + index));
        using var delete = SqliteDatabase.Command(connection, transaction,
            $"DELETE FROM stages WHERE template_id=$template AND id NOT IN ({keep});",
            [("$template", templateId), .. stages.Select((stage, index) => ($"$k{index}", (object?)stage.Id))]);
        await delete.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<IReadOnlyDictionary<Guid, int>> CountProjectsByStageAsync(CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            await using var connection = await database.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT stage_id,COUNT(*) FROM projects GROUP BY stage_id;";
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var counts = new Dictionary<Guid, int>();
            while (await reader.ReadAsync(cancellationToken)) counts[Guid.Parse(reader.GetString(0))] = reader.GetInt32(1);
            return (IReadOnlyDictionary<Guid, int>)counts;
        }, cancellationToken);

    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.Run(() => GetCoreAsync(id, cancellationToken), cancellationToken);

    private async Task<Project?> GetCoreAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: true);
        var project = (await ReadProjectsAsync(connection, transaction, id, cancellationToken)).SingleOrDefault();
        if (project is not null) await ReadChildrenAsync(connection, transaction, project, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return project;
    }

    public Task SaveAsync(Project project, CancellationToken cancellationToken = default) =>
        Task.Run(() => SaveCoreAsync(project, cancellationToken), cancellationToken);

    private async Task SaveCoreAsync(Project project, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var revision = await SaveAggregateAsync(connection, transaction, project, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        project.Revision = revision;
    }

    internal static async Task<long> SaveAggregateAsync(SqliteConnection connection, SqliteTransaction transaction,
        Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.Name);
        if (project.Tasks.Any(task => task.ProjectId != project.Id) || project.Milestones.Any(item => item.ProjectId != project.Id))
            throw new ArgumentException("Tasks and milestones must belong to the containing project.", nameof(project));
        var revision = checked(project.Revision + 1);
        using (var command = SqliteDatabase.Command(connection, transaction, """
            INSERT INTO projects(id,name,company_name,stage_id,status,owner,notes,created_at,updated_at,template_id,revision)
            VALUES($id,$name,$company,$stage,$status,$owner,$notes,$created,$updated,$template,$next)
            ON CONFLICT(id) DO UPDATE SET name=$name,company_name=$company,stage_id=$stage,status=$status,
                owner=$owner,notes=$notes,updated_at=$updated,template_id=$template,revision=$next
            WHERE projects.revision=$revision;
            """, ("$id", project.Id), ("$name", project.Name), ("$company", project.CompanyName),
            ("$stage", project.StageId), ("$status", project.Status), ("$owner", project.Owner), ("$notes", project.Notes),
            ("$created", project.CreatedAt), ("$updated", project.UpdatedAt), ("$template", project.TemplateId),
            ("$next", revision), ("$revision", project.Revision)))
        {
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("The project has changed since it was loaded. Reload before saving.");
        }
        using (var clear = SqliteDatabase.Command(connection, transaction, """
            DELETE FROM requirements WHERE project_id=$id;
            DELETE FROM tasks WHERE project_id=$id;
            DELETE FROM milestones WHERE project_id=$id;
            DELETE FROM project_state WHERE project_id=$id;
            DELETE FROM state_items WHERE project_id=$id;
            """, ("$id", project.Id))) await clear.ExecuteNonQueryAsync(cancellationToken);

        for (var position = 0; position < project.Requirements.Count; position++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requirement = project.Requirements[position];
            using var command = SqliteDatabase.Command(connection, transaction, """
                INSERT INTO requirements VALUES($id,$project,$position,$definition,$group,$title,$type,$status,$notes,$value,$reviewed);
                """, ("$id", requirement.Id), ("$project", project.Id), ("$position", position),
                ("$definition", requirement.DefinitionId), ("$group", requirement.GroupId), ("$title", requirement.Title),
                ("$type", requirement.Type), ("$status", requirement.Status), ("$notes", requirement.Notes),
                ("$value", requirement.Value), ("$reviewed", requirement.LastReviewedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
            for (var filePosition = 0; filePosition < requirement.Files.Count; filePosition++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = requirement.Files[filePosition];
                using var fileCommand = SqliteDatabase.Command(connection, transaction,
                    "INSERT INTO files VALUES($id,$requirement,$position,$path,$name,$size,$added);",
                    ("$id", file.Id), ("$requirement", requirement.Id), ("$position", filePosition),
                    ("$path", file.Path), ("$name", file.FileName), ("$size", file.SizeBytes), ("$added", file.AddedAt));
                await fileCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        for (var position = 0; position < project.Tasks.Count; position++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = project.Tasks[position];
            using var command = SqliteDatabase.Command(connection, transaction,
                "INSERT INTO tasks VALUES($id,$project,$position,$title,$description,$status,$due,$created,$updated,$requirement);",
                ("$id", task.Id), ("$project", project.Id), ("$position", position), ("$title", task.Title),
                ("$description", task.Description), ("$status", task.Status), ("$due", task.DueAt),
                ("$created", task.CreatedAt), ("$updated", task.UpdatedAt), ("$requirement", task.RequirementId));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        for (var position = 0; position < project.Milestones.Count; position++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var milestone = project.Milestones[position];
            using var command = SqliteDatabase.Command(connection, transaction,
                "INSERT INTO milestones VALUES($id,$project,$position,$title,$notes,$due,$complete);",
                ("$id", milestone.Id), ("$project", project.Id), ("$position", position), ("$title", milestone.Title),
                ("$notes", milestone.Notes), ("$due", milestone.DueAt), ("$complete", milestone.IsComplete));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        using (var state = SqliteDatabase.Command(connection, transaction,
            "INSERT INTO project_state VALUES($id,$summary);", ("$id", project.Id), ("$summary", project.State.Summary)))
            await state.ExecuteNonQueryAsync(cancellationToken);
        for (var kind = 0; kind < 2; kind++)
        {
            var items = kind == 0 ? project.State.OpenQuestions : project.State.FollowUps;
            for (var position = 0; position < items.Count; position++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var command = SqliteDatabase.Command(connection, transaction,
                    "INSERT INTO state_items VALUES($id,$kind,$position,$text);",
                    ("$id", project.Id), ("$kind", kind), ("$position", position), ("$text", items[position]));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        return revision;
    }

    private static async Task<List<Project>> ReadProjectsAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid? id, CancellationToken cancellationToken)
    {
        using var command = SqliteDatabase.Command(connection, transaction,
            "SELECT p.id,p.name,p.company_name,p.stage_id,p.status,p.owner,p.notes,p.created_at,p.updated_at,p.template_id,p.revision,s.title,s.color"
            + " FROM projects p JOIN stages s ON s.id=p.stage_id" + (id.HasValue ? " WHERE p.id=$id;" : ";"), ("$id", id));
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<Project>();
        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stageId = Guid.Parse(reader.GetString(3));
            result.Add(new Project
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                CompanyName = reader.GetString(2),
                StageId = stageId,
                Stage = new ProjectStage { Id = stageId, Title = reader.GetString(11), Color = reader.GetString(12) },
                Status = (ProjectStatus)reader.GetInt32(4),
                Owner = reader.GetString(5),
                Notes = reader.GetString(6),
                CreatedAt = SqliteDatabase.Date(reader, 7),
                UpdatedAt = SqliteDatabase.Date(reader, 8),
                TemplateId = reader.GetString(9),
                Revision = reader.GetInt64(10)
            });
        }
        return result;
    }

    private static async Task ReadChildrenAsync(SqliteConnection connection, SqliteTransaction transaction,
        Project project, CancellationToken cancellationToken)
    {
        using (var command = SqliteDatabase.Command(connection, transaction,
            "SELECT id,definition_id,group_id,title,type,status,notes,value,last_reviewed_at FROM requirements WHERE project_id=$id ORDER BY position;", ("$id", project.Id)))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                project.Requirements.Add(new ProjectRequirement
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    DefinitionId = reader.GetString(1),
                    GroupId = reader.GetString(2),
                    Title = reader.GetString(3),
                    Type = (RequirementType)reader.GetInt32(4),
                    Status = (RequirementStatus)reader.GetInt32(5),
                    Notes = reader.GetString(6),
                    Value = reader.GetString(7),
                    LastReviewedAt = SqliteDatabase.NullableDate(reader, 8)
                });
            }
        foreach (var requirement in project.Requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var command = SqliteDatabase.Command(connection, transaction,
                "SELECT id,path,name,size,added_at FROM files WHERE requirement_id=$id ORDER BY position;", ("$id", requirement.Id));
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                requirement.Files.Add(new ProjectFile
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Path = reader.GetString(1),
                    FileName = reader.GetString(2),
                    SizeBytes = reader.GetInt64(3),
                    AddedAt = SqliteDatabase.Date(reader, 4)
                });
            }
        }
        using (var command = SqliteDatabase.Command(connection, transaction,
            "SELECT id,title,description,status,due_at,created_at,updated_at,requirement_id FROM tasks WHERE project_id=$id ORDER BY position;", ("$id", project.Id)))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                project.Tasks.Add(new ProjectTask
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    ProjectId = project.Id,
                    Title = reader.GetString(1),
                    Description = reader.GetString(2),
                    Status = (ProjectTaskStatus)reader.GetInt32(3),
                    DueAt = SqliteDatabase.NullableDate(reader, 4),
                    CreatedAt = SqliteDatabase.Date(reader, 5),
                    UpdatedAt = SqliteDatabase.Date(reader, 6),
                    RequirementId = reader.IsDBNull(7) ? null : Guid.Parse(reader.GetString(7))
                });
            }
        using (var command = SqliteDatabase.Command(connection, transaction,
            "SELECT id,title,notes,due_at,is_complete FROM milestones WHERE project_id=$id ORDER BY position;", ("$id", project.Id)))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                project.Milestones.Add(new Milestone
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    ProjectId = project.Id,
                    Title = reader.GetString(1),
                    Notes = reader.GetString(2),
                    DueAt = SqliteDatabase.NullableDate(reader, 3),
                    IsComplete = reader.GetBoolean(4)
                });
            }
        using (var command = SqliteDatabase.Command(connection, transaction,
            "SELECT summary FROM project_state WHERE project_id=$id;", ("$id", project.Id)))
        {
            project.State.Summary = (string?)await command.ExecuteScalarAsync(cancellationToken) ?? "";
        }
        using (var command = SqliteDatabase.Command(connection, transaction,
            "SELECT kind,text FROM state_items WHERE project_id=$id ORDER BY kind,position;", ("$id", project.Id)))
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                (reader.GetInt32(0) == 0 ? project.State.OpenQuestions : project.State.FollowUps).Add(reader.GetString(1));
            }
    }
}
