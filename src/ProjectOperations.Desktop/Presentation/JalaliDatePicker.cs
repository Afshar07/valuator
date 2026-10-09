using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>
/// Jalali date picker: a text box that accepts a typed Jalali or Gregorian date (converted automatically) and a month popup.
/// It exposes a Gregorian <see cref="SelectedDate"/>, so storage and the rest of the app are unaffected.
/// </summary>
internal sealed class JalaliDatePicker : Grid
{
    // Saturday-first single-letter weekday headings (ش ی د س چ پ ج).
    private static readonly string[] WeekdayLetters = ["ش", "ی", "د", "س", "چ", "پ", "ج"];

    private readonly PresentationContext _context;
    private readonly TextBox _text = Forms.Input("", 36);
    private readonly Button _open = new() { Name = "JalaliDateOpen" };
    private readonly Popup _popup = new() { IsLightDismissEnabled = true, Placement = PlacementMode.Bottom, Name = "JalaliDatePopup" };
    private readonly bool _allowEmpty;
    private DateTime? _selected;
    private DateTime _view;
    private bool _syncing;

    public event EventHandler? SelectedDateChanged;

    public JalaliDatePicker(PresentationContext context, bool allowEmpty)
    {
        _context = context; _allowEmpty = allowEmpty;
        Name = "DialogJalaliDate"; MinWidth = 190; HorizontalAlignment = HorizontalAlignment.Left;
        ColumnDefinitions = new ColumnDefinitions("*,Auto"); ColumnSpacing = 6; FlowDirection = FlowDirection.LeftToRight;

        _text.Name = "JalaliDateText"; _text.MinWidth = 130;
        context.Localized.Bind(_text, control => control.PlaceholderText = context.Text.Get("date.jalaliPlaceholder"));
        _text.TextChanged += (_, _) => OnTyped();
        _text.LostFocus += (_, _) => Commit();
        _text.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };

        _open.Classes.Add("square"); _open.Height = 36; _open.Width = 36;
        _open.Content = Icons.Glyph(Icons.CalendarBlank, 16, "TextPrimary");
        context.Localized.Bind(_open, control => { AutomationProperties.SetName(control, context.Text.Get("date.pick")); ToolTip.SetTip(control, context.Text.Get("date.pick")); });
        _open.Click += (_, _) => Toggle();
        SetColumn(_open, 1);

        _popup.PlacementTarget = this; _popup.Closed += (_, _) => _open.Focus();
        Children.Add(_text); Children.Add(_open); Children.Add(_popup);
    }

    /// <summary>The chosen day (Gregorian), or null when empty. Setting it updates the text without raising <see cref="SelectedDateChanged"/>.</summary>
    public DateTime? SelectedDate
    {
        get => _selected;
        set { _selected = value?.Date; ShowSelected(); }
    }

    private void ShowSelected()
    {
        // While the user types a date that already means the selected day, leave their text (and caret) alone.
        if (_text.IsFocused)
        {
            var meansSelected = string.IsNullOrWhiteSpace(_text.Text) ? _selected is null : JalaliDate.TryParse(_text.Text, out var typed) && typed == _selected;
            if (meansSelected) return;
        }
        _syncing = true; _text.Text = _selected is { } day ? JalaliDate.Format(day) : ""; _syncing = false;
    }

    private void OnTyped()
    {
        if (_syncing) return;
        if (string.IsNullOrWhiteSpace(_text.Text))
        {
            if (_allowEmpty && _selected is not null) Select(null);
            return;
        }
        if (JalaliDate.TryParse(_text.Text, out var date) && date != _selected) Select(date);
    }

    /// <summary>On blur or Enter: rewrite what was typed as the normalized Jalali date, or restore the last good value.</summary>
    private void Commit()
    {
        if (string.IsNullOrWhiteSpace(_text.Text) && _allowEmpty) { if (_selected is not null) Select(null); }
        else if (JalaliDate.TryParse(_text.Text, out var date) && date != _selected) Select(date);
        _syncing = true; _text.Text = _selected is { } day ? JalaliDate.Format(day) : ""; _syncing = false;
    }

    private void Select(DateTime? date)
    {
        _selected = date; SelectedDateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Toggle()
    {
        if (_popup.IsOpen) { _popup.IsOpen = false; return; }
        var anchor = _selected ?? DateTime.Today;
        _view = JalaliDate.IsSupported(anchor) ? JalaliDate.MonthStart(anchor) : JalaliDate.MonthStart(DateTime.Today);
        _popup.Child = BuildMonth(); _popup.IsOpen = true;
    }

    private Control BuildMonth()
    {
        var root = new StackPanel { Spacing = 8, Width = 266, FlowDirection = FlowDirection.RightToLeft };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var previous = Step(Icons.CaretRight, "calendar.previous", -1); // Right-to-left: "previous" sits at the leading (right) edge.
        var next = Step(Icons.CaretLeft, "calendar.next", 1); Grid.SetColumn(next, 2);
        var title = _context.Label(() => JalaliDate.MonthYear(_view), "BodyStrong");
        title.TextAlignment = TextAlignment.Center; title.VerticalAlignment = VerticalAlignment.Center; title.Name = "JalaliMonthTitle";
        Grid.SetColumn(title, 1);
        header.Children.Add(previous); header.Children.Add(title); header.Children.Add(next);
        root.Children.Add(header);

        var names = new UniformGrid { Columns = 7 };
        foreach (var letter in WeekdayLetters)
        {
            var label = _context.Label(() => letter, "Micro", "TextSecondary"); label.TextAlignment = TextAlignment.Center; label.HorizontalAlignment = HorizontalAlignment.Center;
            names.Children.Add(label);
        }
        root.Children.Add(names);

        var days = new UniformGrid { Columns = 7, Name = "JalaliDays" };
        var lead = ((int)_view.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7; // Saturday is the first column
        for (var blank = 0; blank < lead; blank++) days.Children.Add(new Border());
        var count = JalaliDate.DaysInMonth(_view);
        for (var offset = 0; offset < count; offset++) days.Children.Add(Day(_view.AddDays(offset), offset + 1));
        root.Children.Add(days);

        var card = new Border { Padding = new Thickness(12), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusCard), Child = root, Margin = new Thickness(0, 4, 0, 0) }
            .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault").RaisedShadowed();
        card.Name = "JalaliCalendar";
        return card;

        Button Step(string glyph, string key, int months)
        {
            var button = new Button { Content = Icons.Glyph(glyph, 15, "TextPrimary") }; button.Classes.Add("square");
            AutomationProperties.SetName(button, _context.Text.Get(key)); ToolTip.SetTip(button, _context.Text.Get(key));
            button.Click += (_, _) => { _view = JalaliDate.AddMonths(_view, months); _popup.Child = BuildMonth(); };
            return button;
        }
    }

    private Button Day(DateTime date, int number)
    {
        var selected = date == _selected; var today = date == DateTime.Today;
        var label = new TextBlock { Text = number.ToString(CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        PresentationTheme.Typeset(label, "Caption", selected ? "OnAccent" : today ? "Accent" : "TextPrimary");
        label.FontWeight = selected || today ? FontWeight.Bold : FontWeight.Normal;
        var button = new Button
        {
            Content = label,
            Width = 34,
            Height = 30,
            MinHeight = 0,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            BorderThickness = new Thickness(0),
            Name = "JalaliDay"
        };
        if (selected) button.Paint(Button.BackgroundProperty, "Accent"); else button.Background = Brushes.Transparent;
        AutomationProperties.SetName(button, $"{number} {JalaliDate.MonthNames[JalaliDate.FromGregorian(date).Month - 1]} {JalaliDate.FromGregorian(date).Year}");
        button.Click += (_, _) => { _popup.IsOpen = false; SelectedDate = date; Select(date); };
        return button;
    }
}
