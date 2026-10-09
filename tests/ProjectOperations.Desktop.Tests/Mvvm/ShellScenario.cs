using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Desktop.Updates;
using ProjectOperations.Infrastructure.Persistence;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>
/// The shell view-model over a throw-away SQLite database: real services, a runtime that is never called, and recording fakes for the file
/// dialogs and the assistant panel. The clock is fixed on Friday 9 October 2026.
/// </summary>
internal sealed class ShellScenario : IDisposable
{
    private readonly string _directory;

    private ShellScenario(string directory, SqliteProjectRepository repository, ProjectService projects, AgentService agents)
    {
        _directory = directory; Repository = repository; Projects = projects;
        SettingsPath = Path.Combine(directory, "settings.json");
        Locale = new LocaleContext(SettingsPath);
        Appearance = new AppearanceContext(SettingsPath);
        Strings = new LocalizedStrings(Locale, new LocalizationService(Locale));
        Workspace = new WorkspaceSession(projects, agents, new DesktopEnvironment(AgentConfigured: false), Calendar);
        Shell = new MainWindowViewModel(Workspace, Locale, Appearance, Strings, new UpdateController(Updater), Files, Picker, () => repository.InitializeAsync(), Clock)
        {
            Assistant = Assistant,
            DelegationFactory = (_, _) => new StubTab()
        };
    }

    public SqliteProjectRepository Repository { get; }
    public ProjectService Projects { get; }
    public string SettingsPath { get; }
    public LocaleContext Locale { get; }
    public AppearanceContext Appearance { get; }
    public LocalizedStrings Strings { get; }
    public WorkspaceSession Workspace { get; }
    public TimeProvider Clock { get; } = new PageScenario.FixedClock(PageScenario.Noon);
    public PageScenario.FakeFiles Files { get; } = new();
    public PageScenario.FakePicker Picker { get; } = new();
    public PageScenario.FakeCalendar Calendar { get; } = new();
    public PageScenario.FakeUpdater Updater { get; } = new();
    public FakeAssistant Assistant { get; } = new();
    public MainWindowViewModel Shell { get; }
    public UiState State => Shell.State;

    public static async Task<ShellScenario> CreateAsync(bool initialize = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), "project-operations-shell-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "shell.db");
        var repository = new SqliteProjectRepository(database);
        if (initialize) await repository.InitializeAsync();
        var projects = new ProjectService(repository);
        var agents = new AgentService(projects, new RefusingRuntime(), new SqliteAgentJobRepository(database));
        return new ShellScenario(directory, repository, projects, agents);
    }

    public Task<Project> AddProjectAsync(string name = "Deal") => Projects.CreateAsync(name, "Company", ProjectStatus.Active, "", "");

    public Task<Project> ReloadAsync(Project project) => Projects.GetAsync(project.Id)!;

    public void Dispose()
    {
        Shell.Dispose();
        Strings.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    internal sealed class StubTab : ViewModelBase;

    internal sealed class RefusingRuntime : IAgentRuntime
    {
        public Task<AgentResult> RunAsync(AgentRequest request, IProgress<AgentEvent> progress, CancellationToken cancellationToken) => throw new InvalidOperationException("Not used.");
        public Task CancelAsync(string jobId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal sealed class FakeAssistant : IAssistantPanel
    {
        public int Pickers { get; private set; }
        public List<Guid> Projects { get; } = [];
        public List<string> Reviewed { get; } = [];
        public Task ShowPickerAsync() { Pickers++; return Task.CompletedTask; }
        public Task ShowProjectAsync(Project project) { Projects.Add(project.Id); return Task.CompletedTask; }
        public Task ReviewAsync(string jobId) { Reviewed.Add(jobId); return Task.CompletedTask; }
    }
}
