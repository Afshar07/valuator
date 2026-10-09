using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Requirements;

/// <summary>
/// Requirements &amp; files tab: a collapsible checklist. Opening an item expands it in place to link a file or enter a value, set its
/// review status and add a follow-up task. Only Complete counts toward readiness. Which groups and item are open survives a project
/// reload (the shell reloads after every save) through <see cref="UiState"/>.
/// </summary>
internal sealed partial class RequirementsViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectScreenServices _services;
    private readonly ProjectRequirement? _next;

    [ObservableProperty] private bool _isTipVisible;
    [ObservableProperty] private bool _isAdding;
    [ObservableProperty] private string _newRequirementTitle = "";

    public RequirementsViewModel(Project project, ProjectScreenServices services)
    {
        _project = project; _services = services; L = services.Strings;
        var state = services.State;
        if (state.InitializedProjects.Add(project.Id) && project.Requirements.Count > 0)
            state.ExpandedGroups.Add(GroupKey(project.Id, project.Requirements[0].GroupId));
        _isTipVisible = !state.TipHidden;
        _next = project.Requirements.FirstOrDefault(item => item.Status == RequirementStatus.Missing);

        Groups = project.Requirements.GroupBy(item => item.GroupId)
            .Select(group => new RequirementGroupViewModel(this, group.Key, group.Select(item => new RequirementRowViewModel(this, project, item, services)).ToList(), services))
            .ToList();
        if (state.PendingFollowUp is { } pending)
        {
            // The getting-started shortcut opens the item and asks for its follow-up form; the form only shows while the item is open.
            Groups.SelectMany(group => group.Rows).FirstOrDefault(row => row.Id == pending)?.Detail?.StartFollowUp();
            if (project.Requirements.Any(item => item.Id == pending)) state.PendingFollowUp = null;
        }
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public IReadOnlyList<RequirementGroupViewModel> Groups { get; }

    public bool HasNextUp => _next is not null;
    public string NextUpTitle => _next is null ? "" : L.Requirement(_next);
    public string NextUpHint => _next is null ? "" : L["v3.hint_" + _next.Type.ToString().ToLowerInvariant()];

    /// <summary>Blank projects have no template, so requirements are added here, one title at a time.</summary>
    public bool IsBlank => _project.TemplateId == ProjectService.BlankTemplateId;
    public bool IsNotAdding => !IsAdding;

    internal static string GroupKey(Guid projectId, string groupId) => $"{projectId}:{groupId}";

    partial void OnIsAddingChanged(bool value) => OnPropertyChanged(nameof(IsNotAdding));

    [RelayCommand]
    private void DismissTip()
    {
        _services.State.TipHidden = true;
        IsTipVisible = false;
    }

    [RelayCommand]
    private void OpenNextUp()
    {
        if (_next is null) return;
        var row = Groups.SelectMany(group => group.Rows).First(item => item.Id == _next.Id);
        Groups.First(group => group.Rows.Contains(row)).Expand();
        SetOpen(_next.Id);
    }

    [RelayCommand]
    private void StartAdding() { NewRequirementTitle = ""; IsAdding = true; }

    [RelayCommand]
    private void CancelAdding() => IsAdding = false;

    [RelayCommand]
    private Task AddRequirementAsync()
    {
        var title = NewRequirementTitle.Trim();
        if (title.Length == 0) return Task.CompletedTask;
        return _services.Host.RunAsync(async () =>
        {
            var requirement = ProjectService.NewCustomRequirement(title);
            _project.Requirements.Add(requirement);
            IsAdding = false;
            _services.State.OpenRequirement = requirement.Id;
            _services.State.ExpandedGroups.Add(GroupKey(_project.Id, requirement.GroupId));
            await _services.Projects.SaveAsync(_project);
            await _services.Host.RefreshProjectAsync();
        });
    }

    /// <summary>Opens the item (or closes it when it is the one open) and closes the others. Opening starts the item's form from scratch.</summary>
    internal void Toggle(RequirementRowViewModel row) => SetOpen(row.IsOpen ? null : row.Id);

    private void SetOpen(Guid? id)
    {
        _services.State.OpenRequirement = id;
        foreach (var row in Groups.SelectMany(group => group.Rows)) row.SetOpen(row.Id == id);
    }
}

/// <summary>A titled, collapsible group of requirements with its own completion figures.</summary>
internal sealed partial class RequirementGroupViewModel : ViewModelBase
{
    private static readonly ProjectTemplate BuiltIn = VcTemplate.Create();
    private readonly string _groupId;
    private readonly Guid _projectId;
    private readonly ProjectScreenServices _services;
    private readonly int _complete;

    [ObservableProperty] private bool _isExpanded;

    public RequirementGroupViewModel(RequirementsViewModel owner, string groupId, IReadOnlyList<RequirementRowViewModel> rows, ProjectScreenServices services)
    {
        _groupId = groupId; _services = services; L = services.Strings; Rows = rows;
        _projectId = rows.Count > 0 ? rows[0].ProjectId : Guid.Empty;
        _complete = rows.Count(row => row.IsComplete);
        _isExpanded = services.State.ExpandedGroups.Contains(RequirementsViewModel.GroupKey(_projectId, groupId));
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public IReadOnlyList<RequirementRowViewModel> Rows { get; }

    public string Title => L.Group(_groupId, _groupId == "custom" ? L["v3.blankGroup"] : BuiltIn.Groups.FirstOrDefault(group => group.Id == _groupId)?.Title ?? _groupId);
    public string SummaryText => L.Format("presentation.groupComplete", L.Number(_complete), L.Number(Rows.Count));
    public double Percent => Rows.Count == 0 ? 0 : 100.0 * _complete / Rows.Count;
    public string AccessibleName => $"{Title} · {SummaryText}";
    public string CaretIcon => IsExpanded ? Icons.CaretDown : L.IsRightToLeft ? Icons.CaretLeft : Icons.CaretRight;

    partial void OnIsExpandedChanged(bool value)
    {
        var key = RequirementsViewModel.GroupKey(_projectId, _groupId);
        if (value) _services.State.ExpandedGroups.Add(key); else _services.State.ExpandedGroups.Remove(key);
        OnPropertyChanged(nameof(CaretIcon));
    }

    internal void Expand() => IsExpanded = true;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

/// <summary>One requirement in a group: its status, what is attached, and the editor that opens beneath it.</summary>
internal sealed partial class RequirementRowViewModel : ViewModelBase
{
    private readonly RequirementsViewModel _owner;
    private readonly Project _project;
    private readonly ProjectRequirement _requirement;
    private readonly ProjectScreenServices _services;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private RequirementDetailViewModel? _detail;

    public RequirementRowViewModel(RequirementsViewModel owner, Project project, ProjectRequirement requirement, ProjectScreenServices services)
    {
        _owner = owner; _project = project; _requirement = requirement; _services = services; L = services.Strings;
        (Icon, Weight, var tone) = StatusVisuals.Requirement(requirement.Status);
        Token = tone.Foreground;
        if (services.State.OpenRequirement == requirement.Id) { _isOpen = true; _detail = new RequirementDetailViewModel(project, requirement, services); }
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public Guid Id => _requirement.Id;
    public Guid ProjectId => _project.Id;
    public bool IsComplete => _requirement.Status == RequirementStatus.Complete;

    public string Icon { get; }
    public IconWeight Weight { get; }
    public string Token { get; }
    public string Title => L.Requirement(_requirement);
    public string StatusText => L.Enum(_requirement.Status);
    public PillKind StatusKind => _requirement.Status switch
    {
        RequirementStatus.Complete => PillKind.Success,
        RequirementStatus.Provided => PillKind.Accent,
        RequirementStatus.NeedsReview => PillKind.Warning,
        _ => PillKind.Error
    };

    public bool HasFiles => _requirement.Files.Count > 0;
    public string FirstFileName => _requirement.Files.Count > 0 ? _requirement.Files[0].FileName : "";
    public bool HasMoreFiles => _requirement.Files.Count > 1;
    public string MoreFilesText => "+" + L.Number(_requirement.Files.Count - 1);
    public bool HasValue => !HasFiles && !string.IsNullOrWhiteSpace(_requirement.Value);
    public string ValueText => _requirement.Value.Trim();

    public string CaretIcon => IsOpen ? Icons.CaretUp : Icons.CaretDown;
    public string AccessibleName => L.Format("requirement.summary", Title, L.Enum(_requirement.Type), StatusText, _requirement.Files.Count);

    partial void OnIsOpenChanged(bool value) => OnPropertyChanged(nameof(CaretIcon));

    /// <summary>Opens or closes the editor. Either way what was typed in it is dropped.</summary>
    internal void SetOpen(bool open)
    {
        Detail = open ? new RequirementDetailViewModel(_project, _requirement, _services) : null;
        IsOpen = open;
    }

    [RelayCommand]
    private void Toggle() => _owner.Toggle(this);
}
