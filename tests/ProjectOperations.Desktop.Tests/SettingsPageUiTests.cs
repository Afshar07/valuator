using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

/// <summary>
/// The Settings page in the real window: what only a window can show about the view-model wiring — leaving the page, key and focus
/// handling in the stage editor, the colour picker, and language switching that keeps what the user has typed.
/// </summary>
public sealed class SettingsPageUiTests
{
    /// <summary>A calendar whose sign-in waits until the test releases it or the page abandons it.</summary>
    private sealed class WaitingCalendar : IExternalCalendarSource
    {
        public bool Connected { get; private set; }
        public bool SignInAbandoned { get; private set; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsAvailable => true;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Connected);

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try { await Release.Task.WaitAsync(cancellationToken); Connected = true; }
            catch (OperationCanceledException) { SignInAbandoned = true; throw; }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default) { Connected = false; return Task.CompletedTask; }

        public Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExternalCalendarEvent>>([]);
    }

    private static MainWindow Open(MainWindowTests.Fixture fixture, IExternalCalendarSource? calendar = null) =>
        new(fixture.Projects, fixture.Agents, () => Task.CompletedTask, "Synthetic", fixture.Locale, calendar: calendar);

    private static async Task<MainWindow> OpenSettingsAsync(MainWindowTests.Fixture fixture, IExternalCalendarSource? calendar = null)
    {
        await fixture.InitializeAsync();
        await fixture.Projects.CreateAsync("Nova Logistics", "Nova", ProjectStatus.Active, "", "");
        var window = Open(fixture, calendar);
        window.Show();
        await UntilAsync(() => Nav(window, "NavigationSettings").IsEffectivelyEnabled);
        Click(Nav(window, "NavigationSettings"));
        await UntilAsync(() => Controls<TextBlock>(window).Any(block => block.Name == "DatabasePath"));
        return window;
    }

    [AvaloniaFact]
    public async Task Leaving_Settings_abandons_a_sign_in_that_is_still_waiting_on_the_browser()
    {
        using var fixture = new MainWindowTests.Fixture();
        var calendar = new WaitingCalendar();
        var window = await OpenSettingsAsync(fixture, calendar);
        try
        {
            await UntilAsync(() => Shown<Button>(window, "GoogleCalendarConnect") is not null);
            Click(Shown<Button>(window, "GoogleCalendarConnect")!);
            await UntilAsync(() => Shown<Button>(window, "GoogleCalendarCancel") is not null);
            Assert.False(calendar.SignInAbandoned);

            Click(Nav(window, "NavigationProjects"));
            await UntilAsync(() => calendar.SignInAbandoned);

            Assert.False(calendar.Connected);
            Assert.DoesNotContain(Controls<StackPanel>(window), panel => panel.Name == "GoogleCalendarPanel");
            Assert.Null(window.LastFailure);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_theme_choice_follows_the_sidebar_and_changes_the_window_theme()
    {
        using var fixture = new MainWindowTests.Fixture();
        var window = await OpenSettingsAsync(fixture);
        try
        {
            Assert.True(IsChosen(window, "SettingsTheme", "Light"));

            Click(Choice(window, "ThemeSelector", "Dark"));
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.True(IsChosen(window, "SettingsTheme", "Dark"));
            Assert.False(IsChosen(window, "SettingsTheme", "Light"));

            Click(Choice(window, "SettingsTheme", "Light"));
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
            Assert.True(IsChosen(window, "ThemeSelector", "Light"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Switching_language_here_updates_the_page_in_place_and_keeps_what_was_typed()
    {
        using var fixture = new MainWindowTests.Fixture();
        var window = await OpenSettingsAsync(fixture);
        try
        {
            var firstTitle = Controls<TextBox>(window).First(box => box.Name == "StageTitle");
            firstTitle.Text = "Half-typed stage name";
            var database = Controls<TextBlock>(window).Single(block => block.Name == "DatabasePath");

            Click(Choice(window, "SettingsLanguage", "فارسی"));
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
            Assert.Equal("fa", fixture.Locale.LanguageCode);
            Assert.Same(firstTitle, Controls<TextBox>(window).First(box => box.Name == "StageTitle"));
            Assert.Equal("Half-typed stage name", firstTitle.Text);
            Assert.Same(database, Controls<TextBlock>(window).Single(block => block.Name == "DatabasePath"));
            Assert.True(IsChosen(window, "SettingsLanguage", "فارسی"));
            Assert.Equal(FlowDirection.LeftToRight, Controls<TextBox>(window).Single(box => box.Name == "Setting_PROJECTOPS_OPENCODE_URL").FlowDirection);

            // The sidebar's language button switches back and the choice here follows.
            Click(Controls<Button>(window).Single(button => button.Name == "LanguageSelector"));
            Dispatcher.UIThread.RunJobs();
            Assert.True(IsChosen(window, "SettingsLanguage", "English"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_stage_is_renamed_when_the_box_is_left_or_Enter_is_pressed()
    {
        using var fixture = new MainWindowTests.Fixture();
        var window = await OpenSettingsAsync(fixture);
        try
        {
            var vc = VcTemplate.Create().Id;
            await UntilAsync(() => Controls<TextBox>(window).Count(box => box.Name == "StageTitle") == 6);

            var second = Controls<TextBox>(window).Where(box => box.Name == "StageTitle").ElementAt(1);
            second.Focus();
            second.Text = "Term sheet";
            LeaveFocus(window);
            await UntilAsync(() => Stored(fixture, vc).Result[1].Title == "Term sheet");

            await UntilAsync(() => Controls<TextBox>(window).Where(box => box.Name == "StageTitle").ElementAt(2).Text == "Investment Committee");
            var third = Controls<TextBox>(window).Where(box => box.Name == "StageTitle").ElementAt(2);
            third.Text = "Final approval";
            third.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await UntilAsync(() => Stored(fixture, vc).Result[2].Title == "Final approval");

            // An empty title is put back instead of saved.
            await UntilAsync(() => Controls<TextBox>(window).Where(box => box.Name == "StageTitle").ElementAt(0).Text == "Screening");
            var first = Controls<TextBox>(window).Where(box => box.Name == "StageTitle").ElementAt(0);
            first.Focus();
            first.Text = "  ";
            LeaveFocus(window);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Screening", first.Text);
            Assert.Equal("Screening", (await Stored(fixture, vc))[0].Title);
            Assert.Null(window.LastFailure);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Adding_a_stage_shows_a_new_row_and_the_stage_a_project_uses_cannot_be_deleted()
    {
        using var fixture = new MainWindowTests.Fixture();
        var window = await OpenSettingsAsync(fixture);
        try
        {
            await UntilAsync(() => Controls<Grid>(window).Count(row => row.Name == "StageRow") == 6);
            var deletes = Controls<Button>(window).Where(button => button.Name == "StageDelete").ToList();
            Assert.False(deletes[0].IsEffectivelyEnabled);
            Assert.All(deletes.Skip(1), button => Assert.True(button.IsEffectivelyEnabled));
            Assert.Contains(Controls<TextBlock>(window), block => block.Name == "StageInUse" && block.IsEffectivelyVisible);

            Click(Controls<Button>(window).Single(button => button.Name == "AddStage"));
            await UntilAsync(() => Controls<Grid>(window).Count(row => row.Name == "StageRow") == 7);

            Assert.Equal(7, (await Stored(fixture, VcTemplate.Create().Id)).Count);
            Assert.Equal(1, Controls<TextBlock>(window).Count(block => block.Name == "StageInUse" && block.IsEffectivelyVisible));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_colour_picker_opens_from_the_swatch_and_choosing_a_colour_saves_it_and_closes_the_picker()
    {
        using var fixture = new MainWindowTests.Fixture();
        var window = await OpenSettingsAsync(fixture);
        try
        {
            var vc = VcTemplate.Create().Id;
            await UntilAsync(() => Controls<Button>(window).Count(button => button.Name == "StageColor") == 6);
            Assert.DoesNotContain(Controls<Button>(window), button => button.Name == "StageColorChoice" && button.IsEffectivelyVisible);

            Click(Controls<Button>(window).Where(button => button.Name == "StageColor").ElementAt(2));
            await UntilAsync(() => Controls<Button>(window).Count(button => button.Name == "StageColorChoice" && button.IsEffectivelyVisible) == 9);

            var pink = Controls<Button>(window).Single(button => button.Name == "StageColorChoice" && Avalonia.Automation.AutomationProperties.GetName(button) == "#DB2777");
            Click(pink);
            await UntilAsync(() => Stored(fixture, vc).Result[2].Color == "#DB2777");
            await UntilAsync(() => !Controls<Button>(window).Any(button => button.Name == "StageColorChoice" && button.IsEffectivelyVisible));
            Assert.Null(window.LastFailure);
        }
        finally { window.Close(); }
    }

    private static Task<IReadOnlyList<ProjectStage>> Stored(MainWindowTests.Fixture fixture, string template) => fixture.Projects.ListStagesAsync(template);

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static T? Shown<T>(Window window, string name) where T : Control => Controls<T>(window).SingleOrDefault(control => control.Name == name && control.IsEffectivelyVisible);
    private static Button Nav(Window window, string name) => Controls<Button>(window).Single(button => button.Name == name);

    /// <summary>One option of a segmented control, found by the control's name and the option's label (the sidebar has the same labels).</summary>
    private static Button Choice(Window window, string segmented, string label) =>
        Controls<Button>(window).Single(button => Avalonia.Automation.AutomationProperties.GetName(button) == label
            && button.GetLogicalAncestors().OfType<Border>().Any(border => border.Name == segmented));

    /// <summary>Whether the option is the selected one of its segmented control.</summary>
    private static bool IsChosen(Window window, string segmented, string label) => Choice(window, segmented, label).Classes.Contains("selected");

    /// <summary>Moves keyboard focus away from the focused box, as tabbing or clicking elsewhere does.</summary>
    private static void LeaveFocus(Window window)
    {
        Controls<Button>(window).First(button => button.Name == "AddStage").Focus();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Button button) => UiWait.Click(button);

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "UI did not reach the expected state within 10 seconds." + MainWindowTests.Fixture.DescribeLastWindow());
    }
}
