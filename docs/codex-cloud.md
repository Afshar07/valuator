# Codex Cloud development environment

## Scope

The setup installs Flutter **3.47.6 stable**, including **Dart 3.13.5**, from
the official Linux x64 archive with a pinned SHA-256 checksum. It adds the
Linux desktop compiler/GTK prerequisites and SQLite CLI/runtime libraries.
No application dependencies, daemon, OpenCode installation, Android SDK,
browser, database service, or project adapters are introduced.

The existing `AGENTS.md` and `.ai/` files remain authoritative and unchanged.
This repository currently has no `pubspec.yaml` or application implementation.
Verification therefore builds a temporary stock Flutter desktop app outside
the repository, with Linux and Windows runners, and removes it afterwards.
That verifies the toolchain rather than the intended harness product.

## Configure and run in Cloud

Commit these files to the GitHub branch selected for environment setup; a
local working-tree change is not available to a remote checkout. This document
and the domain list are configuration inputs, not an automatically applied
Codex environment manifest or firewall.

In **Settings > Codex Cloud > Environments**, create or edit an environment
for `Afshar07/unnamed-harness`. Use an Ubuntu/Debian Linux x64 image with root
or passwordless sudo. In the setup conversation, ask Codex to:

```text
Run bash scripts/codex/setup.sh from the repository root. Configure limited
internet access using scripts/codex/allowed-domains.txt, plus only the Ubuntu
APT mirror hosts required by the selected image. Preserve AGENTS.md and .ai/.
Review flutter doctor output and verify the Linux build before publishing.
```

For environments with script fields, set:

```bash
# Setup script
bash scripts/codex/setup.sh
```

```bash
# Maintenance script (cached-container resume)
bash scripts/codex/maintenance.sh
```

Select **Publish** or **Republish** after the setup is verified, then verify
again in a new task. Setup scripts do not publish an environment or change
its internet-access settings. The cloud controls must enforce the allowlist.

Flutter and Dart are linked into `/usr/local/bin`, so new non-login sessions
can find them without relying on setup's exported `PATH`. Scripts source
`scripts/codex/environment.sh` for cache paths, official package endpoints,
and disabled analytics. For equivalent settings in an interactive shell:

```bash
source scripts/codex/environment.sh
flutter doctor -v
bash scripts/codex/verify.sh
```

Once application source exists at the repository root, the same verification
script selects it automatically. If it lives elsewhere, set the non-secret
environment variable `FLUTTER_PROJECT_DIR` to its absolute path, or run:

```bash
bash scripts/codex/verify.sh path/to/flutter/app
```

An explicitly selected path without `pubspec.yaml` fails rather than falling
back to the template. The script runs `flutter pub get`, `flutter analyze
--no-pub`, `flutter test --no-pub` when `test/` exists, and `flutter build linux
--release --no-pub`. It also prints doctor/device diagnostics and checks Git,
child-process execution, and an in-memory SQLite read/write operation.
Missing tests are reported as skipped. Diagnostic doctor failures are printed;
dependency, analysis, test, SQLite, and native build failures fail the script.

## Network policy

Use **limited access**, with the eleven exact hosts in
`scripts/codex/allowed-domains.txt`. Do not select unrestricted access or add
a broad package-registry preset. Flutter downloads use `storage.googleapis.com`;
Dart package resolution uses `pub.dev`. Fetch Git repositories over HTTPS
through the managed proxy; the local origin's SSH URL may not work in Cloud.
The setup does not rewrite Git remotes or supply Git credentials.

Concrete exception: APT must fetch native compiler and GTK packages from the
image's configured Ubuntu repositories. For standard Ubuntu x64 sources,
allow `archive.ubuntu.com` and `security.ubuntu.com` when those exact hosts are
configured. If the image uses `azure.archive.ubuntu.com`, allow that host
instead of adding a wildcard. Inspect `/etc/apt/sources.list` and
`/etc/apt/sources.list.d/` in the setup container for the actual hostnames;
do not add unused mirrors. A Debian image needs its actual Debian mirror
hosts instead. Keep those exceptions to provisioning when the environment
offers separate setup/runtime network policies.

The scripts respect inherited proxy settings and CA trust. They do not disable
TLS checks, bypass the proxy, or install a separate network service. The
download rejects non-HTTPS redirects and verifies the archive before extraction.
`flutter doctor` may warn about network probes to hosts outside the allowlist;
do not widen access just to clear unrelated Android/web diagnostics. Native
desktop builds provide the relevant executable check.

OpenCode's domains are reserved for later integration. No runtime is started
and no model-provider access or secrets are configured. Later dependencies
(including GitHub release asset hosts), adapters, and provider endpoints need
specific justification before extending the allowlist.

## Platform and lifecycle limits

- **Linux:** analysis, unit/widget tests, and native release builds are the
  cloud target. Compilation and standard Flutter tests need no display server.
  Running a desktop window or desktop integration tests needs a display; this
  setup deliberately adds no Xvfb or graphical services.
- **Windows:** Dart source and Windows runner files can be edited in Linux,
  but `flutter build windows` requires a Windows host and Visual Studio's
  **Desktop development with C++** workload. Enabling the Windows Flutter
  setting does not install MSVC or provide cross-compilation. On a prepared
  Windows machine, use the same Flutter version, run `flutter config
  --enable-windows-desktop`, then doctor, pub get, analyze, test, and
  `flutter build windows --release`. This Linux installer does not provision
  a Windows machine.
- **SDK changes:** change the version, revision, and checksum together in
  `flutter-version.sh`. Reset the cloud cache and reconcile existing
  `/usr/local/bin/flutter` and `dart` links when changing versions. Setup refuses
  to overwrite another toolchain or reuse a mismatched revision.
- **Local integrations:** Git, subprocesses, filesystem operations, and SQLite
  stay local to the container. Future adapters inherit its permissions and
  network limits; tools on the user's Windows computer are not available in
  the cloud. No adapter-specific toolchains are preinstalled by this setup.
- **Persistence:** SDK and Pub caches are outside the repo. Cloud container
  state is not a durable application database. Persist real work through Git
  and export needed artifacts before the environment is discarded.

## Verification evidence (2026-10-05)

Preparation ran in a local Windows Codex desktop session. Flutter and Dart
were absent, no installed WSL distribution was available, and no cloud
executor was attached. Therefore cloud installation, `flutter doctor`, Pub
resolution, Dart analysis, Flutter tests, and desktop builds are **not yet
verified**. The setup and verification scripts must run in the cloud setup
conversation before claiming desktop readiness.

Local checks passed: Bash syntax for every script, `git diff --check`, and
unchanged tracked `AGENTS.md`/`.ai/` content. Both setup and verification
correctly exited with an unsupported-host message on Windows, before executing
Linux installation or Flutter commands. The pinned SDK metadata and archive
checksum were retrieved from the official release manifest.

Official references:

- [Cloud environments](https://learn.chatgpt.com/docs/environments/cloud-environments)
- [Legacy setup and maintenance scripts](https://learn.chatgpt.com/docs/environments/cloud-environment)
- [Flutter Linux prerequisites](https://docs.flutter.dev/platform-integration/linux/setup)
- [Flutter Windows prerequisites](https://docs.flutter.dev/platform-integration/windows/setup)
- [Official Linux release manifest](https://storage.googleapis.com/flutter_infra_release/releases/releases_linux.json)
