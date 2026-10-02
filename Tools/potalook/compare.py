#!/usr/bin/env python3
"""Pota | Unity looks at matched moments (spec 2026-09-30 studio polish §1.2).

Usage: uv run --with pillow Tools/potalook/compare.py OUT_PREFIX POTA_VIDEO POTA_EVENTS LABEL=UNITY_DIR [LABEL=UNITY_DIR ...]
Each UNITY_DIR holds ai_colorhunt_record frames and events.json. For each moment whose anchor is in every events file ("tap", "chain",
"rotate", "win") writes OUT_PREFIX-<moment>.jpg: one row for Pota, one per Unity dir, one column per time after the anchor (the
sheet labels them +1 s). Pota frames are extracted from the video at 30 fps with ffmpeg; Pota's anchors come from its sync marker.

Special balls (spec 2026-10-01): uv run --with pillow Tools/potalook/compare.py specials OUT_PREFIX SCENARIO LABEL=DIR [LABEL=DIR ...]
writes OUT_PREFIX-SCENARIO.jpg: one row per Unity dir (the Pota look is a Unity row; no three.js video), one column per SPECIAL_MOMENTS
time after the scenario's anchor marker (birth: born, golden: land, the rest: tap; the sheet labels them +1 s).
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import frame_sheet  # noqa: E402  (Tools/frame_sheet.py)

FFMPEG = "/opt/homebrew/bin/ffmpeg"
# idle, salvo mid, tapped block's swell peak (0.5 + 0.11·0.55), burst +0.08 / +0.16, a landing, the sticker home (peak + 0.55);
# chain: the steps after the tap (the tapped block is the longest chain's, "tap auto")
MOMENTS = {
    "tap": [-0.3, 0.25, 0.5605, 0.64, 0.72, 0.9, 1.11],
    "chain": [0.72, 0.87, 1.02, 1.17, 1.32, 1.5],
    "rotate": [0.0, 0.21, 0.33, 0.5],
    "win": [0.56, 0.9, 1.5],
}
ANCHORS = {"chain": "tap"}
# special balls (spec 2026-10-01 §2): (anchor marker, seconds after it). Line/bomb: the squash or wind-up, the sweep or the blast, the
# drops; chain: the three fires within 0.6 s; birth: the gather (Royal/Court) into the pop; golden: the gate, the arc, the landing.
SPECIAL_MOMENTS = {
    "birth": ("born", [-0.25, -0.12, 0.0, 0.06, 0.15, 0.3, 0.5]),
    "line": ("tap", [0.0, 0.08, 0.17, 0.26, 0.36, 0.5, 0.75]),
    "bomb": ("tap", [0.0, 0.08, 0.17, 0.26, 0.36, 0.5, 0.75]),
    "chain": ("tap", [0.0, 0.1, 0.2, 0.3, 0.42, 0.55, 0.8]),
    "golden": ("land", [-0.8, -0.5, -0.25, 0.0, 0.1, 0.25, 0.5]),
}


def special_moment(scenario):
    """(anchor, times) of a special-ball scenario; ValueError for any other name."""
    if scenario not in SPECIAL_MOMENTS:
        raise ValueError(f"unknown scenario '{scenario}' ({', '.join(SPECIAL_MOMENTS)})")
    return SPECIAL_MOMENTS[scenario]


def anchor_of(moment):
    return ANCHORS.get(moment, moment)


def parse_rows(args):
    """["A (Candy)=Temp/record/a", …] → [("A (Candy)", "Temp/record/a"), …]."""
    rows = []
    for arg in args:
        label, sep, folder = arg.partition("=")
        if not sep or not label or not folder:
            raise ValueError(f"expected LABEL=DIR, got '{arg}'")
        rows.append((label, folder))
    return rows
WINDOW = 1.0   # frames from 1 s before the anchor; names carry t − anchor + 1 so frame_sheet's regex sees no minus sign


def shifted_name(index, t, anchor):
    s = t - anchor + WINDOW
    return None if s < 0 else f"f_{index:05d}_t{s:.3f}.png"


def is_marker(rgb):
    """record-pota.mjs flashes a magenta 10 px square in the top-left corner at every event."""
    r, g, b = rgb[:3]
    return r > 200 and g < 90 and b > 200


def onset_times(flags, fps):
    """Times of the frames where the marker turns on (the first frame of each run)."""
    return [i / fps for i, f in enumerate(flags) if f and (i == 0 or not flags[i - 1])]


def marker_events(frames, keys):
    """Pota's anchors from the video itself: marker onsets in order, matched to the events file's keys in time order."""
    from PIL import Image
    flags = [is_marker(Image.open(p).convert("RGB").getpixel((4, 4))) for p, _ in frames]
    onsets = onset_times(flags, 30)
    return dict(zip(keys, onsets)) if len(onsets) == len(keys) else None


def extract(video, folder, fps=30):
    raw = os.path.join(folder, "raw")
    os.makedirs(raw)
    subprocess.run([FFMPEG, "-loglevel", "error", "-i", video, "-vf", f"fps={fps}", os.path.join(raw, "r_%05d.png")], check=True)
    frames = []
    for i, n in enumerate(sorted(os.listdir(raw))):
        frames.append((os.path.join(raw, n), i / fps))
    return frames


def aligned(frames, anchor, folder):
    os.makedirs(folder)
    for i, (path, t) in enumerate(frames):
        name = shifted_name(i, t, anchor)
        if name:
            os.symlink(os.path.abspath(path), os.path.join(folder, name))
    return folder


def pota_anchor_times(video, events_path, work):
    """Pota's frames (30 fps) and its anchor times: from the sync marker when every event has one, else the events file."""
    events = json.load(open(events_path))
    frames = extract(video, work)
    synced = marker_events(frames, sorted(events, key=events.get))
    if synced:
        print(f"pota anchors from the marker: {synced} (events file: {events})")
        return frames, synced
    print("pota marker not found for every event: using the events file (video start latency not corrected)")
    return frames, events


def specials_main(argv):
    """compare.py specials OUT_PREFIX SCENARIO LABEL=DIR [LABEL=DIR ...] → OUT_PREFIX-SCENARIO.jpg (rows = dirs, columns = moments)."""
    if len(argv) < 3:
        sys.exit(specials_main.__doc__)
    prefix, scenario = argv[:2]
    anchor, times = special_moment(scenario)
    rows = parse_rows(argv[2:])
    work = tempfile.mkdtemp(prefix="specials-")
    try:
        columns = []
        for i, (label, folder) in enumerate(rows):
            ev = json.load(open(os.path.join(folder, "events.json")))
            if anchor not in ev:
                marks = sorted(k for k, v in ev.items() if isinstance(v, (int, float)))
                raise ValueError(f"{folder}: no '{anchor}' marker in events.json (has {marks})")
            columns.append(f"{label}={aligned(frame_sheet.load_frames(folder), ev[anchor], os.path.join(work, f'row{i}'))}")
        out = f"{prefix}-{scenario}.jpg"
        frame_sheet.main([out, *columns, "--times", ",".join(f"{t + WINDOW:.4f}" for t in times), "--height", "400"])
        return out
    finally:
        shutil.rmtree(work, ignore_errors=True)


def main(argv):
    if argv and argv[0] == "specials":
        specials_main(argv[1:])
        return
    if len(argv) < 4:
        sys.exit(__doc__)
    prefix, video, pota_events = argv[:3]
    rows = parse_rows(argv[3:])
    work = tempfile.mkdtemp(prefix="potalook-")
    try:
        pota, pe = pota_anchor_times(video, pota_events, work)
        unity = [(label, frame_sheet.load_frames(folder), json.load(open(os.path.join(folder, "events.json"))))
                 for label, folder in rows]
        for moment, times in MOMENTS.items():
            anchor = anchor_of(moment)
            if anchor not in pe or any(anchor not in ev for _, _, ev in unity):
                continue
            columns = [f"Pota={aligned(pota, pe[anchor], os.path.join(work, f'pota-{moment}'))}"]
            for i, (label, frames, ev) in enumerate(unity):
                columns.append(f"{label}={aligned(frames, ev[anchor], os.path.join(work, f'unity{i}-{moment}'))}")
            frame_sheet.main([f"{prefix}-{moment}.jpg", *columns,
                              "--times", ",".join(f"{t + WINDOW:.4f}" for t in times), "--height", "640"])
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main(sys.argv[1:])
