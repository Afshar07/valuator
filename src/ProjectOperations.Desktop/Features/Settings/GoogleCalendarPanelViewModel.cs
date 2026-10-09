using System.ComponentModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProjectOperations.Core.Calendar;
using ProjectOperations.Desktop.Common;
using ProjectOperations.Desktop.Localization;
using ProjectOperations.Desktop.Shell;

namespace ProjectOperations.Desktop.Features.Settings;

/// <summary>
/// The optional Google Calendar overlay: status, Connect (browser sign-in with a visible Cancel) and Disconnect. Only offered when the
/// external calendar is available. Sign-in can wait on the browser for minutes, so Connect deliberately does not use the shell's page
/// lock the way other commands do: Cancel and navigation stay available, and leaving the page abandons the sign-in.
/// </summary>
internal sealed partial class GoogleCalendarPanelViewModel : ViewModelBase, IDisposable
{
    private readonly IExternalCalendarSource _calendar;
    private readonly IPageHost _host;
    private CancellationTokenSource? _signIn;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusText), nameof(CanConnect), nameof(CanDisconnect))] private bool _isConnected;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NoteText), nameof(CanConnect), nameof(CanDisconnect))] private bool _isSigningIn;

    public GoogleCalendarPanelViewModel(SettingsServices services)
    {
        _calendar = services.Calendar; _host = services.Host; L = services.Strings;
        RefreshOnLanguageChange(L);
    }

    public LocalizedStrings L { get; }

    public string StatusText => L[IsConnected ? "google.stateOn" : "google.stateOff"];
    public string NoteText => L[IsSigningIn ? "google.connecting" : "google.note"];

    // Exactly one of the three actions is offered: Cancel while a sign-in is pending, otherwise Disconnect or Connect.
    public bool CanConnect => !IsSigningIn && !IsConnected;
    public bool CanDisconnect => !IsSigningIn && IsConnected;

    /// <summary>Reads whether a sign-in is stored on this machine. A local check, so it never waits on the network.</summary>
    public async Task RefreshAsync()
    {
        try { IsConnected = await _calendar.IsConnectedAsync(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { IsConnected = false; }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            using var signIn = new CancellationTokenSource();
            _signIn = signIn; IsSigningIn = true;
            var connected = false;
            try
            {
                await _calendar.ConnectAsync(signIn.Token);
                IsConnected = connected = true;
            }
            catch (OperationCanceledException) { /* user cancelled: nothing stored, nothing to report */ }
            catch (ExternalCalendarAuthorizationException) { _host.ShowError("google.connectDenied"); }
            catch (Exception exception) when (exception is HttpRequestException or IOException or HttpListenerException or Win32Exception)
            {
                _host.ShowError("google.connectFailed");
            }
            finally
            {
                _signIn = null; IsSigningIn = false;
            }
            if (connected) await _host.RefreshShellAsync();
        }
        catch (Exception)
        {
            _host.ShowError("validation.operationFailed");
        }
    }

    [RelayCommand]
    private void CancelSignIn() => _signIn?.Cancel();

    [RelayCommand]
    private Task DisconnectAsync() => _host.RunAsync(async () =>
    {
        await _calendar.DisconnectAsync();
        IsConnected = false;
        await _host.RefreshShellAsync();
    });

    /// <summary>Leaving Settings abandons a sign-in that is still waiting on the browser.</summary>
    public void Dispose() => _signIn?.Cancel();
}
