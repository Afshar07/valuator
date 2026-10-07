#!/bin/bash
# Installs the .NET 10 SDK (if missing) and restores NuGet packages for cloud sessions.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"

# Skip the first-run banner and telemetry; keep output quiet and non-interactive.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DEBIAN_FRONTEND=noninteractive

# builds.dotnet.microsoft.com (dotnet-install.sh) is blocked by the cloud egress
# policy, so use the Ubuntu archive package, which is reachable.
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  SUDO=""
  [ "$(id -u)" -ne 0 ] && SUDO="sudo"
  $SUDO apt-get update -qq
  $SUDO apt-get install -y -qq dotnet-sdk-10.0
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"
fi

dotnet restore ProjectOperations.slnx
