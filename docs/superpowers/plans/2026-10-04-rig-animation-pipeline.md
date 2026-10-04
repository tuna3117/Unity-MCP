# Rig + Animation Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A character model from the studio becomes a rigged hero with stylised (Supercell-like) clips on its own skeleton, imported into Unity as Generic with secondary motion, measured against raw AI motion.

**Architecture:** Three workshops, file hand-offs only. copy makes the paid calls (Uthana rig + text-to-motion on Replicate) with the spend ledger; Blender scores/fixes the rig, retargets the Y Bot clips onto the hero, stylises them by recipe, measures them and exports one FBX+glb `kind: character` delivery with `clips_meta`; Unity imports it Generic with per-clip loops, builds the prefab, adds squash/stretch and spring bones, and shows the clips in a showcase.

**Tech Stack:** copy (TypeScript, tsx, vitest, Replicate), Blender 5.2.2 headless Python + numpy (`tools/run.sh`, unittest), Unity 6000.6.2f1 (ModelImporter, Animator, Unity Test Framework).

**Spec:** `docs/superpowers/specs/2026-10-04-rig-animation-pipeline-design.md` (research: `docs/reports/2026-10-04-rig-anim-research.md`)

## Global Constraints

- Spend cap 15 USD for this sub-project on top of the ledger total of 2026-10-04 (`~/projects/copy/cikti/harcama.jsonl`); every paid call through copy's CLIs with `--kurugidis` first; never retry a paid call.
- Headless only: no GUI Blender (`blender_up.sh`), never `open -a`/osascript; Unity work goes through the bridge in the background.
- Delivery contract (`docs/studyo.md`): `assets/<Game>/<id>/export/{<id>.fbx, <id>.glb, manifest.json, preview.png}`; for animated characters `kind: "character"`, `stats.clips`, `stats.bone_names`, `clips_meta: [{name, frames, fps, loop}]`, `import: {rig: "generic"}`.
- Hero clips are Generic (bone scale survives); the game's current Basket player is not changed.
- Commit after each task in the repo it touches; English messages ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; never push.

## Review Focus

1. A Y Bot clip with longer legs than the hero: retargeted feet must stay on the floor (lowest foot ≈ 0), not float or sink (Task B2).
2. A clip name in `clips_meta` that the FBX lacks: Unity reports it as a difference, never silently drops it (Task C1).
3. Stylisation steps applied to a looping clip must keep the loop seamless (first = last pose after `loop`) (Task B3).
4. A big-head rig whose AI weights pull head vertices with the arms: `rig_check` flags it and `rig_reweight` fixes it (Task B1).
5. SpringBone on an unanimated chain must not drift (its own bend never feeds back) (Task C2).

---

## Track A — copy (paid calls)

### Task A1: `cli/iskelet.ts` and `cli/hareket.ts`
Uthana `create-character-v1` and `text-to-motion-diffusion-v2` with the ledger, dry run, provenance, batch list for motion; unit tests for input building and pricing; README section; prove with 1 rig + 3 motion calls on the hero (< 0.10 USD). `Unity MCP/Tools/character/rep.py` gets a pointer to the new commands.

## Track B — Blender workshop (`~/projects/Blender MCP/tools/bl/`)

### Task B1: `rig_check` + `rig_reweight`
- `rig_check(path, out=None)` → `{score, landmarks, bleed, intersections, png}`: neck/shoulder/elbow/knee joint vs mesh cross-section landmarks (deviation as % of height), head-region weight bleed from non-head/neck bones, stress poses (arms up, arms crossed, deep crouch, head 45°) with approximate self-intersection count (BVH overlap) and one PNG.
- `rig_reweight(path, head_rigid=True)` → same skeleton, `ARMATURE_AUTO` weights, vertices above the neck 100 % head.
- Tests: a figure fixture with deliberately bad weights (head verts on the arm) scores lower and reweight raises it; landmark deviation 0 on a rig fitted to its own mesh.

### Task B2: `anim_retarget(source, target, clips=None)`
- Mixamo-named source (Uthana Y Bot FBX) → target armature (hero, Mixamo names, possibly `mixamorig:` prefix on either side): per bone, world rotation delta from rest transferred onto the target rest; hips translation scaled by leg-length ratio; missing bones reported; slotted actions via `bpy_extras.anim_utils`.
- Tests: retarget onto an identical skeleton is identity; long-legged source onto short-legged target keeps the lowest foot at ≈ 0 over the clip.

### Task B3: `anim_stylize(path, clip, recipe)` + recipes
- Steps (each its own function and test, skipped when absent from the recipe): `foot_lock`, `reduce`, `timewarp`, `anticipate`, `overshoot`, `exaggerate` (per region, quaternion log space around a low-passed curve), `squash` (helper bone `squash` added under the root if missing, y-scale with x = z = 1/√y), `loop`, `in_place`.
- Recipes `recipes/anim/<clip>.json` for idle, run, shoot, jump, victory, defeat, hit, celebrate.
- Tests per step on synthetic curves (e.g. foot speed under threshold → locked position constant; loop → first = last; exaggerate k = 1 → unchanged).

### Task B4: `anim_metrics(path, clip)` + `character_animated(rig, clips, game, id, version)`
- Metrics (spec §5.5) as JSON + one Markdown row.
- `character_animated` writes the delivery (FBX with all actions as takes, glb, manifest with `clips_meta`, `bone_names`, `import.rig`), preview = `anim_sheet` of the first clip.
- Tests: metrics on a synthetic hold-move-hold clip (hold share ≈ known value); delivery manifest lists every action and FBX reimport has the same take count.

## Track C — Unity (`Unity MCP`)

### Task C1: `ai_import_asset` U1/U4
FBX `ModelImporter` settings from `import.rig` (generic default when bones > 0, humanoid with avatar check, none) and `clips_meta` (names, loops; missing clip = difference); meshless deliveries count skeleton bones (`stats.bone_names`, else below the armature node). Fixtures `Tools/fixtures/handoff-rig-clips/` (two takes) and `handoff-anim-only/`.

### Task C2: `SquashStretch` + `SpringBone` (Core Juice)
Tests: volume kept, spring back with overshoot, landing clamp; spring bone lags a sideways jerk, settles, never drifts when nothing animates it.

### Task C3: `ai_character_prefab` (Core) + `ai_anim_showcase`
- `ai_character_prefab {model, prefab?, toon?, squash?}`: Generic FBX delivery → prefab: wrapper (SquashStretch) → model instance with Animator + generated controller (one state per clip, first = default, transitions ≤ 0.1 s), toon material via `ToonMaterialTool`. Pota's Humanoid `ai_character_setup` stays in Pota (game-specific, BasketSettings).
- `ai_anim_showcase {prefab, scene}`: plays each state in turn with its name on screen.
- Tests: prefab from the two-clip fixture has an Animator with two states, wrapper scale 1, controller asset next to it.

## Track D — Proof

### Task D1: Hero × 8 clips
- copy: hero rig (Track A output) + 8 Uthana clips (prompts in the plan's recipe files; ≤ 2 takes each).
- Blender: rig_check on Uthana weights vs reweight → pick; retarget each clip (raw); stylise by recipe; metrics raw vs stylised; deliver `pota-hero-anim`.
- Unity: import, prefab, showcase in `Experiments/StudioAnim/`, contact sheets (`ai_anim_contact_sheet`) and a side-by-side video Raw | Stylised per clip (`docs/screenshots/2026-10-04-anim-*`), metrics report `docs/reports/2026-10-04-anim-metrics.md`.
- Look at every clip; fix what looks wrong before calling it done; update `docs/studyo.md`, both CLAUDE.md files, backlog (close U1, U4).
