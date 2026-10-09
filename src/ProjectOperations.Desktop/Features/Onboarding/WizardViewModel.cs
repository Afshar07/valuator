using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Onboarding;

/// <summary>Three-step new-project wizard: name, checklist template, first item. Nothing is created until the last step.</summary>
internal sealed partial class WizardViewModel : ViewModelBase
{
    private const int LastStep = 2;
    private readonly IOnboardingHost _host;
    private readonly IFilePicker _picker;
    private readonly bool _fromWelcome;

    [ObservableProperty] private int _step;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _company = "";
    [ObservableProperty] private string _requirement = "";
    [ObservableProperty] private int _template;
    [ObservableProperty] private string? _deckPath;

    public WizardViewModel(IOnboardingHost host, LocalizedStrings strings, DisplayOptionsViewModel display, IFilePicker picker, bool fromWelcome)
    {
        _host = host; L = strings; Display = display; _picker = picker; _fromWelcome = fromWelcome;
        Steps = Enumerable.Range(0, 3).Select(index => new WizardStepViewModel(strings, index)).ToList();
        TemplateOptions = [new TemplateOptionViewModel(strings, 0, "TemplateVc", "v3.tplVc", "v3.tplVcSub", this), new TemplateOptionViewModel(strings, 1, "TemplateBlank", "v3.tplBlank", "v3.tplBlankSub", this)];
        PreviewGroups = VcTemplate.Create().Groups.Select((group, index) => new PreviewGroupViewModel(strings, group, index == 0)).ToList();
        UpdateSteps(); UpdateTemplates();
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }
    public DisplayOptionsViewModel Display { get; }
    public IReadOnlyList<WizardStepViewModel> Steps { get; }
    public IReadOnlyList<TemplateOptionViewModel> TemplateOptions { get; }
    public IReadOnlyList<PreviewGroupViewModel> PreviewGroups { get; }

    public bool IsNameStep => Step == 0;
    public bool IsTemplateStep => Step == 1;
    public bool IsFirstItemStep => Step == LastStep;
    public bool IsVcTemplate => Template == 0;
    public bool IsBlankTemplate => Template == 1;
    public bool ShowPreview => IsTemplateStep && IsVcTemplate;
    public bool ShowDeck => IsFirstItemStep && IsVcTemplate;
    public bool ShowRequirement => IsFirstItemStep && IsBlankTemplate;

    public string HeadingText => L[Step switch { 0 => "v3.w1Title", 1 => "v3.w2Title", _ => "v3.w3Title" }];
    public string SubheadingText => L[Step switch { 0 => "v3.w1Sub", 1 => "v3.w2Sub", _ => IsVcTemplate ? "v3.w3Sub" : "v3.w3BlankSub" }];

    public bool HasDeck => DeckPath is not null;
    public string DeckTitle => L.Requirement("pitch-deck", "Pitch deck");
    public string DeckSubtitle => DeckPath is { } path ? Path.GetFileName(path) : L.Enum(RequirementType.Document);
    public string DeckIcon => HasDeck ? Icons.CheckCircle : Icons.FileDashed;
    public IconWeight DeckIconWeight => HasDeck ? IconWeight.Fill : IconWeight.Regular;
    public string DeckIconToken => HasDeck ? "Success" : "TextTertiary";

    /// <summary>The next step needs a name; the others need nothing.</summary>
    public bool CanContinue => Step != 0 || Name.Trim().Length > 0;
    /// <summary>A first item, once given, makes Skip pointless.</summary>
    public bool ShowSkip => IsFirstItemStep && !(IsVcTemplate ? HasDeck : Requirement.Trim().Length > 0);
    public string PrimaryText => L[IsFirstItemStep ? "v3.openProject" : "v3.next"];
    public string BackIcon => L.IsRightToLeft ? Icons.ArrowRight : Icons.ArrowLeft;

    partial void OnStepChanged(int value)
    {
        UpdateSteps();
        OnPropertyChanged(string.Empty);
    }

    partial void OnTemplateChanged(int value)
    {
        UpdateTemplates();
        OnPropertyChanged(string.Empty);
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanContinue));
    partial void OnRequirementChanged(string value) => OnPropertyChanged(nameof(ShowSkip));

    partial void OnDeckPathChanged(string? value)
    {
        foreach (var name in new[] { nameof(HasDeck), nameof(DeckSubtitle), nameof(DeckIcon), nameof(DeckIconWeight), nameof(DeckIconToken), nameof(ShowSkip) }) OnPropertyChanged(name);
    }

    private void UpdateSteps() { foreach (var step in Steps) step.Update(Step); }
    private void UpdateTemplates() { foreach (var option in TemplateOptions) option.Update(Template); }

    internal void ChooseTemplate(int index) => Template = index;

    [RelayCommand]
    private Task BackAsync() => _host.RunAsync(() =>
    {
        if (Step > 0) Step--;
        else if (_fromWelcome) _host.ShowWelcome();
        else _host.CloseOverlay();
        return Task.CompletedTask;
    });

    [RelayCommand]
    private Task PrimaryAsync() => _host.RunAsync(() =>
    {
        if (Step < LastStep) { Step++; return Task.CompletedTask; }
        return FinishAsync();
    });

    [RelayCommand]
    private Task SkipAsync() => _host.RunAsync(FinishAsync);

    [RelayCommand]
    private Task ChooseDeckAsync() => _host.RunAsync(async () =>
    {
        var files = await _picker.PickAsync(L["file.pickerTitle"]);
        if (files.Count == 0) return;
        if (files[0].LocalPath is not { } path) { _host.ShowError("validation.localFilesOnly"); return; }
        DeckPath = path;
    });

    private async Task FinishAsync()
    {
        // Starting a real project from the sample workspace leaves the sample (and its throw-away data) behind.
        if (_host.IsSample) await _host.ExitSampleAsync();
        var name = Name.Trim().Length > 0 ? Name.Trim() : L["v3.defaultName"];
        Project project;
        if (IsVcTemplate)
        {
            project = await _host.Projects.CreateAsync(name, Company, ProjectStatus.Active, "", "");
            if (DeckPath is { } deckPath && project.Requirements.FirstOrDefault(item => item.DefinitionId == "pitch-deck") is { } deck)
            {
                deck.Files.Add(new ProjectFile { FileName = Path.GetFileName(deckPath), Path = deckPath, SizeBytes = File.Exists(deckPath) ? new FileInfo(deckPath).Length : 0 });
                deck.Status = RequirementStatus.Provided;
                await _host.Projects.SaveAsync(project);
            }
        }
        else
            project = await _host.Projects.CreateBlankAsync(name, Company, ProjectStatus.Active, "", "",
                Requirement.Trim().Length > 0 ? Requirement : L["v3.defaultRequirement"]);
        _host.CloseOverlay();
        await _host.OpenCreatedProjectAsync(project.Id);
    }
}

/// <summary>One of the three step markers above the wizard card: a number, or a tick once passed, and its name.</summary>
internal sealed partial class WizardStepViewModel : ViewModelBase
{
    [ObservableProperty] private bool _reached;
    [ObservableProperty] private bool _done;
    [ObservableProperty] private bool _current;

    public WizardStepViewModel(LocalizedStrings strings, int index)
    {
        L = strings; Index = index;
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }
    public int Index { get; }
    public string NumberText => L.Number(Index + 1);
    public string Label => L["v3.wStep" + Index];
    /// <summary>A rule runs from every step but the last to the next one.</summary>
    public bool HasLine => Index < 2;
    public bool ShowNumber => !Done;

    internal void Update(int step)
    {
        Reached = Index <= step; Done = Index < step; Current = Index == step;
        OnPropertyChanged(nameof(ShowNumber));
    }
}

/// <summary>A checklist template the wizard offers (the VC checklist or a blank one).</summary>
internal sealed partial class TemplateOptionViewModel : ViewModelBase
{
    private readonly WizardViewModel _wizard;
    private readonly string _titleKey, _subKey;

    [ObservableProperty] private bool _isSelected;

    public TemplateOptionViewModel(LocalizedStrings strings, int index, string controlName, string titleKey, string subKey, WizardViewModel wizard)
    {
        L = strings; Index = index; ControlName = controlName; _titleKey = titleKey; _subKey = subKey; _wizard = wizard;
        RefreshOnLanguageChange(strings);
    }

    public LocalizedStrings L { get; }
    public int Index { get; }
    public string ControlName { get; }
    public string Title => L[_titleKey];
    public string Subtitle => L[_subKey];
    public string Icon => IsSelected ? Icons.RadioButton : Icons.Circle;
    public IconWeight Weight => IsSelected ? IconWeight.Fill : IconWeight.Regular;
    public string IconToken => IsSelected ? "Accent" : "TextTertiary";

    internal void Update(int template)
    {
        IsSelected = Index == template;
        OnPropertyChanged(nameof(Icon)); OnPropertyChanged(nameof(Weight)); OnPropertyChanged(nameof(IconToken));
    }

    [RelayCommand]
    private void Choose() => _wizard.ChooseTemplate(Index);
}

/// <summary>A group of the VC checklist as the template preview lists it: its name, the first three items and the count.</summary>
internal sealed class PreviewGroupViewModel : ViewModelBase
{
    private readonly LocalizedStrings _strings;
    private readonly RequirementGroup _group;

    public PreviewGroupViewModel(LocalizedStrings strings, RequirementGroup group, bool isFirst)
    {
        _strings = strings; _group = group; IsFirst = isFirst;
        RefreshOnLanguageChange(strings);
    }

    /// <summary>The first group has no rule above it.</summary>
    public bool IsFirst { get; }
    public string Title => _strings.Group(_group.Id, _group.Title);
    public string Items => string.Join(" · ", _group.Requirements.Take(3).Select(item => _strings.Requirement(item.Id, item.Title))) + (_group.Requirements.Count > 3 ? " …" : "");
    public string CountText => $"{_strings.Number(_group.Requirements.Count)} {_strings["v3.items"]}";
}
