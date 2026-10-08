using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class PresentationTests
{
    [AvaloniaTheory]
    [InlineData("en", 1440)]
    [InlineData("fa", 1440)]
    [InlineData("en", 800)]
    [InlineData("fa", 800)]
    public async Task Designed_screens_remain_navigable_at_desktop_sizes(string language, int width)
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Atlas اطلس", "Synthetic Capital", ProjectStatus.Active, "Local owner", "Synthetic project notes");
        project.Requirements[0].Status = RequirementStatus.Complete;
        project.Requirements[1].Status = RequirementStatus.Provided;
        project.Requirements[2].Status = RequirementStatus.NeedsReview;
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Review updated financial model", DueAt = DateTimeOffset.Now.AddDays(-1) });
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Prepare IC brief", DueAt = DateTimeOffset.Now.AddDays(2) });
        project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "IC review", DueAt = DateTimeOffset.Now.AddDays(3) });
        project.State.Summary = "Reviewing supplied information";
        project.State.OpenQuestions.Add("Confirm board composition");
        project.State.FollowUps.Add("Request updated forecast");
        await fixture.Projects.SaveAsync(project);
        fixture.Locale.SetLanguage(language);
        var text = new LocalizationService(fixture.Locale);
        var window = fixture.Window;
        window.Width = width;
        window.Height = width == 800 ? 600 : 1000;
        window.Show();
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button).StartsWith(project.Name + " ·") && button.IsEffectivelyEnabled));
        Capture("dashboard");
        ClickPrefix(window, text.Get("navigation.projects"));
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button).StartsWith(project.Name + " · Synthetic Capital") && button.IsEffectivelyEnabled));
        ClickPrefix(window, project.Name + " · Synthetic Capital");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Buttons(window).First(button => MainWindowTests.ButtonText(button) == text.Get("navigation.projects")).IsEffectivelyEnabled);
        var tabs = Controls<TabControl>(window).Single();
        Assert.Equal(4, tabs.Items.Count);
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Equal(fixture.Locale.FlowDirection, tabs.FlowDirection);
        await Task.Delay(100); Dispatcher.UIThread.RunJobs();
        Capture("overview");
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        var requirement = new DomainDisplay(text).Requirement(project.Requirements[0]);
        ClickPrefix(window, requirement + " ·");
        Assert.Contains(Controls<Control>(window), control => control.Name == "RequirementDetail");
        Capture("requirement-editor");
        tabs.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Buttons(window), button => MainWindowTests.ButtonText(button) == text.Get("task.new"));
        Capture("tasks");
        tabs.SelectedIndex = 3;
        Dispatcher.UIThread.RunJobs();
        Assert.False(Buttons(window).Single(button => MainWindowTests.ButtonText(button) == text.Get("agent.run")).IsEnabled);
        Capture("delegation");
        Assert.Equal(0, fixture.Runtime.Calls);
        window.Close();

        void Capture(string screen)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var directory = Environment.GetEnvironmentVariable("PROJECTOPS_UI_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            for (var tick = 0; tick < 3; tick++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(directory, $"{screen}-{language}-{width}.png"));
        }
    }

    [AvaloniaFact]
    public async Task Dashboard_counts_stored_data_and_every_section_is_navigable()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var active = await fixture.Projects.CreateAsync("Synthetic active", "Company", ProjectStatus.Active, "", "");
        active.Requirements[0].Status = RequirementStatus.Complete;
        active.Requirements[1].Status = RequirementStatus.Provided;
        active.Requirements[2].Status = RequirementStatus.NeedsReview;
        active.Tasks.Add(new ProjectTask { ProjectId = active.Id, Title = "Overdue", DueAt = DateTimeOffset.Now.AddDays(-2) });
        active.Tasks.Add(new ProjectTask { ProjectId = active.Id, Title = "Already done", DueAt = DateTimeOffset.Now.AddDays(-2), Status = ProjectTaskStatus.Done });
        active.Milestones.Add(new Milestone { ProjectId = active.Id, Title = "Upcoming", DueAt = DateTimeOffset.Now.AddDays(2) });
        await fixture.Projects.SaveAsync(active);
        var completed = await fixture.Projects.CreateAsync("Synthetic completed", "Company", ProjectStatus.Completed, "", "");
        foreach (var requirement in completed.Requirements) requirement.Status = RequirementStatus.Complete;
        completed.Tasks.Add(new ProjectTask { ProjectId = completed.Id, Title = "Excluded deadline", DueAt = DateTimeOffset.Now.AddDays(-2) });
        await fixture.Projects.SaveAsync(completed);

        var window = fixture.Window;
        window.Show();
        // Startup keeps navigation locked until the first screen and its attention badge have loaded.
        await UntilAsync(() => Controls<Control>(window).Any(control => control.Name == "PriorityItems")
            && Controls<Control>(window).Single(control => control.Name == "NavigationCalendar").IsEffectivelyEnabled);
        string Stat(string name) => Controls<Control>(window).Single(card => card.Name == name).GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Name == "StatValue").Text!;
        Assert.Equal("1", Stat("DashboardOverdueStat"));
        Assert.Equal("0", Stat("DashboardUpcomingStat"));
        Assert.Equal("1", Stat("DashboardMilestonesStat"));
        Assert.Equal("1", Stat("DashboardActiveStat"));
        Assert.Contains(Controls<Control>(window), control => control.Name == "PriorityRow");
        Assert.True(Controls<Control>(window).Single(control => control.Name == "AgendaCard").IsVisible);
        Assert.DoesNotContain(Controls<Control>(window), control => control.Name == "GlobalSearch");
        foreach (var name in new[] { "NavigationCalendar", "NavigationDocuments", "NavigationSettings" })
            Assert.True(Controls<Control>(window).Single(control => control.Name == name).IsEffectivelyEnabled);
        Assert.Equal(0, fixture.Runtime.Calls);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Quick_actions_only_populate_request_and_requirement_edits_survive_language_switch()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Mixed Atlas اطلس", "Company", ProjectStatus.Active, "", "");
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button) == "All projects" && button.IsEffectivelyEnabled));
        OpenProject(window, project.Name);
        await UntilAsync(() => Controls<TabControl>(window).Any() && Buttons(window).First(button => MainWindowTests.ButtonText(button) == "All projects").IsEffectivelyEnabled);
        var tabs = Controls<TabControl>(window).Single();
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        ClickPrefix(window, "Operations information / execution process · Text");
        var value = Controls<TextBox>(window).Single(box => box.Name == "RequirementValue");
        value.Text = "Unsaved English / فارسی";
        fixture.Locale.SetLanguage("fa");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Unsaved English / فارسی", value.Text);
        Assert.Equal(FlowDirection.RightToLeft, value.FlowDirection);
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.Empty((await fixture.Projects.GetAsync(project.Id))!.Requirements[1].Value);

        fixture.Locale.SetLanguage("en");
        tabs.SelectedIndex = 3;
        Dispatcher.UIThread.RunJobs();
        var text = new LocalizationService(fixture.Locale);
        var prompt = Controls<TextBox>(window).Single(box => box.Name == "AgentPrompt");
        foreach (var preset in AgentPrompts.Actions)
        {
            Click(window, text.Get("agent.action." + preset.Id));
            Assert.Equal(preset.Prompt, prompt.Text);
            Assert.Equal(0, fixture.Runtime.Calls);
            Assert.False(Buttons(window).Single(button => MainWindowTests.ButtonText(button) == text.Get("agent.run")).IsEnabled);
        }
        window.Close();
    }

    [AvaloniaFact]
    public async Task Milestones_can_be_created_edited_completed_and_deleted_in_task_workspace()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Milestone project", "Company", ProjectStatus.Active, "", "");
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button) == "All projects" && button.IsEffectivelyEnabled));
        OpenProject(window, project.Name);
        await UntilAsync(() => Controls<TabControl>(window).Any() && Buttons(window).First(button => MainWindowTests.ButtonText(button) == "All projects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        var text = new LocalizationService(fixture.Locale);
        Click(window, text.Get("v3.newMs"));
        Controls<TextBox>(window).Single(box => box.Name == "MilestoneTitleInput").Text = "Synthetic review";
        Controls<CalendarDatePicker>(window).Single().SelectedDate = new DateTime(2030, 1, 15);
        Click(window, text.Get("v3.createMs"));
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button).StartsWith("Synthetic review ·") && button.IsEffectivelyEnabled));
        Assert.Single((await fixture.Projects.GetAsync(project.Id))!.Milestones);
        ClickPrefix(window, "Synthetic review ·");
        Controls<TextBox>(window).Single(box => box.Name == "MilestoneTitleInput").Text = "Reviewed milestone";
        Controls<CheckBox>(window).Single(check => check.Name == "MilestoneReached").IsChecked = true;
        Click(window, text.Get("v3.saveB"));
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button).StartsWith("Reviewed milestone ·") && button.IsEffectivelyEnabled));
        Assert.True((await fixture.Projects.GetAsync(project.Id))!.Milestones.Single().IsComplete);
        ClickPrefix(window, "Reviewed milestone ·");
        Click(window, text.Get("v3.del"));
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button) == text.Get("v3.newMs")) && Buttons(window).First(button => MainWindowTests.ButtonText(button) == text.Get("navigation.projects")).IsEffectivelyEnabled
            && !Buttons(window).Any(button => MainWindowTests.ButtonText(button).StartsWith("Reviewed milestone ·")));
        Assert.Empty((await fixture.Projects.GetAsync(project.Id))!.Milestones);
        window.Close();
    }

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static IEnumerable<Button> Buttons(Window window) => Controls<Button>(window);
    private static TextBox LabeledInput(Window window, string label)
    {
        var block = Controls<TextBlock>(window).Last(block => block.Text == label);
        var parent = Assert.IsType<StackPanel>(block.Parent);
        return Assert.IsType<TextBox>(parent.Children[parent.Children.IndexOf(block) + 1]);
    }
    private static void Click(Window window, string text) => UiWait.Click(window, button => MainWindowTests.ButtonText(button) == text, $"\"{text}\"");
    private static void ClickPrefix(Window window, string prefix) => UiWait.Click(window, button => MainWindowTests.ButtonText(button).StartsWith(prefix), $"starting with \"{prefix}\"");
    // Projects without open tasks are not on the dashboard, so tests reach them through All projects.
    private static void OpenProject(Window window, string name)
    {
        Click(window, "All projects");
        ClickPrefix(window, name + " ·");
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "UI did not reach the expected presentation state.");
    }
}
