using CommunityToolkit.Mvvm.ComponentModel;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Base for view-models. View-models reference no Avalonia controls and are tested with plain xUnit; domain rules
/// (urgency, readiness, attention) stay in Core.
/// </summary>
public abstract class ViewModelBase : ObservableObject, ILanguageAware
{
    /// <summary>
    /// Re-reads every property of this view-model when the language switches, so text computed from <paramref name="strings"/> follows
    /// the language in place. <paramref name="strings"/> holds this view-model weakly: one that is no longer shown is simply collected.
    /// </summary>
    protected void RefreshOnLanguageChange(LocalizedStrings strings) => strings.Register(this);

    void ILanguageAware.OnLanguageChanged()
    {
        OnLanguageChanged();
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Runs after a language switch, before every property is announced as changed. Override to rebuild state that depends on the
    /// language itself (for example a calendar that changes between Gregorian and Jalali months).
    /// </summary>
    protected virtual void OnLanguageChanged() { }
}
