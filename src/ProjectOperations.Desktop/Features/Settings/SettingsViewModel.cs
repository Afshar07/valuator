using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Settings;

/// <summary>
/// Settings: appearance and language are editable here, with application updates, the optional Google Calendar sign-in and the stage
/// editor. The agent runtime settings come from environment variables and the database location from the data directory, so they are
/// shown read-only. Backup and encryption are not built yet. Disposed by the shell when the page is replaced.
/// </summary>
internal sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsServices _services;

    public SettingsViewModel(SettingsServices services, StageEditorViewModel stages)
    {
        _services = services; L = services.Strings; Stages = stages;
        Theme = new ChipGroupViewModel<string>(L, AppearanceContext.Themes.Select(id => (id, (Func<string>)(() => L["theme." + id]))), services.Appearance.Theme);
        Language = new ChipGroupViewModel<string>(L, new (string, Func<string>)[] { ("en", () => "English"), ("fa", () => "فارسی") }, L.LanguageCode);
        Theme.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Theme.Selected)) ApplyTheme(Theme.Selected); };
        Language.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Language.Selected)) ApplyLanguage(Language.Selected); };
        services.Appearance.Changed += OnAppearanceChanged;
        Updates = new UpdatePanelViewModel(services);
        Google = services.Calendar.IsAvailable ? new GoogleCalendarPanelViewModel(services) : null;
        RefreshOnLanguageChange(L);
    }

    /// <summary>Reads the stages and, when the Google overlay is offered, whether it is connected, so the page opens complete.</summary>
    public static async Task<SettingsViewModel> LoadAsync(SettingsServices services)
    {
        var settings = new SettingsViewModel(services, await StageEditorViewModel.LoadAsync(services));
        if (settings.Google is { } google) await google.RefreshAsync();
        return settings;
    }

    public LocalizedStrings L { get; }

    public ChipGroupViewModel<string> Theme { get; }
    public ChipGroupViewModel<string> Language { get; }

    public UpdatePanelViewModel Updates { get; }

    /// <summary>The Google Calendar sign-in, or null when no Google client is configured (the card is then not shown at all).</summary>
    public GoogleCalendarPanelViewModel? Google { get; }
    public bool HasGoogle => Google is not null;

    public StageEditorViewModel Stages { get; }

    // AI assistant: environment variables, shown but never edited.
    public bool AgentConfigured => _services.Environment.AgentConfigured;
    public string AgentStatusText => L[AgentConfigured ? "agent.state.configured" : "agent.state.off"];
    public string Endpoint => _services.Environment.Endpoint ?? "";
    public string ConfigDirectory => _services.Environment.ConfigDirectory ?? "";

    // Variable names are code, so they are isolated to stay readable in right-to-left text.
    public string EndpointLabel => $"{L["settings.endpoint"]} · ⁦PROJECTOPS_OPENCODE_URL⁩";
    public string ConfigDirectoryLabel => $"{L["settings.configDirectory"]} · ⁦PROJECTOPS_OPENCODE_CONFIG_DIR⁩";
    public string SentTitle => L["settings.sentTitle"] + ". ";

    // Data: where the database lives.
    public string DatabaseLabel => L["settings.database"] + " · SQLite";
    public string DatabaseText => _services.Environment.DatabasePath ?? L["date.notSet"];
    public bool CanOpenFolder => _services.Environment.DatabasePath is not null;

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private Task OpenFolderAsync() => _services.Host.RunAsync(async () =>
    {
        var directory = Path.GetDirectoryName(_services.Environment.DatabasePath ?? "");
        if (string.IsNullOrEmpty(directory) || !await _services.Files.OpenFolderAsync(directory)) _services.Host.ShowError("documents.openFailed");
    });

    [RelayCommand]
    private Task ShowWelcomeAsync() => _services.Host.RunAsync(() => { _services.Host.ShowWelcome(); return Task.CompletedTask; });

    private void ApplyTheme(string id)
    {
        if (id == _services.Appearance.Theme) return;
        try { _services.Appearance.SetTheme(id); }
        catch (Exception)
        {
            _services.Host.ShowError("validation.themeSaveFailed");
            Theme.Selected = _services.Appearance.Theme;
        }
    }

    private void ApplyLanguage(string code)
    {
        if (code == _services.Locale.LanguageCode) return;
        try { _services.Locale.SetLanguage(code); }
        catch (Exception)
        {
            _services.Host.ShowError("validation.languageSaveFailed");
            Language.Selected = _services.Locale.LanguageCode;
        }
    }

    /// <summary>The sidebar changes the theme too, so the choice here follows whoever changed it last.</summary>
    private void OnAppearanceChanged(object? sender, EventArgs e) => Theme.Selected = _services.Appearance.Theme;

    protected override void OnLanguageChanged() => Language.Selected = _services.Locale.LanguageCode;

    public void Dispose()
    {
        _services.Appearance.Changed -= OnAppearanceChanged;
        Updates.Dispose();
        Google?.Dispose();
    }
}
