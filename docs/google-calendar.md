# Google Calendar overlay (optional)

An opt-in, read-only view of your Google Calendar inside **Calendar**. If you do nothing, the app behaves exactly as before:
no Google option appears in Settings and no request is ever made to Google.

## What it does and does not do

- Shows your **primary** calendar's events next to project tasks and milestones, as quiet read-only chips.
- Requests only the `calendar.readonly` scope, and asks Google only for each event's id, status, title and times
  (no attendees, descriptions, links or locations).
- Never writes to Google. Never stores events (they are fetched for the visible month and held in memory).
- **Never sends events to the assistant.** They are not part of any agent context snapshot, prompt or proposal, and a test fails
  if agent or project code gains a dependency on them.
- The sample workspace never uses your Google account.
- Disconnect revokes the sign-in with Google (best effort) and always deletes the local token.

## Requirements

- Windows. The refresh token is protected with Windows DPAPI (current user). On other platforms the option is not offered,
  rather than storing the token as plain text.
- A Google OAuth client you create (below).

## Create the OAuth client (about 10 minutes, free)

Menu names in Google Cloud Console change from time to time; follow the intent if a label differs.

1. Open <https://console.cloud.google.com/> and create a project (or pick one).
2. **APIs & Services → Library**: enable **Google Calendar API**.
3. **OAuth consent screen / Google Auth Platform**: choose **External**, fill the app name and your email, and add the scope
   `https://www.googleapis.com/auth/calendar.readonly`.
4. While the app is in **Testing**, add your Google account under **Test users**.
5. **Credentials → Create credentials → OAuth client ID → Application type: Desktop app**.
6. Copy the **Client ID** and **Client secret**.

## Configure the app

Set these environment variables before starting the app, then restart it:

```text
PROJECTOPS_GOOGLE_CLIENT_ID=1234567890-abc.apps.googleusercontent.com
PROJECTOPS_GOOGLE_CLIENT_SECRET=GOCSPX-...
```

Google issues a client secret for Desktop clients and its token endpoint expects it, but it does not protect anything (the
sign-in uses PKCE), so it is treated as configuration like the client id. Leave it unset if Google did not issue one.

Then open **Settings → Google Calendar → Connect**, finish signing in in your browser, and return to the app. Calendar appears in
the sidebar once you are connected, even before you have any dated work.

## Limits to know about

- **Testing mode:** up to 100 test users, and Google expires refresh tokens after **7 days**. You will see "Google Calendar needs to
  be connected again" and can simply Connect again. Publishing the app removes the expiry but needs Google's verification because
  `calendar.readonly` is a sensitive scope (a privacy policy and a scope justification; it costs time, not money).
- Only the primary calendar, and at most 2,500 events per visible month grid (10 pages of 250).
- Timed events appear on their start day; all-day events on every day they cover.
- The API is free at this usage; the default quota is far above what a month view needs.

## Troubleshooting

| You see | Meaning |
| --- | --- |
| No Google card in Settings | `PROJECTOPS_GOOGLE_CLIENT_ID` is unset, or the platform has no secure token storage. |
| "Google Calendar wasn't connected. Access was declined or the sign-in wasn't completed." | You cancelled or denied access in the browser, or the response did not match the request. |
| "Couldn't connect…" | A network or local listener problem. Check the connection and retry. |
| "Google Calendar needs to be connected again." | Google rejected the stored sign-in (revoked, expired in Testing mode, password changed). The token was cleared. |
| "Couldn't load Google Calendar events…" | A temporary network or Google error. Project dates are unaffected. |
