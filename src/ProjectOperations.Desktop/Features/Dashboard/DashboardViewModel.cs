using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Dashboard;

/// <summary>
/// Attention home: counts, live projects with open tasks and this week's agenda. Deterministic summaries from
/// <see cref="ProjectSummaries"/> only; no agent or LLM involvement.
/// </summary>
internal sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly PageServices _services;
    private readonly DashboardSummary _dashboard;

    public DashboardViewModel(IReadOnlyList<Project> projects, PageServices services)
    {
        _services = services; L = services.Strings;
        var now = services.Clock.GetLocalNow();
        _dashboard = ProjectSummaries.Dashboard(projects, now);
        Priority = projects.Where(project => project.Status is ProjectStatus.Active or ProjectStatus.OnHold)
            .Select(project => (Project: project, Open: project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress).ToList()))
            .Where(item => item.Open.Count > 0)
            .Select(item => (item.Project, item.Open, Overdue: item.Open.Count(task => task.DueAt < now),
                Week: item.Open.Count(task => task.DueAt >= now && task.DueAt <= now.AddDays(7)),
                Next: item.Open.OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue).First()))
            .OrderByDescending(item => item.Overdue).ThenBy(item => item.Next.DueAt ?? DateTimeOffset.MaxValue)
            .Select(item => new PriorityRowViewModel(item.Project, item.Open.Count, item.Overdue, item.Week, item.Next, now, services))
            .ToList();
        Agenda = BuildAgenda(projects, now);
        RefreshOnLanguageChange(L);
    }

    public static async Task<DashboardViewModel> LoadAsync(PageServices services) => new(await services.Projects.ListAsync(), services);

    public LocalizedStrings L { get; }

    /// <summary>No projects yet: the page offers to create the first one instead of showing figures.</summary>
    public bool IsEmpty => _dashboard.Projects.Count == 0;
    public bool HasContent => !IsEmpty;

    public string OverdueText => L.Number(_dashboard.OverdueTasks.Count);
    public string OverdueToken => _dashboard.OverdueTasks.Count > 0 ? "Error" : "TextPrimary";
    public string UpcomingText => L.Number(_dashboard.UpcomingTasks.Count);
    public string MilestonesText => L.Number(_dashboard.UpcomingMilestones.Count);
    public string ActiveText => L.Number(_dashboard.Projects.Count(summary => summary.Project.Status == ProjectStatus.Active));

    /// <summary>Live projects that still have open tasks, most overdue first, each with its next task.</summary>
    public IReadOnlyList<PriorityRowViewModel> Priority { get; }
    public string PriorityCount => L.Number(Priority.Count);
    public bool HasPriority => Priority.Count > 0;
    public bool NoPriority => Priority.Count == 0;

    /// <summary>The seven days of the current week (Monday first, Saturday in Persian) with their dated work.</summary>
    public IReadOnlyList<AgendaDayViewModel> Agenda { get; }

    private IReadOnlyList<AgendaDayViewModel> BuildAgenda(IReadOnlyList<Project> projects, DateTimeOffset now)
    {
        var today = now.Date;
        var firstDay = L.LanguageCode == "fa" ? DayOfWeek.Saturday : DayOfWeek.Monday;
        var start = today.AddDays(-(((int)today.DayOfWeek - (int)firstDay + 7) % 7));
        var startInstant = new DateTimeOffset(start, _services.Clock.LocalTimeZone.GetUtcOffset(start));
        var items = ProjectSummaries.Schedule(projects, startInstant, startInstant.AddDays(7), now);
        return Enumerable.Range(0, 7).Select(offset =>
        {
            var day = start.AddDays(offset);
            return new AgendaDayViewModel(day, day == today, L, items.Where(item => item.DueAt.ToLocalTime().Date == day).Select(item => new AgendaEntryViewModel(item)).ToList());
        }).ToList();
    }

    [RelayCommand]
    private Task ToggleAssistantAsync() => _services.Host.RunAsync(() => { _services.Host.ToggleAssistant(); return Task.CompletedTask; });

    [RelayCommand]
    private Task NewProjectAsync() => _services.GoAsync(new PageRoute(AppPage.Projects, NewProject: true));
}

/// <summary>A live project with open work: its next task, how late it is, and how much is open.</summary>
internal sealed partial class PriorityRowViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectTask _next;
    private readonly int _open, _overdue, _week;
    private readonly PageServices _services;

    public PriorityRowViewModel(Project project, int open, int overdue, int week, ProjectTask next, DateTimeOffset now, PageServices services)
    {
        _project = project; _open = open; _overdue = overdue; _week = week; _next = next; _services = services; L = services.Strings;
        IsLate = next.DueAt < now;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string Name => _project.Name;
    public ProjectStage Stage => _project.Stage;

    /// <summary>"Next: Chase the deck · " — the text before the date, which is coloured on its own.</summary>
    public string NextPrefix => $"{L["v3.nextL"]}: {_next.Title} · ";
    /// <summary>The next task's date and how far away it is, or "No date".</summary>
    public string NextWhen => _next.DueAt is null ? L["date.none"] : $"{L.ShortDate(_next.DueAt)} · {L.Relative(_next.DueAt, true, _services.Clock.GetLocalNow().Date)}";
    public bool IsLate { get; }

    public bool HasOverdue => _overdue > 0;
    public string OverdueText => L.Format("v3.nOverdue", L.Number(_overdue));
    public bool HasWeek => _week > 0;
    public string WeekText => L.Format("v3.nWeek", L.Number(_week));
    public string OpenText => L.Format("v3.nOpen", L.Number(_open));

    /// <summary>The disclosure caret points along the reading direction.</summary>
    public string Caret => L.LanguageCode == "fa" ? Icons.CaretLeft : Icons.CaretRight;
    public string AccessibleName => $"{_project.Name} · {OpenText}";

    [RelayCommand]
    private Task OpenAsync() => _services.GoAsync(new ProjectRoute(_project.Id, ProjectTab.Tasks));
}

/// <summary>One day of the week's agenda.</summary>
internal sealed class AgendaDayViewModel : ViewModelBase
{
    private readonly DateTime _day;

    public AgendaDayViewModel(DateTime day, bool isToday, LocalizedStrings strings, IReadOnlyList<AgendaEntryViewModel> entries)
    {
        _day = day; IsToday = isToday; L = strings; Entries = entries;
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }
    public bool IsToday { get; }
    public IReadOnlyList<AgendaEntryViewModel> Entries { get; }
    public bool IsEmpty => Entries.Count == 0;

    public string DayName => L.LanguageCode == "fa" ? L.Culture.DateTimeFormat.GetDayName(_day.DayOfWeek) : L.Culture.DateTimeFormat.GetAbbreviatedDayName(_day.DayOfWeek);
    public string DateText => L.Dates.ShortDate(_day);
}

/// <summary>A dated task or milestone in the agenda.</summary>
internal sealed class AgendaEntryViewModel(ScheduleItem item) : ViewModelBase
{
    public string Title => item.Title;
    public string ProjectName => item.ProjectName;
    public bool IsOverdue => item.IsOverdue;
    public bool IsMilestone => item.Kind == ScheduleItemKind.Milestone;
}
