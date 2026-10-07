using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

/// <summary>
/// Clicks that wait for the target to exist and be enabled. Navigation and button handlers finish asynchronously,
/// so on a slow machine the next control may not be there yet when a test reaches for it.
/// </summary>
internal static class UiWait
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    /// <summary>Runs queued UI work until the condition holds or the limit passes.</summary>
    public static void Pump(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Limit;
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
    }

    public static void Click(Window window, Func<Button, bool> match, string description, bool last = false)
    {
        Button? Find()
        {
            var matches = window.GetLogicalDescendants().OfType<Button>().Distinct().Where(match).Where(button => button.IsEffectivelyEnabled);
            return last ? matches.LastOrDefault() : matches.FirstOrDefault();
        }
        Pump(() => Find() is not null);
        var button = Find();
        Assert.True(button is not null, $"No enabled button {description} appeared within {Limit.TotalSeconds:0} seconds.");
        Click(button!);
    }

    public static void Click(Button button)
    {
        Pump(() => button.IsEffectivelyEnabled);
        Assert.True(button.IsEffectivelyEnabled, "The button stayed disabled for 10 seconds.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
