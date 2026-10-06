using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Infrastructure.Agents;
using ProjectOperations.Infrastructure.Persistence;

namespace ProjectOperations.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
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
            var configuration = options.IsConfigured && Uri.TryCreate(options.Url, UriKind.Absolute, out var endpoint)
                && endpoint.IsLoopback && endpoint.UserInfo.Length == 0
                ? $"Agent endpoint: {endpoint.Scheme}://{endpoint.Host}:{endpoint.Port}. Context is sent to this dedicated runtime and its configured model provider. Provider processing/retention is not local-only."
                : "Agent not configured. Set PROJECTOPS_OPENCODE_URL and PROJECTOPS_OPENCODE_CONFIG_DIR for a dedicated finance runtime as described in docs/agent-runtime.md. No model request will succeed until setup is complete.";
            desktop.MainWindow = new MainWindow(projects, agents, () => repository.InitializeAsync(),
                configuration);
            desktop.Exit += (_, _) => runtime.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
