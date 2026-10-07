using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Tasks & dates tab: committed tasks grouped by state, milestones, and a pointer to proposals still awaiting review.</summary>
internal sealed class TasksView : PresentationView
{
    private readonly IReadOnlyList<AgentJob> _jobs;
    public TasksView(PresentationContext context, Project project, IReadOnlyList<AgentJob> jobs) : base(context)
    {
        _jobs = jobs;
        var host = new ContentControl();
        host.Content = TaskList(project, host);
        Children.Add(host);
    }

    private Control TaskList(Project project, ContentControl host)
    {
        var panel = new StackPanel { Spacing = 16 };
        var pending = _jobs.Where(job => job.Status == AgentJobStatus.Completed && job.Proposals.Any(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending))
            .OrderByDescending(job => job.CreatedAt).ToList();
        var waiting = pending.Sum(job => job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending));
        if (waiting > 0)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10, Name = "PendingProposalsBanner" };
            row.Children.Add(Label(() => F("tasks.pendingProposals", waiting), "Small"));
            var review = Action("tasks.review", () => Context.Shell.ReviewInAssistantAsync(pending[0].Id), "ai"); review.MinHeight = 28;
            Grid.SetColumn(review, 1); row.Children.Add(review);
            var glyph = Icons.Glyph(Icons.Sparkle, 16, "Ai", IconWeight.Fill);
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            line.Children.Add(glyph); Grid.SetColumn(row, 1); line.Children.Add(row);
            panel.Children.Add(new DashedFrame(line, "AiBorder", "AiSoft", PresentationTheme.RadiusMedium, new Thickness(14, 10)));
        }

        var columns = new AdaptiveGrid { MinItemWidth = 260, Weights = [2, 1] };
        var create = Context.IconAction("task.new", Icons.Plus, () => { host.Content = TaskEditor(project, new ProjectTask { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; },
            "primary", IconWeight.Bold);
        create.MinHeight = 30;
        var tasks = new ListCard(Context, () => T("tasks.title"), create) { Name = "TasksCard" };
        var now = DateTimeOffset.Now;
        var active = project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress).OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue).ToList();
        var closed = project.Tasks.Where(task => task.Status is ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled).ToList();
        foreach (var (key, group) in new[] { ("tasks.active", active), ("tasks.closed", closed) })
        {
            if (group.Count == 0) continue;
            tasks.AddGroup(Context, () => T(key));
            foreach (var task in group)
                tasks.Add(new TaskRow(Context, task, now,
                    () => { host.Content = TaskEditor(project, task, false, host); host.BringIntoView(); return Task.CompletedTask; },
                    async () => { task.Status = ProjectTaskStatus.Done; task.UpdatedAt = DateTimeOffset.UtcNow; await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
        }
        if (project.Tasks.Count == 0) tasks.Add(new Border { Padding = new Thickness(16, 12), Child = Label("presentation.noActiveTasks", "Body", "TextTertiary") });
        columns.Children.Add(tasks);

        var addMilestone = Context.IconAction("milestone.new", Icons.Plus, () => { host.Content = MilestoneEditor(project, new Milestone { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; },
            "square", iconOnly: true);
        addMilestone.Width = 28; addMilestone.Height = 28;
        var milestones = new SectionCard(Context, "milestones.title", addMilestone) { Name = "MilestonesCard" };
        milestones.Body.Spacing = 0; milestones.Body.Children[0].Margin = new Thickness(0, 0, 0, 6);
        if (project.Milestones.Count == 0) milestones.Body.Children.Add(Label("overview.noMilestone", "Body", "TextTertiary"));
        foreach (var milestone in project.Milestones.OrderBy(item => item.DueAt ?? DateTimeOffset.MaxValue))
        {
            var state = () => T(milestone.IsComplete ? "milestone.complete" : "milestone.open");
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            var flag = Icons.Glyph(milestone.IsComplete ? Icons.FlagCheckered : Icons.Flag, 16, milestone.IsComplete ? "Success" : "Accent", milestone.IsComplete ? IconWeight.Fill : IconWeight.Regular);
            flag.VerticalAlignment = VerticalAlignment.Top; flag.Margin = new Thickness(0, 1, 0, 0); content.Children.Add(flag);
            var labels = new StackPanel();
            labels.Children.Add(Label(() => milestone.Title, "BodyMedium", milestone.IsComplete ? "TextSecondary" : "TextPrimary"));
            labels.Children.Add(Label(() => milestone.DueAt is null ? T("date.none") : Context.ShortDate(milestone.DueAt), "Caption", "TextSecondary"));
            Grid.SetColumn(labels, 1); content.Children.Add(labels);
            var row = new ListRow(Context, content, () => $"{milestone.Title} · {state()} · {Due(milestone.DueAt)}",
                () => { host.Content = MilestoneEditor(project, milestone, false, host); host.BringIntoView(); return Task.CompletedTask; })
            { Name = "MilestoneRow", Padding = new Thickness(0, 9) };
            milestones.Body.Children.Add(Ui.Separated(row));
        }
        columns.Children.Add(milestones);
        panel.Children.Add(columns);
        return panel;
    }

    private Control TaskEditor(Project project, ProjectTask task, bool isNew, ContentControl host)
    {
        var card = new SectionCard(Context, isNew ? "task.new" : "task.edit") { Name = "TaskEditor" }; var panel = card.Body; panel.Spacing = 12;
        var title = Input(task.Title); var notes = Input(task.Description, true);
        var due = DateInput(task.DueAt); var status = Choice(task.Status);
        panel.Children.Insert(0, BackLink(project, host));
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
        var card = new SectionCard(Context, isNew ? "milestone.new" : "milestone.edit") { Name = "MilestoneEditor" }; var panel = card.Body; panel.Spacing = 12;
        var title = Input(milestone.Title); var notes = Input(milestone.Notes, true);
        var due = DateInput(milestone.DueAt); var complete = new CheckBox { IsChecked = milestone.IsComplete }; Bind(complete, control => control.Content = T("milestone.complete"));
        panel.Children.Insert(0, BackLink(project, host));
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

    private Button BackLink(Project project, ContentControl host) => Context.IconAction("tasks.back", _locale.LanguageCode == "fa" ? Icons.ArrowRight : Icons.ArrowLeft,
        () => { host.Content = TaskList(project, host); host.BringIntoView(); return Task.CompletedTask; }, "link");
}

/// <summary>Committed task row (opens the editor) with a separate quick Complete action for active tasks.</summary>
internal sealed class TaskRow : Grid
{
    public TaskRow(PresentationContext context, ProjectTask task, DateTimeOffset now, Func<Task> edit, Func<Task> complete)
    {
        Name = "TaskRow"; ColumnDefinitions = new ColumnDefinitions("*,Auto");
        var active = task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress;
        var overdue = active && task.DueAt < now;
        var (icon, weight, color) = StatusVisuals.Task(task.Status);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,84"), ColumnSpacing = 12 };
        grid.Children.Add(Icons.Glyph(icon, 17, color, weight));
        var title = context.Label(() => task.Title, "BodyMedium", active ? "TextPrimary" : "TextTertiary"); title.VerticalAlignment = VerticalAlignment.Center;
        if (task.Status == ProjectTaskStatus.Cancelled) title.TextDecorations = TextDecorations.Strikethrough;
        Grid.SetColumn(title, 1); grid.Children.Add(title);
        var pill = Ui.Pill(context, () => context.EnumText(task.Status), Tone.Neutral); Grid.SetColumn(pill, 2); grid.Children.Add(pill);
        var due = context.Label(() => task.DueAt is null ? context.Text.Get("date.none") : context.ShortDate(task.DueAt), overdue ? "CaptionMedium" : "Caption", overdue ? "Error" : "TextSecondary");
        if (overdue) due.FontWeight = FontWeight.SemiBold;
        due.TextAlignment = TextAlignment.End; due.VerticalAlignment = VerticalAlignment.Center; due.TextWrapping = TextWrapping.NoWrap; Grid.SetColumn(due, 3); grid.Children.Add(due);
        Children.Add(new ListRow(context, grid, () => $"{task.Title} · {context.EnumText(task.Status)} · {context.Due(task.DueAt)}", edit));
        if (active)
        {
            var done = context.IconAction("task.complete", Icons.Check, complete, "icon", IconWeight.Bold, iconOnly: true);
            done.Margin = new Thickness(0, 0, 10, 0); done.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(done, 1); Children.Add(done);
        }
    }
}
