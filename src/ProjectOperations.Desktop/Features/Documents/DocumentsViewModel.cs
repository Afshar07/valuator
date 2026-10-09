using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Documents;

/// <summary>A file reference with the project and requirement it is linked to, and whether the file still exists.</summary>
internal sealed record DocumentItem(Project Project, ProjectRequirement Requirement, ProjectFile File, bool Exists);

/// <summary>
/// File references linked to requirements across projects. Only paths and metadata are stored: files are never copied, read, previewed
/// or sent. The only filesystem access is an existence check and the user-initiated open and reveal.
/// </summary>
internal sealed partial class DocumentsViewModel : ViewModelBase
{
    private readonly PageServices _services;
    private readonly IReadOnlyList<DocumentRowViewModel> _all;

    [ObservableProperty] private IReadOnlyList<DocumentRowViewModel> _rows = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDetail))] private DocumentDetailViewModel? _detail;

    public DocumentsViewModel(IReadOnlyList<DocumentItem> documents, PageServices services)
    {
        _services = services; L = services.Strings;
        var items = documents.OrderByDescending(item => item.File.AddedAt).ToList();
        _all = items.Select(item => new DocumentRowViewModel(item, services, Select)).ToList();
        var projects = items.Select(item => item.Project).DistinctBy(project => project.Id).ToList();
        var chips = new List<(Guid Value, Func<string> Label)> { (Guid.Empty, () => L["documents.allProjects"]) };
        chips.AddRange(projects.Select(project => (project.Id, (Func<string>)(() => project.Name))));
        Filter = new ChipGroupViewModel<Guid>(L, chips, Guid.Empty);
        Filter.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Filter.Selected)) Apply(); };
        HasDocuments = items.Count > 0;
        Apply();
        RefreshOnLanguageChange(L);
    }

    /// <summary>Reads every project's file references and checks, off the UI thread, which files still exist.</summary>
    public static async Task<DocumentsViewModel> LoadAsync(PageServices services)
    {
        var projects = await services.Projects.ListAsync();
        var references = projects.SelectMany(project => project.Requirements.SelectMany(requirement => requirement.Files.Select(file => (project, requirement, file)))).ToList();
        var exists = await Task.Run(() => references.Select(item => File.Exists(item.file.Path)).ToList());
        return new(references.Select((item, index) => new DocumentItem(item.project, item.requirement, item.file, exists[index])).ToList(), services);
    }

    public LocalizedStrings L { get; }
    public bool HasDocuments { get; }
    public bool IsEmpty => !HasDocuments;
    public bool HasDetail => Detail is not null;

    /// <summary>"All projects" and one chip per project that has files.</summary>
    public ChipGroupViewModel<Guid> Filter { get; }

    private Guid? _selected;

    private void Apply()
    {
        var visible = _all.Where(row => Filter.Selected == Guid.Empty || row.ProjectId == Filter.Selected).ToList();
        if (visible.All(row => row.FileId != _selected)) _selected = visible.FirstOrDefault()?.FileId;
        for (var index = 0; index < visible.Count; index++) { visible[index].IsFirst = index == 0; visible[index].IsSelected = visible[index].FileId == _selected; }
        Rows = visible;
        Detail = visible.FirstOrDefault(row => row.FileId == _selected) is { } row ? new DocumentDetailViewModel(row.Item, _services) : null;
    }

    private void Select(Guid fileId)
    {
        _selected = fileId;
        Apply();
    }
}

/// <summary>One file in the list. The row selects it; the detail pane shows the selected one.</summary>
internal sealed partial class DocumentRowViewModel : ViewModelBase
{
    private readonly PageServices _services;
    private readonly Action<Guid> _select;

    [ObservableProperty] private bool _isFirst;
    [ObservableProperty] private bool _isSelected;

    public DocumentRowViewModel(DocumentItem item, PageServices services, Action<Guid> select)
    {
        Item = item; _services = services; _select = select; L = services.Strings;
        RefreshOnLanguageChange(L);
    }

    public DocumentItem Item { get; }
    public LocalizedStrings L { get; }
    public Guid FileId => Item.File.Id;
    public Guid ProjectId => Item.Project.Id;

    public string Icon => Icons.ForFile(Item.File.Path);
    public string FileName => Item.File.FileName;
    public string Where => $"{Item.Project.Name} · {L.Requirement(Item.Requirement)}";
    public string AccessibleName => $"{Item.File.FileName} · {Where}";
    public string StatusText => L[Item.Exists ? "documents.referenceOnly" : "documents.missingFile"];
    public PillKind StatusKind => Item.Exists ? PillKind.Neutral : PillKind.Error;

    [RelayCommand]
    private void Select() => _select(Item.File.Id);
}

/// <summary>The selected file: where it lives, what it is linked to, and the explicit actions to open it, reveal it or unlink it.</summary>
internal sealed partial class DocumentDetailViewModel : ViewModelBase
{
    private readonly DocumentItem _item;
    private readonly PageServices _services;

    public DocumentDetailViewModel(DocumentItem item, PageServices services)
    {
        _item = item; _services = services; L = services.Strings;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string Icon => Icons.ForFile(_item.File.Path);
    public string FileName => _item.File.FileName;
    public string Path => _item.File.Path;
    public string LinkedTo => $"{_item.Project.Name} · {L.Requirement(_item.Requirement)}";
    public string AddedText => $"{L.Due(_item.File.AddedAt)} · {Size(_item.File.SizeBytes)}";
    /// <summary>Open and reveal need the file to exist; unlinking only removes the reference.</summary>
    public bool CanOpen => _item.Exists;

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task OpenAsync()
    {
        if (!await _services.Files.OpenFileAsync(_item.File.Path)) _services.Host.ShowError("documents.openFailed");
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private async Task RevealAsync()
    {
        var folder = System.IO.Path.GetDirectoryName(_item.File.Path);
        if (folder is null || !await _services.Files.OpenFolderAsync(folder)) _services.Host.ShowError("documents.openFailed");
    }

    /// <summary>Removes the reference from its requirement. The file on disk is never touched.</summary>
    [RelayCommand]
    private Task UnlinkAsync() => _services.Host.RunAsync(async () =>
    {
        var project = await _services.Projects.GetAsync(_item.Project.Id);
        var requirement = project?.Requirements.FirstOrDefault(item => item.Id == _item.Requirement.Id);
        var file = requirement?.Files.FirstOrDefault(item => item.Id == _item.File.Id);
        if (project is null || requirement is null || file is null) { _services.Host.ShowError("validation.projectUnavailable"); return; }
        requirement.Files.Remove(file);
        await _services.Projects.SaveAsync(project);
        await _services.Navigator.GoToAsync(new PageRoute(AppPage.Documents));
    });

    /// <summary>Bytes as B, KB, MB or GB, wrapped in directional isolates so right-to-left text keeps the digits in order.</summary>
    internal static string Size(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes; var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return string.Create(CultureInfo.InvariantCulture, $"⁦{value:0.#} {units[unit]}⁩");
    }
}
