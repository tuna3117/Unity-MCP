#!/bin/zsh
# Saves a screenshot into docs/screenshots at a repo-friendly size (longest edge 1280 px, JPEG quality 80).
# Usage: Tools/keep_screenshot.sh <source image> <name without extension>
#   e.g. Tools/keep_screenshot.sh Temp/mcp_out/basket.png 2026-09-28-basket-portrait
set -eu
ROOT="${0:A:h:h}"
[[ $# -eq 2 ]] || { echo "usage: $0 <source image> <name>" >&2; exit 2; }
OUT="$ROOT/docs/screenshots/$2.jpg"
mkdir -p "${OUT:h}"
sips -Z 1280 -s format jpeg -s formatOptions 80 "$1" --out "$OUT" >/dev/null
echo "$OUT ($(( $(stat -f%z "$OUT") / 1024 )) KB)"
