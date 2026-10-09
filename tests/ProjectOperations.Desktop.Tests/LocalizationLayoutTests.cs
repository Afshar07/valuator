using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class LocalizationLayoutTests
{
    [AvaloniaFact]
    public async Task Navigation_positions_mirror_in_Persian_and_restore_in_English()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Layout project", "", ProjectStatus.Active, "", "");
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Dated task", DueAt = DateTimeOffset.Now.AddDays(2) });
        await fixture.Projects.SaveAsync(project);
        var window = fixture.Window;
        window.Show();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        Button Find(string text) => window.GetLogicalDescendants().OfType<Button>().Single(button => MainWindowTests.ButtonText(button) == text);
        while (!Find("All projects").IsEffectivelyEnabled && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Assert.True(Find("All projects").IsEffectivelyEnabled);
        double Position(Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, 0), window)!.Value.X;
        TextBlock Heading(string text) => window.GetLogicalDescendants().OfType<TextBlock>().Single(block => block.Text == text);
        void Layout() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }

        Layout();
        Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        Assert.True(Position(Find("All projects")) < Position(Heading("What needs your attention?")));
        fixture.Locale.SetLanguage("fa");
        Layout();
        Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
        Assert.True(Position(Find("همهٔ پروژه‌ها")) > Position(Heading(new LocalizationService(fixture.Locale).Get("dashboard.title"))));
        fixture.Locale.SetLanguage("en");
        Layout();
        Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        Assert.True(Position(Find("All projects")) < Position(Heading("What needs your attention?")));
        window.Close();
    }

    [AvaloniaFact]
    public void Single_line_inputs_detect_their_alignment_so_the_caret_follows_the_text_and_multi_line_ones_start_at_the_edge()
    {
        using var fixture = new MainWindowTests.Fixture();
        var single = new TextBox { TextWrapping = TextWrapping.NoWrap };
        var notes = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        var window = fixture.Window;
        window.Content = new StackPanel { Children = { single, notes } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // A no-wrap right-to-left box with a fixed alignment puts its caret far from the text.
        Assert.Equal(TextAlignment.DetectFromContent, single.TextAlignment);
        Assert.Equal(TextAlignment.Start, notes.TextAlignment);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Add_requirement_input_is_wide_enough_to_type_in_Persian()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        await fixture.Projects.CreateBlankAsync("Blank deal", "", ProjectStatus.Active, "", "");
        fixture.Locale.SetLanguage("fa");
        var window = fixture.Window;
        window.Show();
        UiWait.Pump(() => window.GetLogicalDescendants().OfType<Button>().Any(button => button.IsEffectivelyEnabled && MainWindowTests.ButtonText(button).StartsWith("Blank deal")));
        UiWait.Click(window, button => MainWindowTests.ButtonText(button).StartsWith("Blank deal"), "Blank deal");
        UiWait.Pump(() => window.GetLogicalDescendants().OfType<TabControl>().Any());
        window.GetLogicalDescendants().OfType<TabControl>().Distinct().Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        UiWait.Click(window, button => button.Name == "AddRequirement", "Add requirement");
        UiWait.Pump(() => window.GetLogicalDescendants().OfType<TextBox>().Any(box => box.Name == "NewRequirementTitle"));
        window.UpdateLayout();

        var input = window.GetLogicalDescendants().OfType<TextBox>().Distinct().Single(box => box.Name == "NewRequirementTitle");
        Assert.True(input.Bounds.Width >= 320, $"The input was only {input.Bounds.Width:0} px wide.");
        window.Close();
    }
}
