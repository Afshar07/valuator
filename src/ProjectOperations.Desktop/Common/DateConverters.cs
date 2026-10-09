using System.Globalization;
using Avalonia.Data.Converters;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Locale-aware date text for XAML. Bind the date and <see cref="LocalizedStrings.Dates"/> so the text follows language switches:
/// <code>
/// &lt;MultiBinding Converter="{x:Static common:DateConverters.Display}"&gt;
///   &lt;Binding Path="DueAt"/&gt;&lt;Binding Path="L.Dates"/&gt;
/// &lt;/MultiBinding&gt;
/// </code>
/// Missing dates convert to an empty string; view-models decide what "no date" reads as.
/// </summary>
public static class DateConverters
{
    /// <summary>Date and time (Jalali in Persian).</summary>
    public static readonly IMultiValueConverter Display = new DateConverter((dates, instant) => dates.Display(instant), (dates, day) => dates.Display(new DateTimeOffset(day)));
    /// <summary>Gregorian <c>yyyy-MM-dd HH:mm</c> for editing, in every language.</summary>
    public static readonly IMultiValueConverter Edit = new DateConverter((dates, instant) => dates.Edit(instant), (dates, day) => dates.Edit(new DateTimeOffset(day)));
    /// <summary>Short day and month, such as "Oct 9" or "17 مهر".</summary>
    public static readonly IMultiValueConverter ShortDate = new DateConverter((dates, instant) => dates.ShortDate(instant.ToLocalTime().DateTime), (dates, day) => dates.ShortDate(day));
    /// <summary>Month heading, such as "October 2026" or "مهر 1405".</summary>
    public static readonly IMultiValueConverter MonthYear = new DateConverter((dates, instant) => dates.MonthYear(instant.ToLocalTime().DateTime), (dates, day) => dates.MonthYear(day));

    private sealed class DateConverter(Func<ILocaleDateFormatter, DateTimeOffset, string> instant, Func<ILocaleDateFormatter, DateTime, string> day) : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2 || values[1] is not ILocaleDateFormatter dates) return "";
            return values[0] switch
            {
                DateTimeOffset value => instant(dates, value),
                DateTime value => day(dates, value),
                _ => ""
            };
        }
    }
}
