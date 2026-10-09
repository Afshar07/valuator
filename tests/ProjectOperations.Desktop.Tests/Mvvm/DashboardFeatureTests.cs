using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Dashboard;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The attention home as plain view-models: no window, no controls, a clock fixed at noon UTC on Friday 9 October 2026.</summary>
public sealed class DashboardFeatureTests
{
    private static readonly DateTimeOffset Noon = PageScenario.Noon;

    [Fact]
    public async Task With_no_projects_the_page_offers_to_create_the_first_one()
    {
        using var scenario = await PageScenario.CreateAsync();
        var dashboard = await DashboardViewModel.LoadAsync(scenario.Services);

        Assert.True(dashboard.IsEmpty);
        Assert.False(dashboard.HasContent);
        Assert.Empty(dashboard.Priority);

        await dashboard.NewProjectCommand.ExecuteAsync(null);

        Assert.Equal(new PageRoute(AppPage.Projects, NewProject: true), scenario.Navigator.Current);
        await dashboard.ToggleAssistantCommand.ExecuteAsync(null);
        Assert.Equal(1, scenario.Host.AssistantToggles);
    }

    [Fact]
    public async Task Counts_come_from_stored_state_and_leave_out_closed_work_and_finished_projects()
    {
        using var scenario = await PageScenario.CreateAsync();
        var active = await scenario.AddProjectAsync("Active");
        active.Tasks.AddRange([PageScenario.Task(active, "Overdue", Noon.AddDays(-2)), PageScenario.Task(active, "Done", Noon.AddDays(-2), ProjectTaskStatus.Done),
            PageScenario.Task(active, "Soon", Noon.AddDays(3))]);
        active.Milestones.Add(new Milestone { ProjectId = active.Id, Title = "IC", DueAt = Noon.AddDays(2) });
        await scenario.Projects.SaveAsync(active);
        var finished = await scenario.AddProjectAsync("Finished", status: ProjectStatus.Completed);
        finished.Tasks.Add(PageScenario.Task(finished, "Excluded", Noon.AddDays(-2)));
        await scenario.Projects.SaveAsync(finished);

        var dashboard = await DashboardViewModel.LoadAsync(scenario.Services);

        Assert.True(dashboard.HasContent);
        Assert.Equal("1", dashboard.OverdueText);
        Assert.Equal("Error", dashboard.OverdueToken);
        Assert.Equal("1", dashboard.UpcomingText);
        Assert.Equal("1", dashboard.MilestonesText);
        Assert.Equal("1", dashboard.ActiveText);
        Assert.Equal(["Active"], dashboard.Priority.Select(row => row.Name));
    }

    [Fact]
    public async Task The_overdue_count_is_only_red_when_something_is_overdue()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Calm");
        project.Tasks.Add(PageScenario.Task(project, "Later", Noon.AddDays(30)));
        await scenario.Projects.SaveAsync(project);

        var dashboard = await DashboardViewModel.LoadAsync(scenario.Services);

        Assert.Equal("0", dashboard.OverdueText);
        Assert.Equal("TextPrimary", dashboard.OverdueToken);
    }

    [Fact]
    public async Task Projects_with_open_tasks_are_ordered_by_how_late_they_are_and_describe_their_next_task()
    {
        using var scenario = await PageScenario.CreateAsync();
        var calm = await scenario.AddProjectAsync("Calm");
        calm.Tasks.Add(PageScenario.Task(calm, "Send NDA", Noon.AddDays(3), ProjectTaskStatus.InProgress));
        await scenario.Projects.SaveAsync(calm);
        var late = await scenario.AddProjectAsync("Late");
        late.Tasks.AddRange([PageScenario.Task(late, "Chase deck", Noon.AddDays(-3)), PageScenario.Task(late, "Chase model", Noon.AddDays(-1)),
            PageScenario.Task(late, "Review", Noon.AddDays(2)), PageScenario.Task(late, "Closed", Noon.AddDays(-9), ProjectTaskStatus.Done)]);
        await scenario.Projects.SaveAsync(late);
        var idle = await scenario.AddProjectAsync("No open tasks");

        var dashboard = await DashboardViewModel.LoadAsync(scenario.Services);
        var L = scenario.Strings;

        Assert.Equal(["Late", "Calm"], dashboard.Priority.Select(row => row.Name));
        Assert.DoesNotContain(dashboard.Priority, row => row.Name == idle.Name);
        Assert.Equal("2", dashboard.PriorityCount);
        Assert.True(dashboard.HasPriority);
        Assert.False(dashboard.NoPriority);

        var first = dashboard.Priority[0];
        Assert.True(first.IsLate);
        Assert.True(first.HasOverdue);
        Assert.True(first.HasWeek);
        Assert.Equal(L.Format("v3.nOverdue", "2"), first.OverdueText);
        Assert.Equal(L.Format("v3.nWeek", "1"), first.WeekText);
        Assert.Equal(L.Format("v3.nOpen", "3"), first.OpenText);
        Assert.Equal($"{L["v3.nextL"]}: Chase deck · ", first.NextPrefix);
        Assert.Equal($"{L.ShortDate(Noon.AddDays(-3))} · {L.Relative(Noon.AddDays(-3), true, Noon.Date)}", first.NextWhen);
        Assert.Equal($"Late · {first.OpenText}", first.AccessibleName);

        var second = dashboard.Priority[1];
        Assert.False(second.IsLate);
        Assert.False(second.HasOverdue);
        Assert.True(second.HasWeek);
    }

    [Fact]
    public async Task A_next_task_without_a_date_reads_no_date_and_opening_a_row_goes_to_its_tasks()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Loose");
        project.Tasks.Add(PageScenario.Task(project, "Someday"));
        await scenario.Projects.SaveAsync(project);
        var row = (await DashboardViewModel.LoadAsync(scenario.Services)).Priority.Single();

        Assert.Equal(scenario.Strings["date.none"], row.NextWhen);
        Assert.False(row.IsLate);
        Assert.False(row.HasWeek);

        await row.OpenCommand.ExecuteAsync(null);

        Assert.Equal(new ProjectRoute(project.Id, ProjectTab.Tasks), scenario.Navigator.Current);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task The_agenda_is_the_current_week_starting_on_Monday_with_each_days_dated_work()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Nova");
        project.Tasks.AddRange([PageScenario.Task(project, "Collect licenses", Noon.AddHours(-3)), PageScenario.Task(project, "Next week", Noon.AddDays(5))]);
        project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "IC pre-read", DueAt = Noon.AddHours(2) });
        await scenario.Projects.SaveAsync(project);

        var agenda = (await DashboardViewModel.LoadAsync(scenario.Services)).Agenda;

        Assert.Equal(7, agenda.Count);
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], agenda.Select(day => day.DayName));
        Assert.Equal([false, false, false, false, true, false, false], agenda.Select(day => day.IsToday));
        Assert.Equal([true, true, true, true, false, true, true], agenda.Select(day => day.IsEmpty));
        var friday = agenda[4];
        Assert.Equal(scenario.Strings.Dates.ShortDate(new DateTime(2026, 10, 9)), friday.DateText);
        Assert.Equal(["Collect licenses", "IC pre-read"], friday.Entries.Select(entry => entry.Title));
        Assert.Equal([true, false], friday.Entries.Select(entry => entry.IsOverdue));
        Assert.Equal([false, true], friday.Entries.Select(entry => entry.IsMilestone));
        Assert.All(friday.Entries, entry => Assert.Equal("Nova", entry.ProjectName));
    }

    [Fact]
    public async Task In_Persian_the_week_starts_on_Saturday_and_text_follows_the_language_in_place()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Nova");
        project.Tasks.Add(PageScenario.Task(project, "Chase", Noon.AddDays(-1)));
        await scenario.Projects.SaveAsync(project);
        scenario.Locale.SetLanguage("fa");

        var dashboard = await DashboardViewModel.LoadAsync(scenario.Services);

        Assert.Equal(DayOfWeek.Saturday, new DateTime(2026, 10, 3).DayOfWeek);
        Assert.Equal(scenario.Strings.Culture.DateTimeFormat.GetDayName(DayOfWeek.Saturday), dashboard.Agenda[0].DayName);
        Assert.Equal(scenario.Strings.Dates.ShortDate(new DateTime(2026, 10, 3)), dashboard.Agenda[0].DateText);
        var row = dashboard.Priority.Single();
        Assert.Equal(Icons.CaretLeft, row.Caret);

        var changes = new List<string?>();
        row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        scenario.Locale.SetLanguage("en");

        Assert.Equal([string.Empty], changes);
        Assert.Equal(Icons.CaretRight, row.Caret);
        Assert.StartsWith("Next: ", row.NextPrefix);
    }

    [Fact]
    public void View_models_are_shown_by_the_view_locator()
    {
        Assert.Equal(typeof(DashboardView), Common.ViewLocator.ViewTypeFor(typeof(DashboardViewModel)));
        Assert.Equal(typeof(PriorityRowView), Common.ViewLocator.ViewTypeFor(typeof(PriorityRowViewModel)));
        Assert.Equal(typeof(AgendaDayView), Common.ViewLocator.ViewTypeFor(typeof(AgendaDayViewModel)));
        Assert.Equal(typeof(AgendaEntryView), Common.ViewLocator.ViewTypeFor(typeof(AgendaEntryViewModel)));
    }
}
