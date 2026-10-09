using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Settings;

/// <summary>
/// Where the stages of each template are added, renamed, recoloured, reordered and deleted. Every change saves the whole ordered list and
/// reloads it, so the rows always show what is stored. A stage a project uses, and a template's last stage, cannot be deleted.
/// </summary>
internal sealed partial class StageEditorViewModel : ViewModelBase
{
    /// <summary>The colours offered for a stage.</summary>
    public static readonly IReadOnlyList<string> Palette = ["#64748B", "#2563EB", "#0D9488", "#16A34A", "#CA8A04", "#D97706", "#DC2626", "#DB2777", "#7C3AED"];

    private readonly SettingsServices _services;
    private string _template = VcTemplate.Create().Id;
    private List<ProjectStage> _stages = [];
    private IReadOnlyDictionary<Guid, int> _usage = new Dictionary<Guid, int>();

    [ObservableProperty] private IReadOnlyList<StageRowViewModel> _rows = [];

    public StageEditorViewModel(SettingsServices services)
    {
        _services = services; L = services.Strings;
        var templates = new (string Id, Func<string> Label)[]
        {
            (_template, () => L.Template(VcTemplate.Create())),
            (ProjectService.BlankTemplateId, () => L["template.blank"])
        };
        Templates = new ChipGroupViewModel<string>(L, templates, _template);
        Templates.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(Templates.Selected)) return;
            _template = Templates.Selected;
            Reloading = ReloadAsync();
        };
        RefreshOnLanguageChange(L);
    }

    /// <summary>The editor for the Vc template with its stages read. A failure is reported in the banner and leaves the list empty.</summary>
    public static async Task<StageEditorViewModel> LoadAsync(SettingsServices services)
    {
        var editor = new StageEditorViewModel(services);
        await editor.ReloadAsync();
        return editor;
    }

    public LocalizedStrings L { get; }

    /// <summary>The template whose stages are shown.</summary>
    public ChipGroupViewModel<string> Templates { get; }

    /// <summary>The reload that started last, for tests; completes immediately when none is running.</summary>
    public Task Reloading { get; private set; } = Task.CompletedTask;

    private async Task ReloadAsync()
    {
        try
        {
            _stages = (await _services.Projects.ListStagesAsync(_template)).ToList();
            _usage = await _services.Projects.CountProjectsByStageAsync();
            Rebuild();
        }
        catch (Exception) { _services.Host.ShowError("stages.saveFailed"); }
    }

    private void Rebuild() =>
        Rows = _stages.Select((stage, index) => new StageRowViewModel(stage, index, _stages.Count, _usage.GetValueOrDefault(stage.Id), this, L)).ToList();

    [RelayCommand]
    private Task AddAsync() => _services.Host.RunAsync(() => CommitAsync(stages =>
        stages.Add(new ProjectStage { Title = L["stages.newName"], Color = Palette[stages.Count % Palette.Count] })));

    /// <summary>Applies a change to a copy of the list, saves the whole ordered list and reloads it.</summary>
    private async Task CommitAsync(Action<List<ProjectStage>> change)
    {
        var next = _stages.Select(stage => new ProjectStage { Id = stage.Id, Title = stage.Title, Color = stage.Color }).ToList();
        change(next);
        await _services.Projects.SaveStagesAsync(_template, next);
        await ReloadAsync();
    }

    /// <summary>A failed save is reported on the spot and the rows are reloaded, so they never show what was not stored.</summary>
    private async Task GuardedAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception)
        {
            _services.Host.ShowError("stages.saveFailed");
            await ReloadAsync();
        }
    }

    internal Task RecolorAsync(int position, string color) => GuardedAsync(() => CommitAsync(stages => stages[position].Color = color));

    internal Task RenameAsync(int position, string title) => GuardedAsync(() => CommitAsync(stages => stages[position].Title = title));

    internal Task MoveAsync(int from, int to) => _services.Host.RunAsync(() => CommitAsync(stages => (stages[from], stages[to]) = (stages[to], stages[from])));

    internal Task DeleteAsync(Guid stageId) => _services.Host.RunAsync(async () =>
    {
        await _services.Projects.DeleteStageAsync(_template, stageId);
        await ReloadAsync();
    });
}

/// <summary>One stage: its colour, its title (edited in place and saved when the box is left or Enter is pressed), and move and delete.</summary>
internal sealed partial class StageRowViewModel : ViewModelBase
{
    private readonly ProjectStage _stage;
    private readonly int _position;
    private readonly int _count;
    private readonly int _inUse;
    private readonly StageEditorViewModel _editor;
    private bool _committed;

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isColorOpen;

    public StageRowViewModel(ProjectStage stage, int position, int count, int inUse, StageEditorViewModel editor, LocalizedStrings strings)
    {
        _stage = stage; _position = position; _count = count; _inUse = inUse; _editor = editor; L = strings;
        _title = stage.Title;
        Colors = StageEditorViewModel.Palette.Select(color => new StageColorViewModel(color, PickColorAsync)).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public Guid Id => _stage.Id;
    public string Color => _stage.Color;
    public IReadOnlyList<StageColorViewModel> Colors { get; }

    public bool IsInUse => _inUse > 0;
    public string InUseText => _inUse == 1 ? L["stages.inUseOne"] : L.Format("stages.inUse", L.Number(_inUse));

    public bool CanMoveUp => _position > 0;
    public bool CanMoveDown => _position < _count - 1;
    public bool CanDelete => _inUse == 0 && _count > 1;

    /// <summary>Why Delete is unavailable, or its plain name when it is available.</summary>
    public string DeleteTip => _inUse > 0 ? L["stages.inUseWhy"] : _count == 1 ? L["stages.lastOne"] : L["stages.delete"];

    [RelayCommand]
    private void ToggleColor() => IsColorOpen = !IsColorOpen;

    private Task PickColorAsync(string color)
    {
        IsColorOpen = false;
        return _editor.RecolorAsync(_position, color);
    }

    /// <summary>Saves the edited title. An empty or unchanged title is put back instead; a saved one is committed once, because the reload replaces this row.</summary>
    [RelayCommand]
    private async Task CommitTitleAsync()
    {
        if (_committed) return;
        var text = Title.Trim();
        if (text.Length == 0 || text == _stage.Title) { Title = _stage.Title; return; }
        _committed = true;
        await _editor.RenameAsync(_position, text);
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private Task MoveUpAsync() => _editor.MoveAsync(_position, _position - 1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private Task MoveDownAsync() => _editor.MoveAsync(_position, _position + 1);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private Task DeleteAsync() => _editor.DeleteAsync(_stage.Id);
}

/// <summary>One colour in a stage's colour picker.</summary>
internal sealed partial class StageColorViewModel(string color, Func<string, Task> pick) : ViewModelBase
{
    public string Color => color;

    [RelayCommand]
    private Task PickAsync() => pick(color);
}
