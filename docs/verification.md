# MVP verification

## Automated verification

Verified on 2026-10-06 with .NET SDK 10.0.300 on Windows:

| Check | Result |
| --- | --- |
| `dotnet build ProjectOperations.slnx --no-restore --nologo` | Passed; zero warnings/errors |
| `dotnet test ProjectOperations.slnx --no-restore --nologo` | 107 passed: 83 domain/application/SQLite/runtime tests + 24 desktop/localization tests (including Avalonia Headless) |
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

Native visual rendering and file-picker interaction were not manually verified;
RTL/LTR layout and switching were verified through Avalonia Headless.

## Please verify the desktop workflow

For a manual visual check, run:

```sh
dotnet run --project src/ProjectOperations.Desktop
```

1. **Create project:** create `Atlas` with company, owner and notes; choose
   `VC Investment Review`. Reopen it from All projects. Check the four localized groups
   contain **6 / 6 / 3 / 1** requirements (16 total).
2. **Requirements & files:** open Pitch deck, add a local file reference and notes.
   Check its name/path/size/added time. Remove an association and confirm the original
   file still exists. Attach it again.
3. **Readiness:** mark one requirement Provided, one NeedsReview and one Complete.
   Check only Complete contributes to readiness: one complete item is **6.25%** of
   the 16 requirements (the display may round it). Missing/NeedsReview must be listed.
4. **Tasks & dates:** create one task due yesterday and another due tomorrow, using
   local `yyyy-MM-dd HH:mm` dates. Edit a task. Check Attention shows one overdue
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
inputs remain LTR. Display dates are locale-aware Gregorian; Jalali display is not
implemented. Enter dates as `yyyy-MM-dd HH:mm` in either language.

Language changes must not modify project names, notes, stored enum values, file
paths, timestamps or task/proposal data. Agent prose follows the selected language,
but action identifiers, `task-proposals`, JSON field names and ISO deadlines retain
their stable contract. Changing language is unavailable while an agent job runs.
