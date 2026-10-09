using System.Globalization;

namespace ProjectOperations.Desktop.Localization;

/// <summary>
/// Jalali (Solar Hijri) conversion and formatting for the presentation layer. Stored dates stay Gregorian instants;
/// this type only converts at the entry/display boundary, using the framework's <see cref="PersianCalendar"/>.
/// </summary>
public static class JalaliDate
{
    /// <summary>Entered years below this are read as Jalali, at or above as Gregorian (Jalali 1700 is Gregorian 2321).</summary>
    public const int GregorianYearThreshold = 1700;

    private static readonly PersianCalendar Calendar = new();

    public static readonly IReadOnlyList<string> MonthNames =
        ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];

    private static readonly DateTime MinDate = Calendar.MinSupportedDateTime.Date.AddDays(1);
    private static readonly DateTime MaxDate = new(9999, 12, 29);

    public static bool IsSupported(DateTime date) => date.Date >= MinDate && date.Date <= MaxDate;

    public static (int Year, int Month, int Day) FromGregorian(DateTime date) =>
        (Calendar.GetYear(date), Calendar.GetMonth(date), Calendar.GetDayOfMonth(date));

    public static DateTime ToGregorian(int year, int month, int day) => Calendar.ToDateTime(year, month, day, 0, 0, 0, 0);

    public static int DaysInMonth(int year, int month) => Calendar.GetDaysInMonth(year, month);

    public static bool IsValid(int year, int month, int day) =>
        year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1
        && day <= DaysInMonth(year, month) && ToGregorianOrNull(year, month, day) is not null;

    /// <summary>First Gregorian day of the Jalali month containing <paramref name="date"/>.</summary>
    public static DateTime MonthStart(DateTime date)
    {
        var (year, month, _) = FromGregorian(date); return ToGregorian(year, month, 1);
    }

    /// <summary>First Gregorian day of the Jalali month <paramref name="months"/> after (or before) the one containing <paramref name="date"/>.</summary>
    public static DateTime AddMonths(DateTime date, int months)
    {
        var (year, month, _) = FromGregorian(date);
        var index = year * 12 + (month - 1) + months;
        return ToGregorian(index / 12, index % 12 + 1, 1);
    }

    public static int DaysInMonth(DateTime date)
    {
        var (year, month, _) = FromGregorian(date); return DaysInMonth(year, month);
    }

    /// <summary>Jalali yyyy/MM/dd with Latin digits, the form accepted back by <see cref="TryParse"/>.</summary>
    public static string Format(DateTime date)
    {
        if (!IsSupported(date)) return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var (year, month, day) = FromGregorian(date);
        return string.Create(CultureInfo.InvariantCulture, $"{year:0000}/{month:00}/{day:00}");
    }

    /// <summary>Short display such as "17 مهر".</summary>
    public static string ShortDate(DateTime date)
    {
        if (!IsSupported(date)) return date.ToString("MMM d", CultureInfo.InvariantCulture);
        var (_, month, day) = FromGregorian(date);
        return string.Create(CultureInfo.InvariantCulture, $"{day} {MonthNames[month - 1]}");
    }

    /// <summary>Month heading such as "مهر 1405".</summary>
    public static string MonthYear(DateTime date)
    {
        if (!IsSupported(date)) return date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var (year, month, _) = FromGregorian(date);
        return string.Create(CultureInfo.InvariantCulture, $"{MonthNames[month - 1]} {year}");
    }

    /// <summary>
    /// Parses a typed date, converting automatically: <c>1405/07/17</c> (Jalali) and <c>2026-10-09</c> (Gregorian) both yield
    /// the same day. Accepts Persian and Arabic-Indic digits and the separators <c>/ - .</c>. Returns false for anything else,
    /// including impossible days such as Esfand 30 in a common year.
    /// </summary>
    public static bool TryParse(string? text, out DateTime date)
    {
        date = default;
        var parts = NormalizeDigits(text ?? "").Trim().Split(['/', '-', '.', '٫', '٬'], StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts.Any(part => part.Length is 0 or > 4 || !part.All(char.IsAsciiDigit))) return false;
        var year = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var month = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var day = int.Parse(parts[2], CultureInfo.InvariantCulture);
        if (year >= GregorianYearThreshold)
        {
            if (year > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
            date = new DateTime(year, month, day); return true;
        }
        if (!IsValid(year, month, day)) return false;
        date = ToGregorian(year, month, day); return true;
    }

    /// <summary>
    /// Keeps digits, separators and spaces in left-to-right order inside right-to-left text. Avalonia's text layout ignores
    /// the directional isolates (U+2066/U+2069), so left-to-right marks (U+200E) go around the text and on both sides of each space.
    /// </summary>
    public static string KeepLeftToRight(string text) => "\u200E" + text.Replace(" ", "\u200E \u200E") + "\u200E";

    /// <summary>Replaces Persian (۰-۹) and Arabic-Indic (٠-٩) digits with ASCII digits.</summary>
    public static string NormalizeDigits(string text) => string.Create(text.Length, text, static (span, source) =>
    {
        for (var index = 0; index < source.Length; index++)
        {
            var c = source[index];
            span[index] = c is >= '۰' and <= '۹' ? (char)('0' + c - '۰')
                : c is >= '٠' and <= '٩' ? (char)('0' + c - '٠') : c;
        }
    });

    private static DateTime? ToGregorianOrNull(int year, int month, int day)
    {
        try { return ToGregorian(year, month, day); } catch (ArgumentException) { return null; }
    }
}
