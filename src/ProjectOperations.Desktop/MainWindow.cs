using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    private readonly ProjectService _projects;
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

    public MainWindow(ProjectService projects, AgentService agents, Func<Task> initialize, string configuration, LocaleContext? locale = null, Func<string>? configurationText = null,
        AppearanceContext? appearance = null, DesktopEnvironment? environment = null)
    {
        _projects = projects;
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
        Content = _shell;
        PlaceAssistant();
        SizeChanged += (_, _) => PlaceAssistant();
        KeyDown += (_, e) => { if (e.Key == Key.Escape && _dialogLayer.IsVisible) { CloseModal(); e.Handled = true; } };

        Closed += (_, _) => { _locale.Changed -= LocaleChanged; _appearance.Changed -= AppearanceChanged; _localized.Dispose(); };
        Opened += async (_, _) =>
        {
            SetNavigationEnabled(false);
            try { await GuardAsync(async () => { await initialize(); await NavigateAsync("dashboard"); }); }
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
        _page.Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new ReadableColumn { Child = new Border { Padding = new Thickness(32, 28, 32, 40), Child = control } }
        };
    }

    public async Task NavigateAsync(string page)
    {
        var create = page == "projects:new";
        if (create) page = "projects";
        _projectTabs = null; _currentProject = null; _navigation.Select(page);
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
        if (create) ShowModal(new CreateProjectView(_context));
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

    private async Task RefreshAttentionAsync()
    {
        var dashboard = ProjectSummaries.Dashboard(await _projects.ListAsync(), DateTimeOffset.Now);
        _navigation.SetAttentionCount(dashboard.OverdueTasks.Count);
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
