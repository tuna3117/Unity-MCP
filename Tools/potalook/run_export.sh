#!/bin/zsh
# Pota's current puzzle look → Unity. ~/projects/Pota is READ-ONLY: everything runs in <scratch>/pota-copy.
# Usage: Tools/potalook/run_export.sh <scratch dir>     then: execute_custom_tool ai_potalook_import
set -euo pipefail
SRC="$HOME/projects/Pota"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRATCH="${1:?usage: run_export.sh <scratch dir>}"
COPY="$SCRATCH/pota-copy"
OUT="$SCRATCH/potalook-out"
NODE="$HOME/.nvm/versions/node/v22.23.2/bin/node"
PORT=5199                                   # another Pota session serves on :5174
ART="$ROOT/Assets/_Project/Games/Pota/Art"
LOOK="$ART/PotaLook"
AUDIO="$ROOT/Assets/_Project/Games/Pota/Audio/PotaLook"

rsync -a --delete --exclude .git --exclude dist "$SRC/" "$COPY/"
# The drawing helpers are module-private in Pota; export them in the COPY only.
sed -i '' -e 's/^class EtiketAtlasi /export class EtiketAtlasi /' "$COPY/src/yapbozSahne.ts"
sed -i '' -e 's/^function yildizKivilcim(/export function yildizKivilcim(/' \
          -e 's/^function izDokusu(/export function izDokusu(/' \
          -e 's/^function yumusakDaire(/export function yumusakDaire(/' "$COPY/src/patlamaEfekt.ts"
grep -q '^export class EtiketAtlasi ' "$COPY/src/yapbozSahne.ts"
[ "$(grep -c '^export function \(yildizKivilcim\|izDokusu\|yumusakDaire\)(' "$COPY/src/patlamaEfekt.ts")" = 3 ]
mkdir -p "$COPY/deneme"
cp "$ROOT/Tools/potalook/export-page.html" "$COPY/deneme/potalook-export.html"
cp "$ROOT/Tools/potalook/export-page.ts" "$COPY/deneme/potalook-export.ts"

rm -rf "$OUT"; mkdir -p "$OUT"
( cd "$COPY" && exec "$NODE" node_modules/.bin/vite --port $PORT --strictPort ) > "$SCRATCH/potalook-vite.log" 2>&1 &
VITE=$!
trap 'kill $VITE 2>/dev/null || true' EXIT
"$NODE" "$ROOT/Tools/potalook/export.mjs" "http://localhost:$PORT/deneme/potalook-export.html" "$OUT"

mkdir -p "$LOOK" "$AUDIO" "$ART/Fonts"
cp "$OUT"/{label-atlas,star,trail,soft-disc,soft-ring,ball-skin}.png "$LOOK/"
cp "$OUT"/*.wav "$AUDIO/"
# Files Pota ships: the item stickers replace the old set IN PLACE (same names → same .meta/GUID), backboard, arrow.
for n in forma ayakkabi sapka corap bileklik; do cp "$COPY/public/assets/esya-$n.png" "$ART/esya-$n.png"; done
cp "$COPY/public/assets/ok-a.png" "$ART/ok-a.png"
cp "$COPY/public/assets/pano-a.png" "$LOOK/pano-a.png"
# Lilita One (SIL OFL 1.1): Pota ships only woff2 subsets, Unity needs the TTF.
[ -f "$ART/Fonts/LilitaOne-Regular.ttf" ] || curl -fsSL -o "$ART/Fonts/LilitaOne-Regular.ttf" https://github.com/google/fonts/raw/main/ofl/lilitaone/LilitaOne-Regular.ttf
[ -f "$ART/Fonts/LilitaOne-OFL.txt" ] || curl -fsSL -o "$ART/Fonts/LilitaOne-OFL.txt" https://github.com/google/fonts/raw/main/ofl/lilitaone/OFL.txt
echo "textures: $(ls "$LOOK"/*.png | wc -l | tr -d ' ')  sounds: $(ls "$AUDIO"/*.wav | wc -l | tr -d ' ')"
