using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

internal sealed class TasksView : PresentationView
{
    public TasksView(PresentationContext context, Project project) : base(context) => Children.Add(Tasks(project));
    private Control Tasks(Project project)
    {
        var host = new ContentControl();
        host.Content = TaskList(project, host);
        return host;
    }

    private Control TaskList(Project project, ContentControl host)
    {
        var panel = new StackPanel { Spacing = 16 };
        var summary = ProjectSummaries.Summarize(project, DateTimeOffset.Now);
        var tasks = new SectionCard(Context, "tasks.title", Action("task.new", () => { host.Content = TaskEditor(project, new ProjectTask { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; }, "primary")) { Name = "TasksCard" };
        tasks.Body.Children.Add(Label(() => F("tasks.deadlines", summary.OverdueTasks.Count, summary.UpcomingTasks.Count), "Caption", "TextSecondary"));
        var list = new StackPanel { Spacing = 0 };
        foreach (var task in project.Tasks)
        {
            if (list.Children.Count > 0) list.Children.Add(Divider());
            list.Children.Add(new TaskRow(Context, task,
                () => { host.Content = TaskEditor(project, task, false, host); host.BringIntoView(); return Task.CompletedTask; },
                async () => { task.Status = ProjectTaskStatus.Done; task.UpdatedAt = DateTimeOffset.UtcNow; await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
        }
        tasks.Body.Children.Add(list); panel.Children.Add(tasks);

        var milestones = new SectionCard(Context, "milestones.title", Action("milestone.new", () => { host.Content = MilestoneEditor(project, new Milestone { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; })) { Name = "MilestonesCard" };
        var milestoneList = new StackPanel { Spacing = 0 };
        foreach (var milestone in project.Milestones)
        {
            if (milestoneList.Children.Count > 0) milestoneList.Children.Add(Divider());
            var tone = milestone.IsComplete ? "Success" : "Info";
            milestoneList.Children.Add(new ListRow(Context, TaskRow.StatusMark(milestone.IsComplete ? ProjectTaskStatus.Done : ProjectTaskStatus.Todo, false),
                () => milestone.Title, () => Due(milestone.DueAt), () => T(milestone.IsComplete ? "milestone.complete" : "milestone.open"), tone, null, tone,
                () => $"{milestone.Title} · {T(milestone.IsComplete ? "milestone.complete" : "milestone.open")} · {Due(milestone.DueAt)}",
                () => { host.Content = MilestoneEditor(project, milestone, false, host); host.BringIntoView(); return Task.CompletedTask; })
            { Name = "MilestoneRow" });
        }
        milestones.Body.Children.Add(milestoneList); panel.Children.Add(milestones);
        return panel;
    }

    private static Control Divider() => new Border { Height = 1, Background = PresentationTheme.Brush("BorderSubtle"), Margin = new Thickness(0, 2) };

    private Control TaskEditor(Project project, ProjectTask task, bool isNew, ContentControl host)
    {
        var card = new SectionCard(Context, isNew ? "task.new" : "tasks.title") { Name = "TaskEditor" }; var panel = card.Body;
        var title = Input(task.Title); var notes = Input(task.Description, true);
        var due = DateInput(task.DueAt); var status = Choice(task.Status);
        panel.Children.Add(Action("tasks.back", () => { host.Content = TaskList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        Field(panel, "task.title", title); Field(panel, "field.notes", notes); Field(panel, "field.status", status);
        Field(panel, "task.dueDate", due);
        var actions = new WrapPanel { ItemSpacing = 8 };
        actions.Children.Add(Action("task.save", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { ShowError("validation.taskTitleRequired"); return; }
            if (!TryDate(due, out var date)) return;
            task.Title = title.Text.Trim(); task.Description = notes.Text ?? ""; task.Status = (ProjectTaskStatus)status.SelectedItem!; task.DueAt = date;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            if (isNew && !project.Tasks.Contains(task)) project.Tasks.Add(task);
            await SaveAsync(project); await OpenProjectAsync(project.Id, 2);
        }, "primary"));
        if (!isNew) actions.Children.Add(Action("task.delete", async () => { project.Tasks.Remove(task); await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }, "danger"));
        panel.Children.Add(actions);
        return card;
    }

    private Control MilestoneEditor(Project project, Milestone milestone, bool isNew, ContentControl host)
    {
        var card = new SectionCard(Context, "milestone.title") { Name = "MilestoneEditor" }; var panel = card.Body;
        var title = Input(milestone.Title); var notes = Input(milestone.Notes, true);
        var due = DateInput(milestone.DueAt); var complete = new CheckBox { IsChecked = milestone.IsComplete }; Bind(complete, control => control.Content = T("milestone.complete"));
        panel.Children.Add(Action("tasks.back", () => { host.Content = TaskList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        Field(panel, "milestone.title", title); Field(panel, "field.notes", notes); Field(panel, "milestone.date", due); panel.Children.Add(complete);
        var actions = new WrapPanel { ItemSpacing = 8 };
        actions.Children.Add(Action("milestone.save", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { ShowError("validation.milestoneTitleRequired"); return; }
            if (!TryDate(due, out var date)) return;
            milestone.Title = title.Text.Trim(); milestone.Notes = notes.Text ?? ""; milestone.DueAt = date; milestone.IsComplete = complete.IsChecked == true;
            if (isNew && !project.Milestones.Contains(milestone)) project.Milestones.Add(milestone);
            await SaveAsync(project); await OpenProjectAsync(project.Id, 2);
        }, "primary"));
        if (!isNew) actions.Children.Add(Action("milestone.delete", async () => { project.Milestones.Remove(milestone); await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }, "danger"));
        panel.Children.Add(actions);
        return card;
    }
}
