<!-- toolkit:sha256=7ee2c2deb8e0417cdbc8862e0eb7b59a88d3cf40a824d3440e86b82dd7accd37 -->
# unnamed-harness

## Scope

Primary project in this workspace.

## Durable guardrails

- Build a creative interface for autonomous software development, not an IDE, coding chatbot, no-code builder, or prettier OpenCode GUI.
- Validate the core loop first: Describe → Agent works → Understand → Preview → Keep or discard.
- Flutter/Dart desktop is the chosen MVP stack. SQLite stores application state; Git remains the source of repository state.
- OpenCode is the first runtime implementation, behind a generic runtime boundary. Project-specific behavior belongs in adapters; FinApp is only the initial test case.
- Preserve genuine autonomy and optional access to technical details. Translate engineering activity rather than concealing it.
- Isolation, a visible Stop action, and trustworthy keep/discard behavior are core requirements, not polish.
- Start with a single Dart application. A separate local daemon is a future option only if concrete system complexity justifies it.

## Focused context

- Product intent and interaction principles: `.ai/product.md`.
- MVP scope, workflows, and acceptance criteria: `.ai/mvp.md`.
- System boundaries and chosen technology: `.ai/architecture.md`.
- These documents record intended requirements and decisions, not evidence that features are implemented.

## Ownership boundaries

- This project owns its implementation details and local conventions.
- Cross-project contracts belong in shared workspace context.
