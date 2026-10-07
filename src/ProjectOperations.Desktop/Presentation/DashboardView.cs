using Avalonia;
using Avalonia.Controls;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Minimal attention home: one question, one counts line, the top priority items. Deterministic summaries only, no agent or LLM involvement.</summary>
internal sealed class DashboardView : PresentationView
{
    private const int PriorityLimit = 5;
    public DashboardView(PresentationContext context) : base(context) { Spacing = 16; }

    public async Task LoadAsync()
    {
        var dashboard = ProjectSummaries.Dashboard(await _projects.ListAsync(), DateTimeOffset.Now);
        var live = dashboard.Projects.Where(summary => summary.Project.Status is ProjectStatus.Active or ProjectStatus.OnHold).ToList();

        var intro = new StackPanel { Spacing = 4 };
        intro.Children.Add(Label("dashboard.title", "HeadingLarge"));
        if (dashboard.Projects.Count == 0)
        {
            intro.Children.Add(Label("dashboard.empty", "Body", "TextSecondary"));
            Children.Add(intro);
            return;
        }
        intro.Children.Add(Label(() => F("dashboard.counts", dashboard.OverdueTasks.Count, dashboard.UpcomingTasks.Count, dashboard.UpcomingMilestones.Count), "Body", "TextSecondary"));
        Children.Add(intro);
        Children.Add(PriorityCard(dashboard, live));
    }

    private SectionCard PriorityCard(DashboardSummary dashboard, List<ProjectSummary> live)
    {
        var card = new SectionCard(Context, "presentation.priority") { Name = "PriorityItems" };
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

    private static string Detail(string note, string fallback) => string.IsNullOrWhiteSpace(note) ? fallback : note.Trim();
    private static Control Divider() => new Border { Height = 1, Background = PresentationTheme.Brush("BorderSubtle"), Margin = new Thickness(0, 2) };
}
