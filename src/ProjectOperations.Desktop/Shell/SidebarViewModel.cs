using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Shell;

/// <summary>What the sidebar asks of the shell.</summary>
internal interface ISidebarHost
{
    /// <summary>Shows a section. A failure lands in the error banner.</summary>
    Task NavigateAsync(AppPage page);
    /// <summary>Runs an action under the shell's busy state.</summary>
    Task RunAsync(Func<Task> action);
}

/// <summary>
/// The sidebar: navigation entries that appear as data arrives (and are flagged New once), the attention count, the getting-started card
/// and the theme and language controls. Which entries show comes from <see cref="SidebarPolicy"/>; this view-model keeps the state.
/// </summary>
internal sealed partial class SidebarViewModel : ViewModelBase
{
    private static readonly (AppPage Page, string Key, string ControlName, string Icon)[] Entries =
    [
        (AppPage.Dashboard, "navigation.attention", "NavigationDashboard", Icons.BellSimple),
        (AppPage.Projects, "navigation.projects", "NavigationProjects", Icons.Folders),
        (AppPage.Calendar, "presentation.navigationCalendar", "NavigationCalendar", Icons.CalendarBlank),
        (AppPage.Documents, "presentation.navigationDocuments", "NavigationDocuments", Icons.Files),
        (AppPage.Settings, "presentation.navigationSettings", "NavigationSettings", Icons.GearSix)
    ];

    private readonly Dictionary<AppPage, bool> _shown = [];
    private bool _known;

    [ObservableProperty] private int _attentionCount;
    [ObservableProperty] private GettingStartedViewModel? _gettingStarted;

    public SidebarViewModel(LocalizedStrings strings, DisplayOptionsViewModel display, ISidebarHost host)
    {
        L = strings; Display = display;
        Items = Entries.Select(entry => new NavItemViewModel(strings, entry.Page, entry.Key, entry.ControlName, entry.Icon,
            () => host.NavigateAsync(entry.Page))).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public DisplayOptionsViewModel Display { get; }
    public IReadOnlyList<NavItemViewModel> Items { get; }

    public bool HasAttention => AttentionCount > 0;
    public string AttentionText => L.Number(AttentionCount);
    public bool HasGettingStarted => GettingStarted is not null;

    partial void OnAttentionCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasAttention));
        OnPropertyChanged(nameof(AttentionText));
        Item(AppPage.Dashboard).CountText = L.Number(value);
        Item(AppPage.Dashboard).HasCount = value > 0;
    }

    partial void OnGettingStartedChanged(GettingStartedViewModel? value) => OnPropertyChanged(nameof(HasGettingStarted));

    private NavItemViewModel Item(AppPage page) => Items.First(item => item.Page == page);

    /// <summary>Marks the section on screen.</summary>
    public void Select(AppPage page)
    {
        foreach (var item in Items) item.IsSelected = item.Page == page;
    }

    public bool IsVisible(AppPage page) => Item(page).IsVisible;

    /// <summary>Opening a section clears its New flag.</summary>
    public void MarkSeen(AppPage page) => Item(page).IsNew = false;

    /// <summary>
    /// Shows the sections the policy allows. A section that appears after the first call (and outside the sample workspace) is flagged New
    /// and returned so the shell can announce it.
    /// </summary>
    public IReadOnlyList<AppPage> Apply(IReadOnlyDictionary<AppPage, bool> sections, bool isSample)
    {
        var revealed = new List<AppPage>();
        foreach (var (page, show) in sections)
        {
            var item = Item(page);
            if (_known && !isSample && show && !_shown.GetValueOrDefault(page)) { item.IsNew = true; revealed.Add(page); }
            _shown[page] = show;
            item.IsVisible = show;
        }
        _known = true;
        return revealed;
    }

    /// <summary>Forgets what was shown and flagged (switching between the sample and the user's own data).</summary>
    public void Reset()
    {
        _shown.Clear(); _known = false;
        foreach (var item in Items) item.IsNew = false;
    }

    /// <summary>Shows the getting-started card with these steps, or hides it when there are none.</summary>
    public void ShowGettingStarted(IReadOnlyList<GettingStartedStep>? steps, Func<Task> hide) =>
        GettingStarted = steps is null ? null : new GettingStartedViewModel(L, steps, hide);
}

/// <summary>One navigation entry.</summary>
internal sealed partial class NavItemViewModel : ViewModelBase
{
    private readonly string _key;

    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private bool _hasCount;
    [ObservableProperty] private string _countText = "";

    public NavItemViewModel(LocalizedStrings strings, AppPage page, string key, string controlName, string icon, Func<Task> navigate)
    {
        L = strings; Page = page; _key = key; ControlName = controlName; Icon = icon;
        NavigateCommand = new AsyncRelayCommand(navigate);
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }
    public AppPage Page { get; }
    /// <summary>The automation name of the button (tests and assistive technology find the entry by it).</summary>
    public string ControlName { get; }
    public string Icon { get; }
    public string Title => L[_key];
    public string IconToken => IsSelected ? "TextPrimary" : "TextSecondary";
    public IAsyncRelayCommand NavigateCommand { get; }

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(IconToken));
}

/// <summary>One getting-started step: caption key, whether it is done, whether it is optional, and what moves it forward.</summary>
internal sealed record GettingStartedStep(string Key, bool Done, bool Optional, Func<Task>? Go);

/// <summary>The getting-started card: progress and the five milestones, each a button that moves it forward until it is done.</summary>
internal sealed class GettingStartedViewModel : ViewModelBase
{
    private readonly int _done;

    public GettingStartedViewModel(LocalizedStrings strings, IReadOnlyList<GettingStartedStep> steps, Func<Task> hide)
    {
        L = strings;
        Items = steps.Select(step => new GettingStartedItemViewModel(strings, step)).ToList();
        _done = steps.Count(step => step.Done);
        HideCommand = new AsyncRelayCommand(hide);
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }
    public IReadOnlyList<GettingStartedItemViewModel> Items { get; }
    public double Percent => Items.Count == 0 ? 0 : 100.0 * _done / Items.Count;
    public string CountText => $"{L.Number(_done)}/{L.Number(Items.Count)}";
    public IAsyncRelayCommand HideCommand { get; }
}

internal sealed class GettingStartedItemViewModel : ViewModelBase
{
    private readonly LocalizedStrings _strings;
    private readonly GettingStartedStep _step;

    public GettingStartedItemViewModel(LocalizedStrings strings, GettingStartedStep step)
    {
        _strings = strings; _step = step;
        GoCommand = new AsyncRelayCommand(() => CanGo ? step.Go!() : Task.CompletedTask);
        RefreshOnLanguageChange(strings);
    }

    public string Title => _strings[_step.Key];
    public LocalizedStrings L => _strings;
    public bool IsDone => _step.Done;
    public bool IsOptional => _step.Optional;
    /// <summary>A step that is done, or that has nothing to go to, cannot be pressed.</summary>
    public bool CanGo => !_step.Done && _step.Go is not null;
    public string Icon => _step.Done ? Icons.CheckCircle : Icons.Circle;
    public IconWeight Weight => _step.Done ? IconWeight.Fill : IconWeight.Regular;
    public string IconToken => _step.Done ? "Success" : "TextTertiary";
    public IAsyncRelayCommand GoCommand { get; }
}
