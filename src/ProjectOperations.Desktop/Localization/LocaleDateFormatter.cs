using System.Globalization;

namespace ProjectOperations.Desktop.Localization;

public interface ILocaleDateFormatter
{
    string Display(DateTimeOffset? value);
    string Edit(DateTimeOffset? value);
}

public sealed class LocaleDateFormatter(ILocaleContext locale) : ILocaleDateFormatter
{
    public string Display(DateTimeOffset? value) => value?.ToLocalTime().ToString("g", locale.Culture) ?? "";
    public string Edit(DateTimeOffset? value) => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
}
