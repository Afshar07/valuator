using Microsoft.Data.Sqlite;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Infrastructure.Persistence;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class MvpWorkflowTests
{
    [Fact]
    public async Task SampleProjectFileReadinessAndAttentionSurviveDatabaseReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "projectops-workflow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = Path.Combine(directory, "projects.db");
            var repository = new SqliteProjectRepository(database);
            await repository.InitializeAsync();
            var service = new ProjectService(repository);
            var project = await service.CreateAsync("Atlas", "Atlas Company",
                ProjectStatus.Active, "Analyst", "Synthetic verification project");
            Assert.Equal(16, project.Requirements.Count);
            var path = Path.Combine(directory, "synthetic-pitch.txt");
            await File.WriteAllTextAsync(path, "Synthetic data only. No confidential documents.");
            var info = new FileInfo(path);
            project.Requirements[0].Files.Add(new ProjectFile { FileName = info.Name, Path = info.FullName, SizeBytes = info.Length });
            project.Requirements[0].Status = RequirementStatus.Provided;
            project.Requirements[1].Status = RequirementStatus.Complete;
            project.Requirements[1].Value = "Supplied and reviewed operational information";
            project.Requirements[2].Status = RequirementStatus.NeedsReview;
            var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
            project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Overdue forecast", DueAt = now.AddDays(-1) });
            project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Upcoming meeting brief", DueAt = now.AddDays(1) });
            await service.SaveAsync(project);

            var reopened = new SqliteProjectRepository(database);
            await reopened.InitializeAsync();
            var dashboard = ProjectSummaries.Dashboard(await reopened.ListAsync(), now);
            var summary = Assert.Single(dashboard.Projects);
            Assert.Equal("Atlas", summary.Project.Name);
            Assert.Equal("Analyst", summary.Project.Owner);
            Assert.Equal(6.25, summary.CompletionPercentage);
            Assert.Equal(14, summary.MissingRequirements.Count);
            Assert.Equal("Overdue forecast", Assert.Single(dashboard.OverdueTasks).Task.Title);
            Assert.Equal("Upcoming meeting brief", Assert.Single(dashboard.UpcomingTasks).Task.Title);
            Assert.Equal(project.Id, Assert.Single(dashboard.RecentProjects).Id);
            var file = Assert.Single(summary.Project.Requirements[0].Files);
            Assert.Equal(info.FullName, file.Path);
            Assert.Equal(info.Length, file.SizeBytes);
            Assert.True(File.Exists(file.Path));

            summary.Project.Requirements[0].Files.Clear();
            await new ProjectService(reopened).SaveAsync(summary.Project);
            Assert.True(File.Exists(path));
            Assert.Empty((await reopened.GetAsync(project.Id))!.Requirements[0].Files);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task BlankProjectHasOnlyTheRequirementsTheUserAddsAndKeepsThemAfterReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "projectops-blank-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = Path.Combine(directory, "projects.db");
            var repository = new SqliteProjectRepository(database);
            await repository.InitializeAsync();
            var service = new ProjectService(repository);
            var empty = await service.CreateBlankAsync("Empty deal", "", ProjectStatus.Active, "", "");
            Assert.Equal(ProjectService.BlankTemplateId, empty.TemplateId);
            Assert.Empty(empty.Requirements);

            var project = await service.CreateBlankAsync("  Custom deal ", " Company ", ProjectStatus.Active, "", "", " Customer contracts ");
            var requirement = Assert.Single(project.Requirements);
            Assert.Equal("Customer contracts", requirement.Title);
            Assert.Equal("custom", requirement.GroupId);
            Assert.Equal(RequirementType.Document, requirement.Type);
            Assert.Equal(RequirementStatus.Missing, requirement.Status);
            project.Requirements.Add(ProjectService.NewCustomRequirement("Cap table"));
            Assert.NotEqual(project.Requirements[0].DefinitionId, project.Requirements[1].DefinitionId);
            await service.SaveAsync(project);

            SqliteConnection.ClearAllPools();
            var reopened = (await new SqliteProjectRepository(database).GetAsync(project.Id))!;
            Assert.Equal("Custom deal", reopened.Name);
            Assert.Equal(["Customer contracts", "Cap table"], reopened.Requirements.Select(item => item.Title));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateBlankAsync(" ", "", ProjectStatus.Active, "", ""));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}
