using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

/// <summary>The opt-in Google Calendar overlay: invisible by default, read-only when connected, and never reaching the sample workspace.</summary>
public sealed class GoogleCalendarUiTests
{
    private sealed class FakeCalendar(bool available = true) : IExternalCalendarSource
    {
        public bool Connected { get; set; }
        public int ConnectCalls { get; private set; }
        public int ListCalls { get; private set; }
        public Exception? ListFailure { get; set; }
        public TaskCompletionSource? ConnectGate { get; set; }
        public List<ExternalCalendarEvent> Events { get; } = [];
        public bool IsAvailable => available;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Connected);
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            if (ConnectGate is not null) await ConnectGate.Task.WaitAsync(cancellationToken);
            Connected = true;
        }
        public Task DisconnectAsync(CancellationToken cancellationToken = default) { Connected = false; return Task.CompletedTask; }
        public Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            if (ListFailure is not null) throw ListFailure;
            return Task.FromResult<IReadOnlyList<ExternalCalendarEvent>>(Events.Where(item => item.Start >= from && item.Start < to).ToList());
        }
    }

    private static ExternalCalendarEvent LunchToday()
    {
        var start = DateTime.Today.AddHours(14);
        var offset = TimeZoneInfo.Local.GetUtcOffset(start);
        return new("g1", "Partner lunch", new DateTimeOffset(start, offset), new DateTimeOffset(start.AddHours(1), offset), false);
    }

    private static async Task<Project> SeedDatedProjectAsync(MainWindowTests.Fixture fixture)
    {
        var project = await fixture.Projects.CreateAsync("Nova Logistics", "Nova", ProjectStatus.Active, "", "");
        var due = DateTime.Today.AddHours(12);
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Collect licenses", DueAt = new DateTimeOffset(due, TimeZoneInfo.Local.GetUtcOffset(due)) });
        await fixture.Projects.SaveAsync(project);
        return project;
    }

    private static MainWindow Open(MainWindowTests.Fixture fixture, IExternalCalendarSource? calendar) =>
        new(fixture.Projects, fixture.Agents, () => Task.CompletedTask, "Synthetic", fixture.Locale, calendar: calendar);

    [AvaloniaFact]
    public async Task Default_install_shows_no_google_option_and_no_google_events()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await SeedDatedProjectAsync(fixture);
        var window = Open(fixture, calendar: null);
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationSettings").IsEffectivelyEnabled);
            Click(Nav(window, "NavigationSettings"));
            await UntilAsync(() => Controls<TextBox>(window).Any(box => box.Name == "Setting_PROJECTOPS_OPENCODE_URL"));
            Assert.DoesNotContain(Controls<StackPanel>(window), panel => panel.Name == "GoogleCalendarPanel");

            Click(Nav(window, "NavigationCalendar"));
            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "CalendarEvent"));
            Assert.DoesNotContain(Controls<Border>(window), border => border.Name == "CalendarGoogleEvent");
            Assert.False(Controls<StackPanel>(window).Single(panel => panel.Name == "CalendarGoogleLegend").IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Configured_but_not_connected_offers_connect_and_never_asks_for_events()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await SeedDatedProjectAsync(fixture);
        var calendar = new FakeCalendar();
        var window = Open(fixture, calendar);
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationSettings").IsEffectivelyEnabled);
            Click(Nav(window, "NavigationSettings"));
            await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "GoogleCalendarStatus" && block.Text == "Not connected"));
            Assert.Contains(Controls<Button>(window), button => button.Name == "GoogleCalendarConnect" && button.IsEffectivelyVisible);

            Click(Nav(window, "NavigationCalendar"));
            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "CalendarEvent"));
            Assert.DoesNotContain(Controls<Border>(window), border => border.Name == "CalendarGoogleEvent");
            Assert.Equal(0, calendar.ListCalls);
            Assert.Equal(0, calendar.ConnectCalls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Connecting_reveals_Calendar_even_without_dated_work_and_shows_events_read_only()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await fixture.Projects.CreateAsync("Undated project", "Co", ProjectStatus.Active, "", "");
        var calendar = new FakeCalendar();
        calendar.Events.Add(LunchToday());
        var window = Open(fixture, calendar);
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationSettings").IsEffectivelyEnabled);
            Assert.False(Nav(window, "NavigationCalendar").IsVisible);

            Click(Nav(window, "NavigationSettings"));
            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "GoogleCalendarConnect" && button.IsEffectivelyVisible));
            Click(Controls<Button>(window).Single(button => button.Name == "GoogleCalendarConnect" && button.IsEffectivelyVisible));
            await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "GoogleCalendarStatus" && block.Text == "Connected"));
            Assert.Equal(1, calendar.ConnectCalls);
            await UntilAsync(() => Nav(window, "NavigationCalendar").IsVisible);

            Click(Nav(window, "NavigationCalendar"));
            await UntilAsync(() => Controls<Border>(window).Any(border => border.Name == "CalendarGoogleEvent"));
            var shown = Controls<Border>(window).Single(border => border.Name == "CalendarGoogleEvent");
            Assert.Contains("Partner lunch", AutomationProperties.GetName(shown));
            Assert.True(Controls<StackPanel>(window).Single(panel => panel.Name == "CalendarGoogleLegend").IsVisible);
            // Display only: it is not a button, so it cannot open a project or be edited.
            Assert.Empty(shown.GetLogicalDescendants().OfType<Button>());
            Assert.DoesNotContain(Controls<Button>(window), button => button.Name == "CalendarEvent");

            Click(Nav(window, "NavigationSettings"));
            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "GoogleCalendarDisconnect" && button.IsEffectivelyVisible));
            Click(Controls<Button>(window).Single(button => button.Name == "GoogleCalendarDisconnect" && button.IsEffectivelyVisible));
            await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "GoogleCalendarStatus" && block.Text == "Not connected"));
            Assert.False(calendar.Connected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Cancelling_sign_in_leaves_everything_unchanged_and_reports_no_error()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await SeedDatedProjectAsync(fixture);
        var calendar = new FakeCalendar { ConnectGate = new TaskCompletionSource() };
        var window = Open(fixture, calendar);
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationSettings").IsEffectivelyEnabled);
            Click(Nav(window, "NavigationSettings"));
            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "GoogleCalendarConnect" && button.IsEffectivelyVisible));
            Click(Controls<Button>(window).Single(button => button.Name == "GoogleCalendarConnect" && button.IsEffectivelyVisible));

            // While the browser sign-in is pending the page stays usable and Cancel is offered.
            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "GoogleCalendarCancel" && button.IsEffectivelyVisible));
            Assert.True(Nav(window, "NavigationCalendar").IsEffectivelyEnabled);
            Click(Controls<Button>(window).Single(button => button.Name == "GoogleCalendarCancel" && button.IsEffectivelyVisible));

            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "GoogleCalendarConnect" && button.IsEffectivelyVisible));
            Assert.False(calendar.Connected);
            Assert.Contains(Controls<TextBlock>(window), block => block.Name == "GoogleCalendarStatus" && block.Text == "Not connected");
            Assert.Null(window.LastFailure);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Revoked_sign_in_shows_a_notice_and_keeps_project_dates()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await SeedDatedProjectAsync(fixture);
        var calendar = new FakeCalendar { Connected = true, ListFailure = new ExternalCalendarAuthorizationException("revoked") };
        var window = Open(fixture, calendar);
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationCalendar").IsEffectivelyEnabled);
            Click(Nav(window, "NavigationCalendar"));
            await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "CalendarNotice" && block.IsVisible && block.Text!.StartsWith("Google Calendar needs to be connected again")));
            Assert.Contains(Controls<Button>(window), button => button.Name == "CalendarEvent" && MainWindowTests.ButtonText(button).StartsWith("Collect licenses · Nova Logistics"));
            Assert.DoesNotContain(Controls<Border>(window), border => border.Name == "CalendarGoogleEvent");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Network_failure_shows_a_notice_and_keeps_project_dates()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await SeedDatedProjectAsync(fixture);
        var calendar = new FakeCalendar { Connected = true, ListFailure = new HttpRequestException("offline") };
        var window = Open(fixture, calendar);
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationCalendar").IsEffectivelyEnabled);
            Click(Nav(window, "NavigationCalendar"));
            await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "CalendarNotice" && block.IsVisible && block.Text!.StartsWith("Couldn't load Google Calendar events")));
            Assert.Contains(Controls<Button>(window), button => button.Name == "CalendarEvent");
            Assert.Null(window.LastFailure);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Sample_workspace_never_reads_the_connected_google_calendar()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await SeedDatedProjectAsync(fixture);
        var calendar = new FakeCalendar { Connected = true };
        calendar.Events.Add(LunchToday());
        var window = Open(fixture, calendar);
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationCalendar").IsEffectivelyEnabled);
            await window.StartSampleAsync();
            Click(Nav(window, "NavigationCalendar"));
            await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "CalendarEvent"));
            Assert.Equal(0, calendar.ListCalls);
            Assert.DoesNotContain(Controls<Border>(window), border => border.Name == "CalendarGoogleEvent");
            Assert.DoesNotContain(Controls<StackPanel>(window), panel => panel.Name == "GoogleCalendarPanel");

            await window.ExitSampleAsync();
            Click(Nav(window, "NavigationSettings"));
            await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "GoogleCalendarStatus" && block.Text == "Connected"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Persian_labels_are_translated()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await SeedDatedProjectAsync(fixture);
        fixture.Locale.SetLanguage("fa");
        var window = Open(fixture, new FakeCalendar());
        try
        {
            window.Show();
            await UntilAsync(() => Nav(window, "NavigationSettings").IsEffectivelyEnabled);
            Click(Nav(window, "NavigationSettings"));
            await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "GoogleCalendarStatus" && block.Text == "متصل نیست"));
            Assert.Contains(Controls<Button>(window), button => button.Name == "GoogleCalendarConnect" && button.IsEffectivelyVisible && MainWindowTests.ButtonText(button) == "اتصال به تقویم گوگل");
        }
        finally { window.Close(); }
    }

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static Button Nav(Window window, string name) => Controls<Button>(window).Single(button => button.Name == name);
    private static void Click(Button button) => UiWait.Click(button);
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "UI did not reach the expected state within 10 seconds." + MainWindowTests.Fixture.DescribeLastWindow());
    }
}
