# MVP scope and acceptance

## Validation goal

Prove that a non-engineer can open a real software project, describe an idea, let a capable autonomous coding agent work, understand what it is doing, and safely keep or discard the result.

Keep scope tied to this validation: **Describe → Agent works → Understand → Preview → Keep or discard.** This document describes intended scope, not current implementation status.

## Required workflow

### Open project

Select a local software repository. Detect basic project information, locate existing agent instructions, understand the Git repository, and determine supported capabilities.

Relevant context includes `.ai/`, `AGENTS.md`, `README.md`, and project-specific agent rules. Support existing routed `.ai` structures naturally; do not assume every context file should be loaded indiscriminately.

### Describe and run

Prominent creative prompt, Build it / Explore ideas actions, and Guided / Independent / Playground modes. Use OpenCode initially while keeping product concepts runtime-independent.

Let the agent do real development work under the selected permissions. Translate activity into understandable states, retain access to real technical actions, and expose What's happening? summaries.

### Stop and safety

- A clearly visible Stop action is always available during agent work.
- Each major run is an Experiment, safely isolated so ambitious work feels disposable.
- Keep and discard must be reliable. Discard must return the project to its prior state without losing unrelated user work.
- Isolation mechanics and exact stop behavior must be resolved during implementation; UI wording alone does not establish safety.
- Do not claim repository isolation also reverses external effects. Permission decisions must account for significant external and destructive actions.

### Results and artifacts

Show a concise completion state, actual build/check outcomes, changed-file count, supported artifacts, and Open preview / Keep / Remix / Discard actions.

Provide plain-language change summaries, including relevant areas not changed when evidence supports that claim. Example changed areas: shared bank card component, animation state, spacing tokens. Example unchanged areas: transaction storage, database schema, networking.

Technical diffs remain available through Show details. Build/check success must not be shown when a check failed or was not run.

Support meaningful results for selected project types:

- Initial Android support: debug build and APK discovery. Emulator/device preview is eventual, not an MVP requirement.
- Generic support: configured commands, repository changes, diffs, and agent execution.
- Web examples: local preview, browser screenshot, dev server. These do not require immediate implementation of all web adapters.

### Experiments and history

User-facing experiment concepts include Keep, Remix, Discard, Compare, Archive, without requiring Git knowledge. Keep / Remix / Discard are the primary result actions; advanced comparison is excluded below. Detailed Compare / Archive behavior is not yet specified.

Maintain lightweight history recording prompt, status, creation time, result, disposition (kept, remixed, discarded, archived), and associated agent session. Do not turn history into project management.

## Explicit exclusions

Do not build these unless necessary for core validation:

- Embedded code editor or full terminal emulator.
- GitHub issue management or CI/CD configuration UI.
- Figma integration or visual screenshot annotation.
- Multi-agent orchestration or complex agent teams.
- Plugin or theme marketplaces.
- Cloud hosting, collaboration, or mobile companion app.
- Automatic visual regression or advanced screenshot comparison.
- Sophisticated generated variations.
- Broad project-management functionality.

Do not prematurely add every platform adapter or a separate backend/native daemon.

## Success criteria

1. **Non-engineer usability:** a designer completes the core workflow without understanding Git branches/worktrees, shell commands, Gradle, terminal usage, or runtime internals.
2. **Genuine agent freedom:** a developer inspecting underlying actions recognizes a real CLI-style autonomous agent, not a constrained no-code builder.
3. **More experimentation:** the user intentionally tries more ambitious or unusual ideas because attempts feel cheap, reversible, understandable, and safe. This behavior matters more than feature count.

## Decisions still needed before implementation

The summary does not settle precise permission mappings, experiment/Git lifecycle mechanics (including existing uncommitted work and keep conflicts), Stop/process cancellation semantics, Explore ideas behavior, Remix semantics, or basic Compare/Archive interactions. Resolve these at the relevant boundary rather than treating illustrative labels as complete contracts.
