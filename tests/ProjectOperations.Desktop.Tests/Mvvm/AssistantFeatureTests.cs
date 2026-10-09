using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Assistant;
using ProjectOperations.Desktop.Shell;
using Xunit;
using static ProjectOperations.Desktop.Tests.Mvvm.AssistantScenario;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>
/// The assistant panel as a plain view-model: picking a project, the request / preview / consent steps, a run with live activity and Stop,
/// and the proposal tray. Every run needs a request, a preview of the exact context and explicit consent; proposals are never committed
/// until approved here.
/// </summary>
public sealed class AssistantFeatureTests
{
    private static void PreviewAndConsent(AssistantScenario scenario, string prompt = "Summarize supplied facts")
    {
        scenario.Assistant.Prompt = prompt;
        scenario.Assistant.PreviewCommand.Execute(null);
        scenario.Assistant.IsConsentChecked = true;
    }

    private static async Task<AgentJob> FinishedJobAsync(AssistantScenario scenario, Project project, params TaskProposal[] proposals)
    {
        scenario.Runtime.Result = new AgentResult { Text = "Synthetic analysis", Proposals = [.. proposals] };
        scenario.Runtime.Release.TrySetResult();
        return await scenario.Agents.RunAsync(project, "Find gaps", new Progress<AgentEvent>(), CancellationToken.None);
    }

    // ---- no project open ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Without_a_project_the_picker_lists_active_and_on_hold_projects_by_name()
    {
        using var scenario = await CreateAsync();
        await scenario.AddProjectAsync("Zeta", company: "");
        await scenario.AddProjectAsync("Alpha", ProjectStatus.OnHold, "Alpha Co");
        await scenario.AddProjectAsync("Finished", ProjectStatus.Completed);

        await scenario.Assistant.ShowPickerAsync();

        var assistant = scenario.Assistant;
        Assert.False(assistant.HasProject);
        Assert.True(assistant.IsPickerVisible);
        Assert.False(assistant.IsRequestVisible);
        Assert.Equal("Assistant", assistant.Title);
        Assert.Equal(["Alpha", "Zeta"], assistant.Projects.Select(row => row.Name));
        Assert.Equal(["Alpha Co", ""], assistant.Projects.Select(row => row.Company));
        Assert.Equal([true, false], assistant.Projects.Select(row => row.HasCompany));
        Assert.False(assistant.IsPickerEmpty);
        Assert.Equal("Ready", assistant.StatusText);
        Assert.Equal("Accent", assistant.StatusToken);
    }

    [Fact]
    public async Task An_empty_workspace_shows_the_empty_picker_text_and_an_unconfigured_one_says_so()
    {
        using var scenario = await CreateAsync(configured: false);

        await scenario.Assistant.ShowPickerAsync();

        Assert.True(scenario.Assistant.IsPickerEmpty);
        Assert.Empty(scenario.Assistant.Projects);
        Assert.Equal("Not configured", scenario.Assistant.StatusText);
        Assert.Equal("TextTertiary", scenario.Assistant.StatusToken);
    }

    [Fact]
    public async Task Choosing_a_project_in_the_picker_opens_it_on_the_Delegate_tab_under_the_busy_state()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync("Nova");
        await scenario.Assistant.ShowPickerAsync();

        await scenario.Assistant.Projects.Single().OpenCommand.ExecuteAsync(null);

        Assert.Equal(new ProjectRoute(project.Id, ProjectTab.Delegate), scenario.Navigator.Current);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task The_picker_is_left_alone_while_a_job_holds_the_application()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        await scenario.OpenAsync(project);
        scenario.Host.ForceRunning = true;

        await scenario.Assistant.ShowPickerAsync();

        Assert.True(scenario.Assistant.HasProject);
    }

    // ---- an open project ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_open_project_names_itself_and_outlines_exactly_what_the_context_contains()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync("Nova");
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Chase deck" });
        project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "IC" });
        project.Requirements[0].Files.Add(new ProjectFile { FileName = "deck.pdf", Path = "/synthetic/deck.pdf" });
        await scenario.Projects.SaveAsync(project);
        var loaded = await scenario.OpenAsync(project);

        var assistant = scenario.Assistant;
        Assert.True(assistant.HasProject);
        Assert.Equal("Assistant · Nova", assistant.Title);
        Assert.False(assistant.IsPickerVisible);
        Assert.True(assistant.IsRequestVisible);
        Assert.False(assistant.IsOffVisible);
        Assert.Equal("Waiting for consent", assistant.StatusText);
        Assert.Equal("Warning", assistant.StatusToken);
        Assert.Equal(["Project metadata, notes and current state", $"{loaded.Requirements.Count} requirement values and statuses",
            "1 tasks and 1 milestones with dates", "1 file paths (not file content)", "0 earlier results, labeled unverified (up to 3)"], assistant.SummaryLines);
        Assert.Equal("Agent endpoint · http://127.0.0.1:4096", assistant.EndpointText);
    }

    [Fact]
    public async Task Without_an_endpoint_the_context_card_falls_back_to_the_configuration_text()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        var services = scenario.Services with { Workspace = new WorkspaceSession(scenario.Projects, scenario.Agents, new DesktopEnvironment(), scenario.Workspace.Calendar) };
        var assistant = new AssistantViewModel(services);
        await assistant.ShowProjectAsync(project);

        Assert.Equal("Synthetic test runtime", assistant.EndpointText);
    }

    [Fact]
    public async Task An_unconfigured_assistant_offers_setup_guidance_and_no_request()
    {
        using var scenario = await CreateAsync(configured: false);
        var project = await scenario.AddProjectAsync();
        await scenario.OpenAsync(project);

        var assistant = scenario.Assistant;
        Assert.True(assistant.IsOffVisible);
        Assert.False(assistant.IsRequestVisible);
        Assert.Equal("Not configured", assistant.StatusText);
        Assert.Equal("TextTertiary", assistant.StatusToken);
        Assert.Equal(0, scenario.Runtime.Calls);
    }

    [Fact]
    public async Task Starting_points_fill_the_request_and_mark_themselves_until_the_text_is_edited_away()
    {
        using var scenario = await CreateAsync();
        await scenario.OpenAsync(await scenario.AddProjectAsync());
        var assistant = scenario.Assistant;

        Assert.Equal(AgentPrompts.Actions.Count, assistant.Actions.Count);
        Assert.Equal(AgentPrompts.Actions.Select(action => scenario.Strings["agent.action." + action.Id]), assistant.Actions.Select(action => action.Label));
        foreach (var (preset, chip) in AgentPrompts.Actions.Zip(assistant.Actions))
        {
            chip.SelectCommand.Execute(null);
            Assert.Equal(preset.Prompt, assistant.Prompt);
            Assert.Equal([chip], assistant.Actions.Where(action => action.IsSelected));
            Assert.False(assistant.CanRun);
        }

        assistant.Prompt += " Also check the cap table.";

        Assert.DoesNotContain(assistant.Actions, action => action.IsSelected);
        Assert.Equal(0, scenario.Runtime.Calls);
    }

    // ---- preview, consent and the guard ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Running_needs_a_request_a_preview_of_the_exact_context_and_consent_and_any_edit_withdraws_consent()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.OpenAsync(await scenario.AddProjectAsync("Nova"));
        var assistant = scenario.Assistant;
        Assert.False(assistant.CanRun);
        Assert.False(assistant.IsConsentEnabled);
        Assert.False(assistant.IsPreviewVisible);

        assistant.Prompt = "Summarize supplied facts";
        assistant.PreviewCommand.Execute(null);

        Assert.Equal(AgentService.BuildContext(project, []), assistant.PreviewText);
        Assert.True(assistant.IsPreviewVisible);
        Assert.True(assistant.IsConsentEnabled);
        Assert.False(assistant.IsConsentChecked);
        Assert.False(assistant.CanRun);

        assistant.IsConsentChecked = true;
        Assert.True(assistant.CanRun);

        assistant.Prompt = "A different request";
        Assert.False(assistant.IsConsentChecked);
        Assert.False(assistant.CanRun);
        Assert.True(assistant.IsPreviewVisible);
        Assert.Equal(0, scenario.Runtime.Calls);
    }

    [Fact]
    public async Task Running_without_consent_reports_it_and_never_reaches_the_runtime()
    {
        using var scenario = await CreateAsync();
        await scenario.OpenAsync(await scenario.AddProjectAsync());
        scenario.Assistant.Prompt = "Summarize";

        await scenario.Assistant.RunCommand.ExecuteAsync(null);

        Assert.Equal(["validation.contextConsentRequired"], scenario.Host.Errors);
        Assert.Equal(0, scenario.Runtime.Calls);
        Assert.False(scenario.Assistant.IsOutcomeVisible);

        scenario.Assistant.PreviewCommand.Execute(null);
        scenario.Assistant.Prompt = "Summarize again";
        scenario.Assistant.IsConsentChecked = true;
        scenario.Assistant.Prompt = "";
        await scenario.Assistant.RunCommand.ExecuteAsync(null);
        Assert.Equal(2, scenario.Host.Errors.Count);
        Assert.Equal(0, scenario.Runtime.Calls);
    }

    [Fact]
    public async Task A_project_that_changed_after_the_preview_withdraws_consent_and_hides_the_stale_preview_text()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.OpenAsync(await scenario.AddProjectAsync());
        PreviewAndConsent(scenario);
        Assert.True(scenario.Assistant.CanRun);
        project.Notes = "New facts since the preview";
        await scenario.Projects.SaveAsync(project);

        await scenario.OpenAsync(project);

        var assistant = scenario.Assistant;
        Assert.False(assistant.IsConsentChecked);
        Assert.False(assistant.IsConsentEnabled);
        Assert.False(assistant.IsPreviewVisible);
        Assert.Equal("", assistant.PreviewText);
        Assert.Equal("Summarize supplied facts", assistant.Prompt);
    }

    [Fact]
    public async Task Reopening_the_same_project_keeps_the_request_and_consent_but_another_project_starts_clean()
    {
        using var scenario = await CreateAsync();
        var first = await scenario.OpenAsync(await scenario.AddProjectAsync("First"));
        PreviewAndConsent(scenario);

        await scenario.OpenAsync(first);
        Assert.Equal("Summarize supplied facts", scenario.Assistant.Prompt);
        Assert.True(scenario.Assistant.IsConsentChecked);
        Assert.True(scenario.Assistant.IsPreviewVisible);

        await scenario.OpenAsync(await scenario.AddProjectAsync("Second"));
        Assert.Equal("", scenario.Assistant.Prompt);
        Assert.False(scenario.Assistant.IsConsentChecked);
        Assert.False(scenario.Assistant.IsPreviewVisible);
        Assert.Equal("Assistant · Second", scenario.Assistant.Title);
    }

    [Fact]
    public async Task The_language_switch_relabels_the_panel_in_place_and_keeps_the_prompt_preview_and_consent()
    {
        using var scenario = await CreateAsync();
        await scenario.OpenAsync(await scenario.AddProjectAsync("Nova"));
        PreviewAndConsent(scenario);
        var preview = scenario.Assistant.PreviewText;
        var changes = new List<string?>();
        scenario.Assistant.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        scenario.Locale.SetLanguage("fa");

        var assistant = scenario.Assistant;
        Assert.Contains(string.Empty, changes);
        Assert.Equal(scenario.Strings["agent.assistant"] + " · Nova", assistant.Title);
        Assert.NotEqual("Assistant · Nova", assistant.Title);
        Assert.Equal("Summarize supplied facts", assistant.Prompt);
        Assert.Equal(preview, assistant.PreviewText);
        Assert.True(assistant.IsConsentChecked);
        Assert.Equal(scenario.Strings["agent.action." + AgentPrompts.Actions[0].Id], assistant.Actions[0].Label);
    }

    [Fact]
    public async Task Closing_is_offered_until_a_job_starts()
    {
        using var scenario = await CreateAsync();
        await scenario.OpenAsync(await scenario.AddProjectAsync());
        Assert.True(scenario.Assistant.CanClose);

        scenario.Assistant.CloseCommand.Execute(null);

        Assert.Equal([false], scenario.Host.OpenStates);
    }

    // ---- a run ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_completed_run_streams_activity_and_the_result_then_shows_its_proposals_in_the_tray()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.OpenAsync(await scenario.AddProjectAsync("Nova"));
        scenario.Runtime.Result = new AgentResult { Text = "Synthetic analysis", Proposals = [new TaskProposal { Title = "Request deck" }, new TaskProposal { Title = "Prepare briefing" }] };
        PreviewAndConsent(scenario);
        var assistant = scenario.Assistant;

        var run = assistant.RunCommand.ExecuteAsync(null);
        await UntilAsync(() => scenario.Runtime.Calls == 1 && assistant.ResultText.Contains("Synthetic live delta") && assistant.Steps.Count == 1, "a running job with its first activity");

        Assert.True(assistant.IsRunning);
        Assert.True(scenario.Host.IsLocked);
        Assert.False(assistant.IsRequestVisible);
        Assert.False(assistant.CanClose);
        Assert.True(assistant.IsOutcomeVisible);
        Assert.True(assistant.IsStopVisible);
        Assert.True(assistant.IsStopEnabled);
        Assert.False(assistant.IsConsentChecked);
        Assert.Equal("Running", assistant.OutcomeText);
        Assert.Equal("Running", assistant.StatusText);
        Assert.Equal("Ai", assistant.StatusToken);
        Assert.Equal("Reviewing synthetic project facts", assistant.Steps.Single().Text);
        Assert.True(assistant.Steps.Single().IsCurrent);
        Assert.False(assistant.HasReview);

        scenario.Runtime.Release.SetResult();
        await run;

        Assert.False(assistant.IsRunning);
        Assert.False(scenario.Host.IsLocked);
        Assert.Equal("Completed", assistant.OutcomeText);
        Assert.Equal("Synthetic analysis", assistant.ResultText);
        Assert.False(assistant.IsStopVisible);
        Assert.False(assistant.IsFailed);
        Assert.False(assistant.Steps.Single().IsCurrent);
        Assert.True(assistant.IsRequestVisible);
        Assert.True(assistant.CanClose);
        Assert.False(assistant.IsConsentChecked);
        Assert.False(assistant.IsConsentEnabled);
        Assert.False(assistant.IsPreviewVisible);
        Assert.Equal("Proposals ready", assistant.StatusText);
        Assert.Equal(1, scenario.Host.Refreshes);
        var review = Assert.IsType<ProposalReviewViewModel>(assistant.Review);
        Assert.Equal("Review proposals · 2 pending", review.TrayTitle);
        Assert.Equal(["Request deck", "Prepare briefing"], review.Items.Select(item => item.Text.Split(" · ")[0]));
        Assert.All(review.Items, item => { Assert.False(item.IsSelected); Assert.True(item.IsEnabled); Assert.Contains(" · Pending", item.Text); });
        Assert.Empty((await scenario.ReloadAsync(project)).Tasks);
        Assert.Single(await scenario.Agents.HistoryAsync(project.Id));
    }

    [Fact]
    public async Task The_run_carries_the_request_the_context_the_user_approved_and_the_response_language()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.OpenAsync(await scenario.AddProjectAsync());
        scenario.Locale.SetLanguage("fa");
        scenario.Runtime.Release.SetResult();
        PreviewAndConsent(scenario, "Check the data room");
        var approved = scenario.Assistant.PreviewText;

        await scenario.Assistant.RunCommand.ExecuteAsync(null);

        Assert.Equal("Check the data room", scenario.Runtime.LastRequest!.Prompt);
        Assert.Equal(approved, scenario.Runtime.LastRequest.Context);
        Assert.Equal(AgentPrompts.BuildOutputInstructions(AgentResponseLanguage.Persian), scenario.Runtime.LastRequest.SystemInstructions);
        Assert.Equal(project.Id, scenario.Runtime.LastRequest.ProjectId);
    }

    [Fact]
    public async Task Activity_events_with_a_key_are_shown_in_the_current_language()
    {
        using var scenario = await CreateAsync();
        await scenario.OpenAsync(await scenario.AddProjectAsync());
        var step = new AssistantStepViewModel(scenario.Strings, new AgentEvent { Kind = AgentEventKind.Activity, Message = "raw", ActivityKey = "agent.activity.readingProjectInformation" }, isCurrent: true);

        Assert.Equal("Reading project information", step.Text);
        scenario.Locale.SetLanguage("fa");
        Assert.Equal(scenario.Strings["agent.activity.readingProjectInformation"], step.Text);
        Assert.NotEqual("Reading project information", step.Text);
        Assert.Equal("raw", new AssistantStepViewModel(scenario.Strings, new AgentEvent { Message = "raw" }, isCurrent: false).Text);
    }

    [Fact]
    public async Task Stop_asks_the_runtime_to_cancel_and_the_job_ends_cancelled_only_once_the_runtime_confirms()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.OpenAsync(await scenario.AddProjectAsync());
        PreviewAndConsent(scenario);
        var assistant = scenario.Assistant;
        var run = assistant.RunCommand.ExecuteAsync(null);
        await UntilAsync(() => scenario.Runtime.Calls == 1 && assistant.IsStopVisible, "a running job");

        assistant.StopCommand.Execute(null);

        await UntilAsync(() => scenario.Runtime.CancelRequested.Task.IsCompleted, "a cancellation request");
        Assert.True(assistant.IsStopping);
        Assert.False(assistant.IsStopEnabled);
        Assert.Equal("Stop requested", assistant.OutcomeText);
        Assert.Equal("Stop requested", assistant.StatusText);
        Assert.Equal("Warning", assistant.StatusToken);
        Assert.True(scenario.Host.IsLocked);
        Assert.False(assistant.IsRequestVisible);
        Assert.False(run.IsCompleted);

        scenario.Runtime.Release.SetResult();
        await run;

        Assert.Equal("Cancelled", assistant.OutcomeText);
        Assert.Equal("Cancelled", assistant.StatusText);
        Assert.Equal("TextSecondary", assistant.StatusToken);
        Assert.False(assistant.IsStopping);
        Assert.False(assistant.IsStopVisible);
        Assert.False(assistant.HasReview);
        Assert.False(assistant.IsConsentEnabled);
        Assert.True(assistant.IsRequestVisible);
        Assert.Equal(AgentJobStatus.Cancelled, (await scenario.Agents.HistoryAsync(project.Id)).Single().Status);
    }

    [Fact]
    public async Task A_failed_run_is_shown_as_failed_with_its_error_in_the_technical_details_and_never_as_success()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.OpenAsync(await scenario.AddProjectAsync());
        scenario.Runtime.Failure = new InvalidOperationException("Synthetic transport unavailable");
        scenario.Runtime.Release.SetResult();
        PreviewAndConsent(scenario);

        await scenario.Assistant.RunCommand.ExecuteAsync(null);

        var assistant = scenario.Assistant;
        Assert.Equal("Failed", assistant.OutcomeText);
        Assert.Equal("Failed", assistant.StatusText);
        Assert.Equal("Error", assistant.StatusToken);
        Assert.True(assistant.IsFailed);
        Assert.False(assistant.HasReview);
        Assert.Contains("Synthetic transport unavailable", assistant.DetailsText);
        Assert.False(assistant.IsDetailsVisible);
        Assert.Equal(AgentJobStatus.Failed, (await scenario.Agents.HistoryAsync(project.Id)).Single().Status);

        assistant.ToggleDetailsCommand.Execute(null);
        Assert.True(assistant.IsDetailsVisible);
        assistant.ToggleDetailsCommand.Execute(null);
        Assert.False(assistant.IsDetailsVisible);
    }

    [Fact]
    public async Task A_completed_run_without_proposals_is_just_completed()
    {
        using var scenario = await CreateAsync();
        await scenario.OpenAsync(await scenario.AddProjectAsync());
        scenario.Runtime.Release.SetResult();
        PreviewAndConsent(scenario);

        await scenario.Assistant.RunCommand.ExecuteAsync(null);

        Assert.Equal("Completed", scenario.Assistant.StatusText);
        Assert.Equal("Success", scenario.Assistant.StatusToken);
        Assert.False(scenario.Assistant.HasReview);
    }

    [Fact]
    public async Task A_second_run_clears_the_previous_outcome_and_sends_the_earlier_result_as_unverified_history()
    {
        using var scenario = await CreateAsync();
        await scenario.OpenAsync(await scenario.AddProjectAsync());
        scenario.Runtime.Result = new AgentResult { Text = "First answer" };
        scenario.Runtime.Release.SetResult();
        PreviewAndConsent(scenario, "First request");
        await scenario.Assistant.RunCommand.ExecuteAsync(null);
        Assert.Equal("First answer", scenario.Assistant.ResultText);

        scenario.Runtime.Release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        PreviewAndConsent(scenario, "Second request");
        Assert.Contains("First answer", scenario.Assistant.PreviewText);
        Assert.Equal("1 earlier results, labeled unverified (up to 3)", scenario.Assistant.SummaryLines.Last());
        var run = scenario.Assistant.RunCommand.ExecuteAsync(null);
        await UntilAsync(() => scenario.Runtime.Calls == 2, "the second run");

        Assert.DoesNotContain("First answer", scenario.Assistant.ResultText);
        Assert.Single(scenario.Assistant.Steps);
        scenario.Runtime.Release.SetResult();
        await run;
        Assert.Equal(2, (await scenario.Agents.HistoryAsync((await scenario.Projects.ListAsync()).Single().Id)).Count);
    }

    // ---- the proposal tray ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Approving_the_selected_proposals_commits_only_those_and_leaves_the_rest_pending()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        var job = await FinishedJobAsync(scenario, project, new TaskProposal { Title = "Request deck" }, new TaskProposal { Title = "Prepare briefing" });
        await scenario.OpenAsync(project);
        var review = Assert.IsType<ProposalReviewViewModel>(scenario.Assistant.Review);

        review.Items[0].IsSelected = true;
        await review.ApproveCommand.ExecuteAsync(null);

        var stored = await scenario.ReloadAsync(project);
        Assert.Equal("Request deck", Assert.Single(stored.Tasks).Title);
        Assert.Equal(1, scenario.Host.Runs);
        Assert.Equal(1, scenario.Host.Refreshes);
        var after = Assert.IsType<ProposalReviewViewModel>(scenario.Assistant.Review);
        Assert.NotSame(review, after);
        Assert.Equal("Review proposals · 1 pending", after.TrayTitle);
        Assert.Contains(" · Approved", after.Items[0].Text);
        Assert.False(after.Items[0].IsEnabled);
        Assert.Contains(" · Pending", after.Items[1].Text);
        Assert.False(after.Items[1].IsSelected);
        Assert.Equal(["Request deck"], after.CommittedTasks.Select(task => task.Title));
        Assert.Equal(job.Id, (await scenario.Agents.HistoryAsync(project.Id)).Single().Id);
    }

    [Fact]
    public async Task Rejecting_the_selected_proposals_commits_nothing()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        await FinishedJobAsync(scenario, project, new TaskProposal { Title = "Request deck" }, new TaskProposal { Title = "Prepare briefing" });
        await scenario.OpenAsync(project);
        var review = scenario.Assistant.Review!;

        review.Items[1].IsSelected = true;
        await review.RejectCommand.ExecuteAsync(null);

        Assert.Empty((await scenario.ReloadAsync(project)).Tasks);
        var after = scenario.Assistant.Review!;
        Assert.Contains(" · Pending", after.Items[0].Text);
        Assert.Contains(" · Rejected", after.Items[1].Text);
        Assert.True(after.CanReview);
        Assert.True(after.HasNoCommittedTasks);
    }

    [Fact]
    public async Task Once_every_proposal_is_decided_the_tray_offers_no_more_review()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        await FinishedJobAsync(scenario, project, new TaskProposal { Title = "Only one" });
        await scenario.OpenAsync(project);
        var review = scenario.Assistant.Review!;
        Assert.True(review.CanReview);

        review.Items.Single().IsSelected = true;
        await review.ApproveCommand.ExecuteAsync(null);

        Assert.False(scenario.Assistant.Review!.CanReview);
        Assert.Equal("Review proposals · 0 pending", scenario.Assistant.Review.TrayTitle);
    }

    [Fact]
    public async Task Approving_with_nothing_selected_changes_nothing()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        await FinishedJobAsync(scenario, project, new TaskProposal { Title = "Request deck" });
        await scenario.OpenAsync(project);

        await scenario.Assistant.Review!.ApproveCommand.ExecuteAsync(null);

        Assert.Empty((await scenario.ReloadAsync(project)).Tasks);
        Assert.Contains(" · Pending", scenario.Assistant.Review!.Items.Single().Text);
    }

    [Fact]
    public async Task The_tray_lists_the_projects_open_tasks_apart_from_proposals_and_marks_late_ones()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        project.Tasks.AddRange([
            new ProjectTask { ProjectId = project.Id, Title = "Late one", DueAt = PageScenario.Noon.AddDays(-2) },
            new ProjectTask { ProjectId = project.Id, Title = "Upcoming", DueAt = PageScenario.Noon.AddDays(3), Status = ProjectTaskStatus.InProgress },
            new ProjectTask { ProjectId = project.Id, Title = "Undated" },
            new ProjectTask { ProjectId = project.Id, Title = "Already done", DueAt = PageScenario.Noon.AddDays(-9), Status = ProjectTaskStatus.Done }]);
        await scenario.Projects.SaveAsync(project);
        await FinishedJobAsync(scenario, project, new TaskProposal { Title = "Proposal" });
        await scenario.OpenAsync(project);

        var committed = scenario.Assistant.Review!.CommittedTasks.OrderBy(task => task.Title, StringComparer.Ordinal).ToList();

        Assert.Equal(["Late one", "Undated", "Upcoming"], committed.Select(task => task.Title));
        Assert.Equal([true, false, false], committed.Select(task => task.IsLate));
        Assert.Equal("", committed.Single(task => task.Title == "Undated").DueText);
        Assert.False(scenario.Assistant.Review.HasNoCommittedTasks);
    }

    // ---- reviewing what already happened --------------------------------------------------------------------------------------------

    [Fact]
    public async Task Opening_a_project_whose_latest_job_has_proposals_shows_the_tray_and_its_outcome_straight_away()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        var job = await FinishedJobAsync(scenario, project, new TaskProposal { Title = "Request deck" });

        await scenario.OpenAsync(project);

        var assistant = scenario.Assistant;
        Assert.True(assistant.IsOutcomeVisible);
        Assert.Equal("Completed", assistant.OutcomeText);
        Assert.Equal(job.ResultText, assistant.ResultText);
        Assert.Equal("Proposals ready", assistant.StatusText);
        Assert.Equal("Ai", assistant.StatusToken);
        Assert.True(assistant.HasReview);
        Assert.False(assistant.IsStopVisible);
    }

    [Fact]
    public async Task Opening_a_project_without_pending_work_starts_with_an_empty_outcome()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        scenario.Runtime.Release.SetResult();
        await scenario.Agents.RunAsync(project, "Old run", new Progress<AgentEvent>(), CancellationToken.None);

        await scenario.OpenAsync(project);

        Assert.False(scenario.Assistant.IsOutcomeVisible);
        Assert.False(scenario.Assistant.HasReview);
        Assert.Equal("Waiting for consent", scenario.Assistant.StatusText);
    }

    [Fact]
    public async Task Reviewing_a_given_job_shows_that_jobs_tray_and_ignores_an_unknown_one()
    {
        using var scenario = await CreateAsync();
        var project = await scenario.AddProjectAsync();
        var older = await FinishedJobAsync(scenario, project, new TaskProposal { Title = "Older proposal" });
        scenario.Runtime.Release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scenario.Runtime.Release.SetResult();
        scenario.Runtime.Result = new AgentResult { Text = "Newer", Proposals = [new TaskProposal { Title = "Newer proposal" }] };
        await scenario.Agents.RunAsync(project, "Again", new Progress<AgentEvent>(), CancellationToken.None);
        await scenario.OpenAsync(project);
        Assert.Contains("Newer proposal", scenario.Assistant.Review!.Items.Single().Text);

        await scenario.Assistant.ReviewAsync(older.Id);
        Assert.Contains("Older proposal", scenario.Assistant.Review!.Items.Single().Text);
        Assert.Equal(older.ResultText, scenario.Assistant.ResultText);

        await scenario.Assistant.ReviewAsync("no-such-job");
        Assert.Contains("Older proposal", scenario.Assistant.Review!.Items.Single().Text);
    }

    [Fact]
    public async Task Reviewing_without_an_open_project_does_nothing()
    {
        using var scenario = await CreateAsync();

        await scenario.Assistant.ReviewAsync("job");

        Assert.False(scenario.Assistant.HasReview);
        Assert.False(scenario.Assistant.IsOutcomeVisible);
    }
}
