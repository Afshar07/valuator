using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Overview tab: explicit current state, missing/unreviewed items, open questions, follow-ups and project editing.</summary>
internal sealed class OverviewView : PresentationView
{
    private readonly Project _project;
    private readonly ProjectSummary _summary;

    public OverviewView(PresentationContext context, Project project) : base(context)
    {
        _project = project; Spacing = 16;
        _summary = ProjectSummaries.Summarize(project, DateTimeOffset.Now);
        var cards = new AdaptiveGrid { MinItemWidth = 300 };
        cards.Children.Add(StateCard());
        cards.Children.Add(MissingCard());
        cards.Children.Add(ListCard("presentation.openQuestions", Icons.Question, project.State.OpenQuestions, "OpenQuestionsCard"));
        cards.Children.Add(ListCard("presentation.followUps", Icons.ArrowBendUpRight, project.State.FollowUps, "FollowUpsCard"));
        Children.Add(cards);
    }

    private Control StateCard()
    {
        var card = new SectionCard(Context, "overview.currentState") { Name = "CurrentStateCard" };
        card.Body.Spacing = 0; card.Body.Children[0].Margin = new Thickness(0, 0, 0, 8);
        void Fact(Func<string> key, Func<string> value, string color = "TextPrimary")
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
            row.Children.Add(Label(key, "Body", "TextSecondary"));
            var text = Label(value, "BodyStrong", color); text.TextWrapping = TextWrapping.NoWrap; text.TextAlignment = TextAlignment.End; Grid.SetColumn(text, 1); row.Children.Add(text);
            card.Body.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 8), Child = row }));
        }
        var stageRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Name = "OverviewStage" };
        stageRow.Children.Add(Label(() => T("project.stage"), "Body", "TextSecondary"));
        var stagePill = Ui.StagePill(Context, _project.Stage); stagePill.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(stagePill, 1); stageRow.Children.Add(stagePill);
        card.Body.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 8), Child = stageRow }));
        Fact(() => T("presentation.nextMilestone"), () => _summary.NextMilestone is null ? T("overview.noMilestone") : $"{_summary.NextMilestone.Title} · {Context.ShortDate(_summary.NextMilestone.DueAt)}");
        Fact(() => T("project.column.readiness"), () => $"{N(_summary.CompleteRequirements)}/{N(_summary.TotalRequirements)}");
        Fact(() => T("presentation.stat.overdue"), () => N(_summary.OverdueTasks.Count), _summary.OverdueTasks.Count > 0 ? "Error" : "TextPrimary");
        Fact(() => T("presentation.openQuestions"), () => N(_project.State.OpenQuestions.Count));
        Fact(() => T("presentation.followUps"), () => N(_project.State.FollowUps.Count));
        foreach (var (key, value) in new[] { ("overview.summary", (Func<string>)(() => _project.State.Summary)), ("field.notes", () => _project.Notes) })
        {
            if (key == "overview.summary" && string.IsNullOrWhiteSpace(value())) continue;
            var section = new StackPanel { Spacing = 6 };
            section.Children.Add(Label(key, "Body", "TextSecondary"));
            section.Children.Add(Label(() => string.IsNullOrWhiteSpace(value()) ? T("date.notSet") : value(), "Body", string.IsNullOrWhiteSpace(value()) ? "TextTertiary" : "TextPrimary"));
            card.Body.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 12, 0, 4), Child = section }));
        }
        return card;
    }

    private Control MissingCard()
    {
        var card = new SectionCard(Context, "overview.missing", count: () => N(_summary.MissingRequirements.Count)) { Name = "MissingCard" };
        card.Body.Spacing = 0; card.Body.Children[0].Margin = new Thickness(0, 0, 0, 8);
        if (_summary.MissingRequirements.Count == 0) card.Body.Children.Add(Label("overview.noMissing", "Body", "TextTertiary"));
        foreach (var requirement in _summary.MissingRequirements)
        {
            var (icon, weight, tone) = StatusVisuals.Requirement(requirement.Status);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            row.Children.Add(Icons.Glyph(icon, 16, tone.Foreground, weight));
            var title = Label(() => RequirementTitle(requirement), "Body"); title.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(title, 1); row.Children.Add(title);
            var status = Label(() => EnumText(requirement.Status), "Meta", tone.Foreground); status.VerticalAlignment = VerticalAlignment.Center; status.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(status, 2); row.Children.Add(status);
            card.Body.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 7), Child = row }));
        }
        return card;
    }

    /// <summary>Open questions / follow-ups list with an inline add line that saves to the project's explicit state.</summary>
    private Control ListCard(string titleKey, string icon, List<string> items, string name)
    {
        var host = new ContentControl();
        var add = Context.IconAction("overview.add", Icons.Plus, () => { host.Content = AddLine(titleKey, items, () => host.Content = null); return Task.CompletedTask; });
        add.MinHeight = 26; add.Padding = new Thickness(9, 0); add.FontSize = 12;
        var card = new SectionCard(Context, titleKey, add, () => N(items.Count)) { Name = name };
        card.Body.Spacing = 0; card.Body.Children[0].Margin = new Thickness(0, 0, 0, 8);
        if (items.Count == 0) card.Body.Children.Add(Label("date.notSet", "Body", "TextTertiary"));
        foreach (var item in items)
        {
            var text = item;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            var glyph = Icons.Glyph(icon, 15, "TextTertiary"); glyph.VerticalAlignment = VerticalAlignment.Top; glyph.Margin = new Thickness(0, 2, 0, 0); row.Children.Add(glyph);
            var label = Label(() => text, "Body"); Grid.SetColumn(label, 1); row.Children.Add(label);
            card.Body.Children.Add(Ui.Separated(new Border { Padding = new Thickness(0, 7), Child = row }));
        }
        card.Body.Children.Add(host);
        return card;
    }

    private Control AddLine(string titleKey, List<string> items, Action close)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        var input = Input(); Bind(input, control => control.Watermark = T(titleKey));
        panel.Children.Add(input);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(Action("overview.addSave", async () =>
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            items.Add(input.Text.Trim()); await SaveAsync(_project); await OpenProjectAsync(_project.Id, 0);
        }, "primary"));
        actions.Children.Add(Action("action.cancel", () => { close(); return Task.CompletedTask; }));
        panel.Children.Add(actions);
        input.AttachedToVisualTree += (_, _) => input.Focus();
        return panel;
    }

}
