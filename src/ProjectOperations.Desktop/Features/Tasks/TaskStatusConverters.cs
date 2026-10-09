using Avalonia.Data.Converters;
using ProjectOperations.Core.Domain;

namespace ProjectOperations.Desktop.Features.Tasks;

/// <summary>Icon, weight and colour token for a task status, from the one mapping in <see cref="StatusVisuals"/>. For <see cref="Common.IconGlyph"/> bindings.</summary>
internal static class TaskStatusConverters
{
    public static readonly IValueConverter Icon = new FuncValueConverter<ProjectTaskStatus, string?>(status => StatusVisuals.Task(status).Icon);
    public static readonly IValueConverter Weight = new FuncValueConverter<ProjectTaskStatus, IconWeight>(status => StatusVisuals.Task(status).Weight);
    public static readonly IValueConverter Token = new FuncValueConverter<ProjectTaskStatus, string?>(status => StatusVisuals.Task(status).Color);
}
