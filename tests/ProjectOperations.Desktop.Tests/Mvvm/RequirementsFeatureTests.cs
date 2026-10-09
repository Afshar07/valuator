using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.Requirements;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The Requirements &amp; files tab as plain view-models: the checklist, its editor, follow-up tasks and the blank-project add line.</summary>
public sealed class RequirementsFeatureTests
{
    private static readonly DateTimeOffset Noon = PageScenario.Noon;

    private static RequirementsViewModel Checklist(ProjectScenario scenario, Project project) => new(project, scenario.Services);

    private static RequirementRowViewModel Row(RequirementsViewModel checklist, ProjectRequirement requirement) =>
        checklist.Groups.SelectMany(group => group.Rows).Single(row => row.Id == requirement.Id);

    private static async Task<(Project Project, ProjectRequirement Deck, ProjectRequirement Text)> SeedAsync(ProjectScenario scenario)
    {
        var project = await scenario.AddProjectAsync();
        var deck = project.Requirements.Single(requirement => requirement.DefinitionId == "pitch-deck");
        var text = project.Requirements.Single(requirement => requirement.DefinitionId == "operations");
        return (project, deck, text);
    }

    [Fact]
    public async Task Groups_follow_the_template_with_the_first_open_and_their_own_completion_figures()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, _) = await SeedAsync(scenario);
        deck.Status = RequirementStatus.Complete;
        var L = scenario.L;

        var checklist = Checklist(scenario, project);

        Assert.Equal(["Business information", "Financial information", "Shareholder information", "Supplemental information"], checklist.Groups.Select(group => group.Title));
        Assert.Equal([true, false, false, false], checklist.Groups.Select(group => group.IsExpanded));
        var first = checklist.Groups[0];
        Assert.Equal(6, first.Rows.Count);
        Assert.Equal(L.Format("presentation.groupComplete", "1", "6"), first.SummaryText);
        Assert.Equal(100.0 / 6, first.Percent, 6);
        Assert.Equal($"{first.Title} · {first.SummaryText}", first.AccessibleName);
        Assert.Equal(Icons.CaretDown, first.CaretIcon);
        Assert.Equal(Icons.CaretRight, checklist.Groups[1].CaretIcon);
        Assert.Contains(project.Id.ToString(), scenario.State.ExpandedGroups.Single());
    }

    [Fact]
    public async Task Only_the_first_visit_to_a_project_opens_its_first_group()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, _) = await SeedAsync(scenario);
        var first = Checklist(scenario, project);
        first.Groups[0].ToggleCommand.Execute(null);

        var again = Checklist(scenario, project);

        Assert.False(again.Groups[0].IsExpanded);
    }

    [Fact]
    public async Task Toggling_a_group_remembers_it_in_the_view_state_so_a_reload_keeps_it()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, _) = await SeedAsync(scenario);
        var checklist = Checklist(scenario, project);

        checklist.Groups[2].ToggleCommand.Execute(null);
        Assert.True(checklist.Groups[2].IsExpanded);
        Assert.Equal(Icons.CaretDown, checklist.Groups[2].CaretIcon);
        Assert.True(Checklist(scenario, project).Groups[2].IsExpanded);

        checklist.Groups[2].ToggleCommand.Execute(null);
        Assert.False(Checklist(scenario, project).Groups[2].IsExpanded);
    }

    [Fact]
    public async Task The_caret_of_a_closed_group_points_to_the_start_edge_in_either_language()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, _) = await SeedAsync(scenario);
        var group = Checklist(scenario, project).Groups[1];
        Assert.Equal(Icons.CaretRight, group.CaretIcon);

        scenario.Locale.SetLanguage("fa");

        Assert.Equal(Icons.CaretLeft, group.CaretIcon);
        Assert.Equal(scenario.L.Group("business", "Business information"), Checklist(scenario, project).Groups[0].Title);
    }

    [Fact]
    public async Task A_row_shows_status_attachments_and_a_summary_for_assistive_technology()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, text) = await SeedAsync(scenario);
        deck.Status = RequirementStatus.Provided;
        deck.Files.AddRange([new ProjectFile { FileName = "a.pdf", Path = "/a.pdf" }, new ProjectFile { FileName = "b.pdf", Path = "/b.pdf" }]);
        text.Status = RequirementStatus.Provided; text.Value = "  Running smoothly  ";
        var L = scenario.L;
        var checklist = Checklist(scenario, project);

        var withFiles = Row(checklist, deck);
        Assert.True(withFiles.HasFiles);
        Assert.Equal("a.pdf", withFiles.FirstFileName);
        Assert.True(withFiles.HasMoreFiles);
        Assert.Equal("+1", withFiles.MoreFilesText);
        Assert.False(withFiles.HasValue);
        Assert.Equal(PillKind.Accent, withFiles.StatusKind);
        Assert.Equal(L.Format("requirement.summary", L.Requirement(deck), L.Enum(RequirementType.Document), L.Enum(RequirementStatus.Provided), 2), withFiles.AccessibleName);

        var withValue = Row(checklist, text);
        Assert.False(withValue.HasFiles);
        Assert.True(withValue.HasValue);
        Assert.Equal("Running smoothly", withValue.ValueText);
    }

    [Theory]
    [InlineData(RequirementStatus.Missing, PillKind.Error)]
    [InlineData(RequirementStatus.Provided, PillKind.Accent)]
    [InlineData(RequirementStatus.NeedsReview, PillKind.Warning)]
    [InlineData(RequirementStatus.Complete, PillKind.Success)]
    public async Task Each_status_has_its_own_pill_colour(RequirementStatus status, PillKind kind)
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, _) = await SeedAsync(scenario);
        deck.Status = status;

        Assert.Equal(kind, Row(Checklist(scenario, project), deck).StatusKind);
    }

    [Fact]
    public async Task Next_up_is_the_first_missing_item_and_opening_it_expands_its_group_and_opens_its_editor()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, text) = await SeedAsync(scenario);
        deck.Status = RequirementStatus.Complete; text.Status = RequirementStatus.Provided;
        var next = project.Requirements.First(requirement => requirement.Status == RequirementStatus.Missing);
        var checklist = Checklist(scenario, project);

        Assert.True(checklist.HasNextUp);
        Assert.Equal(scenario.L.Requirement(next), checklist.NextUpTitle);
        Assert.Equal(scenario.L["v3.hint_" + next.Type.ToString().ToLowerInvariant()], checklist.NextUpHint);

        checklist.OpenNextUpCommand.Execute(null);

        var row = Row(checklist, next);
        Assert.True(row.IsOpen);
        Assert.NotNull(row.Detail);
        Assert.Equal(next.Id, scenario.State.OpenRequirement);
        Assert.True(checklist.Groups.Single(group => group.Rows.Contains(row)).IsExpanded);
        Assert.Equal(Icons.CaretUp, row.CaretIcon);
    }

    [Fact]
    public async Task When_nothing_is_missing_next_up_gives_way_to_the_review_note()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, _) = await SeedAsync(scenario);
        foreach (var requirement in project.Requirements) requirement.Status = RequirementStatus.Provided;

        var checklist = Checklist(scenario, project);

        Assert.False(checklist.HasNextUp);
        Assert.Equal("", checklist.NextUpTitle);
        checklist.OpenNextUpCommand.Execute(null);
        Assert.Null(scenario.State.OpenRequirement);
    }

    [Fact]
    public async Task One_row_is_open_at_a_time_and_opening_another_closes_the_first_and_drops_what_was_typed()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, text) = await SeedAsync(scenario);
        var checklist = Checklist(scenario, project);
        var first = Row(checklist, deck); var second = Row(checklist, text);

        first.ToggleCommand.Execute(null);
        Assert.True(first.IsOpen);
        Assert.Equal(Icons.CaretUp, first.CaretIcon);
        second.ToggleCommand.Execute(null);
        Assert.False(first.IsOpen);
        Assert.Null(first.Detail);
        Assert.Equal(Icons.CaretDown, first.CaretIcon);
        Assert.True(second.IsOpen);
        Assert.Equal(text.Id, scenario.State.OpenRequirement);

        second.Detail!.Draft = "typed but not saved";
        second.ToggleCommand.Execute(null);
        Assert.Null(scenario.State.OpenRequirement);
        second.ToggleCommand.Execute(null);
        Assert.Equal("", second.Detail!.Draft);
    }

    [Fact]
    public async Task The_open_row_survives_a_reload_through_the_view_state()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        Row(Checklist(scenario, project), text).ToggleCommand.Execute(null);

        var reloaded = Row(Checklist(scenario, project), text);

        Assert.True(reloaded.IsOpen);
        Assert.NotNull(reloaded.Detail);
    }

    [Fact]
    public async Task A_value_requirement_edits_a_value_and_a_document_links_files()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, text) = await SeedAsync(scenario);
        text.Value = "Existing value";
        var checklist = Checklist(scenario, project);
        var L = scenario.L;

        var value = Row(checklist, text); value.ToggleCommand.Execute(null);
        Assert.True(value.Detail!.IsValue);
        Assert.False(value.Detail.IsDocument);
        Assert.Equal("Existing value", value.Detail.Draft);
        Assert.Equal(L.Enum(RequirementType.Text) + ".", value.Detail.TypeLabel);
        Assert.Equal(L["v3.hint_text"], value.Detail.Hint);

        var document = Row(checklist, deck); document.ToggleCommand.Execute(null);
        Assert.True(document.Detail!.IsDocument);
        Assert.False(document.Detail.IsValue);
        Assert.Equal(L["v3.hint_document"], document.Detail.Hint);
    }

    [Fact]
    public async Task Saving_a_value_marks_a_missing_item_provided_and_stamps_the_review_time_then_reloads()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        var row = Row(Checklist(scenario, project), text); row.ToggleCommand.Execute(null);

        row.Detail!.Draft = "  Awaiting revised deck  ";
        await row.Detail.SaveValueCommand.ExecuteAsync(null);

        var saved = (await scenario.ReloadAsync(project)).Requirements.Single(requirement => requirement.Id == text.Id);
        Assert.Equal("Awaiting revised deck", saved.Value);
        Assert.Equal(RequirementStatus.Provided, saved.Status);
        Assert.Equal(Noon, saved.LastReviewedAt);
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task Saving_a_value_keeps_a_status_the_user_already_chose()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        text.Status = RequirementStatus.Complete;
        var row = Row(Checklist(scenario, project), text); row.ToggleCommand.Execute(null);

        row.Detail!.Draft = "New value";
        await row.Detail.SaveValueCommand.ExecuteAsync(null);

        Assert.Equal(RequirementStatus.Complete, (await scenario.ReloadAsync(project)).Requirements.Single(requirement => requirement.Id == text.Id).Status);
    }

    [Fact]
    public async Task A_blank_value_is_not_saved()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        var row = Row(Checklist(scenario, project), text); row.ToggleCommand.Execute(null);

        row.Detail!.Draft = "   ";
        await row.Detail.SaveValueCommand.ExecuteAsync(null);

        Assert.Equal(0, scenario.Host.Refreshes);
        Assert.Equal(RequirementStatus.Missing, (await scenario.ReloadAsync(project)).Requirements.Single(requirement => requirement.Id == text.Id).Status);
    }

    [Fact]
    public async Task Choosing_a_status_saves_it_at_once_and_marks_the_chip()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        text.Status = RequirementStatus.Provided;
        var row = Row(Checklist(scenario, project), text); row.ToggleCommand.Execute(null);
        var chips = row.Detail!.Statuses;

        Assert.Equal([RequirementStatus.Missing, RequirementStatus.Provided, RequirementStatus.NeedsReview, RequirementStatus.Complete], chips.Select(chip => chip.Status));
        Assert.Equal([false, true, false, false], chips.Select(chip => chip.IsSelected));
        Assert.Equal(scenario.L.Enum(RequirementStatus.NeedsReview), chips[2].Label);
        Assert.Equal([true, false, false, false], chips.Select(chip => chip.IsMissing));

        await chips[3].SelectCommand.ExecuteAsync(null);

        var saved = (await scenario.ReloadAsync(project)).Requirements.Single(requirement => requirement.Id == text.Id);
        Assert.Equal(RequirementStatus.Complete, saved.Status);
        Assert.Equal(Noon, saved.LastReviewedAt);
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task Choosing_files_links_the_new_ones_marks_the_item_provided_and_skips_what_is_already_linked()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, _) = await SeedAsync(scenario);
        var existing = scenario.Pages.TempFile("existing.pdf", "old");
        var fresh = scenario.Pages.TempFile("fresh.pdf", "new content");
        deck.Files.Add(new ProjectFile { FileName = "existing.pdf", Path = existing });
        deck.Status = RequirementStatus.Missing;
        var row = Row(Checklist(scenario, project), deck); row.ToggleCommand.Execute(null);

        scenario.Picker.Next = [new PickedFile("existing.pdf", existing), new PickedFile("fresh.pdf", fresh)];
        await row.Detail!.PickFilesCommand.ExecuteAsync(null);

        Assert.Equal([scenario.L["file.pickerTitle"]], scenario.Picker.Titles);
        var saved = (await scenario.ReloadAsync(project)).Requirements.Single(requirement => requirement.Id == deck.Id);
        Assert.Equal(["existing.pdf", "fresh.pdf"], saved.Files.Select(file => file.FileName));
        Assert.Equal(new FileInfo(fresh).Length, saved.Files[1].SizeBytes);
        Assert.Equal(RequirementStatus.Provided, saved.Status);
        Assert.Equal(Noon, saved.LastReviewedAt);
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task Cancelling_the_picker_or_choosing_only_linked_files_saves_nothing_and_a_remote_file_is_refused()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, _) = await SeedAsync(scenario);
        var existing = scenario.Pages.TempFile("existing.pdf");
        deck.Files.Add(new ProjectFile { FileName = "existing.pdf", Path = existing });
        await scenario.Pages.Projects.SaveAsync(project);
        var row = Row(Checklist(scenario, project), deck); row.ToggleCommand.Execute(null);

        scenario.Picker.Next = [];
        await row.Detail!.PickFilesCommand.ExecuteAsync(null);
        scenario.Picker.Next = [new PickedFile("existing.pdf", existing), new PickedFile("cloud.pdf", null)];
        await row.Detail.PickFilesCommand.ExecuteAsync(null);

        Assert.Equal(["validation.localFilesOnly"], scenario.Host.Errors);
        Assert.Equal(0, scenario.Host.Refreshes);
        Assert.Single((await scenario.ReloadAsync(project)).Requirements.Single(requirement => requirement.Id == deck.Id).Files);
    }

    [Fact]
    public async Task Unlinking_removes_the_reference_and_leaves_the_file_on_disk()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, deck, _) = await SeedAsync(scenario);
        var path = scenario.Pages.TempFile("keep.pdf", "keep me");
        deck.Files.Add(new ProjectFile { FileName = "keep.pdf", Path = path });
        var row = Row(Checklist(scenario, project), deck); row.ToggleCommand.Execute(null);
        var link = Assert.Single(row.Detail!.Files);
        Assert.Equal("keep.pdf", link.FileName);
        Assert.Equal(Icons.FilePdf, link.Icon);
        Assert.True(row.Detail.HasFiles);

        await link.UnlinkCommand.ExecuteAsync(null);

        Assert.Empty((await scenario.ReloadAsync(project)).Requirements.Single(requirement => requirement.Id == deck.Id).Files);
        Assert.Equal("keep me", await File.ReadAllTextAsync(path));
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task The_follow_up_form_starts_with_a_title_and_a_due_chip_and_closes_on_cancel()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        var row = Row(Checklist(scenario, project), text); row.ToggleCommand.Execute(null);
        var detail = row.Detail!;
        Assert.True(detail.HasNoForm);
        Assert.False(detail.HasForm);

        detail.AddTaskCommand.Execute(null);

        var form = detail.Form!;
        Assert.True(detail.HasForm);
        Assert.False(detail.HasNoForm);
        Assert.Equal($"{scenario.L["v3.followUp"]} {scenario.L.Requirement(text)}", form.Title);
        Assert.Equal(1, form.Due.Selected);
        Assert.Equal([scenario.L["v3.dTomorrow"], scenario.L["v3.dWeek"], scenario.L["v3.d2Week"], scenario.L["v3.dNone"]], form.Due.Options.Select(option => option.Label));

        form.CancelCommand.Execute(null);
        Assert.Null(detail.Form);
        Assert.True(detail.HasNoForm);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 7)]
    [InlineData(2, 14)]
    public async Task Adding_a_follow_up_creates_a_task_linked_to_the_item_due_at_the_end_of_the_chosen_day(int chip, int days)
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        var row = Row(Checklist(scenario, project), text); row.ToggleCommand.Execute(null);
        row.Detail!.AddTaskCommand.Execute(null);
        var form = row.Detail.Form!;

        form.Title = "  Ask for the numbers  ";
        form.Due.Options[chip].SelectCommand.Execute(null);
        await form.CreateCommand.ExecuteAsync(null);

        var task = Assert.Single((await scenario.ReloadAsync(project)).Tasks);
        Assert.Equal("Ask for the numbers", task.Title);
        Assert.Equal(text.Id, task.RequirementId);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 23, 59, 0, TimeSpan.Zero).AddDays(days), task.DueAt);
        Assert.Equal(["v3.toastTask"], scenario.Host.Toasts);
        Assert.Equal(1, scenario.Host.Refreshes);
        Assert.Null(row.Detail.Form);
    }

    [Fact]
    public async Task A_follow_up_can_have_no_date_and_a_blank_title_is_not_created()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        var row = Row(Checklist(scenario, project), text); row.ToggleCommand.Execute(null);
        row.Detail!.AddTaskCommand.Execute(null);
        var form = row.Detail.Form!;

        form.Title = "   ";
        await form.CreateCommand.ExecuteAsync(null);
        Assert.Empty((await scenario.ReloadAsync(project)).Tasks);
        Assert.Equal(0, scenario.Host.Refreshes);

        form.Title = "Someday";
        form.Due.Options[3].SelectCommand.Execute(null);
        await form.CreateCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single((await scenario.ReloadAsync(project)).Tasks).DueAt);
    }

    [Fact]
    public async Task The_getting_started_shortcut_opens_the_follow_up_form_once()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        scenario.State.OpenRequirement = text.Id;
        scenario.State.PendingFollowUp = text.Id;

        var checklist = Checklist(scenario, project);

        Assert.NotNull(Row(checklist, text).Detail!.Form);
        Assert.Null(scenario.State.PendingFollowUp);
        Assert.Null(Row(Checklist(scenario, project), text).Detail!.Form);
    }

    [Fact]
    public async Task The_tip_shows_until_dismissed_and_stays_dismissed()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, _) = await SeedAsync(scenario);
        var checklist = Checklist(scenario, project);
        Assert.True(checklist.IsTipVisible);

        checklist.DismissTipCommand.Execute(null);

        Assert.False(checklist.IsTipVisible);
        Assert.False(Checklist(scenario, project).IsTipVisible);
    }

    [Fact]
    public async Task Only_a_blank_project_offers_to_add_requirements()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (template, _, _) = await SeedAsync(scenario);
        var blank = await scenario.AddBlankProjectAsync();

        Assert.False(Checklist(scenario, template).IsBlank);
        Assert.True(Checklist(scenario, blank).IsBlank);
        Assert.Empty(Checklist(scenario, blank).Groups);
    }

    [Fact]
    public async Task Adding_a_requirement_saves_it_opens_it_and_reloads_with_its_group_expanded()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var blank = await scenario.AddBlankProjectAsync();
        var checklist = Checklist(scenario, blank);
        Assert.True(checklist.IsNotAdding);

        checklist.StartAddingCommand.Execute(null);
        Assert.True(checklist.IsAdding);
        Assert.False(checklist.IsNotAdding);
        checklist.NewRequirementTitle = "  Customer contracts  ";
        await checklist.AddRequirementCommand.ExecuteAsync(null);

        var saved = Assert.Single((await scenario.ReloadAsync(blank)).Requirements);
        Assert.Equal("Customer contracts", saved.Title);
        Assert.False(checklist.IsAdding);
        Assert.Equal(saved.Id, scenario.State.OpenRequirement);
        Assert.Equal(1, scenario.Host.Refreshes);

        var reloaded = Checklist(scenario, await scenario.ReloadAsync(blank));
        Assert.Equal(scenario.L["v3.blankGroup"], reloaded.Groups.Single().Title);
        Assert.True(reloaded.Groups.Single().IsExpanded);
        Assert.True(reloaded.Groups.Single().Rows.Single().IsOpen);
    }

    [Fact]
    public async Task A_blank_title_is_not_added_and_cancel_closes_the_line()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var blank = await scenario.AddBlankProjectAsync();
        var checklist = Checklist(scenario, blank);
        checklist.StartAddingCommand.Execute(null);

        checklist.NewRequirementTitle = "   ";
        await checklist.AddRequirementCommand.ExecuteAsync(null);
        Assert.Empty((await scenario.ReloadAsync(blank)).Requirements);
        Assert.True(checklist.IsAdding);

        checklist.NewRequirementTitle = "Typed";
        checklist.CancelAddingCommand.Execute(null);
        Assert.False(checklist.IsAdding);
        checklist.StartAddingCommand.Execute(null);
        Assert.Equal("", checklist.NewRequirementTitle);
    }

    [Fact]
    public async Task A_language_switch_keeps_what_was_typed_and_retitles_the_rows()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var (project, _, text) = await SeedAsync(scenario);
        var checklist = Checklist(scenario, project);
        var row = Row(checklist, text); row.ToggleCommand.Execute(null);
        row.Detail!.Draft = "Unsaved English / فارسی";
        var english = row.Title;

        scenario.Locale.SetLanguage("fa");

        Assert.Equal("Unsaved English / فارسی", row.Detail.Draft);
        Assert.NotEqual(english, row.Title);
        Assert.Equal(scenario.L.Requirement(text), row.Title);
        Assert.True(row.IsOpen);
        Assert.Equal(scenario.L.Enum(RequirementStatus.Missing), row.StatusText);
        Assert.True(scenario.L.IsRightToLeft);
    }
}
