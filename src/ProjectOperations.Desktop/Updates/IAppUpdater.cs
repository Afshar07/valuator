using System.Reflection;

namespace ProjectOperations.Desktop.Updates;

/// <summary>An update that is available to install. <see cref="ReleaseNotes"/> is the markdown published with the release.</summary>
public sealed record AppUpdate(string Version, string? ReleaseNotes, long SizeBytes);

/// <summary>
/// The application's self-update capability. The Settings screen only talks to this boundary; the Velopack-backed
/// implementation is created by the composition root and tests substitute a fake.
/// </summary>
public interface IAppUpdater
{
    /// <summary>False when this copy was not installed by the installer (portable run, development build), so it cannot update itself.</summary>
    bool CanUpdate { get; }
    string CurrentVersion { get; }
    /// <summary>A previously downloaded update awaiting explicit installation, including across app launches.</summary>
    AppUpdate? PendingUpdate => null;
    /// <summary>Returns the newest release when it is newer than the running one, otherwise null.</summary>
    Task<AppUpdate?> CheckAsync();
    /// <summary>Downloads the update; <paramref name="progress"/> receives whole percentages from 0 to 100.</summary>
    Task DownloadAsync(AppUpdate update, IProgress<int> progress, CancellationToken cancellation);
    /// <summary>Closes the application, installs the downloaded update and starts the new version.</summary>
    void RestartToInstall(AppUpdate update);
}

/// <summary>Used when no updater is supplied (tests, tools): nothing can be checked.</summary>
internal sealed class NoAppUpdater : IAppUpdater
{
    public bool CanUpdate => false;
    public string CurrentVersion => AppVersion.Current;
    public Task<AppUpdate?> CheckAsync() => Task.FromResult<AppUpdate?>(null);
    public Task DownloadAsync(AppUpdate update, IProgress<int> progress, CancellationToken cancellation) => Task.CompletedTask;
    public void RestartToInstall(AppUpdate update) { }
}

internal static class AppVersion
{
    /// <summary>The version stamped into the assembly at build time, without the source-revision suffix.</summary>
    public static string Current { get; } = Read();

    private static string Read()
    {
        var text = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(text)) return "0.0.0";
        var plus = text.IndexOf('+');
        return plus < 0 ? text : text[..plus];
    }
}
