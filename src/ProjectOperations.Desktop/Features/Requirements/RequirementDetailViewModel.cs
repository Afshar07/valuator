using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Requirements;

/// <summary>
/// The editor under an open requirement: link files (documents) or enter a value (everything else), set the review status, and add a
/// follow-up task. Every change is saved and the project reloaded, as the rest of the project screens do.
/// </summary>
internal sealed partial class RequirementDetailViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectRequirement _requirement;
    private readonly ProjectScreenServices _services;

    [ObservableProperty] private string _draft;
    [ObservableProperty] private FollowUpFormViewModel? _form;

    public RequirementDetailViewModel(Project project, ProjectRequirement requirement, ProjectScreenServices services)
    {
        _project = project; _requirement = requirement; _services = services; L = services.Strings;
        _draft = requirement.Value;
        Files = requirement.Files.Select(file => new LinkedFileViewModel(this, file)).ToList();
        Statuses = new[] { RequirementStatus.Missing, RequirementStatus.Provided, RequirementStatus.NeedsReview, RequirementStatus.Complete }
            .Select(status => new RequirementStatusChipViewModel(L, status, status == requirement.Status, () => SetStatusAsync(status))).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public bool IsDocument => _requirement.Type == RequirementType.Document;
    public bool IsValue => !IsDocument;
    public string TypeLabel => L.Enum(_requirement.Type) + ".";
    public string Hint => L["v3.hint_" + _requirement.Type.ToString().ToLowerInvariant()];

    public IReadOnlyList<LinkedFileViewModel> Files { get; }
    public IReadOnlyList<RequirementStatusChipViewModel> Statuses { get; }
    public bool HasFiles => Files.Count > 0;
    public bool HasForm => Form is not null;
    public bool HasNoForm => Form is null;

    partial void OnFormChanged(FollowUpFormViewModel? value)
    {
        OnPropertyChanged(nameof(HasForm));
        OnPropertyChanged(nameof(HasNoForm));
    }

    private DateTimeOffset Now => _services.Clock.GetUtcNow();

    private async Task SaveAndReloadAsync()
    {
        await _services.Projects.SaveAsync(_project);
        await _services.Host.RefreshProjectAsync();
    }

    [RelayCommand]
    private Task SaveValueAsync()
    {
        var value = (Draft ?? "").Trim();
        if (value.Length == 0) return Task.CompletedTask;
        return _services.Host.RunAsync(() =>
        {
            _requirement.Value = value;
            if (_requirement.Status == RequirementStatus.Missing) _requirement.Status = RequirementStatus.Provided;
            _requirement.LastReviewedAt = Now;
            return SaveAndReloadAsync();
        });
    }

    [RelayCommand]
    private Task PickFilesAsync() => _services.Host.RunAsync(async () =>
    {
        var picked = await _services.Files.PickAsync(L["file.pickerTitle"]);
        var added = false;
        foreach (var file in picked)
        {
            if (file.LocalPath is not { } path) { _services.Host.ShowError("validation.localFilesOnly"); continue; }
            if (_requirement.Files.Any(existing => existing.Path == path)) continue;
            _requirement.Files.Add(new ProjectFile { FileName = file.Name, Path = path, SizeBytes = new FileInfo(path).Length });
            added = true;
        }
        if (!added) return;
        if (_requirement.Status == RequirementStatus.Missing) _requirement.Status = RequirementStatus.Provided;
        _requirement.LastReviewedAt = Now;
        await SaveAndReloadAsync();
    });

    internal Task UnlinkAsync(ProjectFile file) => _services.Host.RunAsync(() =>
    {
        _requirement.Files.Remove(file);
        return SaveAndReloadAsync();
    });

    private Task SetStatusAsync(RequirementStatus status) => _services.Host.RunAsync(() =>
    {
        _requirement.Status = status;
        _requirement.LastReviewedAt = Now;
        return SaveAndReloadAsync();
    });

    internal void StartFollowUp() => Form = new FollowUpFormViewModel(_project, _requirement, _services, SaveAndReloadAsync, () => Form = null);

    [RelayCommand]
    private void AddTask() => StartFollowUp();
}

/// <summary>A file linked to a requirement, with the button that unlinks it. The file itself is never touched.</summary>
internal sealed partial class LinkedFileViewModel : ViewModelBase
{
    private readonly RequirementDetailViewModel _detail;
    private readonly ProjectFile _file;

    public LinkedFileViewModel(RequirementDetailViewModel detail, ProjectFile file)
    {
        _detail = detail; _file = file; Icon = Icons.ForFile(file.Path);
        RefreshOnLanguageChange(detail.L);
    }

    public LocalizedStrings L => _detail.L;

    public string Icon { get; }
    public string FileName => _file.FileName;

    [RelayCommand]
    private Task UnlinkAsync() => _detail.UnlinkAsync(_file);
}

/// <summary>One review status the user can set. Setting it saves at once.</summary>
internal sealed partial class RequirementStatusChipViewModel : ViewModelBase
{
    private readonly LocalizedStrings _strings;
    private readonly Func<Task> _select;

    public RequirementStatusChipViewModel(LocalizedStrings strings, RequirementStatus status, bool isSelected, Func<Task> select)
    {
        _strings = strings; Status = status; IsSelected = isSelected; _select = select;
        RefreshOnLanguageChange(strings);
    }

    public RequirementStatus Status { get; }
    public bool IsSelected { get; }
    public string Label => _strings.Enum(Status);
    public bool IsMissing => Status == RequirementStatus.Missing;
    public bool IsProvided => Status == RequirementStatus.Provided;
    public bool IsNeedsReview => Status == RequirementStatus.NeedsReview;
    public bool IsComplete => Status == RequirementStatus.Complete;

    [RelayCommand]
    private Task SelectAsync() => _select();
}

/// <summary>The small form that adds a follow-up task for a requirement: a title and a due date a day, a week or two weeks ahead.</summary>
internal sealed partial class FollowUpFormViewModel : ViewModelBase
{
    private const int NoDate = -1;
    private readonly Project _project;
    private readonly ProjectRequirement _requirement;
    private readonly ProjectScreenServices _services;
    private readonly Func<Task> _saveAndReload;
    private readonly Action _close;

    [ObservableProperty] private string _title;

    public FollowUpFormViewModel(Project project, ProjectRequirement requirement, ProjectScreenServices services, Func<Task> saveAndReload, Action close)
    {
        _project = project; _requirement = requirement; _services = services; _saveAndReload = saveAndReload; _close = close; L = services.Strings;
        _title = $"{L["v3.followUp"]} {L.Requirement(requirement)}";
        Due = new ChipGroupViewModel<int>(L, new (int, Func<string>)[] { (1, () => L["v3.dTomorrow"]), (7, () => L["v3.dWeek"]), (14, () => L["v3.d2Week"]), (NoDate, () => L["v3.dNone"]) }, 1);
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public ChipGroupViewModel<int> Due { get; }

    [RelayCommand]
    private void Cancel() => _close();

    [RelayCommand]
    private Task CreateAsync()
    {
        var title = Title.Trim();
        if (title.Length == 0) return Task.CompletedTask;
        return _services.Host.RunAsync(async () =>
        {
            DateTimeOffset? dueAt = null;
            if (Due.Selected != NoDate)
            {
                var day = _services.Clock.GetLocalNow().Date.AddDays(Due.Selected).AddHours(23).AddMinutes(59);
                dueAt = new DateTimeOffset(day, _services.Clock.LocalTimeZone.GetUtcOffset(day));
            }
            _project.Tasks.Add(new ProjectTask { ProjectId = _project.Id, Title = title, DueAt = dueAt, RequirementId = _requirement.Id });
            _close();
            _services.Host.ShowToast("v3.toastTask");
            await _saveAndReload();
        });
    }
}
