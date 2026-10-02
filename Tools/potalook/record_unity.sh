#!/bin/zsh
# Records Color Hunt with ai_colorhunt_record: opens the scene the parameters name ("scene": ColorHunt (default) or Specials; refuses if
# the open scene has unsaved changes), enters Play, records, leaves Play, prints the markers and the clips that played.
# Usage: Tools/potalook/record_unity.sh NAME ['{"look":"candy","win":true,…}' | '{"scene":"Specials","scenario":"bomb","special_look":"royal"}']
#   → Temp/record/NAME/ (frames, events.json, sounds/*.wav)
set -u
cd "${0:A:h:h:h}"
name=$1
extra="${2:-}"
[[ -z "$extra" ]] && extra='{}'
scene=$(python3 -c '
import json, sys
s = (json.loads(sys.argv[1]).get("scene") or "ColorHunt").lower()
if s not in ("colorhunt", "specials"):
    sys.exit(f"unknown scene {s!r} (ColorHunt, Specials)")
print("Specials" if s == "specials" else "ColorHunt")
' "$extra") || exit 1
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
tmp=$(mktemp -d)
python3 - "$tmp/open.json" "$scene" <<'EOF'
import json, sys
path = f"Assets/_Project/Games/Pota/Scenes/{sys.argv[2]}.unity"
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        f'if (s.path != "{path}") {{ if (s.isDirty) return "dirty: " + s.path; '
        f'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{path}"); }} '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
M call execute_code @"$tmp/open.json" | grep '"result"' | grep -q "$scene" || { echo "could not open $scene (unsaved scene?)"; exit 1; }
M call manage_editor '{"action":"play"}' > /dev/null
for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
python3 - "$tmp/rec.json" "$name" "$extra" <<'EOF'
import json, sys
params = json.loads(sys.argv[3]); params["name"] = sys.argv[2]
json.dump({"tool_name": "ai_colorhunt_record", "parameters": params}, open(sys.argv[1], "w"))
EOF
M call execute_custom_tool @"$tmp/rec.json" | grep '"message"' | head -1
for i in {1..200}; do
  sleep 3
  r=$(M call execute_custom_tool '{"tool_name":"ai_colorhunt_record","parameters":{"action":"status"}}' | grep '"result"\|"message"' | head -2 | tr '\n' ' ')
  echo "$r" | grep -q 'done\|error\|idle' && { echo "$r"; break; }
done
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
python3 - "Temp/record/$name/events.json" <<'EOF'
import json, sys
try:
    ev = json.load(open(sys.argv[1]))
except (OSError, ValueError) as e:
    sys.exit(f"no events: {e}")
marks = {k: round(v, 3) for k, v in ev.items() if isinstance(v, (int, float)) and not isinstance(v, bool)}
sounds = ev.get("sounds", [])
print(json.dumps(marks), "|", ev.get("meta", {}).get("result", ""), "|", len(sounds), "sounds:", " ".join(sorted({s["clip"] for s in sounds})))
EOF
rm -rf "$tmp"
