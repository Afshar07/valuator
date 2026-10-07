using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Attention home: counts, the top priority items and this week's agenda. Deterministic summaries only, no agent or LLM involvement.</summary>
internal sealed class DashboardView : PresentationView
{
    private const int PriorityLimit = 5;
    public DashboardView(PresentationContext context) : base(context) { }

    public async Task LoadAsync()
    {
        var now = DateTimeOffset.Now;
        var projects = await _projects.ListAsync();
        var dashboard = ProjectSummaries.Dashboard(projects, now);
        var live = dashboard.Projects.Where(summary => summary.Project.Status is ProjectStatus.Active or ProjectStatus.OnHold).ToList();

        Children.Add(PageHeader("dashboard.title", "dashboard.subtitle", AssistantButton()));
        if (dashboard.Projects.Count == 0)
        {
            var empty = Ui.Card(Column(12));
            var body = (StackPanel)empty.Child!;
            body.Children.Add(Label("dashboard.empty", "Body", "TextSecondary"));
            body.Children.Add(Context.IconAction("project.new", Icons.Plus, () => Context.Shell.NavigateAsync("projects:new"), "primary", IconWeight.Bold));
            Children.Add(empty);
            return;
        }

        var stats = new AdaptiveGrid { MinItemWidth = 170, Gap = 12, StretchRows = true };
        stats.Children.Add(new StatCard(Context, "DashboardOverdueStat", "presentation.stat.overdue", dashboard.OverdueTasks.Count, Icons.WarningCircle, "Error", "Error"));
        stats.Children.Add(new StatCard(Context, "DashboardUpcomingStat", "presentation.stat.upcoming", dashboard.UpcomingTasks.Count, Icons.Clock, "Warning"));
        stats.Children.Add(new StatCard(Context, "DashboardMilestonesStat", "presentation.stat.milestones", dashboard.UpcomingMilestones.Count, Icons.Flag, "Accent"));
        stats.Children.Add(new StatCard(Context, "DashboardActiveStat", "presentation.stat.active", dashboard.Projects.Count(summary => summary.Project.Status == ProjectStatus.Active), Icons.Briefcase, "TextSecondary"));
        Children.Add(stats);

        var columns = new AdaptiveGrid { MinItemWidth = 320 };
        columns.Children.Add(PriorityCard(dashboard, live, now));
        columns.Children.Add(AgendaCard(projects, now));
        Children.Add(columns);
    }

    private Control PriorityCard(DashboardSummary dashboard, List<ProjectSummary> live, DateTimeOffset now)
    {
        var card = new ListCard(Context, () => T("presentation.priority")) { Name = "PriorityItems" };
        var rows = new List<Control>();
        foreach (var item in dashboard.OverdueTasks)
            rows.Add(Row(item.ProjectName, () => item.Task.Title, () => T("presentation.pill.overdue"), Tone.Error, () => LateText(item.Task.DueAt!.Value, now), "Error",
                () => F("dashboard.overdue", item.ProjectName, item.Task.Title, Due(item.Task.DueAt)), () => OpenProjectAsync(item.ProjectId, 2)));
        foreach (var item in dashboard.UpcomingTasks)
            rows.Add(Row(item.ProjectName, () => item.Task.Title, () => T("presentation.pill.upcoming"), Tone.Warning, () => DayTime(item.Task.DueAt!.Value), "TextSecondary",
                () => F("dashboard.soon", item.ProjectName, item.Task.Title, Due(item.Task.DueAt)), () => OpenProjectAsync(item.ProjectId, 2)));
        foreach (var item in dashboard.UpcomingMilestones)
            rows.Add(Row(item.ProjectName, () => item.Milestone.Title, () => T("presentation.pill.milestone"), Tone.Accent, () => Day(item.Milestone.DueAt!.Value), "TextSecondary",
                () => F("dashboard.milestone", item.ProjectName, item.Milestone.Title, Due(item.Milestone.DueAt)), () => OpenProjectAsync(item.ProjectId, 2)));
        foreach (var status in new[] { RequirementStatus.NeedsReview, RequirementStatus.Missing })
            foreach (var summary in live)
                foreach (var requirement in summary.Project.Requirements.Where(item => item.Status == status))
                {
                    var project = summary.Project; var captured = requirement; var state = status;
                    rows.Add(Row(project.Name, () => RequirementTitle(captured), () => EnumText(state), StatusVisuals.Requirement(state).Tone, () => "", "TextSecondary",
                        () => $"{project.Name} · {RequirementTitle(captured)} · {EnumText(state)}", () => OpenProjectAsync(project.Id, 1)));
                }
        if (rows.Count == 0) card.Add(new Border { Padding = new Thickness(16, 12), Child = Label("presentation.priority.empty", "Body", "TextSecondary") });
        foreach (var row in rows.Take(PriorityLimit)) card.Add(row);
        return card;
    }

    private ListRow Row(string projectName, Func<string> title, Func<string> pill, Tone tone, Func<string> date, string dateColor, Func<string> accessibleName, Func<Task> action)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        var pillHost = new Panel { MinWidth = 78, VerticalAlignment = VerticalAlignment.Center }; pillHost.Children.Add(Ui.Pill(Context, pill, tone)); grid.Children.Add(pillHost);
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(Label(title, "BodyMedium")); labels.Children.Add(Label(() => projectName, "Caption", "TextSecondary"));
        Grid.SetColumn(labels, 1); grid.Children.Add(labels);
        var when = Label(date, "Caption", dateColor); when.TextWrapping = TextWrapping.NoWrap; when.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(when, 2); grid.Children.Add(when);
        return new ListRow(Context, grid, accessibleName, action) { Name = "PriorityRow", Padding = new Thickness(16, 11) };
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
            var number = Label(() => day.ToString("MMM d", _locale.Culture), "BodyStrong", isToday ? "Accent" : "TextSecondary"); number.FontSize = 15; number.TextWrapping = TextWrapping.NoWrap; date.Children.Add(number);
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

    private string LateText(DateTimeOffset due, DateTimeOffset now)
    {
        var days = Math.Max(1, (int)Math.Floor((now - due).TotalDays));
        return days == 1 ? F("presentation.dueLateOne", Context.ShortDate(due)) : F("presentation.dueLate", Context.ShortDate(due), days);
    }
    private string Day(DateTimeOffset date) => date.ToLocalTime().ToString("ddd MMM d", _locale.Culture);
    private string DayTime(DateTimeOffset date) => date.ToLocalTime().TimeOfDay == TimeSpan.Zero ? Day(date) : date.ToLocalTime().ToString("ddd MMM d, HH:mm", _locale.Culture);
}
