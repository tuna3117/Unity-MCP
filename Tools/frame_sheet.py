#!/usr/bin/env python3
"""Contact sheet from recorded frame folders: one row per folder, one column per time.

Usage: uv run --with pillow Tools/frame_sheet.py OUT.jpg LABEL=DIR [LABEL=DIR ...] [--times 0.2,0.8,1.3,1.8,2.3,2.6] [--height 480]
       [--labels "A|B|…"] [--corner TEXT] [--anchor MARKER] [--transpose]
--anchor MARKER: times are relative to each folder's events.json marker (e.g. tap), so rows whose moments differ in absolute time line up.
Frames are PNGs named ..._t<seconds>... (ai_potaflow_record, ai_basket_record, or renamed ffmpeg output). Columns are labelled by
their time unless --labels gives one label per time ('|'-separated); --corner writes a note in the top-left cell.
--transpose: one column per folder (its label on top) and one row per time (the time labels on the left), for comparing many
options side by side at a few moments.
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


def column_labels(times, labels):
    """The column headers: "<t> s" per time, or the '|'-separated labels (one per time)."""
    if labels is None:
        return [f"{t:.1f} s" for t in times]
    out = labels.split("|")
    if len(out) != len(times):
        raise ValueError(f"{len(out)} labels for {len(times)} times")
    return out


# Turkish labels ("1 Büyük şut"): Arial Bold has ğ/ş/İ; the label column opens to the longest label, up to MAX_LABEL_W,
# and longer labels wrap onto more lines instead of running under the first frame.
FONT = "/System/Library/Fonts/Supplemental/Arial Bold.ttf"
MIN_LABEL_W, MAX_LABEL_W, LABEL_PAD = 150, 360, 24
GAP = 14                                                  # --transpose: dark gap between columns, so similar frames stay apart


def label_width(labels, font):
    """Width of the row-label column: the longest label plus padding, between MIN_LABEL_W and MAX_LABEL_W."""
    longest = max((font.getlength(label) for label in labels), default=0)
    return int(min(MAX_LABEL_W, max(MIN_LABEL_W, longest + LABEL_PAD)))


def wrap(text, font, max_width):
    """Splits text into lines no wider than max_width (a single word wider than that stays on its own line)."""
    lines, line = [], ""
    for word in text.split():
        candidate = f"{line} {word}".strip()
        if line and font.getlength(candidate) > max_width:
            lines.append(line)
            line = word
        else:
            line = candidate
    return lines + ([line] if line else [])


def load_frames(folder):
    out = []
    for n in sorted(os.listdir(folder)):
        t = frame_time(n)
        if t is not None:
            out.append((os.path.join(folder, n), t))
    if not out:
        sys.exit(f"no frames in {folder}")
    return out


def cell_origin(r, c, label_w, head_h, w, height, transpose):
    """Top-left pixel of folder r's frame at time c: folders are rows (default) or columns (--transpose)."""
    if transpose:
        return label_w + r * (w + GAP), head_h + c * height
    return label_w + c * w, head_h + r * height


def main(argv):
    times, height, rows, out, labels, corner, anchor = [0.2, 0.8, 1.3, 1.8, 2.3, 2.6], 480, [], None, None, None, None
    transpose = False
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
        if a == "--labels":
            labels = argv[i + 1]
            i += 2
            continue
        if a == "--corner":
            corner = argv[i + 1]
            i += 2
            continue
        if a == "--anchor":
            anchor = argv[i + 1]
            i += 2
            continue
        if a == "--transpose":
            transpose = True
            i += 1
            continue
        if out is None:
            out = a
        else:
            label, folder = a.split("=", 1)
            rows.append((label, load_frames(folder), folder))
        i += 1
    if out is None or not rows:
        sys.exit(__doc__)
    heads = column_labels(times, labels)
    if anchor and labels is None:
        heads = [f"{anchor} {t:+.1f} s" for t in times]
    first = Image.open(rows[0][1][0][0])
    w = round(first.width * height / first.height)
    try:
        font = ImageFont.truetype(FONT, 28)
        small = ImageFont.truetype(FONT, 20)
    except OSError:
        font = small = ImageFont.load_default()
    if transpose:
        # folders across the top (wrapped to the frame width), times down the left
        label_w = label_width(heads, font)
        head_h = 44 + 34 * (max(len(wrap(label, font, w - 16)) for label, _, _ in rows) - 1)
        sheet = Image.new("RGB", (label_w + (w + GAP) * len(rows) - GAP, head_h + height * len(times)), (18, 22, 30))
    else:
        label_w, head_h = label_width([label for label, _, _ in rows], font), 44
        sheet = Image.new("RGB", (label_w + w * len(times), head_h + height * len(rows)), (18, 22, 30))
    draw = ImageDraw.Draw(sheet)
    for c, head in enumerate(heads):
        if transpose:
            draw.text((12, head_h + c * height + height // 2 - 17), head, fill=(240, 240, 240), font=font)
        else:
            draw.text((label_w + c * w + 10, 8), head, fill=(240, 240, 240), font=font)
    if corner:
        draw.text((12, 12), corner, fill=(255, 200, 60), font=small)
    for r, (label, frames, folder) in enumerate(rows):
        if transpose:
            for k, line in enumerate(wrap(label, font, w - 16)):
                draw.text((label_w + r * (w + GAP) + 10, 8 + k * 34), line, fill=(255, 200, 60), font=font)
        else:
            y = head_h + r * height
            lines = wrap(label, font, label_w - LABEL_PAD)
            line_h = 34
            for k, line in enumerate(lines):
                draw.text((12, y + height // 2 - line_h * len(lines) // 2 + k * line_h), line, fill=(255, 200, 60), font=font)
        offset = 0.0
        if anchor:
            import json
            events = json.load(open(os.path.join(folder, "events.json")))
            if anchor not in events:
                sys.exit(f"{folder}: no '{anchor}' marker in events.json")
            offset = float(events[anchor])
        for c, t in enumerate(times):
            img = Image.open(nearest(frames, t + offset)).convert("RGB").resize((w, height), Image.LANCZOS)
            sheet.paste(img, cell_origin(r, c, label_w, head_h, w, height, transpose))
    sheet.save(out, quality=85)
    shape = f"{len(rows)} columns × {len(times)} times" if transpose else f"{len(rows)} rows × {len(times)} times"
    print(f"{out}: {shape}, {sheet.width}×{sheet.height}")


if __name__ == "__main__":
    main(sys.argv[1:])
