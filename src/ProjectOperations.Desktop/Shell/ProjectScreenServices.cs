using ProjectOperations.Core.Application;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// What a project screen's view-model asks of the shell, without controls: reload the open project, open the assistant, and run an
/// action with the shell's busy state and error banner. <see cref="MainWindow"/> implements it until the shell itself is a view-model.
/// </summary>
internal interface IProjectHost
{
    /// <summary>Reloads the open project on its current tab.</summary>
    Task RefreshProjectAsync();
    Task ReviewInAssistantAsync(string jobId);
    /// <summary>Locks navigation and the page while the action runs, and reports a failure in the error banner instead of throwing.</summary>
    Task RunAsync(Func<Task> action);
    /// <summary>Shows an error in the banner by its localization key.</summary>
    void ShowError(string key);
    /// <summary>Shows a short confirmation by its localization key.</summary>
    void ShowToast(string key);
    /// <summary>Opens or closes the assistant panel.</summary>
    void ToggleAssistant();
    /// <summary>Opens the assistant panel (it stays open if it already is).</summary>
    void OpenAssistant();
    /// <summary>Records the tab the user picked, so a reload of the project returns to it.</summary>
    void SelectTab(ProjectTab tab);
}

/// <summary>A file the user chose in the system file picker. <paramref name="LocalPath"/> is null when it is not a local file.</summary>
internal sealed record PickedFile(string Name, string? LocalPath);

/// <summary>Asks the user to choose files. The only dialog a view-model needs besides its own modals.</summary>
internal interface IFilePicker
{
    /// <summary>The chosen files; empty when the user cancelled.</summary>
    Task<IReadOnlyList<PickedFile>> PickAsync(string title);
}

/// <summary>The services every project-screen view-model takes (a screen of the open project, and the dialogs it opens).</summary>
internal sealed record ProjectScreenServices(ProjectService Projects, LocalizedStrings Strings, IDialogService Dialogs, IProjectHost Host, TimeProvider Clock,
    INavigator Navigator, IFilePicker Files, UiState State);
