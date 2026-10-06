using System.Net;
using System.Text;
using System.Text.Json;
using ProjectOperations.Core.Agents;
using ProjectOperations.Infrastructure.Agents;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class OpenCodeRuntimeTests
{
    [Fact]
    public async Task V2RequestsStreamRealDeltasAndParseProposals()
    {
        using var handler = new FixtureHandler();
        using var runtime = Runtime(handler);
        var progress = new Events();
        var result = await runtime.RunAsync(new AgentRequest { Prompt = "Prepare brief", Context = "Opted-in context" },
            progress, CancellationToken.None);
        Assert.Equal(handler.Result, result.Text);
        Assert.Equal("Request forecast", Assert.Single(result.Proposals).Title);
        Assert.Equal(handler.Result, string.Concat(progress.Items.Where(x => x.Kind == AgentEventKind.ResultDelta).Select(x => x.Message)));
        Assert.Equal(new[] { "Reading project information", "Preparing response" },
            progress.Items.Where(x => x.Kind == AgentEventKind.Activity).Select(x => x.Message));
        Assert.DoesNotContain(progress.Items.Where(x => x.Kind == AgentEventKind.Detail), x => x.Message.Contains("Opted-in context"));
        using var create = JsonDocument.Parse(handler.CreateBody!);
        Assert.Equal(new[] { "id", "title", "agent", "permissions" }, create.RootElement.EnumerateObject().Select(x => x.Name));
        Assert.Equal("projectops", create.RootElement.GetProperty("agent").GetString());
        Assert.Equal("deny", create.RootElement.GetProperty("permissions")[0].GetProperty("effect").GetString());
        using var prompt = JsonDocument.Parse(handler.PromptBody!);
        Assert.Equal(new[] { "id", "text", "files", "agents", "skills", "resume" }, prompt.RootElement.EnumerateObject().Select(x => x.Name));
        Assert.Contains("Opted-in context", prompt.RootElement.GetProperty("text").GetString());
        Assert.Contains(AgentPrompts.OutputInstructions, prompt.RootElement.GetProperty("text").GetString());
        Assert.Empty(prompt.RootElement.GetProperty("files").EnumerateArray());
        Assert.Empty(prompt.RootElement.GetProperty("agents").EnumerateArray());
        Assert.Empty(prompt.RootElement.GetProperty("skills").EnumerateArray());
        Assert.True(prompt.RootElement.GetProperty("resume").GetBoolean());
        Assert.All(handler.Authorization, x => Assert.Equal("Bearer fixture-token", x));
        Assert.DoesNotContain(handler.Paths, x => x.StartsWith("/session", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("no-policy")]
    [InlineData("agent-allow")]
    [InlineData("plugins")]
    [InlineData("mcp")]
    [InlineData("directory")]
    [InlineData("extra-directory")]
    [InlineData("duplicate-directory")]
    [InlineData("no-directory")]
    [InlineData("loaded-plugin")]
    public async Task UnsafeRuntimeIsRejectedBeforeSendingProjectInformation(string unsafePolicy)
    {
        using var handler = new FixtureHandler { UnsafePolicy = unsafePolicy };
        using var runtime = Runtime(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest { Context = "Private" },
            new Events(), CancellationToken.None));
        Assert.Null(handler.CreateBody);
        Assert.Null(handler.PromptBody);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://remote.example/")]
    [InlineData("http://127.0.0.1:4096/path")]
    [InlineData("http://user:password@localhost:4096")]
    public async Task MissingOrNonLoopbackConfigurationMakesNoRequests(string? url)
    {
        using var handler = new FixtureHandler();
        using var runtime = new OpenCodeAgentRuntime(new OpenCodeRuntimeOptions { Url = url, ConfigDirectory = FixtureDirectory }, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest(), new Events(), CancellationToken.None));
        Assert.Empty(handler.Paths);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/config")]
    public async Task MissingOrRelativeTrustedDirectoryMakesNoRequests(string? directory)
    {
        using var handler = new FixtureHandler();
        using var runtime = new OpenCodeAgentRuntime(new OpenCodeRuntimeOptions
        {
            Url = "http://127.0.0.1:49123/",
            ConfigDirectory = directory
        }, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest { Context = "Private" },
            new Events(), CancellationToken.None));
        Assert.Empty(handler.Paths);
        Assert.Null(handler.PromptBody);
    }

    [Fact]
    public async Task ExactlyMatchingTrustedDirectoryAllowsDelegationAfterPathNormalization()
    {
        using var handler = new FixtureHandler { ConfigDirectory = Path.Combine(FixtureDirectory, ".") + Path.DirectorySeparatorChar };
        using var runtime = Runtime(handler);
        var result = await runtime.RunAsync(new AgentRequest(), new Events(), CancellationToken.None);
        Assert.Equal(handler.Result, result.Text);
        Assert.NotNull(handler.PromptBody);
    }

    [Fact]
    public async Task DifferentTrustedDirectoryIsRejectedBeforeAdmission()
    {
        using var handler = new FixtureHandler { ConfigDirectory = Path.Combine(FixtureDirectory, "unrelated") };
        using var runtime = Runtime(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest { Context = "Private" },
            new Events(), CancellationToken.None));
        Assert.Null(handler.CreateBody);
        Assert.Null(handler.PromptBody);
    }

    [Fact]
    public async Task LastExplicitDisabledShareWinsAndDocumentsWithoutSharePreserveIt()
    {
        using var handler = new FixtureHandler { Shares = ["auto", "disabled", null] };
        using var runtime = Runtime(handler);
        var result = await runtime.RunAsync(new AgentRequest(), new Events(), CancellationToken.None);
        Assert.Equal(handler.Result, result.Text);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("manual")]
    public async Task LastExplicitUnsafeShareRejectsDelegation(string sharing)
    {
        using var handler = new FixtureHandler { Shares = ["disabled", sharing, null] };
        using var runtime = Runtime(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest { Context = "Private" },
            new Events(), CancellationToken.None));
        Assert.Null(handler.CreateBody);
        Assert.Null(handler.PromptBody);
    }

    [Fact]
    public async Task EntirelyAbsentSharePolicyRejectsDelegation()
    {
        using var handler = new FixtureHandler { Shares = [null, null] };
        using var runtime = Runtime(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest(), new Events(), CancellationToken.None));
        Assert.Null(handler.PromptBody);
    }

    [Theory]
    [InlineData("1.0.0", false, "Unsupported")]
    [InlineData("2.0.24", true, "authentication")]
    public async Task VersionAndAuthenticationErrorsFailBeforeAdmission(string version, bool unauthorized, string expected)
    {
        using var handler = new FixtureHandler { Version = version, Unauthorized = unauthorized };
        using var runtime = Runtime(handler);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest(), new Events(), CancellationToken.None));
        Assert.Contains(expected, error.Message);
        Assert.Null(handler.CreateBody);
    }

    [Fact]
    public async Task StopCancelsInboxInterruptsAndWaitsForAcknowledgedIdle()
    {
        using var handler = new FixtureHandler { BlockStream = true, HoldStopWait = true };
        using var runtime = Runtime(handler);
        var running = runtime.RunAsync(new AgentRequest { JobId = "job" }, new Events(), CancellationToken.None);
        await handler.PromptAdmitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = runtime.CancelAsync("job", CancellationToken.None);
        await handler.StopWaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(stopping.IsCompleted);
        Assert.False(running.IsCompleted);
        handler.StopWaitAllowed.SetResult();
        await stopping;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Contains(handler.Paths, x => x.Contains("/inbox/msg_"));
        Assert.Contains(handler.Paths, x => x.EndsWith("/interrupt?resume=false"));
        Assert.Equal("/api/session/active", handler.Paths.Last());
        Assert.False(handler.StopTokenWasCancelled);
    }

    [Fact]
    public async Task TokenCancellationAlsoUsesIndependentInterruptTimeout()
    {
        using var handler = new FixtureHandler { BlockStream = true };
        using var runtime = Runtime(handler);
        using var cancellation = new CancellationTokenSource();
        var running = runtime.RunAsync(new AgentRequest(), new Events(), cancellation.Token);
        await handler.PromptAdmitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Contains(handler.Paths, x => x.EndsWith("/interrupt?resume=false"));
        Assert.False(handler.StopTokenWasCancelled);
    }

    [Fact]
    public async Task UnacknowledgedStopIsFailureNotCancellation()
    {
        using var handler = new FixtureHandler { BlockStream = true, FailInterrupt = true };
        using var runtime = Runtime(handler);
        var running = runtime.RunAsync(new AgentRequest { JobId = "job" }, new Events(), CancellationToken.None);
        await handler.PromptAdmitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.CancelAsync("job", CancellationToken.None));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => running);
        Assert.Contains("stop could not be confirmed", error.Message);
    }

    [Theory]
    [InlineData("session.execution.failed")]
    [InlineData("session.execution.interrupted")]
    [InlineData("disconnected")]
    [InlineData("mismatch")]
    public async Task FailedOrIncompleteStreamsNeverReturnSuccess(string ending)
    {
        using var handler = new FixtureHandler { Ending = ending };
        using var runtime = Runtime(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync(new AgentRequest(), new Events(), CancellationToken.None));
        Assert.Contains(handler.Paths, x => x.EndsWith("/interrupt?resume=false"));
    }

    private static string FixtureDirectory => Path.Combine(Path.GetTempPath(), "projectops-fixture", "config");

    private static OpenCodeAgentRuntime Runtime(FixtureHandler handler) => new(new OpenCodeRuntimeOptions
    {
        Url = "http://127.0.0.1:49123/",
        Token = "fixture-token",
        ConfigDirectory = FixtureDirectory
    }, handler);

    private sealed class Events : IProgress<AgentEvent>
    {
        public List<AgentEvent> Items { get; } = [];
        public void Report(AgentEvent value) => Items.Add(value);
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public string Version { get; init; } = "2.0.24";
        public bool Unauthorized { get; init; }
        public string? UnsafePolicy { get; init; }
        public string ConfigDirectory { get; init; } = FixtureDirectory;
        public string?[] Shares { get; init; } = ["disabled"];
        public bool BlockStream { get; init; }
        public bool HoldStopWait { get; init; }
        public bool FailInterrupt { get; init; }
        public string Ending { get; init; } = "session.execution.succeeded";
        public List<string> Paths { get; } = [];
        public List<string?> Authorization { get; } = [];
        public string? CreateBody { get; private set; }
        public string? PromptBody { get; private set; }
        public bool StopTokenWasCancelled { get; private set; }
        public TaskCompletionSource PromptAdmitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopWaitStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StopWaitAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Result { get; } = "Brief.\n```task-proposals\n{\"tasks\":[{\"title\":\"Request forecast\",\"dueAt\":null}]}\n```";
        private string session = "";
        private bool interrupted;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.PathAndQuery;
            Paths.Add(path);
            Authorization.Add(request.Headers.Authorization?.ToString());
            if (Unauthorized) return new(HttpStatusCode.Unauthorized);
            if (path == "/api/info") return Json(new { version = Version, pid = 1, urls = Array.Empty<string>(), paths = new { tmp = "isolated" } });
            if (path == "/api/config")
            {
                var settings = new Dictionary<string, object>
                {
                    ["experimental"] = new { policies = UnsafePolicy == "no-policy" ? Array.Empty<object>() : new object[] { new { action = "permission", resource = "*", effect = "deny" } } },
                    ["plugins"] = UnsafePolicy == "plugins" ? new[] { "unsafe" } : Array.Empty<string>(),
                    ["mcp"] = new { servers = UnsafePolicy == "mcp" ? new Dictionary<string, object> { ["unsafe"] = new { type = "local", command = new[] { "unsafe" } } } : new Dictionary<string, object>() }
                };
                if (Shares[0] is { } firstShare) settings["share"] = firstShare;
                var entries = new List<object>
                {
                    new { type = "document", path = Path.Combine(ConfigDirectory, "opencode.json"), info = settings }
                };
                if (UnsafePolicy != "no-directory")
                    entries.Add(new { type = "directory", path = UnsafePolicy == "directory" ? Path.Combine(FixtureDirectory, "unexpected") : ConfigDirectory });
                if (UnsafePolicy is "extra-directory" or "duplicate-directory")
                    entries.Add(new { type = "directory", path = UnsafePolicy == "duplicate-directory" ? ConfigDirectory : Path.Combine(FixtureDirectory, "other") });
                foreach (var sharing in Shares.Skip(1))
                {
                    var info = new Dictionary<string, object>();
                    if (sharing is not null) info["share"] = sharing;
                    entries.Add(new { type = "document", info });
                }
                return Json(entries);
            }
            if (path == "/api/agent/projectops") return Json(new
            {
                location = new { directory = "isolated" },
                data = new
                {
                    id = "projectops",
                    name = "projectops",
                    request = new { },
                    mode = "primary",
                    hidden = false,
                    permissions = new[] { new { action = "*", resource = "*", effect = UnsafePolicy == "agent-allow" ? "allow" : "deny" } }
                }
            });
            if (path == "/api/plugin") return Json(new { location = new { directory = "isolated" }, data = UnsafePolicy == "loaded-plugin" ? new object[] { new { id = "unsafe" } } : Array.Empty<object>() });
            if (path == "/api/mcp") return Json(new { location = new { directory = "isolated" }, data = Array.Empty<object>() });
            if (path == "/api/session" && request.Method == HttpMethod.Post)
            {
                CreateBody = await request.Content!.ReadAsStringAsync(token);
                using var body = JsonDocument.Parse(CreateBody);
                session = body.RootElement.GetProperty("id").GetString()!;
                return Json(new { data = new { id = session } });
            }
            if (path == "/api/event")
            {
                if (BlockStream)
                    return new(HttpStatusCode.OK) { Content = new StreamContent(new WaitingStream()) { Headers = { ContentType = new("text/event-stream") } } };
                string Event(string type, object data) => "event: " + type + "\r\ndata: " + JsonSerializer.Serialize(new { id = "evt", created = 1, type, data }) + "\r\n\r\n";
                var sse = ": heartbeat\r\n\r\n" + Event("server.connected", new { })
                    + Event("session.text.delta", new { sessionID = "ses_other", delta = "OTHER PRIVATE PROJECT" })
                    + Event("session.text.delta", new { sessionID = session, assistantMessageID = "msg_response", ordinal = 0, delta = Result[..6] })
                    + Event("session.text.delta", new { sessionID = session, assistantMessageID = "msg_response", ordinal = 0, delta = Result[6..] })
                    + Event("session.text.ended", new { sessionID = session, assistantMessageID = "msg_response", ordinal = 0, text = Ending == "mismatch" ? "Different" : Result });
                if (Ending != "disconnected") sse += Event(Ending == "mismatch" ? "session.execution.succeeded" : Ending, new { sessionID = session });
                return new(HttpStatusCode.OK) { Content = new StringContent(sse, Encoding.UTF8, "text/event-stream") };
            }
            if (path.EndsWith("/prompt", StringComparison.Ordinal))
            {
                PromptBody = await request.Content!.ReadAsStringAsync(token);
                using var body = JsonDocument.Parse(PromptBody);
                var id = body.RootElement.GetProperty("id").GetString();
                PromptAdmitted.SetResult();
                return Json(new { data = new { id, sessionID = session, time = new { created = 1 }, type = "user", payload = new { text = "redacted" }, delivery = "queue" } });
            }
            if (path.Contains("/inbox/", StringComparison.Ordinal)) return new(HttpStatusCode.NoContent);
            if (path.EndsWith("/interrupt?resume=false", StringComparison.Ordinal))
            {
                StopTokenWasCancelled = token.IsCancellationRequested;
                interrupted = true;
                return FailInterrupt ? new(HttpStatusCode.InternalServerError) : Json(new { interrupted = true });
            }
            if (path.EndsWith("/wait", StringComparison.Ordinal))
            {
                if (interrupted)
                {
                    StopWaitStarted.TrySetResult();
                    if (HoldStopWait) await StopWaitAllowed.Task.WaitAsync(token);
                }
                return new(HttpStatusCode.NoContent);
            }
            if (path == "/api/session/active") return Json(new { data = new { } });
            throw new InvalidOperationException("Unexpected HTTP fixture request: " + path);
        }

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }

    private sealed class WaitingStream : Stream
    {
        private readonly MemoryStream prefix = new(Encoding.UTF8.GetBytes("data: {\"type\":\"server.connected\",\"data\":{}}\n\n"));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (prefix.Position < prefix.Length)
                return await prefix.ReadAsync(buffer, cancellationToken);
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
