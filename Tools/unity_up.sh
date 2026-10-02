#!/bin/zsh
# Makes sure this project is open in Unity and its MCP bridge answers.
# Usage (from anywhere): Tools/unity_up.sh [timeout_seconds=300]
# Exit 0 = bridge ready, 1 = timed out, 2 = Unity version not installed.
set -u
ROOT="${0:A:h:h}"
NAME="${ROOT:t}"
TIMEOUT="${1:-300}"
UV="$(command -v uv 2>/dev/null || echo "$HOME/.local/bin/uv")"
VERSION="$(awk '/m_EditorVersion:/{print $2}' "$ROOT/ProjectSettings/ProjectVersion.txt")"
APP="/Applications/Unity/Hub/Editor/$VERSION/Unity.app"

SINCE=""   # when we launch Unity ourselves, only a bridge connection made after the launch counts

# Unity holds Temp/UnityLockfile open while the project is loaded.
if lsof -t "$ROOT/Temp/UnityLockfile" >/dev/null 2>&1; then
  echo "Unity already has '$NAME' open"
else
  [[ -d "$APP" ]] || { echo "Unity $VERSION not found at $APP" >&2; exit 2; }
  echo "Opening '$NAME' in Unity $VERSION ..."
  SINCE="$(date -u +%Y-%m-%dT%H:%M:%S)"
  # -n: start a separate Unity even when another project (e.g. Kaptan) is already open; -g: leave it in the background.
  open -gna "$APP" --args -projectPath "$ROOT"
fi

cd "$ROOT"
start=$SECONDS
while (( SECONDS - start < TIMEOUT )); do
  if "$UV" run --quiet Tools/mcp_call.py --timeout 10 read mcpforunity://instances 2>/dev/null | python3 -c '
import json, sys
name, since = sys.argv[1], sys.argv[2]
try:
    doc = json.load(sys.stdin)
except ValueError:
    sys.exit(1)
instances = doc.get("instances") or doc.get("data", {}).get("instances") or []
sys.exit(0 if any(i.get("name") == name and i.get("connected_at", "")[:19] >= since for i in instances) else 1)
' "$NAME" "$SINCE"; then
    echo "bridge ready after $(( SECONDS - start )) s"
    exit 0
  fi
  sleep 5
done
echo "bridge did not answer within $TIMEOUT s; check Logs/Editor.log" >&2
exit 1
