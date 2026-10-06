using Avalonia;
using Avalonia.Controls;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Attention dashboard: deterministic summaries only, no agent or LLM involvement.</summary>
internal sealed class DashboardView : PresentationView
{
    private const int PriorityLimit = 8;
    private readonly Func<Task> _showProjects;
    public DashboardView(PresentationContext context, Func<Task> showProjects) : base(context) { _showProjects = showProjects; Spacing = 24; }

    public async Task LoadAsync()
    {
        var dashboard = ProjectSummaries.Dashboard(await _projects.ListAsync(), DateTimeOffset.Now);
        var live = dashboard.Projects.Where(summary => summary.Project.Status is ProjectStatus.Active or ProjectStatus.OnHold).ToList();
        var missing = live.Sum(summary => summary.Project.Requirements.Count(item => item.Status == RequirementStatus.Missing));
        var needsReview = live.Sum(summary => summary.Project.Requirements.Count(item => item.Status == RequirementStatus.NeedsReview));
        var onHold = dashboard.Projects.Count(summary => summary.Project.Status == ProjectStatus.OnHold);
        var active = dashboard.Projects.Count(summary => summary.Project.Status == ProjectStatus.Active);

        var intro = new StackPanel { Spacing = 8 };
        intro.Children.Add(Label("dashboard.title", "Hero")); intro.Children.Add(Label("app.subtitle", "Body", "TextSecondary"));
        Children.Add(intro);

        var stats = new AdaptiveGrid { MinItemWidth = 200 };
        stats.Children.Add(new StatCard(Context, "DashboardOverdueStat", () => T("presentation.stat.overdue"), dashboard.OverdueTasks.Count, "Error",
            () => F("presentation.stat.overdueHint", dashboard.UpcomingTasks.Count)));
        stats.Children.Add(new StatCard(Context, "DashboardMissingStat", () => T("presentation.stat.missing"), missing, "Warning",
            () => F("presentation.stat.missingHint", needsReview)));
        stats.Children.Add(new StatCard(Context, "DashboardMilestonesStat", () => T("presentation.stat.milestones"), dashboard.UpcomingMilestones.Count, "Info",
            () => T("presentation.stat.milestonesHint")));
        stats.Children.Add(new StatCard(Context, "DashboardActiveStat", () => T("presentation.stat.active"), active, "Success",
            () => F("presentation.stat.activeHint", onHold)));
        Children.Add(stats);

        if (dashboard.Projects.Count == 0)
        {
            var empty = new SectionCard(Context, "dashboard.recentProjects");
            empty.Body.Children.Add(Label("dashboard.empty", "Body", "TextSecondary")); Children.Add(empty);
            return;
        }

        Children.Add(PriorityCard(dashboard, live));
        var lower = new AdaptiveGrid { MinItemWidth = 340 };
        lower.Children.Add(RecentCard(dashboard));
        lower.Children.Add(ReadinessCard(dashboard));
        Children.Add(lower);
    }

    private SectionCard PriorityCard(DashboardSummary dashboard, List<ProjectSummary> live)
    {
        var card = new SectionCard(Context, "presentation.priority") { Name = "PriorityItems" };
        card.Body.Children.Add(Label(() => F("dashboard.counts", dashboard.OverdueTasks.Count, dashboard.UpcomingTasks.Count, dashboard.UpcomingMilestones.Count), "Caption", "TextSecondary"));
        var rows = new List<Control>();
        foreach (var item in dashboard.OverdueTasks)
            rows.Add(new ProjectRow(Context, item.ProjectName, () => item.Task.Title, () => Detail(item.Task.Description, item.ProjectName),
                () => T("presentation.pill.overdue"), "Error", () => Context.ShortDate(item.Task.DueAt),
                () => F("dashboard.overdue", item.ProjectName, item.Task.Title, Due(item.Task.DueAt)), () => OpenProjectAsync(item.ProjectId, 2)));
        foreach (var item in dashboard.UpcomingTasks)
            rows.Add(new ProjectRow(Context, item.ProjectName, () => item.Task.Title, () => Detail(item.Task.Description, item.ProjectName),
                () => T("presentation.pill.upcoming"), "BrandPrimary", () => Context.ShortDate(item.Task.DueAt),
                () => F("dashboard.soon", item.ProjectName, item.Task.Title, Due(item.Task.DueAt)), () => OpenProjectAsync(item.ProjectId, 2)));
        foreach (var item in dashboard.UpcomingMilestones)
            rows.Add(new ProjectRow(Context, item.ProjectName, () => item.Milestone.Title, () => Detail(item.Milestone.Notes, item.ProjectName),
                () => T("presentation.pill.milestone"), "Info", () => Context.ShortDate(item.Milestone.DueAt),
                () => F("dashboard.milestone", item.ProjectName, item.Milestone.Title, Due(item.Milestone.DueAt)), () => OpenProjectAsync(item.ProjectId)));
        foreach (var status in new[] { RequirementStatus.NeedsReview, RequirementStatus.Missing })
            foreach (var summary in live)
                foreach (var requirement in summary.Project.Requirements.Where(item => item.Status == status))
                {
                    var project = summary.Project; var captured = requirement; var state = status;
                    rows.Add(new ProjectRow(Context, project.Name, () => RequirementTitle(captured), () => project.Name,
                        () => EnumText(state), StatusPill.Tone(state), () => "",
                        () => $"{project.Name} · {RequirementTitle(captured)} · {EnumText(state)}", () => OpenProjectAsync(project.Id, 1)));
                }
        if (rows.Count == 0) card.Body.Children.Add(Label("presentation.priority.empty", "Body", "TextSecondary"));
        var list = new StackPanel { Spacing = 0 };
        foreach (var row in rows.Take(PriorityLimit))
        {
            if (list.Children.Count > 0) list.Children.Add(Divider());
            list.Children.Add(row);
        }
        card.Body.Children.Add(list);
        return card;
    }

    private SectionCard RecentCard(DashboardSummary dashboard)
    {
        var card = new SectionCard(Context, "dashboard.recentProjects", SectionCard.Link(Context, "presentation.viewAll", _showProjects));
        var list = new StackPanel { Spacing = 0 };
        var summaries = dashboard.Projects.ToDictionary(summary => summary.Project.Id);
        foreach (var project in dashboard.RecentProjects.Take(5))
        {
            if (list.Children.Count > 0) list.Children.Add(Divider());
            var summary = summaries[project.Id];
            list.Children.Add(new ProjectRow(Context, project, () => OpenProjectAsync(project.Id),
                () => summary.CompletionPercentage.ToString("0", _locale.Culture) + "%"));
        }
        card.Body.Children.Add(list); return card;
    }

    private SectionCard ReadinessCard(DashboardSummary dashboard)
    {
        var card = new SectionCard(Context, "dashboard.projectReadiness");
        var list = new StackPanel { Spacing = 0 };
        foreach (var summary in dashboard.Projects.Take(6))
        {
            if (list.Children.Count > 0) list.Children.Add(Divider());
            list.Children.Add(new ReadinessRow(Context, summary, () => OpenProjectAsync(summary.Project.Id)));
        }
        card.Body.Children.Add(list); return card;
    }

    private static string Detail(string note, string fallback) => string.IsNullOrWhiteSpace(note) ? fallback : note.Trim();
    private static Control Divider() => new Border { Height = 1, Background = PresentationTheme.Brush("BorderSubtle"), Margin = new Thickness(0, 2) };
}
