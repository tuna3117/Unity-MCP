import importlib.util
import json
import os
import subprocess
import sys

import numpy as np
import pytest
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
spec = importlib.util.spec_from_file_location("seq_video", os.path.join(HERE, "seq_video.py"))
seq = importlib.util.module_from_spec(spec)
spec.loader.exec_module(seq)


def frames(folder, times, size=(4, 8)):
    os.makedirs(folder, exist_ok=True)
    for i, t in enumerate(times):
        Image.new("RGB", size, (i * 20, 0, 0)).save(os.path.join(folder, f"f_{i:04d}_t{t:.3f}.png"))
    return str(folder)


def segment(folder, sounds, n_ticks):
    s = seq.Segment("Bomba", "Pota", folder)
    s.ticks = ["x"] * n_ticks
    s.sounds = sounds
    return s


def sound(clip, t, voice, volume=1.0, pitch=1.0, sid=None):
    return {"id": sid or clip, "clip": clip, "t": t, "volume": volume, "pitch": pitch, "voice": voice}


def tone(path, value, n, rate=48000, channels=1):
    seq.write_wav(str(path), np.full((n, channels), value, np.float32), rate)
    return str(path)


def test_segments_parse_title_look_and_folder_in_order():
    got = seq.parse_segments(["Bomba|Pota=Temp/record/a", "Bomba|1 · Royal=Temp/record/b", "Zincir=c"])
    assert [(s.title, s.look, s.folder) for s in got] == [
        ("Bomba", "Pota", "Temp/record/a"), ("Bomba", "1 · Royal", "Temp/record/b"), ("Zincir", "", "c")]
    assert got[1].label == "Bomba · 1 · Royal"
    assert got[2].label == "Zincir"


def test_a_segment_without_a_folder_is_refused():
    with pytest.raises(ValueError):
        seq.parse_segments(["Bomba|Pota"])


def test_layout_puts_a_card_before_every_segment_in_order():
    slots, total = seq.layout([1.0, 1.5, 0.5], 0.5)
    assert [(s.card, s.body, s.end) for s in slots] == [
        (pytest.approx(0.0), pytest.approx(0.5), pytest.approx(1.5)),
        (pytest.approx(1.5), pytest.approx(2.0), pytest.approx(3.5)),
        (pytest.approx(3.5), pytest.approx(4.0), pytest.approx(4.5))]
    assert total == pytest.approx(4.5)


def test_ticks_resample_the_recording_by_the_clock_in_the_frame_names(tmp_path):
    # hit-stops crowd frames together (0.020, 0.021, 0.022 …); the video takes one per 1/30 s of that clock
    folder = frames(tmp_path / "rec", [0.0, 0.02, 0.021, 0.022, 0.1])
    got = [os.path.basename(p) for p in seq.ticks(folder)]
    assert got == ["f_0000_t0.000.png", "f_0003_t0.022.png", "f_0003_t0.022.png", "f_0004_t0.100.png"]


def test_a_slow_segment_plays_its_clock_at_its_speed(tmp_path):
    # a close-up steps 0.006 s at a time and says speed 0.3 in its meta: one video tick per 0.01 s of its clock
    folder = frames(tmp_path / "cu", [0.0, 0.006, 0.012, 0.018, 0.024, 0.030])
    with open(os.path.join(folder, "events.json"), "w") as f:
        json.dump({"meta": {"speed": 0.3}, "sounds": [sound("bomba", 0.03, 0)]}, f)
    seg = seq.load(seq.parse_segments([f"Bomba|1={folder}"])[0])
    assert seg.speed == pytest.approx(0.3)
    assert [os.path.basename(p) for p in seg.ticks] == [
        "f_0000_t0.000.png", "f_0001_t0.006.png", "f_0003_t0.018.png", "f_0005_t0.030.png"]
    slots, total = seq.layout([len(seg.ticks) / seq.FPS], 0.0)
    assert seq.mix_plan([seg], slots, total)[0].at == pytest.approx(0.1), "0.03 s of its clock is 0.1 s of video"
    assert seq.load(seq.parse_segments([f"Bomba|1={folder}"])[0], speed=1.0).speed == 1.0, "--speed overrides the meta"


def test_mix_plan_places_each_sound_at_its_segment_body_plus_its_time():
    a = segment("A", [sound("kirildi", 0.1, 0)], 30)
    b = segment("B", [sound("bomba", 0.25, 1, volume=0.8, pitch=1.1, sid="patlama")], 45)
    slots, total = seq.layout([1.0, 1.5], 0.5)
    plan = seq.mix_plan([a, b], slots, total)
    assert [e.path for e in plan] == [os.path.join("A", "sounds", "kirildi.wav"), os.path.join("B", "sounds", "bomba.wav")]
    assert plan[0].at == pytest.approx(0.6)
    assert plan[1].at == pytest.approx(2.25)
    assert (plan[1].volume, plan[1].pitch) == (pytest.approx(0.8), pytest.approx(1.1))


def test_mix_plan_cuts_a_taken_voice_and_ends_a_segment_in_the_next_card():
    a = segment("A", [sound("kombo", 0.1, 2), sound("kirildi", 0.2, 3), sound("dustu", 0.4, 2)], 30)
    b = segment("B", [sound("bomba", 0.0, 0)], 15)
    slots, total = seq.layout([1.0, 0.5], 0.5)
    plan = {(os.path.basename(e.path), round(e.at, 3)): e for e in seq.mix_plan([a, b], slots, total)}
    kombo = plan[("kombo.wav", 0.6)]
    assert kombo.stop == pytest.approx(0.9) and kombo.fade == seq.VOICE_FADE, "voice 2 plays dustu at 0.4 s"
    kirildi = plan[("kirildi.wav", 0.7)]
    assert kirildi.stop == pytest.approx(2.0) and kirildi.fade == seq.WINDOW_FADE, "A's sounds end as B's frames start"
    assert plan[("bomba.wav", 2.0)].stop == pytest.approx(total), "the last segment runs to the end"


def test_render_mix_honours_time_pitch_and_volume(tmp_path):
    path = tone(tmp_path / "a.wav", 0.5, 4800)                            # 0.1 s of 0.5, mono 48 kHz
    track, gain = seq.render_mix([seq.MixEvent(0.1, path, 0.5, 2.0, 1.0, seq.WINDOW_FADE)], 0.5)
    assert track.shape == (24000, 2)
    assert gain == 1.0
    assert np.allclose(track[:4800], 0.0)
    assert np.allclose(track[4800:7200], 0.25, atol=1e-3), "pitch 2 plays the 0.1 s clip in 0.05 s, volume 0.5"
    assert np.allclose(track[7200:], 0.0)


def test_render_mix_resamples_other_rates_and_fades_a_cut(tmp_path):
    path = tone(tmp_path / "b.wav", 0.4, 44100, rate=44100, channels=2)  # 1 s, stereo 44.1 kHz
    track, _ = seq.render_mix([seq.MixEvent(0.0, path, 1.0, 1.0, 0.5, 0.1)], 1.0)
    assert np.allclose(track[:19200], 0.4, atol=1e-3), "44.1 kHz plays at its own speed on the 48 kHz track"
    assert track[23999, 0] == pytest.approx(0.0, abs=1e-3), "cut at 0.5 s through a 0.1 s fade"
    assert np.allclose(track[24000:], 0.0)


def test_render_mix_lowers_a_clipping_sum_to_the_peak(tmp_path):
    path = tone(tmp_path / "c.wav", 0.9, 4800)
    events = [seq.MixEvent(0.0, path, 1.0, 1.0, 1.0, seq.WINDOW_FADE)] * 2
    track, gain = seq.render_mix(events, 0.2)
    assert gain == pytest.approx(seq.PEAK / 1.8, rel=1e-3)
    assert np.abs(track).max() == pytest.approx(seq.PEAK, rel=1e-3)


def test_a_missing_wav_is_skipped(tmp_path, capsys):
    track, gain = seq.render_mix([seq.MixEvent(0.0, str(tmp_path / "nope.wav"), 1.0, 1.0, 1.0, 0.1)], 0.1)
    assert np.allclose(track, 0.0) and gain == 1.0
    assert "missing" in capsys.readouterr().out


def test_label_strip_card_and_even_sizes():
    assert seq.strip(540, "Bomba · 1 · Royal").size == (540, seq.STRIP_H)
    assert seq.card(540, 1224, "Bomba", "1 · Royal").size == (540, 1224)
    assert (seq.even(1169), seq.even(1224)) == (1168, 1224)


def test_sync_rows_take_the_frames_around_each_special_sound():
    s = seq.Segment("Bomba", "Pota", "A")
    s.ticks = [f"t{i}" for i in range(10)]
    s.sounds = [sound("dustu", 0.1, 0), sound("bomba", 0.1, 1, sid="patlama")]
    rows = seq.sync_rows([s])
    assert len(rows) == 1, "drops are routine"
    assert rows[0][2] == ["t2", "t3", "t4"]


def test_video_frames_are_the_card_then_the_strip_over_each_tick(tmp_path):
    a = seq.load(seq.parse_segments([f"Bomba|Pota={frames(tmp_path / 'a', [0.0, 0.05, 0.1])}"])[0])
    imgs = list(seq.video_frames([a], (4, 8 + seq.STRIP_H), 2))
    assert len(imgs) == 2 + len(a.ticks) == 6
    assert all(im.size == (4, 8 + seq.STRIP_H) for im in imgs)
    assert imgs[2].getpixel((1, seq.STRIP_H + 2))[0] == 0
    assert imgs[-1].getpixel((1, seq.STRIP_H + 2))[0] == 40


@pytest.mark.skipif(not os.path.exists(seq.FFMPEG), reason="ffmpeg missing")
def test_main_builds_a_voiced_video_and_a_sync_sheet(tmp_path):
    rec = frames(tmp_path / "rec", [0.0, 0.1, 0.2, 0.3], size=(32, 32))
    os.makedirs(os.path.join(rec, "sounds"))
    tone(os.path.join(rec, "sounds", "bomba.wav"), 0.3, 4800)
    with open(os.path.join(rec, "events.json"), "w") as f:
        json.dump({"tap": 0.1, "meta": {"scenario": "bomb"}, "sounds": [sound("bomba", 0.1, 0, sid="patlama")]}, f)
    out = str(tmp_path / "v.mp4")
    seq.main([out, f"Bomba|Pota={rec}", "--card", "0.2", "--sync-sheet", str(tmp_path / "sync")])
    probe = subprocess.run([os.path.join(os.path.dirname(seq.FFMPEG), "ffprobe"), "-v", "error", "-show_entries",
                            "stream=codec_type", "-of", "csv=p=0", out], capture_output=True, text=True, check=True)
    assert sorted(probe.stdout.split()) == ["audio", "video"]
    assert os.path.exists(str(tmp_path / "sync-00-bomb-pota.jpg"))
