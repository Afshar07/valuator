using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

public sealed class MainWindow : Window, IShell
{
    /// <summary>Below this window width the assistant panel slides over the content instead of docking beside it.</summary>
    private const double DockedPanelMinWidth = 1280;
    private static readonly (string Page, string Key, string Name, string Icon)[] Pages =
    [
        ("dashboard", "navigation.attention", "NavigationDashboard", Icons.BellSimple),
        ("projects", "navigation.projects", "NavigationProjects", Icons.Folders),
        ("calendar", "presentation.navigationCalendar", "NavigationCalendar", Icons.CalendarBlank),
        ("documents", "presentation.navigationDocuments", "NavigationDocuments", Icons.Files),
        ("settings", "presentation.navigationSettings", "NavigationSettings", Icons.GearSix)
    ];

    private ProjectService _projects => _context.Projects;
    private readonly ContentControl _page = new() { Name = "ScreenHost" };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Start, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _errorBanner;
    private readonly AppSidebar _navigation;
    private readonly AssistantPanel _assistant;
    private readonly Grid _shell;
    private readonly Panel _dialogLayer = new() { IsVisible = false, ZIndex = 10 };
    private TabControl? _projectTabs;
    private Guid? _currentProject;
    private CancellationTokenSource? _runCancellation;
    private Task? _running;
    private bool _closing;
    private bool _closeRequested;
    private readonly LocaleContext _locale;
    private readonly AppearanceContext _appearance;
    private readonly LocalizationService _text;
    private readonly LocalizedControls _localized;
    private readonly PresentationContext _context;
    private string _errorKey = "";
    private readonly Panel _overlay = new() { IsVisible = false, ZIndex = 30, Name = "OnboardingOverlay" };
    private readonly Border _toast;
    private readonly TextBlock _toastText = new() { TextWrapping = TextWrapping.Wrap, Name = "ToastText" };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(4.8) };
    private SampleWorkspace? _sample;
    private (ProjectService Projects, AgentService Agents, DesktopEnvironment Environment)? _real;
    private readonly Dictionary<string, bool> _visible = [];
    private readonly HashSet<string> _fresh = [];
    private bool _shellKnown;

    public MainWindow(ProjectService projects, AgentService agents, Func<Task> initialize, string configuration, LocaleContext? locale = null, Func<string>? configurationText = null,
        AppearanceContext? appearance = null, DesktopEnvironment? environment = null)
    {
        _locale = locale ?? new LocaleContext();
        _appearance = appearance ?? new AppearanceContext();
        _text = new LocalizationService(_locale);
        _localized = new LocalizedControls(_locale);
        _context = new PresentationContext(projects, agents, _locale, _appearance, _text, _localized, configuration, configurationText, environment ?? new DesktopEnvironment(), this);
        PresentationTheme.Apply(this);
        this.Paint(BackgroundProperty, "BackgroundApp");
        PresentationTheme.Typeset(_error, "Small", "TextPrimary");
        _localized.Bind(_error, control => control.Text = _errorKey.Length == 0 ? "" : _text.Get(_errorKey));
        _locale.Changed += LocaleChanged;
        _appearance.Changed += AppearanceChanged;
        _localized.Bind(this, window => window.Title = _text.Get("app.title"));
        ApplyLocalePresentation();
        RequestedThemeVariant = PresentationTheme.Variant(_appearance.Theme);
        Width = 1280; Height = 860; MinWidth = 800; MinHeight = 600;

        _navigation = new AppSidebar(_context, Pages, page => _ = GuardAsync(() => NavigateAsync(page)));
        _assistant = new AssistantPanel(_context) { Name = "AssistantPanel", IsVisible = false, Width = 360 };

        var dismiss = new Button { Content = Icons.Glyph(Icons.X, 14, "TextSecondary"), VerticalAlignment = VerticalAlignment.Top }; dismiss.Classes.Add("icon");
        _localized.Bind(dismiss, control => Avalonia.Automation.AutomationProperties.SetName(control, _text.Get("action.close")));
        dismiss.Click += (_, _) => HideError();
        var errorRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        var errorIcon = Icons.Glyph(Icons.WarningCircle, 17, "Error"); errorIcon.VerticalAlignment = VerticalAlignment.Top; errorIcon.Margin = new Thickness(0, 1, 0, 0);
        errorRow.Children.Add(errorIcon); Grid.SetColumn(_error, 1); errorRow.Children.Add(_error); Grid.SetColumn(dismiss, 2); errorRow.Children.Add(dismiss);
        _errorBanner = new Border { Name = "ErrorBanner", IsVisible = false, Padding = new Thickness(14, 8, 8, 8), Margin = new Thickness(32, 16, 32, 0), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), BorderThickness = new Thickness(1), Child = errorRow }
            .Paint(Border.BackgroundProperty, "ErrorSoft").Paint(Border.BorderBrushProperty, "Error");

        var body = new DockPanel { Name = "WorkspaceBody" };
        DockPanel.SetDock(_errorBanner, Dock.Top); body.Children.Add(_errorBanner);
        body.Children.Add(_page);

        _shell = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        _shell.Children.Add(_navigation);
        Grid.SetColumn(body, 1); _shell.Children.Add(body);
        _shell.Children.Add(_assistant);
        Grid.SetColumnSpan(_dialogLayer, 3); _shell.Children.Add(_dialogLayer);
        Grid.SetColumnSpan(_overlay, 3); _shell.Children.Add(_overlay);
        PresentationTheme.Typeset(_toastText, "Small", "BackgroundApp");
        var toastRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        toastRow.Children.Add(Icons.Glyph(Icons.Info, 16, "BackgroundApp", IconWeight.Fill)); _toastText.VerticalAlignment = VerticalAlignment.Center; toastRow.Children.Add(_toastText);
        _toast = new Border
        {
            Name = "Toast",
            IsVisible = false,
            ZIndex = 40,
            IsHitTestVisible = false,
            MaxWidth = 540,
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(20, 0, 20, 20),
            Child = toastRow
        }
            .Paint(Border.BackgroundProperty, "TextPrimary").RaisedShadowed();
        Grid.SetColumnSpan(_toast, 3); _shell.Children.Add(_toast);
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _toast.IsVisible = false; };
        Content = _shell;
        PlaceAssistant();
        SizeChanged += (_, _) => PlaceAssistant();
        KeyDown += (_, e) => { if (e.Key == Key.Escape && _dialogLayer.IsVisible) { CloseModal(); e.Handled = true; } };

        Closed += (_, _) => { _locale.Changed -= LocaleChanged; _appearance.Changed -= AppearanceChanged; _localized.Dispose(); };
        Opened += async (_, _) =>
        {
            SetNavigationEnabled(false);
            try { await GuardAsync(async () => { await initialize(); await StartAsync(); }); }
            finally { SetNavigationEnabled(true); }
        };
        Closing += async (_, e) =>
        {
            if (_closing || _running is null) return;
            e.Cancel = true; _closeRequested = true;
            _runCancellation?.Cancel();
            await GuardAsync(async () => { await _running; _closing = true; Close(); });
        };
    }

    public bool IsAgentRunning => _running is not null;

    private void SetNavigationEnabled(bool enabled) => _navigation.IsEnabled = enabled;
    private async Task GuardAsync(Func<Task> action, bool clearError = true)
    {
        if (clearError) HideError();
        try { await action(); }
        catch (OperationCanceledException) { ShowError("validation.operationCancelled"); }
        catch (Exception exception) { LastFailure = exception; ShowError("validation.operationFailed"); }
    }
    /// <summary>The most recent exception reported to the user as a generic failure; retained for diagnostics and tests.</summary>
    public Exception? LastFailure { get; private set; }
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
    public void ShowError(string key) { _errorKey = key; _error.Text = _text.Get(key); _error.IsVisible = true; _errorBanner.IsVisible = true; }
    private void HideError() { _error.IsVisible = false; _errorBanner.IsVisible = false; }

    public async Task ActAsync(Button button, Func<Task> action)
    {
        button.IsEnabled = false; SetNavigationEnabled(false); _page.IsEnabled = false;
        try { await GuardAsync(action); }
        finally { button.IsEnabled = true; SetNavigationEnabled(!IsAgentRunning); _page.IsEnabled = !IsAgentRunning; }
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

    public async Task NavigateAsync(string page)
    {
        var create = page == "projects:new";
        if (create) page = "projects";
        _projectTabs = null; _currentProject = null; _navigation.Select(page);
        _fresh.Remove(page); _navigation.SetNew(page, false);
        Control view = page switch
        {
            "projects" => await Load(new ProjectsView(_context), view => view.LoadAsync()),
            "calendar" => await Load(new CalendarView(_context), view => view.LoadAsync()),
            "documents" => await Load(new DocumentsView(_context), view => view.LoadAsync()),
            "settings" => new SettingsView(_context),
            _ => await Load(new DashboardView(_context), view => view.LoadAsync())
        };
        Show(view);
        await _assistant.ShowPickerAsync();
        await RefreshAttentionAsync();
        if (create) ShowWizard(fromWelcome: false);
    }

    private static async Task<T> Load<T>(T view, Func<T, Task> load) where T : Control { await load(view); return view; }

    public async Task OpenProjectAsync(Guid id, int selectedTab = 0)
    {
        var project = await _projects.GetAsync(id);
        if (project is null) { ShowError("validation.projectUnavailable"); return; }
        _navigation.Select("projects");
        var view = new ProjectDetailView(_context, project); await view.LoadAsync(selectedTab);
        _projectTabs = view.Tabs; _currentProject = id; Show(view);
        await _assistant.ShowProjectAsync(project);
        if (selectedTab == 3) SetAssistantOpen(true);
        await RefreshAttentionAsync();
    }

    public Task RefreshProjectAsync() => _currentProject is { } id ? OpenProjectAsync(id, _projectTabs?.SelectedIndex ?? 0) : Task.CompletedTask;

    public bool IsSample => _sample is not null;
    public Task RefreshShellAsync() => UpdateShellAsync();
    private Task RefreshAttentionAsync() => UpdateShellAsync();

    /// <summary>First screen: the attention home when it has something to show, otherwise the project list; the welcome screen sits on top of an empty workspace.</summary>
    private async Task StartAsync()
    {
        var count = (await _projects.ListAsync()).Count;
        await UpdateShellAsync();
        await NavigateAsync(_navigation.IsPageVisible("dashboard") ? "dashboard" : "projects");
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
        var shown = new Dictionary<string, bool> { ["dashboard"] = IsSample || hasDated, ["projects"] = true, ["calendar"] = IsSample || hasDated, ["documents"] = IsSample || hasFile, ["settings"] = true };
        var revealed = new List<string>();
        foreach (var (page, show) in shown)
        {
            if (_shellKnown && !IsSample && show && !_visible.GetValueOrDefault(page)) { _fresh.Add(page); revealed.Add(page); }
            _visible[page] = show; _navigation.SetVisible(page, show); _navigation.SetNew(page, _fresh.Contains(page));
        }
        _shellKnown = true;
        if (revealed.Contains("dashboard") || revealed.Contains("calendar")) ShowToast("v3.toastDated");
        else if (revealed.Contains("documents")) ShowToast("v3.toastDocs");
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
        var target = (_currentProject is { } open ? projects.FirstOrDefault(project => project.Id == open) : null) ?? projects[0];
        var next = target.Requirements.FirstOrDefault(requirement => requirement.Status == RequirementStatus.Missing);
        var document = target.Requirements.FirstOrDefault(requirement => requirement.Type == RequirementType.Document && requirement.Files.Count == 0) ?? target.Requirements.FirstOrDefault();
        Task OpenItem(ProjectRequirement? requirement, bool followUp = false)
        {
            if (requirement is not null)
            {
                state.OpenRequirement = requirement.Id; state.ExpandedGroups.Add($"{target.Id}:{requirement.GroupId}"); state.InitializedProjects.Add(target.Id);
                if (followUp) state.PendingFollowUp = requirement.Id;
            }
            return OpenProjectAsync(target.Id, 1);
        }
        var items = new List<AppSidebar.GettingStartedItem>
        {
            new("v3.gs0", done[0], false, null),
            new("v3.gs1", done[1], false, () => OpenItem(next)),
            new("v3.gs2", done[2], false, () => OpenItem(document)),
            new("v3.gs3", done[3], false, () => OpenItem(next ?? target.Requirements.FirstOrDefault(), followUp: true)),
            new("v3.gs4", done[4], true, () => NavigateAsync("settings"))
        };
        _navigation.SetGettingStarted(items, () => { state.GettingStartedHidden = true; _navigation.SetGettingStarted(null, () => Task.CompletedTask); return Task.CompletedTask; });
    }

    public void ShowToast(string key)
    {
        _toastText.Text = _text.Get(key); _toast.IsVisible = true; _toastTimer.Stop(); _toastTimer.Start();
    }

    public void ShowWelcome() => ShowOverlay(new WelcomeScreen(_context));
    public void ShowWizard(bool fromWelcome) => ShowOverlay(new WizardScreen(_context, fromWelcome));
    public void CloseOverlay() { _overlay.IsVisible = false; _overlay.Children.Clear(); }
    private void ShowOverlay(Control screen) { _overlay.Children.Clear(); _overlay.Children.Add(screen); _overlay.IsVisible = true; }

    public async Task OpenCreatedProjectAsync(Guid id)
    {
        _context.State.TipHidden = false; _context.State.GettingStartedHidden = false;
        await OpenProjectAsync(id, 1);
        ShowToast("v3.toastCreated");
    }

    public async Task StartSampleAsync()
    {
        if (IsSample) { CloseOverlay(); return; }
        var workspace = await SampleWorkspace.CreateAsync(_locale.LanguageCode == "fa");
        _real = (_context.Projects, _context.Agents, _context.Environment);
        _sample = workspace;
        _context.Projects = workspace.Projects; _context.Agents = workspace.Agents; _context.Environment = _real.Value.Environment with { AgentConfigured = false };
        ResetShellFacts(); CloseOverlay();
        await UpdateShellAsync();
        await NavigateAsync("dashboard");
    }

    /// <summary>Discards the sample workspace and returns to the user's own data; the caller decides what to show next.</summary>
    public Task ExitSampleAsync()
    {
        if (_real is not { } real) return Task.CompletedTask;
        _context.Projects = real.Projects; _context.Agents = real.Agents; _context.Environment = real.Environment;
        _sample?.Dispose(); _sample = null; _real = null;
        ResetShellFacts();
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

    public void ShowModal(Control dialog)
    {
        _dialogLayer.Children.Clear();
        var scrim = new Border().Paint(Border.BackgroundProperty, "Scrim");
        scrim.PointerPressed += (_, _) => CloseModal();
        dialog.HorizontalAlignment = HorizontalAlignment.Center; dialog.VerticalAlignment = VerticalAlignment.Center;
        _dialogLayer.Children.Add(scrim); _dialogLayer.Children.Add(dialog); _dialogLayer.IsVisible = true;
    }
    public void CloseModal() { _dialogLayer.IsVisible = false; _dialogLayer.Children.Clear(); }

    /** Keep navigation, editing and close cancellation locked for the complete runtime lifetime. */
    public async Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh)
    {
        _runCancellation = new CancellationTokenSource();
        SetNavigationEnabled(false); _page.IsEnabled = false;
        if (_projectTabs is not null)
            foreach (var item in _projectTabs.Items.OfType<TabItem>()) item.IsEnabled = false;
        _running = GuardAsync(() => run(_runCancellation.Token));
        try { await _running; }
        finally
        {
            _running = null; _runCancellation.Dispose(); _runCancellation = null;
            // Reload while still locked: unlocking first would let the user act on screens that the pending reload then replaces.
            // A window that is closing only waits for the runtime outcome; it does not reload screens.
            try { if (!_closeRequested) await GuardAsync(refresh, clearError: false); }
            finally
            {
                SetNavigationEnabled(true); _page.IsEnabled = true;
                if (_projectTabs is not null)
                    foreach (var item in _projectTabs.Items.OfType<TabItem>()) item.IsEnabled = true;
            }
        }
    }
    public void CancelRun() => _runCancellation?.Cancel();

    public static DateTimeOffset? ParseLocalDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || TimeZoneInfo.Local.IsInvalidTime(date) || TimeZoneInfo.Local.IsAmbiguousTime(date))
            throw new FormatException("Enter an unambiguous local date as yyyy-MM-dd HH:mm, or leave it empty.");
        return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
    }
}
