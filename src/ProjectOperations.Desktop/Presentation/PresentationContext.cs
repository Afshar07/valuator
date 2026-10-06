using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Shared services and localized control factories handed to views and components by the shell.</summary>
internal sealed class PresentationContext(
    ProjectService projects, AgentService agents, LocaleContext locale, LocalizationService text, LocalizedControls localized,
    string configuration, Func<string>? configurationText, Func<Guid, int, Task> openProjectAsync,
    Func<Button, Func<Task>, Task> actAsync, Action<string> showError,
    Func<Control, Control, Control, Button, Func<CancellationToken, Task>, Func<Task>, Task> runAsync, Action cancelRun)
{
    public ProjectService Projects { get; } = projects;
    public AgentService Agents { get; } = agents;
    public LocaleContext Locale { get; } = locale;
    public LocalizationService Text { get; } = text;
    public LocalizedControls Localized { get; } = localized;
    public string Configuration { get; } = configuration;
    public Func<string>? ConfigurationText { get; } = configurationText;
    public Func<Guid, int, Task> OpenProjectAsync { get; } = openProjectAsync;
    public Func<Button, Func<Task>, Task> ActAsync { get; } = actAsync;
    public Action<string> ShowError { get; } = showError;
    public Func<Control, Control, Control, Button, Func<CancellationToken, Task>, Func<Task>, Task> RunAsync { get; } = runAsync;
    public Action CancelRun { get; } = cancelRun;
    public string EnumText<T>(T value) where T : struct, Enum => new DomainDisplay(Text).Enum(value);
    public string Due(DateTimeOffset? date) => date is null ? Text.Get("date.none") : new LocaleDateFormatter(Locale).Display(date);
    public string ShortDate(DateTimeOffset? date) => date is null ? Text.Get("date.none") : date.Value.ToLocalTime().ToString("MMM d", Locale.Culture);

    public TextBlock Label(string key, string style = "Body", string color = "TextPrimary") => Label(() => Text.Get(key), style, color);
    public TextBlock Label(Func<string> text, string style = "Body", string color = "TextPrimary")
    {
        var label = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Start };
        PresentationTheme.Typeset(label, style, color);
        Localized.Bind(label, control => control.Text = text()); return label;
    }
    public TextBlock Heading(string key, string style = "HeadingMedium") => Heading(() => Text.Get(key), style);
    public TextBlock Heading(Func<string> text, string style = "HeadingMedium") => Label(text, style);
    public Button Action(string key, Func<Task> action, string? appearance = null) => Action(() => Text.Get(key), action, appearance);
    public Button Action(Func<string> text, Func<Task> action, string? appearance = null)
    {
        var button = new Button { HorizontalAlignment = HorizontalAlignment.Left };
        if (appearance is not null) button.Classes.Add(appearance);
        Localized.Bind(button, control => control.Content = text());
        button.Click += async (_, _) => await ActAsync(button, action); return button;
    }
}
