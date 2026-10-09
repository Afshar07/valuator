using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Infrastructure.Calendar;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class GoogleCalendarSourceTests
{
    private static readonly GoogleCalendarOptions Configured = new("client-id.apps.googleusercontent.com", "client-secret");

    private sealed class MemoryTokens(string? initial = null) : ITokenStore
    {
        public string? Token { get; private set; } = initial;
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Token);
        public Task SaveAsync(string token, CancellationToken cancellationToken = default) { Token = token; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Token = null; return Task.CompletedTask; }
    }

    private sealed record Seen(HttpMethod Method, Uri Uri, string Body, string? Authorization);

    /// <summary>Stands in for Google. Records every request; the responder decides the reply.</summary>
    private sealed class FakeGoogle(Func<Seen, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Seen> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var seen = new Seen(request.Method, request.RequestUri!, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.Authorization?.ToString());
            Requests.Add(seen);
            return respond(seen);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static GoogleCalendarSource Source(FakeGoogle google, ITokenStore tokens, GoogleCalendarOptions? options = null, Func<Uri, CancellationToken, Task>? browser = null) =>
        new(new HttpClient(google), options ?? Configured, tokens, browser ?? ((_, _) => Task.CompletedTask));

    private static readonly DateTimeOffset From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Default_source_is_off_and_empty()
    {
        var none = new NoExternalCalendar();
        Assert.False(none.IsAvailable);
        Assert.False(await none.IsConnectedAsync());
        Assert.Empty(await none.ListEventsAsync(From, To));
        await Assert.ThrowsAsync<InvalidOperationException>(() => none.ConnectAsync());
    }

    [Fact]
    public async Task Without_client_id_nothing_is_offered_and_no_request_is_made()
    {
        var google = new FakeGoogle(_ => throw new Xunit.Sdk.XunitException("no request expected"));
        var source = Source(google, new MemoryTokens("stale"), new GoogleCalendarOptions(null, null));
        Assert.False(source.IsAvailable);
        Assert.False(await source.IsConnectedAsync());
        Assert.Empty(await source.ListEventsAsync(From, To));
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ConnectAsync());
        Assert.Empty(google.Requests);
    }

    [Fact]
    public async Task Not_connected_returns_no_events_and_contacts_nobody()
    {
        var google = new FakeGoogle(_ => throw new Xunit.Sdk.XunitException("no request expected"));
        var source = Source(google, new MemoryTokens());
        Assert.True(source.IsAvailable);
        Assert.False(await source.IsConnectedAsync());
        Assert.Empty(await source.ListEventsAsync(From, To));
        Assert.Empty(google.Requests);
    }

    [Fact]
    public async Task Connect_uses_pkce_loopback_and_read_only_scope_then_stores_only_the_refresh_token()
    {
        var tokens = new MemoryTokens();
        string? challenge = null;
        var google = new FakeGoogle(seen =>
        {
            var form = HttpUtility.ParseQueryString(seen.Body);
            Assert.Equal("https://oauth2.googleapis.com/token", seen.Uri.ToString());
            Assert.Equal("authorization_code", form["grant_type"]);
            Assert.Equal("the-code", form["code"]);
            Assert.Equal("client-secret", form["client_secret"]);
            var hashed = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]!))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Assert.Equal(challenge, hashed);
            return Json("""{"access_token":"access-1","refresh_token":"refresh-1","expires_in":3600}""");
        });
        var source = Source(google, tokens, browser: (url, ct) =>
        {
            var query = HttpUtility.ParseQueryString(url.Query);
            Assert.Equal("accounts.google.com", url.Host);
            Assert.Equal("https://www.googleapis.com/auth/calendar.readonly", query["scope"]);
            Assert.Equal("S256", query["code_challenge_method"]);
            Assert.Equal(Configured.ClientId, query["client_id"]);
            Assert.StartsWith("http://127.0.0.1:", query["redirect_uri"]);
            challenge = query["code_challenge"];
            // The user's browser: follow the redirect back to the app's loopback listener.
            _ = Task.Run(async () => await new HttpClient().GetAsync($"{query["redirect_uri"]}?code=the-code&state={query["state"]}"));
            return Task.CompletedTask;
        });

        await source.ConnectAsync();

        Assert.Equal("refresh-1", tokens.Token);
        Assert.True(await source.IsConnectedAsync());
        Assert.Single(google.Requests);
    }

    [Fact]
    public async Task Connect_rejects_a_mismatched_state_and_stores_nothing()
    {
        var tokens = new MemoryTokens();
        var google = new FakeGoogle(_ => throw new Xunit.Sdk.XunitException("must not exchange a forged code"));
        var source = Source(google, tokens, browser: (url, ct) =>
        {
            var redirect = HttpUtility.ParseQueryString(url.Query)["redirect_uri"];
            _ = Task.Run(async () => await new HttpClient().GetAsync($"{redirect}?code=evil&state=forged"));
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<ExternalCalendarAuthorizationException>(() => source.ConnectAsync());
        Assert.Null(tokens.Token);
        Assert.Empty(google.Requests);
    }

    [Fact]
    public async Task Connect_when_user_denies_access_stores_nothing()
    {
        var tokens = new MemoryTokens();
        var source = Source(new FakeGoogle(_ => throw new Xunit.Sdk.XunitException("no exchange")), tokens, browser: (url, ct) =>
        {
            var query = HttpUtility.ParseQueryString(url.Query);
            _ = Task.Run(async () => await new HttpClient().GetAsync($"{query["redirect_uri"]}?error=access_denied&state={query["state"]}"));
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<ExternalCalendarAuthorizationException>(() => source.ConnectAsync());
        Assert.Null(tokens.Token);
    }

    [Fact]
    public async Task Cancelling_connect_abandons_it_without_storing_anything()
    {
        var tokens = new MemoryTokens();
        using var cancel = new CancellationTokenSource();
        var source = Source(new FakeGoogle(_ => throw new Xunit.Sdk.XunitException("no exchange")), tokens, browser: (_, _) => { cancel.CancelAfter(50); return Task.CompletedTask; });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ConnectAsync(cancel.Token));
        Assert.Null(tokens.Token);
    }

    [Fact]
    public async Task Lists_events_with_a_refreshed_access_token_and_a_minimal_field_set()
    {
        var google = new FakeGoogle(seen => seen.Uri.Host == "oauth2.googleapis.com"
            ? Json("""{"access_token":"access-2","expires_in":3600}""")
            : Json("""
                {"items":[
                  {"id":"b","status":"confirmed","summary":"Board call","start":{"dateTime":"2026-10-12T09:00:00+02:00"},"end":{"dateTime":"2026-10-12T10:00:00+02:00"}},
                  {"id":"x","status":"cancelled","summary":"Gone","start":{"dateTime":"2026-10-13T09:00:00Z"},"end":{"dateTime":"2026-10-13T10:00:00Z"}},
                  {"id":"a","summary":"Offsite","start":{"date":"2026-10-10"},"end":{"date":"2026-10-11"}},
                  {"id":"n","start":{"dateTime":"2026-10-14T09:00:00Z"},"end":{"dateTime":"2026-10-14T09:30:00Z"}}
                ]}
                """));
        var source = Source(google, new MemoryTokens("refresh-1"));

        var events = await source.ListEventsAsync(From, To);

        Assert.Equal(["a", "b", "n"], events.Select(item => item.Id));
        var allDay = events[0];
        Assert.True(allDay.IsAllDay);
        Assert.Equal(new DateTime(2026, 10, 10), allDay.Start.DateTime);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.FromHours(2)), events[1].Start);
        Assert.Equal("Board call", events[1].Title);
        Assert.Equal("", events[2].Title);

        var refresh = google.Requests[0];
        Assert.Equal("refresh_token", HttpUtility.ParseQueryString(refresh.Body)["grant_type"]);
        Assert.Equal("refresh-1", HttpUtility.ParseQueryString(refresh.Body)["refresh_token"]);
        var list = google.Requests[1];
        Assert.Equal("Bearer access-2", list.Authorization);
        var query = HttpUtility.ParseQueryString(list.Uri.Query);
        Assert.Equal("nextPageToken,items(id,status,summary,start,end)", query["fields"]);
        Assert.Equal("true", query["singleEvents"]);
        Assert.Equal("2026-10-01T00:00:00.0000000Z", query["timeMin"]);
        Assert.Contains("/calendars/primary/events", list.Uri.AbsolutePath);
    }

    [Fact]
    public async Task Follows_pages_and_reuses_the_access_token()
    {
        var pages = 0;
        var google = new FakeGoogle(seen =>
        {
            if (seen.Uri.Host == "oauth2.googleapis.com") return Json("""{"access_token":"access-3","expires_in":3600}""");
            pages++;
            return pages == 1
                ? Json("""{"nextPageToken":"p2","items":[{"id":"1","summary":"One","start":{"dateTime":"2026-10-02T09:00:00Z"},"end":{"dateTime":"2026-10-02T10:00:00Z"}}]}""")
                : Json("""{"items":[{"id":"2","summary":"Two","start":{"dateTime":"2026-10-03T09:00:00Z"},"end":{"dateTime":"2026-10-03T10:00:00Z"}}]}""");
        });
        var source = Source(google, new MemoryTokens("refresh-1"));

        Assert.Equal(["1", "2"], (await source.ListEventsAsync(From, To)).Select(item => item.Id));
        Assert.Equal("p2", HttpUtility.ParseQueryString(google.Requests.Last().Uri.Query)["pageToken"]);
        Assert.Single(google.Requests, request => request.Uri.Host == "oauth2.googleapis.com");
    }

    [Fact]
    public async Task Revoked_sign_in_is_forgotten_and_reported_so_the_user_can_reconnect()
    {
        var tokens = new MemoryTokens("revoked");
        var google = new FakeGoogle(_ => Json("""{"error":"invalid_grant","error_description":"Token has been expired or revoked."}""", HttpStatusCode.BadRequest));
        var source = Source(google, tokens);

        await Assert.ThrowsAsync<ExternalCalendarAuthorizationException>(() => source.ListEventsAsync(From, To));

        Assert.Null(tokens.Token);
        Assert.False(await source.IsConnectedAsync());
        Assert.Empty(await source.ListEventsAsync(From, To));
    }

    [Fact]
    public async Task Disconnect_revokes_and_always_clears_the_local_sign_in_even_offline()
    {
        var tokens = new MemoryTokens("refresh-1");
        var revoked = new FakeGoogle(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Source(revoked, tokens).DisconnectAsync();
        Assert.Null(tokens.Token);
        Assert.Equal("refresh-1", HttpUtility.ParseQueryString(revoked.Requests.Single().Body)["token"]);

        var offlineTokens = new MemoryTokens("refresh-2");
        var offline = new FakeGoogle(_ => throw new HttpRequestException("offline"));
        await Source(offline, offlineTokens).DisconnectAsync();
        Assert.Null(offlineTokens.Token);
    }
}
