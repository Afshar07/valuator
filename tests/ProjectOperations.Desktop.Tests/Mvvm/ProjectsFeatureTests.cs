using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.Projects;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The All projects table and its delete confirmation as plain view-models: no window, no controls, a fixed clock.</summary>
public sealed class ProjectsFeatureTests
{
    private static readonly DateTimeOffset Noon = PageScenario.Noon;

    [Fact]
    public async Task Rows_summarize_each_project_from_stored_state()
    {
        using var scenario = await PageScenario.CreateAsync();
        var nova = await scenario.AddProjectAsync("Nova Logistics", "Nova Freight", owner: "Sam");
        nova.Tasks.Add(PageScenario.Task(nova, "Collect licenses", Noon.AddDays(-1)));
        nova.Requirements[0].Status = RequirementStatus.Complete;
        await scenario.Projects.SaveAsync(nova);
        await scenario.AddProjectAsync("Plain", "", ProjectStatus.Completed);

        var projects = await ProjectsViewModel.LoadAsync(scenario.Services);
        var L = scenario.Strings;
        var row = projects.AllRows.Single(item => item.Name == "Nova Logistics");
        var plain = projects.AllRows.Single(item => item.Name == "Plain");

        Assert.True(row.HasCompany);
        Assert.Equal("Nova Freight", row.Company);
        Assert.Equal($"1/{nova.Requirements.Count}", row.ReadinessText);
        Assert.Equal(100.0 / nova.Requirements.Count, row.ReadinessPercent, 6);
        Assert.Equal(L.ShortDate(Noon.AddDays(-1)), row.NextText);
        Assert.True(row.IsNextOverdue);
        Assert.Equal("Sam", row.OwnerText);
        Assert.Equal(PillKind.Success, row.StatusKind);
        Assert.Equal(L.Enum(ProjectStatus.Active), row.StatusText);
        Assert.Equal($"Nova Logistics · Nova Freight · {nova.Stage.Title} · {L.Enum(ProjectStatus.Active)} · Sam", row.AccessibleName);

        Assert.False(plain.HasCompany);
        Assert.Equal("—", plain.OwnerText);
        Assert.Equal(L["date.notSet"], plain.NextText);
        Assert.False(plain.IsNextOverdue);
        Assert.Equal(PillKind.Accent, plain.StatusKind);
    }

    [Fact]
    public async Task The_filter_matches_name_company_and_owner_and_flags_the_first_visible_row()
    {
        using var scenario = await PageScenario.CreateAsync();
        await scenario.AddProjectAsync("Nova Logistics", "Nova Freight", owner: "Sam");
        await scenario.AddProjectAsync("Atlas", "Orbit Capital", owner: "Priya");
        await scenario.AddProjectAsync("Borealis", "Fjord", owner: "Sam Lee");
        var projects = await ProjectsViewModel.LoadAsync(scenario.Services);
        var changes = new List<string?>();
        projects.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Assert.Equal(3, projects.VisibleRows.Count);
        Assert.Equal([true, false, false], projects.VisibleRows.Select(row => row.IsFirst));

        projects.Filter = "sam";
        Assert.Equal(["Borealis", "Nova Logistics"], projects.VisibleRows.Select(row => row.Name).Order());
        Assert.True(projects.VisibleRows[0].IsFirst);
        Assert.False(projects.VisibleRows[1].IsFirst);
        Assert.Contains(nameof(ProjectsViewModel.VisibleRows), changes);

        projects.Filter = "ORBIT";
        Assert.Equal(["Atlas"], projects.VisibleRows.Select(row => row.Name));
        Assert.True(projects.VisibleRows.Single().IsFirst);

        projects.Filter = "   nothing like this ";
        Assert.Empty(projects.VisibleRows);
        Assert.True(projects.HasRows);

        projects.Filter = "";
        Assert.Equal(3, projects.VisibleRows.Count);
    }

    [Fact]
    public async Task With_no_projects_the_table_is_empty_and_new_project_opens_the_wizard()
    {
        using var scenario = await PageScenario.CreateAsync();
        var projects = await ProjectsViewModel.LoadAsync(scenario.Services);

        Assert.True(projects.IsEmpty);
        Assert.False(projects.HasRows);
        Assert.Empty(projects.VisibleRows);

        await projects.NewProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, scenario.Host.WizardsShown);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task Opening_a_row_navigates_to_the_project_under_the_shell_busy_state()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Nova Logistics");
        var row = (await ProjectsViewModel.LoadAsync(scenario.Services)).AllRows.Single();

        await row.OpenCommand.ExecuteAsync(null);

        Assert.Equal(new ProjectRoute(project.Id), scenario.Navigator.Current);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task Deleting_needs_a_confirmation_that_names_the_project_and_cancel_keeps_it()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Doomed project");
        var row = (await ProjectsViewModel.LoadAsync(scenario.Services)).AllRows.Single();

        row.DeleteCommand.Execute(null);

        var dialog = Assert.IsType<DeleteProjectDialogViewModel>(scenario.Dialogs.Shown);
        Assert.Equal(scenario.Strings["v3.delProjectT"], dialog.Heading);
        Assert.Contains("Doomed project", dialog.Body);
        Assert.NotNull(await scenario.Projects.GetAsync(project.Id));

        dialog.CancelCommand.Execute(null);

        Assert.False(scenario.Dialogs.IsOpen);
        Assert.NotNull(await scenario.Projects.GetAsync(project.Id));
        Assert.Empty(scenario.Navigator.Visited);
    }

    [Fact]
    public async Task Confirming_deletes_only_that_project_closes_the_dialog_and_returns_to_the_list()
    {
        using var scenario = await PageScenario.CreateAsync();
        var doomed = await scenario.AddProjectAsync("Doomed project");
        var kept = await scenario.AddProjectAsync("Kept project");
        var projects = await ProjectsViewModel.LoadAsync(scenario.Services);
        projects.AllRows.Single(row => row.Name == "Doomed project").DeleteCommand.Execute(null);
        var dialog = Assert.IsType<DeleteProjectDialogViewModel>(scenario.Dialogs.Shown);

        await dialog.ConfirmCommand.ExecuteAsync(null);

        Assert.Null(await scenario.Projects.GetAsync(doomed.Id));
        Assert.NotNull(await scenario.Projects.GetAsync(kept.Id));
        Assert.False(scenario.Dialogs.IsOpen);
        Assert.Equal(new PageRoute(AppPage.Projects), scenario.Navigator.Current);
    }

    [Fact]
    public async Task Row_and_dialog_text_follow_the_language_in_place()
    {
        using var scenario = await PageScenario.CreateAsync();
        var project = await scenario.AddProjectAsync("Nova Logistics");
        var row = (await ProjectsViewModel.LoadAsync(scenario.Services)).AllRows.Single();
        var dialog = new DeleteProjectDialogViewModel(project, scenario.Services);
        var english = (row.StatusText, dialog.Heading);
        var changes = new List<string?>();
        row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        scenario.Locale.SetLanguage("fa");

        Assert.Equal([string.Empty], changes);
        Assert.NotEqual(english.StatusText, row.StatusText);
        Assert.NotEqual(english.Heading, dialog.Heading);
        Assert.Equal("Nova Logistics", row.Name);
    }

    [Fact]
    public void View_models_are_shown_by_the_view_locator()
    {
        Assert.Equal(typeof(ProjectsView), ViewLocator.ViewTypeFor(typeof(ProjectsViewModel)));
        Assert.Equal(typeof(ProjectRowView), ViewLocator.ViewTypeFor(typeof(ProjectRowViewModel)));
        Assert.Equal(typeof(DeleteProjectDialogView), ViewLocator.ViewTypeFor(typeof(DeleteProjectDialogViewModel)));
    }
}
