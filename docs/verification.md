# MVP verification

## Automated verification

Verified on 2026-10-06 with .NET SDK 10.0.300 on Windows:

| Check | Result |
| --- | --- |
| `dotnet build ProjectOperations.slnx --no-restore --nologo` | Passed; zero warnings/errors |
| `dotnet test ProjectOperations.slnx --no-restore --nologo` | 86 passed: 75 domain/application/SQLite/runtime tests + 11 Avalonia headless tests |
| `dotnet format ProjectOperations.slnx --no-restore --verify-no-changes` | Passed |
| `dotnet list ProjectOperations.slnx package --vulnerable --include-transitive` | No known vulnerable packages reported |
| `git diff --check` | Passed |

Coverage includes the exact template, independent requirement instantiation,
completion/missing rules, timezone-aware deadline boundaries, SQLite reopen/WAL/
rollback/stale-edit protection, atomic task approval, proposal parsing, selected-
project context isolation, runtime policy/SSE/cancellation/failure fixtures, and
native headless create/edit/task/consent/proposal/Stop/close interactions.

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

Manual GUI layout and native file-picker interaction were not verified, as requested.

## Please verify the desktop workflow

Manual desktop interaction is intentionally left to you, as requested. Run:

```sh
dotnet run --project src/ProjectOperations.Desktop
```

1. **Create project:** create `Atlas` with company, owner and notes; choose
   `VC Investment Review`. Reopen it from All projects. Check the four Persian groups
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
