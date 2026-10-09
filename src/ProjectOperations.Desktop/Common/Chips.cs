using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Desktop.Localization;

namespace ProjectOperations.Desktop.Common;

/// <summary>One pill in a row of one-of-many chips. Shown by <see cref="ChipOptionView"/>.</summary>
public sealed partial class ChipOptionViewModel : ViewModelBase
{
    private readonly Func<string> _label;

    public ChipOptionViewModel(LocalizedStrings strings, Func<string> label, Action select)
    {
        _label = label;
        SelectCommand = new RelayCommand(select);
        RefreshOnLanguageChange(strings);
    }

    public string Label => _label();
    public IRelayCommand SelectCommand { get; }
    [ObservableProperty] private bool _isSelected;
}

/// <summary>A row of one-of-many chips over a set of values; exactly one is selected.</summary>
public sealed class ChipGroupViewModel<T> : ViewModelBase where T : notnull
{
    private readonly List<(T Value, ChipOptionViewModel Chip)> _options = [];
    private T _selected;

    public ChipGroupViewModel(LocalizedStrings strings, IEnumerable<(T Value, Func<string> Label)> options, T selected)
    {
        _selected = selected;
        foreach (var (value, label) in options)
        {
            var captured = value;
            _options.Add((value, new ChipOptionViewModel(strings, label, () => Selected = captured) { IsSelected = EqualityComparer<T>.Default.Equals(value, selected) }));
        }
        Options = _options.Select(option => option.Chip).ToList();
    }

    public IReadOnlyList<ChipOptionViewModel> Options { get; }

    public T Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            foreach (var (option, chip) in _options) chip.IsSelected = EqualityComparer<T>.Default.Equals(option, value);
        }
    }
}
