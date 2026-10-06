using System.Text.Json;

namespace ProjectOperations.Desktop.Localization;

public interface ILocalizationService
{
    string Get(string key);
    string Format(string key, params object?[] arguments);
}

public sealed class LocalizationService(ILocaleContext locale) : ILocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> English = Load("en");
    private static readonly IReadOnlyDictionary<string, string> Persian = Load("fa");

    public string Get(string key)
    {
        if (locale.LanguageCode == "fa" && Persian.TryGetValue(key, out var translated)) return translated;
        return English.TryGetValue(key, out var fallback) ? fallback : $"[{key}]";
    }

    public string Format(string key, params object?[] arguments) => string.Format(locale.Culture, Get(key), arguments);

    private static IReadOnlyDictionary<string, string> Load(string code)
    {
        var assembly = typeof(LocalizationService).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(name => name.Contains(".Localization.Resources.", StringComparison.Ordinal) &&
                                    name.EndsWith($".{code}.json", StringComparison.Ordinal))
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
            foreach (var entry in entries) result.Add(entry.Key, entry.Value);
        }
        return result;
    }
}
