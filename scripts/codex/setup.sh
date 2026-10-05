#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/environment.sh"

if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
  echo 'This setup requires Linux x86_64 (the Codex Cloud Ubuntu image).' >&2
  exit 1
fi
command -v apt-get >/dev/null || { echo 'An apt-based Linux image is required.' >&2; exit 1; }
as_root=()
if [[ "$EUID" -ne 0 ]]; then
  command -v sudo >/dev/null || { echo 'Root or passwordless sudo is required.' >&2; exit 1; }
  as_root=(sudo -n)
fi

# Only desktop build tooling, archive tooling, Git, and SQLite. No full OS upgrade.
"${as_root[@]}" apt-get update
"${as_root[@]}" env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
  ca-certificates curl git unzip xz-utils zip \
  clang cmake ninja-build pkg-config libgtk-3-dev libstdc++-12-dev \
  libsqlite3-0 sqlite3

sdk_parent="$(dirname -- "$FLUTTER_ROOT")"
if [[ ! -e "$sdk_parent" ]]; then
  mkdir -p "$(dirname -- "$sdk_parent")"
  download_dir="$(mktemp -d "$(dirname -- "$sdk_parent")/.download.XXXXXX")"
  trap 'rm -rf -- "$download_dir"' EXIT
  archive="$download_dir/flutter.tar.xz"
  curl --fail --location --retry 3 --proto '=https' --proto-redir '=https' \
    "https://storage.googleapis.com/flutter_infra_release/releases/stable/linux/flutter_linux_${FLUTTER_VERSION}-stable.tar.xz" \
    --output "$archive"
  printf '%s  %s\n' "$FLUTTER_ARCHIVE_SHA256" "$archive" | sha256sum --check --strict
  mkdir "$download_dir/sdk"
  tar -xJf "$archive" -C "$download_dir/sdk"
  mv -- "$download_dir/sdk" "$sdk_parent"
fi
if [[ ! -x "$FLUTTER_ROOT/bin/flutter" ]] || \
   [[ "$(git -C "$FLUTTER_ROOT" rev-parse HEAD)" != "$FLUTTER_REVISION" ]]; then
  echo "SDK at $FLUTTER_ROOT does not match the pinned release; inspect it before retrying." >&2
  exit 1
fi

# Setup exports do not survive into other sessions. These links work in
# non-login shells too; refuse to overwrite a separately installed SDK.
"${as_root[@]}" mkdir -p /usr/local/bin
for tool in flutter dart; do
  target="/usr/local/bin/$tool"
  if [[ -e "$target" || -L "$target" ]]; then
    [[ "$(readlink -- "$target")" == "$FLUTTER_ROOT/bin/$tool" ]] || {
      echo "$target already exists; reconcile that toolchain explicitly." >&2
      exit 1
    }
  else
    "${as_root[@]}" ln -s "$FLUTTER_ROOT/bin/$tool" "$target"
  fi
done

flutter config --no-analytics --enable-linux-desktop --enable-windows-desktop \
  --no-enable-android --no-enable-ios --no-enable-web --no-enable-macos-desktop
dart --disable-analytics
flutter precache --linux
bash "$HARNESS_REPO_ROOT/scripts/codex/verify.sh"
