#!/usr/bin/env bash
set -euo pipefail
source "$(dirname -- "${BASH_SOURCE[0]}")/environment.sh"
[[ "$(uname -s)" == Linux ]] || { echo 'Run cloud verification on Linux.' >&2; exit 1; }
command -v flutter >/dev/null || { echo 'Flutter is missing; run scripts/codex/setup.sh.' >&2; exit 1; }
flutter --version
dart --version
# Doctor also checks remote endpoints outside our allowlist. Preserve its
# diagnostic output and status; the native build below gates desktop readiness.
doctor_status=0
flutter doctor -v || doctor_status=$?
printf 'flutter doctor exit status: %s\n' "$doctor_status"
flutter devices
git --version
[[ "$(sqlite3 :memory: "CREATE TABLE probe(value TEXT); INSERT INTO probe VALUES ('ok'); SELECT value FROM probe;")" == ok ]]
[[ "$(bash -c 'printf process-ok')" == process-ok ]]

if [[ $# -gt 1 ]]; then
  echo 'Usage: bash scripts/codex/verify.sh [Flutter-project-directory]' >&2
  exit 1
fi
project="${1:-${FLUTTER_PROJECT_DIR:-$HARNESS_REPO_ROOT}}"
if [[ -f "$project/pubspec.yaml" ]]; then
  cd -- "$project"
else
  # This repository starts as routed Markdown context, without application source.
  # Never scaffold over it or report a probe as an application build.
  if [[ $# -gt 0 || -n "${FLUTTER_PROJECT_DIR:-}" ]]; then
    echo "No pubspec.yaml at explicitly selected project: $project" >&2
    exit 1
  fi
  probe_dir="$(mktemp -d "${TMPDIR:-/tmp}/harness-flutter-probe.XXXXXX")"
  trap 'rm -rf -- "$probe_dir"' EXIT
  echo 'No application yet: verifying a temporary Flutter desktop template.'
  flutter create --no-pub --platforms=linux,windows --project-name=harness_environment_probe "$probe_dir"
  cd -- "$probe_dir"
fi
flutter pub get
flutter analyze --no-pub
if [[ -d test ]]; then
  flutter test --no-pub
else
  echo 'No test/ directory: application tests were not run.'
fi
[[ -f linux/CMakeLists.txt ]] || { echo 'The selected app has no Linux desktop runner.' >&2; exit 1; }
flutter build linux --release --no-pub
echo 'Linux dependency resolution, analysis, and release build passed (tests as reported above).'
echo 'Windows builds require a Windows host with Visual Studio Desktop development with C++.'
