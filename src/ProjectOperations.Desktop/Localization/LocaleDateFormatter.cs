using System.Globalization;

namespace ProjectOperations.Desktop.Localization;

public interface ILocaleDateFormatter
{
    string Display(DateTimeOffset? value);
    string Edit(DateTimeOffset? value);
    string ShortDate(DateTime value);
    string MonthYear(DateTime value);
}

/// <summary>Persian displays Jalali dates, English Gregorian; stored instants and <see cref="Edit"/> stay Gregorian.</summary>
public sealed class LocaleDateFormatter(ILocaleContext locale) : ILocaleDateFormatter
{
    private bool Jalali => locale.LanguageCode == "fa";

    public string Display(DateTimeOffset? value)
    {
        if (value is not { } instant) return "";
        var local = instant.ToLocalTime();
        if (!Jalali || !JalaliDate.IsSupported(local.DateTime)) return local.ToString("g", locale.Culture);
        // Keep the date and time in reading order inside right-to-left text.
        return JalaliDate.KeepLeftToRight($"{JalaliDate.Format(local.DateTime)} {local.ToString("HH:mm", CultureInfo.InvariantCulture)}");
    }

    public string Edit(DateTimeOffset? value) => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";

    // The leading right-to-left mark stops the day number joining a neighbouring Latin run (such as an English task title).
    public string ShortDate(DateTime value) => Jalali ? "\u200F" + JalaliDate.ShortDate(value) : value.ToString("MMM d", locale.Culture);

    public string MonthYear(DateTime value) => Jalali ? JalaliDate.MonthYear(value) : value.ToString("MMMM yyyy", locale.Culture);
}
