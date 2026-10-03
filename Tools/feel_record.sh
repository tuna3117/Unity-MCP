#!/bin/zsh
# Records every in-game feel moment (spec 2026-10-03) twice — feel off ("Önce") and on ("Sonra") — and builds the deliverables:
#   docs/screenshots/2026-10-03-feel-<moment>-side.mp4   Önce | Sonra, aligned at the moment's marker
#   docs/screenshots/2026-10-03-feel-<moment>-sheet.jpg   timed contact sheet, one row per column
#   docs/screenshots/2026-10-03-feel-all.mp4              every moment one after another
# Usage: Tools/feel_record.sh [record|videos|all] [moment ...]   (default all; moments: intro lowammo finale lose chain idle pull miss swish)
# Unity is never brought to the front; each recording steps the paused editor frame by frame (ai_colorhunt_record / ai_basket_record).
set -u
cd "${0:A:h:h}"
UV=~/.local/bin/uv
FF=/opt/homebrew/bin/ffmpeg
OUT=docs/screenshots
DATE=2026-10-03
what="${1:-all}"; shift || true
moments=("$@")
(( ${#moments} )) || moments=(intro lowammo finale lose chain idle pull miss swish)
M() { $UV run Tools/mcp_call.py "$@" 2>&1; }

# ---- recording ----
open_scene() {  # open_scene Basket|ColorHunt (refuses a dirty open scene)
  local tmp; tmp=$(mktemp)
  python3 - "$tmp" "$1" <<'EOF'
import json, sys
path = f"Assets/_Project/Games/Pota/Scenes/{sys.argv[2]}.unity"
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        f'if (s.path != "{path}") {{ if (s.isDirty) return "dirty: " + s.path; '
        f'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{path}"); }} '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
  M call execute_code @"$tmp" | grep '"result"' | grep -q "$1" || { echo "could not open $1 (unsaved scene?)"; rm -f "$tmp"; return 1; }
  rm -f "$tmp"
}
play() { M call manage_editor '{"action":"play"}' > /dev/null; for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4; }
stop() { M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3; }
poll() {  # poll TOOL
  for i in {1..240}; do
    sleep 3
    r=$(M call execute_custom_tool "{\"tool_name\":\"$1\",\"parameters\":{\"action\":\"status\"}}" | grep '"result"\|"message"' | head -2 | tr '\n' ' ')
    echo "$r" | grep -q 'done\|error\|idle\|timeout\|cancelled' && { echo "   $r"; return; }
  done
  echo "   (still running after 12 min)"
}
record_colorhunt() {  # record_colorhunt MOMENT on|off
  local name="feel-$1-$2"
  echo "== $name"
  M call execute_custom_tool "{\"tool_name\":\"ai_colorhunt_record\",\"parameters\":{\"name\":\"$name\",\"scenario\":\"$1\",\"feel\":\"$2\"}}" | grep '"message"' | head -1
  poll ai_colorhunt_record
}
record_basket() {  # record_basket MODE on|off
  local name="feel-$1-$2"
  echo "== $name"
  M call execute_custom_tool "{\"tool_name\":\"ai_basket_record\",\"parameters\":{\"name\":\"$name\",\"mode\":\"$1\",\"feel\":\"$2\",\"every\":4,\"seconds\":14}}" | grep '"message"' | head -1
  poll ai_basket_record
}
is_basket() { [[ "$1" == pull || "$1" == miss || "$1" == swish ]]; }

if [[ "$what" == record || "$what" == all ]]; then
  stop
  ch=(); bk=()
  for m in "${moments[@]}"; do if is_basket "$m"; then bk+=("$m"); else ch+=("$m"); fi; done
  if (( ${#ch} )); then
    open_scene ColorHunt || exit 1
    for m in "${ch[@]}"; do for f in off on; do play; record_colorhunt "$m" "$f"; stop; done; done
  fi
  if (( ${#bk} )); then
    open_scene Basket || exit 1
    for m in "${bk[@]}"; do for f in off on; do play; record_basket "$m" "$f"; stop; done; done
  fi
fi

# ---- videos and sheets ----
title() { case "$1" in
  intro) echo "Bölüm başı";; lowammo) echo "Az top gerilimi";; finale) echo "Kalan topların finali";; lose) echo "Kaybetme anı";;
  chain) echo "Zincir kelimeleri";; idle) echo "Boşta ipucu";; pull) echo "Sapan";; miss) echo "Iska";; swish) echo "Temiz giriş";; esac; }
anchor() { case "$1" in
  intro|idle) echo start;; lowammo|chain) echo tap;; finale) echo result;; lose) echo result;; pull) echo pull;; miss) echo miss;; swish) echo shot;; esac; }
before() { case "$1" in intro|idle) echo 0;; lowammo) echo 0.5;; finale|lose) echo 0.8;; chain) echo 0.3;; pull) echo 0.4;; miss) echo 1.2;; swish) echo 0.3;; esac; }
times() { case "$1" in
  intro) echo "0.1,0.4,0.8,1.0,1.3,1.7,2.2";; lowammo) echo "0.5,1.2,1.6,2.0,2.5,3.0";; finale) echo "1.4,1.8,2.1,2.5,2.9,3.3,3.9,4.6";;
  lose) echo "0.1,0.4,0.8,1.2,1.6,2.2";; chain) echo "0.7,1.0,1.2,1.4,1.6,1.9";; idle) echo "0.5,5.6,5.8,6.0,6.3,7.2";;
  pull) echo "0.3,0.8,1.3,1.8,2.1,2.6,3.4";; miss) echo "1.2,2.0,2.6,3.0,3.3,3.8";; swish) echo "0.5,1.2,1.5,1.7,2.0,2.6";; esac; }
if [[ "$what" == videos || "$what" == all ]]; then
  mkdir -p "$OUT"
  parts=()
  for m in "${moments[@]}"; do
    off="Temp/record/feel-$m-off"; on="Temp/record/feel-$m-on"
    [[ -f "$off/events.json" && -f "$on/events.json" ]] || { echo "skip $m: no recordings"; continue; }
    side="$OUT/$DATE-feel-$m-side.mp4"
    $UV run --with pillow Tools/potalook/side_video.py groups "$side" --group "$(title $m)" "Önce=$off" "Sonra=$on" --anchor "$(anchor $m)" --before "$(before $m)" --width 540 || continue
    $UV run --with pillow Tools/frame_sheet.py "$OUT/$DATE-feel-$m-sheet.jpg" "Önce=$off" "Sonra=$on" --times "$(times $m)" --height 420
    parts+=("$side")
  done
  if (( ${#parts} > 1 )); then
    list=$(mktemp); for p in "${parts[@]}"; do echo "file '$PWD/$p'" >> "$list"; done
    $FF -y -v error -f concat -safe 0 -i "$list" -c copy "$OUT/$DATE-feel-all.mp4" && echo "$OUT/$DATE-feel-all.mp4"
    rm -f "$list"
  fi
fi
