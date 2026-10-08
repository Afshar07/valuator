# Architecture and technical boundaries

## Status and chosen MVP stack

The first MVP now implements these boundaries in `src/ProjectOperations.Core`,
`src/ProjectOperations.Infrastructure` and `src/ProjectOperations.Desktop`.
The broader concepts below remain direction rather than a claim that every future
scheduling or document-analysis capability exists.

- **C#/.NET:** main application and core services. A strong fit for filesystem/process handling, async and background work, cancellation, IPC, local services, and future system integrations.
- **Avalonia UI:** cross-platform desktop UI while keeping the main application in C#. The application is fundamentally a desktop project/work orchestration tool.
- **SQLite:** local application-owned structured state, queryable independently of the agent.
- **OpenCode:** initial autonomous agent runtime behind a generic runtime boundary, not the product or the domain model.

Use normal .NET capabilities where suitable: `Process`, `FileSystemWatcher`, `CancellationToken`, `IAsyncEnumerable<T>`, `Channel<T>`, and `HttpClient`, alongside SQLite. These are available tools, not a mandate to use all of them. Keep one desktop application for the MVP; introduce a separate daemon only for a concrete technical need.

## Layering

```text
Avalonia UI
    ↓
Application / Core
    ↓
Project services
Task / scheduling services
Document services
Agent runtime abstraction
Persistence
    ↓
OpenCode / filesystem / processes / SQLite
```

UI consumes application/domain operations and must not directly depend on OpenCode-specific APIs. Application services coordinate project context, jobs, review, and persistence; infrastructure handles runtime communication, files, processes, and storage.

## Core domain and services

- **Project:** company/project name, stage, status, owner, important contacts/dates, notes, open questions, and current state. A project is an investment/portfolio work context, not a software repository.
- **ProjectDocument:** project association, file reference and metadata, category, and context needed to track expected/present documents. Initial categories include pitch deck, financial plan / FP, cap table, board structure, shareholder documents, investment memo, contracts, KPI/reporting files, and other supporting material; allow additional categories.
- **Task:** project association, title, status, and deadline. Keep agent proposals separate from committed tasks until user approval.
- **Milestone / ProjectEvent:** internal scheduling concepts for deadlines, meetings, expected responses, reporting dates, recurring reviews, and other milestones. Exact relationship and recurrence model remain open.
- **ProjectState:** explicit, persisted/queryable current state supporting overviews and attention queries, including open questions and follow-ups. Derived counts should reflect stored records rather than opaque LLM output.
- **AgentJob:** project-scoped delegated request/workflow, lifecycle, result/history, and proposals/review state.
- **AgentRuntime:** infrastructure capability accessed through `IAgentRuntime`, not a user-facing project object.

Project services own metadata and state operations; task/scheduling services own workload and date queries; document services own file association, categorization, expected-document visibility, and eventual extraction. Do not finalize parsers or introduce software-repository adapters here.

## Agent runtime boundary

`IAgentRuntime` owns runtime integration; the first implementation is OpenCode-backed (conceptually `OpenCodeAgentRuntime`). Keep runtime sessions, transport, events, permissions, authentication, and cancellation behind this boundary. Product requests and results should describe analysis, monitoring, preparation, and task suggestions rather than coding sessions.

Assemble relevant project metadata, documents, tasks, deadlines, state, and previous results for a job. Distinguish unavailable/unreadable content from content actually inspected. Determine access and tool permissions explicitly; never assume a coding runtime's defaults are suitable for sensitive finance work or that it enforces restrictions it cannot support.

The user intends to use a Codex subscription through OpenCode **where supported**. Confirm actual provider/subscription support and authentication when implementing integration; this is not an assumed compatibility guarantee.

Potential future runtimes include Codex CLI and Claude Code. Do not build a plugin framework or excessive cross-runtime abstraction before a concrete need exists. The transport/API and exact runtime contract remain implementation decisions.

## Jobs, approval, and cancellation

Run long-lived work without blocking UI interaction. Model job progress and completed, failed, and cancelled outcomes explicitly; use .NET async/process/cancellation capabilities as appropriate.

Analysis results are not automatically authoritative state. Application services own review and application of proposed updates. Agent-generated tasks require user approval before persistence as committed tasks. Define how concurrent user edits and stale job context are handled before applying updates; raw runtime output must not silently overwrite project truth.

A visible Stop action must reach actual runtime/process cancellation. Decide cancellation propagation, termination escalation, partial-result handling, restart recovery, and persistence behavior before promising reliable stopping. Cancellation is not rollback of already completed effects. Protect original files and unrelated work; do not grant destructive or external-communication capabilities merely because the runtime supports them.

## Activity translation

Treat translation as a first-class boundary rather than scattered UI conditionals:

```text
Raw agent/tool event → ActivityTranslator → Project-oriented activity
```

Examples: Reading pitch deck, Reviewing financial plan, Comparing board information, Preparing due-diligence summary, Checking open tasks. Preserve raw details in a secondary view. Titles, descriptions, status, evidence, and uncertainty must reflect actual activity; do not fabricate progress, explanations, or safety claims.

## Persistence and scheduling

SQLite stores application-owned structured state: projects, project documents/metadata, tasks, milestones/events, project state, agent jobs, results/history, and settings. Filesystem content and LLM output are not the only source of truth.

Attention dashboards, project overviews, and internal upcoming-work views query persisted state and derived workload/date information without an LLM call for every view. Scheduling is first-class but external calendars are outside MVP scope.

The first-MVP choices below resolve its schema, data-access, migration, file-reference
and date rules. Retention controls, backup/recovery, recurrence and richer urgency
ranking remain future decisions. Separate runtime session metadata from
application-owned job history. No cloud hosting or multi-user infrastructure is required.

## First MVP implementation contracts

- .NET 10, Avalonia 11.3, Microsoft.Data.Sqlite 10.0; native xUnit tests.
  See `.ai/testing.md` and `docs/verification.md` for verification boundaries.
- Core owns project aggregates, the built-in `VC Investment Review` template,
  requirement definitions/statuses, task/milestone/state models, deterministic
  summaries, agent contracts and application-level proposal approval. UI does not
  reference runtime DTOs. OpenCode-specific construction stays in the composition root.
- Readiness counts **Complete only**. Missing/NeedsReview need attention; Provided
  is supplied but not verified. File association never changes status automatically.
- Active tasks are Todo/InProgress. Overdue is strictly before the supplied instant;
  upcoming is inclusive from now through seven days. Done/Cancelled tasks and
  Completed/Archived projects do not contribute to dashboard deadline attention.
- UTC/offset instants are persisted losslessly. UI accepts local dates with an
  explicit format and rejects ambiguous/nonexistent daylight-saving times.
- SQLite schema version 1 uses transactional `PRAGMA user_version` migration,
  normalized aggregate tables, foreign keys, WAL and optimistic project revisions.
  Agent job snapshots live in a project-associated table. Approved tasks and their
  proposal review outcomes commit atomically. Native SQLite work runs off the UI thread.
- Files stay in their original locations. Persist references, size and timestamps;
  removal only deletes the association. No extraction, OCR or managed storage layer.
- Agent context is a snapshot of the selected project only, including values/notes,
  dates, file paths and up to three recent completed result excerpts (12,000 characters
  each). Prior output is labeled unverified history, not current fact. Every request
  requires a context preview and consent to the exact displayed snapshot.
  Runtime/provider transmission is explicit, not a local-only inference promise.
- `IAgentRuntime` exposes awaited Run and Cancel operations with native models and
  cancellation tokens. OpenCode V2 HTTP/SSE stays in Infrastructure; a dedicated
  explicitly configured loopback runtime, deny-tool policy and isolated deployment
  are required. `docs/agent-runtime.md` is the exact protocol/deployment contract.
- Running jobs retain partial text and final outcomes. Stop waits for independently
  acknowledged backend interruption/idle; unconfirmed stop is failure, not Cancelled.
  Stale running history becomes Interrupted, without claiming the backend stopped.
- Task proposals use a bounded `task-proposals` JSON block. Approval reloads current
  project state, never overwrites metadata from the agent snapshot, and is idempotent.
- Runtime settings come from environment variables; the desktop language and theme
  preferences are stored locally in `settings.json` in the application data directory
  (each preference rewrites only its own key, atomically). There is no
  accounts/settings platform. Backup, encryption and transcript-retention controls
  are not implemented.

## Desktop localization

- Desktop localization services own embedded English (`en`) and Persian (`fa`)
  resources, stable presentation keys, domain display mapping, and date formatting.
  Views do not load resource files. Missing translations fall back to English, then
  a visible key marker. English is the deterministic default.
- Domain enums, identifiers, stored template definitions, database values, runtime
  protocol fields and structured proposal JSON keys remain language-neutral. Map
  built-in template labels by stable identifiers at the presentation boundary;
  preserve user-authored content and custom labels.
- Localization owns inherited window RTL/LTR (Persian RTL, English LTR). Language
  switching updates the existing controls without discarding unsaved form values.
  Technical previews, paths and date-entry fields remain readable in LTR.
- Persist dates as timezone-safe `DateTimeOffset` instants. Format display dates
  according to locale; date entry remains explicit Gregorian `yyyy-MM-dd HH:mm`.
  Jalali display is deferred and belongs in the presentation date formatter.
- Agent response language is an explicit preference derived from the selected
  locale, separate from stable internal instructions, action identifiers and the
  structured output contract. Never translate structured JSON after generation.

## Desktop presentation system

- Figma file `fZ9yGw5Nzv6n5OTl1O9PZx` is the MVP visual source of truth;
  Dashboard (`4:2`) and Project Detail (`4:157`) establish the visual system. The
  Valuator design (Claude Design handoff, 2026-10-07) extends it: the light tokens keep
  the Figma foundations with the teal accent, dark tokens are derived, and AI-owned
  surfaces use a separate violet so proposals never read as committed data.
- `PresentationTheme` owns every raw color, radius and type size. Views bind color
  tokens as dynamic resources, so Light/Dark/Auto switches live (Auto follows the OS).
  Inter (OFL, Latin subset), IRANYekanX and Phosphor icons (MIT) are bundled under
  `Assets/Fonts` with their licenses. English uses Inter; Persian uses IRANYekanX.
- `MainWindow` owns the shell: sidebar navigation (Needs attention, All projects,
  Calendar, Documents, Settings), the page host, a modal layer and the assistant
  panel. Focused views own screen construction and existing editing flows. This split
  does not introduce a separate MVVM framework or change service boundaries.
- The assistant is a project-scoped panel docked at the end edge (it overlays the
  content below 1280 px). It keeps one set of request, preview and consent controls per
  open project. Every run still needs a request, an explicit preview of the exact
  context snapshot and consent; changed project state invalidates consent. Proposals
  are reviewed only in the panel's tray (one approval control per proposal); Delegate &
  review holds the job history. Without a configured runtime the panel shows setup
  guidance and no Run action.
- Calendar and the dashboard agenda use `ProjectSummaries.Schedule` (open dated tasks
  and milestones of Active/OnHold projects); the projects table uses `NextDeadline`.
  Documents lists stored file references only: existence is checked, contents are
  never read or previewed, and open/reveal are explicit user actions. Settings shows
  runtime configuration and the database path read-only; backup and encryption are
  shown as not built.
- RTL/LTR remains localization-owned and inherited by presentation layouts.
  Live language switching updates controls in place, not by rebuilding screens.
  Paths, explicit date entries and technical/context previews stay LTR.
- **Valuator v3 presentation (2026-10-08).** The dashboard lists live projects that still have
  open tasks (most overdue first) beside this week's agenda. Project editing (name, company,
  owner, stage, status, notes, current state), tasks and milestones use modal dialogs
  (`ProjectDialogs.cs`); the requirements tab is an in-place accordion (open an item to link a
  file or enter a value, set its review status, add a follow-up task). Task due dates are
  whole days: a newly chosen date is stored at 23:59 local time so it only becomes overdue the
  next day; existing times of day are kept.
- A task may reference the requirement it follows up on (`ProjectTask.RequirementId`, SQLite
  schema version 2; version-1 databases are migrated in place with `ALTER TABLE`).
- Blank projects (`ProjectService.CreateBlankAsync`, template id `blank`) have no built-in
  checklist; requirements are added by the user into the single `custom` group.
- First run: with no projects the shell shows the welcome screen over the project list; the
  wizard creates a VC-template or blank project. The sidebar only offers Projects and
  Settings until there is dated work (Needs attention, Calendar) or a linked file (Documents);
  sections that appear later are flagged New and announced with a toast. A getting-started
  card tracks five milestones. View-state such as dismissed hints lives in `UiState` for the
  running session only.
- "Explore a sample deal" runs on `SampleWorkspace`: its own temporary SQLite database and
  placeholder files, a runtime that always refuses, and the assistant reported as not
  configured. Leaving it (or exiting the app) deletes the temporary data; the user's database
  is never opened by the sample.
