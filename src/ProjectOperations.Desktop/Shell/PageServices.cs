using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// What a top-level page's view-model asks of the shell, without controls. <see cref="MainWindow"/> implements it until the
/// shell itself is a view-model.
/// </summary>
internal interface IPageHost
{
    /// <summary>Locks navigation and the page while the action runs, and reports a failure in the error banner instead of throwing.</summary>
    Task RunAsync(Func<Task> action);
    /// <summary>Shows an error in the banner by its localization key.</summary>
    void ShowError(string key);
    /// <summary>Opens or closes the assistant panel.</summary>
    void ToggleAssistant();
    /// <summary>Opens the new-project wizard over the current page.</summary>
    void ShowWizard();
    /// <summary>Shows the first-run welcome screen again.</summary>
    void ShowWelcome();
    /// <summary>True while an assistant job holds the application: nothing that closes or restarts it may proceed.</summary>
    bool IsAgentRunning { get; }
    /// <summary>Re-evaluates what the sidebar offers (sections appear as data arrives) and the getting-started card.</summary>
    Task RefreshShellAsync();
}

/// <summary>Opens files and folders in the operating system. The only filesystem access a view-model asks for besides an existence check.</summary>
internal interface IFileLauncher
{
    /// <summary>Opens the file with its default application; false when the system refused.</summary>
    Task<bool> OpenFileAsync(string path);
    /// <summary>Shows a folder in the system file browser; false when the system refused.</summary>
    Task<bool> OpenFolderAsync(string path);
}

/// <summary>The services every top-level page's view-model takes (Needs attention, All projects, Calendar, Documents).</summary>
internal sealed record PageServices(ProjectService Projects, LocalizedStrings Strings, IDialogService Dialogs, INavigator Navigator, IPageHost Host,
    IFileLauncher Files, IExternalCalendarSource Calendar, TimeProvider Clock)
{
    /// <summary>Navigates under the shell's busy state, so a failure lands in the error banner.</summary>
    public Task GoAsync(Route route) => Host.RunAsync(() => Navigator.GoToAsync(route));
}
