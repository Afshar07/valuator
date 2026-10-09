using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ProjectOperations.Core.Domain;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

/// <summary>The project detail screens in the real window: the XAML wiring that the plain view-model tests cannot see.</summary>
public sealed class ProjectScreensUiTests
{
    private static IEnumerable<T> All<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static Button ButtonNamed(Window window, string name) => All<Button>(window).Single(button => button.Name == name && UiWait.IsShown(button));
    private static Button ButtonText(Window window, string text) => All<Button>(window).First(button => MainWindowTests.ButtonText(button) == text && UiWait.IsShown(button));

    private static async Task<(MainWindowTests.Fixture Fixture, Project Project)> OpenAsync(string name, bool blank = false)
    {
        var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = blank
            ? await fixture.Projects.CreateBlankAsync(name, "Company", ProjectStatus.Active, "Owner", "")
            : await fixture.Projects.CreateAsync(name, "Company", ProjectStatus.Active, "Owner", "");
        var window = fixture.Window;
        window.Show();
        UiWait.Click(window, button => button.Name == "NavigationProjects", "All projects");
        UiWait.Click(window, button => MainWindowTests.ButtonText(button).StartsWith(name + " ·"), name);
        UiWait.Pump(() => All<TabControl>(window).Any());
        return (fixture, project);
    }

    [AvaloniaFact]
    public async Task The_edit_dialog_keeps_the_dialog_open_on_a_blank_name_then_renames_the_project_in_the_header()
    {
        var (fixture, project) = await OpenAsync("Original");
        using var _ = fixture;
        var window = fixture.Window;

        UiWait.Click(ButtonNamed(window, "EditProjectButton"));
        UiWait.Pump(() => All<Control>(window).Any(control => control.Name == "EditProjectDialog"));
        var name = All<TextBox>(window).Single(box => box.Name == "ProjectNameInput");
        Assert.Equal("Original", name.Text);
        Assert.False(All<TextBlock>(window).Single(block => block.Name == "EditProjectError").IsVisible);

        name.Text = "";
        Dispatcher.UIThread.RunJobs();
        UiWait.Click(ButtonNamed(window, "SaveProjectButton"));
        Assert.True(All<TextBlock>(window).Single(block => block.Name == "EditProjectError").IsVisible);
        Assert.Contains(All<Control>(window), control => control.Name == "EditProjectDialog");

        name.Text = "Renamed";
        Dispatcher.UIThread.RunJobs();
        Assert.False(All<TextBlock>(window).Single(block => block.Name == "EditProjectError").IsVisible);
        UiWait.Click(ButtonNamed(window, "SaveProjectButton"));
        UiWait.Pump(() => All<TextBlock>(window).Any(block => block.Name == "ProjectTitle" && block.Text == "Renamed"));

        Assert.DoesNotContain(All<Control>(window), control => control.Name == "EditProjectDialog");
        Assert.Equal("Renamed", (await fixture.Projects.GetAsync(project.Id))!.Name);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_overview_add_line_opens_focused_and_saves_an_open_question()
    {
        var (fixture, project) = await OpenAsync("Questions");
        using var _ = fixture;
        var window = fixture.Window;
        var card = All<Control>(window).Single(control => control.Name == "OpenQuestionsCard");

        Assert.DoesNotContain(card.GetLogicalDescendants().OfType<TextBox>(), box => UiWait.IsShown(box));
        UiWait.Click(window, button => button.Name == "AddStateItem" && button.GetLogicalAncestors().Contains(card), "Add open question");
        UiWait.Pump(() => card.GetLogicalDescendants().OfType<TextBox>().Any(UiWait.IsShown));
        var input = card.GetLogicalDescendants().OfType<TextBox>().Single(UiWait.IsShown);
        UiWait.Pump(() => input.IsFocused);
        Assert.True(input.IsFocused);

        input.Text = "Who signs?";
        Dispatcher.UIThread.RunJobs();
        UiWait.Click(window, button => MainWindowTests.ButtonText(button) == "Save" && button.GetLogicalAncestors().Contains(card), "Save");
        UiWait.Pump(() => All<TextBlock>(window).Any(block => block.Text == "Who signs?"));

        Assert.Equal(["Who signs?"], (await fixture.Projects.GetAsync(project.Id))!.State.OpenQuestions);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_checklist_editor_is_indented_from_the_start_edge_in_both_directions()
    {
        var (fixture, _) = await OpenAsync("Indent");
        using var _ = fixture;
        var window = fixture.Window;
        All<TabControl>(window).Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        UiWait.Click(window, button => button.Name == "NextUpOpen", "Open item");
        UiWait.Pump(() => All<Border>(window).Any(border => border.Name == "RequirementDetail"));
        var detail = All<Border>(window).Single(border => border.Name == "RequirementDetail");

        Assert.Equal(new Thickness(48, 4, 16, 16), detail.Padding);
        fixture.Locale.SetLanguage("fa");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Thickness(16, 4, 48, 16), detail.Padding);
        fixture.Locale.SetLanguage("en");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Thickness(48, 4, 16, 16), detail.Padding);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_follow_up_task_is_added_from_the_open_item_and_a_status_chip_saves_at_once()
    {
        var (fixture, project) = await OpenAsync("Follow up");
        using var _ = fixture;
        var window = fixture.Window;
        All<TabControl>(window).Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        UiWait.Click(window, button => button.Name == "NextUpOpen", "Open item");
        UiWait.Pump(() => All<Button>(window).Any(button => button.Name == "AddFollowUpTask" && UiWait.IsShown(button)));

        UiWait.Click(ButtonNamed(window, "AddFollowUpTask"));
        UiWait.Pump(() => All<TextBox>(window).Any(box => box.Name == "FollowUpTitle" && UiWait.IsShown(box)));
        Assert.DoesNotContain(All<Button>(window), button => button.Name == "AddFollowUpTask" && UiWait.IsShown(button));
        UiWait.Click(ButtonNamed(window, "AddFollowUpSubmit"));
        UiWait.Pump(() => All<Button>(window).Any(button => button.Name == "AddFollowUpTask" && UiWait.IsShown(button)));

        var task = Assert.Single((await fixture.Projects.GetAsync(project.Id))!.Tasks);
        Assert.StartsWith("Follow up:", task.Title);
        Assert.NotNull(task.DueAt);
        Assert.NotNull(task.RequirementId);

        UiWait.Click(window, button => MainWindowTests.ButtonText(button) == "Needs review" && UiWait.IsShown(button), "Needs review");
        UiWait.Pump(() => (fixture.Projects.GetAsync(project.Id).Result)!.Requirements.Any(requirement => requirement.Status == RequirementStatus.NeedsReview));
        Assert.Equal(1, (await fixture.Projects.GetAsync(project.Id))!.Requirements.Count(requirement => requirement.Status == RequirementStatus.NeedsReview));
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_blank_project_adds_a_requirement_from_the_input_that_appears_when_asked_for()
    {
        var (fixture, project) = await OpenAsync("Blank deal", blank: true);
        using var _ = fixture;
        var window = fixture.Window;
        All<TabControl>(window).Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(All<TextBox>(window), box => box.Name == "NewRequirementTitle" && UiWait.IsShown(box));
        UiWait.Click(ButtonNamed(window, "AddRequirement"));
        UiWait.Pump(() => All<TextBox>(window).Any(box => box.Name == "NewRequirementTitle" && UiWait.IsShown(box)));
        var input = All<TextBox>(window).Single(box => box.Name == "NewRequirementTitle");
        UiWait.Pump(() => input.IsFocused);
        Assert.True(input.IsFocused);

        input.Text = "Customer contracts";
        Dispatcher.UIThread.RunJobs();
        UiWait.Click(ButtonNamed(window, "AddRequirementSubmit"));
        UiWait.Pump(() => (fixture.Projects.GetAsync(project.Id).Result)!.Requirements.Count == 1);

        Assert.Equal("Customer contracts", (await fixture.Projects.GetAsync(project.Id))!.Requirements.Single().Title);
        UiWait.Pump(() => All<Border>(window).Any(border => border.Name == "RequirementDetail"));
        Assert.Contains(All<Border>(window), border => border.Name == "RequirementDetail");
        window.Close();
    }
}
