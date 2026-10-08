using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Velopack;
using Velopack.Sources;

namespace ProjectOperations.Desktop.Updates;

/// <summary>Velopack implementation: releases come from the repository's GitHub Releases and only stable releases are considered.</summary>
internal sealed class VelopackAppUpdater : IAppUpdater
{
    public const string RepositoryUrl = "https://github.com/Afshar07/unnamed-harness";

    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, null, false));
    private UpdateInfo? _pending;

    public bool CanUpdate => _manager.IsInstalled;
    public string CurrentVersion => _manager.CurrentVersion?.ToString() ?? AppVersion.Current;

    public AppUpdate? PendingUpdate
    {
        get
        {
            if (!CanUpdate || _manager.UpdatePendingRestart is not { } asset) return null;
            _pending ??= new UpdateInfo(asset, false);
            return new AppUpdate(asset.Version.ToString(), asset.NotesMarkdown, asset.Size);
        }
    }

    public async Task<AppUpdate?> CheckAsync()
    {
        var info = await _manager.CheckForUpdatesAsync();
        _pending = info;
        if (info is null) return null;
        var release = info.TargetFullRelease;
        // Deltas are what is actually downloaded when available; otherwise the full package.
        var size = info.DeltasToTarget.Length > 0 ? info.DeltasToTarget.Sum(delta => delta.Size) : release.Size;
        var notes = string.IsNullOrWhiteSpace(release.NotesMarkdown) ? null : release.NotesMarkdown.Trim();
        return new AppUpdate(release.Version.ToString(), notes, size);
    }

    public Task DownloadAsync(AppUpdate update, IProgress<int> progress, CancellationToken cancellation)
    {
        var pending = Pending(update);
        return _manager.DownloadUpdatesAsync(pending, percent => progress.Report(percent), cancellation);
    }

    public void RestartToInstall(AppUpdate update)
    {
        var pending = Pending(update);
        // Hand over to the updater, then close normally so the window and the agent runtime shut down cleanly.
        _manager.WaitExitThenApplyUpdates(pending.TargetFullRelease, silent: false, restart: true);
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
        });
    }

    private UpdateInfo Pending(AppUpdate update) =>
        _pending is not null && _pending.TargetFullRelease.Version.ToString() == update.Version
            ? _pending
            : throw new InvalidOperationException("The update was not produced by the latest check.");
}
