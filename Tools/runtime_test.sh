#!/bin/zsh
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PLAYER="$ROOT/Builds/macOS/PhysicsUpperBodyAvatar.app/Contents/MacOS/PhysicsUpperBodyAvatar"
if [[ ! -x "$PLAYER" ]]; then
  PLAYER="$ROOT/Builds/macOS/PhysicsUpperBodyAvatar.app/Contents/MacOS/Physics Upper Body Avatar"
fi
mkdir -p "$ROOT/TestResults/Standalone"
rm -f "$ROOT/TestResults/Standalone/runtime-validation.json"
# Intentionally NOT -nographics: this verifies the real Metal renderer and UI.
"$PLAYER" -hdtSelfTest -hdtOutput "$ROOT/TestResults/Standalone" -logFile "$ROOT/Logs/player-selftest.log" -screen-width 1280 -screen-height 800 -screen-fullscreen 0
