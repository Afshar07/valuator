using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Settings;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Desktop.Updates;
using ProjectOperations.Infrastructure.Persistence;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>
/// What the top-level page view-models run against: a throw-away SQLite database, English strings, a clock fixed at noon UTC on
/// Friday 9 October 2026, and recording fakes for everything the shell would do (navigation, dialogs, files, the external calendar).
/// </summary>
internal sealed class PageScenario : IDisposable
{
    public static readonly DateTimeOffset Noon = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly string _directory;

    private PageScenario(string directory, ProjectService projects)
    {
        _directory = directory; Projects = projects;
        SettingsPath = Path.Combine(directory, "settings.json");
        Locale = new LocaleContext(SettingsPath);
        Appearance = new AppearanceContext(SettingsPath);
        Strings = new LocalizedStrings(Locale, new LocalizationService(Locale));
        Services = new PageServices(projects, Strings, Dialogs, Navigator, Host, Files, Calendar, Clock);
        Environment = new DesktopEnvironment(true, "http://127.0.0.1:4096", null, Path.Combine(directory, "pages.db"));
    }

    public ProjectService Projects { get; }
    /// <summary>Where the language and theme are stored. Making it a directory makes every save fail.</summary>
    public string SettingsPath { get; }
    public LocaleContext Locale { get; }
    public AppearanceContext Appearance { get; }
    public LocalizedStrings Strings { get; }
    public TimeProvider Clock { get; } = new FixedClock(Noon);
    public FakeHost Host { get; } = new();
    public FakeNavigator Navigator { get; } = new();
    public FakeDialogs Dialogs { get; } = new();
    public FakeFiles Files { get; } = new();
    public FakeCalendar Calendar { get; } = new();
    public FakeUpdater Updater { get; } = new();
    public PageServices Services { get; }

    /// <summary>The runtime facts Settings shows. Replace before building <see cref="SettingsServices"/> to test another configuration.</summary>
    public DesktopEnvironment Environment { get; set; }

    /// <summary>
    /// The Settings page's services. The updater defaults to the scenario's fake; pass <paramref name="calendar"/> to replace the external
    /// calendar (for example one that is not available) and <paramref name="updates"/> to start from a given update state.
    /// </summary>
    public SettingsServices SettingsServices(UpdateController? updates = null, IExternalCalendarSource? calendar = null) =>
        new(calendar is null ? Services : Services with { Calendar = calendar }, Locale, Appearance, Environment, updates ?? new UpdateController(Updater));

    public static async Task<PageScenario> CreateAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "project-operations-pages-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var repository = new SqliteProjectRepository(Path.Combine(directory, "pages.db"));
        await repository.InitializeAsync();
        return new PageScenario(directory, new ProjectService(repository));
    }

    public async Task<Project> AddProjectAsync(string name, string company = "", ProjectStatus status = ProjectStatus.Active, string owner = "")
    {
        var project = await Projects.CreateAsync(name, company, status, owner, "");
        return project;
    }

    public async Task<Project> ReloadAsync(Project project) => (await Projects.GetAsync(project.Id))!;

    public static ProjectTask Task(Project project, string title, DateTimeOffset? due = null, ProjectTaskStatus status = ProjectTaskStatus.Todo) =>
        new() { ProjectId = project.Id, Title = title, DueAt = due, Status = status };

    public void Dispose()
    {
        Strings.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    public string TempFile(string name, string content = "synthetic")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    public string MissingFile(string name) => Path.Combine(_directory, "gone", name);

    internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    internal sealed class FakeHost : IPageHost
    {
        public int Runs { get; private set; }
        public List<string> Errors { get; } = [];
        public int AssistantToggles { get; private set; }
        public int WizardsShown { get; private set; }
        public int ShellRefreshes { get; private set; }
        public int WelcomesShown { get; private set; }
        public bool IsAgentRunning { get; set; }
        public Task RunAsync(Func<Task> action) { Runs++; return action(); }
        public void ShowError(string key) => Errors.Add(key);
        public void ToggleAssistant() => AssistantToggles++;
        public void ShowWizard() => WizardsShown++;
        public void ShowWelcome() => WelcomesShown++;
        public Task RefreshShellAsync() { ShellRefreshes++; return System.Threading.Tasks.Task.CompletedTask; }
    }

    internal sealed class FakeNavigator : INavigator
    {
        public List<Route> Visited { get; } = [];
        public Route? Current => Visited.LastOrDefault();
        public Task GoToAsync(Route route) { Visited.Add(route); return System.Threading.Tasks.Task.CompletedTask; }
    }

    internal sealed class FakeDialogs : IDialogService
    {
        public object? Shown { get; private set; }
        public bool IsOpen => Shown is not null;
        public void Show(object content) => Shown = content;
        public void Close() => Shown = null;
    }

    internal sealed class FakeFiles : IFileLauncher
    {
        public bool Succeeds { get; set; } = true;
        public List<string> Opened { get; } = [];
        public List<string> Revealed { get; } = [];
        public Task<bool> OpenFileAsync(string path) { Opened.Add(path); return System.Threading.Tasks.Task.FromResult(Succeeds); }
        public Task<bool> OpenFolderAsync(string path) { Revealed.Add(path); return System.Threading.Tasks.Task.FromResult(Succeeds); }
    }

    internal sealed class FakeCalendar : IExternalCalendarSource
    {
        public bool Connected { get; set; }
        public bool Available { get; set; } = true;
        public Exception? Failure { get; set; }
        public Exception? ConnectFailure { get; set; }
        /// <summary>When set, a sign-in waits on it (or on cancellation) instead of finishing at once.</summary>
        public TaskCompletionSource? ConnectGate { get; set; }
        public int ConnectCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public List<ExternalCalendarEvent> Events { get; } = [];
        public List<(DateTimeOffset From, DateTimeOffset To)> Requests { get; } = [];
        public bool IsAvailable => Available;
        public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(Connected);

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
            if (ConnectGate is not null) await ConnectGate.Task.WaitAsync(cancellationToken);
            if (ConnectFailure is not null) throw ConnectFailure;
            Connected = true;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCalls++; Connected = false;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            Requests.Add((from, to));
            if (Failure is not null) throw Failure;
            return System.Threading.Tasks.Task.FromResult<IReadOnlyList<ExternalCalendarEvent>>(Events.Where(item => item.Start >= from && item.Start < to).ToList());
        }
    }

    /// <summary>An updater whose check and download finish when the test says so. It never touches a feed, files or the process.</summary>
    internal sealed class FakeUpdater : IAppUpdater
    {
        public bool CanUpdate { get; set; } = true;
        public string CurrentVersion => "1.2.3";
        public AppUpdate? PendingUpdate { get; set; }
        public int CheckCalls { get; private set; }
        public int DownloadCalls { get; private set; }
        public int RestartCalls { get; private set; }
        public AppUpdate? RestartedUpdate { get; private set; }
        public CancellationToken DownloadCancellation { get; private set; }
        public IProgress<int>? Progress { get; private set; }
        public Exception? RestartFailure { get; set; }
        public TaskCompletionSource<AppUpdate?> CheckRelease { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DownloadRelease { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppUpdate?> CheckAsync() { CheckCalls++; return CheckRelease.Task; }

        public async Task DownloadAsync(AppUpdate update, IProgress<int> progress, CancellationToken cancellation)
        {
            DownloadCalls++; Progress = progress; DownloadCancellation = cancellation;
            await DownloadRelease.Task.WaitAsync(cancellation);
        }

        public void ResetDownload() => DownloadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RestartToInstall(AppUpdate update)
        {
            RestartCalls++; RestartedUpdate = update;
            if (RestartFailure is not null) throw RestartFailure;
        }
    }
}
