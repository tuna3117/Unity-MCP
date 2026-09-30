#!/usr/bin/env python3
"""Pota | Unity at matched moments (spec 2026-09-30 §2.1).

Usage: uv run --with pillow Tools/potalook/compare.py OUT_PREFIX POTA_VIDEO POTA_EVENTS UNITY_FRAMES UNITY_EVENTS
For each anchor in both events files ("tap", "rotate", "win") writes OUT_PREFIX-<anchor>.jpg: two rows (Pota, Unity), one column per
moment (seconds after the anchor; the sheet labels them +1 s). Pota frames are extracted from the video at 30 fps with ffmpeg.
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
# idle, salvo mid, tapped block's swell peak (0.5 + 0.11·0.55), burst +0.08 / +0.16, a landing, the sticker home (peak + 0.55)
MOMENTS = {
    "tap": [-0.3, 0.25, 0.5605, 0.64, 0.72, 0.9, 1.11],
    "rotate": [0.0, 0.21, 0.33, 0.5],
    "win": [0.56, 0.9, 1.5],
}
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


def main(argv):
    if len(argv) != 5:
        sys.exit(__doc__)
    prefix, video, pota_events, unity_dir, unity_events = argv
    pe, ue = json.load(open(pota_events)), json.load(open(unity_events))
    work = tempfile.mkdtemp(prefix="potalook-")
    try:
        pota = extract(video, work)
        synced = marker_events(pota, sorted(pe, key=pe.get))
        if synced:
            print(f"pota anchors from the marker: {synced} (events file: {pe})")
            pe = synced
        else:
            print("pota marker not found for every event: using the events file (video start latency not corrected)")
        unity = frame_sheet.load_frames(unity_dir)
        for anchor, times in MOMENTS.items():
            if anchor not in pe or anchor not in ue:
                continue
            a = aligned(pota, pe[anchor], os.path.join(work, f"pota-{anchor}"))
            b = aligned(unity, ue[anchor], os.path.join(work, f"unity-{anchor}"))
            frame_sheet.main([f"{prefix}-{anchor}.jpg", f"Pota={a}", f"Unity={b}",
                              "--times", ",".join(f"{t + WINDOW:.4f}" for t in times), "--height", "640"])
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main(sys.argv[1:])
