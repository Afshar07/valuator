#!/usr/bin/env bash
# Source this file when using the scripts from a fresh, non-login shell.
HARNESS_REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$HARNESS_REPO_ROOT/scripts/codex/flutter-version.sh"
export FLUTTER_ROOT="$HOME/.local/share/unnamed-harness/flutter/$FLUTTER_VERSION/flutter"
export PUB_CACHE="$HOME/.cache/unnamed-harness/pub"
export PUB_HOSTED_URL=https://pub.dev
export FLUTTER_STORAGE_BASE_URL=https://storage.googleapis.com
export PATH="$FLUTTER_ROOT/bin:$PATH"
export CI=true
export FLUTTER_SUPPRESS_ANALYTICS=true
export DART_SUPPRESS_ANALYTICS=true
