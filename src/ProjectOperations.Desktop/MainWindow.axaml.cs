using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Features.ProjectDetail;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Desktop.Updates;

namespace ProjectOperations.Desktop;

/// <summary>
/// The application window. Everything it shows comes from <see cref="MainWindowViewModel"/>; what stays here needs controls: the system
/// file dialogs, the code-built assistant panel (until phase 5) and where it sits, the scroll position, the toast timer and closing.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Below this window width the assistant panel slides over the content instead of docking beside it.</summary>
    private const double DockedPanelMinWidth = 1280;
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(4.8);

    private readonly MainWindowViewModel _model;
    private readonly AssistantPanel _assistant;
    private readonly LocalizedControls _localized;
    private readonly LocalizedStrings _strings;
    private readonly DispatcherTimer _toastTimer = new() { Interval = ToastDuration };
    private bool _closing;

    public MainWindow(ProjectService projects, AgentService agents, Func<Task> initialize, string configuration, LocaleContext? locale = null, Func<string>? configurationText = null,
        AppearanceContext? appearance = null, DesktopEnvironment? environment = null, IAppUpdater? updater = null, IExternalCalendarSource? calendar = null)
    {
        locale ??= new LocaleContext();
        appearance ??= new AppearanceContext();
        var text = new LocalizationService(locale);
        _localized = new LocalizedControls(locale);
        _strings = new LocalizedStrings(locale, text);
        var workspace = new WorkspaceSession(projects, agents, environment ?? new DesktopEnvironment(), calendar ?? new NoExternalCalendar());
        var dialogs = new WindowDialogs(this);
        _model = new MainWindowViewModel(workspace, locale, appearance, _strings, new UpdateController(updater ?? new NoAppUpdater()), dialogs, dialogs, initialize);
        var context = new PresentationContext(workspace, locale, text, _localized, _strings, configuration, configurationText, new ShellAdapter(_model));

        PresentationTheme.Apply(this);
        this.Paint(BackgroundProperty, "BackgroundApp");
        DataContext = _model;
        InitializeComponent();

        _assistant = new AssistantPanel(context) { Name = "AssistantPanel", IsVisible = false, Width = 360 };
        AssistantSlot.Content = _assistant;
        _model.Assistant = _assistant;
        _model.DelegationFactory = (project, jobs) => new DelegationTabViewModel(context, project, jobs);

        _model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsAssistantOpen)) { _assistant.IsVisible = _model.IsAssistantOpen; PlaceAssistant(); }
            // A new page opens at the top, as a freshly built scroll viewer did.
            else if (e.PropertyName == nameof(MainWindowViewModel.Page)) PageScroll.ScrollToHome();
        };
        _model.Messages.ToastShown += (_, _) => { _toastTimer.Stop(); _toastTimer.Start(); };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _model.Messages.DismissToast(); };
        PlaceAssistant();
        SizeChanged += (_, _) => PlaceAssistant();
        KeyDown += (_, e) => { if (e.Key == Key.Escape && _model.Dialogs.IsOpen) { _model.Dialogs.Close(); e.Handled = true; } };

        Closed += (_, _) =>
        {
            _toastTimer.Stop();
            _model.Dispose(); _localized.Dispose(); _strings.Dispose();
        };
        Opened += async (_, _) => await _model.StartAsync();
        Closing += async (_, e) =>
        {
            if (_closing || _model.RequestClose() is not { } running) return;
            e.Cancel = true;
            await _model.Messages.GuardAsync(async () => { await running; _closing = true; Close(); });
        };
    }

    /// <summary>The most recent exception reported to the user as a generic failure; retained for diagnostics and tests.</summary>
    public Exception? LastFailure => _model.LastFailure;
    public bool IsSample => _model.IsSample;
    public bool IsAgentRunning => _model.IsAgentRunning;

    public Task StartSampleAsync() => _model.StartSampleAsync();
    public Task ExitSampleAsync() => _model.ExitSampleAsync();

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e) => _model.Dialogs.Close();

    private void PlaceAssistant()
    {
        var overlay = Bounds.Width > 0 ? Bounds.Width < DockedPanelMinWidth : Width < DockedPanelMinWidth;
        Grid.SetColumn(AssistantSlot, overlay ? 1 : 2); Grid.SetColumnSpan(AssistantSlot, overlay ? 2 : 1);
        AssistantSlot.HorizontalAlignment = overlay ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        AssistantSlot.ZIndex = overlay ? 5 : 0;
        _assistant.SetOverlay(overlay);
    }

    /// <summary>The system file dialogs and launcher the view-models ask for through <see cref="IFilePicker"/> and <see cref="IFileLauncher"/>.</summary>
    private sealed class WindowDialogs(MainWindow window) : IFilePicker, IFileLauncher
    {
        public async Task<IReadOnlyList<PickedFile>> PickAsync(string title)
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = true });
            return files.Select(file => new PickedFile(file.Name, file.TryGetLocalPath())).ToList();
        }

        public async Task<bool> OpenFileAsync(string path) => await window.Launcher.LaunchFileInfoAsync(new FileInfo(path));
        public async Task<bool> OpenFolderAsync(string path) => await window.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));
    }
}
