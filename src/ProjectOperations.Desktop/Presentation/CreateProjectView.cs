using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Templates;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

internal sealed class CreateProjectView : PresentationView
{
    public CreateProjectView(PresentationContext context) : base(context) => Build();
    private void Build()
    {
        var card = new SectionCard(Context, "project.create") { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Stretch };
        var panel = card.Body;
        var name = Input(); var company = Input(); var owner = Input(); var notes = Input("", true);
        var stage = Choice(ProjectStage.Screening); var status = Choice(ProjectStatus.Active);
        Field(panel, "project.name", name); Field(panel, "project.company", company);
        Field(panel, "project.stage", stage); Field(panel, "field.status", status); Field(panel, "project.owner", owner); Field(panel, "field.notes", notes);
        var template = new ComboBox { MinWidth = 250 };
        Bind(template, control => { control.ItemsSource = new[] { new DomainDisplay(_text).Template(VcTemplate.Create()) }; control.SelectedIndex = 0; });
        Field(panel, "project.template", template);
        panel.Children.Add(Action("project.create", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { ShowError("validation.projectNameRequired"); return; }
            var project = await _projects.CreateAsync(name.Text, company.Text ?? "", (ProjectStage)stage.SelectedItem!,
                (ProjectStatus)status.SelectedItem!, owner.Text ?? "", notes.Text ?? "");
            await OpenProjectAsync(project.Id);
        }, "primary"));
        Children.Add(card);
    }
}
