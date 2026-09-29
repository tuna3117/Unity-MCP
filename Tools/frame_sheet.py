#!/usr/bin/env python3
"""Contact sheet from recorded frame folders: one row per folder, one column per time.

Usage: uv run --with pillow Tools/frame_sheet.py OUT.jpg LABEL=DIR [LABEL=DIR ...] [--times 0.2,0.8,1.3,1.8,2.3,2.6] [--height 480]
Frames are PNGs named ..._t<seconds>... (ai_potaflow_record, ai_basket_record, or renamed ffmpeg output).
"""
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

_T = re.compile(r"_t(\d+(?:\.\d+)?)")


def frame_time(name):
    m = _T.search(name)
    return float(m.group(1)) if m and name.lower().endswith(".png") else None


def nearest(frames, t):
    return min(frames, key=lambda f: abs(f[1] - t))[0]


def load_frames(folder):
    out = []
    for n in sorted(os.listdir(folder)):
        t = frame_time(n)
        if t is not None:
            out.append((os.path.join(folder, n), t))
    if not out:
        sys.exit(f"no frames in {folder}")
    return out


def main(argv):
    times, height, rows, out = [0.2, 0.8, 1.3, 1.8, 2.3, 2.6], 480, [], None
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--times":
            times = [float(x) for x in argv[i + 1].split(",")]
            i += 2
            continue
        if a == "--height":
            height = int(argv[i + 1])
            i += 2
            continue
        if out is None:
            out = a
        else:
            label, folder = a.split("=", 1)
            rows.append((label, load_frames(folder)))
        i += 1
    if out is None or not rows:
        sys.exit(__doc__)
    first = Image.open(rows[0][1][0][0])
    w = round(first.width * height / first.height)
    label_w, head_h = 150, 44
    sheet = Image.new("RGB", (label_w + w * len(times), head_h + height * len(rows)), (18, 22, 30))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 28)
    except OSError:
        font = ImageFont.load_default()
    for c, t in enumerate(times):
        draw.text((label_w + c * w + 10, 8), f"{t:.1f} s", fill=(240, 240, 240), font=font)
    for r, (label, frames) in enumerate(rows):
        y = head_h + r * height
        draw.text((12, y + height // 2 - 14), label, fill=(255, 200, 60), font=font)
        for c, t in enumerate(times):
            img = Image.open(nearest(frames, t)).convert("RGB").resize((w, height), Image.LANCZOS)
            sheet.paste(img, (label_w + c * w, y))
    sheet.save(out, quality=85)
    print(f"{out}: {len(rows)} rows × {len(times)} times, {sheet.width}×{sheet.height}")


if __name__ == "__main__":
    main(sys.argv[1:])
