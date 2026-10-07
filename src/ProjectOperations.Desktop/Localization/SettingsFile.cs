using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjectOperations.Desktop.Localization;

/// <summary>Desktop preferences in <c>settings.json</c>. Each preference updates only its own key and writes atomically.</summary>
internal static class SettingsFile
{
    public static string? Read(string? path, string key)
    {
        if (path is null) return null;
        try { return JsonNode.Parse(File.ReadAllText(path)) is JsonObject root && root[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    public static void Write(string? path, string key, string value)
    {
        if (path is null) return;
        JsonObject root;
        try { root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? []; }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or JsonException) { root = []; }
        root[key] = value;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, root.ToJsonString());
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }
}
