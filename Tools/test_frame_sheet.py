import importlib.util
import os

spec = importlib.util.spec_from_file_location("frame_sheet", os.path.join(os.path.dirname(__file__), "frame_sheet.py"))
fs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fs)


def test_frame_time_reads_the_t_field():
    assert fs.frame_time("f_0012_t0.400.png") == 0.4
    assert fs.frame_time("f_0000_t12.034_b3.png") == 12.034
    assert fs.frame_time("notes.txt") is None


def test_nearest_picks_the_closest_frame():
    frames = [("a", 0.0), ("b", 0.2), ("c", 0.45)]
    assert fs.nearest(frames, 0.3) == "b"
    assert fs.nearest(frames, 0.4) == "c"
    assert fs.nearest(frames, 9.0) == "c"


def test_column_labels_default_to_the_time():
    assert fs.column_labels([0.25, 1.0], None) == ["0.2 s", "1.0 s"]


def test_column_labels_take_the_given_ones_in_order():
    assert fs.column_labels([1.0, 2.0], "−0,25 sn|0") == ["−0,25 sn", "0"]
    import pytest
    with pytest.raises(ValueError):
        fs.column_labels([1.0, 2.0], "only one")


def test_label_lines_fit_inside_the_label_column():
    from PIL import ImageFont
    font = ImageFont.truetype(fs.FONT, 28)
    width = fs.label_width(["A", "1 Büyük şut"], font)
    assert width >= font.getlength("1 Büyük şut") + 24, "the column opens to the longest label"
    long_label = "3 Pano damgası, dokunma atlamalı ve yavaş çekimli"
    width = fs.label_width([long_label], font)
    assert width <= fs.MAX_LABEL_W
    lines = fs.wrap(long_label, font, width - 24)
    assert len(lines) > 1 and " ".join(lines) == long_label
    assert all(font.getlength(line) <= width - 24 for line in lines)
