namespace ProjectOperations.Desktop.Shell;

/// <summary>Top-level sections offered by the sidebar.</summary>
public enum AppPage { Dashboard, Projects, Calendar, Documents, Settings }

/// <summary>Project detail tabs, in display order.</summary>
public enum ProjectTab { Overview = 0, Checklist = 1, Tasks = 2, Delegate = 3 }

/// <summary>Where the shell is, or should go. Screens navigate with routes instead of page strings and tab indexes.</summary>
public abstract record Route;

/// <summary>A sidebar section. <paramref name="NewProject"/> also opens the new-project wizard over the project list.</summary>
public sealed record PageRoute(AppPage Page, bool NewProject = false) : Route;

public sealed record ProjectRoute(Guid ProjectId, ProjectTab Tab = ProjectTab.Overview) : Route;
