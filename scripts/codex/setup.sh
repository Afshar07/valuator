#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/environment.sh"

if [[ "$(uname -s)" != Linux ]]; then
  echo 'This setup requires Linux (the Codex Cloud Ubuntu image).' >&2
  exit 1
fi
command -v apt-get >/dev/null || { echo 'An apt-based Linux image is required.' >&2; exit 1; }
as_root=()
if [[ "$EUID" -ne 0 ]]; then
  command -v sudo >/dev/null || { echo 'Root or passwordless sudo is required.' >&2; exit 1; }
  as_root=(sudo -n)
fi

# Install the .NET 10 SDK from the distro archive only if it is missing. No full OS upgrade.
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  "${as_root[@]}" apt-get update
  "${as_root[@]}" env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    ca-certificates git dotnet-sdk-10.0
fi

bash "$HARNESS_REPO_ROOT/scripts/codex/verify.sh"
