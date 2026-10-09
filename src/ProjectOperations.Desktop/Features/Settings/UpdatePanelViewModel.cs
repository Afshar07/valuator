using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Desktop.Updates;

namespace ProjectOperations.Desktop.Features.Settings;

/// <summary>
/// Application updates: current version, a manual check, the available version with its release notes, download progress and
/// restart-to-install. State lives in the shell's <see cref="UpdateController"/> so a download keeps going when the user leaves Settings;
/// this view-model only mirrors it while the page is shown.
/// </summary>
internal sealed partial class UpdatePanelViewModel : ViewModelBase, IDisposable
{
    private readonly UpdateController _updates;
    private readonly IPageHost _host;
    private UpdatePhase _shown;

    public UpdatePanelViewModel(SettingsServices services)
    {
        _updates = services.Updates; _host = services.Host; L = services.Strings;
        _shown = _updates.Phase;
        _updates.Changed += OnChanged;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string CurrentVersionText => L.Format("updates.version", Isolate(_updates.Updater.CurrentVersion));

    /// <summary>False for a portable or development copy: it shows the version and why it cannot update itself, and nothing else.</summary>
    public bool CanUpdate => _updates.Updater.CanUpdate;
    public bool IsPortable => !CanUpdate;
    public bool CanCheck => CanUpdate && _updates.Phase is not (UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Ready);

    public bool IsChecking => CanUpdate && _updates.Phase == UpdatePhase.Checking;
    public bool IsUpToDate => CanUpdate && _updates.Phase == UpdatePhase.UpToDate;

    /// <summary>A release is offered, whether or not its download has started.</summary>
    public bool IsOffered => CanUpdate && _updates.Phase is UpdatePhase.Available or UpdatePhase.Downloading && _updates.Update is not null;
    public bool IsDownloading => IsOffered && _updates.Phase == UpdatePhase.Downloading;
    public bool CanDownload => IsOffered && !IsDownloading;
    public bool IsReady => CanUpdate && _updates.Phase == UpdatePhase.Ready && _updates.Update is not null;

    public string AvailableText => _updates.Update is { } update ? L.Format("updates.available", Isolate(update.Version)) : "";
    public bool HasSize => _updates.Update is { SizeBytes: > 0 };
    public string SizeText => _updates.Update is { SizeBytes: > 0 } update ? L.Format("updates.size", Isolate(Size(update.SizeBytes))) : "";

    /// <summary>The release notes as published. Markdown is shown as written: it reads fine as text and avoids rendering remote content.</summary>
    public string? Notes => _updates.Update?.ReleaseNotes;
    public bool HasNotes => Notes is not null;
    public bool HasNoNotes => !HasNotes;

    public int Percent => _updates.Percent;
    public string PercentText => L.Format("updates.downloading", L.Number(_updates.Percent));
    public string ReadyText => _updates.Update is { } update ? L.Format("updates.ready", Isolate(update.Version)) : "";

    public bool HasError => CanUpdate && _updates.ErrorKey is not null;
    public string ErrorText => _updates.ErrorKey is { } key ? L[key] : "";

    // These run on click and never lock the page, so navigation stays available during a check or a download.
    [RelayCommand(CanExecute = nameof(CanCheck))]
    private void Check() => _ = _updates.CheckAsync();

    [RelayCommand]
    private void Download() => _ = _updates.DownloadAsync();

    [RelayCommand]
    private void CancelDownload() => _updates.CancelDownload();

    [RelayCommand]
    private void Restart()
    {
        if (_host.IsAgentRunning) { _host.ShowError("updates.agentRunning"); return; }
        _updates.RestartToInstall();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        // Progress ticks only move the bar, so the buttons the user may be about to press are left alone.
        if (_updates.Phase == UpdatePhase.Downloading && _shown == UpdatePhase.Downloading)
        {
            OnPropertyChanged(nameof(Percent)); OnPropertyChanged(nameof(PercentText));
            return;
        }
        _shown = _updates.Phase;
        OnPropertyChanged(string.Empty);
        CheckCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() => _updates.Changed -= OnChanged;

    /// <summary>Wrapped in directional isolates so right-to-left text keeps version digits in order.</summary>
    private static string Isolate(string text) => "\u2066" + text + "\u2069";

    private string Size(long bytes)
    {
        var megabytes = bytes / 1048576d;
        return megabytes >= 1 ? megabytes.ToString("0.#", L.Culture) + " MB" : Math.Max(1, bytes / 1024) + " KB";
    }
}
