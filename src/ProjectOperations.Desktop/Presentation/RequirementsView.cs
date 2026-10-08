using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>
/// Requirements &amp; files tab: a collapsible checklist. Opening an item expands it in place to link a file or enter a value,
/// set its review status and add a follow-up task. Only Complete counts toward readiness.
/// </summary>
internal sealed class RequirementsView : PresentationView
{
    private readonly Project _project;
    private Guid? _taskFor;
    private string _taskTitle = "";
    private int? _taskDays = 1;
    private string _draft = "";
    private Guid? _draftFor;
    private bool _adding;
    private string _newRequirement = "";

    public RequirementsView(PresentationContext context, Project project) : base(context)
    {
        _project = project; Spacing = 16;
        if (Context.State.InitializedProjects.Add(project.Id) && project.Requirements.Count > 0)
            Context.State.ExpandedGroups.Add(Key(project.Requirements[0].GroupId));
        if (Context.State.PendingFollowUp is { } pending && project.Requirements.FirstOrDefault(item => item.Id == pending) is { } target)
        {
            _taskFor = pending; _taskTitle = $"{T("v3.followUp")} {RequirementTitle(target)}"; _taskDays = 1; Context.State.PendingFollowUp = null;
        }
        Rebuild();
    }

    private string Key(string groupId) => $"{_project.Id}:{groupId}";

    private void Rebuild()
    {
        Children.Clear();
        if (!Context.State.TipHidden) Children.Add(Tip());
        var next = _project.Requirements.FirstOrDefault(item => item.Status == RequirementStatus.Missing);
        Children.Add(next is null ? AllTouched() : NextUp(next));
        foreach (var group in _project.Requirements.GroupBy(item => item.GroupId))
            Children.Add(Group(group.Key, group.ToList()));
        if (_project.TemplateId == ProjectService.BlankTemplateId) Children.Add(AddRequirement());
    }

    /// <summary>Blank projects have no template, so requirements are added here, one title at a time.</summary>
    private Control AddRequirement()
    {
        if (!_adding)
        {
            var link = Context.IconAction("v3.addRequirement", Icons.Plus, () => { _adding = true; _newRequirement = ""; Rebuild(); return Task.CompletedTask; }, "link");
            link.Name = "AddRequirement"; link.Paint(Button.ForegroundProperty, "AccentText"); return link;
        }
        var input = Forms.Input(_newRequirement, 36); input.Name = "NewRequirementTitle";
        Bind(input, control => control.Watermark = T("v3.requirementPh"));
        input.TextChanged += (_, _) => _newRequirement = input.Text ?? "";
        input.AttachedToVisualTree += (_, _) => input.Focus();
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left };
        row.Children.Add(input);
        var cancel = Action("action.cancel", () => { _adding = false; Rebuild(); return Task.CompletedTask; }, "ghost"); Grid.SetColumn(cancel, 1); row.Children.Add(cancel);
        var add = Action("v3.addRequirement", async () =>
        {
            if (_newRequirement.Trim().Length == 0) return;
            var requirement = ProjectService.NewCustomRequirement(_newRequirement);
            _project.Requirements.Add(requirement); _adding = false; Context.State.OpenRequirement = requirement.Id; Context.State.ExpandedGroups.Add(Key(requirement.GroupId));
            await SaveAsync(_project); await Context.Shell.RefreshProjectAsync();
        }, "primary");
        add.Name = "AddRequirementSubmit"; Grid.SetColumn(add, 2); row.Children.Add(add);
        return row;
    }

    private string GroupName(string id) => id == "custom" ? T("v3.blankGroup") : GroupTitle(id, VcTemplate.Create().Groups.FirstOrDefault(group => group.Id == id)?.Title ?? id);

    private Control Tip()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, Name = "ChecklistTip" };
        var bulb = Icons.Glyph(Icons.Lightbulb, 18, "Accent"); bulb.VerticalAlignment = VerticalAlignment.Top; bulb.Margin = new Thickness(0, 1, 0, 0);
        grid.Children.Add(bulb);
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label("v3.howTitle", "BodyStrong")); text.Children.Add(Label("v3.howBody", "Small", "TextSecondary"));
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        var gotIt = Action("v3.gotIt", () => { Context.State.TipHidden = true; Rebuild(); return Task.CompletedTask; }); gotIt.MinHeight = 28; gotIt.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(gotIt, 2); grid.Children.Add(gotIt);
        return Outlined(grid, "BackgroundCard", "BorderDefault", 10, new Thickness(14, 12));
    }

    private Control NextUp(ProjectRequirement requirement)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16, Name = "NextUpCard" };
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var eyebrow = Label("v3.nextUp", "Micro", "AccentText"); eyebrow.FontWeight = FontWeight.SemiBold; eyebrow.LetterSpacing = 0.44; text.Children.Add(eyebrow);
        text.Children.Add(Label(() => RequirementTitle(requirement), "Heading"));
        text.Children.Add(Label(() => T("v3.hint_" + HintKey(requirement.Type)), "Small", "TextSecondary"));
        grid.Children.Add(text);
        var open = Action("v3.openItem", () => { Open(requirement); return Task.CompletedTask; }, "primary"); open.Name = "NextUpOpen"; open.MinHeight = 32;
        Grid.SetColumn(open, 1); grid.Children.Add(open);
        return Outlined(grid, "AccentSoft", "Accent", PresentationTheme.RadiusCard, new Thickness(16, 14));
    }

    private Control AllTouched()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10, Name = "AllTouchedBanner" };
        row.Children.Add(Icons.Glyph(Icons.CheckCircle, 16, "Success", IconWeight.Fill));
        var text = Label("v3.allTouched", "Small"); Grid.SetColumn(text, 1); row.Children.Add(text);
        return new Border { Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), Child = row }.Paint(Border.BackgroundProperty, "SuccessSoft");
    }

    private static Border Outlined(Control child, string background, string border, double radius, Thickness padding) => new Border
    {
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Padding = padding,
        Child = child
    }.Paint(Border.BackgroundProperty, background).Paint(Border.BorderBrushProperty, border);

    private void Open(ProjectRequirement requirement)
    {
        Context.State.OpenRequirement = requirement.Id; Context.State.ExpandedGroups.Add(Key(requirement.GroupId)); _draftFor = null; _taskFor = null;
        Rebuild();
    }

    private static string HintKey(RequirementType type) => type.ToString().ToLowerInvariant() switch { "structured" => "structured", var other => other };

    private Control Group(string groupId, List<ProjectRequirement> requirements)
    {
        var expanded = Context.State.ExpandedGroups.Contains(Key(groupId));
        var done = requirements.Count(item => item.Status == RequirementStatus.Complete);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        var caret = Icons.Glyph(expanded ? Icons.CaretDown : Icons.CaretRight, 14, "TextTertiary");
        Bind(caret, control => control.Text = expanded ? Icons.CaretDown : _locale.LanguageCode == "fa" ? Icons.CaretLeft : Icons.CaretRight);
        header.Children.Add(caret);
        var title = Label(() => GroupName(groupId), "Heading"); title.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(title, 1); header.Children.Add(title);
        var summary = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var bar = Ui.Progress(requirements.Count == 0 ? 0 : 100.0 * done / requirements.Count, 4); bar.Width = 64; summary.Children.Add(bar);
        summary.Children.Add(Label(() => F("presentation.groupComplete", N(done), N(requirements.Count)), "Caption", "TextSecondary"));
        Grid.SetColumn(summary, 2); header.Children.Add(summary);
        var button = new ListRow(Context, header, () => $"{GroupName(groupId)} · {F("presentation.groupComplete", N(done), N(requirements.Count))}", () =>
        {
            var key = Key(groupId);
            if (!Context.State.ExpandedGroups.Remove(key)) Context.State.ExpandedGroups.Add(key);
            Rebuild(); return Task.CompletedTask;
        })
        { Name = "RequirementGroupHeader", Padding = new Thickness(16, 12) };

        var stack = new StackPanel();
        stack.Children.Add(button);
        if (expanded)
            foreach (var requirement in requirements) stack.Children.Add(Ui.Separated(Row(requirement)));
        return new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusCard), ClipToBounds = true, Child = stack, Name = "RequirementGroup" }
            .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault").CardShadowed();
    }

    private Control Row(ProjectRequirement requirement)
    {
        var open = Context.State.OpenRequirement == requirement.Id;
        var (icon, weight, tone) = StatusVisuals.Requirement(requirement.Status);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*,200,112,14"), ColumnSpacing = 12 };
        grid.Children.Add(Icons.Glyph(icon, 17, tone.Foreground, weight));
        var title = Label(() => RequirementTitle(requirement), "BodyMedium"); title.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(title, 1); grid.Children.Add(title);
        Control detail = new Panel();
        if (requirement.Files.Count > 0)
        {
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            chip.Children.Add(Icons.Glyph(Icons.LinkSimple, 12, "TextSecondary"));
            var name = Label(() => requirement.Files[0].FileName, "Caption", "TextSecondary"); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; name.MaxWidth = 150; name.FlowDirection = FlowDirection.LeftToRight;
            chip.Children.Add(name);
            if (requirement.Files.Count > 1) chip.Children.Add(Label(() => "+" + N(requirement.Files.Count - 1), "Caption", "TextTertiary"));
            detail = new Border { Padding = new Thickness(8, 3), CornerRadius = new CornerRadius(PresentationTheme.RadiusSmall), BorderThickness = new Thickness(1), Child = chip }
                .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault");
        }
        else if (!string.IsNullOrWhiteSpace(requirement.Value))
        {
            var value = Label(() => requirement.Value.Trim(), "BodyStrong"); value.TextWrapping = TextWrapping.NoWrap; value.TextTrimming = TextTrimming.CharacterEllipsis; detail = value;
        }
        detail.HorizontalAlignment = HorizontalAlignment.Left; detail.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(detail, 2); grid.Children.Add(detail);
        var pill = Ui.Pill(Context, () => EnumText(requirement.Status), tone); pill.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(pill, 3); grid.Children.Add(pill);
        var caret = Icons.Glyph(open ? Icons.CaretUp : Icons.CaretDown, 14, "TextTertiary"); Grid.SetColumn(caret, 4); grid.Children.Add(caret);

        var row = new ListRow(Context, grid,
            () => Context.Text.Format("requirement.summary", RequirementTitle(requirement), EnumText(requirement.Type), EnumText(requirement.Status), requirement.Files.Count),
            () => { Context.State.OpenRequirement = open ? null : requirement.Id; _draftFor = null; _taskFor = null; Rebuild(); return Task.CompletedTask; })
        { Name = "RequirementRow", Padding = new Thickness(16, 10) };
        row.Classes.Set("selected", open);
        var stack = new StackPanel();
        stack.Children.Add(row);
        if (open) stack.Children.Add(Detail(requirement));
        return stack;
    }

    private Control Detail(ProjectRequirement requirement)
    {
        var panel = new StackPanel { Spacing = 12 };
        var hint = new TextBlock { TextWrapping = TextWrapping.Wrap }; PresentationTheme.Typeset(hint, "Small", "TextSecondary");
        Bind(hint, control => control.Inlines =
        [
            new Run(EnumText(requirement.Type) + ".") { FontWeight = FontWeight.SemiBold }.Paint(TextElement.ForegroundProperty, "TextPrimary"),
            new Run(" " + T("v3.hint_" + HintKey(requirement.Type)))
        ]);
        panel.Children.Add(hint);

        if (requirement.Type == RequirementType.Document)
        {
            var files = new StackPanel { Spacing = 8 };
            foreach (var file in requirement.Files.ToList())
            {
                var fileRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
                var chip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
                chip.Children.Add(Icons.Glyph(Icons.ForFile(file.Path), 14, "TextSecondary"));
                var name = Label(() => file.FileName, "Small"); name.FlowDirection = FlowDirection.LeftToRight; name.VerticalAlignment = VerticalAlignment.Center; chip.Children.Add(name);
                fileRow.Children.Add(new Border { Height = 30, Padding = new Thickness(10, 0), CornerRadius = new CornerRadius(PresentationTheme.RadiusControl), BorderThickness = new Thickness(1), Child = chip, VerticalAlignment = VerticalAlignment.Center }
                    .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault"));
                var unlink = Action("documents.unlink", async () => { requirement.Files.Remove(file); await SaveAsync(_project); await Context.Shell.RefreshProjectAsync(); }, "ghost");
                unlink.Name = "UnlinkFile"; unlink.MinHeight = 30; unlink.Padding = new Thickness(8, 0); unlink.VerticalAlignment = VerticalAlignment.Center; fileRow.Children.Add(unlink);
                files.Children.Add(fileRow);
            }
            var pick = Context.IconAction("v3.chooseFile", Icons.FolderOpen, () => PickFilesAsync(requirement));
            pick.Name = "ChooseFile"; pick.MinHeight = 30; pick.HorizontalAlignment = HorizontalAlignment.Left; files.Children.Add(pick);
            panel.Children.Add(files);
        }
        else
        {
            var input = Forms.Input(_draftFor == requirement.Id ? _draft : requirement.Value, 34); input.Name = "RequirementValue";
            Bind(input, control => control.Watermark = T("v3.valuePh"));
            input.TextChanged += (_, _) => { _draft = input.Text ?? ""; _draftFor = requirement.Id; };
            var save = Action("v3.saveV", async () =>
            {
                var value = (input.Text ?? "").Trim(); if (value.Length == 0) return;
                requirement.Value = value; if (requirement.Status == RequirementStatus.Missing) requirement.Status = RequirementStatus.Provided;
                requirement.LastReviewedAt = DateTimeOffset.UtcNow;
                await SaveAsync(_project); await Context.Shell.RefreshProjectAsync();
            }, "primary");
            save.Name = "SaveValue"; save.MinHeight = 34;
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
            line.Children.Add(input); Grid.SetColumn(save, 1); line.Children.Add(save);
            panel.Children.Add(line);
        }

        var status = new WrapPanel { ItemSpacing = 8, LineSpacing = 6 };
        var statusLabel = Label("v3.statusL", "Caption", "TextSecondary"); statusLabel.MinWidth = 52; statusLabel.VerticalAlignment = VerticalAlignment.Center; status.Children.Add(statusLabel);
        var chips = new WrapPanel { ItemSpacing = 4, LineSpacing = 4, Name = "RequirementStatusChips" };
        foreach (var option in new[] { RequirementStatus.Missing, RequirementStatus.Provided, RequirementStatus.NeedsReview, RequirementStatus.Complete })
        {
            var tone = StatusVisuals.Requirement(option).Tone; var selected = requirement.Status == option; var captured = option;
            var chip = new Button { Height = 26, MinHeight = 26, Padding = new Thickness(10, 0) }; chip.Classes.Add("chip");
            Bind(chip, control => { control.Content = EnumText(captured); Avalonia.Automation.AutomationProperties.SetName(control, EnumText(captured)); });
            if (selected) { chip.Paint(Button.BackgroundProperty, tone.Background).Paint(Button.ForegroundProperty, tone.Foreground).Paint(Button.BorderBrushProperty, tone.Foreground); }
            chip.Click += async (_, _) => await Context.ActAsync(chip, async () =>
            {
                requirement.Status = captured; requirement.LastReviewedAt = DateTimeOffset.UtcNow;
                await SaveAsync(_project); await Context.Shell.RefreshProjectAsync();
            });
            chips.Children.Add(chip);
        }
        status.Children.Add(chips);
        panel.Children.Add(status);

        panel.Children.Add(_taskFor == requirement.Id ? TaskForm(requirement) : AddTaskLink(requirement));

        var border = new Border { Child = panel, Name = "RequirementDetail" }.Paint(Border.BackgroundProperty, "BackgroundMuted");
        Bind(border, control => control.Padding = _locale.LanguageCode == "fa" ? new Thickness(16, 4, 48, 16) : new Thickness(48, 4, 16, 16));
        return border;
    }

    private async Task PickFilesAsync(ProjectRequirement requirement)
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = T("file.pickerTitle"), AllowMultiple = true });
        var added = false;
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is null) { ShowError("validation.localFilesOnly"); continue; }
            if (requirement.Files.Any(existing => existing.Path == path)) continue;
            requirement.Files.Add(new ProjectFile { FileName = file.Name, Path = path, SizeBytes = new FileInfo(path).Length }); added = true;
        }
        if (!added) return;
        if (requirement.Status == RequirementStatus.Missing) requirement.Status = RequirementStatus.Provided;
        requirement.LastReviewedAt = DateTimeOffset.UtcNow;
        await SaveAsync(_project); await Context.Shell.RefreshProjectAsync();
    }

    private Control AddTaskLink(ProjectRequirement requirement)
    {
        var link = Context.IconAction("v3.addTask", Icons.Plus, () =>
        {
            _taskFor = requirement.Id; _taskTitle = $"{T("v3.followUp")} {RequirementTitle(requirement)}"; _taskDays = 1; Rebuild(); return Task.CompletedTask;
        }, "link");
        link.Name = "AddFollowUpTask"; link.Paint(Button.ForegroundProperty, "AccentText");
        return link;
    }

    private Control TaskForm(ProjectRequirement requirement)
    {
        var form = new StackPanel { Spacing = 10 };
        var input = Forms.Input(_taskTitle, 34); input.Name = "FollowUpTitle"; input.Background = null;
        input.Paint(TextBox.BackgroundProperty, "BackgroundMuted");
        Bind(input, control => control.Watermark = T("v3.taskPh"));
        input.TextChanged += (_, _) => _taskTitle = input.Text ?? "";
        form.Children.Add(input);

        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };
        var due = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var dueLabel = Label("proposal.due", "Caption", "TextSecondary"); dueLabel.VerticalAlignment = VerticalAlignment.Center; due.Children.Add(dueLabel);
        var buttons = new List<(int? Days, Button Button)>();
        foreach (var (days, key) in new (int?, string)[] { (1, "v3.dTomorrow"), (7, "v3.dWeek"), (14, "v3.d2Week"), (null, "v3.dNone") })
        {
            var chip = new Button { Height = 26, MinHeight = 26, Padding = new Thickness(10, 0) }; chip.Classes.Add("chip");
            Bind(chip, control => { control.Content = T(key); Avalonia.Automation.AutomationProperties.SetName(control, T(key)); });
            chip.Classes.Set("selected", _taskDays == days);
            var captured = days;
            chip.Click += (_, _) => { _taskDays = captured; foreach (var (other, button) in buttons) button.Classes.Set("selected", other == captured); };
            buttons.Add((days, chip)); due.Children.Add(chip);
        }
        line.Children.Add(due);
        var cancel = Action("action.cancel", () => { _taskFor = null; Rebuild(); return Task.CompletedTask; }, "ghost"); cancel.MinHeight = 30;
        Grid.SetColumn(cancel, 2); line.Children.Add(cancel);
        var add = Action("v3.createTask", async () =>
        {
            var title = _taskTitle.Trim(); if (title.Length == 0) return;
            DateTimeOffset? dueAt = _taskDays is { } days ? new DateTimeOffset(DateTime.Today.AddDays(days).AddHours(23).AddMinutes(59), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today.AddDays(days))) : null;
            _project.Tasks.Add(new ProjectTask { ProjectId = _project.Id, Title = title, DueAt = dueAt, RequirementId = requirement.Id });
            _taskFor = null;
            Context.Shell.ShowToast("v3.toastTask");
            await SaveAsync(_project); await Context.Shell.RefreshProjectAsync();
        }, "primary");
        add.Name = "AddFollowUpSubmit"; add.MinHeight = 30;
        Grid.SetColumn(add, 3); line.Children.Add(add);
        form.Children.Add(line);
        var card = new Border { Padding = new Thickness(12), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusMedium), MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left, Child = form }
            .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault");
        return card;
    }
}
