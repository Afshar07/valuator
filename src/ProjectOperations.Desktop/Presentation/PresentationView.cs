using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop;

/// <summary>Base for screen views: thin, localization-aware helpers over the shared presentation context.</summary>
internal abstract class PresentationView : StackPanel
{
    protected readonly PresentationContext Context;
    protected ProjectService _projects => Context.Projects;
    protected Core.Agents.AgentService _agents => Context.Agents;
    protected LocalizationService _text => Context.Text;
    protected LocaleContext _locale => Context.Locale;
    protected PresentationView(PresentationContext context) { Context = context; Spacing = 20; }
    protected Task OpenProjectAsync(Guid id, ProjectTab tab = ProjectTab.Overview) => Context.OpenProjectAsync(id, tab);
    protected Task SaveAsync(Project project) => _projects.SaveAsync(project);
    protected string T(string key) => _text.Get(key);
    protected string F(string key, params object?[] values) => _text.Format(key, values);
    protected string N(int value) => Context.Number(value);
    protected void Bind<TControl>(TControl control, Action<TControl> update) where TControl : Control => Context.Localized.Bind(control, update);
    protected void ShowError(string message) => Context.ShowError(message);
    protected static StackPanel Column(double spacing = 12) => new() { Spacing = spacing };
    protected TextBlock Label(string text, string style = "Body", string color = "TextPrimary") => Context.Label(text, style, color);
    protected TextBlock Label(Func<string> text, string style = "Body", string color = "TextPrimary") => Context.Label(text, style, color);
    protected TextBlock Heading(string text, string style = "Heading") => Context.Heading(text, style);
    protected TextBlock Heading(Func<string> text, string style = "Heading") => Context.Heading(text, style);
    protected Button Action(string text, Func<Task> action, string? appearance = null) => Context.Action(text, action, appearance);
    protected Button Action(Func<string> text, Func<Task> action, string? appearance = null) => Context.Action(text, action, appearance);

    /// <summary>Page title row: heading with optional subtitle and trailing controls.</summary>
    protected Control PageHeader(string titleKey, string? subtitleKey = null, params Control[] trailing)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Bottom };
        titles.Children.Add(Label(titleKey, "Title"));
        if (subtitleKey is not null) titles.Children.Add(Label(subtitleKey, "Body", "TextSecondary"));
        grid.Children.Add(titles);
        if (trailing.Length > 0)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = subtitleKey is null ? VerticalAlignment.Center : VerticalAlignment.Bottom };
            foreach (var control in trailing) { control.VerticalAlignment = VerticalAlignment.Center; actions.Children.Add(control); }
            Grid.SetColumn(actions, 1); grid.Children.Add(actions);
        }
        return grid;
    }

    protected Button AssistantButton()
    {
        var button = Context.IconAction("agent.assistant", Icons.Sparkle, () => { Context.Shell.ToggleAssistant(); return Task.CompletedTask; }, "ai");
        button.Name = "AssistantToggle"; button.MinHeight = 34; return button;
    }

    protected static TextBox Input(string value = "", bool multiline = false) => new()
    {
        Text = value,
        AcceptsReturn = multiline,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Start,
        MinHeight = multiline ? 76 : 34,
        VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    protected static TextBox Readable(string value) => new()
    {
        Text = value,
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Start,
        MinHeight = 64,
        MaxHeight = 300,
        FontSize = 12.5,
        VerticalContentAlignment = VerticalAlignment.Top
    };
    protected ComboBox Choice<T>(T selected) where T : struct, Enum => new()
    {
        ItemsSource = Enum.GetValues<T>(),
        SelectedItem = selected,
        MinWidth = 200,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        ItemTemplate = new FuncDataTemplate<T>((value, _) => Label(() => EnumText(value), "Body"))
    };
    protected string EnumText<T>(T value) where T : struct, Enum => Context.EnumText(value);
    protected string GroupTitle(string id, string original) => new DomainDisplay(_text).Group(id, original);
    protected string RequirementTitle(ProjectRequirement requirement) => new DomainDisplay(_text).Requirement(requirement);
    /// <summary>Label + input pair. The label is directly followed by its input in one stack (an accessibility and test contract).</summary>
    protected StackPanel Field(StackPanel panel, string label, Control input)
    {
        panel.Children.Add(Label(label, "Caption", "TextSecondary")); panel.Children.Add(input); return panel;
    }
    protected StackPanel FieldGroup(string label, Control input)
    {
        var group = new StackPanel { Spacing = 5 }; return Field(group, label, input);
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
        try { due = LocalDateInput.Parse(input.Text); return true; }
        catch (FormatException) { due = null; ShowError("validation.localDate"); return false; }
    }
}
