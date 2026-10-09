using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>
/// What the open project's screen view-models run against: a <see cref="PageScenario"/> (throw-away database, English strings, clock fixed on
/// Friday 9 October 2026) plus recording fakes for the project host and the file picker, and a fresh <see cref="UiState"/>.
/// </summary>
internal sealed class ProjectScenario : IDisposable
{
    private ProjectScenario(PageScenario pages)
    {
        Pages = pages;
        Services = new ProjectScreenServices(pages.Projects, pages.Strings, pages.Dialogs, Host, pages.Clock, pages.Navigator, Picker, State);
    }

    public PageScenario Pages { get; }
    public FakeProjectHost Host { get; } = new();
    public PageScenario.FakePicker Picker { get; } = new();
    public UiState State { get; } = new();
    public ProjectScreenServices Services { get; }

    public LocalizedStrings L => Pages.Strings;
    public LocaleContext Locale => Pages.Locale;
    public PageScenario.FakeDialogs Dialogs => Pages.Dialogs;

    public static async Task<ProjectScenario> CreateAsync() => new(await PageScenario.CreateAsync());

    public Task<Project> AddProjectAsync(string name = "Deal", string company = "Company", ProjectStatus status = ProjectStatus.Active, string owner = "") =>
        Pages.AddProjectAsync(name, company, status, owner);

    public Task<Project> AddBlankProjectAsync(string name = "Blank deal") => Pages.Projects.CreateBlankAsync(name, "", ProjectStatus.Active, "", "");

    /// <summary>The project as stored, to check what a view-model saved.</summary>
    public Task<Project> ReloadAsync(Project project) => Pages.ReloadAsync(project);

    public void Dispose() => Pages.Dispose();

    internal sealed class FakeProjectHost : IProjectHost
    {
        public int Runs { get; private set; }
        public int Refreshes { get; private set; }
        public List<string> Errors { get; } = [];
        public List<string> Toasts { get; } = [];
        public int AssistantToggles { get; private set; }
        public int AssistantOpens { get; private set; }
        public List<ProjectTab> Tabs { get; } = [];
        public Task RefreshProjectAsync() { Refreshes++; return Task.CompletedTask; }
        public Task ReviewInAssistantAsync(string jobId) => Task.CompletedTask;
        public Task RunAsync(Func<Task> action) { Runs++; return action(); }
        public void ShowError(string key) => Errors.Add(key);
        public void ShowToast(string key) => Toasts.Add(key);
        public void ToggleAssistant() => AssistantToggles++;
        public void OpenAssistant() => AssistantOpens++;
        public void SelectTab(ProjectTab tab) => Tabs.Add(tab);
    }
}
