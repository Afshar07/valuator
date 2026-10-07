using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Infrastructure.Agents;
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
            desktop.MainWindow = new MainWindow(projects, agents, () => repository.InitializeAsync(),
                ConfigurationText(), locale, ConfigurationText, appearance, environment);
            desktop.Exit += (_, _) => runtime.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
