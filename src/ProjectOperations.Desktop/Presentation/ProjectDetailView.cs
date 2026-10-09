using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop;

/// <summary>Project workspace: header, then Overview, Requirements & files, Tasks & dates, and Delegate & review tabs.</summary>
internal sealed class ProjectDetailView : PresentationView
{
    private readonly Project _project;
    public TabControl Tabs { get; } = new() { Name = "ProjectTabs" };
    public ProjectDetailView(PresentationContext context, Project project) : base(context) { _project = project; Spacing = 16; }

    public async Task LoadAsync(ProjectTab selectedTab)
    {
        var jobs = await _agents.HistoryAsync(_project.Id);
        Children.Add(Header());
        Tabs.ItemsSource = new[]
        {
            Tab("tabs.overview", new OverviewView(Context, _project)),
            Tab("tabs.requirements", new RequirementsView(Context, _project)),
            Tab("tabs.tasks", new ContentControl { Content = new Features.Tasks.TasksViewModel(_project, jobs, Context.ProjectScreen) }),
            Tab("tabs.delegate", new DelegationView(Context, _project, jobs))
        };
        Tabs.SelectedIndex = (int)selectedTab;
        Tabs.SelectionChanged += (_, _) => { if (Tabs.SelectedIndex == (int)ProjectTab.Delegate) Context.Shell.SetAssistantOpen(true); };
        // The tab strip's baseline rule sits behind the selected underline.
        var host = new Panel();
        host.Children.Add(new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 35, 0, 0) }.Paint(Border.BackgroundProperty, "BorderDefault"));
        host.Children.Add(Tabs);
        Children.Add(host);
    }

    private TabItem Tab(string key, Control content)
    {
        var tab = new TabItem { Content = content }; Bind(tab, control => control.Header = T(key)); return tab;
    }

    private Control Header()
    {
        var summary = ProjectSummaries.Summarize(_project, DateTimeOffset.Now);
        var header = new StackPanel { Spacing = 10, Name = "ProjectHeader" };
        var back = Context.IconAction("navigation.projects", _locale.LanguageCode == "fa" ? Icons.ArrowRight : Icons.ArrowLeft, () => Context.Shell.NavigateAsync(AppPage.Projects), "link");
        Bind(back, control => ((TextBlock)((StackPanel)control.Content!).Children[0]).Text = _locale.LanguageCode == "fa" ? Icons.ArrowRight : Icons.ArrowLeft);
        header.Children.Add(back);

        // Identity on the start side; readiness and the assistant toggle on the end side, dropping below together when narrow.
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 16 };
        var identity = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Bottom };
        var titleLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var title = Label(() => _project.Name, "Title"); title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis; title.Name = "ProjectTitle";
        titleLine.Children.Add(title);
        var stage = Ui.StagePill(Context, _project.Stage); stage.Name = "StagePill"; titleLine.Children.Add(stage);
        if (_project.Status != ProjectStatus.Active) titleLine.Children.Add(Ui.Pill(Context, () => EnumText(_project.Status), Tone.Neutral));
        var edit = Context.IconAction("v3.editProject", Icons.PencilSimple, async () => Context.Shell.ShowModal(new EditProjectDialog(Context, _project, await Context.Projects.ListStagesAsync(_project.TemplateId))), "square", iconOnly: true);
        edit.Name = "EditProjectButton"; edit.Width = 30; edit.Height = 30; edit.VerticalAlignment = VerticalAlignment.Center;
        edit.Paint(Button.ForegroundProperty, "TextSecondary");
        titleLine.Children.Add(edit);
        // Long names end in an ellipsis instead of wrapping; the pills and edit button stay beside the title.
        identity.SizeChanged += (_, e) => title.MaxWidth = Math.Max(80, e.NewSize.Width - titleLine.Children.Where(child => child != title).Sum(child => child.DesiredSize.Width) - titleLine.Spacing * (titleLine.Children.Count - 1));
        identity.Children.Add(titleLine);
        identity.Children.Add(Label(() => string.Join(" · ", new[] { _project.CompanyName, _project.Owner }.Where(part => !string.IsNullOrWhiteSpace(part))), "Body", "TextSecondary"));
        row.Children.Add(identity);

        var end = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Bottom };
        var readiness = new StackPanel { Spacing = 6, Width = 220, VerticalAlignment = VerticalAlignment.Center, Name = "ReadinessSummary" };
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        line.Children.Add(Label("project.column.readiness", "Caption", "TextSecondary"));
        var ratio = Label(() => $"{N(summary.CompleteRequirements)}/{N(summary.TotalRequirements)}", "CaptionMedium"); ratio.Name = "ReadinessRatio";
        Bind(ratio, control => ToolTip.SetTip(control, F("overview.readiness", summary.CompleteRequirements, summary.TotalRequirements, summary.CompletionPercentage)));
        ratio.FontWeight = FontWeight.SemiBold; ratio.TextWrapping = TextWrapping.NoWrap; Grid.SetColumn(ratio, 1); line.Children.Add(ratio);
        readiness.Children.Add(line); readiness.Children.Add(Ui.Progress(summary.CompletionPercentage, 6));
        end.Children.Add(readiness);
        end.Children.Add(AssistantButton());
        Grid.SetColumn(end, 1); row.Children.Add(end);
        row.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 680;
            Grid.SetColumn(end, narrow ? 0 : 1); Grid.SetRow(end, narrow ? 1 : 0);
            end.Margin = new Thickness(0, narrow ? 12 : 0, 0, 0); end.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        };
        header.Children.Add(row);
        return header;
    }
}
