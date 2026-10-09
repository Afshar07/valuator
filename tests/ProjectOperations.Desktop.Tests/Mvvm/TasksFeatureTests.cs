using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.Tasks;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Infrastructure.Persistence;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The Tasks tab and its dialogs as plain view-models: no window, no controls, a fixed clock.</summary>
public sealed class TasksFeatureTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Tasks_are_grouped_by_urgency_in_display_order_with_counts()
    {
        using var scenario = await Scenario.CreateAsync();
        var project = scenario.Project;
        project.Tasks.AddRange([NewTask("late", due: Noon.AddDays(-3)), NewTask("soon", due: Noon.AddDays(2)), NewTask("far", due: Noon.AddDays(40)),
            NewTask("loose"), NewTask("done", ProjectTaskStatus.Done, Noon.AddDays(-9)), NewTask("soon too", ProjectTaskStatus.InProgress, Noon.AddDays(1))]);

        var tasks = new TasksViewModel(project, [], scenario.Services);

        var L = scenario.Strings;
        Assert.Equal([L["v3.grpOverdue"], L["v3.grpWeek"], L["v3.grpLater"], L["v3.grpNoDate"], L["tasks.closed"]], tasks.Groups.Select(group => group.Title));
        Assert.Equal(["1", "2", "1", "1", "1"], tasks.Groups.Select(group => group.CountText));
        Assert.Equal(["soon too", "soon"], tasks.Groups[1].Rows.Select(row => row.Title));
        Assert.Equal([true, false, false, false, false], tasks.Groups.Select(group => group.IsOverdue));
        Assert.Equal([false, false, false, false, true], tasks.Groups.Select(group => group.IsClosed));
        Assert.False(tasks.IsEmpty);
        Assert.Empty(tasks.Milestones);
        Assert.False(tasks.HasMilestones);
    }

    [Fact]
    public async Task An_empty_project_shows_empty_states_and_no_proposal_banner()
    {
        using var scenario = await Scenario.CreateAsync();
        var tasks = new TasksViewModel(scenario.Project, [], scenario.Services);
        Assert.True(tasks.IsEmpty);
        Assert.Empty(tasks.Groups);
        Assert.False(tasks.HasPendingProposals);
        Assert.False(tasks.HasMilestones);
    }

    [Fact]
    public async Task Row_text_describes_the_task_and_follows_the_language_in_place()
    {
        using var scenario = await Scenario.CreateAsync();
        var project = scenario.Project;
        var requirement = project.Requirements.First(item => item.DefinitionId == "pitch-deck");
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Chase the deck", Status = ProjectTaskStatus.InProgress, DueAt = Noon.AddDays(-2), RequirementId = requirement.Id });
        var row = new TasksViewModel(project, [], scenario.Services).Groups.Single().Rows.Single();
        var changes = new List<string?>();
        row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        var L = scenario.Strings;

        Assert.True(row.IsActive); Assert.True(row.IsOverdue); Assert.True(row.IsInProgress); Assert.False(row.IsCancelled);
        Assert.Equal($"Chase the deck · {L.Enum(ProjectTaskStatus.InProgress)} · {L.Due(Noon.AddDays(-2))}", row.AccessibleName);
        Assert.Equal(L["task.complete"], row.ToggleLabel);
        Assert.Equal(L.Relative(Noon.AddDays(-2), true, Noon.Date), row.RelativeText);
        Assert.Equal(L.Requirement(requirement), row.RequirementTitle);
        Assert.True(row.HasRequirement);

        scenario.Locale.SetLanguage("fa");

        Assert.Equal([string.Empty], changes);
        Assert.Equal("Chase the deck", row.Title);
        Assert.Equal(L["task.complete"], row.ToggleLabel);
        Assert.Contains(L.Enum(ProjectTaskStatus.InProgress), row.AccessibleName);
        Assert.NotEqual(requirement.Title, row.RequirementTitle);
    }

    [Fact]
    public async Task Closed_tasks_show_their_status_where_open_ones_show_how_late_they_are()
    {
        using var scenario = await Scenario.CreateAsync();
        var project = scenario.Project;
        project.Tasks.Add(NewTask("Dropped", ProjectTaskStatus.Cancelled, Noon.AddDays(-4)));
        project.Tasks.Add(NewTask("Undated"));
        var rows = new TasksViewModel(project, [], scenario.Services).Groups.SelectMany(group => group.Rows).ToDictionary(row => row.Title);

        Assert.False(rows["Dropped"].IsActive);
        Assert.True(rows["Dropped"].IsCancelled);
        Assert.Equal(scenario.Strings.Enum(ProjectTaskStatus.Cancelled), rows["Dropped"].RelativeText);
        Assert.Equal(scenario.Strings["v3.reopen"], rows["Dropped"].ToggleLabel);
        Assert.Equal("—", rows["Undated"].DueText);
        Assert.Equal("", rows["Undated"].RelativeText);
        Assert.False(rows["Undated"].IsOverdue);
    }

    [Fact]
    public async Task Toggle_completes_then_reopens_saves_and_reloads_the_project()
    {
        using var scenario = await Scenario.CreateAsync();
        scenario.Project.Tasks.Add(NewTask("Send the NDA", due: Noon.AddDays(3), project: scenario.Project));
        await scenario.Projects.SaveAsync(scenario.Project);
        var row = new TasksViewModel(scenario.Project, [], scenario.Services).Groups.Single().Rows.Single();

        await row.ToggleCommand.ExecuteAsync(null);

        Assert.Equal(ProjectTaskStatus.Done, (await scenario.Reload()).Tasks.Single().Status);
        Assert.Equal(1, scenario.Host.Refreshes);
        Assert.False(row.IsActive);

        await row.ToggleCommand.ExecuteAsync(null);

        Assert.Equal(ProjectTaskStatus.Todo, (await scenario.Reload()).Tasks.Single().Status);
        Assert.Equal(2, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task Pending_proposals_are_counted_and_review_opens_the_most_recent_job()
    {
        using var scenario = await Scenario.CreateAsync();
        TaskProposal Proposal(ProposalReviewStatus status) => new() { Title = "p", ReviewStatus = status };
        AgentJob Job(string id, int daysAgo, AgentJobStatus status, params TaskProposal[] proposals) =>
            new() { Id = id, ProjectId = scenario.Project.Id, Status = status, CreatedAt = Noon.AddDays(-daysAgo), Proposals = [.. proposals] };
        var jobs = new[]
        {
            Job("older", 5, AgentJobStatus.Completed, Proposal(ProposalReviewStatus.Pending)),
            Job("newest", 1, AgentJobStatus.Completed, Proposal(ProposalReviewStatus.Pending), Proposal(ProposalReviewStatus.Pending), Proposal(ProposalReviewStatus.Approved)),
            Job("failed", 0, AgentJobStatus.Failed, Proposal(ProposalReviewStatus.Pending)),
            Job("reviewed", 2, AgentJobStatus.Completed, Proposal(ProposalReviewStatus.Rejected))
        };
        var tasks = new TasksViewModel(scenario.Project, jobs, scenario.Services);

        Assert.True(tasks.HasPendingProposals);
        Assert.Equal(scenario.Strings.Format("tasks.pendingProposals", 3), tasks.PendingProposalsText);

        await tasks.ReviewCommand.ExecuteAsync(null);

        Assert.Equal("newest", scenario.Host.Reviewed);
        Assert.False(new TasksViewModel(scenario.Project, [jobs[2], jobs[3]], scenario.Services).HasPendingProposals);
    }

    [Fact]
    public async Task Milestones_run_by_date_with_undated_last_and_describe_their_state()
    {
        using var scenario = await Scenario.CreateAsync();
        var project = scenario.Project;
        project.Milestones.AddRange([new Milestone { Title = "Undated" }, new Milestone { Title = "IC", DueAt = Noon.AddDays(5) },
            new Milestone { Title = "Signed", DueAt = Noon.AddDays(-5), IsComplete = true }]);
        var tasks = new TasksViewModel(project, [], scenario.Services);
        var L = scenario.Strings;

        Assert.Equal(["Signed", "IC", "Undated"], tasks.Milestones.Select(row => row.Title));
        Assert.True(tasks.HasMilestones);
        var signed = tasks.Milestones[0]; var open = tasks.Milestones[1]; var undated = tasks.Milestones[2];
        Assert.Equal(L["v3.reached"], signed.RelativeText);
        Assert.Equal($"Signed · {L["milestone.complete"]} · {L.Due(Noon.AddDays(-5))}", signed.AccessibleName);
        Assert.Equal(L.Relative(Noon.AddDays(5), false, Noon.Date), open.RelativeText);
        Assert.Equal($"IC · {L["milestone.open"]} · {L.Due(Noon.AddDays(5))}", open.AccessibleName);
        Assert.False(undated.HasDate);
        Assert.Equal(L["date.none"], undated.DateText);
    }

    [Fact]
    public async Task New_buttons_and_rows_open_the_matching_dialog_view_model()
    {
        using var scenario = await Scenario.CreateAsync();
        scenario.Project.Tasks.Add(NewTask("Existing"));
        scenario.Project.Milestones.Add(new Milestone { Title = "Existing milestone", DueAt = Noon.AddDays(3) });
        var tasks = new TasksViewModel(scenario.Project, [], scenario.Services);

        tasks.NewTaskCommand.Execute(null);
        var created = Assert.IsType<TaskDialogViewModel>(scenario.Dialogs.Shown);
        Assert.False(created.CanDelete);
        tasks.Groups.Single().Rows.Single().EditCommand.Execute(null);
        Assert.True(Assert.IsType<TaskDialogViewModel>(scenario.Dialogs.Shown).CanDelete);
        tasks.NewMilestoneCommand.Execute(null);
        Assert.IsType<MilestoneDialogViewModel>(scenario.Dialogs.Shown);
        tasks.Milestones.Single().EditCommand.Execute(null);
        Assert.True(Assert.IsType<MilestoneDialogViewModel>(scenario.Dialogs.Shown).CanDelete);
        Assert.Equal(typeof(TaskDialogView), ViewLocator.ViewTypeFor(typeof(TaskDialogViewModel)));
        Assert.Equal(typeof(MilestoneDialogView), ViewLocator.ViewTypeFor(typeof(MilestoneDialogViewModel)));
        Assert.Equal(typeof(TasksView), ViewLocator.ViewTypeFor(typeof(TasksViewModel)));
    }

    [Fact]
    public async Task A_new_task_cannot_be_saved_without_a_title()
    {
        using var scenario = await Scenario.CreateAsync();
        var dialog = new TaskDialogViewModel(scenario.Project, new ProjectTask { ProjectId = scenario.Project.Id }, isNew: true, scenario.Services);
        var enabledChanges = 0;
        dialog.SaveCommand.CanExecuteChanged += (_, _) => enabledChanges++;

        Assert.False(dialog.SaveCommand.CanExecute(null));
        dialog.Title = "   ";
        Assert.False(dialog.SaveCommand.CanExecute(null));
        dialog.Title = "Request financial plan";
        Assert.True(dialog.SaveCommand.CanExecute(null));
        Assert.True(enabledChanges > 0);
        Assert.Equal(scenario.Strings["v3.newTaskT"], dialog.Heading);
        Assert.Equal(scenario.Strings["v3.createTask"], dialog.SaveText);
    }

    [Fact]
    public async Task Saving_a_new_task_stores_it_due_at_the_end_of_the_chosen_day_and_closes_the_dialog()
    {
        using var scenario = await Scenario.CreateAsync();
        var requirement = scenario.Project.Requirements[2];
        var dialog = new TaskDialogViewModel(scenario.Project, new ProjectTask { ProjectId = scenario.Project.Id }, isNew: true, scenario.Services);
        scenario.Dialogs.Show(dialog);
        Assert.Equal(new DateTime(2026, 10, 10), dialog.Date.Date);
        Assert.Equal(ProjectTaskStatus.Todo, dialog.Status.Selected);

        dialog.Title = "  Request financial plan  ";
        dialog.Date.Date = new DateTime(2030, 1, 15);
        dialog.Status.Options[1].SelectCommand.Execute(null);
        dialog.SelectedRequirement = dialog.Requirements.Single(option => option.Id == requirement.Id);
        await dialog.SaveCommand.ExecuteAsync(null);

        var task = Assert.Single((await scenario.Reload()).Tasks);
        Assert.Equal("Request financial plan", task.Title);
        Assert.Equal(new DateTimeOffset(2030, 1, 15, 23, 59, 0, TimeSpan.Zero), task.DueAt);
        Assert.Equal(ProjectTaskStatus.InProgress, task.Status);
        Assert.Equal(requirement.Id, task.RequirementId);
        Assert.False(scenario.Dialogs.IsOpen);
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task Editing_keeps_the_time_of_day_and_the_no_date_chip_clears_the_deadline()
    {
        using var scenario = await Scenario.CreateAsync();
        var existing = new ProjectTask { ProjectId = scenario.Project.Id, Title = "Review", Status = ProjectTaskStatus.InProgress, DueAt = new DateTimeOffset(2030, 1, 15, 9, 30, 0, TimeSpan.Zero) };
        scenario.Project.Tasks.Add(existing);
        await scenario.Projects.SaveAsync(scenario.Project);

        var dialog = new TaskDialogViewModel(scenario.Project, existing, isNew: false, scenario.Services);
        Assert.Equal(ProjectTaskStatus.InProgress, dialog.Status.Selected);
        Assert.Equal(new DateTime(2030, 1, 15), dialog.Date.Date);
        dialog.Date.Date = new DateTime(2030, 2, 3);
        await dialog.SaveCommand.ExecuteAsync(null);
        Assert.Equal(new DateTimeOffset(2030, 2, 3, 9, 30, 0, TimeSpan.Zero), (await scenario.Reload()).Tasks.Single().DueAt);

        var again = new TaskDialogViewModel(scenario.Project, existing, isNew: false, scenario.Services);
        again.Date.Chips[^1].SelectCommand.Execute(null);
        await again.SaveCommand.ExecuteAsync(null);
        Assert.Null((await scenario.Reload()).Tasks.Single().DueAt);
        Assert.Equal(scenario.Strings["v3.saveB"], again.SaveText);
    }

    [Fact]
    public async Task Deleting_removes_the_task_and_cancel_changes_nothing()
    {
        using var scenario = await Scenario.CreateAsync();
        var existing = NewTask("Keep until deleted", due: Noon.AddDays(2), project: scenario.Project);
        scenario.Project.Tasks.Add(existing);
        await scenario.Projects.SaveAsync(scenario.Project);

        var dialog = new TaskDialogViewModel(scenario.Project, existing, isNew: false, scenario.Services);
        scenario.Dialogs.Show(dialog);
        dialog.Title = "Edited but cancelled";
        dialog.CancelCommand.Execute(null);
        Assert.False(scenario.Dialogs.IsOpen);
        Assert.Equal("Keep until deleted", (await scenario.Reload()).Tasks.Single().Title);
        Assert.Equal(0, scenario.Host.Refreshes);

        await dialog.DeleteCommand.ExecuteAsync(null);
        Assert.Empty((await scenario.Reload()).Tasks);
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task The_requirement_choice_starts_on_none_or_the_linked_requirement_and_follows_the_language()
    {
        using var scenario = await Scenario.CreateAsync();
        var linked = scenario.Project.Requirements.First(item => item.DefinitionId == "pitch-deck");
        var existing = new ProjectTask { ProjectId = scenario.Project.Id, Title = "Chase", RequirementId = linked.Id };
        var fresh = new TaskDialogViewModel(scenario.Project, new ProjectTask { ProjectId = scenario.Project.Id }, isNew: true, scenario.Services);
        var follow = new TaskDialogViewModel(scenario.Project, existing, isNew: false, scenario.Services);

        Assert.Null(fresh.SelectedRequirement.Id);
        Assert.Equal(scenario.Strings["v3.noneL"], fresh.SelectedRequirement.Title);
        Assert.Equal(1 + scenario.Project.Requirements.Count, fresh.Requirements.Count);
        Assert.Equal(linked.Id, follow.SelectedRequirement.Id);
        var english = follow.SelectedRequirement.Title;

        scenario.Locale.SetLanguage("fa");

        Assert.NotEqual(english, follow.SelectedRequirement.Title);
        Assert.Equal(scenario.Strings["v3.editTaskT"], follow.Heading);
    }

    [Fact]
    public async Task Milestone_dialog_creates_edits_completes_and_deletes()
    {
        using var scenario = await Scenario.CreateAsync();
        var created = new MilestoneDialogViewModel(scenario.Project, new Milestone { ProjectId = scenario.Project.Id }, isNew: true, scenario.Services);
        Assert.False(created.SaveCommand.CanExecute(null));
        Assert.Equal(new DateTime(2026, 10, 16), created.Date.Date);
        Assert.False(created.Date.AllowNone);
        created.Title = " Investment committee ";
        await created.SaveCommand.ExecuteAsync(null);
        var stored = Assert.Single((await scenario.Reload()).Milestones);
        Assert.Equal("Investment committee", stored.Title);
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 23, 59, 0, TimeSpan.Zero), stored.DueAt);
        Assert.False(stored.IsComplete);

        var edit = new MilestoneDialogViewModel(scenario.Project, scenario.Project.Milestones.Single(), isNew: false, scenario.Services);
        scenario.Dialogs.Show(edit);
        Assert.True(edit.CanDelete);
        edit.Title = "IC held"; edit.IsReached = true;
        await edit.SaveCommand.ExecuteAsync(null);
        Assert.True((await scenario.Reload()).Milestones.Single().IsComplete);
        Assert.False(scenario.Dialogs.IsOpen);

        await edit.DeleteCommand.ExecuteAsync(null);
        Assert.Empty((await scenario.Reload()).Milestones);
    }

    [Fact]
    public async Task Date_chips_pick_days_and_follow_manual_dates()
    {
        using var scenario = await Scenario.CreateAsync();
        var field = DateFieldViewModel.Ahead(scenario.Strings, 1, allowNone: true, scenario.Clock);
        var L = scenario.Strings;

        Assert.Equal([L["v3.dToday"], L["v3.dTomorrow"], L["v3.dWeek"], L["v3.d2Week"], L["v3.dNone"]], field.Chips.Select(chip => chip.Label));
        Assert.Equal([false, true, false, false, false], field.Chips.Select(chip => chip.IsSelected));
        Assert.Equal(L["v3.relTomorrow"], field.RelativeText);

        field.Chips[2].SelectCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 10, 16), field.Date);
        Assert.Equal([false, false, true, false, false], field.Chips.Select(chip => chip.IsSelected));

        field.Date = new DateTime(2026, 10, 12);
        Assert.Equal([false, false, false, false, false], field.Chips.Select(chip => chip.IsSelected));
        Assert.Equal(L.Format("v3.relIn", "3"), field.RelativeText);

        field.Chips[^1].SelectCommand.Execute(null);
        Assert.Null(field.Date);
        Assert.Null(field.Value);
        Assert.Equal("", field.RelativeText);
        Assert.True(field.Chips[^1].IsSelected);
        Assert.Equal(4, DateFieldViewModel.Ahead(L, 0, allowNone: false, scenario.Clock).Chips.Count);
    }

    [Fact]
    public async Task Persian_edits_the_Jalali_picker_and_spells_out_the_Gregorian_day()
    {
        using var scenario = await Scenario.CreateAsync();
        var field = DateFieldViewModel.Existing(scenario.Strings, new DateTimeOffset(2030, 1, 15, 9, 30, 0, TimeSpan.Zero), allowNone: true, scenario.Clock);
        var changes = new List<string?>();
        field.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Assert.False(field.IsPersian);
        Assert.Equal("", field.ConvertedText);

        scenario.Locale.SetLanguage("fa");

        Assert.True(field.IsPersian);
        Assert.Contains(string.Empty, changes);
        Assert.Equal("میلادی: ‎2030-01-15‎", field.ConvertedText);
        Assert.Equal(new DateTimeOffset(2030, 1, 15, 9, 30, 0, TimeSpan.Zero), field.Value);
        Assert.Equal(new DateTime(2030, 1, 15), field.Date);
    }

    [Fact]
    public void A_chip_group_keeps_exactly_one_option_selected()
    {
        var locale = new LocaleContext();
        using var strings = new LocalizedStrings(locale, new LocalizationService(locale));
        var group = new ChipGroupViewModel<ProjectStatus>(strings, Enum.GetValues<ProjectStatus>().Select(item => (item, (Func<string>)(() => strings.Enum(item)))), ProjectStatus.OnHold);
        var changes = new List<string?>();
        group.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Assert.Equal(ProjectStatus.OnHold, group.Selected);
        Assert.Single(group.Options, option => option.IsSelected);
        Assert.Equal(strings.Enum(ProjectStatus.OnHold), group.Options.Single(option => option.IsSelected).Label);

        group.Options[0].SelectCommand.Execute(null);

        Assert.Equal(Enum.GetValues<ProjectStatus>()[0], group.Selected);
        Assert.Single(group.Options, option => option.IsSelected);
        Assert.Equal([nameof(group.Selected)], changes);
        Assert.Same(group.Options, group.Options);
    }

    private static ProjectTask NewTask(string title, ProjectTaskStatus status = ProjectTaskStatus.Todo, DateTimeOffset? due = null, Project? project = null) =>
        new() { Title = title, Status = status, DueAt = due, ProjectId = project?.Id ?? Guid.Empty };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class Host : IProjectHost
    {
        public int Refreshes { get; private set; }
        public string? Reviewed { get; private set; }
        public Task RefreshProjectAsync() { Refreshes++; return Task.CompletedTask; }
        public Task ReviewInAssistantAsync(string jobId) { Reviewed = jobId; return Task.CompletedTask; }
        public Task RunAsync(Func<Task> action) => action();
        public void ShowError(string key) { }
        public void ShowToast(string key) { }
        public void ToggleAssistant() { }
        public void OpenAssistant() { }
        public void SelectTab(ProjectTab tab) { }
    }

    private sealed class Dialogs : IDialogService
    {
        public object? Shown { get; private set; }
        public bool IsOpen => Shown is not null;
        public void Show(object content) => Shown = content;
        public void Close() => Shown = null;
    }

    /// <summary>A project in a throw-away SQLite database, English strings, a clock fixed at noon UTC on 9 October 2026.</summary>
    private sealed class Scenario : IDisposable
    {
        private readonly string _directory;
        private readonly LocalizedStrings _strings;

        private Scenario(string directory, ProjectService projects, Project project, LocaleContext locale)
        {
            _directory = directory; Projects = projects; Project = project; Locale = locale;
            _strings = new LocalizedStrings(locale, new LocalizationService(locale));
            Services = new ProjectScreenServices(projects, _strings, Dialogs, Host, Clock, new PageScenario.FakeNavigator(), new PageScenario.FakePicker(), new UiState());
        }

        public ProjectService Projects { get; }
        public Project Project { get; }
        public LocaleContext Locale { get; }
        public LocalizedStrings Strings => _strings;
        public TimeProvider Clock { get; } = new FixedClock(Noon);
        public Host Host { get; } = new();
        public Dialogs Dialogs { get; } = new();
        public ProjectScreenServices Services { get; }

        public static async Task<Scenario> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "project-operations-tasks-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            var repository = new SqliteProjectRepository(Path.Combine(directory, "tasks.db"));
            await repository.InitializeAsync();
            var projects = new ProjectService(repository);
            var project = await projects.CreateAsync("Deal", "Company", ProjectStatus.Active, "", "");
            return new Scenario(directory, projects, project, new LocaleContext(Path.Combine(directory, "settings.json")));
        }

        public async Task<Project> Reload() => (await Projects.GetAsync(Project.Id))!;

        public void Dispose()
        {
            _strings.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
