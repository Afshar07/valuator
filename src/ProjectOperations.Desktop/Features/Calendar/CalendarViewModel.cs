using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Calendar;

/// <summary>
/// Month calendar of open tasks and milestones across live projects. In Persian it pages through Jalali months, otherwise Gregorian ones;
/// the schedule query itself stays Gregorian. When the user has opted in and connected Google Calendar, their events are overlaid
/// read-only: display only, never stored, never given to the assistant.
/// </summary>
internal sealed partial class CalendarViewModel : ViewModelBase
{
    private const int EventsPerDay = 3;

    private readonly IReadOnlyList<Project> _projects;
    private readonly PageServices _services;
    private DateTime _month;
    private IReadOnlyList<ExternalCalendarEvent> _external = [];
    private int _fetch;
    private bool _started;

    [ObservableProperty] private IReadOnlyList<CalendarWeekViewModel> _weeks = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNotice), nameof(NoticeText))] private string? _noticeKey;
    [ObservableProperty] private bool _isGoogleConnected;

    public CalendarViewModel(IReadOnlyList<Project> projects, PageServices services)
    {
        _projects = projects; _services = services; L = services.Strings;
        _month = MonthStart(LocalToday);
        Rebuild();
        RefreshOnLanguageChange(L);
    }

    public static async Task<CalendarViewModel> LoadAsync(PageServices services) => new(await services.Projects.ListAsync(), services);

    public LocalizedStrings L { get; }

    /// <summary>The month heading, such as "October 2026" or "مهر 1405".</summary>
    public string Title => L.Dates.MonthYear(_month);
    public bool HasNotice => NoticeKey is not null;
    public string NoticeText => NoticeKey is null ? "" : L[NoticeKey];

    /// <summary>Day names in column order: Monday first, Saturday first in Persian.</summary>
    public IReadOnlyList<string> Weekdays => Enumerable.Range(0, 7).Select(offset => (DayOfWeek)(((int)FirstDay + offset) % 7))
        .Select(day => Jalali ? L.Culture.DateTimeFormat.GetDayName(day) : L.Culture.DateTimeFormat.GetAbbreviatedDayName(day)).ToList();

    /// <summary>Caret glyphs do not mirror on their own; they point along the reading direction.</summary>
    public string PreviousIcon => Jalali ? Icons.CaretRight : Icons.CaretLeft;
    public string NextIcon => Jalali ? Icons.CaretLeft : Icons.CaretRight;

    /// <summary>The fetch of the visible range from the external calendar that started last; completes immediately when there is none.</summary>
    public Task Refreshing { get; private set; } = Task.CompletedTask;

    private bool Jalali => L.LanguageCode == "fa";
    private DayOfWeek FirstDay => Jalali ? DayOfWeek.Saturday : DayOfWeek.Monday;
    private DateTime LocalToday => _services.Clock.GetLocalNow().Date;

    /// <summary>Starts the opt-in overlay once the page is shown. It only does anything when Google Calendar is connected.</summary>
    public Task StartAsync()
    {
        _started = true;
        return Refreshing = RefreshExternalAsync();
    }

    [RelayCommand]
    private void Previous() => Shift(-1);

    [RelayCommand]
    private void Next() => Shift(1);

    [RelayCommand]
    private void Today()
    {
        _month = MonthStart(LocalToday);
        MonthChanged();
    }

    private void Shift(int months)
    {
        _month = AddMonths(_month, months);
        MonthChanged();
    }

    private void MonthChanged()
    {
        OnPropertyChanged(nameof(Title));
        Rebuild();
        Refreshing = RefreshExternalAsync();
    }

    /// <summary>A language switch can change the calendar system, so the shown month is re-anchored, the grid redrawn and the range fetched again.</summary>
    protected override void OnLanguageChanged()
    {
        _month = MonthStart(_month.AddDays(14));
        Rebuild();
        if (_started) Refreshing = RefreshExternalAsync();
    }

    /// <summary>Fetches the visible range from the external calendar. Never throws: failures show a notice and leave project dates untouched.</summary>
    public async Task RefreshExternalAsync()
    {
        var ticket = ++_fetch;
        IReadOnlyList<ExternalCalendarEvent> events = [];
        string? notice = null;
        var connected = false;
        try
        {
            connected = await _services.Calendar.IsConnectedAsync();
            if (connected)
            {
                var (from, to) = GridRange(_month);
                events = await _services.Calendar.ListEventsAsync(from, to);
            }
        }
        catch (ExternalCalendarAuthorizationException) { connected = false; notice = "google.reauth"; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException or System.Text.Json.JsonException) { notice = "google.loadFailed"; }
        if (ticket != _fetch) return; // a newer month was requested meanwhile
        _external = events; NoticeKey = notice; IsGoogleConnected = connected;
        Rebuild();
        if (notice == "google.reauth") await _services.Host.RefreshShellAsync();
    }

    /// <summary>The span of whole weeks the grid shows for <paramref name="month"/>.</summary>
    private (DateTimeOffset From, DateTimeOffset To) GridRange(DateTime month)
    {
        var (start, weeks) = GridShape(month);
        var from = new DateTimeOffset(start, _services.Clock.LocalTimeZone.GetUtcOffset(start));
        return (from, from.AddDays(weeks * 7));
    }

    private (DateTime Start, int Weeks) GridShape(DateTime month)
    {
        var lead = ((int)month.DayOfWeek - (int)FirstDay + 7) % 7;
        var weeks = (int)Math.Ceiling((lead + (Jalali ? JalaliDate.DaysInMonth(month) : DateTime.DaysInMonth(month.Year, month.Month))) / 7.0);
        return (month.AddDays(-lead), weeks);
    }

    /// <summary>Timed events appear on their start day; all-day events on every day they cover (Google's end date is exclusive).</summary>
    private static bool OnDay(ExternalCalendarEvent item, DateTime date)
    {
        if (!item.IsAllDay) return item.Start.ToLocalTime().Date == date;
        var first = item.Start.DateTime.Date; var after = item.End.DateTime.Date;
        return date >= first && date < (after > first ? after : first.AddDays(1));
    }

    /// <summary>First day of the calendar month containing <paramref name="date"/>: a Jalali month in Persian, a Gregorian month otherwise.</summary>
    private DateTime MonthStart(DateTime date) => Jalali ? JalaliDate.MonthStart(date) : new DateTime(date.Year, date.Month, 1);

    private DateTime AddMonths(DateTime monthStart, int months) => Jalali ? JalaliDate.AddMonths(monthStart, months) : monthStart.AddMonths(months);

    private void Rebuild()
    {
        var month = _month;
        var (start, weeks) = GridShape(month);
        var startInstant = new DateTimeOffset(start, _services.Clock.LocalTimeZone.GetUtcOffset(start));
        var items = ProjectSummaries.Schedule(_projects, startInstant, startInstant.AddDays(weeks * 7), _services.Clock.GetLocalNow());
        var today = LocalToday;

        var built = new List<CalendarWeekViewModel>();
        for (var week = 0; week < weeks; week++)
        {
            var days = new List<CalendarDayViewModel>();
            for (var column = 0; column < 7; column++)
            {
                var date = start.AddDays(week * 7 + column);
                var inMonth = MonthStart(date) == month;
                var entries = inMonth ? items.Where(item => item.DueAt.ToLocalTime().Date == date).ToList() : [];
                var outside = inMonth ? _external.Where(item => OnDay(item, date)).ToList() : [];
                var shown = new List<ViewModelBase>(entries.Take(EventsPerDay).Select(entry => new CalendarEntryViewModel(entry, _services)));
                shown.AddRange(outside.Take(Math.Max(0, EventsPerDay - entries.Count)).Select(entry => new ExternalEntryViewModel(entry, L)));
                var number = (Jalali ? JalaliDate.FromGregorian(date).Day : date.Day).ToString(L.Culture);
                days.Add(new CalendarDayViewModel(number, date == today, inMonth, hasStartRule: column > 0, hasTopRule: week > 0, shown, entries.Count + outside.Count - EventsPerDay, L));
            }
            built.Add(new CalendarWeekViewModel(days));
        }
        Weeks = built;
    }
}

/// <summary>A row of seven days.</summary>
internal sealed class CalendarWeekViewModel(IReadOnlyList<CalendarDayViewModel> days) : ViewModelBase
{
    public IReadOnlyList<CalendarDayViewModel> Days { get; } = days;
}

/// <summary>One day cell: its number, up to three entries (tasks and milestones first, then external events) and how many more there are.</summary>
internal sealed class CalendarDayViewModel : ViewModelBase
{
    private readonly int _overflow;
    private readonly LocalizedStrings _strings;

    public CalendarDayViewModel(string number, bool isToday, bool inMonth, bool hasStartRule, bool hasTopRule, IReadOnlyList<ViewModelBase> items, int overflow, LocalizedStrings strings)
    {
        Number = number; IsToday = isToday; InMonth = inMonth; HasStartRule = hasStartRule; HasTopRule = hasTopRule; Items = items; _overflow = overflow; _strings = strings;
    }

    public string Number { get; }
    public bool IsToday { get; }
    public bool InMonth { get; }
    public bool IsOutside => !InMonth;
    public bool HasStartRule { get; }
    public bool HasTopRule { get; }
    /// <summary>The entries shown: <see cref="CalendarEntryViewModel"/> and <see cref="ExternalEntryViewModel"/>.</summary>
    public IReadOnlyList<ViewModelBase> Items { get; }
    public bool HasOverflow => _overflow > 0;
    public string OverflowText => HasOverflow ? _strings.Format("calendar.more", _overflow) : "";
}

/// <summary>A task or milestone on a day. Opens the project's tasks.</summary>
internal sealed partial class CalendarEntryViewModel : ViewModelBase
{
    private readonly ScheduleItem _item;
    private readonly PageServices _services;

    public CalendarEntryViewModel(ScheduleItem item, PageServices services)
    {
        _item = item; _services = services;
    }

    public string Text => $"{_item.Title} · {_item.ProjectName}";
    public bool IsOverdue => _item.IsOverdue;
    public bool IsMilestone => _item.Kind == ScheduleItemKind.Milestone;
    public string AccessibleName => $"{_item.Title} · {_item.ProjectName} · {_services.Strings.Due(_item.DueAt)}";

    [RelayCommand]
    private Task OpenAsync() => _services.GoAsync(new ProjectRoute(_item.ProjectId, ProjectTab.Tasks));
}

/// <summary>A Google Calendar event: display only, with no way to open or edit it.</summary>
internal sealed class ExternalEntryViewModel(ExternalCalendarEvent item, LocalizedStrings strings) : ViewModelBase
{
    public string Title => item.Title.Length == 0 ? strings["google.untitled"] : item.Title;
    public string AccessibleName => $"{Title} · {(item.IsAllDay ? strings["google.allDay"] : strings.Due(item.Start))} · {strings["calendar.legend.google"]}";
}
