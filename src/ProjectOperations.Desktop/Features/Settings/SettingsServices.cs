using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Desktop.Updates;

namespace ProjectOperations.Desktop.Features.Settings;

/// <summary>
/// What the Settings page and its sections take: the services every top-level page gets, plus the preferences, the runtime facts and the
/// updater that only Settings shows. They follow the sample workspace like the page services do.
/// </summary>
internal sealed record SettingsServices(PageServices Page, LocaleContext Locale, AppearanceContext Appearance, DesktopEnvironment Environment, UpdateController Updates)
{
    public ProjectService Projects => Page.Projects;
    public LocalizedStrings Strings => Page.Strings;
    public IPageHost Host => Page.Host;
    public IFileLauncher Files => Page.Files;
    public IExternalCalendarSource Calendar => Page.Calendar;
}
