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

public sealed class MainWindow : Window
{
    private readonly ProjectService _projects;
    private readonly AgentService _agents;
    private readonly Func<Task> _initialize;
    private readonly string _configuration;
    private readonly ContentControl _page = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Start, IsVisible = false };
    private readonly StackPanel _navigation = Row();
    private TabControl? _projectTabs;
    private CancellationTokenSource? _runCancellation;
    private Task? _running;
    private bool _closing;
    private readonly LocaleContext _locale;
    private readonly LocalizationService _text;
    private readonly LocalizedControls _localized;
    private readonly Func<string>? _configurationText;
    private string _errorKey = "";

    public MainWindow(ProjectService projects, AgentService agents, Func<Task> initialize, string configuration, LocaleContext? locale = null, Func<string>? configurationText = null)
    {
        _projects = projects;
        _agents = agents;
        _initialize = initialize;
        _configuration = configuration;
        _configurationText = configurationText;
        _locale = locale ?? new LocaleContext();
        _text = new LocalizationService(_locale);
        _localized = new LocalizedControls(_locale);
        Bind(_error, control => control.Text = _errorKey.Length == 0 ? "" : T(_errorKey));
        _locale.Changed += LocaleChanged;
        Bind(this, window => window.Title = T("app.title"));
        FlowDirection = _locale.FlowDirection;
        Width = 1120;
        Height = 820;
        MinWidth = 800;
        MinHeight = 600;
        Background = new SolidColorBrush(Color.Parse("#F5F7F8"));
        var shell = new DockPanel { Margin = new Thickness(28) };
        var header = Column();
        header.Children.Add(Heading("app.title", 26));
        header.Children.Add(Label("app.subtitle"));
        _navigation.Children.Add(Action("navigation.attention", DashboardAsync));
        _navigation.Children.Add(Action("navigation.projects", ProjectsAsync));
        _navigation.Children.Add(Action("project.create", () => { ShowCreate(); return Task.CompletedTask; }));
        var language = new ComboBox { Name = "LanguageSelector", ItemsSource = new[] { "English", "فارسی" }, SelectedIndex = _locale.LanguageCode == "fa" ? 1 : 0, MinWidth = 110 };
        language.SelectionChanged += (_, _) =>
        {
            try { _locale.SetLanguage(language.SelectedIndex == 1 ? "fa" : "en"); }
            catch (Exception)
            {
                language.SelectedIndex = _locale.LanguageCode == "fa" ? 1 : 0;
                ShowError("validation.languageSaveFailed");
            }
        };
        Bind(language, control => control.SelectedIndex = _locale.LanguageCode == "fa" ? 1 : 0);
        _navigation.Children.Add(language);
        header.Children.Add(_navigation);
        header.Children.Add(_error);
        DockPanel.SetDock(header, Dock.Top);
        shell.Children.Add(header);
        shell.Children.Add(_page);
        Content = shell;
        Closed += (_, _) => { _locale.Changed -= LocaleChanged; _localized.Dispose(); };
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
        catch (OperationCanceledException) { ShowError("validation.operationCancelled"); }
        catch (Exception) { ShowError("validation.operationFailed"); }
    }

    private void LocaleChanged(object? sender, EventArgs e)
    {
        FlowDirection = _locale.FlowDirection;
    }
    private string T(string key) => _text.Get(key);
    private string F(string key, params object?[] values) => _text.Format(key, values);
    private void Bind<TControl>(TControl control, Action<TControl> update) where TControl : Control
        => _localized.Bind(control, update);
    private void ShowError(string message) { _errorKey = message; _error.Text = T(message); _error.IsVisible = true; }
    private static StackPanel Column() => new() { Spacing = 12 };
    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Spacing = 10 };
    private TextBlock Label(string text) => Label(() => T(text));
    private TextBlock Label(Func<string> text)
    {
        var control = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Start };
        Bind(control, target => target.Text = text());
        return control;
    }
    private TextBlock Heading(string text, double size = 20) => Heading(() => T(text), size);
    private TextBlock Heading(Func<string> text, double size = 20)
    {
        var control = Label(text); control.FontSize = size; control.FontWeight = FontWeight.SemiBold; return control;
    }
    private Button Action(string text, Func<Task> action)
        => Action(() => T(text), action);
    private Button Action(Func<string> text, Func<Task> action)
    {
        var button = new Button { Padding = new Thickness(14, 8) };
        Bind(button, control => control.Content = text());
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
        TextAlignment = TextAlignment.Start,
        MinHeight = multiline ? 80 : 34,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private static TextBox Readable(string value) => new()
    {
        Text = value,
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Start,
        MinHeight = 70,
        MaxHeight = 300
    };
    private ComboBox Choice<T>(T selected) where T : struct, Enum => new()
    {
        ItemsSource = Enum.GetValues<T>(),
        SelectedItem = selected,
        MinWidth = 200,
        ItemTemplate = new FuncDataTemplate<T>((value, _) => Label(() => EnumText(value)))
    };
    private string EnumText<T>(T value) where T : struct, Enum => new DomainDisplay(_text).Enum(value);
    private string GroupTitle(string id, string original) => new DomainDisplay(_text).Group(id, original);
    private string RequirementTitle(ProjectRequirement requirement) => new DomainDisplay(_text).Requirement(requirement);
    private void Field(StackPanel panel, string label, Control input)
    {
        panel.Children.Add(Label(label)); panel.Children.Add(input);
    }
    private void Show(Control control) => _page.Content = new ScrollViewer { Content = control, Margin = new Thickness(0, 20, 0, 0) };
    private string Due(DateTimeOffset? date) => date is null ? T("date.none") : new LocaleDateFormatter(_locale).Display(date);
    private TextBox DateInput(DateTimeOffset? date)
    {
        var input = Input(new LocaleDateFormatter(_locale).Edit(date));
        input.FlowDirection = FlowDirection.LeftToRight;
        return input;
    }
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
        catch (FormatException) { due = null; ShowError("validation.localDate"); return false; }
    }

    private async Task DashboardAsync()
    {
        _projectTabs = null;
        var dashboard = ProjectSummaries.Dashboard(await _projects.ListAsync(), DateTimeOffset.Now);
        var panel = Column();
        panel.Children.Add(Heading("dashboard.title"));
        panel.Children.Add(Label(() => F("dashboard.counts", dashboard.OverdueTasks.Count, dashboard.UpcomingTasks.Count, dashboard.UpcomingMilestones.Count)));
        foreach (var item in dashboard.OverdueTasks)
            panel.Children.Add(Action(() => F("dashboard.overdue", item.ProjectName, item.Task.Title, Due(item.Task.DueAt)), () => OpenProjectAsync(item.ProjectId)));
        foreach (var item in dashboard.UpcomingTasks)
            panel.Children.Add(Action(() => F("dashboard.soon", item.ProjectName, item.Task.Title, Due(item.Task.DueAt)), () => OpenProjectAsync(item.ProjectId)));
        foreach (var item in dashboard.UpcomingMilestones)
            panel.Children.Add(Action(() => F("dashboard.milestone", item.ProjectName, item.Milestone.Title, Due(item.Milestone.DueAt)), () => OpenProjectAsync(item.ProjectId)));
        panel.Children.Add(Heading("dashboard.projectReadiness", 18));
        foreach (var summary in dashboard.Projects)
        {
            panel.Children.Add(Action(() => F("dashboard.readiness", summary.Project.Name, summary.CompleteRequirements, summary.TotalRequirements, summary.MissingRequirements.Count), () => OpenProjectAsync(summary.Project.Id)));
            var nextDue = summary.Project.Tasks.Where(task => task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress)
                .Where(task => task.DueAt.HasValue).OrderBy(task => task.DueAt).FirstOrDefault();
            panel.Children.Add(Label(() => F("dashboard.deadline", summary.OverdueTasks.Count, nextDue is null ? T("date.notSet") : nextDue.Title + " · " + Due(nextDue.DueAt))));
            panel.Children.Add(Label(() => F("dashboard.questions", summary.Project.State.OpenQuestions.Count, summary.Project.State.FollowUps.Count)));
        }
        panel.Children.Add(Heading("dashboard.recentProjects", 18));
        foreach (var recent in dashboard.RecentProjects.Take(5))
            panel.Children.Add(Action(() => F("dashboard.updated", recent.Name, Due(recent.UpdatedAt)), () => OpenProjectAsync(recent.Id)));
        if (dashboard.Projects.Count == 0) panel.Children.Add(Label("dashboard.empty"));
        Show(panel);
    }

    private async Task ProjectsAsync()
    {
        _projectTabs = null;
        var panel = Column(); panel.Children.Add(Heading("navigation.projects"));
        foreach (var project in await _projects.ListAsync())
            panel.Children.Add(Action(() => $"{project.Name} · {project.CompanyName} · {EnumText(project.Stage)} · {EnumText(project.Status)} · {project.Owner}", () => OpenProjectAsync(project.Id)));
        Show(panel);
    }

    private void ShowCreate()
    {
        _projectTabs = null;
        var panel = Column(); panel.Children.Add(Heading("project.create"));
        var name = Input(); var company = Input(); var owner = Input(); var notes = Input("", true);
        var stage = Choice(ProjectStage.Screening); var status = Choice(ProjectStatus.Active);
        Field(panel, "project.name", name); Field(panel, "project.company", company);
        Field(panel, "project.stage", stage); Field(panel, "field.status", status); Field(panel, "project.owner", owner); Field(panel, "field.notes", notes);
        var template = new ComboBox { MinWidth = 250 };
        Bind(template, control => { control.ItemsSource = new[] { new DomainDisplay(_text).Template(VcTemplate.Create()) }; control.SelectedIndex = 0; });
        Field(panel, "project.template", template);
        panel.Children.Add(Action("project.create", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { ShowError("validation.projectNameRequired"); return; }
            var project = await _projects.CreateAsync(name.Text, company.Text ?? "", (ProjectStage)stage.SelectedItem!,
                (ProjectStatus)status.SelectedItem!, owner.Text ?? "", notes.Text ?? "");
            await OpenProjectAsync(project.Id);
        }));
        Show(panel);
    }

    private async Task OpenProjectAsync(Guid id, int selectedTab = 0)
    {
        var project = await _projects.GetAsync(id);
        if (project is null) { ShowError("validation.projectUnavailable"); return; }
        var panel = Column(); panel.Children.Add(Heading(() => project.Name, 24));
        panel.Children.Add(Label(() => $"{project.CompanyName} · {EnumText(project.Stage)} · {EnumText(project.Status)}"));
        var tabs = new TabControl();
        _projectTabs = tabs;
        tabs.ItemsSource = new[]
        {
            Tab("tabs.overview", Overview(project)),
            Tab("tabs.requirements", Requirements(project)),
            Tab("tabs.tasks", Tasks(project)),
            Tab("tabs.delegate", await AgentPanelAsync(project))
        };
        tabs.SelectedIndex = selectedTab;
        panel.Children.Add(tabs); Show(panel);
    }

    private async Task SaveAsync(Project project) { await _projects.SaveAsync(project); }
    private TabItem Tab(string key, Control content)
    {
        var tab = new TabItem { Content = content }; Bind(tab, control => control.Header = T(key)); return tab;
    }

    private Control Overview(Project project)
    {
        var panel = Column();
        var summary = ProjectSummaries.Summarize(project, DateTimeOffset.Now);
        panel.Children.Add(Heading(() => F("overview.readiness", summary.CompleteRequirements, summary.TotalRequirements, summary.CompletionPercentage)));
        panel.Children.Add(Label("overview.readinessDisclosure"));
        foreach (var group in VcTemplate.Create().Groups)
        {
            var items = project.Requirements.Where(r => r.GroupId == group.Id).ToList();
            panel.Children.Add(Label(() => F("dashboard.readiness", GroupTitle(group.Id, group.Title), items.Count(r => r.Status == RequirementStatus.Complete), items.Count, items.Count(r => r.Status is RequirementStatus.Missing or RequirementStatus.NeedsReview))));
        }
        panel.Children.Add(Label(() => F("overview.deadlines", summary.OverdueTasks.Count, summary.UpcomingTasks.Count)));
        panel.Children.Add(Heading("overview.missing", 18));
        if (summary.MissingRequirements.Count == 0) panel.Children.Add(Label("overview.noMissing"));
        foreach (var requirement in summary.MissingRequirements)
            panel.Children.Add(Label(() => $"{RequirementTitle(requirement)} · {EnumText(requirement.Status)}"));
        panel.Children.Add(Label(() => summary.NextMilestone is null ? T("overview.noMilestone") : F("overview.milestone", summary.NextMilestone.Title, Due(summary.NextMilestone.DueAt))));
        var name = Input(project.Name); var company = Input(project.CompanyName); var owner = Input(project.Owner);
        var notes = Input(project.Notes, true); var stage = Choice(project.Stage); var status = Choice(project.Status);
        Field(panel, "project.name", name); Field(panel, "project.company", company); Field(panel, "project.owner", owner);
        Field(panel, "project.stage", stage); Field(panel, "field.status", status); Field(panel, "field.notes", notes);
        var state = Input(project.State.Summary, true);
        var questions = Input(string.Join('\n', project.State.OpenQuestions), true);
        var followups = Input(string.Join('\n', project.State.FollowUps), true);
        Field(panel, "overview.currentState", state); Field(panel, "overview.questions", questions); Field(panel, "overview.followUps", followups);
        panel.Children.Add(Action("overview.save", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { ShowError("validation.projectNameRequired"); return; }
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
            panel.Children.Add(Heading(() => GroupTitle(group.Id, group.Title), 18));
            foreach (var requirement in project.Requirements.Where(r => r.GroupId == group.Id))
                panel.Children.Add(Action(() => F("requirement.summary", RequirementTitle(requirement), EnumText(requirement.Type), EnumText(requirement.Status), requirement.Files.Count), () =>
                { host.Content = RequirementEditor(project, requirement, host); host.BringIntoView(); return Task.CompletedTask; }));
        }
        return panel;
    }

    private Control RequirementEditor(Project project, ProjectRequirement requirement, ContentControl host)
    {
        var panel = Column(); panel.Children.Add(Heading(() => RequirementTitle(requirement)));
        panel.Children.Add(Action("requirement.back", () => { host.Content = RequirementList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        var status = Choice(requirement.Status); var value = Input(requirement.Value, true); var notes = Input(requirement.Notes, true);
        Field(panel, "requirement.reviewStatus", status); panel.Children.Add(Label(() => F("requirement.value", EnumText(requirement.Type)))); panel.Children.Add(value); Field(panel, "field.notes", notes);
        panel.Children.Add(Label(() => F("requirement.reviewed", Due(requirement.LastReviewedAt))));
        panel.Children.Add(Action("requirement.save", async () =>
        {
            requirement.Status = (RequirementStatus)status.SelectedItem!; requirement.Value = value.Text ?? ""; requirement.Notes = notes.Text ?? "";
            requirement.LastReviewedAt = DateTimeOffset.UtcNow;
            await SaveAsync(project); await OpenProjectAsync(project.Id, 1);
        }));
        panel.Children.Add(Label("file.disclosure"));
        foreach (var file in requirement.Files.ToList())
        {
            var reference = Readable(""); reference.FlowDirection = FlowDirection.LeftToRight; Bind(reference, control => control.Text = F("file.metadata", file.FileName, file.SizeBytes, Due(file.AddedAt), file.Path)); panel.Children.Add(reference);
            panel.Children.Add(Action("file.removeAssociation", async () =>
            {
                requirement.Files.Remove(file); await SaveAsync(project);
                host.Content = RequirementEditor(project, requirement, host);
            }));
        }
        panel.Children.Add(Action("file.addReferences", async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = T("file.pickerTitle"), AllowMultiple = true });
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (path is null) { ShowError("validation.localFilesOnly"); continue; }
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
        var panel = Column(); panel.Children.Add(Heading("tasks.title"));
        var summary = ProjectSummaries.Summarize(project, DateTimeOffset.Now);
        panel.Children.Add(Label(() => F("tasks.deadlines", summary.OverdueTasks.Count, summary.UpcomingTasks.Count)));
        panel.Children.Add(Action("task.new", () => { host.Content = TaskEditor(project, new ProjectTask { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; }));
        foreach (var task in project.Tasks)
        {
            var row = Row();
            row.Children.Add(Action(() => $"{task.Title} · {EnumText(task.Status)} · {Due(task.DueAt)}", () => { host.Content = TaskEditor(project, task, false, host); host.BringIntoView(); return Task.CompletedTask; }));
            if (task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress)
                row.Children.Add(Action("task.complete", async () => { task.Status = ProjectTaskStatus.Done; task.UpdatedAt = DateTimeOffset.UtcNow; await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
            panel.Children.Add(row);
        }
        panel.Children.Add(Heading("milestones.title", 18));
        panel.Children.Add(Action("milestone.new", () => { host.Content = MilestoneEditor(project, new Milestone { ProjectId = project.Id }, true, host); host.BringIntoView(); return Task.CompletedTask; }));
        foreach (var milestone in project.Milestones)
            panel.Children.Add(Action(() => $"{milestone.Title} · {T(milestone.IsComplete ? "milestone.complete" : "milestone.open")} · {Due(milestone.DueAt)}", () => { host.Content = MilestoneEditor(project, milestone, false, host); host.BringIntoView(); return Task.CompletedTask; }));
        return panel;
    }

    private Control TaskEditor(Project project, ProjectTask task, bool isNew, ContentControl host)
    {
        var panel = Column(); var title = Input(task.Title); var notes = Input(task.Description, true);
        var due = DateInput(task.DueAt); var status = Choice(task.Status);
        panel.Children.Add(Action("tasks.back", () => { host.Content = TaskList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        Field(panel, "task.title", title); Field(panel, "field.notes", notes); Field(panel, "field.status", status);
        Field(panel, "task.dueDate", due);
        panel.Children.Add(Action("task.save", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { ShowError("validation.taskTitleRequired"); return; }
            if (!TryDate(due, out var date)) return;
            task.Title = title.Text.Trim(); task.Description = notes.Text ?? ""; task.Status = (ProjectTaskStatus)status.SelectedItem!; task.DueAt = date;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            if (isNew && !project.Tasks.Contains(task)) project.Tasks.Add(task);
            await SaveAsync(project); await OpenProjectAsync(project.Id, 2);
        }));
        if (!isNew) panel.Children.Add(Action("task.delete", async () => { project.Tasks.Remove(task); await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
        return panel;
    }

    private Control MilestoneEditor(Project project, Milestone milestone, bool isNew, ContentControl host)
    {
        var panel = Column(); var title = Input(milestone.Title); var notes = Input(milestone.Notes, true);
        var due = DateInput(milestone.DueAt); var complete = new CheckBox { IsChecked = milestone.IsComplete }; Bind(complete, control => control.Content = T("milestone.complete"));
        panel.Children.Add(Action("tasks.back", () => { host.Content = TaskList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        Field(panel, "milestone.title", title); Field(panel, "field.notes", notes); Field(panel, "milestone.date", due); panel.Children.Add(complete);
        panel.Children.Add(Action("milestone.save", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { ShowError("validation.milestoneTitleRequired"); return; }
            if (!TryDate(due, out var date)) return;
            milestone.Title = title.Text.Trim(); milestone.Notes = notes.Text ?? ""; milestone.DueAt = date; milestone.IsComplete = complete.IsChecked == true;
            if (isNew && !project.Milestones.Contains(milestone)) project.Milestones.Add(milestone);
            await SaveAsync(project); await OpenProjectAsync(project.Id, 2);
        }));
        if (!isNew) panel.Children.Add(Action("milestone.delete", async () => { project.Milestones.Remove(milestone); await SaveAsync(project); await OpenProjectAsync(project.Id, 2); }));
        return panel;
    }

    private async Task<Control> AgentPanelAsync(Project project)
    {
        var previousJobs = await _agents.HistoryAsync(project.Id);
        var panel = Column(); var request = Column();
        panel.Children.Add(Heading("agent.title"));
        var configuration = Label(() => _configurationText?.Invoke() ?? _configuration); panel.Children.Add(configuration);
        var prompt = Input("", true); var presets = Row();
        foreach (var preset in AgentPrompts.Actions)
            presets.Children.Add(Action(() => T("agent.action." + preset.Id), () => { prompt.Text = preset.Prompt; return Task.CompletedTask; }));
        request.Children.Add(new ScrollViewer { Content = presets, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Field(request, "agent.request", prompt);
        var preview = Readable(""); preview.FlowDirection = FlowDirection.LeftToRight; var consent = new CheckBox
        {
            Content = Label("agent.consent"),
            IsEnabled = false
        };
        var context = "";
        var run = new Button { IsEnabled = false, Padding = new Thickness(14, 8) }; Bind(run, control => control.Content = T("agent.run"));
        request.Children.Add(Action("agent.preview", () =>
        {
            context = AgentService.BuildContext(project, previousJobs); preview.Text = context; consent.IsEnabled = true; consent.IsChecked = false;
            return Task.CompletedTask;
        }));
        request.Children.Add(preview); request.Children.Add(consent); request.Children.Add(run);
        consent.IsCheckedChanged += (_, _) => run.IsEnabled = consent.IsChecked == true && !string.IsNullOrWhiteSpace(prompt.Text);
        prompt.TextChanged += (_, _) => { consent.IsChecked = false; };
        panel.Children.Add(request);
        var outcomeKey = "agent.ready";
        Func<string> outcomeText = () => T(outcomeKey);
        var outcome = Label(() => outcomeText()); var activity = Readable(""); var result = Readable(""); var details = Readable("");
        var activities = new List<AgentEvent>();
        string ActivityText() => string.Concat(activities.Select(item => (item.ActivityKey is null ? item.Message : T(item.ActivityKey)) + "\n"));
        Bind(activity, control => control.Text = ActivityText());
        details.FlowDirection = FlowDirection.LeftToRight;
        var detailToggle = new CheckBox(); Bind(detailToggle, control => control.Content = T("agent.showDetails"));
        details.IsVisible = false;
        detailToggle.IsCheckedChanged += (_, _) => details.IsVisible = detailToggle.IsChecked == true;
        var stop = new Button { IsVisible = false }; Bind(stop, control => control.Content = T("agent.stop"));
        stop.Click += (_, _) => { outcomeKey = "agent.stopping"; outcome.Text = outcomeText(); stop.IsEnabled = false; _runCancellation?.Cancel(); };
        panel.Children.Add(outcome); panel.Children.Add(stop); panel.Children.Add(Label("agent.activity")); panel.Children.Add(activity);
        panel.Children.Add(Label("agent.result")); panel.Children.Add(result); panel.Children.Add(detailToggle); panel.Children.Add(details);
        var history = Column(); panel.Children.Add(Heading("history.title", 18)); panel.Children.Add(history);
        await FillHistoryAsync(project, history);
        run.Click += async (_, _) =>
        {
            if (consent.IsChecked != true || string.IsNullOrWhiteSpace(prompt.Text) || context != AgentService.BuildContext(project, previousJobs))
            { consent.IsChecked = false; ShowError("validation.contextConsentRequired"); return; }
            _runCancellation = new CancellationTokenSource();
            _navigation.IsEnabled = false; request.IsEnabled = false; history.IsEnabled = false;
            /** Disable edits and project switching for the full runtime lifetime. */
            if (_projectTabs is not null)
                foreach (var item in _projectTabs.Items.OfType<TabItem>()) if (!ReferenceEquals(item.Content, panel)) item.IsEnabled = false;
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
            _running = GuardAsync(async () =>
            {
                AgentJob job;
                try { job = await _agents.RunAsync(project, prompt.Text!, progress, _runCancellation.Token, previousJobs, _locale.AgentResponseLanguage); }
                catch { outcomeKey = "agent.outcomeNotRecorded"; outcome.Text = outcomeText(); throw; }
                acceptingProgress = false;
                previousJobs = previousJobs.Append(job).ToList();
                outcomeText = () => EnumText(job.Status); outcome.Text = outcomeText(); result.Text = job.ResultText;
                if (job.Status == AgentJobStatus.Failed) ShowError("validation.agentFailed");
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
        if (jobs.Count == 0) history.Children.Add(Label("history.empty"));
        foreach (var job in jobs.OrderByDescending(job => job.CreatedAt))
        {
            var card = Column(); card.Children.Add(Heading(() => $"{EnumText(job.Status)} · {Due(job.CreatedAt)}", 17));
            card.Children.Add(Label(() => job.FinishedAt.HasValue ? F("history.finished", Due(job.FinishedAt)) : T("history.noOutcome")));
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
                var actions = Row();
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
            history.Children.Add(new Border { Background = Brushes.White, Padding = new Thickness(16), CornerRadius = new CornerRadius(8), Child = card });
        }
    }
}
