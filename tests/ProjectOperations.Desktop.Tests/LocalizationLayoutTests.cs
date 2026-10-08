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
}
