# MVP verification

## Automated verification

Verified on 2026-10-06 with .NET SDK 10.0.300 on Windows:

| Check | Result |
| --- | --- |
| `dotnet build ProjectOperations.slnx --no-restore --nologo` | Passed; zero warnings/errors |
| `dotnet test ProjectOperations.slnx --no-restore --nologo` | 114 passed: 83 domain/application/SQLite/runtime tests + 31 desktop/localization/presentation tests (including Avalonia Headless) |
| `dotnet format ProjectOperations.slnx --no-restore --verify-no-changes` | Passed |
| `dotnet list ProjectOperations.slnx package --vulnerable --include-transitive` | No known vulnerable packages reported |
| `git diff --check` | Passed |

Coverage includes the exact template, independent requirement instantiation,
completion/missing rules, timezone-aware deadline boundaries, SQLite reopen/WAL/
rollback/stale-edit protection, atomic task approval, proposal parsing, selected-
project context isolation, runtime policy/SSE/cancellation/failure fixtures, and
native headless create/edit/task/consent/proposal/Stop/close interactions. Localization
coverage includes English/Persian keys, semantic resource/placeholder parity,
settings persistence/failure recovery, enum/template display mapping, live edits
and tab reattachment, actual mirrored navigation positions, RTL/LTR inheritance,
date offsets, unchanged SQLite aggregates/storage enums, response-language prompt
generation and unchanged proposal/runtime schemas with Persian prose.

`MvpWorkflowTests` creates a synthetic Atlas project, generates 16 requirements,
associates a real temporary text file, sets one Complete requirement, creates overdue
and upcoming tasks, reopens SQLite and verifies 6.25% readiness and attention summaries.
It also confirms removing an association retains the source file.

Automated tests use synthetic data and isolated temporary databases; they do not
operate on the user's projects or authenticate a model provider. Runtime fixtures
are not a live OpenCode/provider test. **No dedicated isolated runtime/provider has
been configured for a live test**, so authentication, live model output and live
Stop remain unverified. Installed OpenCode 2.0.24 and its published protocol/types
were inspected; the shared user runtime was not used for finance requests.

Native desktop-window rendering and file-picker interaction were not manually verified.
RTL/LTR layout and switching were verified through Avalonia Headless structural
assertions. Setting `PROJECTOPS_UI_CAPTURE_DIR` when running the desktop tests also
renders Dashboard, Project Overview, requirement editor, Tasks and Delegation frames
(English/Persian at 1440 and 800 px) with the headless Skia renderer for visual review.

### Valuator redesign (2026-10-07)

Verified on 2026-10-07 with .NET SDK 10.0.112 on Linux (cloud container, headless only):
build with zero warnings/errors; `dotnet test` **121 passed** (85 domain/application/SQLite/runtime
+ 36 desktop/localization/presentation); `dotnet format --verify-no-changes` passed; no
vulnerable packages; `git diff --check` passed. New coverage: schedule/next-deadline
queries, theme persistence beside the language key, Calendar/Documents/Settings rendering
from stored data in light and dark, the new-project dialog (blank template disabled), the
pending-proposal banner leading to the assistant review tray, and the not-configured
assistant offering no Run. With `PROJECTOPS_UI_CAPTURE_DIR` set, `DesignedSectionsTests`
also captures dashboard, calendar, documents, settings, projects, new-project and review
frames (`en`/light/1440, `fa`/dark/1440, `en`/dark/800). The native window, real OS
theme following (Auto), file launching and the file picker were **not** exercised here.

### Valuator v3 UI (2026-10-08)

Verified on 2026-10-08 with .NET SDK 10.0.401 on Windows 11: build with zero warnings/errors;
`dotnet test` **128 passed** (88 domain/application/SQLite/runtime + 40 desktop). New coverage:
schema v1→v2 migration and the task↔requirement link, blank projects, the first-run welcome
screen, the wizard, sections appearing as data arrives (New badge, toast), the getting-started
card, the isolated sample workspace, the task/milestone/project dialogs and the checklist
accordion. `dotnet format` reports only the pre-existing CRLF working-tree findings. The native
window, the OS file picker and the Windows date picker popup were **not** exercised
(headless only). With `PROJECTOPS_UI_CAPTURE_DIR` set, `OnboardingTests` captures the welcome,
wizard, checklist and sample screens (English/light and Persian/dark).

### Jalali dates (2026-10-09)

Verified on 2026-10-09 with .NET SDK 10.0.112 on Linux (cloud container, headless only):
`dotnet test` **77 passed** in the desktop project (26 new in `JalaliDateTests`: conversions incl.
leap-year Esfand 30, Persian/Arabic digits, Jalali/Gregorian auto-detection, rejection of impossible
dates, month navigation, formatter output, and the Persian task dialog typing/popup/save flow).
With `PROJECTOPS_UI_CAPTURE_DIR` set, the Persian dashboard, calendar (Mehr 1405) and the
date-picker popup were reviewed visually. **Not verified:** a native window, real keyboard focus
behaviour in the picker, and the conversion accuracy of `PersianCalendar` outside the years the
tests cover. Holiday and Persian-digit display are not implemented (Latin digits are shown).

## Please verify the desktop workflow

For a manual visual check, run:

```sh
dotnet run --project src/ProjectOperations.Desktop
```

1. **Create project:** on a fresh data folder choose **Start my first project** (or, later, **New project**
   in All projects), name it `Atlas` with a company and pick `VC Investment Review`. Use the pencil
   beside the title to add owner, stage and notes. Reopen it from All projects. Check the four localized groups
   contain **6 / 6 / 3 / 1** requirements (16 total). Repeat with **Blank project** and add two requirements.
2. **Requirements & files:** open Pitch deck, choose a local file and check it appears
   (Documents then appears in the sidebar as New). Remove the link and confirm the original
   file still exists. Attach it again.
3. **Readiness:** mark one requirement Provided, one NeedsReview and one Complete.
   Check only Complete contributes to readiness: one complete item is **6.25%** of
   the 16 requirements (the display may round it). Missing/NeedsReview must be listed.
4. **Tasks & dates:** create one task due yesterday and another due tomorrow with the date
   chips or picker (Needs attention and Calendar appear). Edit a task. Check Attention shows one overdue
   task, the upcoming task, missing information, and a recent Atlas project.
5. **Persistence:** close and reopen the app. Check metadata, requirements, file
   references and deadlines are unchanged. Complete the overdue task and confirm it
   no longer counts as overdue; delete a throwaway task.
6. **Agent (only after explicit runtime setup):** follow `agent-runtime.md`. In
   Delegate & review select Summarize project, preview the context, and confirm only
   Atlas appears. Consent and run. Inspect actual activity/result/history. The answer
   must not claim to have read file contents supplied only as paths.
7. **Stop:** start another request and click Stop while it is active. It should show
   Stopping until the runtime confirms cancellation; a stop failure must be reported
   as failed/unconfirmed, not successful cancellation.
8. **Approval:** request Extract action items. Suggested tasks should initially be
   unchecked and absent from Tasks & dates. Select one and Add selected tasks;
   verify it is added once and has an Approved review status. Reject another and
   verify no task is created for it.

Do not use real confidential documents for the initial agent test. File-content
extraction and document-format analysis are deliberately not part of this MVP.

## Localization verification

Choose **English** or **فارسی** in the desktop language selector. Switching is live
and must preserve unsaved form input and selected enum values. Persian uses RTL;
English restores LTR. Restart to confirm the language selection is restored from
`settings.json` alongside the application database (or in `PROJECTOPS_DATA_DIR`).

Check navigation, project tabs, requirements, task forms, consent and review cards
in both directions. Paths, technical context previews and explicit Gregorian date
inputs remain LTR. In Persian, display dates are Jalali and the task and milestone
dialogs use a Jalali date picker (type `1405/07/17` or `2026-10-09`, or pick from the
month popup; the Gregorian equivalent is shown beneath). English keeps Gregorian.

Language changes must not modify project names, notes, stored enum values, file
paths, timestamps or task/proposal data. Agent prose follows the selected language,
but action identifiers, `task-proposals`, JSON field names and ISO deadlines retain
their stable contract. Changing language is unavailable while an agent job runs.

## Appearance verification

Switch Light, Dark and Auto from the sidebar or Settings. Colors change live without
reloading screens; Auto follows the operating system. Restart and confirm the theme is
restored while the language preference in the same `settings.json` is unchanged. Check
AI surfaces (assistant panel, proposal tray, pending-proposal banner) stay violet and
visually distinct from committed tasks in both themes and both directions.

## Google Calendar overlay

`GoogleCalendarSourceTests` runs the OAuth flow (PKCE, loopback redirect, state check, cancellation, revoked tokens, paging,
all-day and cancelled events) against a fake HTTP handler and a real loopback listener; no request leaves the machine.
`GoogleCalendarUiTests` drives the real window with a fake source: default installs show nothing, connect/disconnect, Cancel
during sign-in, failure notices that keep project dates, and the sample workspace never reading the source.
`ExternalCalendarBoundaryTests` fails if agent, domain, application or runtime types depend on external calendar types.

Not verified here: a real Google sign-in and Windows DPAPI storage. Both need a Google OAuth client and a Windows machine; follow
`docs/google-calendar.md` and connect once manually before relying on it.
