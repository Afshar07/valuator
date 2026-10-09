using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Assistant;

/// <summary>
/// The assistant for the open project, docked at the end edge. It keeps one set of request, preview and consent state per project, so a consent
/// decision is tied to the exact snapshot on screen. Every run needs a request, a fresh context preview and explicit consent; proposals are never
/// committed until approved in the tray, which keeps them visually apart from the project's committed tasks. All of it is state here: the view
/// only binds to it.
/// </summary>
internal sealed partial class AssistantViewModel : ViewModelBase, IAssistantPanel
{
    private readonly AssistantServices _services;
    private Project? _project;
    private IReadOnlyList<AgentJob> _jobs = [];
    private string? _reviewJobId;
    /// <summary>The context snapshot the user previewed and consented to; a run only goes ahead while it still equals the project's current snapshot.</summary>
    private string _approvedContext = "";
    private string _statusKey = "agent.state.consent";
    private string? _outcomeKey = "agent.ready";
    private AgentJobStatus? _outcomeStatus;

    [ObservableProperty] private string _statusToken = "Warning";
    [ObservableProperty] private IReadOnlyList<AssistantProjectRowViewModel> _projects = [];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _prompt = "";
    [ObservableProperty] private string _previewText = "";
    [ObservableProperty] private bool _isPreviewVisible;
    [ObservableProperty] private bool _isConsentEnabled;
    [ObservableProperty] private bool _isConsentChecked;
    [ObservableProperty] private bool _isOutcomeVisible;
    [ObservableProperty] private string _resultText = "";
    [ObservableProperty] private bool _isStopping;
    [ObservableProperty] private bool _isStopVisible;
    [ObservableProperty] private bool _isStopEnabled;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private string _detailsText = "";
    [ObservableProperty] private bool _isDetailsVisible;
    [ObservableProperty] private ProposalReviewViewModel? _review;

    public AssistantViewModel(AssistantServices services)
    {
        _services = services; L = services.Strings;
        Actions = AgentPrompts.Actions.Select(preset => new ChipOptionViewModel(L, () => L["agent.action." + preset.Id], () => Prompt = preset.Prompt)).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    /// <summary>The starting points for a request; choosing one fills the prompt and marks it, until the text no longer equals it.</summary>
    public IReadOnlyList<ChipOptionViewModel> Actions { get; }
    /// <summary>The activity the running or finished job reported, in order.</summary>
    public ObservableCollection<AssistantStepViewModel> Steps { get; } = [];

    // ---- what is on screen -------------------------------------------------------------------------------------------------------------

    public bool HasProject => _project is not null;
    public bool IsConfigured => _services.Workspace.Environment.AgentConfigured;
    public string Title => _project is null ? L["agent.assistant"] : $"{L["agent.assistant"]} · {_project.Name}";
    public string StatusText => L[_statusKey];
    public bool IsPickerVisible => !HasProject;
    public bool IsPickerEmpty => Projects.Count == 0;
    /// <summary>Setup guidance: the assistant is not configured, so there is no request and no Run.</summary>
    public bool IsOffVisible => HasProject && !IsConfigured;
    public bool IsRequestVisible => HasProject && IsConfigured && !IsRunning;
    /// <summary>The assistant cannot be closed while a job holds the application.</summary>
    public bool CanClose => !IsRunning;

    /// <summary>What the context snapshot contains, computed from the project itself.</summary>
    public IReadOnlyList<string> SummaryLines
    {
        get
        {
            if (_project is not { } project) return [];
            var results = _jobs.Count(job => job.Status == AgentJobStatus.Completed && !string.IsNullOrWhiteSpace(job.ResultText));
            return
            [
                L["agent.context.metadata"],
                L.Format("agent.context.requirements", project.Requirements.Count),
                L.Format("agent.context.tasks", project.Tasks.Count, project.Milestones.Count),
                L.Format("agent.context.files", project.Requirements.Sum(item => item.Files.Count)),
                L.Format("agent.context.results", Math.Min(3, results))
            ];
        }
    }

    public string EndpointText => _services.Workspace.Environment.Endpoint is { Length: > 0 } url ? L.Format("agent.endpointShort", url) : _services.ConfigurationText();

    public bool CanRun => IsConsentChecked && !string.IsNullOrWhiteSpace(Prompt);
    public bool HasReview => Review is not null;
    public string OutcomeText => _outcomeStatus is { } status ? L.Enum(status) : L[_outcomeKey ?? "agent.ready"];

    partial void OnProjectsChanged(IReadOnlyList<AssistantProjectRowViewModel> value) => OnPropertyChanged(nameof(IsPickerEmpty));
    partial void OnReviewChanged(ProposalReviewViewModel? value) => OnPropertyChanged(nameof(HasReview));
    partial void OnIsConsentCheckedChanged(bool value) => OnPropertyChanged(nameof(CanRun));

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRequestVisible));
        OnPropertyChanged(nameof(CanClose));
    }

    partial void OnPromptChanged(string value)
    {
        // Any edit to the request withdraws consent: the user approved a different request.
        IsConsentChecked = false;
        OnPropertyChanged(nameof(CanRun));
        for (var index = 0; index < Actions.Count; index++) Actions[index].IsSelected = value == AgentPrompts.Actions[index].Prompt;
    }

    private void NotifyMode()
    {
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsPickerVisible));
        OnPropertyChanged(nameof(IsOffVisible));
        OnPropertyChanged(nameof(IsRequestVisible));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(SummaryLines));
        OnPropertyChanged(nameof(EndpointText));
    }

    private void SetStatus(string key, string token)
    {
        _statusKey = key; StatusToken = token;
        OnPropertyChanged(nameof(StatusText));
    }

    private void SetOutcome(string key)
    {
        _outcomeStatus = null; _outcomeKey = key;
        OnPropertyChanged(nameof(OutcomeText));
    }

    private void UpdateStatus()
    {
        if (!HasProject) SetStatus(IsConfigured ? "agent.state.ready" : "agent.state.off", IsConfigured ? "Accent" : "TextTertiary");
        else if (!IsConfigured) SetStatus("agent.state.off", "TextTertiary");
        else if (!IsRunning && !IsOutcomeVisible)
        {
            var pending = PendingCount() > 0;
            SetStatus(pending ? "agent.state.review" : "agent.state.consent", pending ? "Ai" : "Warning");
        }
    }

    private int PendingCount() => _jobs.Where(job => job.Status == AgentJobStatus.Completed).Sum(job => job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending));

    // ---- which project -----------------------------------------------------------------------------------------------------------------

    /// <summary>No project open: offer the projects the assistant can work on.</summary>
    public async Task ShowPickerAsync()
    {
        if (_services.Host.IsAgentRunning) return;
        _project = null; _jobs = []; _reviewJobId = null;
        ResetRequest();
        Projects = (await _services.Workspace.Projects.ListAsync()).Where(item => item.Status is ProjectStatus.Active or ProjectStatus.OnHold)
            .OrderBy(item => item.Name).Select(item => new AssistantProjectRowViewModel(item, _services)).ToList();
        NotifyMode(); UpdateStatus();
    }

    /// <summary>Shows or refreshes the assistant for a project. Reopening the same project keeps the request, preview and consent.</summary>
    public async Task ShowProjectAsync(Project project)
    {
        var same = _project?.Id == project.Id;
        _project = project;
        _jobs = await _services.Workspace.Agents.HistoryAsync(project.Id);
        if (!same)
        {
            ResetRequest();
            var latest = _jobs.OrderByDescending(job => job.CreatedAt).FirstOrDefault();
            _reviewJobId = latest is { Status: AgentJobStatus.Completed } && latest.Proposals.Count > 0 ? latest.Id : null;
            if (_reviewJobId is not null) ShowFinished(latest!);
        }
        else if (_approvedContext.Length > 0 && _approvedContext != AgentService.BuildContext(project, _jobs)) ResetConsent();
        RenderReview(); NotifyMode(); UpdateStatus();
    }

    /// <summary>Opens the review tray of a finished job of the open project.</summary>
    public async Task ReviewAsync(string jobId)
    {
        if (_project is null) return;
        _jobs = await _services.Workspace.Agents.HistoryAsync(_project.Id);
        var job = _jobs.FirstOrDefault(item => item.Id == jobId);
        if (job is null) return;
        _reviewJobId = job.Id;
        ShowFinished(job); RenderReview(); NotifyMode();
    }

    /// <summary>Forgets the request, preview, consent and the outcome on screen: a different project starts clean.</summary>
    private void ResetRequest()
    {
        Prompt = "";
        ResetConsent();
        IsOutcomeVisible = false; IsFailed = false; IsStopVisible = false; IsStopping = false;
        Steps.Clear(); ResultText = ""; DetailsText = ""; IsDetailsVisible = false;
        Review = null;
    }

    private void ResetConsent()
    {
        _approvedContext = ""; PreviewText = ""; IsPreviewVisible = false;
        IsConsentChecked = false; IsConsentEnabled = false;
    }

    // ---- request, preview, consent -----------------------------------------------------------------------------------------------------

    [RelayCommand]
    private void Close() => _services.Host.SetAssistantOpen(false);

    /// <summary>Shows the exact context that would be sent, and allows consent to it.</summary>
    [RelayCommand]
    private void Preview()
    {
        if (_project is null) return;
        _approvedContext = AgentService.BuildContext(_project, _jobs);
        PreviewText = _approvedContext; IsPreviewVisible = true;
        IsConsentEnabled = true; IsConsentChecked = false;
    }

    // ---- a run -------------------------------------------------------------------------------------------------------------------------

    [RelayCommand]
    private async Task RunAsync()
    {
        if (_project is null) return;
        var project = _project; var previousJobs = _jobs;
        if (!IsConsentChecked || string.IsNullOrWhiteSpace(Prompt) || _approvedContext != AgentService.BuildContext(project, previousJobs))
        {
            IsConsentChecked = false;
            _services.Host.ShowError("validation.contextConsentRequired");
            return;
        }
        IsConsentChecked = false;
        _reviewJobId = null; Review = null; IsFailed = false; IsStopping = false;
        Steps.Clear(); ResultText = ""; DetailsText = ""; IsDetailsVisible = false;
        SetOutcome("agent.running");
        IsOutcomeVisible = true; IsStopVisible = true; IsStopEnabled = true;
        SetStatus("agent.running", "Ai");
        var prompt = Prompt;
        var accepting = true;
        // Progress<T> posts back to the thread that created it (the UI thread), so these callbacks never race the state above.
        var progress = new Progress<AgentEvent>(item =>
        {
            if (!accepting) return;
            if (item.Kind == AgentEventKind.ResultDelta) ResultText += item.Message;
            else if (item.Kind == AgentEventKind.Activity) AddStep(item);
            else DetailsText += item.Message + "\n";
        });
        await _services.Host.RunAgentAsync(async token =>
        {
            IsRunning = true;
            try
            {
                AgentJob job;
                try { job = await _services.Workspace.Agents.RunAsync(project, prompt, progress, token, previousJobs, _services.Locale.AgentResponseLanguage); }
                catch
                {
                    SetOutcome("agent.outcomeNotRecorded");
                    SetStatus("agent.state.failed", "Error");
                    throw;
                }
                accepting = false;
                FinishSteps();
                if (job.Proposals.Count > 0 && job.Status == AgentJobStatus.Completed) _reviewJobId = job.Id;
                // The outcome and the review tray appear together, so "Completed" is never shown without its proposals.
                _jobs = [.. previousJobs.Where(item => item.Id != job.Id), job];
                RenderReview();
                ShowFinished(job);
            }
            finally { accepting = false; }
        }, async () =>
        {
            IsRunning = false; IsStopVisible = false; IsStopping = false;
            ResetConsent();
            _jobs = await _services.Workspace.Agents.HistoryAsync(project.Id);
            RenderReview(); NotifyMode();
            await _services.Host.RefreshProjectAsync();
        });
    }

    private void AddStep(AgentEvent item)
    {
        if (Steps.Count > 0) Steps[^1].IsCurrent = false;
        Steps.Add(new AssistantStepViewModel(L, item, isCurrent: true));
    }

    private void FinishSteps()
    {
        foreach (var step in Steps) step.IsCurrent = false;
    }

    /// <summary>Asks the runtime to stop. The job only ends as Cancelled once the runtime confirms it.</summary>
    [RelayCommand]
    private void Stop()
    {
        IsStopEnabled = false; IsStopping = true;
        SetStatus("agent.state.stopping", "Warning");
        SetOutcome("agent.state.stopping");
        _services.Host.CancelRun();
    }

    [RelayCommand]
    private void ToggleDetails() => IsDetailsVisible = !IsDetailsVisible;

    /// <summary>Shows a finished job's truthful outcome; a failure is never presented as success.</summary>
    private void ShowFinished(AgentJob job)
    {
        IsOutcomeVisible = true; IsStopVisible = false; IsStopping = false;
        _outcomeStatus = job.Status;
        OnPropertyChanged(nameof(OutcomeText));
        ResultText = job.ResultText;
        if (!string.IsNullOrWhiteSpace(job.Error) && !DetailsText.Contains(job.Error)) DetailsText += job.Error;
        IsFailed = job.Status == AgentJobStatus.Failed;
        var pending = job.Proposals.Count(proposal => proposal.ReviewStatus == ProposalReviewStatus.Pending);
        var (key, token) = job.Status switch
        {
            AgentJobStatus.Completed when pending > 0 => ("agent.state.review", "Ai"),
            AgentJobStatus.Completed => ("agent.status.completed", "Success"),
            AgentJobStatus.Cancelled => ("agent.status.cancelled", "TextSecondary"),
            AgentJobStatus.Failed => ("agent.state.failed", "Error"),
            _ => ("agent.status.interrupted", "Warning")
        };
        SetStatus(key, token);
    }

    /// <summary>The review tray for the job in view, plus the project's committed tasks, kept visually apart.</summary>
    private void RenderReview()
    {
        var job = _jobs.FirstOrDefault(item => item.Id == _reviewJobId);
        Review = _project is null || job is null || job.Proposals.Count == 0 ? null : new ProposalReviewViewModel(job, _project, _services);
    }
}
