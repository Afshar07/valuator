using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Full-window first-run surface: brand and display options on top, a centered column below.</summary>
internal abstract class OnboardingScreen : Border
{
    protected readonly PresentationContext Context;
    protected readonly StackPanel Column = new();

    protected OnboardingScreen(PresentationContext context, string name, double maxWidth)
    {
        Context = context; Name = name;
        this.Paint(BackgroundProperty, "BackgroundApp");
        var frame = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(20, 16) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(Ui.Tile(Icons.Glyph(Icons.Scales, 15, "OnAccent", IconWeight.Bold), 28, 8, "Accent"));
        var title = context.Label("app.title", "BodyStrong"); title.VerticalAlignment = VerticalAlignment.Center; brand.Children.Add(title);
        top.Children.Add(brand);
        var theme = new Segmented(context, AppearanceContext.Themes.Select(id => (id, (Func<string>)(() => context.Text.Get("theme." + id)))), () => context.Appearance.Theme, id => context.SetTheme(id), optionWidth: 56, name: "OnboardingTheme");
        theme.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(theme, 1); top.Children.Add(theme);
        var language = new Button { Name = "OnboardingLanguage", Height = 32, MinHeight = 32, Padding = new Thickness(10, 0), CornerRadius = new CornerRadius(9) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(Icons.Glyph(Icons.Translate, 14, "TextPrimary"));
        var other = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        context.Localized.Bind(other, control =>
        {
            var persian = context.Locale.LanguageCode == "fa";
            control.Text = persian ? "English" : "فارسی";
            control.FontFamily = persian ? PresentationTheme.LatinFontFamily : PresentationTheme.PersianFontFamily;
        });
        row.Children.Add(other); language.Content = row;
        context.Localized.Bind(language, control => Avalonia.Automation.AutomationProperties.SetName(control, context.Text.Get("settings.language")));
        language.Click += (_, _) => context.SetLanguage(context.Locale.LanguageCode == "fa" ? "en" : "fa");
        Grid.SetColumn(language, 2); top.Children.Add(language);
        frame.Children.Add(top);

        Column.MaxWidth = maxWidth; Column.HorizontalAlignment = HorizontalAlignment.Center; Column.VerticalAlignment = VerticalAlignment.Center;
        var body = new Border { Padding = new Thickness(24, 24, 24, 64), Child = Column };
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); frame.Children.Add(scroll);
        Child = frame;
    }

    protected TextBlock Label(string key, string style = "Body", string color = "TextPrimary") => Context.Label(key, style, color);
}

/// <summary>First launch with an empty database: start a real project or look around a sample workspace.</summary>
internal sealed class WelcomeScreen : OnboardingScreen
{
    public WelcomeScreen(PresentationContext context) : base(context, "WelcomeScreen", 660)
    {
        Column.Spacing = 28;
        var heading = new StackPanel { Spacing = 10 };
        var title = Label("v3.welcomeTitle", "Title"); title.FontSize = 32; title.LineHeight = 38; title.LetterSpacing = -0.64; heading.Children.Add(title);
        var sub = Label("v3.welcomeSub", "Body", "TextSecondary"); sub.FontSize = 14.5; sub.MaxWidth = 540; sub.HorizontalAlignment = HorizontalAlignment.Left; heading.Children.Add(sub);
        Column.Children.Add(heading);

        var cards = new AdaptiveGrid { MinItemWidth = 260, Gap = 12, StretchRows = true };
        cards.Children.Add(Card("StartOwnProject", Icons.Plus, IconWeight.Bold, "v3.startOwn", "v3.startOwnSub", primary: true, () => { context.Shell.ShowWizard(fromWelcome: true); return Task.CompletedTask; }));
        cards.Children.Add(Card("StartSample", Icons.Flask, IconWeight.Regular, "v3.startSample", "v3.startSampleSub", primary: false, () => context.Shell.StartSampleAsync()));
        Column.Children.Add(cards);

        var note = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        note.Children.Add(Icons.Glyph(Icons.HardDrives, 14, "TextTertiary"));
        var text = Label("v3.noAccount", "Caption", "TextTertiary"); text.VerticalAlignment = VerticalAlignment.Center; note.Children.Add(text);
        Column.Children.Add(note);
    }

    private Button Card(string name, string icon, IconWeight weight, string titleKey, string subKey, bool primary, Func<Task> action)
    {
        var stack = new StackPanel { Spacing = 8 };
        var tile = Ui.Tile(Icons.Glyph(icon, 18, primary ? "OnAccent" : "TextSecondary", weight), 36, 10, primary ? "Accent" : "BackgroundTrack"); tile.HorizontalAlignment = HorizontalAlignment.Left;
        stack.Children.Add(tile);
        var title = Label(titleKey, "PanelTitle"); title.Margin = new Thickness(0, 6, 0, 0); stack.Children.Add(title);
        stack.Children.Add(Label(subKey, "Small", "TextSecondary"));
        var button = new Button { Name = name, Content = stack }; button.Classes.Add("welcome"); if (primary) button.Classes.Add("welcomePrimary");
        Context.Localized.Bind(button, control => Avalonia.Automation.AutomationProperties.SetName(control, Context.Text.Get(titleKey)));
        button.Click += async (_, _) => await Context.ActAsync(button, action);
        return button;
    }
}

/// <summary>Three-step new-project wizard: name, checklist template, first item.</summary>
internal sealed class WizardScreen : OnboardingScreen
{
    private readonly bool _fromWelcome;
    private int _step;
    private string _name = "", _company = "", _requirement = "";
    private int _template;
    private string? _deck;
    private readonly Grid _steps = new() { ColumnDefinitions = new ColumnDefinitions("*,*,Auto"), ColumnSpacing = 10 };
    private readonly Border _card = new() { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusDialog), Padding = new Thickness(24) };
    private readonly Grid _actions = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };

    public WizardScreen(PresentationContext context, bool fromWelcome) : base(context, "WizardScreen", 560)
    {
        _fromWelcome = fromWelcome; Column.Spacing = 24;
        _card.Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault").CardShadowed();
        Column.Children.Add(_steps); Column.Children.Add(_card); Column.Children.Add(_actions);
        Render();
    }

    private string T(string key) => Context.Text.Get(key);

    private void Render()
    {
        RenderSteps(); RenderCard(); RenderActions();
    }

    private void RenderSteps()
    {
        _steps.Children.Clear();
        for (var index = 0; index < 3; index++)
        {
            var current = index; var reached = index <= _step; var done = index < _step;
            var item = new Grid { ColumnDefinitions = new ColumnDefinitions(index < 2 ? "Auto,Auto,*" : "Auto,Auto"), ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var number = reached
                ? (done ? (Control)Icons.Glyph(Icons.Check, 11, "OnAccent", IconWeight.Bold) : Context.Label(() => Context.Number(current + 1), "MetaMedium", "OnAccent"))
                : Context.Label(() => Context.Number(current + 1), "MetaMedium", "TextSecondary");
            if (number is TextBlock block) { block.FontWeight = FontWeight.SemiBold; block.HorizontalAlignment = HorizontalAlignment.Center; block.VerticalAlignment = VerticalAlignment.Center; }
            item.Children.Add(new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Child = number, VerticalAlignment = VerticalAlignment.Center }.Paint(Border.BackgroundProperty, reached ? "Accent" : "BackgroundTrack"));
            var label = Context.Label("v3.wStep" + index, "Small", reached ? "TextPrimary" : "TextTertiary"); label.TextWrapping = TextWrapping.NoWrap; label.VerticalAlignment = VerticalAlignment.Center;
            label.FontWeight = index == _step ? FontWeight.SemiBold : FontWeight.Medium; Grid.SetColumn(label, 1); item.Children.Add(label);
            if (index < 2) { var line = new Border { Height = 1, MinWidth = 16, VerticalAlignment = VerticalAlignment.Center }.Paint(Border.BackgroundProperty, "BorderDefault"); Grid.SetColumn(line, 2); item.Children.Add(line); }
            Grid.SetColumn(item, index); _steps.Children.Add(item);
        }
    }

    private Control Heading(string titleKey, Func<string> sub)
    {
        var heading = new StackPanel { Spacing = 4 };
        var title = Context.Label(titleKey, "Dialog"); title.FontSize = 20; heading.Children.Add(title);
        heading.Children.Add(Context.Label(sub, "Body", "TextSecondary"));
        return heading;
    }

    private static TextBox Field(string value, Action<string> changed, string name)
    {
        var box = Forms.Input(value, 40); box.FontSize = 14; box.Name = name; box.Padding = new Thickness(12, 0); box.CornerRadius = new CornerRadius(9);
        box.TextChanged += (_, _) => changed(box.Text ?? "");
        return box;
    }

    private void RenderCard()
    {
        var content = new StackPanel { Spacing = 16 };
        switch (_step)
        {
            case 0:
                content.Children.Add(Heading("v3.w1Title", () => T("v3.w1Sub")));
                var name = Field(_name, value => { _name = value; RenderActions(); }, "WizardName");
                Context.Localized.Bind(name, box => box.Watermark = T("v3.namePh"));
                var company = Field(_company, value => _company = value, "WizardCompany");
                Context.Localized.Bind(company, box => box.Watermark = T("v3.coPh"));
                content.Children.Add(Forms.Field(Context, "v3.projNameL", name));
                content.Children.Add(Forms.Field(Context, "v3.company", company));
                name.AttachedToVisualTree += (_, _) => name.Focus();
                break;
            case 1:
                content.Children.Add(Heading("v3.w2Title", () => T("v3.w2Sub")));
                var options = new StackPanel { Spacing = 8 };
                options.Children.Add(Template(0, "v3.tplVc", "v3.tplVcSub"));
                options.Children.Add(Template(1, "v3.tplBlank", "v3.tplBlankSub"));
                content.Children.Add(options);
                if (_template == 0) content.Children.Add(Preview());
                break;
            default:
                content.Children.Add(Heading("v3.w3Title", () => T(_template == 0 ? "v3.w3Sub" : "v3.w3BlankSub")));
                if (_template == 0)
                {
                    content.Children.Add(DeckRow());
                    content.Children.Add(Context.Label("v3.fileNote", "Caption", "TextTertiary"));
                }
                else
                {
                    var first = Field(_requirement, value => { _requirement = value; RenderActions(); }, "WizardRequirement");
                    Context.Localized.Bind(first, box => box.Watermark = T("v3.reqPh"));
                    content.Children.Add(first);
                }
                break;
        }
        _card.Child = content;
    }

    private Button Template(int index, string titleKey, string subKey)
    {
        var selected = _template == index;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        row.Children.Add(Icons.Glyph(selected ? Icons.RadioButton : Icons.Circle, 18, selected ? "Accent" : "TextTertiary", selected ? IconWeight.Fill : IconWeight.Regular));
        var labels = new StackPanel(); labels.Children.Add(Context.Label(titleKey, "BodyStrong")); labels.Children.Add(Context.Label(subKey, "Caption", "TextSecondary"));
        Grid.SetColumn(labels, 1); row.Children.Add(labels);
        var button = new Button { Content = row, Name = index == 0 ? "TemplateVc" : "TemplateBlank", Padding = new Thickness(14, 12) };
        button.Classes.Add("option"); button.Classes.Add("committed"); button.Classes.Set("selected", selected);
        Context.Localized.Bind(button, control => Avalonia.Automation.AutomationProperties.SetName(control, T(titleKey)));
        button.Click += (_, _) => { _template = index; Render(); };
        return button;
    }

    private Control Preview()
    {
        var stack = new StackPanel();
        var first = true;
        foreach (var group in VcTemplate.Create().Groups)
        {
            var group_ = group;
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
            var names = new StackPanel();
            names.Children.Add(Context.Label(() => new DomainDisplay(Context.Text).Group(group_), "BodyMedium"));
            var list = Context.Label(() =>
            {
                var display = new DomainDisplay(Context.Text);
                return string.Join(" · ", group_.Requirements.Take(3).Select(item => display.Requirement(item.Id, item.Title))) + (group_.Requirements.Count > 3 ? " …" : "");
            }, "Caption", "TextTertiary");
            list.TextWrapping = TextWrapping.NoWrap; list.TextTrimming = TextTrimming.CharacterEllipsis; names.Children.Add(list);
            line.Children.Add(names);
            var count = Context.Label(() => $"{Context.Number(group_.Requirements.Count)} {T("v3.items")}", "Caption", "TextSecondary"); count.TextWrapping = TextWrapping.NoWrap; count.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(count, 1); line.Children.Add(count);
            stack.Children.Add(Ui.Separated(new Border { Padding = new Thickness(14, 9), Child = line }, first)); first = false;
        }
        return new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), ClipToBounds = true, Child = stack }.Paint(Border.BorderBrushProperty, "BorderDefault");
    }

    private Control DeckRow()
    {
        var linked = _deck is not null;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        row.Children.Add(Icons.Glyph(linked ? Icons.CheckCircle : Icons.FileDashed, 20, linked ? "Success" : "TextTertiary", linked ? IconWeight.Fill : IconWeight.Regular));
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(Context.Label(() => new DomainDisplay(Context.Text).Requirement("pitch-deck", "Pitch deck"), "BodyStrong"));
        var subtitle = linked ? Context.Label(() => Path.GetFileName(_deck!), "Caption", "TextSecondary") : Context.Label(() => Context.EnumText(RequirementType.Document), "Caption", "TextSecondary");
        subtitle.FlowDirection = linked ? FlowDirection.LeftToRight : subtitle.FlowDirection; subtitle.HorizontalAlignment = HorizontalAlignment.Left;
        labels.Children.Add(subtitle);
        Grid.SetColumn(labels, 1); row.Children.Add(labels);
        Control trailing;
        if (linked) trailing = Ui.Pill(Context, () => T("v3.linked"), Tone.Success);
        else
        {
            var pick = Context.IconAction("v3.chooseFile", Icons.FolderOpen, async () =>
            {
                var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = T("file.pickerTitle"), AllowMultiple = false });
                if (files.Count == 0) return;
                var path = files[0].TryGetLocalPath();
                if (path is null) { Context.ShowError("validation.localFilesOnly"); return; }
                _deck = path; Render();
            });
            pick.Name = "WizardChooseFile"; pick.MinHeight = 32; trailing = pick;
        }
        trailing.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(trailing, 2); row.Children.Add(trailing);
        return new Border { Padding = new Thickness(14), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), Child = row }
            .Paint(Border.BackgroundProperty, linked ? "SuccessSoft" : "BackgroundMuted").Paint(Border.BorderBrushProperty, linked ? "Success" : "BorderDefault");
    }

    private void RenderActions()
    {
        _actions.Children.Clear();
        var persian = Context.Locale.LanguageCode == "fa";
        var back = Context.IconAction("v3.back2", persian ? Icons.ArrowRight : Icons.ArrowLeft, () =>
        {
            if (_step > 0) { _step--; Render(); }
            else if (_fromWelcome) Context.Shell.ShowWelcome();
            else Context.Shell.CloseOverlay();
            return Task.CompletedTask;
        }, "ghost");
        back.Name = "WizardBack"; back.MinHeight = 36; _actions.Children.Add(back);

        var canContinue = _step != 0 || _name.Trim().Length > 0;
        var chosen = _template == 0 ? _deck is not null : _requirement.Trim().Length > 0;
        if (_step == 2 && !chosen)
        {
            var skip = Context.Action("v3.skip", FinishAsync); skip.Name = "WizardSkip"; skip.MinHeight = 36; skip.Padding = new Thickness(14, 0);
            Grid.SetColumn(skip, 2); _actions.Children.Add(skip);
        }
        var primary = Context.Action(_step < 2 ? "v3.next" : "v3.openProject", () =>
        {
            if (_step < 2) { _step++; Render(); return Task.CompletedTask; }
            return FinishAsync();
        }, "primary");
        primary.Name = "WizardPrimary"; primary.MinHeight = 36; primary.Padding = new Thickness(18, 0); primary.IsEnabled = canContinue;
        Grid.SetColumn(primary, 3); _actions.Children.Add(primary);
    }

    private async Task FinishAsync()
    {
        // Starting a real project from the sample workspace leaves the sample (and its throw-away data) behind.
        if (Context.Shell.IsSample) await Context.Shell.ExitSampleAsync();
        var name = _name.Trim().Length > 0 ? _name.Trim() : T("v3.defaultName");
        Project project;
        if (_template == 0)
        {
            project = await Context.Projects.CreateAsync(name, _company, ProjectStatus.Active, "", "");
            if (_deck is not null && project.Requirements.FirstOrDefault(item => item.DefinitionId == "pitch-deck") is { } deck)
            {
                deck.Files.Add(new ProjectFile { FileName = Path.GetFileName(_deck), Path = _deck, SizeBytes = File.Exists(_deck) ? new FileInfo(_deck).Length : 0 });
                deck.Status = RequirementStatus.Provided;
                await Context.Projects.SaveAsync(project);
            }
        }
        else
            project = await Context.Projects.CreateBlankAsync(name, _company, ProjectStatus.Active, "", "",
                _requirement.Trim().Length > 0 ? _requirement : T("v3.defaultRequirement"));
        Context.Shell.CloseOverlay();
        await Context.Shell.OpenCreatedProjectAsync(project.Id);
    }
}
