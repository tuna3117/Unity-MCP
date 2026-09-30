import importlib.util
import os
import sys

import pytest

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
