using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Infrastructure.Persistence;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

public sealed class ShellServiceTests
{
    [Fact]
    public async Task Navigator_tracks_the_current_route_and_keeps_the_previous_one_when_showing_fails()
    {
        var id = Guid.NewGuid();
        var shown = new List<Route>();
        var navigator = new Navigator(route => { shown.Add(route); return Task.FromResult(route is not ProjectRoute { ProjectId: var missing } || missing == id); });
        Assert.Null(navigator.Current);

        await navigator.GoToAsync(new PageRoute(AppPage.Projects));
        await navigator.GoToAsync(new ProjectRoute(Guid.NewGuid()));
        Assert.Equal(new PageRoute(AppPage.Projects), navigator.Current);

        await navigator.GoToAsync(new ProjectRoute(id, ProjectTab.Tasks));
        navigator.SelectTab(ProjectTab.Delegate);
        Assert.Equal(new ProjectRoute(id, ProjectTab.Delegate), navigator.Current);
        Assert.Equal(3, shown.Count);

        var failing = new Navigator(_ => throw new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.GoToAsync(new PageRoute(AppPage.Settings)));
        Assert.Null(failing.Current);
        failing.SelectTab(ProjectTab.Tasks);
        Assert.Null(failing.Current);
    }

    [Fact]
    public async Task Messages_guard_actions_and_report_toasts_by_key()
    {
        var messages = new ShellMessages();
        var toasts = new List<string>();
        messages.ToastShown += (_, key) => toasts.Add(key);
        messages.ShowToast("a"); messages.ShowToast("a");
        Assert.Equal(["a", "a"], toasts);
        messages.DismissToast();
        Assert.Null(messages.ToastKey);

        await messages.GuardAsync(() => throw new OperationCanceledException());
        Assert.Equal("validation.operationCancelled", messages.ErrorKey);
        Assert.Null(messages.LastFailure);

        var failure = new InvalidOperationException("boom");
        await messages.GuardAsync(() => throw failure);
        Assert.Equal("validation.operationFailed", messages.ErrorKey);
        Assert.Same(failure, messages.LastFailure);

        messages.ShowError("kept");
        await messages.GuardAsync(() => Task.CompletedTask, clearError: false);
        Assert.Equal("kept", messages.ErrorKey);
        await messages.GuardAsync(() => Task.CompletedTask);
        Assert.Null(messages.ErrorKey);
    }

    [Fact]
    public async Task Run_lock_stays_locked_through_the_reload_and_stop_cancels_the_job()
    {
        var runLock = new AgentRunLock(new ShellMessages());
        var states = new List<bool>();
        runLock.LockChanged += (_, _) => states.Add(runLock.IsLocked);
        var started = new TaskCompletionSource();
        var reloadSawLock = false;
        var running = runLock.RunAsync(async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); },
            () => { reloadSawLock = runLock.IsLocked && !runLock.IsRunning; return Task.CompletedTask; });
        await started.Task;
        Assert.True(runLock.IsRunning);
        Assert.True(runLock.IsLocked);

        runLock.Cancel();
        await running;

        Assert.True(reloadSawLock);
        Assert.False(runLock.IsLocked);
        Assert.False(runLock.IsRunning);
        Assert.Equal([true, false], states);
    }

    [Fact]
    public async Task Closing_waits_for_the_job_without_reloading_screens()
    {
        var runLock = new AgentRunLock(new ShellMessages());
        Assert.Null(runLock.RequestClose());
        var started = new TaskCompletionSource();
        var reloaded = false;
        var running = runLock.RunAsync(async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); }, () => { reloaded = true; return Task.CompletedTask; });
        await started.Task;

        var waiting = runLock.RequestClose();
        Assert.NotNull(waiting);
        await waiting!;
        await running;

        Assert.True(runLock.CloseRequested);
        Assert.False(reloaded);
        Assert.False(runLock.IsLocked);
    }

    [Fact]
    public async Task Workspace_session_swaps_to_the_sample_and_back_without_touching_real_services()
    {
        var directory = Path.Combine(Path.GetTempPath(), "project-operations-session-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var database = Path.Combine(directory, "real.db");
            var repository = new SqliteProjectRepository(database);
            await repository.InitializeAsync();
            var projects = new ProjectService(repository);
            var agents = new AgentService(projects, new NoRuntime(), new SqliteAgentJobRepository(database));
            var calendar = new RecordingCalendar();
            var environment = new DesktopEnvironment(true, "http://127.0.0.1:1", null, database);
            using var session = new WorkspaceSession(projects, agents, environment, calendar);
            Assert.Same(projects, session.Projects);
            Assert.Same(calendar, session.Calendar);
            Assert.False(session.IsSample);
            Assert.False(session.ExitSample());

            Assert.True(await session.StartSampleAsync(persian: false));
            Assert.True(session.IsSample);
            Assert.NotSame(projects, session.Projects);
            Assert.NotSame(agents, session.Agents);
            Assert.NotSame(calendar, session.Calendar);
            Assert.False(session.Environment.AgentConfigured);
            Assert.Equal(database, session.Environment.DatabasePath);
            Assert.NotEmpty(await session.Projects.ListAsync());
            Assert.False(await session.StartSampleAsync(persian: false));

            Assert.True(session.ExitSample());
            Assert.Same(projects, session.Projects);
            Assert.Same(agents, session.Agents);
            Assert.Same(calendar, session.Calendar);
            Assert.True(session.Environment.AgentConfigured);
            Assert.Empty(await session.Projects.ListAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    private sealed class NoRuntime : IAgentRuntime
    {
        public Task<AgentResult> RunAsync(AgentRequest request, IProgress<AgentEvent> progress, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelAsync(string jobId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingCalendar : IExternalCalendarSource
    {
        public bool IsAvailable => true;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExternalCalendarEvent>>([]);
    }
}
