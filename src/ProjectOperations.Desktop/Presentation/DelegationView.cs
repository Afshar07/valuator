using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Delegate & review tab: where runs happen (the assistant panel) and the project's lightweight job/result history.</summary>
internal sealed class DelegationView : PresentationView
{
    private const string Columns = "1.4*,120,120,1.6*";

    public DelegationView(PresentationContext context, Project project, IReadOnlyList<AgentJob> jobs) : base(context)
    {
        Spacing = 16;
        var intro = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 12 };
        intro.Children.Add(Ui.Tile(Icons.Glyph(Icons.Sparkle, 15, "Ai", IconWeight.Fill), 30, 8, "AiSoft"));
        var text = Label("delegation.intro", "Small", "TextSecondary"); text.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(text, 1); intro.Children.Add(text);
        var status = AssistantPanel.StatusLine(Context, () => context.Environment.AgentConfigured ? "agent.state.ready" : "agent.state.off", () => context.Environment.AgentConfigured ? "Accent" : "TextTertiary");
        Grid.SetColumn(status, 2); intro.Children.Add(status);
        var open = Action("delegation.openAssistant", () => { Context.Shell.SetAssistantOpen(true); return Task.CompletedTask; }, "ai"); Grid.SetColumn(open, 3); intro.Children.Add(open);
        Children.Add(new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusCard), Padding = new Thickness(16, 12), Child = intro }
            .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault"));

        var history = new ListCard(Context, () => T("history.title")) { Name = "HistoryCard" };
        history.Add(new TableHeader(Context, Columns, "history.column.request", "history.column.status", "history.column.started", "history.column.outcome") { BorderThickness = new Thickness(0) });
        if (jobs.Count == 0) history.Add(new Border { Padding = new Thickness(16, 12), Child = Label("history.empty", "Body", "TextTertiary") });
        foreach (var job in jobs.OrderByDescending(job => job.CreatedAt)) history.Add(HistoryRow(job));
        Children.Add(history);
    }

    private Control HistoryRow(AgentJob job)
    {
        var stack = new StackPanel();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), ColumnSpacing = 14 };
        var request = Label(() => job.Prompt, "BodyMedium"); request.MaxLines = 2; request.TextTrimming = TextTrimming.CharacterEllipsis; request.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(request);
        var (tone, icon) = JobVisual(job.Status);
        var pill = Ui.Pill(Context, () => EnumText(job.Status), tone, icon); Grid.SetColumn(pill, 1); grid.Children.Add(pill);
        var started = Label(() => Context.Due(job.CreatedAt), "Caption", "TextSecondary"); started.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(started, 2); grid.Children.Add(started);
        var outcome = Label(() => Outcome(job), "Small", "TextSecondary"); outcome.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(outcome, 3); grid.Children.Add(outcome);
        var details = new ContentControl { IsVisible = false };
        var row = new ListRow(Context, grid, () => $"{job.Prompt} · {EnumText(job.Status)} · {Context.Due(job.CreatedAt)}", () =>
        {
            details.Content ??= Details(job);
            details.IsVisible = !details.IsVisible; return Task.CompletedTask;
        })
        { Name = "HistoryRow", Padding = new Thickness(16, 11) };
        stack.Children.Add(row); stack.Children.Add(details);
        return stack;
    }

    /// <summary>Expanded history entry. Proposal review itself happens in the assistant panel so each proposal has exactly one approval control.</summary>
    private Control Details(AgentJob job)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16, 0, 16, 14) };
        panel.Children.Add(Label(() => job.FinishedAt.HasValue ? F("history.finished", Due(job.FinishedAt)) : T("history.noOutcome"), "Caption", "TextSecondary"));
        if (job.Status == AgentJobStatus.Interrupted) panel.Children.Add(Label("history.interrupted", "Small", "Warning"));
        panel.Children.Add(Label("agent.request", "Caption", "TextSecondary")); panel.Children.Add(Readable(job.Prompt));
        if (!string.IsNullOrWhiteSpace(job.ResultText)) { panel.Children.Add(Label("agent.resultLabel", "Caption", "TextSecondary")); panel.Children.Add(Readable(job.ResultText)); }
        if (!string.IsNullOrWhiteSpace(job.Error))
        {
            var technical = Readable(job.Error); technical.FlowDirection = FlowDirection.LeftToRight;
            var expander = new Expander { Content = technical, HorizontalAlignment = HorizontalAlignment.Stretch }; Bind(expander, control => control.Header = T("history.outcomeDetails")); panel.Children.Add(expander);
        }
        foreach (var proposal in job.Proposals)
            panel.Children.Add(Label(() => $"• {proposal.Title} · {Due(proposal.DueAt)} · {EnumText(proposal.ReviewStatus)}", "Small"));
        if (job.Status == AgentJobStatus.Completed && job.Proposals.Any(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending))
            panel.Children.Add(Action("history.reviewInAssistant", () => Context.Shell.ReviewInAssistantAsync(job.Id), "ai"));
        return panel;
    }

    private string Outcome(AgentJob job)
    {
        var pending = job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending);
        return job.Status switch
        {
            AgentJobStatus.Completed when job.Proposals.Count == 0 => T("history.outcome.noProposals"),
            AgentJobStatus.Completed when pending > 0 => F("history.outcome.pending", job.Proposals.Count, pending),
            AgentJobStatus.Completed => F("history.outcome.reviewed", job.Proposals.Count(p => p.ReviewStatus == ProposalReviewStatus.Approved), job.Proposals.Count(p => p.ReviewStatus == ProposalReviewStatus.Rejected)),
            AgentJobStatus.Cancelled => T("history.outcome.cancelled"),
            AgentJobStatus.Failed => T("history.outcome.failed"),
            AgentJobStatus.Interrupted => T("history.outcome.interrupted"),
            _ => T("history.noOutcome")
        };
    }

    public static (Tone Tone, string Icon) JobVisual(AgentJobStatus status) => status switch
    {
        AgentJobStatus.Completed => (Tone.Success, Icons.Check),
        AgentJobStatus.Cancelled => (Tone.Neutral, Icons.Stop),
        AgentJobStatus.Failed => (Tone.Error, Icons.X),
        AgentJobStatus.Interrupted => (Tone.Warning, Icons.Plugs),
        _ => (Tone.Ai, Icons.CircleNotch)
    };
}
