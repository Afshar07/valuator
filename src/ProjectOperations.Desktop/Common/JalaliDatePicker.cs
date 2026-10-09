using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Jalali date picker: a text box that accepts a typed Jalali or Gregorian date (converted automatically) and a month popup.
/// It exposes a Gregorian, two-way bindable <see cref="SelectedDate"/>, so storage and the rest of the app are unaffected.
/// Its texts come from <see cref="Strings"/> and follow language switches while the picker is on screen.
/// </summary>
internal sealed class JalaliDatePicker : Grid
{
    // Saturday-first single-letter weekday headings (ش ی د س چ پ ج).
    private static readonly string[] WeekdayLetters = ["ش", "ی", "د", "س", "چ", "پ", "ج"];

    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<JalaliDatePicker, DateTime?>(nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> AllowEmptyProperty = AvaloniaProperty.Register<JalaliDatePicker, bool>(nameof(AllowEmpty));
    public static readonly StyledProperty<LocalizedStrings?> StringsProperty = AvaloniaProperty.Register<JalaliDatePicker, LocalizedStrings?>(nameof(Strings));

    private readonly TextBox _text = new() { TextWrapping = TextWrapping.NoWrap, MinHeight = 36, VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _open = new() { Name = "JalaliDateOpen" };
    private readonly Popup _popup = new() { IsLightDismissEnabled = true, Placement = PlacementMode.Bottom, Name = "JalaliDatePopup" };
    private LocalizedStrings? _subscribed;
    private DateTime _view;
    private bool _syncing;

    static JalaliDatePicker()
    {
        SelectedDateProperty.Changed.AddClassHandler<JalaliDatePicker>((picker, _) => picker.ShowSelected());
        StringsProperty.Changed.AddClassHandler<JalaliDatePicker>((picker, _) => picker.OnStringsReplaced());
    }

    public JalaliDatePicker()
    {
        MinWidth = 190; HorizontalAlignment = HorizontalAlignment.Left;
        ColumnDefinitions = new ColumnDefinitions("*,Auto"); ColumnSpacing = 6; FlowDirection = FlowDirection.LeftToRight;

        _text.Name = "JalaliDateText"; _text.MinWidth = 130;
        _text.TextChanged += (_, _) => OnTyped();
        _text.LostFocus += (_, _) => Commit();
        _text.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };

        _open.Classes.Add("square"); _open.Height = 36; _open.Width = 36;
        _open.Content = Icons.Glyph(Icons.CalendarBlank, 16, "TextPrimary");
        _open.Click += (_, _) => Toggle();
        SetColumn(_open, 1);

        _popup.PlacementTarget = this; _popup.Closed += (_, _) => _open.Focus();
        Children.Add(_text); Children.Add(_open); Children.Add(_popup);
    }

    /// <summary>The chosen day (Gregorian), or null when empty.</summary>
    public DateTime? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    /// <summary>Whether the day may be cleared (an empty text box then means no date).</summary>
    public bool AllowEmpty { get => GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
    /// <summary>Source of the picker's texts; they are re-read whenever the language switches.</summary>
    public LocalizedStrings? Strings { get => GetValue(StringsProperty); set => SetValue(StringsProperty, value); }

    private DateTime? Selected => SelectedDate?.Date;
    private string Text(string key) => Strings?[key] ?? key;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
        ApplyStrings(); // the language may have switched while the picker was detached
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
    }

    private void Subscribe()
    {
        Unsubscribe();
        if (Strings is not { } strings) return;
        _subscribed = strings; strings.PropertyChanged += OnStringsChanged;
    }

    private void Unsubscribe()
    {
        if (_subscribed is { } strings) strings.PropertyChanged -= OnStringsChanged;
        _subscribed = null;
    }

    private void OnStringsReplaced() { if (TopLevel.GetTopLevel(this) is not null) Subscribe(); ApplyStrings(); }
    private void OnStringsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ApplyStrings();

    private void ApplyStrings()
    {
        _text.PlaceholderText = Text("date.jalaliPlaceholder");
        AutomationProperties.SetName(_open, Text("date.pick")); ToolTip.SetTip(_open, Text("date.pick"));
    }

    private void ShowSelected()
    {
        // While the user types a date that already means the selected day, leave their text (and caret) alone.
        if (_text.IsFocused)
        {
            var meansSelected = string.IsNullOrWhiteSpace(_text.Text) ? Selected is null : JalaliDate.TryParse(_text.Text, out var typed) && typed == Selected;
            if (meansSelected) return;
        }
        _syncing = true; _text.Text = Selected is { } day ? JalaliDate.Format(day) : ""; _syncing = false;
    }

    private void OnTyped()
    {
        if (_syncing) return;
        if (string.IsNullOrWhiteSpace(_text.Text))
        {
            if (AllowEmpty && Selected is not null) Select(null);
            return;
        }
        if (JalaliDate.TryParse(_text.Text, out var date) && date != Selected) Select(date);
    }

    /// <summary>On blur or Enter: rewrite what was typed as the normalized Jalali date, or restore the last good value.</summary>
    private void Commit()
    {
        if (string.IsNullOrWhiteSpace(_text.Text) && AllowEmpty) { if (Selected is not null) Select(null); }
        else if (JalaliDate.TryParse(_text.Text, out var date) && date != Selected) Select(date);
        _syncing = true; _text.Text = Selected is { } day ? JalaliDate.Format(day) : ""; _syncing = false;
    }

    /// <summary>A day the user chose: written with <c>SetCurrentValue</c> so a two-way binding or style still owns the property.</summary>
    private void Select(DateTime? date) => SetCurrentValue(SelectedDateProperty, date?.Date);

    private void Toggle()
    {
        if (_popup.IsOpen) { _popup.IsOpen = false; return; }
        var anchor = Selected ?? DateTime.Today;
        _view = JalaliDate.IsSupported(anchor) ? JalaliDate.MonthStart(anchor) : JalaliDate.MonthStart(DateTime.Today);
        _popup.Child = BuildMonth(); _popup.IsOpen = true;
    }

    private Control BuildMonth()
    {
        var root = new StackPanel { Spacing = 8, Width = 266, FlowDirection = FlowDirection.RightToLeft };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var previous = Step(Icons.CaretRight, "calendar.previous", -1); // Right-to-left: "previous" sits at the leading (right) edge.
        var next = Step(Icons.CaretLeft, "calendar.next", 1); Grid.SetColumn(next, 2);
        var title = PresentationTheme.Typeset(new TextBlock { Text = JalaliDate.MonthYear(_view) }, "BodyStrong");
        title.TextAlignment = TextAlignment.Center; title.VerticalAlignment = VerticalAlignment.Center; title.Name = "JalaliMonthTitle";
        Grid.SetColumn(title, 1);
        header.Children.Add(previous); header.Children.Add(title); header.Children.Add(next);
        root.Children.Add(header);

        var names = new UniformGrid { Columns = 7 };
        foreach (var letter in WeekdayLetters)
        {
            var label = PresentationTheme.Typeset(new TextBlock { Text = letter }, "Micro", "TextSecondary"); label.TextAlignment = TextAlignment.Center; label.HorizontalAlignment = HorizontalAlignment.Center;
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
            AutomationProperties.SetName(button, Text(key)); ToolTip.SetTip(button, Text(key));
            button.Click += (_, _) => { _view = JalaliDate.AddMonths(_view, months); _popup.Child = BuildMonth(); };
            return button;
        }
    }

    private Button Day(DateTime date, int number)
    {
        var selected = date == Selected; var today = date == DateTime.Today;
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
        button.Click += (_, _) => { _popup.IsOpen = false; Select(date); };
        return button;
    }
}
