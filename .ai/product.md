# Product intent and experience

## Working concept

A **desktop project-operations assistant for an overloaded VC/finance professional**.

> Let the user define structured VC/finance projects, attach the relevant documents and information, then delegate project-monitoring, analysis, preparation, and follow-up work to an autonomous AI agent while retaining clear oversight and control.

OpenCode is infrastructure, not the product. The user experiences project analysis, delegated work, summaries, monitoring, preparation, and task suggestions—not coding-agent sessions or shell operations. These documents describe intended requirements, not implemented features.

## Primary user and outcome

The initial user works in VC finance, manages many investment/portfolio projects simultaneously, and carries too much project state mentally. Work is document-heavy, with deadlines, open questions, follow-ups, meetings, reviews, boards, and investment decisions to prepare for.

Reduce cognitive load by offloading analysis, organization, preparation, and monitoring. Do not make the user operate a complicated project-management system or understand runtime internals.

## Core workflow

Define project → Add documents and important information → Track tasks and deadlines → Agent understands the project → Agent identifies work / risks / missing information → Agent performs delegated analysis or preparation → User reviews the result → Project state and tasks are updated.

Structured project state, not chat history or a folder alone, anchors the experience. Use project vocabulary: Project, Document, Task, Deadline, Milestone, Open question, Follow-up, Brief, Agent job, Result, and Review.

## Main experiences

### Define and organize a project

Create a project with company/project name, stage, status, owner, important contacts and dates, notes, open questions, and current state. Optionally specify expected document types.

Add and categorize project files, and distinguish expected documents that are present from those that are missing. Initial category examples include pitch deck, financial plan / FP, cap table, board structure, shareholder documents, investment memo, contracts, KPI/reporting files, and other supporting material. Categories must be extensible rather than a hardcoded exhaustive list.

### Understand current state

Each project has an explicit overview, for example:

```text
Investment stage: Due diligence
Next milestone: IC review
Due: Oct 14

Open questions: 4
Missing documents: 2
Pending follow-ups: 3
Overdue tasks: 1
```

These are illustrative values, not defaults. Structured state must remain queryable without an LLM call for every dashboard view. Show when information is missing or stale rather than inventing certainty.

### Know what needs attention

The home screen answers **What needs my attention?** Prioritize workload and urgency: overdue tasks, upcoming deadlines, missing documents, unresolved project work, pending follow-ups, and upcoming meetings/milestones. Raw file browsing is secondary.

Tasks belong to projects and minimally have title, status, deadline, and project association. Agent-generated tasks start as proposals requiring user approval before commitment to project state.

### Schedule work

Scheduling is first-class. Projects can contain deadlines, meetings, expected responses, reporting dates, recurring reviews, and other milestones. Support an eventual internal agenda such as:

```text
THIS WEEK
Mon — Nova financial plan review
Tue — Atlas founder follow-up
Wed — Orbit IC preparation
Thu — Atlas board review
```

External calendar integration is not required for the MVP. The one exception is an opt-in, read-only Google Calendar overlay in Calendar; it is off until the user connects and changes nothing for users who do not.

### Delegate useful work

The key prompt is **What can you take off my plate?** Support freeform requests and useful predefined workflows: summarize a project, identify missing documents/information, compare documents, detect inconsistencies, extract action items, prepare a meeting/board brief or due-diligence summary, suggest what needs attention, and suggest delegable work.

The agent works from metadata, documents, tasks, deadlines, project state, and previous relevant results. It should eventually distinguish work it can perform autonomously, work requiring approval, and decisions only the user can make. Analysis and preparation may be delegated; investment decisions remain with the user. Follow-up preparation and internal tracking do not authorize external communication.

## Visible autonomy and trust

Translate real activity into project-oriented intent rather than presenting raw technical actions as the main UX:

```text
✓ Reading pitch deck
✓ Reviewing financial plan
✓ Comparing board information
● Preparing due-diligence summary
○ Checking open tasks
```

Explain what is happening and why, distinguish observed facts from inference, and expose supporting documents and uncertainty in results. Do not fabricate progress, conclusions, or claims about unaffected data. Technical details remain available through a secondary **Show details** view.

Keep **Stop** clearly visible during agent work. Cancellation behavior must be trustworthy; do not claim work has stopped while it continues. Review results before applying proposed changes to structured project state; task proposals always require approval. Retain lightweight job/result history so the user can understand what was delegated and what happened.

## Experience principles and long-term boundary

The product reveals itself progressively: a new workspace offers only Projects and Settings, and sections such as Needs attention, Calendar and Documents appear (flagged New) when there is data for them. Hints are dismissible and the sample workspace never saves anything.

Prefer a calm attention dashboard, concise project overviews, actionable results, clear approval controls, and understandable activity over chat-heavy screens, dense file trees, raw logs, or configuration-heavy workflows.

The long-term generic assistant idea may remain, but the MVP is specifically VC/finance project operations. Broader use cases do not justify software-building workflows, broad project-management features, or premature automation in this MVP.
