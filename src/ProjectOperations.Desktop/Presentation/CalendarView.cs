using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop;

/// <summary>
/// Month calendar of open tasks and milestones across live projects. When the user has opted in and connected Google Calendar,
/// their events are overlaid read-only (display only: never stored, never given to the assistant).
/// </summary>
internal sealed class CalendarView : PresentationView
{
    private const int EventsPerDay = 3;
    private IReadOnlyList<Project> _all = [];
    private DateTime _month;
    private readonly ContentControl _grid = new() { Name = "CalendarGrid" };
    private IReadOnlyList<ExternalCalendarEvent> _external = [];
    private string? _noticeKey;
    private readonly TextBlock _notice;
    private readonly StackPanel _googleLegend = new() { Orientation = Orientation.Horizontal, Spacing = 6, IsVisible = false, Name = "CalendarGoogleLegend" };
    private int _fetch;
    private bool _started;

    public CalendarView(PresentationContext context) : base(context)
    {
        _month = MonthStart(DateTime.Today);
        _notice = Label(() => _noticeKey is null ? "" : T(_noticeKey), "Caption", "TextSecondary"); _notice.Name = "CalendarNotice"; _notice.IsVisible = false;
    }

    public async Task LoadAsync()
    {
        _all = await _projects.ListAsync();
        var title = Label(() => Context.MonthYear(CurrentMonth()), "BodyStrong"); title.MinWidth = 130; title.TextAlignment = TextAlignment.Center; title.VerticalAlignment = VerticalAlignment.Center;
        var previous = Context.IconAction("calendar.previous", Icons.CaretLeft, () => Shift(-1, title), "square", iconOnly: true);
        var next = Context.IconAction("calendar.next", Icons.CaretRight, () => Shift(1, title), "square", iconOnly: true);
        // Caret glyphs do not mirror on their own; point them along the reading direction.
        Bind(previous, control => ((TextBlock)control.Content!).Text = _locale.LanguageCode == "fa" ? Icons.CaretRight : Icons.CaretLeft);
        Bind(next, control => ((TextBlock)control.Content!).Text = _locale.LanguageCode == "fa" ? Icons.CaretLeft : Icons.CaretRight);
        var pager = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        pager.Children.Add(previous); pager.Children.Add(title); pager.Children.Add(next);
        var today = Action("calendar.today", () => { _month = MonthStart(DateTime.Today); title.Text = Context.MonthYear(_month); Render(); _ = RefreshExternalAsync(); return Task.CompletedTask; });
        today.MinHeight = 30;
        Children.Add(PageHeader("presentation.navigationCalendar", null, pager, today));

        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var (key, color, square) in new[] { ("calendar.legend.task", "TextTertiary", false), ("calendar.legend.milestone", "Accent", true), ("calendar.legend.overdue", "Error", false) })
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            item.Children.Add(Ui.Dot(8, color, square)); var label = Label(key, "Caption", "TextSecondary"); label.VerticalAlignment = VerticalAlignment.Center; item.Children.Add(label);
            legend.Children.Add(item);
        }
        var google = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        google.Children.Add(Ui.Dot(8, "TextSecondary")); var googleLabel = Label("calendar.legend.google", "Caption", "TextSecondary"); googleLabel.VerticalAlignment = VerticalAlignment.Center; google.Children.Add(googleLabel);
        _googleLegend.Children.Add(google);
        legend.Children.Add(_googleLegend);
        Children.Add(legend);
        Children.Add(_notice);
        Children.Add(_grid);
        Bind(_grid, grid =>
        {
            Render();
            // A language switch can move the visible range (Jalali months), so fetch it again.
            if (_started) _ = RefreshExternalAsync();
        });
        _started = true;
        // Opt-in overlay: loads after the page is shown, and only does anything when Google Calendar is connected.
        _ = RefreshExternalAsync();
    }

    /// <summary>Fetches the visible range from the external calendar. Never throws: failures show a notice and leave project dates untouched.</summary>
    private async Task RefreshExternalAsync()
    {
        var ticket = ++_fetch;
        IReadOnlyList<ExternalCalendarEvent> events = [];
        string? notice = null;
        var connected = false;
        try
        {
            connected = await Context.Calendar.IsConnectedAsync();
            if (connected)
            {
                var (from, to) = GridRange(CurrentMonth());
                events = await Context.Calendar.ListEventsAsync(from, to);
            }
        }
        catch (ExternalCalendarAuthorizationException) { connected = false; notice = "google.reauth"; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException or System.Text.Json.JsonException) { notice = "google.loadFailed"; }
        if (ticket != _fetch) return; // a newer month was requested meanwhile
        _external = events; _noticeKey = notice; _googleLegend.IsVisible = connected;
        _notice.IsVisible = notice is not null; _notice.Text = notice is null ? "" : T(notice);
        Render();
        if (notice == "google.reauth") await Context.Shell.RefreshShellAsync();
    }

    /// <summary>The Monday/Saturday-aligned span of weeks the grid shows for <paramref name="month"/>.</summary>
    private (DateTimeOffset From, DateTimeOffset To) GridRange(DateTime month)
    {
        var (start, weeks) = GridShape(month);
        var from = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start));
        return (from, from.AddDays(weeks * 7));
    }

    private (DateTime Start, int Weeks) GridShape(DateTime month)
    {
        var firstDay = _locale.LanguageCode == "fa" ? DayOfWeek.Saturday : DayOfWeek.Monday;
        var lead = ((int)month.DayOfWeek - (int)firstDay + 7) % 7;
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

    private Task Shift(int months, TextBlock title)
    {
        _month = AddMonths(CurrentMonth(), months); title.Text = Context.MonthYear(_month); Render(); _ = RefreshExternalAsync(); return Task.CompletedTask;
    }

    private bool Jalali => _locale.LanguageCode == "fa";

    /// <summary>First day of the calendar month containing <paramref name="date"/>: a Jalali month in Persian, a Gregorian month otherwise.</summary>
    private DateTime MonthStart(DateTime date) => Jalali ? JalaliDate.MonthStart(date) : new DateTime(date.Year, date.Month, 1);

    private DateTime AddMonths(DateTime monthStart, int months) => Jalali ? JalaliDate.AddMonths(monthStart, months) : monthStart.AddMonths(months);

    /// <summary>Re-anchors the shown month to the active calendar system (the language can change while the page is open).</summary>
    private DateTime CurrentMonth() => _month = MonthStart(_month.AddDays(14));

    private void Render()
    {
        var month = CurrentMonth();
        var firstDay = _locale.LanguageCode == "fa" ? DayOfWeek.Saturday : DayOfWeek.Monday;
        var (start, weeks) = GridShape(month);
        var startInstant = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start));
        var items = ProjectSummaries.Schedule(_all, startInstant, startInstant.AddDays(weeks * 7), DateTimeOffset.Now);

        var card = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusCard), ClipToBounds = true }
            .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault").CardShadowed();
        var stack = new StackPanel();
        // Star columns (not UniformGrid) so long event titles ellipsize instead of widening every column.
        const string week = "*,*,*,*,*,*,*";
        var names = new Grid { ColumnDefinitions = new ColumnDefinitions(week) };
        for (var day = 0; day < 7; day++)
        {
            var dayOfWeek = (DayOfWeek)(((int)firstDay + day) % 7);
            var label = Label(() => _locale.LanguageCode == "fa" ? _locale.Culture.DateTimeFormat.GetDayName(dayOfWeek) : _locale.Culture.DateTimeFormat.GetAbbreviatedDayName(dayOfWeek), "MetaMedium", "TextSecondary");
            label.TextTrimming = TextTrimming.CharacterEllipsis; label.TextWrapping = TextWrapping.NoWrap;
            var header = new Border { Padding = new Thickness(10, 8), Child = label }; Grid.SetColumn(header, day); names.Children.Add(header);
        }
        stack.Children.Add(new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = names }.Paint(Border.BackgroundProperty, "BackgroundMuted").Paint(Border.BorderBrushProperty, "BorderDefault"));
        var days = new Grid { ColumnDefinitions = new ColumnDefinitions(week), RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", weeks))) };
        var today = DateTime.Today;
        for (var index = 0; index < weeks * 7; index++)
        {
            var date = start.AddDays(index); var inMonth = MonthStart(date) == month; var isToday = date == today;
            var cell = new StackPanel { Spacing = 3 };
            var number = PresentationTheme.Typeset(new TextBlock { Text = (Jalali ? JalaliDate.FromGregorian(date).Day : date.Day).ToString(_locale.Culture), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                "Caption", isToday ? "OnAccent" : inMonth ? "TextPrimary" : "TextTertiary");
            number.FontWeight = isToday ? FontWeight.Bold : FontWeight.Medium;
            var badge = new Border { MinWidth = 22, Height = 22, Padding = new Thickness(5, 0), CornerRadius = new CornerRadius(11), HorizontalAlignment = HorizontalAlignment.Left, Child = number };
            if (isToday) badge.Paint(Border.BackgroundProperty, "Accent");
            cell.Children.Add(badge);
            var entries = inMonth ? items.Where(item => item.DueAt.ToLocalTime().Date == date).ToList() : [];
            var outside = inMonth ? _external.Where(item => OnDay(item, date)).ToList() : [];
            foreach (var entry in entries.Take(EventsPerDay)) cell.Children.Add(Event(entry));
            foreach (var entry in outside.Take(Math.Max(0, EventsPerDay - entries.Count))) cell.Children.Add(ExternalEvent(entry));
            var overflow = entries.Count + outside.Count - EventsPerDay;
            if (overflow > 0) cell.Children.Add(Label(() => F("calendar.more", overflow), "Micro", "TextTertiary"));
            var border = new Border { MinHeight = 96, Padding = new Thickness(6, 6, 6, 8), BorderThickness = new Thickness(index % 7 == 0 ? 0 : 1, index < 7 ? 0 : 1, 0, 0), Child = cell, Name = isToday ? "CalendarToday" : null }
                .Paint(Border.BorderBrushProperty, "BorderSubtle").Paint(Border.BackgroundProperty, inMonth ? "BackgroundCard" : "BackgroundMuted");
            Grid.SetColumn(border, index % 7); Grid.SetRow(border, index / 7); days.Children.Add(border);
        }
        stack.Children.Add(days); card.Child = stack; _grid.Content = card;
    }

    private Button Event(ScheduleItem item)
    {
        var milestone = item.Kind == ScheduleItemKind.Milestone;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 5 };
        row.Children.Add(Ui.Dot(6, item.IsOverdue ? "Error" : milestone ? "Accent" : "TextTertiary", milestone));
        var text = Label(() => $"{item.Title} · {item.ProjectName}", "Micro"); text.FontWeight = FontWeight.Normal; text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var button = new Button
        {
            Content = row,
            Padding = new Thickness(5, 2),
            MinHeight = 0,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Name = "CalendarEvent"
        };
        button.Paint(Button.BackgroundProperty, item.IsOverdue ? "ErrorSoft" : milestone ? "AccentSoft" : "BackgroundTrack");
        Bind(button, control => { AutomationProperties.SetName(control, $"{item.Title} · {item.ProjectName} · {Due(item.DueAt)}"); ToolTip.SetTip(control, $"{item.Title} · {item.ProjectName} · {Due(item.DueAt)}"); });
        button.Click += async (_, _) => await Context.ActAsync(button, () => OpenProjectAsync(item.ProjectId, ProjectTab.Tasks));
        return button;
    }

    /// <summary>A Google event: display only (no click target), with a quiet bordered style distinct from tasks, milestones and overdue work.</summary>
    private Border ExternalEvent(ExternalCalendarEvent item)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 5 };
        row.Children.Add(Ui.Dot(6, "TextSecondary"));
        var text = Label(() => item.Title.Length == 0 ? T("google.untitled") : item.Title, "Micro"); text.FontWeight = FontWeight.Normal; text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var border = new Border { Child = row, Padding = new Thickness(5, 2), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Name = "CalendarGoogleEvent" }
            .Paint(Border.BackgroundProperty, "BackgroundMuted").Paint(Border.BorderBrushProperty, "BorderDefault");
        Bind(border, control =>
        {
            var name = $"{(item.Title.Length == 0 ? T("google.untitled") : item.Title)} · {(item.IsAllDay ? T("google.allDay") : Due(item.Start))} · {T("calendar.legend.google")}";
            AutomationProperties.SetName(control, name); ToolTip.SetTip(control, name);
        });
        return border;
    }
}
