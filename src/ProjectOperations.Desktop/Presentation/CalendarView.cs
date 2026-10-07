using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Internal month calendar of open tasks and milestones across live projects. No external calendar integration.</summary>
internal sealed class CalendarView : PresentationView
{
    private const int EventsPerDay = 3;
    private IReadOnlyList<Project> _all = [];
    private DateTime _month;
    private readonly ContentControl _grid = new() { Name = "CalendarGrid" };

    public CalendarView(PresentationContext context) : base(context)
    {
        var today = DateTime.Today; _month = new DateTime(today.Year, today.Month, 1);
    }

    public async Task LoadAsync()
    {
        _all = await _projects.ListAsync();
        var title = Label(() => _month.ToString("MMMM yyyy", _locale.Culture), "BodyStrong"); title.MinWidth = 130; title.TextAlignment = TextAlignment.Center; title.VerticalAlignment = VerticalAlignment.Center;
        var previous = Context.IconAction("calendar.previous", Icons.CaretLeft, () => Shift(-1, title), "square", iconOnly: true);
        var next = Context.IconAction("calendar.next", Icons.CaretRight, () => Shift(1, title), "square", iconOnly: true);
        // Caret glyphs do not mirror on their own; point them along the reading direction.
        Bind(previous, control => ((TextBlock)control.Content!).Text = _locale.LanguageCode == "fa" ? Icons.CaretRight : Icons.CaretLeft);
        Bind(next, control => ((TextBlock)control.Content!).Text = _locale.LanguageCode == "fa" ? Icons.CaretLeft : Icons.CaretRight);
        var pager = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        pager.Children.Add(previous); pager.Children.Add(title); pager.Children.Add(next);
        var today = Action("calendar.today", () => { var now = DateTime.Today; _month = new DateTime(now.Year, now.Month, 1); title.Text = _month.ToString("MMMM yyyy", _locale.Culture); Render(); return Task.CompletedTask; });
        today.MinHeight = 30;
        Children.Add(PageHeader("presentation.navigationCalendar", null, pager, today));

        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var (key, color, square) in new[] { ("calendar.legend.task", "TextTertiary", false), ("calendar.legend.milestone", "Accent", true), ("calendar.legend.overdue", "Error", false) })
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            item.Children.Add(Ui.Dot(8, color, square)); var label = Label(key, "Caption", "TextSecondary"); label.VerticalAlignment = VerticalAlignment.Center; item.Children.Add(label);
            legend.Children.Add(item);
        }
        Children.Add(legend);
        Children.Add(_grid);
        Bind(_grid, _ => Render());
    }

    private Task Shift(int months, TextBlock title)
    {
        _month = _month.AddMonths(months); title.Text = _month.ToString("MMMM yyyy", _locale.Culture); Render(); return Task.CompletedTask;
    }

    private void Render()
    {
        var firstDay = _locale.LanguageCode == "fa" ? DayOfWeek.Saturday : DayOfWeek.Monday;
        var lead = ((int)_month.DayOfWeek - (int)firstDay + 7) % 7;
        var start = _month.AddDays(-lead);
        var weeks = (int)Math.Ceiling((lead + DateTime.DaysInMonth(_month.Year, _month.Month)) / 7.0);
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
            var date = start.AddDays(index); var inMonth = date.Month == _month.Month; var isToday = date == today;
            var cell = new StackPanel { Spacing = 3 };
            var number = PresentationTheme.Typeset(new TextBlock { Text = date.Day.ToString(_locale.Culture), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                "Caption", isToday ? "OnAccent" : inMonth ? "TextPrimary" : "TextTertiary");
            number.FontWeight = isToday ? FontWeight.Bold : FontWeight.Medium;
            var badge = new Border { MinWidth = 22, Height = 22, Padding = new Thickness(5, 0), CornerRadius = new CornerRadius(11), HorizontalAlignment = HorizontalAlignment.Left, Child = number };
            if (isToday) badge.Paint(Border.BackgroundProperty, "Accent");
            cell.Children.Add(badge);
            var entries = inMonth ? items.Where(item => item.DueAt.ToLocalTime().Date == date).ToList() : [];
            foreach (var entry in entries.Take(EventsPerDay)) cell.Children.Add(Event(entry));
            if (entries.Count > EventsPerDay) cell.Children.Add(Label(() => F("calendar.more", entries.Count - EventsPerDay), "Micro", "TextTertiary"));
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
        button.Click += async (_, _) => await Context.ActAsync(button, () => OpenProjectAsync(item.ProjectId, 2));
        return button;
    }
}
