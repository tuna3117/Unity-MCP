import importlib.util
import os
import sys

import pytest

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




def test_onsets_are_the_first_frame_of_each_marker_run():
    flags = [False, False, True, True, False, False, False, True, False]
    assert compare.onset_times(flags, 30) == [2 / 30, 7 / 30]


def test_marker_pixel_is_magenta():
    assert compare.is_marker((255, 0, 255))
    assert not compare.is_marker((142, 201, 242))


def test_moments_cover_the_spec():
    assert set(compare.MOMENTS) == {"tap", "chain", "rotate", "win"}
    assert 0.5605 in compare.MOMENTS["tap"]
    assert compare.anchor_of("chain") == "tap"
    assert compare.anchor_of("win") == "win"


def test_rows_are_label_equals_dir():
    assert compare.parse_rows(["A (Candy)=Temp/record/a", "B=Temp/record/b"]) == [
        ("A (Candy)", "Temp/record/a"), ("B", "Temp/record/b")]


def test_a_row_without_a_label_is_refused():
    with pytest.raises(ValueError):
        compare.parse_rows(["Temp/record/a"])
