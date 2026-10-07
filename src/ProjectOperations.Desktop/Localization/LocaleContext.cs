using System.Globalization;
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
        LanguageCode = Normalize(SettingsFile.Read(settingsPath, "LanguageCode"));
        Culture = CreateCulture(LanguageCode);
    }

    public void SetLanguage(string languageCode)
    {
        var normalized = Normalize(languageCode);
        if (normalized == LanguageCode) return;
        SettingsFile.Write(settingsPath, "LanguageCode", normalized);
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
}
