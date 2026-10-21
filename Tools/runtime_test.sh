#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PLAYER="$ROOT/Builds/macOS/PhysicsUpperBodyAvatar.app/Contents/MacOS/PhysicsUpperBodyAvatar"
if [[ ! -x "$PLAYER" ]]; then
  PLAYER="$ROOT/Builds/macOS/PhysicsUpperBodyAvatar.app/Contents/MacOS/Physics Upper Body Avatar"
fi
mkdir -p "$ROOT/TestResults/Standalone"
# Intentionally NOT -nographics: this verifies the real Metal renderer and UI.
"$PLAYER" -hdtSelfTest -hdtOutput "$ROOT/TestResults/Standalone" -logFile "$ROOT/Logs/player-selftest.log" -screen-width 1280 -screen-height 800 -screen-fullscreen 0
