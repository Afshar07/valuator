using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Delegation;

/// <summary>
/// The Delegate &amp; review tab: whether the assistant is ready, a way to open it, and the project's lightweight job history. Runs happen in the
/// assistant panel; this tab only keeps what was asked and what came of it, newest first.
/// </summary>
internal sealed partial class DelegationViewModel : ViewModelBase
{
    private readonly ProjectScreenServices _services;
    private readonly bool _agentConfigured;

    public DelegationViewModel(Project project, IReadOnlyList<AgentJob> jobs, bool agentConfigured, ProjectScreenServices services)
    {
        _services = services; _agentConfigured = agentConfigured; L = services.Strings;
        Rows = jobs.OrderByDescending(job => job.CreatedAt).Select(job => new HistoryRowViewModel(job, services)).ToList();
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }
    public string StatusText => L[_agentConfigured ? "agent.state.ready" : "agent.state.off"];
    public string StatusToken => _agentConfigured ? "Accent" : "TextTertiary";
    public IReadOnlyList<HistoryRowViewModel> Rows { get; }
    public bool IsEmpty => Rows.Count == 0;

    [RelayCommand]
    private void OpenAssistant() => _services.Host.OpenAssistant();
}
