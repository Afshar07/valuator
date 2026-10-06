using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Responsive equal- or weighted-column panel. It wraps by available width instead of by breakpoint classes.</summary>
internal sealed class AdaptiveGrid : Panel
{
    public double MinItemWidth { get; set; } = 220;
    public double Gap { get; set; } = 16;
    /// <summary>Optional relative column widths, used only when every child fits on a single row.</summary>
    public double[]? Weights { get; set; }

    private List<Control> Items => Children.Where(child => child.IsVisible).ToList();
    private int Columns(double width, int count)
    {
        if (count == 0) return 1;
        if (double.IsInfinity(width)) return count;
        return Math.Clamp((int)Math.Floor((width + Gap) / (MinItemWidth + Gap)), 1, count);
    }
    private double[] ColumnWidths(double width, int columns, int count)
    {
        if (double.IsInfinity(width)) return Enumerable.Repeat(MinItemWidth, columns).ToArray();
        var available = Math.Max(0, width - Gap * (columns - 1));
        var weights = Weights is not null && columns == count && Weights.Length == columns ? Weights : Enumerable.Repeat(1d, columns).ToArray();
        return weights.Select(weight => available * weight / weights.Sum()).ToArray();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Items; var columns = Columns(availableSize.Width, items.Count); var widths = ColumnWidths(availableSize.Width, columns, items.Count);
        double height = 0, width = 0;
        for (var start = 0; start < items.Count; start += columns)
        {
            double row = 0, rowWidth = 0;
            for (var column = 0; column < columns && start + column < items.Count; column++)
            {
                items[start + column].Measure(new Size(widths[column], double.PositiveInfinity));
                row = Math.Max(row, items[start + column].DesiredSize.Height); rowWidth += widths[column] + (column > 0 ? Gap : 0);
            }
            height += row + (start > 0 ? Gap : 0); width = Math.Max(width, rowWidth);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var items = Items; var columns = Columns(finalSize.Width, items.Count); var widths = ColumnWidths(finalSize.Width, columns, items.Count);
        double y = 0;
        for (var start = 0; start < items.Count; start += columns)
        {
            double x = 0, row = 0;
            for (var column = 0; column < columns && start + column < items.Count; column++)
                row = Math.Max(row, items[start + column].DesiredSize.Height);
            for (var column = 0; column < columns && start + column < items.Count; column++)
            {
                items[start + column].Arrange(new Rect(x, y, widths[column], row)); x += widths[column] + Gap;
            }
            y += row + Gap;
        }
        return finalSize;
    }
}

internal sealed class Avatar : Border
{
    public Avatar(string name, string tone = "BrandPrimary", string soft = "BrandSoft", double size = 34)
    {
        Width = size; Height = size; CornerRadius = PresentationTheme.Radius(size / 2); Background = PresentationTheme.Brush(soft); VerticalAlignment = VerticalAlignment.Center;
        var initial = string.IsNullOrWhiteSpace(name) ? "△" : name.Trim().EnumerateRunes().First().ToString();
        Child = PresentationTheme.Typeset(new TextBlock { Text = initial, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, "HeadingSmall", tone);
    }
}

internal sealed class SectionCard : Border
{
    public StackPanel Body { get; } = new() { Spacing = 12 };
    public SectionCard(PresentationContext context, string title, Control? action = null) : this(context, () => context.Text.Get(title), action) { }
    public SectionCard(PresentationContext context, Func<string> title, Control? action = null)
    {
        Classes.Add("sectionCard"); Background = PresentationTheme.Brush("BackgroundCard"); BorderBrush = PresentationTheme.Brush("BorderDefault");
        BorderThickness = new Thickness(1); CornerRadius = PresentationTheme.Radius(PresentationTheme.RadiusLarge); Padding = new Thickness(20);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, MinHeight = 32 };
        var heading = context.Heading(title, "HeadingSmall"); heading.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(heading);
        if (action is not null) { action.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(action, 1); header.Children.Add(action); }
        Body.Children.Add(header); Child = Body;
    }
    public static Button Link(PresentationContext context, string key, Func<Task> action)
    {
        var button = context.Action(key, action); button.Classes.Add("link"); return button;
    }
}

internal sealed class StatCard : Border
{
    public StatCard(PresentationContext context, string name, Func<string> title, int value, string tone, Func<string> hint)
    {
        Name = name; MinHeight = 116;
        Background = PresentationTheme.Brush("BackgroundCard"); BorderBrush = PresentationTheme.Brush("BorderDefault"); BorderThickness = new Thickness(1);
        CornerRadius = PresentationTheme.Radius(PresentationTheme.RadiusLarge); Padding = new Thickness(16);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 12, ColumnSpacing = 12 };
        var icon = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = PresentationTheme.Radius(9),
            Background = PresentationTheme.Brush(PresentationTheme.SoftToken(tone)),
            Child = new Ellipse { Width = 10, Height = 10, Fill = PresentationTheme.Brush(tone), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        grid.Children.Add(icon);
        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(context.Label(title, "Label")); labels.Children.Add(context.Label(hint, "Caption", tone));
        Grid.SetColumn(labels, 1); grid.Children.Add(labels);
        var number = context.Heading(() => value.ToString("N0", context.Locale.Culture), "HeadingMedium"); number.Name = "StatValue";
        Grid.SetRow(number, 1); Grid.SetColumnSpan(number, 2); grid.Children.Add(number);
        Child = grid;
    }
}

internal sealed class StatusPill : Border
{
    public StatusPill(PresentationContext context, Func<string> caption, string tone)
    {
        Background = PresentationTheme.Brush(PresentationTheme.SoftToken(tone)); CornerRadius = PresentationTheme.Radius(PresentationTheme.RadiusPill);
        Padding = new Thickness(12, 5); HorizontalAlignment = HorizontalAlignment.Left; VerticalAlignment = VerticalAlignment.Center;
        var text = context.Label(caption, "Label", tone); text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis; Child = text;
    }
    public static string Tone(RequirementStatus status) => status switch
    {
        RequirementStatus.Missing => "Error",
        RequirementStatus.Provided => "BrandPrimary",
        RequirementStatus.NeedsReview => "Warning",
        _ => "Success"
    };
    public static string Tone(ProjectStatus status) => status switch
    {
        ProjectStatus.Active => "Success",
        ProjectStatus.OnHold => "Warning",
        ProjectStatus.Completed => "BrandPrimary",
        _ => "Neutral"
    };
    public static string Tone(ProjectTaskStatus status, bool overdue) => overdue ? "Error" : status switch
    {
        ProjectTaskStatus.Done => "Success",
        ProjectTaskStatus.InProgress => "BrandPrimary",
        ProjectTaskStatus.Cancelled => "Neutral",
        _ => "Neutral"
    };
}

/// <summary>Progress meter that mirrors with the inherited flow direction.</summary>
internal sealed class ReadinessBar : Grid
{
    public ReadinessBar(double fraction, string tone = "Success")
    {
        fraction = Math.Clamp(double.IsNaN(fraction) ? 0 : fraction, 0, 1); Height = 8; VerticalAlignment = VerticalAlignment.Center;
        ColumnDefinitions.Add(new ColumnDefinition(fraction, GridUnitType.Star)); ColumnDefinitions.Add(new ColumnDefinition(1 - fraction, GridUnitType.Star));
        Children.Add(new Border { Background = PresentationTheme.Brush("BackgroundTrack"), CornerRadius = PresentationTheme.Radius(4), [Grid.ColumnSpanProperty] = 2 });
        if (fraction > 0) Children.Add(new Border { Background = PresentationTheme.Brush(tone), CornerRadius = PresentationTheme.Radius(4) });
    }
}

internal sealed class ReadinessRing : Panel
{
    public ReadinessRing(double percent, PresentationContext context)
    {
        Width = 68; Height = 68; var sweep = Math.Clamp(percent, 0, 100) * 3.6;
        Children.Add(new Ellipse { Stroke = PresentationTheme.Brush("BackgroundTrack"), StrokeThickness = 8, Width = 60, Height = 60 });
        if (sweep > 0) Children.Add(new Arc
        {
            Stroke = PresentationTheme.Brush("Success"),
            StrokeThickness = 8,
            StrokeLineCap = PenLineCap.Round,
            Width = 60,
            Height = 60,
            StartAngle = -90,
            SweepAngle = Math.Min(sweep, 359.9)
        });
        var label = PresentationTheme.Typeset(new TextBlock { Text = percent.ToString("0", context.Locale.Culture) + "%", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.LeftToRight }, "HeadingSmall");
        Children.Add(label);
    }
}

/// <summary>Base whole-row button: leading visual, title/subtitle, optional status pill and trailing value.</summary>
internal class ListRow : Button
{
    protected override Type StyleKeyOverride => typeof(Button);
    public ListRow(PresentationContext context, Control leading, Func<string> title, Func<string>? subtitle, Func<string>? pill, string tone,
        Func<string>? trailing, string trailingTone, Func<string> accessibleName, Func<Task> action)
    {
        Classes.Add("row");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(leading);
        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var heading = context.Label(title, "Label"); heading.FontWeight = FontWeight.SemiBold; heading.FontSize = 13; labels.Children.Add(heading);
        if (subtitle is not null) labels.Children.Add(context.Label(subtitle, "Caption", "TextTertiary"));
        Grid.SetColumn(labels, 1); grid.Children.Add(labels);
        if (pill is not null) { var item = new StatusPill(context, pill, tone); Grid.SetColumn(item, 2); grid.Children.Add(item); }
        if (trailing is not null)
        {
            var value = context.Label(trailing, "Label", trailingTone); value.TextWrapping = TextWrapping.NoWrap; value.VerticalAlignment = VerticalAlignment.Center; value.MinWidth = 44;
            value.TextAlignment = TextAlignment.End; Grid.SetColumn(value, 3); grid.Children.Add(value);
        }
        Content = grid; context.Localized.Bind(this, control => AutomationProperties.SetName(control, accessibleName()));
        Click += async (_, _) => await context.ActAsync(this, action);
    }
}

internal sealed class ProjectRow : ListRow
{
    public ProjectRow(PresentationContext context, Project project, Func<Task> action, Func<string>? trailing = null, bool statusPill = false)
        : base(context, new Avatar(project.Name), () => project.Name,
            () => statusPill ? Join(project.CompanyName, context.EnumText(project.Stage)) : project.CompanyName,
            () => statusPill ? context.EnumText(project.Status) : context.EnumText(project.Stage),
            statusPill ? StatusPill.Tone(project.Status) : "BrandPrimary", trailing, "TextPrimary",
            () => $"{project.Name} · {project.CompanyName} · {context.EnumText(project.Stage)} · {context.EnumText(project.Status)} · {project.Owner}", action)
    { Name = "ProjectRow"; }
    private static string Join(string first, string second) => string.IsNullOrWhiteSpace(first) ? second : first + " · " + second;

    /// <summary>Row for attention items that belong to a project but are not the project itself.</summary>
    public ProjectRow(PresentationContext context, string projectName, Func<string> title, Func<string>? detail, Func<string> pill, string tone,
        Func<string> dueText, Func<string> accessibleName, Func<Task> action)
        : base(context, new Avatar(projectName), title, detail, pill, tone, dueText, tone, accessibleName, action) { Name = "PriorityRow"; }
}

internal sealed class RequirementGroupCard : Border
{
    public StackPanel Body { get; } = new() { Spacing = 2 };
    public RequirementGroupCard(PresentationContext context, string id, string title, IReadOnlyList<ProjectRequirement> requirements)
    {
        Name = "RequirementGroup"; Padding = new Thickness(16, 12); CornerRadius = PresentationTheme.Radius(PresentationTheme.RadiusMedium);
        Background = PresentationTheme.Brush("BackgroundMuted"); BorderBrush = PresentationTheme.Brush("BorderDefault"); BorderThickness = new Thickness(1);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 6) };
        header.Children.Add(context.Heading(() => new DomainDisplay(context.Text).Group(id, title), "Label"));
        var count = context.Label(() => context.Text.Format("presentation.groupComplete", requirements.Count(item => item.Status == RequirementStatus.Complete), requirements.Count), "Caption", "TextSecondary");
        Grid.SetColumn(count, 1); header.Children.Add(count); Body.Children.Add(header); Child = Body;
    }
}

/// <summary>Editable requirement row (opens the existing editor).</summary>
internal sealed class RequirementRow : Button
{
    protected override Type StyleKeyOverride => typeof(Button);
    public RequirementRow(PresentationContext context, ProjectRequirement requirement, Func<Task> action)
    {
        Name = "RequirementRow"; Classes.Add("row");
        string Title() => new DomainDisplay(context.Text).Requirement(requirement);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(context.Label(Title, "Label"));
        labels.Children.Add(context.Label(() => context.Text.Format("presentation.requirementFiles", context.EnumText(requirement.Type), requirement.Files.Count), "Caption", "TextTertiary"));
        grid.Children.Add(labels);
        var pill = new StatusPill(context, () => context.EnumText(requirement.Status), StatusPill.Tone(requirement.Status)); Grid.SetColumn(pill, 1); grid.Children.Add(pill); Content = grid;
        context.Localized.Bind(this, control => AutomationProperties.SetName(control, context.Text.Format("requirement.summary", Title(), context.EnumText(requirement.Type), context.EnumText(requirement.Status), requirement.Files.Count)));
        Click += async (_, _) => await context.ActAsync(this, action);
    }
}

/// <summary>Read-only requirement line used by the overview summary.</summary>
internal sealed class RequirementSummaryRow : Grid
{
    public RequirementSummaryRow(PresentationContext context, ProjectRequirement requirement)
    {
        Name = "RequirementSummaryRow"; ColumnDefinitions = new ColumnDefinitions("*,Auto"); ColumnSpacing = 12; Margin = new Thickness(0, 4);
        Children.Add(context.Label(() => new DomainDisplay(context.Text).Requirement(requirement), "BodySmall"));
        var pill = new StatusPill(context, () => context.EnumText(requirement.Status), StatusPill.Tone(requirement.Status)); Grid.SetColumn(pill, 1); Children.Add(pill);
    }
}

internal sealed class TaskRow : Grid
{
    public TaskRow(PresentationContext context, ProjectTask task, Func<Task> edit, Func<Task> complete)
    {
        Name = "TaskRow"; ColumnDefinitions = new ColumnDefinitions("*,Auto"); ColumnSpacing = 8;
        var active = task.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress;
        var overdue = active && task.DueAt < DateTimeOffset.Now;
        var tone = StatusPill.Tone(task.Status, overdue);
        Children.Add(new ListRow(context, StatusMark(task.Status, overdue), () => task.Title, () => context.Due(task.DueAt), () => context.EnumText(task.Status), tone, null, tone,
            () => $"{task.Title} · {context.EnumText(task.Status)} · {context.Due(task.DueAt)}", edit));
        if (active)
        {
            var done = context.Action("task.complete", complete); done.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(done, 1); Children.Add(done);
        }
    }
    public static Control StatusMark(ProjectTaskStatus status, bool overdue)
    {
        var tone = overdue ? "Error" : status == ProjectTaskStatus.Done ? "Success" : "TextTertiary";
        var glyph = status == ProjectTaskStatus.Done ? "✓" : overdue ? "●" : "○";
        return PresentationTheme.Typeset(new TextBlock { Text = glyph, Width = 20, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, "Label", tone);
    }
}

internal sealed class AiActionButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);
    public AiActionButton(PresentationContext context, string id, Func<Task> action)
    {
        Name = "AiAction_" + id; Classes.Add("aiAction");
        context.Localized.Bind(this, control => control.Content = "✦  " + context.Text.Get("agent.action." + id));
        context.Localized.Bind(this, control => AutomationProperties.SetName(control, context.Text.Get("agent.action." + id)));
        Click += async (_, _) => await context.ActAsync(this, action);
    }
}

internal sealed class AppSidebar : Border
{
    private readonly Button _dashboard;
    private readonly Button _projects;
    public AppSidebar(PresentationContext context, Func<Task> dashboard, Func<Task> projects)
    {
        Name = "AppSidebar"; Background = PresentationTheme.Brush("BackgroundSidebar"); Padding = new Thickness(16, 24); Width = 208;
        var dock = new DockPanel();
        var footer = new StackPanel { Spacing = 12 };
        footer.Children.Add(WorkspaceCard(context));
        var language = new ComboBox { Name = "LanguageSelector", ItemsSource = new[] { "English", "فارسی" }, SelectedIndex = context.Locale.LanguageCode == "fa" ? 1 : 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        language.SelectionChanged += (_, _) =>
        {
            try { context.Locale.SetLanguage(language.SelectedIndex == 1 ? "fa" : "en"); }
            catch (Exception) { language.SelectedIndex = context.Locale.LanguageCode == "fa" ? 1 : 0; context.ShowError("validation.languageSaveFailed"); }
        };
        context.Localized.Bind(language, control => control.SelectedIndex = context.Locale.LanguageCode == "fa" ? 1 : 0);
        footer.Children.Add(language); DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        var entries = new StackPanel { Spacing = 8 };
        var brand = context.Heading(() => "△  " + context.Text.Get("app.title"), "HeadingSmall"); brand.FontSize = 13; brand.Margin = new Thickness(8, 4, 8, 28); brand.TextWrapping = TextWrapping.NoWrap; entries.Children.Add(brand);
        _dashboard = Entry(context, "navigation.attention", "NavigationDashboard", dashboard);
        _projects = Entry(context, "navigation.projects", "NavigationProjects", projects);
        entries.Children.Add(_dashboard); entries.Children.Add(_projects);
        foreach (var id in new[] { "Calendar", "Documents", "Delegation" }) entries.Children.Add(Entry(context, "presentation.navigation" + id, "Navigation" + id, null));
        entries.Children.Add(new Border { Height = 1, Background = PresentationTheme.Brush("BorderSubtle"), Margin = new Thickness(4, 12) });
        entries.Children.Add(Entry(context, "presentation.navigationSettings", "NavigationSettings", null));
        dock.Children.Add(entries); Child = dock;
    }
    private static Control WorkspaceCard(PresentationContext context)
    {
        var card = new Border { Background = PresentationTheme.Brush("BackgroundCard"), BorderBrush = PresentationTheme.Brush("BorderDefault"), BorderThickness = new Thickness(1), CornerRadius = PresentationTheme.Radius(PresentationTheme.RadiusMedium), Padding = new Thickness(12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
        grid.Children.Add(new Avatar("△", "OnAccent", "Accent", 30));
        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(context.Label("presentation.workspace", "Label")); labels.Children.Add(context.Label("presentation.localMvp", "Caption", "TextTertiary"));
        Grid.SetColumn(labels, 1); grid.Children.Add(labels); card.Child = grid; return card;
    }
    private static Button Entry(PresentationContext context, string key, string name, Func<Task>? action)
    {
        var button = new Button { Name = name, IsEnabled = action is not null };
        button.Classes.Add("nav");
        context.Localized.Bind(button, control =>
        {
            var caption = context.Text.Get(key);
            AutomationProperties.SetName(control, caption);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(PresentationTheme.Typeset(new TextBlock { Text = action is null ? "○" : "●", FontSize = 11, VerticalAlignment = VerticalAlignment.Center }, "Caption", action is null ? "TextTertiary" : "BrandPrimary"));
            row.Children.Add(new TextBlock { Text = caption, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            control.Content = row;
        });
        if (action is not null) button.Click += async (_, _) => await context.ActAsync(button, action);
        return button;
    }
    public void Select(string page)
    {
        _dashboard.Classes.Set("selected", page == "dashboard"); _projects.Classes.Set("selected", page == "projects");
    }
}

internal sealed class TopBar : Border
{
    public TopBar(PresentationContext context, Func<Task> create)
    {
        Name = "TopBar"; Background = PresentationTheme.Brush("BackgroundApp"); Padding = new Thickness(40, 20, 40, 8);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 16 };
        var search = new TextBox
        {
            Name = "GlobalSearch",
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 40,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = PresentationTheme.Brush("BackgroundCard"),
            CornerRadius = PresentationTheme.Radius(PresentationTheme.RadiusMedium)
        };
        context.Localized.Bind(search, control => { control.Watermark = context.Text.Get("presentation.search"); AutomationProperties.SetName(control, context.Text.Get("presentation.search")); });
        grid.Children.Add(new ReadableColumn { MaxContentWidth = 440, Child = search });
        var date = context.Label(() => DateTimeOffset.Now.ToString("D", context.Locale.Culture), "Caption", "TextSecondary"); date.VerticalAlignment = VerticalAlignment.Center; date.TextWrapping = TextWrapping.NoWrap; date.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(date, 1); grid.Children.Add(date);
        var button = context.Action("project.create", create, "primary"); button.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(button, 2); grid.Children.Add(button);
        Child = grid;
    }
}

/// <summary>Project readiness line: avatar, name, mirrored progress bar and percentage.</summary>
internal sealed class ReadinessRow : Button
{
    protected override Type StyleKeyOverride => typeof(Button);
    public ReadinessRow(PresentationContext context, ProjectSummary summary, Func<Task> action)
    {
        Name = "ReadinessRow"; Classes.Add("row");
        var project = summary.Project;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,110,*,Auto"), ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(new Avatar(project.Name));
        var name = context.Label(() => project.Name, "Label"); name.FontWeight = FontWeight.SemiBold; name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap; name.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(name, 1); grid.Children.Add(name);
        var bar = new ReadinessBar(summary.CompletionPercentage / 100, summary.CompletionPercentage >= 70 ? "Success" : "BrandPrimary"); Grid.SetColumn(bar, 2); grid.Children.Add(bar);
        var percent = context.Label(() => summary.CompletionPercentage.ToString("0", context.Locale.Culture) + "%", "Label"); percent.MinWidth = 36; percent.TextAlignment = TextAlignment.End; percent.VerticalAlignment = VerticalAlignment.Center; percent.FlowDirection = FlowDirection.LeftToRight;
        Grid.SetColumn(percent, 3); grid.Children.Add(percent); Content = grid;
        context.Localized.Bind(this, control => AutomationProperties.SetName(control,
            context.Text.Format("dashboard.readiness", project.Name, summary.CompleteRequirements, summary.TotalRequirements, summary.MissingRequirements.Count)));
        Click += async (_, _) => await context.ActAsync(this, action);
    }
}

/// <summary>Keeps screens at a readable maximum width, anchored to the start edge in both flow directions.</summary>
internal sealed class ReadableColumn : Decorator
{
    public double MaxContentWidth { get; set; } = 1120;
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Min(availableSize.Width, MaxContentWidth);
        Child?.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, Child?.DesiredSize.Height ?? 0);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, MaxContentWidth), finalSize.Height));
        return finalSize;
    }
}
