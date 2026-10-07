#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/environment.sh"
dotnet --list-sdks | grep -q '^10\.' || {
  echo 'The .NET 10 SDK is missing. Reset the environment cache and rerun setup.' >&2
  exit 1
}
# Verify the selected branch and refresh its dependencies on cache resume.
bash "$HARNESS_REPO_ROOT/scripts/codex/verify.sh"
