<!-- toolkit:sha256=d0f67cb28e23c8e581bfb01dcf6e804bcbff63c3ee20006c2456342d017722e0 -->
# .ai/ - Project Context for AI Agents

At workspace-router step 3, select one route below; defer its documents until
step 7, then follow the route exactly in order.

## Files

| File | Read when you're... |
| --- | --- |
| `ongoing/README.md` | continuing an in-progress task; read only the matching task document |
| `technical-debt/README.md` | locating unresolved technical debt relevant to the current work; read only the matching record |
| `product.md` | working on VC/finance product direction, project-operations experience, or terminology |
| `mvp.md` | implementing or verifying the core workflow, safety, scope, or acceptance criteria |
| `architecture.md` | working on project/domain services, scheduling, documents, runtime integration, activity translation, persistence, or infrastructure |

## Rules

- This folder is the local source of truth for AI task context in `unnamed-harness`.
- Keep file descriptions and task routes updated when project-specific decisions or boundaries change.
- Read only the files selected by the applicable task route or an exact user-named file.

## Task routes

- Continuing in-progress work: `ongoing/README.md` → only the matching task document → source.
- Product / UX: `product.md` → `mvp.md` → source.
- MVP workflow / safety / acceptance: `product.md` → `mvp.md` → `architecture.md` → source.
- Domain services / scheduling / documents / runtime / persistence / infrastructure: `mvp.md` → `architecture.md` → source.
- When an ongoing task crosses one of these boundaries, read that boundary's route before source unless the task document already includes it.
- Add focused task routes here when another real project boundary appears.
- Route format: `local-context.md` → optional shared/domain context → source.
- When no route applies, inspect the requested source file directly.
