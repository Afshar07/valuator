# MVP scope and acceptance

## Validation goal

Prove that an overloaded VC/finance professional can organize multiple investment/portfolio projects, see what needs attention, and delegate useful analysis, preparation, and monitoring while retaining oversight.

Validate: **Define project → Add documents and information → Track tasks and deadlines → Agent understands → Identifies work, risks, and gaps → Performs delegated work → User reviews → Project state and tasks are updated.** This is intended scope, not current implementation status.

The chosen MVP stack is **C#/.NET, Avalonia UI, SQLite, and OpenCode behind `IAgentRuntime`**. Start with a single desktop application, not a separate daemon.

## Required scope

1. **Project creation and metadata:** company/project name, stage/status, owner, contacts, important dates, notes, open questions, and current state; optional expected document types.
2. **Project document management/categorization:** add files to projects and categorize them. Initial VC document categories are examples, not a closed list.
3. **Missing-document visibility:** show which expected document types are present or missing. File presence is not proof that its content is adequate or verified.
4. **Tasks and deadlines:** project-associated tasks with title, status, and deadline. Internal scheduling covers deadlines, meetings, expected responses, reporting dates, recurring reviews, and milestones; exact recurrence behavior remains to be specified.
5. **Project overview/current state:** explicit stage/status, next milestone/date, open questions, missing documents, follow-ups, and overdue work. Query structured state without requiring an LLM call for each view.
6. **Attention dashboard:** prioritize overdue tasks, upcoming deadlines, missing documents, unresolved work, pending follow-ups, and upcoming meetings/milestones across projects; support a lightweight internal upcoming-work view, not an external calendar dependency. The implemented home lists live projects that still have open tasks (most overdue first, with each project's next task) beside this week's agenda; missing documents surface in each project's Overview and checklist.
7. **First-run experience:** an empty workspace opens a welcome screen (start a first project, or explore a throwaway sample workspace) and a three-step wizard (name, VC or blank checklist, first item). Needs attention and Calendar appear once something is dated, Documents once a file is linked; a getting-started card tracks five milestones. Blank projects have no built-in checklist; the user adds requirements.
7. **Agent access to project context:** metadata, available document content, tasks, deadlines, current state, and previous relevant agent results. Surface unreadable or unsupported documents; parsing/extraction details are not decided here.
8. **Freeform agent prompt:** delegate project work in ordinary finance/project language, including “What can you take off my plate?”
9. **Useful predefined agent workflows:** project summary, missing information/documents, document comparison, inconsistency detection, action-item extraction, meeting/board brief, due-diligence summary, attention suggestions, and delegable-work suggestions.
10. **Visible agent activity and Stop:** finance/project-oriented activity, clear running/completed/failed/cancelled outcomes, optional technical details, and a visible Stop action while running.
11. **Agent-proposed tasks requiring approval:** proposals are distinct from committed tasks. Review results and proposed project-state updates; an agent task proposal must not silently become an approved task.
12. **Lightweight agent job/result history:** retain project association, request/workflow, status, timestamps, result, and proposal/review outcome where applicable. Runtime session identifiers are implementation metadata, not the primary experience.

## Oversight and safety

- Keep delegated analysis/preparation separate from user decisions and approval of structured-state changes.
- Protect source documents and unrelated user files; do not treat agent access as authorization for destructive edits.
- Deleting a project is a user-only destructive action: it requires an explicit confirmation naming the project, removes only application-owned records (never linked files), and is never offered to or performed by the agent.
- Distinguish missing information, unsupported extraction, inferred conclusions, and verified facts. Report actual job outcomes and provenance rather than assuming success.
- Stop must initiate real cancellation. Resolve process termination, partial results, failure recovery, and state-write behavior before claiming reliable cancellation; cancellation does not automatically undo prior effects.
- Sensitive finance documents require explicit decisions about runtime access, provider transmission, credentials, and retention before implementation at those boundaries.
- Internal follow-up tracking and preparation do not include automatic external communication.

## Explicit exclusions

- Email integration.
- Google Calendar or other external calendar integration.
- Slack and CRM integrations.
- Automatic external communication.
- Portfolio-wide financial analytics.
- Editing complex financial models.
- Collaboration/multi-user features.
- Advanced workflow automation.
- Multi-agent orchestration.
- Broad project-management functionality beyond this focused workflow.
- Cloud hosting requirements unless implementation later demonstrates a concrete need.

Do not introduce software-repository adapters, build/preview workflows, or a separate daemon as MVP requirements.

## Success criteria

1. A VC/finance user can create projects, attach/categorize documents, define expected documents, and track tasks and internal dates without runtime knowledge.
2. The dashboard answers what needs attention across projects from stored structured state, not a fresh LLM request per view.
3. The agent produces useful project analysis or preparation from relevant context, with visible activity, source references where available, uncertainty, and truthful outcomes.
4. The user can stop a job, review its result, approve or reject proposed tasks, and understand resulting state changes through lightweight history.
5. The loop demonstrably reduces the user's mental project-tracking and preparation burden rather than adding administrative work.

## Decisions still needed before implementation

Resolve exact field/status models, document storage and extraction/format support, scheduling/recurrence and urgency rules, runtime transport/authentication/capabilities, permissions and sensitive-data handling, Stop/recovery semantics, approval and state-update mechanics, and SQLite schemas/migrations/retention at their respective boundaries. These open implementation contracts do not reopen the chosen stack or expand MVP scope.
