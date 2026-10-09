using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// The theme and language choices the sidebar and the first-run screens offer. A choice that cannot be saved is refused with an error
/// in the banner and the chips go back to what is stored. Follows a theme changed elsewhere (Settings). Dispose to stop listening.
/// </summary>
internal sealed partial class DisplayOptionsViewModel : ViewModelBase, IDisposable
{
    private readonly LocaleContext _locale;
    private readonly AppearanceContext _appearance;
    private readonly Action<string> _showError;

    public DisplayOptionsViewModel(LocalizedStrings strings, LocaleContext locale, AppearanceContext appearance, Action<string> showError)
    {
        L = strings; _locale = locale; _appearance = appearance; _showError = showError;
        Theme = new ChipGroupViewModel<string>(L, AppearanceContext.Themes.Select(id => (id, (Func<string>)(() => L["theme." + id]))), appearance.Theme);
        Theme.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Theme.Selected)) ApplyTheme(Theme.Selected); };
        appearance.Changed += OnAppearanceChanged;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public ChipGroupViewModel<string> Theme { get; }

    public bool IsPersian => L.LanguageCode == "fa";
    /// <summary>The language in use, written in itself.</summary>
    public string CurrentLanguageName => IsPersian ? "فارسی" : "English";
    /// <summary>The language a click switches to, written in itself.</summary>
    public string OtherLanguageName => IsPersian ? "English" : "فارسی";

    [RelayCommand]
    private void ToggleLanguage()
    {
        try { _locale.SetLanguage(IsPersian ? "en" : "fa"); }
        catch (Exception) { _showError("validation.languageSaveFailed"); }
    }

    private void ApplyTheme(string id)
    {
        if (id == _appearance.Theme) return;
        try { _appearance.SetTheme(id); }
        catch (Exception)
        {
            _showError("validation.themeSaveFailed");
            Theme.Selected = _appearance.Theme;
        }
    }

    private void OnAppearanceChanged(object? sender, EventArgs e) => Theme.Selected = _appearance.Theme;

    public void Dispose() => _appearance.Changed -= OnAppearanceChanged;
}
