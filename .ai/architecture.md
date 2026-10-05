# Architecture and technical boundaries

## Status

These are chosen directions and conceptual boundaries, not a description of implemented code. Interface names and entity lists below are illustrative; they do not prescribe exact Dart APIs or schemas.

## Chosen MVP stack

- **Flutter desktop:** prioritize a custom creative interaction experience over deep native infrastructure. Supports playful UI, transitions, experiment cards, animated activity, artifact previews, and future mobile reuse. Desktop performance is sufficient for this product.
- **Dart:** handle child processes, filesystem access/watching, HTTP, WebSocket, Git commands, runtime communication, and local persistence in the MVP. Do not add a second backend runtime without a concrete need.
- **SQLite:** persist application-specific state, not a duplicate of Git-owned repository state.
- **OpenCode:** first agent runtime implementation, behind a generic abstraction.

## Layering

```text
Flutter UI
    ↓
Application / Core
    ↓
Agent runtime abstraction
Project adapters
Experiment service
Git service
Persistence
    ↓
OpenCode / Git / filesystem / processes
```

Keep strong boundaries around Project, Experiment, AgentRuntime, AgentSession, AgentActivity, Artifact, and ProjectAdapter. Flutter widgets must not directly depend on OpenCode-specific APIs.

## Agent runtime

A conceptual `IAgentRuntime` boundary owns integration with the agent runtime. Initial implementation: `OpenCodeAgentRuntime`. Potential future implementations: `CodexAgentRuntime`, `ClaudeCodeAgentRuntime`.

Keep runtime sessions/events/permissions behind this boundary. Product concepts and UI should not be modeled as OpenCode-specific concepts. Freedom modes must translate into real runtime permissions; if the runtime cannot enforce a mode, do not silently pretend it can.

## Project adapters

Project-specific behavior belongs behind a conceptual `IProjectAdapter`. Potential capabilities: `detect`, `run`, `build`, `preview`, `getArtifacts`. Expose detected/supported capabilities rather than assuming all projects support every operation.

Initial adapters:

| Adapter | Scope |
| --- | --- |
| Generic | Basic repository detection, configured commands, file changes, Git diff, agent execution |
| Android | Gradle detection, debug build, APK discovery |

Potential future adapters: Vite/web, Nuxt, Flutter, Node, React Native, games, or other specialized environments. Do not implement them prematurely. FinApp-specific assumptions must not leak into generic core behavior.

## Experiments and Git

The Experiment service exposes creative operations while the Git service owns technical repository operations. Branches/worktrees are possible isolation mechanisms, not a settled implementation contract.

Experiments must be safely isolated. Keep/discard behavior must protect existing work and be trustworthy. Design lifecycle handling, recovery, conflicts, and cancellation before claiming reversible operation. Git isolation does not itself undo installed dependencies, external services, or other side effects outside the isolated repository.

## Activity translation

Treat translation as a first-class subsystem, not scattered UI conditionals:

```text
Raw agent/tool event → ActivityTranslator → Human-friendly AgentActivity
```

Conceptual activity fields: `type`, `title`, `description`, `technicalDetails`, `risk`, `status`.

Preserve raw technical actions for Show details while translating intent for the main UI. Support summaries of files viewed/changed, commands executed, dependency changes, database/schema touches, and approximate risk. Distinguish observed facts from inference; do not fabricate explanations or assert unaffected data without evidence.

## Persistence

Potential SQLite entities: `projects`, `experiments`, `agent_sessions`, `activity_events`, `settings`.

Persist product state, including lightweight history and session associations. Git remains authoritative for repository state. Exact schemas, persistence library, retention rules, and migration strategy are not yet selected.

## Future core extraction

Only if system-level complexity warrants it, the architecture may evolve to:

```text
Flutter UI → local IPC → Native/Core daemon
                         ├ Agent runtime (initially OpenCode)
                         ├ Git
                         ├ Process management / PTY
                         ├ Filesystem
                         └ Project adapters
```

C#/.NET is a strong future daemon candidate for process, filesystem, async, IPC, and desktop tooling capabilities. This is not an MVP dependency or authorization to introduce the split now.
