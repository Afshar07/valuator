using System.Globalization;
using System.Text.Json;
using Avalonia.Media;
using ProjectOperations.Core.Agents;

namespace ProjectOperations.Desktop.Localization;

public interface ILocaleContext
{
    string LanguageCode { get; }
    CultureInfo Culture { get; }
    FlowDirection FlowDirection { get; }
    AgentResponseLanguage AgentResponseLanguage { get; }
    event EventHandler? Changed;
    void SetLanguage(string languageCode);
}

public sealed class LocaleContext : ILocaleContext
{
    private readonly string? settingsPath;
    public string LanguageCode { get; private set; } = "en";
    public CultureInfo Culture { get; private set; } = CreateCulture("en");
    public FlowDirection FlowDirection => LanguageCode == "fa" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public AgentResponseLanguage AgentResponseLanguage => LanguageCode == "fa"
        ? AgentResponseLanguage.Persian : AgentResponseLanguage.English;
    public event EventHandler? Changed;

    public LocaleContext(string? settingsPath = null)
    {
        this.settingsPath = settingsPath;
        if (settingsPath is null) return;
        try
        {
            var settings = JsonSerializer.Deserialize<LocaleSettings>(File.ReadAllText(settingsPath));
            LanguageCode = Normalize(settings?.LanguageCode);
            Culture = CreateCulture(LanguageCode);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LanguageCode = "en";
        }
    }

    public void SetLanguage(string languageCode)
    {
        var normalized = Normalize(languageCode);
        if (normalized == LanguageCode) return;
        if (settingsPath is not null)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(settingsPath));
            Directory.CreateDirectory(directory!);
            var temporary = Path.Combine(directory!, $".locale-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(new LocaleSettings { LanguageCode = normalized }));
                File.Move(temporary, settingsPath, overwrite: true);
            }
            finally { File.Delete(temporary); }
        }
        LanguageCode = normalized;
        Culture = CreateCulture(normalized);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Normalize(string? code) => code?.Trim().ToLowerInvariant() == "fa" ? "fa" : "en";

    private static CultureInfo CreateCulture(string code)
    {
        var culture = new CultureInfo(code == "fa" ? "fa-IR" : "en-US");
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return CultureInfo.ReadOnly(culture);
    }

    private sealed class LocaleSettings
    {
        public string? LanguageCode { get; set; }
    }
}
