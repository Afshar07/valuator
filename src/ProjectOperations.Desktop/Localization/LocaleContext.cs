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

    internal static string Normalize(string? code) => code?.Trim().ToLowerInvariant() == "fa" ? "fa" : "en";

    internal static CultureInfo CreateCulture(string code)
    {
        var culture = new CultureInfo(code == "fa" ? "fa-IR" : "en-US");
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        return CultureInfo.ReadOnly(culture);
    }
}

/// <summary>A locale that never changes; formatters built on it give a stable result for one language.</summary>
internal sealed class FixedLocaleContext(string languageCode) : ILocaleContext
{
    public string LanguageCode { get; } = LocaleContext.Normalize(languageCode);
    public CultureInfo Culture { get; } = LocaleContext.CreateCulture(LocaleContext.Normalize(languageCode));
    public FlowDirection FlowDirection => LanguageCode == "fa" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public AgentResponseLanguage AgentResponseLanguage => LanguageCode == "fa" ? AgentResponseLanguage.Persian : AgentResponseLanguage.English;
    public event EventHandler? Changed { add { } remove { } }
    public void SetLanguage(string languageCode) => throw new NotSupportedException("A fixed locale cannot change language.");
}
