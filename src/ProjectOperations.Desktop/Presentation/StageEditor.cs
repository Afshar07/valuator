using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ProjectOperations.Core.Application;
using ProjectOperations.Core.Domain;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop;

/// <summary>Settings card where the stages of each template are added, renamed, recolored, reordered and deleted.</summary>
internal sealed class StageEditor : StackPanel
{
    private static readonly string[] Palette = ["#64748B", "#2563EB", "#0D9488", "#16A34A", "#CA8A04", "#D97706", "#DC2626", "#DB2777", "#7C3AED"];

    private readonly PresentationContext _context;
    private readonly StackPanel _rows = new() { Name = "StageRows" };
    private string _template = VcTemplate.Create().Id;
    private List<ProjectStage> _stages = [];
    private IReadOnlyDictionary<Guid, int> _usage = new Dictionary<Guid, int>();

    public StageEditor(PresentationContext context)
    {
        _context = context; Name = "StageEditor";
        var display = new DomainDisplay(context.Text);
        var templates = new (string Id, Func<string> Label)[]
        {
            (_template, () => display.Template(VcTemplate.Create())),
            (ProjectService.BlankTemplateId, () => context.Text.Get("template.blank"))
        };
        var picker = new Segmented(context, templates, () => _template, id => { _template = id; _ = ReloadAsync(); }, name: "StageTemplate") { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 8) };
        Children.Add(context.Label("stages.note", "Small", "TextSecondary"));
        Children.Add(picker);
        Children.Add(_rows);
        var add = context.IconAction("stages.add", Icons.Plus, () => CommitAsync(stages => stages.Add(new ProjectStage { Title = context.Text.Get("stages.newName"), Color = Palette[stages.Count % Palette.Length] })), "ghost");
        add.Name = "AddStage"; add.Margin = new Thickness(0, 10, 0, 12);
        Children.Add(add);
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _stages = (await _context.Projects.ListStagesAsync(_template)).ToList();
            _usage = await _context.Projects.CountProjectsByStageAsync();
            Render();
        }
        catch (Exception) { _context.ShowError("stages.saveFailed"); }
    }

    /// <summary>Applies a change to the working list, saves the whole ordered list and redraws.</summary>
    private async Task CommitAsync(Action<List<ProjectStage>> change)
    {
        var next = _stages.Select(stage => new ProjectStage { Id = stage.Id, Title = stage.Title, Color = stage.Color }).ToList();
        change(next);
        await _context.Projects.SaveStagesAsync(_template, next);
        await ReloadAsync();
    }

    private async Task GuardedAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception) { _context.ShowError("stages.saveFailed"); await ReloadAsync(); }
    }

    private void Render()
    {
        _rows.Children.Clear();
        for (var index = 0; index < _stages.Count; index++)
        {
            var stage = _stages[index]; var position = index;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(0, 6, 0, 0), Name = "StageRow" };

            var swatch = new Button { Width = 28, Height = 28, MinHeight = 28, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Center, Name = "StageColor" };
            swatch.Classes.Add("icon");
            swatch.Content = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.Parse(stage.Color)) };
            Avalonia.Automation.AutomationProperties.SetName(swatch, _context.Text.Get("stages.color"));
            var colors = new WrapPanel { MaxWidth = 170, ItemSpacing = 6, LineSpacing = 6 };
            var flyout = new Flyout { Content = colors };
            foreach (var color in Palette)
            {
                var choice = new Button { Width = 24, Height = 24, MinHeight = 24, Padding = new Thickness(0), Content = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.Parse(color)) } };
                choice.Classes.Add("icon"); choice.Name = "StageColorChoice";
                choice.Click += async (_, _) => { flyout.Hide(); await GuardedAsync(() => CommitAsync(stages => stages[position].Color = color)); };
                colors.Children.Add(choice);
            }
            swatch.Flyout = flyout;
            row.Children.Add(swatch);

            var title = Forms.Input(stage.Title); title.Name = "StageTitle"; title.MinHeight = 32;
            Avalonia.Automation.AutomationProperties.SetName(title, _context.Text.Get("stages.name"));
            var committed = false;
            async Task RenameAsync()
            {
                if (committed) return; committed = true;
                var text = title.Text?.Trim() ?? "";
                if (text.Length == 0 || text == stage.Title) { title.Text = stage.Title; return; }
                await GuardedAsync(() => CommitAsync(stages => stages[position].Title = text));
            }
            title.LostFocus += async (_, _) => await RenameAsync();
            title.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await RenameAsync(); };
            Grid.SetColumn(title, 1); row.Children.Add(title);

            var inUse = _usage.TryGetValue(stage.Id, out var projects) ? projects : 0;
            if (inUse > 0)
            {
                var note = _context.Label(() => inUse == 1 ? _context.Text.Get("stages.inUseOne") : _context.Text.Format("stages.inUse", _context.Number(inUse)), "Caption", "TextTertiary");
                note.VerticalAlignment = VerticalAlignment.Center; note.Name = "StageInUse";
                ToolTip.SetTip(note, _context.Text.Get("stages.inUseWhy"));
                Grid.SetColumn(note, 2); row.Children.Add(note);
            }

            var up = _context.IconAction("stages.up", Icons.CaretUp, () => CommitAsync(stages => Swap(stages, position, position - 1)), "icon", iconOnly: true);
            var down = _context.IconAction("stages.down", Icons.CaretDown, () => CommitAsync(stages => Swap(stages, position, position + 1)), "icon", iconOnly: true);
            up.IsEnabled = position > 0; down.IsEnabled = position < _stages.Count - 1; up.Name = "StageUp"; down.Name = "StageDown";
            var delete = _context.IconAction("stages.delete", Icons.Trash, async () => { await _context.Projects.DeleteStageAsync(_template, stage.Id); await ReloadAsync(); }, "icon", iconOnly: true);
            delete.Name = "StageDelete";
            delete.IsEnabled = inUse == 0 && _stages.Count > 1;
            if (_stages.Count == 1 && inUse == 0) ToolTip.SetTip(delete, _context.Text.Get("stages.lastOne"));
            else if (inUse > 0) ToolTip.SetTip(delete, _context.Text.Get("stages.inUseWhy"));
            Grid.SetColumn(up, 3); Grid.SetColumn(down, 4); Grid.SetColumn(delete, 5);
            row.Children.Add(up); row.Children.Add(down); row.Children.Add(delete);
            _rows.Children.Add(row);
        }
    }

    private static void Swap(List<ProjectStage> stages, int from, int to) => (stages[from], stages[to]) = (stages[to], stages[from]);
}
