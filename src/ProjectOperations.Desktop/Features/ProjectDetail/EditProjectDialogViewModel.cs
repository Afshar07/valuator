using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.ProjectDetail;

/// <summary>Edit-project dialog opened from the pencil beside the project title: name, company, owner, stage, status, notes and current state.</summary>
internal sealed partial class EditProjectDialogViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly ProjectScreenServices _services;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _company;
    [ObservableProperty] private string _owner;
    [ObservableProperty] private string _notes;
    [ObservableProperty] private string _summary;
    [ObservableProperty] private ProjectStage? _selectedStage;
    [ObservableProperty] private bool _showNameError;

    public EditProjectDialogViewModel(Project project, IReadOnlyList<ProjectStage> stages, ProjectScreenServices services)
    {
        _project = project; _services = services; L = services.Strings;
        _name = project.Name; _company = project.CompanyName; _owner = project.Owner; _notes = project.Notes; _summary = project.State.Summary;
        Stages = stages;
        _selectedStage = stages.FirstOrDefault(stage => stage.Id == project.StageId);
        Status = new ChipGroupViewModel<ProjectStatus>(L, Enum.GetValues<ProjectStatus>().Select(item => (item, (Func<string>)(() => L.Enum(item)))), project.Status);
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public string Heading => L["v3.editProject"];
    public IReadOnlyList<ProjectStage> Stages { get; }
    public ChipGroupViewModel<ProjectStatus> Status { get; }

    partial void OnNameChanged(string value) => ShowNameError = false;

    [RelayCommand]
    private Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { ShowNameError = true; return Task.CompletedTask; }
        return _services.Host.RunAsync(async () =>
        {
            _project.Name = Name.Trim(); _project.CompanyName = Company?.Trim() ?? ""; _project.Owner = Owner?.Trim() ?? "";
            if (SelectedStage is { } stage) { _project.Stage = stage; _project.StageId = stage.Id; }
            _project.Status = Status.Selected;
            _project.Notes = Notes ?? ""; _project.State.Summary = Summary ?? "";
            await _services.Projects.SaveAsync(_project);
            _services.Dialogs.Close();
            await _services.Host.RefreshProjectAsync();
        });
    }

    [RelayCommand]
    private void Cancel() => _services.Dialogs.Close();
}
