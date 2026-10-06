using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class ProjectSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OnlyCompleteRequirementsCountTowardCompletion()
    {
        var project = new Project
        {
            Requirements = Enum.GetValues<RequirementStatus>()
            .Select(status => new ProjectRequirement { Status = status }).ToList()
        };
        var summary = ProjectSummaries.Summarize(project, Now);
        Assert.Equal(1, summary.CompleteRequirements);
        Assert.Equal(4, summary.TotalRequirements);
        Assert.Equal(25, summary.CompletionPercentage);
    }

    [Fact]
    public void MissingIncludesMissingAndNeedsReviewButNotProvided()
    {
        var project = new Project
        {
            Requirements = Enum.GetValues<RequirementStatus>()
            .Select(status => new ProjectRequirement { Status = status }).ToList()
        };
        Assert.Equal([RequirementStatus.Missing, RequirementStatus.NeedsReview],
            ProjectSummaries.Summarize(project, Now).MissingRequirements.Select(requirement => requirement.Status));
    }

    [Fact]
    public void EmptyRequirementsHaveZeroCompletion()
    {
        Assert.Equal(0, ProjectSummaries.Summarize(new Project(), Now).CompletionPercentage);
    }

    [Fact]
    public void OverdueIsStrictlyBeforeNowAndUpcomingIncludesBothEdges()
    {
        var overdue = new ProjectTask { DueAt = Now.AddTicks(-1) };
        var current = new ProjectTask { DueAt = Now };
        var last = new ProjectTask { DueAt = Now.AddDays(7), Status = ProjectTaskStatus.InProgress };
        var later = new ProjectTask { DueAt = Now.AddDays(7).AddTicks(1) };
        var project = new Project { Tasks = [overdue, current, last, later, new()] };
        var summary = ProjectSummaries.Summarize(project, Now);
        Assert.Equal([overdue.Id], summary.OverdueTasks.Select(task => task.Id));
        Assert.Equal([current.Id, last.Id], summary.UpcomingTasks.Select(task => task.Id));
    }

    [Theory]
    [InlineData(ProjectTaskStatus.Done)]
    [InlineData(ProjectTaskStatus.Cancelled)]
    public void ClosedTasksAreNotAttentionItems(ProjectTaskStatus status)
    {
        var project = new Project { Tasks = [new() { DueAt = Now.AddDays(-1), Status = status }, new() { DueAt = Now, Status = status }] };
        var summary = ProjectSummaries.Summarize(project, Now);
        Assert.Empty(summary.OverdueTasks);
        Assert.Empty(summary.UpcomingTasks);
    }

    [Fact]
    public void ComparisonUsesInstantsRatherThanLocalClock()
    {
        var task = new ProjectTask { DueAt = Now.ToOffset(TimeSpan.FromHours(3.5)) };
        Assert.Single(ProjectSummaries.Summarize(new Project { Tasks = [task] }, Now).UpcomingTasks);
    }

    [Fact]
    public void DashboardSortsRecentProjectsAndExcludesClosedProjectsFromAttention()
    {
        var older = new Project { UpdatedAt = Now.AddDays(-1), Tasks = [new() { DueAt = Now }] };
        var closed = new Project { UpdatedAt = Now, Status = ProjectStatus.Completed, Tasks = [new() { DueAt = Now.AddDays(-1) }] };
        var dashboard = ProjectSummaries.Dashboard([older, closed], Now);
        Assert.Equal([closed.Id, older.Id], dashboard.RecentProjects.Select(project => project.Id));
        Assert.Empty(dashboard.OverdueTasks);
        Assert.Equal(older.Id, Assert.Single(dashboard.UpcomingTasks).ProjectId);
    }

    [Fact]
    public void UpcomingMilestonesExcludeUndatedCompleteAndOutOfWindow()
    {
        var first = new Milestone { DueAt = Now };
        var last = new Milestone { DueAt = Now.AddDays(7) };
        var project = new Project
        {
            Milestones = [first, last, new(),
            new() { DueAt = Now.AddDays(-1) }, new() { DueAt = Now, IsComplete = true }]
        };
        Assert.Equal([first.Id, last.Id], ProjectSummaries.Dashboard([project], Now).UpcomingMilestones.Select(item => item.Milestone.Id));
    }
}
