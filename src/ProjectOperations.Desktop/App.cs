using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Updates;
using ProjectOperations.Infrastructure.Agents;
using ProjectOperations.Infrastructure.Calendar;
using ProjectOperations.Infrastructure.Persistence;

namespace ProjectOperations.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var directory = Environment.GetEnvironmentVariable("PROJECTOPS_DATA_DIR");
            if (string.IsNullOrWhiteSpace(directory))
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectOperations");
            var database = Path.Combine(directory, "projects.db");
            var repository = new SqliteProjectRepository(database);
            var projects = new ProjectService(repository);
            var options = OpenCodeRuntimeOptions.FromEnvironment();
            var runtime = new OpenCodeAgentRuntime(options);
            var agents = new AgentService(projects, runtime, new SqliteAgentJobRepository(database));
            var locale = new LocaleContext(Path.Combine(directory, "settings.json"));
            var appearance = new AppearanceContext(Path.Combine(directory, "settings.json"));
            var localization = new LocalizationService(locale);
            Uri.TryCreate(options.Url, UriKind.Absolute, out var endpoint);
            var endpointConfigured = options.IsConfigured && endpoint is not null
                && endpoint.IsLoopback && endpoint.UserInfo.Length == 0;
            string ConfigurationText() => endpointConfigured
                ? localization.Format("agent.configuration.endpoint", $"\u2066{endpoint!.Scheme}://{endpoint.Host}:{endpoint.Port}\u2069")
                : localization.Get("agent.configuration.missing");
            var environment = new DesktopEnvironment(endpointConfigured,
                endpointConfigured ? $"{endpoint!.Scheme}://{endpoint.Host}:{endpoint.Port}" : null,
                string.IsNullOrWhiteSpace(options.ConfigDirectory) ? null : options.ConfigDirectory, database);
            // Opt-in: without a Google client id (or secure token storage) this stays the do-nothing source and the app behaves as before.
            var google = GoogleCalendarOptions.FromEnvironment();
            var googleHttp = google.IsConfigured && DpapiTokenStore.IsSupported ? new HttpClient() : null;
            IExternalCalendarSource calendar = googleHttp is null ? new NoExternalCalendar()
                : new GoogleCalendarSource(googleHttp, google, new DpapiTokenStore(Path.Combine(directory, "google-calendar.token")), OpenBrowser);
            desktop.MainWindow = new MainWindow(projects, agents, () => repository.InitializeAsync(),
                ConfigurationText(), locale, ConfigurationText, appearance, environment, new VelopackAppUpdater(), calendar);
            desktop.Exit += (_, _) => { runtime.Dispose(); googleHttp?.Dispose(); };
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Opens the Google sign-in page in the user's default browser.</summary>
    private static Task OpenBrowser(Uri url, CancellationToken cancellationToken)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        return Task.CompletedTask;
    }
}
