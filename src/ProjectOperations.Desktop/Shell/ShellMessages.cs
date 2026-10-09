using CommunityToolkit.Mvvm.ComponentModel;
using ProjectOperations.Desktop.Common;

namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// The shell's error banner and toast, by localization key, and the guard that turns failed actions into a banner.
/// Views render the keys; this type holds no controls.
/// </summary>
public sealed partial class ShellMessages : ViewModelBase
{
    /// <summary>Key of the error shown in the banner, or null when the banner is hidden.</summary>
    [ObservableProperty] private string? _errorKey;

    /// <summary>Key of the most recent toast, or null once it was dismissed.</summary>
    [ObservableProperty] private string? _toastKey;

    /// <summary>Raised for every toast, including a repeat of the same key, so the view can restart its timer.</summary>
    public event EventHandler<string>? ToastShown;

    /// <summary>The most recent exception reported to the user as a generic failure; retained for diagnostics and tests.</summary>
    public Exception? LastFailure { get; private set; }

    public void ShowError(string key) => ErrorKey = key;
    public void HideError() => ErrorKey = null;

    public void ShowToast(string key)
    {
        ToastKey = key;
        ToastShown?.Invoke(this, key);
    }

    public void DismissToast() => ToastKey = null;

    /// <summary>Runs an action and reports cancellation or failure in the banner instead of letting it escape.</summary>
    public async Task GuardAsync(Func<Task> action, bool clearError = true)
    {
        if (clearError) HideError();
        try { await action(); }
        catch (OperationCanceledException) { ShowError("validation.operationCancelled"); }
        catch (Exception exception) { LastFailure = exception; ShowError("validation.operationFailed"); }
    }
}
