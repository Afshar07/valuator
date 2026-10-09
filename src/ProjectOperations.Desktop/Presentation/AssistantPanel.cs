using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop;

/// <summary>
/// Docked, toggleable assistant for the open project. Controls persist for the project so a consent decision is tied
/// to the exact snapshot on screen. Every run needs: a request, a fresh context preview, and explicit consent.
/// Proposals are never committed until approved here; committed tasks and proposals are visually distinct.
/// </summary>
internal sealed class AssistantPanel : Border
{
    private readonly PresentationContext _context;
    private readonly TextBlock _title;
    private readonly Control _status;
    private string _statusKey = "agent.state.consent";
    private string _statusColor = "Warning";
    private readonly Button _close;
    private readonly StackPanel _picker = new() { Spacing = 8 };
    private readonly Control _off;
    private readonly StackPanel _request = new() { Spacing = 12, Name = "DelegationRequest" };
    private readonly StackPanel _outcome = new() { Spacing = 10, IsVisible = false, Name = "AgentOutcome" };
    private readonly StackPanel _review = new() { Spacing = 12, IsVisible = false, Name = "ProposalReview" };
    private readonly TextBox _prompt;
    private readonly TextBox _preview;
    private readonly CheckBox _consent;
    private readonly Button _run;
    private readonly StackPanel _summary = new() { Spacing = 4 };
    private readonly TextBlock _outcomeLabel;
    private readonly StackPanel _steps = new() { Spacing = 6 };
    private readonly TextBox _result;
    private readonly Control _stopping;
    private readonly Button _stop;
    private readonly Control _failed;
    private readonly Button _detailsToggle;
    private readonly TextBox _details;
    private readonly List<Button> _actions = [];
    private Project? _project;
    private IReadOnlyList<AgentJob> _jobs = [];
    private string? _reviewJobId;
    private string _approvedContext = "";
    private string _outcomeKey = "agent.ready";
    private Func<string>? _outcomeText;

    public AssistantPanel(PresentationContext context)
    {
        _context = context;
        BorderThickness = new Thickness(1, 0, 0, 0);
        this.Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault");

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        header.Children.Add(Ui.Tile(Icons.Glyph(Icons.Sparkle, 15, "Ai", IconWeight.Fill), 28, 8, "AiSoft"));
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _title = context.Label(() => _project is null ? context.Text.Get("agent.assistant") : $"{context.Text.Get("agent.assistant")} · {_project.Name}", "BodyStrong");
        _title.TextWrapping = TextWrapping.NoWrap; _title.TextTrimming = TextTrimming.CharacterEllipsis; names.Children.Add(_title);
        _status = StatusLine(context, () => _statusKey, () => _statusColor); _status.Name = "AgentStatus"; names.Children.Add(_status);
        Grid.SetColumn(names, 1); header.Children.Add(names);
        _close = context.IconAction("action.close", Icons.X, () => { context.Shell.SetAssistantOpen(false); return Task.CompletedTask; }, "icon", iconOnly: true);
        Grid.SetColumn(_close, 2); header.Children.Add(_close);
        var top = new Border { Padding = new Thickness(16, 16, 16, 12), BorderThickness = new Thickness(0, 0, 0, 1), Child = header }.Paint(BorderBrushProperty, "BorderSubtle");

        var question = context.Label("agent.title", "PanelTitle");
        _off = Ui.Banner(context.Label(() => context.Text.Get("agent.offBody"), "Small", "TextSecondary"), Icons.Plugs, "TextTertiary", "BackgroundMuted", "BorderDefault");

        // Request: starting points, freeform request, context summary, exact preview, consent and Run.
        _request.Children.Add(context.Label("agent.pickAction", "Caption", "TextSecondary"));
        var actions = new UniformGrid { Columns = 2 };
        foreach (var preset in AgentPrompts.Actions)
        {
            var option = new Button { Margin = new Thickness(0, 0, 6, 6), HorizontalContentAlignment = HorizontalAlignment.Left };
            option.Classes.Add("option");
            var caption = context.Label(() => context.Text.Get("agent.action." + preset.Id), "Small"); caption.FontWeight = FontWeight.Medium; option.Content = caption;
            context.Localized.Bind(option, control => AutomationProperties.SetName(control, context.Text.Get("agent.action." + preset.Id)));
            var prompt = preset.Prompt;
            option.Click += (_, _) => { _prompt!.Text = prompt; HighlightAction(option); };
            _actions.Add(option); actions.Children.Add(option);
        }
        _request.Children.Add(actions);
        var requestField = new StackPanel { Spacing = 5 };
        requestField.Children.Add(context.Label("agent.request", "Caption", "TextSecondary"));
        _prompt = new TextBox { Name = "AgentPrompt", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 76, VerticalContentAlignment = VerticalAlignment.Top, TextAlignment = TextAlignment.Start };
        _prompt.TextChanged += (_, _) => { _consent!.IsChecked = false; if (!_actions.Any(a => AgentPrompts.Actions[_actions.IndexOf(a)].Prompt == _prompt.Text)) HighlightAction(null); };
        requestField.Children.Add(_prompt); _request.Children.Add(requestField);

        var contextCard = new StackPanel { Spacing = 8 };
        var contextTitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        contextTitle.Children.Add(Icons.Glyph(Icons.Eye, 13, "TextPrimary")); var contextHeading = context.Label("agent.contextTitle", "CaptionMedium"); contextHeading.FontWeight = FontWeight.SemiBold; contextTitle.Children.Add(contextHeading);
        contextCard.Children.Add(contextTitle); contextCard.Children.Add(_summary);
        var endpoint = context.Label(() => context.Environment.Endpoint is { Length: > 0 } url ? context.Text.Format("agent.endpointShort", url) : context.ConfigurationText(), "Meta", "TextTertiary");
        contextCard.Children.Add(endpoint);
        var previewButton = context.IconAction("agent.preview", Icons.Eye, () => { Preview(); return Task.CompletedTask; });
        previewButton.Name = "PreviewContext"; contextCard.Children.Add(previewButton);
        _preview = new TextBox
        {
            Name = "ContextPreview",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            MaxHeight = 260,
            FontSize = 11,
            IsVisible = false,
            FlowDirection = FlowDirection.LeftToRight,
            TextAlignment = TextAlignment.Start,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        _preview.Paint(TextBox.BackgroundProperty, "BackgroundCard");
        contextCard.Children.Add(_preview);
        _request.Children.Add(new Border { Padding = new Thickness(12), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), BorderThickness = new Thickness(1), Child = contextCard }
            .Paint(BackgroundProperty, "BackgroundMuted").Paint(BorderBrushProperty, "BorderDefault"));

        _consent = AiCheckBox(context.Label("agent.consent", "Small"));
        _consent.IsEnabled = false;
        _consent.IsCheckedChanged += (_, _) => _run!.IsEnabled = _consent.IsChecked == true && !string.IsNullOrWhiteSpace(_prompt.Text);
        _request.Children.Add(_consent);
        _run = new Button { IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch, Name = "RunAgent" }; _run.Classes.Add("aiPrimary");
        var runContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };
        var runIcon = Icons.Glyph(Icons.Play, 13, "OnAccent", IconWeight.Bold); runContent.Children.Add(runIcon);
        var runText = new TextBlock { VerticalAlignment = VerticalAlignment.Center }; runContent.Children.Add(runText); _run.Content = runContent;
        context.Localized.Bind(_run, control => { runText.Text = context.Text.Get("agent.run"); AutomationProperties.SetName(control, context.Text.Get("agent.run")); });
        _run.Click += async (_, _) => await RunAsync();
        _request.Children.Add(_run);

        // Outcome: live activity, partial result, Stop and technical details for the job in view.
        _outcomeLabel = context.Label(() => _outcomeText?.Invoke() ?? context.Text.Get(_outcomeKey), "SmallStrong");
        _outcomeLabel.Name = "AgentOutcomeLabel";
        var working = new StackPanel { Spacing = 10 };
        working.Children.Add(_outcomeLabel); working.Children.Add(_steps);
        _outcome.Children.Add(new Border { Padding = new Thickness(14), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), BorderThickness = new Thickness(1), Child = working }
            .Paint(BorderBrushProperty, "AiBorder"));
        _result = new TextBox
        {
            Name = "AgentResult",
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            MaxHeight = 320,
            FontSize = 12.5,
            TextAlignment = TextAlignment.Start,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        var resultLabel = context.Label("agent.result", "Caption", "TextSecondary");
        _outcome.Children.Add(resultLabel); _outcome.Children.Add(_result);
        _stopping = Ui.Banner(context.Label("agent.stopping", "Small"), Icons.HourglassMedium, "Warning", "WarningSoft"); _stopping.IsVisible = false;
        _outcome.Children.Add(_stopping);
        _failed = Ui.Banner(context.Label("validation.agentFailed", "Small"), Icons.XCircle, "Error", "ErrorSoft", "Error"); _failed.IsVisible = false; _failed.Name = "AgentFailed";
        _outcome.Children.Add(_failed);
        _stop = new Button { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch, Name = "StopButton" }; _stop.Classes.Add("danger");
        var stopContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };
        var stopIcon = Icons.Glyph(Icons.Stop, 13, "Error", IconWeight.Fill); stopContent.Children.Add(stopIcon); var stopText = new TextBlock { VerticalAlignment = VerticalAlignment.Center }; stopContent.Children.Add(stopText);
        _stop.Content = stopContent;
        context.Localized.Bind(_stop, control => { stopText.Text = context.Text.Get("agent.stop"); AutomationProperties.SetName(control, context.Text.Get("agent.stop")); });
        _stop.Click += (_, _) =>
        {
            _stop.IsEnabled = false; _stopping.IsVisible = true;
            SetStatus("agent.state.stopping", "Warning"); _outcomeText = null; _outcomeKey = "agent.state.stopping"; _outcomeLabel.Text = context.Text.Get(_outcomeKey);
            context.Shell.CancelRun();
        };
        _outcome.Children.Add(_stop);
        _details = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 48, MaxHeight = 220, FontSize = 11, IsVisible = false, FlowDirection = FlowDirection.LeftToRight };
        _detailsToggle = context.Action("agent.showDetails", () => { _details.IsVisible = !_details.IsVisible; return Task.CompletedTask; }, "link");
        _outcome.Children.Add(_detailsToggle); _outcome.Children.Add(_details);

        var body = new StackPanel { Spacing = 14 };
        body.Children.Add(question); body.Children.Add(_picker); body.Children.Add(_off); body.Children.Add(_outcome); body.Children.Add(_review); body.Children.Add(_request);
        var scroll = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = new Border { Padding = new Thickness(16), Child = body } };
        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top); dock.Children.Add(scroll);
        Child = dock;
        ShowMode();
    }

    public Guid? ProjectId => _project?.Id;

    public void SetOverlay(bool overlay)
    {
        if (overlay) this.RaisedShadowed(); else ClearValue(BoxShadowProperty);
    }

    public static Control StatusLine(PresentationContext context, Func<string> key, Func<string> color)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var dot = Ui.Dot(7, color());
        var label = context.Label(() => context.Text.Get(key()), "Meta", color()); label.TextWrapping = TextWrapping.NoWrap;
        line.Children.Add(dot); line.Children.Add(label);
        context.Localized.Bind(label, control => { control.Text = context.Text.Get(key()); control.Paint(TextBlock.ForegroundProperty, color()); dot.Paint(BackgroundProperty, color()); });
        return line;
    }

    private void SetStatus(string key, string color)
    {
        _statusKey = key; _statusColor = color;
        var line = (StackPanel)_status; var dot = (Border)line.Children[0]; var label = (TextBlock)line.Children[1];
        label.Text = _context.Text.Get(key); label.Paint(TextBlock.ForegroundProperty, color); dot.Paint(BackgroundProperty, color);
    }

    /// <summary>No project open: offer the projects the assistant can work on.</summary>
    public async Task ShowPickerAsync()
    {
        if (_context.Shell.IsAgentRunning) return;
        _project = null; _jobs = []; _reviewJobId = null;
        _title.Text = _context.Text.Get("agent.assistant");
        _picker.Children.Clear();
        _picker.Children.Add(_context.Label("agent.pickProject", "Small", "TextSecondary"));
        foreach (var project in (await _context.Projects.ListAsync()).Where(item => item.Status is ProjectStatus.Active or ProjectStatus.OnHold).OrderBy(item => item.Name))
        {
            var id = project.Id;
            var content = ListRow.Layout(_context, Ui.Initial(project.Name, 26), () => project.Name, string.IsNullOrWhiteSpace(project.CompanyName) ? null : () => project.CompanyName, null, null);
            var row = new ListRow(_context, content, () => project.Name, () => _context.OpenProjectAsync(id, ProjectTab.Delegate)) { Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(PresentationTheme.RadiusControl) };
            _picker.Children.Add(row);
        }
        if (_picker.Children.Count == 1) _picker.Children.Add(_context.Label("dashboard.empty", "Small", "TextTertiary"));
        ShowMode();
    }

    /// <summary>Show or refresh the panel for a project. Reopening the same project keeps request, preview and consent controls.</summary>
    public async Task ShowProjectAsync(Project project)
    {
        var same = _project?.Id == project.Id;
        _project = project; _title.Text = $"{_context.Text.Get("agent.assistant")} · {project.Name}";
        _jobs = await _context.Agents.HistoryAsync(project.Id);
        if (!same)
        {
            _prompt.Text = ""; HighlightAction(null); ResetConsent();
            _outcome.IsVisible = false; _steps.Children.Clear(); _result.Text = ""; _details.Text = ""; _failed.IsVisible = false;
            var latest = _jobs.OrderByDescending(job => job.CreatedAt).FirstOrDefault();
            _reviewJobId = latest is { Status: AgentJobStatus.Completed } && latest.Proposals.Count > 0 ? latest.Id : null;
            if (_reviewJobId is not null) ShowFinished(latest!);
        }
        else if (_approvedContext.Length > 0 && _approvedContext != AgentService.BuildContext(project, _jobs)) ResetConsent();
        RenderSummary(); RenderReview(); ShowMode();
    }

    public async Task ReviewAsync(string jobId)
    {
        if (_project is null) return;
        _jobs = await _context.Agents.HistoryAsync(_project.Id);
        var job = _jobs.FirstOrDefault(item => item.Id == jobId);
        if (job is null) return;
        _reviewJobId = job.Id; ShowFinished(job); RenderReview(); ShowMode();
    }

    private void ShowMode()
    {
        var hasProject = _project is not null;
        var configured = _context.Environment.AgentConfigured;
        var running = _context.Shell.IsAgentRunning;
        _picker.IsVisible = !hasProject;
        _off.IsVisible = hasProject && !configured;
        _request.IsVisible = hasProject && configured && !running;
        _close.IsEnabled = !running;
        if (!hasProject) SetStatus(configured ? "agent.state.ready" : "agent.state.off", configured ? "Accent" : "TextTertiary");
        else if (!configured) SetStatus("agent.state.off", "TextTertiary");
        else if (!running && !_outcome.IsVisible) SetStatus(PendingCount() > 0 ? "agent.state.review" : "agent.state.consent", PendingCount() > 0 ? "Ai" : "Warning");
    }

    private void HighlightAction(Button? selected)
    {
        foreach (var action in _actions) action.Classes.Set("selected", ReferenceEquals(action, selected));
    }

    private void ResetConsent()
    {
        _approvedContext = ""; _preview.Text = ""; _preview.IsVisible = false;
        _consent.IsChecked = false; _consent.IsEnabled = false; _run.IsEnabled = false;
    }

    private void Preview()
    {
        if (_project is null) return;
        _approvedContext = AgentService.BuildContext(_project, _jobs);
        _preview.Text = _approvedContext; _preview.IsVisible = true;
        _consent.IsEnabled = true; _consent.IsChecked = false;
    }

    /// <summary>Human-readable outline of what the context snapshot contains, computed from the project itself.</summary>
    private void RenderSummary()
    {
        _summary.Children.Clear();
        if (_project is null) return;
        var project = _project;
        var results = _jobs.Count(job => job.Status == AgentJobStatus.Completed && !string.IsNullOrWhiteSpace(job.ResultText));
        var lines = new Func<string>[]
        {
            () => _context.Text.Get("agent.context.metadata"),
            () => _context.Text.Format("agent.context.requirements", project.Requirements.Count),
            () => _context.Text.Format("agent.context.tasks", project.Tasks.Count, project.Milestones.Count),
            () => _context.Text.Format("agent.context.files", project.Requirements.Sum(item => item.Files.Count)),
            () => _context.Text.Format("agent.context.results", Math.Min(3, results))
        };
        foreach (var line in lines)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6 };
            var bullet = Icons.Glyph(Icons.DotOutline, 13, "TextSecondary"); bullet.VerticalAlignment = VerticalAlignment.Top; bullet.Margin = new Thickness(0, 2, 0, 0); row.Children.Add(bullet);
            var text = _context.Label(line, "Small", "TextSecondary"); Grid.SetColumn(text, 1); row.Children.Add(text);
            _summary.Children.Add(row);
        }
    }

    private int PendingCount() => _jobs.Where(job => job.Status == AgentJobStatus.Completed).Sum(job => job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending));

    private async Task RunAsync()
    {
        if (_project is null) return;
        var project = _project; var previousJobs = _jobs;
        if (_consent.IsChecked != true || string.IsNullOrWhiteSpace(_prompt.Text) || _approvedContext != AgentService.BuildContext(project, previousJobs))
        { _consent.IsChecked = false; _context.ShowError("validation.contextConsentRequired"); return; }
        _consent.IsChecked = false;
        _reviewJobId = null; _review.IsVisible = false; _failed.IsVisible = false; _stopping.IsVisible = false;
        _steps.Children.Clear(); _result.Text = ""; _details.Text = ""; _details.IsVisible = false;
        _outcomeText = null; _outcomeKey = "agent.running"; _outcomeLabel.Text = _context.Text.Get(_outcomeKey);
        _outcome.IsVisible = true; _stop.IsVisible = true; _stop.IsEnabled = true;
        SetStatus("agent.running", "Ai");
        var activities = new List<AgentEvent>();
        var accepting = true;
        var progress = new Progress<AgentEvent>(item =>
        {
            if (!accepting) return;
            if (item.Kind == AgentEventKind.ResultDelta) _result.Text += item.Message;
            else if (item.Kind == AgentEventKind.Activity) { activities.Add(item); RenderSteps(activities, running: true); }
            else _details.Text += item.Message + "\n";
        });
        var prompt = _prompt.Text!;
        await _context.Shell.RunAgentAsync(async token =>
        {
            _request.IsVisible = false; _close.IsEnabled = false;
            try
            {
                AgentJob job;
                try { job = await _context.Agents.RunAsync(project, prompt, progress, token, previousJobs, _context.Locale.AgentResponseLanguage); }
                catch { _outcomeText = null; _outcomeKey = "agent.outcomeNotRecorded"; _outcomeLabel.Text = _context.Text.Get(_outcomeKey); SetStatus("agent.state.failed", "Error"); throw; }
                accepting = false;
                RenderSteps(activities, running: false);
                if (job.Proposals.Count > 0 && job.Status == AgentJobStatus.Completed) _reviewJobId = job.Id;
                // Outcome and review tray appear together, so "Completed" is never shown without its proposals.
                _jobs = [.. previousJobs.Where(item => item.Id != job.Id), job];
                RenderReview();
                ShowFinished(job);
            }
            finally { accepting = false; }
        }, async () =>
        {
            _stop.IsVisible = false; _stopping.IsVisible = false;
            ResetConsent();
            _jobs = await _context.Agents.HistoryAsync(project.Id);
            RenderSummary(); RenderReview(); ShowMode();
            await _context.Shell.RefreshProjectAsync();
        });
    }

    private void RenderSteps(List<AgentEvent> activities, bool running)
    {
        _steps.Children.Clear();
        for (var index = 0; index < activities.Count; index++)
        {
            var item = activities[index]; var current = running && index == activities.Count - 1;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(Icons.Glyph(current ? Icons.CircleNotch : Icons.Check, 13, "Ai", IconWeight.Bold));
            row.Children.Add(_context.Label(() => item.ActivityKey is null ? item.Message : _context.Text.Get(item.ActivityKey), "Small"));
            _steps.Children.Add(row);
        }
    }

    /// <summary>Shows a finished job's truthful outcome; failures are never presented as success.</summary>
    private void ShowFinished(AgentJob job)
    {
        _outcome.IsVisible = true; _stop.IsVisible = false; _stopping.IsVisible = false;
        _outcomeText = () => _context.EnumText(job.Status); _outcomeLabel.Text = _outcomeText();
        _result.Text = job.ResultText;
        if (!string.IsNullOrWhiteSpace(job.Error) && _details.Text?.Contains(job.Error) != true) _details.Text += job.Error;
        _failed.IsVisible = job.Status == AgentJobStatus.Failed;
        var pending = job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending);
        var (key, color) = job.Status switch
        {
            AgentJobStatus.Completed when pending > 0 => ("agent.state.review", "Ai"),
            AgentJobStatus.Completed => ("agent.status.completed", "Success"),
            AgentJobStatus.Cancelled => ("agent.status.cancelled", "TextSecondary"),
            AgentJobStatus.Failed => ("agent.state.failed", "Error"),
            _ => ("agent.status.interrupted", "Warning")
        };
        SetStatus(key, color);
    }

    /// <summary>Review tray for the job in view plus the project's committed tasks, kept visually apart.</summary>
    private void RenderReview()
    {
        _review.Children.Clear();
        var job = _jobs.FirstOrDefault(item => item.Id == _reviewJobId);
        if (_project is null || job is null || job.Proposals.Count == 0) { _review.IsVisible = false; return; }
        _review.IsVisible = true;
        var pending = job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending);
        var tray = new StackPanel();
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 6 };
        head.Children.Add(Icons.Glyph(Icons.Sparkle, 13, "Ai", IconWeight.Fill));
        var trayTitle = _context.Label(() => _context.Text.Format("proposal.trayTitle", pending), "CaptionMedium", "Ai"); trayTitle.FontWeight = FontWeight.SemiBold; Grid.SetColumn(trayTitle, 1); head.Children.Add(trayTitle);
        var note = _context.Label("proposal.notCommitted", "Micro", "TextSecondary"); note.FontWeight = FontWeight.Normal; note.VerticalAlignment = VerticalAlignment.Center; note.TextWrapping = TextWrapping.NoWrap; Grid.SetColumn(note, 2); head.Children.Add(note);
        tray.Children.Add(new Border { Padding = new Thickness(12, 10), Child = head });
        var selections = new List<(Guid Id, CheckBox Check)>();
        foreach (var proposal in job.Proposals)
        {
            var text = _context.Label(() => $"{proposal.Title} · {_context.Text.Get("proposal.due")} {_context.Due(proposal.DueAt)} · {_context.EnumText(proposal.ReviewStatus)}"
                + (string.IsNullOrWhiteSpace(proposal.Description) ? "" : "\n" + proposal.Description), "Small");
            var check = AiCheckBox(text);
            check.IsChecked = false; check.IsEnabled = job.Status == AgentJobStatus.Completed && proposal.ReviewStatus == ProposalReviewStatus.Pending;
            selections.Add((proposal.Id, check));
            tray.Children.Add(new Border { Padding = new Thickness(12, 8), BorderThickness = new Thickness(0, 1, 0, 0), Child = check }.Paint(BorderBrushProperty, "AiBorder"));
        }
        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        var approve = _context.Action("proposal.addSelected", async () =>
        {
            await _context.Agents.ApproveAsync(job, selections.Where(s => s.Check.IsChecked == true).Select(s => s.Id));
            await RefreshAfterReviewAsync();
        }, "primary");
        approve.HorizontalAlignment = HorizontalAlignment.Stretch; approve.IsEnabled = pending > 0; buttons.Children.Add(approve);
        var reject = _context.Action("proposal.rejectSelected", async () =>
        {
            await _context.Agents.RejectAsync(job, selections.Where(s => s.Check.IsChecked == true).Select(s => s.Id));
            await RefreshAfterReviewAsync();
        });
        reject.IsEnabled = pending > 0; Grid.SetColumn(reject, 1); buttons.Children.Add(reject);
        tray.Children.Add(new Border { Padding = new Thickness(12, 10), BorderThickness = new Thickness(0, 1, 0, 0), Child = buttons }.Paint(BorderBrushProperty, "AiBorder"));
        _review.Children.Add(new Border { CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), BorderThickness = new Thickness(1), ClipToBounds = true, Child = tray, Name = "ProposalTray" }
            .Paint(BackgroundProperty, "AiSoft").Paint(BorderBrushProperty, "AiBorder"));

        var committed = new StackPanel { Spacing = 4, Name = "CommittedTasks" };
        committed.Children.Add(_context.Label("proposal.committed", "Caption", "TextSecondary"));
        var now = DateTimeOffset.Now;
        var active = _project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress).ToList();
        if (active.Count == 0) committed.Children.Add(_context.Label("presentation.noActiveTasks", "Small", "TextTertiary"));
        foreach (var task in active)
        {
            var late = task.DueAt < now;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            row.Children.Add(Icons.Glyph(Icons.Circle, 14, "TextTertiary"));
            var title = _context.Label(() => task.Title, "BodyMedium"); Grid.SetColumn(title, 1); row.Children.Add(title);
            var due = _context.Label(() => task.DueAt is null ? "" : _context.ShortDate(task.DueAt), "Caption", late ? "Error" : "TextSecondary"); due.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(due, 2); row.Children.Add(due);
            committed.Children.Add(new Border { Padding = new Thickness(10, 8), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusControl), Child = row }
                .Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault"));
        }
        _review.Children.Add(committed);
    }

    private async Task RefreshAfterReviewAsync()
    {
        await _context.Shell.RefreshProjectAsync();
    }

    /// <summary>Checkbox whose checked state uses the assistant color, so proposal selection never reads as committed data.</summary>
    private static CheckBox AiCheckBox(Control content)
    {
        var check = new CheckBox { Content = content, VerticalContentAlignment = VerticalAlignment.Top };
        foreach (var (variant, dark) in new[] { (ThemeVariant.Light, false), (ThemeVariant.Dark, true) })
        {
            var color = PresentationTheme.TokenColor("Ai", dark);
            var resources = new ResourceDictionary();
            foreach (var key in new[] { "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckBackgroundFillCheckedPressed",
                         "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed" })
                resources[key] = new SolidColorBrush(color);
            check.Resources.ThemeDictionaries[variant] = resources;
        }
        return check;
    }
}
