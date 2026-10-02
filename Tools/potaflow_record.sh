#!/bin/zsh
# Records one PotaFlow candidate from the running editor (PotaFlow.unity in Play Mode) and prints the result line.
# Usage: Tools/potaflow_record.sh <A|B|1|2|3> <name> [count=158] [tail=0.5]
set -u
ROOT="${0:A:h:h}"; cd "$ROOT"
UV="$HOME/.local/bin/uv"
CAND="$1"; NAME="$2"; COUNT="${3:-158}"; TAIL="${4:-0.5}"
if [[ "$CAND" != "wait" ]]; then
  "$UV" run --quiet Tools/mcp_call.py --timeout 60 call execute_custom_tool "{\"tool_name\":\"ai_potaflow_record\",\"parameters\":{\"candidate\":\"$CAND\",\"count\":$COUNT,\"name\":\"$NAME\",\"tail\":$TAIL}}" | sed -n 3p
fi
for i in {1..120}; do
  sleep 8
  r=$("$UV" run --quiet Tools/mcp_call.py --timeout 30 call execute_custom_tool '{"tool_name":"ai_potaflow_record","parameters":{"action":"status"}}' 2>/dev/null | python3 -c "import json,sys; d=json.load(sys.stdin)['data']; print(d['running'], d['phase'], d['frames'], d['result'])" 2>/dev/null)
  case "$r" in False*) echo "$r"; exit 0;; esac
done
echo "timeout: $r"; exit 1
