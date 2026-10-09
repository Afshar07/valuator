using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Assistant;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Infrastructure.Persistence;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>
/// What the assistant view-model runs against: a throw-away SQLite database with real services, a runtime the test controls
/// (<see cref="MainWindowTests.ControlledRuntime"/>), and a host that applies the shell's real run lock. Like the shell, the host reloads the
/// open project into the assistant after a run or a review. The clock is fixed on Friday 9 October 2026.
/// </summary>
internal sealed class AssistantScenario : IDisposable
{
    private readonly string _directory;

    private AssistantScenario(string directory, ProjectService projects, AgentService agents, MainWindowTests.ControlledRuntime runtime, bool configured)
    {
        _directory = directory; Projects = projects; Agents = agents; Runtime = runtime;
        Locale = new LocaleContext(Path.Combine(directory, "settings.json"));
        Strings = new LocalizedStrings(Locale, new LocalizationService(Locale));
        Workspace = new WorkspaceSession(projects, agents, new DesktopEnvironment(AgentConfigured: configured, Endpoint: "http://127.0.0.1:4096"), new NoExternalCalendar());
        Host = new FakeHost(this);
        Services = new AssistantServices(Workspace, Strings, Locale, Navigator, Host, () => "Synthetic test runtime", new PageScenario.FixedClock(PageScenario.Noon));
        Assistant = new AssistantViewModel(Services);
    }

    public ProjectService Projects { get; }
    public AgentService Agents { get; }
    public MainWindowTests.ControlledRuntime Runtime { get; }
    public LocaleContext Locale { get; }
    public LocalizedStrings Strings { get; }
    public WorkspaceSession Workspace { get; }
    public PageScenario.FakeNavigator Navigator { get; } = new();
    public ShellMessages Messages { get; } = new();
    public FakeHost Host { get; }
    public AssistantServices Services { get; }
    public AssistantViewModel Assistant { get; }

    public static async Task<AssistantScenario> CreateAsync(bool configured = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), "project-operations-assistant-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "assistant.db");
        var repository = new SqliteProjectRepository(database);
        await repository.InitializeAsync();
        var projects = new ProjectService(repository);
        var runtime = new MainWindowTests.ControlledRuntime();
        return new AssistantScenario(directory, projects, new AgentService(projects, runtime, new SqliteAgentJobRepository(database)), runtime, configured);
    }

    public Task<Project> AddProjectAsync(string name = "Deal", ProjectStatus status = ProjectStatus.Active, string company = "Company") =>
        Projects.CreateAsync(name, company, status, "", "");

    public async Task<Project> ReloadAsync(Project project) => (await Projects.GetAsync(project.Id))!;

    /// <summary>Opens the project in the assistant the way the shell does after loading it.</summary>
    public async Task<Project> OpenAsync(Project project)
    {
        var loaded = await ReloadAsync(project);
        Host.OpenProject = loaded.Id;
        await Assistant.ShowProjectAsync(loaded);
        return loaded;
    }

    /// <summary>Waits for a state that arrives through a progress callback (those are not posted back to a UI thread in a plain test).</summary>
    public static async Task UntilAsync(Func<bool> condition, string what = "the expected state")
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition(), $"The assistant did not reach {what} within 10 seconds.");
    }

    public void Dispose()
    {
        Runtime.Release.TrySetResult();
        Strings.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    internal sealed class FakeHost(AssistantScenario scenario) : IAssistantHost
    {
        private readonly AgentRunLock _lock = new(scenario.Messages);
        public bool ForceRunning { get; set; }
        public int Refreshes { get; private set; }
        public int Runs { get; private set; }
        public List<string> Errors { get; } = [];
        public List<bool> OpenStates { get; } = [];
        public Guid? OpenProject { get; set; }

        public bool IsAgentRunning => ForceRunning || _lock.IsRunning;
        public bool IsLocked => _lock.IsLocked;
        public Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh) => _lock.RunAsync(run, refresh);
        public void CancelRun() => _lock.Cancel();
        public void SetAssistantOpen(bool open) => OpenStates.Add(open);

        public async Task RefreshProjectAsync()
        {
            Refreshes++;
            if (OpenProject is { } id && await scenario.Projects.GetAsync(id) is { } project) await scenario.Assistant.ShowProjectAsync(project);
        }

        public Task RunAsync(Func<Task> action) { Runs++; return scenario.Messages.GuardAsync(action); }
        public void ShowError(string key) { Errors.Add(key); scenario.Messages.ShowError(key); }
    }
}
