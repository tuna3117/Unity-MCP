#!/usr/bin/env -S uv run --quiet --with pillow python
"""Re-encode EXT_texture_webp images inside a GLB as PNG (glTFast does not read WebP). Usage: glb_webp_to_png.py in.glb out.glb"""
import struct, json, sys, io
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
b = open(src, "rb").read()
cl = struct.unpack("<I", b[12:16])[0]; g = json.loads(b[20:20 + cl])
bl = struct.unpack("<I", b[20 + cl:24 + cl])[0]; bin_ = b[28 + cl:28 + cl + bl]
views = g["bufferViews"]
chunks = []   # rebuild the buffer: keep every bufferView, replacing webp image views
new_views = []
def pad4(x): return x + b"\0" * ((4 - len(x) % 4) % 4)
data = bytearray()
img_by_view = {im["bufferView"]: i for i, im in enumerate(g.get("images", [])) if "bufferView" in im}
for vi, v in enumerate(views):
    chunk = bin_[v.get("byteOffset", 0): v.get("byteOffset", 0) + v["byteLength"]]
    if vi in img_by_view and g["images"][img_by_view[vi]].get("mimeType") == "image/webp":
        im = Image.open(io.BytesIO(chunk)); buf = io.BytesIO(); im.save(buf, "PNG"); chunk = buf.getvalue()
        g["images"][img_by_view[vi]]["mimeType"] = "image/png"
        print(f"image {img_by_view[vi]}: webp {im.size} -> png {len(chunk)//1024} KB")
    nv = dict(v); nv["byteOffset"] = len(data); nv["byteLength"] = len(chunk); new_views.append(nv)
    data += pad4(chunk)
g["bufferViews"] = new_views; g["buffers"][0]["byteLength"] = len(data)
for t in g.get("textures", []):
    ext = t.pop("extensions", {}); w = ext.get("EXT_texture_webp")
    if w and "source" in w: t["source"] = w["source"]
    if ext and "EXT_texture_webp" not in ext: t["extensions"] = ext
for k in ("extensionsUsed", "extensionsRequired"):
    if k in g: g[k] = [e for e in g[k] if e != "EXT_texture_webp"] or None
    if k in g and g[k] is None: del g[k]
js = json.dumps(g, separators=(",", ":")).encode(); js += b" " * ((4 - len(js) % 4) % 4)
out = struct.pack("<III", 0x46546C67, 2, 28 + len(js) + len(data)) + struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(data), 0x004E4942) + bytes(data)
open(dst, "wb").write(out); print("wrote", dst, len(out) // 1024, "KB")
