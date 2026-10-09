using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop;

/// <summary>
/// File references linked to requirements across projects. Only paths and metadata are stored: files are never copied,
/// read, previewed or sent. The only filesystem access here is an existence check and user-initiated open/reveal.
/// </summary>
internal sealed class DocumentsView : PresentationView
{
    private sealed record Document(Project Project, ProjectRequirement Requirement, ProjectFile File, bool Exists);
    private List<Document> _documents = [];
    private Guid? _filter;
    private Guid? _selected;
    private readonly WrapPanel _filters = new() { ItemSpacing = 6, LineSpacing = 6 };
    private readonly ContentControl _body = new();

    public DocumentsView(PresentationContext context) : base(context) { }

    public async Task LoadAsync()
    {
        var projects = await _projects.ListAsync();
        var references = projects.SelectMany(project => project.Requirements.SelectMany(requirement => requirement.Files.Select(file => (project, requirement, file)))).ToList();
        var exists = await Task.Run(() => references.Select(item => File.Exists(item.file.Path)).ToList());
        _documents = references.Select((item, index) => new Document(item.project, item.requirement, item.file, exists[index]))
            .OrderByDescending(item => item.File.AddedAt).ToList();
        _selected = _documents.FirstOrDefault()?.File.Id;
        Children.Add(PageHeader("documents.title", "documents.subtitle"));
        Children.Add(_filters);
        Children.Add(_body);
        Render();
    }

    private void Render()
    {
        _filters.Children.Clear();
        var withFiles = _documents.Select(item => item.Project).DistinctBy(project => project.Id).ToList();
        _filters.Children.Add(Chip(() => T("documents.allProjects"), null));
        foreach (var project in withFiles) _filters.Children.Add(Chip(() => project.Name, project.Id));
        _filters.IsVisible = _documents.Count > 0;

        if (_documents.Count == 0)
        {
            _body.Content = Ui.Card(new StackPanel { Spacing = 6, Children = { Label("documents.empty", "BodyMedium"), Label("documents.emptyHint", "Small", "TextSecondary") } });
            return;
        }
        var visible = _documents.Where(item => _filter is null || item.Project.Id == _filter).ToList();
        if (visible.All(item => item.File.Id != _selected)) _selected = visible.FirstOrDefault()?.File.Id;
        var columns = new AdaptiveGrid { MinItemWidth = 340 };
        var list = new ListCard(Context) { Name = "DocumentList" };
        foreach (var document in visible) list.Add(Row(document));
        columns.Children.Add(list);
        var selected = visible.FirstOrDefault(item => item.File.Id == _selected);
        if (selected is not null) columns.Children.Add(Detail(selected));
        _body.Content = columns;
    }

    private Button Chip(Func<string> label, Guid? project)
    {
        var chip = Action(label, () => { _filter = project; Render(); return Task.CompletedTask; }, "chip");
        chip.Classes.Set("selected", _filter == project);
        return chip;
    }

    private Control Row(Document document)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        grid.Children.Add(Ui.Tile(Icons.Glyph(Icons.ForFile(document.File.Path), 17), 32));
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = Label(() => document.File.FileName, "BodyMedium"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; name.FlowDirection = FlowDirection.LeftToRight;
        name.HorizontalAlignment = HorizontalAlignment.Left; labels.Children.Add(name);
        var where = Label(() => $"{document.Project.Name} · {RequirementTitle(document.Requirement)}", "Caption", "TextSecondary"); where.TextWrapping = TextWrapping.NoWrap; where.TextTrimming = TextTrimming.CharacterEllipsis;
        labels.Children.Add(where);
        Grid.SetColumn(labels, 1); grid.Children.Add(labels);
        var pill = document.Exists ? Ui.Pill(Context, () => T("documents.referenceOnly"), Tone.Neutral) : Ui.Pill(Context, () => T("documents.missingFile"), Tone.Error);
        Grid.SetColumn(pill, 2); grid.Children.Add(pill);
        var selected = document.File.Id == _selected;
        var marker = new Border { BorderThickness = new Thickness(3, 0, 0, 0), Child = grid, Margin = new Thickness(-16, 0, 0, 0), Padding = new Thickness(13, 0, 0, 0) }
            .Paint(Border.BorderBrushProperty, selected ? "Accent" : "BackgroundCard");
        if (!selected) marker.BorderBrush = Brushes.Transparent;
        var row = new ListRow(Context, marker, () => $"{document.File.FileName} · {document.Project.Name} · {RequirementTitle(document.Requirement)}", () => { _selected = document.File.Id; Render(); return Task.CompletedTask; })
        { Name = "DocumentRow", Padding = new Thickness(16, 10, 14, 10) };
        row.Classes.Set("selected", selected);
        return row;
    }

    private Control Detail(Document document)
    {
        var panel = new StackPanel { Spacing = 14 };
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        head.Children.Add(Ui.Tile(Icons.Glyph(Icons.ForFile(document.File.Path), 21), 40, 10));
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var name = Label(() => document.File.FileName, "BodyStrong"); name.FontSize = 14; name.FlowDirection = FlowDirection.LeftToRight; name.HorizontalAlignment = HorizontalAlignment.Left; names.Children.Add(name);
        var path = Label(() => document.File.Path, "Caption", "TextTertiary"); path.FlowDirection = FlowDirection.LeftToRight; path.HorizontalAlignment = HorizontalAlignment.Left; path.Name = "DocumentPath"; names.Children.Add(path);
        Grid.SetColumn(names, 1); head.Children.Add(names);
        panel.Children.Add(head);

        var facts = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 16, RowSpacing = 6 };
        facts.Children.Add(Label("documents.linkedTo", "Small", "TextSecondary"));
        var linked = Label(() => $"{document.Project.Name} · {RequirementTitle(document.Requirement)}", "Small"); Grid.SetColumn(linked, 1); facts.Children.Add(linked);
        var added = Label("documents.added", "Small", "TextSecondary"); Grid.SetRow(added, 1); facts.Children.Add(added);
        var when = Label(() => $"{Due(document.File.AddedAt)} · {Size(document.File.SizeBytes)}", "Small"); Grid.SetRow(when, 1); Grid.SetColumn(when, 1); facts.Children.Add(when);
        panel.Children.Add(facts);

        var actions = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        var open = Context.IconAction("documents.open", Icons.ArrowSquareOut, async () =>
        {
            if (!await TopLevel.GetTopLevel(this)!.Launcher.LaunchFileInfoAsync(new FileInfo(document.File.Path))) ShowError("documents.openFailed");
        });
        var reveal = Context.IconAction("documents.reveal", Icons.FolderOpen, async () =>
        {
            var folder = Path.GetDirectoryName(document.File.Path);
            if (folder is null || !await TopLevel.GetTopLevel(this)!.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder))) ShowError("documents.openFailed");
        });
        open.IsEnabled = reveal.IsEnabled = document.Exists;
        var unlink = Context.IconAction("documents.unlink", Icons.LinkBreak, async () =>
        {
            var project = await _projects.GetAsync(document.Project.Id);
            var requirement = project?.Requirements.FirstOrDefault(item => item.Id == document.Requirement.Id);
            var file = requirement?.Files.FirstOrDefault(item => item.Id == document.File.Id);
            if (project is null || requirement is null || file is null) { ShowError("validation.projectUnavailable"); return; }
            requirement.Files.Remove(file);
            await SaveAsync(project);
            await Context.Shell.NavigateAsync(AppPage.Documents);
        }, "ghost");
        actions.Children.Add(open); actions.Children.Add(reveal); actions.Children.Add(unlink);
        panel.Children.Add(actions);

        var unavailable = new StackPanel { Spacing = 6 };
        unavailable.Children.Add(Icons.Glyph(Icons.FileDashed, 26, "TextTertiary"));
        ((TextBlock)unavailable.Children[0]).HorizontalAlignment = HorizontalAlignment.Left;
        unavailable.Children.Add(Label("documents.previewUnavailable", "BodyStrong"));
        unavailable.Children.Add(Label("documents.previewUnavailableBody", "Small", "TextSecondary"));
        panel.Children.Add(new DashedFrame(unavailable, "BorderDefault", "BackgroundMuted", PresentationTheme.RadiusMedium, new Thickness(18, 28)));
        return Ui.Card(panel, new Thickness(16));
    }

    private string Size(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes; var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"⁦{value:0.#} {units[unit]}⁩");
    }
}
