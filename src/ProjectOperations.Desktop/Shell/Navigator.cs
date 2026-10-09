namespace ProjectOperations.Desktop.Shell;

public interface INavigator
{
    /// <summary>The route on screen; null before the first navigation.</summary>
    Route? Current { get; }
    Task GoToAsync(Route route);
}

/// <summary>
/// Tracks the current route and asks the host to show the next one. The host returns false when it could not show the route
/// (for example a project that no longer exists); the previous route then stays current, as it does when showing throws.
/// </summary>
public sealed class Navigator(Func<Route, Task<bool>> show) : INavigator
{
    public Route? Current { get; private set; }

    public async Task GoToAsync(Route route)
    {
        var previous = Current;
        Current = route;
        var shown = false;
        try { shown = await show(route); }
        finally { if (!shown) Current = previous; }
    }

    /// <summary>Records a tab the user picked directly, so a reload returns to it.</summary>
    public void SelectTab(ProjectTab tab)
    {
        if (Current is ProjectRoute project) Current = project with { Tab = tab };
    }
}
