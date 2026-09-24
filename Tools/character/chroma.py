#!/usr/bin/env python3
"""Key pure-green chroma background to alpha, crop to content with a margin, save RGBA PNG."""
import sys
import numpy as np
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
im = np.asarray(Image.open(src).convert("RGB")).astype(np.int32)
r, g, b = im[..., 0], im[..., 1], im[..., 2]
green = (g - np.maximum(r, b))            # how green a pixel is
alpha = np.clip((120 - green) / 60.0, 0, 1)   # >=120 fully bg, <=60 fully fg
# despill: pull green down to max(r,b) where partially transparent
gs = np.minimum(g, np.maximum(r, b) + 10)
rgb = np.stack([r, gs, b], -1)
out = np.dstack([rgb, (alpha * 255)]).astype(np.uint8)
ys, xs = np.where(alpha > 0.5)
m = 40
y0, y1, x0, x1 = max(ys.min()-m, 0), min(ys.max()+m, out.shape[0]), max(xs.min()-m, 0), min(xs.max()+m, out.shape[1])
Image.fromarray(out[y0:y1, x0:x1], "RGBA").save(dst)
print(dst, out[y0:y1, x0:x1].shape)
