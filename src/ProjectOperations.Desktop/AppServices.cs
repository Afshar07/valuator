using Microsoft.Extensions.DependencyInjection;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Updates;
using ProjectOperations.Infrastructure.Agents;
using ProjectOperations.Infrastructure.Calendar;
using ProjectOperations.Infrastructure.Persistence;

namespace ProjectOperations.Desktop;

/// <summary>
/// Composition root. Every concrete runtime, storage and calendar choice is made here; OpenCode-specific construction
/// stays out of views and view-models. Disposing the provider disposes the runtime and the Google HTTP client.
/// </summary>
internal static class AppServices
{
    /// <param name="overrides">Later registrations win; tests use this to swap platform-bound services such as the updater.</param>
    public static ServiceProvider Create(string directory, Action<IServiceCollection>? overrides = null)
    {
        var database = Path.Combine(directory, "projects.db");
        var settings = Path.Combine(directory, "settings.json");
        var services = new ServiceCollection();

        services.AddSingleton(_ => new SqliteProjectRepository(database));
        services.AddSingleton<IProjectRepository>(provider => provider.GetRequiredService<SqliteProjectRepository>());
        services.AddSingleton<ProjectService>();
        services.AddSingleton<IAgentJobRepository>(_ => new SqliteAgentJobRepository(database));
        services.AddSingleton(_ => OpenCodeRuntimeOptions.FromEnvironment());
        services.AddSingleton(provider => new OpenCodeAgentRuntime(provider.GetRequiredService<OpenCodeRuntimeOptions>()));
        services.AddSingleton<IAgentRuntime>(provider => provider.GetRequiredService<OpenCodeAgentRuntime>());
        services.AddSingleton<AgentService>();

        services.AddSingleton(_ => new LocaleContext(settings));
        services.AddSingleton<ILocaleContext>(provider => provider.GetRequiredService<LocaleContext>());
        services.AddSingleton(_ => new AppearanceContext(settings));
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<LocalizedStrings>();
        services.AddSingleton<ILocaleDateFormatter, LocaleDateFormatter>();

        services.AddSingleton(provider => AgentEndpoint.From(provider.GetRequiredService<OpenCodeRuntimeOptions>()));
        services.AddSingleton(provider =>
        {
            var endpoint = provider.GetRequiredService<AgentEndpoint>();
            var options = provider.GetRequiredService<OpenCodeRuntimeOptions>();
            return new DesktopEnvironment(endpoint.Address is not null, endpoint.Address,
                string.IsNullOrWhiteSpace(options.ConfigDirectory) ? null : options.ConfigDirectory, database);
        });
        services.AddSingleton<IAppUpdater, VelopackAppUpdater>();

        // Opt-in: without a Google client id (or secure token storage) this stays the do-nothing source and the app behaves as before.
        services.AddSingleton(_ => GoogleCalendarOptions.FromEnvironment());
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IExternalCalendarSource>(provider =>
        {
            var google = provider.GetRequiredService<GoogleCalendarOptions>();
            if (!google.IsConfigured || !DpapiTokenStore.IsSupported) return new NoExternalCalendar();
            return new GoogleCalendarSource(provider.GetRequiredService<HttpClient>(), google,
                new DpapiTokenStore(Path.Combine(directory, "google-calendar.token")), OpenBrowser);
        });

        services.AddSingleton(provider =>
        {
            var repository = provider.GetRequiredService<SqliteProjectRepository>();
            var endpoint = provider.GetRequiredService<AgentEndpoint>();
            var text = provider.GetRequiredService<ILocalizationService>();
            string ConfigurationText() => endpoint.Address is null
                ? text.Get("agent.configuration.missing")
                : text.Format("agent.configuration.endpoint", $"⁦{endpoint.Address}⁩");
            return new MainWindow(provider.GetRequiredService<ProjectService>(), provider.GetRequiredService<AgentService>(),
                () => repository.InitializeAsync(), ConfigurationText(), provider.GetRequiredService<LocaleContext>(), ConfigurationText,
                provider.GetRequiredService<AppearanceContext>(), provider.GetRequiredService<DesktopEnvironment>(),
                provider.GetRequiredService<IAppUpdater>(), provider.GetRequiredService<IExternalCalendarSource>());
        });

        overrides?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    /// <summary>Opens the Google sign-in page in the user's default browser.</summary>
    private static Task OpenBrowser(Uri url, CancellationToken cancellationToken)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>The agent runtime address shown to the user, or null unless it is configured as a credential-free loopback URL.</summary>
internal sealed record AgentEndpoint(string? Address)
{
    public static AgentEndpoint From(OpenCodeRuntimeOptions options)
    {
        Uri.TryCreate(options.Url, UriKind.Absolute, out var endpoint);
        return options.IsConfigured && endpoint is not null && endpoint.IsLoopback && endpoint.UserInfo.Length == 0
            ? new AgentEndpoint($"{endpoint.Scheme}://{endpoint.Host}:{endpoint.Port}") : new AgentEndpoint((string?)null);
    }
}
