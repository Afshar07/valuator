# Architecture and technical boundaries

## Status and chosen MVP stack

These are chosen directions and conceptual boundaries, not implemented code. Entity and interface names do not prescribe exact APIs or database schemas.

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

Exact schemas, data-access library, migrations, document copy-versus-reference strategy, retention, backup/recovery, recurrence/time-zone rules, and urgency ranking remain open. Separate runtime session metadata from application-owned job history. No cloud hosting or multi-user infrastructure is required by this architecture.
