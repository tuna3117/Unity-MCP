#!/bin/zsh
# Records Color Hunt with ai_colorhunt_record: opens the ColorHunt scene (refuses if the open scene has unsaved changes), enters Play,
# records, leaves Play. Usage: Tools/potalook/record_unity.sh NAME ['{"look":"candy","win":true,…}']  → Temp/record/NAME/
set -u
cd "${0:A:h:h:h}"
name=$1
extra="${2:-}"
[[ -z "$extra" ]] && extra='{}'
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
tmp=$(mktemp -d)
python3 - "$tmp/open.json" <<'EOF'
import json, sys
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        'if (s.path != "Assets/_Project/Games/Pota/Scenes/ColorHunt.unity") { if (s.isDirty) return "dirty: " + s.path; '
        'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Games/Pota/Scenes/ColorHunt.unity"); } '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
M call execute_code @"$tmp/open.json" | grep '"result"' | grep -q ColorHunt || { echo "could not open ColorHunt (unsaved scene?)"; exit 1; }
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
cat "Temp/record/$name/events.json"
rm -rf "$tmp"
