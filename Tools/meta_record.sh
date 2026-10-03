#!/bin/zsh
# Records every menu/reward moment (spec 2026-10-03 meta feel) twice — feel off ("Önce") and on ("Sonra") — next to Pota's own
# screen (a still from ~/projects/Pota/onizleme/, read only) and builds the deliverables:
#   docs/screenshots/2026-10-03-meta-<scenario>-side.mp4   Pota | Önce | Sonra, aligned at the scenario's marker
#   docs/screenshots/2026-10-03-meta-<scenario>-sheet.jpg  timed contact sheet, one row per column
#   docs/screenshots/2026-10-03-meta-all.mp4               every scenario one after another
# Usage: Tools/meta_record.sh [record|videos|all] [scenario ...]   (default all; scenarios: menu level win lose map promotion bonus market tutorial)
# Unity is never brought to the front; each recording steps the paused editor frame by frame (ai_meta_record).
set -u
cd "${0:A:h:h}"
UV=~/.local/bin/uv
FF=/opt/homebrew/bin/ffmpeg
OUT=docs/screenshots
DATE=2026-10-03
POTA=~/projects/Pota/onizleme
what="${1:-all}"; shift || true
scenarios=("$@")
(( ${#scenarios} )) || scenarios=(menu level win lose map promotion bonus market tutorial)
M() { $UV run Tools/mcp_call.py "$@" 2>&1; }

open_scene() {  # refuses a dirty open scene
  local tmp; tmp=$(mktemp)
  python3 - "$tmp" <<'EOF'
import json, sys
path = "Assets/_Project/Games/Pota/Scenes/Meta.unity"
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        f'if (s.path != "{path}") {{ if (s.isDirty) return "dirty: " + s.path; '
        f'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{path}"); }} '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
  M call execute_code @"$tmp" | grep '"result"' | grep -q "Meta" || { echo "could not open Meta (unsaved scene?)"; rm -f "$tmp"; return 1; }
  rm -f "$tmp"
}
play() { M call manage_editor '{"action":"play"}' > /dev/null; for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4; }
stop() { M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3; }
poll() {
  for i in {1..200}; do
    sleep 3
    r=$(M call execute_custom_tool '{"tool_name":"ai_meta_record","parameters":{"action":"status"}}' | grep '"result"\|"message"' | head -2 | tr '\n' ' ')
    echo "$r" | grep -q 'done\|error\|idle\|never\|cancelled\|ended' && { echo "   $r"; return; }
  done
  echo "   (still running after 10 min)"
}
record() {  # record SCENARIO on|off
  local name="meta-$1-$2"
  echo "== $name"
  M call execute_custom_tool "{\"tool_name\":\"ai_meta_record\",\"parameters\":{\"name\":\"$name\",\"scenario\":\"$1\",\"feel\":\"$2\"}}" | grep '"message"' | head -1
  poll
}

# Pota's own screen as a one-frame column (its markers all at 0, so the videos align it at the anchor and hold it).
still() { case "$1" in
  menu) echo renkavi-01-menu.png;; level) echo renkavi-02-bolum-pencere.png;; win) echo akis-09-kazandi.png;; lose) echo kaybetti-bedava.png;;
  map) echo menu-2-harita.png;; promotion) echo terfi.png;; bonus) echo bonus-durdu.png;; market) echo market-1-giysi.png;; tutorial) echo ogretici-parmak.png;; esac; }
pota_column() {  # pota_column SCENARIO → prints the folder or nothing
  local f; f=$(still "$1"); [[ -n "$f" && -f "$POTA/$f" ]] || return 0
  local dir="Temp/record/pota-$1"; rm -rf "$dir"; mkdir -p "$dir"
  cp "$POTA/$f" "$dir/f_0000_t0.000.png"
  printf '{"start":0,"open":0,"play":0,"score":0,"medal":0,"coins":0,"continue":0,"devam":0,"map":0,"unlock":0,"league":0,"spin":0,"stop":0,"tap":0,"buy":0,"fail":0,"end":0.1,"meta":{"scene":"Pota","scenario":"%s","feel":false,"still":"%s"}}\n' "$1" "$f" > "$dir/events.json"
  echo "$dir"
}

if [[ "$what" == record || "$what" == all ]]; then
  stop
  open_scene || exit 1
  for s in "${scenarios[@]}"; do for f in off on; do play; record "$s" "$f"; stop; done; done
fi

title() { case "$1" in
  menu) echo "Ana ekran";; level) echo "Bölüm penceresi";; win) echo "Kazanma";; lose) echo "Kaybetme";; map) echo "Harita ilerlemesi";;
  promotion) echo "Lig terfisi";; bonus) echo "Bonus";; market) echo "Market";; tutorial) echo "Öğretici";; esac; }
anchor() { case "$1" in bonus) echo spin;; market) echo buy;; map) echo devam;; *) echo open;; esac; }
before() { case "$1" in market) echo 1.0;; map) echo 0.3;; *) echo 0.2;; esac; }
times() { case "$1" in
  menu) echo "0.1,0.3,0.6,1.5,3.0,4.5,6.5";; level) echo "0.05,0.15,0.3,0.6,2.3,2.6,3.2";; win) echo "0.1,0.3,0.6,0.9,1.3,1.6,2.0,2.6,3.6";;
  lose) echo "0.05,0.15,0.3,0.6,1.2,2.2";; map) echo "0.3,0.8,1.2,1.5,1.9,2.3,2.8,3.4";; promotion) echo "0.1,0.3,0.5,0.8,1.5,3.3,3.9";;
  bonus) echo "0.3,1.0,1.5,2.0,2.3,2.8,3.6";; market) echo "-1.0,0.1,0.4,0.8,1.3,2.2,3.0";; tutorial) echo "0.1,0.3,1.3,2.3,3.6,4.9,6.0";; esac; }
if [[ "$what" == videos || "$what" == all ]]; then
  mkdir -p "$OUT"
  parts=()
  for s in "${scenarios[@]}"; do
    off="Temp/record/meta-$s-off"; on="Temp/record/meta-$s-on"
    [[ -f "$off/events.json" && -f "$on/events.json" ]] || { echo "skip $s: no recordings"; continue; }
    cols=(); pota=$(pota_column "$s"); [[ -n "$pota" ]] && cols+=("Pota=$pota")
    cols+=("Önce=$off" "Sonra=$on")
    side="$OUT/$DATE-meta-$s-side.mp4"
    $UV run --with pillow Tools/potalook/side_video.py groups "$side" --group "$(title $s)" "${cols[@]}" --anchor "$(anchor $s)" --before "$(before $s)" --width 540 || continue
    $UV run --with pillow Tools/frame_sheet.py "$OUT/$DATE-meta-$s-sheet.jpg" "${cols[@]}" --times "$(times $s)" --anchor "$(anchor $s)" --height 420
    parts+=("$side")
  done
  if (( ${#parts} > 1 )); then
    list=$(mktemp); for p in "${parts[@]}"; do echo "file '$PWD/$p'" >> "$list"; done
    $FF -y -v error -f concat -safe 0 -i "$list" -c copy "$OUT/$DATE-meta-all.mp4" && echo "$OUT/$DATE-meta-all.mp4"
    rm -f "$list"
  fi
fi
