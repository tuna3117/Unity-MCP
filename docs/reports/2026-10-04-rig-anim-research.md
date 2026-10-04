# Rig + animation pipeline research (sub-project 3)

Date: 2026-10-04. Scope: API-callable auto-rig and AI motion services, commercial animation libraries, headless Blender 5.2 clean-up and stylisation, Supercell-style animation traits, and Unity 6 secondary motion. No paid API was called. Prices were checked on the official pages where they could be loaded. Anything marked **[unverified]** came only from third-party pages or search snippets, or could not be confirmed at all.

Context from our own runs (2026-09-25, `Tools/character/out/*.koken.json`): Uthana `create-character-v1` on Replicate took 29–39 s per rig. `text-to-motion-diffusion-v2` took 3–12 s per clip. Both cost less than $0.01 per call on our Replicate bill. The v2 model outputs **20 fps** on Uthana's default biped (Y Bot).

---

## 1. Auto-rigging services callable by API

| Name | API? | Price per call | Skeleton / naming | Fingers | Output | Licence | Quality notes (stylised, big head) | Source |
|---|---|---|---|---|---|---|---|---|
| **Uthana `create-character-v1` on Replicate** (what we use) | Yes (Replicate) | ~<$0.01 (our bill; Replicate bills by GPU-seconds) | Standard biped with Mixamo names (our 2026-09-25 rig had 52 bones). No face bones. | Yes (in our rig) | FBX, GLB | Not stated on the Replicate page **[unverified, ask Uthana before shipping]** | Needs T- or A-pose, feet on the ground, clean mesh. glTFast mis-skins its 0.01 Armature unless we run `fix_rig_glb.py`. | https://replicate.com/uthana/create-character-v1 |
| **Uthana API (direct)** | Yes (GraphQL + Python/TS SDKs since 2026-04) | **$0.15 per auto-rig**, pay-as-you-go only (new subscriptions stopped Aug 2026) | Any biped. `include_fingers` (default true), `rerig_target` = `r15` or `ue5`, `auto_rig_feet_on_floor`. Returns `auto_rig_confidence` 0–1. | Yes | FBX, GLB, GLTF in (<30 MB); FBX, GLB out | "Paid usage includes commercial use of generated outputs" | Docs list known weak cases: **"extreme proportions (oversized body parts)"**, limb intersections, flowing clothes. A big head is exactly this risk. | https://uthana.com/pricing, https://uthana.com/docs/api/capabilities/auto-rig-and-add-character |
| **Tripo Auto Rig** | Yes (`POST /v3/animations/rig`, free `rig-check` first) | **25 credits = $0.25** (1 credit = $0.01). The docs example shows `credits_consumed: 30`. | `spec`: `tripo` or **`mixamo`**. Humanoid model `v1.0-20240301`. `v2.5-20260210` covers 7 creature types. | **[unverified]** | GLB, FBX (input GLB/GLTF/FBX/OBJ/STL up to 150 MB) | Paid API output; terms not read **[unverified]** | Third-party test (May 2026): best on a stylised robot, but shoulder/chest weights over-smoothed and jaw weights noisy. | https://developers.tripo3d.com/en/models/rig, https://developers.tripo3d.com/en/pricing, https://developers.tripo3d.com/en/docs/animations-rig.md |
| **Meshy Auto-Rigging** | Yes. The direct API needs a paid plan (Pro $20/mo, 1,000 credits). Also on **fal.ai**. | Direct: **5 credits** (~$0.10 at Pro's $0.02/credit **[derived]**). fal: **$0.20 per request + $0.12 per extra clip**. | Humanoid only, textured mesh only, face toward +Z, `height_meters`, <300k faces. Bone naming is non-standard for spine and clavicle per the third-party test. | **[unverified]** | FBX, GLB, plus walk and run clips | Free plan is CC BY 4.0; Pro and above: "user owns output" | The third-party test praised tolerance of varied topology but found hip/pelvis weighting inconsistent. | https://docs.meshy.ai/en/api/pricing, https://docs.meshy.ai/en/api/rigging, https://fal.ai/models/fal-ai/meshy/rigging/multi-animation/llms.txt |
| **UniRig** (VAST, MIT) via `aaronjmars/unirig-ai` | Yes (Replicate) or self-host (CUDA, 8 GB+) | **~$0.46 per run** (A100, ~6 min) | Articulation-XL2.0 skeleton, **not Mixamo-named**, so a name map or re-rig is needed. The full VRoid/Rig-XL checkpoint is still "coming soon". | Not documented | FBX (+NPZ) | MIT (code). Replicate wrapper terms not checked. | Research-grade. The successor SkinTokens (2025) claims 98–133% better skinning. Costs more and does less for a humanoid than the options above. | https://replicate.com/aaronjmars/unirig-ai, https://github.com/VAST-AI-Research/UniRig |
| **Puppeteer** (NeurIPS 2025, Apache-2.0) | Self-host only | GPU time | Auto-regressive skeleton + attention skinning, plus animation | – | – | Apache-2.0 | Research. Could be worth a try later for creatures. | https://arxiv.org/abs/2508.10898 |
| **Anything World / Animate Anything** (now everythinguniver.se) | Yes ("API … process hundreds of models") | Plans $0 / $50 / $250 a month. **Credits per rig not published [unverified]**. | Humanoids, animals, vehicles | – | – | **[unverified]** | Aimed at bulk and creatures. No quality data for stylised humanoids. | https://everythinguniver.se/pricing |
| **3D AI Studio "Prism"** (aggregator) | Yes (credits) | 20 credits per rig **[unverified $]** | Mixamo-compatible, all creature types | – | GLB, FBX | **[unverified]** | Aggregator; unclear which model sits behind it. | https://docs.3daistudio.com/processing/rigging |
| **Mixamo** | **No official API** (community scripts call undocumented endpoints: ToS risk) | Free | Mixamo names, optional fingers | Yes | FBX | Free for games. Raw rigs/animations may not be redistributed. **Mixamo content may not be used to train ML models.** | In maintenance mode. The backend broke on 2025-06-16 and was only partly restored. Weak on stylised meshes. | https://community.adobe.com/t5/mixamo-discussions/mixamo-faq-licensing-royalties-ownership-eula-and-tos/m-p/13234775 |
| **AccuRIG 2 / ActorCore** (Reallusion) | **No API**, desktop app, **Windows-only** | Free (ActorCore account needed to export) | Full body + fingers. Close to the UE mannequin. | Yes | FBX, USD | Free tool. ActorCore motions are paid per motion. | Tight weights, but scale-sensitive, expects A-pose, no non-humanoids. Not usable on our Mac or headless. | https://80.lv/articles/reallusion-launches-free-rigging-tool-accurig-2-0, https://forum.reallusion.com/PrintTopic541835.aspx |
| **Rodin (Hyper3D) Gen-2 / 2.5** | Yes (generation) | – | **No built-in rig.** Only T/A-pose control, which we already use. | – | – | – | Keep using it for the mesh. | https://runware.ai/docs/models/hyper3d-rodin-gen-2 |
| **Cascadeur auto-rig** | App with a Python API and (since 2026.2) an MCP server. Not a cloud API. | Indie $19/mo or $96/yr | Retarget-ready FBX names | – | FBX | Commercial on paid tiers | "Auto-rig OK but not great". It is an animation tool, not a rigger. | https://www.cgchannel.com/?p=177897 |
| **Blender own fit** (template skeleton + `ARMATURE_AUTO` heat weights) | Ours, headless | $0 | Whatever we define: Mixamo names plus our extra bones (squash and head-scale bones) | Yes | FBX, GLB | Ours | Bone heat needs a closed mesh. `character_model` already guarantees 0 open edges. This is the fallback when an AI rig's weights fail on the big head. | (Blender built-in) |

Third-party bake-off of Tripo, Meshy, Cascadeur, AccuRIG and Mixamo (May 2026, ~30k-triangle humanoid, Blender 5.1 / UE 5.7): https://www.strayspark.studio/blog/ai-auto-rigging-showdown-2026-tripo-meshy-cascadeur-mixamo. **No published test covers chibi or big-head characters [gap]**, so we have to run our own.

---

## 2. AI motion generation callable by API

| Name | API? | Price | Length / fps | Skeleton and retarget | Loops | Style control | Licence | Source |
|---|---|---|---|---|---|---|---|---|
| **Uthana text-to-motion v2 on Replicate** (what we use) | Yes | ~<$0.01 per clip (our bill) | "Typical 2–6 s", **20 fps** | Uthana default biped (Y Bot). Retargets via Unity Humanoid or Blender. `retargeting_ik` flag. | No | Prompt + `cfg_scale`. Realistic mocap style. | Not stated on Replicate **[unverified]** | https://replicate.com/uthana/text-to-motion-diffusion-v2 |
| **Uthana text-to-motion 3.0** (direct API, since 2026-06-15; `fast` flag added in Sept) | Yes (async job) | **$0.10/s** (v2 $0.03/s, v1 $0.02/s on the direct API) | 4–10 s (8 s default), 24/30/60 fps | Retargets directly to *our uploaded character* (motion-only GLB option). `in_place`, `playback_speed` 0.1–2.0×. | Separate "stitch and loop" at $0.01/s | Prompts can carry "pacing, energy, posture, movement style". Manual CFG/seed knobs removed. **No explicit cartoon or exaggeration claim.** | Commercial use included (paid) | https://uthana.com/models/text-to-motion, https://uthana.com/blog/text-to-motion-30-sets-a-new-bar-for-ai-animation, https://uthana.com/docs/api/changelog |
| **Uthana locomotion v1** | Yes | $0.02/s | – | Same retarget path | **Loopable, directional** | Speed/direction | Commercial (paid) | https://uthana.com/pricing |
| **Uthana video-to-motion 2.0/2.1** | Yes | $0.05/s | 4–10 s | Same retarget path | – | Copies the performance in the video | Commercial (paid) | https://uthana.com/pricing, https://help.scenario.com/articles/6720109441-uthana-motion-the-essentials |
| **Meshy Text to Motion** (new) | Yes (direct API, paid plan) | prime **10 credits** (FBX), swift **3 credits** (BVH). About $0.20 / $0.06 **[derived]**. | 2–10 s in 0.5 s steps | Standalone clip, applied to a Meshy-rigged character via `motion_task_id` (3-day retention) | **[unverified]** | Prompt only | Pro+: user owns output | https://docs.meshy.ai/en/api/text-to-motion |
| **Tencent HY-Motion 1.0** via fal (`fal-ai/hunyuan-motion`) | Yes | **$0.08 per generation** | 0.5–12 s | **SMPL-H** skeleton, so retargeting is on us. No loops, no in-place. | No | Prompt + guidance 1–10 | Tencent Hunyuan Community licence (these licences usually exclude the EU, UK and South Korea **[unverified for this model]**). SMPL-H body-model licence is non-commercial. **Licence risk.** | https://fal.ai/models/fal-ai/hunyuan-motion/llms.txt, https://huggingface.co/tencent/HY-Motion-1.0 |
| **DeepMotion Animate 3D (video) / SayMotion (text)** | REST API exists; **API pricing only via sales** | Web plans: 1 credit = 1 s (+0.5/s each for hands and face). Third-party list: Innovator $48/mo 480 cr, Professional $117/mo 1,500 cr **[unverified]**. | – | Upload own FBX/GLB character | – | Video copies the performance | Commercial needs a premium plan | https://www.deepmotion.com/pricing-animate3d |
| **Move AI** (video, s2 single camera) | Yes (developer API) | **$0.024/s** × resolution/fps multipliers (search snippet of the official pricing page; the page returned 404 on fetch) **[partly verified]** | – | – | – | Video performance | **[unverified]** | https://developers.move.ai/docs/pricing |
| **Kinetix** (video/text to animation) | API mentioned, pricing opaque | Web €15/mo (3 min video), €120/mo (30 min) **[unverified]** | – | – | – | – | **[unverified]** | https://www.toolmage.com/fr/tool/kinetix/ |
| **Rokoko Vision** | No public API found | Free 30 s/mo, paid tiers by seconds **[unverified]** | – | – | – | – | – | https://rokoko.com/pricing/ |
| **Plask** | API is "pay as you go, contact sales" | Web $18 / $50 a month | – | – | – | – | – | https://motions.plask.ai/products-api |
| **Tripo animation (preset retarget)** | Yes | **10 credits = $0.10 per clip** (batch up to 5) | Presets (idle, walk, run, dive, climb, jump, slash, shoot, hurt, fall, turn; 100+ total) | Its own rig. `animate_in_place`. Baked into geometry by default (can be disabled for GLB). | – | **No text-to-motion** | Paid API | https://developers.tripo3d.com/en/models/animation |
| **Academic models** (MoMask, MotionGPT, T2M-GPT…) | Self-host | GPU | HumanML3D/SMPL | – | – | – | Trained on AMASS/HumanML3D, which are **research-only**. Avoid for shipping. | https://arxiv.org/abs/2312.00063 |
| **AI video → video-to-motion** (new idea, not a product) | Yes (two calls) | e.g. Kling 2.5 Turbo Pro on fal **$0.35 per 5 s** + Uthana video-to-motion $0.05/s | 5–10 s | Through Uthana retarget | – | **The only route here that can carry cartoon timing**, if the video model performs it. Pose estimation on chibi proportions is untested. | Per-vendor | https://fal.ai/models/fal-ai/kling-video/v2.5-turbo/pro/image-to-video/llms.txt |

What this means: every text-to-motion service above is trained on realistic human mocap. None of them claims cartoony or exaggerated output, and Uthana v3's "movement style" prompting is the closest thing. Stylisation has to come from our Blender pass (§4). The video route is the only experiment that could bring cartoon timing in from outside.

---

## 3. Ready-made animation libraries (baseline, retarget source)

| Library | Price | Clips | Rig | Licence | Fit for stylised mobile | Source |
|---|---|---|---|---|---|---|
| **Quaternius Universal Animation Library** | Free Standard (~60–70% of clips); Pro/Source via Patreon $10+/mo | 120+ (incl. 8-dir locomotion, sprint, death) | One "universal humanoid" rig, retarget-ready, tested in Unity/UE/Godot | **CC0** | Good neutral baseline, game-cut (in-place, looping). Somewhat generic, not Supercell-snappy. | https://quaternius.com/packs/universalanimationlibrary.html |
| **Quaternius UAL 2** (2026-02-10) | Same model | 130+ (melee/armed combos, parkour, farming/fishing, zombie) | Same rig | **CC0** | Combat and farming clips are useful for Hay Day-type sim characters | https://digitalproduction.com/2026/02/10/130-animations-one-rig-zero-drama/ |
| **Kenney Animated Characters 1–3 / Blocky Characters** | Free | 3 (idle, jump, run) / 27 | Own simple rig | **CC0** | Too few clips; blocky style | https://kenney-assets.itch.io/animated-characters-3, https://opengameart.org/content/blocky-characters |
| **Kevin Iglesias Human Basic Motions FREE** (Asset Store / itch / Gumroad) | Free (full pack $18, 356 files) | 118 files: idles, walk, run, sprint, jump, fall, turn | Unity Humanoid | Asset Store EULA (use in games, no raw redistribution) | Clean, slightly stylised game locomotion. A good "run" baseline. | https://kevdev.itch.io/basic-motions |
| **Mixamo** | Free | ~2,500 | Mixamo | Free for games. No raw redistribution. **No ML training.** | Looks dated. Usable for retarget tests and reference only. | (see §1) |
| **Meshy animation library** | 3 credits per action (direct) or $0.12 per clip (fal) | 590+ (IDs 0–696): DailyActions, WalkAndRun, Fighting, Dancing, BodyMovements | Meshy rig | Pro+: user owns output | Includes "Funny" and stylised variants. Cheap to sample through fal. | https://docs.meshy.ai/en/api/animation-library |
| **Tripo presets** | $0.10 per clip | 100+ (90+ biped) | Tripo rig | Paid API | – | https://developers.tripo3d.com/en/models/animation |
| **ActorCore** (via AccuRIG) | Paid per motion | 4,500+ | CC/AccuRIG | Reallusion licence | Mocap-realistic; Windows tool | https://technode.global/prnasia/accurig-2-debuts-direct-in-app-access-to-4500-actorcore-motions/ |
| **Asset Store toon packs** (e.g. Toony Tiny Citizens, 70 clips; Pocket Heroes) | Mostly paid **[prices unverified]** | – | Humanoid | Asset Store EULA | Closest in *style* to chibi mobile, but we cannot see the quality until bought | https://assetstore-fallback.unity.com/packages/3d/characters/toony-tiny-citizens-megapack-99854 |

Recommendation: use Quaternius UAL 1+2 (CC0) and Kevin Iglesias Basic Motions FREE as the **zero-cost control group** for every clip. If our AI-plus-Blender clip doesn't beat the free CC0 clip side by side, it isn't worth paying for.

---

## 4. Blender 5.2 automation for clean-up and stylisation (headless Python)

### Verified locally on Blender 5.2.2 LTS (`-b --factory-startup`, scratch scripts, no project files touched)

- **Slotted actions:** `action.fcurves` no longer exists. Get F-curves with `bpy_extras.anim_utils.action_get_channelbag_for_slot(action, anim_data.action_slot).fcurves`. Also available: `action_ensure_channelbag_for_slot`, `animdata_get_channelbag_for_assigned_slot`, `bake_action` / `bake_action_objects`.
- **Graph Editor operators do run headless.** With no override their `poll()` is False. In background mode, though, `window_manager.windows[0].screen` exists. Set `area.type = 'GRAPH_EDITOR'`, select keys, then call `with bpy.context.temp_override(window=w, area=area, region=<WINDOW region>):`. Inside that block, `graph.decimate(mode='ERROR', remove_error_margin=…)`, `graph.scale_average(factor=…)` and `graph.butterworth_smooth(…)` all returned FINISHED (a 120-key test went down to 16 keys). The same menu offers `blend_to_ease`, `push_pull`, `time_offset`, `gaussian_smooth`, `breakdown`, `blend_offset`, `shear`, `scale_from_neighbor` and `match_slope`. Caveat: this depends on the default UI screen being loaded (factory startup or a .blend saved with UI), so it is fragile. Use these operators as references and keep our own numpy versions as the main path.
- **`bpy.ops.nla.bake`** runs headless without any override (FINISHED).
- `FCurve.bake(start, end, step, remove)`, `convert_to_samples`, `convert_to_keyframes`, `keyframe_points.deduplicate()` and `handles_recalc()` are all in the 5.2 API.

### Techniques and tools

| Need | How (headless) | Tool options | Blender 5 / slotted actions | Source |
|---|---|---|---|---|
| Keyframe reduction | `graph.decimate` ERROR mode (override above), or our own RDP/"extrema keys" over **all bones together** so key poses line up across bones | Built-in | Yes (tested 5.2) | local test |
| Smoothing mocap jitter | `graph.butterworth_smooth` / `gaussian_smooth`, or scipy filtfilt | Built-in | Yes | local test |
| Exaggeration | Deviation scaling: `x' = s + k·(x − s)`, where `s` is the low-pass-filtered curve and k ≈ 1.2–1.6 (`graph.scale_average` is the per-selection version). For rotations, scale the quaternion log relative to the smoothed rotation, not Euler channels. | Built-in + numpy | Yes | local test |
| Anticipation / overshoot | At each major action key insert a counter-pose `P − α·(P_next − P)` 3–6 frames before, and an overshoot `P + β·(P − P_prev)` 2 frames after, settling by +4…6 frames | Own code | – | craft technique |
| Timing: holds, on twos, retime | Piecewise time-warp from beat markers (compress transitions, extend key poses). Moving holds keep 3–8% drift. Stepped segments: bake every 2nd frame with `interpolation='CONSTANT'` (or a `STEPPED` F-modifier, then bake). | Own code | Yes | – |
| Foot locking | Contact = foot/toe height < h₀ and horizontal speed < v₀ for ≥2 frames. Lock target = mean contact position. Two-bone IK solved in numpy per frame, or IK constraint + empties then `nla.bake` (headless verified). Blend 2 frames in/out. | Own code. Paid add-ons for reference: Mocap Bone Lock, Foot Lock, GH Animation Pro, WK PowerPin (Superhive) **[prices unverified]**. | Bake path works | https://superhivemarket.com/products/mocap-bone-lock/ |
| Retarget Mixamo / Y Bot / SMPL-H → hero | Name map + rest-pose delta (world-space rotation transfer), feet via the lock pass. Uthana direct can retarget server-side onto our character. | **"Retarget" extension** (KBS-DEV, GPL-3, v5.2.0, Blender **5.0+**, presets for Mixamo/Unreal/VRoid/MMD/Daz/ARP, includes AnimAide curve tools, 26.8k downloads). Rokoko Studio Live (LGPL-3, free retarget, "2.80+"; Blender 5 status **[unverified]**). Auto-Rig Pro Full with Remap ($50, supports up to 5.1 per a third party **[unverified]**). Expy Kit (original; superseded by the Retarget fork). | Retarget: built for 5.x and slot-aware | https://extensions.blender.org/add-ons/retarget/, https://github.com/Rokoko/rokoko-studio-live-blender |
| Loop closure | Match first/last pose (cross-fade the last N frames toward the first), cyclic F-modifier, bake | Own code | Yes | – |
| Squash & stretch / smears | Keys on dedicated bones (root "squash" bone, spine, head, limb Y-scale 1.2–1.5 for 1–2 fast frames). Volume preserve: x = z = 1/√y. | Own code | Yes | https://www.blendernation.com/2021/03/12/avoiding-squash-and-stretch-issues-in-game-rigs/ |
| Not usable | AnimAide original (unmaintained, ≤ 3.6; its tools now live in the Retarget fork and in built-in graph ops). Cascadeur AI inbetweening/AutoPhysics: strong pose-to-pose tool with a Python API and MCP server (2026.2), but it is a **GUI app that would take focus** on Tuna's Mac. | – | – | https://www.cgchannel.com/?p=177897 |

---

## 5. How Supercell-style animation is made, and what we can measure

**What public sources say (thin):**
- No public GDC talk, ArtStation breakdown or interview with frame data for Squad Busters, Brawl Stars or Clash in-game animation turned up. GDC Vault has Supercell talks on rendering, design and IP, none on animation. LightBox Expo's "Animation at Supercell" covers process, not numbers. ArtStation pages (e.g. "Brawl Stars – Animation – Belle Star Power", Sylvain Couillet) block automated reads (403) **[could not read]**.
- Brawl Stars character pillars (Fernanda Oliveira, Games First Helsinki): humour, nostalgia, diversity, **iconic simplicity**, Saturday-morning-cartoon "timeless tribute". Chuck Jones's "simplest form possible" and **silent-film physical comedy** drive how the animation shows a mechanic. https://www.pocketgamer.biz/5-creative-pillars-for-designing-brawlers-in-supercells-brawl-stars/
- Supercell animator job ads (Clash of Clans): Maya, "appealing in-game animations", in-engine implementation, working with and adjusting existing rigs. https://hitmarker.net/jobs/supercell-senior-gameplay-animator-clash-of-clans-847366
- **Squad Busters is being shut down.** Announced 2025-10-30; last update Dec 2025; servers close "between mid and the end of 2026", not before June. It may already be offline on 2026-10-04 **[unverified]**. Capture reference from the live game now if it is up, otherwise from 60 fps YouTube gameplay. https://supercell.com/en/news/squad-busters-faq/

**Measurable traits to check frame by frame.** The numbers below are general craft norms for stylised gameplay animation at 30 fps, **not Supercell data**. Step one of the sub-project is to measure the real values from the 3 reference moves and replace these.

| Trait | How to measure (reference video and our Unity render, same camera) | Craft-norm starting range |
|---|---|---|
| Clip and cycle length | Frames between repeated poses | Run cycle 10–16 frames (0.33–0.53 s). Idle loop 1.5–3 s. Attack 0.4–0.8 s. Victory 1.5–3 s. |
| Key poses and holds | Frames where mean joint/silhouette speed < threshold (moving holds) | Stylised: 20–40% of frames in holds or near-holds. Raw mocap: ~0–5%. |
| Anticipation : action : recovery | Phase boundaries from speed peaks | Roughly 25–35% : 5–15% (2–4 frames) : 50–65% |
| Snappiness | Peak speed ÷ mean speed per limb | Stylised clips are much spikier than mocap. Target ≥ 2× the raw AI clip. |
| Overshoot and settle | % past the final pose; frames to settle | 5–15% overshoot, settles in 3–6 frames |
| Squash & stretch | Silhouette bounding-box height ÷ rest height (and width) | Landing 0.8–0.9 height, take-off 1.1–1.2, volume roughly kept |
| Run bounce and lean | Root height amplitude ÷ character height; torso pitch | Bob 5–10% of height, lean 10–20° |
| Readability | Arm/prop pixels outside the torso box at key poses; pose readable in ≤ 3 frames | Arms off the body at every key pose |
| Secondary overlap | Head, ears, hair lag vs. root (frames) | 1–3 frames of lag |

Tooling we already have: `Tools/frame_sheet.py` (timed contact sheets), `ai_anim_contact_sheet` (per-frame bone heights) and `side_video.py` (resampled side by side). A cheap extra: run Uthana video-to-motion ($0.05/s) on the 3 Squad Busters clips. That would give approximate joint curves for the reference, if pose estimation copes with chibi proportions **[untested]**.

---

## 6. Unity 6 secondary motion

| Option | Cost / licence | What it gives | Mobile notes | Source |
|---|---|---|---|---|
| **Animation Rigging** `com.unity.animation.rigging` 1.3.1 (1.4.0-pre.1 available) | Free (Unity package) | Animation C# jobs constraints: Damped Transform (lagging chains), Multi-Aim (head look-at), Two Bone IK (foot planting), Override/Multi-Rotation, Twist Correction, Chain IK | Fine for a few heroes; costs grow per rig. **With Humanoid, TransformStreamHandle position/scale changes are ignored ("As Designed").** | https://docs.unity3d.com/6/Documentation/Manual/com.unity.animation.rigging.html, https://issuetracker.unity.com/issues/20734/... |
| **JigglePhysics** (naelstrof) | **MIT**, UPM git | Jobs-based Verlet jiggle **with built-in squash-and-stretch**, colliders, 0–1 authoring params ("authoring rather than physically accurate") | "600 jiggling armadillos in fractions of a ms" (author's claim) | https://github.com/naelstrof/JigglePhysics |
| UnityChanSpringBone (`com.unity.springbone`) | MIT + others | Lightweight spring bones | Maintenance status **[unverified]** | https://github.com/unity3d-jp/UnityChanSpringBone |
| UniVRM SpringBone | MIT | VRM spring bones (FastSpringBone jobs) | Tied to VRM components. Release page showed v0.131.3 **[verify; looks old]**. | https://github.com/vrm-c/UniVRM/releases |
| **Magica Cloth 2** | **$35** (on sale at $17.50 on 2026-10-04), v2.18.3 (2026-09-03), Asset Store EULA | BoneCloth/BoneSpring + MeshCloth, Burst, colliders | BoneCloth is fine on mobile. MeshCloth is CPU-heavy on Android. | https://assetstore.unity.com/packages/tools/physics/magica-cloth-2-242307 |
| **Procedural squash & stretch** (our own `Project.Juice` component) | Ours | Volume-preserving scale on a transform *above* the skinned rig (root wrapper), driven by vertical velocity, landing impulses and attack events | Almost free. Works with both Humanoid and Generic because it scales the whole model. | – |
| Look-at | `Animator.SetLookAtPosition` (Humanoid IK pass) or Multi-Aim | Head/eyes track a target | Cheap | – |

**Humanoid vs Generic (decision for the pipeline).** Mecanim Humanoid "only supports rotation animation". Bone scale curves are dropped and runtime scaling has to be redone in LateUpdate (https://unity.com/blog/engine-platform/mecanim-humanoids, https://discussions.unity.com/t/humanoid-bone-scaling-customization/765733). Our clips will be retargeted in Blender onto the hero's own skeleton, so **import hero clips as Generic**. That keeps squash/stretch and smear bone scales and exact hand poses (no muscle-space approximation). Keep Humanoid only for borrowing library clips quickly. Unity notes that scale curves cost more than rotation/translation, so keep scale keys to a few bones. Set Animator culling to Cull Completely (https://docs.unity3d.com/6000.2/Documentation/Manual/MecanimPeformanceandOptimization.html). This touches `ai_character_setup` (currently Humanoid) and backlog U1 (import settings by `kind`).

---

## Recommendation

### Pipeline to build

1. **Rig (copy, paid call):**
   - **Primary:** Uthana `create-character-v1` on Replicate. It is proven here, costs about a cent, uses Mixamo names and includes fingers.
   - **Challengers:** run the same hero through **Tripo** (`spec: mixamo`, free rig-check first) and **Meshy via fal** (no subscription needed).
   - **How to judge:** score each rig in Blender, not by eye on thumbnails:
     - joint positions vs. mesh landmarks (neck at head base, shoulder, elbow, knee);
     - weight bleed (head vertices influenced by clavicle/arm bones);
     - stress-pose renders (arms up and crossed, deep crouch, head turn);
     - Uthana's `auto_rig_confidence` when available.
   - **Fallback (free, fully ours):** fit a fixed hero template skeleton (Mixamo names plus `squash`/`head_scale` helper bones), then `ARMATURE_AUTO` bone-heat weights on the closed mesh. Big heads are a documented weak spot of AI riggers, so this fallback is likely to matter.
2. **Motion (copy, paid call):**
   - **Body motion:** Uthana. Iterate with v2 on Replicate (cheap; prompt the body mechanics), then run final candidates through **v3 on the direct API**, retargeted server-side to our character at 30 fps.
   - **Run cycles:** Uthana **locomotion** (loopable), with Kevin Iglesias and Quaternius CC0 clips as the free control.
   - **One wildcard experiment:** AI video (Kling, image-to-video from the hero render) → Uthana video-to-motion, the only route that can carry cartoon timing in from outside.
   - **Third generator:** HY-Motion on fal, only as a quality comparison. Its licence is a risk for shipping.
3. **Blender (new `anim_stylize` tool in the Blender workshop, headless numpy plus verified bpy calls). Order:**
   - retarget to the hero;
   - contact detection and foot lock;
   - extrema-based key reduction;
   - beat-marker time-warp (holds, fast transitions);
   - anticipation and overshoot insertion;
   - deviation exaggeration (k per body region);
   - squash/stretch and smear keys on helper bones;
   - on-twos on impact segments;
   - loop closure;
   - in-place/root extraction;
   - export FBX + `manifest.json` (`kind: animation`, clips, fps, metrics).
   
   Every parameter goes in a per-clip recipe so Tuna's notes become numbers.
4. **Unity:**
   - Import as **Generic** (update `ai_character_setup` and `ai_import_asset`'s per-kind settings, backlog U1/U4).
   - Short Animator transitions (≤ 0.1 s) to stay snappy.
   - JigglePhysics on head, ears, hair and props.
   - A procedural `SquashStretch` juice component (jump, land, hit) next to the existing CameraShake and TimeScaleController.
   - Multi-Aim or Humanoid look-at only where needed.
5. **Measurement:** capture the 3 Squad Busters moves (suggestion: run start → run, a basic attack, the victory/celebration). Render our hero from the same 3/4 top-down camera. Compare with `frame_sheet.py` and `side_video.py`, plus the trait table in §5 computed by a small script for both sides.

### Budget to prove it on 1 hero × 8 clips (idle, run, shoot, jump, victory, defeat, hit, celebrate)

| Phase | Calls | Estimate |
|---|---|---|
| Rig bake-off | Uthana/Replicate ×2 (~$0.02), Tripo rig ×2 ($0.50), Meshy via fal ×2 ($0.40), optional UniRig ×1 ($0.46) | **$0.9–1.4** |
| Motion bake-off | Uthana v2 32 runs (~$0.35); Uthana v3 8 clips × 2 takes × ~5 s × $0.10 ($8.00) + direct auto-rig ($0.15); Uthana locomotion for run (~$0.10); HY-Motion 16 runs ($1.28); Tripo presets 6 × $0.10 ($0.60); Meshy library via fal 1 rig + 8 clips ($1.16) | **~$11.6** |
| Reference + wildcard | Uthana video-to-motion on 3 Squad Busters clips (~12 s, $0.60); 3 Kling 5 s videos ($1.05) + video-to-motion 15 s ($0.75) | **~$2.4** |
| Re-runs after the first Blender pass (~50%) | – | **~$7** |
| **Total** | | **≈ $22–23**; suggested ledger cap (`--tavan`) **$30** |
| Optional | Meshy text-to-motion needs the direct API, which needs Pro ($20/mo, includes 1,000 credits) | +$20 |

Uthana's direct API is prepaid pay-as-you-go. The minimum top-up is **[unverified]**.

### Main risks

1. **The style gap is the core risk.** Every generator is trained on realistic mocap. Snappiness, holds and exaggeration must come from our Blender pass, and algorithmic exaggeration can break contacts, joint limits and silhouettes. Gate each step with the §5 metrics and side-by-side sheets, and keep the free CC0 control clip in every comparison.
2. **Big-head proportions.** Uthana's own docs list oversized body parts as an auto-rig failure case. Retargeted mocap on chibi proportions makes hands pass through the head and torso. We need a per-clip additive arm-offset layer and collision checks (Cascadeur's "collision penetration cleaning" shows the need, but we write our own).
3. **Reference availability.** Squad Busters servers close in H2 2026. Capture now, or fall back to YouTube footage. Internal comparison only: don't copy or train on it.
4. **Unity Humanoid drops scale.** Squash/stretch and smears die on Humanoid. Switching hero clips to Generic changes `ai_character_setup` and costs Humanoid retargeting convenience.
5. **Licences and vendors.**
   - Uthana's Replicate-hosted models have no stated licence.
   - Uthana stopped new subscriptions (Aug 2026).
   - HY-Motion: Tencent community licence with likely territory limits, and the SMPL-H non-commercial body model.
   - Academic models are trained on AMASS (research-only).
   - Mixamo: no ML training, no raw redistribution, unmaintained.
   - Meshy free tier is CC BY.
   - Get written confirmation from Uthana for the Replicate route, or ship from the direct API (commercial use stated).
6. **Headless fragility.** Graph Editor operators work headless only through a screen-area override. Keep numpy implementations as the main path and the bpy operators as cross-checks in tests.
7. **No GUI tools.** Cascadeur, AccuRIG (Windows-only) and Blender with a GUI would take focus on Tuna's Mac. The pipeline must stay headless.
8. **Unverified pieces.** Finger rigs on Tripo and Meshy, Meshy's USD credit price on the direct API, DeepMotion/Kinetix/Move AI API prices, Anything World credits per rig, Auto-Rig Pro's Blender 5.2 support, and Rokoko add-on support for Blender 5.
