import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("compare", os.path.join(HERE, "compare.py"))
compare = importlib.util.module_from_spec(spec)
sys.path.insert(0, os.path.dirname(HERE))
spec.loader.exec_module(compare)


def test_shifted_name_puts_the_anchor_at_one_second():
    assert compare.shifted_name(3, 2.5, 2.0) == "f_00003_t1.500.png"
    assert compare.shifted_name(0, 1.2, 2.0) == "f_00000_t0.200.png"


def test_frames_before_the_window_are_skipped():
    assert compare.shifted_name(0, 0.5, 2.0) is None


def test_moments_cover_the_spec():
    assert set(compare.MOMENTS) == {"tap", "rotate", "win"}
    assert 0.5605 in compare.MOMENTS["tap"]


def test_onsets_are_the_first_frame_of_each_marker_run():
    flags = [False, False, True, True, False, False, False, True, False]
    assert compare.onset_times(flags, 30) == [2 / 30, 7 / 30]


def test_marker_pixel_is_magenta():
    assert compare.is_marker((255, 0, 255))
    assert not compare.is_marker((142, 201, 242))
