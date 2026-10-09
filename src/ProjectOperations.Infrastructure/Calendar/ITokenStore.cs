using System.Security.Cryptography;
using System.Text;

namespace ProjectOperations.Infrastructure.Calendar;

/// <summary>Holds one long-lived credential outside SQLite and settings.json.</summary>
public interface ITokenStore
{
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string token, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Windows DPAPI (current-user scope): the file is unreadable by other accounts and other machines.
/// Only meaningful on Windows, where the app ships; <see cref="IsSupported"/> is false elsewhere so the feature stays off
/// instead of falling back to plain text.
/// </summary>
public sealed class DpapiTokenStore(string path) : ITokenStore
{
    public static bool IsSupported => OperatingSystem.IsWindows();

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!File.Exists(path)) return null;
        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException) { return null; }
    }

    public async Task SaveAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Secure token storage is only available on Windows.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        File.Delete(path);
        return Task.CompletedTask;
    }
}
