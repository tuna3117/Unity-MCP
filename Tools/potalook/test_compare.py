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


def test_special_moments_cover_the_scenarios():
    assert set(compare.SPECIAL_MOMENTS) == {"birth", "line", "bomb", "chain", "golden"}
    # the Birth sheet anchors at the bomb birth (tap 32): its gathered caps are no target, so no HUD sticker covers the pop
    assert compare.special_moment("birth")[0] == "born4"
    assert compare.special_moment("golden")[0] == "land"
    assert {compare.special_moment(s)[0] for s in ("line", "bomb", "chain")} == {"tap"}
    for _, times in compare.SPECIAL_MOMENTS.values():
        assert min(times) >= -compare.WINDOW, "aligned() drops frames more than WINDOW before the anchor"


def test_an_unknown_special_scenario_is_refused():
    with pytest.raises(ValueError):
        compare.special_moment("free")


def test_special_sheet_labels_count_from_the_anchor_in_turkish():
    corner, labels = compare.special_labels("bomb")
    assert corner == "0 = dokunuş"
    assert labels[:3] == ["+0,08 sn", "+0,20 sn", "+0,30 sn"]
    corner, labels = compare.special_labels("birth")
    assert corner == "0 = doğuş\n(bomba)"
    assert labels[0] == "−0,20 sn"
    assert compare.special_labels("golden")[0] == "0 = iniş"
    assert len(labels) == len(compare.special_moment("birth")[1])


def test_special_columns_land_on_each_looks_moments():
    # measured in the 2026-10-01 recordings (tap-relative): Royal's split +0.24, the chain's screen flash +0.34, the fireball clear of
    # stickers +0.48, Court's SLAM! +0.56 (bomb) / +0.68 (chain), the column rockets +0.85
    assert {0.24, 0.36}.issubset(compare.special_moment("line")[1])
    assert {0.30, 0.40, 0.48, 0.56}.issubset(compare.special_moment("bomb")[1])
    assert {0.34, 0.68, 0.85}.issubset(compare.special_moment("chain")[1])
