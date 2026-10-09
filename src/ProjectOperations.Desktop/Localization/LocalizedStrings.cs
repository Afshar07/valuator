using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop.Localization;

/// <summary>Something whose text is computed from <see cref="LocalizedStrings"/> and must be re-read after a language switch.</summary>
public interface ILanguageAware
{
    void OnLanguageChanged();
}

/// <summary>
/// Bindable string source for XAML views: <c>{Binding L[tasks.title]}</c>. Raises change notifications when the language
/// switches, so bound text, flow direction and dates update in place without rebuilding the view.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged, IDisposable
{
    private readonly ILocaleContext _locale;
    private readonly ILocalizationService _text;
    private readonly List<WeakReference<ILanguageAware>> _aware = [];
    private int _pruneAt = 64;

    public LocalizedStrings(ILocaleContext locale, ILocalizationService text)
    {
        _locale = locale;
        _text = text;
        Dates = new LocaleDateFormatter(new FixedLocaleContext(locale.LanguageCode));
        _locale.Changed += OnLocaleChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _text.Get(key);
    public string Format(string key, params object?[] arguments) => _text.Format(key, arguments);
    public string LanguageCode => _locale.LanguageCode;
    public CultureInfo Culture => _locale.Culture;
    public FlowDirection FlowDirection => _locale.FlowDirection;
    public bool IsRightToLeft => _locale.FlowDirection == FlowDirection.RightToLeft;

    /// <summary>Localized name of a domain enum value (task status, project status, ...).</summary>
    public string Enum<T>(T value) where T : struct, Enum => new DomainDisplay(_text).Enum(value);

    /// <summary>Localized title of a requirement; custom requirements keep the user's own title.</summary>
    public string Requirement(ProjectRequirement requirement) => new DomainDisplay(_text).Requirement(requirement);

    /// <summary>Localized title of a built-in requirement by its template id; any other id keeps <paramref name="original"/>.</summary>
    public string Requirement(string id, string original) => new DomainDisplay(_text).Requirement(id, original);

    /// <summary>Localized title of a requirement group; custom groups keep the user's own title.</summary>
    public string Group(string id, string original) => new DomainDisplay(_text).Group(id, original);

    /// <summary>Localized name of a project template; only the built-in one is translated.</summary>
    public string Template(ProjectTemplate template) => new DomainDisplay(_text).Template(template);

    public string Number(int value) => value.ToString("N0", Culture);

    /// <summary>The date and time, or "No date".</summary>
    public string Due(DateTimeOffset? date) => date is null ? this["date.none"] : Dates.Display(date);

    /// <summary>The short day and month, or "No date".</summary>
    public string ShortDate(DateTimeOffset? date) => date is null ? this["date.none"] : Dates.ShortDate(date.Value.ToLocalTime().DateTime);

    /// <summary>Whole local days from <paramref name="today"/> to the date, as friendly relative text. Open work past its date reads "late", closed work "ago".</summary>
    public string Relative(DateTimeOffset? date, bool open, DateTime today) =>
        date is null ? "" : RelativeDays((date.Value.ToLocalTime().Date - today.Date).Days, open);

    /// <summary>Relative text for a local calendar day that is not open work (so the past reads "ago").</summary>
    public string Relative(DateTime day, DateTime today) => RelativeDays((day.Date - today.Date).Days, open: false);

    private string RelativeDays(int days, bool open) => days switch
    {
        0 => this["v3.relToday"],
        1 => this["v3.relTomorrow"],
        -1 => open ? this["v3.relLateOne"] : this["v3.relYesterday"],
        > 0 => Format("v3.relIn", Number(days)),
        _ => Format(open ? "v3.relLate" : "v3.relAgo", Number(-days))
    };

    /// <summary>Date formatting for the current language. A new instance after every switch, so date bindings re-evaluate.</summary>
    public ILocaleDateFormatter Dates { get; private set; }

    /// <summary>
    /// Asks <paramref name="listener"/> to refresh after every language switch. Held weakly, so a listener nobody uses any more is never
    /// kept alive; dead entries are dropped as the list grows. Call and switch languages on the UI thread.
    /// </summary>
    public void Register(ILanguageAware listener)
    {
        if (_aware.Count >= _pruneAt)
        {
            _aware.RemoveAll(entry => !entry.TryGetTarget(out _));
            _pruneAt = Math.Max(64, _aware.Count * 2);
        }
        _aware.Add(new WeakReference<ILanguageAware>(listener));
    }

    private void OnLocaleChanged(object? sender, EventArgs e)
    {
        Dates = new LocaleDateFormatter(new FixedLocaleContext(_locale.LanguageCode));
        // An empty name tells bindings that every property, the indexer included, has changed.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        foreach (var entry in _aware.ToArray())
            if (entry.TryGetTarget(out var listener)) listener.OnLanguageChanged();
        _aware.RemoveAll(entry => !entry.TryGetTarget(out _));
    }

    public void Dispose() => _locale.Changed -= OnLocaleChanged;
}
