using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Calendar;
using ProjectOperations.Desktop.Features.Dashboard;
using ProjectOperations.Desktop.Features.Documents;
using ProjectOperations.Desktop.Features.Onboarding;
using ProjectOperations.Desktop.Features.ProjectDetail;
using ProjectOperations.Desktop.Features.Projects;
using ProjectOperations.Desktop.Features.Settings;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The shell as a view-model: start-up, routing, the sidebar as data arrives, the sample workspace, the run lock and the assistant.</summary>
public sealed class ShellViewModelTests
{
    private static async Task AddDatedTaskAsync(ShellScenario scenario, Project project)
    {
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Review", DueAt = PageScenario.Noon.AddDays(2) });
        await scenario.Projects.SaveAsync(project);
    }

    // ---- start-up ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_empty_workspace_opens_the_project_list_under_a_welcome_screen_with_only_projects_and_settings()
    {
        using var scenario = await ShellScenario.CreateAsync(initialize: false);
        var shell = scenario.Shell;

        await shell.StartAsync();

        Assert.IsType<ProjectsViewModel>(shell.Page);
        Assert.IsType<WelcomeViewModel>(shell.Overlay);
        Assert.True(shell.HasOverlay);
        Assert.Equal([false, true, false, false, true], shell.Sidebar.Items.Select(item => item.IsVisible));
        Assert.Equal(new PageRoute(AppPage.Projects), shell.Navigator.Current);
        Assert.True(shell.IsSidebarEnabled);
        Assert.Null(shell.Messages.ErrorKey);
        Assert.Equal(1, scenario.Assistant.Pickers);
    }

    [Fact]
    public async Task A_workspace_with_dated_work_opens_on_the_attention_home_without_a_welcome_screen()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await AddDatedTaskAsync(scenario, await scenario.AddProjectAsync());

        await scenario.Shell.StartAsync();

        Assert.IsType<DashboardViewModel>(scenario.Shell.Page);
        Assert.Null(scenario.Shell.Overlay);
        Assert.Equal([true, true, true, false, true], scenario.Shell.Sidebar.Items.Select(item => item.IsVisible));
        Assert.True(scenario.Shell.Sidebar.Items[0].IsSelected);
    }

    [Fact]
    public async Task The_sidebar_is_off_while_starting_and_back_on_after()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var observed = new List<bool>();
        scenario.Shell.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainWindowViewModel.IsSidebarEnabled)) observed.Add(scenario.Shell.IsSidebarEnabled); };

        await scenario.Shell.StartAsync();

        Assert.False(observed.First());
        Assert.True(observed.Last());
        Assert.True(scenario.Shell.IsSidebarEnabled);
    }

    [Fact]
    public async Task A_failure_to_start_lands_in_the_banner_and_gives_the_sidebar_back()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var shell = new MainWindowViewModel(scenario.Workspace, scenario.Locale, scenario.Appearance, scenario.Strings, new ProjectOperations.Desktop.Updates.UpdateController(scenario.Updater),
            scenario.Files, scenario.Picker, () => throw new InvalidOperationException("no database"), scenario.Clock);

        await shell.StartAsync();

        Assert.Equal("validation.operationFailed", shell.Messages.ErrorKey);
        Assert.Equal("no database", shell.LastFailure!.Message);
        Assert.True(shell.IsSidebarEnabled);
        Assert.Null(shell.Page);
    }

    // ---- routing -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Each_section_shows_its_own_page_and_selects_its_entry()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var shell = scenario.Shell;
        await AddDatedTaskAsync(scenario, await scenario.AddProjectAsync());
        await shell.StartAsync();

        foreach (var (page, type) in new (AppPage, Type)[] { (AppPage.Projects, typeof(ProjectsViewModel)), (AppPage.Calendar, typeof(CalendarViewModel)),
            (AppPage.Settings, typeof(SettingsViewModel)), (AppPage.Dashboard, typeof(DashboardViewModel)) })
        {
            await shell.NavigateAsync(page);
            Assert.IsType(type, shell.Page);
            Assert.Equal([page], shell.Sidebar.Items.Where(item => item.IsSelected).Select(item => item.Page));
        }
    }

    [Fact]
    public async Task Documents_shows_once_a_file_is_linked_and_the_wizard_can_be_asked_for_with_the_page()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.AddProjectAsync();

        await scenario.Shell.NavigateAsync(AppPage.Documents, newProject: true);

        Assert.IsType<DocumentsViewModel>(scenario.Shell.Page);
        Assert.IsType<WizardViewModel>(scenario.Shell.Overlay);
    }

    [Fact]
    public async Task Pressing_a_sidebar_entry_shows_the_page_and_a_failure_lands_in_the_banner_instead_of_throwing()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.StartAsync();

        await scenario.Shell.Sidebar.Items[4].NavigateCommand.ExecuteAsync(null);
        Assert.IsType<SettingsViewModel>(scenario.Shell.Page);

        var broken = await ShellScenario.CreateAsync(initialize: false);
        using (broken)
        {
            await broken.Shell.Sidebar.Items[1].NavigateCommand.ExecuteAsync(null);
            Assert.Equal("validation.operationFailed", broken.Shell.Messages.ErrorKey);
            Assert.NotNull(broken.Shell.LastFailure);
        }
    }

    [Fact]
    public async Task Opening_a_project_shows_its_detail_tells_the_assistant_and_a_delegate_tab_opens_the_panel()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        var shell = scenario.Shell;

        await shell.OpenProjectAsync(project.Id, ProjectTab.Tasks);

        var detail = Assert.IsType<ProjectDetailViewModel>(shell.Page);
        Assert.Equal((int)ProjectTab.Tasks, detail.SelectedIndex);
        Assert.Equal([project.Id], scenario.Assistant.Projects);
        Assert.Equal([AppPage.Projects], shell.Sidebar.Items.Where(item => item.IsSelected).Select(item => item.Page));
        Assert.False(shell.IsAssistantOpen);

        await shell.OpenProjectAsync(project.Id, ProjectTab.Delegate);
        Assert.True(shell.IsAssistantOpen);
    }

    [Fact]
    public async Task Choosing_a_tab_is_remembered_so_a_reload_returns_to_it()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        await scenario.Shell.OpenProjectAsync(project.Id);
        var detail = (ProjectDetailViewModel)scenario.Shell.Page!;

        detail.SelectedIndex = (int)ProjectTab.Checklist;
        await scenario.Shell.RefreshProjectAsync();

        Assert.Equal(new ProjectRoute(project.Id, ProjectTab.Checklist), scenario.Shell.Navigator.Current);
        Assert.Equal((int)ProjectTab.Checklist, ((ProjectDetailViewModel)scenario.Shell.Page!).SelectedIndex);
        Assert.NotSame(detail, scenario.Shell.Page);
    }

    [Fact]
    public async Task A_project_that_no_longer_exists_is_reported_and_the_previous_page_stays()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.StartAsync();
        var before = scenario.Shell.Page;

        await scenario.Shell.OpenProjectAsync(Guid.NewGuid());

        Assert.Equal("validation.projectUnavailable", scenario.Shell.Messages.ErrorKey);
        Assert.Same(before, scenario.Shell.Page);
        Assert.Equal(new PageRoute(AppPage.Projects), scenario.Shell.Navigator.Current);
    }

    [Fact]
    public async Task Reloading_when_no_project_is_open_does_nothing()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.StartAsync();
        var before = scenario.Shell.Page;

        await scenario.Shell.RefreshProjectAsync();

        Assert.Same(before, scenario.Shell.Page);
    }

    // ---- the sidebar as data arrives -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_section_that_appears_later_is_flagged_new_announced_once_and_unflagged_when_opened()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        await scenario.Shell.StartAsync();
        Assert.False(scenario.Shell.Sidebar.IsVisible(AppPage.Dashboard));

        await AddDatedTaskAsync(scenario, project);
        await scenario.Shell.RefreshShellAsync();

        Assert.True(scenario.Shell.Sidebar.IsVisible(AppPage.Dashboard));
        Assert.True(scenario.Shell.Sidebar.Items[0].IsNew);
        Assert.True(scenario.Shell.Sidebar.Items[2].IsNew);
        Assert.Equal("v3.toastDated", scenario.Shell.Messages.ToastKey);
        Assert.True(scenario.Shell.HasToast);
        Assert.Equal(scenario.Strings["v3.toastDated"], scenario.Shell.ToastText);

        scenario.Shell.Messages.DismissToast();
        await scenario.Shell.RefreshShellAsync();
        Assert.Null(scenario.Shell.Messages.ToastKey);

        await scenario.Shell.NavigateAsync(AppPage.Dashboard);
        Assert.False(scenario.Shell.Sidebar.Items[0].IsNew);
        Assert.True(scenario.Shell.Sidebar.Items[2].IsNew);
    }

    [Fact]
    public async Task A_linked_file_announces_documents_and_a_connected_google_calendar_shows_the_calendar()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        await scenario.Shell.StartAsync();

        project.Requirements[0].Files.Add(new ProjectFile { FileName = "a.pdf", Path = "/a.pdf" });
        await scenario.Projects.SaveAsync(project);
        scenario.Calendar.Connected = true;
        await scenario.Shell.RefreshShellAsync();

        Assert.True(scenario.Shell.Sidebar.IsVisible(AppPage.Documents));
        Assert.True(scenario.Shell.Sidebar.IsVisible(AppPage.Calendar));
        Assert.Equal("v3.toastDocs", scenario.Shell.Messages.ToastKey);
        Assert.False(scenario.Shell.Sidebar.IsVisible(AppPage.Dashboard));
    }

    [Fact]
    public async Task The_attention_count_is_the_number_of_overdue_tasks()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Late", DueAt = PageScenario.Noon.AddDays(-3) });
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Later", DueAt = PageScenario.Noon.AddDays(3) });
        await scenario.Projects.SaveAsync(project);

        await scenario.Shell.RefreshShellAsync();

        Assert.Equal(1, scenario.Shell.Sidebar.AttentionCount);
        Assert.True(scenario.Shell.Sidebar.Items[0].HasCount);
    }

    [Fact]
    public async Task Getting_started_tracks_five_milestones_opens_the_next_item_and_can_be_hidden_for_the_session()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        await scenario.Shell.RefreshShellAsync();

        var card = scenario.Shell.Sidebar.GettingStarted!;
        Assert.Equal([true, false, false, false, false], card.Items.Select(item => item.IsDone));
        Assert.Equal([false, true, true, true, true], card.Items.Select(item => item.CanGo));
        Assert.True(card.Items[4].IsOptional);

        var first = project.Requirements.First(requirement => requirement.Status == RequirementStatus.Missing);
        await card.Items[1].GoCommand.ExecuteAsync(null);
        Assert.Equal(new ProjectRoute(project.Id, ProjectTab.Checklist), scenario.Shell.Navigator.Current);
        Assert.Equal(first.Id, scenario.State.OpenRequirement);

        await card.Items[3].GoCommand.ExecuteAsync(null);
        var opened = ((ProjectDetailViewModel)scenario.Shell.Page!).Requirements.Groups.SelectMany(group => group.Rows).Single(row => row.IsOpen);
        Assert.Equal(first.Id, opened.Id);
        Assert.NotNull(opened.Detail!.Form);
        Assert.Null(scenario.State.PendingFollowUp);
        Assert.Contains(scenario.State.ExpandedGroups, key => key == $"{project.Id}:{first.GroupId}");

        await scenario.Shell.Sidebar.GettingStarted!.Items[4].GoCommand.ExecuteAsync(null);
        Assert.Equal(new PageRoute(AppPage.Settings), scenario.Shell.Navigator.Current);

        await scenario.Shell.Sidebar.GettingStarted!.HideCommand.ExecuteAsync(null);
        Assert.Null(scenario.Shell.Sidebar.GettingStarted);
        Assert.True(scenario.State.GettingStartedHidden);
        await scenario.Shell.RefreshShellAsync();
        Assert.Null(scenario.Shell.Sidebar.GettingStarted);
    }

    [Fact]
    public async Task Getting_started_is_not_shown_without_a_project_or_once_everything_is_done()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.RefreshShellAsync();
        Assert.Null(scenario.Shell.Sidebar.GettingStarted);

        var project = await scenario.AddProjectAsync();
        project.Requirements[0].Status = RequirementStatus.Provided;
        project.Requirements[0].Files.Add(new ProjectFile { FileName = "a.pdf", Path = "/a.pdf" });
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "t", DueAt = PageScenario.Noon });
        await scenario.Projects.SaveAsync(project);
        await scenario.Shell.RefreshShellAsync();
        Assert.NotNull(scenario.Shell.Sidebar.GettingStarted);
        Assert.Equal([true, true, true, true, false], scenario.Shell.Sidebar.GettingStarted!.Items.Select(item => item.IsDone));
    }

    // ---- the sample workspace ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_sample_workspace_has_its_own_data_a_banner_and_every_section_and_never_touches_the_real_database()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.StartAsync();
        Assert.IsType<WelcomeViewModel>(scenario.Shell.Overlay);

        await scenario.Shell.StartSampleAsync();

        Assert.True(scenario.Shell.IsSample);
        Assert.IsType<SampleBannerViewModel>(scenario.Shell.SampleBanner);
        Assert.True(scenario.Shell.HasSampleBanner);
        Assert.Null(scenario.Shell.Overlay);
        Assert.IsType<DashboardViewModel>(scenario.Shell.Page);
        Assert.All(scenario.Shell.Sidebar.Items, item => Assert.True(item.IsVisible));
        Assert.All(scenario.Shell.Sidebar.Items, item => Assert.False(item.IsNew));
        Assert.Null(scenario.Shell.Sidebar.GettingStarted);
        Assert.Equal(4, (await scenario.Shell.Projects.ListAsync()).Count);
        Assert.Empty(await scenario.Projects.ListAsync());
        Assert.False(scenario.Shell.Environment.AgentConfigured);
    }

    [Fact]
    public async Task Starting_the_sample_twice_does_nothing_more_and_leaving_it_returns_to_the_real_workspace()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.StartSampleAsync();
        var banner = scenario.Shell.SampleBanner;

        await scenario.Shell.StartSampleAsync();
        Assert.Same(banner, scenario.Shell.SampleBanner);

        await scenario.Shell.ExitSampleAsync();
        Assert.False(scenario.Shell.IsSample);
        Assert.Null(scenario.Shell.SampleBanner);
        Assert.Empty(await scenario.Shell.Projects.ListAsync());
        await scenario.Shell.ExitSampleAsync();
        Assert.False(scenario.Shell.IsSample);
    }

    [Fact]
    public async Task The_sample_banner_offers_the_wizard_for_a_real_project()
    {
        using var scenario = await ShellScenario.CreateAsync();
        await scenario.Shell.StartSampleAsync();

        await ((SampleBannerViewModel)scenario.Shell.SampleBanner!).StartMineCommand.ExecuteAsync(null);

        Assert.IsType<WizardViewModel>(scenario.Shell.Overlay);
    }

    [Fact]
    public async Task Creating_a_project_from_the_wizard_resets_hints_shown_before()
    {
        using var scenario = await ShellScenario.CreateAsync();
        scenario.State.TipHidden = true; scenario.State.GettingStartedHidden = true;
        var project = await scenario.AddProjectAsync();

        await scenario.Shell.OpenCreatedProjectAsync(project.Id);

        Assert.False(scenario.State.TipHidden);
        Assert.False(scenario.State.GettingStartedHidden);
        Assert.Equal("v3.toastCreated", scenario.Shell.Messages.ToastKey);
        Assert.Equal(new ProjectRoute(project.Id, ProjectTab.Checklist), scenario.Shell.Navigator.Current);
    }

    // ---- locking, messages and the assistant ---------------------------------------------------------------------------------------

    [Fact]
    public async Task An_action_run_through_the_shell_locks_navigation_and_the_page_until_it_is_done()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var gate = new TaskCompletionSource();
        var states = new List<(bool Sidebar, bool Page)>();

        var running = scenario.Shell.RunAsync(async () => { states.Add((scenario.Shell.IsSidebarEnabled, scenario.Shell.IsPageEnabled)); await gate.Task; });
        Assert.Equal([(false, false)], states);
        Assert.False(scenario.Shell.IsSidebarEnabled);
        gate.SetResult();
        await running;

        Assert.True(scenario.Shell.IsSidebarEnabled);
        Assert.True(scenario.Shell.IsPageEnabled);
    }

    [Fact]
    public async Task A_failing_action_is_reported_in_the_banner_which_can_be_dismissed()
    {
        using var scenario = await ShellScenario.CreateAsync();

        await scenario.Shell.RunAsync(() => throw new InvalidOperationException("boom"));

        Assert.True(scenario.Shell.HasError);
        Assert.Equal(scenario.Strings["validation.operationFailed"], scenario.Shell.ErrorText);
        Assert.Equal("boom", scenario.Shell.LastFailure!.Message);
        Assert.True(scenario.Shell.IsSidebarEnabled);

        scenario.Shell.DismissErrorCommand.Execute(null);
        Assert.False(scenario.Shell.HasError);
        Assert.Equal("", scenario.Shell.ErrorText);

        await scenario.Shell.RunAsync(() => throw new OperationCanceledException());
        Assert.Equal("validation.operationCancelled", scenario.Shell.Messages.ErrorKey);
    }

    [Fact]
    public async Task A_new_action_clears_the_previous_error_and_the_message_text_follows_the_language()
    {
        using var scenario = await ShellScenario.CreateAsync();
        scenario.Shell.ShowError("validation.localDate");
        var english = scenario.Shell.ErrorText;

        scenario.Locale.SetLanguage("fa");
        Assert.NotEqual(english, scenario.Shell.ErrorText);

        await scenario.Shell.RunAsync(() => Task.CompletedTask);
        Assert.False(scenario.Shell.HasError);
    }

    [Fact]
    public async Task A_toast_is_shown_by_key_until_dismissed()
    {
        using var scenario = await ShellScenario.CreateAsync();
        Assert.False(scenario.Shell.HasToast);

        scenario.Shell.ShowToast("v3.toastTask");
        Assert.True(scenario.Shell.HasToast);
        Assert.Equal(scenario.Strings["v3.toastTask"], scenario.Shell.ToastText);

        scenario.Shell.Messages.DismissToast();
        Assert.False(scenario.Shell.HasToast);
    }

    [Fact]
    public async Task An_agent_run_locks_everything_through_its_reload_and_the_assistant_cannot_be_closed_meanwhile()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        await scenario.Shell.OpenProjectAsync(project.Id);
        var detail = (ProjectDetailViewModel)scenario.Shell.Page!;
        var gate = new TaskCompletionSource();
        var reloaded = false;
        scenario.Shell.SetAssistantOpen(true);

        var run = scenario.Shell.RunAgentAsync(_ => gate.Task, () => { reloaded = true; return Task.CompletedTask; });

        Assert.True(scenario.Shell.IsAgentRunning);
        Assert.False(scenario.Shell.IsSidebarEnabled);
        Assert.False(scenario.Shell.IsPageEnabled);
        Assert.True(detail.IsLocked);
        scenario.Shell.SetAssistantOpen(false);
        Assert.True(scenario.Shell.IsAssistantOpen);
        scenario.Shell.ToggleAssistant();
        Assert.True(scenario.Shell.IsAssistantOpen);

        gate.SetResult();
        await run;

        Assert.True(reloaded);
        Assert.False(scenario.Shell.IsAgentRunning);
        Assert.True(scenario.Shell.IsSidebarEnabled);
        Assert.True(scenario.Shell.IsPageEnabled);
        Assert.False(detail.IsLocked);
        scenario.Shell.ToggleAssistant();
        Assert.False(scenario.Shell.IsAssistantOpen);
    }

    [Fact]
    public async Task Stop_cancels_the_run_and_closing_the_window_waits_for_the_job()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var started = new TaskCompletionSource();
        CancellationToken seen = default;
        var run = scenario.Shell.RunAgentAsync(async token => { seen = token; started.SetResult(); await Task.Delay(Timeout.Infinite, token).ContinueWith(_ => { }); }, () => Task.CompletedTask);
        await started.Task;
        Assert.Null(scenario.Shell.RequestClose() is null ? "nothing" : null);

        scenario.Shell.CancelRun();
        await run;

        Assert.True(seen.IsCancellationRequested);
        Assert.Null(scenario.Shell.RequestClose());
    }

    [Fact]
    public async Task Reviewing_a_job_opens_the_assistant_on_it()
    {
        using var scenario = await ShellScenario.CreateAsync();

        await scenario.Shell.ReviewInAssistantAsync("job-1");

        Assert.True(scenario.Shell.IsAssistantOpen);
        Assert.Equal(["job-1"], scenario.Assistant.Reviewed);
    }

    [Fact]
    public async Task Dialogs_show_one_modal_at_a_time_and_close_on_request()
    {
        using var scenario = await ShellScenario.CreateAsync();
        var dialogs = scenario.Shell.Dialogs;
        Assert.False(dialogs.IsOpen);

        dialogs.Show("first");
        dialogs.Show("second");
        Assert.Equal("second", dialogs.Content);
        Assert.True(dialogs.IsOpen);

        dialogs.Close();
        Assert.False(dialogs.IsOpen);
        Assert.Null(dialogs.Content);
    }

    [Fact]
    public async Task The_theme_variant_follows_the_stored_preference()
    {
        using var scenario = await ShellScenario.CreateAsync();
        Assert.Equal("light", scenario.Shell.ThemeId);

        scenario.Appearance.SetTheme("dark");

        Assert.Equal("dark", scenario.Shell.ThemeId);
    }
}
