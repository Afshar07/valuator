using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Features.Overview;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The Overview tab as plain view-models: current state, missing requirements, and the open-questions and follow-ups lists.</summary>
public sealed class OverviewFeatureTests
{
    private static readonly DateTimeOffset Noon = PageScenario.Noon;

    [Fact]
    public async Task The_current_state_card_reads_the_facts_from_the_project()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        project.Requirements[0].Status = RequirementStatus.Complete;
        project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "IC review", DueAt = Noon.AddDays(3) });
        project.Milestones.Add(new Milestone { ProjectId = project.Id, Title = "Done already", DueAt = Noon.AddDays(1), IsComplete = true });
        project.Tasks.Add(new ProjectTask { ProjectId = project.Id, Title = "Late", DueAt = Noon.AddDays(-2) });
        project.State.OpenQuestions.Add("Who signs?");
        project.State.FollowUps.AddRange(["Call", "Email"]);
        project.State.Summary = "Reviewing supplied information";
        project.Notes = "Synthetic notes";

        var overview = new OverviewViewModel(project, scenario.Services);

        Assert.Equal("IC review · " + scenario.L.ShortDate(Noon.AddDays(3)), overview.NextMilestoneText);
        Assert.Equal("1/16", overview.ReadinessText);
        Assert.Equal("1", overview.OverdueText);
        Assert.True(overview.HasOverdue);
        Assert.Equal("1", overview.OpenQuestionsCount);
        Assert.Equal("2", overview.FollowUpsCount);
        Assert.True(overview.HasSummary);
        Assert.Equal("Reviewing supplied information", overview.SummaryText);
        Assert.True(overview.HasNotes);
        Assert.Equal("Synthetic notes", overview.NotesText);
        Assert.Same(project.Stage, overview.Stage);
    }

    [Fact]
    public async Task A_project_with_nothing_set_says_so_and_flags_no_overdue_work()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();

        var overview = new OverviewViewModel(project, scenario.Services);

        Assert.Equal(scenario.L["overview.noMilestone"], overview.NextMilestoneText);
        Assert.False(overview.HasOverdue);
        Assert.False(overview.HasSummary);
        Assert.False(overview.HasNotes);
        Assert.Equal(scenario.L["date.notSet"], overview.NotesText);
    }

    [Fact]
    public async Task Missing_lists_requirements_that_are_missing_or_need_review_and_nothing_that_is_provided_or_complete()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        project.Requirements[0].Status = RequirementStatus.Complete;
        project.Requirements[1].Status = RequirementStatus.Provided;
        project.Requirements[2].Status = RequirementStatus.NeedsReview;

        var overview = new OverviewViewModel(project, scenario.Services);

        Assert.Equal(14, overview.MissingItems.Count);
        Assert.Equal("14", overview.MissingCount);
        Assert.False(overview.HasNoMissing);
        var review = overview.MissingItems[0];
        Assert.Equal(scenario.L.Requirement(project.Requirements[2]), review.Title);
        Assert.Equal(scenario.L.Enum(RequirementStatus.NeedsReview), review.StatusText);
        Assert.False(review.IsMissing);
        Assert.True(overview.MissingItems[1].IsMissing);
        Assert.DoesNotContain(overview.MissingItems, item => item.Title == scenario.L.Requirement(project.Requirements[0]));
    }

    [Fact]
    public async Task When_everything_is_reviewed_the_missing_card_says_so()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        foreach (var requirement in project.Requirements) requirement.Status = RequirementStatus.Complete;

        var overview = new OverviewViewModel(project, scenario.Services);

        Assert.True(overview.HasNoMissing);
        Assert.Equal("0", overview.MissingCount);
    }

    [Fact]
    public async Task Missing_titles_and_counts_follow_a_language_switch()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        var overview = new OverviewViewModel(project, scenario.Services);
        var english = overview.MissingItems[0].Title;
        var changed = new List<string?>();
        overview.MissingItems[0].PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        scenario.Locale.SetLanguage("fa");

        Assert.NotEqual(english, overview.MissingItems[0].Title);
        Assert.Equal(scenario.L.Requirement(project.Requirements[0]), overview.MissingItems[0].Title);
        Assert.Contains(string.Empty, changed);
        Assert.Equal(scenario.L.Number(16), new OverviewViewModel(project, scenario.Services).MissingCount);
    }

    [Fact]
    public async Task The_two_lists_show_their_items_with_their_own_titles_icons_and_card_names()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        project.State.OpenQuestions.Add("Who signs?");

        var overview = new OverviewViewModel(project, scenario.Services);

        Assert.Equal(scenario.L["presentation.openQuestions"], overview.OpenQuestions.Title);
        Assert.Equal(scenario.L["presentation.followUps"], overview.FollowUps.Title);
        Assert.Equal("OpenQuestionsCard", overview.OpenQuestions.CardName);
        Assert.Equal("FollowUpsCard", overview.FollowUps.CardName);
        var item = Assert.Single(overview.OpenQuestions.Items);
        Assert.Equal(("Who signs?", Icons.Question), (item.Text, item.Icon));
        Assert.Equal("1", overview.OpenQuestions.CountText);
        Assert.False(overview.OpenQuestions.IsEmpty);
        Assert.True(overview.FollowUps.IsEmpty);
    }

    [Fact]
    public async Task Adding_an_item_opens_a_blank_line_and_saving_stores_it_in_the_projects_state_and_reloads()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        var overview = new OverviewViewModel(project, scenario.Services);
        var list = overview.OpenQuestions;

        Assert.False(list.IsAdding);
        list.StartAddCommand.Execute(null);
        Assert.True(list.IsAdding);
        Assert.Equal("", list.NewText);

        list.NewText = "  Who signs?  ";
        await list.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["Who signs?"], (await scenario.ReloadAsync(project)).State.OpenQuestions);
        Assert.Empty((await scenario.ReloadAsync(project)).State.FollowUps);
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task A_blank_line_is_not_saved_and_cancel_closes_the_line_without_saving()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        var list = new OverviewViewModel(project, scenario.Services).FollowUps;
        list.StartAddCommand.Execute(null);

        list.NewText = "   ";
        await list.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, scenario.Host.Refreshes);
        Assert.Empty((await scenario.ReloadAsync(project)).State.FollowUps);
        Assert.True(list.IsAdding);

        list.NewText = "Call them";
        list.CancelCommand.Execute(null);
        Assert.False(list.IsAdding);
        Assert.Empty((await scenario.ReloadAsync(project)).State.FollowUps);

        list.StartAddCommand.Execute(null);
        Assert.Equal("", list.NewText);
    }
}
