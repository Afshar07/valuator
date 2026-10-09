using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;

namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// The workspace the screens work on: the user's own data, or the throw-away sample. The sample has its own database, an assistant
/// reported as not configured, and no external calendar, so it never touches the user's database or Google account.
/// </summary>
internal sealed class WorkspaceSession(ProjectService projects, AgentService agents, DesktopEnvironment environment, IExternalCalendarSource calendar,
    Func<bool, Task<SampleWorkspace>>? createSample = null) : IDisposable
{
    private readonly (ProjectService Projects, AgentService Agents, DesktopEnvironment Environment, IExternalCalendarSource Calendar) _real =
        (projects, agents, environment, calendar);
    private readonly Func<bool, Task<SampleWorkspace>> _createSample = createSample ?? (persian => SampleWorkspace.CreateAsync(persian));
    private static readonly IExternalCalendarSource NoCalendar = new NoExternalCalendar();
    private SampleWorkspace? _sample;

    public ProjectService Projects => _sample?.Projects ?? _real.Projects;
    public AgentService Agents => _sample?.Agents ?? _real.Agents;
    public DesktopEnvironment Environment => _sample is null ? _real.Environment : _real.Environment with { AgentConfigured = false };
    /// <summary>Optional read-only external calendar. Display only: it is never given to <see cref="AgentService"/> or stored with a project.</summary>
    public IExternalCalendarSource Calendar => _sample is null ? _real.Calendar : NoCalendar;
    public bool IsSample => _sample is not null;

    /// <summary>Switches to a fresh sample workspace. Returns false when the sample is already open.</summary>
    public async Task<bool> StartSampleAsync(bool persian)
    {
        if (IsSample) return false;
        _sample = await _createSample(persian);
        return true;
    }

    /// <summary>Discards the sample and returns to the user's own data. Returns false when no sample was open.</summary>
    public bool ExitSample()
    {
        if (_sample is null) return false;
        _sample.Dispose(); _sample = null;
        return true;
    }

    public void Dispose() => ExitSample();
}
