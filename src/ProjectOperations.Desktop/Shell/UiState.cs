namespace ProjectOperations.Desktop.Shell;

/// <summary>View state that survives a project reload (the shell rebuilds screens after every save): expanded checklist rows, dismissed hints.</summary>
internal sealed class UiState
{
    public HashSet<string> ExpandedGroups { get; } = [];
    public HashSet<Guid> InitializedProjects { get; } = [];
    public Guid? OpenRequirement { get; set; }
    public bool TipHidden { get; set; }
    public bool GettingStartedHidden { get; set; }
    /// <summary>A checklist item whose follow-up task form should open as soon as its screen is built (getting-started shortcut).</summary>
    public Guid? PendingFollowUp { get; set; }

    /// <summary>Forgets everything tied to one workspace (used when switching between the sample and the user's own data).</summary>
    public void Reset() { ExpandedGroups.Clear(); InitializedProjects.Clear(); OpenRequirement = null; TipHidden = false; GettingStartedHidden = false; PendingFollowUp = null; }
}
