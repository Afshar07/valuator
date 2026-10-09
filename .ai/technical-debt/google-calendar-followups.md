# Google Calendar overlay follow-ups

- **Status:** Open
- **Verified:** 2026-10-09
- **Scope/owner:** ProjectOperations Infrastructure/Desktop calendar boundary (`IExternalCalendarSource`)

## Problem

The opt-in, read-only Google Calendar overlay is implemented and fixture-tested, but several things are unverified or deliberately
limited.

## Evidence and inventory

- No real Google sign-in has been performed. OAuth (PKCE, loopback redirect, refresh, revoke, paging) is tested only against a fake
  HTTP handler and a real local listener (`GoogleCalendarSourceTests`). Google's real response shapes, consent-screen behavior and
  the client-secret requirement for Desktop clients follow Google's documentation, not a live run.
- `DpapiTokenStore` (`Infrastructure/Calendar/ITokenStore.cs`) has never executed: development and CI ran on Linux, where it is
  compiled but returns nothing. It is the only secure token store.
- Non-Windows platforms get no Google option (`DpapiTokenStore.IsSupported` is false) rather than plain-text storage.
- A client id comes only from `PROJECTOPS_GOOGLE_CLIENT_ID` / `PROJECTOPS_GOOGLE_CLIENT_SECRET`; none is bundled. While the user's
  Google consent screen is in Testing, Google expires refresh tokens after seven days, which surfaces as the reconnect notice.
- Only the primary calendar is read, at most 10 pages of 250 events per visible grid. Timed events show on their start day only.
- Pixel-level styling of the Google chip (light, dark, Persian RTL) has not been reviewed in a rendered frame.

## Impact

A user could connect successfully in tests yet fail on first real use (consent screen configuration, DPAPI error handling, response
differences). Weekly reconnects in Testing mode are expected, not a defect. macOS and Linux users cannot use the overlay.

## Migration constraints

Keep the overlay opt-in, read-only (`calendar.readonly`) and display-only: events must not be persisted, attached to a project or
included in any agent context snapshot (`ExternalCalendarBoundaryTests`). Do not store tokens in SQLite or `settings.json`, and do
not add a plain-text fallback. Other platforms need a new `ITokenStore` (Keychain, libsecret) behind the same interface. Bundling a
client id requires owning a Google Cloud project and passing Google's verification for the sensitive scope.

## Completion criteria

A manual end-to-end connect, event display, Disconnect and revoked-token run on Windows against a real Google account; a decision on
platform coverage and on bundling versus user-supplied client ids; a rendered-frame review of the chip in both themes and Persian.

## Verification

Not performed. Record the Windows manual run (date, Windows version, consent-screen mode) and any defects found here.
