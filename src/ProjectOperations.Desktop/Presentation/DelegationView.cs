using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Templates;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

internal sealed class DelegationView : PresentationView
{
    private TextBox? _prompt;
    public DelegationView(PresentationContext context) : base(context) { Spacing = 16; }
    /// <summary>Quick actions only populate the request. Context preview and consent still gate every run.</summary>
    public void SetPrompt(string prompt) { if (_prompt is not null) _prompt.Text = prompt; }
    public async Task LoadAsync(Project project)
    {
        var previousJobs = await _agents.HistoryAsync(project.Id);
        var panel = new StackPanel { Spacing = 16 }; var request = Column();
        var configuration = Label(() => _configurationText?.Invoke() ?? _configuration, "Caption", "TextSecondary");
        var prompt = Input("", true); prompt.Name = "AgentPrompt"; prompt.MinHeight = 84; _prompt = prompt; var presets = new AdaptiveGrid { MinItemWidth = 220, Gap = 8 };
        foreach (var preset in AgentPrompts.Actions)
            presets.Children.Add(new AiActionButton(Context, preset.Id, () => { prompt.Text = preset.Prompt; return Task.CompletedTask; }));
        request.Children.Add(configuration);
        request.Children.Add(presets);
        Field(request, "agent.request", prompt);
        var preview = Readable(""); preview.FlowDirection = FlowDirection.LeftToRight; var consent = new CheckBox
        {
            Content = Label("agent.consent"),
            IsEnabled = false
        };
        var context = "";
        var run = new Button { IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left }; Bind(run, control => control.Content = T("agent.run"));
        run.Classes.Add("primary");
        request.Children.Add(Action("agent.preview", () =>
        {
            context = AgentService.BuildContext(project, previousJobs); preview.Text = context; consent.IsEnabled = true; consent.IsChecked = false;
            return Task.CompletedTask;
        }));
        preview.Background = PresentationTheme.Brush("BackgroundMuted"); preview.FontSize = 11;
        request.Children.Add(preview); request.Children.Add(consent); request.Children.Add(run);
        consent.IsCheckedChanged += (_, _) => run.IsEnabled = consent.IsChecked == true && !string.IsNullOrWhiteSpace(prompt.Text);
        prompt.TextChanged += (_, _) => { consent.IsChecked = false; };
        var requestCard = new SectionCard(Context, () => "✦  " + T("agent.title")) { Name = "DelegationRequestCard" }; requestCard.Body.Children.Add(request); panel.Children.Add(requestCard);
        var outcomeKey = "agent.ready";
        Func<string> outcomeText = () => T(outcomeKey);
        var outcome = Label(() => outcomeText(), "HeadingSmall"); var activity = Readable(""); var result = Readable(""); var details = Readable("");
        var activities = new List<AgentEvent>();
        string ActivityText() => string.Concat(activities.Select(item => (item.ActivityKey is null ? item.Message : T(item.ActivityKey)) + "\n"));
        Bind(activity, control => control.Text = ActivityText());
        details.FlowDirection = FlowDirection.LeftToRight;
        var detailToggle = new CheckBox(); Bind(detailToggle, control => control.Content = T("agent.showDetails"));
        details.IsVisible = false;
        detailToggle.IsCheckedChanged += (_, _) => details.IsVisible = detailToggle.IsChecked == true;
        var stop = new Button { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, Name = "StopButton" }; stop.Classes.Add("danger"); Bind(stop, control => control.Content = T("agent.stop"));
        stop.Click += (_, _) => { outcomeKey = "agent.stopping"; outcome.Text = outcomeText(); stop.IsEnabled = false; Context.CancelRun(); };
        var activityCard = new SectionCard(Context, "agent.activity");
        activityCard.Body.Children.Add(outcome); activityCard.Body.Children.Add(stop); activityCard.Body.Children.Add(activity);
        activityCard.Body.Children.Add(Label("agent.result", "Label", "TextSecondary")); activityCard.Body.Children.Add(result); activityCard.Body.Children.Add(detailToggle); activityCard.Body.Children.Add(details);
        panel.Children.Add(activityCard);
        var history = Column(); panel.Children.Add(Heading("history.title", "HeadingSmall")); panel.Children.Add(history);
        await FillHistoryAsync(project, history);
        run.Click += async (_, _) =>
        {
            if (consent.IsChecked != true || string.IsNullOrWhiteSpace(prompt.Text) || context != AgentService.BuildContext(project, previousJobs))
            { consent.IsChecked = false; ShowError("validation.contextConsentRequired"); return; }
            consent.IsChecked = false; activities.Clear(); activity.Text = ""; result.Text = ""; details.Text = "";
            outcomeText = () => T(outcomeKey);
            outcomeKey = "agent.running"; outcome.Text = outcomeText(); stop.IsVisible = true; stop.IsEnabled = true;
            stop.BringIntoView();
            var acceptingProgress = true;
            var progress = new Progress<AgentEvent>(item =>
            {
                if (!acceptingProgress) return;
                if (item.Kind == AgentEventKind.ResultDelta) result.Text += item.Message;
                else if (item.Kind == AgentEventKind.Activity) { activities.Add(item); activity.Text = ActivityText(); }
                else details.Text += item.Message + "\n";
            });
            await Context.RunAsync(this, request, history, stop, async token =>
            {
                try
                {
                    AgentJob job;
                    try { job = await _agents.RunAsync(project, prompt.Text!, progress, token, previousJobs, _locale.AgentResponseLanguage); }
                    catch { outcomeKey = "agent.outcomeNotRecorded"; outcome.Text = outcomeText(); throw; }
                    acceptingProgress = false;
                    previousJobs = previousJobs.Append(job).ToList();
                    outcomeText = () => EnumText(job.Status); outcome.Text = outcomeText(); result.Text = job.ResultText;
                    if (job.Status == AgentJobStatus.Failed) ShowError("validation.agentFailed");
                }
                finally { acceptingProgress = false; }
            }, async () =>
            {
                consent.IsEnabled = false; run.IsEnabled = false; context = "";
                await FillHistoryAsync(project, history);
            });
        };
        Children.Add(panel);
    }

    private async Task FillHistoryAsync(Project project, StackPanel history)
    {
        history.Children.Clear();
        var jobs = await _agents.HistoryAsync(project.Id);
        if (jobs.Count == 0) history.Children.Add(Label("history.empty"));
        foreach (var job in jobs.OrderByDescending(job => job.CreatedAt))
        {
            var card = Column(); card.Children.Add(Heading(() => $"{EnumText(job.Status)} · {Due(job.CreatedAt)}", "HeadingSmall"));
            card.Children.Add(Label(() => job.FinishedAt.HasValue ? F("history.finished", Due(job.FinishedAt)) : T("history.noOutcome"), "Caption", "TextSecondary"));
            if (job.Status == AgentJobStatus.Interrupted) card.Children.Add(Label("history.interrupted"));
            card.Children.Add(Readable(job.Prompt)); card.Children.Add(Readable(job.ResultText));
            if (!string.IsNullOrWhiteSpace(job.Error))
            { var technical = Readable(job.Error); technical.FlowDirection = FlowDirection.LeftToRight; var expander = new Expander { Content = technical }; Bind(expander, control => control.Header = T("history.outcomeDetails")); card.Children.Add(expander); }
            var selections = new List<(Guid Id, CheckBox Check)>();
            foreach (var proposal in job.Proposals)
            {
                var check = new CheckBox { Content = Label(() => $"{proposal.Title} · {Due(proposal.DueAt)} · {EnumText(proposal.ReviewStatus)}\n{proposal.Description}"), IsChecked = false, IsEnabled = job.Status == AgentJobStatus.Completed && proposal.ReviewStatus == ProposalReviewStatus.Pending };
                card.Children.Add(check); selections.Add((proposal.Id, check));
            }
            if (selections.Count > 0)
            {
                var actions = new WrapPanel();
                actions.Children.Add(Action("proposal.addSelected", async () =>
                {
                    await _agents.ApproveAsync(job, selections.Where(s => s.Check.IsChecked == true).Select(s => s.Id));
                    await OpenProjectAsync(project.Id, 3);
                }));
                actions.Children.Add(Action("proposal.rejectSelected", async () =>
                {
                    await _agents.RejectAsync(job, selections.Where(s => s.Check.IsChecked == true).Select(s => s.Id));
                    await FillHistoryAsync(project, history);
                }));
                card.Children.Add(actions);
            }
            history.Children.Add(new Border { Background = PresentationTheme.Brush("BackgroundCard"), BorderBrush = PresentationTheme.Brush("BorderDefault"), BorderThickness = new Thickness(1), Padding = new Thickness(16), CornerRadius = PresentationTheme.Radius(PresentationTheme.RadiusLarge), Child = card });
        }
    }
}
