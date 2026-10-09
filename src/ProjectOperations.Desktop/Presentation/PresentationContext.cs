using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Features.Settings;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;
using ProjectOperations.Desktop.Updates;

namespace ProjectOperations.Desktop;

/// <summary>Runtime facts the shell displays (never edits): agent configuration and local storage location.</summary>
public sealed record DesktopEnvironment(bool AgentConfigured = true, string? Endpoint = null, string? ConfigDirectory = null, string? DatabasePath = null);

/// <summary>
/// What the code-built screens still left (the assistant panel and the Delegate tab) ask of the shell. The shell itself is
/// <see cref="MainWindowViewModel"/>; phase 5 moves these screens to MVVM and removes this interface.
/// </summary>
internal interface IShell
{
    Task OpenProjectAsync(Guid id, ProjectTab tab);
    /// <summary>Reloads the open project on its current tab (after reviews or runs); the assistant keeps its controls.</summary>
    Task RefreshProjectAsync();
    Task ActAsync(Button button, Func<Task> action);
    void ShowError(string key);
    /// <summary>Locks navigation, project tabs and close for the complete runtime lifetime of one agent job.</summary>
    Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh);
    void CancelRun();
    bool IsAgentRunning { get; }
    void SetAssistantOpen(bool open);
    Task ReviewInAssistantAsync(string jobId);
}

/// <summary><see cref="IShell"/> over the shell view-model.</summary>
internal sealed class ShellAdapter(MainWindowViewModel shell) : IShell
{
    public Task OpenProjectAsync(Guid id, ProjectTab tab) => shell.OpenProjectAsync(id, tab);
    public Task RefreshProjectAsync() => shell.RefreshProjectAsync();
    public async Task ActAsync(Button button, Func<Task> action)
    {
        button.IsEnabled = false;
        try { await shell.RunAsync(action); }
        finally { button.IsEnabled = true; }
    }
    public void ShowError(string key) => shell.ShowError(key);
    public Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh) => shell.RunAgentAsync(run, refresh);
    public void CancelRun() => shell.CancelRun();
    public bool IsAgentRunning => shell.IsAgentRunning;
    public void SetAssistantOpen(bool open) => shell.SetAssistantOpen(open);
    public Task ReviewInAssistantAsync(string jobId) => shell.ReviewInAssistantAsync(jobId);
}

/// <summary>Shared services and localized control factories for the code-built screens (the assistant panel and the Delegate tab).</summary>
internal sealed class PresentationContext(
    WorkspaceSession workspace, LocaleContext locale, LocalizationService text, LocalizedControls localized,
    LocalizedStrings strings, string configuration, Func<string>? configurationText, IShell shell)
{
    public WorkspaceSession Workspace { get; } = workspace;
    public ProjectService Projects => Workspace.Projects;
    public AgentService Agents => Workspace.Agents;
    public LocaleContext Locale { get; } = locale;
    public LocalizationService Text { get; } = text;
    public LocalizedControls Localized { get; } = localized;
    /// <summary>Bindable strings.</summary>
    public LocalizedStrings Strings { get; } = strings;
    public DesktopEnvironment Environment => Workspace.Environment;
    public IShell Shell { get; } = shell;
    public string ConfigurationText() => configurationText?.Invoke() ?? configuration;
    public Task OpenProjectAsync(Guid id, ProjectTab tab = ProjectTab.Overview) => Shell.OpenProjectAsync(id, tab);
    public Task ActAsync(Button button, Func<Task> action) => Shell.ActAsync(button, action);
    public void ShowError(string key) => Shell.ShowError(key);

    public string EnumText<T>(T value) where T : struct, Enum => new DomainDisplay(Text).Enum(value);
    public string Due(DateTimeOffset? date) => Strings.Due(date);
    public string ShortDate(DateTimeOffset? date) => Strings.ShortDate(date);
    public string ShortDate(DateTime date) => new LocaleDateFormatter(Locale).ShortDate(date);
    public string MonthYear(DateTime date) => new LocaleDateFormatter(Locale).MonthYear(date);
    public string Number(int value) => Strings.Number(value);

    /// <summary>Whole local days from today to the date, as friendly relative text. Open work past its date reads "late", closed work "ago".</summary>
    public string Relative(DateTimeOffset? date, bool open) => Strings.Relative(date, open, DateTime.Today);

    public TextBlock Label(string key, string style = "Body", string color = "TextPrimary") => Label(() => Text.Get(key), style, color);
    public TextBlock Label(Func<string> text, string style = "Body", string color = "TextPrimary")
    {
        var label = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Start };
        PresentationTheme.Typeset(label, style, color);
        Localized.Bind(label, control => control.Text = text()); return label;
    }
    public TextBlock Heading(string key, string style = "Heading") => Label(key, style);
    public TextBlock Heading(Func<string> text, string style = "Heading") => Label(text, style);
    public Button Action(string key, Func<Task> action, string? appearance = null) => Action(() => Text.Get(key), action, appearance);
    public Button Action(Func<string> text, Func<Task> action, string? appearance = null)
    {
        var button = new Button { HorizontalAlignment = HorizontalAlignment.Left };
        if (appearance is not null) foreach (var name in appearance.Split(' ')) button.Classes.Add(name);
        Localized.Bind(button, control => control.Content = text());
        button.Click += async (_, _) => await ActAsync(button, action); return button;
    }
    /// <summary>Button with a leading icon. The accessible name is the localized caption.</summary>
    public Button IconAction(string key, string icon, Func<Task> action, string? appearance = null, IconWeight weight = IconWeight.Regular, bool iconOnly = false)
    {
        var button = new Button { HorizontalAlignment = HorizontalAlignment.Left };
        if (appearance is not null) foreach (var name in appearance.Split(' ')) button.Classes.Add(name);
        var glyph = Icons.Glyph(icon, iconOnly ? 15 : 14, "TextPrimary", weight);
        glyph.Bind(TextBlock.ForegroundProperty, button.GetObservable(Button.ForegroundProperty));
        if (iconOnly) button.Content = glyph;
        else
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var caption = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(glyph); row.Children.Add(caption); button.Content = row;
            Localized.Bind(caption, control => control.Text = Text.Get(key));
        }
        Localized.Bind(button, control => { AutomationProperties.SetName(control, Text.Get(key)); ToolTip.SetTip(control, iconOnly ? Text.Get(key) : null); });
        button.Click += async (_, _) => await ActAsync(button, action); return button;
    }
}
