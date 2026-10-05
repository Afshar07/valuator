# Product intent and experience

## Working concept

A creative development harness for non-engineers.

> Serious engineering underneath, playful creative experience on top.

Preserve the freedom and capability developers get from CLI coding agents without requiring users to think like developers. This is not another IDE, coding chatbot, no-code builder, or prettier OpenCode GUI. OpenCode is an implementation, not the product.

FinApp is the first real-world validation project. Keep the product generic rather than baking FinApp behavior into the harness.

## Initial user

The initial test user is a highly creative designer with strong product and visual ideas who is comfortable describing desired outcomes and already uses Codex Cloud to build a native Android app. They do not have deep engineering or infrastructure knowledge.

Do not try to turn this user into a developer. They should not need to understand branches, worktrees, Gradle, terminals, CI, shell commands, or architecture internals. Show enough agent reasoning and activity to make the process understandable and trustworthy.

## Product philosophy

Avoid both extremes: a restricted chat wrapper that hides autonomy, and a developer environment that overwhelms non-engineers.

The experience should feel like experimenting, creating, remixing, exploring, and watching something being built—not operating Git, reviewing terminal output, or configuring infrastructure. Real development tools remain underneath.

## Core interaction

Idea → Explore → Agent works autonomously → Working result → Preview / Build → Keep / Remix / Discard.

Use product vocabulary: Idea, Experiment, Build, Preview, Keep, Remix, Undo, Discard, Compare, Archive. Developer terminology remains optional in progressive technical details. Vocabulary and future concepts do not automatically expand MVP scope.

The main prompt is **What do you want to make?** Users describe outcomes in ordinary design/product language, for example: “Make the bank cards feel more physical and playful.” Primary actions are **Build it** and **Explore ideas**.

The artifact/result is the primary object; chat must not dominate the interface.

## Freedom modes

- **Guided:** asks before consequential actions.
- **Independent:** can inspect, edit, run builds, install dependencies, and use Git locally; interrupts for significant external or destructive actions.
- **Playground:** explicit broad freedom inside an isolated experiment. Communicate: “Try something ambitious. It is safe to throw away.”

These modes must map to actual runtime permissions, not cosmetic UI states. Exact permission rules are an implementation decision constrained by safety requirements.

## Transparency and trust

Translate technical activity into human-readable intent, with **Show details** always providing access to real actions:

| Technical activity | Human-friendly presentation |
| --- | --- |
| Reading `HomeScreen.kt` | Understanding the current screen |
| Searching for `BankCard` | Finding the shared bank card component |
| `./gradlew assembleDebug` | Building the Android app |

Typical states: Understanding the project, Exploring the current implementation, Finding the relevant component, Editing the design, Running checks, Building the application, Checking the result.

**What's happening?** is a core differentiator. Explain the approach, not just activity volume. Example: “The agent found that all bank cards use one shared component. It is updating that component instead of changing each screen separately.”

Expose files viewed/changed, commands executed, dependency changes, database/schema touches, and approximate risk. Statements such as “Stored data has not been modified” or “Risk: Low” require supporting evidence; they are examples, not defaults.

Keep **Stop** clearly visible while work is running. Autonomy feels safer when users can immediately interrupt it. Keep/discard must be trustworthy.

## Visual direction

Prefer a large creative prompt, clear agent state, artifact previews, experiment cards, simple decision buttons, smooth transitions, understandable statuses, and optional technical detail. Put the result above chat in the visual hierarchy.

Avoid dense file trees, persistent terminal panels, IDE chrome, Git terminology in the main flow, raw log spam, and configuration-heavy screens.

Illustrative primary screen elements:

- Project name and readiness state.
- Creative prompt with Build it / Explore ideas.
- Guided / Independent / Playground selector.
- Activity progression and current state.
- What's happening? explanation, risk, viewed/changed/command counts.
- Show details and a clearly visible Stop action.

Illustrative result: Experiment complete; build/check outcomes; changed-file count; Open preview; Keep / Remix / Discard. Report actual outcomes rather than assuming success.

## Long-term direction, not MVP commitments

A creative interface for autonomous software development for designers, founders, product people, and other non-engineers with strong ideas.

Potential capabilities: visual variations, screenshot feedback, drawing/annotation over UI, before/after previews, experiment comparison, “What if?” idea generation, creative agent personalities, design critique, automatic phone/emulator preview, remote mobile companion, and richer project adapters.

Possible future agent roles: Builder, Critic, Simplifier, Chaos, Engineer. Do not implement these merely because they are listed here.
