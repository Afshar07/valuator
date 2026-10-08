# Project Operations

A local, single-window VC investment project assistant built with C#/.NET,
Avalonia and SQLite. OpenCode is an optional, explicitly configured agent runtime,
not the application's source of project truth.

## Download

Each [GitHub release](https://github.com/Afshar07/unnamed-harness/releases) includes a
per-user Windows x64 installer, `ProjectOperationsApp-win-Setup.exe`. It includes
the .NET runtime and does not require administrator rights. The build is unsigned,
so Windows SmartScreen may warn. No portable build is distributed.

In **Settings → Application updates**, choose **Check for updates** to see a newer
stable version and its changelog, then download it with progress and cancellation.
Choose **Restart and install** when ready; checking and installation are not automatic.
Release notes are displayed as plain Markdown. Checking contacts GitHub but does not
send project data. Development/standalone copies cannot self-update.

## Run from source

Install the .NET 10 SDK, then:

```sh
dotnet restore ProjectOperations.slnx
dotnet build ProjectOperations.slnx
dotnet run --project src/ProjectOperations.Desktop
```

Application records are stored under the current user's local application data
directory in `ProjectOperations/projects.db`. Set `PROJECTOPS_DATA_DIR` to use a
different directory (for example, an isolated verification directory).

Choose **English** or **فارسی** in the language selector. Labels and layout direction
switch live (English LTR, Persian RTL), without changing project data. The selection
is saved in `settings.json` in the same data directory, as is the Light / Dark / Auto
theme chosen in the sidebar or Settings. Agent responses use the
selected language; structured proposal fields remain unchanged. Date entry uses
Gregorian `yyyy-MM-dd HH:mm` in both languages; Jalali display is deferred.
See [localization boundaries and extension guidance](docs/localization.md).

## Workflow

1. Create a project using **VC Investment Review**. Its 16 requirements are
   automatically instantiated across four groups.
2. Enter requirement values/notes or attach local file references. Removing an
   attachment removes its association, not the original file.
3. Review requirement statuses. **Only Complete counts toward readiness**;
   Provided is supplied but not yet reviewed. Missing and NeedsReview need attention.
4. Add tasks and optional deadlines. Overdue means an active task due before now;
   upcoming means an active task due within seven days. Done/Cancelled are excluded.
5. Open the project agent panel, inspect the context preview and consent to sending
   that context to the configured runtime/provider. Ask a question or select an action.
6. Review the result. Suggested tasks stay proposals until you select and approve
   them. Stop requests real runtime cancellation and waits for a confirmed outcome.

## Data and agent safety

- SQLite owns application data locally. There are no accounts, telemetry or cloud sync.
- The agent receives only the selected project's metadata, requirement values and
  notes, task information, up to three recent result excerpts from that project,
  and file **path references**. Historical output is explicitly unverified. The MVP does not extract,
  upload or claim to read document contents.
- Agent use may transmit the displayed context to the model provider you configure.
  Do not enable it for confidential data without approving that provider's retention
  and processing terms. Application and runtime job history may retain sensitive text.
- Analysis does not authorize file writes, shell commands, external communication
  or project mutation. Investment decisions remain with the user.
- See [runtime setup and limitations](docs/agent-runtime.md) before enabling the agent.

## Structure

```text
src/ProjectOperations.Core            Domain, services, deterministic rules, runtime contracts
src/ProjectOperations.Infrastructure  SQLite and isolated OpenCode adapter
src/ProjectOperations.Desktop         Avalonia screens and composition root
tests/                               Native unit/integration and desktop tests
```

## Releases

Publishing a GitHub release runs `.github/workflows/release.yml` on a Windows runner:
restore, build, test, then a self-contained `win-x64` directory publish and Velopack
packaging. A valid SemVer tag (`v1.2.3`) is required and sets the app/package version.
The release body supplies the in-app changelog. The installer, full `.nupkg`, and
`releases.win.json` update feed are attached to that same release; keep the package
and feed assets because the installed app needs them. This workflow publishes full
updates, not delta packages. Prereleases are not offered by the stable update checker.

Velopack SDK and CLI are both pinned to `1.2.161`. The installation is under
`%LocalAppData%\ProjectOperationsApp`; project data remains separate under
`%LocalAppData%\ProjectOperations`, so uninstalling does not delete it. The built-in
GitHub update source requires this repository and release assets to be publicly
accessible; no GitHub credentials are embedded in the app.

## Checks

```sh
dotnet test ProjectOperations.slnx
dotnet format ProjectOperations.slnx --verify-no-changes
```

`bash scripts/codex/verify.sh` runs restore, build, tests, format and whitespace checks
in one step on Linux (see [cloud environment setup](docs/codex-cloud.md)).

Tests use isolated temporary databases and synthetic project data. Runtime transport
tests do not prove provider authentication or a live model response. Verification
results and known limitations are recorded in `docs/verification.md`.

## Deliberate limits

No document extraction/OCR, financial-model editor, RAG, external integrations,
recurrence engine, collaboration, accounts, cloud sync, portfolio analytics,
multi-agent orchestration or automatic agent changes. File references can become
unavailable if the original file moves. The local SQLite database is not encrypted;
use OS account permissions and disk encryption to protect sensitive records.
