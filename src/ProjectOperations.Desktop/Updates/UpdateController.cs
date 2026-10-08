namespace ProjectOperations.Desktop.Updates;

internal enum UpdatePhase { Idle, Checking, UpToDate, Available, Downloading, Ready }

/// <summary>
/// Update state for the running session. It lives in the shell, not in the Settings screen, so a download keeps going
/// (and its progress is still shown) when the user leaves Settings and comes back. Everything runs on the UI thread.
/// </summary>
internal sealed class UpdateController(IAppUpdater updater)
{
    private CancellationTokenSource? _download;

    public IAppUpdater Updater { get; } = updater;
    public UpdatePhase Phase { get; private set; } = updater.PendingUpdate is null ? UpdatePhase.Idle : UpdatePhase.Ready;
    public AppUpdate? Update { get; private set; } = updater.PendingUpdate;
    /// <summary>Download progress, 0 to 100.</summary>
    public int Percent { get; private set; }
    /// <summary>Localization key of the last failure, or null. Cleared by the next state change.</summary>
    public string? ErrorKey { get; private set; }
    public event EventHandler? Changed;

    public async Task CheckAsync()
    {
        if (!Updater.CanUpdate || Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Ready) return;
        Update = null; Set(UpdatePhase.Checking);
        try
        {
            Update = await Updater.CheckAsync();
            Set(Update is null ? UpdatePhase.UpToDate : UpdatePhase.Available);
        }
        catch (Exception)
        {
            Update = null; Set(UpdatePhase.Idle, "updates.checkFailed");
        }
    }

    public async Task DownloadAsync()
    {
        if (Update is null || Phase != UpdatePhase.Available) return;
        var update = Update;
        using var source = new CancellationTokenSource();
        _download = source; Percent = 0; Set(UpdatePhase.Downloading);
        try
        {
            // Progress<T> posts back to the UI thread, where this controller was created.
            var progress = new Progress<int>(value =>
            {
                if (Phase != UpdatePhase.Downloading || !ReferenceEquals(_download, source)) return;
                Percent = Math.Clamp(value, 0, 100); Changed?.Invoke(this, EventArgs.Empty);
            });
            await Updater.DownloadAsync(update, progress, source.Token);
            Percent = 100; Set(UpdatePhase.Ready);
        }
        catch (OperationCanceledException) { Set(UpdatePhase.Available); }
        catch (Exception) { Set(UpdatePhase.Available, "updates.downloadFailed"); }
        finally { _download = null; }
    }

    public void CancelDownload() => _download?.Cancel();

    /// <summary>Closes the app, installs the downloaded update and starts the new version.</summary>
    public void RestartToInstall()
    {
        if (Phase != UpdatePhase.Ready || Update is null) return;
        try { Updater.RestartToInstall(Update); }
        catch (Exception) { Set(UpdatePhase.Ready, "updates.restartFailed"); }
    }

    private void Set(UpdatePhase phase, string? error = null)
    {
        Phase = phase; ErrorKey = error; Changed?.Invoke(this, EventArgs.Empty);
    }
}
