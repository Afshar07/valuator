#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/environment.sh"
if [[ ! -x "$FLUTTER_ROOT/bin/flutter" ]] || \
   [[ "$(git -C "$FLUTTER_ROOT" rev-parse HEAD)" != "$FLUTTER_REVISION" ]]; then
  echo 'Missing or changed pinned SDK. Reset the environment cache and rerun setup.' >&2
  exit 1
fi
# Verify the selected branch and refresh its dependencies on cache resume.
bash "$HARNESS_REPO_ROOT/scripts/codex/verify.sh"
