using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>Project workspace: header, supported tabs (Overview, Requirements, Tasks, Delegate).</summary>
internal sealed class ProjectDetailView : PresentationView
{
    private readonly Project _project;
    public TabControl Tabs { get; } = new() { Name = "ProjectTabs" };
    public ProjectDetailView(PresentationContext context, Project project) : base(context) { _project = project; Spacing = 20; }

    public async Task LoadAsync(int selectedTab)
    {
        var delegation = new DelegationView(Context); await delegation.LoadAsync(_project);
        Children.Add(Header());
        Tabs.ItemsSource = new[]
        {
            Tab("tabs.overview", new OverviewView(Context, _project, prompt => { delegation.SetPrompt(prompt); Tabs.SelectedIndex = 3; }, tab => Tabs.SelectedIndex = tab)),
            Tab("tabs.requirements", new RequirementsView(Context, _project)),
            Tab("tabs.tasks", new TasksView(Context, _project)),
            Tab("tabs.delegate", delegation)
        };
        Tabs.SelectedIndex = selectedTab; Children.Add(Tabs);
    }

    private TabItem Tab(string key, Control content)
    {
        var tab = new TabItem { Content = content }; Bind(tab, control => control.Header = T(key)); return tab;
    }

    private Control Header()
    {
        var summary = ProjectSummaries.Summarize(_project, DateTimeOffset.Now);
        var header = new StackPanel { Spacing = 12, Name = "ProjectHeader" };
        var breadcrumb = Label(() => $"{T("navigation.projects")}  ›  {_project.Name}", "Caption", "TextTertiary");
        header.Children.Add(breadcrumb);
        var identity = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 14 };
        identity.Children.Add(new Avatar(_project.Name, size: 44));
        var names = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(Label(() => _project.Name, "HeadingLarge"));
        names.Children.Add(Label(() => _project.CompanyName, "Caption", "TextTertiary"));
        Grid.SetColumn(names, 1); identity.Children.Add(names); header.Children.Add(identity);

        var meta = new WrapPanel { Orientation = Orientation.Horizontal };
        meta.Children.Add(Meta("project.stage", new StatusPill(Context, () => EnumText(_project.Stage), "BrandPrimary")));
        meta.Children.Add(Meta("field.status", new StatusPill(Context, () => EnumText(_project.Status), StatusPill.Tone(_project.Status))));
        meta.Children.Add(Meta("project.owner", Label(() => string.IsNullOrWhiteSpace(_project.Owner) ? T("date.notSet") : _project.Owner, "Label")));
        meta.Children.Add(Meta("presentation.nextMilestone", Label(() => summary.NextMilestone is null ? T("overview.noMilestone")
            : $"{summary.NextMilestone.Title} · {Due(summary.NextMilestone.DueAt)}", "Label")));
        header.Children.Add(meta);
        return header;
    }

    private Control Meta(string labelKey, Control value)
    {
        var item = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 32, 8) };
        item.Children.Add(Label(labelKey, "Caption", "TextTertiary")); value.HorizontalAlignment = HorizontalAlignment.Left; item.Children.Add(value); return item;
    }
}
