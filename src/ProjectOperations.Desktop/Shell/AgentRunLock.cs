namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// Holds navigation, editing and window close for the complete runtime lifetime of one agent job, then for the reload that
/// follows it. Stop cancels the job; closing the window waits for the job's outcome and skips the reload.
/// </summary>
public sealed class AgentRunLock(ShellMessages messages)
{
    private CancellationTokenSource? _cancellation;
    private Task? _running;

    /// <summary>True while the job itself runs (not during the reload that follows it).</summary>
    public bool IsRunning => _running is not null;

    /// <summary>True from the start of a job until its reload has finished.</summary>
    public bool IsLocked { get; private set; }

    public bool CloseRequested { get; private set; }

    /// <summary>Raised when <see cref="IsLocked"/> changes.</summary>
    public event EventHandler? LockChanged;

    public async Task RunAsync(Func<CancellationToken, Task> run, Func<Task> refresh)
    {
        _cancellation = new CancellationTokenSource();
        SetLocked(true);
        _running = messages.GuardAsync(() => run(_cancellation.Token));
        try { await _running; }
        finally
        {
            _running = null; _cancellation.Dispose(); _cancellation = null;
            // Reload while still locked: unlocking first would let the user act on screens that the pending reload then replaces.
            // A window that is closing only waits for the runtime outcome; it does not reload screens.
            try { if (!CloseRequested) await messages.GuardAsync(refresh, clearError: false); }
            finally { SetLocked(false); }
        }
    }

    public void Cancel() => _cancellation?.Cancel();

    /// <summary>Cancels a running job for a closing window and returns the job to wait for, or null when nothing runs.</summary>
    public Task? RequestClose()
    {
        if (_running is not { } running) return null;
        CloseRequested = true;
        _cancellation?.Cancel();
        return running;
    }

    private void SetLocked(bool locked)
    {
        IsLocked = locked;
        LockChanged?.Invoke(this, EventArgs.Empty);
    }
}
