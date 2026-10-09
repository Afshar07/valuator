using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;

namespace ProjectOperations.Desktop.Localization;

/// <summary>
/// Bindable string source for XAML views: <c>{Binding L[tasks.title]}</c>. Raises change notifications when the language
/// switches, so bound text, flow direction and dates update in place without rebuilding the view.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged, IDisposable
{
    private readonly ILocaleContext _locale;
    private readonly ILocalizationService _text;

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

    /// <summary>Date formatting for the current language. A new instance after every switch, so date bindings re-evaluate.</summary>
    public ILocaleDateFormatter Dates { get; private set; }

    private void OnLocaleChanged(object? sender, EventArgs e)
    {
        Dates = new LocaleDateFormatter(new FixedLocaleContext(_locale.LanguageCode));
        // An empty name tells bindings that every property, the indexer included, has changed.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public void Dispose() => _locale.Changed -= OnLocaleChanged;
}
