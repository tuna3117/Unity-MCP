#!/usr/bin/env -S uv run --quiet --with numpy python
"""Bake a scaled/rotated Armature node into a rigged GLB so every joint has unit scale and metre translations.

Uthana create-character (Blender export) puts the skeleton under an Armature node with scale 0.01 and a 90 degree
rotation, with inverse bind matrices that only make sense when that node is ignored. glTFast then skins the mesh wrong.
This rewrites the node transforms (globals kept, scale stripped) and recomputes the inverse bind matrices.

Usage: fix_rig_glb.py in.glb out.glb
"""
import struct, json, sys
import numpy as np

src, dst = sys.argv[1], sys.argv[2]
b = open(src, "rb").read()
cl = struct.unpack("<I", b[12:16])[0]
g = json.loads(b[20:20 + cl])
bl = struct.unpack("<I", b[20 + cl:24 + cl])[0]
bin_ = bytearray(b[28 + cl:28 + cl + bl])

def acc_slice(i):
    a = g["accessors"][i]; bv = g["bufferViews"][a["bufferView"]]
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    n = {"SCALAR": 1, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
    return off, a["count"] * n * 4

def quat_to_mat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])

def mat_to_quat(R):
    t = np.trace(R)
    if t > 0:
        s = np.sqrt(t + 1) * 2; w = 0.25 * s; x = (R[2, 1] - R[1, 2]) / s; y = (R[0, 2] - R[2, 0]) / s; z = (R[1, 0] - R[0, 1]) / s
    elif R[0, 0] > R[1, 1] and R[0, 0] > R[2, 2]:
        s = np.sqrt(1 + R[0, 0] - R[1, 1] - R[2, 2]) * 2; w = (R[2, 1] - R[1, 2]) / s; x = 0.25 * s; y = (R[0, 1] + R[1, 0]) / s; z = (R[0, 2] + R[2, 0]) / s
    elif R[1, 1] > R[2, 2]:
        s = np.sqrt(1 + R[1, 1] - R[0, 0] - R[2, 2]) * 2; w = (R[0, 2] - R[2, 0]) / s; x = (R[0, 1] + R[1, 0]) / s; y = 0.25 * s; z = (R[1, 2] + R[2, 1]) / s
    else:
        s = np.sqrt(1 + R[2, 2] - R[0, 0] - R[1, 1]) * 2; w = (R[1, 0] - R[0, 1]) / s; x = (R[0, 2] + R[2, 0]) / s; y = (R[1, 2] + R[2, 1]) / s; z = 0.25 * s
    q = np.array([x, y, z, w]); return (q / np.linalg.norm(q)).tolist()

def local(n):
    M = np.eye(4); M[:3, :3] = quat_to_mat(n.get("rotation", [0, 0, 0, 1])) @ np.diag(n.get("scale", [1, 1, 1])); M[:3, 3] = n.get("translation", [0, 0, 0]); return M

nodes = g["nodes"]
parent = {c: i for i, n in enumerate(nodes) for c in n.get("children", [])}
def glob(i):
    M = local(nodes[i]); return (glob(parent[i]) @ M) if i in parent else M

skin = g["skins"][0]
joints = skin["joints"]
off, size = acc_slice(skin["inverseBindMatrices"])
ibm = np.frombuffer(bytes(bin_[off:off + size]), dtype=np.float32).reshape(-1, 4, 4).transpose(0, 2, 1)
G = {j: glob(j) for j in joints}
err_spec = max(np.abs(G[j] @ ibm[k] - np.eye(4)).max() for k, j in enumerate(joints))
print(f"bind check (G_j * IBM_j ~ I): max error {err_spec:.4f}")

# Unit-scale globals: keep rotation + translation, drop scale.
def strip(M):
    R = M[:3, :3]; s = np.linalg.norm(R, axis=0); Rn = R / s
    N = np.eye(4); N[:3, :3] = Rn; N[:3, 3] = M[:3, 3]; return N, s
Gn = {}
for j in joints:
    Gn[j], s = strip(G[j])
print("joint global scale before fix:", np.round(s, 4), "(stripped)")

# New locals: parent = nearest ancestor that is a joint, otherwise scene root (identity).
jset = set(joints)
for j in joints:
    p = parent.get(j)
    while p is not None and p not in jset: p = parent.get(p)
    L = Gn[j] if p is None else np.linalg.inv(Gn[p]) @ Gn[j]
    n = nodes[j]
    n["translation"] = L[:3, 3].tolist(); n["rotation"] = mat_to_quat(L[:3, :3]); n.pop("scale", None)

# New IBMs = inverse of the new unit-scale globals (mesh node sits at identity).
new_ibm = np.stack([np.linalg.inv(Gn[j]) for j in joints]).astype(np.float32)
bin_[off:off + size] = new_ibm.transpose(0, 2, 1).tobytes()   # back to column-major
skin.pop("skeleton", None)

# Re-parent: joints whose parent is a non-joint (the Armature) and the skinned mesh go to the scene root.
scene = g["scenes"][g.get("scene", 0)]
roots = []
for i, n in enumerate(nodes):
    if "mesh" in n or (i in jset and parent.get(i) not in jset):
        roots.append(i)
for i in roots:
    p = parent.get(i)
    if p is not None: nodes[p]["children"].remove(i)
    for k in ("translation", "rotation", "scale"):
        if "mesh" in nodes[i]: nodes[i].pop(k, None)
scene["nodes"] = [i for i in scene["nodes"] if nodes[i].get("children") or "mesh" in nodes[i] or i in jset] + [i for i in roots if i not in scene["nodes"]]
# drop now-empty Armature-style nodes from the scene list
scene["nodes"] = [i for i in scene["nodes"] if "mesh" in nodes[i] or i in jset or nodes[i].get("children")]

# Material: Trellis textures are baked albedo; glTF's default metallic (1.0) renders them dark. Force dielectric.
for m in g.get("materials", []):
    pbr = m.setdefault("pbrMetallicRoughness", {})
    pbr["metallicFactor"] = 0.0; pbr["roughnessFactor"] = 0.6
    print("material", m.get("name"), "-> metallic 0, roughness 0.6")

# Verify
parent = {c: i for i, n in enumerate(nodes) for c in n.get("children", [])}
G2 = {j: glob(j) for j in joints}
err = max(np.abs(G2[j] @ np.linalg.inv(Gn[j]) - np.eye(4)).max() for j in joints)
print(f"after fix: max |G'_j * IBM'_j - I| = {err:.6f}; hips global = {np.round(G2[joints[0]][:3,3],3)}, head global y = {max(G2[j][1,3] for j in joints):.3f}")

js = json.dumps(g, separators=(",", ":")).encode()
js += b" " * ((4 - len(js) % 4) % 4)
out = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(bin_)) + struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(bin_), 0x004E4942) + bytes(bin_)
open(dst, "wb").write(out)
print("wrote", dst, len(out) // 1024, "KB; scene roots:", [nodes[i]["name"] for i in scene["nodes"]])
