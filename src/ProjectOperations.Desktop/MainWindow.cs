using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

public sealed class MainWindow : Window
{
    private readonly ProjectService _projects;
    private readonly AgentService _agents;
    private readonly Func<Task> _initialize;
    private readonly string _configuration;
    private readonly ContentControl _page = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly StackPanel _navigation = Row();
    private TabControl? _projectTabs;
    private CancellationTokenSource? _runCancellation;
    private Task? _running;
    private bool _closing;

    public MainWindow(ProjectService projects, AgentService agents, Func<Task> initialize, string configuration)
    {
        _projects = projects;
        _agents = agents;
        _initialize = initialize;
        _configuration = configuration;
        Title = "Project Operations";
        Width = 1120;
        Height = 820;
        MinWidth = 800;
        MinHeight = 600;
        Background = new SolidColorBrush(Color.Parse("#F5F7F8"));
        var shell = new DockPanel { Margin = new Thickness(28) };
        var header = Column();
        header.Children.Add(Heading("Project Operations", 26));
        header.Children.Add(Label("A clear view of your projects, next steps, and delegated work."));
        _navigation.Children.Add(Action("Attention", DashboardAsync));
        _navigation.Children.Add(Action("All projects", ProjectsAsync));
        _navigation.Children.Add(Action("Create project", () => { ShowCreate(); return Task.CompletedTask; }));
        header.Children.Add(_navigation);
        header.Children.Add(_error);
        DockPanel.SetDock(header, Dock.Top);
        shell.Children.Add(header);
        shell.Children.Add(_page);
        Content = shell;
        Opened += async (_, _) =>
        {
            _navigation.IsEnabled = false;
            try { await GuardAsync(async () => { await _initialize(); await DashboardAsync(); }); }
            finally { _navigation.IsEnabled = true; }
        };
        Closing += async (_, e) =>
        {
            if (_closing || _running is null) return;
            e.Cancel = true;
            _runCancellation?.Cancel();
            await GuardAsync(async () => { await _running; _closing = true; Close(); });
        };
    }

    private async Task GuardAsync(Func<Task> action, bool clearError = true)
    {
        if (clearError) _error.IsVisible = false;
        try { await action(); }
        catch (OperationCanceledException) { ShowError("The operation was cancelled."); }
        catch (Exception) { ShowError("The operation could not be completed. Verify the local data folder is writable and the configured agent is available. Reload the project before retrying a save. Your source files have not been deleted."); }
    }

    private void ShowError(string message) { _error.Text = message; _error.IsVisible = true; }
    private static StackPanel Column() => new() { Spacing = 12 };
    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static TextBlock Heading(string text, double size = 20) => new() { Text = text, FontSize = size, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private Button Action(string text, Func<Task> action)
    {
        var button = new Button { Content = text, Padding = new Thickness(14, 8) };
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            _navigation.IsEnabled = false;
            _page.IsEnabled = false;
            try { await GuardAsync(action); }
            finally { button.IsEnabled = true; _navigation.IsEnabled = true; _page.IsEnabled = true; }
        };
        return button;
    }
    private static TextBox Input(string value = "", bool multiline = false) => new()
    {
        Text = value,
        AcceptsReturn = multiline,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = multiline ? 80 : 34,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private static TextBox Readable(string value) => new()
    {
        Text = value,
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        MinHeight = 70,
        MaxHeight = 300
    };
    private static ComboBox Choice<T>(T selected) where T : struct, Enum => new()
    {
        ItemsSource = Enum.GetValues<T>(),
        SelectedItem = selected,
        MinWidth = 200
    };
    private static void Field(StackPanel panel, string label, Control input)
    {
        panel.Children.Add(Label(label)); panel.Children.Add(input);
    }
    private void Show(Control control) => _page.Content = new ScrollViewer { Content = control, Margin = new Thickness(0, 20, 0, 0) };
    private static string Due(DateTimeOffset? date) => date?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "No date";
    public static DateTimeOffset? ParseLocalDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || TimeZoneInfo.Local.IsInvalidTime(date) || TimeZoneInfo.Local.IsAmbiguousTime(date))
            throw new FormatException("Enter an unambiguous local date as yyyy-MM-dd HH:mm, or leave it empty.");
        return new DateTimeOffset(date, TimeZoneInfo.Local.GetUtcOffset(date));
    }
    private bool TryDate(TextBox input, out DateTimeOffset? due)
    {
        try { due = ParseLocalDate(input.Text); return true; }
        catch (FormatException e) { due = null; ShowError(e.Message); return false; }
    }

    private async Task DashboardAsync()
    {
        _projectTabs = null;
        var dashboard = ProjectSummaries.Dashboard(await _projects.ListAsync(), DateTimeOffset.Now);
        var panel = Column();
        panel.Children.Add(Heading("What needs your attention?"));
        panel.Children.Add(Label($"{dashboard.OverdueTasks.Count} overdue tasks · {dashboard.UpcomingTasks.Count} tasks due in 7 days · {dashboard.UpcomingMilestones.Count} upcoming milestones"));
        foreach (var item in dashboard.OverdueTasks)
            panel.Children.Add(Action($"Overdue · {item.ProjectName} · {item.Task.Title} · {Due(item.Task.DueAt)}", () => OpenProjectAsync(item.ProjectId)));
        foreach (var item in dashboard.UpcomingTasks)
            panel.Children.Add(Action($"Due soon · {item.ProjectName} · {item.Task.Title} · {Due(item.Task.DueAt)}", () => OpenProjectAsync(item.ProjectId)));
        foreach (var item in dashboard.UpcomingMilestones)
            panel.Children.Add(Action($"Milestone · {item.ProjectName} · {item.Milestone.Title} · {Due(item.Milestone.DueAt)}", () => OpenProjectAsync(item.ProjectId)));
        panel.Children.Add(Heading("Project readiness", 18));
        foreach (var summary in dashboard.Projects)
        {
            panel.Children.Add(Action($"{summary.Project.Name} · {summary.CompleteRequirements}/{summary.TotalRequirements} complete · {summary.MissingRequirements.Count} missing / needs review", () => OpenProjectAsync(summary.Project.Id)));
            var nextDue = summary.Project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress)
                .Where(task => task.DueAt.HasValue).OrderBy(task => task.DueAt).FirstOrDefault();
            panel.Children.Add(Label($"{summary.OverdueTasks.Count} overdue · Next active task deadline: {(nextDue is null ? "Not set" : nextDue.Title + " · " + Due(nextDue.DueAt))}"));
            panel.Children.Add(Label($"{summary.Project.State.OpenQuestions.Count} open questions · {summary.Project.State.FollowUps.Count} follow-ups"));
        }
        panel.Children.Add(Heading("Recent projects", 18));
        foreach (var recent in dashboard.RecentProjects.Take(5))
            panel.Children.Add(Action($"{recent.Name} · Updated {Due(recent.UpdatedAt)}", () => OpenProjectAsync(recent.Id)));
        if (dashboard.Projects.Count == 0) panel.Children.Add(Label("No projects yet. Create your first project to start organizing the work."));
        Show(panel);
    }

    private async Task ProjectsAsync()
    {
        _projectTabs = null;
        var panel = Column(); panel.Children.Add(Heading("All projects"));
        foreach (var project in await _projects.ListAsync())
            panel.Children.Add(Action($"{project.Name} · {project.CompanyName} · {project.Stage} · {project.Status} · {project.Owner}", () => OpenProjectAsync(project.Id)));
        Show(panel);
    }

    private void ShowCreate()
    {
        _projectTabs = null;
        var panel = Column(); panel.Children.Add(Heading("Create project"));
        var name = Input(); var company = Input(); var owner = Input(); var notes = Input("", true);
        var stage = Choice(ProjectStage.Screening); var status = Choice(ProjectStatus.Active);
        Field(panel, "Project name *", name); Field(panel, "Company", company);
        Field(panel, "Stage", stage); Field(panel, "Status", status); Field(panel, "Owner", owner); Field(panel, "Notes", notes);
        var template = new ComboBox { ItemsSource = new[] { VcTemplate.Create().Name }, SelectedIndex = 0, MinWidth = 250 };
        Field(panel, "Template", template);
        panel.Children.Add(Action("Create project", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { ShowError("A project name is required."); return; }
            var project = await _projects.CreateAsync(name.Text, company.Text ?? "", (ProjectStage)stage.SelectedItem!,
                (ProjectStatus)status.SelectedItem!, owner.Text ?? "", notes.Text ?? "");
            await OpenProjectAsync(project.Id);
        }));
        Show(panel);
    }

    private async Task OpenProjectAsync(Guid id, int selectedTab = 0)
    {
        var project = await _projects.GetAsync(id);
        if (project is null) { ShowError("This project is no longer available."); return; }
        var panel = Column(); panel.Children.Add(Heading(project.Name, 24));
        panel.Children.Add(Label($"{project.CompanyName} · {project.Stage} · {project.Status}"));
        var tabs = new TabControl();
        _projectTabs = tabs;
        tabs.ItemsSource = new[]
        {
            new TabItem { Header = "Overview", Content = Overview(project) },
            new TabItem { Header = "Requirements & files", Content = Requirements(project) },
            new TabItem { Header = "Tasks & dates", Content = Tasks(project) },
            new TabItem { Header = "Delegate & review", Content = await AgentPanelAsync(project) }
        };
        tabs.SelectedIndex = selectedTab;
        panel.Children.Add(tabs); Show(panel);
    }

    private async Task SaveAsync(Project project) { await _projects.SaveAsync(project); }

    private Control Overview(Project project)
    {
        var panel = Column();
        var summary = ProjectSummaries.Summarize(project, DateTimeOffset.Now);
        panel.Children.Add(Heading($"Readiness · {summary.CompleteRequirements}/{summary.TotalRequirements} complete ({summary.CompletionPercentage:0}%)"));
        panel.Children.Add(Label("Only Complete counts toward readiness. A file association alone is not verification."));
        foreach (var group in VcTemplate.Create().Groups)
        {
            var items = project.Requirements.Where(r => r.GroupId == group.Id).ToList();
            panel.Children.Add(Label($"{group.Title} · {items.Count(r => r.Status == RequirementStatus.Complete)}/{items.Count} complete · {items.Count(r => r.Status is RequirementStatus.Missing or RequirementStatus.NeedsReview)} missing / needs review"));
        }
        panel.Children.Add(Label($"{summary.OverdueTasks.Count} overdue · {summary.UpcomingTasks.Count} tasks due within 7 days"));
        panel.Children.Add(Heading("Missing / needs review", 18));
        if (summary.MissingRequirements.Count == 0) panel.Children.Add(Label("No missing or unreviewed requirements."));
        foreach (var requirement in summary.MissingRequirements)
            panel.Children.Add(Label($"{requirement.Title} · {requirement.Status}"));
        panel.Children.Add(Label(summary.NextMilestone is null ? "No next milestone set" : $"Next milestone: {summary.NextMilestone.Title} · {Due(summary.NextMilestone.DueAt)}"));
        var name = Input(project.Name); var company = Input(project.CompanyName); var owner = Input(project.Owner);
        var notes = Input(project.Notes, true); var stage = Choice(project.Stage); var status = Choice(project.Status);
        Field(panel, "Project name *", name); Field(panel, "Company", company); Field(panel, "Owner", owner);
        Field(panel, "Stage", stage); Field(panel, "Status", status); Field(panel, "Notes", notes);
        var state = Input(project.State.Summary, true);
        var questions = Input(string.Join('\n', project.State.OpenQuestions), true);
        var followups = Input(string.Join('\n', project.State.FollowUps), true);
        Field(panel, "Current state", state); Field(panel, "Open questions (one per line)", questions); Field(panel, "Follow-ups (one per line)", followups);
        panel.Children.Add(Action("Save overview", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { ShowError("A project name is required."); return; }
            project.Name = name.Text.Trim(); project.CompanyName = company.Text ?? ""; project.Owner = owner.Text ?? "";
            project.Stage = (ProjectStage)stage.SelectedItem!; project.Status = (ProjectStatus)status.SelectedItem!; project.Notes = notes.Text ?? "";
            project.State.Summary = state.Text ?? "";
            project.State.OpenQuestions = Lines(questions.Text); project.State.FollowUps = Lines(followups.Text);
            await SaveAsync(project); await OpenProjectAsync(project.Id);
        }));
        return panel;
    }
    private static List<string> Lines(string? value) => (value ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    private Control Requirements(Project project)
    {
        var host = new ContentControl();
        host.Content = RequirementList(project, host);
        return host;
    }

    private Control RequirementList(Project project, ContentControl host)
    {
        var panel = Column();
        foreach (var group in VcTemplate.Create().Groups)
        {
            panel.Children.Add(Heading(group.Title, 18));
            foreach (var requirement in project.Requirements.Where(r => r.GroupId == group.Id))
                panel.Children.Add(Action($"{requirement.Title} · {requirement.Type} · {requirement.Status} · {requirement.Files.Count} files", () =>
                { host.Content = RequirementEditor(project, requirement, host); host.BringIntoView(); return Task.CompletedTask; }));
        }
        return panel;
    }

    private Control RequirementEditor(Project project, ProjectRequirement requirement, ContentControl host)
    {
        var panel = Column(); panel.Children.Add(Heading(requirement.Title));
        panel.Children.Add(Action("Back to requirements", () => { host.Content = RequirementList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        var status = Choice(requirement.Status); var value = Input(requirement.Value, true); var notes = Input(requirement.Notes, true);
        Field(panel, "Status (explicitly reviewed by you)", status); Field(panel, $"Value / information ({requirement.Type})", value); Field(panel, "Notes", notes);
        panel.Children.Add(Label($"Last reviewed: {Due(requirement.LastReviewedAt)}"));
        panel.Children.Add(Action("Save requirement", async () =>
        {
            requirement.Status = (RequirementStatus)status.SelectedItem!; requirement.Value = value.Text ?? ""; requirement.Notes = notes.Text ?? "";
            requirement.LastReviewedAt = DateTimeOffset.UtcNow;
            await SaveAsync(project); await OpenProjectAsync(project.Id, 1);
        }));
        panel.Children.Add(Label("File references only. Files are not copied, parsed, or sent as contents. Removing an association never deletes the source file."));
        foreach (var file in requirement.Files.ToList())
        {
            panel.Children.Add(Readable($"{file.FileName} · {file.SizeBytes:N0} bytes · Added {Due(file.AddedAt)}\n{file.Path}"));
            panel.Children.Add(Action("Remove association", async () =>
            {
                requirement.Files.Remove(file); await SaveAsync(project);
                host.Content = RequirementEditor(project, requirement, host);
            }));
        }
        panel.Children.Add(Action("Add file references…", async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Associate project files", AllowMultiple = true });
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (path is null) { ShowError("Only local file references are supported."); continue; }
                if (requirement.Files.Any(existing => existing.Path == path)) continue;
                var info = new FileInfo(path);
                requirement.Files.Add(new ProjectFile { FileName = file.Name, Path = path, SizeBytes = info.Length });
            }
            await SaveAsync(project); host.Content = RequirementEditor(project, requirement, host);
        }));
        return panel;
    }

    private Control Tasks(Project project)
    {
        var host = new ContentControl();
        host.Content = TaskList(project, host);
        return host;
    }

    private Control TaskList(Project project, ContentControl host)
    {
        var panel = Column(); panel.Children.Add(Heading("Tasks & internal dates"));
        var summary = ProjectSummaries.Summarize(project, DateTimeOffset.Now);
        panel.Children.Add(Label($"{summary.OverdueTasks.Count} overdue · {summary.UpcomingTasks.Count} active tasks due within 7 days. Completed and cancelled tasks remain visible below."));
        panel.Children.Add(Action("New task", () => { host.Content = TaskEditor(project, new ProjectTask { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; }));
        foreach (var task in project.Tasks)
        {
            var row = Row();
            row.Children.Add(Action($"{task.Title} · {task.Status} · {Due(task.DueAt)}", () => { host.Content = TaskEditor(project, task, false, host); host.BringIntoView(); return Task.CompletedTask; }));
            if (task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress)
                row.Children.Add(Action("Complete", async () => { task.Status = ProjectTaskStatus.Done; task.UpdatedAt = DateTimeOffset.UtcNow; await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
            panel.Children.Add(row);
        }
        panel.Children.Add(Heading("Milestones", 18));
        panel.Children.Add(Action("New milestone", () => { host.Content = MilestoneEditor(project, new Milestone { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; }));
        foreach (var milestone in project.Milestones)
            panel.Children.Add(Action($"{milestone.Title} · {(milestone.IsComplete ? "Complete" : "Open")} · {Due(milestone.DueAt)}", () => { host.Content = MilestoneEditor(project, milestone, false, host); host.BringIntoView(); return Task.CompletedTask; }));
        return panel;
    }

    private Control TaskEditor(Project project, ProjectTask task, bool isNew, ContentControl host)
    {
        var panel = Column(); var title = Input(task.Title); var notes = Input(task.Description, true);
        var due = Input(task.DueAt is null ? "" : Due(task.DueAt)); var status = Choice(task.Status);
        panel.Children.Add(Action("Back to tasks & dates", () => { host.Content = TaskList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        Field(panel, "Task title *", title); Field(panel, "Notes", notes); Field(panel, "Status", status);
        Field(panel, "Optional due date · local time · yyyy-MM-dd HH:mm", due);
        panel.Children.Add(Action("Save task", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { ShowError("A task title is required."); return; }
            if (!TryDate(due, out var date)) return;
            task.Title = title.Text.Trim(); task.Description = notes.Text ?? ""; task.Status = (ProjectTaskStatus)status.SelectedItem!; task.DueAt = date;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            if (isNew && !project.Tasks.Contains(task)) project.Tasks.Add(task);
            await SaveAsync(project); await OpenProjectAsync(project.Id, 2);
        }));
        if (!isNew) panel.Children.Add(Action("Delete task", async () => { project.Tasks.Remove(task); await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
        return panel;
    }

    private Control MilestoneEditor(Project project, Milestone milestone, bool isNew, ContentControl host)
    {
        var panel = Column(); var title = Input(milestone.Title); var notes = Input(milestone.Notes, true);
        var due = Input(milestone.DueAt is null ? "" : Due(milestone.DueAt)); var complete = new CheckBox { Content = "Complete", IsChecked = milestone.IsComplete };
        panel.Children.Add(Action("Back to tasks & dates", () => { host.Content = TaskList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        Field(panel, "Milestone title *", title); Field(panel, "Notes", notes); Field(panel, "Optional local date · yyyy-MM-dd HH:mm", due); panel.Children.Add(complete);
        panel.Children.Add(Action("Save milestone", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { ShowError("A milestone title is required."); return; }
            if (!TryDate(due, out var date)) return;
            milestone.Title = title.Text.Trim(); milestone.Notes = notes.Text ?? ""; milestone.DueAt = date; milestone.IsComplete = complete.IsChecked == true;
            if (isNew && !project.Milestones.Contains(milestone)) project.Milestones.Add(milestone);
            await SaveAsync(project); await OpenProjectAsync(project.Id, 2);
        }));
        if (!isNew) panel.Children.Add(Action("Delete milestone", async () => { project.Milestones.Remove(milestone); await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
        return panel;
    }

    private async Task<Control> AgentPanelAsync(Project project)
    {
        var previousJobs = await _agents.HistoryAsync(project.Id);
        var panel = Column(); var request = Column();
        panel.Children.Add(Heading("What can you take off my plate?"));
        panel.Children.Add(Label(_configuration));
        var prompt = Input("", true); var presets = Row();
        foreach (var preset in AgentPrompts.Actions)
            presets.Children.Add(Action(preset.Name, () => { prompt.Text = preset.Prompt; return Task.CompletedTask; }));
        request.Children.Add(new ScrollViewer { Content = presets, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Field(request, "Delegation request", prompt);
        var preview = Readable(""); var consent = new CheckBox
        {
            Content = Label("I consent to sending this preview and request to the configured agent and model provider. Metadata, notes, values, task dates, file paths, and up to three prior result excerpts from this project are included. File CONTENTS are never read in this MVP."),
            IsEnabled = false
        };
        var context = "";
        var run = new Button { Content = "Run with approved context", IsEnabled = false, Padding = new Thickness(14, 8) };
        request.Children.Add(Action("Preview context before request", () =>
        {
            context = AgentService.BuildContext(project, previousJobs); preview.Text = context; consent.IsEnabled = true; consent.IsChecked = false;
            return Task.CompletedTask;
        }));
        request.Children.Add(preview); request.Children.Add(consent); request.Children.Add(run);
        consent.IsCheckedChanged += (_, _) => run.IsEnabled = consent.IsChecked == true && !string.IsNullOrWhiteSpace(prompt.Text);
        prompt.TextChanged += (_, _) => { consent.IsChecked = false; };
        panel.Children.Add(request);
        var outcome = Label("Ready"); var activity = Readable(""); var result = Readable(""); var details = Readable("");
        var detailToggle = new CheckBox { Content = "Show technical details" };
        details.IsVisible = false;
        detailToggle.IsCheckedChanged += (_, _) => details.IsVisible = detailToggle.IsChecked == true;
        var stop = new Button { Content = "Stop", IsVisible = false };
        stop.Click += (_, _) => { outcome.Text = "Stopping — waiting for the agent to finish cancellation…"; stop.IsEnabled = false; _runCancellation?.Cancel(); };
        panel.Children.Add(outcome); panel.Children.Add(stop); panel.Children.Add(Label("Activity")); panel.Children.Add(activity);
        panel.Children.Add(Label("Result (select text to copy)")); panel.Children.Add(result); panel.Children.Add(detailToggle); panel.Children.Add(details);
        var history = Column(); panel.Children.Add(Heading("Job history & task review", 18)); panel.Children.Add(history);
        await FillHistoryAsync(project, history);
        run.Click += async (_, _) =>
        {
            if (consent.IsChecked != true || string.IsNullOrWhiteSpace(prompt.Text) || context != AgentService.BuildContext(project, previousJobs))
            { consent.IsChecked = false; ShowError("Preview the current context and give consent before running."); return; }
            _runCancellation = new CancellationTokenSource();
            _navigation.IsEnabled = false; request.IsEnabled = false; history.IsEnabled = false;
            /** Disable edits and project switching for the full runtime lifetime. */
            if (_projectTabs is not null)
                foreach (var item in _projectTabs.Items.OfType<TabItem>()) if (!ReferenceEquals(item.Content, panel)) item.IsEnabled = false;
            consent.IsChecked = false; activity.Text = ""; result.Text = ""; details.Text = "";
            outcome.Text = "Running"; stop.IsVisible = true; stop.IsEnabled = true;
            stop.BringIntoView();
            var acceptingProgress = true;
            var progress = new Progress<AgentEvent>(item =>
            {
                if (!acceptingProgress) return;
                if (item.Kind == AgentEventKind.ResultDelta) result.Text += item.Message;
                else if (item.Kind == AgentEventKind.Activity) activity.Text += item.Message + "\n";
                else details.Text += item.Message + "\n";
            });
            _running = GuardAsync(async () =>
            {
                AgentJob job;
                try { job = await _agents.RunAsync(project, prompt.Text!, progress, _runCancellation.Token, previousJobs); }
                catch { outcome.Text = "Outcome could not be recorded — check job history before retrying."; throw; }
                acceptingProgress = false;
                previousJobs = previousJobs.Append(job).ToList();
                outcome.Text = job.Status.ToString(); result.Text = job.ResultText;
                if (job.Status == AgentJobStatus.Failed) ShowError("The agent job failed. Check configured agent/provider access and retry after reviewing the history.");
            });
            try { await _running; }
            finally
            {
                acceptingProgress = false;
                _running = null; _runCancellation.Dispose(); _runCancellation = null;
                _navigation.IsEnabled = true; request.IsEnabled = true; history.IsEnabled = true; stop.IsVisible = false;
                consent.IsEnabled = false; run.IsEnabled = false; context = "";
                if (_projectTabs is not null)
                    foreach (var item in _projectTabs.Items.OfType<TabItem>()) item.IsEnabled = true;
                await GuardAsync(async () => await FillHistoryAsync(project, history), clearError: false);
            }
        };
        return panel;
    }

    private async Task FillHistoryAsync(Project project, StackPanel history)
    {
        history.Children.Clear();
        var jobs = await _agents.HistoryAsync(project.Id);
        if (jobs.Count == 0) history.Children.Add(Label("No delegated work yet."));
        foreach (var job in jobs.OrderByDescending(job => job.CreatedAt))
        {
            var card = Column(); card.Children.Add(Heading($"{job.Status} · {Due(job.CreatedAt)}", 17));
            card.Children.Add(Label(job.FinishedAt.HasValue ? $"Finished: {Due(job.FinishedAt)}" : "No final outcome recorded yet"));
            if (job.Status == AgentJobStatus.Interrupted) card.Children.Add(Label("Interrupted — the app ended before this job completed. No successful outcome is assumed."));
            card.Children.Add(Readable(job.Prompt)); card.Children.Add(Readable(job.ResultText));
            if (!string.IsNullOrWhiteSpace(job.Error))
                card.Children.Add(new Expander { Header = "Outcome details", Content = Readable(job.Error) });
            var selections = new List<(Guid Id, CheckBox Check)>();
            foreach (var proposal in job.Proposals)
            {
                var check = new CheckBox { Content = Label($"{proposal.Title} · {Due(proposal.DueAt)} · {proposal.ReviewStatus}\n{proposal.Description}"), IsChecked = false, IsEnabled = job.Status == AgentJobStatus.Completed && proposal.ReviewStatus == ProposalReviewStatus.Pending };
                card.Children.Add(check); selections.Add((proposal.Id, check));
            }
            if (selections.Count > 0)
            {
                var actions = Row();
                actions.Children.Add(Action("Add selected tasks", async () =>
                {
                    await _agents.ApproveAsync(job, selections.Where(s => s.Check.IsChecked == true).Select(s => s.Id));
                    await OpenProjectAsync(project.Id, 3);
                }));
                actions.Children.Add(Action("Reject selected proposals", async () =>
                {
                    await _agents.RejectAsync(job, selections.Where(s => s.Check.IsChecked == true).Select(s => s.Id));
                    await FillHistoryAsync(project, history);
                }));
                card.Children.Add(actions);
            }
            history.Children.Add(new Border { Background = Brushes.White, Padding = new Thickness(16), CornerRadius = new CornerRadius(8), Child = card });
        }
    }
}
