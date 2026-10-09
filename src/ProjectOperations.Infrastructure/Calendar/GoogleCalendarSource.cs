using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectOperations.Core.Calendar;

namespace ProjectOperations.Infrastructure.Calendar;

/// <summary>
/// Read-only Google Calendar overlay. Desktop OAuth with PKCE and a loopback redirect; only the
/// <c>calendar.readonly</c> scope is ever requested and only the primary calendar is read. Requests ask Google for
/// id, status, title and times only (no attendees, descriptions or links). Nothing is contacted until the user connects.
/// </summary>
public sealed class GoogleCalendarSource(HttpClient http, GoogleCalendarOptions options, ITokenStore tokens,
    Func<Uri, CancellationToken, Task> openBrowser, TimeProvider? time = null) : IExternalCalendarSource
{
    public const string Scope = "https://www.googleapis.com/auth/calendar.readonly";
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";
    private const string EventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";
    private const int MaxPages = 10;
    private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private string? _accessToken;
    private DateTimeOffset _accessExpires;

    public bool IsAvailable => options.IsConfigured;

    public async Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default) =>
        IsAvailable && await tokens.LoadAsync(cancellationToken) is not null;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("No Google OAuth client id is configured.");
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64Url(RandomNumberGenerator.GetBytes(16));

        var port = FreeLoopbackPort();
        var redirect = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(redirect);
        listener.Start();

        var authorization = new Uri(AuthorizationEndpoint + "?" + Query(
            ("client_id", options.ClientId!), ("redirect_uri", redirect), ("response_type", "code"), ("scope", Scope),
            ("state", state), ("code_challenge", challenge), ("code_challenge_method", "S256"),
            ("access_type", "offline"), ("prompt", "consent")));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SignInTimeout);
        await openBrowser(authorization, timeout.Token);
        var code = await WaitForCodeAsync(listener, state, timeout.Token);

        var response = await PostFormAsync(TokenEndpoint, TokenForm(("grant_type", "authorization_code"), ("code", code),
            ("code_verifier", verifier), ("redirect_uri", redirect)), cancellationToken);
        var granted = await ReadTokenAsync(response, cancellationToken);
        if (granted.RefreshToken is null) throw new ExternalCalendarAuthorizationException("Google did not return a refresh token.");
        await tokens.SaveAsync(granted.RefreshToken, cancellationToken);
        Remember(granted);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await tokens.LoadAsync(cancellationToken);
            if (token is not null && IsAvailable)
            {
                try { using var _ = await PostFormAsync(RevokeEndpoint, new Dictionary<string, string> { ["token"] = token }, cancellationToken); }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException) { /* local sign-out must not depend on the network */ }
            }
        }
        finally
        {
            _accessToken = null;
            await tokens.ClearAsync(CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (!await IsConnectedAsync(cancellationToken)) return [];
        var events = new List<ExternalCalendarEvent>();
        string? page = null;
        for (var index = 0; index < MaxPages; index++)
        {
            using var document = await GetEventsPageAsync(from, to, page, cancellationToken);
            var root = document.RootElement;
            if (root.TryGetProperty("items", out var items))
                foreach (var item in items.EnumerateArray())
                    if (Parse(item) is { } parsed) events.Add(parsed);
            page = root.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
            if (string.IsNullOrEmpty(page)) break;
        }
        return events.OrderBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
    }

    private async Task<JsonDocument> GetEventsPageAsync(DateTimeOffset from, DateTimeOffset to, string? page, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var query = new List<(string, string)>
            {
                ("timeMin", from.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)), ("timeMax", to.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)),
                ("singleEvents", "true"), ("orderBy", "startTime"), ("maxResults", "250"),
                ("fields", "nextPageToken,items(id,status,summary,start,end)")
            };
            if (page is not null) query.Add(("pageToken", page));
            using var request = new HttpRequestMessage(HttpMethod.Get, EventsEndpoint + "?" + Query(query.ToArray()));
            request.Headers.Authorization = new("Bearer", await AccessTokenAsync(cancellationToken));
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { _accessToken = null; continue; }
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new ExternalCalendarAuthorizationException("Google rejected the stored sign-in.");
            response.EnsureSuccessStatusCode();
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        }
    }

    private async Task<string> AccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && _accessExpires > _time.GetUtcNow().AddMinutes(1)) return _accessToken;
        var refresh = await tokens.LoadAsync(cancellationToken) ?? throw new ExternalCalendarAuthorizationException("Google Calendar is not connected.");
        using var response = await PostFormAsync(TokenEndpoint, TokenForm(("grant_type", "refresh_token"), ("refresh_token", refresh)), cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest && (await response.Content.ReadAsStringAsync(cancellationToken)).Contains("invalid_grant", StringComparison.Ordinal))
        {
            // Revoked in the Google account, expired (Testing-mode consent screens expire after 7 days) or password changed.
            await tokens.ClearAsync(CancellationToken.None);
            _accessToken = null;
            throw new ExternalCalendarAuthorizationException("Google no longer accepts the stored sign-in. Connect again.");
        }
        var granted = await ReadTokenAsync(response, cancellationToken);
        Remember(granted);
        return granted.AccessToken;
    }

    private void Remember(Grant grant)
    {
        _accessToken = grant.AccessToken;
        _accessExpires = _time.GetUtcNow().AddSeconds(grant.ExpiresInSeconds);
    }

    private static async Task<string> WaitForCodeAsync(HttpListener listener, string state, CancellationToken cancellationToken)
    {
        while (true)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            var query = context.Request.QueryString;
            var returned = query["state"]; var code = query["code"]; var error = query["error"];
            var relevant = returned is not null || code is not null || error is not null;
            await RespondAsync(context.Response, relevant, error is null && code is not null);
            if (!relevant) continue; // favicon or probes
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(returned ?? ""), Encoding.UTF8.GetBytes(state)))
                throw new ExternalCalendarAuthorizationException("The sign-in response did not match this request.");
            if (error is not null) throw new ExternalCalendarAuthorizationException($"Google sign-in was not completed ({error}).");
            return code ?? throw new ExternalCalendarAuthorizationException("Google sign-in returned no authorization code.");
        }
    }

    private static async Task RespondAsync(HttpListenerResponse response, bool relevant, bool success)
    {
        response.StatusCode = relevant ? 200 : 404;
        var body = Encoding.UTF8.GetBytes(!relevant ? "" : success
            ? "<!doctype html><meta charset=utf-8><title>Connected</title><p>Google Calendar is connected. You can close this tab and return to the app.</p>"
            : "<!doctype html><meta charset=utf-8><title>Not connected</title><p>Google Calendar was not connected. You can close this tab and return to the app.</p>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = body.Length;
        await response.OutputStream.WriteAsync(body);
        response.Close();
    }

    private Task<HttpResponseMessage> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken cancellationToken) =>
        http.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);

    private Dictionary<string, string> TokenForm(params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, string> { ["client_id"] = options.ClientId! };
        if (!string.IsNullOrWhiteSpace(options.ClientSecret)) form["client_secret"] = options.ClientSecret;
        foreach (var (key, value) in fields) form[key] = value;
        return form;
    }

    private sealed record Grant(string AccessToken, string? RefreshToken, int ExpiresInSeconds);

    private static async Task<Grant> ReadTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        if (string.IsNullOrEmpty(access)) throw new ExternalCalendarAuthorizationException("Google returned no access token.");
        return new(access, root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null, root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 300);
    }

    /// <summary>Cancelled events are skipped. All-day events use local midnight and keep Google's exclusive end date.</summary>
    private static ExternalCalendarEvent? Parse(JsonElement item)
    {
        if (item.TryGetProperty("status", out var status) && status.GetString() == "cancelled") return null;
        var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        if (id is null || !Boundary(item, "start", out var start, out var allDay) || !Boundary(item, "end", out var end, out _)) return null;
        return new(id, item.TryGetProperty("summary", out var title) ? title.GetString() ?? "" : "", start, end, allDay);
    }

    private static bool Boundary(JsonElement item, string name, out DateTimeOffset value, out bool allDay)
    {
        value = default; allDay = false;
        if (!item.TryGetProperty(name, out var boundary)) return false;
        if (boundary.TryGetProperty("dateTime", out var dateTime) && dateTime.GetString() is { } text)
            return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
        if (boundary.TryGetProperty("date", out var date) && DateTime.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            allDay = true; value = new DateTimeOffset(day, TimeZoneInfo.Local.GetUtcOffset(day)); return true;
        }
        return false;
    }

    private static string Query(params (string Key, string Value)[] fields) =>
        string.Join("&", fields.Select(field => $"{Uri.EscapeDataString(field.Key)}={Uri.EscapeDataString(field.Value)}"));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static int FreeLoopbackPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try { return ((IPEndPoint)probe.LocalEndpoint).Port; }
        finally { probe.Stop(); }
    }
}
