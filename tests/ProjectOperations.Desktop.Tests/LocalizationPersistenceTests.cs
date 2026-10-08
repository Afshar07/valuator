using System.Text.Json;
using Microsoft.Data.Sqlite;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Infrastructure.Persistence;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class LocalizationPersistenceTests
{
    [Fact]
    public async Task Language_switch_and_restart_preserve_the_entire_existing_aggregate_and_storage_values()
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode", "locale-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "projects.db");
        var settings = Path.Combine(directory, "settings.json");
        try
        {
            var repository = new SqliteProjectRepository(database);
            await repository.InitializeAsync();
            var projects = new ProjectService(repository);
            var project = await projects.CreateAsync("شرکت Example", "Example Ltd",
                ProjectStatus.OnHold, "Owner", "Original notes / یادداشت");
            var instant = new DateTimeOffset(2030, 1, 15, 9, 30, 0, TimeSpan.FromHours(3.5));
            project.Requirements[0].Status = RequirementStatus.NeedsReview;
            project.Requirements[0].LastReviewedAt = instant;
            project.Requirements[0].Value = "Unchanged value";
            project.Requirements[0].Files.Add(new ProjectFile { FileName = "forecast-2030.pdf", Path = Path.Combine(directory, "forecast-2030.pdf"), AddedAt = instant });
            project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Existing task", Status = ProjectTaskStatus.InProgress, DueAt = instant });
            project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "Existing milestone", DueAt = instant });
            project.State.OpenQuestions.Add("Original question");
            await projects.SaveAsync(project);
            var baseline = JsonSerializer.Serialize(await projects.GetAsync(project.Id));

            var locale = new LocaleContext(settings);
            var display = new DomainDisplay(new LocalizationService(locale));
            var dates = new LocaleDateFormatter(locale);
            foreach (var code in new[] { "fa", "en", "fa" })
            {
                locale.SetLanguage(code);
                _ = display.Get(project.Status);
                foreach (var requirement in project.Requirements)
                {
                    _ = display.Requirement(requirement);
                    _ = display.Get(requirement.Type);
                    _ = display.Get(requirement.Status);
                }
                _ = dates.Display(instant);
                Assert.Equal(baseline, JsonSerializer.Serialize(await projects.GetAsync(project.Id)));
            }

            Assert.Equal("fa", new LocaleContext(settings).LanguageCode);
            var reopened = await new SqliteProjectRepository(database).GetAsync(project.Id);
            Assert.Equal(baseline, JsonSerializer.Serialize(reopened));
            Assert.Equal(TimeSpan.FromHours(3.5), reopened!.Tasks.Single().DueAt!.Value.Offset);

            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database }.ToString());
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT stage_id,status FROM projects WHERE id=$id";
            command.Parameters.AddWithValue("$id", project.Id.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(project.StageId.ToString("D"), reader.GetString(0));
            Assert.Equal((int)ProjectStatus.OnHold, reader.GetInt32(1));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
