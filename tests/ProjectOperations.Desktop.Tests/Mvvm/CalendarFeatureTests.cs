using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Calendar;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The month calendar and its opt-in Google overlay as plain view-models: no window, no controls, a fixed clock and a fake calendar source.</summary>
public sealed class CalendarFeatureTests
{
    private static readonly DateTimeOffset Noon = PageScenario.Noon;

    private static async Task<(PageScenario Scenario, Project Project)> WithProjectAsync()
    {
        var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Nova Logistics");
        project.Tasks.Add(PageScenario.Task(project, "Collect licenses", Noon.AddHours(-3)));
        project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "IC pre-read", DueAt = Noon.AddHours(2) });
        await scenario.Projects.SaveAsync(project);
        return (scenario, project);
    }

    private static CalendarDayViewModel Day(CalendarViewModel calendar, int number) =>
        calendar.Weeks.SelectMany(week => week.Days).Single(day => day.InMonth && day.Number == number.ToString());

    [Fact]
    public async Task October_2026_is_five_Monday_first_weeks_with_today_marked_and_neighbouring_days_outside()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);

        Assert.Equal(scenario.Strings.Dates.MonthYear(new DateTime(2026, 10, 1)), calendar.Title);
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], calendar.Weekdays);
        Assert.Equal(5, calendar.Weeks.Count);
        Assert.All(calendar.Weeks, week => Assert.Equal(7, week.Days.Count));
        var days = calendar.Weeks.SelectMany(week => week.Days).ToList();
        Assert.Equal(["28", "29", "30"], days.Take(3).Select(day => day.Number));
        Assert.Equal([true, true, true], days.Take(3).Select(day => day.IsOutside));
        Assert.Equal(31, days.Count(day => day.InMonth));
        Assert.Equal(["9"], days.Where(day => day.IsToday).Select(day => day.Number));
        Assert.Equal(Enumerable.Range(0, 35).Select(index => index % 7 > 0), days.Select(day => day.HasStartRule));
        Assert.Equal(Enumerable.Range(0, 35).Select(index => index >= 7), days.Select(day => day.HasTopRule));
        Assert.False(calendar.HasNotice);
        Assert.False(calendar.IsGoogleConnected);
        Assert.Equal(Icons.CaretLeft, calendar.PreviousIcon);
        Assert.Equal(Icons.CaretRight, calendar.NextIcon);
    }

    [Fact]
    public async Task Tasks_and_milestones_sit_on_their_day_and_open_the_projects_tasks()
    {
        var (scenario, project) = await WithProjectAsync();
        using var _ = scenario;
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);

        var today = Day(calendar, 9);
        var entries = today.Items.Cast<CalendarEntryViewModel>().ToList();

        Assert.Equal(["Collect licenses · Nova Logistics", "IC pre-read · Nova Logistics"], entries.Select(entry => entry.Text));
        Assert.Equal([true, false], entries.Select(entry => entry.IsOverdue));
        Assert.Equal([false, true], entries.Select(entry => entry.IsMilestone));
        Assert.Equal($"Collect licenses · Nova Logistics · {scenario.Strings.Due(Noon.AddHours(-3))}", entries[0].AccessibleName);
        Assert.False(today.HasOverflow);
        Assert.All(calendar.Weeks.SelectMany(week => week.Days).Where(day => day != today), day => Assert.Empty(day.Items));

        await entries[0].OpenCommand.ExecuteAsync(null);

        Assert.Equal(new ProjectRoute(project.Id, ProjectTab.Tasks), scenario.Navigator.Current);
    }

    [Fact]
    public async Task A_day_shows_three_entries_and_counts_the_rest()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Busy");
        for (var index = 1; index <= 5; index++) project.Tasks.Add(PageScenario.Task(project, $"Task {index}", Noon.AddMinutes(index)));
        await scenario.Projects.SaveAsync(project);
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);

        var today = Day(calendar, 9);

        Assert.Equal(3, today.Items.Count);
        Assert.True(today.HasOverflow);
        Assert.Equal(scenario.Strings.Format("calendar.more", 2), today.OverflowText);
    }

    [Fact]
    public async Task Paging_moves_the_month_and_Today_returns_to_the_current_one()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);
        var titles = new List<string>();
        calendar.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CalendarViewModel.Title)) titles.Add(calendar.Title); };

        calendar.NextCommand.Execute(null);
        Assert.Equal(scenario.Strings.Dates.MonthYear(new DateTime(2026, 11, 1)), calendar.Title);
        Assert.Equal(6, calendar.Weeks.Count); // 1 November 2026 is a Sunday, so the Monday-first grid needs six rows
        Assert.Empty(calendar.Weeks.SelectMany(week => week.Days).SelectMany(day => day.Items));
        Assert.DoesNotContain(calendar.Weeks.SelectMany(week => week.Days), day => day.IsToday);

        calendar.PreviousCommand.Execute(null);
        calendar.PreviousCommand.Execute(null);
        Assert.Equal(scenario.Strings.Dates.MonthYear(new DateTime(2026, 9, 1)), calendar.Title);
        Assert.Equal(["30"], calendar.Weeks.SelectMany(week => week.Days).Where(day => day.InMonth).Select(day => day.Number).TakeLast(1));

        calendar.TodayCommand.Execute(null);
        Assert.Equal(scenario.Strings.Dates.MonthYear(new DateTime(2026, 10, 1)), calendar.Title);
        Assert.Equal(4, titles.Count);
        Assert.Equal(2, Day(calendar, 9).Items.Count);
    }

    [Fact]
    public async Task In_Persian_it_pages_Jalali_months_from_Saturday_and_switching_language_redraws_in_place()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);
        var english = calendar.Weeks;
        var changes = new List<string?>();
        calendar.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        scenario.Locale.SetLanguage("fa");

        Assert.NotSame(english, calendar.Weeks);
        Assert.Contains(string.Empty, changes);
        Assert.Equal(scenario.Strings.Culture.DateTimeFormat.GetDayName(DayOfWeek.Saturday), calendar.Weekdays[0]);
        Assert.Equal(7, calendar.Weekdays.Count);
        Assert.Equal(JalaliDate.MonthYear(new DateTime(2026, 10, 9)), calendar.Title);
        Assert.Equal(Icons.CaretRight, calendar.PreviousIcon);
        Assert.Equal(Icons.CaretLeft, calendar.NextIcon);
        var days = calendar.Weeks.SelectMany(week => week.Days).ToList();
        Assert.Equal(JalaliDate.DaysInMonth(new DateTime(2026, 10, 9)), days.Count(day => day.InMonth));
        Assert.Single(days, day => day.IsToday);
        Assert.Equal(JalaliDate.FromGregorian(new DateTime(2026, 10, 9)).Day.ToString(scenario.Strings.Culture), days.Single(day => day.IsToday).Number);
        Assert.Equal(2, days.Single(day => day.IsToday).Items.Count);

        calendar.NextCommand.Execute(null);
        Assert.Equal(JalaliDate.MonthYear(JalaliDate.AddMonths(JalaliDate.MonthStart(new DateTime(2026, 10, 9)), 1)), calendar.Title);

        scenario.Locale.SetLanguage("en");
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], calendar.Weekdays);
        // Aban 1405 begins on 23 October 2026, so the month that was open in Persian becomes November when the language goes back.
        Assert.Equal(scenario.Strings.Dates.MonthYear(new DateTime(2026, 11, 1)), calendar.Title);
    }

    [Fact]
    public async Task Without_a_connected_calendar_nothing_is_requested_and_no_legend_shows()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);

        await calendar.StartAsync();

        Assert.Empty(scenario.Calendar.Requests);
        Assert.False(calendar.IsGoogleConnected);
        Assert.False(calendar.HasNotice);
        Assert.DoesNotContain(calendar.Weeks.SelectMany(week => week.Days).SelectMany(day => day.Items), item => item is ExternalEntryViewModel);
    }

    [Fact]
    public async Task A_connected_calendar_overlays_its_events_read_only_beside_project_dates()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        scenario.Calendar.Connected = true;
        scenario.Calendar.Events.AddRange([
            new ExternalCalendarEvent("1", "Lunch", Noon.AddHours(1), Noon.AddHours(2), IsAllDay: false),
            new ExternalCalendarEvent("2", "", new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero), IsAllDay: true)]);
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);
        var L = scenario.Strings;

        await calendar.StartAsync();

        Assert.True(calendar.IsGoogleConnected);
        var request = Assert.Single(scenario.Calendar.Requests);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero), request.From);
        Assert.Equal(request.From.AddDays(35), request.To);
        var today = Day(calendar, 9).Items;
        Assert.Equal(3, today.Count);
        var lunch = Assert.IsType<ExternalEntryViewModel>(today[2]);
        Assert.Equal("Lunch", lunch.Title);
        Assert.Equal($"Lunch · {L.Due(Noon.AddHours(1))} · {L["calendar.legend.google"]}", lunch.AccessibleName);
        // An all-day event covers each day it spans (the end date is exclusive), and an untitled one says so.
        foreach (var number in new[] { 12, 13 })
        {
            var untitled = Assert.IsType<ExternalEntryViewModel>(Assert.Single(Day(calendar, number).Items));
            Assert.Equal(L["google.untitled"], untitled.Title);
            Assert.Contains(L["google.allDay"], untitled.AccessibleName);
        }
        Assert.Empty(Day(calendar, 14).Items);
    }

    [Fact]
    public async Task Project_entries_come_first_and_external_ones_fill_what_is_left_of_the_three_slots()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Busy");
        project.Tasks.AddRange([PageScenario.Task(project, "One", Noon), PageScenario.Task(project, "Two", Noon.AddMinutes(1))]);
        await scenario.Projects.SaveAsync(project);
        scenario.Calendar.Connected = true;
        scenario.Calendar.Events.AddRange(new[] { "a", "b", "c" }.Select(id => new ExternalCalendarEvent(id, id, Noon.AddHours(1), Noon.AddHours(2), false)));
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);

        await calendar.StartAsync();

        var today = Day(calendar, 9);
        Assert.Equal(new[] { typeof(CalendarEntryViewModel), typeof(CalendarEntryViewModel), typeof(ExternalEntryViewModel) }, today.Items.Select(item => item.GetType()));
        Assert.Equal(scenario.Strings.Format("calendar.more", 2), today.OverflowText);
    }

    [Fact]
    public async Task A_revoked_sign_in_shows_a_notice_hides_the_legend_and_refreshes_the_sidebar()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        scenario.Calendar.Connected = true;
        scenario.Calendar.Failure = new ExternalCalendarAuthorizationException("revoked");
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);

        await calendar.StartAsync();

        Assert.True(calendar.HasNotice);
        Assert.Equal(scenario.Strings["google.reauth"], calendar.NoticeText);
        Assert.False(calendar.IsGoogleConnected);
        Assert.Equal(1, scenario.Host.ShellRefreshes);
        Assert.Equal(2, Day(calendar, 9).Items.Count); // project dates always render
    }

    [Fact]
    public async Task A_network_failure_shows_a_notice_and_the_next_success_clears_it()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        scenario.Calendar.Connected = true;
        scenario.Calendar.Failure = new HttpRequestException("offline");
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);

        await calendar.StartAsync();

        Assert.Equal(scenario.Strings["google.loadFailed"], calendar.NoticeText);
        Assert.True(calendar.IsGoogleConnected);
        Assert.Equal(0, scenario.Host.ShellRefreshes);
        Assert.Equal(2, Day(calendar, 9).Items.Count);

        scenario.Calendar.Failure = null;
        calendar.NextCommand.Execute(null);
        await calendar.Refreshing;

        Assert.False(calendar.HasNotice);
        Assert.Equal("", calendar.NoticeText);
    }

    [Fact]
    public async Task Changing_the_language_after_the_overlay_started_fetches_the_visible_range_again()
    {
        var (scenario, _) = await WithProjectAsync();
        using var _ = scenario;
        scenario.Calendar.Connected = true;
        var calendar = await CalendarViewModel.LoadAsync(scenario.Services);
        await calendar.StartAsync();
        Assert.Single(scenario.Calendar.Requests);

        scenario.Locale.SetLanguage("fa");
        await calendar.Refreshing;

        Assert.Equal(2, scenario.Calendar.Requests.Count);
        // A Jalali month grid starts on a Saturday and is not the Gregorian one.
        Assert.Equal(DayOfWeek.Saturday, scenario.Calendar.Requests[1].From.DayOfWeek);
    }

    [Fact]
    public void View_models_are_shown_by_the_view_locator()
    {
        Assert.Equal(typeof(CalendarView), Common.ViewLocator.ViewTypeFor(typeof(CalendarViewModel)));
        Assert.Equal(typeof(CalendarWeekView), Common.ViewLocator.ViewTypeFor(typeof(CalendarWeekViewModel)));
        Assert.Equal(typeof(CalendarDayView), Common.ViewLocator.ViewTypeFor(typeof(CalendarDayViewModel)));
        Assert.Equal(typeof(CalendarEntryView), Common.ViewLocator.ViewTypeFor(typeof(CalendarEntryViewModel)));
        Assert.Equal(typeof(ExternalEntryView), Common.ViewLocator.ViewTypeFor(typeof(ExternalEntryViewModel)));
    }
}
