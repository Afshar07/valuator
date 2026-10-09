using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectOperations.Desktop.Common;

/// <summary>
/// Base for view-models. View-models reference no Avalonia controls and are tested with plain xUnit; domain rules
/// (urgency, readiness, attention) stay in Core.
/// </summary>
public abstract class ViewModelBase : ObservableObject;
