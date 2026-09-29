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
