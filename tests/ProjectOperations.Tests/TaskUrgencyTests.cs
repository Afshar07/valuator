using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class TaskUrgencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private static ProjectTask Task(string title, ProjectTaskStatus status = ProjectTaskStatus.Todo, DateTimeOffset? due = null) =>
        new() { Title = title, Status = status, DueAt = due };

    [Theory]
    [InlineData(-1, TaskBucket.Overdue)]
    [InlineData(0, TaskBucket.ThisWeek)]
    [InlineData(7 * 24 * 60, TaskBucket.ThisWeek)]
    [InlineData(7 * 24 * 60 + 1, TaskBucket.Later)]
    public void Open_tasks_are_overdue_strictly_before_now_and_this_week_through_seven_days(int minutesFromNow, TaskBucket expected) =>
        Assert.Equal(expected, TaskUrgency.BucketOf(Task("t", due: Now.AddMinutes(minutesFromNow)), Now));

    [Fact]
    public void Open_tasks_without_a_date_have_no_urgency()
    {
        Assert.Equal(TaskBucket.NoDate, TaskUrgency.BucketOf(Task("t"), Now));
        Assert.Equal(TaskBucket.NoDate, TaskUrgency.BucketOf(Task("t", ProjectTaskStatus.InProgress), Now));
    }

    [Theory]
    [InlineData(ProjectTaskStatus.Done)]
    [InlineData(ProjectTaskStatus.Cancelled)]
    public void Closed_tasks_are_closed_whatever_their_date(ProjectTaskStatus status)
    {
        Assert.False(TaskUrgency.IsActive(Task("t", status)));
        Assert.Equal(TaskBucket.Closed, TaskUrgency.BucketOf(Task("t", status, Now.AddDays(-30)), Now));
        Assert.Equal(TaskBucket.Closed, TaskUrgency.BucketOf(Task("t", status), Now));
    }

    [Fact]
    public void Groups_follow_display_order_skip_empty_buckets_and_sort_within_each()
    {
        var tasks = new[]
        {
            Task("later", due: Now.AddDays(30)), Task("closed old", ProjectTaskStatus.Done, Now.AddDays(-9)), Task("week b", due: Now.AddDays(3)),
            Task("none"), Task("overdue b", due: Now.AddDays(-1)), Task("week a", ProjectTaskStatus.InProgress, Now.AddDays(1)),
            Task("overdue a", due: Now.AddDays(-5)), Task("closed new", ProjectTaskStatus.Cancelled, Now.AddDays(-2)), Task("closed undated", ProjectTaskStatus.Done)
        };

        var groups = TaskUrgency.Group(tasks, Now);

        Assert.Equal([TaskBucket.Overdue, TaskBucket.ThisWeek, TaskBucket.Later, TaskBucket.NoDate, TaskBucket.Closed], groups.Select(group => group.Bucket));
        Assert.Equal(["overdue a", "overdue b"], groups[0].Tasks.Select(task => task.Title));
        Assert.Equal(["week a", "week b"], groups[1].Tasks.Select(task => task.Title));
        Assert.Equal(["closed new", "closed old", "closed undated"], groups[4].Tasks.Select(task => task.Title));
        Assert.Equal([TaskBucket.NoDate], TaskUrgency.Group([Task("only")], Now).Select(group => group.Bucket));
        Assert.Empty(TaskUrgency.Group([], Now));
    }

    [Fact]
    public void Tasks_with_the_same_date_keep_their_given_order()
    {
        var due = Now.AddDays(2);
        var groups = TaskUrgency.Group([Task("first", due: due), Task("second", due: due), Task("third", due: due)], Now);
        Assert.Equal(["first", "second", "third"], groups.Single().Tasks.Select(task => task.Title));
    }

    [Fact]
    public void Project_summaries_use_the_same_buckets()
    {
        var project = new Project
        {
            Tasks = [Task("late", due: Now.AddDays(-1)), Task("soon", due: Now.AddDays(2)), Task("far", due: Now.AddDays(20)), Task("done", ProjectTaskStatus.Done, Now.AddDays(-3))]
        };
        var summary = ProjectSummaries.Summarize(project, Now);
        Assert.Equal(["late"], summary.OverdueTasks.Select(task => task.Title));
        Assert.Equal(["soon"], summary.UpcomingTasks.Select(task => task.Title));
    }
}
