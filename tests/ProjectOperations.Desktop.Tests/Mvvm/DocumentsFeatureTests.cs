using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Features.Documents;
using ProjectOperations.Desktop.Shell;
using Xunit;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>The Documents page as plain view-models: file references only, no window, no controls, and a launcher fake that records what would be opened.</summary>
public sealed class DocumentsFeatureTests
{
    private static readonly DateTimeOffset Noon = PageScenario.Noon;

    private static ProjectFile File(string path, int daysAgo, long size = 2048) =>
        new() { Path = path, FileName = System.IO.Path.GetFileName(path), SizeBytes = size, AddedAt = Noon.AddDays(-daysAgo) };

    private static async Task<(Project Nova, Project Atlas, ProjectFile Present, ProjectFile Gone, ProjectFile Other)> SeedAsync(PageScenario scenario)
    {
        var nova = await scenario.AddProjectAsync("Nova Logistics");
        var atlas = await scenario.AddProjectAsync("Atlas");
        var present = File(scenario.TempFile("deck.pdf"), daysAgo: 3);
        var gone = File(scenario.MissingFile("moved-away.pdf"), daysAgo: 1);
        var other = File(scenario.TempFile("notes.txt"), daysAgo: 5);
        nova.Requirements[0].Files.Add(present);
        nova.Requirements[1].Files.Add(gone);
        atlas.Requirements[0].Files.Add(other);
        await scenario.Projects.SaveAsync(nova);
        await scenario.Projects.SaveAsync(atlas);
        return (nova, atlas, present, gone, other);
    }

    [Fact]
    public async Task Files_are_listed_newest_first_with_the_first_selected_and_missing_ones_flagged()
    {
        using var scenario = await PageScenario.CreateAsync();
        var (_, _, present, gone, other) = await SeedAsync(scenario);
        var L = scenario.Strings;

        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);

        Assert.True(documents.HasDocuments);
        Assert.False(documents.IsEmpty);
        Assert.Equal([gone.FileName, present.FileName, other.FileName], documents.Rows.Select(row => row.FileName));
        Assert.Equal([true, false, false], documents.Rows.Select(row => row.IsSelected));
        Assert.Equal([true, false, false], documents.Rows.Select(row => row.IsFirst));
        Assert.Equal([PillKind.Error, PillKind.Neutral, PillKind.Neutral], documents.Rows.Select(row => row.StatusKind));
        Assert.Equal([L["documents.missingFile"], L["documents.referenceOnly"], L["documents.referenceOnly"]], documents.Rows.Select(row => row.StatusText));
        var row = documents.Rows[1];
        Assert.StartsWith("Nova Logistics · ", row.Where);
        Assert.Equal($"deck.pdf · {row.Where}", row.AccessibleName);
        Assert.True(documents.HasDetail);
        Assert.Equal(gone.FileName, documents.Detail!.FileName);
    }

    [Fact]
    public async Task Selecting_a_row_shows_its_detail_and_a_filter_keeps_only_that_projects_files()
    {
        using var scenario = await PageScenario.CreateAsync();
        var (nova, atlas, present, gone, other) = await SeedAsync(scenario);
        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);

        Assert.Equal(["All projects", "Nova Logistics", "Atlas"], documents.Filter.Options.Select(option => option.Label));
        Assert.True(documents.Filter.Options[0].IsSelected);

        documents.Rows[2].SelectCommand.Execute(null);
        Assert.Equal(other.FileName, documents.Detail!.FileName);
        Assert.Equal([false, false, true], documents.Rows.Select(row => row.IsSelected));

        documents.Filter.Options[1].SelectCommand.Execute(null);
        Assert.Equal([gone.FileName, present.FileName], documents.Rows.Select(row => row.FileName));
        Assert.Equal(gone.FileName, documents.Detail!.FileName); // the selection was hidden, so the first visible takes over
        Assert.Equal([true, false], documents.Rows.Select(row => row.IsFirst));

        documents.Filter.Options[2].SelectCommand.Execute(null);
        Assert.Equal([other.FileName], documents.Rows.Select(row => row.FileName));
        Assert.Equal($"{atlas.Name} · {scenario.Strings.Requirement(atlas.Requirements[0])}", documents.Detail!.LinkedTo);

        documents.Filter.Options[0].SelectCommand.Execute(null);
        Assert.Equal(3, documents.Rows.Count);
        Assert.Equal(other.FileName, documents.Detail!.FileName); // still visible, so it stays selected
        Assert.NotEqual(nova.Id, atlas.Id);
    }

    [Fact]
    public async Task Detail_describes_the_file_and_only_offers_open_and_reveal_when_it_exists()
    {
        using var scenario = await PageScenario.CreateAsync();
        var (_, _, present, gone, _) = await SeedAsync(scenario);
        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);

        var missing = documents.Detail!;
        Assert.Equal(gone.Path, missing.Path);
        Assert.False(missing.CanOpen);
        Assert.False(missing.OpenCommand.CanExecute(null));
        Assert.False(missing.RevealCommand.CanExecute(null));
        Assert.True(missing.UnlinkCommand.CanExecute(null));

        documents.Rows[1].SelectCommand.Execute(null);
        var detail = documents.Detail!;
        Assert.Equal(present.Path, detail.Path);
        Assert.Equal($"{scenario.Strings.Due(present.AddedAt)} · ⁦2 KB⁩", detail.AddedText);
        Assert.True(detail.CanOpen);
        Assert.True(detail.OpenCommand.CanExecute(null));
    }

    [Fact]
    public async Task Open_and_reveal_go_through_the_launcher_and_report_a_refusal()
    {
        using var scenario = await PageScenario.CreateAsync();
        var (_, _, present, _, _) = await SeedAsync(scenario);
        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);
        documents.Rows[1].SelectCommand.Execute(null);
        var detail = documents.Detail!;

        await detail.OpenCommand.ExecuteAsync(null);
        await detail.RevealCommand.ExecuteAsync(null);

        Assert.Equal([present.Path], scenario.Files.Opened);
        Assert.Equal([Path.GetDirectoryName(present.Path)!], scenario.Files.Revealed);
        Assert.Empty(scenario.Host.Errors);

        scenario.Files.Succeeds = false;
        await detail.OpenCommand.ExecuteAsync(null);
        await detail.RevealCommand.ExecuteAsync(null);

        Assert.Equal(["documents.openFailed", "documents.openFailed"], scenario.Host.Errors);
    }

    [Fact]
    public async Task Unlinking_removes_only_the_reference_and_reloads_the_page()
    {
        using var scenario = await PageScenario.CreateAsync();
        var (nova, _, present, gone, _) = await SeedAsync(scenario);
        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);
        documents.Rows[1].SelectCommand.Execute(null);

        await documents.Detail!.UnlinkCommand.ExecuteAsync(null);

        var reloaded = await scenario.ReloadAsync(nova);
        Assert.Equal([gone.FileName], reloaded.Requirements.SelectMany(requirement => requirement.Files).Select(file => file.FileName));
        Assert.True(System.IO.File.Exists(present.Path)); // the file on disk is never touched
        Assert.Equal(new PageRoute(AppPage.Documents), scenario.Navigator.Current);
        Assert.Equal(1, scenario.Host.Runs);
    }

    [Fact]
    public async Task Unlinking_a_file_that_is_already_gone_reports_the_project_as_unavailable()
    {
        using var scenario = await PageScenario.CreateAsync();
        var (nova, _, _, _, _) = await SeedAsync(scenario);
        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);
        await scenario.Projects.DeleteAsync(nova.Id);

        await documents.Detail!.UnlinkCommand.ExecuteAsync(null);

        Assert.Equal(["validation.projectUnavailable"], scenario.Host.Errors);
        Assert.Empty(scenario.Navigator.Visited);
    }

    [Fact]
    public async Task With_no_files_linked_the_page_says_so_and_has_nothing_to_filter_or_show()
    {
        using var scenario = await PageScenario.CreateAsync();
        await scenario.AddProjectAsync("Empty");

        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);

        Assert.True(documents.IsEmpty);
        Assert.False(documents.HasDocuments);
        Assert.False(documents.HasDetail);
        Assert.Empty(documents.Rows);
        Assert.Single(documents.Filter.Options);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5L * 1024 * 1024, "5 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 * 1024, "3072 GB")]
    public void Sizes_are_shown_in_the_largest_whole_unit_inside_directional_isolates(long bytes, string expected) =>
        Assert.Equal($"⁦{expected}⁩", DocumentDetailViewModel.Size(bytes));

    [Fact]
    public async Task Text_follows_the_language_in_place()
    {
        using var scenario = await PageScenario.CreateAsync();
        await SeedAsync(scenario);
        var documents = await DocumentsViewModel.LoadAsync(scenario.Services);
        var row = documents.Rows[0];
        var english = row.StatusText;
        var changes = new List<string?>();
        row.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        scenario.Locale.SetLanguage("fa");

        Assert.Equal([string.Empty], changes);
        Assert.NotEqual(english, row.StatusText);
        Assert.Equal("moved-away.pdf", row.FileName);
    }

    [Fact]
    public void View_models_are_shown_by_the_view_locator()
    {
        Assert.Equal(typeof(DocumentsView), ViewLocator.ViewTypeFor(typeof(DocumentsViewModel)));
        Assert.Equal(typeof(DocumentRowView), ViewLocator.ViewTypeFor(typeof(DocumentRowViewModel)));
        Assert.Equal(typeof(DocumentDetailView), ViewLocator.ViewTypeFor(typeof(DocumentDetailViewModel)));
    }
}
