using CommunityToolkit.Mvvm.ComponentModel;
using ProjectOperations.Core.Agents;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Features.Assistant;

/// <summary>One thing the agent reported doing, in project terms ("Reading project information"). The last step of a running job is the current one.</summary>
internal sealed partial class AssistantStepViewModel : ViewModelBase
{
    private readonly LocalizedStrings _strings;
    private readonly AgentEvent _event;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icon))]
    private bool _isCurrent;

    public AssistantStepViewModel(LocalizedStrings strings, AgentEvent item, bool isCurrent)
    {
        _strings = strings; _event = item; _isCurrent = isCurrent;
        RefreshOnLanguageChange(strings);
    }

    /// <summary>The translated activity when the runtime gave a key, otherwise its own wording.</summary>
    public string Text => _event.ActivityKey is null ? _event.Message : _strings[_event.ActivityKey];
    public string Icon => IsCurrent ? Icons.CircleNotch : Icons.Check;
}
