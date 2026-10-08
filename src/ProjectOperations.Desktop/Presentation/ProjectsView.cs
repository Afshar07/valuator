using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop;

/// <summary>All projects as a table: stage, readiness (Complete only), next open deadline, owner and status.</summary>
internal sealed class ProjectsView : PresentationView
{
    private const string Columns = "2*,1.3*,1.4*,1*,0.9*,90,30";
    private const string CompactColumns = "2*,1.4*,1*,90,30";
    private readonly List<(Project Project, Control Row)> _rows = [];
    private readonly TextBox _filter = new() { Name = "ProjectFilter", MinWidth = 0, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), MinHeight = 0 };

    public ProjectsView(PresentationContext context) : base(context) { }

    public async Task LoadAsync()
    {
        var search = new Border { Width = 220, Height = 34, Padding = new Thickness(10, 0), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(PresentationTheme.RadiusControl) }
            .Paint(Border.BackgroundProperty, "BackgroundCard").Paint(Border.BorderBrushProperty, "BorderDefault");
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        searchRow.Children.Add(Icons.Glyph(Icons.MagnifyingGlass, 14, "TextTertiary"));
        _filter.Resources["TextControlBackgroundFocused"] = Brushes.Transparent; _filter.Resources["TextControlBackgroundPointerOver"] = Brushes.Transparent;
        _filter.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        Bind(_filter, control => { control.Watermark = T("projects.filter"); AutomationProperties.SetName(control, T("projects.filter")); });
        _filter.TextChanged += (_, _) => ApplyFilter();
        Grid.SetColumn(_filter, 1); searchRow.Children.Add(_filter); search.Child = searchRow;
        var create = Context.IconAction("project.new", Icons.Plus, () => { ShowCreate(); return Task.CompletedTask; }, "primary", IconWeight.Bold);
        create.Name = "NewProjectButton"; create.MinHeight = 34;
        Children.Add(PageHeader("navigation.projects", null, search, create));

        var compact = false;
        var header = new TableHeader(Context, Columns, "project.column.project", "project.column.stage", "project.column.readiness", "project.column.next", "project.column.owner", "project.column.status", "");
        var card = new ListCard(Context, columnHeader: header) { Name = "ProjectsCard" };
        var now = DateTimeOffset.Now;
        foreach (var project in await _projects.ListAsync())
        {
            var row = new ProjectRow(Context, project, now, () => OpenProjectAsync(project.Id));
            _rows.Add((project, row)); card.Add(row);
        }
        if (_rows.Count == 0) card.Add(new Border { Padding = new Thickness(16, 14), Child = Label("dashboard.empty", "Body", "TextSecondary") });
        Children.Add(card);
        card.SizeChanged += (_, e) =>
        {
            var next = e.NewSize.Width < 640;
            if (next == compact) return;
            compact = next;
            foreach (var grid in new[] { (Grid)header.Child! }.Concat(_rows.Select(row => (Grid)((Button)row.Row).Content!)))
                ProjectRow.SetCompact(grid, compact);
        };
    }

    private void ApplyFilter()
    {
        var query = _filter.Text?.Trim() ?? "";
        foreach (var (project, row) in _rows)
            row.Parent!.SetValue(IsVisibleProperty, query.Length == 0 || project.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || project.CompanyName.Contains(query, StringComparison.CurrentCultureIgnoreCase) || project.Owner.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private void ShowCreate() => Context.Shell.ShowWizard(fromWelcome: false);

    private sealed class ProjectRow : ListRow
    {
        public ProjectRow(PresentationContext context, Project project, DateTimeOffset now, Func<Task> action)
            : base(context, Layout(context, project, now), () => $"{project.Name} · {project.CompanyName} · {project.Stage.Title} · {context.EnumText(project.Status)} · {project.Owner}", action)
        { Name = "ProjectRow"; Padding = new Thickness(16, 12); }

        public static void SetCompact(Grid grid, bool compact)
        {
            grid.ColumnDefinitions = new ColumnDefinitions(compact ? CompactColumns : Columns);
            foreach (var child in grid.Children)
            {
                if (!child.IsSet(OriginalColumn)) child.SetValue(OriginalColumn, Grid.GetColumn(child));
                var column = (int)child.GetValue(OriginalColumn);
                child.IsVisible = !compact || column is not (1 or 4);
                Grid.SetColumn(child, compact ? column switch { 0 => 0, 2 => 1, 3 => 2, 5 => 3, 6 => 4, _ => 0 } : column);
            }
        }
        private static readonly AttachedProperty<int> OriginalColumn = AvaloniaProperty.RegisterAttached<ProjectRow, Control, int>("OriginalColumn");

        private static Grid Layout(PresentationContext context, Project project, DateTimeOffset now)
        {
            var summary = ProjectSummaries.Summarize(project, now);
            var next = ProjectSummaries.NextDeadline(project, now);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), ColumnSpacing = 16 };
            var identity = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
            identity.Children.Add(Ui.Initial(project.Name));
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(context.Label(() => project.Name, "BodyStrong"));
            if (!string.IsNullOrWhiteSpace(project.CompanyName)) names.Children.Add(context.Label(() => project.CompanyName, "Caption", "TextSecondary"));
            Grid.SetColumn(names, 1); identity.Children.Add(names);
            Place(grid, identity, 0);
            Place(grid, Ui.StagePill(context, project.Stage), 1);
            var readiness = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
            readiness.Children.Add(Ui.Progress(summary.CompletionPercentage));
            var ratio = context.Label(() => $"{context.Number(summary.CompleteRequirements)}/{context.Number(summary.TotalRequirements)}", "Caption", "TextSecondary");
            ratio.MinWidth = 40; ratio.TextWrapping = TextWrapping.NoWrap; ratio.FlowDirection = FlowDirection.LeftToRight; ratio.TextAlignment = TextAlignment.Right; Grid.SetColumn(ratio, 1); readiness.Children.Add(ratio);
            Place(grid, readiness, 2);
            var deadline = context.Label(() => next is null ? context.Text.Get("date.notSet") : context.ShortDate(next.DueAt), next?.IsOverdue == true ? "BodyStrong" : "Body", next?.IsOverdue == true ? "Error" : "TextSecondary");
            Place(grid, Cell(deadline), 3);
            Place(grid, Cell(context.Label(() => string.IsNullOrWhiteSpace(project.Owner) ? "—" : project.Owner, "Body", "TextSecondary")), 4);
            Place(grid, Ui.Pill(context, () => context.EnumText(project.Status), StatusVisuals.Project(project.Status)), 5);
            // The delete button sits inside the row button; it handles its own click, so the row does not open the project.
            var delete = context.IconAction("v3.delProject", Icons.Trash, () => { context.Shell.ShowModal(new DeleteProjectDialog(context, project)); return Task.CompletedTask; }, "dangerGhost", iconOnly: true);
            delete.Click += (_, e) => e.Handled = true; // Click bubbles: keep the row from also opening the project.
            delete.Name = "DeleteProjectButton"; delete.Width = 30; delete.Height = 30; delete.MinHeight = 30; delete.Padding = new Thickness(0);
            delete.HorizontalAlignment = HorizontalAlignment.Center; delete.VerticalAlignment = VerticalAlignment.Center;
            delete.HorizontalContentAlignment = HorizontalAlignment.Center; delete.VerticalContentAlignment = VerticalAlignment.Center;
            Place(grid, delete, 6);
            return grid;
        }
        private static TextBlock Cell(TextBlock text) { text.VerticalAlignment = VerticalAlignment.Center; text.TextTrimming = TextTrimming.CharacterEllipsis; return text; }
        private static void Place(Grid grid, Control control, int column) { control.SetValue(OriginalColumn, column); Grid.SetColumn(control, column); grid.Children.Add(control); }
    }
}
