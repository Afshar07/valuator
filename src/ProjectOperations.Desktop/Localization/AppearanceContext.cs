namespace ProjectOperations.Desktop.Localization;

/// <summary>Theme preference (light, dark, or auto = follow the operating system), persisted beside the language in settings.json.</summary>
public sealed class AppearanceContext
{
    public static readonly IReadOnlyList<string> Themes = ["light", "dark", "auto"];
    private readonly string? settingsPath;
    public string Theme { get; private set; }
    public event EventHandler? Changed;

    public AppearanceContext(string? settingsPath = null)
    {
        this.settingsPath = settingsPath;
        Theme = Normalize(SettingsFile.Read(settingsPath, "Theme"));
    }

    public void SetTheme(string theme)
    {
        var normalized = Normalize(theme);
        if (normalized == Theme) return;
        SettingsFile.Write(settingsPath, "Theme", normalized);
        Theme = normalized;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Normalize(string? theme) => theme?.Trim().ToLowerInvariant() is "dark" or "auto" ? theme.Trim().ToLowerInvariant() : "light";
}
