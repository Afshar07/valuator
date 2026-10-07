using ProjectOperations.Core.Domain;

namespace ProjectOperations.Core.Application;

public sealed record ProjectSummary(Project Project, int CompleteRequirements, int TotalRequirements,
    IReadOnlyList<ProjectRequirement> MissingRequirements, IReadOnlyList<ProjectTask> OverdueTasks,
    IReadOnlyList<ProjectTask> UpcomingTasks, Milestone? NextMilestone)
{
    public double CompletionPercentage => TotalRequirements == 0 ? 0 : 100.0 * CompleteRequirements / TotalRequirements;
}

public sealed record TaskAttention(Guid ProjectId, string ProjectName, ProjectTask Task);
public sealed record MilestoneAttention(Guid ProjectId, string ProjectName, Milestone Milestone);
public sealed record DashboardSummary(IReadOnlyList<ProjectSummary> Projects,
    IReadOnlyList<Project> RecentProjects, IReadOnlyList<TaskAttention> OverdueTasks,
    IReadOnlyList<TaskAttention> UpcomingTasks, IReadOnlyList<MilestoneAttention> UpcomingMilestones);

public enum ScheduleItemKind { Task, Milestone }

/// <summary>A dated, still-open task or milestone. Overdue applies to active tasks due before the supplied instant.</summary>
public sealed record ScheduleItem(Guid ProjectId, string ProjectName, ScheduleItemKind Kind, Guid ItemId, string Title,
    DateTimeOffset DueAt, bool IsOverdue);

public static class ProjectSummaries
{
    public static ProjectSummary Summarize(Project project, DateTimeOffset now)
    {
        var active = project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress);
        return new(project,
            project.Requirements.Count(requirement => requirement.Status == RequirementStatus.Complete),
            project.Requirements.Count,
            project.Requirements.Where(requirement => requirement.Status is RequirementStatus.Missing or RequirementStatus.NeedsReview).ToList(),
            active.Where(task => task.DueAt < now).OrderBy(task => task.DueAt).ThenBy(task => task.Id).ToList(),
            active.Where(task => task.DueAt >= now && task.DueAt <= now.AddDays(7)).OrderBy(task => task.DueAt).ThenBy(task => task.Id).ToList(),
            project.Milestones.Where(milestone => !milestone.IsComplete && milestone.DueAt.HasValue)
                .OrderBy(milestone => milestone.DueAt).ThenBy(milestone => milestone.Id).FirstOrDefault());
    }

    public static DashboardSummary Dashboard(IEnumerable<Project> projects, DateTimeOffset now)
    {
        var all = projects.OrderByDescending(project => project.UpdatedAt).ThenBy(project => project.Id).ToList();
        var summaries = all.Select(project => Summarize(project, now)).ToList();
        var active = summaries.Where(summary => summary.Project.Status is ProjectStatus.Active or ProjectStatus.OnHold).ToList();
        return new(summaries, all,
            active.SelectMany(summary => summary.OverdueTasks.Select(task => new TaskAttention(summary.Project.Id, summary.Project.Name, task)))
                .OrderBy(item => item.Task.DueAt).ThenBy(item => item.Task.Id).ToList(),
            active.SelectMany(summary => summary.UpcomingTasks.Select(task => new TaskAttention(summary.Project.Id, summary.Project.Name, task)))
                .OrderBy(item => item.Task.DueAt).ThenBy(item => item.Task.Id).ToList(),
            active.SelectMany(summary => summary.Project.Milestones
                .Where(milestone => !milestone.IsComplete && milestone.DueAt >= now && milestone.DueAt <= now.AddDays(7))
                .Select(milestone => new MilestoneAttention(summary.Project.Id, summary.Project.Name, milestone)))
                .OrderBy(item => item.Milestone.DueAt).ThenBy(item => item.Milestone.Id).ToList());
    }

    /// <summary>
    /// Open dated work (active tasks and incomplete milestones) of Active/OnHold projects due within [from, to),
    /// ordered by date. Done/Cancelled tasks, completed milestones and Completed/Archived projects are excluded.
    /// </summary>
    public static IReadOnlyList<ScheduleItem> Schedule(IEnumerable<Project> projects, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now) =>
        projects.Where(project => project.Status is ProjectStatus.Active or ProjectStatus.OnHold)
            .SelectMany(OpenItems)
            .Where(item => item.DueAt >= from && item.DueAt < to)
            .Select(item => item with { IsOverdue = item.Kind == ScheduleItemKind.Task && item.DueAt < now })
            .OrderBy(item => item.DueAt).ThenBy(item => item.ItemId).ToList();

    /// <summary>Earliest open dated task or milestone of a project, or null when nothing open is dated.</summary>
    public static ScheduleItem? NextDeadline(Project project, DateTimeOffset now) =>
        OpenItems(project).OrderBy(item => item.DueAt).ThenBy(item => item.ItemId)
            .Select(item => item with { IsOverdue = item.Kind == ScheduleItemKind.Task && item.DueAt < now }).FirstOrDefault();

    private static IEnumerable<ScheduleItem> OpenItems(Project project) =>
        project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress && task.DueAt.HasValue)
            .Select(task => new ScheduleItem(project.Id, project.Name, ScheduleItemKind.Task, task.Id, task.Title, task.DueAt!.Value, false))
            .Concat(project.Milestones.Where(milestone => !milestone.IsComplete && milestone.DueAt.HasValue)
                .Select(milestone => new ScheduleItem(project.Id, project.Name, ScheduleItemKind.Milestone, milestone.Id, milestone.Title, milestone.DueAt!.Value, false)));
}
