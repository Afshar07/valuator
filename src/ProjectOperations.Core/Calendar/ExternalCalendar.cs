namespace ProjectOperations.Core.Calendar;

/// <summary>
/// A read-only event from the user's own external calendar. It is display data only: it is never stored in SQLite,
/// never part of a project, and never included in an agent context snapshot.
/// </summary>
public sealed record ExternalCalendarEvent(string Id, string Title, DateTimeOffset Start, DateTimeOffset End, bool IsAllDay);

/// <summary>The user's sign-in must be redone (revoked, expired or removed); the caller should offer Connect again.</summary>
public sealed class ExternalCalendarAuthorizationException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Optional, opt-in read-only view of an external calendar. Nothing is contacted until the user calls
/// <see cref="ConnectAsync"/>; an unconfigured or disconnected source returns no events and makes no requests.
/// </summary>
public interface IExternalCalendarSource
{
    /// <summary>The app has what it needs to offer Connect (for Google: an OAuth client id). False hides the option entirely.</summary>
    bool IsAvailable { get; }

    /// <summary>A sign-in is stored on this machine. Local check only; never makes a network request.</summary>
    Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default);

    /// <summary>Interactive sign-in in the user's browser. Cancelling the token abandons it without storing anything.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Forgets the stored sign-in and, best effort, revokes it with the provider. Always clears the local copy.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Events overlapping [from, to), ordered by start. Empty when not connected.</summary>
    Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

/// <summary>The default: no external calendar. Keeps current behavior for everyone who has not opted in.</summary>
public sealed class NoExternalCalendar : IExternalCalendarSource
{
    public bool IsAvailable => false;
    public Task<bool> IsConnectedAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task ConnectAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("No external calendar is configured.");
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<ExternalCalendarEvent>> ListEventsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalCalendarEvent>>([]);
}
