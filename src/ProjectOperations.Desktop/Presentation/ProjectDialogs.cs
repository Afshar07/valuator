using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Edit-project dialog opened from the pencil beside the project title: name, company, owner, stage, status, notes and current state.</summary>
internal sealed class EditProjectDialog : DialogFrame
{
    public EditProjectDialog(PresentationContext context, Project project) : base(context, "EditProjectDialog", () => context.Text.Get("v3.editProject"))
    {
        var name = Forms.Input(project.Name); var company = Forms.Input(project.CompanyName); var owner = Forms.Input(project.Owner);
        var stage = Forms.Choice(context, project.Stage, context.EnumText);
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
            project.Stage = (ProjectStage)stage.SelectedItem!; project.Status = status;
            project.Notes = notes.Text ?? ""; project.State.Summary = summary.Text ?? "";
            await context.Projects.SaveAsync(project);
            context.Shell.CloseModal();
            await context.Shell.RefreshProjectAsync();
        }, "primary");
        Footer(null, cancel, save);
    }
}

/// <summary>Shared date row of the task and milestone dialogs: quick chips, a date picker and the relative hint.</summary>
internal sealed class DateRow : StackPanel
{
    private readonly PresentationContext _context;
    private readonly CalendarDatePicker _picker = new() { MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 160, Name = "DialogDate", FlowDirection = FlowDirection.LeftToRight };
    private readonly List<(int? Offset, Button Button)> _chips = [];
    private readonly TextBlock _relative;
    private readonly TimeSpan _time;
    private bool _syncing;

    public DateTime? Date { get; private set; }

    public DateRow(PresentationContext context, DateTimeOffset? initial, bool allowNone)
    {
        _context = context; Spacing = 8; Date = initial?.ToLocalTime().Date;
        _time = initial?.ToLocalTime().TimeOfDay is { } time && time != TimeSpan.Zero ? time : new TimeSpan(23, 59, 0);
        var options = new List<(int? Offset, string Key)> { (0, "v3.dToday"), (1, "v3.dTomorrow"), (7, "v3.dWeek"), (14, "v3.d2Week") };
        if (allowNone) options.Add((null, "v3.dNone"));
        var chips = new WrapPanel { ItemSpacing = 6, LineSpacing = 6, Name = "DateChips" };
        foreach (var (offset, key) in options)
        {
            var chip = new Button { Height = 28, MinHeight = 28, Padding = new Thickness(11, 0) }; chip.Classes.Add("chip");
            context.Localized.Bind(chip, control => { control.Content = context.Text.Get(key); Avalonia.Automation.AutomationProperties.SetName(control, context.Text.Get(key)); });
            var captured = offset;
            chip.Click += (_, _) => { Date = captured is { } days ? DateTime.Today.AddDays(days) : null; Sync(); };
            _chips.Add((offset, chip)); chips.Children.Add(chip);
        }
        Children.Add(chips);
        _picker.SelectedDateChanged += (_, e) => { if (!_syncing) { Date = e.AddedItems.Count > 0 ? ((DateTime?)e.AddedItems[0])?.Date : null; Sync(); } };
        _relative = context.Label(RelativeText, "Small", "TextSecondary");
        _relative.VerticalAlignment = VerticalAlignment.Center;
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        line.Children.Add(_picker); line.Children.Add(_relative);
        Children.Add(line);
        Sync();
    }

    private string RelativeText() => Date is not { } day ? "" : _context.Relative(new DateTimeOffset(day, TimeZoneInfo.Local.GetUtcOffset(day)), false);

    private void Sync()
    {
        _syncing = true; _picker.SelectedDate = Date; _syncing = false;
        foreach (var (offset, button) in _chips)
            button.Classes.Set("selected", offset is { } days ? Date == DateTime.Today.AddDays(days) : Date is null);
        _relative.Text = RelativeText();
    }

    /// <summary>The chosen day at the original time of day (end of day for a new date), or null when no date is set.</summary>
    public DateTimeOffset? Value => Date is { } day ? new DateTimeOffset(day + _time, TimeZoneInfo.Local.GetUtcOffset(day + _time)) : null;
}

/// <summary>New / edit task dialog: title, due date, status and the requirement it follows up on.</summary>
internal sealed class TaskDialog : DialogFrame
{
    private sealed record RequirementOption(Guid? Id, string Title);

    public TaskDialog(PresentationContext context, Project project, ProjectTask task, bool isNew)
        : base(context, "TaskDialog", () => context.Text.Get(isNew ? "v3.newTaskT" : "v3.editTaskT"))
    {
        var title = Forms.Input(task.Title); title.Name = "TaskTitleInput";
        context.Localized.Bind(title, control => control.Watermark = context.Text.Get("v3.taskPh"));
        var status = isNew ? ProjectTaskStatus.Todo : task.Status;
        var display = new Localization.DomainDisplay(context.Text);
        var options = new List<RequirementOption> { new(null, "") };
        options.AddRange(project.Requirements.Select(requirement => new RequirementOption(requirement.Id, display.Requirement(requirement))));
        var requirement = new ComboBox
        {
            ItemsSource = options,
            SelectedItem = options.FirstOrDefault(option => option.Id == task.RequirementId) ?? options[0],
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Name = "TaskRequirement",
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<RequirementOption>((option, _) => context.Label(() => option.Id is null ? context.Text.Get("v3.noneL") : option.Title, "Body"))
        };
        var date = new DateRow(context, isNew ? DateTimeOffset.Now.AddDays(1) : task.DueAt, allowNone: true);

        Body.Children.Add(Forms.Field(context, "v3.titleL", title));
        Body.Children.Add(Forms.Field(context, "v3.dueDate", date));
        Body.Children.Add(Forms.Field(context, "field.status", Forms.Chips(context, Enum.GetValues<ProjectTaskStatus>().Select(item => (item, (Func<string>)(() => context.EnumText(item)))), status, value => status = value, "TaskStatusChips")));
        Body.Children.Add(Forms.Field(context, "v3.linkReq", requirement));

        var cancel = context.Action("action.cancel", () => { context.Shell.CloseModal(); return Task.CompletedTask; });
        var save = context.Action(isNew ? "v3.createTask" : "v3.saveB", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) return;
            task.Title = title.Text.Trim(); task.DueAt = date.Value; task.Status = status; task.RequirementId = ((RequirementOption)requirement.SelectedItem!).Id;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            if (isNew && !project.Tasks.Contains(task)) project.Tasks.Add(task);
            await context.Projects.SaveAsync(project);
            context.Shell.CloseModal();
            await context.Shell.RefreshProjectAsync();
        }, "primary");
        save.Name = "SaveTaskButton"; save.IsEnabled = !string.IsNullOrWhiteSpace(title.Text);
        title.TextChanged += (_, _) => save.IsEnabled = !string.IsNullOrWhiteSpace(title.Text);
        Button? delete = null;
        if (!isNew)
            delete = context.IconAction("v3.del", Icons.Trash, async () =>
            {
                project.Tasks.Remove(task); await context.Projects.SaveAsync(project);
                context.Shell.CloseModal(); await context.Shell.RefreshProjectAsync();
            }, "dangerGhost");
        Footer(delete, cancel, save);
    }
}

/// <summary>New / edit milestone dialog: title, date and whether it has been reached.</summary>
internal sealed class MilestoneDialog : DialogFrame
{
    public MilestoneDialog(PresentationContext context, Project project, Milestone milestone, bool isNew)
        : base(context, "MilestoneDialog", () => context.Text.Get(isNew ? "v3.newMsT" : "v3.editMsT"))
    {
        var title = Forms.Input(milestone.Title); title.Name = "MilestoneTitleInput";
        context.Localized.Bind(title, control => control.Watermark = context.Text.Get("v3.msPh"));
        var date = new DateRow(context, isNew ? DateTimeOffset.Now.AddDays(7) : milestone.DueAt, allowNone: false);
        var reached = new CheckBox { IsChecked = milestone.IsComplete, Name = "MilestoneReached" };
        context.Localized.Bind(reached, control => control.Content = context.Text.Get("v3.reachedL"));

        Body.Children.Add(Forms.Field(context, "v3.titleL", title));
        Body.Children.Add(Forms.Field(context, "v3.dateL", date));
        Body.Children.Add(reached);

        var cancel = context.Action("action.cancel", () => { context.Shell.CloseModal(); return Task.CompletedTask; });
        var save = context.Action(isNew ? "v3.createMs" : "v3.saveB", async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) return;
            milestone.Title = title.Text.Trim(); milestone.DueAt = date.Value; milestone.IsComplete = reached.IsChecked == true;
            if (isNew && !project.Milestones.Contains(milestone)) project.Milestones.Add(milestone);
            await context.Projects.SaveAsync(project);
            context.Shell.CloseModal();
            await context.Shell.RefreshProjectAsync();
        }, "primary");
        save.Name = "SaveMilestoneButton"; save.IsEnabled = !string.IsNullOrWhiteSpace(title.Text);
        title.TextChanged += (_, _) => save.IsEnabled = !string.IsNullOrWhiteSpace(title.Text);
        Button? delete = null;
        if (!isNew)
            delete = context.IconAction("v3.del", Icons.Trash, async () =>
            {
                project.Milestones.Remove(milestone); await context.Projects.SaveAsync(project);
                context.Shell.CloseModal(); await context.Shell.RefreshProjectAsync();
            }, "dangerGhost");
        Footer(delete, cancel, save);
    }
}
