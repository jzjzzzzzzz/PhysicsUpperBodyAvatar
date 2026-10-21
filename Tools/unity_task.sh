#!/bin/zsh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.7f1/Unity.app/Contents/MacOS/Unity}"
TASK="${1:-validate}"
case "$TASK" in
  setup) METHOD=ALS.HDT.Editor.PrototypeBuilder.CreateDemo ;;
  validate) METHOD=ALS.HDT.Editor.PrototypeValidation.Run ;;
  build) METHOD=ALS.HDT.Editor.PrototypeBuilder.BuildMac ;;
  all) METHOD=ALS.HDT.Editor.PrototypeValidation.ValidateAndBuildMac ;;
  *) echo 'Usage: Tools/unity_task.sh setup|validate|build|all'; exit 1 ;;
esac
cd "$ROOT"
mkdir -p Logs
"$UNITY" -batchmode -nographics -projectPath "$ROOT" -executeMethod "$METHOD" -quit -logFile "$ROOT/Logs/$TASK.log"
echo "Completed: $TASK. Log: $ROOT/Logs/$TASK.log"
