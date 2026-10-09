using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.Calendar;
using ProjectOperations.Desktop.Features.Dashboard;
using ProjectOperations.Desktop.Features.Documents;
using ProjectOperations.Desktop.Features.Onboarding;
using ProjectOperations.Desktop.Features.ProjectDetail;
using ProjectOperations.Desktop.Features.Projects;
using ProjectOperations.Desktop.Features.Settings;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Updates;

namespace ProjectOperations.Desktop.Shell;

/// <summary>The assistant panel as the shell drives it. Code-built until the assistant moves to MVVM.</summary>
internal interface IAssistantPanel
{
    /// <summary>Lists the projects the assistant can work on (shown when no project is open).</summary>
    Task ShowPickerAsync();
    Task ShowProjectAsync(Project project);
    /// <summary>Opens the review tray of a finished job.</summary>
    Task ReviewAsync(string jobId);
}

/// <summary>
/// The application shell: the sidebar, the page on screen, the first-run overlay, the modal layer, the error banner and toast, the
/// run lock and the assistant's open state. It routes between pages, keeps the sidebar honest as data arrives, switches between the
/// user's workspace and the sample, and is what every screen's view-model asks for navigation, busy state and messages.
/// </summary>
internal sealed partial class MainWindowViewModel : ViewModelBase, IProjectHost, IPageHost, IOnboardingHost, ISidebarHost, IDisposable
{
    private readonly WorkspaceSession _workspace;
    private readonly LocaleContext _locale;
    private readonly AppearanceContext _appearance;
    private readonly UpdateController _updates;
    private readonly IFileLauncher _files;
    private readonly IFilePicker _picker;
    private readonly Func<Task> _initialize;
    private readonly AgentRunLock _runLock;
    private readonly Navigator _navigator;
    private readonly TimeProvider _clock;
    /// <summary>The shown page's view-model when it holds something to release (a pending sign-in, an event subscription); disposed when the page is replaced.</summary>
    private IDisposable? _pageModel;
    private ProjectDetailViewModel? _projectModel;

    [ObservableProperty] private object? _page;
    [ObservableProperty] private object? _overlay;
    [ObservableProperty] private object? _sampleBanner;
    [ObservableProperty] private bool _isSidebarEnabled = true;
    [ObservableProperty] private bool _isPageEnabled = true;
    [ObservableProperty] private bool _isAssistantOpen;
    [ObservableProperty] private string _themeId;

    public MainWindowViewModel(WorkspaceSession workspace, LocaleContext locale, AppearanceContext appearance, LocalizedStrings strings, UpdateController updates,
        IFileLauncher files, IFilePicker picker, Func<Task> initialize, TimeProvider? clock = null)
    {
        _workspace = workspace; _locale = locale; _appearance = appearance; L = strings; _updates = updates; _files = files; _picker = picker; _initialize = initialize;
        _clock = clock ?? TimeProvider.System;
        _themeId = appearance.Theme;
        Messages = new ShellMessages();
        _runLock = new AgentRunLock(Messages);
        _navigator = new Navigator(ShowRouteAsync);
        Display = new DisplayOptionsViewModel(strings, locale, appearance, Messages.ShowError);
        Sidebar = new SidebarViewModel(strings, Display, this);
        _runLock.LockChanged += (_, _) => ApplyLock(_runLock.IsLocked);
        appearance.Changed += OnAppearanceChanged;
        Messages.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShellMessages.ErrorKey)) { OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(ErrorText)); }
            else if (e.PropertyName is nameof(ShellMessages.ToastKey)) { OnPropertyChanged(nameof(HasToast)); OnPropertyChanged(nameof(ToastText)); }
        };
        Messages.ToastShown += (_, _) => { OnPropertyChanged(nameof(HasToast)); OnPropertyChanged(nameof(ToastText)); };
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public ShellMessages Messages { get; }
    public DialogService Dialogs { get; } = new();
    public DisplayOptionsViewModel Display { get; }
    public SidebarViewModel Sidebar { get; }
    /// <summary>View state that survives a screen rebuild (the shell reloads screens after every save): open checklist rows, dismissed hints.</summary>
    public UiState State { get; } = new();
    public INavigator Navigator => _navigator;

    /// <summary>The assistant panel, set once it exists (it needs the shell to exist first).</summary>
    public IAssistantPanel? Assistant { get; set; }
    /// <summary>Builds the code-built Delegate tab's view-model for a project; set with <see cref="Assistant"/> until the assistant moves to MVVM.</summary>
    public Func<Project, IReadOnlyList<AgentJob>, ViewModelBase>? DelegationFactory { get; set; }

    public bool HasError => Messages.ErrorKey is not null;
    public string ErrorText => Messages.ErrorKey is { } key ? L[key] : "";
    public bool HasToast => Messages.ToastKey is not null;
    public string ToastText => Messages.ToastKey is { } key ? L[key] : "";
    public bool HasOverlay => Overlay is not null;
    public bool HasSampleBanner => SampleBanner is not null;

    /// <summary>The most recent exception reported to the user as a generic failure; retained for diagnostics and tests.</summary>
    public Exception? LastFailure => Messages.LastFailure;

    public bool IsSample => _workspace.IsSample;
    public bool IsAgentRunning => _runLock.IsRunning;
    public ProjectService Projects => _workspace.Projects;
    public AgentService Agents => _workspace.Agents;
    public DesktopEnvironment Environment => _workspace.Environment;

    partial void OnOverlayChanged(object? value) => OnPropertyChanged(nameof(HasOverlay));
    partial void OnSampleBannerChanged(object? value) => OnPropertyChanged(nameof(HasSampleBanner));

    private void OnAppearanceChanged(object? sender, EventArgs e) => ThemeId = _appearance.Theme;

    // ---- the services the screens take; they follow the sample workspace -----------------------------------------------------------

    private PageServices PageServices => new(Projects, L, Dialogs, _navigator, this, _files, _workspace.Calendar, _clock);
    private SettingsServices SettingsServices => new(PageServices, _locale, _appearance, Environment, _updates);
    /// <summary>The services handed to view-models of the open project's screens.</summary>
    public ProjectScreenServices ProjectScreen => new(Projects, L, Dialogs, this, _clock, _navigator, _picker, State);

    // ---- start, locking and closing ---------------------------------------------------------------------------------------------

    /// <summary>Opens the database, then the first screen: the attention home when it has something to show, otherwise the project list. A welcome screen sits on top of an empty workspace.</summary>
    public async Task StartAsync()
    {
        IsSidebarEnabled = false;
        try
        {
            await Messages.GuardAsync(async () =>
            {
                await _initialize();
                var count = (await Projects.ListAsync()).Count;
                await RefreshShellAsync();
                await NavigateAsync(Sidebar.IsVisible(AppPage.Dashboard) ? AppPage.Dashboard : AppPage.Projects);
                if (count == 0) ShowWelcome();
            });
        }
        finally { IsSidebarEnabled = true; }
    }

    private void ApplyLock(bool locked)
    {
        IsSidebarEnabled = !locked; IsPageEnabled = !locked;
        if (_projectModel is not null) _projectModel.IsLocked = locked;
    }

    /// <summary>Locks navigation, project tabs and close for the complete runtime lifetime of one agent job, then for the reload that follows it.</summary>
    public Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh) => _runLock.RunAsync(run, refresh);
    public void CancelRun() => _runLock.Cancel();
    /// <summary>Cancels a running job for a closing window and returns the job to wait for, or null when nothing runs.</summary>
    public Task? RequestClose() => _runLock.RequestClose();

    /// <summary>Runs an action with navigation and the page locked, and reports a failure in the banner instead of throwing.</summary>
    public async Task RunAsync(Func<Task> action)
    {
        IsSidebarEnabled = false; IsPageEnabled = false;
        try { await Messages.GuardAsync(action); }
        finally { IsSidebarEnabled = !IsAgentRunning; IsPageEnabled = !IsAgentRunning; }
    }

    public void ShowError(string key) => Messages.ShowError(key);
    public void ShowToast(string key) => Messages.ShowToast(key);
    [RelayCommand]
    private void DismissError() => Messages.HideError();

    // ---- navigation ---------------------------------------------------------------------------------------------------------------

    /// <summary>Shows a section; a failure lands in the error banner.</summary>
    Task ISidebarHost.NavigateAsync(AppPage page) => Messages.GuardAsync(() => NavigateAsync(page));
    public Task NavigateAsync(AppPage page, bool newProject = false) => _navigator.GoToAsync(new PageRoute(page, newProject));
    public Task OpenProjectAsync(Guid id, ProjectTab tab = ProjectTab.Overview) => _navigator.GoToAsync(new ProjectRoute(id, tab));
    public Task RefreshProjectAsync() => _navigator.Current is ProjectRoute route ? OpenProjectAsync(route.ProjectId, route.Tab) : Task.CompletedTask;
    void IProjectHost.SelectTab(ProjectTab tab) => _navigator.SelectTab(tab);

    private async Task<bool> ShowRouteAsync(Route route)
    {
        switch (route)
        {
            case PageRoute page: await ShowPageAsync(page); return true;
            case ProjectRoute project: return await ShowProjectAsync(project);
            default: return false;
        }
    }

    /// <summary>Replaces the page. The page it replaces releases what it holds; <paramref name="model"/> is released in turn when this one is replaced.</summary>
    private void Show(object page, IDisposable? model = null)
    {
        _pageModel?.Dispose(); _pageModel = model;
        Page = page;
    }

    private async Task ShowPageAsync(PageRoute route)
    {
        var page = route.Page;
        _projectModel = null; Sidebar.Select(page);
        Sidebar.MarkSeen(page);
        var services = PageServices;
        CalendarViewModel? calendar = null;
        SettingsViewModel? settings = null;
        object model = page switch
        {
            AppPage.Projects => await ProjectsViewModel.LoadAsync(services),
            AppPage.Calendar => calendar = await CalendarViewModel.LoadAsync(services),
            AppPage.Documents => await DocumentsViewModel.LoadAsync(services),
            AppPage.Settings => settings = await SettingsViewModel.LoadAsync(SettingsServices),
            _ => await DashboardViewModel.LoadAsync(services)
        };
        Show(model, settings);
        // The opt-in external overlay loads after the page is shown and does nothing unless Google Calendar is connected.
        if (calendar is not null) _ = calendar.StartAsync();
        if (Assistant is not null) await Assistant.ShowPickerAsync();
        await RefreshShellAsync();
        if (route.NewProject) ShowWizard(fromWelcome: false);
    }

    private async Task<bool> ShowProjectAsync(ProjectRoute route)
    {
        var project = await Projects.GetAsync(route.ProjectId);
        if (project is null) { ShowError("validation.projectUnavailable"); return false; }
        Sidebar.Select(AppPage.Projects);
        var model = await ProjectDetailViewModel.LoadAsync(project, route.Tab, ProjectScreen, Agents,
            jobs => DelegationFactory?.Invoke(project, jobs) ?? throw new InvalidOperationException("The shell has no Delegate tab factory."));
        model.IsLocked = _runLock.IsLocked;
        _projectModel = model; Show(model);
        if (Assistant is not null) await Assistant.ShowProjectAsync(project);
        if (route.Tab == ProjectTab.Delegate) SetAssistantOpen(true);
        await RefreshShellAsync();
        return true;
    }

    // ---- the sidebar as data arrives ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Refreshes the attention count and which sections the sidebar offers. Calendar and Needs attention appear once something is dated,
    /// Documents once a file is linked; a section that appears later is flagged New and announced once.
    /// </summary>
    public async Task RefreshShellAsync()
    {
        var projects = await Projects.ListAsync();
        Sidebar.AttentionCount = ProjectSummaries.Dashboard(projects, _clock.GetUtcNow()).OverdueTasks.Count;
        var googleConnected = !IsSample && await _workspace.Calendar.IsConnectedAsync();
        var revealed = Sidebar.Apply(SidebarPolicy.Sections(projects, IsSample, googleConnected), IsSample);
        if (revealed.Contains(AppPage.Dashboard) || (revealed.Contains(AppPage.Calendar) && SidebarPolicy.HasDatedWork(projects))) ShowToast("v3.toastDated");
        else if (revealed.Contains(AppPage.Documents)) ShowToast("v3.toastDocs");
        UpdateGettingStarted(projects);
    }

    private void UpdateGettingStarted(IReadOnlyList<Project> projects)
    {
        if (IsSample || State.GettingStartedHidden || projects.Count == 0) { Sidebar.ShowGettingStarted(null, () => Task.CompletedTask); return; }
        var done = SidebarPolicy.GettingStartedDone(projects, Environment.AgentConfigured);
        if (done.All(item => item)) { Sidebar.ShowGettingStarted(null, () => Task.CompletedTask); return; }
        var target = (_navigator.Current is ProjectRoute { ProjectId: var open } ? projects.FirstOrDefault(project => project.Id == open) : null) ?? projects[0];
        var next = target.Requirements.FirstOrDefault(requirement => requirement.Status == RequirementStatus.Missing);
        var document = target.Requirements.FirstOrDefault(requirement => requirement.Type == RequirementType.Document && requirement.Files.Count == 0) ?? target.Requirements.FirstOrDefault();
        Task OpenItem(ProjectRequirement? requirement, bool followUp = false)
        {
            if (requirement is not null)
            {
                State.OpenRequirement = requirement.Id; State.ExpandedGroups.Add($"{target.Id}:{requirement.GroupId}"); State.InitializedProjects.Add(target.Id);
                if (followUp) State.PendingFollowUp = requirement.Id;
            }
            return OpenProjectAsync(target.Id, ProjectTab.Checklist);
        }
        GettingStartedStep[] steps =
        [
            new("v3.gs0", done[0], false, null),
            new("v3.gs1", done[1], false, () => RunAsync(() => OpenItem(next))),
            new("v3.gs2", done[2], false, () => RunAsync(() => OpenItem(document))),
            new("v3.gs3", done[3], false, () => RunAsync(() => OpenItem(next ?? target.Requirements.FirstOrDefault(), followUp: true))),
            new("v3.gs4", done[4], true, () => RunAsync(() => NavigateAsync(AppPage.Settings)))
        ];
        Sidebar.ShowGettingStarted(steps, () => { State.GettingStartedHidden = true; Sidebar.ShowGettingStarted(null, () => Task.CompletedTask); return Task.CompletedTask; });
    }

    // ---- first run and the sample workspace ---------------------------------------------------------------------------------------

    public void ShowWelcome() => Overlay = new WelcomeViewModel(this, L, Display);
    public void ShowWizard(bool fromWelcome) => Overlay = new WizardViewModel(this, L, Display, _picker, fromWelcome);
    void IPageHost.ShowWizard() => ShowWizard(fromWelcome: false);
    public void CloseOverlay() => Overlay = null;

    public async Task OpenCreatedProjectAsync(Guid id)
    {
        State.TipHidden = false; State.GettingStartedHidden = false;
        await OpenProjectAsync(id, ProjectTab.Checklist);
        ShowToast("v3.toastCreated");
    }

    public async Task StartSampleAsync()
    {
        if (!await _workspace.StartSampleAsync(_locale.LanguageCode == "fa")) { CloseOverlay(); return; }
        ResetShellFacts(); CloseOverlay();
        SampleBanner = new SampleBannerViewModel(this, L);
        OnPropertyChanged(nameof(IsSample));
        await RefreshShellAsync();
        await NavigateAsync(AppPage.Dashboard);
    }

    /// <summary>Discards the sample workspace and returns to the user's own data; the caller decides what to show next.</summary>
    public Task ExitSampleAsync()
    {
        if (_workspace.ExitSample()) { ResetShellFacts(); SampleBanner = null; OnPropertyChanged(nameof(IsSample)); }
        return Task.CompletedTask;
    }

    private void ResetShellFacts()
    {
        Sidebar.Reset(); State.Reset();
    }

    // ---- the assistant ------------------------------------------------------------------------------------------------------------

    public void SetAssistantOpen(bool open)
    {
        if (!open && IsAgentRunning) return;
        IsAssistantOpen = open;
    }

    public void ToggleAssistant() => SetAssistantOpen(!IsAssistantOpen);
    void IProjectHost.OpenAssistant() => SetAssistantOpen(true);

    public async Task ReviewInAssistantAsync(string jobId)
    {
        SetAssistantOpen(true);
        if (Assistant is not null) await Assistant.ReviewAsync(jobId);
    }

    public void Dispose()
    {
        _appearance.Changed -= OnAppearanceChanged;
        _pageModel?.Dispose(); _pageModel = null;
        Display.Dispose();
        _workspace.Dispose();
    }
}

/// <summary>The "you're looking at sample data" strip with the way out to a real project.</summary>
internal sealed partial class SampleBannerViewModel : ViewModelBase
{
    private readonly IOnboardingHost _host;

    public SampleBannerViewModel(IOnboardingHost host, LocalizedStrings strings)
    {
        _host = host; L = strings;
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }

    [RelayCommand]
    private Task StartMineAsync() => _host.RunAsync(() => { _host.ShowWizard(fromWelcome: false); return Task.CompletedTask; });
}
