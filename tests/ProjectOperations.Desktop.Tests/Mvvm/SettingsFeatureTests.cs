using System.ComponentModel;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Settings;
using ProjectOperations.Desktop.Updates;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>
/// The Settings page as plain view-models: appearance and language, the read-only runtime and data facts, application updates, the
/// optional Google Calendar sign-in and the stage editor. No window and no controls; the shell, files, updater and calendar are fakes.
/// </summary>
public sealed class SettingsFeatureTests
{
    private static readonly AppUpdate Release = new("2.3.4", "## Improvements\n- Synthetic release notes", 2097152);

    private static async Task<SettingsViewModel> OpenAsync(PageScenario scenario, UpdateController? updates = null, IExternalCalendarSource? calendar = null) =>
        await SettingsViewModel.LoadAsync(scenario.SettingsServices(updates, calendar));

    // ---- Appearance and language --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Appearance_shows_the_saved_theme_and_language_and_the_choices_in_order()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Appearance.SetTheme("dark");

        using var settings = await OpenAsync(scenario);

        Assert.Equal(["Light", "Dark", "Auto"], settings.Theme.Options.Select(option => option.Label));
        Assert.Equal([false, true, false], settings.Theme.Options.Select(option => option.IsSelected));
        Assert.Equal(["English", "فارسی"], settings.Language.Options.Select(option => option.Label));
        Assert.Equal([true, false], settings.Language.Options.Select(option => option.IsSelected));
    }

    [Fact]
    public async Task Choosing_a_theme_saves_it_and_a_change_made_elsewhere_moves_the_choice()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);

        settings.Theme.Options[1].SelectCommand.Execute(null);
        Assert.Equal("dark", scenario.Appearance.Theme);
        Assert.Equal("dark", settings.Theme.Selected);
        Assert.Equal([false, true, false], settings.Theme.Options.Select(option => option.IsSelected));

        // The sidebar switches the theme too; the choice here follows without saving again.
        scenario.Appearance.SetTheme("auto");
        Assert.Equal("auto", settings.Theme.Selected);
        Assert.Equal([false, false, true], settings.Theme.Options.Select(option => option.IsSelected));
        Assert.Empty(scenario.Host.Errors);
    }

    [Fact]
    public async Task Choosing_a_language_switches_the_page_in_place_and_labels_keep_their_own_script()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var english = settings.L["presentation.navigationSettings"];

        settings.Language.Options[1].SelectCommand.Execute(null);

        Assert.Equal("fa", scenario.Locale.LanguageCode);
        Assert.NotEqual(english, settings.L["presentation.navigationSettings"]);
        Assert.Equal(["English", "فارسی"], settings.Language.Options.Select(option => option.Label));
        Assert.Equal([false, true], settings.Language.Options.Select(option => option.IsSelected));

        // The sidebar's language button switches it back; the choice here follows.
        scenario.Locale.SetLanguage("en");
        Assert.Equal("en", settings.Language.Selected);
        Assert.Equal([true, false], settings.Language.Options.Select(option => option.IsSelected));
        Assert.Empty(scenario.Host.Errors);
    }

    [Fact]
    public async Task A_preference_that_cannot_be_saved_shows_an_error_and_keeps_the_previous_choice()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        Directory.CreateDirectory(scenario.SettingsPath); // the preferences file can no longer be read or written

        settings.Theme.Options[1].SelectCommand.Execute(null);
        settings.Language.Options[1].SelectCommand.Execute(null);

        Assert.Equal(["validation.themeSaveFailed", "validation.languageSaveFailed"], scenario.Host.Errors);
        Assert.Equal("light", scenario.Appearance.Theme);
        Assert.Equal("light", settings.Theme.Selected);
        Assert.Equal("en", scenario.Locale.LanguageCode);
        Assert.Equal("en", settings.Language.Selected);
        Assert.Equal([true, false, false], settings.Theme.Options.Select(option => option.IsSelected));
    }

    [Fact]
    public async Task A_disposed_page_no_longer_follows_the_theme()
    {
        using var scenario = await PageScenario.CreateAsync();
        var settings = await OpenAsync(scenario);

        settings.Dispose();
        scenario.Appearance.SetTheme("dark");

        Assert.Equal("light", settings.Theme.Selected);
    }

    // ---- Assistant and data -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_assistant_section_shows_the_configured_runtime_read_only_with_isolated_variable_names()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Environment = new DesktopEnvironment(true, "http://127.0.0.1:4096", "/opt/agent-config", "/data/projects.db");

        using var settings = await OpenAsync(scenario);

        Assert.True(settings.AgentConfigured);
        Assert.Equal("Configured", settings.AgentStatusText);
        Assert.Equal("http://127.0.0.1:4096", settings.Endpoint);
        Assert.Equal("/opt/agent-config", settings.ConfigDirectory);
        Assert.Equal("Agent endpoint · \u2066PROJECTOPS_OPENCODE_URL\u2069", settings.EndpointLabel);
        Assert.Equal("Config directory · \u2066PROJECTOPS_OPENCODE_CONFIG_DIR\u2069", settings.ConfigDirectoryLabel);
        Assert.Equal("What can be sent. ", settings.SentTitle);
    }

    [Fact]
    public async Task An_unconfigured_assistant_is_reported_as_off_with_empty_fields()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Environment = new DesktopEnvironment(AgentConfigured: false);

        using var settings = await OpenAsync(scenario);

        Assert.False(settings.AgentConfigured);
        Assert.Equal("Not configured", settings.AgentStatusText);
        Assert.Equal("", settings.Endpoint);
        Assert.Equal("", settings.ConfigDirectory);
    }

    [Fact]
    public async Task The_database_folder_is_opened_by_the_launcher_and_a_refusal_is_reported()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Environment = new DesktopEnvironment(true, null, null, Path.Combine("data", "projects.db"));
        using var settings = await OpenAsync(scenario);

        Assert.Equal(Path.Combine("data", "projects.db"), settings.DatabaseText);
        Assert.True(settings.OpenFolderCommand.CanExecute(null));
        await settings.OpenFolderCommand.ExecuteAsync(null);
        Assert.Equal(["data"], scenario.Files.Revealed);
        Assert.Equal(1, scenario.Host.Runs);
        Assert.Empty(scenario.Host.Errors);

        scenario.Files.Succeeds = false;
        await settings.OpenFolderCommand.ExecuteAsync(null);
        Assert.Equal(["documents.openFailed"], scenario.Host.Errors);
    }

    [Fact]
    public async Task Without_a_database_path_the_folder_cannot_be_opened()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Environment = new DesktopEnvironment(true, null, null, null);
        using var settings = await OpenAsync(scenario);

        Assert.Equal(scenario.Strings["date.notSet"], settings.DatabaseText);
        Assert.False(settings.CanOpenFolder);
        Assert.False(settings.OpenFolderCommand.CanExecute(null));
    }

    [Fact]
    public async Task Showing_the_welcome_screen_again_goes_through_the_shell()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);

        await settings.ShowWelcomeCommand.ExecuteAsync(null);

        Assert.Equal(1, scenario.Host.WelcomesShown);
        Assert.Equal(1, scenario.Host.Runs);
    }

    // ---- Updates ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Checking_is_manual_and_an_available_release_is_not_downloaded_until_asked()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var updates = settings.Updates;

        Assert.Equal("Current version: \u20661.2.3\u2069", updates.CurrentVersionText);
        Assert.True(updates.CanCheck);
        Assert.Equal(0, scenario.Updater.CheckCalls);
        Assert.False(updates.IsOffered);

        updates.CheckCommand.Execute(null);
        Assert.Equal(1, scenario.Updater.CheckCalls);
        Assert.True(updates.IsChecking);
        Assert.False(updates.CanCheck);
        Assert.False(updates.CheckCommand.CanExecute(null));

        scenario.Updater.CheckRelease.SetResult(Release);
        await UntilAsync(() => updates.IsOffered);

        Assert.False(updates.IsChecking);
        Assert.True(updates.CanCheck);
        Assert.True(updates.CanDownload);
        Assert.False(updates.IsDownloading);
        Assert.Equal("Version \u20662.3.4\u2069 is available", updates.AvailableText);
        Assert.True(updates.HasSize);
        Assert.Contains("2 MB", updates.SizeText);
        Assert.Equal(Release.ReleaseNotes, updates.Notes);
        Assert.True(updates.HasNotes);
        Assert.False(updates.HasNoNotes);
        Assert.Equal(0, scenario.Updater.DownloadCalls);
        Assert.Equal(0, scenario.Updater.RestartCalls);
    }

    [Fact]
    public async Task A_release_without_notes_or_size_says_so_instead_of_showing_empty_boxes()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        settings.Updates.CheckCommand.Execute(null);
        scenario.Updater.CheckRelease.SetResult(new AppUpdate("2.3.4", null, 0));
        await UntilAsync(() => settings.Updates.IsOffered);

        Assert.False(settings.Updates.HasNotes);
        Assert.True(settings.Updates.HasNoNotes);
        Assert.False(settings.Updates.HasSize);
        Assert.Equal("", settings.Updates.SizeText);
    }

    [Fact]
    public async Task No_new_release_reports_up_to_date_and_allows_another_check()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);

        settings.Updates.CheckCommand.Execute(null);
        scenario.Updater.CheckRelease.SetResult(null);
        await UntilAsync(() => settings.Updates.IsUpToDate);

        Assert.True(settings.Updates.CanCheck);
        Assert.False(settings.Updates.IsOffered);
        Assert.False(settings.Updates.IsReady);
        Assert.False(settings.Updates.HasError);
        Assert.Equal(0, scenario.Updater.DownloadCalls);
    }

    [Fact]
    public async Task A_copy_that_cannot_update_itself_shows_only_its_version_and_why()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Updater.CanUpdate = false;
        using var settings = await OpenAsync(scenario);

        Assert.False(settings.Updates.CanUpdate);
        Assert.True(settings.Updates.IsPortable);
        Assert.False(settings.Updates.CanCheck);
        Assert.False(settings.Updates.CheckCommand.CanExecute(null));
        Assert.False(settings.Updates.IsChecking);
        Assert.False(settings.Updates.HasError);
        Assert.Equal(0, scenario.Updater.CheckCalls);
    }

    [Fact]
    public async Task A_failed_check_shows_an_error_and_the_next_check_clears_it()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);

        settings.Updates.CheckCommand.Execute(null);
        scenario.Updater.CheckRelease.SetException(new InvalidOperationException("Synthetic check failure"));
        await UntilAsync(() => settings.Updates.HasError);

        Assert.Equal(scenario.Strings["updates.checkFailed"], settings.Updates.ErrorText);
        Assert.True(settings.Updates.CanCheck);
        Assert.False(settings.Updates.IsOffered);

        scenario.Updater.CheckRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        settings.Updates.CheckCommand.Execute(null);
        Assert.False(settings.Updates.HasError);
        scenario.Updater.CheckRelease.SetResult(Release);
        await UntilAsync(() => settings.Updates.IsOffered);
        Assert.Equal(2, scenario.Updater.CheckCalls);
    }

    [Fact]
    public async Task Download_progress_only_moves_the_bar_and_Cancel_returns_to_the_offer_for_a_retry()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        await MakeAvailableAsync(scenario, settings);
        var updates = settings.Updates;

        updates.DownloadCommand.Execute(null);
        Assert.True(updates.IsDownloading);
        Assert.False(updates.CanDownload);
        Assert.False(updates.CanCheck);
        Assert.Equal(0, updates.Percent);

        var changed = new List<string>();
        ((INotifyPropertyChanged)updates).PropertyChanged += (_, e) => { lock (changed) changed.Add(e.PropertyName!); };
        scenario.Updater.Progress!.Report(37);
        await UntilAsync(() => updates.Percent == 37 && Count(changed) >= 2);
        Assert.Equal(["Percent", "PercentText"], changed.ToArray());
        Assert.Equal("Downloading and preparing update: 37%", updates.PercentText);

        updates.CancelDownloadCommand.Execute(null);
        Assert.True(scenario.Updater.DownloadCancellation.IsCancellationRequested);
        await UntilAsync(() => updates.CanDownload);
        Assert.False(updates.IsDownloading);
        Assert.False(updates.HasError);
        Assert.False(updates.IsReady);
        Assert.Equal(Release.ReleaseNotes, updates.Notes);

        scenario.Updater.ResetDownload();
        updates.DownloadCommand.Execute(null);
        Assert.Equal(2, scenario.Updater.DownloadCalls);
        Assert.Equal(1, scenario.Updater.CheckCalls);
        Assert.Equal(0, updates.Percent);
    }

    [Fact]
    public async Task A_finished_download_waits_for_an_explicit_restart_and_a_failed_restart_stays_ready()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        await MakeAvailableAsync(scenario, settings);
        var updates = settings.Updates;
        updates.DownloadCommand.Execute(null);

        scenario.Updater.DownloadRelease.SetResult();
        await UntilAsync(() => updates.IsReady);

        Assert.False(updates.IsOffered);
        Assert.False(updates.IsDownloading);
        Assert.Equal("Version \u20662.3.4\u2069 is ready to install", updates.ReadyText);
        Assert.Equal(0, scenario.Updater.RestartCalls);

        scenario.Updater.RestartFailure = new InvalidOperationException("Synthetic restart failure");
        updates.RestartCommand.Execute(null);
        Assert.True(updates.HasError);
        Assert.True(updates.IsReady);

        scenario.Updater.RestartFailure = null;
        updates.RestartCommand.Execute(null);
        Assert.Equal(2, scenario.Updater.RestartCalls);
        Assert.Same(Release, scenario.Updater.RestartedUpdate);
    }

    [Fact]
    public async Task A_failed_download_keeps_the_release_so_it_can_be_downloaded_again()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        await MakeAvailableAsync(scenario, settings);
        settings.Updates.DownloadCommand.Execute(null);

        scenario.Updater.DownloadRelease.SetException(new InvalidOperationException("Synthetic download failure"));
        await UntilAsync(() => settings.Updates.HasError);

        Assert.Equal(scenario.Strings["updates.downloadFailed"], settings.Updates.ErrorText);
        Assert.True(settings.Updates.CanDownload);
        Assert.False(settings.Updates.IsReady);
        Assert.Equal(Release.ReleaseNotes, settings.Updates.Notes);
    }

    [Fact]
    public async Task A_previously_downloaded_update_is_ready_at_once_and_installing_is_refused_while_the_assistant_runs()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Updater.PendingUpdate = Release;
        using var settings = await OpenAsync(scenario);
        scenario.Host.IsAgentRunning = true;

        Assert.True(settings.Updates.IsReady);
        Assert.False(settings.Updates.CanCheck);
        Assert.Equal(0, scenario.Updater.CheckCalls);

        settings.Updates.RestartCommand.Execute(null);
        Assert.Equal(["updates.agentRunning"], scenario.Host.Errors);
        Assert.Equal(0, scenario.Updater.RestartCalls);

        scenario.Host.IsAgentRunning = false;
        settings.Updates.RestartCommand.Execute(null);
        Assert.Equal(1, scenario.Updater.RestartCalls);
    }

    [Fact]
    public async Task A_download_continues_in_the_shell_when_the_page_is_left_and_the_next_page_sees_its_state()
    {
        using var scenario = await PageScenario.CreateAsync();
        var shared = new UpdateController(scenario.Updater);
        var first = await OpenAsync(scenario, shared);
        await MakeAvailableAsync(scenario, first);
        first.Updates.DownloadCommand.Execute(null);
        first.Dispose();

        scenario.Updater.Progress!.Report(64);
        await UntilAsync(() => shared.Percent == 64);
        Assert.False(scenario.Updater.DownloadCancellation.IsCancellationRequested);

        using var second = await OpenAsync(scenario, shared);
        Assert.True(second.Updates.IsDownloading);
        Assert.Equal(64, second.Updates.Percent);
        Assert.Equal(1, scenario.Updater.DownloadCalls);

        scenario.Updater.DownloadRelease.SetResult();
        await UntilAsync(() => second.Updates.IsReady);
    }

    [Fact]
    public async Task A_disposed_page_stops_listening_to_the_updater()
    {
        using var scenario = await PageScenario.CreateAsync();
        var shared = new UpdateController(scenario.Updater);
        var settings = await OpenAsync(scenario, shared);
        var raised = 0;
        ((INotifyPropertyChanged)settings.Updates).PropertyChanged += (_, _) => raised++;

        settings.Dispose();
        var check = shared.CheckAsync();
        scenario.Updater.CheckRelease.SetResult(null);
        await check;

        Assert.Equal(UpdatePhase.UpToDate, shared.Phase);
        Assert.Equal(0, raised);
    }

    // ---- Google Calendar ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Without_a_configured_Google_client_the_option_is_not_offered_at_all()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Calendar.Available = false;

        using var settings = await OpenAsync(scenario);

        Assert.False(settings.HasGoogle);
        Assert.Null(settings.Google);
    }

    [Fact]
    public async Task The_sample_workspace_never_offers_the_connected_account()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Calendar.Connected = true;

        using var settings = await OpenAsync(scenario, calendar: new NoExternalCalendar());

        Assert.Null(settings.Google);
    }

    [Fact]
    public async Task Google_starts_disconnected_and_reads_a_stored_sign_in_when_the_page_opens()
    {
        using var scenario = await PageScenario.CreateAsync();
        using (var settings = await OpenAsync(scenario))
        {
            var google = settings.Google!;
            Assert.True(settings.HasGoogle);
            Assert.False(google.IsConnected);
            Assert.Equal("Not connected", google.StatusText);
            Assert.True(google.CanConnect);
            Assert.False(google.CanDisconnect);
            Assert.False(google.IsSigningIn);
            Assert.Equal(scenario.Strings["google.note"], google.NoteText);
        }

        scenario.Calendar.Connected = true;
        using var connected = await OpenAsync(scenario);
        Assert.True(connected.Google!.IsConnected);
        Assert.Equal("Connected", connected.Google.StatusText);
        Assert.False(connected.Google.CanConnect);
        Assert.True(connected.Google.CanDisconnect);
        Assert.Equal(0, scenario.Calendar.ConnectCalls);
    }

    [Fact]
    public async Task Connecting_signs_in_without_locking_the_page_then_refreshes_the_shell_and_Disconnect_undoes_it()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var google = settings.Google!;

        await google.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(1, scenario.Calendar.ConnectCalls);
        Assert.True(google.IsConnected);
        Assert.False(google.IsSigningIn);
        Assert.True(google.CanDisconnect);
        Assert.Equal(1, scenario.Host.ShellRefreshes);
        Assert.Equal(0, scenario.Host.Runs); // Connect waits on the browser, so it must not hold the shell's page lock
        Assert.Empty(scenario.Host.Errors);

        await google.DisconnectCommand.ExecuteAsync(null);

        Assert.Equal(1, scenario.Calendar.DisconnectCalls);
        Assert.False(google.IsConnected);
        Assert.True(google.CanConnect);
        Assert.Equal(2, scenario.Host.ShellRefreshes);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task While_signing_in_only_Cancel_is_offered_and_cancelling_leaves_nothing_stored_and_no_error()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Calendar.ConnectGate = new TaskCompletionSource();
        using var settings = await OpenAsync(scenario);
        var google = settings.Google!;

        var signIn = google.ConnectCommand.ExecuteAsync(null);

        Assert.True(google.IsSigningIn);
        Assert.False(google.CanConnect);
        Assert.False(google.CanDisconnect);
        Assert.Equal(scenario.Strings["google.connecting"], google.NoteText);

        google.CancelSignInCommand.Execute(null);
        await signIn;

        Assert.False(google.IsSigningIn);
        Assert.False(google.IsConnected);
        Assert.True(google.CanConnect);
        Assert.Empty(scenario.Host.Errors);
        Assert.Equal(0, scenario.Host.ShellRefreshes);
    }

    [Fact]
    public async Task Leaving_the_page_abandons_a_sign_in_that_is_still_waiting_on_the_browser()
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Calendar.ConnectGate = new TaskCompletionSource();
        var settings = await OpenAsync(scenario);
        var signIn = settings.Google!.ConnectCommand.ExecuteAsync(null);
        Assert.True(settings.Google.IsSigningIn);

        settings.Dispose();
        await signIn;

        Assert.False(settings.Google.IsConnected);
        Assert.Empty(scenario.Host.Errors);
    }

    [Theory]
    [InlineData("denied", "google.connectDenied")]
    [InlineData("network", "google.connectFailed")]
    [InlineData("io", "google.connectFailed")]
    [InlineData("other", "validation.operationFailed")]
    public async Task A_failed_sign_in_reports_why_and_stays_disconnected(string failure, string error)
    {
        using var scenario = await PageScenario.CreateAsync();
        scenario.Calendar.ConnectFailure = failure switch
        {
            "denied" => new ExternalCalendarAuthorizationException("declined"),
            "network" => new HttpRequestException("offline"),
            "io" => new IOException("listener"),
            _ => new InvalidOperationException("Synthetic failure")
        };
        using var settings = await OpenAsync(scenario);

        await settings.Google!.ConnectCommand.ExecuteAsync(null);

        Assert.Equal([error], scenario.Host.Errors);
        Assert.False(settings.Google.IsConnected);
        Assert.False(settings.Google.IsSigningIn);
        Assert.True(settings.Google.CanConnect);
        Assert.Equal(0, scenario.Host.ShellRefreshes);
    }

    // ---- Stages -------------------------------------------------------------------------------------------------------------------

    private static readonly string VcTemplateId = VcTemplate.Create().Id;

    private static async Task<IReadOnlyList<ProjectStage>> StoredAsync(PageScenario scenario, string? template = null) =>
        await scenario.Projects.ListStagesAsync(template ?? VcTemplateId);

    [Fact]
    public async Task The_editor_lists_the_stages_in_order_with_where_they_can_move_and_which_are_in_use()
    {
        using var scenario = await PageScenario.CreateAsync();
        var stages = await StoredAsync(scenario);
        await scenario.AddProjectAsync("Nova Logistics"); // the first stage is the default
        await scenario.AddProjectAsync("Atlas");

        using var settings = await OpenAsync(scenario);
        var rows = settings.Stages.Rows;

        Assert.Equal(stages.Select(stage => stage.Title), rows.Select(row => row.Title));
        Assert.Equal(stages.Select(stage => stage.Color), rows.Select(row => row.Color));
        Assert.Equal([false, true, true, true, true, true], rows.Select(row => row.CanMoveUp));
        Assert.Equal([true, true, true, true, true, false], rows.Select(row => row.CanMoveDown));
        var used = Assert.Single(rows, row => row.IsInUse);
        Assert.Equal(stages[0].Id, used.Id);
        Assert.Equal(scenario.Strings.Format("stages.inUse", "2"), used.InUseText);
        Assert.False(used.CanDelete);
        Assert.Equal(scenario.Strings["stages.inUseWhy"], used.DeleteTip);
        Assert.All(rows.Where(row => !row.IsInUse), row => Assert.True(row.CanDelete));
        Assert.Equal(2, settings.Stages.Templates.Options.Count);
        Assert.Equal(scenario.Strings.Template(VcTemplate.Create()), settings.Stages.Templates.Options[0].Label);
        Assert.Equal(scenario.Strings["template.blank"], settings.Stages.Templates.Options[1].Label);
        Assert.Equal([true, false], settings.Stages.Templates.Options.Select(option => option.IsSelected));
    }

    [Fact]
    public async Task A_project_using_one_stage_reads_singular_in_the_note()
    {
        using var scenario = await PageScenario.CreateAsync();
        await scenario.AddProjectAsync("Nova Logistics");

        using var settings = await OpenAsync(scenario);

        Assert.Equal(scenario.Strings["stages.inUseOne"], settings.Stages.Rows.Single(row => row.IsInUse).InUseText);
    }

    [Fact]
    public async Task Adding_a_stage_appends_a_new_one_in_the_next_palette_colour_and_saves_the_list()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var before = settings.Stages.Rows.Count;

        await settings.Stages.AddCommand.ExecuteAsync(null);

        Assert.Equal(before + 1, settings.Stages.Rows.Count);
        var added = settings.Stages.Rows[^1];
        Assert.Equal(scenario.Strings["stages.newName"], added.Title);
        Assert.Equal(StageEditorViewModel.Palette[before % StageEditorViewModel.Palette.Count], added.Color);
        var stored = await StoredAsync(scenario);
        Assert.Equal(before + 1, stored.Count);
        Assert.Equal(added.Id, stored[^1].Id);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task Renaming_saves_the_trimmed_title_once_and_an_empty_or_unchanged_title_is_put_back()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var row = settings.Stages.Rows[1];
        var original = row.Title;

        row.Title = "   ";
        await row.CommitTitleCommand.ExecuteAsync(null);
        Assert.Equal(original, row.Title);
        Assert.Equal(original, (await StoredAsync(scenario))[1].Title);

        row.Title = original;
        await row.CommitTitleCommand.ExecuteAsync(null);
        Assert.Equal(original, (await StoredAsync(scenario))[1].Title);

        row.Title = "  Term sheet  ";
        await row.CommitTitleCommand.ExecuteAsync(null);
        await row.CommitTitleCommand.ExecuteAsync(null); // Enter and leaving the box both commit; the second finds nothing to do
        Assert.Equal("Term sheet", (await StoredAsync(scenario))[1].Title);
        Assert.Equal("Term sheet", settings.Stages.Rows[1].Title);
        Assert.NotSame(row, settings.Stages.Rows[1]); // the reload replaced the row
        Assert.Equal(0, scenario.Host.Runs); // editing in place never locks the page
        Assert.Empty(scenario.Host.Errors);
    }

    [Fact]
    public async Task Picking_a_colour_closes_the_picker_and_saves_it()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var row = settings.Stages.Rows[2];

        Assert.Equal(StageEditorViewModel.Palette, row.Colors.Select(color => color.Color));
        row.ToggleColorCommand.Execute(null);
        Assert.True(row.IsColorOpen);
        await row.Colors.Single(color => color.Color == "#DB2777").PickCommand.ExecuteAsync(null);

        Assert.False(row.IsColorOpen);
        Assert.Equal("#DB2777", (await StoredAsync(scenario))[2].Color);
        Assert.Equal("#DB2777", settings.Stages.Rows[2].Color);
        Assert.Empty(scenario.Host.Errors);
    }

    [Fact]
    public async Task Moving_a_stage_swaps_it_with_its_neighbour_and_the_ends_cannot_leave_the_list()
    {
        using var scenario = await PageScenario.CreateAsync();
        var original = (await StoredAsync(scenario)).Select(stage => stage.Id).ToList();
        using var settings = await OpenAsync(scenario);

        Assert.False(settings.Stages.Rows[0].MoveUpCommand.CanExecute(null));
        Assert.False(settings.Stages.Rows[^1].MoveDownCommand.CanExecute(null));

        await settings.Stages.Rows[2].MoveUpCommand.ExecuteAsync(null);
        Assert.Equal([original[0], original[2], original[1], .. original.Skip(3)], (await StoredAsync(scenario)).Select(stage => stage.Id));
        Assert.Equal([original[0], original[2], original[1], .. original.Skip(3)], settings.Stages.Rows.Select(row => row.Id));

        await settings.Stages.Rows[0].MoveDownCommand.ExecuteAsync(null);
        Assert.Equal(original[2], (await StoredAsync(scenario))[0].Id);
    }

    [Fact]
    public async Task A_stage_that_no_project_uses_can_be_deleted_and_one_in_use_cannot()
    {
        using var scenario = await PageScenario.CreateAsync();
        await scenario.AddProjectAsync("Nova Logistics");
        using var settings = await OpenAsync(scenario);
        var before = settings.Stages.Rows.Count;

        Assert.False(settings.Stages.Rows[0].DeleteCommand.CanExecute(null));
        var last = settings.Stages.Rows[^1];
        Assert.True(last.DeleteCommand.CanExecute(null));
        Assert.Equal(scenario.Strings["stages.delete"], last.DeleteTip);

        await last.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(before - 1, settings.Stages.Rows.Count);
        Assert.DoesNotContain(settings.Stages.Rows, row => row.Id == last.Id);
        Assert.DoesNotContain(await StoredAsync(scenario), stage => stage.Id == last.Id);
    }

    [Fact]
    public async Task The_last_stage_of_a_template_cannot_be_deleted_and_the_tooltip_says_why()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        settings.Stages.Templates.Options[1].SelectCommand.Execute(null);
        await settings.Stages.Reloading;
        while (settings.Stages.Rows.Count > 1) await settings.Stages.Rows[^1].DeleteCommand.ExecuteAsync(null);

        var only = Assert.Single(settings.Stages.Rows);

        Assert.False(only.CanDelete);
        Assert.False(only.DeleteCommand.CanExecute(null));
        Assert.Equal(scenario.Strings["stages.lastOne"], only.DeleteTip);
    }

    [Fact]
    public async Task Switching_template_shows_that_templates_own_stages()
    {
        using var scenario = await PageScenario.CreateAsync();
        var blank = await StoredAsync(scenario, ProjectOperations.Core.Application.ProjectService.BlankTemplateId);
        using var settings = await OpenAsync(scenario);
        var vc = settings.Stages.Rows.Select(row => row.Id).ToList();

        settings.Stages.Templates.Options[1].SelectCommand.Execute(null);
        await settings.Stages.Reloading;

        Assert.Equal([false, true], settings.Stages.Templates.Options.Select(option => option.IsSelected));
        Assert.Equal(blank.Select(stage => stage.Id), settings.Stages.Rows.Select(row => row.Id));
        Assert.Empty(settings.Stages.Rows.Select(row => row.Id).Intersect(vc));

        await settings.Stages.AddCommand.ExecuteAsync(null);
        Assert.Equal(blank.Count + 1, (await StoredAsync(scenario, ProjectOperations.Core.Application.ProjectService.BlankTemplateId)).Count);
        Assert.Equal(vc.Count, (await StoredAsync(scenario)).Count);
    }

    [Fact]
    public async Task A_save_the_database_refuses_is_reported_and_the_rows_are_reloaded_to_what_is_stored()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var stale = settings.Stages.Rows.Count;

        // Another window adds a stage and puts a project on it. This editor's list does not know it, so saving would delete it.
        var stages = (await StoredAsync(scenario)).ToList();
        var closing = new ProjectStage { Title = "Closing", Color = "#DC2626" };
        stages.Add(closing);
        await scenario.Projects.SaveStagesAsync(VcTemplateId, stages);
        await scenario.Projects.CreateAsync("Late addition", "", ProjectStatus.Active, "", "", closing.Id);

        var row = settings.Stages.Rows[0];
        row.Title = "Sourcing";
        await row.CommitTitleCommand.ExecuteAsync(null);

        Assert.Equal(["stages.saveFailed"], scenario.Host.Errors);
        Assert.Equal(stale + 1, settings.Stages.Rows.Count);
        Assert.Equal("Closing", settings.Stages.Rows[^1].Title);
        Assert.NotEqual("Sourcing", (await StoredAsync(scenario))[0].Title);
    }

    [Fact]
    public async Task Stage_titles_are_the_users_own_text_and_do_not_change_with_the_language()
    {
        using var scenario = await PageScenario.CreateAsync();
        using var settings = await OpenAsync(scenario);
        var titles = settings.Stages.Rows.Select(row => row.Title).ToList();
        var tip = settings.Stages.Rows[^1].DeleteTip;

        scenario.Locale.SetLanguage("fa");

        Assert.Equal(titles, settings.Stages.Rows.Select(row => row.Title));
        Assert.NotEqual(tip, settings.Stages.Rows[^1].DeleteTip); // the labels around the user's own text are translated
    }

    private static int Count(List<string> list) { lock (list) return list.Count; }

    private static async Task MakeAvailableAsync(PageScenario scenario, SettingsViewModel settings)
    {
        settings.Updates.CheckCommand.Execute(null);
        scenario.Updater.CheckRelease.SetResult(Release);
        await UntilAsync(() => settings.Updates.IsOffered);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition(), "The view-model did not reach the expected state within 10 seconds.");
    }
}
