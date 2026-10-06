namespace ProjectOperations.Infrastructure.Agents;

public sealed class OpenCodeRuntimeOptions
{
    public string? Url { get; init; }
    public string? Token { get; init; }
    public string? ConfigDirectory { get; init; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url)
        && !string.IsNullOrWhiteSpace(ConfigDirectory) && Path.IsPathFullyQualified(ConfigDirectory);

    public static OpenCodeRuntimeOptions FromEnvironment() => new()
    {
        Url = Environment.GetEnvironmentVariable("PROJECTOPS_OPENCODE_URL"),
        Token = Environment.GetEnvironmentVariable("PROJECTOPS_OPENCODE_TOKEN"),
        ConfigDirectory = Environment.GetEnvironmentVariable("PROJECTOPS_OPENCODE_CONFIG_DIR")
    };
}
