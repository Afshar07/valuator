using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Assistant;

/// <summary>
/// The services the assistant's view-models take. <paramref name="Workspace"/> is the live workspace (the user's data or the sample), so the
/// assistant follows a switch between them; <paramref name="ConfigurationText"/> describes where the agent runs when no endpoint is known.
/// </summary>
internal sealed record AssistantServices(WorkspaceSession Workspace, LocalizedStrings Strings, LocaleContext Locale, INavigator Navigator, IAssistantHost Host,
    Func<string> ConfigurationText, TimeProvider Clock);
