namespace ProjectOperations.Desktop;

/// <summary>Runtime facts the shell displays (never edits): agent configuration and local storage location.</summary>
public sealed record DesktopEnvironment(bool AgentConfigured = true, string? Endpoint = null, string? ConfigDirectory = null, string? DatabasePath = null);
