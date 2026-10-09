namespace ProjectOperations.Infrastructure.Calendar;

/// <summary>
/// Google OAuth client for the optional calendar overlay. Create a "Desktop app" client in Google Cloud Console.
/// Google issues a client secret for desktop clients and its token endpoint expects it, but it does not protect
/// anything (PKCE does), so it is configuration like the client id. With no client id the feature is not offered.
/// </summary>
public sealed record GoogleCalendarOptions(string? ClientId, string? ClientSecret)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);

    public static GoogleCalendarOptions FromEnvironment() => new(
        Environment.GetEnvironmentVariable("PROJECTOPS_GOOGLE_CLIENT_ID"),
        Environment.GetEnvironmentVariable("PROJECTOPS_GOOGLE_CLIENT_SECRET"));
}
