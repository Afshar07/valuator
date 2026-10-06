using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Core.Agents;

public sealed class AgentService(ProjectService projects, IAgentRuntime runtime, IAgentJobRepository jobs)
{
    private readonly ConcurrentDictionary<string, byte> _active = new();
    private readonly SemaphoreSlim _reviewLock = new(1, 1);
    private static readonly JsonSerializerOptions ContextOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() }
    };

    public static string BuildContext(Project project, IEnumerable<AgentJob>? history = null)
    {
        var context = new ProjectAgentContext
        {
            Project = project,
            RecentResults = (history ?? []).Where(job => job.ProjectId == project.Id
                && job.Status == AgentJobStatus.Completed && !string.IsNullOrWhiteSpace(job.ResultText))
                .OrderByDescending(job => job.CreatedAt).ThenBy(job => job.Id).Take(3)
                .Select(job => new PreviousAgentResult
                {
                    JobId = job.Id,
                    FinishedAt = job.FinishedAt,
                    Request = job.Prompt[..Math.Min(job.Prompt.Length, 500)],
                    ResultExcerpt = job.ResultText[..Math.Min(job.ResultText.Length, 12_000)]
                }).ToList()
        };
        return JsonSerializer.Serialize(context, ContextOptions);
    }

    public async Task<AgentJob> RunAsync(Project project, string prompt, IProgress<AgentEvent> progress,
        CancellationToken cancellationToken, IReadOnlyList<AgentJob>? previousJobs = null,
        AgentResponseLanguage responseLanguage = AgentResponseLanguage.English)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        var systemInstructions = AgentPrompts.BuildOutputInstructions(responseLanguage);
        var job = new AgentJob
        {
            ProjectId = project.Id,
            Prompt = prompt.Trim(),
            ContextSnapshot = BuildContext(project, previousJobs ?? await jobs.ListAsync(project.Id, cancellationToken)),
            Status = AgentJobStatus.Running
        };
        await jobs.SaveAsync(job, cancellationToken);
        _active.TryAdd(job.Id, 0);
        var partial = new StringBuilder();
        var forwarded = new ForwardProgress(progress, partial);
        try
        {
            var result = await runtime.RunAsync(new AgentRequest
            {
                JobId = job.Id,
                ProjectId = job.ProjectId,
                Prompt = job.Prompt,
                Context = job.ContextSnapshot,
                SystemInstructions = systemInstructions
            }, forwarded, cancellationToken);
            job.ResultText = result.Text;
            job.Proposals = result.Proposals;
            job.Status = AgentJobStatus.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Status = AgentJobStatus.Cancelled;
            job.ResultText = forwarded.PartialText;
        }
        catch (Exception exception)
        {
            job.Status = AgentJobStatus.Failed;
            job.Error = exception.Message;
            job.ResultText = forwarded.PartialText;
        }
        finally
        {
            job.FinishedAt = DateTimeOffset.UtcNow;
            try
            {
                // A cancelled UI token must not erase the job's truthful final outcome.
                await jobs.SaveAsync(job, CancellationToken.None);
            }
            finally
            {
                _active.TryRemove(job.Id, out _);
            }
        }
        return job;
    }

    public async Task<IReadOnlyList<AgentJob>> HistoryAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var history = await jobs.ListAsync(projectId, cancellationToken);
        foreach (var job in history.Where(job => job.Status == AgentJobStatus.Running && !_active.ContainsKey(job.Id)))
        {
            job.Status = AgentJobStatus.Interrupted;
            job.Error = "The application closed before a final outcome was recorded. Runtime stop is not confirmed.";
            job.FinishedAt = DateTimeOffset.UtcNow;
            await jobs.SaveAsync(job, cancellationToken);
        }
        return history;
    }

    public async Task ApproveAsync(AgentJob job, IEnumerable<Guid> proposalIds, CancellationToken cancellationToken = default)
    {
        await _reviewLock.WaitAsync(cancellationToken);
        try
        {
            // Reload both records: approving an old result must never overwrite intervening edits.
            var stored = (await jobs.ListAsync(job.ProjectId, cancellationToken)).Single(item => item.Id == job.Id);
            if (stored.Status != AgentJobStatus.Completed)
                throw new InvalidOperationException("Only a completed job can propose tasks for approval.");
            var project = await projects.GetAsync(job.ProjectId, cancellationToken)
                ?? throw new InvalidOperationException("The project is no longer available.");
            var selected = proposalIds.ToHashSet();
            var pending = stored.Proposals.Where(item => selected.Contains(item.Id)
                && item.ReviewStatus == ProposalReviewStatus.Pending).ToList();
            if (pending.Count == 0)
            {
                job.Proposals = stored.Proposals;
                return;
            }
            foreach (var proposal in pending)
            {
                // Stable IDs also make repeated approval requests idempotent.
                if (project.Tasks.All(task => task.Id != proposal.Id))
                    project.Tasks.Add(new ProjectTask
                    {
                        Id = proposal.Id,
                        ProjectId = project.Id,
                        Title = proposal.Title,
                        Description = proposal.Description,
                        DueAt = proposal.DueAt,
                        Status = ProjectTaskStatus.Todo,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                proposal.ReviewStatus = ProposalReviewStatus.Approved;
            }
            await jobs.SaveReviewAsync(project, stored, cancellationToken);
            job.Proposals = stored.Proposals;
        }
        finally
        {
            _reviewLock.Release();
        }
    }

    public async Task RejectAsync(AgentJob job, IEnumerable<Guid> proposalIds, CancellationToken cancellationToken = default)
    {
        await _reviewLock.WaitAsync(cancellationToken);
        try
        {
            var stored = (await jobs.ListAsync(job.ProjectId, cancellationToken)).Single(item => item.Id == job.Id);
            var selected = proposalIds.ToHashSet();
            var pending = stored.Proposals.Where(item => selected.Contains(item.Id)
                && item.ReviewStatus == ProposalReviewStatus.Pending).ToList();
            if (pending.Count == 0)
            {
                job.Proposals = stored.Proposals;
                return;
            }
            foreach (var proposal in pending)
                proposal.ReviewStatus = ProposalReviewStatus.Rejected;
            await jobs.SaveAsync(stored, cancellationToken);
            job.Proposals = stored.Proposals;
        }
        finally
        {
            _reviewLock.Release();
        }
    }

    private sealed class ForwardProgress(IProgress<AgentEvent> target, StringBuilder partial) : IProgress<AgentEvent>
    {
        public string PartialText
        {
            get { lock (partial) return partial.ToString(); }
        }

        public void Report(AgentEvent value)
        {
            if (value.Kind == AgentEventKind.ResultDelta)
                lock (partial) partial.Append(value.Message);
            target.Report(value);
        }
    }
}
