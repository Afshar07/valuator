using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>
/// Settings: appearance and language are editable here. Agent runtime settings come from environment variables and the
/// database location from the data directory, so they are shown read-only. Backup and encryption are not built yet.
/// </summary>
internal sealed class SettingsView : PresentationView
{
    public SettingsView(PresentationContext context) : base(context)
    {
        Children.Add(PageHeader("presentation.navigationSettings"));
        var sections = new StackPanel { Spacing = 16, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };

        var appearance = Section("settings.appearance");
        appearance.Children.Add(Row(Label("settings.theme"), new Segmented(Context, AppearanceContext.Themes.Select(id => (id, (Func<string>)(() => T("theme." + id)))),
            () => Context.Appearance.Theme, id => Context.SetTheme(id), optionWidth: 72, name: "SettingsTheme")));
        var language = new StackPanel();
        language.Children.Add(Label("settings.language")); language.Children.Add(Label("settings.languageNote", "Caption", "TextSecondary"));
        var languages = new Segmented(Context, new (string, Func<string>)[] { ("en", () => "English"), ("fa", () => "فارسی") }, () => _locale.LanguageCode, code => Context.SetLanguage(code), optionWidth: 72, name: "SettingsLanguage");
        appearance.Children.Add(Row(language, languages));
        sections.Children.Add(Card(appearance));

        var agent = Section("settings.agent", AssistantPanel.StatusLine(Context, () => Context.Environment.AgentConfigured ? "agent.state.configured" : "agent.state.off",
            () => Context.Environment.AgentConfigured ? "Accent" : "TextTertiary"));
        agent.Children.Add(new Border { Padding = new Thickness(0, 0, 0, 10), Child = Label("settings.agentNote", "Small", "TextSecondary") });
        var fields = new AdaptiveGrid { MinItemWidth = 240, Gap = 12 };
        fields.Children.Add(ReadOnlyField("settings.endpoint", "PROJECTOPS_OPENCODE_URL", Context.Environment.Endpoint));
        fields.Children.Add(ReadOnlyField("settings.configDirectory", "PROJECTOPS_OPENCODE_CONFIG_DIR", Context.Environment.ConfigDirectory));
        agent.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 12, 0, 0), Child = fields }));
        var sent = new TextBlock { TextWrapping = TextWrapping.Wrap }; PresentationTheme.Typeset(sent, "Small");
        Bind(sent, control => control.Inlines = [new Avalonia.Controls.Documents.Run(T("settings.sentTitle") + ". ") { FontWeight = FontWeight.SemiBold },
            new Avalonia.Controls.Documents.Run(T("settings.sentBody")).Paint(Avalonia.Controls.Documents.TextElement.ForegroundProperty, "TextSecondary")]);
        agent.Children.Add(new Border { Margin = new Thickness(0, 12, 0, 0), Child = Ui.Banner(sent, Icons.Info, "TextSecondary", "BackgroundMuted") });
        agent.Children.Add(new Border { Margin = new Thickness(0, 10, 0, 0), Child = Label("settings.envNote", "Caption", "TextTertiary") });
        sections.Children.Add(Card(agent, bottom: 14));

        var data = Section("settings.data");
        var database = new StackPanel();
        database.Children.Add(Label(() => T("settings.database") + " · SQLite"));
        var path = Label(() => Context.Environment.DatabasePath ?? T("date.notSet"), "Caption", "TextTertiary"); path.FlowDirection = FlowDirection.LeftToRight; path.HorizontalAlignment = HorizontalAlignment.Left;
        path.Name = "DatabasePath"; database.Children.Add(path);
        var folder = Action("settings.openFolder", async () =>
        {
            var directory = Path.GetDirectoryName(Context.Environment.DatabasePath ?? "");
            if (string.IsNullOrEmpty(directory) || !await TopLevel.GetTopLevel(this)!.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(directory))) ShowError("documents.openFailed");
        });
        folder.IsEnabled = Context.Environment.DatabasePath is not null;
        data.Children.Add(Row(database, folder));
        foreach (var (icon, key) in new[] { (Icons.CloudArrowUp, "settings.backup"), (Icons.LockKey, "settings.encryption") })
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            line.Children.Add(Icons.Glyph(icon, 16, "TextTertiary")); line.Children.Add(Label(key, "Body", "TextTertiary"));
            data.Children.Add(Row(line, Label("settings.notBuilt", "Meta", "TextTertiary")));
        }
        sections.Children.Add(Card(data));
        Children.Add(sections);
    }

    private StackPanel Section(string titleKey, Control? trailing = null)
    {
        var panel = new StackPanel();
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 12, 0, 4) };
        header.Children.Add(Label(titleKey, "Heading"));
        if (trailing is not null) { Grid.SetColumn(trailing, 1); header.Children.Add(trailing); }
        panel.Children.Add(header);
        return panel;
    }

    private static Border Card(Control content, double bottom = 4) => Ui.Card(content, new Thickness(16, 4, 16, bottom));

    private static Control Row(Control start, Control end)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        start.VerticalAlignment = VerticalAlignment.Center; end.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(start); Grid.SetColumn(end, 1); grid.Children.Add(end);
        return Ui.Separated(new Border { Padding = new Thickness(0, 12), Child = grid });
    }

    private Control ReadOnlyField(string labelKey, string variable, string? value)
    {
        var group = new StackPanel { Spacing = 5 };
        group.Children.Add(Label(() => $"{T(labelKey)} · ⁦{variable}⁩", "Caption", "TextSecondary"));
        var box = new TextBox { Text = string.IsNullOrWhiteSpace(value) ? "" : value, IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, Name = "Setting_" + variable };
        Bind(box, control => control.Watermark = T("settings.notSetEnv"));
        group.Children.Add(box);
        return group;
    }
}
