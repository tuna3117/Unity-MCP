#!/usr/bin/env python3
"""Side-by-side video of PotaFlow recordings (and Pota's own transition): one column per frame folder, a label strip on top.

Usage: uv run --with pillow Tools/potaflow_side.py OUT.mp4 LABEL=DIR [LABEL=DIR ...] [--width 405] [--height 720] [--fps 30]
       [--end 3.9] [--crf 26]
Frames are PNGs named ..._t<seconds>... (ai_potaflow_record, or Pota frames renamed by the sync marker). Every column is resampled
to one frame per 1/fps of its own time; a column that ends early holds its last frame (the board is ready, nothing moves).
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from frame_sheet import frame_time, nearest  # noqa: E402

FFMPEG = "/opt/homebrew/bin/ffmpeg"
FONT = "/System/Library/Fonts/Supplemental/Arial Black.ttf"   # has ğ/ş/İ (Lilita One does not)
HEAD = 56
BG = (16, 22, 34)
GOLD = (255, 200, 61)


def frames_of(folder):
    fs = [(os.path.join(folder, f), frame_time(f)) for f in sorted(os.listdir(folder))]
    fs = [f for f in fs if f[1] is not None]
    if not fs:
        raise SystemExit(f"no _t frames in {folder}")
    return fs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("columns", nargs="+", help="LABEL=DIR")
    ap.add_argument("--width", type=int, default=404, help="per column; the total must stay even for H.264")
    ap.add_argument("--height", type=int, default=720)
    ap.add_argument("--fps", type=int, default=30)
    ap.add_argument("--end", type=float, default=None, help="seconds; default: the longest column")
    ap.add_argument("--crf", type=int, default=26)
    a = ap.parse_args()
    cols = []
    for c in a.columns:
        label, folder = c.split("=", 1)
        cols.append((label, frames_of(folder)))
    end = a.end if a.end is not None else max(f[-1][1] for _, f in cols)
    n = int(round(end * a.fps)) + 1
    font = ImageFont.truetype(FONT, 26)
    tmp = tempfile.mkdtemp(prefix="potaflow-side-")
    try:
        cache = {}
        for i in range(n):
            t = i / a.fps
            total = a.width * len(cols) + (a.width * len(cols)) % 2            # H.264 wants even sizes
            canvas = Image.new("RGB", (total, HEAD + a.height), BG)
            d = ImageDraw.Draw(canvas)
            for k, (label, frames) in enumerate(cols):
                path = nearest(frames, min(t, frames[-1][1]))
                im = cache.get(path)
                if im is None:
                    im = Image.open(path).convert("RGB").resize((a.width, a.height), Image.LANCZOS)
                    cache[path] = im
                    if len(cache) > 64:
                        cache.pop(next(iter(cache)))
                canvas.paste(im, (k * a.width, HEAD))
                w = d.textlength(label, font=font)
                d.text((k * a.width + (a.width - w) / 2, 12), label, fill=GOLD, font=font)
            d.text((8, 18), f"{t:0.2f} s", fill=(200, 200, 200), font=ImageFont.truetype(FONT, 18))   # in the strip, off the frames
            canvas.save(os.path.join(tmp, f"f_{i:05d}.png"))
        subprocess.run([FFMPEG, "-y", "-loglevel", "error", "-framerate", str(a.fps), "-i", os.path.join(tmp, "f_%05d.png"),
                        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", str(a.crf), a.out], check=True)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print(f"{a.out}: {len(cols)} columns, {n} frames, {end:.2f} s")


if __name__ == "__main__":
    main()
