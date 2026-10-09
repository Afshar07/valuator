using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Overview;

/// <summary>
/// Overview tab: the explicit current state, requirements that are missing or need review, open questions and follow-ups.
/// The figures come from <see cref="ProjectSummaries"/>; questions and follow-ups are added in place and saved to the project's state.
/// </summary>
internal sealed class OverviewViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectSummary _summary;

    public OverviewViewModel(Project project, ProjectScreenServices services)
    {
        _project = project; L = services.Strings;
        _summary = ProjectSummaries.Summarize(project, services.Clock.GetUtcNow());
        MissingItems = _summary.MissingRequirements.Select(requirement => new MissingItemViewModel(requirement, L)).ToList();
        OpenQuestions = new StateListViewModel(project, project.State.OpenQuestions, "presentation.openQuestions", "OpenQuestionsCard", Icons.Question, services);
        FollowUps = new StateListViewModel(project, project.State.FollowUps, "presentation.followUps", "FollowUpsCard", Icons.ArrowBendUpRight, services);
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public ProjectStage Stage => _project.Stage;

    public string NextMilestoneText => _summary.NextMilestone is { } milestone ? $"{milestone.Title} · {L.ShortDate(milestone.DueAt)}" : L["overview.noMilestone"];
    public string ReadinessText => $"{L.Number(_summary.CompleteRequirements)}/{L.Number(_summary.TotalRequirements)}";
    public string OverdueText => L.Number(_summary.OverdueTasks.Count);
    public bool HasOverdue => _summary.OverdueTasks.Count > 0;
    public string OpenQuestionsCount => L.Number(_project.State.OpenQuestions.Count);
    public string FollowUpsCount => L.Number(_project.State.FollowUps.Count);

    public bool HasSummary => !string.IsNullOrWhiteSpace(_project.State.Summary);
    public string SummaryText => _project.State.Summary;
    public bool HasNotes => !string.IsNullOrWhiteSpace(_project.Notes);
    public string NotesText => HasNotes ? _project.Notes : L["date.notSet"];

    public IReadOnlyList<MissingItemViewModel> MissingItems { get; }
    public string MissingCount => L.Number(MissingItems.Count);
    public bool HasNoMissing => MissingItems.Count == 0;

    public StateListViewModel OpenQuestions { get; }
    public StateListViewModel FollowUps { get; }
}

/// <summary>A requirement that is missing or needs review, with its status icon and colour.</summary>
internal sealed class MissingItemViewModel : ViewModelBase
{
    private readonly ProjectRequirement _requirement;
    private readonly LocalizedStrings _strings;

    public MissingItemViewModel(ProjectRequirement requirement, LocalizedStrings strings)
    {
        _requirement = requirement; _strings = strings;
        (Icon, Weight, var tone) = StatusVisuals.Requirement(requirement.Status);
        Token = tone.Foreground;
        RefreshOnLanguageChange(strings);
    }

    public string Icon { get; }
    public IconWeight Weight { get; }
    public string Token { get; }
    public bool IsMissing => _requirement.Status == RequirementStatus.Missing;
    public string Title => _strings.Requirement(_requirement);
    public string StatusText => _strings.Enum(_requirement.Status);
}

/// <summary>One line in an open-questions or follow-ups list.</summary>
internal sealed record StateItem(string Text, string Icon);

/// <summary>An open-questions or follow-ups card: the items, and an inline line to add one that saves to the project's explicit state.</summary>
internal sealed partial class StateListViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly List<string> _items;
    private readonly string _titleKey;
    private readonly ProjectScreenServices _services;

    [ObservableProperty] private bool _isAdding;
    [ObservableProperty] private string _newText = "";

    public StateListViewModel(Project project, List<string> items, string titleKey, string cardName, string icon, ProjectScreenServices services)
    {
        _project = project; _items = items; _titleKey = titleKey; CardName = cardName; _services = services; L = services.Strings;
        Items = items.Select(text => new StateItem(text, icon)).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    /// <summary>The automation name of the card.</summary>
    public string CardName { get; }
    public string Title => L[_titleKey];
    public IReadOnlyList<StateItem> Items { get; }
    public string CountText => L.Number(_items.Count);
    public bool IsEmpty => _items.Count == 0;

    [RelayCommand]
    private void StartAdd() { NewText = ""; IsAdding = true; }

    [RelayCommand]
    private void Cancel() => IsAdding = false;

    [RelayCommand]
    private Task SaveAsync()
    {
        var text = NewText.Trim();
        if (text.Length == 0) return Task.CompletedTask;
        return _services.Host.RunAsync(async () =>
        {
            _items.Add(text);
            await _services.Projects.SaveAsync(_project);
            await _services.Host.RefreshProjectAsync();
        });
    }
}
