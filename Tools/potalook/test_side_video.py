import importlib.util
import json
import os
import sys

import pytest
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
spec = importlib.util.spec_from_file_location("side_video", os.path.join(HERE, "side_video.py"))
side = importlib.util.module_from_spec(spec)
spec.loader.exec_module(side)


def test_segments_parse():
    assert side.parse_segments("tap:-1:3.4,rotate:-0.5:2.5") == [("tap", -1.0, 3.4), ("rotate", -0.5, 2.5)]


def test_trims_are_windows_around_each_event():
    got = side.trims({"tap": 1.0, "rotate": 5.1}, [("tap", -1.0, 3.4), ("rotate", -0.5, 2.5)])
    assert got == [(pytest.approx(0.0), pytest.approx(4.4)), (pytest.approx(4.6), pytest.approx(7.6))]


def test_a_window_before_the_recording_is_refused():
    with pytest.raises(ValueError):
        side.trims({"tap": 0.5}, [("tap", -1.0, 1.0)])


def test_filter_stacks_every_column_per_segment_and_joins_them():
    f = side.build_filter([[(0, 1), (2, 3)]] * 3, 540, 960)
    assert "hstack=inputs=3[row0]" in f
    assert "hstack=inputs=3[row1]" in f
    assert "concat=n=2:v=1[body]" in f
    assert "[3:v]" in f, "the label strip is the last input"


def test_float_noise_at_the_start_is_clamped_to_zero():
    assert side.trims({"tap": 0.999999}, [("tap", -1.0, 1.0)]) == [(0.0, pytest.approx(1.999999))]


def test_resample_holds_the_last_frame_at_each_tick():
    # hit-stops crowd frames together: 0.02, 0.021, 0.022 …; the video needs one frame per 1/30 s of game time
    frames = [("a", 0.0), ("b", 0.02), ("c", 0.021), ("d", 0.022), ("e", 0.1)]
    assert side.resample(frames, 30) == ["a", "d", "d", "e"]


def test_groups_parse_a_title_and_its_columns():
    assert side.parse_groups([["Bomba", "Pota=a", "1 · Royal=b"]]) == [("Bomba", [("Pota", "a"), ("1 · Royal", "b")])]
    with pytest.raises(ValueError):
        side.parse_groups([["Bomba"]])


def test_group_window_runs_from_before_the_anchors_to_the_longest_column():
    assert side.window([1.0, 1.5], [3.0, 2.8], 0.6) == (-0.6, pytest.approx(2.0))


def test_index_at_clamps_to_the_recording():
    assert side.index_at(1.234, 50) == 37
    assert side.index_at(-0.1, 50) == 0
    assert side.index_at(10.0, 50) == 49


def test_group_frames_align_every_column_at_its_own_anchor(tmp_path):
    def rec(name, anchor):
        folder = tmp_path / name
        folder.mkdir()
        for i in range(11):                                         # frames every 0.1 s, red = 20 per frame
            Image.new("RGB", (4, 8), (i * 20, 0, 0)).save(folder / f"f_{i:04d}_t{i / 10:.3f}.png")
        (folder / "events.json").write_text(json.dumps({"tap": anchor, "sounds": []}))
        return str(folder)

    imgs = list(side.group_frames("Bomba", [("Pota", rec("a", 0.5)), ("1", rec("b", 0.2))], 4, 8, 0.1, "tap"))
    assert len(imgs) == 28, "0.1 s before the taps to b's end (1.0 − 0.2 = 0.8 s), both ends included, at 30 fps"
    assert imgs[0].size == (8, side.HEAD_H + 8)
    assert imgs[0].getpixel((1, side.HEAD_H + 2))[0] == 80, "a shows 0.4 s"
    assert imgs[0].getpixel((5, side.HEAD_H + 2))[0] == 20, "b shows 0.1 s"
    assert imgs[-1].getpixel((5, side.HEAD_H + 2))[0] == 200, "b ends on its last frame"


@pytest.mark.skipif(not os.path.exists(side.FFMPEG), reason="ffmpeg missing")
def test_encode_pipes_frames_into_an_mp4(tmp_path):
    out = str(tmp_path / "x.mp4")
    side.encode((Image.new("RGB", (32, 32), (i * 40, 0, 0)) for i in range(5)), (32, 32), out)
    assert os.path.getsize(out) > 0


def test_the_label_font_draws_every_turkish_letter():
    # titles and labels are Turkish ("Doğuş", "Altın", "İniş"): a font without a glyph leaves a hole ("Do u")
    from PIL import ImageFont
    font = ImageFont.truetype(side.FONT, 40)
    missing = [ch for ch in "ÇçĞğİıÖöŞşÜü·×" if font.getmask(ch).getbbox() is None]
    assert missing == []


def test_a_long_label_shrinks_to_fit_its_column():
    from PIL import Image, ImageDraw
    draw = ImageDraw.Draw(Image.new("RGB", (8, 8)))
    font = side.fitted("Bomba ×0,3 · 2 · Basketbol", 540 - 24, 34)
    assert draw.textlength("Bomba ×0,3 · 2 · Basketbol", font=font) <= 540 - 24
    assert side.fitted("Pota", 540, 34).size == 34, "a short label keeps its size"
