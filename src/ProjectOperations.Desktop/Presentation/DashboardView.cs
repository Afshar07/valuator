using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop;

/// <summary>Attention home: counts, projects with open work and this week's agenda. Deterministic summaries only, no agent or LLM involvement.</summary>
internal sealed class DashboardView : PresentationView
{
    public DashboardView(PresentationContext context) : base(context) { }

    public async Task LoadAsync()
    {
        var now = DateTimeOffset.Now;
        var projects = await _projects.ListAsync();
        var dashboard = ProjectSummaries.Dashboard(projects, now);

        Children.Add(PageHeader("dashboard.title", "dashboard.subtitle", AssistantButton()));
        if (dashboard.Projects.Count == 0)
        {
            var empty = Ui.Card(Column(12));
            var body = (StackPanel)empty.Child!;
            body.Children.Add(Label("dashboard.empty", "Body", "TextSecondary"));
            body.Children.Add(Context.IconAction("project.new", Icons.Plus, () => Context.Shell.NavigateAsync(AppPage.Projects, newProject: true), "primary", IconWeight.Bold));
            Children.Add(empty);
            return;
        }

        var stats = new AdaptiveGrid { MinItemWidth = 170, Gap = 12, StretchRows = true };
        stats.Children.Add(new StatCard(Context, "DashboardOverdueStat", "presentation.stat.overdue", dashboard.OverdueTasks.Count, Icons.WarningCircle, "Error", dashboard.OverdueTasks.Count > 0 ? "Error" : "TextPrimary"));
        stats.Children.Add(new StatCard(Context, "DashboardUpcomingStat", "presentation.stat.upcoming", dashboard.UpcomingTasks.Count, Icons.Clock, "Warning"));
        stats.Children.Add(new StatCard(Context, "DashboardMilestonesStat", "presentation.stat.milestones", dashboard.UpcomingMilestones.Count, Icons.Flag, "Accent"));
        stats.Children.Add(new StatCard(Context, "DashboardActiveStat", "presentation.stat.active", dashboard.Projects.Count(summary => summary.Project.Status == ProjectStatus.Active), Icons.Briefcase, "TextSecondary"));
        Children.Add(stats);

        var columns = new AdaptiveGrid { MinItemWidth = 320 };
        columns.Children.Add(PriorityCard(projects, now));
        columns.Children.Add(AgendaCard(projects, now));
        Children.Add(columns);
    }

    /// <summary>Live projects that still have open tasks, most overdue first, each with its next task.</summary>
    private Control PriorityCard(IReadOnlyList<Project> projects, DateTimeOffset now)
    {
        var rows = projects.Where(project => project.Status is ProjectStatus.Active or ProjectStatus.OnHold)
            .Select(project => (Project: project, Open: project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress).ToList()))
            .Where(item => item.Open.Count > 0)
            .Select(item => (item.Project, item.Open, Overdue: item.Open.Count(task => task.DueAt < now),
                Week: item.Open.Count(task => task.DueAt >= now && task.DueAt <= now.AddDays(7)),
                Next: item.Open.OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue).First()))
            .OrderByDescending(item => item.Overdue).ThenBy(item => item.Next.DueAt ?? DateTimeOffset.MaxValue).ToList();
        var card = new ListCard(Context, () => T("v3.projOpen"), Label(() => N(rows.Count), "Caption", "TextSecondary")) { Name = "PriorityItems" };
        if (rows.Count == 0) card.Add(new Border { Padding = new Thickness(16, 14), Child = Label("v3.noOpen", "Body", "TextSecondary") });
        foreach (var item in rows) card.Add(ProjectRow(item.Project, item.Open.Count, item.Overdue, item.Week, item.Next, now));
        return card;
    }

    private ListRow ProjectRow(Project project, int open, int overdue, int week, ProjectTask next, DateTimeOffset now)
    {
        var late = next.DueAt < now;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(Ui.Initial(project.Name, 32));
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
        var title = new WrapPanel { ItemSpacing = 8 };
        title.Children.Add(Label(() => project.Name, "BodyStrong"));
        title.Children.Add(Ui.StagePill(Context, project.Stage));
        labels.Children.Add(title);
        var line = Label(() => "", "Small", "TextSecondary"); line.TextWrapping = TextWrapping.NoWrap; line.TextTrimming = TextTrimming.CharacterEllipsis;
        Bind(line, control =>
        {
            var date = next.DueAt is null ? T("date.none") : $"{Context.ShortDate(next.DueAt)} · {Context.Relative(next.DueAt, true)}";
            var when = new Avalonia.Controls.Documents.Run(date) { FontWeight = late ? FontWeight.SemiBold : FontWeight.Normal };
            when.Paint(Avalonia.Controls.Documents.TextElement.ForegroundProperty, late ? "Error" : "TextSecondary");
            control.Inlines = [new Avalonia.Controls.Documents.Run($"{T("v3.nextL")}: {next.Title} · "), when];
        });
        labels.Children.Add(line);
        Grid.SetColumn(labels, 1); grid.Children.Add(labels);
        var pills = new WrapPanel { ItemSpacing = 4, LineSpacing = 4, MaxWidth = 190, VerticalAlignment = VerticalAlignment.Center };
        if (overdue > 0) pills.Children.Add(Ui.Pill(Context, () => F("v3.nOverdue", N(overdue)), Tone.Error));
        if (week > 0) pills.Children.Add(Ui.Pill(Context, () => F("v3.nWeek", N(week)), Tone.Warning));
        pills.Children.Add(Ui.Pill(Context, () => F("v3.nOpen", N(open)), Tone.Neutral));
        Grid.SetColumn(pills, 2); grid.Children.Add(pills);
        var caret = Icons.Glyph(Icons.CaretRight, 14, "TextTertiary");
        Bind(caret, control => control.Text = _locale.LanguageCode == "fa" ? Icons.CaretLeft : Icons.CaretRight);
        Grid.SetColumn(caret, 3); grid.Children.Add(caret);
        return new ListRow(Context, grid, () => $"{project.Name} · {F("v3.nOpen", N(open))}", () => OpenProjectAsync(project.Id, ProjectTab.Tasks)) { Name = "PriorityRow", Padding = new Thickness(16, 12) };
    }

    private Control AgendaCard(IReadOnlyList<Project> projects, DateTimeOffset now)
    {
        var card = new SectionCard(Context, "presentation.agenda") { Name = "AgendaCard" };
        card.Body.Spacing = 0; card.Body.Children[0].Margin = new Thickness(0, 0, 0, 6);
        var today = now.ToLocalTime().Date;
        var firstDay = _locale.LanguageCode == "fa" ? DayOfWeek.Saturday : DayOfWeek.Monday;
        var start = today.AddDays(-(((int)today.DayOfWeek - (int)firstDay + 7) % 7));
        var startInstant = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start));
        var items = ProjectSummaries.Schedule(projects, startInstant, startInstant.AddDays(7), now);
        for (var offset = 0; offset < 7; offset++)
        {
            var day = start.AddDays(offset); var isToday = day == today;
            var entries = items.Where(item => item.DueAt.ToLocalTime().Date == day).ToList();
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("60,*"), ColumnSpacing = 12 };
            var date = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            date.Children.Add(Label(() => _locale.LanguageCode == "fa" ? _locale.Culture.DateTimeFormat.GetDayName(day.DayOfWeek) : _locale.Culture.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek),
                "Micro", isToday ? "Accent" : "TextSecondary"));
            var number = Label(() => Context.ShortDate(day), "BodyStrong", isToday ? "Accent" : "TextSecondary"); number.FontSize = 15; number.TextWrapping = TextWrapping.NoWrap; date.Children.Add(number);
            row.Children.Add(date);
            var list = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            if (entries.Count == 0) list.Children.Add(Label(() => "—", "Body", "TextTertiary"));
            foreach (var entry in entries)
            {
                var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 8 };
                line.Children.Add(Ui.Dot(6, entry.IsOverdue ? "Error" : entry.Kind == ScheduleItemKind.Milestone ? "Accent" : "TextTertiary", entry.Kind == ScheduleItemKind.Milestone));
                var title = Label(() => entry.Title, "Body"); title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextWrapping = TextWrapping.NoWrap; Grid.SetColumn(title, 1); line.Children.Add(title);
                var project = Label(() => entry.ProjectName, "Caption", "TextTertiary"); project.TextTrimming = TextTrimming.CharacterEllipsis; project.TextWrapping = TextWrapping.NoWrap; project.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(project, 2); line.Children.Add(project);
                list.Children.Add(line);
            }
            Grid.SetColumn(list, 1); row.Children.Add(list);
            card.Body.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 8), Child = row }));
        }
        return card;
    }
}
