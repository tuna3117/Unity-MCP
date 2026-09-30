#!/usr/bin/env python3
"""Pota and Unity looks side by side in one video. Every column is cut around the same events (tap, rotate, …) and the cuts are
joined, so the columns' moments line up even though their boards differ; a label strip sits on top.

Usage: uv run --with pillow Tools/potalook/side_video.py OUT.mp4 --pota VIDEO EVENTS --unity LABEL=DIR [LABEL=DIR ...]
       [--segments tap:-1:3.4,rotate:-0.5:2.5] [--width 540] [--height 960]
Unity DIRs hold ai_colorhunt_record frames and events.json; the frames are named by game time and crowd together in hit-stops and
slow motion, so each column is resampled to one frame per 1/30 s of game time before cutting.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import compare  # noqa: E402

FFMPEG = compare.FFMPEG
FPS = 30
FONT = os.path.join(os.path.dirname(os.path.dirname(HERE)), "Assets/_Project/Games/Pota/Art/Fonts/LilitaOne-Regular.ttf")


def resample(frames, fps):
    """[(path, t), …] sorted by t → one path per 1/fps tick from 0 to the last frame: the latest frame at or before each tick."""
    out, k = [], 0
    last = frames[-1][1]
    i = 0
    while i / fps <= last + 1e-6:
        tick = i / fps + 1e-6
        while k + 1 < len(frames) and frames[k + 1][1] <= tick:
            k += 1
        out.append(frames[k][0])
        i += 1
    return out


def parse_segments(text):
    """"tap:-1:3.4,rotate:-0.5:2.5" → [("tap", -1.0, 3.4), ("rotate", -0.5, 2.5)]."""
    out = []
    for part in text.split(","):
        name, before, after = part.split(":")
        out.append((name, float(before), float(after)))
    return out


def trims(events, segments):
    """(start, end) windows in one recording's own time; a window may not start before the recording."""
    out = []
    for name, before, after in segments:
        start = events[name] + before
        if start < -1e-3:
            raise ValueError(f"the {name} window starts {-start:.2f} s before the recording")
        out.append((max(0.0, start), events[name] + after))           # events are floats: 0.999999 − 1 is the start
    return out


def build_filter(columns, width, height):
    """columns[i] = input i's windows; input len(columns) is the label strip. Same segment count in every column."""
    parts = []
    count = len(columns[0])
    for c, windows in enumerate(columns):
        outs = "".join(f"[c{c}s{k}]" for k in range(count))
        parts.append(f"[{c}:v]fps=30,scale={width}:{height}:flags=lanczos,setsar=1,split={count}{outs}")
        for k, (a, b) in enumerate(windows):
            parts.append(f"[c{c}s{k}]trim={a:.4f}:{b:.4f},setpts=PTS-STARTPTS[c{c}t{k}]")
    for k in range(count):
        parts.append("".join(f"[c{c}t{k}]" for c in range(len(columns))) + f"hstack=inputs={len(columns)}[row{k}]")
    parts.append("".join(f"[row{k}]" for k in range(count)) + f"concat=n={count}:v=1[body]")
    parts.append(f"[{len(columns)}:v]fps=30,setsar=1[head]")
    parts.append("[head][body]vstack=shortest=1,format=yuv420p[out]")
    return ";".join(parts)


def header(labels, width, out):
    strip = Image.new("RGB", (width * len(labels), 56), (16, 22, 34))
    draw = ImageDraw.Draw(strip)
    font = ImageFont.truetype(FONT, 34)
    for i, text in enumerate(labels):
        w = draw.textlength(text, font=font)
        draw.text((i * width + (width - w) / 2, 10), text, font=font, fill=(255, 200, 61))
    strip.save(out)


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("out")
    ap.add_argument("--pota", nargs=2, metavar=("VIDEO", "EVENTS"), required=True)
    ap.add_argument("--unity", nargs="+", required=True)
    ap.add_argument("--segments", default="tap:-1:3.4,rotate:-0.5:2.5")
    ap.add_argument("--width", type=int, default=540)
    ap.add_argument("--height", type=int, default=960)
    a = ap.parse_args(argv)
    segments = parse_segments(a.segments)
    rows = compare.parse_rows(a.unity)
    work = tempfile.mkdtemp(prefix="side-")
    try:
        _, pota_times = compare.pota_anchor_times(a.pota[0], a.pota[1], work)
        columns = [trims(pota_times, segments)]
        inputs = ["-i", a.pota[0]]
        for c, (_, folder) in enumerate(rows):
            columns.append(trims(json.load(open(os.path.join(folder, "events.json"))), segments))
            steady = os.path.join(work, f"col{c}")
            os.makedirs(steady)
            for i, path in enumerate(resample(compare.frame_sheet.load_frames(folder), FPS)):
                os.symlink(os.path.abspath(path), os.path.join(steady, f"s_{i:05d}.png"))
            inputs += ["-framerate", str(FPS), "-i", os.path.join(steady, "s_%05d.png")]
        head = os.path.join(work, "head.png")
        header(["Pota (three.js)"] + [label for label, _ in rows], a.width, head)
        inputs += ["-loop", "1", "-i", head]
        subprocess.run([FFMPEG, "-hide_banner", "-loglevel", "error", "-y", *inputs,
                        "-filter_complex", build_filter(columns, a.width, a.height), "-map", "[out]",
                        "-c:v", "libx264", "-crf", "23", "-preset", "slow", "-movflags", "+faststart", a.out], check=True)
        print(a.out)
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main(sys.argv[1:])
