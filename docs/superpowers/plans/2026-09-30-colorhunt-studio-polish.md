# Color Hunt — "Better than Pota" Burst Candidates and Studio Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Color Hunt gets a look switch — Pota (today's 1:1 port) / Candy (A, stylised pop) / Shatter (B, 3D fracture) — plus a
studio polish layer shared by A and B (sticker flight to the HUD with arrival, counter roll and ✓ stamp; press feedback; board intro;
idle hint; ammo counter), with Blender-made flipbooks and a pre-fractured block, verified in a Pota | A | B video and moment sheets.

**Architecture:** Two new background-Blender tools in `~/projects/Blender MCP` (`vfx_flipbook`, `vfx_fracture`) hand off PNG
flipbooks and a fractured-pillow GLB through the existing manifest contract; Unity's `ai_import_asset` learns the flipbook kind. In
Unity, `ColorHuntGame` calls an `IBurstStyle` at the swell peaks (Pota = today's `ColorHuntFx`, unchanged; `CandyBurst`; `ShatterBurst`)
and, when the look is not Pota, drives small polish pieces (`StudioCurves` pure curves, `CollectFlight`, `AmmoCounter`, press/intro/hint
state on `BlockView`). Rhythm, rules and sounds stay Pota's; new sounds are synthesised.

**Tech Stack:** Unity 6000.6.2f1, URP 17.6 (Forward+, Render Graph), hand-written URP HLSL shaders, ParticleSystem (Shuriken), uGUI,
Unity Test Framework; Blender 5.2.2 LTS headless (`tools/run.sh`, EEVEE, bmesh, numpy), Python + Pillow via `uv`, ffmpeg
(`/opt/homebrew/bin/ffmpeg`).

**Spec:** `docs/superpowers/specs/2026-09-30-colorhunt-studio-polish-design.md` (approved by Tuna 2026-09-30: "onayladım").

## Global Constraints

- Rhythm unchanged in all three looks: tap swell 0.11 s, burst at the peak (0.0605 s), chain steps 0.15 s, drops, rotation; rules
  (`ColorHuntRules`) untouched. The Pota look is today's behaviour: every existing test stays green with `Look = Pota`.
- `ColorHuntSettings.Look` defaults to `Pota` (the winner becomes the default in a later polish round, not in this plan).
- Live particles + pieces per look ≤ 1.5 × Pota's pools: `BurstBudget.Max = 1026` (Pota pools 12+12+12+72+160+16+160+240 = 684).
- Sounds stay Pota's; new ones ("topla", "tamam") are synthesised in code. No paid calls, no AI services.
- `~/projects/Pota` is read-only (comparison recordings come from the scratch copy, as in the previous plan).
- Unity 6 / URP only: HLSL + URP ShaderLibrary, `UniversalForward` (+ `SRPDefaultUnlit` for outlines), `CBUFFER_START(UnityPerMaterial)`;
  Input System only; `FindAnyObjectByType`.
- Every runtime material comes from a template asset through `RuntimeMaterial.From(template, "shader")`; each new shader gets a template
  in `PotaMaterialTemplates` and a field in `ColorHuntSettings` (`RuntimeShaderBuildTests` must stay green).
- Never hand-edit `.unity/.prefab/.asset/.mat` YAML; never `mv`/`rm` assets (use `AssetDatabase`); no compile in Play Mode.
- Blender repo: tools are functions registered in `bl.TOOLS`, return JSON-able dicts and log one `[bl] …` line; new tool = failing test
  first (`tools/run.sh test <file>`); outputs under `assets/<game>/<id>/export/`; quote paths (`Blender MCP` has a space).
- Commits in English ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, per task, in each repo; never push.
- Texts Turkish (İyi!, Süper!, Harika!, Muhteşem!, CEPHANE, HEDEF).
- Deliverables: `docs/screenshots/2026-09-30-studio-*` (sheets, side video, close-ups).

## Review Focus

1. **Switching the look mid-round** (the record tool does it; a designer may do it in Play Mode) while particles and pieces are alive:
   the old style is cleared, no piece or flyer is orphaned, target counts stay exact. Pinned by `BurstStyleTests.SetLook_ActivatesTheNewStyle_AndClearsTheOldOne`
   (Task 5) and `ShatterPlayTests.Clear_ReturnsEveryPiece` (Task 11).
2. **Rotating the board while Shatter pieces still fall:** pieces live in the game root (the hoop's space), not the board, so they keep
   falling straight and bouncing off the frame; nothing throws. Pinned by `ShatterPlayTests.Rotate_WhilePiecesFall_TheyStayInTheFrame`.
3. **Many arrivals at once** (a 6+ chain after a tap): the staggered stream delivers every sticker exactly once, the count ends equal to
   the rules, the pitch streak caps. Pinned by `CollectFlightPlayTests.LongChain_EveryStickerArrives_CountMatchesRules_Candy` / `_Shatter` (Task 7).
4. **A new round while studio stickers fly:** late arrivals from the old round never touch the new counts. Pinned by
   `CollectFlightPlayTests.NewRound_WhileStickersFly_KeepsTheNewCounts` (Task 7).
5. **Missing Blender assets** (a settings field left empty, a mesh without its second UV set): the look falls back to Pota's burst with
   one warning; nothing throws. Pinned by `BurstStyleTests.MissingAssets_FallBackToPota` (Task 5) and
   `ShatterTests.PiecesWithoutFrontUv_AreNotReady` (Task 11).

## Decisions taken while planning (each is a ruling for the ledger)

1. **Flipbook hand-off has its own manifest shape.** `bl.export` needs a mesh and Unity's importer needs a glb/fbx, so `vfx_flipbook`
   writes `files: {glb: null, fbx: null, sheet: "<id>.png", previews: [...]}` plus a `flipbook` block (cols, rows, frames, frame_px,
   duration_s, linear), and `ImportAssetTool` learns `kind: vfx-flipbook` (copies the sheet, sets texture import, compares the sheet size).
2. **Smoke is a procedural volume, not a Mantaflow simulation.** Spec §2 B says "duman simülasyonundan"; spec §7 names the sim's
   slowness and sensitivity as the main risk. A noise-driven Principled Volume in EEVEE gives the same lit, soft puff deterministically
   in seconds. Cost if wrong: a Mantaflow recipe can replace `smoke_lit` later behind the same id.
3. **Fracture is our own Voronoi cut with bmesh.** Blender 5.2's Cell Fracture add-on is not installed (and `run.sh` uses
   `--factory-startup`). Each piece = the pillow bisected by the planes between its seed and every other seed, holes filled. Pieces of
   the bulged front are not strictly convex near the corners; spec says "dışbükey" — cost if wrong: none visible (pieces are closed).
4. **The shock ring's flipbook stores a screen-space normal** (R = x, G = y, A = mask) rendered with the Raw view transform and imported
   linear; the Unity shader offsets `_CameraOpaqueTexture` by it.
5. **Low-ammo pulse:** spec §3.5 says "hiçbir bloğa yetmeyince"; in the rules that state ends the round at once
   (`AfterSettle` → `End(false, "insufficient")`), so the pulse would never be seen. The counter pulses when the ammo covers less
   than twice the cheapest block (the last affordable tap) and stays red during the lose wait. Cost if wrong: one condition.
6. **Candy shards are one ParticleSystem with a vertex-coloured toon shader** (`Project/CandyShard`, toon ramp + inverted-hull outline)
   instead of five `Project/Toon` materials (one per colour): one system, one pool, tone per particle.
7. **Shatter pieces are pooled GameObjects in the game root** with a deterministic hand-written sim (gravity 12, bounces off the frame's
   inner edges ±4.4 and the glass at z 0.7, restitution 0.45) — no PhysX: deterministic for recordings, no colliders on the board.
8. **Pieces show the sticker:** the fracture keeps a planar front UV (UV0) and a front flag (UV1.x = 1 on original front faces);
   `Project/PotaPiece` draws the tone with main light + SH + probe reflection and overlays the item and number atlas cells at
   `BlockView`'s placement.

## How to run things

- Unity open with the bridge up (`Tools/unity_up.sh`). Compile + test: `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Project.Tests.Fixture ...]`
  (exit 2 on `error CS`).
- MCP: `~/.local/bin/uv run Tools/mcp_call.py call <tool> '<json>'`; custom tools via `execute_custom_tool`; menu items via
  `execute_menu_item {"menu_path": "..."}`.
- Blender: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh <tool> '<json>'`, tests `tools/run.sh test [test_vfx.py]`.
- `$S` = `/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/ddddd890-0b80-4935-84fd-734de99f5121/scratchpad`.
- Work on `main` in both repos, commit per task, never push.

## File map

| File | Responsibility | Task |
|---|---|---|
| Blender `tools/bl/vfx.py`, `tools/bl/__init__.py`, `tests/test_vfx.py`, `CLAUDE.md` | `vfx_flipbook` (4 recipes) and `vfx_fracture`, registered, tested, documented | 1–3 |
| `Core/Editor/AITools/ImportAssetTool.cs`, `Tools/fixtures/handoff-flipbook/*`, `Core/Tests/EditMode/ImportAssetToolTests.cs` | `vfx-flipbook` hand-offs | 4 |
| `Games/Pota/Editor/StudioImportTool.cs` (`ai_studio_import`), `Games/Pota/Art/VFX/<id>/*` | Import the five Blender hand-offs, wire settings | 4 |
| `Scripts/ColorHunt/ColorHuntSettings.cs` | `PuzzleLook`, studio assets, templates, feel numbers | 4, 5, 10–12 |
| `Scripts/ColorHunt/Bursts/{IBurstStyle,PotaBurstStyle,BurstBudget}.cs`, `ColorHuntGame.cs`, `ColorHuntFx.cs`, `ColorHuntJuice.cs`, `ColorHuntHint.cs` | Look switch, style seam, shared best-move helper | 5 |
| `Scripts/ColorHunt/Studio/StudioCurves.cs`, `PotaSounds.cs` | Pure polish curves; synth "topla"/"tamam" | 6 |
| `Scripts/ColorHunt/Studio/CollectFlight.cs`, `ColorHuntHud.cs` | Sticker flight, arrival, counter roll, ✓ stamp | 7 |
| `Scripts/ColorHunt/Studio/AmmoCounter.cs`, `BlockView.cs`, `ColorHuntGame.cs` | Press feedback, ghost cost, ammo roll and pulse | 8 |
| `ColorHuntGame.cs`, `BlockView.cs` | Board intro, idle hint | 9 |
| `Shaders/{PotaFlipbook,CandyShard}.shader`, `Scripts/ColorHunt/Bursts/{StudioParticles,StudioTextures,CandyBurst,CandyHud}.cs` | Candy (A) | 10 |
| `Shaders/PotaPiece.shader`, `Scripts/ColorHunt/Bursts/{ShatterDebris,ShatterBurst}.cs`, `ColorHuntCamera.cs` | Shatter (B), camera push/zoom | 11 |
| `Shaders/PotaShockRing.shader`, `ShatterBurst.cs` | Shock ring distortion | 12 |
| `Editor/ColorHuntRecordTool.cs`, `Tools/potalook/{compare.py,test_compare.py,side_video.py,test_side_video.py}` | Recordings, Pota \| A \| B sheets and video, close-ups | 13 |
| `Games/Pota/README.md`, `CLAUDE.md`, Blender `CLAUDE.md` + `docs/studyo.md`, backlog, memory | Docs | 14 |

(`Games/…`, `Core/…`, `Scripts/…`, `Shaders/…`, `Editor/…` are under `Assets/_Project/` and `Assets/_Project/Games/Pota/`.)

New tests: Blender `tests/test_vfx.py`; Unity EditMode `StudioAssetsTests`, `BurstStyleTests`, `StudioCurvesTests`, `ShatterTests`,
`CandyTests`, `ColorHuntCameraTests`; PlayMode `StudioLookPlayTests`, `CollectFlightPlayTests`, `StudioPolishPlayTests`,
`CandyPlayTests`, `ShatterPlayTests`; Python `test_side_video.py`; updated: `ImportAssetToolTests`, `PotaSoundsTests`,
`ColorHuntRecordToolTests`, `test_compare.py`, `RuntimeShaderBuildTests` (unchanged code, new shaders covered).

---

### Task 1: Blender — `vfx_flipbook` with the cartoon puff and ring

**Repo:** `/Users/tunaaltin17/projects/Blender MCP` (quote the path: it has a space).

**Files:**
- Create: `tools/bl/vfx.py`
- Modify: `tools/bl/__init__.py` (import + `TOOLS`)
- Test: `tests/test_vfx.py`
- Output (committed): `assets/Pota/vfx-puff-toon/export/{vfx-puff-toon.png, preview.png, manifest.json}`,
  `assets/Pota/vfx-ring-toon/export/{…}`

**Interfaces:**
- Produces: `bl.vfx_flipbook(recipe, game, id, frames=16, cols=4, frame_px=256, duration=0.35, seed=7, out_dir=None) -> dict`
  (the manifest). Manifest shape (Task 4 consumes it):
  ```json
  {"id": "...", "game": "Pota", "kind": "vfx-flipbook", "version": 1,
   "files": {"glb": null, "fbx": null, "sheet": "<id>.png", "previews": ["preview.png"]},
   "flipbook": {"cols": 4, "rows": 4, "frames": 16, "frame_px": 256, "duration_s": 0.35, "linear": false},
   "units": "px", "up": "+Y", "scale_applied": true,
   "stats": {"tris": 0, "materials": 0, "textures": [{"name": "<id>.png", "size": [1024, 1024]}], "bones": 0, "clips": [], "size_m": [0, 0, 0]},
   "source": {"recipe": "vfx_flipbook:<recipe>", "copy_provenance": null, "inputs": [], "params": {"seed": 7, "frames": 16}},
   "checks": {"frames_empty": [], "coverage": [...], "passed": true}, "made": "…Z"}
  ```
- Produces: `bl.vfx.RECIPES` (dict keyed by recipe name; Task 2 adds `smoke_lit`, `shock_ring`).

- [ ] **Step 1: Write the failing tests**

`tests/test_vfx.py`:
```python
import json
import os
import shutil
import unittest

import bpy
import numpy as np

import bl


def _pixels(path):
    """RGBA float pixels, rows from the bottom (Blender's order)."""
    image = bpy.data.images.load(path)
    w, h = image.size
    px = np.empty(w * h * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    bpy.data.images.remove(image)
    return px.reshape(h, w, 4)


def _frame(sheet, index, cols, rows, tile):
    """Frame `index` (row-major from the top-left) of a sheet in Blender row order."""
    col, row = index % cols, index // cols
    y0 = (rows - 1 - row) * tile
    return sheet[y0:y0 + tile, col * tile:(col + 1) * tile]


def _width(frame):
    """Drawn width in pixels: columns that hold any pixel above half alpha."""
    cols = np.where((frame[..., 3] > 0.5).any(axis=0))[0]
    return 0 if cols.size == 0 else int(cols[-1] - cols[0] + 1)


class FlipbookTests(unittest.TestCase):
    def setUp(self):
        self.out = bl.util.resolve(".logs/vfx test/flipbook")  # space in the path on purpose
        shutil.rmtree(self.out, ignore_errors=True)

    def _make(self, recipe, frame_px=64):
        return bl.vfx_flipbook(recipe=recipe, game="_test", id="fb-" + recipe, frames=4, cols=2, frame_px=frame_px,
                               out_dir=self.out)

    def test_puff_sheet_is_transparent_inked_and_every_frame_drawn(self):
        m = self._make("puff_toon", frame_px=128)
        sheet = _pixels(os.path.join(self.out, "fb-puff_toon.png"))
        self.assertEqual(sheet.shape, (256, 256, 4))
        self.assertEqual(float(sheet[..., 3].min()), 0.0, "the background must be transparent")
        self.assertEqual(m["checks"]["frames_empty"], [])
        self.assertTrue(m["checks"]["passed"])
        covered = sheet[sheet[..., 3] > 0.9][:, :3]
        self.assertLess(float(covered.min()), 0.3, "no dark outline")
        self.assertGreater(float(covered.max()), 0.9, "no lit white")

    def test_manifest_is_a_flipbook_handoff(self):
        m = self._make("ring_toon")
        with open(os.path.join(self.out, "manifest.json"), encoding="utf-8") as fh:
            self.assertEqual(json.load(fh), m)
        self.assertEqual((m["id"], m["game"], m["kind"], m["version"]), ("fb-ring_toon", "_test", "vfx-flipbook", 1))
        self.assertEqual(m["files"], {"glb": None, "fbx": None, "sheet": "fb-ring_toon.png", "previews": ["preview.png"]})
        self.assertEqual(m["flipbook"], {"cols": 2, "rows": 2, "frames": 4, "frame_px": 64, "duration_s": 0.35, "linear": False})
        self.assertEqual(m["stats"]["textures"], [{"name": "fb-ring_toon.png", "size": [128, 128]}])
        self.assertEqual(m["source"]["recipe"], "vfx_flipbook:ring_toon")
        self.assertTrue(os.path.isfile(os.path.join(self.out, "preview.png")))

    def test_ring_grows(self):
        self._make("ring_toon")
        sheet = _pixels(os.path.join(self.out, "fb-ring_toon.png"))
        self.assertLess(_width(_frame(sheet, 0, 2, 2, 64)), _width(_frame(sheet, 2, 2, 2, 64)))

    def test_unknown_recipe_is_refused(self):
        with self.assertRaises(ValueError):
            bl.vfx_flipbook(recipe="fire", game="_test", id="x", out_dir=self.out)
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test test_vfx.py`
Expected: 4 errors, `AttributeError: module 'bl' has no attribute 'vfx_flipbook'`.

- [ ] **Step 3: Write `tools/bl/vfx.py`**

```python
"""VFX hand-offs for the game engines: flipbook sprite sheets rendered from recipes (vfx_flipbook) and, from Task 3, a
pre-fractured pillow block (vfx_fracture). A flipbook has its own manifest shape (files.sheet + a flipbook block):
bl.export needs a mesh, and a sheet is not one."""
import datetime
import json
import math
import os
import random

import bpy
import numpy as np
from mathutils import Vector

from .scene import new_scene
from .util import log, node_tree, resolve, run_background

OUTLINE = (0.078, 0.106, 0.18, 1.0)  # #141b2e, the cartoon ink
DRAWN_ALPHA = 0.05                    # a pixel is drawn above this alpha
DRAWN_SHARE = 0.005                   # a frame is drawn above this share of drawn pixels


def _ease_out(u):
    return 1.0 - (1.0 - u) ** 3


def _smooth(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def _stage(frame_px, ortho, view_transform, sun_energy=3.0):
    """Transparent EEVEE stage: orthographic camera at −Y looking +Y (the workshop's front), one sun from the upper left front."""
    scene = bpy.context.scene
    r = scene.render
    r.engine = "BLENDER_EEVEE"
    r.resolution_x = r.resolution_y = frame_px
    r.resolution_percentage = 100
    r.film_transparent = True
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = view_transform
    scene.view_settings.look = "None"
    scene.eevee.taa_render_samples = 16
    world = bpy.data.worlds.new("vfx_world")
    background = next(n for n in node_tree(world).nodes if n.type == "BACKGROUND")
    background.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    background.inputs["Strength"].default_value = 0.0
    scene.world = world
    camera_data = bpy.data.cameras.new("vfx_cam")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = ortho
    camera = bpy.data.objects.new("vfx_cam", camera_data)
    camera.location = (0.0, -6.0, 0.0)
    camera.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    scene.collection.objects.link(camera)
    scene.camera = camera
    sun = bpy.data.objects.new("vfx_sun", bpy.data.lights.new("vfx_sun", "SUN"))
    sun.data.energy = sun_energy
    sun.rotation_euler = Vector((0.55, 0.6, -0.6)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun)
    return scene


def _toon_material(name, shade=(0.72, 0.77, 0.88), lit=(1.0, 1.0, 1.0), threshold=0.45):
    """Two flat tones: Diffuse → Shader to RGB → constant ramp → Emission."""
    material = bpy.data.materials.new(name)
    tree = node_tree(material)
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    diffuse = tree.nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = tree.nodes.new("ShaderNodeShaderToRGB")
    ramp = tree.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (*shade, 1.0)
    ramp.color_ramp.elements[1].position = threshold
    ramp.color_ramp.elements[1].color = (*lit, 1.0)
    emission = tree.nodes.new("ShaderNodeEmission")
    tree.links.new(diffuse.outputs["BSDF"], to_rgb.inputs["Shader"])
    tree.links.new(to_rgb.outputs["Color"], ramp.inputs["Fac"])
    tree.links.new(ramp.outputs["Color"], emission.inputs["Color"])
    tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    return material


def _outline_material():
    """Inverted-hull ink: flat dark emission, back faces culled so only the silhouette shell shows."""
    material = bpy.data.materials.new("vfx_outline")
    tree = node_tree(material)
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = OUTLINE
    tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    material.use_backface_culling = True
    return material


def _inked(obj, width):
    """Toon body + a flipped solidify shell `width` m thick with the ink material."""
    obj.data.materials.append(_toon_material("vfx_toon"))
    obj.data.materials.append(_outline_material())
    for poly in obj.data.polygons:
        poly.use_smooth = True
    shell = obj.modifiers.new("vfx_outline", "SOLIDIFY")
    shell.thickness = width
    shell.offset = 1.0
    shell.use_flip_normals = True
    shell.material_offset = 1
    return obj


def _mesh_from(obj):
    """A metaball object → a real mesh object (modifiers need one); the metaball is removed."""
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
    data = obj.data
    bpy.data.objects.remove(obj)
    bpy.data.metaballs.remove(data)
    solid = bpy.data.objects.new("vfx_mesh", mesh)
    bpy.context.scene.collection.objects.link(solid)
    return solid


def _puff_toon(u, seed):
    """Cartoon cloud: seven balls and a core grow out fast, drift up and melt away one by one (A's "puf")."""
    rng = random.Random(seed)
    ball = bpy.data.metaballs.new("vfx_puff")
    ball.resolution = ball.render_resolution = 0.025
    ball.threshold = 0.6
    grow = _ease_out(min(1.0, u / 0.55))
    for k in range(7):
        angle = 2.0 * math.pi * k / 7.0 + rng.uniform(-0.3, 0.3)
        spread = (0.12 + 0.34 * grow) * rng.uniform(0.8, 1.15)
        fade = 1.0 - _smooth(0.45 + 0.25 * rng.random(), 1.0, u)
        size = 0.26 + 0.1 * rng.random()
        element = ball.elements.new()
        element.co = (math.cos(angle) * spread, 0.0, math.sin(angle) * spread + 0.12 * u)
        element.radius = max(0.0, size * (0.55 + 0.75 * grow) * fade)
    core = ball.elements.new()
    core.co = (0.0, 0.0, 0.1 * u)
    core.radius = max(0.0, 0.34 * (0.6 + 0.6 * grow) * (1.0 - _smooth(0.35, 0.85, u)))
    obj = bpy.data.objects.new("vfx_puff", ball)
    bpy.context.scene.collection.objects.link(obj)
    return [_mesh_from(obj)]


def _ring_toon(u, seed):
    """Stylised ring: grows fast and thins out (A's chain wave)."""
    major = 0.2 + 0.8 * _ease_out(u)
    minor = 0.1 * (1.0 - u) ** 0.8 + 0.006
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=72, minor_segments=16,
                                     rotation=(math.radians(90.0), 0.0, 0.0))
    return [bpy.context.object]


RECIPES = {
    "puff_toon": {"build": _puff_toon, "ortho": 2.4, "view": "Standard", "linear": False, "ink": 0.035},
    "ring_toon": {"build": _ring_toon, "ortho": 2.4, "view": "Standard", "linear": False, "ink": 0.022},
}


def _tile(path, tile):
    image = bpy.data.images.load(path)
    pixels = np.empty(tile * tile * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)
    return pixels.reshape(tile, tile, 4)


def _compose(tiles, cols, rows, tile):
    """Frames row-major from the top-left on a transparent sheet (Blender image rows start at the bottom)."""
    sheet = np.zeros((rows * tile, cols * tile, 4), dtype=np.float32)
    for index, pixels in enumerate(tiles):
        col, row = index % cols, index // cols
        y0 = (rows - 1 - row) * tile
        sheet[y0:y0 + tile, col * tile:(col + 1) * tile] = pixels
    return sheet


def _save(pixels, path):
    h, w = pixels.shape[:2]
    image = bpy.data.images.new("vfx_sheet", w, h, alpha=True)
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def _flatten(sheet, grey):
    """The sheet over a flat grey for the preview (alpha drawn in)."""
    flat = sheet.copy()
    alpha = sheet[..., 3:4]
    flat[..., :3] = sheet[..., :3] * alpha + grey * (1.0 - alpha)
    flat[..., 3] = 1.0
    return flat


def vfx_flipbook(recipe, game, id, frames=16, cols=4, frame_px=256, duration=0.35, seed=7, out_dir=None):
    if not bpy.app.background:
        return run_background("vfx_flipbook", {"recipe": recipe, "game": game, "id": id, "frames": frames, "cols": cols,
                                               "frame_px": frame_px, "duration": duration, "seed": seed, "out_dir": out_dir})
    if recipe not in RECIPES:
        raise ValueError("recipe must be one of {}, got '{}'".format(", ".join(sorted(RECIPES)), recipe))
    spec = RECIPES[recipe]
    rows = int(math.ceil(frames / float(cols)))
    out_dir = resolve(out_dir or "assets/{}/{}/export".format(game, id))
    os.makedirs(out_dir, exist_ok=True)
    frames_dir = resolve(".logs/vfx/{}".format(id))
    os.makedirs(frames_dir, exist_ok=True)
    tiles = []
    for index in range(frames):
        new_scene()
        scene = _stage(frame_px, spec["ortho"], spec["view"], spec.get("sun", 3.0))
        objects = spec["build"](index / float(max(1, frames - 1)), seed)
        if spec.get("ink"):
            for obj in objects:
                _inked(obj, spec["ink"])
        path = os.path.join(frames_dir, "f_{:02d}.png".format(index))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        tiles.append(_tile(path, frame_px))
    sheet = _compose(tiles, cols, rows, frame_px)
    _save(sheet, os.path.join(out_dir, id + ".png"))
    _save(_flatten(sheet, 0.14), os.path.join(out_dir, "preview.png"))
    coverage = [float((t[..., 3] > DRAWN_ALPHA).mean()) for t in tiles]
    empty = [i for i, c in enumerate(coverage[:-1]) if c < DRAWN_SHARE]  # the last frame may have faded out
    size = [cols * frame_px, rows * frame_px]
    manifest = {
        "id": id, "game": game, "kind": "vfx-flipbook", "version": 1,
        "files": {"glb": None, "fbx": None, "sheet": id + ".png", "previews": ["preview.png"]},
        "flipbook": {"cols": cols, "rows": rows, "frames": frames, "frame_px": frame_px, "duration_s": duration,
                     "linear": spec["linear"]},
        "units": "px", "up": "+Y", "scale_applied": True,
        "stats": {"tris": 0, "materials": 0, "textures": [{"name": id + ".png", "size": size}], "bones": 0, "clips": [],
                  "size_m": [0, 0, 0]},
        "source": {"recipe": "vfx_flipbook:" + recipe, "copy_provenance": None, "inputs": [],
                   "params": {"seed": seed, "frames": frames}},
        "checks": {"frames_empty": empty, "coverage": [round(c, 4) for c in coverage], "passed": not empty},
        "made": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
    log("vfx_flipbook {} ({}): {} frames on {}x{} px, empty {} -> {}".format(id, recipe, frames, size[0], size[1], empty,
                                                                            out_dir))
    return manifest
```

`tools/bl/__init__.py`: add the import line after `from .texture import bake_colors, palette`:
```python
from .vfx import vfx_flipbook
```
and add to `TOOLS` (after `"character_model": character_model`):
```python
           "vfx_flipbook": vfx_flipbook}
```
(the closing brace moves to the new last entry).

- [ ] **Step 4: Run the tests**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test test_vfx.py`
Expected: `Ran 4 tests … OK`. If the outline test fails with a min around 0.4, the ink is thinner than a pixel at 128 px: the ink
width in the recipe is in metres at ortho 2.4; do not weaken the test — check `_inked` was applied (two material slots, SOLIDIFY
modifier) and that `use_backface_culling` is on.

- [ ] **Step 5: Render Pota's two flipbooks and look at them**

```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
tools/run.sh vfx_flipbook '{"recipe": "puff_toon", "game": "Pota", "id": "vfx-puff-toon", "duration": 0.35}'
tools/run.sh vfx_flipbook '{"recipe": "ring_toon", "game": "Pota", "id": "vfx-ring-toon", "duration": 0.4}'
```
Expected: two `[bl] vfx_flipbook …: 16 frames on 1024x1024 px, empty [] -> …` lines.
Open `assets/Pota/vfx-puff-toon/export/preview.png` and `assets/Pota/vfx-ring-toon/export/preview.png` with the Read tool and check:
the puff reads as a white cartoon cloud with a two-tone shade and a dark ink line, grows in the first ~9 frames and melts away
ball by ball; the ring is a clean inked ring growing and thinning. A visible flaw (lumpy cloud, broken ink, clipped frames) is
fixed in the recipe numbers before committing (quality rule), with a ledger ruling naming the number changed.

- [ ] **Step 6: Full suite and commit**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test` → Expected: all pass (69 before + 4).
```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
git add tools/bl/vfx.py tools/bl/__init__.py tests/test_vfx.py assets/Pota/vfx-puff-toon assets/Pota/vfx-ring-toon
git commit -m "feat: vfx_flipbook — transparent EEVEE flipbooks with their own hand-off manifest; cartoon puff and ring

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Blender — lit smoke and the shock-ring normal flipbooks

**Repo:** `/Users/tunaaltin17/projects/Blender MCP`.

**Files:**
- Modify: `tools/bl/vfx.py` (two recipes)
- Test: `tests/test_vfx.py`
- Output (committed): `assets/Pota/vfx-smoke-lit/export/*`, `assets/Pota/vfx-shock-ring/export/*`

**Interfaces:**
- Consumes: `vfx_flipbook`, `RECIPES`, `_stage`, `_ease_out`, `node_tree` (Task 1).
- Produces: recipes `smoke_lit` (sRGB, soft lit volume puff) and `shock_ring` (`linear: true`; R = screen x normal·0.5+0.5,
  G = screen y normal·0.5+0.5, A = ring coverage).

- [ ] **Step 1: Write the failing tests** (append to `tests/test_vfx.py`)

```python
class LitFlipbookTests(unittest.TestCase):
    def setUp(self):
        self.out = bl.util.resolve(".logs/vfx test/lit")
        shutil.rmtree(self.out, ignore_errors=True)

    def test_smoke_is_soft_and_lit_from_one_side(self):
        m = bl.vfx_flipbook(recipe="smoke_lit", game="_test", id="smoke", frames=4, cols=2, frame_px=64, out_dir=self.out)
        self.assertEqual(m["checks"]["frames_empty"], [])
        frame = _frame(_pixels(os.path.join(self.out, "smoke.png")), 1, 2, 2, 64)
        drawn = frame[frame[..., 3] > 0.05]
        self.assertLess(float(drawn[:, 3].mean()), 0.9, "smoke must be soft, not a solid ball")
        left, right = frame[:, :32], frame[:, 32:]
        lit = lambda half: float((half[..., :3].mean(axis=2) * half[..., 3]).sum() / max(1e-6, half[..., 3].sum()))
        self.assertGreater(abs(lit(left) - lit(right)), 0.02, "one side must be lit more than the other")

    def test_shock_ring_stores_screen_normals_linearly(self):
        m = bl.vfx_flipbook(recipe="shock_ring", game="_test", id="shock", frames=4, cols=2, frame_px=64, out_dir=self.out)
        self.assertTrue(m["flipbook"]["linear"])
        frame = _frame(_pixels(os.path.join(self.out, "shock.png")), 1, 2, 2, 64)
        self.assertEqual(float(frame[32, 32, 3]), 0.0, "the ring's centre is a hole")
        ring = frame[frame[..., 3] > 0.9]
        self.assertLess(float(ring[:, 0].min()), 0.3, "left side: normal x < 0")
        self.assertGreater(float(ring[:, 0].max()), 0.7, "right side: normal x > 0")
        self.assertLess(float(ring[:, 1].min()), 0.3)
        self.assertGreater(float(ring[:, 1].max()), 0.7)
```

- [ ] **Step 2: Run to see them fail**

Run: `tools/run.sh test test_vfx.py` → Expected: 2 errors, `ValueError: recipe must be one of puff_toon, ring_toon, got 'smoke_lit'`.

- [ ] **Step 3: Add the recipes** (in `tools/bl/vfx.py`, before `RECIPES`, and extend `RECIPES`)

```python
def _volume_settings(scene):
    """EEVEE volumetrics fine enough for a 256 px smoke tile (names guarded: they moved between Blender versions)."""
    eevee = scene.eevee
    for name, value in (("volumetric_tile_size", "2"), ("volumetric_samples", 64), ("volumetric_start", 0.1),
                        ("volumetric_end", 20.0), ("use_volumetric_shadows", True), ("volumetric_shadow_samples", 16)):
        if hasattr(eevee, name):
            setattr(eevee, name, value)


def _smoke_lit(u, seed):
    """Soft lit smoke (B): a noise-driven Principled Volume in a growing sphere, rising and thinning out.
    Procedural instead of a Mantaflow sim: deterministic and seconds per frame (plan decision 2)."""
    scene = bpy.context.scene
    _volume_settings(scene)
    scene.objects["vfx_sun"].data.energy = 5.0
    grow = _ease_out(u)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1.0)
    obj = bpy.context.object
    s = 0.42 + 0.62 * grow
    obj.scale = (s, s, s)
    obj.location = (0.0, 0.0, 0.18 * u)
    material = bpy.data.materials.new("vfx_smoke")
    tree = node_tree(material)
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    volume = tree.nodes.new("ShaderNodeVolumePrincipled")
    volume.inputs["Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    volume.inputs["Anisotropy"].default_value = 0.3
    coord = tree.nodes.new("ShaderNodeTexCoord")
    mapping = tree.nodes.new("ShaderNodeMapping")
    mapping.inputs["Location"].default_value = (0.37 * seed, 0.0, -0.8 * u)
    noise = tree.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 2.6
    noise.inputs["Detail"].default_value = 6.0
    noise.inputs["Distortion"].default_value = 0.35
    falloff = tree.nodes.new("ShaderNodeTexGradient")
    falloff.gradient_type = "SPHERICAL"
    lift = tree.nodes.new("ShaderNodeMath")
    lift.operation = "SUBTRACT"
    lift.use_clamp = True
    lift.inputs[1].default_value = 0.38
    shape = tree.nodes.new("ShaderNodeMath")
    shape.operation = "MULTIPLY"
    density = tree.nodes.new("ShaderNodeMath")
    density.operation = "MULTIPLY"
    density.inputs[1].default_value = 14.0 * (1.0 - u) ** 1.4
    tree.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
    tree.links.new(mapping.outputs["Vector"], noise.inputs["Vector"])
    tree.links.new(coord.outputs["Object"], falloff.inputs["Vector"])
    tree.links.new(noise.outputs["Fac"], lift.inputs[0])
    tree.links.new(lift.outputs["Value"], shape.inputs[0])
    tree.links.new(falloff.outputs["Fac"], shape.inputs[1])
    tree.links.new(shape.outputs["Value"], density.inputs[0])
    tree.links.new(density.outputs["Value"], volume.inputs["Density"])
    tree.links.new(volume.outputs["Volume"], out.inputs["Volume"])
    obj.data.materials.append(material)
    return [obj]


def _shock_ring(u, seed):
    """Screen-space normal of a growing, thinning ring (B's shock wave): R = x·0.5 + 0.5, G = y·0.5 + 0.5, alpha = the ring.
    Rendered with the Raw view transform so the numbers reach the PNG unchanged; Unity imports it linear (plan decision 4)."""
    major = 0.2 + 0.85 * _ease_out(u)
    minor = 0.14 * (1.0 - u) + 0.02
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=96, minor_segments=24,
                                     rotation=(math.radians(90.0), 0.0, 0.0))
    obj = bpy.context.object
    for poly in obj.data.polygons:
        poly.use_smooth = True
    material = bpy.data.materials.new("vfx_normal")
    tree = node_tree(material)
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    split = tree.nodes.new("ShaderNodeSeparateXYZ")
    red = tree.nodes.new("ShaderNodeMath")
    red.operation = "MULTIPLY_ADD"
    red.inputs[1].default_value = 0.5
    red.inputs[2].default_value = 0.5
    green = tree.nodes.new("ShaderNodeMath")
    green.operation = "MULTIPLY_ADD"
    green.inputs[1].default_value = 0.5
    green.inputs[2].default_value = 0.5
    join = tree.nodes.new("ShaderNodeCombineXYZ")
    emission = tree.nodes.new("ShaderNodeEmission")
    tree.links.new(geometry.outputs["Normal"], split.inputs["Vector"])
    tree.links.new(split.outputs["X"], red.inputs[0])       # screen x = world x (camera at −Y looking +Y)
    tree.links.new(split.outputs["Z"], green.inputs[0])     # screen y = world z
    tree.links.new(red.outputs["Value"], join.inputs["X"])
    tree.links.new(green.outputs["Value"], join.inputs["Y"])
    tree.links.new(join.outputs["Vector"], emission.inputs["Color"])
    tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    obj.data.materials.append(material)
    return [obj]
```
and `RECIPES` becomes:
```python
RECIPES = {
    "puff_toon": {"build": _puff_toon, "ortho": 2.4, "view": "Standard", "linear": False, "ink": 0.035},
    "ring_toon": {"build": _ring_toon, "ortho": 2.4, "view": "Standard", "linear": False, "ink": 0.022},
    "smoke_lit": {"build": _smoke_lit, "ortho": 2.6, "view": "Standard", "linear": False, "ink": None},
    "shock_ring": {"build": _shock_ring, "ortho": 2.4, "view": "Raw", "linear": True, "ink": None},
}
```

- [ ] **Step 4: Run the tests**

Run: `tools/run.sh test test_vfx.py` → Expected: `Ran 6 tests … OK`.
If `smoke_lit` renders fully transparent, EEVEE's volumes did not reach the film alpha: print the scene's `eevee` volumetric attributes in
the failing log and fix the names in `_volume_settings` (they are the only version-dependent part); do not loosen the test.

- [ ] **Step 5: Render Pota's two and look at them**

```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
tools/run.sh vfx_flipbook '{"recipe": "smoke_lit", "game": "Pota", "id": "vfx-smoke-lit", "duration": 0.9}'
tools/run.sh vfx_flipbook '{"recipe": "shock_ring", "game": "Pota", "id": "vfx-shock-ring", "duration": 0.4}'
```
Read both `preview.png`: the smoke is a soft, lumpy, lit puff (bright upper left, darker lower right) that grows, rises and thins
to nothing; the shock ring shows red rising left→right and green bottom→top around the ring. Fix visible flaws in the recipe numbers
(ledger ruling) before committing.

- [ ] **Step 6: Full suite and commit**

Run: `tools/run.sh test` → Expected: all pass.
```bash
git add tools/bl/vfx.py tests/test_vfx.py assets/Pota/vfx-smoke-lit assets/Pota/vfx-shock-ring
git commit -m "feat: vfx_flipbook recipes — lit procedural smoke and the shock ring's screen-normal flipbook

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Blender — `vfx_fracture`, the pre-fractured pillow block

**Repo:** `/Users/tunaaltin17/projects/Blender MCP`.

**Files:**
- Modify: `tools/bl/vfx.py` (fracture), `tools/bl/__init__.py`, `CLAUDE.md` (§5 tool table: `vfx_flipbook`, `vfx_fracture` rows)
- Test: `tests/test_vfx.py`
- Output (committed): `assets/Pota/vfx-pillow-shatter/export/{vfx-pillow-shatter.glb, preview.png, manifest.json}`

**Interfaces:**
- Produces: `bl.vfx_fracture(game, id, pieces=10, seed=7, size=0.9, radius=0.22, bulge=0.1, segments=5, out_dir=None, preview=True) -> dict`.
  GLB: one mesh object per piece named `piece_00…`, origin at the piece's centre (so Unity children's `localPosition` = centre),
  UV0 = front planar projection `(x/size + 0.5, z/size + 0.5)` on every face, UV1.x = 1 on the block's original front faces, 0 elsewhere.
  Manifest = `bl.export`'s plus `"pieces": [{"name", "center": [x, y, z] (Blender axes), "volume"}]` and `checks.fracture`
  `{"pieces", "volume_ratio", "front_faces", "front_uv_range": [lo, hi], "passed"}`.

- [ ] **Step 1: Write the failing tests** (append to `tests/test_vfx.py`)

```python
class FractureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        bl.new_scene()
        cls.out = bl.util.resolve(".logs/vfx test/fracture")
        shutil.rmtree(cls.out, ignore_errors=True)
        cls.m = bl.vfx_fracture(game="_test", id="fracture", pieces=6, seed=3, out_dir=cls.out, preview=False)

    def test_six_closed_pieces_fill_the_block(self):
        f = self.m["checks"]["fracture"]
        self.assertEqual(f["pieces"], 6)
        self.assertEqual(len(self.m["pieces"]), 6)
        self.assertAlmostEqual(f["volume_ratio"], 1.0, delta=0.03)
        self.assertEqual(self.m["checks"]["non_manifold"], 0)
        self.assertEqual(self.m["kind"], "vfx-mesh")
        self.assertTrue(self.m["checks"]["passed"])

    def test_front_uvs_stay_on_the_face(self):
        lo, hi = self.m["checks"]["fracture"]["front_uv_range"]
        self.assertGreaterEqual(lo, 0.0)
        self.assertLessEqual(hi, 1.0)
        self.assertGreater(self.m["checks"]["fracture"]["front_faces"], 0)

    def test_exported_pieces_carry_two_uv_sets_and_front_flags(self):
        names = bl.load(os.path.join(self.out, "fracture.glb"))
        meshes = [bpy.data.objects[n] for n in names if bpy.data.objects[n].type == "MESH"]
        self.assertEqual(len(meshes), 6)
        flagged = 0
        for obj in meshes:
            self.assertEqual(len(obj.data.uv_layers), 2, obj.name)
            flags = obj.data.uv_layers[1].data
            flagged += sum(1 for loop in flags if loop.uv[0] > 0.5)
        self.assertGreater(flagged, 0, "no front faces flagged")

    def test_same_seed_same_cut(self):
        again = bl.vfx_fracture(game="_test", id="fracture2", pieces=6, seed=3,
                                out_dir=bl.util.resolve(".logs/vfx test/fracture2"), preview=False)
        self.assertEqual([p["volume"] for p in again["pieces"]], [p["volume"] for p in self.m["pieces"]])
```

- [ ] **Step 2: Run to see them fail**

Run: `tools/run.sh test test_vfx.py` → Expected: `FractureTests` errors, `AttributeError: module 'bl' has no attribute 'vfx_fracture'`.

- [ ] **Step 3: Implement** (append to `tools/bl/vfx.py`; add `import bmesh` to the imports and `from .export import export`)

```python
def _pillow(size, radius, bulge, segments):
    """Pota's pillow block in workshop axes (front −Y): a bevelled cube whose front half bulges like PillowBlockMesh.cs
    (z += bulge·(1−u²)(1−w²)·depth/h). Returns a bmesh."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=size)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=radius, segments=segments, profile=0.5, affect="EDGES",
                    clamp_overlap=True)
    h = size * 0.5
    for vert in bm.verts:
        depth = -vert.co.y
        if depth <= 0.0 or bulge == 0.0:
            continue
        u, w, d = vert.co.x / h, vert.co.z / h, depth / h
        a, b = max(0.0, 1.0 - u * u), max(0.0, 1.0 - w * w)
        vert.co.y = -(depth + bulge * a * b * d)
    bm.normal_update()
    return bm


def _seeds(count, size, rng):
    """`count` Voronoi seeds inside the block, at least a fifth of its size apart."""
    half, gap, seeds = size * 0.4, size * 0.2, []
    for _ in range(5000):
        if len(seeds) == count:
            return seeds
        p = Vector((rng.uniform(-half, half), rng.uniform(-half * 0.8, half * 0.8), rng.uniform(-half, half)))
        if all((p - q).length >= gap for q in seeds):
            seeds.append(p)
    raise ValueError("could not place {} seeds {:.3f} m apart".format(count, gap))


def _cell(base, seeds, index):
    """The block cut down to seed `index`'s Voronoi cell: bisect by the plane halfway to every other seed, fill each cut.
    Cap faces get material index 1 so they are never flagged as front."""
    bm = base.copy()
    for other_index, other in enumerate(seeds):
        if other_index == index:
            continue
        normal = (other - seeds[index]).normalized()
        middle = (other + seeds[index]) * 0.5
        result = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=middle,
                                        plane_no=normal, clear_outer=True)
        cut = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if not cut:
            continue
        filled = bmesh.ops.holes_fill(bm, edges=cut, sides=0)
        for face in filled["faces"]:
            face.material_index = 1
        bmesh.ops.triangulate(bm, faces=filled["faces"])
    bm.normal_update()
    return bm


def vfx_fracture(game, id, pieces=10, seed=7, size=0.9, radius=0.22, bulge=0.1, segments=5, out_dir=None, preview=True):
    params = {"pieces": pieces, "seed": seed, "size": size, "radius": radius, "bulge": bulge, "segments": segments}
    if not bpy.app.background:
        return run_background("vfx_fracture", dict(params, game=game, id=id, out_dir=out_dir, preview=preview))
    new_scene()
    base = _pillow(size, radius, bulge, segments)
    whole = base.calc_volume(signed=False)
    seeds = _seeds(pieces, size, random.Random(seed))
    material = bpy.data.materials.new("vfx_piece")
    made, total, front_faces, lo, hi = [], 0.0, 0, 1.0, 0.0
    for index in range(pieces):
        bm = _cell(base, seeds, index)
        uv = bm.loops.layers.uv.new("UVMap")
        flag = bm.loops.layers.uv.new("Front")
        for face in bm.faces:
            front = face.material_index == 0 and face.normal.y < -0.5
            front_faces += int(front)
            for loop in face.loops:
                u, v = loop.vert.co.x / size + 0.5, loop.vert.co.z / size + 0.5
                loop[uv].uv = (u, v)
                loop[flag].uv = (1.0 if front else 0.0, 0.0)
                if front:
                    lo, hi = min(lo, u, v), max(hi, u, v)
            face.material_index = 0
        volume = bm.calc_volume(signed=False)
        total += volume
        centre = sum((vert.co for vert in bm.verts), Vector()) / len(bm.verts)
        bmesh.ops.translate(bm, vec=-centre, verts=bm.verts)
        name = "piece_{:02d}".format(index)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(material)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = centre
        bpy.context.scene.collection.objects.link(obj)
        made.append({"name": name, "center": [round(c, 4) for c in centre], "volume": round(volume, 6)})
    base.free()
    out_dir = resolve(out_dir or "assets/{}/{}/export".format(game, id))
    manifest = export(out_dir=out_dir, id=id, game=game, kind="vfx-mesh",
                      source={"recipe": "vfx_fracture", "params": params}, preview=preview)
    ratio = total / whole
    fracture = {"pieces": len(made), "volume_ratio": round(ratio, 4), "front_faces": front_faces,
                "front_uv_range": [round(lo, 4), round(hi, 4)]}
    fracture["passed"] = (len(made) == pieces and 0.97 <= ratio <= 1.03 and front_faces > 0
                          and lo >= -1e-4 and hi <= 1.0 + 1e-4)
    manifest["pieces"] = made
    manifest["checks"]["fracture"] = fracture
    manifest["checks"]["passed"] = bool(manifest["checks"]["passed"] and fracture["passed"])
    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
    log("vfx_fracture {}: {} pieces, volume {:.3f} of the block, {} front faces -> {}".format(
        id, len(made), ratio, front_faces, out_dir))
    return manifest
```
`tools/bl/__init__.py`: `from .vfx import vfx_flipbook, vfx_fracture` and `"vfx_fracture": vfx_fracture` in `TOOLS`.

`CLAUDE.md` §5 table, two rows (same column form as the others):
```markdown
| `vfx_flipbook(recipe, game, id, frames=16, cols=4, frame_px=256, duration=0.35, seed=7, out_dir=None)` | Tariften kare kare efekt dokusu (şeffaf EEVEE): `puff_toon`, `ring_toon`, `smoke_lit`, `shock_ring` (doğrusal normal) | `assets/<Oyun>/<id>/export/{<id>.png, preview.png, manifest.json}` (`kind: vfx-flipbook`, `files.sheet`, `flipbook` bloğu) |
| `vfx_fracture(game, id, pieces=10, seed=7, size=0.9, radius=0.22, bulge=0.1, segments=5, out_dir=None, preview=True)` | Yastık bloğu Voronoi ile parçalara böler (bmesh, eklenti yok); parça başına nesne, merkez orijin, UV0 ön yüz izdüşümü, UV1.x ön yüz bayrağı | `assets/<Oyun>/<id>/export/` (`kind: vfx-mesh`, `pieces`, `checks.fracture`) |
```

- [ ] **Step 4: Run the tests**

Run: `tools/run.sh test test_vfx.py` → Expected: `Ran 10 tests … OK`.

- [ ] **Step 5: Make Pota's block and look at it**

```bash
tools/run.sh vfx_fracture '{"game": "Pota", "id": "vfx-pillow-shatter", "pieces": 10}'
```
Expected: `[bl] vfx_fracture vfx-pillow-shatter: 10 pieces, volume ~1.00 of the block, … front faces`. Read
`assets/Pota/vfx-pillow-shatter/export/preview.png`: a whole pillow (the pieces sit in place), wireframe shows the cuts, no holes.

- [ ] **Step 6: Full suite and commit**

Run: `tools/run.sh test` → Expected: all pass.
```bash
git add tools/bl/vfx.py tools/bl/__init__.py tests/test_vfx.py CLAUDE.md assets/Pota/vfx-pillow-shatter
git commit -m "feat: vfx_fracture — Voronoi-cut pillow block with front UVs and flags for the sticker; tool table

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Unity — flipbook hand-offs in `ai_import_asset`, and `ai_studio_import`

**Files:**
- Modify: `Assets/_Project/Core/Editor/AITools/ImportAssetTool.cs`
- Create: `Tools/fixtures/handoff-flipbook/{fixture-flipbook.png, manifest.json}`
- Test: `Assets/_Project/Core/Tests/EditMode/ImportAssetToolTests.cs`
- Create: `Assets/_Project/Games/Pota/Editor/StudioImportTool.cs` (`ai_studio_import`)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs` (studio asset fields)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/StudioAssetsTests.cs`
- Output: `Assets/_Project/Games/Pota/Art/VFX/{vfx-puff-toon, vfx-ring-toon, vfx-smoke-lit, vfx-shock-ring, vfx-pillow-shatter}/*`

**Interfaces:**
- Consumes: Blender hand-offs from Tasks 1–3 at `~/projects/Blender MCP/assets/Pota/<id>/export/manifest.json`.
- Produces: `ImportAssetTool.Result` gains `public int[] sheetSize, manifestSheetSize;` (null for mesh kinds).
- Produces: `ColorHuntSettings` fields `Texture2D CandyPuff, CandyRing, ShatterSmoke, ShockRing; GameObject ShatterPieces;`
- Produces: `StudioImportTool.Ids` (string[5]), `StudioImportTool.Import(string workshop) -> List<string>`, menu
  `AI Tools/Color Hunt/Import Studio Look`.

- [ ] **Step 1: Make the fixture**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
mkdir -p Tools/fixtures/handoff-flipbook
~/.local/bin/uv run --with pillow python - <<'EOF'
from PIL import Image, ImageDraw
sheet = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
d = ImageDraw.Draw(sheet)
for i in range(4):
    x, y, r = (i % 2) * 64 + 32, (i // 2) * 64 + 32, 8 + 6 * i
    d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 255), outline=(20, 27, 46, 255), width=3)
sheet.save("Tools/fixtures/handoff-flipbook/fixture-flipbook.png")
EOF
cat > Tools/fixtures/handoff-flipbook/manifest.json <<'EOF'
{
  "id": "fixture-flipbook", "game": "_test", "kind": "vfx-flipbook", "version": 1,
  "files": {"glb": null, "fbx": null, "sheet": "fixture-flipbook.png", "previews": []},
  "flipbook": {"cols": 2, "rows": 2, "frames": 4, "frame_px": 64, "duration_s": 0.35, "linear": false},
  "units": "px", "up": "+Y", "scale_applied": true,
  "stats": {"tris": 0, "materials": 0, "textures": [{"name": "fixture-flipbook.png", "size": [128, 128]}], "bones": 0, "clips": [], "size_m": [0, 0, 0]},
  "source": {"recipe": "fixture", "copy_provenance": null, "inputs": [], "params": {}},
  "checks": {"frames_empty": [], "coverage": [0.1, 0.2, 0.3, 0.4], "passed": true},
  "made": "2026-09-30T00:00:00Z"
}
EOF
```

- [ ] **Step 2: Write the failing importer tests** (add to `ImportAssetToolTests`)

```csharp
        private static string FlipbookFixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-flipbook", "manifest.json"));

        /// <summary>A copy of the flipbook fixture with <paramref name="edit"/> applied to its manifest, in Temp.</summary>
        private static string FlipbookVariant(string name, Action<Newtonsoft.Json.Linq.JObject> edit)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "handoff-flipbook-" + name));
            Directory.CreateDirectory(dir);
            string src = Path.GetDirectoryName(FlipbookFixture);
            File.Copy(Path.Combine(src, "fixture-flipbook.png"), Path.Combine(dir, "fixture-flipbook.png"), true);
            var m = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(FlipbookFixture));
            edit(m);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), m.ToString());
            return Path.Combine(dir, "manifest.json");
        }

        [Test]
        public void Flipbook_CopiesTheSheet_ImportsItForParticles_AndMatches()
        {
            var r = ImportAssetTool.Import(FlipbookFixture, Root + "/flipbook");
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            CollectionAssert.AreEqual(new[] { 128, 128 }, r.sheetSize);
            CollectionAssert.AreEqual(new[] { 128, 128 }, r.manifestSheetSize);
            string sheet = Root + "/flipbook/fixture-flipbook.png";
            Assert.AreEqual(sheet, r.main);
            var imp = (TextureImporter)AssetImporter.GetAtPath(sheet);
            Assert.IsTrue(imp.alphaIsTransparency);
            Assert.AreEqual(TextureWrapMode.Clamp, imp.wrapMode);
            Assert.IsTrue(imp.sRGBTexture, "linear: false → sRGB");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/flipbook/manifest.json"));
        }

        [Test]
        public void Flipbook_Linear_ImportsWithoutSrgb()
        {
            string manifest = FlipbookVariant("linear", m => m["flipbook"]["linear"] = true);
            ImportAssetTool.Import(manifest, Root + "/linear");
            var imp = (TextureImporter)AssetImporter.GetAtPath(Root + "/linear/fixture-flipbook.png");
            Assert.IsFalse(imp.sRGBTexture);
        }

        [Test]
        public void Flipbook_WrongFrameSize_IsADifference()
        {
            string manifest = FlipbookVariant("size", m => m["flipbook"]["frame_px"] = 32);
            var r = ImportAssetTool.Import(manifest, Root + "/size");
            Assert.IsFalse(r.matches);
            StringAssert.Contains("sheet", string.Join("; ", r.differences));
        }

        [Test]
        public void Flipbook_WithoutSheet_IsRefused()
        {
            string manifest = FlipbookVariant("nosheet", m => ((Newtonsoft.Json.Linq.JObject)m["files"]).Remove("sheet"));
            Assert.Throws<ArgumentException>(() => ImportAssetTool.Import(manifest, Root + "/nosheet"));
        }
```

- [ ] **Step 3: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ImportAssetToolTests`
Expected: exit 2, `error CS1061: 'ImportAssetTool.Result' does not contain a definition for 'sheetSize'`.

- [ ] **Step 4: Teach `ImportAssetTool` the flipbook kind**

In `Result`, add:
```csharp
            public int[] sheetSize, manifestSheetSize;
```
In `HandleCommand`'s success payload (the anonymous object listing `r.id, r.folder, …`), add `r.sheetSize, r.manifestSheetSize`.

Replace the block from `var files = m["files"] …` to `r.main = $"{r.folder}/{mainName}";` / `Measure(r); Compare(r, …);` with:
```csharp
            var files = m["files"] as JObject ?? throw new ArgumentException("manifest has no 'files'.");
            string glb = files.Value<string>("glb"), fbx = files.Value<string>("fbx"), sheet = files.Value<string>("sheet");
            bool flipbook = kind == "vfx-flipbook";
            if (flipbook && string.IsNullOrEmpty(sheet)) throw new ArgumentException("a vfx-flipbook manifest lists no 'sheet'.");
            string mainName = flipbook ? sheet : !string.IsNullOrEmpty(glb) ? glb : fbx;
            if (string.IsNullOrEmpty(mainName)) throw new ArgumentException("manifest lists neither a glb nor an fbx.");

            var r = new Result { id = id, folder = dest ?? $"Assets/_Project/Games/{game}/Art/{kindFolder}/{id}" };
            AIToolsCommon.EnsureFolder(r.folder);
            string sourceDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            foreach (string name in new[] { glb, fbx, flipbook ? sheet : null, "manifest.json" }.Where(n => !string.IsNullOrEmpty(n)))
            {
                string source = name == "manifest.json" ? manifestPath : Path.Combine(sourceDir, name);
                if (!File.Exists(source)) throw new FileNotFoundException("Hand-off file missing: " + source);
                string target = $"{r.folder}/{name}";
                File.Copy(source, target, true); // in place: the existing .meta (GUID) stays
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                r.copied.Add(target);
            }
            r.main = $"{r.folder}/{mainName}";
            if (flipbook)
            {
                var fb = m["flipbook"] as JObject ?? throw new ArgumentException("a vfx-flipbook manifest has no 'flipbook' block.");
                ConfigureSheet(r.main, fb.Value<bool?>("linear") ?? false);
                MeasureSheet(r, fb);
                return r;
            }
            Measure(r);
            Compare(r, m["stats"] as JObject);
            return r;
```
Add:
```csharp
        /// <summary>Particle flipbook import: straight alpha, clamped, mipmapped, uncompressed; sRGB unless the sheet holds data.</summary>
        private static void ConfigureSheet(string path, bool linear)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.sRGBTexture = !linear;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        private static void MeasureSheet(Result r, JObject fb)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(r.main) ?? throw new IOException("Unity imported no texture from " + r.main);
            int px = fb.Value<int?>("frame_px") ?? 0;
            r.sheetSize = new[] { tex.width, tex.height };
            r.manifestSheetSize = new[] { (fb.Value<int?>("cols") ?? 0) * px, (fb.Value<int?>("rows") ?? 0) * px };
            if (r.sheetSize[0] != r.manifestSheetSize[0] || r.sheetSize[1] != r.manifestSheetSize[1])
                r.differences.Add($"sheet manifest {r.manifestSheetSize[0]}x{r.manifestSheetSize[1]} vs Unity {tex.width}x{tex.height}");
        }
```

- [ ] **Step 5: Run the importer tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ImportAssetToolTests` → Expected: all pass (the 4 new + existing).

- [ ] **Step 6: Write the failing studio-asset test**

`Assets/_Project/Games/Pota/Tests/EditMode/StudioAssetsTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The Blender hand-offs for the Candy and Shatter looks are in the project and wired into the settings asset.</summary>
    public class StudioAssetsTests
    {
        private const string Hint = "run the Blender tools (plan Tasks 1–3), then execute_custom_tool ai_studio_import";

        private static ColorHuntSettings Settings() =>
            AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");

        [Test]
        public void Flipbooks_Are4x4Of256_WithAlpha()
        {
            var s = Settings();
            foreach (var tex in new[] { s.CandyPuff, s.CandyRing, s.ShatterSmoke, s.ShockRing })
            {
                Assert.IsNotNull(tex, Hint);
                Assert.AreEqual(1024, tex.width, tex.name);
                Assert.AreEqual(1024, tex.height, tex.name);
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                Assert.IsTrue(imp.alphaIsTransparency, tex.name);
            }
            Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.ShockRing))).sRGBTexture,
                "the shock ring holds normals: linear");
        }

        [Test]
        public void ShatterPieces_AreTenMeshesWithTheFrontUvSet()
        {
            var model = Settings().ShatterPieces;
            Assert.IsNotNull(model, Hint);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            Assert.AreEqual(10, filters.Length);
            bool anyFront = false;
            foreach (var f in filters)
            {
                var mesh = f.sharedMesh;
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, f.name + " UV0");
                Assert.AreEqual(mesh.vertexCount, mesh.uv2.Length, f.name + " UV1 (front flag)");
                foreach (var flag in mesh.uv2) anyFront |= flag.x > 0.5f;
            }
            Assert.IsTrue(anyFront, "no piece carries a front face");
        }
    }
}
```

- [ ] **Step 7: Run to see it fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.StudioAssetsTests`
Expected: exit 2, `error CS1061: 'ColorHuntSettings' does not contain a definition for 'CandyPuff'`.

- [ ] **Step 8: Settings fields and the import tool**

`ColorHuntSettings.cs`, after the material-template fields:
```csharp
        [Header("Studio looks (Blender hand-offs, ai_studio_import)")]
        [Tooltip("A: vfx_flipbook puff_toon, 4×4 frames of 256 px.")]
        public Texture2D CandyPuff;
        [Tooltip("A: vfx_flipbook ring_toon.")]
        public Texture2D CandyRing;
        [Tooltip("B: vfx_flipbook smoke_lit.")]
        public Texture2D ShatterSmoke;
        [Tooltip("B: vfx_flipbook shock_ring (linear screen normals).")]
        public Texture2D ShockRing;
        [Tooltip("B: vfx_fracture pillow, one child mesh per piece (origin = piece centre, UV1.x = front flag).")]
        public GameObject ShatterPieces;
```

`Assets/_Project/Games/Pota/Editor/StudioImportTool.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Imports the Blender workshop's hand-offs for the Candy (A) and Shatter (B) looks (flipbooks and the fractured pillow) through
    /// ai_import_asset and wires them into ColorHuntSettings. Idempotent: same ids keep their GUIDs.
    /// </summary>
    [McpForUnityTool("ai_studio_import", Description =
        "Import the Blender studio-look hand-offs (vfx-puff-toon, vfx-ring-toon, vfx-smoke-lit, vfx-shock-ring, vfx-pillow-shatter) " +
        "into Games/Pota/Art/VFX and wire them into ColorHuntSettings. Params: workshop (default ~/projects/Blender MCP).")]
    public static class StudioImportTool
    {
        public const string DefaultWorkshop = "~/projects/Blender MCP";
        public static readonly string[] Ids = { "vfx-puff-toon", "vfx-ring-toon", "vfx-smoke-lit", "vfx-shock-ring", "vfx-pillow-shatter" };

        public class Parameters
        {
            [ToolParameter("Blender workshop root", Required = false)]
            public string workshop { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            try
            {
                var report = Import(AIToolsCommon.GetString(p, "workshop", DefaultWorkshop));
                AIToolsCommon.Log(string.Join(" | ", report));
                return new SuccessResponse("Studio look imported.", new { steps = report });
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                return new ErrorResponse(e.Message);
            }
        }

        [MenuItem("AI Tools/Color Hunt/Import Studio Look")]
        public static void Menu() => AIToolsCommon.Log(string.Join(" | ", Import(DefaultWorkshop)));

        public static string Folder(string id) => $"Assets/_Project/Games/Pota/Art/VFX/{id}";

        public static List<string> Import(string workshop)
        {
            string root = workshop.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), workshop.Substring(2))
                : workshop;
            var report = new List<string>();
            foreach (string id in Ids)
            {
                var r = ImportAssetTool.Import(Path.Combine(root, "assets", "Pota", id, "export", "manifest.json"));
                report.Add($"{id}: {(r.matches ? "matches" : string.Join("; ", r.differences))}");
            }
            var s = ColorHuntSceneTool.EnsureSettings(report);
            s.CandyPuff = Sheet("vfx-puff-toon");
            s.CandyRing = Sheet("vfx-ring-toon");
            s.ShatterSmoke = Sheet("vfx-smoke-lit");
            s.ShockRing = Sheet("vfx-shock-ring");
            s.ShatterPieces = AssetDatabase.LoadAssetAtPath<GameObject>(Folder("vfx-pillow-shatter") + "/vfx-pillow-shatter.glb");
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            report.Add($"settings: puff={s.CandyPuff != null} ring={s.CandyRing != null} smoke={s.ShatterSmoke != null} " +
                       $"shock={s.ShockRing != null} pieces={s.ShatterPieces != null}");
            report.AddRange(PotaMaterialTemplates.Ensure());
            return report;
        }

        private static Texture2D Sheet(string id) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder(id)}/{id}.png");
    }
}
```

- [ ] **Step 9: Compile, import, run the tests**

```bash
~/.local/bin/uv run Tools/mcp_call.py call refresh_unity '{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}'
~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_studio_import","parameters":{}}'
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.StudioAssetsTests Project.Tests.ImportAssetToolTests --no-compile
```
Expected: the tool reports `matches` for all five ids (the fracture's tris equal Unity's) and all tests pass. A `tris manifest … vs
Unity …` difference on the pillow means glTFast split or merged something: read the numbers, report them in the ledger, and fix the
side that is wrong (a Blender triangulation left an n-gon → export triangulates; or Unity added a collider mesh → exclude it) before
going on.

- [ ] **Step 10: Commit**

```bash
git add Assets/_Project/Core/Editor/AITools/ImportAssetTool.cs Assets/_Project/Core/Tests/EditMode/ImportAssetToolTests.cs \
  Tools/fixtures/handoff-flipbook Assets/_Project/Games/Pota/Editor/StudioImportTool.cs* \
  Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs Assets/_Project/Games/Pota/Tests/EditMode/StudioAssetsTests.cs* \
  Assets/_Project/Games/Pota/Art/VFX Assets/_Project/Games/Pota/Art/VFX.meta Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset
git commit -m "feat: ai_import_asset takes vfx-flipbook hand-offs; ai_studio_import brings the Blender flipbooks and fractured block

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The look switch — `PuzzleLook`, `IBurstStyle`, Pota behind the seam

**Files:**
- Modify: `Scripts/ColorHunt/ColorHuntSettings.cs` (`PuzzleLook`, `Look`), `Scripts/ColorHunt/ColorHuntGame.cs`, `Scripts/ColorHunt/ColorHuntFx.cs`
  (`LiveCount`), `Scripts/ColorHunt/ColorHuntJuice.cs` (`Chain(…, number)`), `Editor/ColorHuntRecordTool.cs` (`Cell` uses the hint)
- Create: `Scripts/ColorHunt/Bursts/IBurstStyle.cs`, `Scripts/ColorHunt/Bursts/PotaBurstStyle.cs`, `Scripts/ColorHunt/Bursts/BurstBudget.cs`,
  `Scripts/ColorHunt/ColorHuntHint.cs`
- Test: `Tests/EditMode/BurstStyleTests.cs`, `Tests/EditMode/ColorHuntHintTests.cs`, `Tests/PlayMode/StudioLookPlayTests.cs`

(All paths under `Assets/_Project/Games/Pota/`.)

**Interfaces:**
- Produces (namespace `Project.ColorHunt`):
  ```csharp
  public enum PuzzleLook { Pota, Candy, Shatter }                       // ColorHuntSettings.cs
  public PuzzleLook Look = PuzzleLook.Pota;                             // ColorHuntSettings field
  public interface IBurstStyle
  {
      bool Ready { get; }
      bool ShowsChainNumber { get; }
      int LiveCount { get; }
      void Build(ColorHuntGame game);
      void Activate(bool on);
      void TapBurst(BlockView v, Color tone);
      void StepBurst(BlockView v, Color tone, int depth);
      void ChainPeak(int depth, Vector2 at, Color tone);   // once per step; at = board-local centroid of the step's blocks
      void Clear();
  }
  public static class BurstBudget { public const int Pota = 684; public const int Max = 1026; }
  public sealed class PotaBurstStyle : IBurstStyle { … }
  public sealed class NoStyle : IBurstStyle { … }                      // Ready = false
  public static class ColorHuntHint { public static int Best(Board b); }
  // ColorHuntGame:
  public PuzzleLook Look { get; }           public bool Studio { get; }            // Look != Pota
  public IBurstStyle Style { get; }         public event Action LookChanged;
  public void SetLook(PuzzleLook look);     public void UseStyle(PuzzleLook look, IBurstStyle style);
  // ColorHuntFx: public int LiveCount { get; }
  // ColorHuntJuice: public void Chain(int depth, Vector3 at, bool number = true)
  ```
- Later tasks add cases to `ColorHuntGame.CreateStyle` (Candy: Task 10, Shatter: Task 11).

- [ ] **Step 1: Write the failing EditMode tests**

`Tests/EditMode/BurstStyleTests.cs`:
```csharp
using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class BurstStyleTests
    {
        private class FakeStyle : IBurstStyle
        {
            public bool IsReady = true;
            public int Built, Cleared, Taps;
            public bool Active;
            public bool Ready => IsReady;
            public bool ShowsChainNumber => false;
            public int LiveCount => 0;
            public void Build(ColorHuntGame game) => Built++;
            public void Activate(bool on) => Active = on;
            public void TapBurst(BlockView v, Color tone) => Taps++;
            public void StepBurst(BlockView v, Color tone, int depth) { }
            public void ChainPeak(int depth, Vector2 at, Color tone) { }
            public void Clear() => Cleared++;
        }

        private GameObject _go;

        private ColorHuntGame MakeGame()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = ScriptableObject.CreateInstance<ColorHuntSettings>();
            return game;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void DefaultLook_IsPota_WhosStyleIsAlwaysReady()
        {
            var game = MakeGame();
            Assert.AreEqual(PuzzleLook.Pota, ScriptableObject.CreateInstance<ColorHuntSettings>().Look, "the settings default");
            Assert.AreEqual(PuzzleLook.Pota, game.Look);
            Assert.IsFalse(game.Studio);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style);
            Assert.IsTrue(game.Style.Ready);
            Assert.IsTrue(game.Style.ShowsChainNumber, "Pota shows its ×d popup");
        }

        [Test]
        public void MissingAssets_FallBackToPota()
        {
            var game = MakeGame();                            // in-memory settings: no flipbooks, no pieces
            LogAssert.Expect(LogType.Warning, new Regex("Candy look is missing assets"));
            game.SetLook(PuzzleLook.Candy);
            Assert.AreEqual(PuzzleLook.Candy, game.Look, "the look (and the studio polish) stays what was asked");
            Assert.IsTrue(game.Studio);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style, "the burst falls back to Pota's");
        }

        [Test]
        public void SetLook_ActivatesTheNewStyle_AndClearsTheOldOne()
        {
            var game = MakeGame();
            var candy = new FakeStyle();
            game.UseStyle(PuzzleLook.Candy, candy);
            Assert.AreEqual(1, candy.Built);
            game.SetLook(PuzzleLook.Candy);
            Assert.AreSame(candy, game.Style);
            Assert.IsTrue(candy.Active);
            game.SetLook(PuzzleLook.Pota);
            Assert.AreEqual(1, candy.Cleared);
            Assert.IsFalse(candy.Active);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style);
        }

        [Test]
        public void LookChanged_FiresOncePerChange()
        {
            var game = MakeGame();
            game.UseStyle(PuzzleLook.Candy, new FakeStyle());
            int fired = 0;
            game.LookChanged += () => fired++;
            game.SetLook(PuzzleLook.Candy);
            game.SetLook(PuzzleLook.Candy);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Budget_IsHalfAgainPotasPools()
        {
            Assert.AreEqual(12 + 12 + 12 + 72 + 160 + 16 + 160 + 240, BurstBudget.Pota);
            Assert.AreEqual(1026, BurstBudget.Max);
        }
    }
}
```
`Tests/EditMode/ColorHuntHintTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntHintTests
    {
        private static Board Deal(int ammo) =>
            ColorHuntRules.Create(ScriptableObject.CreateInstance<ColorHuntSettings>().ToDesign(), ammo, 7);

        [Test]
        public void Best_IsTheAffordableBlockWithTheLongestChain_TiesGoToTheCheapest()
        {
            var b = Deal(158);
            int best = ColorHuntHint.Best(b);
            Assert.GreaterOrEqual(best, 0);
            int bestSteps = ColorHuntRules.Break(b, best).Steps.Count;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i].Hp > b.Ammo) continue;
                int steps = ColorHuntRules.Break(b, i).Steps.Count;
                Assert.LessOrEqual(steps, bestSteps, $"cell {i} chains longer");
                if (steps == bestSteps) Assert.LessOrEqual(b.Cells[best].Hp, b.Cells[i].Hp, $"cell {i} is as long and cheaper");
            }
        }

        [Test]
        public void Best_IsMinusOne_WhenNothingIsAffordable() => Assert.AreEqual(-1, ColorHuntHint.Best(Deal(0)));
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BurstStyleTests Project.Tests.ColorHuntHintTests`
Expected: exit 2, `error CS0246: The type or namespace name 'IBurstStyle' could not be found` (and `PuzzleLook`, `ColorHuntHint`).

- [ ] **Step 3: The seam**

`ColorHuntSettings.cs`, inside `namespace Project.ColorHunt` above the class:
```csharp
    /// <summary>Pota = the 1:1 port of Pota's puzzle; Candy = A "Şeker patlaması"; Shatter = B "Gerçek kırılma".</summary>
    public enum PuzzleLook { Pota, Candy, Shatter }
```
and first thing in the class body:
```csharp
        [Header("Look (spec 2026-09-30 studio polish: Pota, A = Candy, B = Shatter)")]
        [Tooltip("Pota keeps today's burst and HUD; Candy and Shatter add their burst and the studio polish.")]
        public PuzzleLook Look = PuzzleLook.Pota;
```

`Scripts/ColorHunt/Bursts/IBurstStyle.cs`:
```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// What a look draws at the swell peaks. The game keeps the rhythm (swell, peak, chain steps, drops), the sounds, the dips and the
    /// shake; a style only draws. Positions are board-local (BlockView.Pos space); tones are palette colours (sRGB).
    /// </summary>
    public interface IBurstStyle
    {
        /// <summary>Its assets are in place. A style that is not ready is replaced by Pota's burst, with one warning.</summary>
        bool Ready { get; }
        /// <summary>Pota's "×d" chain popup (ColorHuntJuice.Chain) shows under this look.</summary>
        bool ShowsChainNumber { get; }
        /// <summary>Live particles and pieces this style owns (BurstBudget).</summary>
        int LiveCount { get; }
        void Build(ColorHuntGame game);
        void Activate(bool on);
        /// <summary>The tapped block's burst at its swell peak.</summary>
        void TapBurst(BlockView v, Color tone);
        /// <summary>A chain block's burst at its swell peak, <paramref name="depth"/> ≥ 1.</summary>
        void StepBurst(BlockView v, Color tone, int depth);
        /// <summary>Once per chain step at its first peak: the step's board-local centroid and its first block's tone.</summary>
        void ChainPeak(int depth, Vector2 at, Color tone);
        void Clear();
    }
}
```

`Scripts/ColorHunt/Bursts/BurstBudget.cs`:
```csharp
namespace Project.ColorHunt
{
    /// <summary>Spec §1.5: live particles and pieces per look ≤ 1.5 × Pota's pools.</summary>
    public static class BurstBudget
    {
        /// <summary>ColorHuntFx pools: white flash 12, tone flash 12, ring 12, stars 72, points 160, puff 16, shards 160, confetti 240.</summary>
        public const int Pota = 12 + 12 + 12 + 72 + 160 + 16 + 160 + 240;
        public const int Max = Pota * 3 / 2;
    }
}
```

`Scripts/ColorHunt/Bursts/PotaBurstStyle.cs`:
```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>The Pota look: exactly what the game drew before the seam (ColorHuntFx puff, hot points and burst layers).</summary>
    public sealed class PotaBurstStyle : IBurstStyle
    {
        private ColorHuntGame _game;

        public bool Ready => true;
        public bool ShowsChainNumber => true;
        public int LiveCount => _game != null && _game.Fx != null ? _game.Fx.LiveCount : 0;

        public void Build(ColorHuntGame game) => _game = game;
        public void Activate(bool on) { }

        public void TapBurst(BlockView v, Color tone)
        {
            var fx = _game != null ? _game.Fx : null;
            if (fx == null) return;
            fx.Puff(v.Pos, 1.15f);
            fx.Particles(v.Pos, tone, _game.S.PointsBase + 4, 2.6f, 0.12f);
            fx.Burst(v.Pos, tone, 0);
        }

        public void StepBurst(BlockView v, Color tone, int depth)
        {
            var fx = _game != null ? _game.Fx : null;
            if (fx == null) return;
            fx.Puff(v.Pos, 1f + depth * 0.12f);
            fx.Particles(v.Pos, tone, PotaFx.PointCount(_game.S, depth), 2.2f + depth * 0.3f, 0.1f);
            fx.Burst(v.Pos, tone, depth);
        }

        public void ChainPeak(int depth, Vector2 at, Color tone) { }
        public void Clear() { }   // the game clears ColorHuntFx itself (Prepare)
    }

    /// <summary>A look whose style is not written yet (or a future enum value): never ready, so the game plays Pota's burst.</summary>
    public sealed class NoStyle : IBurstStyle
    {
        public bool Ready => false;
        public bool ShowsChainNumber => true;
        public int LiveCount => 0;
        public void Build(ColorHuntGame game) { }
        public void Activate(bool on) { }
        public void TapBurst(BlockView v, Color tone) { }
        public void StepBurst(BlockView v, Color tone, int depth) { }
        public void ChainPeak(int depth, Vector2 at, Color tone) { }
        public void Clear() { }
    }
}
```

`Scripts/ColorHunt/ColorHuntHint.cs`:
```csharp
namespace Project.ColorHunt
{
    /// <summary>The move a hint (and the recorder's "auto") points at.</summary>
    public static class ColorHuntHint
    {
        /// <summary>The affordable block whose break gives the longest chain (ties: the cheapest); −1 when nothing is affordable.</summary>
        public static int Best(Board b)
        {
            int best = -1, bestSteps = -1, bestHp = int.MaxValue;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var block = b.Cells[i];
                if (block.Hp > b.Ammo) continue;
                int steps = ColorHuntRules.Break(b, i).Steps.Count;           // pure: Break clones the board
                if (steps > bestSteps || (steps == bestSteps && block.Hp < bestHp)) { best = i; bestSteps = steps; bestHp = block.Hp; }
            }
            return best;
        }
    }
}
```

`ColorHuntFx.cs`, public member:
```csharp
        /// <summary>Live particles across the eight layers (BurstBudget).</summary>
        public int LiveCount
        {
            get
            {
                if (!_built) return 0;
                int n = 0;
                foreach (var ps in new[] { _flashWhite, _flashTone, _ring, _stars, _points, _puff, _shards, _confetti }) n += ps.particleCount;
                return n;
            }
        }
```

`ColorHuntJuice.cs`, `Chain`:
```csharp
        public void Chain(int depth, Vector3 at, bool number = true)
        {
            Shake?.Add(S.ShakeChain + (depth - 1) * S.ShakeChainPerDepth);
            TimeCtl?.Dip(S.HitStopChainScale, Mathf.Min(S.HitStopChainMax, S.HitStopChainBase + depth * S.HitStopChainPerDepth));
            Sounds?.Combo(depth);
            if (number && depth >= 2)
                Popups?.Show(at, $"×{depth}", depth >= 4 ? PotaLook.Hex(0xff7a3d) : PotaLook.HudGold, depth >= 4 ? 96 : 72, false, 0.9f);
        }
```
(Keep the existing body lines exactly; only the parameter and the `number &&` are new.)

`ColorHuntGame.cs`:
1. Fields and API (near the other public properties):
```csharp
        public PuzzleLook Look { get; private set; } = PuzzleLook.Pota;
        /// <summary>The studio polish runs for every look but Pota (spec §3).</summary>
        public bool Studio => Look != PuzzleLook.Pota;
        public IBurstStyle Style => _style ?? (_style = Resolve(Look));
        public event Action LookChanged;

        private IBurstStyle _style;
        private readonly Dictionary<PuzzleLook, IBurstStyle> _styles = new Dictionary<PuzzleLook, IBurstStyle>();

        /// <summary>Switches the look (the record tool and designers do it at runtime): the old style is cleared and deactivated.</summary>
        public void SetLook(PuzzleLook look)
        {
            if (_style != null && look == Look) return;
            if (_style != null) { _style.Clear(); _style.Activate(false); }
            Look = look;
            _style = Resolve(look);
            _style.Activate(true);
            Raise(LookChanged);
        }

        /// <summary>Registers (and builds) the style a look uses; later looks and tests plug in here.</summary>
        public void UseStyle(PuzzleLook look, IBurstStyle style)
        {
            style.Build(this);
            _styles[look] = style;
        }

        private IBurstStyle Resolve(PuzzleLook look)
        {
            if (!_styles.TryGetValue(look, out var style)) UseStyle(look, style = CreateStyle(look));
            if (style.Ready) return style;
            Debug.LogWarning($"Color Hunt: the {look} look is missing assets (run ai_studio_import); Pota's burst plays instead.");
            return Resolve(PuzzleLook.Pota);
        }

        private IBurstStyle CreateStyle(PuzzleLook look)
        {
            switch (look)
            {
                case PuzzleLook.Pota: return new PotaBurstStyle();
                default: return new NoStyle();          // Candy: Task 10, Shatter: Task 11
            }
        }

        /// <summary>A MonoBehaviour style on its own child of the game root (so it lives in the hoop's space, not the board's).</summary>
        private T AddStyle<T>(string name) where T : MonoBehaviour, IBurstStyle
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }
```
(`AddStyle` is unused until Task 10; add `#pragma warning disable IDE0051` only if the compiler warns — it does not for private generic methods in Unity's compiler.)
2. `Start`: after `if (Salvo != null) Salvo.Board = BoardRoot;` and before `if (AutoStart) NewRound();`:
```csharp
            SetLook(S != null ? S.Look : PuzzleLook.Pota);
```
3. `Prepare`: after `Fx?.Clear();`:
```csharp
            _style?.Clear();
```
4. `FinishSalvo`'s peak lambda becomes:
```csharp
                _swells.Start(view, S.TapSwellTime, S.TapSwell, true, () =>
                {
                    Juice?.Break();
                    Style.TapBurst(view, tone);
                    CountItem(view, BoardRoot.TransformPoint(new Vector3(view.Pos.x, view.Pos.y, -PotaLook.FrontZ)));
                });
```
5. `StartStep`: replace from `int d = st.Depth;` through the `foreach` loop and `if (first == null) StepPeak();` with:
```csharp
            int d = st.Depth;
            bool peaked = false;
            BlockView first = null;
            Vector2 centre = Vector2.zero;
            int count = 0;
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                centre += v.Pos;
                count++;
            }
            if (count > 0) centre /= count;
            void StepPeak()
            {
                if (peaked) return;
                peaked = true;
                var at = first != null ? first.Pos : Vector2.zero;
                Juice?.Chain(d, BoardRoot.TransformPoint(new Vector3(at.x, at.y + 0.5f, -PotaLook.LabelZ)), Style.ShowsChainNumber);
                Style.ChainPeak(d, centre, first != null ? S.PaletteColor(first.Block.Color) : Color.white);
            }
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                if (first == null) first = v;
                v.Exploding = true;
                var tone = S.PaletteColor(v.Block.Color);
                _swells.Start(v, S.ChainTime, S.ChainSwell, false, () =>
                {
                    StepPeak();
                    Style.StepBurst(v, tone, d);
                    CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                });
            }
            if (first == null) StepPeak();
```
(Read the method first: keep any line of the original loop that this listing does not show, e.g. an `_swells`/`Exploding` detail, and note it in the ledger.)

`Editor/ColorHuntRecordTool.cs`, `Cell`:
```csharp
        private static int Cell(ColorHuntGame game, string arg)
        {
            if (int.TryParse(arg, out int c)) return c;
            var b = game.Board;
            if (_win)
            {
                for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i].Hp <= b.Ammo && b.Cells[i].Color == 0) return i;
                return -1;
            }
            return ColorHuntHint.Best(b);
        }
```

- [ ] **Step 4: Run the EditMode tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BurstStyleTests Project.Tests.ColorHuntHintTests Project.Tests.ColorHuntJuiceTests Project.Tests.ColorHuntRecordToolTests`
Expected: all pass.

- [ ] **Step 5: Write the PlayMode test** (`Tests/PlayMode/StudioLookPlayTests.cs`)

```csharp
using System;
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The look switch in the Color Hunt scene as saved.</summary>
    public class StudioLookPlayTests
    {
        public static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>Loads ColorHunt, switches to <paramref name="look"/>, deals a fresh round and waits until it can be played.</summary>
        public static IEnumerator LoadLook(PuzzleLook look, Action<ColorHuntGame, ColorHuntHud> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            game.SetLook(look);
            game.NewRound();
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current, "round did not start");
            ready(game, Object.FindAnyObjectByType<ColorHuntHud>());
        }

        [UnityTest]
        public IEnumerator SavedScene_PlaysThePotaLook_ThroughTheSeam()
        {
            ColorHuntGame game = null;
            yield return LoadLook(PuzzleLook.Pota, (g, h) => game = g);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style);
            int cell = ColorHuntHint.Best(game.Board);
            Assert.AreEqual("broken", game.Tap(cell));
            int most = 0;
            float t = 0f;
            while (t < 1.2f) { most = Mathf.Max(most, game.Fx.LiveCount); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(most, 0, "Pota's burst layers drew");
            Assert.LessOrEqual(most, BurstBudget.Pota);
        }
    }
}
```

- [ ] **Step 6: Run the PlayMode tests** (new + every Color Hunt/PotaFlow fixture: the seam must not change the Pota look)

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → Expected: all pass (the previous 44 + 1).
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile` → Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts Assets/_Project/Games/Pota/Tests Assets/_Project/Games/Pota/Editor/ColorHuntRecordTool.cs
git commit -m "feat: Color Hunt look switch — PuzzleLook, IBurstStyle seam with Pota's burst behind it, shared best-move hint

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `StudioCurves` and the two synth sounds

**Files:**
- Create: `Scripts/ColorHunt/Studio/StudioCurves.cs`
- Modify: `Scripts/ColorHunt/PotaSounds.cs`
- Test: `Tests/EditMode/StudioCurvesTests.cs`, `Tests/EditMode/PotaSoundsTests.cs`

**Interfaces:**
- Produces `public static class StudioCurves` (namespace `Project.ColorHunt`) — every constant and function below; Tasks 7–10 use them
  by these names.
- Produces `PotaSounds.MakeCollect()`, `PotaSounds.MakeDone()` (static, return `AudioClip` "topla" 0.14 s / "tamam" 0.42 s),
  `PotaSounds.Collect(int streak)`, `PotaSounds.TargetDone()`.

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/StudioCurvesTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class StudioCurvesTests
    {
        private static float Max(System.Func<float, float> f)
        {
            float m = float.MinValue;
            for (int i = 0; i <= 200; i++) m = Mathf.Max(m, f(i / 200f));
            return m;
        }

        private static float Min(System.Func<float, float> f)
        {
            float m = float.MaxValue;
            for (int i = 0; i <= 200; i++) m = Mathf.Min(m, f(i / 200f));
            return m;
        }

        [Test]
        public void Launch_GrowsTo1Point4_ThenSettlesOn1Point2_WobbleDiesOut()
        {
            Assert.AreEqual(0.9f, StudioCurves.LaunchScale(0f), 1e-5f);
            Assert.AreEqual(1.4f, StudioCurves.LaunchScale(0.6f), 1e-5f);
            Assert.AreEqual(1.2f, StudioCurves.LaunchScale(1f), 1e-5f);
            Assert.AreEqual(1.4f, Max(StudioCurves.LaunchScale), 1e-3f);
            Assert.AreEqual(0f, StudioCurves.LaunchWobble(0f), 1e-5f);
            Assert.AreEqual(0f, StudioCurves.LaunchWobble(1f), 1e-4f);
            Assert.LessOrEqual(Max(u => Mathf.Abs(StudioCurves.LaunchWobble(u))), 12f);
        }

        [Test]
        public void Side_FansLeftAndRight_Widening_Capped()
        {
            Assert.AreEqual(0.6f, StudioCurves.Side(0), 1e-5f);
            Assert.AreEqual(-0.6f, StudioCurves.Side(1), 1e-5f);
            Assert.AreEqual(0.8f, StudioCurves.Side(2), 1e-5f);
            Assert.AreEqual(-0.8f, StudioCurves.Side(3), 1e-5f);
            Assert.AreEqual(1.2f, StudioCurves.Side(20), 1e-5f);
        }

        [Test]
        public void FlightPoint_StartsAndEndsOnTheEnds_AcceleratesIn_AndBowsToItsSide()
        {
            var from = new Vector2(0f, 0f);
            var to = new Vector2(0f, 100f);
            Assert.AreEqual(from, StudioCurves.FlightPoint(from, to, 1f, 0f));
            Assert.AreEqual(to, StudioCurves.FlightPoint(from, to, 1f, 1f));
            Assert.Less(StudioCurves.FlightPoint(from, to, 0f, 0.5f).y, 50f, "eased in: slower at the start");
            Assert.Less(StudioCurves.FlightPoint(from, to, 1f, 0.5f).x, 0f, "+side bows to the chord's left");
            Assert.Greater(StudioCurves.FlightPoint(from, to, -1f, 0.5f).x, 0f);
            Assert.AreEqual(1.2f, StudioCurves.FlightScale(0f), 1e-5f);
            Assert.AreEqual(0.55f, StudioCurves.FlightScale(1f), 1e-5f);
        }

        [Test]
        public void Arrival_SquashesWide_ThenSpringsBackToOne()
        {
            Assert.AreEqual(Vector2.one, StudioCurves.ArrivalSquash(0f));
            var early = StudioCurves.ArrivalSquash(0.2f);
            Assert.Greater(early.x, 1.2f);
            Assert.Less(early.y, 0.85f);
            var end = StudioCurves.ArrivalSquash(1f);
            Assert.AreEqual(1f, end.x, 1e-4f);
            Assert.AreEqual(1f, end.y, 1e-4f);
        }

        [Test]
        public void CounterRoll_OldGoesUpAndOut_NewComesUpFromBelow()
        {
            Assert.AreEqual(new Vector2(0f, -1f), StudioCurves.CounterRoll(0f));
            Assert.AreEqual(new Vector2(1f, 0f), StudioCurves.CounterRoll(1f));
            Assert.AreEqual(1f, StudioCurves.CounterFlash(0f), 1e-5f);
            Assert.AreEqual(0f, StudioCurves.CounterFlash(1f), 1e-5f);
        }

        [Test]
        public void Stamp_SlamsFrom1Point6_UndershootsTo0Point9_LandsOnOne()
        {
            Assert.AreEqual(1.6f, StudioCurves.Stamp(0f), 1e-5f);
            Assert.AreEqual(0.9f, Min(StudioCurves.Stamp), 1e-3f);
            Assert.AreEqual(1f, StudioCurves.Stamp(1f), 1e-5f);
            Assert.AreEqual(1f, StudioCurves.StampAlpha(0.25f), 1e-5f);
        }

        [Test]
        public void Press_SinksTo0Point92_ReleaseOvershootsAndSettles()
        {
            Assert.AreEqual(1f, StudioCurves.PressScale(0f), 1e-5f);
            Assert.AreEqual(0.92f, StudioCurves.PressScale(1f), 1e-5f);
            Assert.AreEqual(0.92f, StudioCurves.ReleaseScale(0f, 0.92f), 1e-5f);
            Assert.AreEqual(1f, StudioCurves.ReleaseScale(1f, 0.92f), 1e-5f);
            Assert.Greater(Max(u => StudioCurves.ReleaseScale(u, 0.92f)), 1.03f, "a springy overshoot");
            Assert.Less(Max(u => StudioCurves.ReleaseScale(u, 0.92f)), 1.06f);
        }

        [Test]
        public void Intro_PopsFromZeroThrough1Point15_BottomRowFirst_Within0Point6s()
        {
            Assert.AreEqual(0f, StudioCurves.IntroScale(0f), 1e-5f);
            Assert.AreEqual(1f, StudioCurves.IntroScale(1f), 1e-5f);
            Assert.AreEqual(1.15f, Max(StudioCurves.IntroScale), 1e-3f);
            Assert.AreEqual(0f, StudioCurves.IntroDelay(0), 1e-5f);
            Assert.AreEqual(0.18f, StudioCurves.IntroDelay(6), 1e-5f);
            Assert.LessOrEqual(StudioCurves.IntroTotal(7), 0.6f);
        }

        [Test]
        public void Hint_WigglesTwiceWithin4Degrees_AndStops()
        {
            Assert.AreEqual(0f, StudioCurves.HintWiggle(0f), 1e-5f);
            Assert.AreEqual(0f, StudioCurves.HintWiggle(1f), 1e-4f);
            Assert.LessOrEqual(Max(u => Mathf.Abs(StudioCurves.HintWiggle(u))), 4f);
            Assert.Greater(Max(u => Mathf.Abs(StudioCurves.HintWiggle(u))), 2f);
            Assert.AreEqual(5f, StudioCurves.HintDelay);
        }

        [Test]
        public void LowAmmo_IsLessThanTwoCheapestBlocks()
        {
            Assert.IsTrue(StudioCurves.LowAmmo(9, 5));
            Assert.IsFalse(StudioCurves.LowAmmo(10, 5));
            Assert.IsFalse(StudioCurves.LowAmmo(3, 0), "no block: nothing to warn about");
        }

        [Test]
        public void CollectPitch_RisesSixPercentPerArrival_CapsAtSeven()
        {
            Assert.AreEqual(1f, StudioCurves.CollectPitch(0), 1e-5f);
            Assert.AreEqual(1.12f, StudioCurves.CollectPitch(2), 1e-5f);
            Assert.AreEqual(1.42f, StudioCurves.CollectPitch(30), 1e-5f);
        }

        [Test]
        public void CandyWords_GrowWithDepth_Capped()
        {
            Assert.AreEqual(60f, StudioCurves.WordSizeCss(2), 1e-5f);
            Assert.AreEqual(76f, StudioCurves.WordSizeCss(9), 1e-5f);
            Assert.AreEqual(1.25f, Max(StudioCurves.WordScale), 1e-3f);
            Assert.AreEqual(0f, StudioCurves.WordAlpha(1f), 1e-5f);
            Assert.AreEqual(0.55f, StudioCurves.EdgeGlowAlpha(9), 1e-5f);
        }
    }
}
```

Append to `Tests/EditMode/PotaSoundsTests.cs` (add `using UnityEngine;` if missing):
```csharp
        private static float Peak(AudioClip c)
        {
            var d = new float[c.samples];
            c.GetData(d, 0);
            float m = 0f;
            foreach (float s in d) m = Mathf.Max(m, Mathf.Abs(s));
            return m;
        }

        [Test]
        public void CollectAndDone_AreShortAudibleSynths()
        {
            var collect = PotaSounds.MakeCollect();
            Assert.AreEqual("topla", collect.name);
            Assert.AreEqual(0.14f, collect.length, 0.01f);
            Assert.Greater(Peak(collect), 0.2f);
            var done = PotaSounds.MakeDone();
            Assert.AreEqual("tamam", done.name);
            Assert.AreEqual(0.42f, done.length, 0.01f);
            Assert.Greater(Peak(done), 0.2f);
            Assert.LessOrEqual(Peak(done), 1f);
        }
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.StudioCurvesTests Project.Tests.PotaSoundsTests`
Expected: exit 2, `error CS0103: The name 'StudioCurves' does not exist`.

- [ ] **Step 3: Implement**

`Scripts/ColorHunt/Studio/StudioCurves.cs`:
```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The studio polish's numbers and pure curves (spec 2026-09-30 studio polish §3). Times are real seconds; u ∈ [0, 1] is a phase's
    /// progress; canvas distances are CSS px of the 390-px phone (× ColorHuntHud.Css on the canvas).
    /// </summary>
    public static class StudioCurves
    {
        // Sticker flight (§3.1)
        public const float LaunchTime = 0.12f, HangTime = 0.06f, TravelTime = 0.5f, StaggerTime = 0.05f;
        public const float StreamGap = 0.3f, LaunchLiftCss = 26f, SparkEvery = 0.035f, StreakWindow = 1f;
        public const float LaunchStart = 0.9f, LaunchPeak = 1.4f, LaunchEnd = 1.2f, ArrivalScale = 0.55f;
        public const float SquashTime = 0.22f, RingTime = 0.3f, RollTime = 0.18f, StampTime = 0.3f, GlowTime = 0.5f;
        // Press, intro, hint, ammo (§3.2–3.5)
        public const float PressTime = 0.08f, ReleaseTime = 0.2f, PressDepth = 0.08f;
        public const float IntroRowDelay = 0.03f, IntroTime = 0.3f, IntroPeak = 1.15f;
        public const float HintDelay = 5f, HintTime = 0.6f, HintAngle = 4f;
        public const float PulseHz = 1f;
        // Candy (A) chain (§2 A)
        public const float WordTime = 0.6f, WordRiseCss = 30f, EdgeTime = 0.35f;

        public static float EaseOutCubic(float u)
        {
            float v = 1f - Mathf.Clamp01(u);
            return 1f - v * v * v;
        }

        /// <summary>0.9 → 1.4 (eased out, at 60 % of the launch) → settles on 1.2.</summary>
        public static float LaunchScale(float u)
        {
            u = Mathf.Clamp01(u);
            return u < 0.6f ? Mathf.Lerp(LaunchStart, LaunchPeak, EaseOutCubic(u / 0.6f))
                            : Mathf.Lerp(LaunchPeak, LaunchEnd, PotaLook.Smoothstep((u - 0.6f) / 0.4f));
        }

        /// <summary>A ±12° wobble that dies out by the end of the launch.</summary>
        public static float LaunchWobble(float u)
        {
            u = Mathf.Clamp01(u);
            return 12f * Mathf.Sin(u * 2f * Mathf.PI) * (1f - u);
        }

        /// <summary>Stream order → fan side: +0.6, −0.6, +0.8, −0.8, … capped at ±1.2.</summary>
        public static float Side(int index)
        {
            float m = Mathf.Min(1.2f, 0.6f + 0.2f * (index / 2));
            return index % 2 == 0 ? m : -m;
        }

        /// <summary>Quadratic Bezier from → to, the control point off the chord by side·0.35·length (+side = the chord's left); eased in (u²).</summary>
        public static Vector2 FlightPoint(Vector2 from, Vector2 to, float side, float u)
        {
            float p = Mathf.Clamp01(u);
            p *= p;
            Vector2 chord = to - from;
            Vector2 normal = chord.sqrMagnitude > 1e-6f ? new Vector2(-chord.y, chord.x).normalized : Vector2.zero;
            Vector2 control = (from + to) * 0.5f + normal * (side * 0.35f * chord.magnitude);
            float q = 1f - p;
            return q * q * from + 2f * q * p * control + p * p * to;
        }

        public static float FlightScale(float u) => Mathf.Lerp(LaunchEnd, ArrivalScale, Mathf.Pow(Mathf.Clamp01(u), 1.5f));
        public static float FlightTilt(float side, float u) => -side * 14f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

        /// <summary>Icon squash and stretch on arrival: wide and low early, springs back through tall to (1, 1).</summary>
        public static Vector2 ArrivalSquash(float u)
        {
            u = Mathf.Clamp01(u);
            float k = Mathf.Sin(u * Mathf.PI * 2.5f) * (1f - u);
            return new Vector2(1f + 0.35f * k, 1f - 0.25f * k);
        }

        /// <summary>Counter roll offsets in text heights: (old, new) — the old number slides up and out, the new one comes up from below.</summary>
        public static Vector2 CounterRoll(float u)
        {
            float e = EaseOutCubic(u);
            return new Vector2(e, e - 1f);
        }

        public static float CounterFlash(float u) => 1f - Mathf.Clamp01(u);

        /// <summary>✓ stamp: 1.6 → 0.9 (accelerating, half the time) → 1.</summary>
        public static float Stamp(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.5f) { float a = u / 0.5f; return Mathf.Lerp(1.6f, 0.9f, a * a); }
            return Mathf.Lerp(0.9f, 1f, PotaLook.Smoothstep((u - 0.5f) / 0.5f));
        }

        public static float StampAlpha(float u) => Mathf.Clamp01(u * 4f);
        public static float RingScale(float u) => Mathf.Lerp(0.6f, 1.9f, EaseOutCubic(u));
        public static float RingAlpha(float u) => 0.9f * (1f - Mathf.Clamp01(u));
        public static float GlowAlpha(float u) => 0.9f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

        public static float PressScale(float p) => 1f - PressDepth * EaseOutCubic(p);

        /// <summary>Release from <paramref name="from"/>: overshoots to ~1.04 and settles on 1.</summary>
        public static float ReleaseScale(float u, float from)
        {
            u = Mathf.Clamp01(u);
            return 1f - (1f - from) * (1f - u) * Mathf.Cos(2.2f * Mathf.PI * u);
        }

        public static float IntroDelay(int rowFromBottom) => IntroRowDelay * rowFromBottom;

        /// <summary>0 → 1.15 (eased out, 70 % of the time) → 1.</summary>
        public static float IntroScale(float u)
        {
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            return u < 0.7f ? Mathf.Lerp(0f, IntroPeak, EaseOutCubic(u / 0.7f))
                            : Mathf.Lerp(IntroPeak, 1f, PotaLook.Smoothstep((u - 0.7f) / 0.3f));
        }

        public static float IntroTotal(int size) => IntroDelay(size - 1) + IntroTime;

        public static float HintWiggle(float u)
        {
            u = Mathf.Clamp01(u);
            return HintAngle * Mathf.Sin(u * 4f * Mathf.PI) * (1f - u);
        }

        public static float HintGlow(float u) => 0.35f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

        public static float Pulse(float time) => 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * PulseHz * time);

        /// <summary>Plan decision 5: warn while the ammo covers less than two of the cheapest block (the last affordable tap).</summary>
        public static bool LowAmmo(int ammo, int cheapest) => cheapest > 0 && ammo < 2 * cheapest;

        public static float CollectPitch(int streak) => 1f + 0.06f * Mathf.Min(Mathf.Max(0, streak), 7);

        public static float WordSizeCss(int depth) => Mathf.Min(76f, 44f + 8f * depth);

        public static float WordScale(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.2f) return Mathf.Lerp(0.3f, 1.25f, EaseOutCubic(u / 0.2f));
            if (u < 0.35f) return Mathf.Lerp(1.25f, 1f, (u - 0.2f) / 0.15f);
            return 1f;
        }

        public static float WordAlpha(float u)
        {
            u = Mathf.Clamp01(u);
            return u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f;
        }

        public static float EdgeGlowAlpha(int depth) => Mathf.Min(0.55f, 0.18f + 0.08f * depth);
        public static float CandyShake(int depth) => 0.03f * depth;
    }
}
```

`PotaSounds.cs`:
1. Add `{ "topla", 0.03f }` to `Limits`.
2. Fields and static synths (after `LastVolume`):
```csharp
        private const int Rate = 44100;
        private AudioClip _collect, _done;

        /// <summary>"topla": two bright blips (1320 then 1980 Hz) — a sticker lands in its box.</summary>
        public static AudioClip MakeCollect() => Synth("topla", 0.14f,
            t => 0.5f * Mathf.Sin(2f * Mathf.PI * 1320f * t) * Env(t, 0.09f, 30f)
               + 0.35f * Mathf.Sin(2f * Mathf.PI * 1980f * t) * Env(t - 0.03f, 0.1f, 34f));

        /// <summary>"tamam": a rising triangle arpeggio G5–B5–E6 — a target is done.</summary>
        public static AudioClip MakeDone() => Synth("tamam", 0.42f, t =>
        {
            float s = 0f;
            float[] f = { 784f, 988f, 1319f };
            for (int k = 0; k < f.Length; k++) { float tk = t - 0.06f * k; s += Tri(f[k] * tk) * Env(tk, 0.28f, 12f); }
            return 0.35f * s;
        });

        public static AudioClip Synth(string name, float seconds, System.Func<float, float> fn)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(fn(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Env(float t, float dur, float decay) => t < 0f || t > dur ? 0f : Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-decay * t);

        private static float Tri(float cycles)
        {
            float f = cycles - Mathf.Floor(cycles);
            return 4f * Mathf.Abs(f - 0.5f) - 1f;
        }

        public void Collect(int streak) => Play("topla", _collect, 0.8f, StudioCurves.CollectPitch(streak));
        public void TargetDone() => Play("tamam", _done, 0.9f, 1f);
```
3. In `Awake`, after the voices loop: `_collect = MakeCollect(); _done = MakeDone();`
4. `Play` gains a pitch parameter (−1 = the sample jitter / 1 rule as before):
```csharp
        private void Play(string id, AudioClip clip, float volume, float pitch = -1f)
        {
            if (clip == null || !Allow(_last, id, Time.unscaledTime)) return;
            LastClip = clip;
            LastVolume = volume;
            if (_voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = pitch > 0f ? pitch : IsSample(id) ? Random.Range(0.97f, 1.03f) : 1f;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }
```

- [ ] **Step 4: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.StudioCurvesTests Project.Tests.PotaSoundsTests` → Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/ColorHunt/Studio Assets/_Project/Games/Pota/Scripts/ColorHunt/Studio.meta \
  Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaSounds.cs Assets/_Project/Games/Pota/Tests/EditMode/StudioCurvesTests.cs* \
  Assets/_Project/Games/Pota/Tests/EditMode/PotaSoundsTests.cs
git commit -m "feat: StudioCurves — the polish's pure curves (launch, flight, squash, roll, stamp, press, intro, hint); topla/tamam synths

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The sticker's flight to the HUD — launch, stream, arrival, counter roll, ✓ stamp

**Files:**
- Create: `Scripts/ColorHunt/Studio/CollectFlight.cs`
- Modify: `Scripts/ColorHunt/ColorHuntHud.cs`
- Test: `Tests/PlayMode/CollectFlightPlayTests.cs`

**Interfaces:**
- Consumes: `StudioCurves` (Task 6), `PotaSounds.Collect/TargetDone` (Task 6), `ColorHuntGame.Studio` (Task 5),
  `StudioLookPlayTests.LoadLook/WaitUntil` (Task 5).
- Produces: `ColorHuntHud.Collect` (`CollectFlight`), `ColorHuntHud.FlyingCount` includes studio flyers;
  `CollectFlight.Fly(int color, Vector3 world, Action onArrive) -> bool`, `.Flying`, `.Arrived`, `.LastStreak`, `.Departures`,
  `.Animating(int color)`, `.CountChanged(int color, string old, bool done)`, `.StampTick(int color)`, `.ClearAll()`.
- Produces (internal to `Pota.Runtime`): `ColorHuntHud.CanvasPoint(Vector3, out Vector2)`, `CanvasPointOf(RectTransform)`,
  `TryTarget(int, out Image, out Text, out Text)`, `FlyersRoot`, `TargetPanel`, `AmmoText`, `AmmoPanel`, `HudFont` (Tasks 8, 10 use them).

- [ ] **Step 1: Write the failing PlayMode tests**

`Tests/PlayMode/CollectFlightPlayTests.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>The studio sticker flight (spec §3.1) in the Color Hunt scene.</summary>
    public class CollectFlightPlayTests
    {
        private static int Rules(ColorHuntGame game, int color)
        {
            foreach (var t in game.Board.Targets) if (t.Color == color) return t.Remaining;
            return 0;
        }

        /// <summary>An affordable block of a target colour that still counts, or −1.</summary>
        private static int TargetCell(ColorHuntGame game, out int color)
        {
            var b = game.Board;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i].Hp > b.Ammo) continue;
                foreach (var t in b.Targets)
                    if (t.Color == b.Cells[i].Color && t.Remaining > 0) { color = t.Color; return i; }
            }
            color = -1;
            return -1;
        }

        [UnityTest]
        public IEnumerator ThreeStickersAtOnce_LeaveFiftyMsApart_AndAllArrive()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out int color);
            Assert.GreaterOrEqual(cell, 0);
            for (int k = 0; k < 3; k++) Assert.IsTrue(hud.Collect.Fly(color, game.CellWorld(cell), null));
            var starts = new List<float>(hud.Collect.Departures);
            starts.Sort();
            Assert.AreEqual(3, starts.Count);
            Assert.AreEqual(StudioCurves.StaggerTime, starts[1] - starts[0], 1e-4f);
            Assert.AreEqual(StudioCurves.StaggerTime, starts[2] - starts[1], 1e-4f);
            int before = hud.Collect.Arrived;
            yield return StudioLookPlayTests.WaitUntil(() => hud.Collect.Flying == 0, 3f);
            Assert.AreEqual(before + 3, hud.Collect.Arrived);
            Assert.AreEqual(2, hud.Collect.LastStreak, "three arrivals within a second: streak 0, 1, 2");
            Assert.AreEqual("topla", game.Juice.Sounds.LastClip.name, "no tap, nothing else sounded");
        }

        [UnityTest]
        public IEnumerator LongChain_EveryStickerArrives_CountMatchesRules_Candy() => LongChain(PuzzleLook.Candy);

        [UnityTest]
        public IEnumerator LongChain_EveryStickerArrives_CountMatchesRules_Shatter() => LongChain(PuzzleLook.Shatter);

        /// <summary>Spec §1.4: the count drops once per arrival and ends on the rules, in every studio look (Pota: ColorHuntHudPlayTests).</summary>
        private static IEnumerator LongChain(PuzzleLook look)
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(look, (g, h) => { game = g; hud = h; });
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            float t = 0f;
            while ((game.Busy || game.ItemsInFlight > 0 || hud.FlyingCount > 0) && t < 10f)
            {
                foreach (var target in game.Board.Targets)
                    Assert.GreaterOrEqual(game.ShownTarget(target.Color), Rules(game, target.Color), "never below the rules");
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
            foreach (var target in game.Board.Targets)
                Assert.AreEqual(Rules(game, target.Color), game.ShownTarget(target.Color), "settled: shown = rules");
            Assert.AreEqual(0, hud.FlyingCount);
        }

        [UnityTest]
        public IEnumerator NewRound_WhileStickersFly_KeepsTheNewCounts()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out _);
            Assert.GreaterOrEqual(cell, 0);
            Assert.AreEqual("broken", game.Tap(cell));
            yield return StudioLookPlayTests.WaitUntil(() => hud.Collect.Flying > 0, 3f);
            Assert.Greater(hud.Collect.Flying, 0, "a sticker is in the air");
            game.NewRound();
            yield return StudioLookPlayTests.WaitUntil(() => false, 1.5f);
            Assert.AreEqual(0, hud.Collect.Flying);
            foreach (var target in game.Board.Targets)
                Assert.AreEqual(Rules(game, target.Color), game.ShownTarget(target.Color));
        }

        [UnityTest]
        public IEnumerator Arrival_AnimatesTheRow()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out int color);
            Assert.AreEqual("broken", game.Tap(cell));
            int before = hud.Collect.Arrived;
            yield return StudioLookPlayTests.WaitUntil(() => hud.Collect.Arrived > before, 4f);
            Assert.Greater(hud.Collect.Arrived, before);
            Assert.IsTrue(hud.Collect.Animating(color), "squash / roll running on the arrival frame");
        }

        [UnityTest]
        public IEnumerator PotaLook_KeepsItsOwnFlight()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            int cell = TargetCell(game, out _);
            Assert.AreEqual("broken", game.Tap(cell));
            bool flew = false;
            float t = 0f;
            while (t < 3f && !(flew && hud.FlyingCount == 0))
            {
                flew |= hud.FlyingCount > 0;
                Assert.AreEqual(0, hud.Collect.Flying, "the studio flight stays off under Pota");
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(flew);
        }
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.CollectFlightPlayTests`
Expected: exit 2, `error CS1061: 'ColorHuntHud' does not contain a definition for 'Collect'`.

- [ ] **Step 3: HUD hooks** (`ColorHuntHud.cs`)

1. Properties/internals (next to `FlyingCount`):
```csharp
        /// <summary>The studio sticker flight (looks other than Pota).</summary>
        public CollectFlight Collect { get; private set; }
        public int FlyingCount => _flying.Count + (Collect != null ? Collect.Flying : 0);

        internal RectTransform FlyersRoot => _flyers;
        internal RectTransform TargetPanel => _targetPanel;
        internal RectTransform AmmoPanel => _ammoPanel;
        internal Text AmmoText => _ammo;
        internal Font HudFont => _font;

        /// <summary>A world point on the canvas (centre origin, canvas units); false when behind the camera.</summary>
        internal bool CanvasPoint(Vector3 world, out Vector2 point)
        {
            point = Vector2.zero;
            var cam = Game != null ? Game.Cam : null;
            if (cam == null) return false;
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0f) return false;
            var rect = ((RectTransform)transform).rect;
            point = new Vector2((vp.x - 0.5f) * rect.width, (vp.y - 0.5f) * rect.height);
            return true;
        }

        internal Vector2 CanvasPointOf(RectTransform t) =>
            ((RectTransform)transform).InverseTransformPoint(t.TransformPoint(t.rect.center));

        internal bool TryTarget(int color, out Image icon, out Text count, out Text tick)
        {
            foreach (var r in _rows)
                if (r.Color == color) { icon = r.Icon; count = r.Count; tick = r.Tick; return true; }
            icon = null; count = null; tick = null;
            return false;
        }
```
(Delete the old `public int FlyingCount => _flying.Count;` line.)
2. `Bind`, after `WireButtons();`:
```csharp
            Collect = GetComponent<CollectFlight>();
            if (Collect == null) Collect = gameObject.AddComponent<CollectFlight>();
            Collect.Init(this);
```
3. `FlyItem`, first lines of the body:
```csharp
            if (Game != null && Game.Studio && Collect != null) return Collect.Fly(color, world, onArrive);
```
and replace its viewport/From computation with `if (!CanvasPoint(world, out var from)) return false;` + `f.From = from;` (the checks for
`row`, `row.Icon.sprite` and `_flyers` stay).
4. `OnStarted`: before `BuildTargets();` add `Collect?.ClearAll();`.
5. `OnTargets` loop body becomes:
```csharp
            bool studio = Game.Studio && Collect != null;
            foreach (var row in _rows)
            {
                int shown = Game.ShownTarget(row.Color);
                string s = shown.ToString();
                bool done = shown == 0;
                string old = row.Count.text;
                if (old != s)
                {
                    row.Count.text = s;
                    if (studio) Collect.CountChanged(row.Color, old, done);
                    else _pops[row.Count.transform] = 0f;
                }
                row.Count.color = done ? PotaLook.HudGreen : PotaLook.HudGold;
                bool stamped = done && !row.Tick.enabled;
                row.Tick.enabled = done;
                if (stamped && studio) Collect.StampTick(row.Color);
            }
```

- [ ] **Step 4: `CollectFlight`**

`Scripts/ColorHunt/Studio/CollectFlight.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// The studio sticker flight (every look but Pota, spec §3.1). The sticker launches from the burst (0.9 → 1.4 → 1.2, a ±12° wobble,
    /// a short hang), flies an accelerating curve to its target icon trailing sparkles; departures leave 0.05 s apart so a chain reads as
    /// a stream fanning left and right. On arrival the icon squashes, a star burst and a ring pop, a rising "topla" plays and the game
    /// lowers the count; the count rolls (old number up and out, new one in, white → gold); at zero the ✓ is stamped and the panel glows
    /// green. Real time: hit-stops do not freeze the HUD. A new round drops everything without arrivals (the game's flight generation
    /// already ignores late callbacks).
    /// </summary>
    public class CollectFlight : MonoBehaviour
    {
        private class Flyer
        {
            public RectTransform T;
            public Image Img;
            public Vector2 From, To;
            public float Side, Start, NextSpark;
            public int Color;
            public Action Done;
        }

        private class Spark
        {
            public RectTransform T;
            public Image Img;
            public Vector2 V;
            public float Age, Life, Spin;
        }

        private class RowFx
        {
            public float Squash = -1f, Ring = -1f, Roll = -1f, Stamp = -1f;
            public Image RingImg;
            public Text Old;
            public Vector2 CountHome;
            public Color Final;
        }

        private const int SparkMax = 64;
        private ColorHuntHud _hud;
        private readonly List<Flyer> _flying = new List<Flyer>();
        private readonly Stack<Flyer> _flyerPool = new Stack<Flyer>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly Stack<Spark> _sparkPool = new Stack<Spark>();
        private readonly Dictionary<int, RowFx> _rows = new Dictionary<int, RowFx>();
        private Image _glow;
        private float _glowT = -1f;
        private float _lastDeparture = -10f, _lastArrival = -10f;
        private int _stream, _streak;
        private Sprite _star, _ring;

        public int Flying => _flying.Count;
        public int Arrived { get; private set; }
        public int LastStreak => _streak;
        public IEnumerable<float> Departures { get { foreach (var f in _flying) yield return f.Start; } }

        public bool Animating(int color) =>
            _rows.TryGetValue(color, out var fx) && (fx.Squash >= 0f || fx.Roll >= 0f || fx.Stamp >= 0f);

        public void Init(ColorHuntHud hud)
        {
            _hud = hud;
            var s = hud.S;
            if (s != null && s.StarSprite != null) _star = Sprite.Create(s.StarSprite, new Rect(0, 0, s.StarSprite.width, s.StarSprite.height), new Vector2(0.5f, 0.5f));
            if (s != null && s.SoftRing != null) _ring = Sprite.Create(s.SoftRing, new Rect(0, 0, s.SoftRing.width, s.SoftRing.height), new Vector2(0.5f, 0.5f));
        }

        public bool Fly(int color, Vector3 world, Action onArrive)
        {
            if (_hud == null || !_hud.TryTarget(color, out var icon, out _, out _) || icon.sprite == null) return false;
            if (!_hud.CanvasPoint(world, out var from)) return false;
            float now = Time.unscaledTime;
            if (now - _lastDeparture > StudioCurves.StreamGap) _stream = 0;
            float start = Mathf.Max(now, _lastDeparture + StudioCurves.StaggerTime);
            _lastDeparture = start;
            var f = _flyerPool.Count > 0 ? _flyerPool.Pop() : NewFlyer();
            f.From = from;
            f.To = _hud.CanvasPointOf(icon.rectTransform);
            f.Side = StudioCurves.Side(_stream++);
            f.Start = start;
            f.NextSpark = 0f;
            f.Color = color;
            f.Done = onArrive;
            f.Img.sprite = icon.sprite;
            f.T.anchoredPosition = from;
            f.T.localScale = Vector3.one * StudioCurves.LaunchStart;
            f.T.localRotation = Quaternion.identity;
            f.T.gameObject.SetActive(false);
            _flying.Add(f);
            return true;
        }

        /// <summary>The HUD lowered a target's count from <paramref name="old"/>: roll it; <paramref name="done"/> = it reached zero.</summary>
        public void CountChanged(int color, string old, bool done)
        {
            var fx = Row(color);
            if (fx == null) return;
            fx.Old.text = old;
            fx.Old.enabled = true;
            fx.Final = done ? PotaLook.HudGreen : PotaLook.HudGold;
            fx.Roll = 0f;
        }

        public void StampTick(int color)
        {
            var fx = Row(color);
            if (fx == null || !_hud.TryTarget(color, out _, out _, out var tick)) return;
            fx.Stamp = 0f;
            _glowT = 0f;
            Vector2 at = _hud.CanvasPointOf(tick.rectTransform);
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI / 5f;
                EmitSpark(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (220f * ColorHuntHud.Css), 0.4f, 30f);
            }
            Sounds()?.TargetDone();
        }

        public void ClearAll()
        {
            foreach (var f in _flying) { f.T.gameObject.SetActive(false); _flyerPool.Push(f); }
            _flying.Clear();
            foreach (var s in _sparks) { s.T.gameObject.SetActive(false); _sparkPool.Push(s); }
            _sparks.Clear();
            _rows.Clear();                 // their texts and rings are children of the rows BuildTargets destroys
            _glowT = -1f;
            if (_glow != null) _glow.enabled = false;
            _stream = 0;
        }

        private void Update()
        {
            if (_hud == null) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                float t = now - f.Start;
                if (t < 0f) continue;
                f.T.gameObject.SetActive(true);
                Vector2 lifted = f.From + new Vector2(0f, StudioCurves.LaunchLiftCss * ColorHuntHud.Css);
                if (t < StudioCurves.LaunchTime)
                {
                    float u = t / StudioCurves.LaunchTime;
                    f.T.anchoredPosition = Vector2.Lerp(f.From, lifted, StudioCurves.EaseOutCubic(u));
                    f.T.localScale = Vector3.one * StudioCurves.LaunchScale(u);
                    f.T.localRotation = Quaternion.Euler(0f, 0f, StudioCurves.LaunchWobble(u));
                    continue;
                }
                float v = (t - StudioCurves.LaunchTime - StudioCurves.HangTime) / StudioCurves.TravelTime;
                if (v < 0f)
                {
                    f.T.anchoredPosition = lifted;
                    f.T.localScale = Vector3.one * StudioCurves.LaunchEnd;
                    f.T.localRotation = Quaternion.identity;
                    continue;
                }
                if (v >= 1f)
                {
                    _flying.RemoveAt(i);
                    Arrive(f);
                    continue;
                }
                var p = StudioCurves.FlightPoint(lifted, f.To, f.Side, v);
                f.T.anchoredPosition = p;
                f.T.localScale = Vector3.one * StudioCurves.FlightScale(v);
                f.T.localRotation = Quaternion.Euler(0f, 0f, StudioCurves.FlightTilt(f.Side, v));
                if (now >= f.NextSpark)
                {
                    f.NextSpark = now + StudioCurves.SparkEvery;
                    EmitSpark(p, UnityEngine.Random.insideUnitCircle * (40f * ColorHuntHud.Css), 0.25f, 20f);
                }
            }
            TickSparks(dt);
            TickRows(dt);
        }

        private void Arrive(Flyer f)
        {
            f.T.gameObject.SetActive(false);
            _flyerPool.Push(f);
            float now = Time.unscaledTime;
            _streak = now - _lastArrival < StudioCurves.StreakWindow ? _streak + 1 : 0;
            _lastArrival = now;
            Arrived++;
            var fx = Row(f.Color);
            if (fx != null) { fx.Squash = 0f; fx.Ring = 0f; }
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f + 0.3f;
                EmitSpark(f.To, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (180f * ColorHuntHud.Css), 0.3f, 26f);
            }
            Sounds()?.Collect(_streak);
            f.Done?.Invoke();
        }

        private void TickRows(float dt)
        {
            foreach (var kv in _rows)
            {
                if (!_hud.TryTarget(kv.Key, out var icon, out var count, out var tick)) continue;
                var fx = kv.Value;
                if (fx.Squash >= 0f)
                {
                    fx.Squash += dt / StudioCurves.SquashTime;
                    var s = StudioCurves.ArrivalSquash(fx.Squash);
                    icon.rectTransform.localScale = new Vector3(s.x, s.y, 1f);
                    if (fx.Squash >= 1f) { fx.Squash = -1f; icon.rectTransform.localScale = Vector3.one; }
                }
                if (fx.Ring >= 0f && fx.RingImg != null)
                {
                    fx.Ring += dt / StudioCurves.RingTime;
                    fx.RingImg.enabled = fx.Ring < 1f;
                    fx.RingImg.rectTransform.anchoredPosition = icon.rectTransform.anchoredPosition;
                    fx.RingImg.rectTransform.localScale = Vector3.one * StudioCurves.RingScale(fx.Ring);
                    fx.RingImg.color = new Color(1f, 0.93f, 0.6f, StudioCurves.RingAlpha(fx.Ring));
                    if (fx.Ring >= 1f) fx.Ring = -1f;
                }
                if (fx.Roll >= 0f)
                {
                    fx.Roll += dt / StudioCurves.RollTime;
                    float u = Mathf.Min(1f, fx.Roll);
                    var r = StudioCurves.CounterRoll(u);
                    float h = count.rectTransform.rect.height;
                    count.rectTransform.anchoredPosition = fx.CountHome + new Vector2(0f, r.y * h);
                    fx.Old.rectTransform.anchoredPosition = fx.CountHome + new Vector2(0f, r.x * h);
                    fx.Old.color = new Color(PotaLook.HudGold.r, PotaLook.HudGold.g, PotaLook.HudGold.b, 1f - u);
                    count.color = Color.Lerp(fx.Final, Color.white, StudioCurves.CounterFlash(u));
                    if (fx.Roll >= 1f)
                    {
                        fx.Roll = -1f;
                        fx.Old.enabled = false;
                        count.rectTransform.anchoredPosition = fx.CountHome;
                        count.color = fx.Final;
                    }
                }
                if (fx.Stamp >= 0f)
                {
                    fx.Stamp += dt / StudioCurves.StampTime;
                    float u = Mathf.Min(1f, fx.Stamp);
                    tick.rectTransform.localScale = Vector3.one * StudioCurves.Stamp(u);
                    var c = tick.color;
                    c.a = StudioCurves.StampAlpha(u);
                    tick.color = c;
                    if (fx.Stamp >= 1f) { fx.Stamp = -1f; tick.rectTransform.localScale = Vector3.one; }
                }
            }
            if (_glowT >= 0f && _glow != null)
            {
                _glowT += dt / StudioCurves.GlowTime;
                _glow.enabled = _glowT < 1f;
                _glow.color = new Color(PotaLook.HudGreen.r, PotaLook.HudGreen.g, PotaLook.HudGreen.b, StudioCurves.GlowAlpha(_glowT));
                if (_glowT >= 1f) _glowT = -1f;
            }
        }

        private void TickSparks(float dt)
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Age += dt;
                float u = s.Age / s.Life;
                if (u >= 1f) { s.T.gameObject.SetActive(false); _sparks.RemoveAt(i); _sparkPool.Push(s); continue; }
                s.T.anchoredPosition += s.V * dt;
                s.V *= Mathf.Max(0f, 1f - 3f * dt);
                s.T.localScale = Vector3.one * (1f - u);
                s.T.localRotation = Quaternion.Euler(0f, 0f, s.Spin * s.Age);
                s.Img.color = new Color(1f, 0.95f, 0.7f, 1f - u);
            }
        }

        private RowFx Row(int color)
        {
            if (_rows.TryGetValue(color, out var fx)) return fx;
            if (_hud == null || !_hud.TryTarget(color, out var icon, out var count, out _)) return null;
            fx = new RowFx { CountHome = count.rectTransform.anchoredPosition, Final = PotaLook.HudGold };
            fx.Old = Instantiate(count, count.transform.parent);
            fx.Old.name = "CountOld";
            fx.Old.enabled = false;
            if (_ring != null)
            {
                var go = new GameObject("ArrivalRing", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(icon.transform.parent, false);
                fx.RingImg = go.GetComponent<Image>();
                fx.RingImg.sprite = _ring;
                fx.RingImg.raycastTarget = false;
                fx.RingImg.enabled = false;
                var rt = fx.RingImg.rectTransform;
                rt.anchorMin = rt.anchorMax = icon.rectTransform.anchorMin;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = icon.rectTransform.sizeDelta * 1.6f;
            }
            if (_glow == null && _hud.TargetPanel != null)
            {
                var go = new GameObject("TargetGlow", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_hud.TargetPanel, false);
                _glow = go.GetComponent<Image>();
                _glow.sprite = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(18f * ColorHuntHud.Css), Color.clear, Color.clear,
                    Color.white, Mathf.RoundToInt(5f * ColorHuntHud.Css), 0f, 0);
                _glow.type = _glow.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                _glow.raycastTarget = false;
                _glow.enabled = false;
                var rt = _glow.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.one * (-6f * ColorHuntHud.Css);
                rt.offsetMax = Vector2.one * (6f * ColorHuntHud.Css);
            }
            _rows[color] = fx;
            return fx;
        }

        private Flyer NewFlyer()
        {
            var go = new GameObject("StudioFlyer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_hud.FlyersRoot, false);
            var f = new Flyer { T = (RectTransform)go.transform, Img = go.GetComponent<Image>() };
            f.T.anchorMin = f.T.anchorMax = new Vector2(0.5f, 0.5f);
            f.T.sizeDelta = Vector2.one * (56f * ColorHuntHud.Css);
            f.Img.preserveAspect = true;
            f.Img.raycastTarget = false;
            return f;
        }

        private void EmitSpark(Vector2 at, Vector2 velocity, float life, float sizeCss)
        {
            if (_star == null || _sparks.Count >= SparkMax) return;
            Spark s;
            if (_sparkPool.Count > 0) s = _sparkPool.Pop();
            else
            {
                var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_hud.FlyersRoot, false);
                s = new Spark { T = (RectTransform)go.transform, Img = go.GetComponent<Image>() };
                s.T.anchorMin = s.T.anchorMax = new Vector2(0.5f, 0.5f);
                s.Img.sprite = _star;
                s.Img.raycastTarget = false;
            }
            s.T.anchoredPosition = at;
            s.T.sizeDelta = Vector2.one * (sizeCss * ColorHuntHud.Css);
            s.V = velocity;
            s.Age = 0f;
            s.Life = life;
            s.Spin = UnityEngine.Random.Range(-360f, 360f);
            s.T.gameObject.SetActive(true);
            _sparks.Add(s);
        }

        private PotaSounds Sounds() => _hud.Game != null && _hud.Game.Juice != null ? _hud.Game.Juice.Sounds : null;
    }
}
```

- [ ] **Step 5: Run the tests** (the new fixture plus the HUD fixtures: the Pota path must be unchanged)

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.CollectFlightPlayTests Project.Tests.ColorHuntHudPlayTests Project.Tests.PotaFlowPlayTests`
Expected: all pass.

- [ ] **Step 6: Look at it**

Play the ColorHunt scene in the editor with `execute_code` `Object.FindAnyObjectByType<Project.ColorHunt.ColorHuntGame>().SetLook(Project.ColorHunt.PuzzleLook.Candy)`
(C# 6: use `System.Type.GetType` per CLAUDE.md if the type does not resolve), tap a target block with `ai_colorhunt_play`
(`{"action":"tap","cell":<n>}`), then take three Game-view screenshots about 0.25 s apart (`manage_camera` screenshot,
`output_folder: "Temp/mcp_out"`) during the flight and one on arrival. Check: stickers visibly fan out and trail sparkles, the icon
squashes, the number rolls. A visible flaw (sparks too big, sticker hidden behind a panel, number jumping) is fixed now (ledger note).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/ColorHunt/Studio/CollectFlight.cs* Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntHud.cs \
  Assets/_Project/Games/Pota/Tests/PlayMode/CollectFlightPlayTests.cs* Assets/_Project/Games/Pota/Tests/PlayMode/StudioLookPlayTests.cs*
git commit -m "feat: studio sticker flight — launch, fanned stream with sparkles, icon squash, star burst, counter roll, ✓ stamp

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Press feedback and the studio ammo counter

**Files:**
- Modify: `Scripts/ColorHunt/BlockView.cs` (press, wiggle), `Scripts/ColorHunt/ColorHuntGame.cs` (`Select`, `Selected`, `SelectionChanged`,
  `NewView` reset), `Scripts/ColorHunt/ColorHuntHud.cs` (`AmmoCounter` wiring)
- Create: `Scripts/ColorHunt/Studio/AmmoCounter.cs`
- Test: `Tests/PlayMode/StudioPolishPlayTests.cs`

**Interfaces:**
- Consumes: `StudioCurves` (Task 6), HUD internals `AmmoText`, `AmmoPanel` (Task 7), `StudioLookPlayTests.LoadLook/WaitUntil`.
- Produces: `BlockView.Pressed` (bool), `BlockView.Wiggle` (float, degrees; Task 9 drives it), `BlockView.PressScale` (float, read-only),
  `BlockView.ResetStudio()`; `ColorHuntGame.Selected` (int), `ColorHuntGame.SelectionChanged` (event Action);
  `ColorHuntHud.AmmoCounter` (`AmmoCounter`: `.Rolling`, `.Pulsing`, `.GhostShort`, `.GhostText`).

- [ ] **Step 1: Write the failing tests**

`Tests/PlayMode/StudioPolishPlayTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>Press feedback, ammo counter, board intro and idle hint (spec §3.2–3.5).</summary>
    public class StudioPolishPlayTests
    {
        private static IEnumerator Wait(float seconds) => StudioLookPlayTests.WaitUntil(() => false, seconds);

        [UnityTest]
        public IEnumerator Press_SinksTheBlock_ShowsItsCost_AndSpringsBack()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = ColorHuntHint.Best(game.Board);
            var view = game.ViewAt(cell);
            game.Select(cell);
            yield return Wait(0.2f);
            Assert.IsTrue(view.Pressed);
            Assert.AreEqual(StudioCurves.PressScale(1f), view.PressScale, 1e-3f);
            Assert.AreEqual("−" + game.Board.Cells[cell].Hp, hud.AmmoCounter.GhostText);
            Assert.IsFalse(hud.AmmoCounter.GhostShort);
            game.Select(-1);
            float most = 0f, t = 0f;
            while (t < 0.35f) { most = Mathf.Max(most, view.PressScale); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(most, 1.02f, "a springy release");
            Assert.AreEqual(1f, view.PressScale, 1e-4f);
            Assert.AreEqual("", hud.AmmoCounter.GhostText);
        }

        [UnityTest]
        public IEnumerator PressingABlockTheAmmoCannotCover_ShowsARedCost()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            game.Prepare(1, 7, null);
            game.Begin();
            yield return null;
            int cell = -1;
            for (int i = 0; i < game.Board.Cells.Length && cell < 0; i++) if (game.Board.Cells[i].Hp > 1) cell = i;
            Assert.GreaterOrEqual(cell, 0);
            game.Select(cell);
            yield return null;
            Assert.IsTrue(hud.AmmoCounter.GhostShort);
        }

        [UnityTest]
        public IEnumerator PotaLook_KeepsPotasSelection()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            int cell = ColorHuntHint.Best(game.Board);
            game.Select(cell);
            yield return Wait(0.2f);
            Assert.IsFalse(game.ViewAt(cell).Pressed);
            Assert.AreEqual(1f, game.ViewAt(cell).PressScale, 1e-5f);
            Assert.AreEqual("", hud.AmmoCounter.GhostText);
        }

        [UnityTest]
        public IEnumerator Salvo_RollsTheAmmoDown()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = -1;
            for (int i = 0; i < game.Board.Cells.Length && cell < 0; i++)
                if (game.Board.Cells[i].Hp >= 5 && game.Board.Cells[i].Hp <= game.Board.Ammo) cell = i;
            Assert.AreEqual("broken", game.Tap(cell));
            bool rolled = false;
            float t = 0f;
            while (t < 0.6f) { rolled |= hud.AmmoCounter.Rolling; t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(rolled);
        }

        [UnityTest]
        public IEnumerator LowAmmo_Pulses()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            game.Prepare(158, 7, null);
            int cheapest = ColorHuntRules.Cheapest(game.Board);
            game.Prepare(2 * cheapest - 1, 7, null);
            game.Begin();
            Assume.That(ColorHuntRules.Cheapest(game.Board) * 2 > game.Board.Ammo, "the same deal with less ammo");
            yield return null;
            yield return null;
            Assert.IsTrue(hud.AmmoCounter.Pulsing);
        }
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.StudioPolishPlayTests`
Expected: exit 2, `error CS1061: 'BlockView' does not contain a definition for 'Pressed'`.

- [ ] **Step 3: Implement**

`BlockView.cs`:
1. Fields (after `LabelAngle`):
```csharp
        /// <summary>Studio press: the finger is on this block (the game sets it only for looks other than Pota).</summary>
        public bool Pressed;
        /// <summary>Studio idle hint: z tilt in degrees.</summary>
        public float Wiggle;
        /// <summary>The press factor applied this frame (1 = none).</summary>
        public float PressScale { get; private set; } = 1f;
        private float _press, _release = -1f, _releaseFrom = 1f;

        public void ResetStudio()
        {
            Pressed = false;
            Wiggle = 0f;
            _press = 0f;
            _release = -1f;
            PressScale = 1f;
        }
```
2. In `Tick`, replace `transform.localScale = Vector3.one * Mathf.Max(0.001f, own * Scale);` with:
```csharp
            float press = 1f;
            if (Pressed)
            {
                _release = -1f;
                _press = Mathf.Min(1f, _press + dt / StudioCurves.PressTime);
                press = StudioCurves.PressScale(_press);
                Flash = Mathf.Max(Flash, 0.18f);                 // a soft glow under the finger
            }
            else if (_press > 0f)
            {
                if (_release < 0f) { _release = 0f; _releaseFrom = StudioCurves.PressScale(_press); }
                _release += dt / StudioCurves.ReleaseTime;
                press = StudioCurves.ReleaseScale(_release, _releaseFrom);
                if (_release >= 1f) { _press = 0f; _release = -1f; press = 1f; }
            }
            PressScale = press;
            transform.localScale = Vector3.one * Mathf.Max(0.001f, own * Scale * press);
            transform.localRotation = Quaternion.Euler(0f, 0f, Wiggle);
```

`ColorHuntGame.cs`:
1. Public API:
```csharp
        public int Selected => _selected;
        public event Action SelectionChanged;
```
2. `Select` becomes (keep any line of the original that is not shown here after `Juice?.Select();`):
```csharp
        public void Select(int cell)
        {
            if (cell == _selected) return;
            if (_selected >= 0 && _views.TryGetValue(_selected, out var previous)) previous.Pressed = false;
            _selected = cell;
            ShowRing(cell);
            if (cell >= 0 && _views.TryGetValue(cell, out var v))
            {
                v.Squash = Mathf.Max(v.Squash, 0.35f);
                Juice?.Select();
                if (Studio) v.Pressed = true;
            }
            Raise(SelectionChanged);
        }
```
3. `NewView`: after the line that resets `v.Home … v.LabelAngle = 0f;` add `v.ResetStudio();`.

`Scripts/ColorHunt/Studio/AmmoCounter.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// Studio ammo counter (spec §3.2, §3.5): while a block is pressed its cost floats right of the counter as "−12" (red when the ammo
    /// does not cover it); during the salvo the number rolls down step by step; with the ammo short of two of the cheapest block the
    /// counter pulses red at 1 Hz (plan decision 5) and stays red while a lost round waits for its card. Off under the Pota look.
    /// </summary>
    public class AmmoCounter : MonoBehaviour
    {
        private ColorHuntHud _hud;
        private Text _old, _ghost;
        private Vector2 _home;
        private int _last = -1;
        private float _roll = -1f, _ghostAlpha, _ghostTarget;
        private bool _wasStudio, _wasPulsing;

        public bool Rolling => _roll >= 0f;
        public bool Pulsing { get; private set; }
        public bool GhostShort { get; private set; }
        public string GhostText => _ghost != null && _ghostTarget > 0f ? _ghost.text : "";

        public void Init(ColorHuntHud hud)
        {
            _hud = hud;
            var game = hud.Game;
            var ammo = hud.AmmoText;
            if (game == null || ammo == null) return;
            game.AmmoChanged += OnAmmo;
            game.SelectionChanged += OnSelection;
            game.RoundStarted += () => { _last = -1; StopRoll(); };
            _home = ammo.rectTransform.anchoredPosition;
            _old = Instantiate(ammo, ammo.transform.parent);
            _old.name = "AmmoOld";
            _old.enabled = false;
            _ghost = Instantiate(ammo, hud.AmmoPanel);
            _ghost.name = "AmmoCost";
            var rt = _ghost.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(8f * ColorHuntHud.Css, -4f * ColorHuntHud.Css);
            _ghost.alignment = TextAnchor.MiddleLeft;
            _ghost.fontSize = Mathf.RoundToInt(24f * ColorHuntHud.Css);
            _ghost.text = "";
            _ghost.enabled = false;
        }

        private void OnAmmo()
        {
            var game = _hud.Game;
            int v = game.DisplayedAmmo;
            if (game.Studio && _last >= 0 && v < _last)
            {
                _old.text = _last.ToString();
                _old.enabled = true;
                _roll = 0f;
            }
            _last = v;
        }

        private void OnSelection()
        {
            var game = _hud.Game;
            int sel = game.Selected;
            if (game.Studio && game.Board != null && sel >= 0 && sel < game.Board.Cells.Length)
            {
                int hp = game.Board.Cells[sel].Hp;
                GhostShort = hp > game.Board.Ammo;
                _ghost.text = "−" + hp;
                _ghost.color = GhostShort ? PotaLook.HudRed : PotaLook.HudGold;
                _ghostTarget = 1f;
            }
            else _ghostTarget = 0f;
        }

        private void Update()
        {
            if (_hud == null || _hud.Game == null || _ghost == null) return;
            var game = _hud.Game;
            var ammo = _hud.AmmoText;
            float dt = Time.unscaledDeltaTime;
            bool studio = game.Studio;
            if (!studio && _wasStudio) { StopRoll(); ammo.color = PotaLook.HudGold; ammo.transform.localScale = Vector3.one; _ghostTarget = 0f; }
            _wasStudio = studio;
            _ghostAlpha = Mathf.MoveTowards(_ghostAlpha, _ghostTarget, dt / (_ghostTarget > _ghostAlpha ? 0.08f : 0.12f));
            var gc = _ghost.color;
            gc.a = _ghostAlpha;
            _ghost.color = gc;
            _ghost.enabled = _ghostAlpha > 0.001f;
            if (!studio) return;
            if (_roll >= 0f)
            {
                _roll += dt / StudioCurves.RollTime;
                float u = Mathf.Min(1f, _roll);
                var r = StudioCurves.CounterRoll(u);
                float h = ammo.rectTransform.rect.height * 0.6f;
                ammo.rectTransform.anchoredPosition = _home + new Vector2(0f, r.y * h);
                _old.rectTransform.anchoredPosition = _home + new Vector2(0f, r.x * h);
                var oc = _old.color;
                oc.a = 1f - u;
                _old.color = oc;
                if (_roll >= 1f) StopRoll();
            }
            int cheapest = game.Board != null ? ColorHuntRules.Cheapest(game.Board) : 0;
            Pulsing = game.Board != null
                      && ((game.Current == ColorHuntGame.State.Playing && StudioCurves.LowAmmo(game.DisplayedAmmo, cheapest))
                          || (game.Current == ColorHuntGame.State.Ended && !game.Won));
            if (Pulsing)
            {
                float pulse = StudioCurves.Pulse(Time.unscaledTime);
                ammo.color = Color.Lerp(PotaLook.HudGold, PotaLook.HudRed, 0.35f + 0.65f * pulse);
                ammo.transform.localScale = Vector3.one * (1f + 0.06f * pulse);
            }
            else if (_wasPulsing)
            {
                ammo.color = PotaLook.HudGold;
                ammo.transform.localScale = Vector3.one;
            }
            _wasPulsing = Pulsing;
        }

        private void StopRoll()
        {
            _roll = -1f;
            if (_old != null) _old.enabled = false;
            if (_hud != null && _hud.AmmoText != null) _hud.AmmoText.rectTransform.anchoredPosition = _home;
        }
    }
}
```

`ColorHuntHud.cs`: property `public AmmoCounter AmmoCounter { get; private set; }` and in `Bind`, after the `Collect` lines:
```csharp
            AmmoCounter = GetComponent<AmmoCounter>();
            if (AmmoCounter == null) AmmoCounter = gameObject.AddComponent<AmmoCounter>();
            AmmoCounter.Init(this);
```

- [ ] **Step 4: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.StudioPolishPlayTests Project.Tests.ColorHuntInputPlayTests Project.Tests.ColorHuntHudPlayTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/ColorHunt Assets/_Project/Games/Pota/Tests/PlayMode/StudioPolishPlayTests.cs*
git commit -m "feat: studio press feedback (sink, glow, spring back) and ammo counter (cost ghost, roll, low-ammo pulse)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Board intro and idle hint

**Files:**
- Modify: `Scripts/ColorHunt/ColorHuntGame.cs`
- Test: `Tests/PlayMode/StudioPolishPlayTests.cs`

**Interfaces:**
- Consumes: `StudioCurves.IntroScale/IntroDelay/IntroTotal/IntroTime/HintDelay/HintTime/HintWiggle/HintGlow`, `BlockView.Wiggle`,
  `ColorHuntHint.Best`.
- Produces: `ColorHuntGame.Intro` (bool), `ColorHuntGame.HintCell` (int, −1 when no hint runs); `Busy` is true during the intro.

- [ ] **Step 1: Write the failing tests** (append to `StudioPolishPlayTests`)

```csharp
        [UnityTest]
        public IEnumerator Intro_PopsTheBoardInFromTheBottomRow()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            game.NewRound();
            yield return null;
            yield return null;
            Assert.IsTrue(game.Intro);
            Assert.IsTrue(game.Busy, "no taps during the intro");
            int n = game.Board.Size;
            var bottom = game.ViewAt(ColorHuntRules.Index(n, n - 1, 0));
            var top = game.ViewAt(ColorHuntRules.Index(n, 0, 0));
            Assert.Greater(bottom.Scale, top.Scale, "the bottom row pops first");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 1f);
            Assert.IsFalse(game.Intro);
            Assert.AreEqual(1f, top.Scale, 1e-5f);
            Assert.IsFalse(game.Busy);
        }

        [UnityTest]
        public IEnumerator IdleHint_WigglesTheBestBlock_AfterFiveSeconds_AndStopsOnTouch()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            float start = Time.unscaledTime;
            yield return StudioLookPlayTests.WaitUntil(() => game.HintCell >= 0, 6.5f);
            Assert.GreaterOrEqual(Time.unscaledTime - start, StudioCurves.HintDelay - 0.1f);
            int cell = game.HintCell;
            Assert.AreEqual(ColorHuntHint.Best(game.Board), cell);
            var view = game.ViewAt(cell);
            bool wiggled = false;
            float t = 0f;
            while (t < 0.3f) { wiggled |= Mathf.Abs(view.Wiggle) > 0.5f; t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(wiggled);
            game.Select(cell);
            yield return null;
            Assert.AreEqual(-1, game.HintCell);
            Assert.AreEqual(0f, view.Wiggle);
        }

        [UnityTest]
        public IEnumerator PotaLook_DealsWithoutIntro_AndNeverHints()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => game = g);
            game.NewRound();
            yield return null;
            Assert.IsFalse(game.Intro);
            yield return Wait(StudioCurves.HintDelay + 0.6f);
            Assert.AreEqual(-1, game.HintCell);
        }
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.StudioPolishPlayTests`
Expected: exit 2, `error CS1061: 'ColorHuntGame' does not contain a definition for 'Intro'`.

- [ ] **Step 3: Implement** (`ColorHuntGame.cs`)

1. State and API:
```csharp
        private float _intro = -1f, _idle, _hintT = -1f;
        private int _hintCell = -1;

        /// <summary>Studio board intro running (spec §3.3): the board is busy, taps wait.</summary>
        public bool Intro => _intro >= 0f;
        /// <summary>The block the idle hint is wiggling, −1 when none (spec §3.4).</summary>
        public int HintCell => _hintT >= 0f ? _hintCell : -1;
```
2. `Busy`:
```csharp
        public bool Busy => _jobs.Count > 0 || (Salvo != null && Salvo.Busy) || _swells.AnyLoose || _intro >= 0f;
```
3. `NewRound(int ammo)`:
```csharp
        public void NewRound(int ammo)
        {
            Prepare(ammo);
            if (Studio) StartIntro();
            Begin();
        }
```
4. `Update`, in the playing branch: right after `AdvanceJobs(dt);` add `AdvanceIntro(dt);`, and right after `AdvanceViews(dt);` add
`TickHint(Time.unscaledDeltaTime);`.
5. `ClearViews`: add `_intro = -1f; StopHint(); _idle = 0f;` before `_jobs.Clear();`.
6. Methods:
```csharp
        private void StartIntro()
        {
            _intro = 0f;
            foreach (var v in _views.Values) v.Scale = 0f;
        }

        /// <summary>Rows pop in bottom first, 0.03 s apart, each 0 → 1.15 → 1 in 0.3 s.</summary>
        private void AdvanceIntro(float dt)
        {
            if (_intro < 0f || Board == null) return;
            _intro += dt;
            int n = Board.Size;
            foreach (var kv in _views)
            {
                int fromBottom = n - 1 - ColorHuntRules.Row(n, kv.Key);
                kv.Value.Scale = StudioCurves.IntroScale((_intro - StudioCurves.IntroDelay(fromBottom)) / StudioCurves.IntroTime);
            }
            if (_intro < StudioCurves.IntroTotal(n)) return;
            _intro = -1f;
            foreach (var v in _views.Values) v.Scale = 1f;
        }

        /// <summary>After 5 s without a touch the best move wiggles (±4°, twice) and glows; again every 5 s; any touch or job stops it.</summary>
        private void TickHint(float dt)
        {
            if (!Studio || Current != State.Playing || Busy || _selected >= 0) { _idle = 0f; StopHint(); return; }
            if (_hintT >= 0f)
            {
                _hintT += dt;
                float u = _hintT / StudioCurves.HintTime;
                if (_views.TryGetValue(_hintCell, out var v))
                {
                    v.Wiggle = StudioCurves.HintWiggle(u);
                    v.Flash = Mathf.Max(v.Flash, StudioCurves.HintGlow(u));
                }
                if (u >= 1f) { StopHint(); _idle = 0f; }
                return;
            }
            _idle += dt;
            if (_idle < StudioCurves.HintDelay) return;
            _hintCell = ColorHuntHint.Best(Board);
            if (_hintCell >= 0) _hintT = 0f;
            else _idle = 0f;
        }

        private void StopHint()
        {
            if (_hintCell >= 0 && _views.TryGetValue(_hintCell, out var v)) v.Wiggle = 0f;
            _hintCell = -1;
            _hintT = -1f;
        }
```

- [ ] **Step 4: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.StudioPolishPlayTests Project.Tests.ColorHuntPlayTests Project.Tests.PotaFlowPlayTests`
Expected: all pass. Then the whole PlayMode suite: `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → all pass.

- [ ] **Step 5: Look at it**

Screenshot the Game view three times during a Candy `NewRound` (at ~0.1, 0.25, 0.45 s: `execute_code` `game.NewRound()` then
`manage_camera` screenshots) and once during a hint (wait 5.2 s untouched). Check: rows pop bottom-up with a small overshoot; the
hinted block visibly tilts and glows. Fix visible flaws now (ledger note).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs Assets/_Project/Games/Pota/Tests/PlayMode/StudioPolishPlayTests.cs
git commit -m "feat: studio board intro (rows pop in bottom-up) and idle hint (best move wiggles after 5 s)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: A "Şeker patlaması" (`CandyBurst`)

**Files:**
- Create: `Shaders/PotaFlipbook.shader`, `Shaders/CandyShard.shader`
- Create: `Scripts/ColorHunt/Bursts/StudioParticles.cs`, `Scripts/ColorHunt/Bursts/StudioTextures.cs`, `Scripts/ColorHunt/Bursts/CandyBurst.cs`,
  `Scripts/ColorHunt/Bursts/CandyHud.cs`
- Modify: `Scripts/ColorHunt/ColorHuntSettings.cs` (templates), `Editor/PotaMaterialTemplates.cs`, `Scripts/ColorHunt/ColorHuntGame.cs` (`CreateStyle`)
- Test: `Tests/EditMode/CandyTests.cs`, `Tests/PlayMode/CandyPlayTests.cs`

**Interfaces:**
- Consumes: `IBurstStyle`, `BurstBudget`, `ColorHuntGame.UseStyle/SetLook/Style` (Task 5); `StudioCurves` (Task 6); HUD internals
  `CanvasPoint`, `HudFont` (Task 7); `ColorHuntSettings.CandyPuff/CandyRing` (Task 4); `ColorHuntFx.Confetti(int n, int count)`,
  `ColorHuntJuice.Shake/TimeCtl`.
- Produces: `StudioParticles` (static: `System`, `Emit`, `Emit3D`, `Sheet`, `Gravity`, `SizeOverLife`, `AlphaOverLife`, `Flipbook`,
  `Additive`, `Count`), `StudioTextures` (static: `RayStar`, `EdgeGlow`, `EdgeGlowSprite`, `CandyChunk`) — Task 11 reuses both;
  `ColorHuntSettings.FlipbookTemplate`, `CandyShardTemplate`; `CandyBurst` (`Ready`, `Hud`, `AssetsReady(ColorHuntSettings)`);
  `CandyHud` (`WordFor(int)`, `WordColor(int)`, `LastWord`, `EdgeAlpha`, `Word(int, Vector3)`, `EdgeGlow(Color, int)`, `Clear()`).

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/CandyTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class CandyTests
    {
        [Test]
        public void Words_GrowWithTheChain()
        {
            Assert.IsNull(CandyHud.WordFor(1));
            Assert.AreEqual("İyi!", CandyHud.WordFor(2));
            Assert.AreEqual("Süper!", CandyHud.WordFor(3));
            Assert.AreEqual("Harika!", CandyHud.WordFor(4));
            Assert.AreEqual("Muhteşem!", CandyHud.WordFor(5));
            Assert.AreEqual("Muhteşem!", CandyHud.WordFor(9));
            Assert.AreNotEqual(CandyHud.WordColor(2), CandyHud.WordColor(5));
        }

        [Test]
        public void RayStar_HasACore_EightRays_AndClearCorners()
        {
            var t = StudioTextures.RayStar(128, 8);
            Assert.Greater(t.GetPixel(64, 64).a, 0.95f, "core");
            Assert.Less(t.GetPixel(0, 0).a, 0.02f, "corner");
            Assert.Greater(t.GetPixel(64 + 44, 64).a, 0.5f, "a ray on the x axis");
            float between = t.GetPixel(64 + Mathf.RoundToInt(44 * Mathf.Cos(Mathf.PI / 8f)), 64 + Mathf.RoundToInt(44 * Mathf.Sin(Mathf.PI / 8f))).a;
            Assert.Less(between, 0.15f, "dark between two rays");
        }

        [Test]
        public void EdgeGlow_IsClearInTheMiddle_BrightAtTheEdges()
        {
            var t = StudioTextures.EdgeGlow(256);
            Assert.Less(t.GetPixel(128, 128).a, 0.01f);
            Assert.Greater(t.GetPixel(1, 128).a, 0.8f);
            Assert.Greater(t.GetPixel(128, 254).a, 0.8f);
        }

        [Test]
        public void CandyChunk_IsAFacetedIcosahedron_FacingOut()
        {
            var m = StudioTextures.CandyChunk();
            Assert.AreEqual(60, m.vertexCount);
            Assert.AreEqual(60, m.triangles.Length);
            var v = m.vertices;
            var n = m.normals;
            var tri = m.triangles;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Assert.AreEqual(n[tri[i]], n[tri[i + 1]], "flat facets");
                Vector3 centre = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
                Assert.Greater(Vector3.Dot(n[tri[i]], centre), 0f, "normals face out");
                Vector3 wound = Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]);
                Assert.Greater(Vector3.Dot(wound, n[tri[i]]), 0f, "Unity winding matches the normal");
            }
        }

        [Test]
        public void WithoutTheFlipbooks_CandyIsNotReady()
        {
            var go = new GameObject("Game");
            try
            {
                var game = go.AddComponent<ColorHuntGame>();
                game.S = ScriptableObject.CreateInstance<ColorHuntSettings>();
                Assert.IsFalse(CandyBurst.AssetsReady(game.S));
                var styleGo = new GameObject("CandyBurst");
                styleGo.transform.SetParent(go.transform);
                var candy = styleGo.AddComponent<CandyBurst>();
                candy.Build(game);
                Assert.IsFalse(candy.Ready);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
```

`Tests/PlayMode/CandyPlayTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>A "Şeker patlaması" in the Color Hunt scene (spec §2 A).</summary>
    public class CandyPlayTests
    {
        private static int Live(CandyBurst candy, string system) =>
            candy.transform.Find(system).GetComponent<ParticleSystem>().particleCount;

        [UnityTest]
        public IEnumerator Tap_PuffsFlashesAndThrowsChunks_WithinBudget()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            var candy = game.Style as CandyBurst;
            Assert.IsNotNull(candy, "the Candy style is ready (ai_studio_import ran)");
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            int puff = 0, flash = 0, chunks = 0, most = 0;
            float t = 0f;
            while (t < 1.5f)
            {
                puff = Mathf.Max(puff, Live(candy, "CandyPuff"));
                flash = Mathf.Max(flash, Live(candy, "CandyFlash"));
                chunks = Mathf.Max(chunks, Live(candy, "CandyChunks"));
                most = Mathf.Max(most, candy.LiveCount);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Greater(puff, 0);
            Assert.Greater(flash, 0);
            Assert.GreaterOrEqual(chunks, 6);
            Assert.LessOrEqual(most, BurstBudget.Max);
        }

        [UnityTest]
        public IEnumerator ChainPeakFive_SaysMuhtesem_GlowsSlowsAndThrowsConfetti()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            var candy = (CandyBurst)game.Style;
            candy.ChainPeak(5, Vector2.zero, PotaLook.Tones[0]);
            yield return null;
            yield return null;
            Assert.AreEqual("Muhteşem!", candy.Hud.LastWord);
            Assert.Greater(candy.Hud.EdgeAlpha, 0f);
            Assert.Less(Time.timeScale, 0.99f, "0.25 s slow motion");
            Assert.Greater(game.Fx.LiveCount, 0, "confetti");
            Assert.Greater(Live(candy, "CandyRings"), 0);
        }

        [UnityTest]
        public IEnumerator TwelveBurstsAndAChainAtOnce_StayUnderTheCap()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            var candy = (CandyBurst)game.Style;
            for (int i = 0; i < 12; i++) candy.StepBurst(game.ViewAt(i), PotaLook.Tones[i % 5], 6);
            candy.ChainPeak(6, Vector2.zero, PotaLook.Tones[1]);
            int most = 0;
            float t = 0f;
            while (t < 1f) { most = Mathf.Max(most, candy.LiveCount); t += Time.unscaledDeltaTime; yield return null; }
            Assert.LessOrEqual(most, BurstBudget.Max);
        }
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CandyTests`
Expected: exit 2, `error CS0103: The name 'CandyHud' does not exist`.

- [ ] **Step 3: Shaders**

`Shaders/PotaFlipbook.shader`:
```hlsl
// Studio looks: a flipbook sheet (TextureSheetAnimation UVs) tinted by the particle colour, straight alpha blending.
Shader "Project/PotaFlipbook"
{
    Properties
    {
        _BaseMap ("Sheet", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Flipbook"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(t.rgb * i.color.rgb, t.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
```

`Shaders/CandyShard.shader`:
```hlsl
// A's candy chunks: the particle colour (linear, per particle) with a two-tone toon ramp, a rim and an inverted-hull ink outline.
Shader "Project/CandyShard"
{
    Properties
    {
        _ShadeColor ("Shade (multiplies the tone in shadow)", Color) = (0.62, 0.6, 0.78, 1)
        _ShadeThreshold ("Shade Threshold", Range(-1, 1)) = 0.15
        _RimStrength ("Rim", Range(0, 1)) = 0.35
        _OutlineColor ("Outline", Color) = (0.078, 0.106, 0.18, 1)
        _OutlineWidth ("Outline Width (m)", Range(0, 0.1)) = 0.022
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _ShadeColor;
            half _ShadeThreshold;
            half _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "CandyForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                half lit = smoothstep(_ShadeThreshold - 0.03h, _ShadeThreshold + 0.03h, dot(n, light.direction));
                half3 tone = i.color.rgb;
                half3 color = tone * lerp(_ShadeColor.rgb, half3(1, 1, 1), lit) * light.color;
                color += tone * SampleSH(n) * 0.35h;
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                color += pow(1.0h - saturate(dot(n, viewDir)), 3.0h) * _RimStrength * lit;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz) + normalize(TransformObjectToWorldNormal(v.normalOS)) * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(p);
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 4: Helpers, style and HUD**

`Scripts/ColorHunt/Bursts/StudioParticles.cs`:
```csharp
using System;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>ParticleSystem plumbing for the studio looks: world-space, game-time systems emitted by hand (no emission module).</summary>
    public static class StudioParticles
    {
        public static ParticleSystem System(Transform parent, string name, Material mat, ParticleSystemRenderMode mode, int max, int order,
            bool linearColors = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // AddComponent starts playing (CLAUDE.md lesson)
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;                                     // hit-stops and slow-mo freeze them too
            main.startSpeed = 0f;
            main.startSize3D = mode == ParticleSystemRenderMode.Mesh;
            main.startRotation3D = mode == ParticleSystemRenderMode.Mesh;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.sortingOrder = order;
            r.applyActiveColorSpace = !linearColors;
            r.alignment = mode == ParticleSystemRenderMode.Mesh ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        public static void Emit(ParticleSystem ps, Vector3 pos, float size, float life, Color color, Vector3 velocity = default,
            float rotation = 0f)
        {
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = pos, velocity = velocity, startSize = size, startLifetime = life, startColor = color, rotation = rotation,
                applyShapeToPosition = false,
            }, 1);
        }

        public static void Emit3D(ParticleSystem ps, Vector3 pos, Vector3 velocity, Vector3 size, Vector3 rotation, Vector3 spin,
            float life, Color color)
        {
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = pos, velocity = velocity, startSize3D = size, rotation3D = rotation, angularVelocity3D = spin,
                startLifetime = life, startColor = color, applyShapeToPosition = false,
            }, 1);
        }

        /// <summary>Plays the whole cols × rows sheet once over each particle's life.</summary>
        public static void Sheet(ParticleSystem ps, int cols, int rows)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = cols;
            tsa.numTilesY = rows;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            tsa.cycleCount = 1;
        }

        /// <summary>Downward acceleration in world units/s² (gravityModifier is relative to Physics.gravity 9.81).</summary>
        public static void Gravity(ParticleSystem ps, float g)
        {
            var main = ps.main;
            main.gravityModifier = g / 9.81f;
        }

        public static void SizeOverLife(ParticleSystem ps, Func<float, float> f, int keys = 24)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= keys; i++) curve.AddKey(i / (float)keys, f(i / (float)keys));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        public static void AlphaOverLife(ParticleSystem ps, Func<float, float> f)
        {
            var keys = new GradientAlphaKey[8];
            for (int i = 0; i < keys.Length; i++) { float u = i / (float)(keys.Length - 1); keys[i] = new GradientAlphaKey(f(u), u); }
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, keys);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        public static Material Flipbook(ColorHuntSettings s, Texture sheet)
        {
            var m = RuntimeMaterial.From(s.FlipbookTemplate, "Project/PotaFlipbook", "PotaFlipbook");
            m.SetTexture("_BaseMap", sheet);
            return m;
        }

        public static Material Additive(ColorHuntSettings s, Texture tex, float intensity)
        {
            var m = RuntimeMaterial.From(s.AdditiveTemplate, "Project/PotaAdditive", "StudioAdditive");
            m.SetTexture("_BaseMap", tex != null ? tex : Texture2D.whiteTexture);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        public static int Count(params ParticleSystem[] systems)
        {
            int n = 0;
            foreach (var ps in systems) if (ps != null) n += ps.particleCount;
            return n;
        }
    }
}
```

`Scripts/ColorHunt/Bursts/StudioTextures.cs`:
```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Small assets the studio looks make in code (cached): A's ray star and edge glow, a faceted candy chunk.</summary>
    public static class StudioTextures
    {
        private static Texture2D _rayStar, _edge;
        private static Sprite _edgeSprite;
        private static Mesh _chunk;

        /// <summary>White star: a soft core plus <paramref name="rays"/> sharp rays fading out; the alpha carries the shape.</summary>
        public static Texture2D RayStar(int size = 128, int rays = 8)
        {
            if (_rayStar != null && _rayStar.width == size) return _rayStar;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    float core = Mathf.Clamp01(1f - r / 0.28f);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * rays * 0.5f)), 28f) * Mathf.Clamp01(1f - r);
                    float alpha = Mathf.Clamp01(core * core + ray);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            _rayStar = Make("RayStar", size, px);
            return _rayStar;
        }

        /// <summary>Clear in the middle, opaque at the screen edges (squared ramp from 55 % of the way out).</summary>
        public static Texture2D EdgeGlow(int size = 256)
        {
            if (_edge != null && _edge.width == size) return _edge;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    float t = Mathf.Clamp01((Mathf.Max(dx, dy) - 0.55f) / 0.45f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(t * t * 255f));
                }
            _edge = Make("EdgeGlow", size, px);
            return _edge;
        }

        public static Sprite EdgeGlowSprite()
        {
            if (_edgeSprite != null) return _edgeSprite;
            var t = EdgeGlow();
            _edgeSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
            return _edgeSprite;
        }

        /// <summary>A faceted candy chunk: an icosahedron of radius 0.5 with split vertices (flat normals), wound for Unity.</summary>
        public static Mesh CandyChunk()
        {
            if (_chunk != null) return _chunk;
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var p = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < p.Length; i++) p[i] = p[i].normalized * 0.5f;
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var verts = new Vector3[60];
            var normals = new Vector3[60];
            var tris = new int[60];
            for (int k = 0; k < 20; k++)
            {
                Vector3 a = p[f[3 * k]], b = p[f[3 * k + 1]], c = p[f[3 * k + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, a + b + c) < 0f) { var s = b; b = c; c = s; n = -n; }
                verts[3 * k] = a; verts[3 * k + 1] = b; verts[3 * k + 2] = c;
                normals[3 * k] = normals[3 * k + 1] = normals[3 * k + 2] = n;
                tris[3 * k] = 3 * k; tris[3 * k + 1] = 3 * k + 1; tris[3 * k + 2] = 3 * k + 2;
            }
            _chunk = new Mesh { name = "CandyChunk", vertices = verts, normals = normals, triangles = tris };
            _chunk.RecalculateBounds();
            return _chunk;
        }

        private static Texture2D Make(string name, int size, Color32[] px)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
```

`Scripts/ColorHunt/Bursts/CandyHud.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>A's chain words (İyi!, Süper!, Harika!, Muhteşem!) and the screen-edge glow, on the HUD canvas, real time.</summary>
    public class CandyHud : MonoBehaviour
    {
        private class Word { public Text T; public float Age = -1f; public Vector2 Home; }

        private ColorHuntHud _hud;
        private readonly Word[] _words = new Word[3];
        private int _next;
        private Image _edge;
        private float _edgeAge = -1f, _edgePeak;
        private Color _edgeColor;

        public string LastWord { get; private set; }
        public float EdgeAlpha => _edge != null && _edge.enabled ? _edge.color.a : 0f;

        public static string WordFor(int depth) =>
            depth >= 5 ? "Muhteşem!" : depth == 4 ? "Harika!" : depth == 3 ? "Süper!" : depth == 2 ? "İyi!" : null;

        public static Color WordColor(int depth) =>
            depth >= 5 ? PotaLook.Hex(0xb35cff) : depth == 4 ? PotaLook.Hex(0xff4d6d) : depth == 3 ? PotaLook.HudOrange : PotaLook.HudGold;

        public static CandyHud Attach(ColorHuntGame game)
        {
            if (game.Hud == null) return null;
            var go = new GameObject("CandyHud", typeof(RectTransform));
            go.transform.SetParent(game.Hud.transform, false);
            go.transform.SetAsFirstSibling();                       // under the HUD panels
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var hud = go.AddComponent<CandyHud>();
            hud.Init(game.Hud);
            return hud;
        }

        private void Init(ColorHuntHud hud)
        {
            _hud = hud;
            var edgeGo = new GameObject("EdgeGlow", typeof(RectTransform), typeof(Image));
            edgeGo.transform.SetParent(transform, false);
            _edge = edgeGo.GetComponent<Image>();
            _edge.sprite = StudioTextures.EdgeGlowSprite();
            _edge.raycastTarget = false;
            _edge.enabled = false;
            var ert = _edge.rectTransform;
            ert.anchorMin = Vector2.zero;
            ert.anchorMax = Vector2.one;
            ert.offsetMin = ert.offsetMax = Vector2.zero;
            for (int i = 0; i < _words.Length; i++)
            {
                var go = new GameObject("Word", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
                go.transform.SetParent(transform, false);
                var t = go.GetComponent<Text>();
                t.font = hud.HudFont;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                var outline = go.GetComponent<Outline>();
                outline.effectColor = PotaLook.HudNavy;
                outline.effectDistance = new Vector2(3f, -3f) * ColorHuntHud.Css;
                var shadow = go.GetComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
                shadow.effectDistance = new Vector2(0f, -5f) * ColorHuntHud.Css;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(600f, 200f);
                go.SetActive(false);
                _words[i] = new Word { T = t };
            }
        }

        public void Word(int depth, Vector3 world)
        {
            string s = WordFor(depth);
            if (s == null || _hud == null || !_hud.CanvasPoint(world, out var p)) return;
            var w = _words[_next];
            _next = (_next + 1) % _words.Length;
            w.T.text = s;
            w.T.color = WordColor(depth);
            w.T.fontSize = Mathf.RoundToInt(StudioCurves.WordSizeCss(depth) * ColorHuntHud.Css);
            w.Home = p;
            w.Age = 0f;
            w.T.rectTransform.anchoredPosition = p;
            w.T.rectTransform.localScale = Vector3.one * StudioCurves.WordScale(0f);
            w.T.gameObject.SetActive(true);
            LastWord = s;
        }

        public void EdgeGlow(Color tone, int depth)
        {
            if (_edge == null) return;
            _edgeColor = tone;
            _edgePeak = StudioCurves.EdgeGlowAlpha(depth);
            _edgeAge = 0f;
            _edge.enabled = true;
            var c = tone;
            c.a = _edgePeak;
            _edge.color = c;
        }

        public void Clear()
        {
            foreach (var w in _words) { if (w == null) continue; w.Age = -1f; w.T.gameObject.SetActive(false); }
            _edgeAge = -1f;
            if (_edge != null) _edge.enabled = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var w in _words)
            {
                if (w == null || w.Age < 0f) continue;
                w.Age += dt;
                float u = w.Age / StudioCurves.WordTime;
                if (u >= 1f) { w.Age = -1f; w.T.gameObject.SetActive(false); continue; }
                w.T.rectTransform.anchoredPosition = w.Home + new Vector2(0f, StudioCurves.WordRiseCss * ColorHuntHud.Css * u);
                w.T.rectTransform.localScale = Vector3.one * StudioCurves.WordScale(u);
                var c = w.T.color;
                c.a = StudioCurves.WordAlpha(u);
                w.T.color = c;
            }
            if (_edgeAge < 0f) return;
            _edgeAge += dt;
            float e = _edgeAge / StudioCurves.EdgeTime;
            if (e >= 1f) { _edgeAge = -1f; _edge.enabled = false; return; }
            var ec = _edgeColor;
            ec.a = _edgePeak * (1f - e) * (1f - e);
            _edge.color = ec;
        }
    }
}
```

`Scripts/ColorHunt/Bursts/CandyBurst.cs`:
```csharp
using Project.Rendering;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// A "Şeker patlaması" (spec §2 A): the block turns into an inked cartoon puff (Blender flipbook), an eight-ray white flash,
    /// 6–10 fat outlined candy chunks thrown in arcs, twinkling stars. Each chain step adds an inked ring wave, a word
    /// (İyi!, Süper!, Harika!, Muhteşem!), a screen-edge glow in the chain's colour and a stronger camera kick; 5+ adds confetti and a
    /// 0.25 s ×0.5 slow motion. World-space particles on game time; the words and the glow are on the HUD (CandyHud).
    /// </summary>
    public class CandyBurst : MonoBehaviour, IBurstStyle
    {
        public const int PuffMax = 24, FlashMax = 16, ChunkMax = 120, TwinkleMax = 64, RingMax = 16;

        private ColorHuntGame _game;
        private ParticleSystem _puff, _flash, _chunks, _twinkles, _rings;
        private CandyHud _hud;
        private readonly System.Random _rng = new System.Random(11);

        public bool Ready { get; private set; }
        public bool ShowsChainNumber => false;
        public CandyHud Hud => _hud;
        public int LiveCount => StudioParticles.Count(_puff, _flash, _chunks, _twinkles, _rings)
                                + (_game != null && _game.Fx != null ? _game.Fx.LiveCount : 0);

        public static bool AssetsReady(ColorHuntSettings s) =>
            s != null && s.CandyPuff != null && s.CandyRing != null && s.StarSprite != null;

        public void Build(ColorHuntGame game)
        {
            _game = game;
            Ready = AssetsReady(game.S);
            if (!Ready) return;
            var s = game.S;
            _puff = StudioParticles.System(transform, "CandyPuff", StudioParticles.Flipbook(s, s.CandyPuff), ParticleSystemRenderMode.Billboard, PuffMax, 6);
            StudioParticles.Sheet(_puff, 4, 4);
            _flash = StudioParticles.System(transform, "CandyFlash", StudioParticles.Additive(s, StudioTextures.RayStar(), 1.6f), ParticleSystemRenderMode.Billboard, FlashMax, 8);
            StudioParticles.SizeOverLife(_flash, u => Mathf.Lerp(0.4f, 1.15f, StudioCurves.EaseOutCubic(Mathf.Min(1f, u * 1.6f))));
            StudioParticles.AlphaOverLife(_flash, u => 1f - u);
            _chunks = StudioParticles.System(transform, "CandyChunks", RuntimeMaterial.From(s.CandyShardTemplate, "Project/CandyShard", "CandyShard"),
                ParticleSystemRenderMode.Mesh, ChunkMax, 7, true);
            _chunks.GetComponent<ParticleSystemRenderer>().mesh = StudioTextures.CandyChunk();
            StudioParticles.SizeOverLife(_chunks, u => u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f);
            _twinkles = StudioParticles.System(transform, "CandyTwinkles", StudioParticles.Additive(s, s.StarSprite, 2f), ParticleSystemRenderMode.Billboard, TwinkleMax, 9);
            StudioParticles.SizeOverLife(_twinkles, u => Mathf.Sin(u * Mathf.PI) * (0.75f + 0.25f * Mathf.Sin(u * 25f)));
            _rings = StudioParticles.System(transform, "CandyRings", StudioParticles.Flipbook(s, s.CandyRing), ParticleSystemRenderMode.Billboard, RingMax, 5);
            StudioParticles.Sheet(_rings, 4, 4);
            _hud = CandyHud.Attach(game);
            Activate(false);
        }

        public void Activate(bool on)
        {
            gameObject.SetActive(on);
            if (_hud != null) _hud.gameObject.SetActive(on);
        }

        public void TapBurst(BlockView v, Color tone) => Pop(v.Pos, tone, 0);
        public void StepBurst(BlockView v, Color tone, int depth) => Pop(v.Pos, tone, depth);

        public void ChainPeak(int depth, Vector2 at, Color tone)
        {
            if (!Ready) return;
            float s = Scale;
            StudioParticles.Emit(_rings, World(at, PotaLook.FrontZ + 0.2f), (2.4f + 0.35f * depth) * s, 0.4f, Color.Lerp(Color.white, tone, 0.2f));
            _hud?.Word(depth, World(at + new Vector2(0f, 0.7f), PotaLook.LabelZ));
            _hud?.EdgeGlow(tone, depth);
            _game.Juice?.Shake?.Add(StudioCurves.CandyShake(depth));
            if (depth < 5) return;
            _game.Fx?.Confetti(_game.Board.Size, _game.S.ConfettiCount / 2);
            _game.Juice?.TimeCtl?.SlowMo(0.5f, 0.25f);
        }

        public void Clear()
        {
            foreach (var ps in new[] { _puff, _flash, _chunks, _twinkles, _rings }) if (ps != null) ps.Clear();
            _hud?.Clear();
        }

        private float Scale => _game.BoardRoot.lossyScale.x;
        private Vector3 World(Vector2 pos, float z) => _game.BoardRoot.TransformPoint(new Vector3(pos.x, pos.y, -z));
        private Vector3 Dir(Vector3 boardDir) => _game.BoardRoot.TransformVector(boardDir);
        private float R() => (float)_rng.NextDouble();

        private void Pop(Vector2 pos, Color tone, int depth)
        {
            if (!Ready) return;
            float s = Scale;
            Vector3 at = World(pos, PotaLook.FrontZ + 0.25f);
            StudioParticles.Emit(_puff, at, (2.2f + 0.1f * depth) * s, 0.35f, Color.Lerp(Color.white, tone, 0.25f), Vector3.zero, R() * 360f);
            StudioParticles.Emit(_flash, World(pos, PotaLook.FrontZ + 0.35f), (2.6f + 0.2f * depth) * s, 0.12f, Color.white);
            StudioParticles.Gravity(_chunks, 12f * s);
            var lin = tone.linear;
            int chunks = Mathf.Min(10, 6 + depth + (depth == 0 ? 2 : 0));
            for (int i = 0; i < chunks; i++)
            {
                float a = (i + R() * 0.6f) / chunks * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) + 0.35f, -(0.3f + 0.5f * R()));
                var size = new Vector3(0.26f + 0.14f * R(), 0.18f + 0.1f * R(), 0.22f + 0.1f * R()) * s;
                var spin = new Vector3((R() - 0.5f) * 720f, (R() - 0.5f) * 720f, (R() - 0.5f) * 720f);
                StudioParticles.Emit3D(_chunks, at, Dir(dir) * (3.2f + 2f * R()), size, new Vector3(R() * 360f, R() * 360f, R() * 360f),
                    spin, 0.6f + 0.3f * R(), lin);
            }
            int twinkles = Mathf.Min(8, 4 + depth + (depth == 0 ? 2 : 0));
            for (int i = 0; i < twinkles; i++)
            {
                var off = new Vector2((R() - 0.5f) * 1.4f, (R() - 0.5f) * 1.4f);
                StudioParticles.Emit(_twinkles, World(pos + off, PotaLook.FrontZ + 0.4f), (0.18f + 0.14f * R()) * s, 0.35f + 0.25f * R(),
                    new Color(1f, 0.95f, 0.75f, 1f), Dir(new Vector3(off.x, off.y + 0.5f, 0f)) * 0.8f, R() * 360f);
            }
        }
    }
}
```

`ColorHuntSettings.cs`, in the "Material templates" header after `SelectionTemplate`:
```csharp
        [Tooltip("Project/PotaFlipbook: the studio flipbooks (puff, ring, smoke).")]
        public Material FlipbookTemplate;
        [Tooltip("Project/CandyShard: A's inked candy chunks.")]
        public Material CandyShardTemplate;
```
`PotaMaterialTemplates.Ensure`: after the `textDepth` line:
```csharp
            var flipbook = Template("PotaFlipbook", RuntimeMaterial.From(null, "Project/PotaFlipbook"));
            var candyShard = Template("CandyShard", RuntimeMaterial.From(null, "Project/CandyShard"));
```
and in the ColorHuntSettings loop: `s.FlipbookTemplate = flipbook; s.CandyShardTemplate = candyShard;`; report "8 in".
`ColorHuntGame.CreateStyle`: add `case PuzzleLook.Candy: return AddStyle<CandyBurst>("CandyBurst");`.

- [ ] **Step 5: Compile, make the templates, run the tests**

```bash
~/.local/bin/uv run Tools/mcp_call.py call refresh_unity '{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}'
~/.local/bin/uv run Tools/mcp_call.py call execute_menu_item '{"menu_path":"AI Tools/Pota/Material Templates"}'
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CandyTests Project.Tests.BurstStyleTests Project.Tests.RuntimeShaderBuildTests --no-compile
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.CandyPlayTests --no-compile
```
Expected: all pass (`RuntimeShaderBuildTests` now also sees `Project/PotaFlipbook` and `Project/CandyShard` through the templates).

- [ ] **Step 6: Look at it in slow motion**

With ColorHunt playing, `execute_code`: set the look to Candy, `game.Juice.TimeCtl.SlowMo(0.2f, 5f)`, tap the best cell; take four Game-view
screenshots about 0.15 s apart (`manage_camera`, `output_folder: "Temp/mcp_out"`). Check against spec §2 A: an inked white puff tinted by
the block, a white ray flash, fat outlined chunks in the block's colour arcing out, twinkles; on a chain, the ring, the word and the edge
glow. Fix visible flaws (sizes, tint strength, outline too thin/thick) in `CandyBurst`/the shader defaults with a ledger note.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Games/Pota/Shaders Assets/_Project/Games/Pota/Scripts Assets/_Project/Games/Pota/Editor/PotaMaterialTemplates.cs \
  Assets/_Project/Games/Pota/Materials Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset Assets/_Project/Games/Pota/Tests
git commit -m "feat: A — Candy burst: inked puff flipbook, ray flash, outlined candy chunks, twinkles, ring wave, chain words, edge glow

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: B "Gerçek kırılma" (`ShatterBurst`) — pieces, lit smoke, sparks, camera push

**Files:**
- Create: `Shaders/PotaPiece.shader`, `Scripts/ColorHunt/Bursts/ShatterDebris.cs`, `Scripts/ColorHunt/Bursts/ShatterBurst.cs`
- Modify: `Scripts/ColorHunt/ColorHuntCamera.cs` (push, zoom), `Scripts/ColorHunt/ColorHuntSettings.cs` (`PieceTemplate`),
  `Editor/PotaMaterialTemplates.cs`, `Scripts/ColorHunt/ColorHuntGame.cs` (`CreateStyle`)
- Test: `Tests/EditMode/ShatterTests.cs`, `Tests/EditMode/ColorHuntCameraTests.cs`, `Tests/PlayMode/ShatterPlayTests.cs`,
  `Tests/PlayMode/PotaFlowPlayTests.cs` (one test: the studio look through the PotaFlow transition, spec §5)

**Interfaces:**
- Consumes: `IBurstStyle`, `BurstBudget` (Task 5); `StudioParticles` (Task 10); `ColorHuntSettings.ShatterPieces/ShatterSmoke` (Task 4);
  `LabelAtlas.UvRect/ItemCell/NumberCell`; `PotaLook.BlockColor`; `ColorHuntFx.Glow(Vector2, Color, float, float, float, float)`.
- Produces: `Debris` (struct: `P, V, W, R, Age, Life, Size`), `ShatterDebris` (`Launch`, `Step`, `Shrink`, `InnerHalf` 4.4, `GlassZ` 0.7);
  `ShatterBurst` (`Ready`, `ActivePieces`, `PiecePositions`, `PiecesReady(GameObject)`, `ItemPlace`, `NumberPlace`);
  `ColorHuntCamera.Push(Vector3 target, float amount, float seconds)`, `.Pushing`, `.Zoom`, `.ZoomFocus`, `static Zoomed(fit, focus, zoom)`,
  `static PushWeight(u)` (Task 13's close-ups use `Zoom`/`ZoomFocus`); `ColorHuntSettings.PieceTemplate`.

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/ShatterTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ShatterTests
    {
        [Test]
        public void Debris_Falls_BouncesOffTheFrameBottom_AndStaysInside()
        {
            var d = new Debris { P = Vector3.zero, V = new Vector3(0f, -12f, 0f), R = Quaternion.identity, Life = 5f, Size = 1f };
            bool bounced = false;
            for (int i = 0; i < 300; i++)
            {
                ShatterDebris.Step(ref d, 1f / 60f, 0.2f);
                Assert.GreaterOrEqual(d.P.y, -(ShatterDebris.InnerHalf - 0.2f) - 1e-4f);
                bounced |= d.V.y > 0f;
            }
            Assert.IsTrue(bounced);
        }

        [Test]
        public void Debris_BouncesOffTheSides_AndTheGlass()
        {
            var d = new Debris { P = new Vector3(4f, 0f, 0.3f), V = new Vector3(20f, 0f, 20f), R = Quaternion.identity, Life = 5f, Size = 1f };
            ShatterDebris.Step(ref d, 0.1f, 0.2f);
            Assert.AreEqual(ShatterDebris.InnerHalf - 0.2f, d.P.x, 1e-4f);
            Assert.Less(d.V.x, 0f);
            Assert.AreEqual(ShatterDebris.GlassZ - 0.2f, d.P.z, 1e-4f);
            Assert.Less(d.V.z, 0f);
        }

        [Test]
        public void Launch_ThrowsOutAlongThePiece_AndTowardTheCamera()
        {
            var rng = new System.Random(3);
            var d = ShatterDebris.Launch(new Vector3(1f, 2f, 0f), new Vector3(0.3f, 0f, 0f), 1.2f, rng);
            Assert.AreEqual(new Vector3(1.36f, 2f, 0f), d.P);
            Assert.Greater(d.V.x, 0f);
            Assert.Less(d.V.z, 0f, "toward the camera (−z)");
            Assert.That(d.Life, Is.InRange(0.8f, 1.2f));
            Assert.AreEqual(1.2f, d.Size);
        }

        [Test]
        public void Shrink_HoldsThenVanishes()
        {
            Assert.AreEqual(1f, ShatterDebris.Shrink(0.5f, 1f));
            Assert.AreEqual(0.5f, ShatterDebris.Shrink(0.85f, 1f), 1e-4f);
            Assert.AreEqual(0f, ShatterDebris.Shrink(1f, 1f), 1e-4f);
        }

        private static GameObject Model(int pieces, bool frontUv)
        {
            var root = new GameObject("Model");
            for (int i = 0; i < pieces; i++)
            {
                var go = new GameObject("piece_" + i);
                go.transform.SetParent(root.transform);
                var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
                if (frontUv) mesh.uv2 = new[] { Vector2.right, Vector2.zero, Vector2.zero };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
            }
            return root;
        }

        [Test]
        public void PiecesWithoutFrontUv_AreNotReady()
        {
            var bad = Model(10, false);
            var good = Model(10, true);
            try
            {
                Assert.IsFalse(ShatterBurst.PiecesReady(null));
                Assert.IsFalse(ShatterBurst.PiecesReady(bad));
                Assert.IsFalse(ShatterBurst.PiecesReady(Model(2, true)), "a real fracture has 4+ pieces");
                Assert.IsTrue(ShatterBurst.PiecesReady(good));
            }
            finally { Object.DestroyImmediate(bad); Object.DestroyImmediate(good); }
        }

        [Test]
        public void StickerPlacement_MatchesBlockViewsLabels()
        {
            Assert.AreEqual(0.5f, ShatterBurst.ItemPlace.x, 1e-5f);
            Assert.AreEqual(0.5f + 0.06f / 0.9f, ShatterBurst.ItemPlace.y, 1e-5f);
            Assert.AreEqual(0.92f * PotaLook.ItemScale / 0.9f, ShatterBurst.ItemPlace.z, 1e-5f);
            Assert.AreEqual(0.5f + 0.26f / 0.9f, ShatterBurst.NumberPlace.x, 1e-5f);
            Assert.AreEqual(0.5f - 0.28f / 0.9f, ShatterBurst.NumberPlace.y, 1e-5f);
            Assert.AreEqual(0.92f * 0.44f / 0.9f, ShatterBurst.NumberPlace.w, 1e-5f);
        }
    }
}
```

`Tests/EditMode/ColorHuntCameraTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntCameraTests
    {
        [Test]
        public void Zoomed_OneIsTheFit_TwoHalvesTheDistanceTowardTheFocus()
        {
            var fit = new Vector3(0f, 1f, -20f);
            var focus = new Vector3(2f, 3f, -0.5f);
            Assert.AreEqual(fit, ColorHuntCamera.Zoomed(fit, focus, 1f));
            var z2 = ColorHuntCamera.Zoomed(fit, focus, 2f);
            Assert.AreEqual(1f, z2.x, 1e-5f);
            Assert.AreEqual(2f, z2.y, 1e-5f);
            Assert.AreEqual(-0.5f - 19.5f / 2f, z2.z, 1e-5f);
        }

        [Test]
        public void PushWeight_IsZeroAtBothEnds_OneInTheMiddle()
        {
            Assert.AreEqual(0f, ColorHuntCamera.PushWeight(0f));
            Assert.AreEqual(1f, ColorHuntCamera.PushWeight(0.5f), 1e-5f);
            Assert.AreEqual(0f, ColorHuntCamera.PushWeight(1f));
        }
    }
}
```

`Tests/PlayMode/ShatterPlayTests.cs`:
```csharp
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>B "Gerçek kırılma" in the Color Hunt scene (spec §2 B).</summary>
    public class ShatterPlayTests
    {
        private static void AssertInsideTheFrame(ShatterBurst shatter)
        {
            foreach (var p in shatter.PiecePositions)
            {
                Assert.LessOrEqual(Mathf.Abs(p.x), ShatterDebris.InnerHalf + 1e-3f, "x inside the frame");
                Assert.LessOrEqual(Mathf.Abs(p.y), ShatterDebris.InnerHalf + 1e-3f, "y inside the frame");
                Assert.LessOrEqual(p.z, ShatterDebris.GlassZ + 1e-3f, "in front of the glass");
            }
        }

        [UnityTest]
        public IEnumerator Tap_BreaksTheBlockIntoPieces_ThatStayInTheFrame_AndVanish()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = game.Style as ShatterBurst;
            Assert.IsNotNull(shatter, "the Shatter style is ready (ai_studio_import ran)");
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            yield return StudioLookPlayTests.WaitUntil(() => shatter.ActivePieces > 0, 1.5f);
            Assert.Greater(shatter.ActivePieces, 0);
            float t = 0f;
            while (shatter.ActivePieces > 0 && t < 4f)
            {
                AssertInsideTheFrame(shatter);
                Assert.LessOrEqual(shatter.LiveCount, BurstBudget.Max);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(0, shatter.ActivePieces, "every piece shrank away");
        }

        [UnityTest]
        public IEnumerator Rotate_WhilePiecesFall_TheyStayInTheFrame()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            for (int i = 0; i < 3; i++) shatter.TapBurst(game.ViewAt(i), PotaLook.Tones[i]);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            float t = 0f;
            while (t < 1f) { AssertInsideTheFrame(shatter); t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Clear_ReturnsEveryPiece()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            for (int i = 0; i < 3; i++) shatter.TapBurst(game.ViewAt(i), PotaLook.Tones[i]);
            Assert.Greater(shatter.ActivePieces, 0);
            game.SetLook(PuzzleLook.Pota);
            Assert.AreEqual(0, shatter.ActivePieces);
        }

        [UnityTest]
        public IEnumerator EveryBlockAtOnce_StaysUnderTheCap()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            for (int i = 0; i < game.Board.Cells.Length; i++) shatter.StepBurst(game.ViewAt(i), PotaLook.Tones[i % 5], 5);
            Assert.LessOrEqual(shatter.ActivePieces, ShatterBurst.PieceMax);
            float t = 0f;
            while (t < 1f) { Assert.LessOrEqual(shatter.LiveCount, BurstBudget.Max); t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator ChainOfFour_SlowsTime_AndPushesTheCamera()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            game.Style.ChainPeak(4, Vector2.zero, PotaLook.Tones[0]);
            yield return null;
            yield return null;
            Assert.Less(Time.timeScale, 0.99f);
            Assert.IsTrue(game.Cam.GetComponent<ColorHuntCamera>().Pushing);
        }
    }
}
```

Append to `Tests/PlayMode/PotaFlowPlayTests.cs` (it already has `Load`, `WaitUntil`, `Phase`):
```csharp
        [UnityTest]
        public IEnumerator StudioLook_TransitionEndsOnAPlayableBoard_AndTheBurstIsTheLooks()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.Game.SetLook(PuzzleLook.Shatter);
            Assert.IsTrue(d.BeginTransition(158));
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 3.5f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            yield return null;
            Assert.AreEqual(ColorHuntGame.State.Playing, d.Game.Current);
            Assert.IsFalse(d.Game.Intro, "PotaFlow keeps its own reveal");
            var shatter = d.Game.Style as ShatterBurst;
            Assert.IsNotNull(shatter);
            Assert.AreEqual("broken", d.Game.Tap(ColorHuntHint.Best(d.Game.Board)));
            yield return WaitUntil(() => shatter.ActivePieces > 0, 1.5f);
            Assert.Greater(shatter.ActivePieces, 0, "the pieces fly in the giant hoop's space too");
        }
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ShatterTests Project.Tests.ColorHuntCameraTests`
Expected: exit 2, `error CS0246: The type or namespace name 'Debris' could not be found`.

- [ ] **Step 3: The piece shader** (`Shaders/PotaPiece.shader`)

```hlsl
// B's broken pieces: the block tone (linear, per renderer) lit by the main light, SH and the room probe, with the sticker (item and
// number atlas cells at BlockView's placement) on the block's original front faces (UV1.x = 1). Face UV (UV0) runs 0..1 across the face.
Shader "Project/PotaPiece"
{
    Properties
    {
        [MainColor] _BaseColor ("Tone (linear, per renderer)", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.84
        _Reflection ("Reflection", Range(0, 1)) = 0.45
        _LabelAtlas ("Label Atlas", 2D) = "black" {}
        _ItemRect ("Item cell (atlas UV rect)", Vector) = (0, 0, 0, 0)
        _NumRect ("Number cell (atlas UV rect)", Vector) = (0, 0, 0, 0)
        _ItemPlace ("Item place on the face (centre xy, size zw)", Vector) = (0.5, 0.567, 0.836, 0.836)
        _NumPlace ("Number place on the face (centre xy, size zw)", Vector) = (0.789, 0.189, 0.45, 0.45)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            half _Reflection;
            float4 _LabelAtlas_ST;
            float4 _ItemRect;
            float4 _NumRect;
            float4 _ItemPlace;
            float4 _NumPlace;
        CBUFFER_END
        TEXTURE2D(_LabelAtlas);
        SAMPLER(sampler_LabelAtlas);
        ENDHLSL

        Pass
        {
            Name "PieceForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 front : TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float front : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.front = v.front.x;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Label(float2 face, float4 place, float4 rect)
            {
                float2 local = (face - place.xy) / place.zw + 0.5;
                half4 c = SAMPLE_TEXTURE2D(_LabelAtlas, sampler_LabelAtlas, rect.xy + saturate(local) * rect.zw);
                return all(local >= 0.0) && all(local <= 1.0) ? c : half4(0, 0, 0, 0);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = _BaseColor.rgb;
                if (i.front > 0.5)
                {
                    half4 item = Label(i.uv, _ItemPlace, _ItemRect);
                    albedo = lerp(albedo, item.rgb, item.a);
                    half4 number = Label(i.uv, _NumPlace, _NumRect);
                    albedo = lerp(albedo, number.rgb, number.a);
                }
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lightColor = light.color * light.shadowAttenuation;
                half3 color = albedo * (lightColor * saturate(dot(n, light.direction)) + SampleSH(n));
                half3 h = normalize(light.direction + viewDir);
                color += lightColor * pow(saturate(dot(n, h)), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness * 0.5h;
                half fresnel = 0.04h + 0.96h * pow(1.0h - saturate(dot(n, viewDir)), 5.0h);
                color += GlossyEnvironmentReflection(reflect(-viewDir, n), 1.0h - _Smoothness, 1.0h) * fresnel * _Reflection;
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 vert(Attributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
```
(If the 3-argument `GlossyEnvironmentReflection` overload is missing in URP 17.6, read
`Library/PackageCache/com.unity.render-pipelines.universal@*/ShaderLibrary/GlobalIllumination.hlsl` and use the overload that takes
`positionWS` and `normalizedScreenSpaceUV` (`GetNormalizedScreenSpaceUV(i.positionCS)`), with a ledger note.)

- [ ] **Step 4: Debris, style, camera**

`Scripts/ColorHunt/Bursts/ShatterDebris.cs`:
```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>One piece of B's broken block in the game root's space (the hoop's: +y up, the glass at z 0.7 behind the board, the camera
    /// toward −z). Deterministic, hand-written motion (plan decision 7) so recordings replay the same.</summary>
    public struct Debris
    {
        public Vector3 P, V, W;   // position, velocity, angular velocity (degrees/s)
        public Quaternion R;
        public float Age, Life, Size;
    }

    public static class ShatterDebris
    {
        public const float Gravity = 12f, Restitution = 0.45f, Friction = 0.8f, SpinLoss = 0.7f;
        /// <summary>GiantHoop: the frame's inner opening is ±PanoSize/2 = ±4.4; the glass plane is z = 0.7 (game-root space).</summary>
        public const float InnerHalf = PotaLook.PanoSize * 0.5f, GlassZ = 0.7f;

        /// <summary>A piece leaves its block along its offset from the block's centre, a little up and toward the camera.</summary>
        public static Debris Launch(Vector3 blockCentre, Vector3 pieceOffset, float scale, System.Random rng)
        {
            Vector3 dir = pieceOffset.sqrMagnitude > 1e-8f ? pieceOffset.normalized : Vector3.up;
            float speed = (3.2f + 2.2f * (float)rng.NextDouble()) * scale;
            var v = new Vector3(dir.x, dir.y + 0.35f, 0f) * speed
                    + new Vector3(0f, 1.2f * scale, -(1.6f + 1.8f * (float)rng.NextDouble()) * scale);
            var w = new Vector3(Signed(rng), Signed(rng), Signed(rng)) * 540f;
            return new Debris
            {
                P = blockCentre + pieceOffset * scale, V = v, W = w, R = Quaternion.identity,
                Age = 0f, Life = 0.8f + 0.4f * (float)rng.NextDouble(), Size = scale,
            };
        }

        public static void Step(ref Debris d, float dt, float radius)
        {
            d.V.y -= Gravity * dt;
            d.P += d.V * dt;
            d.R = Quaternion.Euler(d.W * dt) * d.R;
            d.Age += dt;
            float lim = InnerHalf - radius;
            if (d.P.x < -lim) { d.P.x = -lim; d.V.x = -d.V.x * Restitution; Hit(ref d); }
            else if (d.P.x > lim) { d.P.x = lim; d.V.x = -d.V.x * Restitution; Hit(ref d); }
            if (d.P.y < -lim) { d.P.y = -lim; d.V.y = -d.V.y * Restitution; Hit(ref d); }
            else if (d.P.y > lim) { d.P.y = lim; d.V.y = -d.V.y * Restitution; Hit(ref d); }
            float back = GlassZ - radius;
            if (d.P.z > back) { d.P.z = back; d.V.z = -d.V.z * Restitution; Hit(ref d); }
        }

        /// <summary>Full size for 70 % of the life, then shrinks to nothing.</summary>
        public static float Shrink(float age, float life)
        {
            float u = age / Mathf.Max(1e-4f, life);
            return u < 0.7f ? 1f : Mathf.Max(0f, 1f - (u - 0.7f) / 0.3f);
        }

        private static void Hit(ref Debris d)
        {
            d.V *= Friction;
            d.W *= SpinLoss;
        }

        private static float Signed(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;
    }
}
```

`Scripts/ColorHunt/Bursts/ShatterBurst.cs`:
```csharp
using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// B "Gerçek kırılma" (spec §2 B): the block breaks into the Blender-fractured pillow's pieces, lit like the blocks (tone, room
    /// reflection) with the sticker on their front faces; they fly out and toward the camera, spin, bounce off the frame's inner edges and
    /// the glass, and shrink away in 0.8–1.2 s. Lit smoke (flipbook) and streaking sparks go with it and Pota's point light flashes
    /// stronger. Chains of 4+ get a 0.3 s ×0.4 slow motion and a short camera push toward the step. Pieces are pooled GameObjects in the
    /// game root (the hoop's space), so a board rotation does not carry them.
    /// </summary>
    public class ShatterBurst : MonoBehaviour, IBurstStyle
    {
        public const int PieceMax = 160, SmokeMax = 24, SparkMax = 96;

        /// <summary>BlockView's label placement in the pieces' face UV (0..1 across the 0.9 m face): centre xy, size zw.</summary>
        public static readonly Vector4 ItemPlace = new Vector4(0.5f, 0.5f + PotaLook.ItemOffsetY / PotaLook.Block,
            PotaLook.LabelQuad * PotaLook.ItemScale / PotaLook.Block, PotaLook.LabelQuad * PotaLook.ItemScale / PotaLook.Block);
        public static readonly Vector4 NumberPlace = new Vector4(0.5f + PotaLook.NumberOffset.x / PotaLook.Block,
            0.5f + PotaLook.NumberOffset.y / PotaLook.Block,
            PotaLook.LabelQuad * PotaLook.NumberScale / PotaLook.Block, PotaLook.LabelQuad * PotaLook.NumberScale / PotaLook.Block);

        private class Piece { public Transform T; public MeshFilter F; public MeshRenderer R; public Debris D; public float Radius; }
        private struct Part { public Mesh Mesh; public Vector3 Centre; public float Radius; }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ItemRectId = Shader.PropertyToID("_ItemRect");
        private static readonly int NumRectId = Shader.PropertyToID("_NumRect");

        private ColorHuntGame _game;
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Piece> _live = new List<Piece>();
        private readonly Stack<Piece> _pool = new Stack<Piece>();
        private Material _pieceMat;
        private MaterialPropertyBlock _mpb;
        private ParticleSystem _smoke, _sparks;
        private readonly System.Random _rng = new System.Random(17);

        public bool Ready { get; private set; }
        public bool ShowsChainNumber => true;
        public int ActivePieces => _live.Count;
        public IEnumerable<Vector3> PiecePositions { get { foreach (var p in _live) yield return p.D.P; } }
        public int LiveCount => _live.Count + StudioParticles.Count(_smoke, _sparks)
                                + (_game != null && _game.Fx != null ? _game.Fx.LiveCount : 0);

        /// <summary>A usable fractured block: 4+ child meshes, each carrying the front-flag UV set (UV1).</summary>
        public static bool PiecesReady(GameObject model)
        {
            if (model == null) return false;
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length < 4) return false;
            foreach (var f in filters)
            {
                var m = f.sharedMesh;
                if (m == null || m.uv2 == null || m.uv2.Length != m.vertexCount) return false;
            }
            return true;
        }

        public void Build(ColorHuntGame game)
        {
            _game = game;
            var s = game.S;
            Ready = s != null && s.ShatterSmoke != null && s.LabelAtlas != null && s.SoftDisc != null && PiecesReady(s.ShatterPieces);
            if (!Ready) return;
            var root = s.ShatterPieces.transform;
            foreach (var f in s.ShatterPieces.GetComponentsInChildren<MeshFilter>(true))
                _parts.Add(new Part { Mesh = f.sharedMesh, Centre = root.InverseTransformPoint(f.transform.position), Radius = f.sharedMesh.bounds.extents.magnitude });
            _pieceMat = RuntimeMaterial.From(s.PieceTemplate, "Project/PotaPiece", "PotaPiece");
            _pieceMat.SetTexture("_LabelAtlas", s.LabelAtlas);
            _pieceMat.SetVector("_ItemPlace", ItemPlace);
            _pieceMat.SetVector("_NumPlace", NumberPlace);
            _mpb = new MaterialPropertyBlock();
            _smoke = StudioParticles.System(transform, "ShatterSmoke", StudioParticles.Flipbook(s, s.ShatterSmoke), ParticleSystemRenderMode.Billboard, SmokeMax, 6);
            StudioParticles.Sheet(_smoke, 4, 4);
            _sparks = StudioParticles.System(transform, "ShatterSparks", StudioParticles.Additive(s, s.SoftDisc, 2f), ParticleSystemRenderMode.Stretch, SparkMax, 9);
            var sr = _sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.06f;
            sr.lengthScale = 1.2f;
            StudioParticles.SizeOverLife(_sparks, u => 1f - u);
            Activate(false);
        }

        public void Activate(bool on)
        {
            if (!on) Clear();
            gameObject.SetActive(on);
        }

        public void TapBurst(BlockView v, Color tone) => Shatter(v, tone, 0);
        public void StepBurst(BlockView v, Color tone, int depth) => Shatter(v, tone, depth);

        public void ChainPeak(int depth, Vector2 at, Color tone)
        {
            if (!Ready || depth < 4) return;
            _game.Juice?.TimeCtl?.SlowMo(0.4f, 0.3f);
            var cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            if (cam != null) cam.Push(_game.BoardRoot.TransformPoint(new Vector3(at.x, at.y, -PotaLook.FrontZ)), 0.18f, 0.6f);
        }

        public void Clear()
        {
            foreach (var p in _live) Recycle(p);
            _live.Clear();
            if (_smoke != null) _smoke.Clear();
            if (_sparks != null) _sparks.Clear();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);          // game time: dips and slow motion hold the pieces too
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                ShatterDebris.Step(ref p.D, dt, p.Radius);
                if (p.D.Age >= p.D.Life) { Recycle(p); _live.RemoveAt(i); continue; }
                Place(p);
            }
        }

        private void Shatter(BlockView v, Color tone, int depth)
        {
            if (!Ready) return;
            var board = _game.BoardRoot;
            float bs = board.lossyScale.x;
            float s = bs * Mathf.Max(0.5f, v.transform.localScale.x);                 // the swollen block breaks at its size
            Vector3 centre = transform.InverseTransformPoint(board.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, 0f)));
            Vector4 item = LabelAtlas.UvRect(LabelAtlas.ItemCell(v.Block.Color));
            Vector4 number = LabelAtlas.UvRect(LabelAtlas.NumberCell(v.Block.Hp));
            Color lin = PotaLook.BlockColor(tone, 0f, false);
            foreach (var part in _parts)
            {
                if (_live.Count >= PieceMax) break;
                var p = _pool.Count > 0 ? _pool.Pop() : NewPiece();
                p.F.sharedMesh = part.Mesh;
                p.Radius = part.Radius * s;
                p.D = ShatterDebris.Launch(centre, part.Centre, s, _rng);
                ShatterDebris.Step(ref p.D, 0f, p.Radius);                       // an edge block's piece starts inside the frame
                _mpb.SetVector(BaseColorId, lin);
                _mpb.SetVector(ItemRectId, item);
                _mpb.SetVector(NumRectId, number);
                p.R.SetPropertyBlock(_mpb);
                Place(p);
                p.T.gameObject.SetActive(true);
                _live.Add(p);
            }
            Vector3 at = board.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -(PotaLook.FrontZ + 0.2f)));
            var smokeTint = Color.Lerp(Color.white, tone, 0.35f);
            for (int k = 0; k < 2; k++)
            {
                var jitter = new Vector3((R() - 0.5f) * 0.3f, (R() - 0.5f) * 0.3f, 0f) * bs;
                StudioParticles.Emit(_smoke, at + jitter, (2.2f + 0.3f * k + 0.1f * depth) * bs, 0.9f, smokeTint, new Vector3(0f, 0.5f * bs, 0f), R() * 360f);
            }
            StudioParticles.Gravity(_sparks, 8f * bs);
            int sparks = Mathf.Min(14, 8 + depth);
            for (int k = 0; k < sparks; k++)
            {
                float a = R() * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) + 0.3f, -(0.2f + 0.6f * R()));
                StudioParticles.Emit(_sparks, at, 0.12f * bs, 0.35f + 0.2f * R(), new Color(1f, 0.85f, 0.55f, 1f), board.TransformVector(dir) * (6f + 4f * R()));
            }
            _game.Fx?.Glow(v.Pos, tone.linear, _game.S.FlashSize * 0.6f, 0f, _game.S.LightFlashIntensity * 1.8f, _game.S.LightFlashTime * 1.5f);
        }

        private void Place(Piece p)
        {
            p.T.localPosition = p.D.P;
            p.T.localRotation = p.D.R;
            p.T.localScale = Vector3.one * (p.D.Size * ShatterDebris.Shrink(p.D.Age, p.D.Life));
        }

        private void Recycle(Piece p)
        {
            p.T.gameObject.SetActive(false);
            _pool.Push(p);
        }

        private Piece NewPiece()
        {
            var go = new GameObject("Piece");
            go.transform.SetParent(transform, false);
            var p = new Piece { T = go.transform, F = go.AddComponent<MeshFilter>(), R = go.AddComponent<MeshRenderer>() };
            p.R.sharedMaterial = _pieceMat;
            p.R.shadowCastingMode = ShadowCastingMode.On;
            p.R.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return p;
        }

        private float R() => (float)_rng.NextDouble();
    }
}
```

`ColorHuntCamera.cs`: fields and API, and the new `LateUpdate` body:
```csharp
        /// <summary>Close-up for recordings: 1 = the Pota fit; 2 = half the distance, centred halfway to <see cref="ZoomFocus"/>.</summary>
        public float Zoom = 1f;
        public Vector3 ZoomFocus;
        private Vector3 _pushTarget;
        private float _pushAmount, _pushTime = -1f, _pushDuration = 1f;

        public bool Pushing => _pushTime >= 0f;

        /// <summary>A short push toward <paramref name="target"/>: <paramref name="amount"/> of the way in and back out (sin), real time.</summary>
        public void Push(Vector3 target, float amount, float seconds)
        {
            _pushTarget = target;
            _pushAmount = amount;
            _pushDuration = Mathf.Max(0.01f, seconds);
            _pushTime = 0f;
        }

        public static Vector3 Zoomed(Vector3 fit, Vector3 focus, float zoom)
        {
            if (zoom <= 1f) return fit;
            float k = 1f - 1f / zoom;
            return new Vector3(Mathf.Lerp(fit.x, focus.x, k), Mathf.Lerp(fit.y, focus.y, k), focus.z + (fit.z - focus.z) / zoom);
        }

        public static float PushWeight(float u) => u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);

        private void LateUpdate()
        {
            var pos = Zoomed(FitFor(_cam.fieldOfView, _cam.aspect), ZoomFocus, Zoom);
            if (_pushTime >= 0f)
            {
                _pushTime += Time.unscaledDeltaTime;
                float u = _pushTime / _pushDuration;
                pos += (_pushTarget - pos) * (_pushAmount * PushWeight(u));
                if (u >= 1f) _pushTime = -1f;
            }
            transform.position = pos + (_shake != null ? _shake.Offset : Vector3.zero);
            transform.rotation = Quaternion.identity;
        }
```
(Replace the old `LateUpdate`; keep its two lines' behaviour: fit + shake, identity rotation.)

`ColorHuntSettings.cs` (templates header): `[Tooltip("Project/PotaPiece: B's lit pieces with the sticker.")] public Material PieceTemplate;`
`PotaMaterialTemplates.Ensure`: `var piece = Template("PotaPiece", RuntimeMaterial.From(null, "Project/PotaPiece"));` and
`s.PieceTemplate = piece;` (report "9 in").
`ColorHuntGame.CreateStyle`: `case PuzzleLook.Shatter: return AddStyle<ShatterBurst>("ShatterBurst");`.

- [ ] **Step 5: Compile, templates, tests**

```bash
~/.local/bin/uv run Tools/mcp_call.py call refresh_unity '{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}'
~/.local/bin/uv run Tools/mcp_call.py call execute_menu_item '{"menu_path":"AI Tools/Pota/Material Templates"}'
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ShatterTests Project.Tests.ColorHuntCameraTests Project.Tests.RuntimeShaderBuildTests --no-compile
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ShatterPlayTests Project.Tests.ColorHuntStagingPlayTests Project.Tests.PotaFlowPlayTests --no-compile
```
Expected: all pass.

- [ ] **Step 6: Look at it in slow motion**

As in Task 10 Step 6 with the Shatter look: four screenshots ~0.15 s apart at ×0.2. Check against spec §2 B: pieces in the block's
tone, lit and glossy like the blocks, parts of the sticker visible on front pieces, smoke soft and lit, sparks streaking, pieces bouncing
off the frame and shrinking. Typical fixes: sticker upside down or mirrored (swap `uv.y` or `uv.x` in the shader with a ledger note after
checking against the Blender preview), pieces too dark (reflection/SH), smoke too opaque (tint alpha).

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Games/Pota/Shaders Assets/_Project/Games/Pota/Scripts Assets/_Project/Games/Pota/Editor/PotaMaterialTemplates.cs \
  Assets/_Project/Games/Pota/Materials Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset Assets/_Project/Games/Pota/Tests
git commit -m "feat: B — Shatter burst: lit fractured pieces with the sticker, bouncing in the frame; lit smoke, sparks, 4+ slow-mo and camera push

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: B's shock ring — a heat-haze ring through the opaque texture

**Files:**
- Create: `Shaders/PotaShockRing.shader`
- Modify: `Scripts/ColorHunt/Bursts/ShatterBurst.cs`, `Scripts/ColorHunt/ColorHuntSettings.cs` (`ShockTemplate`), `Editor/PotaMaterialTemplates.cs`
- Test: `Tests/PlayMode/ShatterPlayTests.cs`

**Interfaces:**
- Consumes: `ColorHuntSettings.ShockRing` (Task 4, linear screen-normal flipbook), `StudioParticles` (Task 10), `ShatterBurst` (Task 11).
- Produces: `ShatterBurst.ShockLive` (int), `ColorHuntSettings.ShockTemplate`; the puzzle camera's `requiresColorOption` is `On` exactly
  while the Shatter look is active.

- [ ] **Step 1: Write the failing tests** (append to `ShatterPlayTests`; add `using UnityEngine.Rendering.Universal;`)

```csharp
        [UnityTest]
        public IEnumerator Shatter_TurnsTheOpaqueTextureOn_OnlyWhileActive()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var data = game.Cam.GetUniversalAdditionalCameraData();
            Assert.AreEqual(CameraOverrideOption.On, data.requiresColorOption);
            game.SetLook(PuzzleLook.Pota);
            Assert.AreEqual(CameraOverrideOption.UsePipelineSettings, data.requiresColorOption);
        }

        [UnityTest]
        public IEnumerator Tap_SendsAShockRing()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Shatter, (g, h) => game = g);
            var shatter = (ShatterBurst)game.Style;
            Assert.AreEqual("broken", game.Tap(ColorHuntHint.Best(game.Board)));
            yield return StudioLookPlayTests.WaitUntil(() => shatter.ShockLive > 0, 1.5f);
            Assert.Greater(shatter.ShockLive, 0);
        }
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ShatterPlayTests`
Expected: exit 2, `error CS1061: 'ShatterBurst' does not contain a definition for 'ShockLive'`.

- [ ] **Step 3: Implement**

`Shaders/PotaShockRing.shader`:
```hlsl
// B's shock ring: bends what is behind it (the camera's opaque texture) along the flipbook's screen normal (R = x, G = y, A = ring;
// linear), with a faint bright rim. Needs the opaque texture on the camera (ShatterBurst turns it on for its look only).
Shader "Project/PotaShockRing"
{
    Properties
    {
        _BaseMap ("Normal flipbook (linear)", 2D) = "black" {}
        _Strength ("Strength (screen UV)", Range(0, 0.1)) = 0.035
        _Brighten ("Rim brighten", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shock"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Strength;
                half _Brighten;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half mask = t.a * i.color.a;
                float2 n = t.rg * 2.0 - 1.0;
                float2 uv = i.screen.xy / i.screen.w + n * _Strength * mask;
                half3 scene = SampleSceneColor(uv);
                return half4(scene * (1.0h + _Brighten * mask), mask);
            }
            ENDHLSL
        }
    }
}
```

`ShatterBurst.cs`:
1. `using UnityEngine.Rendering.Universal;`
2. Constant/field/property: `public const int ShockMax = 16;` `private ParticleSystem _shock;`
   `public int ShockLive => _shock != null ? _shock.particleCount : 0;` and add `_shock` to `LiveCount`'s `StudioParticles.Count(...)`
   and to `Clear()`.
3. In `Build`, before `Activate(false);`:
```csharp
            if (s.ShockRing != null)
            {
                var shock = RuntimeMaterial.From(s.ShockTemplate, "Project/PotaShockRing", "PotaShockRing");
                shock.SetTexture("_BaseMap", s.ShockRing);
                _shock = StudioParticles.System(transform, "ShatterShock", shock, ParticleSystemRenderMode.Billboard, ShockMax, 10);
                StudioParticles.Sheet(_shock, 4, 4);
                StudioParticles.AlphaOverLife(_shock, u => 1f - u * u);
            }
```
4. `Activate`:
```csharp
        public void Activate(bool on)
        {
            if (!on) Clear();
            gameObject.SetActive(on);
            var data = _game != null && _game.Cam != null ? _game.Cam.GetUniversalAdditionalCameraData() : null;
            if (data != null) data.requiresColorOption = on && _shock != null ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;
        }
```
5. In `Shatter`, after the smoke loop: `if (_shock != null && depth == 0) StudioParticles.Emit(_shock, at, 3.2f * bs, 0.4f, Color.white);`
6. `ChainPeak` becomes:
```csharp
        public void ChainPeak(int depth, Vector2 at, Color tone)
        {
            if (!Ready) return;
            float bs = _game.BoardRoot.lossyScale.x;
            if (_shock != null)
                StudioParticles.Emit(_shock, _game.BoardRoot.TransformPoint(new Vector3(at.x, at.y, -(PotaLook.FrontZ + 0.2f))), (3.2f + 0.4f * depth) * bs, 0.45f, Color.white);
            if (depth < 4) return;
            _game.Juice?.TimeCtl?.SlowMo(0.4f, 0.3f);
            var cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            if (cam != null) cam.Push(_game.BoardRoot.TransformPoint(new Vector3(at.x, at.y, -PotaLook.FrontZ)), 0.18f, 0.6f);
        }
```
`ColorHuntSettings.cs` (templates header): `[Tooltip("Project/PotaShockRing: B's heat-haze ring.")] public Material ShockTemplate;`
`PotaMaterialTemplates.Ensure`: `var shock = Template("PotaShockRing", RuntimeMaterial.From(null, "Project/PotaShockRing"));`,
`s.ShockTemplate = shock;` (report "10 in").

- [ ] **Step 4: Compile, templates, tests; measure the cost**

```bash
~/.local/bin/uv run Tools/mcp_call.py call refresh_unity '{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}'
~/.local/bin/uv run Tools/mcp_call.py call execute_menu_item '{"menu_path":"AI Tools/Pota/Material Templates"}'
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ShatterPlayTests --no-compile
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.RuntimeShaderBuildTests --no-compile
```
Expected: all pass. Then, in Play Mode at the phone resolution, read the frame time with `manage_profiler` (or the Game view stats)
for ~2 s of Pota and ~2 s of Shatter during repeated taps, and write both numbers in the ledger (spec §4: the opaque texture's cost is
measured and noted; if Shatter costs more than ~1 ms extra GPU per frame on the editor's Metal, note it as a phone risk in the backlog).

- [ ] **Step 5: Look at it** (slow motion, as in Task 11 Step 6): the ring visibly bends the blocks behind it and fades; no black or
pink rectangle (a black ring = no opaque texture: check `requiresColorOption`; pink = shader error in the console).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Games/Pota/Shaders/PotaShockRing.shader* Assets/_Project/Games/Pota/Scripts Assets/_Project/Games/Pota/Editor/PotaMaterialTemplates.cs \
  Assets/_Project/Games/Pota/Materials Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset Assets/_Project/Games/Pota/Tests/PlayMode/ShatterPlayTests.cs
git commit -m "feat: B — heat-haze shock ring through the opaque texture, on only while the Shatter look is active

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Pota | A | B — recordings, sheets, side video, close-ups, tuning by eye

**Files:**
- Modify: `Editor/ColorHuntRecordTool.cs` (`look`, `closeup`), `Tools/potalook/compare.py`, `Tools/potalook/test_compare.py`
- Create: `Tools/potalook/side_video.py`, `Tools/potalook/test_side_video.py`, `Tools/potalook/record_unity.sh`
- Test: `Tests/EditMode/ColorHuntRecordToolTests.cs`
- Output: `docs/screenshots/2026-09-30-studio-{tap,chain,win}.jpg`, `2026-09-30-studio-side.mp4`,
  `2026-09-30-studio-closeup-{candy,shatter}.mp4`

**Interfaces:**
- Consumes: `ColorHuntGame.SetLook/Look`, `ColorHuntHint.Best` (Task 5), `ColorHuntCamera.Zoom/ZoomFocus` (Task 11),
  `TimeScaleController.SlowMo/Clear`.
- Produces: `ai_colorhunt_record` params `look` ("pota" | "candy"/"a" | "shatter"/"b") and `closeup` (bool: zoom ×2.4 on the first tap's
  block, time ×0.3); `ColorHuntRecordTool.TryLook(string, out PuzzleLook) -> bool`; `compare.py OUT_PREFIX POTA_VIDEO POTA_EVENTS
  LABEL=DIR [LABEL=DIR ...]` (moments `tap`, `chain`, `rotate`, `win`); `side_video.py`.

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/ColorHuntRecordToolTests.cs` (append):
```csharp
        [Test]
        public void TryLook_KnowsTheThreeLooks_AndTheirLetters()
        {
            Assert.IsTrue(ColorHuntRecordTool.TryLook("pota", out var pota));
            Assert.AreEqual(PuzzleLook.Pota, pota);
            Assert.IsTrue(ColorHuntRecordTool.TryLook("Candy", out var candy));
            Assert.AreEqual(PuzzleLook.Candy, candy);
            Assert.IsTrue(ColorHuntRecordTool.TryLook("a", out var a));
            Assert.AreEqual(PuzzleLook.Candy, a);
            Assert.IsTrue(ColorHuntRecordTool.TryLook("B", out var b));
            Assert.AreEqual(PuzzleLook.Shatter, b);
            Assert.IsFalse(ColorHuntRecordTool.TryLook("fire", out _));
            Assert.IsFalse(ColorHuntRecordTool.TryLook(null, out _));
        }
```
(Add `using Project.ColorHunt;` if the file lacks it.)

`Tools/potalook/test_compare.py`: replace `test_moments_cover_the_spec` and append:
```python
import pytest


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
```

`Tools/potalook/test_side_video.py`:
```python
import importlib.util
import os
import sys

import pytest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
spec = importlib.util.spec_from_file_location("side_video", os.path.join(HERE, "side_video.py"))
side = importlib.util.module_from_spec(spec)
spec.loader.exec_module(side)


def test_segments_parse():
    assert side.parse_segments("tap:-1:3.4,rotate:-0.5:2.5") == [("tap", -1.0, 3.4), ("rotate", -0.5, 2.5)]


def test_trims_are_windows_around_each_event():
    got = side.trims({"tap": 1.0, "rotate": 5.1}, [("tap", -1.0, 3.4), ("rotate", -0.5, 2.5)])
    assert got == [(pytest.approx(0.0), pytest.approx(4.4)), (pytest.approx(4.6), pytest.approx(7.6))]


def test_a_window_before_the_recording_is_refused():
    with pytest.raises(ValueError):
        side.trims({"tap": 0.5}, [("tap", -1.0, 1.0)])


def test_filter_stacks_every_column_per_segment_and_joins_them():
    f = side.build_filter([[(0, 1), (2, 3)]] * 3, 540, 960)
    assert "hstack=inputs=3[row0]" in f
    assert "hstack=inputs=3[row1]" in f
    assert "concat=n=2:v=1[body]" in f
    assert "[3:v]" in f, "the label strip is the last input"
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run --with pillow --with pytest pytest -q Tools/potalook/test_compare.py Tools/potalook/test_side_video.py`
Expected: failures — `AttributeError: module 'compare' has no attribute 'anchor_of'` and `side_video.py` not found.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntRecordToolTests`
Expected: exit 2, `'ColorHuntRecordTool' does not contain a definition for 'TryLook'`.

- [ ] **Step 3: `compare.py` — N rows and the chain moment**

Replace the docstring, `MOMENTS` and `main` and add three helpers:
```python
"""Pota | Unity looks at matched moments (spec 2026-09-30 studio polish §1.2).

Usage: uv run --with pillow Tools/potalook/compare.py OUT_PREFIX POTA_VIDEO POTA_EVENTS LABEL=UNITY_DIR [LABEL=UNITY_DIR ...]
Each UNITY_DIR holds ai_colorhunt_record frames and events.json. For each moment whose anchor is in every events file ("tap", "chain",
"rotate", "win") writes OUT_PREFIX-<moment>.jpg: one row for Pota, one per Unity dir, one column per time after the anchor (the
sheet labels them +1 s). Pota frames are extracted from the video at 30 fps with ffmpeg; Pota's anchors come from its sync marker.
"""
```
```python
# idle, salvo mid, tapped block's swell peak (0.5 + 0.11·0.55), burst +0.08 / +0.16, a landing, the sticker home (peak + 0.55);
# chain: the steps after the tap (the tapped block is the longest chain's, "tap auto")
MOMENTS = {
    "tap": [-0.3, 0.25, 0.5605, 0.64, 0.72, 0.9, 1.11],
    "chain": [0.72, 0.87, 1.02, 1.17, 1.32, 1.5],
    "rotate": [0.0, 0.21, 0.33, 0.5],
    "win": [0.56, 0.9, 1.5],
}
ANCHORS = {"chain": "tap"}


def anchor_of(moment):
    return ANCHORS.get(moment, moment)


def parse_rows(args):
    """["A (Candy)=Temp/record/a", …] → [("A (Candy)", "Temp/record/a"), …]."""
    rows = []
    for arg in args:
        label, sep, folder = arg.partition("=")
        if not sep or not label or not folder:
            raise ValueError(f"expected LABEL=DIR, got '{arg}'")
        rows.append((label, folder))
    return rows


def pota_anchor_times(video, events_path, work):
    """Pota's frames (30 fps) and its anchor times: from the sync marker when every event has one, else the events file."""
    events = json.load(open(events_path))
    frames = extract(video, work)
    synced = marker_events(frames, sorted(events, key=events.get))
    if synced:
        print(f"pota anchors from the marker: {synced} (events file: {events})")
        return frames, synced
    print("pota marker not found for every event: using the events file (video start latency not corrected)")
    return frames, events


def main(argv):
    if len(argv) < 4:
        sys.exit(__doc__)
    prefix, video, pota_events = argv[:3]
    rows = parse_rows(argv[3:])
    work = tempfile.mkdtemp(prefix="potalook-")
    try:
        pota, pe = pota_anchor_times(video, pota_events, work)
        unity = [(label, frame_sheet.load_frames(folder), json.load(open(os.path.join(folder, "events.json"))))
                 for label, folder in rows]
        for moment, times in MOMENTS.items():
            anchor = anchor_of(moment)
            if anchor not in pe or any(anchor not in ev for _, _, ev in unity):
                continue
            columns = [f"Pota={aligned(pota, pe[anchor], os.path.join(work, f'pota-{moment}'))}"]
            for i, (label, frames, ev) in enumerate(unity):
                columns.append(f"{label}={aligned(frames, ev[anchor], os.path.join(work, f'unity{i}-{moment}'))}")
            frame_sheet.main([f"{prefix}-{moment}.jpg", *columns,
                              "--times", ",".join(f"{t + WINDOW:.4f}" for t in times), "--height", "640"])
    finally:
        shutil.rmtree(work, ignore_errors=True)
```

- [ ] **Step 4: `side_video.py`**

```python
#!/usr/bin/env python3
"""Pota and Unity looks side by side in one video. Every column is cut around the same events (tap, rotate, …) and the cuts are
joined, so the columns' moments line up even though their boards differ; a label strip sits on top.

Usage: uv run --with pillow Tools/potalook/side_video.py OUT.mp4 --pota VIDEO EVENTS --unity LABEL=DIR [LABEL=DIR ...]
       [--segments tap:-1:3.4,rotate:-0.5:2.5] [--width 540] [--height 960]
Unity DIRs hold ai_colorhunt_record frames (0.02 s apart) and events.json.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import compare  # noqa: E402

FFMPEG = compare.FFMPEG
UNITY_FPS = 50
FONT = os.path.join(os.path.dirname(os.path.dirname(HERE)), "Assets/_Project/Games/Pota/Art/Fonts/LilitaOne-Regular.ttf")


def parse_segments(text):
    """"tap:-1:3.4,rotate:-0.5:2.5" → [("tap", -1.0, 3.4), ("rotate", -0.5, 2.5)]."""
    out = []
    for part in text.split(","):
        name, before, after = part.split(":")
        out.append((name, float(before), float(after)))
    return out


def trims(events, segments):
    """(start, end) windows in one recording's own time; a window may not start before the recording."""
    out = []
    for name, before, after in segments:
        start = events[name] + before
        if start < 0:
            raise ValueError(f"the {name} window starts {-start:.2f} s before the recording")
        out.append((start, events[name] + after))
    return out


def build_filter(columns, width, height):
    """columns[i] = input i's windows; input len(columns) is the label strip. Same segment count in every column."""
    parts = []
    count = len(columns[0])
    for c, windows in enumerate(columns):
        outs = "".join(f"[c{c}s{k}]" for k in range(count))
        parts.append(f"[{c}:v]fps=30,scale={width}:{height}:flags=lanczos,setsar=1,split={count}{outs}")
        for k, (a, b) in enumerate(windows):
            parts.append(f"[c{c}s{k}]trim={a:.4f}:{b:.4f},setpts=PTS-STARTPTS[c{c}t{k}]")
    for k in range(count):
        parts.append("".join(f"[c{c}t{k}]" for c in range(len(columns))) + f"hstack=inputs={len(columns)}[row{k}]")
    parts.append("".join(f"[row{k}]" for k in range(count)) + f"concat=n={count}:v=1[body]")
    parts.append(f"[{len(columns)}:v]fps=30,setsar=1[head]")
    parts.append("[head][body]vstack=shortest=1,format=yuv420p[out]")
    return ";".join(parts)


def header(labels, width, out):
    strip = Image.new("RGB", (width * len(labels), 56), (16, 22, 34))
    draw = ImageDraw.Draw(strip)
    font = ImageFont.truetype(FONT, 34)
    for i, text in enumerate(labels):
        w = draw.textlength(text, font=font)
        draw.text((i * width + (width - w) / 2, 10), text, font=font, fill=(255, 200, 61))
    strip.save(out)


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("out")
    ap.add_argument("--pota", nargs=2, metavar=("VIDEO", "EVENTS"), required=True)
    ap.add_argument("--unity", nargs="+", required=True)
    ap.add_argument("--segments", default="tap:-1:3.4,rotate:-0.5:2.5")
    ap.add_argument("--width", type=int, default=540)
    ap.add_argument("--height", type=int, default=960)
    a = ap.parse_args(argv)
    segments = parse_segments(a.segments)
    rows = compare.parse_rows(a.unity)
    work = tempfile.mkdtemp(prefix="side-")
    try:
        _, pota_times = compare.pota_anchor_times(a.pota[0], a.pota[1], work)
        columns = [trims(pota_times, segments)]
        inputs = ["-i", a.pota[0]]
        for _, folder in rows:
            columns.append(trims(json.load(open(os.path.join(folder, "events.json"))), segments))
            inputs += ["-framerate", str(UNITY_FPS), "-pattern_type", "glob", "-i", os.path.join(folder, "f_*.png")]
        head = os.path.join(work, "head.png")
        header(["Pota (three.js)"] + [label for label, _ in rows], a.width, head)
        inputs += ["-loop", "1", "-i", head]
        subprocess.run([FFMPEG, "-hide_banner", "-loglevel", "error", "-y", *inputs,
                        "-filter_complex", build_filter(columns, a.width, a.height), "-map", "[out]",
                        "-c:v", "libx264", "-crf", "23", "-preset", "slow", "-movflags", "+faststart", a.out], check=True)
        print(a.out)
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main(sys.argv[1:])
```

- [ ] **Step 5: The record tool** (`Editor/ColorHuntRecordTool.cs`)

1. Constants and state:
```csharp
        public const float CloseupZoom = 2.4f, CloseupTimeScale = 0.3f;
        private static PuzzleLook _previousLook;
        private static bool _closeup;

        /// <summary>"pota" | "candy" / "a" | "shatter" / "b", any case.</summary>
        public static bool TryLook(string s, out PuzzleLook look)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "pota": look = PuzzleLook.Pota; return true;
                case "candy": case "a": look = PuzzleLook.Candy; return true;
                case "shatter": case "b": look = PuzzleLook.Shatter; return true;
                default: look = PuzzleLook.Pota; return false;
            }
        }
```
2. `Parameters`: add `look` (string, optional) and `closeup` (bool, optional) with `ToolParameter` descriptions
("pota | candy (a) | shatter (b); default: the game's current look", "zoom ×2.4 on the first tap's block and play at ×0.3").
3. `HandleCommand`, after `_win = …;`:
```csharp
            string lookArg = AIToolsCommon.GetString(p, "look", null);
            if (!string.IsNullOrEmpty(lookArg) && !TryLook(lookArg, out _))
                return new ErrorResponse($"unknown look '{lookArg}' (pota, candy/a, shatter/b)");
            _previousLook = game.Look;
            if (!string.IsNullOrEmpty(lookArg) && TryLook(lookArg, out var look)) game.SetLook(look);
            _closeup = AIToolsCommon.GetBool(p, "closeup", false);
```
4. `Tick`, right after `game.Begin();` in the dealing block:
```csharp
                if (_closeup)
                {
                    var fit = game.Cam.GetComponent<ColorHuntCamera>();
                    int focus = _win ? Cell(game, "auto") : ColorHuntHint.Best(game.Board);
                    if (fit != null && focus >= 0) { fit.Zoom = CloseupZoom; fit.ZoomFocus = game.CellWorld(focus); }
                    game.Juice?.TimeCtl?.SlowMo(CloseupTimeScale, 3600f);
                }
```
5. First lines of `Stop(string why)`:
```csharp
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            if (game != null)
            {
                game.SetLook(_previousLook);
                var fit = game.Cam != null ? game.Cam.GetComponent<ColorHuntCamera>() : null;
                if (fit != null) fit.Zoom = 1f;
                if (_closeup) game.Juice?.TimeCtl?.Clear();
            }
            _closeup = false;
```
6. `Status()`: add `look = Object.FindAnyObjectByType<ColorHuntGame>()?.Look.ToString(), closeup = _closeup`.

`Tools/potalook/record_unity.sh` (the session's scratch recorder, made a tool):
```bash
#!/bin/zsh
# Records Color Hunt with ai_colorhunt_record: opens the ColorHunt scene (refuses if the open scene has unsaved changes), enters Play,
# records, leaves Play. Usage: Tools/potalook/record_unity.sh NAME ['{"look":"candy","win":true,…}']  → Temp/record/NAME/
set -u
cd "${0:A:h:h:h}"
name=$1
extra="${2:-}"
[[ -z "$extra" ]] && extra='{}'
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
tmp=$(mktemp -d)
python3 - "$tmp/open.json" <<'EOF'
import json, sys
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        'if (s.path != "Assets/_Project/Games/Pota/Scenes/ColorHunt.unity") { if (s.isDirty) return "dirty: " + s.path; '
        'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Games/Pota/Scenes/ColorHunt.unity"); } '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
M call execute_code @"$tmp/open.json" | grep '"result"' | grep -q ColorHunt || { echo "could not open ColorHunt (unsaved scene?)"; exit 1; }
M call manage_editor '{"action":"play"}' > /dev/null
for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
python3 - "$tmp/rec.json" "$name" "$extra" <<'EOF'
import json, sys
params = json.loads(sys.argv[3]); params["name"] = sys.argv[2]
json.dump({"tool_name": "ai_colorhunt_record", "parameters": params}, open(sys.argv[1], "w"))
EOF
M call execute_custom_tool @"$tmp/rec.json" | grep '"message"' | head -1
for i in {1..200}; do
  sleep 3
  r=$(M call execute_custom_tool '{"tool_name":"ai_colorhunt_record","parameters":{"action":"status"}}' | grep '"result"\|"message"' | head -2 | tr '\n' ' ')
  echo "$r" | grep -q 'done\|error\|idle' && { echo "$r"; break; }
done
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
cat "Temp/record/$name/events.json"
rm -rf "$tmp"
```
`chmod +x Tools/potalook/record_unity.sh`.

- [ ] **Step 6: Run the tests**

Run: `~/.local/bin/uv run --with pillow --with pytest pytest -q Tools/potalook` → Expected: all pass (5 + 3 + 4).
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntRecordToolTests` → Expected: all pass.

- [ ] **Step 7: Record the three looks**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
for look in pota candy shatter; do
  Tools/potalook/record_unity.sh studio-$look-play "{\"look\":\"$look\"}"
  Tools/potalook/record_unity.sh studio-$look-win "{\"look\":\"$look\",\"win\":true}"
done
for look in candy shatter; do
  Tools/potalook/record_unity.sh studio-$look-closeup "{\"look\":\"$look\",\"closeup\":true,\"script\":\"wait 0.3; tap auto; wait 1.6\",\"tail\":0.3}"
done
```
Expected: each prints `done: N frames` and an events.json with `tap` (+ `rotate`, or `win`). Then
`~/.local/bin/uv run Tools/mcp_call.py call read_console '{"types":["error"],"count":20}'` shows no error from the three looks
(spec §5: each look plays tap, chain and win without errors); fix any before going on.
Pota's recordings: reuse `$S/potalook-pota-play` and `$S/potalook-pota-win` (webm + events.json) when they exist; otherwise record them
again from the scratch copy exactly as the previous plan's Task 11 did (rsync to `$S/pota-copy`, vite on :5199,
`node Tools/potalook/record-pota.mjs http://localhost:5199/ $S/potalook-pota-play play` and `… win`), stop vite, and check
`git -C ~/projects/Pota status` is unchanged. Note in the ledger which Pota state the recordings show.

- [ ] **Step 8: Sheets, side video, close-ups**

```bash
S=/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/ddddd890-0b80-4935-84fd-734de99f5121/scratchpad
C="Tools/potalook/compare.py"
~/.local/bin/uv run --with pillow $C "$S/studio" $S/potalook-pota-play/*.webm $S/potalook-pota-play/events.json \
  "A (Candy)=Temp/record/studio-candy-play" "B (Shatter)=Temp/record/studio-shatter-play"
~/.local/bin/uv run --with pillow $C "$S/studio" $S/potalook-pota-win/*.webm $S/potalook-pota-win/events.json \
  "A (Candy)=Temp/record/studio-candy-win" "B (Shatter)=Temp/record/studio-shatter-win"
for k in tap chain win; do Tools/keep_screenshot.sh "$S/studio-$k.jpg" "2026-09-30-studio-$k"; done
~/.local/bin/uv run --with pillow Tools/potalook/side_video.py docs/screenshots/2026-09-30-studio-side.mp4 \
  --pota $S/potalook-pota-play/*.webm $S/potalook-pota-play/events.json \
  --unity "A (Candy)=Temp/record/studio-candy-play" "B (Shatter)=Temp/record/studio-shatter-play"
for look in candy shatter; do
  /opt/homebrew/bin/ffmpeg -hide_banner -loglevel error -y -framerate 50 -pattern_type glob -i "Temp/record/studio-$look-closeup/f_*.png" \
    -vf "fps=30,format=yuv420p" -c:v libx264 -crf 20 -movflags +faststart docs/screenshots/2026-09-30-studio-closeup-$look.mp4
done
ls -la docs/screenshots/2026-09-30-studio-*
```

- [ ] **Step 9: Look, tune (at most three rounds), stop**

Read the three sheets and three frames from each close-up (`ffmpeg -ss` grabs at 25 %, 50 %, 75 %). Compare against spec §2 and §3:
readable puff/pieces, stickers streaming and landing, words, rings, glow; no clipping, no pink, nothing hidden behind the HUD, no frame
where the board is unreadable. Each fix: a number or a line in the owning class/shader, a ledger line
`Task 13: Ruling: <what> <old → new> — <what the sheet showed> — <cost if wrong>`, re-record the affected look, regenerate. After three
rounds, stop and list what still differs in the ledger.

- [ ] **Step 10: Full suites and commit**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` and `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → all pass.
```bash
git add Assets/_Project/Games/Pota Tools/potalook docs/screenshots/2026-09-30-studio-*
git commit -m "feat: Pota | A | B — record looks and close-ups, three-row moment sheets, three-column side video

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Docs, backlog, memory

**Files:**
- Modify: `Assets/_Project/Games/Pota/README.md`, `CLAUDE.md` §12, Blender `CLAUDE.md` §10, Blender `docs/studyo.md` (roadmap item 4),
  Blender `docs/backlog.md`
- Create/modify: memory `studio-polish-2026-09-30.md` + `MEMORY.md` line

- [ ] **Step 1: Pota README** — add a section "Renk Avı: stüdyo görünümleri A/B ve cila (2026-09-30)" after the Pota-look section:
  - How to switch: `Settings/ColorHuntSettings.asset` → `Look` (Pota / Candy = A / Shatter = B); at runtime `ColorHuntGame.SetLook`.
  - What each look draws (spec §2) and the shared polish (spec §3), in one list each.
  - Where the numbers live: `StudioCurves` (polish), `CandyBurst`/`ShatterBurst` (per look), `ShatterDebris` (piece motion).
  - Assets: the Blender tools and ids (`vfx_flipbook` recipes `puff_toon`, `ring_toon`, `smoke_lit`, `shock_ring`; `vfx_fracture`
    → `vfx-pillow-shatter`), `ai_studio_import`, the templates (`PotaMaterialTemplates`).
  - Recording: `Tools/potalook/record_unity.sh NAME '{"look":"candy","closeup":true}'`, `compare.py` with `LABEL=DIR` rows,
    `side_video.py`; deliverables `docs/screenshots/2026-09-30-studio-*`.
  - The rulings of this plan that change behaviour (decisions 2, 3, 5, 7) in one line each.
- [ ] **Step 2: `CLAUDE.md` §12**
  - Date line: `_Son güncelleme: 2026-09-30 (Renk Avı stüdyo görünümleri: A "Şeker patlaması", B "Gerçek kırılma", ortak cila; Tuna seçecek)._`
  - AITools: `ai_import_asset` also takes `vfx-flipbook` hand-offs (`files.sheet`, `flipbook` block); Pota tools pointer gains
    `ai_studio_import` and `ai_colorhunt_record` `look`/`closeup`.
  - Lessons (only those this plan proved; each one line): the opaque texture is per camera (`requiresColorOption`), turn it on only for
    the look that needs it; mesh particles with a custom shader and no instancing pragma render through the CPU path (fine for ≤ 200);
    `ParticleSystem` vertex colours need `applyActiveColorSpace = false` when the shader treats them as linear (Candy chunks); a HUD
    component added at runtime in `Bind` needs no scene rebuild; the Blender flipbook recipes render on a transparent film with the
    Standard view (Raw for data).
  - Tests line: the counts from the last full runs.
  - Status: "Aktif: Renk Avı A/B adayları hazır, Tuna'nın seçimi bekleniyor (video `docs/screenshots/2026-09-30-studio-side.mp4`);
    seçilen görünüm ayrı bir cila turunda varsayılan olacak".
- [ ] **Step 3: Blender repo** — `CLAUDE.md` §10 status (vfx tools, test count); `docs/studyo.md` item 4: "ilk adım bitti (2026-09-30):
  `vfx_flipbook`, `vfx_fracture`, Unity'de A/B; sıradaki: Tuna'nın seçtiği görünümün cilası"; `docs/backlog.md`: every deferred item
  found in this plan (from the ledger's "Ruling"/"note" lines and Task 13 Step 9's leftovers), next U/B numbers, e.g. mobile compression
  for the four flipbooks (extends U18), the opaque-texture phone cost if Task 12 measured a risk. Commit there:
```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
git add CLAUDE.md docs/studyo.md docs/backlog.md
git commit -m "docs: vfx tools status, studio roadmap item 4 first step, backlog items from the Color Hunt studio looks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
- [ ] **Step 4: Memory** — write `/Users/tunaaltin17/.claude/projects/-Users-tunaaltin17-projects-Unity-MCP/memory/studio-polish-2026-09-30.md`
  (type project): what shipped, how to switch looks and record, where the Blender tools live, the plan's rulings that matter later,
  "Tuna picks A or B next; the winner's polish round makes it the default"; link `[[colorhunt-pota-look-2026-09-30]]`,
  `[[studio-pipeline]]`, `[[feedback-quality-over-cost]]`. Add its `MEMORY.md` line; update `studio-pipeline.md`'s "next" (item 4 begun).
- [ ] **Step 5: Full suites and commit**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` and `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → all pass;
write the counts into `CLAUDE.md` §12.
```bash
git add Assets/_Project/Games/Pota/README.md CLAUDE.md
git commit -m "docs: Color Hunt studio looks — README, CLAUDE.md status and lessons

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
