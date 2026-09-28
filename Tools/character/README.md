# Character pipeline (image → mesh → rig → animation → Unity)

Scripts that turn a single reference image into an animated Humanoid character for Unity, using Replicate models.
Paid outputs land in `out/` (git-ignored, keep them) with a `<name>.koken.json` provenance file each (tracked).
Token: `REPLICATE_API_TOKEN` env or `~/projects/copy/.env`.

| Step | Model | Script / tool | Cost seen |
|---|---|---|---|
| 1. A-pose front view of the character | `google/nano-banana-pro` | `rep.py run google/nano-banana-pro out/apose2.json` (identity locked to `kahraman.png`, green chroma) → `chroma.py` keys it to RGBA | ~$0.15 / image |
| 2. Image → GLB mesh | `firtoz/trellis` (chosen) or `fishwowater/trellis2` | `rep.py run <model:version> out/mesh_t1.json` | $0.04 (trellis, 30 s) / $0.41 (trellis2, 5 min + 6 min queue) |
| 3. Auto-rig (Mixamo bone names, T-pose) | `uthana/create-character-v1` | `rep.py run uthana/create-character-v1:<v> out/rig_t1.json` | < $0.01, 30 s |
| 3b. Make the rig glTFast-friendly | – | `fix_rig_glb.py in.glb out.glb` (bakes the 0.01-scaled Armature into metre joints, recomputes inverse bind matrices, metallic 0) ; `glb_webp_to_png.py` if textures are WebP | – |
| 4. Text → Humanoid animation (Y Bot FBX) | `uthana/text-to-motion-diffusion-v2` | `rep.py run uthana/text-to-motion-diffusion-v2:<v> out/motion/shoot2.json` | < $0.01 / clip, 3–12 s |
| 5. Unity: Humanoid avatar + AnimatorController + prefab | – | MCP tool `ai_character_setup {"model_path": "Assets/_Project/Games/Pota/Art/Mesh/player_rig_a.glb"}` (Pota'da, bkz. `Assets/_Project/Games/Pota/README.md`) | – |
| QA: render a clip on a model as one image | – | MCP tool `ai_anim_contact_sheet {"model_path": ..., "clip_path": ...}` (also prints per-frame bone heights) | – |

Version hashes for the community models are in `versions.txt` (they change; refresh with the Replicate API).

Lessons (2026-09-25):
- Community models need `owner/name:version`; official ones (`google/...`) take `POST /models/{owner}/{name}/predictions`.
- `urllib` gets 403 from Replicate without a browser-like `User-Agent`; the `replicate` Python client hung on file upload here, so `rep.py` uses curl for uploads and plain HTTP with timeouts.
- TRELLIS.2 refuses `decimation_target` < 100000 and outputs WebP textures (glTFast cannot read them); its texture also lost the jersey number. The older TRELLIS kept the "7" and was 10× cheaper/faster.
- Uthana's rig is exported from Blender with an `Armature` node (scale 0.01, rotation 90°). Per the glTF spec that is fine (`G_j·IBM_j = I`), but glTFast skins it wrong; `fix_rig_glb.py` normalises it. Its material has no `metallicFactor` (glTF default 1.0 → dark); the fixer sets 0.
- Unity Humanoid needs the feet at the root's y = 0 (the tool lifts the skeleton) and a real T-pose; creating an Avatar asset over an existing one leaves a broken avatar (delete first), and the prefab must be re-imported after its avatar/controller are created.
- Motion prompts: describe the body mechanics ("bends the knees, jumps, extends both arms above the head, snaps the wrists") with `cfg_scale` 2.5; sport vocabulary alone ("jump shot") gave weak clips. Judge clips by the bone-height table, not thumbnails.
