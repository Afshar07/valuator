using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Base for screen views: thin, localization-aware helpers over the shared presentation context.</summary>
internal abstract class PresentationView : StackPanel
{
    protected readonly PresentationContext Context;
    protected ProjectService _projects => Context.Projects;
    protected Core.Agents.AgentService _agents => Context.Agents;
    protected LocalizationService _text => Context.Text;
    protected LocaleContext _locale => Context.Locale;
    protected string _configuration => Context.Configuration;
    protected Func<string>? _configurationText => Context.ConfigurationText;
    protected PresentationView(PresentationContext context) { Context = context; Spacing = PresentationTheme.Spacing[3]; }
    protected Task OpenProjectAsync(Guid id, int tab = 0) => Context.OpenProjectAsync(id, tab);
    protected Task SaveAsync(Project project) => _projects.SaveAsync(project);
    protected string T(string key) => _text.Get(key);
    protected string F(string key, params object?[] values) => _text.Format(key, values);
    protected void Bind<TControl>(TControl control, Action<TControl> update) where TControl : Control => Context.Localized.Bind(control, update);
    protected void ShowError(string message) => Context.ShowError(message);
    protected static StackPanel Column() => new() { Spacing = 12 };
    protected TextBlock Label(string text, string style = "Body", string color = "TextPrimary") => Context.Label(text, style, color);
    protected TextBlock Label(Func<string> text, string style = "Body", string color = "TextPrimary") => Context.Label(text, style, color);
    protected TextBlock Heading(string text, string style = "HeadingSmall") => Context.Heading(text, style);
    protected TextBlock Heading(Func<string> text, string style = "HeadingSmall") => Context.Heading(text, style);
    protected Button Action(string text, Func<Task> action, string? appearance = null) => Context.Action(text, action, appearance);
    protected Button Action(Func<string> text, Func<Task> action, string? appearance = null) => Context.Action(text, action, appearance);
    protected static TextBox Input(string value = "", bool multiline = false) => new()
    {
        Text = value,
        AcceptsReturn = multiline,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Start,
        MinHeight = multiline ? 80 : 36,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    protected static TextBox Readable(string value) => new()
    {
        Text = value,
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Start,
        MinHeight = 70,
        MaxHeight = 300
    };
    protected ComboBox Choice<T>(T selected) where T : struct, Enum => new()
    {
        ItemsSource = Enum.GetValues<T>(),
        SelectedItem = selected,
        MinWidth = 200,
        ItemTemplate = new FuncDataTemplate<T>((value, _) => Label(() => EnumText(value), "BodySmall"))
    };
    protected string EnumText<T>(T value) where T : struct, Enum => Context.EnumText(value);
    protected string GroupTitle(string id, string original) => new DomainDisplay(_text).Group(id, original);
    protected string RequirementTitle(ProjectRequirement requirement) => new DomainDisplay(_text).Requirement(requirement);
    protected void Field(StackPanel panel, string label, Control input)
    {
        panel.Children.Add(Label(label, "Label", "TextSecondary")); panel.Children.Add(input);
    }
    protected string Due(DateTimeOffset? date) => Context.Due(date);
    protected TextBox DateInput(DateTimeOffset? date)
    {
        var input = Input(new LocaleDateFormatter(_locale).Edit(date));
        input.FlowDirection = FlowDirection.LeftToRight;
        return input;
    }
    protected bool TryDate(TextBox input, out DateTimeOffset? due)
    {
        try { due = MainWindow.ParseLocalDate(input.Text); return true; }
        catch (FormatException) { due = null; ShowError("validation.localDate"); return false; }
    }
}
