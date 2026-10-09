using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop;

/// <summary>Edit-project dialog opened from the pencil beside the project title: name, company, owner, stage, status, notes and current state.</summary>
internal sealed class EditProjectDialog : DialogFrame
{
    public EditProjectDialog(PresentationContext context, Project project, IReadOnlyList<ProjectStage> stages) : base(context, "EditProjectDialog", () => context.Text.Get("v3.editProject"))
    {
        var name = Forms.Input(project.Name); var company = Forms.Input(project.CompanyName); var owner = Forms.Input(project.Owner);
        var stage = new ComboBox { ItemsSource = stages, SelectedItem = stages.FirstOrDefault(item => item.Id == project.StageId), MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Stretch, Name = "ProjectStageChoice", ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ProjectStage>((item, _) => Ui.StagePill(context, item)) };
        var status = project.Status;
        var notes = Forms.Input(project.Notes); notes.AcceptsReturn = true; notes.TextWrapping = TextWrapping.Wrap; notes.MinHeight = 76; notes.VerticalContentAlignment = VerticalAlignment.Top;
        var summary = Forms.Input(project.State.Summary); summary.AcceptsReturn = true; summary.TextWrapping = TextWrapping.Wrap; summary.MinHeight = 76; summary.VerticalContentAlignment = VerticalAlignment.Top;
        var error = context.Label("validation.projectNameRequired", "Small", "Error"); error.IsVisible = false; error.Name = "EditProjectError";
        name.TextChanged += (_, _) => error.IsVisible = false;

        var nameField = Forms.Field(context, "v3.projNameL", name); nameField.Children.Add(error);
        Body.Children.Add(nameField);
        var pair = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
        pair.Children.Add(Forms.Field(context, "v3.company", company));
        var ownerField = Forms.Field(context, "v3.owner", owner); Grid.SetColumn(ownerField, 1); pair.Children.Add(ownerField);
        Body.Children.Add(pair);
        Body.Children.Add(Forms.Field(context, "v3.stage", stage));
        Body.Children.Add(Forms.Field(context, "v3.statusP", Forms.Chips(context, Enum.GetValues<ProjectStatus>().Select(item => (item, (Func<string>)(() => context.EnumText(item)))), status, value => status = value, "ProjectStatusChips")));
        Body.Children.Add(Forms.Field(context, "field.notes", notes));
        Body.Children.Add(Forms.Field(context, "overview.currentState", summary));

        var cancel = context.Action("action.cancel", () => { context.Shell.CloseModal(); return Task.CompletedTask; });
        var save = context.Action("v3.saveB", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { error.IsVisible = true; return; }
            project.Name = name.Text.Trim(); project.CompanyName = company.Text?.Trim() ?? ""; project.Owner = owner.Text?.Trim() ?? "";
            project.Stage = (ProjectStage)stage.SelectedItem!; project.StageId = project.Stage.Id; project.Status = status;
            project.Notes = notes.Text ?? ""; project.State.Summary = summary.Text ?? "";
            await context.Projects.SaveAsync(project);
            context.Shell.CloseModal();
            await context.Shell.RefreshProjectAsync();
        }, "primary");
        Footer(null, cancel, save);
    }
}
