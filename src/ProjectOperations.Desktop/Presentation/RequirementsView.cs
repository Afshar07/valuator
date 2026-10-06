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

internal sealed class RequirementsView : PresentationView
{
    public RequirementsView(PresentationContext context, Project project) : base(context) => Children.Add(Requirements(project));
    private Control Requirements(Project project)
    {
        var host = new ContentControl();
        host.Content = RequirementList(project, host);
        return host;
    }

    private Control RequirementList(Project project, ContentControl host)
    {
        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(Label("overview.readinessDisclosure", "BodySmall", "TextSecondary"));
        foreach (var group in VcTemplate.Create().Groups)
        {
            var requirements = project.Requirements.Where(r => r.GroupId == group.Id).ToList();
            var card = new RequirementGroupCard(Context, group.Id, group.Title, requirements);
            foreach (var requirement in requirements)
                card.Body.Children.Add(new RequirementRow(Context, requirement, () =>
                { host.Content = RequirementEditor(project, requirement, host); host.BringIntoView(); return Task.CompletedTask; }));
            panel.Children.Add(card);
        }
        return panel;
    }

    private Control RequirementEditor(Project project, ProjectRequirement requirement, ContentControl host)
    {
        var card = new SectionCard(Context, () => RequirementTitle(requirement)) { Name = "RequirementEditor" }; var panel = card.Body;
        panel.Children.Add(Action("requirement.back", () => { host.Content = RequirementList(project, host); host.BringIntoView(); return Task.CompletedTask; }));
        var status = Choice(requirement.Status); var value = Input(requirement.Value, true); var notes = Input(requirement.Notes, true);
        Field(panel, "requirement.reviewStatus", status); panel.Children.Add(Label(() => F("requirement.value", EnumText(requirement.Type)), "Label", "TextSecondary")); panel.Children.Add(value); Field(panel, "field.notes", notes);
        panel.Children.Add(Label(() => F("requirement.reviewed", Due(requirement.LastReviewedAt)), "Caption", "TextSecondary"));
        panel.Children.Add(Action("requirement.save", async () =>
        {
            requirement.Status = (RequirementStatus)status.SelectedItem!; requirement.Value = value.Text ?? ""; requirement.Notes = notes.Text ?? "";
            requirement.LastReviewedAt = DateTimeOffset.UtcNow;
            await SaveAsync(project); await OpenProjectAsync(project.Id, 1);
        }, "primary"));
        panel.Children.Add(Label("file.disclosure", "BodySmall", "TextSecondary"));
        foreach (var file in requirement.Files.ToList())
        {
            var reference = Readable(""); reference.FlowDirection = FlowDirection.LeftToRight; Bind(reference, control => control.Text = F("file.metadata", file.FileName, file.SizeBytes, Due(file.AddedAt), file.Path)); panel.Children.Add(reference);
            panel.Children.Add(Action("file.removeAssociation", async () =>
            {
                requirement.Files.Remove(file); await SaveAsync(project);
                host.Content = RequirementEditor(project, requirement, host);
            }));
        }
        panel.Children.Add(Action("file.addReferences", async () =>
        {
            var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = T("file.pickerTitle"), AllowMultiple = true });
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (path is null) { ShowError("validation.localFilesOnly"); continue; }
                if (requirement.Files.Any(existing => existing.Path == path)) continue;
                var info = new FileInfo(path);
                requirement.Files.Add(new ProjectFile { FileName = file.Name, Path = path, SizeBytes = info.Length });
            }
            await SaveAsync(project); host.Content = RequirementEditor(project, requirement, host);
        }));
        return card;
    }
}
