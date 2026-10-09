using ProjectOperations.Core.Application;

namespace ProjectOperations.Desktop.Shell;

/// <summary>What the first-run screens ask of the shell. They always use <see cref="Projects"/> as it is now: leaving the sample swaps it.</summary>
internal interface IOnboardingHost
{
    ProjectService Projects { get; }
    bool IsSample { get; }
    /// <summary>Runs an action under the shell's busy state; a failure lands in the error banner.</summary>
    Task RunAsync(Func<Task> action);
    void ShowError(string key);
    void ShowWelcome();
    /// <summary>Opens the wizard; <paramref name="fromWelcome"/> decides where Back on the first step goes.</summary>
    void ShowWizard(bool fromWelcome);
    void CloseOverlay();
    Task StartSampleAsync();
    /// <summary>Discards the sample workspace and returns to the user's own data.</summary>
    Task ExitSampleAsync();
    /// <summary>Opens a freshly created project on its checklist and announces it.</summary>
    Task OpenCreatedProjectAsync(Guid projectId);
}
