using Avalonia.Controls;
using Avalonia.Layout;
using ProjectOperations.Core.Calendar;

namespace ProjectOperations.Desktop;

/// <summary>
/// Settings card for the optional Google Calendar overlay: status, Connect (browser sign-in with a visible Cancel) and Disconnect.
/// Sign-in can wait on the browser for minutes, so it deliberately does not lock the page the way <c>ActAsync</c> does.
/// </summary>
internal sealed class GoogleCalendarPanel : StackPanel
{
    private readonly PresentationContext _context;
    private readonly StackPanel _body = new() { Spacing = 10 };
    private CancellationTokenSource? _signIn;
    private bool _connected;

    public GoogleCalendarPanel(PresentationContext context)
    {
        _context = context; Name = "GoogleCalendarPanel";
        Children.Add(_body);
        AttachedToLogicalTree += (_, _) => _ = RefreshAsync();
        DetachedFromLogicalTree += (_, _) => _signIn?.Cancel();
        Render();
    }

    private async Task RefreshAsync()
    {
        _connected = await _context.Calendar.IsConnectedAsync();
        Render();
    }

    private void Render()
    {
        _body.Children.Clear();
        var status = _context.Label(() => _context.Text.Get(_connected ? "google.stateOn" : "google.stateOff"), "Meta", _connected ? "Accent" : "TextTertiary");
        status.Name = "GoogleCalendarStatus";
        var action = _signIn is not null ? Cancel() : _connected ? Disconnect() : Connect();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(status);
        text.Children.Add(_context.Label(_signIn is not null ? "google.connecting" : "google.note", "Small", "TextSecondary"));
        header.Children.Add(text);
        Grid.SetColumn(action, 1); action.VerticalAlignment = VerticalAlignment.Center; header.Children.Add(action);
        _body.Children.Add(header);
    }

    private Button Connect() => Plain("google.connect", ConnectAsync, "primary", "GoogleCalendarConnect");

    private Button Cancel() => Plain("google.cancel", () => { _signIn?.Cancel(); return Task.CompletedTask; }, null, "GoogleCalendarCancel");

    /// <summary>A button that does not go through the shell's page lock, so Cancel stays clickable while a sign-in is pending.</summary>
    private Button Plain(string key, Func<Task> action, string? appearance, string name)
    {
        var button = new Button { HorizontalAlignment = HorizontalAlignment.Left, Name = name };
        if (appearance is not null) button.Classes.Add(appearance);
        _context.Localized.Bind(button, control => control.Content = _context.Text.Get(key));
        button.Click += async (_, _) =>
        {
            try { await action(); }
            catch (Exception) { _context.ShowError("validation.operationFailed"); }
        };
        return button;
    }

    private Button Disconnect()
    {
        var button = _context.Action("google.disconnect", async () =>
        {
            await _context.Calendar.DisconnectAsync();
            _connected = false; Render();
            await _context.Shell.RefreshShellAsync();
        });
        button.Name = "GoogleCalendarDisconnect"; return button;
    }

    private async Task ConnectAsync()
    {
        using var signIn = new CancellationTokenSource();
        _signIn = signIn; Render();
        try
        {
            await _context.Calendar.ConnectAsync(signIn.Token);
            _connected = true;
        }
        catch (OperationCanceledException) { /* user cancelled: nothing stored, nothing to report */ }
        catch (ExternalCalendarAuthorizationException) { _context.ShowError("google.connectDenied"); }
        catch (Exception exception) when (exception is HttpRequestException or IOException or System.Net.HttpListenerException or System.ComponentModel.Win32Exception)
        {
            _context.ShowError("google.connectFailed");
        }
        finally
        {
            _signIn = null; Render();
        }
        if (_connected) await _context.Shell.RefreshShellAsync();
    }
}
