# Studio VFX Kit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Any Blender `vfx-flipbook` / `vfx-mesh` delivery becomes a pooled `VfxService` effect in one tool call, with a deterministic contact-sheet tool to look at it.

**Architecture:** Core gets a generic flipbook shader, an `IVfxPart` hook so `VfxInstance` can pool non-particle parts, a closed-form `MeshBurst` part for multi-piece meshes, an editor tool that builds prefab + material + `VfxDefinition` + library entry from a delivery folder, and a contact-sheet tool that renders the prefab at fixed times without Play Mode.

**Tech Stack:** Unity 6000.6.2f1, URP 17.6 (HLSL, SRP Batcher), Shuriken, glTFast-imported deliveries, Unity Test Framework via `Tools/unity_tests.py`, MCP custom tools (`[McpForUnityTool]`).

**Spec:** `docs/superpowers/specs/2026-10-04-studio-vfx-kit-design.md`

## Global Constraints

- Core never references a game (asmdefs `Project.Runtime`, `Project.Editor`, tests `Project.Tests.EditMode` / `Project.Tests.PlayMode`).
- Tool-assigned references are serialized (`[SerializeField]` / asset refs); asset writes through `AssetDatabase`; regenerating the same id keeps GUIDs.
- `AddComponent<ParticleSystem>` starts playing: `Stop(true, StopEmittingAndClear)` before setting `duration`.
- Particle vertex colour is 8-bit: HDR brightness lives in the material (`_Intensity`).
- No paid calls, never bring Unity to the front, no compile in Play Mode; commit after each task (`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).

## Review Focus

1. Effect with only `MeshBurst` (no particles) must not return to the pool on its first frame (Task 1).
2. Replaying a pooled `MeshBurst` must restart from the rest pose, not from where the last play ended (Task 1).
3. A flipbook whose `frames` < `cols×rows` must never show the empty trailing cells (Task 3).
4. Regenerating an effect for the same id must not duplicate the library entry or change GUIDs (Task 3).
5. A `linear: true` data sheet and a folder without `manifest.json` fail with a clear message, no half-written assets (Task 3).

---

### Task 1: `IVfxPart`, `VfxInstance` support, `MeshBurst`

**Files:** Create `Core/Scripts/VFX/IVfxPart.cs`, `Core/Scripts/VFX/MeshBurst.cs`; modify `Core/Scripts/VFX/VfxInstance.cs`; tests `Core/Tests/EditMode/MeshBurstTests.cs`, `Core/Tests/PlayMode/VfxPartPlayTests.cs`.

**Interfaces (produced):**
- `public interface IVfxPart { void Restart(); void Clear(); bool IsAlive { get; } }`
- `public class MeshBurst : MonoBehaviour, IVfxPart` — serialized `speed=2.5f, spread=0.6f, upBias=0.6f, gravity=9.81f, spin=540f, lifetime=1.0f, shrink=0.35f, seed=7, useUnscaledTime=false`; `public void Evaluate(float t)` (closed form; t ≥ lifetime → scale 0), `public float Lifetime => lifetime`, `public int PieceCount`.
- `VfxInstance` also collects `GetComponentsInChildren<IVfxPart>(true)`: `Play` → `Restart()`, `Stop` → `Clear()`, `AnyAlive` → any `IsAlive`.

Steps:
- [ ] Tests first: `Evaluate(0)` = rest pose; pieces at t=0.3 are farther from the burst centre than at rest; t ≥ lifetime → every piece scale 0 and `IsAlive` false; same seed → same positions; `Restart` after an `Evaluate(0.5)` restores rest pose (EditMode). PlayMode: a `VfxDefinition` whose prefab has only `MeshBurst` (lifetime 0.6) is still active after 0.2 s and back in the pool after 0.8 s.
- [ ] Implement, compile, run `Project.Tests.MeshBurstTests` + `Project.Tests.VfxPartPlayTests` and the existing VFX tests; commit.

### Task 2: `Project/Flipbook` shader

**Files:** Create `Core/Shaders/Flipbook.shader`; test in `Core/Tests/EditMode/FlipbookShaderTests.cs`.

**Interfaces (produced):** shader `Project/Flipbook`, properties `_BaseMap` (2D), `_Intensity` (Float, default 1), `_SrcBlend`/`_DstBlend` (Float; Alpha = SrcAlpha/OneMinusSrcAlpha, Additive = SrcAlpha/One), `CBUFFER_START(UnityPerMaterial)`; `public static class FlipbookMaterial { Material Create(Texture2D sheet, bool additive, float intensity); void SetBlend(Material m, bool additive); }` in `Project.VFX` (runtime, so games can make variants).

Steps:
- [ ] Test: `Shader.Find("Project/Flipbook")` not null and `isSupported`; `FlipbookMaterial.Create` sets the blend floats and keeps `_Intensity`; the shader passes the SRP Batcher check (`ShaderUtil.GetShaderMessageCount == 0`).
- [ ] Implement, compile, run, commit.

### Task 3: `ai_vfx_from_delivery`

**Files:** Create `Core/Editor/AITools/VfxFromDeliveryTool.cs`; test `Core/Tests/EditMode/VfxFromDeliveryToolTests.cs` (fixtures: `Tools/fixtures/handoff-flipbook`, a wide still variant written to Temp, `handoff-cube` as single mesh, a two-piece mesh fixture made in Blender `Tools/fixtures/handoff-pieces/`).

**Interfaces:**
- Consumes: `ImportAssetTool.Import`, `FlipbookMaterial.Create`, `MeshBurst`, `VfxDefinition`, `VfxLibrary`, `VfxAuthoringTool.LibraryPath`.
- Produces: `public static VfxFromDeliveryTool.Result Build(string folder, Options o)`; `Options { id, outFolder, library, size=1, count=-1, blend="alpha", intensity=1, color=white }`; `Result { id, prefab, material, definition, library, kind, frames, lifetime }`; MCP `ai_vfx_from_delivery {"folder"|"manifest", "id"?, "out"?, "library"?, "size"?, "count"?, "blend"?, "intensity"?, "color"?}`.

Behaviour (spec §3.4): flipbook multi-frame → one-burst `ParticleSystem` (count default 1, lifetime = `duration_s`, `textureSheetAnimation` Grid `cols×rows`, `frameOverTime` linear 0 → `frames/(cols·rows)` so trailing empty cells never show, `startFrame` 0, `cycleCount` 1; `loop` → looping system, lifetime `duration_s×4`, cycles 4); single frame → `startSize3D` with the sheet aspect, size-over-lifetime pop 0 → 1.15 (15 %) → 1 and colour-over-lifetime alpha fade last 30 %; single-piece mesh → mesh particles (count default 12, random 3D rotation + angular velocity, gravity 1, speed 2–4, shape sphere); multi-piece mesh → instantiate model + `MeshBurst`; linear data sheet → `ArgumentException`. New game library starts with the Core library's entries. Same id → assets overwritten in place (prefab via `PrefabUtility.SaveAsPrefabAsset` on the same path, material/definition updated, library entry replaced not added).

Steps:
- [ ] Tests first (all five Review Focus items 3–5 plus: prefab has `VfxInstance`-free root with ParticleSystem using `Project/Flipbook`; definition lifetime = manifest duration; still aspect 2:1 for 512×256; single mesh → `ParticleSystemRenderMode.Mesh`; two-piece mesh → `MeshBurst` with 2 pieces; library contains Core ids + new id once after two builds; GUIDs equal across builds).
- [ ] Make the two-piece fixture in headless Blender (two cubes, glb, manifest via `bl.export`).
- [ ] Implement, compile, run, commit.

### Task 4: `ai_vfx_contact_sheet`

**Files:** Create `Core/Editor/AITools/VfxContactSheetTool.cs`; test `Core/Tests/EditMode/VfxContactSheetToolTests.cs`.

**Interfaces (produced):** `public static Result Render(GameObject prefab, float[] times01, int size, string outPath)`; `Result { png, visiblePixels[] , duration }`; MCP `ai_vfx_contact_sheet {"prefab", "frames"?=8, "size"?=256, "out"?}` (two rows: dark and light background).

Steps:
- [ ] Test: sheet for the fixture flipbook effect exists, width = frames·size, every frame before the end has visible pixels > 0, the last frame (t = 1) of a non-looping effect is empty or faded; scene has no leftover objects.
- [ ] Implement (hidden root far away like `AnimContactSheetTool`, `ParticleSystem.Simulate(t, true, true, false)`, `MeshBurst.Evaluate(t)`, camera framed from the prefab bounds over all sampled times), compile, run, commit.

### Task 5: Proof on the Pota deliveries + docs

- [ ] Build all 19 effects into `Assets/_Project/Experiments/StudioVfx/` (`out`, `library` = `Experiments/StudioVfx/StudioVfxLibrary.asset`), render each contact sheet, combine into `docs/screenshots/2026-10-04-studio-vfx-kit.jpg` (`Tools/keep_screenshot.sh`), look at every row; fix what looks wrong.
- [ ] Showcase: `Experiments/StudioVfx/StudioVfx.unity` with a `VfxService` + a small spawner that plays each id in turn; PlayMode test that every id plays and returns to its pool.
- [ ] Update CLAUDE.md §12 (tool list, VFX section), `~/projects/Blender MCP/docs/studyo.md` (asset path: delivery → effect), full EditMode + PlayMode run, commit.
