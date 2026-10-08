using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Tasks & dates tab: open tasks grouped by urgency, milestones, and a pointer to proposals still awaiting review. Editing happens in dialogs.</summary>
internal sealed class TasksView : PresentationView
{
    private readonly IReadOnlyList<AgentJob> _jobs;
    private readonly Project _project;

    public TasksView(PresentationContext context, Project project, IReadOnlyList<AgentJob> jobs) : base(context)
    {
        _jobs = jobs; _project = project; Spacing = 16;
        Build();
    }

    private void Build()
    {
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
            Children.Add(new DashedFrame(line, "AiBorder", "AiSoft", PresentationTheme.RadiusMedium, new Thickness(14, 10)));
        }

        var columns = new AdaptiveGrid { MinItemWidth = 260, Weights = [2, 1] };
        columns.Children.Add(TasksCard());
        columns.Children.Add(MilestonesCard());
        Children.Add(columns);
    }

    private Control TasksCard()
    {
        var create = Context.IconAction("task.new", Icons.Plus, () => { Context.Shell.ShowModal(new TaskDialog(Context, _project, new ProjectTask { ProjectId = _project.Id, DueAt = null }, true)); return Task.CompletedTask; }, "primary", IconWeight.Bold);
        create.Name = "NewTaskButton"; create.MinHeight = 30;
        var card = new ListCard(Context, () => T("tabs.tasks"), create) { Name = "TasksCard" };
        if (_project.Tasks.Count == 0)
        {
            var empty = new StackPanel { Spacing = 8, Margin = new Thickness(16, 20, 16, 22) };
            var icon = Icons.Glyph(Icons.CheckSquareOffset, 24, "TextTertiary"); icon.HorizontalAlignment = HorizontalAlignment.Left;
            empty.Children.Add(icon); empty.Children.Add(Label("v3.tasksEmpty", "Body", "TextSecondary"));
            card.Add(empty);
            return card;
        }
        var now = DateTimeOffset.Now;
        var open = _project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress).OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue).ToList();
        var groups = new (string Key, string Color, List<ProjectTask> Items)[]
        {
            ("v3.grpOverdue", "Error", open.Where(task => task.DueAt < now).ToList()),
            ("v3.grpWeek", "TextSecondary", open.Where(task => task.DueAt >= now && task.DueAt <= now.AddDays(7)).ToList()),
            ("v3.grpLater", "TextSecondary", open.Where(task => task.DueAt > now.AddDays(7)).ToList()),
            ("v3.grpNoDate", "TextSecondary", open.Where(task => task.DueAt is null).ToList()),
            ("tasks.closed", "TextTertiary", _project.Tasks.Where(task => task.Status is ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled).OrderByDescending(task => task.DueAt ?? DateTimeOffset.MinValue).ToList())
        };
        foreach (var (key, color, items) in groups)
        {
            if (items.Count == 0) continue;
            card.AddGroup(Context, () => T(key), () => N(items.Count), color);
            foreach (var task in items) card.Add(new TaskRow(Context, _project, task));
        }
        return card;
    }

    private Control MilestonesCard()
    {
        var add = Context.IconAction("v3.newMs", Icons.Plus, () => { Context.Shell.ShowModal(new MilestoneDialog(Context, _project, new Milestone { ProjectId = _project.Id }, true)); return Task.CompletedTask; });
        add.Name = "NewMilestoneButton"; add.MinHeight = 30;
        var card = new SectionCard(Context, "milestones.title", add) { Name = "MilestonesCard" };
        card.Body.Spacing = 0; card.Body.Children[0].Margin = new Thickness(0, 0, 0, 6);
        if (_project.Milestones.Count == 0)
            card.Body.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 10, 0, 8), Child = Label("v3.msEmpty", "Small", "TextSecondary") }));
        foreach (var milestone in _project.Milestones.OrderBy(item => item.DueAt ?? DateTimeOffset.MaxValue))
        {
            var state = () => T(milestone.IsComplete ? "milestone.complete" : "milestone.open");
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            var flag = Icons.Glyph(milestone.IsComplete ? Icons.FlagCheckered : Icons.Flag, 16, milestone.IsComplete ? "Success" : "Accent", milestone.IsComplete ? IconWeight.Fill : IconWeight.Regular);
            flag.VerticalAlignment = VerticalAlignment.Top; flag.Margin = new Thickness(0, 2, 0, 0); content.Children.Add(flag);
            var labels = new StackPanel();
            labels.Children.Add(Label(() => milestone.Title, "BodyMedium", milestone.IsComplete ? "TextSecondary" : "TextPrimary"));
            var when = Label(() => "", "Caption", "TextSecondary");
            Bind(when, control =>
            {
                var rel = new Avalonia.Controls.Documents.Run(milestone.IsComplete ? T("v3.reached") : Context.Relative(milestone.DueAt, false));
                rel.Paint(Avalonia.Controls.Documents.TextElement.ForegroundProperty, milestone.IsComplete ? "Success" : "TextSecondary");
                control.Inlines = milestone.DueAt is null ? [new Avalonia.Controls.Documents.Run(T("date.none"))] : [new Avalonia.Controls.Documents.Run(Context.ShortDate(milestone.DueAt) + " · "), rel];
            });
            labels.Children.Add(when);
            Grid.SetColumn(labels, 1); content.Children.Add(labels);
            var row = new ListRow(Context, content, () => $"{milestone.Title} · {state()} · {Due(milestone.DueAt)}",
                () => { Context.Shell.ShowModal(new MilestoneDialog(Context, _project, milestone, false)); return Task.CompletedTask; })
            { Name = "MilestoneRow", Padding = new Thickness(6, 9) };
            card.Body.Children.Add(Ui.Separated(row));
        }
        return card;
    }
}

/// <summary>Task row: a checkbox-style button toggles done/open; the rest of the row opens the edit dialog.</summary>
internal sealed class TaskRow : Grid
{
    public TaskRow(PresentationContext context, Project project, ProjectTask task)
    {
        Name = "TaskRow"; ColumnDefinitions = new ColumnDefinitions("Auto,*"); Margin = new Thickness(8, 0, 10, 0);
        var active = task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress;
        var overdue = active && task.DueAt < DateTimeOffset.Now;
        var (icon, weight, color) = StatusVisuals.Task(task.Status);

        var toggle = new Button { Width = 32, Height = 32, MinHeight = 0, Padding = new Thickness(0), CornerRadius = new CornerRadius(7), Name = "TaskToggle" };
        toggle.Classes.Add("icon");
        toggle.Content = Icons.Glyph(icon, 18, color, weight);
        context.Localized.Bind(toggle, control => { Avalonia.Automation.AutomationProperties.SetName(control, context.Text.Get(active ? "task.complete" : "v3.reopen")); ToolTip.SetTip(control, context.Text.Get(active ? "task.complete" : "v3.reopen")); });
        toggle.Click += async (_, _) => await context.ActAsync(toggle, async () =>
        {
            task.Status = active ? ProjectTaskStatus.Done : ProjectTaskStatus.Todo; task.UpdatedAt = DateTimeOffset.UtcNow;
            await context.Projects.SaveAsync(project); await context.Shell.RefreshProjectAsync();
        });
        Children.Add(toggle);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,100"), ColumnSpacing = 12 };
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var title = context.Label(() => task.Title, "BodyMedium", active ? "TextPrimary" : "TextTertiary");
        if (task.Status == ProjectTaskStatus.Cancelled) title.TextDecorations = TextDecorations.Strikethrough;
        text.Children.Add(title);
        var requirement = task.RequirementId is { } id ? project.Requirements.FirstOrDefault(item => item.Id == id) : null;
        if (requirement is not null)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            line.Children.Add(Icons.Glyph(Icons.ListChecks, 12, "TextTertiary"));
            line.Children.Add(context.Label(() => new Localization.DomainDisplay(context.Text).Requirement(requirement), "Meta", "TextTertiary"));
            text.Children.Add(line);
        }
        grid.Children.Add(text);
        if (task.Status == ProjectTaskStatus.InProgress)
        {
            var pill = Ui.Pill(context, () => context.EnumText(task.Status), Tone.Accent); Grid.SetColumn(pill, 1); grid.Children.Add(pill);
        }
        var dates = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        var due = context.Label(() => task.DueAt is null ? "—" : context.ShortDate(task.DueAt), overdue ? "CaptionMedium" : "Caption", overdue ? "Error" : active ? "TextPrimary" : "TextTertiary");
        if (overdue) due.FontWeight = FontWeight.SemiBold;
        due.TextAlignment = TextAlignment.End; due.TextWrapping = TextWrapping.NoWrap; dates.Children.Add(due);
        var relative = context.Label(() => active ? context.Relative(task.DueAt, true) : context.EnumText(task.Status), "Micro", overdue ? "Error" : "TextTertiary");
        relative.FontWeight = FontWeight.Normal; relative.TextAlignment = TextAlignment.End; relative.TextWrapping = TextWrapping.NoWrap; dates.Children.Add(relative);
        Grid.SetColumn(dates, 2); grid.Children.Add(dates);

        var edit = new ListRow(context, grid, () => $"{task.Title} · {context.EnumText(task.Status)} · {context.Due(task.DueAt)}",
            () => { context.Shell.ShowModal(new TaskDialog(context, project, task, false)); return Task.CompletedTask; })
        { Padding = new Thickness(6, 10) };
        Grid.SetColumn(edit, 1); Children.Add(edit);

    }
}
