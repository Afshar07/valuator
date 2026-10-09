using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.ProjectDetail;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The project header, its tabs and the edit-project dialog as plain view-models.</summary>
public sealed class ProjectDetailFeatureTests
{
    private static ProjectDetailViewModel Detail(ProjectScenario scenario, Project project, ProjectTab tab = ProjectTab.Overview) =>
        new(project, [], tab, scenario.Services, agentConfigured: true);

    [Fact]
    public async Task The_header_shows_identity_stage_status_and_readiness_from_stored_state()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Nova Logistics", "Nova Freight", ProjectStatus.OnHold, "Sam");
        project.Requirements[0].Status = RequirementStatus.Complete;
        project.Requirements[1].Status = RequirementStatus.Provided;
        var L = scenario.L;

        var detail = Detail(scenario, project);

        Assert.Equal("Nova Logistics", detail.Title);
        Assert.Equal("Nova Freight · Sam", detail.Subtitle);
        Assert.Same(project.Stage, detail.Stage);
        Assert.True(detail.ShowStatus);
        Assert.Equal(L.Enum(ProjectStatus.OnHold), detail.StatusText);
        Assert.Equal("1/16", detail.ReadinessText);
        Assert.Equal(100.0 / 16, detail.ReadinessPercent, 6);
        Assert.Equal(L.Format("overview.readiness", 1, 16, 100.0 / 16), detail.ReadinessTip);
        Assert.Equal(L["navigation.projects"], detail.BackText);
    }

    [Fact]
    public async Task An_active_project_shows_no_status_pill_and_blank_company_or_owner_leave_no_stray_separator()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Solo", company: "", owner: "Sam");

        var detail = Detail(scenario, project);

        Assert.False(detail.ShowStatus);
        Assert.Equal("Sam", detail.Subtitle);
    }

    [Fact]
    public async Task The_back_arrow_points_the_way_back_in_both_directions_and_follows_a_language_switch()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        var detail = Detail(scenario, project);
        var changed = new List<string?>();
        detail.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal(Icons.ArrowLeft, detail.BackIcon);
        scenario.Locale.SetLanguage("fa");

        Assert.Equal(Icons.ArrowRight, detail.BackIcon);
        Assert.Contains(string.Empty, changed);
        Assert.Equal(scenario.L["navigation.projects"], detail.BackText);
    }

    [Fact]
    public async Task Back_goes_to_the_project_list_under_the_busy_state()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var detail = Detail(scenario, await scenario.AddProjectAsync());

        await detail.BackCommand.ExecuteAsync(null);

        Assert.Equal(1, scenario.Host.Runs);
        Assert.Equal(new PageRoute(AppPage.Projects), Assert.Single(scenario.Pages.Navigator.Visited));
    }

    [Theory]
    [InlineData(ProjectTab.Overview)]
    [InlineData(ProjectTab.Checklist)]
    [InlineData(ProjectTab.Tasks)]
    [InlineData(ProjectTab.Delegate)]
    public async Task It_opens_on_the_requested_tab_without_announcing_it(ProjectTab tab)
    {
        using var scenario = await ProjectScenario.CreateAsync();

        var detail = Detail(scenario, await scenario.AddProjectAsync(), tab);

        Assert.Equal((int)tab, detail.SelectedIndex);
        Assert.Empty(scenario.Host.Tabs);
        Assert.Equal(0, scenario.Host.AssistantOpens);
    }

    [Fact]
    public async Task Choosing_a_tab_records_it_for_the_next_reload_and_only_the_delegate_tab_opens_the_assistant()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var detail = Detail(scenario, await scenario.AddProjectAsync());

        detail.SelectedIndex = 1;
        detail.SelectedIndex = 2;
        Assert.Equal([ProjectTab.Checklist, ProjectTab.Tasks], scenario.Host.Tabs);
        Assert.Equal(0, scenario.Host.AssistantOpens);

        detail.SelectedIndex = 3;
        Assert.Equal(ProjectTab.Delegate, scenario.Host.Tabs[^1]);
        Assert.Equal(1, scenario.Host.AssistantOpens);

        detail.SelectedIndex = -1;
        Assert.Equal(3, scenario.Host.Tabs.Count);
    }

    [Fact]
    public async Task The_assistant_button_toggles_the_assistant_panel()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var detail = Detail(scenario, await scenario.AddProjectAsync());

        detail.ToggleAssistantCommand.Execute(null);

        Assert.Equal(1, scenario.Host.AssistantToggles);
    }

    [Fact]
    public async Task The_tabs_hold_one_view_model_each_and_the_lock_is_a_plain_property()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var detail = new ProjectDetailViewModel(await scenario.AddProjectAsync(), [], ProjectTab.Overview, scenario.Services, agentConfigured: true);
        var changed = new List<string?>();
        detail.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.NotNull(detail.Overview);
        Assert.NotNull(detail.Requirements);
        Assert.NotNull(detail.Tasks);
        Assert.NotNull(detail.Delegation);
        Assert.False(detail.IsLocked);
        detail.IsLocked = true;
        Assert.Equal([nameof(detail.IsLocked)], changed);
    }

    [Fact]
    public async Task Edit_loads_the_stages_of_the_projects_template_and_shows_the_dialog()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync();
        var detail = Detail(scenario, project);

        await detail.EditCommand.ExecuteAsync(null);

        var dialog = Assert.IsType<EditProjectDialogViewModel>(scenario.Dialogs.Shown);
        Assert.Equal((await scenario.Pages.Projects.ListStagesAsync(project.TemplateId)).Select(stage => stage.Id), dialog.Stages.Select(stage => stage.Id));
        Assert.Equal(project.StageId, dialog.SelectedStage!.Id);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task The_dialog_starts_from_the_project_and_saves_every_field_then_reloads()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Deal", "Company", ProjectStatus.Active, "Sam");
        var stages = await scenario.Pages.Projects.ListStagesAsync(project.TemplateId);
        var dialog = new EditProjectDialogViewModel(project, stages, scenario.Services);
        Assert.Equal(("Deal", "Company", "Sam"), (dialog.Name, dialog.Company, dialog.Owner));
        Assert.Equal(ProjectStatus.Active, dialog.Status.Selected);
        var target = stages.First(stage => stage.Id != project.StageId);

        dialog.Name = "  Renamed  "; dialog.Company = " New Co "; dialog.Owner = "Alex";
        dialog.SelectedStage = target; dialog.Status.Selected = ProjectStatus.Completed;
        dialog.Notes = "Some notes"; dialog.Summary = "Where it stands";
        await dialog.SaveCommand.ExecuteAsync(null);

        var saved = await scenario.ReloadAsync(project);
        Assert.Equal(("Renamed", "New Co", "Alex"), (saved.Name, saved.CompanyName, saved.Owner));
        Assert.Equal(target.Id, saved.StageId);
        Assert.Equal(ProjectStatus.Completed, saved.Status);
        Assert.Equal("Some notes", saved.Notes);
        Assert.Equal("Where it stands", saved.State.Summary);
        Assert.False(scenario.Dialogs.IsOpen);
        Assert.Equal(1, scenario.Host.Refreshes);
    }

    [Fact]
    public async Task A_blank_name_shows_its_error_until_the_name_changes_and_saves_nothing()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Deal");
        scenario.Dialogs.Show("open");
        var dialog = new EditProjectDialogViewModel(project, await scenario.Pages.Projects.ListStagesAsync(project.TemplateId), scenario.Services);

        dialog.Name = "   ";
        await dialog.SaveCommand.ExecuteAsync(null);

        Assert.True(dialog.ShowNameError);
        Assert.Equal("Deal", (await scenario.ReloadAsync(project)).Name);
        Assert.True(scenario.Dialogs.IsOpen);
        Assert.Equal(0, scenario.Host.Refreshes);
        Assert.Equal(0, scenario.Host.Runs);

        dialog.Name = "Deal 2";
        Assert.False(dialog.ShowNameError);
    }

    [Fact]
    public async Task Cancel_closes_the_dialog_and_leaves_the_project_alone()
    {
        using var scenario = await ProjectScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Deal");
        scenario.Dialogs.Show("open");
        var dialog = new EditProjectDialogViewModel(project, await scenario.Pages.Projects.ListStagesAsync(project.TemplateId), scenario.Services) { Name = "Changed" };

        dialog.CancelCommand.Execute(null);

        Assert.False(scenario.Dialogs.IsOpen);
        Assert.Equal("Deal", (await scenario.ReloadAsync(project)).Name);
        Assert.Equal(0, scenario.Host.Refreshes);
    }
}
