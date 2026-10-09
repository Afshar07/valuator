using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop.Shell;

/// <summary>
/// What the sidebar offers and what the getting-started card counts as done, from stored project data. The workspace opens with only
/// Projects and Settings; the rest appear once there is something to show. Pure, so it is tested without a window.
/// </summary>
internal static class SidebarPolicy
{
    /// <summary>True when any task or milestone has a date (the attention home and the calendar then have something to show).</summary>
    public static bool HasDatedWork(IReadOnlyList<Project> projects) =>
        projects.Any(project => project.Tasks.Any(task => task.DueAt is not null) || project.Milestones.Any(milestone => milestone.DueAt is not null));

    /// <summary>True when any requirement links a file (Documents then has something to list).</summary>
    public static bool HasLinkedFile(IReadOnlyList<Project> projects) =>
        projects.Any(project => project.Requirements.Any(requirement => requirement.Files.Count > 0));

    /// <summary>Which sections the sidebar shows. The sample workspace shows all of them; Google Calendar alone is enough for Calendar.</summary>
    public static IReadOnlyDictionary<AppPage, bool> Sections(IReadOnlyList<Project> projects, bool isSample, bool googleConnected)
    {
        var dated = HasDatedWork(projects);
        return new Dictionary<AppPage, bool>
        {
            [AppPage.Dashboard] = isSample || dated,
            [AppPage.Projects] = true,
            [AppPage.Calendar] = isSample || dated || googleConnected,
            [AppPage.Documents] = isSample || HasLinkedFile(projects),
            [AppPage.Settings] = true
        };
    }

    /// <summary>
    /// The five getting-started milestones: a project exists, something is on its checklist, a file is linked, a task has a date, the
    /// assistant is set up (optional).
    /// </summary>
    public static bool[] GettingStartedDone(IReadOnlyList<Project> projects, bool agentConfigured)
    {
        var requirements = projects.SelectMany(project => project.Requirements).ToList();
        return
        [
            true,
            requirements.Any(requirement => requirement.Status != RequirementStatus.Missing || requirement.Value.Trim().Length > 0),
            requirements.Any(requirement => requirement.Files.Count > 0),
            projects.Any(project => project.Tasks.Any(task => task.DueAt is not null)),
            agentConfigured
        ];
    }
}
