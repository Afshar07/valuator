using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class LocalizationLayoutTests
{
    [AvaloniaFact]
    public async Task Navigation_positions_mirror_in_Persian_and_restore_in_English()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var window = fixture.Window;
        window.Show();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        Button Find(string text) => window.GetLogicalDescendants().OfType<Button>().Single(button => Equals(button.Content, text));
        while (!Find("All projects").IsEffectivelyEnabled && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        Assert.True(Find("All projects").IsEffectivelyEnabled);
        double Position(Button button) => button.TranslatePoint(new Point(button.Bounds.Width / 2, 0), window)!.Value.X;
        void Layout() { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }

        Layout();
        Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        Assert.True(Position(Find("Attention")) < Position(Find("All projects")));
        fixture.Locale.SetLanguage("fa");
        Layout();
        Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
        Assert.True(Position(Find("نیازمند توجه")) > Position(Find("همهٔ پروژه‌ها")));
        fixture.Locale.SetLanguage("en");
        Layout();
        Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        Assert.True(Position(Find("Attention")) < Position(Find("All projects")));
        window.Close();
    }
}
