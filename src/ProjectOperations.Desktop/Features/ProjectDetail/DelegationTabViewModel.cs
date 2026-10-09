using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Common;

namespace ProjectOperations.Desktop.Features.ProjectDetail;

/// <summary>
/// Carries what the code-built Delegate tab needs. A stand-in until the assistant moves to MVVM (phase 5), when this and
/// <see cref="DelegationTabView"/> are replaced by the real delegation view-model.
/// </summary>
internal sealed class DelegationTabViewModel(PresentationContext context, Project project, IReadOnlyList<AgentJob> jobs) : ViewModelBase
{
    public PresentationContext Context { get; } = context;
    public Project Project { get; } = project;
    public IReadOnlyList<AgentJob> Jobs { get; } = jobs;
}
