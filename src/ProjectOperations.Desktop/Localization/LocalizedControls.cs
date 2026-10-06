using Avalonia.Controls;

namespace ProjectOperations.Desktop.Localization;

internal sealed class LocalizedControls : IDisposable
{
    private readonly ILocaleContext _locale;
    private readonly List<WeakReference<IBinding>> _bindings = [];
    private bool _disposed;

    public LocalizedControls(ILocaleContext locale)
    {
        _locale = locale;
        _locale.Changed += Refresh;
    }

    public void Bind<T>(T control, Action<T> update) where T : Control
    {
        var binding = new Binding<T>(control, update);
        Register(binding);
        binding.Refresh();
        control.AttachedToLogicalTree += (_, _) =>
        {
            if (_disposed) return;
            Register(binding);
            binding.Refresh();
        };
        control.DetachedFromLogicalTree += (_, _) => Unregister(binding);
    }

    private void Register(IBinding binding)
    {
        _bindings.RemoveAll(reference => !reference.TryGetTarget(out _));
        if (!_bindings.Any(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, binding)))
            _bindings.Add(new WeakReference<IBinding>(binding));
    }

    private void Unregister(IBinding binding) => _bindings.RemoveAll(reference =>
        !reference.TryGetTarget(out var existing) || ReferenceEquals(existing, binding));

    private void Refresh(object? sender, EventArgs e)
    {
        foreach (var reference in _bindings.ToArray())
            if (reference.TryGetTarget(out var binding)) binding.Refresh();
        _bindings.RemoveAll(reference => !reference.TryGetTarget(out _));
    }

    public void Dispose()
    {
        _disposed = true;
        _locale.Changed -= Refresh;
        _bindings.Clear();
    }

    private interface IBinding
    {
        void Refresh();
    }

    private sealed class Binding<T>(T control, Action<T> update) : IBinding where T : Control
    {
        private readonly WeakReference<T> _control = new(control);
        public void Refresh()
        {
            if (_control.TryGetTarget(out var target)) update(target);
        }
    }
}
