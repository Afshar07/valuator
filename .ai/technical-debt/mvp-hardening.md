# First-MVP operational hardening

- **Status:** Open
- **Verified:** 2026-10-06
- **Scope/owner:** ProjectOperations desktop, persistence and runtime boundaries

## Problem and evidence

- `SqliteProjectRepository` and `SqliteAgentJobRepository` run native SQLite work
  off the UI thread and check cancellation between statements. Cancellation while
  SQLite is waiting on another native writer has not been tested; native busy waits
  can delay completion despite a cancelled token.
- Proposal review is serialized within one `AgentService`. Projects have optimistic
  revisions and approval commits atomically, but agent-job snapshots have no
  cross-process revision check. The supported MVP workflow is one application
  instance per database; competing review operations from two instances are not
  validated. Reject-versus-approve conflicts need stronger coordination before
  supporting multiple instances against one database.
- Local SQLite and dedicated OpenCode transcripts can retain project context and
  results indefinitely. There is no application encryption, retention policy,
  backup UI or transcript-purge control. OS account permissions/disk encryption and
  operator-managed backups are required for sensitive-data deployment.
- The OpenCode V2 protocol is experimental and fixture-tested. Dedicated runtime
  isolation, real provider authentication and live Stop are not verified in this
  environment. The adapter cannot prove operating-system isolation or provider
  retention behavior; see `docs/agent-runtime.md`.

## Impact and constraints

This is a local MVP, not a production confidentiality or multi-process guarantee.
Do not relax fail-closed runtime checks or silently enable tools to work around
configuration errors. Do not add cloud services, accounts or document ingestion as
a substitute for these bounded hardening tasks.

## Completion criteria

Test lock-contention cancellation and recovery; enforce one instance or add job
review conflict protection; choose explicit local backup/retention/encryption
behavior before production use; run the documented synthetic live-runtime
request/cancellation check under an isolated configured deployment.

## Verification

Native unit/integration checks cover aggregate rollback, stale project edits,
atomic approval, proposal parsing, scoped context, and runtime abort/idle fixtures.
Manual desktop/provider checks are intentionally assigned to the user in
`docs/verification.md`. This record does not authorize broader MVP scope.
