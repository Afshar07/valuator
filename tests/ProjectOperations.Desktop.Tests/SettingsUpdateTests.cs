using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Updates;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class SettingsUpdateTests
{
    private static readonly AppUpdate Release = new("2.3.4", "## Improvements\n- Synthetic release notes\n- یادداشت آزمایشی", 2097152);

    [AvaloniaFact]
    public async Task Checking_is_manual_and_does_not_download_an_available_release()
    {
        using var fixture = new UpdateFixture();
        await fixture.OpenAsync();
        Assert.Equal(0, fixture.Updater.CheckCalls);
        Assert.Equal(0, fixture.Updater.DownloadCalls);
        Click(fixture.Window, "CheckForUpdates");
        Assert.Equal(1, fixture.Updater.CheckCalls);
        Assert.False(Named<Button>(fixture.Window, "CheckForUpdates").IsEnabled);
        Assert.Null(Find<Button>(fixture.Window, "DownloadUpdate"));
        fixture.Updater.CheckRelease.SetResult(Release);
        await UntilAsync(() => Find<Button>(fixture.Window, "DownloadUpdate") is not null);
        Assert.Equal(0, fixture.Updater.DownloadCalls);
        Assert.Equal(0, fixture.Updater.RestartCalls);
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("fa")]
    public async Task Available_version_and_unmodified_read_only_notes_survive_language_switch(string language)
    {
        using var fixture = new UpdateFixture();
        fixture.Inner.Locale.SetLanguage(language);
        await fixture.OpenAsync();
        await MakeAvailableAsync(fixture);
        Assert.Contains(fixture.Updater.CurrentVersion, Named<TextBlock>(fixture.Window, "CurrentVersion").Text!);
        Assert.Contains(Release.Version, Named<TextBlock>(fixture.Window, "UpdateAvailable").Text!);
        var notes = Named<TextBox>(fixture.Window, "UpdateNotes");
        Assert.True(notes.IsReadOnly);
        Assert.Equal(Release.ReleaseNotes, notes.Text);
        var check = Named<Button>(fixture.Window, "CheckForUpdates");
        var originalLabel = check.Content;
        fixture.Inner.Locale.SetLanguage(language == "en" ? "fa" : "en");
        Dispatcher.UIThread.RunJobs();
        Assert.Same(notes, Named<TextBox>(fixture.Window, "UpdateNotes"));
        Assert.Equal(Release.ReleaseNotes, notes.Text);
        Assert.NotEqual(originalLabel, check.Content);
        Assert.Equal(language == "en" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, fixture.Window.FlowDirection);
        Assert.Equal(1, fixture.Updater.CheckCalls);
    }

    [AvaloniaFact]
    public async Task No_new_release_reenables_manual_check_without_download_or_restart()
    {
        using var fixture = new UpdateFixture();
        await fixture.OpenAsync();
        Click(fixture.Window, "CheckForUpdates");
        fixture.Updater.CheckRelease.SetResult(null);
        await UntilAsync(() => Named<Button>(fixture.Window, "CheckForUpdates").IsEnabled);
        Assert.Null(Find<Button>(fixture.Window, "DownloadUpdate"));
        Assert.Null(Find<Control>(fixture.Window, "UpdateReady"));
        Assert.Null(Find<Control>(fixture.Window, "UpdateError"));
        Assert.Equal(0, fixture.Updater.DownloadCalls);
        Assert.Equal(0, fixture.Updater.RestartCalls);
    }

    [AvaloniaFact]
    public async Task Portable_copy_has_no_check_download_or_restart_actions()
    {
        using var fixture = new UpdateFixture();
        fixture.Updater.CanUpdate = false;
        await fixture.OpenAsync();
        Assert.NotNull(Find<TextBlock>(fixture.Window, "CurrentVersion"));
        Assert.Null(Find<Button>(fixture.Window, "CheckForUpdates"));
        Assert.Null(Find<Button>(fixture.Window, "DownloadUpdate"));
        Assert.Null(Find<Button>(fixture.Window, "RestartToUpdate"));
        Assert.Equal(0, fixture.Updater.CheckCalls);
    }

    [AvaloniaFact]
    public async Task Previously_downloaded_update_waits_for_explicit_restart_without_checking_again()
    {
        using var fixture = new UpdateFixture(pending: Release);
        await fixture.OpenAsync();
        Assert.Contains(Release.Version, Named<TextBlock>(fixture.Window, "UpdateReady").Text!);
        Assert.Equal(0, fixture.Updater.CheckCalls);
        Assert.Equal(0, fixture.Updater.DownloadCalls);
        Assert.Equal(0, fixture.Updater.RestartCalls);
        Click(fixture.Window, "RestartToUpdate");
        Assert.Same(Release, fixture.Updater.RestartedUpdate);
    }

    [AvaloniaFact]
    public async Task Download_progress_and_ready_state_persist_when_navigating_away_and_back()
    {
        using var fixture = new UpdateFixture();
        await fixture.OpenAsync();
        await MakeAvailableAsync(fixture);
        Click(fixture.Window, "DownloadUpdate");
        Assert.Same(Release, fixture.Updater.DownloadedUpdate);
        Assert.Equal(0, Named<ProgressBar>(fixture.Window, "UpdateProgress").Value);
        Assert.False(Named<Button>(fixture.Window, "CheckForUpdates").IsEnabled);
        var cancel = Named<Button>(fixture.Window, "CancelDownload");
        fixture.Updater.Progress!.Report(37);
        await UntilAsync(() => Named<ProgressBar>(fixture.Window, "UpdateProgress").Value == 37);
        Assert.Same(cancel, Named<Button>(fixture.Window, "CancelDownload"));
        fixture.Navigate("navigation.projects");
        await UntilAsync(() => Find<Control>(fixture.Window, "UpdatePanel") is null);
        fixture.Updater.Progress.Report(64);
        Dispatcher.UIThread.RunJobs();
        fixture.Navigate("presentation.navigationSettings");
        await UntilAsync(() => Find<ProgressBar>(fixture.Window, "UpdateProgress")?.Value == 64);
        Assert.False(fixture.Updater.DownloadCancellation.IsCancellationRequested);
        Assert.Equal(1, fixture.Updater.DownloadCalls);
        fixture.Navigate("navigation.projects");
        fixture.Updater.DownloadRelease.SetResult();
        await UntilAsync(() => fixture.Updater.DownloadFinished);
        Dispatcher.UIThread.RunJobs();
        fixture.Navigate("presentation.navigationSettings");
        await UntilAsync(() => Find<Button>(fixture.Window, "RestartToUpdate") is not null);
        Assert.Contains(Release.Version, Named<TextBlock>(fixture.Window, "UpdateReady").Text!);
        Assert.Null(Find<ProgressBar>(fixture.Window, "UpdateProgress"));
        Assert.Equal(0, fixture.Updater.RestartCalls);
        Click(fixture.Window, "RestartToUpdate");
        Assert.Equal(1, fixture.Updater.RestartCalls);
        Assert.Same(Release, fixture.Updater.RestartedUpdate);
    }

    [AvaloniaFact]
    public async Task Cancel_reaches_download_token_and_allows_retry_without_another_check()
    {
        using var fixture = new UpdateFixture();
        await fixture.OpenAsync();
        await MakeAvailableAsync(fixture);
        Click(fixture.Window, "DownloadUpdate");
        fixture.Updater.Progress!.Report(23);
        await UntilAsync(() => Named<ProgressBar>(fixture.Window, "UpdateProgress").Value == 23);
        Click(fixture.Window, "CancelDownload");
        Assert.True(fixture.Updater.DownloadCancellation.IsCancellationRequested);
        await UntilAsync(() => Find<Button>(fixture.Window, "DownloadUpdate") is not null);
        Assert.Null(Find<Control>(fixture.Window, "UpdateError"));
        Assert.Null(Find<Button>(fixture.Window, "RestartToUpdate"));
        Assert.Equal(Release.ReleaseNotes, Named<TextBox>(fixture.Window, "UpdateNotes").Text);
        fixture.Updater.ResetDownload();
        Click(fixture.Window, "DownloadUpdate");
        Assert.Equal(2, fixture.Updater.DownloadCalls);
        Assert.Equal(1, fixture.Updater.CheckCalls);
        Assert.Equal(0, Named<ProgressBar>(fixture.Window, "UpdateProgress").Value);
        Assert.False(fixture.Updater.DownloadCancellation.IsCancellationRequested);
        fixture.Updater.DownloadRelease.SetResult();
        await UntilAsync(() => Find<Button>(fixture.Window, "RestartToUpdate") is not null);
        Assert.Equal(0, fixture.Updater.RestartCalls);
    }

    [AvaloniaFact]
    public async Task Failed_check_shows_error_and_manual_retry_clears_it()
    {
        using var fixture = new UpdateFixture();
        await fixture.OpenAsync();
        Click(fixture.Window, "CheckForUpdates");
        fixture.Updater.CheckRelease.SetException(new InvalidOperationException("Synthetic check failure"));
        await UntilAsync(() => Find<TextBlock>(fixture.Window, "UpdateError") is not null);
        Assert.True(Named<Button>(fixture.Window, "CheckForUpdates").IsEnabled);
        Assert.Null(Find<Button>(fixture.Window, "DownloadUpdate"));
        fixture.Updater.CheckRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(fixture.Window, "CheckForUpdates");
        Assert.Null(Find<Control>(fixture.Window, "UpdateError"));
        fixture.Updater.CheckRelease.SetResult(Release);
        await UntilAsync(() => Find<Button>(fixture.Window, "DownloadUpdate") is not null);
        Assert.Equal(2, fixture.Updater.CheckCalls);
    }

    [AvaloniaFact]
    public async Task Failed_download_retains_release_and_retry_can_complete_then_restart_failure_is_visible()
    {
        using var fixture = new UpdateFixture();
        await fixture.OpenAsync();
        await MakeAvailableAsync(fixture);
        Click(fixture.Window, "DownloadUpdate");
        fixture.Updater.DownloadRelease.SetException(new InvalidOperationException("Synthetic download failure"));
        await UntilAsync(() => Find<TextBlock>(fixture.Window, "UpdateError") is not null);
        Assert.NotNull(Find<Button>(fixture.Window, "DownloadUpdate"));
        Assert.Null(Find<Button>(fixture.Window, "RestartToUpdate"));
        Assert.Equal(Release.ReleaseNotes, Named<TextBox>(fixture.Window, "UpdateNotes").Text);
        fixture.Updater.ResetDownload();
        Click(fixture.Window, "DownloadUpdate");
        Assert.Null(Find<Control>(fixture.Window, "UpdateError"));
        fixture.Updater.DownloadRelease.SetResult();
        await UntilAsync(() => Find<Button>(fixture.Window, "RestartToUpdate") is not null);
        fixture.Updater.RestartFailure = new InvalidOperationException("Synthetic restart failure");
        Click(fixture.Window, "RestartToUpdate");
        Assert.NotNull(Find<TextBlock>(fixture.Window, "UpdateError"));
        Assert.NotNull(Find<TextBlock>(fixture.Window, "UpdateReady"));
        fixture.Updater.RestartFailure = null;
        Click(fixture.Window, "RestartToUpdate");
        Assert.Equal(2, fixture.Updater.RestartCalls);
        Assert.Same(Release, fixture.Updater.RestartedUpdate);
    }

    private static T? Find<T>(Window window, string name) where T : Control => window.GetLogicalDescendants().OfType<T>().SingleOrDefault(control => control.Name == name);
    private static T Named<T>(Window window, string name) where T : Control => Assert.IsAssignableFrom<T>(Find<T>(window, name));
    private static void Click(Window window, string name) => UiWait.Click(window, button => button.Name == name, name);

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "Update UI did not reach the expected state within 10 seconds.");
    }

    private static async Task MakeAvailableAsync(UpdateFixture fixture)
    {
        Click(fixture.Window, "CheckForUpdates");
        fixture.Updater.CheckRelease.SetResult(Release);
        await UntilAsync(() => Find<Button>(fixture.Window, "DownloadUpdate") is not null);
    }

    /// <summary>Reuses the existing isolated services and locale without changing the shared fixture.</summary>
    private sealed class UpdateFixture : IDisposable
    {
        public MainWindowTests.Fixture Inner { get; } = new();
        public ControlledUpdater Updater { get; } = new();
        public MainWindow Window { get; }

        public UpdateFixture(AppUpdate? pending = null)
        {
            Updater.PendingUpdate = pending;
            Window = new MainWindow(Inner.Projects, Inner.Agents, Inner.InitializeAsync,
                "Synthetic updater tests — no network or installation.", Inner.Locale, updater: Updater);
        }

        public async Task OpenAsync()
        {
            await Inner.InitializeAsync();
            Window.Show();
            Navigate("presentation.navigationSettings");
            await UntilAsync(() => Find<Control>(Window, "UpdatePanel") is not null);
        }

        public void Navigate(string key)
        {
            var label = new LocalizationService(Inner.Locale).Get(key);
            UiWait.Click(Window, button => MainWindowTests.ButtonText(button) == label, label);
        }

        public void Dispose()
        {
            Updater.CheckRelease.TrySetResult(null);
            Updater.DownloadRelease.TrySetResult();
            Window.Close();
            Inner.Dispose();
        }
    }

    /// <summary>Controlled in-memory update boundary; never checks a feed, installs files, or restarts a process.</summary>
    private sealed class ControlledUpdater : IAppUpdater
    {
        public bool CanUpdate { get; set; } = true;
        public string CurrentVersion => "1.2.3";
        public AppUpdate? PendingUpdate { get; set; }
        public int CheckCalls { get; private set; }
        public int DownloadCalls { get; private set; }
        public int RestartCalls { get; private set; }
        public AppUpdate? DownloadedUpdate { get; private set; }
        public AppUpdate? RestartedUpdate { get; private set; }
        public CancellationToken DownloadCancellation { get; private set; }
        public IProgress<int>? Progress { get; private set; }
        public bool DownloadFinished { get; private set; }
        public Exception? RestartFailure { get; set; }
        public TaskCompletionSource<AppUpdate?> CheckRelease { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DownloadRelease { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppUpdate?> CheckAsync() { CheckCalls++; return CheckRelease.Task; }

        public async Task DownloadAsync(AppUpdate update, IProgress<int> progress, CancellationToken cancellation)
        {
            DownloadCalls++; DownloadedUpdate = update; Progress = progress; DownloadCancellation = cancellation;
            try { await DownloadRelease.Task.WaitAsync(cancellation); }
            finally { DownloadFinished = true; }
        }

        public void ResetDownload()
        {
            DownloadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            DownloadFinished = false;
        }

        public void RestartToInstall(AppUpdate update)
        {
            RestartCalls++; RestartedUpdate = update;
            if (RestartFailure is not null) throw RestartFailure;
        }
    }
}
