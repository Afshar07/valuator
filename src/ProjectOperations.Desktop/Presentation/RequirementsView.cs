using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Requirements & files tab: grouped checklist, plus the per-requirement editor with file references.</summary>
internal sealed class RequirementsView : PresentationView
{
    public RequirementsView(PresentationContext context, Project project) : base(context)
    {
        var host = new ContentControl();
        host.Content = RequirementList(project, host);
        Children.Add(host);
    }

    private Control RequirementList(Project project, ContentControl host)
    {
        var panel = new StackPanel { Spacing = 16 };
        var note = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var gap = new TextBlock { TextWrapping = TextWrapping.Wrap }; PresentationTheme.Typeset(gap, "Small");
        Bind(gap, control =>
        {
            control.Inlines = [new Avalonia.Controls.Documents.Run(T("requirements.extractionGap") + ". ") { FontWeight = FontWeight.SemiBold }, new Avalonia.Controls.Documents.Run(T("requirements.fileNote"))];
        });
        note.Children.Add(gap);
        var readiness = Label("overview.readinessDisclosure", "Caption", "TextSecondary"); readiness.MaxWidth = 380; readiness.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(readiness, 1); note.Children.Add(readiness);
        panel.Children.Add(Ui.Banner(note, Icons.FileDashed, "Warning", "WarningSoft"));
        foreach (var group in VcTemplate.Create().Groups)
        {
            var requirements = project.Requirements.Where(r => r.GroupId == group.Id).ToList();
            var complete = requirements.Count(item => item.Status == RequirementStatus.Complete);
            var summary = Label(() => F("presentation.groupComplete", complete, requirements.Count), "Caption", "TextSecondary");
            var card = new ListCard(Context, () => GroupTitle(group.Id, group.Title), summary) { Name = "RequirementGroup" };
            foreach (var requirement in requirements)
                card.Add(new RequirementRow(Context, requirement, () =>
                { host.Content = RequirementEditor(project, requirement, host); host.BringIntoView(); return Task.CompletedTask; }));
            panel.Children.Add(card);
        }
        return panel;
    }

    private Control RequirementEditor(Project project, ProjectRequirement requirement, ContentControl host)
    {
        var back = Context.IconAction("requirement.back", _locale.LanguageCode == "fa" ? Icons.ArrowRight : Icons.ArrowLeft,
            () => { host.Content = RequirementList(project, host); host.BringIntoView(); return Task.CompletedTask; }, "link");
        var card = new SectionCard(Context, () => RequirementTitle(requirement), Ui.Pill(Context, () => EnumText(requirement.Status), StatusVisuals.Requirement(requirement.Status).Tone)) { Name = "RequirementEditor" };
        var panel = card.Body; panel.Spacing = 12;
        panel.Children.Insert(0, back);
        var status = Choice(requirement.Status); var value = Input(requirement.Value, true); var notes = Input(requirement.Notes, true);
        Field(panel, "requirement.reviewStatus", status);
        panel.Children.Add(Label(() => F("requirement.value", EnumText(requirement.Type)), "Caption", "TextSecondary")); panel.Children.Add(value);
        Field(panel, "field.notes", notes);
        panel.Children.Add(Label(() => F("requirement.reviewed", Due(requirement.LastReviewedAt)), "Caption", "TextSecondary"));
        panel.Children.Add(Action("requirement.save", async () =>
        {
            requirement.Status = (RequirementStatus)status.SelectedItem!; requirement.Value = value.Text ?? ""; requirement.Notes = notes.Text ?? "";
            requirement.LastReviewedAt = DateTimeOffset.UtcNow;
            await SaveAsync(project); await OpenProjectAsync(project.Id, 1);
        }, "primary"));
        panel.Children.Add(Ui.Rule());
        panel.Children.Add(Label("file.title", "Heading"));
        panel.Children.Add(Ui.Banner(Label("file.disclosure", "Small"), Icons.Info, "TextSecondary", "BackgroundMuted"));
        foreach (var file in requirement.Files.ToList())
        {
            var reference = Readable(""); reference.FlowDirection = FlowDirection.LeftToRight; reference.MinHeight = 0;
            Bind(reference, control => control.Text = F("file.metadata", file.FileName, file.SizeBytes, Due(file.AddedAt), file.Path)); panel.Children.Add(reference);
            panel.Children.Add(Context.IconAction("file.removeAssociation", Icons.LinkBreak, async () =>
            {
                requirement.Files.Remove(file); await SaveAsync(project);
                host.Content = RequirementEditor(project, requirement, host);
            }, "ghost"));
        }
        panel.Children.Add(Context.IconAction("file.addReferences", Icons.Plus, async () =>
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

/// <summary>Requirement checklist row (opens the editor). Shows a file-reference chip, the stored value, or an add-file hint.</summary>
internal sealed class RequirementRow : ListRow
{
    public RequirementRow(PresentationContext context, ProjectRequirement requirement, Func<Task> action)
        : base(context, Layout(context, requirement),
            () => context.Text.Format("requirement.summary", new DomainDisplay(context.Text).Requirement(requirement), context.EnumText(requirement.Type), context.EnumText(requirement.Status), requirement.Files.Count),
            action)
    { Name = "RequirementRow"; }

    private static Grid Layout(PresentationContext context, ProjectRequirement requirement)
    {
        var (icon, weight, tone) = StatusVisuals.Requirement(requirement.Status);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*,92,Auto,112"), ColumnSpacing = 12 };
        grid.Children.Add(Icons.Glyph(icon, 17, tone.Foreground, weight));
        var title = context.Label(() => new DomainDisplay(context.Text).Requirement(requirement), "BodyMedium"); title.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(title, 1); grid.Children.Add(title);
        var type = context.Label(() => context.EnumText(requirement.Type), "Caption", "TextTertiary"); type.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(type, 2); grid.Children.Add(type);
        Control detail;
        if (requirement.Files.Count > 0)
        {
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            chip.Children.Add(Icons.Glyph(Icons.LinkSimple, 12, "TextSecondary"));
            var name = context.Label(() => requirement.Files[0].FileName, "Caption", "TextSecondary"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.MaxWidth = 170; name.FlowDirection = FlowDirection.LeftToRight; chip.Children.Add(name);
            if (requirement.Files.Count > 1) chip.Children.Add(context.Label(() => "+" + context.Number(requirement.Files.Count - 1), "Caption", "TextTertiary"));
            detail = new Border { Padding = new Thickness(8, 3), CornerRadius = new CornerRadius(PresentationTheme.RadiusSmall), BorderThickness = new Thickness(1), Child = chip }
                .Paint(Border.BackgroundProperty, "BackgroundMuted").Paint(Border.BorderBrushProperty, "BorderDefault");
        }
        else if (!string.IsNullOrWhiteSpace(requirement.Value) && requirement.Type is RequirementType.Money or RequirementType.Number)
        {
            var value = context.Label(() => requirement.Value.Trim(), "BodyStrong"); value.TextWrapping = TextWrapping.NoWrap; value.TextTrimming = TextTrimming.CharacterEllipsis; value.MaxWidth = 200;
            value.FlowDirection = FlowDirection.LeftToRight; detail = value;
        }
        else if (requirement.Type == RequirementType.Document)
        {
            var hint = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            hint.Children.Add(Icons.Glyph(Icons.Plus, 12, "TextTertiary"));
            var text = context.Label("file.addReference", "Caption", "TextTertiary"); text.TextWrapping = TextWrapping.NoWrap; hint.Children.Add(text);
            detail = new DashedFrame(hint, "BorderDefault", null, PresentationTheme.RadiusSmall, new Thickness(8, 3));
        }
        else detail = new Panel();
        detail.HorizontalAlignment = HorizontalAlignment.Left; detail.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(detail, 3); grid.Children.Add(detail);
        var pill = Ui.Pill(context, () => context.EnumText(requirement.Status), tone); pill.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(pill, 4); grid.Children.Add(pill);
        return grid;
    }
}
