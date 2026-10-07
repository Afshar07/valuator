using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Runtime facts the shell displays (never edits): agent configuration and local storage location.</summary>
public sealed record DesktopEnvironment(bool AgentConfigured = true, string? Endpoint = null, string? ConfigDirectory = null, string? DatabasePath = null);

/// <summary>Shell operations available to screens. <see cref="MainWindow"/> owns navigation, locking and the assistant panel.</summary>
internal interface IShell
{
    Task OpenProjectAsync(Guid id, int tab);
    Task NavigateAsync(string page);
    /// <summary>Reloads the open project on its current tab (after reviews or runs); the assistant keeps its controls.</summary>
    Task RefreshProjectAsync();
    Task ActAsync(Button button, Func<Task> action);
    void ShowError(string key);
    /// <summary>Locks navigation, project tabs and close for the complete runtime lifetime of one agent job.</summary>
    Task RunAgentAsync(Func<CancellationToken, Task> run, Func<Task> refresh);
    void CancelRun();
    bool IsAgentRunning { get; }
    void SetAssistantOpen(bool open);
    void ToggleAssistant();
    Task ReviewInAssistantAsync(string jobId);
    void ShowModal(Control dialog);
    void CloseModal();
}

/// <summary>Shared services and localized control factories handed to views and components by the shell.</summary>
internal sealed class PresentationContext(
    ProjectService projects, AgentService agents, LocaleContext locale, AppearanceContext appearance, LocalizationService text, LocalizedControls localized,
    string configuration, Func<string>? configurationText, DesktopEnvironment environment, IShell shell)
{
    public ProjectService Projects { get; } = projects;
    public AgentService Agents { get; } = agents;
    public LocaleContext Locale { get; } = locale;
    public AppearanceContext Appearance { get; } = appearance;
    public LocalizationService Text { get; } = text;
    public LocalizedControls Localized { get; } = localized;
    public DesktopEnvironment Environment { get; } = environment;
    public IShell Shell { get; } = shell;
    public string ConfigurationText() => configurationText?.Invoke() ?? configuration;
    public Task OpenProjectAsync(Guid id, int tab = 0) => Shell.OpenProjectAsync(id, tab);
    public Task ActAsync(Button button, Func<Task> action) => Shell.ActAsync(button, action);
    public void ShowError(string key) => Shell.ShowError(key);

    public void SetLanguage(string code)
    {
        try { Locale.SetLanguage(code); }
        catch (Exception) { ShowError("validation.languageSaveFailed"); }
    }

    public void SetTheme(string theme)
    {
        try { Appearance.SetTheme(theme); }
        catch (Exception) { ShowError("validation.themeSaveFailed"); }
    }

    public string EnumText<T>(T value) where T : struct, Enum => new DomainDisplay(Text).Enum(value);
    public string Due(DateTimeOffset? date) => date is null ? Text.Get("date.none") : new LocaleDateFormatter(Locale).Display(date);
    public string ShortDate(DateTimeOffset? date) => date is null ? Text.Get("date.none") : date.Value.ToLocalTime().ToString("MMM d", Locale.Culture);
    public string Number(int value) => value.ToString("N0", Locale.Culture);

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
