using CommunityToolkit.Mvvm.ComponentModel;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Tests.Mvvm;

/// <summary>Test-only view-model proving the MVVM foundation end to end: view locator, compiled bindings, localization and dates.</summary>
public sealed partial class ProbeViewModel(LocalizedStrings strings) : ViewModelBase
{
    public LocalizedStrings L { get; } = strings;
    public DateTimeOffset? Due { get; init; }
    [ObservableProperty] private string _name = "";
}

/// <summary>Has no matching view.</summary>
public sealed class OrphanViewModel : ViewModelBase;
