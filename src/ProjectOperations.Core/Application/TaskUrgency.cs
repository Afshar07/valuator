using ProjectOperations.Core.Domain;

namespace ProjectOperations.Core.Application;

/// <summary>Where a task sits in time, in display order. Only Todo/InProgress tasks are open; Done/Cancelled are Closed.</summary>
public enum TaskBucket { Overdue, ThisWeek, Later, NoDate, Closed }

/// <summary>The tasks of one bucket, in display order.</summary>
public sealed record TaskGroup(TaskBucket Bucket, IReadOnlyList<ProjectTask> Tasks);

/// <summary>
/// The urgency rules shared by every task list. Overdue is strictly before the supplied instant; "this week" is inclusive
/// from the instant through seven days. UI and dashboards ask here instead of re-deriving the dates.
/// </summary>
public static class TaskUrgency
{
    public const int UpcomingDays = 7;

    /// <summary>Todo and InProgress tasks are open work; Done and Cancelled tasks are closed.</summary>
    public static bool IsActive(ProjectTask task) => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress;

    public static TaskBucket BucketOf(ProjectTask task, DateTimeOffset now)
    {
        if (!IsActive(task)) return TaskBucket.Closed;
        if (task.DueAt is not { } due) return TaskBucket.NoDate;
        if (due < now) return TaskBucket.Overdue;
        return due <= now.AddDays(UpcomingDays) ? TaskBucket.ThisWeek : TaskBucket.Later;
    }

    /// <summary>
    /// Non-empty groups in display order. Open groups run earliest due date first (ties keep their given order); closed tasks
    /// run latest due date first.
    /// </summary>
    public static IReadOnlyList<TaskGroup> Group(IEnumerable<ProjectTask> tasks, DateTimeOffset now)
    {
        var buckets = tasks.Select(task => (Task: task, Bucket: BucketOf(task, now))).ToList();
        var groups = new List<TaskGroup>();
        foreach (var bucket in Enum.GetValues<TaskBucket>())
        {
            var members = buckets.Where(item => item.Bucket == bucket).Select(item => item.Task);
            var ordered = bucket == TaskBucket.Closed
                ? members.OrderByDescending(task => task.DueAt ?? DateTimeOffset.MinValue).ToList()
                : members.OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue).ToList();
            if (ordered.Count > 0) groups.Add(new TaskGroup(bucket, ordered));
        }
        return groups;
    }
}
