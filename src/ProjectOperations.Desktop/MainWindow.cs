using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Desktop.Updates;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

public sealed class MainWindow : Window, IShell, IProjectHost
{
    /// <summary>Below this window width the assistant panel slides over the content instead of docking beside it.</summary>
    private const double DockedPanelMinWidth = 1280;
    private static readonly (AppPage Page, string Key, string Name, string Icon)[] Pages =
    [
        (AppPage.Dashboard, "navigation.attention", "NavigationDashboard", Icons.BellSimple),
        (AppPage.Projects, "navigation.projects", "NavigationProjects", Icons.Folders),
        (AppPage.Calendar, "presentation.navigationCalendar", "NavigationCalendar", Icons.CalendarBlank),
        (AppPage.Documents, "presentation.navigationDocuments", "NavigationDocuments", Icons.Files),
        (AppPage.Settings, "presentation.navigationSettings", "NavigationSettings", Icons.GearSix)
    ];

    private ProjectService _projects => _workspace.Projects;
    private readonly ContentControl _page = new() { Name = "ScreenHost" };
    private readonly ErrorBanner _errorBanner;
    private readonly AppSidebar _navigation;
    private readonly AssistantPanel _assistant;
    private readonly Grid _shell;
    private readonly DialogHost _dialogs = new();
    private TabControl? _projectTabs;
    private bool _closing;
    private readonly LocaleContext _locale;
    private readonly AppearanceContext _appearance;
    private readonly LocalizationService _text;
    private readonly LocalizedControls _localized;
    private readonly LocalizedStrings _strings;
    private readonly PresentationContext _context;
    private readonly ShellMessages _messages = new();
    private readonly AgentRunLock _runLock;
    private readonly Navigator _navigator;
    private readonly WorkspaceSession _workspace;
    private readonly Panel _overlay = new() { IsVisible = false, ZIndex = 30, Name = "OnboardingOverlay" };
    private readonly Dictionary<AppPage, bool> _visible = [];
    private readonly HashSet<AppPage> _fresh = [];
    private bool _shellKnown;

    public MainWindow(ProjectService projects, AgentService agents, Func<Task> initialize, string configuration, LocaleContext? locale = null, Func<string>? configurationText = null,
        AppearanceContext? appearance = null, DesktopEnvironment? environment = null, IAppUpdater? updater = null, IExternalCalendarSource? calendar = null)
    {
        _locale = locale ?? new LocaleContext();
        _appearance = appearance ?? new AppearanceContext();
        _text = new LocalizationService(_locale);
        _localized = new LocalizedControls(_locale);
        _strings = new LocalizedStrings(_locale, _text);
        _runLock = new AgentRunLock(_messages);
        _navigator = new Navigator(ShowRouteAsync);
        _workspace = new WorkspaceSession(projects, agents, environment ?? new DesktopEnvironment(), calendar ?? new NoExternalCalendar());
        _context = new PresentationContext(_workspace, _locale, _appearance, _text, _localized, _strings, configuration, configurationText, this, new UpdateController(updater ?? new NoAppUpdater()), _dialogs, this);
        PresentationTheme.Apply(this);
        this.Paint(BackgroundProperty, "BackgroundApp");
        _locale.Changed += LocaleChanged;
        _appearance.Changed += AppearanceChanged;
        _localized.Bind(this, window => window.Title = _text.Get("app.title"));
        ApplyLocalePresentation();
        RequestedThemeVariant = PresentationTheme.Variant(_appearance.Theme);
        Width = 1280; Height = 860; MinWidth = 800; MinHeight = 600;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ProjectOperations.Desktop/Assets/Icons/app.ico")));

        _navigation = new AppSidebar(_context, Pages, page => _ = GuardAsync(() => NavigateAsync(page)));
        _assistant = new AssistantPanel(_context) { Name = "AssistantPanel", IsVisible = false, Width = 360 };

        _errorBanner = new ErrorBanner(_messages, _text, _localized);

        var body = new DockPanel { Name = "WorkspaceBody" };
        DockPanel.SetDock(_errorBanner, Dock.Top); body.Children.Add(_errorBanner);
        body.Children.Add(_page);

        _shell = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        _shell.Children.Add(_navigation);
        Grid.SetColumn(body, 1); _shell.Children.Add(body);
        _shell.Children.Add(_assistant);
        Grid.SetColumnSpan(_dialogs, 3); _shell.Children.Add(_dialogs);
        Grid.SetColumnSpan(_overlay, 3); _shell.Children.Add(_overlay);
        var toast = new ToastView(_messages, _text);
        Grid.SetColumnSpan(toast, 3); _shell.Children.Add(toast);
        Content = _shell;
        PlaceAssistant();
        SizeChanged += (_, _) => PlaceAssistant();
        KeyDown += (_, e) => { if (e.Key == Key.Escape && _dialogs.IsOpen) { CloseModal(); e.Handled = true; } };

        Closed += (_, _) => { _locale.Changed -= LocaleChanged; _appearance.Changed -= AppearanceChanged; _localized.Dispose(); _strings.Dispose(); _workspace.Dispose(); };
        Opened += async (_, _) =>
        {
            SetNavigationEnabled(false);
            try { await GuardAsync(async () => { await initialize(); await StartAsync(); }); }
            finally { SetNavigationEnabled(true); }
        };
        _runLock.LockChanged += (_, _) => ApplyLock(_runLock.IsLocked);
        Closing += async (_, e) =>
        {
            if (_closing || _runLock.RequestClose() is not { } running) return;
            e.Cancel = true;
            await GuardAsync(async () => { await running; _closing = true; Close(); });
        };
    }

    public bool IsAgentRunning => _runLock.IsRunning;

    private void SetNavigationEnabled(bool enabled) => _navigation.IsEnabled = enabled;
    private Task GuardAsync(Func<Task> action, bool clearError = true) => _messages.GuardAsync(action, clearError);
    /// <summary>The most recent exception reported to the user as a generic failure; retained for diagnostics and tests.</summary>
    public Exception? LastFailure => _messages.LastFailure;
    private void ApplyLock(bool locked)
    {
        SetNavigationEnabled(!locked); _page.IsEnabled = !locked;
        if (_projectTabs is not null)
            foreach (var item in _projectTabs.Items.OfType<TabItem>()) item.IsEnabled = !locked;
    }
    private void LocaleChanged(object? sender, EventArgs e) => ApplyLocalePresentation();
    private void AppearanceChanged(object? sender, EventArgs e)
    {
        RequestedThemeVariant = PresentationTheme.Variant(_appearance.Theme); _navigation.ThemeSelector.Refresh(_appearance.Theme);
    }
    private void ApplyLocalePresentation()
    {
        FlowDirection = _locale.FlowDirection;
        FontFamily = _locale.LanguageCode == "fa" ? PresentationTheme.PersianFontFamily : PresentationTheme.LatinFontFamily;
    }
    public void ShowError(string key) => _messages.ShowError(key);
    private void HideError() => _messages.HideError();

    public async Task RunAsync(Func<Task> action)
    {
        SetNavigationEnabled(false); _page.IsEnabled = false;
        try { await GuardAsync(action); }
        finally { SetNavigationEnabled(!IsAgentRunning); _page.IsEnabled = !IsAgentRunning; }
    }

    public async Task ActAsync(Button button, Func<Task> action)
    {
        button.IsEnabled = false;
        try { await RunAsync(action); }
        finally { button.IsEnabled = true; }
    }

    /// <summary>Hosts a screen in a scrollable, readable-width column that follows the inherited flow direction.</summary>
    private void Show(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        var content = new StackPanel { Spacing = 20 };
        if (IsSample) content.Children.Add(SampleBanner());
        content.Children.Add(control);
        _page.Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new ReadableColumn { Child = new Border { Padding = new Thickness(32, 28, 32, 40), Child = content } }
        };
    }

    public Task NavigateAsync(AppPage page, bool newProject = false) => _navigator.GoToAsync(new PageRoute(page, newProject));
    public Task OpenProjectAsync(Guid id, ProjectTab tab = ProjectTab.Overview) => _navigator.GoToAsync(new ProjectRoute(id, tab));

    private async Task<bool> ShowRouteAsync(Route route)
    {
        switch (route)
        {
            case PageRoute page: await ShowPageAsync(page); return true;
            case ProjectRoute project: return await ShowProjectAsync(project);
            default: return false;
        }
    }

    private async Task ShowPageAsync(PageRoute route)
    {
        var page = route.Page;
        _projectTabs = null; _navigation.Select(page);
        _fresh.Remove(page); _navigation.SetNew(page, false);
        Control view = page switch
        {
            AppPage.Projects => await Load(new ProjectsView(_context), view => view.LoadAsync()),
            AppPage.Calendar => await Load(new CalendarView(_context), view => view.LoadAsync()),
            AppPage.Documents => await Load(new DocumentsView(_context), view => view.LoadAsync()),
            AppPage.Settings => new SettingsView(_context),
            _ => await Load(new DashboardView(_context), view => view.LoadAsync())
        };
        Show(view);
        await _assistant.ShowPickerAsync();
        await RefreshAttentionAsync();
        if (route.NewProject) ShowWizard(fromWelcome: false);
    }

    private static async Task<T> Load<T>(T view, Func<T, Task> load) where T : Control { await load(view); return view; }

    private async Task<bool> ShowProjectAsync(ProjectRoute route)
    {
        var project = await _projects.GetAsync(route.ProjectId);
        if (project is null) { ShowError("validation.projectUnavailable"); return false; }
        _navigation.Select(AppPage.Projects);
        var view = new ProjectDetailView(_context, project); await view.LoadAsync(route.Tab);
        view.Tabs.SelectionChanged += (_, _) => { if (view.Tabs.SelectedIndex >= 0) _navigator.SelectTab((ProjectTab)view.Tabs.SelectedIndex); };
        _projectTabs = view.Tabs; Show(view);
        await _assistant.ShowProjectAsync(project);
        if (route.Tab == ProjectTab.Delegate) SetAssistantOpen(true);
        await RefreshAttentionAsync();
        return true;
    }

    public Task RefreshProjectAsync() => _navigator.Current is ProjectRoute route ? OpenProjectAsync(route.ProjectId, route.Tab) : Task.CompletedTask;

    public bool IsSample => _workspace.IsSample;
    public Task RefreshShellAsync() => UpdateShellAsync();
    private Task RefreshAttentionAsync() => UpdateShellAsync();

    /// <summary>First screen: the attention home when it has something to show, otherwise the project list; the welcome screen sits on top of an empty workspace.</summary>
    private async Task StartAsync()
    {
        var count = (await _projects.ListAsync()).Count;
        await UpdateShellAsync();
        await NavigateAsync(_navigation.IsPageVisible(AppPage.Dashboard) ? AppPage.Dashboard : AppPage.Projects);
        if (count == 0) ShowWelcome();
    }

    /// <summary>
    /// Refreshes the attention badge and which sections the sidebar offers. Calendar and Needs attention appear once something is dated,
    /// Documents once a file is linked; a section that appears later is flagged New and announced once.
    /// </summary>
    private async Task UpdateShellAsync()
    {
        var projects = await _projects.ListAsync();
        _navigation.SetAttentionCount(ProjectSummaries.Dashboard(projects, DateTimeOffset.Now).OverdueTasks.Count);
        var hasDated = projects.Any(project => project.Tasks.Any(task => task.DueAt is not null) || project.Milestones.Any(milestone => milestone.DueAt is not null));
        var hasFile = projects.Any(project => project.Requirements.Any(requirement => requirement.Files.Count > 0));
        var googleConnected = !IsSample && await _context.Calendar.IsConnectedAsync();
        var shown = new Dictionary<AppPage, bool> { [AppPage.Dashboard] = IsSample || hasDated, [AppPage.Projects] = true, [AppPage.Calendar] = IsSample || hasDated || googleConnected, [AppPage.Documents] = IsSample || hasFile, [AppPage.Settings] = true };
        var revealed = new List<AppPage>();
        foreach (var (page, show) in shown)
        {
            if (_shellKnown && !IsSample && show && !_visible.GetValueOrDefault(page)) { _fresh.Add(page); revealed.Add(page); }
            _visible[page] = show; _navigation.SetVisible(page, show); _navigation.SetNew(page, _fresh.Contains(page));
        }
        _shellKnown = true;
        if (revealed.Contains(AppPage.Dashboard) || (revealed.Contains(AppPage.Calendar) && hasDated)) ShowToast("v3.toastDated");
        else if (revealed.Contains(AppPage.Documents)) ShowToast("v3.toastDocs");
        UpdateGettingStarted(projects);
    }

    private void ResetShellFacts()
    {
        _visible.Clear(); _fresh.Clear(); _shellKnown = false; _context.State.Reset();
    }

    private void UpdateGettingStarted(IReadOnlyList<Project> projects)
    {
        var state = _context.State;
        if (IsSample || state.GettingStartedHidden || projects.Count == 0) { _navigation.SetGettingStarted(null, () => Task.CompletedTask); return; }
        var all = projects.SelectMany(project => project.Requirements).ToList();
        var done = new[]
        {
            true,
            all.Any(requirement => requirement.Status != RequirementStatus.Missing || requirement.Value.Trim().Length > 0),
            all.Any(requirement => requirement.Files.Count > 0),
            projects.Any(project => project.Tasks.Any(task => task.DueAt is not null)),
            _context.Environment.AgentConfigured
        };
        if (done.All(item => item)) { _navigation.SetGettingStarted(null, () => Task.CompletedTask); return; }
        var target = (_navigator.Current is ProjectRoute { ProjectId: var open } ? projects.FirstOrDefault(project => project.Id == open) : null) ?? projects[0];
        var next = target.Requirements.FirstOrDefault(requirement => requirement.Status == RequirementStatus.Missing);
        var document = target.Requirements.FirstOrDefault(requirement => requirement.Type == RequirementType.Document && requirement.Files.Count == 0) ?? target.Requirements.FirstOrDefault();
        Task OpenItem(ProjectRequirement? requirement, bool followUp = false)
        {
            if (requirement is not null)
            {
                state.OpenRequirement = requirement.Id; state.ExpandedGroups.Add($"{target.Id}:{requirement.GroupId}"); state.InitializedProjects.Add(target.Id);
                if (followUp) state.PendingFollowUp = requirement.Id;
            }
            return OpenProjectAsync(target.Id, ProjectTab.Checklist);
        }
        var items = new List<AppSidebar.GettingStartedItem>
        {
            new("v3.gs0", done[0], false, null),
            new("v3.gs1", done[1], false, () => OpenItem(next)),
            new("v3.gs2", done[2], false, () => OpenItem(document)),
            new("v3.gs3", done[3], false, () => OpenItem(next ?? target.Requirements.FirstOrDefault(), followUp: true)),
            new("v3.gs4", done[4], true, () => NavigateAsync(AppPage.Settings))
        };
        _navigation.SetGettingStarted(items, () => { state.GettingStartedHidden = true; _navigation.SetGettingStarted(null, () => Task.CompletedTask); return Task.CompletedTask; });
    }

    public void ShowToast(string key) => _messages.ShowToast(key);

    public void ShowWelcome() => ShowOverlay(new WelcomeScreen(_context));
    public void ShowWizard(bool fromWelcome) => ShowOverlay(new WizardScreen(_context, fromWelcome));
    public void CloseOverlay() { _overlay.IsVisible = false; _overlay.Children.Clear(); }
    private void ShowOverlay(Control screen) { _overlay.Children.Clear(); _overlay.Children.Add(screen); _overlay.IsVisible = true; }

    public async Task OpenCreatedProjectAsync(Guid id)
    {
        _context.State.TipHidden = false; _context.State.GettingStartedHidden = false;
        await OpenProjectAsync(id, ProjectTab.Checklist);
        ShowToast("v3.toastCreated");
    }

    public async Task StartSampleAsync()
    {
        if (!await _workspace.StartSampleAsync(_locale.LanguageCode == "fa")) { CloseOverlay(); return; }
        ResetShellFacts(); CloseOverlay();
        await UpdateShellAsync();
        await NavigateAsync(AppPage.Dashboard);
    }

    /// <summary>Discards the sample workspace and returns to the user's own data; the caller decides what to show next.</summary>
    public Task ExitSampleAsync()
    {
        if (_workspace.ExitSample()) ResetShellFacts();
        return Task.CompletedTask;
    }

    /// <summary>"You're looking at sample data" strip with the way out to a real project.</summary>
    private Control SampleBanner()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, Name = "SampleBanner" };
        row.Children.Add(Icons.Glyph(Icons.Flask, 17, "TextSecondary"));
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }; PresentationTheme.Typeset(text, "Small");
        _localized.Bind(text, control => control.Inlines =
        [
            new Avalonia.Controls.Documents.Run(_text.Get("v3.sampleBanner")) { FontWeight = FontWeight.SemiBold },
            new Avalonia.Controls.Documents.Run(" " + _text.Get("v3.sampleNote")).Paint(Avalonia.Controls.Documents.TextElement.ForegroundProperty, "TextSecondary")
        ]);
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var mine = _context.Action("v3.startMine", () => { ShowWizard(fromWelcome: false); return Task.CompletedTask; }, "primary"); mine.Name = "StartMine"; mine.MinHeight = 30;
        Grid.SetColumn(mine, 2); row.Children.Add(mine);
        return new DashedFrame(row, "BorderDefault", "BackgroundCard", PresentationTheme.RadiusMedium, new Thickness(14, 10));
    }

    public void SetAssistantOpen(bool open)
    {
        if (!open && IsAgentRunning) return;
        _assistant.IsVisible = open; PlaceAssistant();
    }
    public void ToggleAssistant() => SetAssistantOpen(!_assistant.IsVisible);
    public async Task ReviewInAssistantAsync(string jobId) { SetAssistantOpen(true); await _assistant.ReviewAsync(jobId); }

    private void PlaceAssistant()
    {
        var overlay = Bounds.Width > 0 ? Bounds.Width < DockedPanelMinWidth : Width < DockedPanelMinWidth;
        Grid.SetColumn(_assistant, overlay ? 1 : 2); Grid.SetColumnSpan(_assistant, overlay ? 2 : 1);
        _assistant.HorizontalAlignment = overlay ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        _assistant.ZIndex = overlay ? 5 : 0;
        _assistant.SetOverlay(overlay);
    }

    public void ShowModal(Control dialog) => _dialogs.Show(dialog);
    public void CloseModal() => _dialogs.Close();

    /// <summary>Keeps navigation, editing and close cancellation locked for the complete runtime lifetime.</summary>
    public Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh) => _runLock.RunAsync(run, refresh);
    public void CancelRun() => _runLock.Cancel();
}
