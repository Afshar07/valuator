using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ProjectOperations.Core.Domain;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

/// <summary>First-run experience: welcome, wizard, sections that appear as data arrives, getting started and the sample workspace.</summary>
public sealed class OnboardingTests
{
    [AvaloniaFact]
    public async Task Empty_workspace_shows_welcome_and_only_offers_projects_and_settings()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var window = fixture.Window;
        window.Width = 1440; window.Height = 900;
        window.Show();
        await UntilAsync(() => Nav(window, "NavigationProjects").IsEffectivelyEnabled);
        Assert.True(Controls<Control>(window).Single(control => control.Name == "OnboardingOverlay").IsVisible);
        Assert.Contains(Controls<Control>(window), control => control.Name == "WelcomeScreen");
        Assert.True(Nav(window, "NavigationProjects").IsVisible);
        Assert.True(Nav(window, "NavigationSettings").IsVisible);
        foreach (var hidden in new[] { "NavigationDashboard", "NavigationCalendar", "NavigationDocuments" })
            Assert.False(Nav(window, hidden).IsVisible, hidden);
        Capture(window, "welcome");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Wizard_creates_a_blank_project_and_sections_appear_as_data_arrives()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var window = fixture.Window;
        window.Width = 1440; window.Height = 900;
        window.Show();
        await UntilAsync(() => Nav(window, "NavigationProjects").IsEffectivelyEnabled);
        Click(window, "Start my first project");
        Capture(window, "wizard-1");
        Controls<TextBox>(window).Single(box => box.Name == "WizardName").Text = "Nova Logistics";
        Click(window, "Continue");
        Capture(window, "wizard-2");
        Click(window, "Blank project");
        Capture(window, "wizard-2-blank");
        Click(window, "Continue");
        Assert.Contains(Controls<Button>(window), button => button.Name == "WizardSkip");
        Controls<TextBox>(window).Single(box => box.Name == "WizardRequirement").Text = "Customer contracts";
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(Controls<Button>(window), button => button.Name == "WizardSkip");
        Capture(window, "wizard-3");
        Click(window, "Open project");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Nav(window, "NavigationProjects").IsEffectivelyEnabled);

        var project = (await fixture.Projects.ListAsync()).Single();
        Assert.Equal("Nova Logistics", project.Name);
        Assert.Equal("blank", project.TemplateId);
        var requirement = Assert.Single(project.Requirements);
        Assert.Equal("Customer contracts", requirement.Title);
        Assert.Equal(RequirementStatus.Missing, requirement.Status);
        Assert.Equal(1, Controls<TabControl>(window).Single().SelectedIndex);
        Assert.False(Controls<Control>(window).Single(control => control.Name == "OnboardingOverlay").IsVisible);
        Assert.Contains(Controls<Control>(window), control => control.Name == "Toast" && control.IsVisible);
        Assert.False(Nav(window, "NavigationDashboard").IsVisible);
        Assert.True(Controls<Control>(window).Single(control => control.Name == "GettingStarted").IsVisible);
        Capture(window, "checklist-new");

        // A follow-up with a due date makes the attention home and calendar appear, flagged as new.
        ClickPrefix(window, "Customer contracts · Document");
        Click(window, "Add follow-up task");
        Capture(window, "checklist-followup");
        Click(window, "Add task");
        await UntilAsync(() => Nav(window, "NavigationDashboard").IsVisible && Nav(window, "NavigationProjects").IsEffectivelyEnabled);
        var task = Assert.Single((await fixture.Projects.GetAsync(project.Id))!.Tasks);
        Assert.Equal(requirement.Id, task.RequirementId);
        Assert.StartsWith("Follow up:", task.Title);
        Assert.True(Nav(window, "NavigationCalendar").IsVisible);
        Assert.Contains(Nav(window, "NavigationDashboard").GetLogicalDescendants().OfType<Border>(), border => border.Name == "NewBadge" && border.IsVisible);
        Click(Nav(window, "NavigationDashboard"));
        await UntilAsync(() => Controls<Control>(window).Any(control => control.Name == "PriorityItems"));
        Assert.DoesNotContain(Nav(window, "NavigationDashboard").GetLogicalDescendants().OfType<Border>(), border => border.Name == "NewBadge" && border.IsVisible);

        Click(Controls<Button>(window).Single(button => button.Name == "GettingStartedHide"));
        Assert.False(Controls<Control>(window).Single(control => control.Name == "GettingStarted").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Sample_workspace_never_touches_real_data_and_leads_to_a_real_project()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var window = fixture.Window;
        window.Width = 1440; window.Height = 900;
        window.Show();
        await UntilAsync(() => Nav(window, "NavigationProjects").IsEffectivelyEnabled);
        Click(window, "Explore a sample deal");
        await UntilAsync(() => Controls<Control>(window).Any(control => control.Name == "SampleBanner") && Controls<Control>(window).Any(control => control.Name == "PriorityItems"));
        Assert.False(Controls<Control>(window).Single(control => control.Name == "OnboardingOverlay").IsVisible);
        foreach (var page in new[] { "NavigationDashboard", "NavigationCalendar", "NavigationDocuments" })
            Assert.True(Nav(window, page).IsVisible, page);
        Assert.Empty(await fixture.Projects.ListAsync());
        Assert.False(Controls<Control>(window).Single(control => control.Name == "GettingStarted").IsVisible);
        Capture(window, "sample-dashboard");

        Click(Nav(window, "NavigationProjects"));
        await UntilAsync(() => Controls<Button>(window).Count(button => button.Name == "ProjectRow") == 4);
        ClickPrefix(window, "Nova Logistics ·");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Controls<Control>(window).Any(control => control.Name == "SampleBanner"));
        Controls<TabControl>(window).Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "sample-checklist");
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "sample-tasks");
        Assert.Empty(await fixture.Projects.ListAsync());

        Click(window, "Start my own project");
        Controls<TextBox>(window).Single(box => box.Name == "WizardName").Text = "My first deal";
        Click(window, "Continue");
        Click(window, "Continue");
        Click(window, "Skip for now");
        await UntilAsync(() => !Controls<Control>(window).Any(control => control.Name == "SampleBanner") && Controls<TabControl>(window).Any() && Nav(window, "NavigationProjects").IsEffectivelyEnabled);
        var project = Assert.Single(await fixture.Projects.ListAsync());
        Assert.Equal("My first deal", project.Name);
        Assert.Equal(16, project.Requirements.Count);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Welcome_wizard_and_sample_render_right_to_left_in_the_dark_theme()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        fixture.Locale.SetLanguage("fa");
        var window = fixture.Window;
        window.Width = 1440; window.Height = 900;
        window.Show();
        await UntilAsync(() => Nav(window, "NavigationProjects").IsEffectivelyEnabled);
        Click(Controls<Button>(window).First(button => button.Classes.Contains("segment") && MainWindowTests.ButtonText(button) == "تیره"));
        Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, window.FlowDirection);
        Capture(window, "welcome-fa-dark");
        Click(window, "شروع اولین پروژه");
        Controls<TextBox>(window).Single(box => box.Name == "WizardName").Text = "نوا لجستیک";
        Dispatcher.UIThread.RunJobs();
        Capture(window, "wizard-1-fa-dark");
        Click(window, "ادامه");
        Capture(window, "wizard-2-fa-dark");
        Click(window, "بازگشت");
        Click(window, "بازگشت");
        Click(window, "مرور یک پروژهٔ نمونه");
        await UntilAsync(() => Controls<Control>(window).Any(control => control.Name == "SampleBanner") && Controls<Control>(window).Any(control => control.Name == "PriorityItems"));
        Capture(window, "sample-dashboard-fa-dark");
        Click(Nav(window, "NavigationProjects"));
        await UntilAsync(() => Controls<Button>(window).Count(button => button.Name == "ProjectRow") == 4);
        ClickPrefix(window, "نوا لجستیک ·");
        await UntilAsync(() => Controls<TabControl>(window).Any());
        Controls<TabControl>(window).Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "sample-checklist-fa-dark");
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "sample-tasks-fa-dark");
        window.Close();
    }

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static Button Nav(Window window, string name) => Controls<Button>(window).Single(button => button.Name == name);
    private static void Click(Button button) => UiWait.Click(button);
    private static void Click(Window window, string text) => UiWait.Click(window, button => MainWindowTests.ButtonText(button) == text, $"\"{text}\"");
    private static void ClickPrefix(Window window, string prefix) => UiWait.Click(window, button => MainWindowTests.ButtonText(button).StartsWith(prefix), $"starting with \"{prefix}\"");
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "UI did not reach the expected state within 10 seconds." + MainWindowTests.Fixture.DescribeLastWindow());
    }
    private static void Capture(Window window, string screen)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var directory = Environment.GetEnvironmentVariable("PROJECTOPS_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        for (var tick = 0; tick < 3; tick++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{screen}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
