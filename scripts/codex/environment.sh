#!/usr/bin/env bash
# Source this file when using the scripts from a fresh, non-login shell.
HARNESS_REPO_ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_PACKAGES="${NUGET_PACKAGES:-$HOME/.cache/unnamed-harness/nuget}"
export CI=true
