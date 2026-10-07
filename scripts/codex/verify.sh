#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/environment.sh"
[[ "$(uname -s)" == Linux ]] || { echo 'Run cloud verification on Linux.' >&2; exit 1; }
command -v dotnet >/dev/null || { echo '.NET SDK is missing; run scripts/codex/setup.sh.' >&2; exit 1; }
dotnet --version
git --version

cd -- "$HARNESS_REPO_ROOT"
dotnet restore ProjectOperations.slnx
dotnet build ProjectOperations.slnx --no-restore
# Includes Avalonia Headless desktop tests; no display server is needed.
dotnet test ProjectOperations.slnx --no-build
dotnet format ProjectOperations.slnx --no-restore --verify-no-changes
git diff --check
echo 'Restore, build, test and format verification passed (Linux, headless).'
echo 'The Windows .exe is built by .github/workflows/release.yml on a Windows runner.'
