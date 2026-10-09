using Avalonia;
using Avalonia.Controls;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// A grid for table headers and rows that drops columns when the table is narrow. Declare the full <see cref="Columns"/> and, for
/// each child, where it goes when compact with <see cref="CompactColumnProperty"/> (-1 hides it). Whoever measures the table sets
/// <see cref="IsCompactProperty"/> once on an ancestor; it is inherited, so every header and row follows.
/// </summary>
internal sealed class TableGrid : Grid
{
    public static readonly AttachedProperty<bool> IsCompactProperty = AvaloniaProperty.RegisterAttached<TableGrid, Control, bool>("IsCompact", inherits: true);
    public static readonly AttachedProperty<int> CompactColumnProperty = AvaloniaProperty.RegisterAttached<TableGrid, Control, int>("CompactColumn", -2);
    private static readonly AttachedProperty<int> FullColumnProperty = AvaloniaProperty.RegisterAttached<TableGrid, Control, int>("FullColumn", -1);

    public static bool GetIsCompact(Control control) => control.GetValue(IsCompactProperty);
    public static void SetIsCompact(Control control, bool value) => control.SetValue(IsCompactProperty, value);
    public static int GetCompactColumn(Control control) => control.GetValue(CompactColumnProperty);
    public static void SetCompactColumn(Control control, int value) => control.SetValue(CompactColumnProperty, value);

    private string _columns = "";

    static TableGrid() => IsCompactProperty.Changed.AddClassHandler<TableGrid>((grid, _) => grid.Apply());

    protected override Type StyleKeyOverride => typeof(Grid);

    /// <summary>Column definitions when the table is wide.</summary>
    public string Columns { get => _columns; set { _columns = value; Apply(); } }
    /// <summary>Column definitions when the table is narrow.</summary>
    public string CompactColumns { get; set; } = "";

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Apply();
    }

    private void Apply()
    {
        var compact = GetIsCompact(this) && CompactColumns.Length > 0;
        if (_columns.Length == 0) return;
        ColumnDefinitions = new ColumnDefinitions(compact ? CompactColumns : _columns);
        foreach (var child in Children)
        {
            if (child.GetValue(FullColumnProperty) < 0) child.SetValue(FullColumnProperty, GetColumn(child));
            var full = child.GetValue(FullColumnProperty);
            var target = GetCompactColumn(child);
            if (!compact) { child.IsVisible = true; SetColumn(child, full); }
            else if (target == -1) child.IsVisible = false;
            else { child.IsVisible = true; SetColumn(child, target == -2 ? full : target); }
        }
    }
}
