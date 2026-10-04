#!/bin/zsh
# Raw | Stylised proof of an animated character delivery (rig + animation pipeline, 2026-10-04).
# For every clip: its raw and stylised take filmed with one shared camera (ai_anim_contact_sheet frames_dir + frame_clips),
# a two-row contact sheet (Ham on top, Stilize below) and one side-by-side video of all clips, played one after another.
# Usage: Tools/anim_proof.sh <model asset path> <prefab asset path> <out name> CLIP [CLIP ...]
#   CLIP is "name" (raw take "name_raw", stylised "name") or "title:raw:stylised".
# Writes docs/screenshots/<out name>-<title>-sheet.jpg and docs/screenshots/<out name>-side.mp4; frames in Temp/anim_proof/<out name>/.
# Never brings Unity to the front: everything goes through the MCP bridge in Edit Mode.
set -eu
ROOT="${0:A:h:h}"
cd "$ROOT"
[[ $# -ge 4 ]] || { sed -n '2,8p' "$0" >&2; exit 2; }
MODEL=$1 PREFAB=$2 NAME=$3
shift 3
UV=~/.local/bin/uv
OUT=Temp/anim_proof/$NAME
rm -rf "$OUT"; mkdir -p "$OUT"

tool() {   # tool <json parameters>: one project tool call, stops the script on an error reply
  local reply
  reply=$($UV run -q Tools/mcp_call.py call execute_custom_tool "{\"tool_name\":\"ai_anim_contact_sheet\",\"parameters\":$1}" 2>&1)
  echo "$reply" | grep -q '"success": true' || { echo "anim_proof: $reply" | head -c 600 >&2; exit 1; }
}

groups=()
for spec in "$@"; do
  if [[ $spec == *:*:* ]]; then title=${spec%%:*}; rest=${spec#*:}; raw=${rest%%:*}; styl=${rest#*:}
  else title=$spec; raw=${spec}_raw; styl=$spec; fi
  for v in raw styl; do
    clip=$styl; other=$raw
    [[ $v == raw ]] && { clip=$raw; other=$styl; }
    common="\"model_path\":\"$PREFAB\",\"clip_path\":\"$MODEL\",\"clip_name\":\"$clip\",\"frame_clips\":[\"$other\"],\"angle_deg\":30"
    tool "{$common,\"frames_dir\":\"$OUT/$title/$v\",\"size\":540}"
    echo '{"start": 0.0}' > "$OUT/$title/$v/events.json"
    tool "{$common,\"frames\":8,\"size\":256,\"out_path\":\"$OUT/$title/$v.png\"}"
  done
  $UV run -q --with pillow python - "$OUT/$title" "$title" "docs/screenshots/$NAME-$title-sheet.jpg" <<'PY'
import sys
from PIL import Image, ImageDraw, ImageFont
folder, title, out = sys.argv[1:4]
rows = [("Ham", Image.open(f"{folder}/raw.png").convert("RGB")), ("Stilize", Image.open(f"{folder}/styl.png").convert("RGB"))]
font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 26)
label_w, w = 170, max(im.width for _, im in rows)
sheet = Image.new("RGB", (label_w + w, sum(im.height for _, im in rows)), (18, 22, 30))
draw, y = ImageDraw.Draw(sheet), 0
for label, im in rows:
    sheet.paste(im, (label_w, y))
    draw.text((14, y + 20), title, fill=(240, 240, 240), font=font)
    draw.text((14, y + im.height // 2), label, fill=(255, 200, 60), font=font)
    y += im.height
sheet.save(out, quality=82)
print(out)
PY
  groups+=(--group "$title" "Ham=$OUT/$title/raw" "Stilize=$OUT/$title/styl")
done
$UV run -q --with pillow Tools/potalook/side_video.py groups "docs/screenshots/$NAME-side.mp4" "${groups[@]}" \
  --anchor start --before 0 --width 540 --height 540
echo "docs/screenshots/$NAME-side.mp4"
