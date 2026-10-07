# Codex Cloud development environment

## Scope

The setup installs the **.NET 10 SDK** from the image's Ubuntu/Debian archive
(`dotnet-sdk-10.0`) when it is missing, then runs the full verification. It
adds no daemon, OpenCode installation, browser, database service or display
server. The existing `AGENTS.md` and `.ai/` files remain authoritative.

## Configure and run in Cloud

Commit these files to the GitHub branch selected for environment setup; a
local working-tree change is not available to a remote checkout. This document
and the domain list are configuration inputs, not an automatically applied
Codex environment manifest or firewall.

In **Settings > Codex Cloud > Environments**, create or edit an environment
for `Afshar07/unnamed-harness`. Use an Ubuntu/Debian Linux image with root
or passwordless sudo. For environments with script fields, set:

```bash
# Setup script
bash scripts/codex/setup.sh
```

```bash
# Maintenance script (cached-container resume)
bash scripts/codex/maintenance.sh
```

Setup scripts do not publish an environment or change its internet-access
settings. The cloud controls must enforce the allowlist.

For an interactive shell with equivalent settings:

```bash
source scripts/codex/environment.sh
bash scripts/codex/verify.sh
```

## What `verify.sh` runs

`dotnet restore`, `dotnet build`, `dotnet test` (domain, SQLite, runtime
fixtures and Avalonia Headless desktop tests, which need no display),
`dotnet format --verify-no-changes` and `git diff --check`. Any failure fails
the script. The NuGet cache lives outside the repo (`~/.cache/unnamed-harness/nuget`).

## Network policy

Use **limited access** with the exact hosts in `scripts/codex/allowed-domains.txt`:
NuGet (`api.nuget.org`, `www.nuget.org`, `nuget.org`), the .NET download hosts,
GitHub, and the reserved OpenCode domains. Do not select unrestricted access.

APT must fetch the SDK from the image's configured Ubuntu repositories. Allow
`archive.ubuntu.com` and `security.ubuntu.com` when those hosts are configured
(or the image's actual mirror, e.g. `azure.archive.ubuntu.com`); inspect
`/etc/apt/sources.list` and `/etc/apt/sources.list.d/` rather than guessing.
Keep these exceptions to provisioning when separate setup/runtime policies exist.
If `builds.dotnet.microsoft.com` is blocked, the apt package is the supported path.

The scripts respect inherited proxy settings and CA trust and never disable TLS checks.
OpenCode's domains are reserved; no runtime is started and no provider access or
secrets are configured.

## Platform limits

- **Linux (cloud):** restore, build, tests and format checks. Running the native
  desktop window needs a display and is not covered.
- **Windows:** the shippable `.exe` is built by
  [`.github/workflows/release.yml`](../.github/workflows/release.yml) on a
  Windows runner when a GitHub release is published.
- **Persistence:** container state is not durable. Persist real work through Git.
