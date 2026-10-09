using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Updates;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

public sealed class MvvmFoundationTests
{
    private static readonly DateTimeOffset October9 = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Localized_strings_follow_the_locale_and_notify_once_per_switch()
    {
        var locale = new LocaleContext();
        using var strings = new LocalizedStrings(locale, new LocalizationService(locale));
        var changes = new List<string?>();
        strings.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        var englishDates = strings.Dates;
        Assert.Equal("Valuator", strings["app.title"]);
        Assert.Equal("[missing.key]", strings["missing.key"]);

        locale.SetLanguage("fa");

        Assert.Equal("ارزیاب", strings["app.title"]);
        Assert.Equal(FlowDirection.RightToLeft, strings.FlowDirection);
        Assert.Equal("fa", strings.LanguageCode);
        Assert.NotSame(englishDates, strings.Dates);
        Assert.Equal([string.Empty], changes);
    }

    [Fact]
    public void View_models_follow_the_language_without_being_kept_alive_by_the_strings()
    {
        var locale = new LocaleContext();
        using var strings = new LocalizedStrings(locale, new LocalizationService(locale));
        var model = new ProbeViewModel(strings);
        var changes = 0;
        model.PropertyChanged += (_, e) => { if (e.PropertyName == string.Empty) changes++; };

        locale.SetLanguage("fa");
        Assert.Equal(1, changes);

        var weak = AbandonedModel(strings);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        locale.SetLanguage("en");
        Assert.Equal(2, changes);
        GC.KeepAlive(model);

        // A separate method, so no local of this test keeps the view-model reachable.
        static WeakReference<ProbeViewModel> AbandonedModel(LocalizedStrings strings) => new(new ProbeViewModel(strings));
    }

    [Fact]
    public void Date_converters_format_by_the_bound_language_and_leave_missing_dates_empty()
    {
        var english = new LocaleDateFormatter(new FixedLocaleContext("en"));
        var persian = new LocaleDateFormatter(new FixedLocaleContext("fa"));
        string Convert(Avalonia.Data.Converters.IMultiValueConverter converter, object? value, ILocaleDateFormatter dates) =>
            (string)converter.Convert([value, dates], typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
        var day = October9.ToLocalTime().DateTime;

        Assert.Equal(english.ShortDate(day), Convert(DateConverters.ShortDate, October9, english));
        Assert.Equal(persian.ShortDate(day), Convert(DateConverters.ShortDate, October9, persian));
        Assert.Contains("مهر", Convert(DateConverters.MonthYear, day, persian));
        Assert.Equal(english.Display(October9), Convert(DateConverters.Display, October9, english));
        Assert.Equal(english.Edit(October9), Convert(DateConverters.Edit, October9, persian));
        Assert.Equal("", Convert(DateConverters.Display, null, english));
        Assert.Equal("", (string)DateConverters.Display.Convert([October9], typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!);
    }

    [Fact]
    public void View_locator_pairs_view_models_with_views_by_name()
    {
        Assert.Equal(typeof(ProbeView), ViewLocator.ViewTypeFor(typeof(ProbeViewModel)));
        Assert.Null(ViewLocator.ViewTypeFor(typeof(OrphanViewModel)));
        Assert.Null(ViewLocator.ViewTypeFor(typeof(string)));
        Assert.False(new ViewLocator().Match("not a view-model"));
    }

    [AvaloniaFact]
    public void Hosted_view_binds_through_the_locator_and_updates_in_place_on_language_switch()
    {
        var locale = new LocaleContext();
        using var strings = new LocalizedStrings(locale, new LocalizationService(locale));
        var model = new ProbeViewModel(strings) { Due = October9 };
        var window = new Window { Content = new ContentControl { Content = model } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var view = Assert.IsType<ProbeView>(((ContentControl)window.Content!).Presenter!.Child);
        Assert.Same(model, view.DataContext);
        var title = view.FindControl<TextBlock>("Title")!;
        var due = view.FindControl<TextBlock>("Due")!;
        var name = view.FindControl<TextBox>("NameBox")!;
        Assert.Equal("Valuator", title.Text);
        Assert.Equal(strings.Dates.ShortDate(October9.ToLocalTime().DateTime), due.Text);
        name.Text = "Draft kept";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Draft kept", model.Name);

        locale.SetLanguage("fa");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("ارزیاب", title.Text);
        Assert.Contains("مهر", due.Text);
        Assert.Equal(FlowDirection.RightToLeft, view.FlowDirection);
        Assert.Equal("Draft kept", name.Text);
        Assert.Same(view, ((ContentControl)window.Content!).Presenter!.Child);
        window.Close();
    }

    [AvaloniaFact]
    public void Composition_root_resolves_every_service_without_touching_the_user_data()
    {
        var directory = Path.Combine(Path.GetTempPath(), "project-operations-services-" + Guid.NewGuid());
        try
        {
            using (var services = AppServices.Create(directory, services => services.AddSingleton<IAppUpdater, NoAppUpdater>()))
            {
                Assert.NotNull(services.GetRequiredService<AgentService>());
                Assert.Same(services.GetRequiredService<ProjectService>(), services.GetRequiredService<ProjectService>());
                Assert.IsType<NoExternalCalendar>(services.GetRequiredService<IExternalCalendarSource>());
                Assert.Same(services.GetRequiredService<LocaleContext>(), services.GetRequiredService<ILocaleContext>());
                Assert.Equal(Path.Combine(directory, "projects.db"), services.GetRequiredService<DesktopEnvironment>().DatabasePath);
                var window = services.GetRequiredService<MainWindow>();
                Assert.Same(window, services.GetRequiredService<MainWindow>());
            }
            Assert.False(File.Exists(Path.Combine(directory, "projects.db")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
