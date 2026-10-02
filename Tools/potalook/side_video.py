#!/usr/bin/env python3
"""Looks side by side in one video.

Pota (three.js) | Unity: every column is cut around the same events (tap, rotate, …) and the cuts are joined, so the columns' moments
line up even though their boards differ; a label strip sits on top.
Usage: uv run --with pillow Tools/potalook/side_video.py OUT.mp4 --pota VIDEO EVENTS --unity LABEL=DIR [LABEL=DIR ...]
       [--segments tap:-1:3.4,rotate:-0.5:2.5] [--width 540] [--height 960]

Groups (Unity only, silent; the special balls' Pota | 1 | 2 per scenario): one recording per column and group, groups one after another.
Usage: uv run --with pillow Tools/potalook/side_video.py groups OUT.mp4 --group TITLE LABEL=DIR [LABEL=DIR ...] [--group ...]
       [--anchor tap] [--before 0.6] [--width 540] [--height H] [--crf 28]
Each group's columns are aligned at their own anchor marker and run until the longest one ends (a shorter one holds its last frame).

Unity DIRs hold ai_colorhunt_record frames and events.json; the frames are named by the recording clock (the time as felt: they crowd
together in hit-stops and slow motion), so each column is resampled to one frame per 1/30 s of that clock.
"""
import argparse
import functools
import json
import math
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
HEAD_H = 56
BG = (16, 22, 34)
GOLD = (255, 200, 61)
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


def header_image(labels, width):
    """A HEAD_H-tall strip, one centred gold label per column of `width`."""
    strip = Image.new("RGB", (width * len(labels), HEAD_H), BG)
    draw = ImageDraw.Draw(strip)
    font = ImageFont.truetype(FONT, 34)
    for i, text in enumerate(labels):
        w = draw.textlength(text, font=font)
        draw.text((i * width + (width - w) / 2, 10), text, font=font, fill=GOLD)
    return strip


def header(labels, width, out):
    header_image(labels, width).save(out)


def index_at(t, count, fps=FPS):
    """The tick of a resampled recording that shows its time t (clamped to the recording)."""
    return max(0, min(count - 1, int(math.floor(t * fps + 1e-6))))


def window(anchors, ends, before):
    """(start, end) relative to each column's anchor: `before` s ahead of the anchors to the latest column's end."""
    return -before, max(e - a for a, e in zip(anchors, ends))


def parse_groups(groups):
    """[["Bomba", "Pota=a", "1 · Royal=b"], …] → [("Bomba", [("Pota", "a"), ("1 · Royal", "b")]), …]."""
    out = []
    for g in groups:
        if len(g) < 2:
            raise ValueError(f"a group needs a title and at least one LABEL=DIR, got {g}")
        out.append((g[0], compare.parse_rows(g[1:])))
    return out


@functools.lru_cache(maxsize=24)
def scaled(path, width, height):
    img = Image.open(path).convert("RGB")
    return img if img.size == (width, height) else img.resize((width, height), Image.LANCZOS)


def group_frames(title, columns, width, height, before, anchor="tap"):
    """One group: columns [(label, dir)] aligned at their own `anchor` marker, each resampled at 30 fps, under a "title · label" strip."""
    ticks, anchors, ends = [], [], []
    for _, folder in columns:
        events = json.load(open(os.path.join(folder, "events.json")))
        if anchor not in events:
            raise ValueError(f"{folder}: no '{anchor}' marker in events.json")
        t = resample(compare.frame_sheet.load_frames(folder), FPS)
        ticks.append(t)
        anchors.append(float(events[anchor]))
        ends.append((len(t) - 1) / FPS)
    start, end = window(anchors, ends, before)
    head = header_image([f"{title} · {label}" for label, _ in columns], width)
    for j in range(int(round((end - start) * FPS)) + 1):
        r = start + j / FPS
        frame = Image.new("RGB", (width * len(columns), HEAD_H + height), BG)
        frame.paste(head, (0, 0))
        for c, t in enumerate(ticks):
            frame.paste(scaled(t[index_at(anchors[c] + r, len(t))], width, height), (c * width, HEAD_H))
        yield frame


def encode(frames, size, out, audio=None, crf=28):
    """Pipes PIL frames (all of `size`, even sides) to ffmpeg as raw RGB at 30 fps; muxes `audio` (a WAV) when given."""
    cmd = [FFMPEG, "-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24",
           "-s", f"{size[0]}x{size[1]}", "-framerate", str(FPS), "-i", "-"]
    if audio:
        cmd += ["-i", audio]
    cmd += ["-c:v", "libx264", "-crf", str(crf), "-preset", "slow", "-pix_fmt", "yuv420p"]
    cmd += ["-c:a", "aac", "-b:a", "128k", "-shortest"] if audio else ["-an"]
    cmd += ["-movflags", "+faststart", out]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    try:
        for img in frames:
            proc.stdin.write(img.tobytes())
    finally:
        proc.stdin.close()
    if proc.wait() != 0:
        raise RuntimeError(f"ffmpeg failed for {out}")


def groups_main(argv):
    ap = argparse.ArgumentParser(prog="side_video.py groups", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("--group", nargs="+", action="append", required=True, metavar="TITLE LABEL=DIR")
    ap.add_argument("--anchor", default="tap")
    ap.add_argument("--before", type=float, default=0.6)
    ap.add_argument("--width", type=int, default=540)
    ap.add_argument("--height", type=int)
    ap.add_argument("--crf", type=int, default=28)
    a = ap.parse_args(argv)
    groups = parse_groups(a.group)
    if len({len(cols) for _, cols in groups}) != 1:
        raise ValueError("every group needs the same number of columns")
    if a.height is None:
        first = Image.open(compare.frame_sheet.load_frames(groups[0][1][0][1])[0][0])
        a.height = round(first.height * a.width / first.width)
    a.width -= a.width % 2
    a.height -= a.height % 2
    size = (a.width * len(groups[0][1]), HEAD_H + a.height)

    def frames():
        for title, columns in groups:
            yield from group_frames(title, columns, a.width, a.height, a.before, a.anchor)

    encode(frames(), size, a.out, crf=a.crf)
    print(a.out)


def main(argv):
    if argv and argv[0] == "groups":
        groups_main(argv[1:])
        return
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
