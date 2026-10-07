using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

/// <summary>Calendar, Documents, Settings, theme switching, proposal review and the not-configured assistant.</summary>
public sealed class DesignedSectionsTests
{
    [AvaloniaTheory]
    [InlineData("en", "light", 1440)]
    [InlineData("fa", "dark", 1440)]
    [InlineData("en", "dark", 800)]
    public async Task Sections_render_from_stored_data_in_both_themes(string language, string theme, int width)
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Nova Logistics", "Nova Freight", ProjectStage.DueDiligence, ProjectStatus.Active, "Owner", "");
        var source = fixture.CreateSourceFile();
        project.Requirements[0].Files.Add(new ProjectFile { FileName = "synthetic.txt", Path = source, SizeBytes = new FileInfo(source).Length });
        var missing = Path.Combine(Path.GetDirectoryName(source)!, "moved-away.pdf");
        project.Requirements[2].Files.Add(new ProjectFile { FileName = "moved-away.pdf", Path = missing, SizeBytes = 2048 });
        // Today at fixed local times keeps both items inside the current calendar month regardless of when the test runs.
        var today = DateTime.Today;
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Collect licenses", DueAt = new DateTimeOffset(today.AddHours(12), TimeZoneInfo.Local.GetUtcOffset(today.AddHours(12))) });
        project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "IC pre-read", DueAt = new DateTimeOffset(today.AddHours(13), TimeZoneInfo.Local.GetUtcOffset(today.AddHours(13))) });
        await fixture.Projects.SaveAsync(project);
        fixture.Runtime.Result = new AgentResult { Text = "Two follow-ups", Proposals = [new TaskProposal { Title = "Request sales history" }, new TaskProposal { Title = "Ask for cap table" }] };
        fixture.Runtime.Release.TrySetResult();
        await fixture.Agents.RunAsync(project, "Find missing information", new Progress<AgentEvent>(), CancellationToken.None);

        fixture.Locale.SetLanguage(language);
        var text = new LocalizationService(fixture.Locale);
        var window = fixture.Window;
        window.Width = width; window.Height = width == 800 ? 640 : 1000;
        window.Show();
        await UntilAsync(() => Nav(window, "NavigationCalendar").IsEffectivelyEnabled);
        Click(Segment(window, text.Get("theme." + theme)));
        Assert.Equal(theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
        var card = Controls<Border>(window).First(border => border.Classes.Contains("sectionCard"));
        Assert.Equal(theme == "dark" ? Color.Parse("#212331") : Colors.White, ((ISolidColorBrush)card.Background!).Color);
        Capture(window, "dashboard", language, theme, width);

        Click(Nav(window, "NavigationCalendar"));
        await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "CalendarEvent"));
        Assert.Contains(Controls<Button>(window), button => button.Name == "CalendarEvent" && MainWindowTests.ButtonText(button).StartsWith("Collect licenses · Nova Logistics"));
        Assert.Contains(Controls<Button>(window), button => button.Name == "CalendarEvent" && MainWindowTests.ButtonText(button).StartsWith("IC pre-read · Nova Logistics"));
        Capture(window, "calendar", language, theme, width);

        Click(Nav(window, "NavigationDocuments"));
        await UntilAsync(() => Controls<Button>(window).Count(button => button.Name == "DocumentRow") == 2);
        Assert.Contains(Controls<TextBlock>(window), block => block.Text == text.Get("documents.missingFile"));
        Assert.Contains(Controls<TextBlock>(window), block => block.Text == text.Get("documents.referenceOnly"));
        Assert.Equal(FlowDirection.LeftToRight, Controls<TextBlock>(window).Single(block => block.Name == "DocumentPath").FlowDirection);
        Assert.Contains(Controls<TextBlock>(window), block => block.Text == text.Get("documents.previewUnavailable"));
        Capture(window, "documents", language, theme, width);

        Click(Nav(window, "NavigationSettings"));
        await UntilAsync(() => Controls<TextBox>(window).Any(box => box.Name == "Setting_PROJECTOPS_OPENCODE_URL"));
        Assert.All(Controls<TextBox>(window).Where(box => box.Name?.StartsWith("Setting_") == true), box => Assert.True(box.IsReadOnly));
        Assert.Contains(Controls<TextBlock>(window), block => block.Text == text.Get("settings.notBuilt"));
        Capture(window, "settings", language, theme, width);

        Click(Nav(window, "NavigationProjects"));
        await UntilAsync(() => Controls<Button>(window).Any(button => button.Name == "NewProjectButton" && button.IsEffectivelyEnabled));
        Capture(window, "projects", language, theme, width);
        Click(Controls<Button>(window).Single(button => button.Name == "NewProjectButton"));
        Assert.Contains(Controls<Control>(window), control => control.Name == "CreateProjectDialog");
        Assert.False(Controls<Button>(window).Single(button => MainWindowTests.ButtonText(button) == text.Get("template.blank")).IsEnabled);
        Capture(window, "new-project", language, theme, width);
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.DoesNotContain(Controls<Control>(window), control => control.Name == "CreateProjectDialog");

        Click(Controls<Button>(window).First(button => MainWindowTests.ButtonText(button).StartsWith("Nova Logistics ·")));
        await UntilAsync(() => Controls<TabControl>(window).Any() && Nav(window, "NavigationProjects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Controls<Control>(window), control => control.Name == "PendingProposalsBanner");
        Click(Controls<Button>(window).First(button => MainWindowTests.ButtonText(button) == text.Get("tasks.review")));
        await UntilAsync(() => Controls<Control>(window).Single(control => control.Name == "AssistantPanel").IsVisible
            && Controls<Control>(window).Any(control => control.Name == "ProposalTray"));
        Assert.Equal(2, Controls<CheckBox>(window).Count(check => check.IsEnabled && check.Content is TextBlock block && block.Text!.Contains(text.Get("proposal.status.pending"))));
        Assert.DoesNotContain((await fixture.Projects.GetAsync(project.Id))!.Tasks, task => task.Title.StartsWith("Request sales"));
        Capture(window, "review", language, theme, width);
        Assert.Equal(1, fixture.Runtime.Calls);
        window.Close();
    }

    [Fact]
    public void Theme_preference_persists_beside_language_without_overwriting_it()
    {
        var directory = Path.Combine(Path.GetTempPath(), "opencode", "appearance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var settings = Path.Combine(directory, "settings.json");
        try
        {
            var locale = new LocaleContext(settings);
            locale.SetLanguage("fa");
            var appearance = new AppearanceContext(settings);
            Assert.Equal("light", appearance.Theme);
            appearance.SetTheme("dark");
            appearance.SetTheme("auto");
            using (var json = JsonDocument.Parse(File.ReadAllText(settings)))
            {
                Assert.Equal("fa", json.RootElement.GetProperty("LanguageCode").GetString());
                Assert.Equal("auto", json.RootElement.GetProperty("Theme").GetString());
            }
            Assert.Equal("auto", new AppearanceContext(settings).Theme);
            Assert.Equal("fa", new LocaleContext(settings).LanguageCode);
            locale.SetLanguage("en");
            Assert.Equal("auto", new AppearanceContext(settings).Theme);
            File.WriteAllText(settings, "{\"Theme\":\"neon\"}");
            Assert.Equal("light", new AppearanceContext(settings).Theme);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [AvaloniaFact]
    public async Task Unconfigured_assistant_shows_setup_state_and_offers_no_run()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Offline project", "Company", ProjectStage.Screening, ProjectStatus.Active, "", "");
        var window = new MainWindow(fixture.Projects, fixture.Agents, () => Task.CompletedTask, "Agent not configured.", fixture.Locale,
            environment: new DesktopEnvironment(AgentConfigured: false, DatabasePath: "/synthetic/projects.db"));
        try
        {
            window.Show();
            await UntilAsync(() => Controls<Button>(window).Any(button => MainWindowTests.ButtonText(button).StartsWith("Offline project ·") && button.IsEffectivelyEnabled));
            Click(Controls<Button>(window).First(button => MainWindowTests.ButtonText(button).StartsWith("Offline project ·")));
            await UntilAsync(() => Controls<TabControl>(window).Any());
            Controls<TabControl>(window).Single().SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            await UntilAsync(() => Controls<Control>(window).Single(control => control.Name == "AssistantPanel").IsVisible);
            Assert.Contains(Controls<TextBlock>(window), block => block.IsEffectivelyVisible && block.Text?.StartsWith("The assistant is off.") == true);
            Assert.False(Controls<Control>(window).Single(control => control.Name == "DelegationRequest").IsVisible);
            Assert.False(Controls<Button>(window).Single(button => button.Name == "RunAgent").IsEffectivelyVisible);
            Assert.Contains(Controls<TextBlock>(window), block => block.IsEffectivelyVisible && block.Text == "Not configured");
            Assert.Equal(0, fixture.Runtime.Calls);
        }
        finally { window.Close(); }
    }

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static Button Nav(Window window, string name) => Controls<Button>(window).Single(button => button.Name == name);
    private static Button Segment(Window window, string label) => Controls<Button>(window).First(button => button.Classes.Contains("segment") && MainWindowTests.ButtonText(button) == label);
    private static void Click(Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "UI did not reach the expected state within 10 seconds.");
    }
    private static void Capture(Window window, string screen, string language, string theme, int width)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var directory = Environment.GetEnvironmentVariable("PROJECTOPS_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        for (var tick = 0; tick < 3; tick++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{screen}-{language}-{theme}-{width}.png"));
    }
}
