using System.Globalization;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class JalaliDateTests
{
    [Theory]
    [InlineData(2026, 10, 9, 1405, 7, 17)]
    [InlineData(2026, 3, 21, 1405, 1, 1)]
    [InlineData(2025, 3, 20, 1403, 12, 30)] // leap-year Esfand 30
    [InlineData(2030, 3, 21, 1409, 1, 1)]
    public void Converts_both_directions(int gy, int gm, int gd, int jy, int jm, int jd)
    {
        Assert.Equal((jy, jm, jd), JalaliDate.FromGregorian(new DateTime(gy, gm, gd)));
        Assert.Equal(new DateTime(gy, gm, gd), JalaliDate.ToGregorian(jy, jm, jd));
        Assert.Equal($"{jy:0000}/{jm:00}/{jd:00}", JalaliDate.Format(new DateTime(gy, gm, gd)));
    }

    [Theory]
    [InlineData("1405/07/17")]
    [InlineData("1405-7-17")]
    [InlineData(" 1405.07.17 ")]
    [InlineData("۱۴۰۵/۰۷/۱۷")]  // Persian digits
    [InlineData("١٤٠٥/٠٧/١٧")]  // Arabic-Indic digits
    [InlineData("2026-10-09")]  // Gregorian is converted to the same day
    [InlineData("2026/10/9")]
    public void Jalali_and_Gregorian_input_name_the_same_day(string text)
    {
        Assert.True(JalaliDate.TryParse(text, out var date));
        Assert.Equal(new DateTime(2026, 10, 9), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("1405/13/01")]
    [InlineData("1405/07/32")]
    [InlineData("1404/12/30")]   // common year: Esfand has 29 days
    [InlineData("2030-02-30")]
    [InlineData("1405/07")]
    [InlineData("1405/07/17/1")]
    [InlineData("14055/07/17")]
    public void Rejects_impossible_or_malformed_dates(string text) => Assert.False(JalaliDate.TryParse(text, out _));

    [Fact]
    public void Month_navigation_crosses_years_and_keeps_month_lengths()
    {
        var esfand = JalaliDate.ToGregorian(1404, 12, 1);
        Assert.Equal(29, JalaliDate.DaysInMonth(esfand));
        Assert.Equal((1405, 1, 1), JalaliDate.FromGregorian(JalaliDate.AddMonths(esfand, 1)));
        Assert.Equal((1404, 11, 1), JalaliDate.FromGregorian(JalaliDate.AddMonths(esfand, -1)));
        Assert.Equal((1406, 1, 1), JalaliDate.FromGregorian(JalaliDate.AddMonths(esfand, 13)));
        Assert.Equal(31, JalaliDate.DaysInMonth(JalaliDate.ToGregorian(1405, 6, 1)));
        Assert.Equal(30, JalaliDate.DaysInMonth(JalaliDate.ToGregorian(1405, 7, 1)));
        Assert.Equal(new DateTime(2026, 9, 23), JalaliDate.MonthStart(new DateTime(2026, 10, 9)));
    }

    [Fact]
    public void Formatter_shows_Jalali_in_Persian_and_Gregorian_in_English_without_changing_edit_or_storage()
    {
        var locale = new LocaleContext();
        var formatter = new LocaleDateFormatter(locale);
        var instant = new DateTimeOffset(2026, 10, 9, 9, 30, 0, TimeSpan.Zero);
        var local = instant.ToLocalTime();

        Assert.Equal(local.ToString("g", locale.Culture), formatter.Display(instant));
        Assert.Equal(local.ToString("MMM d", locale.Culture), formatter.ShortDate(local.DateTime));
        Assert.Equal(local.ToString("MMMM yyyy", locale.Culture), formatter.MonthYear(local.DateTime));

        locale.SetLanguage("fa");
        Assert.Equal($"\u200E{JalaliDate.Format(local.DateTime)}\u200E \u200E{local.ToString("HH:mm", CultureInfo.InvariantCulture)}\u200E", formatter.Display(instant));
        Assert.Equal("\u200F" + JalaliDate.ShortDate(local.DateTime), formatter.ShortDate(local.DateTime));
        Assert.EndsWith(" " + JalaliDate.FromGregorian(local.DateTime).Year, formatter.MonthYear(local.DateTime));
        Assert.Equal(local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), formatter.Edit(instant));
        Assert.Equal("", formatter.Display(null));
    }

    [Theory]
    [InlineData("1405/07/17 09:30", 2026, 10, 9)]
    [InlineData("۱۴۰۵/۰۷/۱۷ ۰۹:۳۰", 2026, 10, 9)]
    [InlineData("2026-10-09 09:30", 2026, 10, 9)]
    public void Legacy_text_entry_converts_Jalali_automatically(string text, int year, int month, int day)
    {
        var parsed = MainWindow.ParseLocalDate(text)!.Value.ToLocalTime();
        Assert.Equal(new DateTime(year, month, day, 9, 30, 0), parsed.DateTime);
    }

    [AvaloniaFact]
    public async Task Persian_task_dialog_accepts_typed_Jalali_or_Gregorian_and_the_month_popup()
    {
        using var fixture = new MainWindowTests.Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Jalali project", "Company", ProjectStatus.Active, "", "");
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Existing task", DueAt = new DateTimeOffset(2030, 1, 15, 9, 30, 0, TimeSpan.Zero) });
        await fixture.Projects.SaveAsync(project);
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => Buttons(window).Any(button => MainWindowTests.ButtonText(button).StartsWith("Jalali project ·") && button.IsEffectivelyEnabled));
        UiWait.Click(window, button => MainWindowTests.ButtonText(button).StartsWith("Jalali project ·"), "Jalali project");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Buttons(window).First(button => MainWindowTests.ButtonText(button) == "All projects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        UiWait.Click(window, button => MainWindowTests.ButtonText(button).StartsWith("Existing task ·"), "Existing task");

        var gregorian = Controls<CalendarDatePicker>(window).Single();
        var jalali = Controls<Control>(window).Single(control => control.Name == "DialogJalaliDate");
        var text = Controls<TextBox>(window).Single(box => box.Name == "JalaliDateText");
        var converted = Controls<TextBlock>(window).Single(block => block.Name == "DateConverted");
        Assert.True(gregorian.IsVisible); Assert.False(jalali.IsVisible); Assert.Equal("", converted.Text);

        fixture.Locale.SetLanguage("fa");
        Assert.False(gregorian.IsVisible); Assert.True(jalali.IsVisible);
        Assert.Equal(JalaliDate.Format(new DateTime(2030, 1, 15)), text.Text);
        Assert.Equal("1408/10/26", text.Text); // 1408 is a leap year, so Dey 1 is Dec 21
        Assert.Equal("میلادی: \u200E2030-01-15\u200E", converted.Text);

        // Typing a Jalali date updates the Gregorian equivalent and the other picker immediately.
        text.Text = "۱۴۰۹/۰۱/۰۱";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new DateTime(2030, 3, 21), gregorian.SelectedDate);
        Assert.Equal("میلادی: \u200E2030-03-21\u200E", converted.Text);
        // Typing a Gregorian date converts it.
        text.Text = "2030-06-01";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new DateTime(2030, 6, 1), gregorian.SelectedDate);
        Assert.Equal("1409/03/11", JalaliDate.Format(new DateTime(2030, 6, 1)));
        Capture(window, "jalali-picker-fa-closed");

        // The month popup shows the Jalali month containing the selection and picks a day.
        UiWait.Click(Controls<Button>(window).Single(button => button.Name == "JalaliDateOpen"));
        Assert.Equal("خرداد 1409", Controls<TextBlock>(window).Single(block => block.Name == "JalaliMonthTitle").Text);
        Assert.Equal(31, Controls<Button>(window).Count(button => button.Name == "JalaliDay"));
        Capture(window, "jalali-picker-fa");
        UiWait.Click(Controls<Button>(window).Single(button => button.Name == "JalaliDay" && AutomationProperties.GetName(button) == "20 خرداد 1409"));
        Assert.Equal(JalaliDate.ToGregorian(1409, 3, 20), gregorian.SelectedDate);
        Assert.Equal("1409/03/20", text.Text);
        UiWait.Click(Controls<Button>(window).Single(button => button.Name == "JalaliDateOpen"));
        UiWait.Click(Controls<Button>(window).Single(button => AutomationProperties.GetName(button) == "ماه بعد"));
        Assert.Equal("تیر 1409", Controls<TextBlock>(window).Single(block => block.Name == "JalaliMonthTitle").Text);

        // Garbage never changes the chosen day.
        text.Text = "1409/13/45";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(JalaliDate.ToGregorian(1409, 3, 20), gregorian.SelectedDate);

        UiWait.Click(Controls<Button>(window).Single(button => button.Name == "SaveTaskButton"));
        await UntilAsync(() => Controls<Control>(window).All(control => control.Name != "TaskDialog"));
        var saved = (await fixture.Projects.ListAsync()).Single().Tasks.Single().DueAt!.Value.ToLocalTime();
        Assert.Equal(JalaliDate.ToGregorian(1409, 3, 20), saved.Date);
        Assert.Equal(9, saved.Hour);
        window.Close();
    }

    private static void Capture(Window window, string screen)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var directory = Environment.GetEnvironmentVariable("PROJECTOPS_UI_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        for (var tick = 0; tick < 3; tick++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{screen}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static IEnumerable<Button> Buttons(Window window) => Controls<Button>(window);
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "UI did not reach the expected state within 10 seconds.");
    }
}
