# Blender Workshop (studio sub-project 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Execution note for this project:** one Blender install, one Unity Editor and one MCP bridge per app on this Mac → tasks run inline and sequentially in this session (superpowers:executing-plans). Blender code is written test-first and tested inside background Blender (`tools/run.sh test`); Unity code is tested with EditMode tests through the Unity bridge.

**Goal:** Create `~/projects/Blender MCP`, a Blender workshop that measures, previews and exports engine-ready assets with a hand-off manifest, connect the official Blender MCP bridge, and prove the round trip into Unity with a stylized Pota hoop.

**Architecture:** A small Python package `bl` (in `tools/bl/`) holds every tool as a plain function that returns a JSON-able dict. The same function runs headless through `tools/run.sh <tool> '<json>'` (background Blender, `tools/run_tool.py` dispatcher) or inside a running Blender through the MCP bridge (`import bl`). Exports write `<id>.glb|fbx + manifest.json + preview.png`; measurements of an export are taken in a fresh background Blender so the working scene is never disturbed. Unity reads the hand-off with a new Core tool `ai_import_asset`.

**Tech Stack:** Blender 5.2.2 LTS (Python 3.13, numpy bundled), EEVEE (`BLENDER_EEVEE`) + Workbench renders, glTF 2.0 / FBX exporters, `unittest` inside Blender, official Blender Lab MCP v1.0.3 (add-on + `blender-mcp` server via `uv`), Unity 6000.6.2f1 + glTFast + MCP for Unity bridge, Unity Test Framework.

**Spec:** `docs/superpowers/specs/2026-09-28-blender-workshop-design.md`

## Global Constraints

- Blender binary: `/Applications/Blender.app/Contents/MacOS/Blender` (5.2.2 LTS); render engine id is `BLENDER_EEVEE`.
- Workshop root: `/Users/tunaaltin17/projects/Blender MCP` — the path has a space: quote it everywhere. Local git only; no remote, no push.
- Bridge: official Blender Lab MCP pinned at tag `v1.0.3` (`https://projects.blender.org/lab/blender_mcp.git`); add-on id `mcp`, TCP `localhost:9876`, auto-start on; server entry point `blender-mcp` over stdio, env `BLENDER_PATH` = the binary above. Registered in Claude Code at **user** scope as `blender`.
- Units metres; Blender scenes are +Z up; exported files are +Y up (glTF convention); manifest `"units": "m", "up": "+Y"`.
- Manifest fields exactly as spec §7: `id, game, kind, version, files{glb,fbx,previews}, units, up, scale_applied, stats{tris,materials,textures,bones,clips,size_m}, source{recipe,copy_provenance,inputs,...}, checks{non_manifold,ngons,uv_missing,transforms_unapplied,passed}, made`.
- `kind` ∈ `prop | character | animation | vfx-mesh | vfx-flipbook`; Unity folder: Props / Characters / Animations / VFX / VFX.
- Every tool works headless (`tools/run.sh`) and inside a running Blender; in a running Blender no tool may wipe or overwrite the user's open file.
- No AI-service calls in the Blender workshop (that is `copy`'s job).
- User-facing docs in Turkish; code, file names and commit messages in English; every commit ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Unity side follows Unity MCP `CLAUDE.md`: new code in `Assets/_Project/Core/...`, Core never references a game, compile with `refresh_unity` then `read_console` (`error CS`) must be empty, never compile during Play Mode.

## Review Focus

- Paths with spaces (both workshop roots contain one): every tool writes/reads such paths — pinned by `test_preview_sheet` and the export tests, which write under `.logs/... test .../`.
- Sources with unapplied object transforms (Uthana rigs come with a 0.01 armature scale): `inspect` must flag them and report world-space metres — pinned by `test_unapplied_scale_is_flagged_and_size_is_world` (Task 2).
- Re-exporting the same asset id must update Unity in place and keep the GUID (scenes keep their references) — pinned by `Reimport_KeepsGuid` (Task 8).
- Textured materials must reach the engine (images embedded in the glb) — pinned by `test_texture_is_embedded` (Task 4).
- Calling a tool from the live Blender session must not wipe the user's file (`new_scene` adds a scene instead of resetting; `make` and `.blend` loading delegate to a background process) — verified live through the bridge in Task 7 Step 6.

---

### Task 1: Workshop skeleton, headless runner and scene tools

**Files:**
- Create: `/Users/tunaaltin17/projects/Blender MCP/.gitignore`
- Create: `/Users/tunaaltin17/projects/Blender MCP/CLAUDE.md` (stub; completed in Task 10)
- Create: `/Users/tunaaltin17/projects/Blender MCP/tools/run.sh`
- Create: `/Users/tunaaltin17/projects/Blender MCP/tools/run_tool.py`
- Create: `/Users/tunaaltin17/projects/Blender MCP/tools/bl/__init__.py`
- Create: `/Users/tunaaltin17/projects/Blender MCP/tools/bl/util.py`
- Create: `/Users/tunaaltin17/projects/Blender MCP/tools/bl/scene.py`
- Create: `/Users/tunaaltin17/projects/Blender MCP/recipes/__init__.py`
- Create: `/Users/tunaaltin17/projects/Blender MCP/tests/__init__.py`
- Test: `/Users/tunaaltin17/projects/Blender MCP/tests/test_scene.py`

**Interfaces:**
- Produces: `bl.TOOLS: dict[str, callable]`; `bl.util.ROOT: str`, `bl.util.log(msg) -> None`, `bl.util.resolve(path) -> str`, `bl.util.world_bounds(objects) -> (Vector, Vector) | None`, `bl.util.node_tree(idblock)`, `bl.util.principled(material) -> ShaderNode`, `bl.util.run_background(tool: str, args: dict) -> dict`; `bl.new_scene(reset: bool | None = None) -> {"scene": str, "reset": bool}`; `bl.load(path: str) -> list[str]` (object names). Runner contract: stdout lines `[bl] …` (summary), `BLRESULT:<json>` (result), `BLERROR:<msg>` (failure, exit 1); `test` prints the unittest report between `BLTEST-BEGIN` / `BLTEST-END`.

- [ ] **Step 1: Create the repo, ignore file and CLAUDE.md stub**

```bash
BM="/Users/tunaaltin17/projects/Blender MCP"
mkdir -p "$BM"/{tools/bl,recipes,tests,assets,docs}
cd "$BM" && git init -q
```

`.gitignore`:
```
.logs/
__pycache__/
*.blend1
*.blend2
assets/_test/
.DS_Store
```

`CLAUDE.md` (stub):
```markdown
# CLAUDE.md — Blender atölyesi

Kullanıcıyla Türkçe konuş; kod, dosya adları ve commit mesajları İngilizce. Tasarım: `~/projects/Unity MCP/docs/superpowers/specs/2026-09-28-blender-workshop-design.md`.

Araçlar: `tools/run.sh <araç> '<json>'` (arayüzsüz Blender), testler: `tools/run.sh test`.
```

- [ ] **Step 2: Write the failing test** — `tests/__init__.py` (empty), `recipes/__init__.py` (empty), and `tests/test_scene.py`:

```python
import os
import unittest

import bpy

import bl


class SceneTests(unittest.TestCase):
    def test_new_scene_is_empty_and_metric(self):
        bpy.ops.mesh.primitive_cube_add()
        result = bl.new_scene()
        scene = bpy.context.scene
        self.assertEqual(result["scene"], scene.name)
        self.assertTrue(result["reset"])
        self.assertEqual(len(scene.objects), 0)
        self.assertEqual(scene.unit_settings.system, "METRIC")
        self.assertAlmostEqual(scene.unit_settings.scale_length, 1.0)

    def test_load_missing_file_raises(self):
        with self.assertRaises(FileNotFoundError):
            bl.load("/nope/missing.glb")

    def test_load_rejects_unknown_type(self):
        path = bl.util.resolve(".logs/test_note.txt")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as fh:
            fh.write("x")
        with self.assertRaises(ValueError):
            bl.load(path)

    def test_resolve_is_relative_to_workshop_root(self):
        self.assertEqual(bl.util.resolve("assets"), os.path.join(bl.util.ROOT, "assets"))
        self.assertEqual(bl.util.resolve("/tmp/x"), "/tmp/x")
```

- [ ] **Step 3: Write the runner** — `tools/run.sh`:

```zsh
#!/bin/zsh
# Runs a workshop tool in background Blender (no UI).
#   tools/run.sh <tool> ['<json args>' | @args.json]
#   tools/run.sh test [file pattern, e.g. test_inspect.py]
# Prints the tool's [bl] summary lines and its JSON result; the full Blender log is in .logs/last_run.log.
# Exit code: 0 ok, 1 tool/test failure (BLERROR line), 2 usage.
set -u
ROOT="${0:A:h:h}"
BLENDER="${BLENDER_PATH:-/Applications/Blender.app/Contents/MacOS/Blender}"
[[ $# -ge 1 ]] || { echo "usage: $0 <tool> ['<json>'|@file.json] | test [pattern]" >&2; exit 2; }
mkdir -p "$ROOT/.logs"
LOG="$ROOT/.logs/last_run.log"
"$BLENDER" -b --factory-startup --python-exit-code 1 -P "$ROOT/tools/run_tool.py" -- "$@" > "$LOG" 2>&1
code=$?
if [[ "$1" == "test" ]]; then
  sed -n '/^BLTEST-BEGIN$/,/^BLTEST-END$/p' "$LOG" | sed '1d;$d'
else
  grep -E '^(\[bl\] |BLRESULT:|BLERROR:)' "$LOG" | sed -E 's/^BLRESULT://'
fi
(( code != 0 )) && echo "(exit $code, full log: $LOG)" >&2
exit $code
```

`tools/run_tool.py`:
```python
"""Background Blender entry: `blender -b -P tools/run_tool.py -- <tool> [json|@file]` or `-- test [pattern]`."""
import json
import os
import sys
import traceback
import unittest

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
for path in (os.path.join(ROOT, "tools"), ROOT):
    if path not in sys.path:
        sys.path.insert(0, path)

import bl  # noqa: E402  (needs the paths above)


def _argv():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print("BLERROR: usage: -- <tool> [json|@file] | test [pattern]")
        sys.exit(2)
    return argv[0], argv[1:]


def _load_args(raw):
    if not raw:
        return {}
    if raw.startswith("@"):
        with open(raw[1:], encoding="utf-8") as fh:
            return json.load(fh)
    return json.loads(raw)


def _run_tests(pattern):
    print("BLTEST-BEGIN")
    suite = unittest.defaultTestLoader.discover(os.path.join(ROOT, "tests"), pattern=pattern or "test_*.py",
                                                top_level_dir=ROOT)
    result = unittest.TextTestRunner(stream=sys.stdout, verbosity=2).run(suite)
    print("BLTEST-END")
    return 0 if result.wasSuccessful() else 1


def main():
    name, rest = _argv()
    if name == "test":
        return _run_tests(rest[0] if rest else None)
    tool = bl.TOOLS.get(name)
    if tool is None:
        print("BLERROR: unknown tool '{}'; known: {}".format(name, ", ".join(sorted(bl.TOOLS))))
        return 1
    try:
        result = tool(**_load_args(rest[0] if rest else ""))
    except Exception as exc:  # report and exit non-zero; never hang
        traceback.print_exc()
        print("BLERROR: {}: {}".format(type(exc).__name__, exc))
        return 1
    print("BLRESULT:" + json.dumps(result, ensure_ascii=False))
    return 0


code = main()
sys.stdout.flush()
if code:
    sys.exit(code)
```

Then: `chmod +x "$BM/tools/run.sh"`.

- [ ] **Step 4: Run the test to verify it fails**

Run: `"$BM/tools/run.sh" test`
Expected: exit 1, log shows `ModuleNotFoundError: No module named 'bl'` (or the report errors on `bl`).

- [ ] **Step 5: Write the package** — `tools/bl/util.py`:

```python
"""Shared helpers: logging, workshop paths, bounds, node trees and fresh background runs."""
import json
import os
import subprocess
import sys

import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
if ROOT not in sys.path:
    sys.path.append(ROOT)  # recipes package


def log(message):
    print("[bl] " + message)
    sys.stdout.flush()


def resolve(path):
    """Absolute path; relative paths are relative to the workshop root."""
    path = os.path.expanduser(path)
    return path if os.path.isabs(path) else os.path.join(ROOT, path)


def world_bounds(objects):
    """(min, max) world corners over the objects' evaluated bounding boxes, or None when there are none."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    lo = Vector((float("inf"),) * 3)
    hi = Vector((float("-inf"),) * 3)
    found = False
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        for corner in evaluated.bound_box:
            point = evaluated.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, point))
            hi = Vector(map(max, hi, point))
            found = True
    return (lo, hi) if found else None


def node_tree(idblock):
    """The material/world node tree (Blender 5 creates it; older files may still need `use_nodes`)."""
    if getattr(idblock, "node_tree", None) is None and hasattr(idblock, "use_nodes"):
        idblock.use_nodes = True
    return idblock.node_tree


def principled(material):
    """The material's Principled BSDF, created and wired to the output if missing."""
    tree = node_tree(material)
    node = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if node is None:
        node = tree.nodes.new("ShaderNodeBsdfPrincipled")
        output = next((n for n in tree.nodes if n.type == "OUTPUT_MATERIAL"), None) \
            or tree.nodes.new("ShaderNodeOutputMaterial")
        tree.links.new(node.outputs["BSDF"], output.inputs["Surface"])
    return node


def run_background(tool, args):
    """Runs a tool in a fresh background Blender (the current scene stays untouched) and returns its result."""
    cmd = [bpy.app.binary_path, "-b", "--factory-startup", "--python-exit-code", "1",
           "-P", os.path.join(ROOT, "tools", "run_tool.py"), "--", tool, json.dumps(args)]
    proc = subprocess.run(cmd, capture_output=True, text=True, check=False)
    lines = proc.stdout.splitlines()
    for line in lines:
        if line.startswith("BLRESULT:"):
            return json.loads(line[len("BLRESULT:"):])
    errors = [line for line in lines if line.startswith("BLERROR:")]
    raise RuntimeError("background '{}' failed (exit {}): {}".format(
        tool, proc.returncode, errors[-1] if errors else proc.stdout[-800:]))
```

`tools/bl/scene.py`:
```python
"""Empty scenes and file loading."""
import os

import bpy

from .util import log, resolve

LOADERS = {".blend": "blend", ".glb": "gltf", ".gltf": "gltf", ".fbx": "fbx"}


def new_scene(reset=None):
    """Empty metric scene. Headless (default) resets the whole file; in a running Blender it adds a new
    scene and switches to it, so the user's work is never wiped."""
    if reset is None:
        reset = bpy.app.background
    if reset:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
    else:
        scene = bpy.data.scenes.new("bl")
        bpy.context.window.scene = scene
    units = scene.unit_settings
    units.system = "METRIC"
    units.scale_length = 1.0
    units.length_unit = "METERS"
    log("new scene '{}'".format(scene.name))
    return {"scene": scene.name, "reset": reset}


def load(path):
    """Opens a .blend (headless only) or imports .glb/.gltf/.fbx into a new empty scene; returns object names."""
    path = resolve(path)
    if not os.path.isfile(path):
        raise FileNotFoundError(path)
    kind = LOADERS.get(os.path.splitext(path)[1].lower())
    if kind is None:
        raise ValueError("unsupported file type: " + path)
    if kind == "blend":
        if not bpy.app.background:
            raise RuntimeError("opening a .blend would replace the open file; use tools/run.sh for .blend files")
        bpy.ops.wm.open_mainfile(filepath=path)
    else:
        new_scene()
        if kind == "gltf":
            bpy.ops.import_scene.gltf(filepath=path)
        else:
            bpy.ops.import_scene.fbx(filepath=path)
    names = [obj.name for obj in bpy.context.scene.objects]
    log("loaded {} ({} objects)".format(os.path.basename(path), len(names)))
    return names
```

`tools/bl/__init__.py`:
```python
"""Blender workshop tools. Every tool returns a JSON-able dict and prints one '[bl] ...' summary line.

Headless:  tools/run.sh <tool> '<json>'
Live (MCP execute_blender_code):
    import sys; sys.path.insert(0, "/Users/tunaaltin17/projects/Blender MCP/tools"); import bl
"""
from . import util
from .scene import load, new_scene

TOOLS = {"new_scene": new_scene}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `"$BM/tools/run.sh" test`
Expected: 4 tests, `OK`, exit 0.

- [ ] **Step 7: Check the runner's error path**

Run: `"$BM/tools/run.sh" nosuchtool; echo "exit=$?"` → `BLERROR: unknown tool 'nosuchtool'; known: new_scene`, `exit=1`.
Run: `"$BM/tools/run.sh" new_scene` → `[bl] new scene 'Scene'` and `{"scene": "Scene", "reset": true}`, exit 0.

- [ ] **Step 8: Commit**

```bash
cd "$BM" && git add -A && git commit -q -m "feat: workshop skeleton, headless runner and scene tools

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `inspect` — measure a file or the scene

**Files:**
- Create: `tools/bl/inspect.py`
- Modify: `tools/bl/__init__.py` (import + TOOLS entry)
- Test: `tests/test_inspect.py`

**Interfaces:**
- Consumes: `bl.load`, `bl.util.log`, `bl.util.world_bounds`.
- Produces: `bl.inspect(path: str | None = None) -> dict` with keys `file, objects, meshes, armatures, tris, ngons, non_manifold, open_edges, uv_missing: list[str], materials: list[str], textures: list[{name, size:[w,h]}], bones, bone_names, size_m:[x,y,z], min_m, max_m, transforms_unapplied: list[str], clips: list[{name, frames:[start,end]}]`. `non_manifold` counts edges with 0 or >2 faces; open (boundary) edges are counted separately.

- [ ] **Step 1: Write the failing test** — `tests/test_inspect.py`:

```python
import unittest

import bpy

import bl


class InspectTests(unittest.TestCase):
    def setUp(self):
        bl.new_scene()

    def test_cube_counts(self):
        bpy.ops.mesh.primitive_cube_add(size=2)
        r = bl.inspect()
        self.assertEqual(r["tris"], 12)
        self.assertEqual((r["ngons"], r["non_manifold"], r["open_edges"]), (0, 0, 0))
        self.assertEqual(r["size_m"], [2.0, 2.0, 2.0])
        self.assertEqual(r["transforms_unapplied"], [])
        self.assertEqual(r["uv_missing"], [])

    def test_ngon_and_open_edges(self):
        bpy.ops.mesh.primitive_circle_add(vertices=8, fill_type="NGON")
        r = bl.inspect()
        self.assertEqual(r["ngons"], 1)
        self.assertEqual(r["open_edges"], 8)
        self.assertEqual(r["tris"], 6)

    def test_unapplied_scale_is_flagged_and_size_is_world(self):
        bpy.ops.mesh.primitive_cube_add(size=2)
        bpy.context.object.scale = (0.01, 0.01, 0.01)
        r = bl.inspect()
        self.assertEqual(r["transforms_unapplied"], ["Cube"])
        self.assertAlmostEqual(r["size_m"][2], 0.02, places=4)

    def test_armature_bones(self):
        bpy.ops.object.armature_add()
        r = bl.inspect()
        self.assertEqual((r["armatures"], r["bones"]), (1, 1))
        self.assertEqual(r["bone_names"], ["Bone"])

    def test_materials_are_listed(self):
        bpy.ops.mesh.primitive_cube_add()
        bpy.context.object.data.materials.append(bpy.data.materials.new("red"))
        self.assertEqual(bl.inspect()["materials"], ["red"])

    def test_missing_file(self):
        with self.assertRaises(FileNotFoundError):
            bl.inspect(path="/nope/x.glb")
```

- [ ] **Step 2: Run it to verify it fails**

Run: `"$BM/tools/run.sh" test test_inspect.py`
Expected: FAIL/ERROR, `module 'bl' has no attribute 'inspect'`.

- [ ] **Step 3: Implement** — `tools/bl/inspect.py`:

```python
"""Measures a file or the current scene: topology, materials, textures, bones, size, transforms, clips."""
import bmesh
import bpy

from .scene import load
from .util import log, world_bounds


def _textures(materials):
    seen = {}
    for material in materials:
        tree = material.node_tree
        for node in (tree.nodes if tree else ()):
            if node.type == "TEX_IMAGE" and node.image is not None:
                seen[node.image.name] = list(node.image.size)
    return [{"name": name, "size": size} for name, size in sorted(seen.items())]


def _unapplied(obj):
    if obj.rotation_mode == "QUATERNION":
        still = all(abs(a - b) < 1e-6 for a, b in zip(obj.rotation_quaternion, (1.0, 0.0, 0.0, 0.0)))
    else:
        still = all(abs(a) < 1e-6 for a in obj.rotation_euler)
    return not (still and all(abs(s - 1.0) < 1e-6 for s in obj.scale))


def inspect(path=None):
    if path:
        load(path)
    objects = list(bpy.context.scene.objects)
    meshes = [o for o in objects if o.type == "MESH"]
    armatures = [o for o in objects if o.type == "ARMATURE"]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    tris = ngons = non_manifold = open_edges = 0
    uv_missing, materials = [], {}
    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        tris += len(mesh.loop_triangles)
        evaluated.to_mesh_clear()
        data = obj.data
        ngons += sum(1 for poly in data.polygons if poly.loop_total > 4)
        bm = bmesh.new()
        bm.from_mesh(data)
        for edge in bm.edges:
            faces = len(edge.link_faces)
            if faces == 0 or faces > 2:
                non_manifold += 1
            elif faces == 1:
                open_edges += 1
        bm.free()
        if not data.uv_layers:
            uv_missing.append(obj.name)
        for slot in obj.material_slots:
            if slot.material is not None:
                materials[slot.material.name] = slot.material
    bones = [bone.name for arm in armatures for bone in arm.data.bones]
    bounds = world_bounds(meshes or armatures)
    lo, hi = bounds if bounds else ((0.0,) * 3, (0.0,) * 3)
    size = [round(hi[i] - lo[i], 4) for i in range(3)]
    result = {
        "file": path, "objects": len(objects), "meshes": len(meshes), "armatures": len(armatures),
        "tris": tris, "ngons": ngons, "non_manifold": non_manifold, "open_edges": open_edges,
        "uv_missing": uv_missing, "materials": sorted(materials), "textures": _textures(materials.values()),
        "bones": len(bones), "bone_names": bones[:200],
        "size_m": size, "min_m": [round(v, 4) for v in lo], "max_m": [round(v, 4) for v in hi],
        "transforms_unapplied": [o.name for o in meshes + armatures if _unapplied(o)],
        "clips": [{"name": a.name, "frames": [int(a.frame_range[0]), int(a.frame_range[1])]} for a in bpy.data.actions],
    }
    log("inspect: {} tris, {} meshes, {} bones, size {:.2f} x {:.2f} x {:.2f} m, {} ngons, {} non-manifold, "
        "{} unapplied".format(tris, len(meshes), len(bones), size[0], size[1], size[2], ngons, non_manifold,
                              len(result["transforms_unapplied"])))
    return result
```

`tools/bl/__init__.py`: add `from .inspect import inspect` and `"inspect": inspect` to `TOOLS`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"$BM/tools/run.sh" test` → all tests `OK`.
Run: `"$BM/tools/run.sh" inspect '{"path":"/nope.glb"}'; echo "exit=$?"` → `BLERROR: FileNotFoundError: /nope.glb`, `exit=1`.

- [ ] **Step 5: Commit** — `git add -A && git commit -q -m "feat: inspect tool (topology, materials, bones, size, transforms, clips)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 3: Render stage and `preview` sheet

**Files:**
- Create: `tools/bl/render.py`
- Create: `tools/bl/preview.py`
- Modify: `tools/bl/__init__.py`
- Test: `tests/test_preview.py`

**Interfaces:**
- Consumes: `bl.inspect`, `bl.load`, `bl.util.{log, resolve, world_bounds, node_tree}`.
- Produces: `bl.render.Stage(tile: int, center, radius: float)` context manager with `.shoot(name: str, direction: tuple) -> str` (PNG path; orthographic camera, same scale for every view); `bl.render.compose(tiles: list[str|None], cols: int, rows: int, tile: int, out: str) -> str` (row-major from top-left, `None` = dark slot); `bl.preview(path=None, out=".logs/preview.png", tile=512) -> {"png": str, "tile": int, "views": list[str], "stats": <inspect dict>}`. Sheet layout 4×2: front, right, back, top / three_quarter, wire, stats, empty.

- [ ] **Step 1: Write the failing test** — `tests/test_preview.py`:

```python
import os
import unittest

import bpy
import numpy as np

import bl


class PreviewTests(unittest.TestCase):
    def test_preview_sheet(self):
        bl.new_scene()
        bpy.ops.mesh.primitive_cube_add(size=2)
        out = bl.util.resolve(".logs/preview test dir/cube.png")  # space in the path on purpose
        if os.path.exists(out):
            os.remove(out)
        r = bl.preview(out=out, tile=128)
        self.assertEqual(r["png"], out)
        self.assertTrue(os.path.isfile(out))
        self.assertEqual(r["stats"]["tris"], 12)
        self.assertEqual(r["views"], ["front", "right", "back", "top", "three_quarter", "wire", "stats"])
        image = bpy.data.images.load(out)
        self.assertEqual(tuple(image.size), (512, 256))
        pixels = np.array(image.pixels[:], dtype=np.float32).reshape(256, 512, 4)
        front = pixels[128:256, 0:128, :3]  # top-left tile (rows count from the bottom)
        self.assertGreater(float(front.std()), 0.02, "front view looks empty")
        self.assertEqual(len(bpy.context.scene.objects), 1, "stage helpers must be cleaned up")
```

- [ ] **Step 2: Run it to verify it fails**

Run: `"$BM/tools/run.sh" test test_preview.py` → ERROR `no attribute 'preview'`.

- [ ] **Step 3: Implement** — `tools/bl/render.py`:

```python
"""Headless render stage shared by preview and anim_sheet: neutral world, key light, orthographic camera,
same scale for every view, and a tile composer. Everything it adds is removed on exit."""
import math
import os

import bpy
import numpy as np
from mathutils import Vector

from .util import node_tree, resolve

HELPER = "bl_stage"


class Stage:
    def __init__(self, tile, center, radius):
        self.tile = tile
        self.center = Vector(center)
        self.radius = max(float(radius), 0.01)
        self.scene = bpy.context.scene
        self.tiles_dir = resolve(".logs/tiles")
        os.makedirs(self.tiles_dir, exist_ok=True)

    def __enter__(self):
        s = self.scene
        r = s.render
        self._saved = (r.engine, r.resolution_x, r.resolution_y, r.resolution_percentage, r.filepath,
                       r.film_transparent, s.view_settings.view_transform, s.world, s.camera)
        r.engine = "BLENDER_EEVEE"
        r.resolution_x = r.resolution_y = self.tile
        r.resolution_percentage = 100
        r.image_settings.file_format = "PNG"
        r.film_transparent = False
        s.view_settings.view_transform = "Standard"
        if hasattr(s, "eevee"):
            s.eevee.taa_render_samples = 16
        self.collection = bpy.data.collections.new(HELPER)
        s.collection.children.link(self.collection)
        self.world = bpy.data.worlds.new(HELPER)
        background = next(n for n in node_tree(self.world).nodes if n.type == "BACKGROUND")
        background.inputs["Color"].default_value = (0.20, 0.21, 0.23, 1.0)
        background.inputs["Strength"].default_value = 1.0
        s.world = self.world
        sun = bpy.data.objects.new(HELPER + "_sun", bpy.data.lights.new(HELPER + "_sun", "SUN"))
        sun.data.energy = 3.0
        sun.rotation_euler = (math.radians(50), 0.0, math.radians(35))
        self.collection.objects.link(sun)
        camera_data = bpy.data.cameras.new(HELPER + "_cam")
        camera_data.type = "ORTHO"
        camera_data.ortho_scale = self.radius * 2.1
        camera_data.clip_start = self.radius * 0.01
        camera_data.clip_end = self.radius * 20
        self.camera = bpy.data.objects.new(HELPER + "_cam", camera_data)
        self.collection.objects.link(self.camera)
        s.camera = self.camera
        return self

    def shoot(self, name, direction):
        d = Vector(direction).normalized()
        self.camera.location = self.center + d * self.radius * 5
        self.camera.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(self.tiles_dir, name + ".png")
        self.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        return path

    def __exit__(self, *exc):
        s = self.scene
        r = s.render
        (r.engine, r.resolution_x, r.resolution_y, r.resolution_percentage, r.filepath,
         r.film_transparent, s.view_settings.view_transform, s.world, s.camera) = self._saved
        for obj in list(self.collection.objects):
            data = obj.data
            bpy.data.objects.remove(obj)
            if isinstance(data, bpy.types.Light):
                bpy.data.lights.remove(data)
            elif isinstance(data, bpy.types.Camera):
                bpy.data.cameras.remove(data)
        bpy.data.collections.remove(self.collection)
        bpy.data.worlds.remove(self.world)
        return False


def compose(tiles, cols, rows, tile, out):
    """Tiles row-major from the top-left into one PNG; None leaves a dark slot. Blender image rows start at the bottom."""
    sheet = np.zeros((rows * tile, cols * tile, 4), dtype=np.float32)
    sheet[..., :3] = 0.08
    sheet[..., 3] = 1.0
    for index, path in enumerate(tiles):
        if path is None:
            continue
        image = bpy.data.images.load(path)
        pixels = np.empty(tile * tile * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        bpy.data.images.remove(image)
        col, row = index % cols, index // cols
        y0 = (rows - 1 - row) * tile
        sheet[y0:y0 + tile, col * tile:(col + 1) * tile] = pixels.reshape(tile, tile, 4)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    result = bpy.data.images.new("bl_sheet", cols * tile, rows * tile, alpha=True)
    result.pixels.foreach_set(sheet.ravel())
    result.filepath_raw = out
    result.file_format = "PNG"
    result.save()
    bpy.data.images.remove(result)
    return out
```

`tools/bl/preview.py`:
```python
"""Standard eye-check sheet: front, right, back, top / three-quarter, wireframe, stats — one PNG."""
import os

import bpy

from .inspect import inspect
from .render import Stage, compose
from .scene import load
from .util import log, resolve, world_bounds

VIEWS = [("front", (0, -1, 0)), ("right", (1, 0, 0)), ("back", (0, 1, 0)), ("top", (0, -0.001, 1)),
         ("three_quarter", (1, -1, 0.7))]


def _stats_lines(stats, title):
    return "\n".join([
        title[:28],
        "tris {:,}  meshes {}".format(stats["tris"], stats["meshes"]),
        "materials {}  textures {}".format(len(stats["materials"]), len(stats["textures"])),
        "bones {}  clips {}".format(stats["bones"], len(stats["clips"])),
        "size {:.2f} x {:.2f} x {:.2f} m".format(*stats["size_m"]),
        "ngons {}  non-manifold {}".format(stats["ngons"], stats["non_manifold"]),
        "unapplied: {}".format(", ".join(stats["transforms_unapplied"])[:24] or "-"),
    ])


def _stats_tile(text, tile, path):
    """Renders the text in a throwaway Workbench scene (fast, no lights needed)."""
    scene = bpy.data.scenes.new("bl_stats")
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = scene.render.resolution_y = tile
    scene.render.resolution_percentage = 100
    scene.render.filepath = path
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "OBJECT"
    world = bpy.data.worlds.new("bl_stats")
    world.color = (0.05, 0.05, 0.06)
    scene.world = world
    curve = bpy.data.curves.new("bl_stats", "FONT")
    curve.body = text
    curve.size = 0.075
    label = bpy.data.objects.new("bl_stats", curve)
    label.location = (-0.46, 0.40, 0.0)
    label.color = (0.92, 0.92, 0.92, 1.0)
    camera = bpy.data.objects.new("bl_stats_cam", bpy.data.cameras.new("bl_stats_cam"))
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.0
    camera.location = (0.0, 0.0, 5.0)
    for obj in (label, camera):
        scene.collection.objects.link(obj)
    scene.camera = camera
    bpy.ops.render.render(write_still=True, scene=scene.name)
    for obj in (label, camera):
        data = obj.data
        bpy.data.objects.remove(obj)
        (bpy.data.curves if isinstance(data, bpy.types.Curve) else bpy.data.cameras).remove(data)
    bpy.data.worlds.remove(world)
    bpy.data.scenes.remove(scene)
    return path


def _wire_tile(stage, meshes, radius):
    """Three-quarter view of dark wire copies; the originals are hidden for this one render."""
    dark = bpy.data.materials.new("bl_wire")
    dark.diffuse_color = (0.02, 0.02, 0.02, 1.0)
    copies = []
    for obj in meshes:
        copy = obj.copy()
        copy.data = obj.data.copy()
        copy.data.materials.clear()
        copy.data.materials.append(dark)
        wire = copy.modifiers.new("bl_wire", "WIREFRAME")
        wire.thickness = radius * 0.004
        stage.collection.objects.link(copy)
        copies.append(copy)
    hidden = [(obj, obj.hide_render) for obj in meshes]
    for obj, _ in hidden:
        obj.hide_render = True
    try:
        return stage.shoot("wire", VIEWS[4][1])
    finally:
        for obj, state in hidden:
            obj.hide_render = state
        for copy in copies:
            data = copy.data
            bpy.data.objects.remove(copy)
            bpy.data.meshes.remove(data)
        bpy.data.materials.remove(dark)


def preview(path=None, out=".logs/preview.png", tile=512):
    if path:
        load(path)
    out = resolve(out)
    stats = inspect()
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bounds = world_bounds(meshes or [o for o in bpy.context.scene.objects if o.type == "ARMATURE"])
    if bounds is None:
        raise ValueError("nothing to preview: the scene has no meshes or armatures")
    lo, hi = bounds
    center, radius = (lo + hi) / 2, (hi - lo).length / 2
    with Stage(tile, center, radius) as stage:
        tiles = [stage.shoot(name, direction) for name, direction in VIEWS]
        tiles.append(_wire_tile(stage, meshes, radius))
    title = os.path.basename(path) if path else bpy.context.scene.name
    tiles.append(_stats_tile(_stats_lines(stats, title), tile, os.path.join(resolve(".logs/tiles"), "stats.png")))
    compose(tiles + [None], 4, 2, tile, out)
    log("preview: {}".format(out))
    return {"png": out, "tile": tile, "views": [v[0] for v in VIEWS] + ["wire", "stats"], "stats": stats}
```

`tools/bl/__init__.py`: add `from . import render`, `from .preview import preview`, and `"preview": preview` in `TOOLS`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `"$BM/tools/run.sh" test` → all `OK`.

- [ ] **Step 5: Look at the sheet** — Read `"$BM/.logs/preview test dir/cube.png"` with the image viewer tool. Expected: 4×2 grid, cube visible in 6 tiles (front/right/back/top/3-4/wire), stats text readable in slot 7, last slot dark. If the stats text is cut off or tiny, adjust `curve.size`/`label.location` and re-run.

- [ ] **Step 6: Commit** — `git add -A && git commit -q -m "feat: render stage and preview sheet (6 views + stats tile)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 4: `export` — engine-ready files + manifest + preview of the export

**Files:**
- Create: `tools/bl/export.py`
- Modify: `tools/bl/__init__.py`
- Test: `tests/test_export.py`

**Interfaces:**
- Consumes: `bl.util.{log, resolve, run_background}`; background tools `inspect` and `preview` (measured on the exported file, in a fresh Blender).
- Produces: `bl.export(out_dir, id, game, kind="prop", formats=("glb",), source=None, version=1, preview=True) -> manifest dict` (also written to `<out_dir>/manifest.json`); `bl.export.KINDS`. Applies rotation+scale on free-standing, non-skinned meshes before writing.

- [ ] **Step 1: Write the failing test** — `tests/test_export.py`:

```python
import json
import os
import shutil
import unittest

import bpy

import bl


class ExportTests(unittest.TestCase):
    def setUp(self):
        bl.new_scene()
        self.out = bl.util.resolve(".logs/export test/cube")  # space in the path on purpose
        shutil.rmtree(self.out, ignore_errors=True)

    def test_glb_manifest_and_applied_scale(self):
        bpy.ops.mesh.primitive_cube_add(size=1)
        bpy.context.object.scale = (2, 2, 2)
        m = bl.export(out_dir=self.out, id="cube", game="_test", kind="prop", source={"recipe": "tests"})
        self.assertTrue(os.path.isfile(os.path.join(self.out, "cube.glb")))
        self.assertTrue(os.path.isfile(os.path.join(self.out, "preview.png")))
        with open(os.path.join(self.out, "manifest.json"), encoding="utf-8") as fh:
            self.assertEqual(json.load(fh), m)
        self.assertEqual((m["id"], m["game"], m["kind"], m["version"]), ("cube", "_test", "prop", 1))
        self.assertEqual(m["files"], {"glb": "cube.glb", "fbx": None, "previews": ["preview.png"]})
        self.assertEqual((m["units"], m["up"]), ("m", "+Y"))
        self.assertEqual(m["stats"]["tris"], 12)
        self.assertEqual(m["stats"]["size_m"], [2.0, 2.0, 2.0])
        self.assertTrue(m["scale_applied"])
        self.assertTrue(m["checks"]["passed"])
        self.assertEqual(m["source"], {"recipe": "tests", "copy_provenance": None, "inputs": []})

    def test_texture_is_embedded(self):
        bpy.ops.mesh.primitive_plane_add()
        image = bpy.data.images.new("checker", 4, 4)
        image.generated_type = "COLOR_GRID"
        image.pack()
        material = bpy.data.materials.new("tex")
        texture = bl.util.node_tree(material).nodes.new("ShaderNodeTexImage")
        texture.image = image
        material.node_tree.links.new(texture.outputs["Color"], bl.util.principled(material).inputs["Base Color"])
        bpy.context.object.data.materials.append(material)
        m = bl.export(out_dir=self.out, id="plane", game="_test", preview=False)
        self.assertEqual(len(m["stats"]["textures"]), 1)
        self.assertEqual(m["files"]["previews"], [])

    def test_fbx_alongside_glb(self):
        bpy.ops.mesh.primitive_cube_add(size=1)
        m = bl.export(out_dir=self.out, id="cube", game="_test", formats=["glb", "fbx"], preview=False)
        self.assertTrue(os.path.isfile(os.path.join(self.out, "cube.fbx")))
        self.assertEqual(m["files"]["fbx"], "cube.fbx")

    def test_rejects_unknown_kind_and_format(self):
        with self.assertRaises(ValueError):
            bl.export(out_dir=self.out, id="x", game="_test", kind="building")
        with self.assertRaises(ValueError):
            bl.export(out_dir=self.out, id="x", game="_test", formats=["obj"])
```

- [ ] **Step 2: Run it to verify it fails** — `"$BM/tools/run.sh" test test_export.py` → ERROR `no attribute 'export'`.

- [ ] **Step 3: Implement** — `tools/bl/export.py`:

```python
"""Engine-ready export (glb and/or fbx) with manifest.json, measured and previewed from the exported file."""
import datetime
import json
import os

import bpy

from .util import log, resolve, run_background

KINDS = ("prop", "character", "animation", "vfx-mesh", "vfx-flipbook")
FORMATS = ("glb", "fbx")


def _apply_transforms():
    """Rotation and scale onto free-standing meshes (no parent, no armature); rigs belong to the rig pipeline."""
    targets = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.parent is None
               and not any(m.type == "ARMATURE" for m in o.modifiers)]
    if not targets:
        return []
    for obj in targets:
        if obj.data.users > 1:
            obj.data = obj.data.copy()
    with bpy.context.temp_override(selected_editable_objects=targets, selected_objects=targets,
                                   active_object=targets[0], object=targets[0]):
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return [o.name for o in targets]


def export(out_dir, id, game, kind="prop", formats=("glb",), source=None, version=1, preview=True):
    if kind not in KINDS:
        raise ValueError("kind must be one of {}, got '{}'".format(", ".join(KINDS), kind))
    formats = [f.lower() for f in formats]
    unknown = [f for f in formats if f not in FORMATS]
    if unknown or not formats:
        raise ValueError("formats must be a non-empty subset of {}, got {}".format(FORMATS, formats))
    out_dir = resolve(out_dir)
    os.makedirs(out_dir, exist_ok=True)
    _apply_transforms()
    files = {"glb": None, "fbx": None, "previews": []}
    if "glb" in formats:
        bpy.ops.export_scene.gltf(filepath=os.path.join(out_dir, id + ".glb"), export_format="GLB", export_yup=True,
                                  export_apply=True, export_animations=True, export_skins=True,
                                  export_image_format="AUTO", use_selection=False)
        files["glb"] = id + ".glb"
    if "fbx" in formats:
        bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, id + ".fbx"), use_selection=False,
                                 object_types={"ARMATURE", "MESH", "EMPTY"}, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                                 axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=True,
                                 mesh_smooth_type="FACE", path_mode="COPY", embed_textures=True)
        files["fbx"] = id + ".fbx"
    main = os.path.join(out_dir, files["glb"] or files["fbx"])
    measured = run_background("inspect", {"path": main})
    if preview:
        run_background("preview", {"path": main, "out": os.path.join(out_dir, "preview.png")})
        files["previews"] = ["preview.png"]
    checks = {key: measured[key] for key in ("non_manifold", "ngons", "uv_missing", "transforms_unapplied")}
    checks["passed"] = checks["non_manifold"] == 0 and not checks["transforms_unapplied"]
    origin = {"recipe": None, "copy_provenance": None, "inputs": []}
    origin.update(source or {})
    manifest = {
        "id": id, "game": game, "kind": kind, "version": version, "files": files,
        "units": "m", "up": "+Y", "scale_applied": not measured["transforms_unapplied"],
        "stats": {"tris": measured["tris"], "materials": len(measured["materials"]), "textures": measured["textures"],
                  "bones": measured["bones"], "clips": [c["name"] for c in measured["clips"]],
                  "size_m": measured["size_m"]},
        "source": origin, "checks": checks,
        "made": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
    log("export {}: {} tris, checks {} -> {}".format(id, measured["tris"], "ok" if checks["passed"] else "FAILED", out_dir))
    return manifest
```

`tools/bl/__init__.py`: add `from .export import export` and `"export": export`.

- [ ] **Step 4: Run the tests to verify they pass** — `"$BM/tools/run.sh" test` → all `OK`. Read `"$BM/.logs/export test/cube/preview.png"`: a 2 m cube in all views.

- [ ] **Step 5: Commit** — `git add -A && git commit -q -m "feat: export tool (glb/fbx + manifest.json + preview of the exported file)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 5: `anim_sheet` — animation frames + bone heights

**Files:**
- Create: `tools/bl/anim_sheet.py`
- Modify: `tools/bl/__init__.py`
- Test: `tests/test_anim_sheet.py`

**Interfaces:**
- Consumes: `bl.render.{Stage, compose}`, `bl.load`, `bl.util.{log, resolve, world_bounds}`.
- Produces: `bl.anim_sheet(path=None, out=".logs/anim_sheet.png", frames=8, tile=256, clip=None, bones=None) -> {"png", "clip", "frames": list[int], "bones": list[str], "heights_m": {bone: list[float]}}`; heights are world Z of each pose bone's head at each sampled frame.

- [ ] **Step 1: Write the failing test** — `tests/test_anim_sheet.py`:

```python
import os
import unittest

import bpy

import bl


class AnimSheetTests(unittest.TestCase):
    def setUp(self):
        bl.new_scene()

    def test_bone_heights_follow_the_clip(self):
        bpy.ops.object.armature_add(location=(0, 0, 0))  # 'Bone' from z 0 to z 1
        armature = bpy.context.object
        bpy.ops.object.mode_set(mode="POSE")
        bone = armature.pose.bones["Bone"]
        bone.location = (0, 0, 0)
        bone.keyframe_insert("location", frame=1)
        bone.location = (0, 1, 0)  # bone-local Y is world Z for this upright bone
        bone.keyframe_insert("location", frame=11)
        bpy.ops.object.mode_set(mode="OBJECT")
        r = bl.anim_sheet(out=".logs/test_anim.png", frames=3, tile=64, bones=["Bone"])
        self.assertEqual(r["frames"], [1, 6, 11])
        heights = r["heights_m"]["Bone"]
        self.assertAlmostEqual(heights[0], 0.0, places=3)
        self.assertAlmostEqual(heights[2], 1.0, places=3)
        self.assertTrue(os.path.isfile(r["png"]))

    def test_needs_an_armature(self):
        bpy.ops.mesh.primitive_cube_add()
        with self.assertRaises(ValueError):
            bl.anim_sheet(out=".logs/test_anim.png")
```

- [ ] **Step 2: Run it to verify it fails** — `"$BM/tools/run.sh" test test_anim_sheet.py` → ERROR `no attribute 'anim_sheet'`.

- [ ] **Step 3: Implement** — `tools/bl/anim_sheet.py`:

```python
"""Animation eye check: sampled frames of one clip (three-quarter view) + world heights of tracked bones."""
import math

import bpy

from .render import Stage, compose
from .scene import load
from .util import log, resolve, world_bounds

KEYWORDS = ("head", "hand", "foot", "hips")


def _default_bones(armature):
    names = [bone.name for bone in armature.pose.bones]
    picked = [n for n in names if any(k in n.lower() for k in KEYWORDS)]
    return picked[:8] or names[:6]


def anim_sheet(path=None, out=".logs/anim_sheet.png", frames=8, tile=256, clip=None, bones=None):
    if path:
        load(path)
    scene = bpy.context.scene
    armature = next((o for o in scene.objects if o.type == "ARMATURE"), None)
    if armature is None:
        raise ValueError("no armature in the scene")
    action = bpy.data.actions.get(clip) if clip else (armature.animation_data.action if armature.animation_data else None)
    if action is None:
        action = bpy.data.actions[0] if bpy.data.actions else None
    if action is None:
        raise ValueError("no animation clip found" + (" named '{}'".format(clip) if clip else ""))
    armature.animation_data_create()
    armature.animation_data.action = action
    start, end = (int(round(v)) for v in action.frame_range)
    count = max(1, min(int(frames), end - start + 1))
    sample = [int(round(start + i * (end - start) / max(count - 1, 1))) for i in range(count)]
    tracked = list(bones) if bones else _default_bones(armature)
    heights = {name: [] for name in tracked}
    meshes = [o for o in scene.objects if o.type == "MESH"]
    scene.frame_set(sample[0])
    lo, hi = world_bounds(meshes or [armature])
    center, radius = (lo + hi) / 2, (hi - lo).length / 2 * 1.3
    shots = []
    with Stage(tile, center, radius) as stage:
        for frame in sample:
            scene.frame_set(frame)
            for name in tracked:
                heights[name].append(round((armature.matrix_world @ armature.pose.bones[name].head).z, 4))
            shots.append(stage.shoot("frame_{:04d}".format(frame), (1, -1, 0.5)))
    cols = min(4, len(shots))
    out = compose(shots, cols, math.ceil(len(shots) / cols), tile, resolve(out))
    log("anim_sheet: {} frames of '{}', {} bones -> {}".format(len(sample), action.name, len(tracked), out))
    return {"png": out, "clip": action.name, "frames": sample, "bones": tracked, "heights_m": heights}
```

`tools/bl/__init__.py`: add `from .anim_sheet import anim_sheet` and `"anim_sheet": anim_sheet`.

- [ ] **Step 4: Run the tests to verify they pass** — `"$BM/tools/run.sh" test` → all `OK`.

- [ ] **Step 5: Commit** — `git add -A && git commit -q -m "feat: anim_sheet tool (frame grid + bone heights)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 6: `make` + recipes (stylized Pota hoop, hand-off cube)

**Files:**
- Create: `tools/bl/recipe.py`
- Create: `recipes/pota_hoop.py`
- Create: `recipes/handoff_cube.py`
- Modify: `tools/bl/__init__.py`
- Test: `tests/test_recipes.py`

**Interfaces:**
- Consumes: `bl.new_scene`, `bl.export`, `bl.util.{log, resolve, run_background, principled}`; each recipe module exposes `build(**params) -> list[bpy.types.Object]`.
- Produces: `bl.make(name, game, id, kind="prop", params=None, formats=("glb",), version=1) -> manifest dict + {"work": "assets/<game>/<id>/work.blend"}`; files in `assets/<game>/<id>/{work.blend, export/}`. In a running Blender `make` delegates to a background process (never saves over the open file).

- [ ] **Step 1: Write the failing test** — `tests/test_recipes.py`:

```python
import os
import unittest

import bl


class RecipeTests(unittest.TestCase):
    def test_pota_hoop(self):
        m = bl.make(name="pota_hoop", game="_test", id="hoop")
        self.assertTrue(m["checks"]["passed"], m["checks"])
        self.assertTrue(500 <= m["stats"]["tris"] <= 8000, m["stats"]["tris"])
        self.assertEqual(m["stats"]["materials"], 5)
        width, depth, height = m["stats"]["size_m"]
        self.assertAlmostEqual(width, 1.8, delta=0.05)
        self.assertTrue(1.2 <= height <= 1.6, height)
        self.assertEqual(m["source"]["recipe"], "recipes/pota_hoop.py")
        self.assertEqual(m["work"], "assets/_test/hoop/work.blend")
        self.assertTrue(os.path.isfile(bl.util.resolve("assets/_test/hoop/export/preview.png")))

    def test_handoff_cube(self):
        m = bl.make(name="handoff_cube", game="_test", id="handoff-cube")
        self.assertEqual(m["stats"]["tris"], 12)
        self.assertEqual(m["stats"]["size_m"], [1.0, 1.0, 1.0])

    def test_unknown_recipe(self):
        with self.assertRaises(ModuleNotFoundError):
            bl.make(name="no_such_recipe", game="_test", id="x")
```

- [ ] **Step 2: Run it to verify it fails** — `"$BM/tools/run.sh" test test_recipes.py` → ERROR `no attribute 'make'`.

- [ ] **Step 3: Implement** — `tools/bl/recipe.py`:

```python
"""Builds an asset from recipes/<name>.py, saves assets/<game>/<id>/work.blend and exports it with a manifest."""
import importlib
import os

import bpy

from .export import export
from .scene import new_scene
from .util import log, resolve, run_background


def make(name, game, id, kind="prop", params=None, formats=("glb",), version=1):
    args = {"name": name, "game": game, "id": id, "kind": kind, "params": params or {}, "formats": list(formats),
            "version": version}
    if not bpy.app.background:
        return run_background("make", args)  # never save over the file open in the UI
    module = importlib.reload(importlib.import_module("recipes." + name))
    new_scene()
    module.build(**(params or {}))
    folder = resolve(os.path.join("assets", game, id))
    os.makedirs(folder, exist_ok=True)
    work = os.path.join(folder, "work.blend")
    bpy.ops.wm.save_as_mainfile(filepath=work, check_existing=False)
    manifest = export(out_dir=os.path.join(folder, "export"), id=id, game=game, kind=kind, formats=formats,
                      source={"recipe": "recipes/{}.py".format(name), "params": params or {}}, version=version)
    log("make {}: {}".format(id, folder))
    return dict(manifest, work=os.path.relpath(work, resolve(".")))
```

`recipes/handoff_cube.py`:
```python
"""1 m cube standing on the ground with one flat material: the fixture for Unity's ai_import_asset test."""
import bpy

from bl.util import principled


def build():
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.5))
    cube = bpy.context.object
    cube.name = "handoff_cube"
    material = bpy.data.materials.new("handoff_cube")
    principled(material).inputs["Base Color"].default_value = (0.2, 0.5, 0.9, 1.0)
    cube.data.materials.append(material)
    return [cube]
```

`recipes/pota_hoop.py`:
```python
"""Stylized Pota basketball hoop: white board with red border and shooter's square, orange rim, white net
lattice, dark mount arm. Real proportions in metres; rim centre at the origin; the hoop faces -Y."""
import bmesh
import bpy

from bl.util import principled

COLORS = {
    "board": (0.96, 0.96, 0.94, 1.0),
    "frame": (0.86, 0.13, 0.11, 1.0),
    "rim": (0.98, 0.42, 0.07, 1.0),
    "net": (1.0, 1.0, 1.0, 1.0),
    "metal": (0.16, 0.18, 0.22, 1.0),
}
RIM_R = 0.2286  # 18 in inner diameter
BOARD_W, BOARD_H, BOARD_T = 1.8, 1.05, 0.05
GAP = 0.15  # board face to rim inner edge; the rim also sits 0.15 m above the board's bottom edge
LINE = 0.05  # painted line width


def _material(name):
    material = bpy.data.materials.new("pota_hoop_" + name)
    node = principled(material)
    node.inputs["Base Color"].default_value = COLORS[name]
    node.inputs["Roughness"].default_value = 0.35 if name == "metal" else 0.45
    material.diffuse_color = COLORS[name]
    return material


def _box(name, size, location, material, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("bevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3
    obj.data.materials.append(material)
    return obj


def _net(material):
    bpy.ops.mesh.primitive_cone_add(vertices=16, radius1=0.14, radius2=RIM_R, depth=0.42,
                                    end_fill_type="NOTHING", location=(0, 0, -0.21))
    net = bpy.context.object
    net.name = "net"
    bm = bmesh.new()
    bm.from_mesh(net.data)
    vertical = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 1e-4]
    bmesh.ops.subdivide_edges(bm, edges=vertical, cuts=3, use_grid_fill=True)
    bm.to_mesh(net.data)
    bm.free()
    wire = net.modifiers.new("lattice", "WIREFRAME")
    wire.thickness = 0.012
    wire.use_even_offset = True
    net.data.materials.append(material)
    return net


def build():
    mats = {name: _material(name) for name in COLORS}
    face_y = GAP + RIM_R
    board_z = -GAP + BOARD_H / 2
    line_y = face_y - 0.006
    objects = [_box("board", (BOARD_W, BOARD_T, BOARD_H), (0, face_y + BOARD_T / 2, board_z), mats["board"],
                    bevel=0.02)]
    for name, size, location in (
        ("border_top", (BOARD_W, 0.012, LINE), (0, line_y, board_z + BOARD_H / 2 - LINE / 2)),
        ("border_bottom", (BOARD_W, 0.012, LINE), (0, line_y, board_z - BOARD_H / 2 + LINE / 2)),
        ("border_left", (LINE, 0.012, BOARD_H), (-BOARD_W / 2 + LINE / 2, line_y, board_z)),
        ("border_right", (LINE, 0.012, BOARD_H), (BOARD_W / 2 - LINE / 2, line_y, board_z)),
        ("square_top", (0.59, 0.012, LINE), (0, line_y, 0.45 - LINE / 2)),
        ("square_bottom", (0.59, 0.012, LINE), (0, line_y, LINE / 2)),
        ("square_left", (LINE, 0.012, 0.45), (-0.295 + LINE / 2, line_y, 0.225)),
        ("square_right", (LINE, 0.012, 0.45), (0.295 - LINE / 2, line_y, 0.225)),
    ):
        objects.append(_box(name, size, location, mats["frame"]))
    bpy.ops.mesh.primitive_torus_add(major_radius=RIM_R + 0.01, minor_radius=0.018, major_segments=32,
                                     minor_segments=8, location=(0, 0, 0))
    rim = bpy.context.object
    rim.name = "rim"
    rim.data.materials.append(mats["rim"])
    bpy.ops.object.shade_smooth()
    objects.append(rim)
    objects.append(_box("rim_bracket", (0.12, GAP + 0.02, 0.03), (0, RIM_R + GAP / 2, -0.01), mats["rim"]))
    objects.append(_net(mats["net"]))
    objects.append(_box("mount_arm", (0.12, 0.6, 0.12), (0, face_y + BOARD_T + 0.3, board_z), mats["metal"]))
    return objects
```

`tools/bl/__init__.py`: add `from .recipe import make` and `"make": make`.

- [ ] **Step 4: Run the tests to verify they pass** — `"$BM/tools/run.sh" test` → all `OK`.

- [ ] **Step 5: Look at the hoop** — Read `"$BM/assets/_test/hoop/export/preview.png"`. Expected: white board with red border and square, orange rim in front of the board, white net lattice hanging from the rim, dark arm behind; no pink/black materials. Fix the recipe and re-run if something is off.

- [ ] **Step 6: Commit** — `git add -A && git commit -q -m "feat: make tool and recipes (stylized Pota hoop, hand-off cube)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 7: Official Blender MCP bridge

**Files:**
- Modify: Blender user extensions (add-on `mcp` 1.0.3 installed + enabled)
- Create: `~/.local/bin/blender-mcp` (via `uv tool install`)
- Modify: `~/.claude.json` (user-scope MCP server `blender`, through the Claude CLI)

**Interfaces:**
- Consumes: `bl` package (live check), `Tools/mcp_call.py` in Unity MCP (HTTP MCP client).
- Produces: MCP server `blender` (stdio) for new Claude Code sessions; add-on listening on `localhost:9876` whenever Blender's UI is open.

- [ ] **Step 1: Fetch the pinned source and build the add-on**

```bash
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/ddddd890-0b80-4935-84fd-734de99f5121/scratchpad"
B=/Applications/Blender.app/Contents/MacOS/Blender
rm -rf "$S/blender_mcp" && git clone -q --depth 1 --branch v1.0.3 https://projects.blender.org/lab/blender_mcp.git "$S/blender_mcp"
"$B" -c extension build --source-dir "$S/blender_mcp/addon/blender_mcp_addon" --output-dir "$S"
ls "$S"/mcp-1.0.3.zip
```
Expected: `mcp-1.0.3.zip` exists.

- [ ] **Step 2: Install and enable the add-on** — `"$B" -c extension install-file -r user_default --enable "$S/mcp-1.0.3.zip"` then `"$B" -c extension list | grep -i mcp` → shows `mcp` 1.0.3 enabled.

- [ ] **Step 3: Install the server** — `~/.local/bin/uv tool install --from "git+https://projects.blender.org/lab/blender_mcp.git@v1.0.3#subdirectory=mcp" blender-mcp` then `~/.local/bin/blender-mcp --help` → shows `--transport {stdio,http}`.

- [ ] **Step 4: Open Blender with the hoop and wait for the add-on socket**

```bash
open -na /Applications/Blender.app --args "/Users/tunaaltin17/projects/Blender MCP/assets/_test/hoop/work.blend"
for i in $(seq 1 40); do lsof -nP -iTCP:9876 -sTCP:LISTEN >/dev/null 2>&1 && { echo "addon listening"; break; }; sleep 1; done
```
Expected: `addon listening` (auto-start delay is a few seconds).

- [ ] **Step 5: Verify end-to-end through MCP** (HTTP transport only for this check; Claude uses stdio)

```bash
BLENDER_PATH=$B ~/.local/bin/blender-mcp --transport http --port 8765 > "$S/blender_mcp_http.log" 2>&1 &
SERVER=$!; sleep 3
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run --quiet Tools/mcp_call.py --url http://127.0.0.1:8765/mcp list | tail -30
~/.local/bin/uv run --quiet Tools/mcp_call.py --url http://127.0.0.1:8765/mcp describe get_screenshot_of_area_as_image
~/.local/bin/uv run --quiet Tools/mcp_call.py --url http://127.0.0.1:8765/mcp call get_blendfile_summary_datablocks '{}'
```
Expected: ~26 tools incl. `execute_blender_code`, `get_screenshot_of_area_as_image`; the datablock summary lists the hoop's objects (board, rim, net, …). Then call the screenshot tool with the arguments its schema asks for (a 3D viewport area) → image saved under `Temp/mcp_out/`; Read it: the hoop in Blender's viewport.

- [ ] **Step 6: Live safety check of `bl` inside the open Blender** — call `execute_blender_code` with:

```python
import sys
sys.path.insert(0, "/Users/tunaaltin17/projects/Blender MCP/tools")
import bpy, bl
before = sorted(s.name for s in bpy.data.scenes)
stats = bl.inspect()
bl.new_scene()
after = sorted(s.name for s in bpy.data.scenes)
print({"tris": stats["tris"], "before": before, "after": after, "file": bpy.data.filepath, "dirty_objects": len(bpy.data.objects)})
```
Expected: `tris` equals the hoop's count, `after` = `before` + one new scene (`bl`), `file` still `work.blend`, objects not deleted. Then switch back and remove the extra scene with `execute_blender_code`: `bpy.context.window.scene = bpy.data.scenes[before[0]]; bpy.data.scenes.remove(bpy.data.scenes["bl"])`. Stop the HTTP server: `kill $SERVER`.

- [ ] **Step 7: Register for Claude Code (user scope)**

```bash
CLAUDE_BIN=$(ls -d ~/.vscode/extensions/anthropic.claude-code-*/resources/native-binary/claude | sort -V | tail -1)
"$CLAUDE_BIN" mcp add --scope user blender -e BLENDER_PATH=/Applications/Blender.app/Contents/MacOS/Blender -- /Users/tunaaltin17/.local/bin/blender-mcp
"$CLAUDE_BIN" mcp get blender
```
Expected: `blender` listed with scope User, command `/Users/tunaaltin17/.local/bin/blender-mcp`. (Tools appear in the next Claude Code session.)

- [ ] **Step 8: Record** — append the bridge facts (tag, port, add-on id, server path, HTTP self-test command, live-safety result) to `"$BM/CLAUDE.md"` under a `## Köprü` heading and commit in the Blender MCP repo: `git commit -am "docs: bridge install notes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`.

---

### Task 8: Unity `ai_import_asset` (hand-off importer)

**Files:**
- Create: `/Users/tunaaltin17/projects/Unity MCP/Assets/_Project/Core/Editor/AITools/ImportAssetTool.cs`
- Create: `/Users/tunaaltin17/projects/Unity MCP/Tools/fixtures/handoff-cube/{handoff-cube.glb, manifest.json}` (copied from the Blender workshop)
- Test: `/Users/tunaaltin17/projects/Unity MCP/Assets/_Project/Core/Tests/EditMode/ImportAssetToolTests.cs`

**Interfaces:**
- Consumes: `AIToolsCommon.{GetString, EnsureFolder, Log}`; manifest schema from Task 4.
- Produces: `ImportAssetTool.Import(string manifestPath, string dest = null) -> ImportAssetTool.Result` with `id, folder, main, copied, manifestTris, unityTris, manifestBones, unityBones, manifestClips, unityClips, differences, matches`; MCP tool `ai_import_asset {manifest, dest?}`; `ImportAssetTool.KindFolders`.

- [ ] **Step 1: Make the fixture**

```bash
"$BM/tools/run.sh" make '{"name":"handoff_cube","game":"_fixtures","id":"handoff-cube"}'
mkdir -p "/Users/tunaaltin17/projects/Unity MCP/Tools/fixtures/handoff-cube"
cp "$BM/assets/_fixtures/handoff-cube/export/handoff-cube.glb" "$BM/assets/_fixtures/handoff-cube/export/manifest.json" \
   "/Users/tunaaltin17/projects/Unity MCP/Tools/fixtures/handoff-cube/"
```
(`Tools/` is outside `Assets/`, so Unity does not import the fixture.)

- [ ] **Step 2: Write the failing test** — `ImportAssetToolTests.cs`:

```csharp
using System;
using System.IO;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    public class ImportAssetToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__handoff_test__";
        private const string Dest = Root + "/cube";

        private static string Fixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-cube", "manifest.json"));

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void Import_CopiesFiles_AndMatchesManifest()
        {
            var r = ImportAssetTool.Import(Fixture, Dest);
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            Assert.AreEqual(12, r.unityTris);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(Dest + "/handoff-cube.glb"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(Dest + "/manifest.json"));
        }

        [Test]
        public void Reimport_KeepsGuid()
        {
            ImportAssetTool.Import(Fixture, Dest);
            string before = AssetDatabase.AssetPathToGUID(Dest + "/handoff-cube.glb");
            ImportAssetTool.Import(Fixture, Dest);
            Assert.AreEqual(before, AssetDatabase.AssetPathToGUID(Dest + "/handoff-cube.glb"));
        }

        [Test]
        public void UnknownKind_Throws()
        {
            string dir = Path.Combine(Path.GetTempPath(), "handoff_bad_kind");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "manifest.json");
            File.WriteAllText(path, "{\"id\":\"x\",\"game\":\"Pota\",\"kind\":\"building\",\"files\":{\"glb\":\"x.glb\"}}");
            Assert.Throws<ArgumentException>(() => ImportAssetTool.Import(path, Dest));
        }

        [Test]
        public void DefaultFolder_FollowsGameAndKind()
        {
            Assert.AreEqual("Props", ImportAssetTool.KindFolders["prop"]);
            Assert.AreEqual("Characters", ImportAssetTool.KindFolders["character"]);
        }
    }
}
```

- [ ] **Step 3: Compile and confirm it fails** — `refresh_unity {"mode":"force","scope":"all","compile":"request","wait_for_ready":true}` → `read_console {"types":["error"],"filter_text":"error CS"}` shows `ImportAssetTool` does not exist.

- [ ] **Step 4: Implement** — `ImportAssetTool.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Imports a Blender workshop hand-off (manifest.json next to a glb/fbx; ~/projects/Blender MCP/docs/studyo.md)
    /// into Games/&lt;Game&gt;/Art/&lt;Kind&gt;/&lt;id&gt;/ and compares what Unity imported with the manifest's numbers.
    /// Re-importing an id overwrites the files in place, so GUIDs and scene references survive.
    /// </summary>
    [McpForUnityTool("ai_import_asset", Description =
        "Import a Blender workshop hand-off. manifest: absolute path to manifest.json (the glb/fbx sits next to it). " +
        "Copies into Assets/_Project/Games/<game>/Art/<Kind>/<id>/ (dest overrides); re-import keeps GUIDs. " +
        "Then compares tris/bones/clip count with the manifest (matches=false lists the differences).")]
    public static class ImportAssetTool
    {
        public class Parameters
        {
            [ToolParameter("Absolute path to the hand-off manifest.json")] public string manifest { get; set; }
            [ToolParameter("Target folder; default Assets/_Project/Games/<game>/Art/<Kind>/<id>", Required = false)]
            public string dest { get; set; }
        }

        public static readonly Dictionary<string, string> KindFolders = new Dictionary<string, string>
        {
            { "prop", "Props" }, { "character", "Characters" }, { "animation", "Animations" },
            { "vfx-mesh", "VFX" }, { "vfx-flipbook", "VFX" },
        };

        public class Result
        {
            public string id, folder, main;
            public List<string> copied = new List<string>();
            public int manifestTris, unityTris, manifestBones, unityBones;
            public List<string> manifestClips = new List<string>(), unityClips = new List<string>();
            public List<string> differences = new List<string>();
            public bool matches => differences.Count == 0;
        }

        public static object HandleCommand(JObject p)
        {
            string manifest = AIToolsCommon.GetString(p, "manifest");
            if (string.IsNullOrEmpty(manifest)) return new ErrorResponse("'manifest' is required.");
            try
            {
                var r = Import(manifest, AIToolsCommon.GetString(p, "dest"));
                string msg = $"{r.id} -> {r.folder}: {r.unityTris} tris, {r.unityBones} bones, {r.unityClips.Count} clips, " +
                             (r.matches ? "matches the manifest" : "DIFFERS: " + string.Join("; ", r.differences));
                AIToolsCommon.Log("Import asset: " + msg);
                return new SuccessResponse(msg, new
                {
                    r.id, r.folder, r.main, r.copied, r.matches, r.differences, r.manifestTris, r.unityTris,
                    r.manifestBones, r.unityBones, r.manifestClips, r.unityClips,
                });
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is JsonException)
            {
                return new ErrorResponse(e.Message);
            }
        }

        public static Result Import(string manifestPath, string dest = null)
        {
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("No manifest at " + manifestPath);
            var m = JObject.Parse(File.ReadAllText(manifestPath));
            string id = Required(m, "id"), game = Required(m, "game"), kind = Required(m, "kind");
            if (!KindFolders.TryGetValue(kind, out string kindFolder))
                throw new ArgumentException($"Unknown kind '{kind}' (known: {string.Join(", ", KindFolders.Keys)}).");
            var files = m["files"] as JObject ?? throw new ArgumentException("manifest has no 'files'.");
            string glb = files.Value<string>("glb"), fbx = files.Value<string>("fbx");
            string mainName = !string.IsNullOrEmpty(glb) ? glb : fbx;
            if (string.IsNullOrEmpty(mainName)) throw new ArgumentException("manifest lists neither a glb nor an fbx.");

            var r = new Result { id = id, folder = dest ?? $"Assets/_Project/Games/{game}/Art/{kindFolder}/{id}" };
            AIToolsCommon.EnsureFolder(r.folder);
            string sourceDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            foreach (string name in new[] { glb, fbx, "manifest.json" }.Where(n => !string.IsNullOrEmpty(n)))
            {
                string source = name == "manifest.json" ? manifestPath : Path.Combine(sourceDir, name);
                if (!File.Exists(source)) throw new FileNotFoundException("Hand-off file missing: " + source);
                string target = $"{r.folder}/{name}";
                File.Copy(source, target, true); // in place: the existing .meta (GUID) stays
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                r.copied.Add(target);
            }
            r.main = $"{r.folder}/{mainName}";
            Measure(r);
            Compare(r, m["stats"] as JObject);
            return r;
        }

        private static void Measure(Result r)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(r.main);
            if (root == null) throw new IOException("Unity imported no model from " + r.main);
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            r.unityTris = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(skinned.Select(s => s.sharedMesh)).Where(mesh => mesh != null).Sum(Triangles);
            r.unityBones = skinned.SelectMany(s => s.bones).Where(b => b != null).Distinct().Count();
            r.unityClips = AssetDatabase.LoadAllAssetsAtPath(r.main).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name).OrderBy(n => n).ToList();
        }

        private static int Triangles(Mesh mesh)
        {
            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) total += (int)mesh.GetIndexCount(i) / 3;
            return total;
        }

        private static void Compare(Result r, JObject stats)
        {
            if (stats == null) { r.differences.Add("manifest has no 'stats'"); return; }
            r.manifestTris = stats.Value<int?>("tris") ?? -1;
            r.manifestBones = stats.Value<int?>("bones") ?? 0;
            r.manifestClips = (stats["clips"] as JArray)?.Select(t => t.ToString()).OrderBy(n => n).ToList()
                              ?? new List<string>();
            if (r.manifestTris != r.unityTris) r.differences.Add($"tris manifest {r.manifestTris} vs Unity {r.unityTris}");
            if (r.manifestBones != r.unityBones) r.differences.Add($"bones manifest {r.manifestBones} vs Unity {r.unityBones}");
            if (r.manifestClips.Count != r.unityClips.Count)
                r.differences.Add($"clips manifest {r.manifestClips.Count} vs Unity {r.unityClips.Count}");
        }

        private static string Required(JObject m, string key)
        {
            string value = m.Value<string>(key);
            if (string.IsNullOrEmpty(value)) throw new ArgumentException($"manifest has no '{key}'.");
            return value;
        }
    }
}
```

- [ ] **Step 5: Compile, run the tests** — `refresh_unity` (force/all/request/wait) → `read_console` `error CS` empty → `run_tests {"mode":"EditMode"}` → `get_test_job` → 38/38 pass (34 + 4 new).

- [ ] **Step 6: Commit (Unity MCP repo)**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP" && git add Assets/_Project/Core/Editor/AITools/ImportAssetTool.cs* \
  Assets/_Project/Core/Tests/EditMode/ImportAssetToolTests.cs* Tools/fixtures/handoff-cube && \
  git commit -q -m "feat: ai_import_asset reads Blender workshop hand-offs (manifest + glb/fbx)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Proof run (spec §8)

**Files:**
- Create: `"$BM"/assets/Pota/pota-hoop-stylized/{work.blend, export/*}`
- Create: `"$BM"/docs/reports/2026-09-29-player-rig-a.md` + `.png`
- Create: `Assets/_Project/Games/Pota/Art/Props/pota-hoop-stylized/*` (Unity MCP, via `ai_import_asset`)
- Create: `docs/screenshots/2026-09-29-blender-handoff-hoop.jpg` (Unity MCP)

**Interfaces:**
- Consumes: everything above.
- Produces: the evidence Tuna sees.

- [ ] **Step 1: Measure the existing hero rig**

```bash
RIG="/Users/tunaaltin17/projects/Unity MCP/Assets/_Project/Games/Pota/Art/Mesh/player_rig_a.glb"
"$BM/tools/run.sh" inspect "{\"path\":\"$RIG\"}"
mkdir -p "$BM/docs/reports" && "$BM/tools/run.sh" preview "{\"path\":\"$RIG\",\"out\":\"docs/reports/2026-09-29-player-rig-a.png\"}"
```
Read the PNG. Write `docs/reports/2026-09-29-player-rig-a.md` (Turkish): tris, bones, size, unapplied transforms, materials/textures, what the preview shows, and which numbers the model pipeline must fix (e.g. triangle budget for a mobile hero, the 0.01 armature scale).

- [ ] **Step 2: Make the stylized hoop for Pota** — `"$BM/tools/run.sh" make '{"name":"pota_hoop","game":"Pota","id":"pota-hoop-stylized"}'` → `checks.passed` true. Read `assets/Pota/pota-hoop-stylized/export/preview.png`.

- [ ] **Step 3: Import into Unity** — `Tools/unity_up.sh` (Unity MCP) then `ai_import_asset {"manifest":"/Users/tunaaltin17/projects/Blender MCP/assets/Pota/pota-hoop-stylized/export/manifest.json"}` → `matches the manifest`, folder `Assets/_Project/Games/Pota/Art/Props/pota-hoop-stylized`.

- [ ] **Step 4: Side by side in the real game** — Basket's hoops are built at runtime (`BasketRound` → `HoopColumnBuilder.Build`), so compare in Play Mode, where nothing is saved to the scene. `manage_scene load` Basket → `manage_editor play` → `execute_code`:

```csharp
var hoopType = System.Type.GetType("Project.Basket.Hoop, Pota.Runtime");
var hoop = (Component)UnityEngine.Object.FindAnyObjectByType(hoopType);
if (hoop == null) return "no hoop yet, retry in a second";
var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Games/Pota/Art/Props/pota-hoop-stylized/pota-hoop-stylized.glb");
var cam = Camera.main;
var spot = hoop.transform.position;
foreach (var dx in new[] { -2.4f, 2.4f, -1.6f, 1.6f })
{
    var candidate = hoop.transform.position + new Vector3(dx, 0f, 0f);
    var v = cam.WorldToViewportPoint(candidate);
    if (v.z > 0 && v.x > 0.1f && v.x < 0.9f && v.y > 0.1f && v.y < 0.9f) { spot = candidate; break; }
}
var copy = (GameObject)UnityEngine.Object.Instantiate(asset, spot, Quaternion.Euler(0f, 180f, 0f));
copy.name = "BlenderHandoffHoop";
return "hoop " + hoop.name + " at " + hoop.transform.position + ", Blender hoop at " + spot;
```

Screenshot (`manage_camera {"action":"screenshot","capture_source":"game_view","include_image":true,"output_folder":"Temp/mcp_out","screenshot_file_name":"blender_handoff_hoop"}`). Read it: both hoops visible, the Blender hoop not pink and upright; if it faces away, rerun with `Quaternion.identity`. Keep it: `Tools/keep_screenshot.sh Temp/mcp_out/blender_handoff_hoop.png 2026-09-29-blender-handoff-hoop`. `manage_editor stop` (the copy disappears with Play Mode; the Basket scene file is untouched).

- [ ] **Step 5: Full test pass** — Blender: `"$BM/tools/run.sh" test` all OK. Unity: EditMode 38/38, PlayMode 5/5.

- [ ] **Step 6: Commit both repos**

```bash
cd "$BM" && git add -A && git commit -q -m "feat: stylized Pota hoop hand-off and player rig report" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
cd "/Users/tunaaltin17/projects/Unity MCP" && git add -A Assets docs/screenshots && git commit -q -m "feat: first Blender hand-off in Pota (stylized hoop), side-by-side screenshot in the running game" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Docs, studio map, `copy` under git, memory

**Files:**
- Modify: `"$BM"/CLAUDE.md` (full)
- Create: `"$BM"/docs/studyo.md`
- Modify: `/Users/tunaaltin17/projects/Unity MCP/CLAUDE.md` (studio pointer, `ai_import_asset`)
- Modify: `/Users/tunaaltin17/projects/copy/README.md` (studio pointer)
- Create: `/Users/tunaaltin17/projects/copy/.git` (local repo)
- Create/Modify: memory `studio-pipeline.md` + `MEMORY.md`

- [ ] **Step 1: Blender MCP `CLAUDE.md`** (Turkish), sections: Amaç (atölye + stüdyo hedefi, spec linki) · Ortam (Blender yolu/sürümü, `BLENDER_EEVEE`, Python 3.13) · Çalışma döngüsü (araç → `preview` PNG'sine bak → sayılara bak → commit; önce arayüzsüz) · Asla yapma (açık dosyanın üstüne kaydetme, `.blend`'i elle düzenleme yok, `assets/` dışına oyun dosyası yazma, yapay zekâ çağrısı yok) · Araçlar tablosu (`new_scene`, `inspect`, `preview`, `export`, `anim_sheet`, `make`; imzalar ve çıktılar) · Tarif yazma kuralları (`build(**params)`, metre, +Z yukarı, tek kullanıcılı mesh, malzeme adları `<asset>_<parça>`) · Teslim sözleşmesi (manifest alanları, klasör düzeni, Unity/three.js tarafı) · Köprü (Task 7 notları) · Dersler (Blender 5.2 malzemeleri/dünyaları düğümlü gelir, sahne sıfırlama glTF'i korur, EEVEE ilk kare ~4 s / Workbench 0.04 s, `sys.exit` kodu Blender'dan geçer).

- [ ] **Step 2: `docs/studyo.md`** (Turkish): the three workshops + games table (yer, sorumluluk, araçlar), the pipeline for one asset (konsept/AI → copy · model/temizlik/önizleme → Blender · içe aktarma/görünüm/oyun → Unity veya three.js), the manifest contract (copy of spec §7 + kind→Unity folder table), who calls what (`tools/run.sh`, `ai_import_asset`, three.js: glb → `public/assets`), roadmap (spec §9).

- [ ] **Step 3: Pointers** — Unity MCP `CLAUDE.md`: in §1 "atölye + şablon" paragraph add "Stüdyo haritası (copy → Blender → Unity): `~/projects/Blender MCP/docs/studyo.md`."; in the AITools list add "`ai_import_asset` — Blender atölyesi teslimini (manifest + glb/fbx) `Games/<Oyun>/Art/<Tür>/<id>/`e alır, GUID korunur, üçgen/kemik/klip sayısını manifest'le karşılaştırır". `copy/README.md`: under the title add "> Stüdyo haritası (copy → Blender → Unity): `~/projects/Blender MCP/docs/studyo.md`."

- [ ] **Step 4: `copy` under local git** — `cd /Users/tunaaltin17/projects/copy && git init -q && git status --short | head -40` → confirm `.env`, `node_modules/`, `dist/`, `cikti/` are absent from the list; `du -sh $(git ls-files -o --exclude-standard | head -200) 2>/dev/null | sort -h | tail -5` → no file over ~5 MB (if one is, stop and ask Tuna); then `git add -A && git commit -q -m "chore: put the asset production service under version control" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`. No remote.

- [ ] **Step 5: Commit docs** — Blender MCP: `git add -A && git commit -q -m "docs: workshop guide and studio map" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`. Unity MCP: `git add CLAUDE.md && git commit -q -m "docs: studio map pointer and ai_import_asset" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`. copy: `git add README.md && git commit -q -m "docs: studio map pointer" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`.

- [ ] **Step 6: Memory** — write `studio-pipeline.md` (project memory: the three workshops and their paths, Blender bridge registered at user scope, roadmap order model → rig/animation → VFX → other games, quality references) and add its line to `MEMORY.md`.
