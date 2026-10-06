<!-- toolkit:sha256=7ee2c2deb8e0417cdbc8862e0eb7b59a88d3cf40a824d3440e86b82dd7accd37 -->
# unnamed-harness

## Scope

Primary project in this workspace.

## Durable guardrails

- Build a desktop project-operations assistant for an overloaded VC/finance professional, not a coding interface or a broad project-management suite. Reduce cognitive load rather than adding administrative complexity.
- Validate: Define project → Add documents and information → Track tasks and deadlines → Agent understands → Identifies work, risks, and gaps → Performs delegated analysis or preparation → User reviews → Project state and tasks are updated.
- C#/.NET and Avalonia UI are the chosen MVP stack. SQLite owns structured application state; files and LLM output are not the sole source of project truth.
- OpenCode is the first runtime implementation behind `IAgentRuntime`. UI and product concepts must not depend on OpenCode-specific APIs or session terminology.
- Keep projects, documents, tasks, milestones/events, project state, and agent jobs explicit. Dashboard views must be queryable without an LLM call for every view.
- Scheduling is first-class; start with internal dates, meetings, deadlines, expected responses, reporting dates, and recurring reviews, not external calendar integration.
- Preserve visible autonomy, project-oriented activity, optional technical details, and trustworthy Stop/cancellation. Agent-generated tasks are proposals requiring user approval before commitment.
- Start with one .NET desktop application. Add a separate daemon only if a concrete technical need appears; do not prematurely generalize runtimes or workflows.
- Scope validation to investment/portfolio project operations. Broader generic use cases are not MVP requirements.

## Focused context

- Product intent and interaction principles: `.ai/product.md`.
- MVP scope, workflows, and acceptance criteria: `.ai/mvp.md`.
- System boundaries and chosen technology: `.ai/architecture.md`.
- These documents record intended requirements and decisions, not evidence that features are implemented.

## Ownership boundaries

- This project owns its implementation details and local conventions.
- Cross-project contracts belong in shared workspace context.
