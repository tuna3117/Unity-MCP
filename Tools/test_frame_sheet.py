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


def test_transpose_puts_folders_in_columns_and_times_in_rows():
    assert fs.cell_origin(0, 0, 150, 44, 200, 300, False) == (150, 44)
    assert fs.cell_origin(2, 1, 150, 44, 200, 300, False) == (150 + 200, 44 + 600), "default: folder = row, time = column"
    assert fs.cell_origin(2, 1, 150, 44, 200, 300, True) == (150 + 2 * (200 + fs.GAP), 44 + 300), "transposed: folder = column, time = row"


def test_transpose_end_to_end(tmp_path):
    from PIL import Image
    for name, colour in (("a", (255, 0, 0)), ("b", (0, 0, 255))):
        d = tmp_path / name
        d.mkdir()
        for t in (0.0, 1.0):
            Image.new("RGB", (40, 40), colour).save(d / f"f_0000_t{t:.3f}.png")
    out = tmp_path / "sheet.jpg"
    fs.main([str(out), f"A={tmp_path / 'a'}", f"B={tmp_path / 'b'}", "--times", "0,1", "--height", "60", "--transpose"])
    img = Image.open(out)
    label_w = fs.label_width(["0.0 s", "1.0 s"], __import__("PIL.ImageFont", fromlist=["x"]).truetype(fs.FONT, 28))
    assert img.size == (label_w + 2 * 60 + fs.GAP, 44 + 2 * 60)
    r, g, b = img.convert("RGB").getpixel((label_w + 30, 44 + 90))
    assert r > 200 and b < 60, "column A is the red folder at the second time"
    r, g, b = img.convert("RGB").getpixel((label_w + 60 + fs.GAP + 30, 44 + 30))
    assert b > 200 and r < 60, "column B is the blue one"
