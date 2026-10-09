using System.Globalization;

namespace ProjectOperations.Desktop.Localization;

/// <summary>Reads a typed local date and time. Jalali or Gregorian dates are both accepted and stored as Gregorian instants.</summary>
public static class LocalDateInput
{
    /// <summary>
    /// Parses <c>yyyy-MM-dd HH:mm</c> local time, or the same with a Jalali date part (<c>1405/07/17 09:30</c>, Persian digits allowed).
    /// Blank input is no date. Throws <see cref="FormatException"/> for anything else, including ambiguous or nonexistent
    /// daylight-saving times.
    /// </summary>
    public static DateTimeOffset? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = ConvertJalaliDatePart(text.Trim());
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || TimeZoneInfo.Local.IsInvalidTime(date) || TimeZoneInfo.Local.IsAmbiguousTime(date))
            throw new FormatException("Enter an unambiguous local date as yyyy-MM-dd HH:mm, or leave it empty.");
        return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
    }

    /// <summary>Rewrites a Jalali date part ("1405/07/17 09:30", Persian digits allowed) as Gregorian "2026-10-09 09:30"; any other text is returned unchanged.</summary>
    private static string ConvertJalaliDatePart(string text)
    {
        var parts = text.Split(' ', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && JalaliDate.TryParse(parts[0], out var day)
            ? $"{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {JalaliDate.NormalizeDigits(parts[1])}" : text;
    }
}
