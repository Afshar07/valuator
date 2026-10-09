using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Builds the view for a view-model by convention: <c>Features.Tasks.TasksViewModel</c> is shown by
/// <c>Features.Tasks.TasksView</c> in the same assembly and namespace.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private static readonly ConcurrentDictionary<Type, Type?> Views = new();

    public bool Match(object? data) => data is ViewModelBase;

    public Control? Build(object? data)
    {
        if (data is null) return null;
        var view = ViewTypeFor(data.GetType());
        if (view is null) return new TextBlock { Text = $"No view for {data.GetType().FullName}" };
        var control = (Control)Activator.CreateInstance(view)!;
        control.DataContext = data;
        return control;
    }

    public static Type? ViewTypeFor(Type viewModel) => Views.GetOrAdd(viewModel, static type =>
    {
        const string suffix = "ViewModel";
        var name = type.FullName;
        if (name is null || !name.EndsWith(suffix, StringComparison.Ordinal)) return null;
        var view = type.Assembly.GetType(name[..^suffix.Length] + "View");
        return view is not null && typeof(Control).IsAssignableFrom(view) && view.GetConstructor(Type.EmptyTypes) is not null ? view : null;
    });
}
