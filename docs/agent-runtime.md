# OpenCode V2 finance runtime

The desktop app does not discover or use your shared OpenCode service. It never changes OpenCode configuration or credentials. Set `PROJECTOPS_OPENCODE_URL` explicitly to a **dedicated** loopback HTTP(S) server root and `PROJECTOPS_OPENCODE_CONFIG_DIR` to that dedicated account's fully qualified global OpenCode configuration directory; otherwise delegation is unavailable. If that server requires bearer authentication, set `PROJECTOPS_OPENCODE_TOKEN`. Configure its model/provider on that server, not in the application.

Only delegate after explicitly consenting to send the assembled project context and prompt to that runtime and its configured provider. Context includes up to three recent completed result excerpts from the selected project, labeled unverified history and limited to 12,000 characters each. No other project records are transmitted by this adapter. File paths in context are references, not attachments: no document file content is read. Provider/subscription support, authentication, retention and confidentiality must be evaluated separately. A Codex subscription is not assumed to work.

## Dedicated deployment (not your normal OpenCode user)

Use a separate local OS account with its own empty home/config/data directories and no access to finance source documents. Start in a new empty working directory outside the repository. There must be no ancestor `AGENTS.md`, `opencode.json(c)`, `.opencode` directories, plugins, skills, MCP servers or references. Do not reuse the shared service, repository directory, or normal user's global config. The adapter cannot prove OS isolation or absence of undiscovered instruction files; these are deployment prerequisites.

For that dedicated account only, create `~/.config/opencode/opencode.json` (Windows: that account's OpenCode config directory) with:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "default_agent": "projectops",
  "permissions": [{ "action": "*", "resource": "*", "effect": "deny" }],
  "experimental": {
    "policies": [{ "action": "permission", "resource": "*", "effect": "deny" }]
  },
  "agents": {
    "projectops": {
      "description": "Supplied-context finance analysis only",
      "mode": "primary",
      "permissions": [{ "action": "*", "resource": "*", "effect": "deny" }]
    }
  },
  "share": "disabled",
  "snapshots": false,
  "formatter": false,
  "lsp": false,
  "warming": false,
  "websearch": false,
  "compaction": { "auto": false },
  "plugins": [],
  "mcp": { "servers": {} },
  "skills": [],
  "instructions": [],
  "references": {},
  "update": "disable"
}
```

Select a supported model and authenticate its provider **under this dedicated account**, without submitting private prompts. Provider configuration may itself execute credential commands or transmit requests: deploy only trusted provider/auth configuration. OpenCode is not an OS sandbox. Start this private server from the empty working directory:

```text
opencode serve --hostname 127.0.0.1 --port 49123
```

This command and flags were checked against installed `opencode v2.0.24` (`opencode serve --help`). Do not add `--service`. Set the app environment:

```text
PROJECTOPS_OPENCODE_URL=http://127.0.0.1:49123/
PROJECTOPS_OPENCODE_CONFIG_DIR=C:\Users\ProjectOpsRuntime\.config\opencode
```

The directory shown is an example: use the actual **dedicated account's** global folder containing the configuration above (on Linux, for example `/home/projectops-runtime/.config/opencode`). Do not use the repository, a repository ancestor, or your normal user's configuration directory. The adapter normalizes fully qualified paths and compares using the host OS's path-case convention; it cannot establish directory ownership, resolve symlink/junction identity, or prove account isolation. These remain operator prerequisites.

Do not enable tools or change configuration while jobs run. Before creating a session, the adapter checks V2 2.0 server identity, configuration documents, a universal hard-deny permission policy, the resolved `projectops` agent's final deny-all rule, zero loaded plugins and zero MCP servers. Exactly one discovered `directory` entry must match the explicitly configured trusted global directory; missing, duplicate or other directories are refused. Documents still reject configured plugins/skills/instructions/references/commands/enterprise/warming and permissive hard policies. Sharing uses the last document explicitly defining `share`, which must be `disabled`; documents omitting it preserve the earlier value. Warming remains conservatively rejected if any document enables it. Configuration/discovery shape changes fail closed. Neither a loopback URL nor an allowlisted path proves dedication; setting them is the operator's explicit selection of the isolated finance runtime.

## Verified wire contract

Sources retrieved on 2026-10-06:

- Actual `https://opencode.ai/v2/openapi.json` (OpenAPI 3.1, experimental `opencode HttpApi` 0.0.1), not V1 routes.
- `https://opencode.ai/v2/docs/api`, `/config`, `/permissions`, `/cli`, `/build/client`.
- Published `@opencode/client` **2.0.24** npm tarball, `package/dist/promise/generated/types.d.ts`, supplies native event payload types that OpenAPI leaves as `V2EventEncoded` JSON strings. No npm dependency is added.
- Published `@opencode/core` **2.0.24** tarball (`https://registry.npmjs.org/@opencode/core/-/core-2.0.24.tgz`): `package/dist/chunks/repository-56gyfky0.js` (`src/config/discovery.ts`) selects the global config directory unless `global:false`; `package/dist/chunks/repository-9s1pr29s.js` (`src/config.ts`) unconditionally appends its `Directory` entry in `loadDirectory()`. The same file's `latest2(entries, key)` reads the last document explicitly defining a setting. This is why a normal dedicated `serve` needs one allowlisted global directory, not blanket directory rejection, and sharing is evaluated across documents. OpenAPI `Config.DirectoryEncoded` contains exactly `type:"directory"` and `path:string`; configuration entries are ordered lowest to highest priority. No Core npm dependency is added.

The adapter uses `GET /api/info`, `/api/config`, `/api/agent/projectops`, `/api/plugin`, `/api/mcp`; `POST /api/session` with a client-generated session ID, agent and deny-all rules; `GET /api/event` SSE; and `POST /api/session/{sessionID}/prompt` with `id`, `text`, empty `files`/`agents`/`skills`, `resume:true`. Prompt submission only admits input: it is **not** a completed result.

SSE native envelopes have `type` and `data.sessionID`. Only this job's `session.text.delta`/`data.delta` becomes result progress. `session.text.ended`/`data.text` must agree with streamed text, and `session.execution.succeeded` plus acknowledged idle is required for success. Execution/step failures, external interruption, disconnects and inconsistent text fail rather than fabricate results. Project activities are only “Reading project information” (sending supplied context) and “Preparing response”. Technical details are metadata only; raw provider errors, prompts, tool arguments, reasoning, and unrelated events are not copied into app activity logs.

Stop first cancels the known undelivered input via `DELETE /api/session/{sessionID}/inbox/{inboxID}`, then `POST /api/session/{sessionID}/interrupt?resume=false` and validates its boolean `interrupted` acknowledgement. `false` means already idle, not a failed abort. The adapter then awaits `POST /api/experimental/session/{sessionID}/wait` (204) and checks absence from `GET /api/session/active`. Stop uses an independent ten-second timeout, even when the job token is cancelled. Stop failure/unknown admission is a non-cancellation failure, never a falsely confirmed Cancelled outcome. Admission has a separate fifteen-second timeout.

## Limits and verification

- HTTP-handler fixtures exercise request shapes, policy rejection, real SSE text deltas, failure/disconnect handling and abort/idle acknowledgement without live providers.
- No live provider prompt was sent and no private server was started: safe separate-account deployment is an operator prerequisite, not available as an automated fixture here.
- This experimental V2 contract may change. V1 is unsupported. No guessed V1 `/session`, `/abort`, `/prompt_async` or message-part events are used.
- OpenCode persists session prompts/results in its own dedicated local database; provider retention is outside the app. The adapter does not delete transcripts or promise zero runtime retention. App-owned SQLite records remain local unless included in the explicitly delegated context.
- The deny-all hard policy blocks tool permission checks, including reads, writes, shell, subagents, external requests and MCP. It does not sandbox server startup, trusted provider code, automatic instruction discovery or an administrator changing policy mid-run. Separate-account/empty-directory isolation is required.
- Process crashes or an unreachable server cannot prove cancellation. The app must mark stop failures as failed/uncertain and tell the user to inspect/stop the dedicated server; cancellation does not undo provider transmission.
