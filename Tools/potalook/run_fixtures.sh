#!/bin/zsh
# Fixtures for Unity's Color Hunt special-ball rules, written by Pota's OWN src/renkAvi.ts (spec 2026-10-01 §3).
# ~/projects/Pota is READ-ONLY: everything runs in <scratch>/pota-copy (the same copy run_export.sh uses).
# Usage: Tools/potalook/run_fixtures.sh <scratch dir>     then commit Games/Pota/Tests/EditMode/Fixtures/special-rules.json
set -euo pipefail
SRC="$HOME/projects/Pota"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRATCH="${1:?usage: run_fixtures.sh <scratch dir>}"
COPY="$SCRATCH/pota-copy"
NODE="$HOME/.nvm/versions/node/v22.23.2/bin/node"
DEST="$ROOT/Assets/_Project/Games/Pota/Tests/EditMode/Fixtures"

# Anchored excludes: a bare "dist" would also skip node_modules/*/dist, where tsx keeps its code.
rsync -a --delete --exclude /.git --exclude /dist --exclude /ios "$SRC/" "$COPY/"
cp "$ROOT/Tools/potalook/special_fixtures.ts" "$COPY/arac/unity-special-fixtures.ts"
REV="$(git -C "$SRC" rev-parse --short HEAD) renkAvi.ts $(shasum -a 256 "$COPY/src/renkAvi.ts" | cut -c1-12)"
( cd "$COPY" && POTA_REV="$REV" exec "$NODE" node_modules/.bin/tsx arac/unity-special-fixtures.ts "$SCRATCH/special-rules.json" )
mkdir -p "$DEST"
cp "$SCRATCH/special-rules.json" "$DEST/special-rules.json"
echo "-> $DEST/special-rules.json ($REV)"
