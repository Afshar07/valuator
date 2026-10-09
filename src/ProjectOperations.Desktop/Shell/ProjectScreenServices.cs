using ProjectOperations.Core.Application;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// What a project screen's view-model asks of the shell, without controls: reload the open project, open the assistant on a job,
/// and run an action with the shell's busy state and error banner. <see cref="MainWindow"/> implements it until the shell itself is a view-model.
/// </summary>
internal interface IProjectHost
{
    /// <summary>Reloads the open project on its current tab.</summary>
    Task RefreshProjectAsync();
    Task ReviewInAssistantAsync(string jobId);
    /// <summary>Locks navigation and the page while the action runs, and reports a failure in the error banner instead of throwing.</summary>
    Task RunAsync(Func<Task> action);
}

/// <summary>The services every project-screen view-model takes (a screen of the open project, and the dialogs it opens).</summary>
internal sealed record ProjectScreenServices(ProjectService Projects, LocalizedStrings Strings, IDialogService Dialogs, IProjectHost Host, TimeProvider Clock);
