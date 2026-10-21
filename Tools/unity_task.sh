#!/bin/zsh
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
# SPDX-License-Identifier: MPL-2.0
# Copyright (c) 2026 ALS HDT Prototype Contributors.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity}"
TASK="${1:-validate}"
if [[ "$TASK" == "test" ]]; then
  cd "$ROOT"
  mkdir -p Logs TestResults
  rm -f "$ROOT/TestResults/editmode-results.xml"
  set +e
  "$UNITY" -batchmode -nographics -projectPath "$ROOT" -runTests -testPlatform EditMode -testResults "$ROOT/TestResults/editmode-results.xml" -logFile "$ROOT/Logs/editmode.log"
  STATUS=$?
  set -e
  python3 Tools/sanitize_test_results.py "$ROOT/TestResults/editmode-results.xml"
  exit "$STATUS"
fi
case "$TASK" in
  setup) METHOD=ALS.HDT.Editor.PrototypeBuilder.CreateDemo ;;
  validate) METHOD=ALS.HDT.Editor.PrototypeValidation.Run ;;
  build) METHOD=ALS.HDT.Editor.PrototypeBuilder.BuildMac ;;
  all) METHOD=ALS.HDT.Editor.PrototypeValidation.ValidateAndBuildMac ;;
  *) echo 'Usage: Tools/unity_task.sh setup|validate|build|all|test'; exit 1 ;;
esac
cd "$ROOT"
mkdir -p Logs
if [[ "$TASK" == "all" || "$TASK" == "validate" ]]; then
  rm -f "$ROOT/TestResults/physics-validation.json"
fi
"$UNITY" -batchmode -nographics -projectPath "$ROOT" -executeMethod "$METHOD" -quit -logFile "$ROOT/Logs/$TASK.log"
echo "Completed: $TASK. Log: $ROOT/Logs/$TASK.log"
