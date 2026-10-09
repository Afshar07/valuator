using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>What the sidebar offers and when: the pure policy, then the view-model that keeps the state.</summary>
public sealed class SidebarFeatureTests
{
    private static readonly DateTimeOffset Noon = PageScenario.Noon;

    private static Project Project(Action<Project>? configure = null)
    {
        var project = new Project { Name = "Deal" };
        project.Requirements.Add(new ProjectRequirement { Title = "Pitch deck", Type = RequirementType.Document });
        configure?.Invoke(project);
        return project;
    }

    [Fact]
    public void A_workspace_without_dated_work_or_files_offers_only_projects_and_settings()
    {
        var sections = SidebarPolicy.Sections([Project()], isSample: false, googleConnected: false);

        Assert.Equal([false, true, false, false, true], new[] { AppPage.Dashboard, AppPage.Projects, AppPage.Calendar, AppPage.Documents, AppPage.Settings }.Select(page => sections[page]));
    }

    [Fact]
    public void A_dated_task_or_milestone_brings_the_attention_home_and_the_calendar()
    {
        var task = SidebarPolicy.Sections([Project(project => project.Tasks.Add(new ProjectTask { Title = "t", DueAt = Noon }))], false, false);
        var milestone = SidebarPolicy.Sections([Project(project => project.Milestones.Add(new Milestone { Title = "m", DueAt = Noon }))], false, false);
        var undated = SidebarPolicy.Sections([Project(project => project.Tasks.Add(new ProjectTask { Title = "t" }))], false, false);

        Assert.All(new[] { task, milestone }, sections => Assert.True(sections[AppPage.Dashboard] && sections[AppPage.Calendar] && !sections[AppPage.Documents]));
        Assert.False(undated[AppPage.Dashboard]);
        Assert.False(undated[AppPage.Calendar]);
    }

    [Fact]
    public void A_linked_file_brings_documents_and_a_google_connection_alone_brings_the_calendar()
    {
        var file = SidebarPolicy.Sections([Project(project => project.Requirements[0].Files.Add(new ProjectFile { FileName = "a.pdf", Path = "/a.pdf" }))], false, false);
        var google = SidebarPolicy.Sections([Project()], false, googleConnected: true);

        Assert.True(file[AppPage.Documents]);
        Assert.False(file[AppPage.Calendar]);
        Assert.True(google[AppPage.Calendar]);
        Assert.False(google[AppPage.Dashboard]);
    }

    [Fact]
    public void The_sample_workspace_shows_every_section()
    {
        var sections = SidebarPolicy.Sections([], isSample: true, googleConnected: false);

        Assert.All(sections.Values, shown => Assert.True(shown));
    }

    [Fact]
    public void Getting_started_counts_what_the_user_has_actually_done()
    {
        Assert.Equal([true, false, false, false, false], SidebarPolicy.GettingStartedDone([Project()], agentConfigured: false));
        var done = SidebarPolicy.GettingStartedDone([Project(project =>
        {
            project.Requirements[0].Status = RequirementStatus.Provided;
            project.Requirements[0].Files.Add(new ProjectFile { FileName = "a.pdf", Path = "/a.pdf" });
            project.Tasks.Add(new ProjectTask { Title = "t", DueAt = Noon });
        })], agentConfigured: true);
        Assert.All(done, step => Assert.True(step));
        Assert.True(SidebarPolicy.GettingStartedDone([Project(project => project.Requirements[0].Value = "text")], false)[1]);
        Assert.False(SidebarPolicy.GettingStartedDone([Project(project => project.Requirements[0].Value = "   ")], false)[1]);
    }

    private sealed class Host : ISidebarHost
    {
        public List<AppPage> Navigated { get; } = [];
        public Task NavigateAsync(AppPage page) { Navigated.Add(page); return Task.CompletedTask; }
        public Task RunAsync(Func<Task> action) => action();
    }

    private static (SidebarViewModel Sidebar, Host Host, PageScenario Pages) Create(PageScenario pages)
    {
        var host = new Host();
        var display = new DisplayOptionsViewModel(pages.Strings, pages.Locale, pages.Appearance, _ => { });
        return (new SidebarViewModel(pages.Strings, display, host), host, pages);
    }

    private static IReadOnlyDictionary<AppPage, bool> Sections(bool dashboard, bool calendar = false, bool documents = false) => new Dictionary<AppPage, bool>
    {
        [AppPage.Dashboard] = dashboard,
        [AppPage.Projects] = true,
        [AppPage.Calendar] = calendar,
        [AppPage.Documents] = documents,
        [AppPage.Settings] = true
    };

    [Fact]
    public async Task The_entries_are_the_five_sections_in_order_with_their_automation_names()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, _, _) = Create(pages);

        Assert.Equal(["NavigationDashboard", "NavigationProjects", "NavigationCalendar", "NavigationDocuments", "NavigationSettings"], sidebar.Items.Select(item => item.ControlName));
        Assert.Equal([pages.Strings["navigation.attention"], pages.Strings["navigation.projects"], pages.Strings["presentation.navigationCalendar"],
            pages.Strings["presentation.navigationDocuments"], pages.Strings["presentation.navigationSettings"]], sidebar.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task The_first_policy_result_flags_nothing_and_a_section_that_appears_later_is_flagged_new_once()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, _, _) = Create(pages);

        Assert.Empty(sidebar.Apply(Sections(dashboard: true), isSample: false));
        Assert.All(sidebar.Items, item => Assert.False(item.IsNew));
        Assert.Equal([true, true, false, false, true], sidebar.Items.Select(item => item.IsVisible));

        var revealed = sidebar.Apply(Sections(dashboard: true, calendar: true), isSample: false);

        Assert.Equal([AppPage.Calendar], revealed);
        Assert.True(sidebar.Items.Single(item => item.Page == AppPage.Calendar).IsNew);
        Assert.Empty(sidebar.Apply(Sections(dashboard: true, calendar: true), isSample: false));
        sidebar.MarkSeen(AppPage.Calendar);
        Assert.False(sidebar.Items.Single(item => item.Page == AppPage.Calendar).IsNew);
    }

    [Fact]
    public async Task Sections_the_sample_shows_are_never_flagged_and_reset_forgets_what_was_shown()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, _, _) = Create(pages);
        sidebar.Apply(Sections(dashboard: false), isSample: false);

        Assert.Empty(sidebar.Apply(Sections(dashboard: true, calendar: true, documents: true), isSample: true));
        Assert.All(sidebar.Items, item => Assert.False(item.IsNew));

        sidebar.Reset();
        Assert.Empty(sidebar.Apply(Sections(dashboard: false), isSample: false));
        Assert.Equal([AppPage.Dashboard], sidebar.Apply(Sections(dashboard: true), isSample: false));
        sidebar.Reset();
        Assert.All(sidebar.Items, item => Assert.False(item.IsNew));
    }

    [Fact]
    public async Task Selecting_a_section_marks_only_that_entry_and_darkens_its_icon()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, _, _) = Create(pages);

        sidebar.Select(AppPage.Calendar);

        Assert.Equal([AppPage.Calendar], sidebar.Items.Where(item => item.IsSelected).Select(item => item.Page));
        Assert.Equal("TextPrimary", sidebar.Items.Single(item => item.IsSelected).IconToken);
        Assert.All(sidebar.Items.Where(item => !item.IsSelected), item => Assert.Equal("TextSecondary", item.IconToken));
        Assert.True(sidebar.IsVisible(AppPage.Projects));
    }

    [Fact]
    public async Task Pressing_an_entry_asks_the_shell_to_show_that_section()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, host, _) = Create(pages);

        await sidebar.Items[2].NavigateCommand.ExecuteAsync(null);

        Assert.Equal([AppPage.Calendar], host.Navigated);
    }

    [Fact]
    public async Task The_attention_badge_is_localized_and_only_shows_for_overdue_work_on_the_attention_entry()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, _, _) = Create(pages);
        var dashboard = sidebar.Items[0];
        Assert.False(dashboard.HasCount);

        sidebar.AttentionCount = 3;
        Assert.True(sidebar.HasAttention);
        Assert.True(dashboard.HasCount);
        Assert.Equal(pages.Strings.Number(3), dashboard.CountText);
        Assert.All(sidebar.Items.Skip(1), item => Assert.False(item.HasCount));

        sidebar.AttentionCount = 0;
        Assert.False(dashboard.HasCount);
    }

    [Fact]
    public async Task The_getting_started_card_shows_progress_and_lets_only_unfinished_steps_be_pressed()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, _, _) = Create(pages);
        var went = new List<string>();
        var hidden = 0;
        Func<Task> Go(string key) => () => { went.Add(key); return Task.CompletedTask; };

        sidebar.ShowGettingStarted([new("v3.gs0", true, false, null), new("v3.gs1", false, false, Go("v3.gs1")), new("v3.gs2", false, false, null), new("v3.gs4", false, true, Go("v3.gs4"))],
            () => { hidden++; return Task.CompletedTask; });

        var card = sidebar.GettingStarted!;
        Assert.True(sidebar.HasGettingStarted);
        Assert.Equal(25, card.Percent);
        Assert.Equal($"{pages.Strings.Number(1)}/{pages.Strings.Number(4)}", card.CountText);
        Assert.Equal([false, true, false, true], card.Items.Select(item => item.CanGo));
        Assert.Equal([true, false, false, false], card.Items.Select(item => item.IsDone));
        Assert.Equal([false, false, false, true], card.Items.Select(item => item.IsOptional));
        Assert.Equal(["Success", "TextTertiary", "TextTertiary", "TextTertiary"], card.Items.Select(item => item.IconToken));
        Assert.Equal(Icons.CheckCircle, card.Items[0].Icon);
        Assert.Equal(Icons.Circle, card.Items[1].Icon);

        await card.Items[1].GoCommand.ExecuteAsync(null);
        await card.Items[0].GoCommand.ExecuteAsync(null);
        await card.HideCommand.ExecuteAsync(null);

        Assert.Equal(["v3.gs1"], went);
        Assert.Equal(1, hidden);
        sidebar.ShowGettingStarted(null, () => Task.CompletedTask);
        Assert.False(sidebar.HasGettingStarted);
    }

    [Fact]
    public async Task The_getting_started_titles_follow_a_language_switch()
    {
        using var pages = await PageScenario.CreateAsync();
        var (sidebar, _, _) = Create(pages);
        sidebar.ShowGettingStarted([new("v3.gs1", false, false, null)], () => Task.CompletedTask);
        var item = sidebar.GettingStarted!.Items[0];
        var english = item.Title;

        pages.Locale.SetLanguage("fa");

        Assert.NotEqual(english, item.Title);
        Assert.Equal(pages.Strings["v3.gs1"], item.Title);
    }
}
