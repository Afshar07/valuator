using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Infrastructure.Persistence;
using ProjectOperations.Desktop.Localization;
using Xunit;

namespace ProjectOperations.Desktop.Tests;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public async Task Create_project_edit_document_information_and_manage_task()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => HasText(window, "What needs your attention?") && Button(window, "All projects").IsEffectivelyEnabled);
        Assert.True(HasText(window, "Recent projects"));
        Click(window, "Create project");
        Input(window, "Project name *").Text = "Synthetic investment";
        Input(window, "Company").Text = "Example company";
        Input(window, "Owner").Text = "Test owner";
        Click(window, "Create project", last: true);
        await UntilAsync(() => HasText(window, "Synthetic investment") && Button(window, "All projects").IsEffectivelyEnabled);
        Assert.Single(await fixture.Projects.ListAsync());
        Assert.True(HasText(window, "Pitch deck") && HasText(window, "Missing"));
        var tabs = Controls<TabControl>(window).Single();
        tabs.SelectedIndex = 1;
        ClickPrefix(window, "Pitch deck · Document");
        Input(window, "Value / information (Document)").Text = "Awaiting revised deck";
        Input(window, "Notes").Text = "Synthetic review note";
        var status = LabeledControl<ComboBox>(window, "Status (explicitly reviewed by you)");
        status.SelectedItem = RequirementStatus.Provided;
        Click(window, "Save requirement");
        await UntilAsync(() => HasText(window, "Readiness · 0/16 complete (0%)") && !Buttons(window).Any(b => ButtonText(b) == "Save requirement") && Button(window, "All projects").IsEffectivelyEnabled);
        var project = (await fixture.Projects.ListAsync()).Single();
        Assert.Equal("Awaiting revised deck", project.Requirements.Single(r => r.DefinitionId == "pitch-deck").Value);
        Assert.Equal(RequirementStatus.Provided, project.Requirements.Single(r => r.DefinitionId == "pitch-deck").Status);
        Assert.Equal(1, Controls<TabControl>(window).Single().SelectedIndex);
        ClickPrefix(window, "Pitch deck · Document");
        LabeledControl<ComboBox>(window, "Status (explicitly reviewed by you)").SelectedItem = RequirementStatus.Complete;
        Click(window, "Save requirement");
        await UntilAsync(() => HasText(window, "Readiness · 1/16 complete (6%)") && Button(window, "All projects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        Click(window, "New task");
        Input(window, "Task title *").Text = "Request financial plan";
        Input(window, "Optional due date · local time · yyyy-MM-dd HH:mm").Text = "2030-01-15 09:30";
        Click(window, "Save task");
        await UntilAsync(() => Buttons(window).Any(b => ButtonText(b).StartsWith("Request financial plan · To do")) && Button(window, "All projects").IsEffectivelyEnabled);
        project = (await fixture.Projects.ListAsync()).Single();
        Assert.Equal("2030-01-15 09:30", project.Tasks.Single().DueAt!.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        ClickPrefix(window, "Request financial plan · To do");
        Input(window, "Notes").Text = "Synthetic task note";
        Input(window, "Optional due date · local time · yyyy-MM-dd HH:mm").Text = "";
        Click(window, "Save task");
        await UntilAsync(() => Buttons(window).Any(b => ButtonText(b) == "Request financial plan · To do · No date") && Button(window, "All projects").IsEffectivelyEnabled);
        var updatedTask = (await fixture.Projects.ListAsync()).Single().Tasks.Single();
        Assert.Equal("Synthetic task note", updatedTask.Description);
        Assert.Null(updatedTask.DueAt);
        Click(window, "Complete");
        await UntilAsync(() => Buttons(window).Any(b => ButtonText(b).StartsWith("Request financial plan · Done")) && Button(window, "All projects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 2;
        ClickPrefix(window, "Request financial plan · Done");
        Click(window, "Delete task");
        await UntilAsync(() => !Buttons(window).Any(b => ButtonText(b).StartsWith("Request financial plan ·"))
            && Button(window, "All projects").IsEffectivelyEnabled);
        Assert.Empty((await fixture.Projects.ListAsync()).Single().Tasks);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Removing_file_association_retains_source_file_and_displays_added_metadata()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("File project", "Example", ProjectStage.Screening, ProjectStatus.Active, "", "");
        var path = fixture.CreateSourceFile();
        project.Requirements.Single(r => r.DefinitionId == "pitch-deck").Files.Add(new ProjectFile
        {
            FileName = "synthetic.txt",
            Path = path,
            SizeBytes = new FileInfo(path).Length,
            AddedAt = new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero)
        });
        await fixture.Projects.SaveAsync(project);
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => HasText(window, "What needs your attention?") && Button(window, "All projects").IsEffectivelyEnabled);
        ClickPrefix(window, project.Name + " ·");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Button(window, "All projects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        ClickPrefix(window, "Pitch deck · Document");
        Assert.Contains(Controls<TextBox>(window), box => box.IsReadOnly && box.Text!.Contains("bytes · Added") && box.Text.Contains(path));
        Click(window, "Remove association");
        await UntilAsync(() => !Buttons(window).Any(button => ButtonText(button) == "Remove association") && Button(window, "All projects").IsEffectivelyEnabled);
        Assert.Empty((await fixture.Projects.GetAsync(project.Id))!.Requirements.Single(r => r.DefinitionId == "pitch-deck").Files);
        Assert.True(File.Exists(path));
        Assert.Equal("Synthetic source contents", await File.ReadAllTextAsync(path));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Proposals_start_unchecked_and_only_selected_tasks_are_committed()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Review project", "Example", ProjectStage.DueDiligence, ProjectStatus.Active, "", "");
        fixture.Runtime.Result = new AgentResult
        {
            Text = "Synthetic analysis with two proposals",
            Proposals = [new TaskProposal { Title = "Request deck" }, new TaskProposal { Title = "Prepare briefing" }]
        };
        fixture.Runtime.Release.TrySetResult();
        var window = fixture.Window;
        await StartDelegationAsync(window, project.Name);
        await UntilAsync(() => HasText(window, "Completed") && Button(window, "All projects").IsEffectivelyEnabled);
        var proposalChecks = Controls<CheckBox>(window).Where(c => c.Content is TextBlock text && text.Text!.Contains(" · Pending")).ToList();
        Assert.Equal(2, proposalChecks.Count);
        Assert.All(proposalChecks, check => Assert.False(check.IsChecked));
        Assert.Empty((await fixture.Projects.GetAsync(project.Id))!.Tasks);
        proposalChecks.Single(c => ((TextBlock)c.Content!).Text!.StartsWith("Request deck")).IsChecked = true;
        Click(window, "Add selected tasks");
        await UntilAsync(() => Controls<CheckBox>(window).Any(c => c.Content is TextBlock text && text.Text!.Contains(" · Approved")) && Button(window, "All projects").IsEffectivelyEnabled);
        var task = Assert.Single((await fixture.Projects.GetAsync(project.Id))!.Tasks);
        Assert.Equal("Request deck", task.Title);
        Assert.Equal(3, Controls<TabControl>(window).Single().SelectedIndex);
        var remaining = Controls<CheckBox>(window).Single(c => c.Content is TextBlock text && text.Text!.Contains(" · Pending"));
        Assert.False(remaining.IsChecked);
        remaining.IsChecked = true;
        Click(window, "Reject selected proposals");
        await UntilAsync(() => Controls<CheckBox>(window).Any(c => c.Content is TextBlock text && text.Text!.Contains(" · Rejected")) && Button(window, "All projects").IsEffectivelyEnabled);
        Assert.Single((await fixture.Projects.GetAsync(project.Id))!.Tasks);
        Assert.Contains(Controls<TextBox>(window), box => box.IsReadOnly && box.Text == "Synthetic analysis with two proposals");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Runtime_failure_shows_failed_outcome_not_success_and_retains_history()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Failure project", "Example", ProjectStage.Screening, ProjectStatus.Active, "", "");
        fixture.Runtime.Failure = new InvalidOperationException("Synthetic transport unavailable");
        fixture.Runtime.Release.TrySetResult();
        var window = fixture.Window;
        await StartDelegationAsync(window, project.Name);
        await UntilAsync(() => HasText(window, "Failed") && Button(window, "All projects").IsEffectivelyEnabled);
        Assert.False(HasText(window, "Completed"));
        Assert.Contains(Controls<TextBlock>(window), text => text.IsVisible && text.Text!.StartsWith("The agent job failed."));
        var job = Assert.Single(await fixture.Agents.HistoryAsync(project.Id));
        Assert.Equal(AgentJobStatus.Failed, job.Status);
        Assert.Equal("Synthetic transport unavailable", job.Error);
        Assert.False(Button(window, "Run with approved context").IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Consent_required_each_run_and_stop_waits_for_runtime_completion()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Synthetic project", "Example", ProjectStage.DueDiligence, ProjectStatus.Active, "", "");
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => HasText(window, "What needs your attention?") && Button(window, "All projects").IsEffectivelyEnabled);
        ClickPrefix(window, project.Name + " ·");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Button(window, "All projects").IsEffectivelyEnabled);
        var tabs = Controls<TabControl>(window).Single();
        tabs.SelectedIndex = 3;
        Dispatcher.UIThread.RunJobs();
        await UntilAsync(() => HasText(window, "Delegation request"));
        Input(window, "Delegation request").Text = "Summarize supplied facts";
        var run = Button(window, "Run with approved context");
        Assert.False(run.IsEnabled);
        Click(window, "Preview context before request");
        var consent = Controls<CheckBox>(window).Single(c => c.Content is TextBlock text && text.Text!.StartsWith("I consent"));
        Assert.False(consent.IsChecked);
        consent.IsChecked = true;
        Assert.True(run.IsEnabled);
        Click(window, "Run with approved context");
        await UntilAsync(() => fixture.Runtime.Calls == 1);
        Assert.All(tabs.Items.OfType<TabItem>().Take(3), tab => Assert.False(tab.IsEnabled));
        Assert.False(Button(window, "All projects").IsEffectivelyEnabled);
        Assert.False(Controls<ComboBox>(window).Single(control => control.Name == "LanguageSelector").IsEffectivelyEnabled);
        Assert.Contains(Controls<TextBox>(window), box => box.Text?.Contains("Synthetic live delta") == true);
        Click(window, "Stop");
        await UntilAsync(() => fixture.Runtime.CancelRequested.Task.IsCompleted);
        Assert.True(HasText(window, "Stopping — waiting for the agent to finish cancellation…"));
        Assert.False(Button(window, "All projects").IsEffectivelyEnabled);
        Assert.False(run.IsEnabled);
        fixture.Runtime.Release.TrySetResult();
        await UntilAsync(() => HasText(window, "Cancelled") && Button(window, "All projects").IsEffectivelyEnabled);
        Assert.False(run.IsEnabled);
        Assert.False(consent.IsEnabled);
        Assert.Equal(AgentJobStatus.Cancelled, (await fixture.Agents.HistoryAsync(project.Id)).Single().Status);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Closing_during_job_waits_for_cancellation_before_window_closes()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var project = await fixture.Projects.CreateAsync("Close test", "Example", ProjectStage.Screening, ProjectStatus.Active, "", "");
        var window = fixture.Window;
        window.Show();
        await UntilAsync(() => HasText(window, "What needs your attention?") && Button(window, "All projects").IsEffectivelyEnabled);
        ClickPrefix(window, project.Name + " ·");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Button(window, "All projects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 3;
        Dispatcher.UIThread.RunJobs();
        await UntilAsync(() => HasText(window, "Delegation request"));
        Input(window, "Delegation request").Text = "Synthetic request";
        Click(window, "Preview context before request");
        Controls<CheckBox>(window).Single(c => c.Content is TextBlock text && text.Text!.StartsWith("I consent")).IsChecked = true;
        Click(window, "Run with approved context");
        await UntilAsync(() => fixture.Runtime.Calls == 1);
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Close();
        await UntilAsync(() => fixture.Runtime.CancelRequested.Task.IsCompleted);
        Assert.False(closed);
        fixture.Runtime.Release.TrySetResult();
        await UntilAsync(() => closed);
        Assert.Equal(AgentJobStatus.Cancelled, (await fixture.Agents.HistoryAsync(project.Id)).Single().Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_date_is_optional(string value) => Assert.Null(MainWindow.ParseLocalDate(value));

    [Theory]
    [InlineData("10/14/2030")]
    [InlineData("2030-02-30 09:30")]
    [InlineData("not a date")]
    public void Invalid_date_is_rejected(string value) => Assert.Throws<FormatException>(() => MainWindow.ParseLocalDate(value));

    private static IEnumerable<T> Controls<T>(Window window) where T : Control => window.GetLogicalDescendants().OfType<T>().Distinct();
    private static IEnumerable<Button> Buttons(Window window) => Controls<Button>(window);
    internal static string ButtonText(Button button) => AutomationProperties.GetName(button) is { Length: > 0 } name ? name : button.Content as string ?? "";
    private static Button Button(Window window, string text) => Buttons(window).First(b => ButtonText(b) == text);
    private static bool HasText(Window window, string text) => Controls<TextBlock>(window).Any(block => block.Text == text)
        || Buttons(window).Any(button => ButtonText(button) == text);
    private static TextBox Input(Window window, string label) => LabeledControl<TextBox>(window, label);
    private static T LabeledControl<T>(Window window, string label) where T : Control
    {
        var block = Controls<TextBlock>(window).Last(b => b.Text == label);
        var parent = Assert.IsType<StackPanel>(block.Parent);
        return Assert.IsType<T>(parent.Children[parent.Children.IndexOf(block) + 1]);
    }
    private static void Click(Window window, string text, bool last = false)
    {
        var matches = Buttons(window).Where(b => ButtonText(b) == text);
        var button = last ? matches.Last() : matches.First();
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
    private static void ClickPrefix(Window window, string prefix)
    {
        var button = Buttons(window).First(b => ButtonText(b).StartsWith(prefix));
        Assert.True(button.IsEffectivelyEnabled);
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(condition(), "UI did not reach the expected state within 10 seconds.");
    }

    private static async Task StartDelegationAsync(Window window, string projectName)
    {
        window.Show();
        await UntilAsync(() => HasText(window, "What needs your attention?") && Button(window, "All projects").IsEffectivelyEnabled);
        ClickPrefix(window, projectName + " ·");
        await UntilAsync(() => Controls<TabControl>(window).Any() && Button(window, "All projects").IsEffectivelyEnabled);
        Controls<TabControl>(window).Single().SelectedIndex = 3;
        Dispatcher.UIThread.RunJobs();
        await UntilAsync(() => HasText(window, "Delegation request"));
        Input(window, "Delegation request").Text = "Synthetic request";
        Click(window, "Preview context before request");
        Controls<CheckBox>(window).Single(c => c.Content is TextBlock text && text.Text!.StartsWith("I consent")).IsChecked = true;
        Click(window, "Run with approved context");
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "opencode", "projectops-desktop-" + Guid.NewGuid().ToString("N"));
        public ProjectService Projects { get; }
        public AgentService Agents { get; }
        public ControlledRuntime Runtime { get; } = new();
        public MainWindow Window { get; }
        public LocaleContext Locale { get; }
        private readonly SqliteProjectRepository _repository;
        public Fixture()
        {
            Directory.CreateDirectory(_directory);
            var database = Path.Combine(_directory, "synthetic.db");
            var repository = new SqliteProjectRepository(database);
            _repository = repository;
            Projects = new ProjectService(repository);
            Agents = new AgentService(Projects, Runtime, new SqliteAgentJobRepository(database));
            Locale = new LocaleContext(Path.Combine(_directory, "settings.json"));
            Window = new MainWindow(Projects, Agents, () => repository.InitializeAsync(), "Synthetic test runtime — no network or provider requests.", Locale);
        }
        public Task InitializeAsync() => _repository.InitializeAsync();
        public string CreateSourceFile()
        {
            var path = Path.Combine(_directory, "synthetic.txt");
            File.WriteAllText(path, "Synthetic source contents");
            return path;
        }
        public void Dispose()
        {
            Runtime.Release.TrySetResult();
            Window.Close();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(_directory, recursive: true);
        }
    }

    internal sealed class ControlledRuntime : IAgentRuntime
    {
        public int Calls { get; private set; }
        public AgentRequest? LastRequest { get; private set; }
        public TaskCompletionSource CancelRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AgentResult Result { get; set; } = new() { Text = "Synthetic result" };
        public Exception? Failure { get; set; }
        public async Task<AgentResult> RunAsync(AgentRequest request, IProgress<AgentEvent> progress, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            progress.Report(new AgentEvent { Kind = AgentEventKind.Activity, Message = "Reviewing synthetic project facts" });
            progress.Report(new AgentEvent { Kind = AgentEventKind.ResultDelta, Message = "Synthetic live delta" });
            using var registration = cancellationToken.Register(() => CancelRequested.TrySetResult());
            await Release.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Result;
        }
        public Task CancelAsync(string jobId, CancellationToken cancellationToken) { CancelRequested.TrySetResult(); return Task.CompletedTask; }
    }
}
