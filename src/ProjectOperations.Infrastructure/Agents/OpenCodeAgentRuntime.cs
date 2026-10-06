using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ProjectOperations.Core.Agents;

namespace ProjectOperations.Infrastructure.Agents;

public sealed class OpenCodeAgentRuntime : IAgentRuntime, IDisposable
{
    private const string Agent = "projectops";
    private readonly HttpClient client;
    private readonly OpenCodeRuntimeOptions options;
    private readonly ConcurrentDictionary<string, RunningJob> jobs = new();

    public OpenCodeAgentRuntime(OpenCodeRuntimeOptions options, HttpMessageHandler? handler = null)
    {
        this.options = options;
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<AgentResult> RunAsync(AgentRequest request, IProgress<AgentEvent> progress,
        CancellationToken cancellationToken)
    {
        ValidateEndpoint();
        using var job = new RunningJob(cancellationToken);
        if (!jobs.TryAdd(request.JobId, job))
            throw new InvalidOperationException("This job is already running.");
        Exception? failure = null;
        var created = false;
        try
        {
            await CheckPolicyAsync(job.Stop.Token);
            job.Stop.Token.ThrowIfCancellationRequested();
            /** Admission is bounded independently so Stop cannot lose a successful server admission. */
            using (var admission = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                using var session = await JsonAsync(HttpMethod.Post, "api/session", new
                {
                    id = job.SessionId,
                    title = "Project analysis",
                    agent = Agent,
                    permissions = new[] { new { action = "*", resource = "*", effect = "deny" } }
                }, admission.Token);
                created = true;
                if (session.RootElement.GetProperty("data").GetProperty("id").GetString() != job.SessionId)
                    throw new InvalidOperationException("OpenCode returned an unexpected session identifier.");
            }
            job.Stop.Token.ThrowIfCancellationRequested();
            using var events = await SendAsync(HttpMethod.Get, "api/event", null, job.Stop.Token);
            if (events.Content.Headers.ContentType?.MediaType != "text/event-stream")
                throw new InvalidOperationException("OpenCode V2 event streaming is unavailable.");
            using var stream = await events.Content.ReadAsStreamAsync(job.Stop.Token);
            using var reader = new StreamReader(stream);
            using (var connectedTimeout = CancellationTokenSource.CreateLinkedTokenSource(job.Stop.Token))
            {
                connectedTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var connected = await ReadEventAsync(reader, connectedTimeout.Token);
                if (connected.RootElement.GetProperty("type").GetString() != "server.connected")
                    throw new InvalidOperationException("OpenCode did not confirm a live event subscription.");
            }
            Report(progress, AgentEventKind.Detail, "OpenCode V2 session admitted; tools denied.");
            if (!string.IsNullOrWhiteSpace(request.Context))
                Report(progress, AgentEventKind.Activity, "Reading project information", "agent.activity.readingProjectInformation");
            job.Stop.Token.ThrowIfCancellationRequested();
            using (var admission = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                try
                {
                    using var prompt = await JsonAsync(HttpMethod.Post, $"api/session/{job.SessionId}/prompt", new
                    {
                        id = job.PromptId,
                        text = request.SystemInstructions + "\n\nProject context:\n" + request.Context
                            + "\n\nUser request:\n" + request.Prompt,
                        files = Array.Empty<object>(),
                        agents = Array.Empty<object>(),
                        skills = Array.Empty<object>(),
                        resume = true
                    }, admission.Token);
                    if (prompt.RootElement.GetProperty("data").GetProperty("id").GetString() != job.PromptId)
                        throw new InvalidOperationException("OpenCode returned an unexpected input identifier.");
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException("OpenCode input admission timed out; outcome is unknown, not a confirmed cancellation.");
                }
            }
            Report(progress, AgentEventKind.Activity, "Preparing response", "agent.activity.preparingResponse");
            var text = await ReadEventsAsync(reader, job.SessionId, progress, job.Stop.Token);
            await WaitAsync(job.SessionId, job.Stop.Token);
            job.Stop.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("OpenCode completed without a text result.");
            return new AgentResult { Text = text, Proposals = TaskProposalParser.Parse(text) };
        }
        catch (Exception error)
        {
            failure = error;
            if (created)
            {
                try
                {
                    await StopSessionAsync(job);
                }
                catch (Exception)
                {
                    failure = new InvalidOperationException("OpenCode stop could not be confirmed. The runtime may still be working; check the dedicated server.");
                    throw failure;
                }
            }
            if (error is OperationCanceledException && !job.Stop.IsCancellationRequested)
            {
                failure = new InvalidOperationException("OpenCode did not acknowledge the request before its timeout; outcome is unknown.");
                throw failure;
            }
            throw;
        }
        finally
        {
            jobs.TryRemove(request.JobId, out _);
            job.Completion.TrySetResult(failure is OperationCanceledException ? null : failure);
        }
    }

    public async Task CancelAsync(string jobId, CancellationToken cancellationToken)
    {
        if (!jobs.TryGetValue(jobId, out var job))
            return;
        job.RequestStop();
        var error = await job.Completion.Task.WaitAsync(cancellationToken);
        if (error is not null)
            throw new InvalidOperationException("OpenCode job did not stop cleanly.", error);
    }

    private void ValidateEndpoint()
    {
        if (!options.IsConfigured)
            throw new InvalidOperationException("Configure PROJECTOPS_OPENCODE_URL and a fully qualified PROJECTOPS_OPENCODE_CONFIG_DIR for a dedicated, isolated OpenCode V2 finance runtime first.");
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !uri.IsLoopback
            || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.UserInfo.Length != 0)
            throw new InvalidOperationException("The OpenCode runtime URL must be a loopback HTTP(S) server root.");
        if (NormalizeDirectory(options.ConfigDirectory) is null)
            throw new InvalidOperationException("PROJECTOPS_OPENCODE_CONFIG_DIR must be a valid fully qualified directory path.");
    }

    private async Task CheckPolicyAsync(CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var info = await JsonAsync(HttpMethod.Get, "api/info", null, deadline.Token);
        if (!(info.RootElement.GetProperty("version").GetString() ?? "").TrimStart('v').StartsWith("2.0.", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsupported OpenCode version. This adapter requires the V2 2.0 API.");
        using var config = await JsonAsync(HttpMethod.Get, "api/config", null, deadline.Token);
        var hardDeny = false;
        var directories = 0;
        string? sharing = null;
        var trustedDirectory = NormalizeDirectory(options.ConfigDirectory);
        foreach (var entry in config.RootElement.EnumerateArray())
        {
            if (entry.GetProperty("type").GetString() == "directory")
            {
                var directory = NormalizeDirectory(entry.GetProperty("path").GetString());
                if (++directories != 1 || directory is null || !string.Equals(directory, trustedDirectory,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw PolicyError();
                continue;
            }
            if (entry.GetProperty("type").GetString() != "document")
                throw PolicyError();
            var settings = entry.GetProperty("info");
            foreach (var name in new[] { "plugins", "skills", "instructions", "references", "commands", "enterprise" })
                if (settings.TryGetProperty(name, out var value) && ChildCount(value) != 0)
                    throw PolicyError();
            if (settings.TryGetProperty("mcp", out var mcp) && mcp.TryGetProperty("servers", out var servers)
                && servers.EnumerateObject().Any())
                throw PolicyError();
            if (settings.TryGetProperty("warming", out var warming) && warming.ValueKind != JsonValueKind.False)
                throw PolicyError();
            if (settings.TryGetProperty("share", out var share))
                sharing = share.GetString();
            if (settings.TryGetProperty("experimental", out var experimental)
                && experimental.TryGetProperty("policies", out var policies))
            {
                foreach (var policy in policies.EnumerateArray())
                {
                    if (policy.GetProperty("action").GetString() != "permission")
                        continue;
                    if (policy.GetProperty("effect").GetString() != "deny")
                        throw PolicyError();
                    if (policy.GetProperty("resource").GetString() == "*")
                        hardDeny = true;
                }
            }
        }
        if (!hardDeny || directories != 1 || sharing != "disabled")
            throw PolicyError();
        using var agents = await JsonAsync(HttpMethod.Get, $"api/agent/{Agent}", null, deadline.Token);
        var rules = agents.RootElement.GetProperty("data").GetProperty("permissions");
        if (!rules.EnumerateArray().Any() || !IsDenyAll(rules.EnumerateArray().Last()))
            throw PolicyError();
        using var plugins = await JsonAsync(HttpMethod.Get, "api/plugin", null, deadline.Token);
        if (plugins.RootElement.GetProperty("data").GetArrayLength() != 0)
            throw PolicyError();
        using var mcpStatus = await JsonAsync(HttpMethod.Get, "api/mcp", null, deadline.Token);
        if (mcpStatus.RootElement.GetProperty("data").GetArrayLength() != 0)
            throw PolicyError();
    }

    private static string? NormalizeDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            return null;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static int ChildCount(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.GetArrayLength(),
        JsonValueKind.Object => value.EnumerateObject().Count(),
        _ => 1
    };

    private static bool IsDenyAll(JsonElement rule) => rule.GetProperty("action").GetString() == "*"
        && rule.GetProperty("resource").GetString() == "*" && rule.GetProperty("effect").GetString() == "deny";

    private static InvalidOperationException PolicyError() => new("OpenCode safety policy refused: require exactly the trusted configured global directory, disabled sharing, hard deny-all permissions, a projectops deny-all agent, and no plugins, MCP, or other discovered context directories.");

    private async Task StopSessionAsync(RunningJob job)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        /** Cancel the known inbox ID first so an input awaiting execution cannot start later. */
        using (var response = await SendRawAsync(HttpMethod.Delete,
            $"api/session/{job.SessionId}/inbox/{job.PromptId}", null, timeout.Token))
        {
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                EnsureSuccess(response);
        }
        using var stopped = await JsonAsync(HttpMethod.Post,
            $"api/session/{job.SessionId}/interrupt?resume=false", null, timeout.Token);
        if (stopped.RootElement.GetProperty("interrupted").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException("Missing interrupt acknowledgement.");
        await WaitAsync(job.SessionId, timeout.Token);
        using var active = await JsonAsync(HttpMethod.Get, "api/session/active", null, timeout.Token);
        if (active.RootElement.GetProperty("data").TryGetProperty(job.SessionId, out _))
            throw new InvalidOperationException("OpenCode session is still running.");
    }

    private async Task WaitAsync(string session, CancellationToken token)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/experimental/session/{session}/wait", null, token);
        if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
            throw new InvalidOperationException("OpenCode did not acknowledge idle execution.");
    }

    private static async Task<string> ReadEventsAsync(StreamReader reader, string session,
        IProgress<AgentEvent> progress, CancellationToken token)
    {
        var text = new StringBuilder();
        var confirmed = new StringBuilder();
        while (true)
        {
            using var document = await ReadEventAsync(reader, token);
            var item = document.RootElement;
            if (!item.TryGetProperty("data", out var data)
                || !data.TryGetProperty("sessionID", out var id) || id.GetString() != session)
                continue;
            var type = item.GetProperty("type").GetString();
            if (type == "session.text.delta")
            {
                var delta = data.GetProperty("delta").GetString() ?? "";
                text.Append(delta);
                Report(progress, AgentEventKind.ResultDelta, delta);
            }
            else if (type == "session.text.ended")
                confirmed.Append(data.GetProperty("text").GetString());
            else if (type == "session.execution.succeeded")
            {
                if (text.ToString() != confirmed.ToString())
                    throw new InvalidOperationException("OpenCode streamed text did not match the confirmed result; retry the job.");
                return confirmed.ToString();
            }
            else if (type is "session.execution.failed" or "session.step.failed")
                throw new InvalidOperationException("OpenCode generation failed. Check provider authentication and availability on the dedicated runtime.");
            else if (type == "session.execution.interrupted")
                throw new InvalidOperationException("OpenCode execution was interrupted externally.");
            else if (type == "session.step.ended" && data.GetProperty("finish").GetString() != "stop")
                throw new InvalidOperationException("OpenCode generation did not finish normally; the response may be incomplete.");
            else if (type == "session.tool.called" && data.GetProperty("executed").GetBoolean())
                throw PolicyError();
        }
    }

    private static async Task<JsonDocument> ReadEventAsync(StreamReader reader, CancellationToken token)
    {
        var payload = new StringBuilder();
        string? line;
        while ((line = await reader.ReadLineAsync(token)) is not null)
        {
            if (line.StartsWith("event: effect/httpapi/stream/failure", StringComparison.Ordinal))
                throw new InvalidOperationException("OpenCode event stream failed.");
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (payload.Length != 0) payload.Append('\n');
                payload.Append(line[5..].TrimStart(' '));
            }
            else if (line.Length == 0 && payload.Length != 0)
                return JsonDocument.Parse(payload.ToString());
        }
        throw new InvalidOperationException("OpenCode event stream ended before generation completed; no successful result was confirmed.");
    }

    private async Task<JsonDocument> JsonAsync(HttpMethod method, string path, object? body, CancellationToken token)
    {
        using var response = await SendAsync(method, path, body, token);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken token)
    {
        var response = await SendRawAsync(method, path, body, token);
        try { EnsureSuccess(response); return response; }
        catch { response.Dispose(); throw; }
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, new Uri(new Uri(options.Url!), path));
        if (!string.IsNullOrWhiteSpace(options.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "OpenCode authentication failed. Check PROJECTOPS_OPENCODE_TOKEN."
                : $"OpenCode request failed (HTTP {(int)response.StatusCode}); check dedicated runtime configuration and V2 API support.");
    }

    private static void Report(IProgress<AgentEvent> progress, AgentEventKind kind, string message, string? activityKey = null)
        => progress.Report(new AgentEvent { Kind = kind, Message = message, ActivityKey = activityKey });

    public void Dispose() => client.Dispose();

    private sealed class RunningJob(CancellationToken token) : IDisposable
    {
        private readonly object gate = new();
        private bool disposed;
        public string SessionId { get; } = "ses_" + Guid.NewGuid().ToString("N");
        public string PromptId { get; } = "msg_" + Guid.NewGuid().ToString("N");
        public CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(token);
        public TaskCompletionSource<Exception?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void RequestStop()
        {
            lock (gate)
                if (!disposed) Stop.Cancel();
        }
        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                Stop.Dispose();
            }
        }
    }
}
