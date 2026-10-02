#!/usr/bin/env python3
"""Recordings one after another in one voiced video (spec 2026-10-01 §1.4: every scenario as Pota, then 1, then 2).

Usage: uv run --with pillow --with numpy Tools/potalook/seq_video.py OUT.mp4 "TITLE|LOOK=DIR" ["TITLE|LOOK=DIR" ...]
       [--card 0.7] [--silent] [--speed S] [--crf 28] [--sync-sheet PREFIX]
Each DIR is an ai_colorhunt_record folder: frames f_NNNN_tS.SSS.png named by the recording clock, events.json with "sounds"
[{id, clip, t, volume, pitch, voice}], "meta" and sounds/<clip>.wav. Each segment opens with a title card (TITLE, LOOK under it) and
shows its frames under a label strip, resampled to 30 fps by the clock in their names (side_video.resample), so slow motion and
hit-stops play as felt; a close-up (meta.speed 0.3, or --speed) is played back that much slower. The sounds are mixed in numpy into one
48 kHz stereo track at their logged times — pitch by resampling, volume, PotaSounds' eight voices (a sound stops when its voice is
taken) — and a segment's sounds fade out during the next card; ffmpeg only encodes and muxes.
Why not ffmpeg adelay + amix: a video carries a few hundred timed sounds; amix scales every input by the input count unless told not to,
pitch needs asetrate + aresample per input, and a graph with hundreds of inputs is slow to build and hard to debug.
--sync-sheet PREFIX writes PREFIX-<NN>-<scenario>-<look>.jpg per segment: for each special sound, the frames one tick before, at and after.
"""
import argparse
import json
import os
import re
import shutil
import sys
import tempfile
import wave
from dataclasses import dataclass, field

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import compare  # noqa: E402
import frame_sheet  # noqa: E402
import side_video  # noqa: E402

FFMPEG = side_video.FFMPEG
FPS = side_video.FPS
STRIP_H = side_video.HEAD_H
BG = side_video.BG
GOLD = side_video.GOLD
RATE = 48000
PEAK = 0.95            # the mix is lowered as a whole when its sum would pass this
VOICE_FADE = 0.005     # a taken voice stops at once (a 5-ms ramp against the click)
WINDOW_FADE = 0.25     # a segment's sounds fade out during the next title card
# routine sounds the sync sheet leaves out: taps, ball hits, normal bursts, combos, drops, rotations, stickers, round ends
SYNC_SKIP = {"dokun", "kuleAt", "carpma", "kirildi", "kombo", "dustu", "dondur", "eksi", "topla", "tamam", "kazandi", "kaybetti"}


@dataclass
class Segment:
    title: str
    look: str
    folder: str
    speed: float = 1.0                           # playback speed: 0.3 plays a close-up's 0.01 s of clock per video frame
    ticks: list = field(default_factory=list)    # one frame path per 1/FPS s of video
    sounds: list = field(default_factory=list)   # events.json "sounds"
    meta: dict = field(default_factory=dict)     # events.json "meta"

    @property
    def label(self):
        return f"{self.title} · {self.look}" if self.look else self.title


@dataclass
class Slot:
    card: float    # the title card starts
    body: float    # the first frame
    end: float     # after the last frame


@dataclass
class MixEvent:
    at: float      # output seconds
    path: str      # the WAV
    volume: float
    pitch: float
    stop: float    # output seconds where it is cut (its voice is taken, or its segment's window ends)
    fade: float    # fade-out length when cut


def even(n):
    return n - n % 2


def parse_segments(args):
    """["Bomba|Pota=Temp/record/a", …] → [Segment("Bomba", "Pota", "Temp/record/a"), …] in order."""
    out = []
    for label, folder in compare.parse_rows(args):
        title, _, look = label.partition("|")
        out.append(Segment(title.strip(), look.strip(), folder))
    return out


def ticks(folder, fps=FPS, speed=1.0):
    """One frame per video tick: every speed/fps s of the recording clock."""
    return side_video.resample(frame_sheet.load_frames(folder), fps / speed)


def load(seg, speed=None):
    """Reads the segment's events.json; speed: given, else the recording's meta.speed, else 1."""
    path = os.path.join(seg.folder, "events.json")
    events = json.load(open(path)) if os.path.exists(path) else {}
    seg.sounds = events.get("sounds", [])
    seg.meta = events.get("meta", {})
    seg.speed = float(speed or seg.meta.get("speed") or 1.0)
    seg.ticks = ticks(seg.folder, FPS, seg.speed)
    return seg


def layout(durations, card):
    """Each segment: `card` s of title card, then its frames. → ([Slot…], total seconds)."""
    slots, t = [], 0.0
    for d in durations:
        slots.append(Slot(t, t + card, t + card + d))
        t += card + d
    return slots, t


def mix_plan(segments, slots, total):
    """Every logged sound at its segment's first frame + its time (÷ speed); cut where its voice plays the next sound, else at the next card's end."""
    events = []
    for k, (seg, slot) in enumerate(zip(segments, slots)):
        window = slots[k + 1].body if k + 1 < len(slots) else total
        ordered = sorted(seg.sounds, key=lambda s: float(s["t"]))
        for i, s in enumerate(ordered):
            stop, fade = window, WINDOW_FADE
            voice = s.get("voice", -1)
            if voice is not None and voice >= 0:
                for later in ordered[i + 1:]:
                    if later.get("voice", -1) == voice:
                        if slot.body + float(later["t"]) / seg.speed < stop:
                            stop, fade = slot.body + float(later["t"]) / seg.speed, VOICE_FADE
                        break
            events.append(MixEvent(slot.body + float(s["t"]) / seg.speed, os.path.join(seg.folder, "sounds", s["clip"] + ".wav"),
                                   float(s.get("volume", 1.0)), float(s.get("pitch", 1.0)), stop, fade))
    return events


def read_wav(path):
    with wave.open(path, "rb") as w:
        channels, width, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        if width != 2:
            raise ValueError(f"{path}: {8 * width}-bit, expected 16-bit PCM")
        data = np.frombuffer(w.readframes(n), dtype="<i2").astype(np.float32) / 32768.0
    return data.reshape(-1, channels), rate


def write_wav(path, samples, rate=RATE):
    pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).round().astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(samples.shape[1])
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())


def render_mix(events, total, rate=RATE):
    """One stereo float track of `total` s → (track, gain); gain < 1 when the sum had to come down to PEAK."""
    n_total = int(round(total * rate))
    out = np.zeros((n_total, 2), np.float32)
    cache = {}
    for e in events:
        if not os.path.exists(e.path):
            print(f"missing sound {e.path}: skipped")
            continue
        if e.path not in cache:
            cache[e.path] = read_wav(e.path)
        data, sr = cache[e.path]
        if data.shape[1] == 1:
            data = np.repeat(data, 2, axis=1)
        elif data.shape[1] > 2:
            data = data[:, :2]
        if len(data) == 0:
            continue
        step = e.pitch * sr / rate                      # AudioSource.pitch p plays the clip p× faster
        count = int((len(data) - 1) / step) + 1
        start = max(0, int(round(e.at * rate)))
        stop = min(int(round(e.stop * rate)), n_total)
        length = min(count, stop - start)
        if length <= 0:
            continue
        pos = np.arange(length) * step
        src = np.arange(len(data))
        chunk = np.stack([np.interp(pos, src, data[:, c]) for c in (0, 1)], axis=1) * e.volume
        if length < count:                              # cut: fade instead of clicking
            f = min(length, max(1, int(round(e.fade * rate))))
            chunk[-f:] *= np.linspace(1.0, 0.0, f)[:, None]
        out[start:start + length] += chunk
    peak = float(np.abs(out).max()) if n_total else 0.0
    gain = min(1.0, PEAK / peak) if peak > 0 else 1.0
    return out * gain, gain


def font(size):
    return ImageFont.truetype(side_video.FONT, size)


def strip(width, text):
    img = Image.new("RGB", (width, STRIP_H), BG)
    d = ImageDraw.Draw(img)
    f = font(34)
    d.text(((width - d.textlength(text, font=f)) / 2, 10), text, font=f, fill=GOLD)
    return img


def card(width, height, title, look):
    img = Image.new("RGB", (width, height), BG)
    d = ImageDraw.Draw(img)
    y = height * 0.38
    for text, f, color, dy in ((title, font(96), GOLD, 0), (look, font(52), (240, 240, 240), 120)):
        if text:
            d.text(((width - d.textlength(text, font=f)) / 2, y + dy), text, font=f, fill=color)
    return img


def video_frames(segments, size, card_ticks):
    """Each segment: its title card for card_ticks frames, then every tick's frame under the label strip."""
    width, height = size
    body = height - STRIP_H
    for seg in segments:
        title_card = card(width, height, seg.title, seg.look)
        for _ in range(card_ticks):
            yield title_card
        head = strip(width, seg.label)
        last, frame = None, None
        for path in seg.ticks:
            if path != last:
                frame = Image.new("RGB", size, BG)
                frame.paste(head, (0, 0))
                frame.paste(side_video.scaled(path, width, body), (0, STRIP_H))
                last = path
            yield frame


def slug(text):
    table = str.maketrans("çğıöşüÇĞİÖŞÜ", "cgiosuCGIOSU")
    return re.sub(r"[^a-z0-9]+", "-", text.translate(table).lower()).strip("-") or "segment"


def sync_rows(segments):
    """[(segment, sound, [tick before, tick at, tick after])] for every sound that is not routine."""
    rows = []
    for seg in segments:
        n = len(seg.ticks)
        for s in seg.sounds:
            if s.get("id") in SYNC_SKIP or n == 0:
                continue
            i = side_video.index_at(float(s["t"]) / seg.speed, n)
            rows.append((seg, s, [seg.ticks[max(0, i - 1)], seg.ticks[i], seg.ticks[min(n - 1, i + 1)]]))
    return rows


def sync_sheet(rows, out, height=320):
    first = Image.open(rows[0][2][0])
    w = round(first.width * height / first.height)
    label_w = 420
    sheet = Image.new("RGB", (label_w + 3 * w, 44 + height * len(rows)), BG)
    d = ImageDraw.Draw(sheet)
    f = font(26)
    for c, name in enumerate(("-1 kare", "ses anı", "+1 kare")):
        d.text((label_w + c * w + 10, 8), name, font=f, fill=(240, 240, 240))
    for r, (seg, s, paths) in enumerate(rows):
        y = 44 + r * height
        d.text((12, y + height // 2 - 34), seg.label, font=f, fill=GOLD)
        d.text((12, y + height // 2 + 2), f"{s['id']} @ {float(s['t']):.3f} s", font=f, fill=(240, 240, 240))
        for c, p in enumerate(paths):
            sheet.paste(side_video.scaled(p, w, height), (label_w + c * w, y))
    sheet.save(out, quality=85)


def sync_sheets(segments, prefix):
    written = []
    for k, seg in enumerate(segments):
        rows = sync_rows([seg])
        if not rows:
            continue
        path = f"{prefix}-{k:02d}-{seg.meta.get('scenario') or slug(seg.title)}-{slug(seg.look)}.jpg"
        sync_sheet(rows, path)
        written.append(path)
    return written


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("segments", nargs="+", metavar="TITLE|LOOK=DIR")
    ap.add_argument("--card", type=float, default=0.7, help="title card seconds before each segment (0: none)")
    ap.add_argument("--silent", action="store_true")
    ap.add_argument("--speed", type=float, help="playback speed of every segment (default: each recording's meta.speed, else 1)")
    ap.add_argument("--crf", type=int, default=28)
    ap.add_argument("--sync-sheet", metavar="PREFIX")
    a = ap.parse_args(argv)
    segments = [load(s, a.speed) for s in parse_segments(a.segments)]
    first = Image.open(segments[0].ticks[0])
    size = (even(first.width), even(first.height + STRIP_H))
    card_ticks = max(0, int(round(a.card * FPS)))
    slots, total = layout([len(s.ticks) / FPS for s in segments], card_ticks / FPS)
    work = tempfile.mkdtemp(prefix="seq-")
    try:
        audio = None
        if not a.silent:
            plan = mix_plan(segments, slots, total)
            track, gain = render_mix(plan, total)
            audio = os.path.join(work, "mix.wav")
            write_wav(audio, track)
            print(f"mix: {len(plan)} sounds over {total:.2f} s, gain {gain:.2f}")
        side_video.encode(video_frames(segments, size, card_ticks), size, a.out, audio=audio, crf=a.crf)
        for seg, slot in zip(segments, slots):
            print(f"{slot.body:7.2f} s  {seg.label}  ({len(seg.ticks)} frames, {len(seg.sounds)} sounds)")
        if a.sync_sheet:
            for path in sync_sheets(segments, a.sync_sheet):
                print(path)
        print(a.out)
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main(sys.argv[1:])
