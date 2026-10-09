using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

/// <summary>
/// Clicks that wait for the target to exist, be shown and be enabled (a view-model may hide a control that stays in the logical tree). Navigation and button handlers finish asynchronously,
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
            var matches = window.GetLogicalDescendants().OfType<Button>().Distinct().Where(match).Where(button => IsShown(button) && button.IsEffectivelyEnabled);
            return last ? matches.LastOrDefault() : matches.FirstOrDefault();
        }
        Pump(() => Find() is not null);
        var button = Find();
        Assert.True(button is not null, $"No enabled button {description} appeared within {Limit.TotalSeconds:0} seconds.");
        Click(button!);
    }

    /// <summary>
    /// A control is shown when it and every logical ancestor are visible. <c>IsEffectivelyVisible</c> alone is not enough: content a tab
    /// showed earlier stays in the logical tree after the tab is left, detached from the visual tree, so its hidden parts still report visible.
    /// </summary>
    public static bool IsShown(Control control) => control.IsEffectivelyVisible && control.GetLogicalAncestors().OfType<Control>().All(ancestor => ancestor.IsVisible);

    public static void Click(Button button)
    {
        Pump(() => button.IsEffectivelyEnabled);
        Assert.True(button.IsEffectivelyEnabled, "The button stayed disabled for 10 seconds.");
        // Invoke as an assistive technology does: it runs the Click handlers and the bound Command, like a real click. Raising ClickEvent alone skips the Command.
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(button));
        invoke.Invoke();
        Dispatcher.UIThread.RunJobs();
    }
}
