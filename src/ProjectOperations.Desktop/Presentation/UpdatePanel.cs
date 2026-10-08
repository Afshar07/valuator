using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ProjectOperations.Desktop.Updates;

namespace ProjectOperations.Desktop;

/// <summary>
/// Settings card for application updates: current version, a manual check, the available version with its release notes,
/// download progress, and restart-to-install. State comes from the shell's <see cref="UpdateController"/>.
/// </summary>
internal sealed class UpdatePanel : StackPanel
{
    private readonly PresentationContext _context;
    private readonly UpdateController _updates;
    private readonly StackPanel _body = new() { Spacing = 10 };
    private ProgressBar? _bar;
    private TextBlock? _percent;
    private UpdatePhase? _rendered;

    public UpdatePanel(PresentationContext context)
    {
        _context = context; _updates = context.Updates; Name = "UpdatePanel";
        Children.Add(_body);
        AttachedToLogicalTree += (_, _) => { _updates.Changed -= OnChanged; _updates.Changed += OnChanged; Render(); };
        DetachedFromLogicalTree += (_, _) => _updates.Changed -= OnChanged;
        Render();
    }

    private string T(string key) => _context.Text.Get(key);
    private string PercentText() => _context.Text.Format("updates.downloading", _context.Number(_updates.Percent));
    private static string Isolate(string text) => "\u2066" + text + "\u2069";

    private void OnChanged(object? sender, EventArgs e)
    {
        // Progress ticks only move the bar. Rebuilding the buttons on every tick could swallow a click on Cancel.
        if (_updates.Phase == UpdatePhase.Downloading && _rendered == UpdatePhase.Downloading && _bar is not null && _percent is not null)
        {
            _bar.Value = _updates.Percent; _percent.Text = PercentText(); return;
        }
        Render();
    }

    private void Render()
    {
        _body.Children.Clear(); _bar = null; _percent = null; _rendered = _updates.Phase;
        var updater = _updates.Updater;

        var version = _context.Label(() => _context.Text.Format("updates.version", Isolate(updater.CurrentVersion)), "Body");
        version.Name = "CurrentVersion";
        if (!updater.CanUpdate)
        {
            _body.Children.Add(version);
            _body.Children.Add(_context.Label("updates.portable", "Caption", "TextSecondary"));
            return;
        }

        var busy = _updates.Phase is UpdatePhase.Checking or UpdatePhase.Downloading or UpdatePhase.Ready;
        var check = Command("updates.check", () => _ = _updates.CheckAsync(), "ghost", "CheckForUpdates");
        check.IsEnabled = !busy;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        version.VerticalAlignment = VerticalAlignment.Center; check.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(version); Grid.SetColumn(check, 1); header.Children.Add(check);
        _body.Children.Add(header);

        switch (_updates.Phase)
        {
            case UpdatePhase.Checking:
                _body.Children.Add(_context.Label("updates.checking", "Caption", "TextSecondary"));
                break;
            case UpdatePhase.UpToDate:
                _body.Children.Add(_context.Label("updates.upToDate", "Caption", "Success"));
                break;
            case UpdatePhase.Available or UpdatePhase.Downloading when _updates.Update is { } update:
                AddAvailable(update, downloading: _updates.Phase == UpdatePhase.Downloading);
                break;
            case UpdatePhase.Ready when _updates.Update is { } ready:
                AddReady(ready);
                break;
        }

        if (_updates.ErrorKey is { } error)
        {
            var message = _context.Label(error, "Caption", "Error"); message.Name = "UpdateError"; _body.Children.Add(message);
        }
    }

    private void AddAvailable(AppUpdate update, bool downloading)
    {
        var title = _context.Label(() => _context.Text.Format("updates.available", Isolate(update.Version)), "BodyStrong");
        title.Name = "UpdateAvailable"; _body.Children.Add(title);
        if (update.SizeBytes > 0)
            _body.Children.Add(_context.Label(() => _context.Text.Format("updates.size", Isolate(Size(update.SizeBytes))), "Caption", "TextSecondary"));

        _body.Children.Add(_context.Label("updates.notes", "CaptionMedium", "TextSecondary"));
        if (update.ReleaseNotes is { } notes)
        {
            // Markdown is shown as written; it reads fine as plain text and avoids rendering remote content.
            var box = new TextBox { Name = "UpdateNotes", Text = notes, IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 64, MaxHeight = 220, FontSize = 12.5 };
            _body.Children.Add(box);
        }
        else _body.Children.Add(_context.Label("updates.noNotes", "Small", "TextTertiary"));

        if (downloading)
        {
            _bar = new ProgressBar { Name = "UpdateProgress", Minimum = 0, Maximum = 100, Value = _updates.Percent, MinHeight = 6, HorizontalAlignment = HorizontalAlignment.Stretch };
            _percent = _context.Label(PercentText, "Caption", "TextSecondary"); _percent.Name = "UpdatePercent";
            _body.Children.Add(_bar); _body.Children.Add(_percent);
            _body.Children.Add(Command("updates.cancel", _updates.CancelDownload, "ghost", "CancelDownload"));
        }
        else _body.Children.Add(Command("updates.download", () => _ = _updates.DownloadAsync(), "primary", "DownloadUpdate"));
    }

    private void AddReady(AppUpdate update)
    {
        var ready = _context.Label(() => _context.Text.Format("updates.ready", Isolate(update.Version)), "BodyStrong", "Success");
        ready.Name = "UpdateReady"; _body.Children.Add(ready);
        _body.Children.Add(_context.Label("updates.restartNote", "Caption", "TextSecondary"));
        _body.Children.Add(Command("updates.restart", () =>
        {
            if (_context.Shell.IsAgentRunning) { _context.ShowError("updates.agentRunning"); return; }
            _updates.RestartToInstall();
        }, "primary", "RestartToUpdate"));
    }

    /// <summary>A button that runs synchronously on click. Unlike <see cref="PresentationContext.Action(string, Func{Task}, string?)"/> it does not lock the page, so navigation stays available during a check or download.</summary>
    private Button Command(string key, Action click, string appearance, string name)
    {
        var button = new Button { Name = name, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var style in appearance.Split(' ')) button.Classes.Add(style);
        _context.Localized.Bind(button, control => control.Content = T(key));
        button.Click += (_, _) => click();
        return button;
    }

    private string Size(long bytes)
    {
        var megabytes = bytes / 1048576d;
        return megabytes >= 1 ? megabytes.ToString("0.#", _context.Locale.Culture) + " MB" : Math.Max(1, bytes / 1024) + " KB";
    }
}
