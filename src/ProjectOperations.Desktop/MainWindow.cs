using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

public sealed class MainWindow : Window
{
    private readonly ProjectService _projects;
    private readonly ContentControl _page = new() { Name = "ScreenHost" };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Start, IsVisible = false };
    private readonly AppSidebar _navigation;
    private readonly TopBar _topBar;
    private TabControl? _projectTabs;
    private CancellationTokenSource? _runCancellation;
    private Task? _running;
    private bool _closing;
    private readonly LocaleContext _locale;
    private readonly LocalizationService _text;
    private readonly LocalizedControls _localized;
    private readonly PresentationContext _context;
    private string _errorKey = "";

    public MainWindow(ProjectService projects, AgentService agents, Func<Task> initialize, string configuration, LocaleContext? locale = null, Func<string>? configurationText = null)
    {
        _projects = projects;
        _locale = locale ?? new LocaleContext();
        _text = new LocalizationService(_locale);
        _localized = new LocalizedControls(_locale);
        _context = new PresentationContext(projects, agents, _locale, _text, _localized, configuration, configurationText,
            OpenProjectAsync, ActAsync, ShowError, RunAgentAsync, () => _runCancellation?.Cancel());
        PresentationTheme.Apply(this);
        _error.Foreground = PresentationTheme.Brush("Error");
        _localized.Bind(_error, control => control.Text = _errorKey.Length == 0 ? "" : _text.Get(_errorKey));
        _locale.Changed += LocaleChanged;
        _localized.Bind(this, window => window.Title = _text.Get("app.title"));
        FlowDirection = _locale.FlowDirection;
        Width = 1280; Height = 860; MinWidth = 800; MinHeight = 600;
        Background = PresentationTheme.Brush("BackgroundApp");
        _navigation = new AppSidebar(_context, DashboardAsync, ProjectsAsync);
        _topBar = new TopBar(_context, () => { ShowCreate(); return Task.CompletedTask; });
        var shell = new Grid { ColumnDefinitions = new ColumnDefinitions("208,*") };
        shell.Children.Add(_navigation);
        var body = new DockPanel { Name = "WorkspaceBody" };
        Grid.SetColumn(body, 1);
        DockPanel.SetDock(_topBar, Dock.Top); body.Children.Add(_topBar);
        DockPanel.SetDock(_error, Dock.Top); _error.Margin = new Thickness(40, 8); body.Children.Add(_error);
        _page.Margin = new Thickness(40, 16, 40, 24); body.Children.Add(_page);
        shell.Children.Add(body); Content = shell;
        Closed += (_, _) => { _locale.Changed -= LocaleChanged; _localized.Dispose(); };
        Opened += async (_, _) =>
        {
            SetNavigationEnabled(false);
            try { await GuardAsync(async () => { await initialize(); await DashboardAsync(); }); }
            finally { SetNavigationEnabled(true); }
        };
        Closing += async (_, e) =>
        {
            if (_closing || _running is null) return;
            e.Cancel = true;
            _runCancellation?.Cancel();
            await GuardAsync(async () => { await _running; _closing = true; Close(); });
        };
    }

    private void SetNavigationEnabled(bool enabled) { _navigation.IsEnabled = enabled; _topBar.IsEnabled = enabled; }
    private async Task GuardAsync(Func<Task> action, bool clearError = true)
    {
        if (clearError) _error.IsVisible = false;
        try { await action(); }
        catch (OperationCanceledException) { ShowError("validation.operationCancelled"); }
        catch (Exception) { ShowError("validation.operationFailed"); }
    }
    private void LocaleChanged(object? sender, EventArgs e) => FlowDirection = _locale.FlowDirection;
    private void ShowError(string message) { _errorKey = message; _error.Text = _text.Get(message); _error.IsVisible = true; }
    private async Task ActAsync(Button button, Func<Task> action)
    {
        button.IsEnabled = false; SetNavigationEnabled(false); _page.IsEnabled = false;
        try { await GuardAsync(action); }
        finally { button.IsEnabled = true; SetNavigationEnabled(true); _page.IsEnabled = true; }
    }
    /// <summary>Hosts a screen in a scrollable, readable-width column that follows the inherited flow direction.</summary>
    private void Show(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        _page.Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new ReadableColumn { Child = new Border { Padding = new Thickness(0, 0, 0, 16), Child = control } }
        };
    }
    private async Task DashboardAsync()
    {
        _projectTabs = null; _navigation.Select("dashboard");
        var view = new DashboardView(_context, ProjectsAsync); await view.LoadAsync(); Show(view);
    }
    private async Task ProjectsAsync()
    {
        _projectTabs = null; _navigation.Select("projects");
        var card = new SectionCard(_context, "navigation.projects") { Name = "ProjectsCard" };
        var list = new StackPanel();
        foreach (var project in await _projects.ListAsync())
        {
            if (list.Children.Count > 0) list.Children.Add(new Border { Height = 1, Background = PresentationTheme.Brush("BorderSubtle"), Margin = new Thickness(0, 2) });
            list.Children.Add(new ProjectRow(_context, project, () => OpenProjectAsync(project.Id), statusPill: true));
        }
        if (list.Children.Count == 0) list.Children.Add(_context.Label("dashboard.empty", "Body", "TextSecondary"));
        card.Body.Children.Add(list); Show(card);
    }
    private void ShowCreate() { _projectTabs = null; _navigation.Select("projects"); Show(new CreateProjectView(_context)); }
    private async Task OpenProjectAsync(Guid id, int selectedTab = 0)
    {
        var project = await _projects.GetAsync(id);
        if (project is null) { ShowError("validation.projectUnavailable"); return; }
        _navigation.Select("projects");
        var view = new ProjectDetailView(_context, project); await view.LoadAsync(selectedTab);
        _projectTabs = view.Tabs; Show(view);
    }

    /** Keep navigation, editing and close cancellation locked for the complete runtime lifetime. */
    private async Task RunAgentAsync(Control panel, Control request, Control history, Button stop, Func<CancellationToken, Task> run, Func<Task> refresh)
    {
        _runCancellation = new CancellationTokenSource();
        SetNavigationEnabled(false); request.IsEnabled = false; history.IsEnabled = false;
        if (_projectTabs is not null)
            foreach (var item in _projectTabs.Items.OfType<TabItem>()) if (!ReferenceEquals(item.Content, panel)) item.IsEnabled = false;
        _running = GuardAsync(() => run(_runCancellation.Token));
        try { await _running; }
        finally
        {
            _running = null; _runCancellation.Dispose(); _runCancellation = null;
            SetNavigationEnabled(true); request.IsEnabled = true; history.IsEnabled = true; stop.IsVisible = false;
            if (_projectTabs is not null)
                foreach (var item in _projectTabs.Items.OfType<TabItem>()) item.IsEnabled = true;
            await GuardAsync(refresh, clearError: false);
        }
    }
    public static DateTimeOffset? ParseLocalDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || TimeZoneInfo.Local.IsInvalidTime(date) || TimeZoneInfo.Local.IsAmbiguousTime(date))
            throw new FormatException("Enter an unambiguous local date as yyyy-MM-dd HH:mm, or leave it empty.");
        return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
    }
}
