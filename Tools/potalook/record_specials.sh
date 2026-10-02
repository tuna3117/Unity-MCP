#!/bin/zsh
# Special balls (spec 2026-10-01 §1.4, plan Task 12): records the five scenarios in the three special looks and the Royal/Court close-ups
# with record_unity.sh, then builds the per-scenario moment sheets, the silent Pota | 1 | 2 side video, the voiced sequential video (each
# scenario as Pota → 1 → 2) and the close-up videos into docs/screenshots/2026-10-01-specials-*; 'cost' measures the phone cost.
# Usage: Tools/potalook/record_specials.sh [all|record|build|cost]
#   LOOKS="royal" SCENARIOS="bomb line" CLOSEUPS=0 Tools/potalook/record_specials.sh record   → re-record a subset after a fix
#   CRF=30 Tools/potalook/record_specials.sh build                                            → smaller videos
# The side video (three 540-px columns) has its own SIDE_CRF (30); the moment sheets are saved at JPEG quality SHEET_Q (56): their dense
# boards made quality 80 ~610 KB each, 56 keeps them under 400 KB with no visible loss at 2× zoom.
set -u
cd "${0:A:h:h:h}"
stage=${1:-all}
LOOKS=(${=LOOKS:-pota royal court})
SCENARIOS=(${=SCENARIOS:-birth line bomb chain golden})
CLOSEUPS=${CLOSEUPS:-1}
CRF=${CRF:-28}
SIDE_CRF=${SIDE_CRF:-30}
SHEET_Q=${SHEET_Q:-56}
ASPECT=0.4623                         # PotaProfile 1170×2532 → 540 × 1168 frames
UV=~/.local/bin/uv
R=Temp/record
OUT=docs/screenshots/2026-10-01-specials
ALL=(birth line bomb chain golden)
CLOSE=(royal court)
typeset -A TITLE LOOK
TITLE=(birth "Doğuş" line "Çizgi" bomb "Bomba" chain "Zincir" golden "Altın")
LOOK=(pota "Pota" royal "1 · Royal" court "2 · Basketbol")
M() { $UV run Tools/mcp_call.py "$@" 2>&1; }

recorded() {   # NAME → exit 0 when the recording ended with "done"
  python3 - "$R/$1/events.json" <<'EOF'
import json, sys
try:
    ev = json.load(open(sys.argv[1]))
except (OSError, ValueError) as e:
    sys.exit(f"{sys.argv[1]}: {e}")
result = ev.get("meta", {}).get("result", "")
sys.exit(0 if result.startswith("done") else f"{sys.argv[1]}: {result or 'no result'}")
EOF
}

record() {     # NAME JSON
  Tools/potalook/record_unity.sh "$1" "$2" && recorded "$1" || { echo "recording $1 failed"; exit 1; }
}

if [[ $stage == all || $stage == record ]]; then
  df -h . | tail -1
  for s in $SCENARIOS; do
    for l in $LOOKS; do
      record sp-$s-$l "{\"scene\":\"Specials\",\"scenario\":\"$s\",\"special_look\":\"$l\",\"aspect\":$ASPECT}"
    done
  done
  if [[ $CLOSEUPS == 1 ]]; then
    for l in ${LOOKS:*CLOSE}; do
      # ×1.5 like the line: at ×2.4 the frame is ~3 cells wide and cut Court's 4-cell SLAM! ("LAM", under the HUD)
      [[ ${SCENARIOS[(Ie)bomb]} -gt 0 ]] && record sp-closeup-$l-bomb \
        "{\"scene\":\"Specials\",\"scenario\":\"bomb\",\"special_look\":\"$l\",\"aspect\":$ASPECT,\"closeup\":true,\"zoom\":1.5,\"until\":\"tap2\",\"tail\":0}"
      [[ ${SCENARIOS[(Ie)line]} -gt 0 ]] && record sp-closeup-$l-line \
        "{\"scene\":\"Specials\",\"scenario\":\"line\",\"special_look\":\"$l\",\"aspect\":$ASPECT,\"closeup\":true,\"zoom\":1.5,\"focus\":24,\"until\":\"tap2\",\"tail\":0}"
    done
  fi
  echo "all recorded"
fi

if [[ $stage == all || $stage == build ]]; then
  for s in $ALL; do for l in pota royal court; do
    [[ -f $R/sp-$s-$l/events.json ]] || { echo "missing $R/sp-$s-$l: record it first"; exit 1; }
  done; done
  for s in $ALL; do
    $UV run -q --with pillow Tools/potalook/compare.py specials $R/specials $s \
      "Pota=$R/sp-$s-pota" "1 Royal=$R/sp-$s-royal" "2 Court=$R/sp-$s-court" || exit 1
    Tools/keep_screenshot.sh $R/specials-$s.jpg 2026-10-01-specials-sheet-$s $SHEET_Q || exit 1
  done
  groups=()
  for s in $ALL; do
    groups+=(--group "${TITLE[$s]}" "Pota=$R/sp-$s-pota" "1 · Royal=$R/sp-$s-royal" "2 · Basketbol=$R/sp-$s-court")
  done
  $UV run -q --with pillow Tools/potalook/side_video.py groups $OUT-side.mp4 "${groups[@]}" --crf $SIDE_CRF || exit 1
  segs=()
  for s in $ALL; do for l in pota royal court; do segs+=("${TITLE[$s]}|${LOOK[$l]}=$R/sp-$s-$l"); done; done
  $UV run -q --with pillow --with numpy Tools/potalook/seq_video.py $OUT-voiced.mp4 "${segs[@]}" --crf $CRF \
    --sync-sheet $R/specials-sync || exit 1
  for l in $CLOSE; do
    for s in bomb line; do
      [[ -f $R/sp-closeup-$l-$s/events.json ]] || { echo "missing close-up $l $s"; exit 1; }
      $UV run -q --with pillow --with numpy Tools/potalook/seq_video.py $OUT-closeup-$l-$s.mp4 \
        "${TITLE[$s]} ×0,3|${LOOK[$l]}=$R/sp-closeup-$l-$s" --silent --card 0 --crf 26 || exit 1
    done
  done
  ls -la $OUT-*
fi

if [[ $stage == cost ]]; then
  open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
  M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
  before=$(git status --short ProjectSettings)
  tmp=$(mktemp -d)
  python3 - "$tmp/open.json" <<'EOF'
import json, sys
path = "Assets/_Project/Games/Pota/Scenes/Specials.unity"
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        f'if (s.path != "{path}") {{ if (s.isDirty) return "dirty: " + s.path; '
        f'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{path}"); }} '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
  M call execute_code @"$tmp/open.json" | grep '"result"' | grep -q Specials || { echo "could not open Specials (unsaved scene?)"; exit 1; }
  M call manage_editor '{"action":"play"}' > /dev/null
  for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
  open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app     # real time: Unity stays in front with the Game view showing
  M call execute_custom_tool '{"tool_name":"ai_specials_cost","parameters":{"looks":"pota,royal,court","runs":3,"scenario":"chain"}}' | grep '"message"' | head -1
  for i in {1..100}; do
    sleep 3
    r=$(M call execute_custom_tool '{"tool_name":"ai_specials_cost","parameters":{"action":"status"}}' | grep '"result"' | head -1)
    echo "$r" | grep -q '"result": "[a-z]' && { echo "$r"; break; }
  done
  M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
  rm -rf "$tmp"
  cat Temp/specials_cost.json
  after=$(git status --short ProjectSettings)
  [[ "$before" == "$after" ]] || { echo "ProjectSettings changed during the measurement:"; echo "$after"; exit 1; }
fi
