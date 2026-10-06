using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Agents;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Overview tab: readiness, requirement completeness, deadlines, delegation shortcuts and project editing.</summary>
internal sealed class OverviewView : PresentationView
{
    private readonly Project _project;
    private readonly ProjectSummary _summary;
    private readonly Action<string> _useQuickAction;
    private readonly Action<int> _selectTab;

    public OverviewView(PresentationContext context, Project project, Action<string> useQuickAction, Action<int> selectTab) : base(context)
    {
        _project = project; _useQuickAction = useQuickAction; _selectTab = selectTab; Spacing = 24;
        _summary = ProjectSummaries.Summarize(project, DateTimeOffset.Now);
        var columns = new AdaptiveGrid { MinItemWidth = 380, Weights = [1.8, 1] };
        var main = new StackPanel { Spacing = 16 };
        main.Children.Add(ReadinessCard()); main.Children.Add(StateCard()); main.Children.Add(RequirementsCard());
        var side = new StackPanel { Spacing = 16 };
        side.Children.Add(TasksCard()); side.Children.Add(AiCard());
        columns.Children.Add(main); columns.Children.Add(side);
        Children.Add(columns); Children.Add(DetailsCard());
    }

    private Control ReadinessCard()
    {
        var card = new SectionCard(Context, "dashboard.projectReadiness") { Name = "ReadinessCard" };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 16 };
        grid.Children.Add(new ReadinessRing(_summary.CompletionPercentage, Context) { VerticalAlignment = VerticalAlignment.Center });
        var copy = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(Label(() => F("overview.readiness", _summary.CompleteRequirements, _summary.TotalRequirements, _summary.CompletionPercentage), "Label"));
        copy.Children.Add(Label("overview.readinessDisclosure", "Caption", "TextSecondary"));
        Grid.SetColumn(copy, 1); grid.Children.Add(copy);
        var counts = new StackPanel { Spacing = 6, MinWidth = 140, VerticalAlignment = VerticalAlignment.Center };
        foreach (var status in new[] { RequirementStatus.Complete, RequirementStatus.Provided, RequirementStatus.NeedsReview, RequirementStatus.Missing })
            counts.Children.Add(CountLine(status));
        Grid.SetColumn(counts, 2); grid.Children.Add(counts);
        card.Body.Children.Add(grid); return card;
    }

    private Control CountLine(RequirementStatus status)
    {
        var tone = StatusPill.Tone(status); var count = _project.Requirements.Count(item => item.Status == status);
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        line.Children.Add(PresentationTheme.Typeset(new TextBlock { Text = "●", FontSize = 10 }, "Caption", tone));
        var name = Label(() => EnumText(status), "Caption", "TextSecondary"); Grid.SetColumn(name, 1); line.Children.Add(name);
        var value = Label(() => count.ToString("N0", _locale.Culture), "Label"); Grid.SetColumn(value, 2); line.Children.Add(value);
        return line;
    }

    private Control StateCard()
    {
        var card = new SectionCard(Context, "overview.currentState") { Name = "CurrentStateCard" };
        var state = _project.State;
        card.Body.Children.Add(Label(() => string.IsNullOrWhiteSpace(state.Summary) ? T("date.notSet") : state.Summary, "Body", string.IsNullOrWhiteSpace(state.Summary) ? "TextTertiary" : "TextPrimary"));
        var lists = new AdaptiveGrid { MinItemWidth = 220 };
        lists.Children.Add(BulletList("presentation.openQuestions", state.OpenQuestions));
        lists.Children.Add(BulletList("presentation.followUps", state.FollowUps));
        card.Body.Children.Add(lists); return card;
    }

    private Control BulletList(string titleKey, IReadOnlyList<string> items)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(Label(() => $"{T(titleKey)} · {items.Count.ToString("N0", _locale.Culture)}", "Label", "TextSecondary"));
        if (items.Count == 0) panel.Children.Add(Label("date.notSet", "BodySmall", "TextTertiary"));
        foreach (var item in items.Take(5))
        {
            var text = item; var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 6 };
            line.Children.Add(PresentationTheme.Typeset(new TextBlock { Text = "•" }, "BodySmall", "TextTertiary"));
            var content = Label(() => text, "BodySmall"); Grid.SetColumn(content, 1); line.Children.Add(content); panel.Children.Add(line);
        }
        return panel;
    }

    private Control RequirementsCard()
    {
        var card = new SectionCard(Context, "tabs.requirements", SectionCard.Link(Context, "presentation.viewAll", () => { _selectTab(1); return Task.CompletedTask; })) { Name = "RequirementsCard" };
        foreach (var group in VcTemplate.Create().Groups)
        {
            var requirements = _project.Requirements.Where(item => item.GroupId == group.Id).ToList();
            var groupCard = new RequirementGroupCard(Context, group.Id, group.Title, requirements);
            foreach (var requirement in requirements) groupCard.Body.Children.Add(new RequirementSummaryRow(Context, requirement));
            card.Body.Children.Add(groupCard);
        }
        return card;
    }

    private Control TasksCard()
    {
        var card = new SectionCard(Context, "tasks.title", SectionCard.Link(Context, "presentation.viewAll", () => { _selectTab(2); return Task.CompletedTask; })) { Name = "DeadlinesCard" };
        card.Body.Children.Add(Label(() => F("overview.deadlines", _summary.OverdueTasks.Count, _summary.UpcomingTasks.Count), "Caption", "TextSecondary"));
        var active = _summary.OverdueTasks.Concat(_summary.UpcomingTasks).Take(5).ToList();
        if (active.Count == 0) card.Body.Children.Add(Label("presentation.noActiveTasks", "BodySmall", "TextTertiary"));
        foreach (var task in active)
        {
            var overdue = task.DueAt < DateTimeOffset.Now;
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
            line.Children.Add(TaskRow.StatusMark(task.Status, overdue));
            var title = Label(() => task.Title, "BodySmall"); title.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetColumn(title, 1); line.Children.Add(title);
            var due = Label(() => Context.ShortDate(task.DueAt), "Caption", overdue ? "Error" : "TextSecondary"); Grid.SetColumn(due, 2); line.Children.Add(due);
            card.Body.Children.Add(line);
        }
        card.Body.Children.Add(Label(() => _summary.NextMilestone is null ? T("overview.noMilestone") : F("overview.milestone", _summary.NextMilestone.Title, Due(_summary.NextMilestone.DueAt)), "Caption", "TextSecondary"));
        return card;
    }

    private Control AiCard()
    {
        var card = new SectionCard(Context, () => "✦  " + T("agent.title")) { Name = "AiDelegationCard" };
        card.Body.Children.Add(Label("presentation.aiHint", "Caption", "TextSecondary"));
        foreach (var preset in AgentPrompts.Actions)
        {
            var prompt = preset.Prompt;
            card.Body.Children.Add(new AiActionButton(Context, preset.Id, () => { _useQuickAction(prompt); return Task.CompletedTask; }));
        }
        return card;
    }

    private Control DetailsCard()
    {
        var card = new SectionCard(Context, "presentation.detailsTitle") { Name = "ProjectDetailsCard" };
        var panel = card.Body;
        var name = Input(_project.Name); var company = Input(_project.CompanyName); var owner = Input(_project.Owner);
        var notes = Input(_project.Notes, true); var stage = Choice(_project.Stage); var status = Choice(_project.Status);
        var fields = new AdaptiveGrid { MinItemWidth = 260 };
        foreach (var (label, input) in new (string, Control)[] { ("project.name", name), ("project.company", company), ("project.owner", owner), ("project.stage", stage), ("field.status", status) })
        {
            var group = new StackPanel { Spacing = 6 }; Field(group, label, input); fields.Children.Add(group);
        }
        panel.Children.Add(fields); Field(panel, "field.notes", notes);
        var state = Input(_project.State.Summary, true);
        var questions = Input(string.Join('\n', _project.State.OpenQuestions), true);
        var followups = Input(string.Join('\n', _project.State.FollowUps), true);
        Field(panel, "overview.currentState", state); Field(panel, "overview.questions", questions); Field(panel, "overview.followUps", followups);
        panel.Children.Add(Action("overview.save", async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) { ShowError("validation.projectNameRequired"); return; }
            _project.Name = name.Text.Trim(); _project.CompanyName = company.Text ?? ""; _project.Owner = owner.Text ?? "";
            _project.Stage = (ProjectStage)stage.SelectedItem!; _project.Status = (ProjectStatus)status.SelectedItem!; _project.Notes = notes.Text ?? "";
            _project.State.Summary = state.Text ?? "";
            _project.State.OpenQuestions = Lines(questions.Text); _project.State.FollowUps = Lines(followups.Text);
            await SaveAsync(_project); await OpenProjectAsync(_project.Id);
        }, "primary"));
        return card;
    }

    private static List<string> Lines(string? value) => (value ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
