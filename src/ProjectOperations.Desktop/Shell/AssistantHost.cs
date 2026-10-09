using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop.Shell;

/// <summary>The assistant as the shell drives it: which project it works on, and which finished job to review.</summary>
internal interface IAssistantPanel
{
    /// <summary>Lists the projects the assistant can work on (shown when no project is open).</summary>
    Task ShowPickerAsync();
    Task ShowProjectAsync(Project project);
    /// <summary>Opens the review tray of a finished job.</summary>
    Task ReviewAsync(string jobId);
}

/// <summary>What the assistant asks of the shell: the run lock for an agent job, its open state and the busy/error services every screen uses.</summary>
internal interface IAssistantHost
{
    /// <summary>True while an agent job holds the application (not during the reload that follows it).</summary>
    bool IsAgentRunning { get; }
    /// <summary>Locks navigation, project tabs and close for the complete runtime lifetime of one agent job, then for <paramref name="refresh"/>.</summary>
    Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh);
    /// <summary>Asks the running job to stop; the job ends when the runtime confirms it.</summary>
    void CancelRun();
    void SetAssistantOpen(bool open);
    /// <summary>Reloads the open project on its current tab (after a review or a run); the assistant keeps its controls.</summary>
    Task RefreshProjectAsync();
    /// <summary>Locks navigation and the page while the action runs, and reports a failure in the error banner instead of throwing.</summary>
    Task RunAsync(Func<Task> action);
    /// <summary>Shows an error in the banner by its localization key.</summary>
    void ShowError(string key);
}
