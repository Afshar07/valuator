using CommunityToolkit.Mvvm.ComponentModel;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// The date row of the task and milestone dialogs: quick chips, one picker per calendar system (Jalali in Persian, Gregorian
/// otherwise; both edit the same day) and the relative and Gregorian hints. Dates are whole days: a newly chosen day is
/// stored at 23:59 local time, and an existing time of day is kept. Shown by <see cref="DateFieldView"/>.
/// </summary>
public sealed partial class DateFieldViewModel : ViewModelBase
{
    private static readonly TimeSpan EndOfDay = new(23, 59, 0);
    private readonly TimeProvider _time;
    private readonly TimeSpan _timeOfDay;
    private readonly List<(int? Offset, ChipOptionViewModel Chip)> _chips = [];

    [ObservableProperty] private DateTime? _date;

    private DateFieldViewModel(LocalizedStrings strings, DateTime? day, TimeSpan timeOfDay, bool allowNone, TimeProvider time)
    {
        L = strings; AllowNone = allowNone; _time = time; _date = day; _timeOfDay = timeOfDay;

        var options = new List<(int? Offset, string Key)> { (0, "v3.dToday"), (1, "v3.dTomorrow"), (7, "v3.dWeek"), (14, "v3.d2Week") };
        if (allowNone) options.Add((null, "v3.dNone"));
        foreach (var (offset, key) in options)
        {
            var captured = offset;
            _chips.Add((offset, new ChipOptionViewModel(strings, () => strings[key], () => Date = captured is { } days ? Today.AddDays(days) : null)));
        }
        Chips = _chips.Select(chip => chip.Chip).ToList();
        UpdateChips();
        RefreshOnLanguageChange(strings);
    }

    /// <summary>A stored date (or none): its day is shown and its time of day is kept; midnight counts as "no time" and becomes end of day.</summary>
    public static DateFieldViewModel Existing(LocalizedStrings strings, DateTimeOffset? due, bool allowNone, TimeProvider time)
    {
        var local = due is { } instant ? TimeZoneInfo.ConvertTime(instant, time.LocalTimeZone) : (DateTimeOffset?)null;
        var timeOfDay = local?.TimeOfDay is { } kept && kept != TimeSpan.Zero ? kept : EndOfDay;
        return new(strings, local?.Date, timeOfDay, allowNone, time);
    }

    /// <summary>A new item's default: <paramref name="daysFromToday"/> days ahead, due at the end of that day.</summary>
    public static DateFieldViewModel Ahead(LocalizedStrings strings, int daysFromToday, bool allowNone, TimeProvider time) =>
        new(strings, time.GetLocalNow().Date.AddDays(daysFromToday), EndOfDay, allowNone, time);

    public LocalizedStrings L { get; }
    public bool AllowNone { get; }
    public IReadOnlyList<ChipOptionViewModel> Chips { get; }
    /// <summary>Persian edits the Jalali picker; every other language the Gregorian one.</summary>
    public bool IsPersian => L.LanguageCode == "fa";

    private DateTime Today => _time.GetLocalNow().Date;
    private DateTimeOffset At(DateTime day) => new(day, _time.LocalTimeZone.GetUtcOffset(day));

    /// <summary>"Today", "in 3 days", "2 days late"... for the chosen day; empty without one.</summary>
    public string RelativeText => Date is { } day ? L.Relative(day, Today) : "";

    /// <summary>In Persian, the chosen day in the Gregorian calendar (left-to-right, so its digits keep their order); otherwise empty.</summary>
    public string ConvertedText => Date is { } day && IsPersian && JalaliDate.IsSupported(day)
        ? L.Format("date.gregorianHint", JalaliDate.KeepLeftToRight(day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))) : "";

    /// <summary>The chosen day at the original time of day (end of day for a new date), or null when no date is set.</summary>
    public DateTimeOffset? Value => Date is { } day ? At(day + _timeOfDay) : null;

    partial void OnDateChanged(DateTime? value)
    {
        UpdateChips();
        OnPropertyChanged(nameof(RelativeText)); OnPropertyChanged(nameof(ConvertedText)); OnPropertyChanged(nameof(Value));
    }

    private void UpdateChips()
    {
        foreach (var (offset, chip) in _chips)
            chip.IsSelected = offset is { } days ? Date == Today.AddDays(days) : Date is null;
    }
}
