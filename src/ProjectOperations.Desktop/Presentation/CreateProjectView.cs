using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>New-project dialog. Projects start from the built-in VC Investment Review template; a blank template is not built yet.</summary>
internal sealed class CreateProjectView : Border
{
    public CreateProjectView(PresentationContext context)
    {
        Name = "CreateProjectDialog"; Width = 480; MaxWidth = 480; Margin = new Thickness(16);
        BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(PresentationTheme.RadiusDialog);
        this.Paint(BackgroundProperty, "BackgroundCard").Paint(BorderBrushProperty, "BorderDefault").RaisedShadowed();
        var form = new Form(context);
        Child = form;
    }

    private sealed class Form : PresentationView
    {
        public Form(PresentationContext context) : base(context)
        {
            Spacing = 0;
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(18, 16, 18, 6) };
            header.Children.Add(Label("project.create.title", "Dialog"));
            var close = Context.IconAction("action.close", Icons.X, () => { Context.Shell.CloseModal(); return Task.CompletedTask; }, "icon", iconOnly: true);
            Grid.SetColumn(close, 1); header.Children.Add(close); Children.Add(header);

            var body = new StackPanel { Spacing = 12, Margin = new Thickness(18, 10, 18, 16) };
            var name = Input(); var company = Input(); var owner = Input();
            var stage = Choice(ProjectStage.Screening);
            body.Children.Add(FieldGroup("project.name", name));
            var pair = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
            pair.Children.Add(FieldGroup("project.company", company));
            var ownerGroup = FieldGroup("project.owner", owner); Grid.SetColumn(ownerGroup, 1); pair.Children.Add(ownerGroup);
            body.Children.Add(pair);
            body.Children.Add(FieldGroup("project.stage", stage));
            var templates = new StackPanel { Spacing = 6 };
            templates.Children.Add(Label("project.template", "Caption", "TextSecondary"));
            templates.Children.Add(Template(() => new DomainDisplay(_text).Template(VcTemplate.Create()), () => T("template.vcSummary"), selected: true));
            templates.Children.Add(Template(() => T("template.blank"), () => T("template.blankUnavailable"), selected: false));
            body.Children.Add(templates);
            var error = Label("validation.projectNameRequired", "Small", "Error"); error.IsVisible = false; error.Name = "CreateProjectError";
            body.Children.Add(error);
            name.TextChanged += (_, _) => error.IsVisible = false;
            Children.Add(body);

            var footer = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(18, 12) }.Paint(Border.BorderBrushProperty, "BorderSubtle");
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(Action("action.cancel", () => { Context.Shell.CloseModal(); return Task.CompletedTask; }));
            actions.Children.Add(Action("project.create", async () =>
            {
                if (string.IsNullOrWhiteSpace(name.Text)) { error.IsVisible = true; return; }
                var project = await _projects.CreateAsync(name.Text, company.Text ?? "", (ProjectStage)stage.SelectedItem!, ProjectStatus.Active, owner.Text ?? "", "");
                Context.Shell.CloseModal();
                await OpenProjectAsync(project.Id);
            }, "primary"));
            footer.Child = actions; Children.Add(footer);
        }

        /// <summary>Template option card. Only the built-in template can be chosen; the blank option is shown disabled until custom requirements exist.</summary>
        private Button Template(Func<string> title, Func<string> subtitle, bool selected)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            row.Children.Add(Icons.Glyph(selected ? Icons.RadioButton : Icons.Circle, 17, selected ? "Accent" : "TextTertiary", selected ? IconWeight.Fill : IconWeight.Regular));
            var labels = new StackPanel();
            labels.Children.Add(Label(title, "BodyMedium")); labels.Children.Add(Label(subtitle, "Caption", "TextSecondary"));
            Grid.SetColumn(labels, 1); row.Children.Add(labels);
            var button = new Button { Content = row, IsEnabled = selected, Padding = new Thickness(12, 10) };
            button.Classes.Add("option"); button.Classes.Add("committed"); button.Classes.Set("selected", selected);
            Bind(button, control => Avalonia.Automation.AutomationProperties.SetName(control, title()));
            return button;
        }
    }
}
