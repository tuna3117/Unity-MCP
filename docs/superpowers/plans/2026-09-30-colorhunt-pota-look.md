# Color Hunt — Pota's Current Puzzle Look, 1:1 Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Unity Color Hunt board (standalone `ColorHunt` scene and inside `PotaFlow`) looks, moves and sounds like the live Pota
puzzle: pillow blocks in candy tones with room reflections, Lilita One numbers bottom-right and big item stickers, the board filling the
giant hoop, swell-then-burst explosions with Pota's layers, Pota's salvo, drops, rotation, HUD style and sounds — verified side by side
with Pota at matched moments.

**Architecture:** Every Pota number and curve becomes a named constant or pure function in `PotaLook` (tested). Textures Pota draws in
code (label atlas, effect sprites, ball skin, sky) are exported from a **scratch copy** of Pota by running Pota's own drawing code in a
browser; files Pota ships (item/special/obstacle sprites, backboard, arrow, sounds) are copied. The Unity side swaps the block mesh,
material and labels, rewrites `ColorHuntFx` and `BallSalvo` to Pota's layers on ParticleSystems, retimes `ColorHuntGame`'s queue to
Pota's swell-at-peak model, restyles the HUD, and rebuilds the board surroundings (giant hoop at Pota's size, room reflection probe,
Pota camera fit, lights, post). Rules (`ColorHuntRules`) do not change.

**Tech Stack:** Unity 6000.6.2f1, URP 17.6 (Forward+, Render Graph), hand-written URP HLSL shaders, ParticleSystem (Shuriken), uGUI,
Unity Test Framework; three.js/Vite/Playwright from the Pota copy (`~/projects/copy/node_modules/playwright`), Node 22
(`~/.nvm/versions/node/v22.23.2/bin/node`), ffmpeg (`/opt/homebrew/bin/ffmpeg`), Python + Pillow via `uv`.

**Spec:** `docs/superpowers/specs/2026-09-30-colorhunt-pota-look-design.md` (approved by Tuna 2026-09-30: "onay").

## Global Constraints

- `~/projects/Pota` is **read-only**. Everything runs from `$S/pota-copy` (`$S` = `/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/ddddd890-0b80-4935-84fd-734de99f5121/scratchpad`), which may be modified (the export step adds `export` keywords and a page there). Another Pota session is live (its vite on :5174): never run anything inside `~/projects/Pota`, and use port 5199.
- Rules unchanged: `ColorHuntRules` untouched; existing Color Hunt, Basket and PotaFlow tests stay green (update only tests that pin old looks, with a ruling).
- Scope A only: normal blocks. Special balls, obstacles, boosters, gold ball, transit rings, tutorial highlight and dunk are out.
- Axes: Pota's board front is +z with the camera at +z; Unity's is −z with the camera at −z. Map Pota `(x, y, z)` → Unity `(x, y, −z)`.
- Every Pota value carries its source (`file.ts` + name) in a comment next to it in `PotaLook`/`ColorHuntSettings`.
- Unity 6 / URP only: HLSL + URP ShaderLibrary + `UniversalForward` + `CBUFFER_START(UnityPerMaterial)`; Input System only; `FindAnyObjectByType`.
- Never hand-edit `.unity/.prefab/.asset/.mat` YAML; overwrite an existing texture/audio file's content in place (keeps its `.meta`/GUID), never `mv`/`rm` assets.
- No compile in Play Mode; tests through `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [fixtures]`.
- Texts stay Turkish (CEPHANE, HEDEF); Pota's Turkish UI is used for the comparison (Playwright locale `tr-TR`).
- No paid calls; commits in English ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; no push.
- Deliverables: `docs/screenshots/2026-09-30-potalook-*`.

## Review Focus

1. **Numbers above 99 and big boards:** a block with hp ≥ 100 must show "99" (Pota `rakamHucresi`), never an empty or wrong atlas cell; 5×5/6×6 boards scale the grid by `(8.8 − 0.4)/n` and still fit the frame while rotating. Pinned by `LabelAtlasTests.Numbers_ClampTo1To99` and `PotaLookTests.GridScale_And_RotationFit`.
2. **A block removed before its swell peak** (chain step ends, drops start, rotation, new round) must still fire its burst, item flight and target count exactly once (Pota `gorselSil` → `tepeyiTetikle`). Pinned by `SwellSetTests.PeakFires_Once_EvenIfTheStepEndsFirst` (and `NewBoard_ForgetsPendingBursts`).
3. **Hit-stop inside hit-stop:** a chain step's 0.06 dip arriving during the tap's 0.12 dip keeps the smaller scale and the longer end (Pota `zamanDusur`), and time always returns to 1. Pinned by `TimeScaleControllerTests.Dip_KeepsTheSmallerScale_AndTheLaterEnd`.
4. **Target counter vs rules:** the HUD target count drops once per item arrival, never below the rules' remaining, and snaps to the rules when the board settles with no item in flight (also after a new round mid-flight). Pinned by `ColorHuntHudPlayTests.TargetCount_DropsPerArrival_ThenMatchesTheRules` and `NewRound_WhileAStickerFlies_KeepsTheNewCounts`.
5. **Aspect change / PotaFlow hand-off:** the Pota camera fit (38°, frame fills 98.5 % of width, HUD bands) stays exact on aspect change and is what the PotaFlow flight lands on (no jump). Pinned by `ColorHuntStagingTests.PotaFit_*` and the existing `PotaFlowPlayTests.AspectChange_MidFlight_StillLandsOnTheFit` (now at 38°).

## How to run things

- Unity open with the bridge up (`Tools/unity_up.sh`). Compile + test: `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Project.Tests.Fixture ...]`.
- MCP: `~/.local/bin/uv run Tools/mcp_call.py call <tool> '<json>'`; custom tools via `execute_custom_tool`.
- Recording: `ai_colorhunt_record` (Task 11), `ai_potaflow_record`; sheets `Tools/frame_sheet.py`.
- Work on `main`, commit per task, never push.

## File map

| File | Responsibility | Task |
|---|---|---|
| `Tools/potalook/{run_export.sh, export.mjs, export-page.html, export-page.ts}` | Run Pota's own drawing/sound code in the scratch copy, copy its files into Unity | 1 |
| `Games/Pota/Art/PotaLook/*`, `Games/Pota/Art/esya-*.png` (content replaced), `Games/Pota/Audio/PotaLook/*`, `Games/Pota/Art/Fonts/LilitaOne-*` | Pota's assets | 1 |
| `Games/Pota/Editor/PotaLookImportTool.cs` (`ai_potalook_import`), `Editor/PotaRoomBaker.cs`, `Shaders/PotaRoom.shader` | Import settings, settings wiring, post profile, room reflection bake | 1, 5 |
| `Games/Pota/Scripts/ColorHunt/PotaLook.cs` | Pota's numbers and pure curves (layout, camera fit, swell, drops, colours, flight, salvo) | 2 |
| `Core/Scripts/Juice/{CameraShake,TimeScaleController}.cs` | Additive linear shake; `Dip(scale, seconds)` | 3 |
| `Scripts/ColorHunt/{PillowBlockMesh,LabelAtlas,BlockView}.cs`, `Shaders/PotaLabel.shader` | Pillow block, atlas labels, candy tones | 4 |
| `Scripts/Flow/{GiantHoop,CourtBuilder,PuzzleStage}.cs`, `Scripts/ColorHunt/{ColorHuntCamera,BoardBuilder}.cs`, `Editor/{SceneParts,ColorHuntSceneTool}.cs` | Hoop at DEV_POTA, court, custom reflection probe, Pota camera fit, Color Hunt scene | 5 |
| `Scripts/ColorHunt/ColorHuntHud.cs` | Pota HUD, sticker flight, shown target counts | 6 |
| `Scripts/ColorHunt/{PotaFx,ColorHuntFx}.cs`, `Shaders/PotaAdditive.shader` | Burst layers on particle systems | 7 |
| `Scripts/ColorHunt/{BallSalvo,PotaSounds,ColorHuntJuice}.cs` | Salvo + trail, Pota's sounds, feel | 8 |
| `Scripts/ColorHunt/{SwellSet,ColorHuntGame}.cs` | Swell-then-burst queue, rotation fit, win dispersal | 9 |
| `Scripts/Flow/{PuzzleLighting,PotaFlowDirector,PotaFlowSettings}.cs`, `Editor/PotaFlowSceneTool.cs` | PotaFlow lands on the puzzle look | 10 |
| `Editor/ColorHuntRecordTool.cs` (`ai_colorhunt_record`), `Tools/potalook/{record-pota.mjs, compare.py, test_compare.py}` | Recordings and Pota \| Unity sheets/video | 11 |
| `Games/Pota/README.md`, `CLAUDE.md`, backlog, memory | Docs | 12 |

New tests: `PotaLookAssetsTests`, `PotaLookTests`, `CameraShakeTests`, `PillowBlockMeshTests`, `LabelAtlasTests`, `BlockViewTests`,
`PotaRoomBakerTests`, `PotaFxCurveTests`, `PotaSoundsTests`, `ColorHuntJuiceTests`, `SwellSetTests`, `PuzzleLightingTests`,
`ColorHuntRecordToolTests` (EditMode); `ColorHuntScenePlayTests`, `PotaFxPlayTests`, `PotaLookPlayTests` (PlayMode); updated:
`GiantHoopTests`, `BallStreamTests`, `ColorHuntStagingTests`, `BoardRevealTests`, `TimeScaleControllerTests`, `ColorHuntHudPlayTests`,
`ColorHuntStagingPlayTests`, `PotaFlowPlayTests`.

(`Games/…`, `Core/…` are under `Assets/_Project/`.)

---

## Deviations from the spec (decided while planning; each is a ruling for the ledger)

1. **Synth sounds are rendered, not re-synthesised.** Spec §0.3 says `kombo`/`carpma` move into Core `SynthSfx` with their parameters.
   The export page instead runs Pota's own `ton`/`gurultu` code in an `OfflineAudioContext` (with Pota's room send 0.09 and master 0.9)
   and writes WAVs; the mp3 samples go through the same chain. Why: it is the exact sound (Web Audio biquad and exponential ramps are
   not re-implemented). Cost if wrong: small — a `SynthSfx` port can replace the WAVs later.
2. **Room reflections come from a Unity replica baked in the Editor, not exported cube faces.** Spec §3 lists "oda yansıması
   (RoomEnvironment küp yüzleri)" as an export. A PNG export of three.js' PMREM would clip the room's emissive panels (intensity 17–100)
   at 1.0. The plan rebuilds the same room (three `RoomEnvironment.js`: box, 6 boxes, 6 panels, point light 900 / range 28 / decay 2)
   in the Editor, lights it with three's formula in a small unlit shader, and bakes it once with Unity's reflection probe bake into an
   HDR cubemap (`PotaRoom.exr`, specular convolution). At runtime a custom reflection probe with that cubemap sits on the board. Realtime
   probes were rejected: they are off in both quality levels (`realtimeReflectionProbes: 0`) and cost per frame on phones. Cost if
   wrong: highlights differ; tuned side by side in Task 11.
3. **Labels are per-block instanced quads.** Spec §4 says "tek instanced quad katmanı". Unity draws the per-block label quads with
   GPU instancing (one material, per-instance `_UvRect`/`_Color`), which is the same draw economy without a custom instanced layer.
4. **Post-processing was never on.** Found while planning: no Pota scene camera has `renderPostProcessing` set (no
   `UniversalAdditionalCameraData` in the scene files), so every Global Volume so far did nothing. The Color Hunt camera turns post on
   with Pota's profile; in PotaFlow post is turned on and only the puzzle volume carries overrides (weight 0 in the basket phase), so
   the approved basket look does not change. CLAUDE.md §12 gets the lesson.
5. **Where the numbers live.** Spec §3 puts the numbers in `ColorHuntSettings`. Feel values Tuna may tune (timings, swell peaks,
   shake, dips, particle counts, light strength) are `ColorHuntSettings` fields whose defaults are Pota's values with their sources;
   the look's geometry (DEV_POTA sizes, Z_ON, label placement, CSS layout) are constants in `PotaLook` — changing those breaks the 1:1
   rather than tuning it. Cost if wrong: a later "make it tunable" moves a constant into the asset.

---

### Task 1: Export pipeline, asset import tool, settings fields

**Files:**
- Create: `Tools/potalook/run_export.sh`, `Tools/potalook/export-page.html`, `Tools/potalook/export-page.ts`, `Tools/potalook/export.mjs`
- Create: `Assets/_Project/Games/Pota/Editor/PotaLookImportTool.cs`
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs` (new "Pota look assets" fields)
- Modify: `Assets/_Project/Games/Pota/Tests/EditMode/Pota.Tests.EditMode.asmdef` (+ URP/Core runtime references)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/PotaLookAssetsTests.cs`

**Interfaces:**
- Produces (fields on `ColorHuntSettings`): `Texture2D LabelAtlas, StarSprite, TrailSprite, SoftDisc, SoftRing, Backboard`;
  `Material SalvoBall`; `VolumeProfile PuzzleProfile`; `AudioClip SfxBreak, SfxSalvo, SfxDrop, SfxRotate, SfxMinus, SfxWin, SfxLose`;
  `AudioClip[] SfxCombo` (11, index = chain depth, Pota `MERDIVEN`), `AudioClip[] SfxHit` (3), `AudioClip SfxSelect`. `Font` now points at Lilita One.
- Produces: `PotaLookImportTool.RoomLayer = "PotaRoom"` (a named layer), `PotaLookImportTool.Import()` (idempotent).
- Produces files: `Games/Pota/Art/PotaLook/{label-atlas,star,trail,soft-disc,soft-ring,ball-skin,pano-a}.png`, `PotaBall.mat`;
  `Games/Pota/Audio/PotaLook/{blok-pop,salvo,dustu,dondur,eksi,kazandi,kaybetti,dokun}.wav`, `kombo-0..10.wav`, `carpma-0..2.wav`;
  `Games/Pota/Settings/PotaPuzzleProfile.asset`; `Games/Pota/Art/Fonts/LilitaOne-Regular.ttf` (+ `LilitaOne-OFL.txt`).

- [ ] **Step 1: Write the failing test**

`Assets/_Project/Games/Pota/Tests/EditMode/PotaLookAssetsTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Tests
{
    /// <summary>The Pota look assets are in the project, imported the way Pota uses them, and wired into the settings asset.</summary>
    public class PotaLookAssetsTests
    {
        private const string Look = "Assets/_Project/Games/Pota/Art/PotaLook/";
        private const string Hint = "run Tools/potalook/run_export.sh <scratch>, then execute_custom_tool ai_potalook_import";

        private static ColorHuntSettings Settings() =>
            AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");

        [Test]
        public void LabelAtlas_IsPotasAtlas_Mipmapped_Uncompressed()
        {
            var imp = AssetImporter.GetAtPath(Look + "label-atlas.png") as TextureImporter;
            Assert.IsNotNull(imp, Hint);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Look + "label-atlas.png");
            Assert.AreEqual(2048, tex.width);
            Assert.AreEqual(2048, tex.height);
            Assert.IsTrue(imp.mipmapEnabled, "Pota: generateMipmaps, LinearMipmapLinear");
            Assert.AreEqual(FilterMode.Trilinear, imp.filterMode);
            Assert.AreEqual(4, imp.anisoLevel);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression);
        }

        [Test]
        public void LabelAtlas_HasInkInTheNumberAndItemCells()
        {
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(Look + "label-atlas.png")), Hint);
            try
            {
                Assert.Greater(OpaqueFraction(tex, 6), 0.05f, "cell 6 = number 7");
                Assert.Greater(OpaqueFraction(tex, 98), 0.05f, "cell 98 = number 99");
                for (int i = 0; i < 5; i++) Assert.Greater(OpaqueFraction(tex, 100 + i), 0.2f, $"item cell {100 + i}");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        /// <summary>Share of pixels with alpha &gt; 0.5 in atlas cell c (Pota: x = c mod 11 · 186, y from the TOP).</summary>
        private static float OpaqueFraction(Texture2D tex, int c)
        {
            int x0 = c % 11 * 186, yTop = c / 11 * 186, n = 0;
            for (int y = 0; y < 186; y++)
                for (int x = 0; x < 186; x++)
                    if (tex.GetPixel(x0 + x, 2047 - (yTop + y)).a > 0.5f) n++;
            return n / (186f * 186f);
        }

        [Test]
        public void ItemStickers_AreThe256Set()
        {
            foreach (var name in new[] { "forma", "ayakkabi", "sapka", "corap", "bileklik" })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Project/Games/Pota/Art/esya-{name}.png");
                Assert.IsNotNull(tex, name);
                Assert.AreEqual(256, tex.width, $"{name}: the old 09-23 set was 217 wide");
                Assert.AreEqual(256, tex.height, name);
            }
        }

        [Test]
        public void Settings_PointAtThePotaLookAssets()
        {
            var s = Settings();
            Assert.IsNotNull(s.LabelAtlas, Hint);
            Assert.IsNotNull(s.StarSprite);
            Assert.IsNotNull(s.TrailSprite);
            Assert.IsNotNull(s.SoftDisc);
            Assert.IsNotNull(s.SoftRing);
            Assert.IsNotNull(s.Backboard);
            Assert.IsNotNull(s.SalvoBall);
            Assert.AreEqual("ball-skin", s.SalvoBall.GetTexture("_BaseMap").name);
            Assert.IsNotNull(s.PuzzleProfile);
            Assert.AreEqual("LilitaOne-Regular", s.Font.name);
            foreach (var c in new[] { s.SfxBreak, s.SfxSalvo, s.SfxDrop, s.SfxRotate, s.SfxMinus, s.SfxWin, s.SfxLose, s.SfxSelect }) Assert.IsNotNull(c);
            Assert.AreEqual(11, s.SfxCombo.Length, "ses.ts MERDIVEN has 11 steps");
            Assert.AreEqual(3, s.SfxHit.Length);
            foreach (var c in s.SfxCombo) Assert.IsNotNull(c);
            foreach (var c in s.SfxHit) Assert.IsNotNull(c);
        }

        [Test]
        public void PuzzleProfile_HasPotasPost()
        {
            var p = Settings().PuzzleProfile;
            Assert.IsNotNull(p, Hint);
            Assert.IsTrue(p.TryGet(out Tonemapping tm));
            Assert.AreEqual(TonemappingMode.Neutral, tm.mode.value);
            Assert.IsTrue(p.TryGet(out ColorAdjustments ca));
            Assert.AreEqual(Mathf.Log(1.12f, 2f), ca.postExposure.value, 1e-3f, "efekt.ts toneMappingExposure 1.12");
            Assert.IsTrue(p.TryGet(out Bloom bloom));
            Assert.AreEqual(Mathf.Pow(0.86f, 1f / 2.2f), bloom.threshold.value, 1e-3f, "linear 0.86; URP's threshold is gamma");
            Assert.IsTrue(p.TryGet(out Vignette v));
            Assert.AreEqual(0.32f, v.intensity.value, 1e-4f);
            Assert.AreEqual(0.55f, v.smoothness.value, 1e-4f);
        }

        [Test]
        public void RoomLayer_Exists() => Assert.GreaterOrEqual(LayerMask.NameToLayer("PotaRoom"), 8, Hint);
    }
}
```

In `Pota.Tests.EditMode.asmdef` add `"Unity.RenderPipelines.Universal.Runtime"` and `"Unity.RenderPipelines.Core.Runtime"` to `references`.

- [ ] **Step 2: Run it to see it fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookAssetsTests`
Expected: exit 2, `error CS1061: 'ColorHuntSettings' does not contain a definition for 'LabelAtlas'`.

- [ ] **Step 3: Add the settings fields**

In `ColorHuntSettings.cs`, after the `DimAmount` field (end of the `[Header("Block")]` group), add:

```csharp
        [Header("Pota look assets (Tools/potalook/run_export.sh + ai_potalook_import)")]
        [Tooltip("yapbozSahne.ts EtiketAtlasi: 2048², 11 columns × 186 px; numbers 1–99 = cells 0–98, star 99, items 100–104.")]
        public Texture2D LabelAtlas;
        [Tooltip("patlamaEfekt.ts yildizKivilcim(128): four-point star + soft core.")]
        public Texture2D StarSprite;
        [Tooltip("patlamaEfekt.ts izDokusu(64): soft across, fading toward the tail.")]
        public Texture2D TrailSprite;
        [Tooltip("patlamaEfekt.ts yumusakDaire(128): the flash sprite.")]
        public Texture2D SoftDisc;
        [Tooltip("patlamaEfekt.ts yumusakDaire(128, 0.62, 0.98): the ring sprite.")]
        public Texture2D SoftRing;
        [Tooltip("public/assets/pano-a.png: the giant hoop's glass backboard.")]
        public Texture2D Backboard;
        [Tooltip("Salvo ball: efekt.ts topKureDokusu() skin, yapbozSahne.ts roughness 0.55, emissive #2a1200.")]
        public Material SalvoBall;
        [Tooltip("efekt.ts SonIslem: Neutral ×1.12, bloom 0.28 / 0.45 / 0.86, vignette 0.32 / 0.55.")]
        public UnityEngine.Rendering.VolumeProfile PuzzleProfile;
        [Tooltip("ses.ts samples (kirildi, kuleAt, dustu, dondur, eksi, kazandi, kaybetti) rendered through Pota's effect chain.")]
        public AudioClip SfxBreak, SfxSalvo, SfxDrop, SfxRotate, SfxMinus, SfxWin, SfxLose;
        [Tooltip("ses.ts kombo: MERDIVEN[0..10], index = chain depth.")]
        public AudioClip[] SfxCombo;
        [Tooltip("ses.ts carpma (guc 0.8) at r = 0.92, 1.00, 1.08.")]
        public AudioClip[] SfxHit;
        [Tooltip("ses.ts dokun: a block gets selected under the finger.")]
        public AudioClip SfxSelect;
```

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookAssetsTests`
Expected: compiles; 7 tests FAIL (assets missing, `Hint` in the messages).

- [ ] **Step 4: Write the export page and scripts**

`Tools/potalook/export-page.html`:

```html
<!doctype html>
<!-- Copied into the Pota scratch copy's deneme/ by run_export.sh; served by the copy's vite. -->
<html>
<head><meta charset="utf-8"><link rel="stylesheet" href="/fonts/fontlar.css"></head>
<body style="font-family: 'Lilita One'">potalook<script type="module" src="./potalook-export.ts"></script></body>
</html>
```

`Tools/potalook/export-page.ts`:

```ts
// Runs Pota's own drawing and sound code (from the SCRATCH COPY; run_export.sh adds `export` to the private helpers there)
// and hands the results to export.mjs as base64 through window.__disa. Never runs inside ~/projects/Pota.
import { EtiketAtlasi } from "../src/yapbozSahne";
import { izDokusu, yildizKivilcim, yumusakDaire } from "../src/patlamaEfekt";
import { topKureDokusu } from "../src/efekt";

type Disa = Record<string, string>;
declare global { interface Window { __disa?: Promise<Disa> } }

const RATE = 48000;
const png = (c: unknown): string => (c as HTMLCanvasElement).toDataURL("image/png").split(",")[1]!;
const resim = (yol: string) => new Promise<HTMLImageElement>((ok, hata) => {
  const i = new Image(); i.onload = () => ok(i); i.onerror = () => hata(new Error(yol)); i.src = yol;
});

async function dokular(): Promise<Disa> {
  await document.fonts.load('400 48px "Lilita One"');
  if (!document.fonts.check('400 48px "Lilita One"')) throw new Error("Lilita One did not load");
  const atlas = new EtiketAtlasi();
  atlas.rakamlariCiz();   // again, now that the font is surely in (the constructor may have drawn with the fallback)
  // renkAviBolum.ts ESYALAR order; yapbozSahne.ts esyalariGiydir "cikartma", ozelleriGiydir / engelleriGiydir "payli".
  for (const [i, ad] of ["forma", "ayakkabi", "sapka", "corap", "bileklik"].entries())
    atlas.resimCiz(100 + i, await resim(`/assets/esya-${ad}.png`), "cikartma");
  for (const [i, ad] of ["cizgi", "bomba", "altin"].entries()) atlas.resimCiz(105 + i, await resim(`/assets/ozel-${ad}-a.png`), "payli");
  for (const [i, ad] of ["file", "buz", "hakem"].entries()) atlas.resimCiz(108 + i, await resim(`/assets/engel-${ad}.png`), "payli");
  return {
    "label-atlas.png": png(atlas.doku.image),
    "star.png": png(yildizKivilcim().image),
    "trail.png": png(izDokusu().image),
    "soft-disc.png": png(yumusakDaire().image),
    "soft-ring.png": png(yumusakDaire(128, 0.62, 0.98).image),
    "ball-skin.png": png(topKureDokusu().image),
  };
}

/** Seeded random (Pota uses Math.random for the noise buffer and the room impulse; a seed keeps exports reproducible). */
function rastgele(tohum: number): () => number {
  let s = tohum >>> 0;
  return () => ((s = (Math.imul(s, 1664525) + 1013904223) >>> 0) / 4294967296);
}

/** ses.ts SesMotoru's graph, offline: efektG (1) → ana (0.9) → out, plus efektG → send 0.09 → 0.28 s room convolver → ana. */
class Cevrimdisi {
  readonly c: OfflineAudioContext;
  private readonly efektG: GainNode;
  private readonly gurultuTampon: AudioBuffer;
  constructor(saniye: number, tohum: number) {
    const r = rastgele(tohum);
    this.c = new OfflineAudioContext(2, Math.ceil(RATE * saniye), RATE);
    const ana = this.c.createGain(); ana.gain.value = 0.9; ana.connect(this.c.destination);
    this.efektG = this.c.createGain(); this.efektG.connect(ana);
    const n = Math.floor(RATE * 0.28), imp = this.c.createBuffer(2, n, RATE);
    for (let k = 0; k < 2; k++) { const d = imp.getChannelData(k); for (let i = 0; i < n; i++) d[i] = (r() * 2 - 1) * Math.pow(1 - i / n, 2.6); }
    const oda = this.c.createConvolver(); oda.buffer = imp;
    const gonder = this.c.createGain(); gonder.gain.value = 0.09;
    this.efektG.connect(gonder).connect(oda).connect(ana);
    this.gurultuTampon = this.c.createBuffer(1, RATE, RATE);
    const d = this.gurultuTampon.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = r() * 2 - 1;
  }
  // ses.ts `ton`, t0 = gecikme (the context starts at 0).
  ton(f0: number, sure: number, tip: OscillatorType, kazanc: number, f1?: number, gecikme = 0, atak = 0.005): void {
    const c = this.c, t0 = gecikme;
    const o = c.createOscillator(); o.type = tip; o.frequency.setValueAtTime(f0, t0);
    if (f1) o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t0 + sure);
    const g = c.createGain();
    g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(kazanc, t0 + atak); g.gain.exponentialRampToValueAtTime(0.0001, t0 + sure);
    o.connect(g).connect(this.efektG); o.start(t0); o.stop(t0 + sure + 0.02);
  }
  // ses.ts `gurultu`.
  gurultu(sure: number, tip: BiquadFilterType, q0: number, kazanc: number, q1?: number, gecikme = 0, Q = 1): void {
    const c = this.c, t0 = gecikme;
    const s = c.createBufferSource(); s.buffer = this.gurultuTampon; s.loop = true;
    const f = c.createBiquadFilter(); f.type = tip; f.Q.value = Q; f.frequency.setValueAtTime(q0, t0);
    if (q1) f.frequency.exponentialRampToValueAtTime(q1, t0 + sure);
    const g = c.createGain();
    g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(kazanc, t0 + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t0 + sure);
    s.connect(f).connect(g).connect(this.efektG); s.start(t0); s.stop(t0 + sure + 0.02);
  }
  // ses.ts `ornekCal` without the random rate (Unity jitters the pitch 0.97–1.03 at play time).
  ornek(b: AudioBuffer): void {
    const s = this.c.createBufferSource(); s.buffer = b; s.connect(this.efektG); s.start(0);
  }
}

/** 16-bit PCM WAV, base64. */
function wav(b: AudioBuffer): string {
  const kanal = b.numberOfChannels, n = b.length, veri = n * kanal * 2;
  const v = new DataView(new ArrayBuffer(44 + veri));
  const yaz = (o: number, s: string) => { for (let i = 0; i < s.length; i++) v.setUint8(o + i, s.charCodeAt(i)); };
  yaz(0, "RIFF"); v.setUint32(4, 36 + veri, true); yaz(8, "WAVE"); yaz(12, "fmt "); v.setUint32(16, 16, true);
  v.setUint16(20, 1, true); v.setUint16(22, kanal, true); v.setUint32(24, b.sampleRate, true);
  v.setUint32(28, b.sampleRate * kanal * 2, true); v.setUint16(32, kanal * 2, true); v.setUint16(34, 16, true);
  yaz(36, "data"); v.setUint32(40, veri, true);
  const kanallar = Array.from({ length: kanal }, (_, k) => b.getChannelData(k));
  let o = 44;
  for (let i = 0; i < n; i++) for (let k = 0; k < kanal; k++) {
    const x = Math.max(-1, Math.min(1, kanallar[k]![i]!)); v.setInt16(o, x < 0 ? x * 0x8000 : x * 0x7fff, true); o += 2;
  }
  let s = ""; const u8 = new Uint8Array(v.buffer);
  for (let i = 0; i < u8.length; i += 0x8000) s += String.fromCharCode(...u8.subarray(i, i + 0x8000));
  return btoa(s);
}

async function sesler(): Promise<Disa> {
  const out: Disa = {};
  const MERDIVEN = [523, 587, 659, 784, 880, 1047, 1175, 1319, 1568, 1760, 2093];   // ses.ts SesMotoru.MERDIVEN
  for (const [d, f] of MERDIVEN.entries()) {
    const m = new Cevrimdisi(0.6, 100 + d);
    m.ton(f, 0.12, "triangle", 0.18, f * 1.25); m.ton(f * 2, 0.18, "sine", 0.1, undefined, 0.05);   // ses.ts case "kombo"
    out[`kombo-${d}.wav`] = wav(await m.c.startRendering());
  }
  for (const [i, r] of [0.92, 1.0, 1.08].entries()) {
    const m = new Cevrimdisi(0.5, 200 + i), k = Math.min(1, 0.35 + 0.8 * 0.15);                   // ses.ts case "carpma", guc 0.8
    m.gurultu(0.09, "bandpass", 900 * r, 0.3 * k, 500, 0, 3); m.ton(230 * r, 0.11, "sine", 0.2 * k, 95);
    out[`carpma-${i}.wav`] = wav(await m.c.startRendering());
  }
  {
    const m = new Cevrimdisi(0.4, 250);
    m.ton(660, 0.07, "sine", 0.18, 990);                                                            // ses.ts case "dokun" (selection)
    out["dokun.wav"] = wav(await m.c.startRendering());
  }
  const cozucu = new OfflineAudioContext(2, RATE, RATE);
  for (const ad of ["blok-pop", "salvo", "dustu", "dondur", "eksi", "kazandi", "kaybetti"]) {
    const b = await cozucu.decodeAudioData(await (await fetch(`/assets/ses/${ad}.mp3`)).arrayBuffer());
    const m = new Cevrimdisi(b.duration + 0.35, 300);
    m.ornek(b);
    out[`${ad}.wav`] = wav(await m.c.startRendering());
  }
  return out;
}

window.__disa = (async () => ({ ...(await dokular()), ...(await sesler()) }))();
```

`Tools/potalook/export.mjs`:

```js
// Opens the export page (served by the Pota scratch copy's vite) in headless Chromium and writes window.__disa
// (file name → base64) into <out dir>. Usage: node export.mjs <url> <out dir>
import { chromium } from "/Users/tunaaltin17/projects/copy/node_modules/playwright/index.mjs";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const [url, out] = process.argv.slice(2);
if (!url || !out) throw new Error("usage: node export.mjs <url> <out dir>");
mkdirSync(out, { recursive: true });
const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  page.on("pageerror", (e) => console.error("[page]", e.message));
  page.on("console", (m) => { if (m.type() === "error") console.error("[console]", m.text()); });
  let open = false;
  for (let i = 0; i < 60 && !open; i++) {       // vite may still be starting
    try { await page.goto(url, { waitUntil: "load", timeout: 5000 }); open = true; }
    catch { await new Promise((r) => setTimeout(r, 500)); }
  }
  if (!open) throw new Error(`page did not open: ${url}`);
  await page.waitForFunction(() => window.__disa !== undefined, null, { timeout: 30000 });
  const files = await page.evaluate(() => window.__disa);
  for (const [name, b64] of Object.entries(files)) {
    const bytes = Buffer.from(b64, "base64");
    writeFileSync(join(out, name), bytes);
    console.log(`${name}\t${bytes.length}`);
  }
} finally {
  await browser.close();
}
```

`Tools/potalook/run_export.sh`:

```bash
#!/bin/zsh
# Pota's current puzzle look → Unity. ~/projects/Pota is READ-ONLY: everything runs in <scratch>/pota-copy.
# Usage: Tools/potalook/run_export.sh <scratch dir>     then: execute_custom_tool ai_potalook_import
set -euo pipefail
SRC="$HOME/projects/Pota"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRATCH="${1:?usage: run_export.sh <scratch dir>}"
COPY="$SCRATCH/pota-copy"
OUT="$SCRATCH/potalook-out"
NODE="$HOME/.nvm/versions/node/v22.23.2/bin/node"
PORT=5199                                   # another Pota session serves on :5174
ART="$ROOT/Assets/_Project/Games/Pota/Art"
LOOK="$ART/PotaLook"
AUDIO="$ROOT/Assets/_Project/Games/Pota/Audio/PotaLook"

rsync -a --delete --exclude .git --exclude dist "$SRC/" "$COPY/"
# The drawing helpers are module-private in Pota; export them in the COPY only.
sed -i '' -e 's/^class EtiketAtlasi /export class EtiketAtlasi /' "$COPY/src/yapbozSahne.ts"
sed -i '' -e 's/^function yildizKivilcim(/export function yildizKivilcim(/' \
          -e 's/^function izDokusu(/export function izDokusu(/' \
          -e 's/^function yumusakDaire(/export function yumusakDaire(/' "$COPY/src/patlamaEfekt.ts"
grep -q '^export class EtiketAtlasi ' "$COPY/src/yapbozSahne.ts"
[ "$(grep -c '^export function \(yildizKivilcim\|izDokusu\|yumusakDaire\)(' "$COPY/src/patlamaEfekt.ts")" = 3 ]
mkdir -p "$COPY/deneme"
cp "$ROOT/Tools/potalook/export-page.html" "$COPY/deneme/potalook-export.html"
cp "$ROOT/Tools/potalook/export-page.ts" "$COPY/deneme/potalook-export.ts"

rm -rf "$OUT"; mkdir -p "$OUT"
( cd "$COPY" && exec "$NODE" node_modules/.bin/vite --port $PORT --strictPort ) > "$SCRATCH/potalook-vite.log" 2>&1 &
VITE=$!
trap 'kill $VITE 2>/dev/null || true' EXIT
"$NODE" "$ROOT/Tools/potalook/export.mjs" "http://localhost:$PORT/deneme/potalook-export.html" "$OUT"

mkdir -p "$LOOK" "$AUDIO" "$ART/Fonts"
cp "$OUT"/{label-atlas,star,trail,soft-disc,soft-ring,ball-skin}.png "$LOOK/"
cp "$OUT"/*.wav "$AUDIO/"
# Files Pota ships: the item stickers replace the old set IN PLACE (same names → same .meta/GUID), backboard, arrow.
for n in forma ayakkabi sapka corap bileklik; do cp "$COPY/public/assets/esya-$n.png" "$ART/esya-$n.png"; done
cp "$COPY/public/assets/ok-a.png" "$ART/ok-a.png"
cp "$COPY/public/assets/pano-a.png" "$LOOK/pano-a.png"
# Lilita One (SIL OFL 1.1): Pota ships only woff2 subsets, Unity needs the TTF.
[ -f "$ART/Fonts/LilitaOne-Regular.ttf" ] || curl -fsSL -o "$ART/Fonts/LilitaOne-Regular.ttf" https://github.com/google/fonts/raw/main/ofl/lilitaone/LilitaOne-Regular.ttf
[ -f "$ART/Fonts/LilitaOne-OFL.txt" ] || curl -fsSL -o "$ART/Fonts/LilitaOne-OFL.txt" https://github.com/google/fonts/raw/main/ofl/lilitaone/OFL.txt
echo "textures: $(ls "$LOOK"/*.png | wc -l | tr -d ' ')  sounds: $(ls "$AUDIO"/*.wav | wc -l | tr -d ' ')"
```

- [ ] **Step 5: Run the export**

Run: `chmod +x Tools/potalook/run_export.sh && Tools/potalook/run_export.sh "$S"` (`$S` = scratchpad).
Expected: 28 `name<TAB>bytes` lines from export.mjs (6 PNG, 22 WAV), no `[page]` errors, last line `textures: 7  sounds: 22`;
`git -C ~/projects/Pota status --porcelain` unchanged from before the run.

- [ ] **Step 6: Look at the atlas**

Read `Assets/_Project/Games/Pota/Art/PotaLook/label-atlas.png` (image). Expected: white Lilita One numbers 1–99 with a navy outline
and a soft shadow in the first nine rows, the star at cell 99, the five new item stickers in cells 100–104, specials/obstacles after
them. If the numbers are in a thin fallback font, the font did not load: stop and fix the page (never ship a fallback atlas).

- [ ] **Step 7: Write the import tool**

`Assets/_Project/Games/Pota/Editor/PotaLookImportTool.cs`:

```csharp
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Imports what Tools/potalook/run_export.sh brought from the Pota copy: import settings (Pota's filtering), the salvo ball
    /// material, the puzzle post profile, the "PotaRoom" layer (room reflection replica), and wires it all into ColorHuntSettings.
    /// Idempotent: run it again after a new export.
    /// </summary>
    [McpForUnityTool("ai_potalook_import", Description =
        "Import the Pota look export (Art/PotaLook, Audio/PotaLook, item stickers, Lilita One): texture/audio import settings, " +
        "PotaBall.mat, Settings/PotaPuzzleProfile.asset (Neutral ×1.12, bloom, vignette), the PotaRoom layer, ColorHuntSettings wiring.")]
    public static class PotaLookImportTool
    {
        public const string LookFolder = "Assets/_Project/Games/Pota/Art/PotaLook";
        public const string AudioFolder = "Assets/_Project/Games/Pota/Audio/PotaLook";
        public const string FontPath = "Assets/_Project/Games/Pota/Art/Fonts/LilitaOne-Regular.ttf";
        public const string ProfilePath = "Assets/_Project/Games/Pota/Settings/PotaPuzzleProfile.asset";
        public const string BallMaterialPath = LookFolder + "/PotaBall.mat";
        public const string RoomLayer = "PotaRoom";

        public class Parameters { }

        public static object HandleCommand(JObject p)
        {
            var report = Import();
            string msg = string.Join(" | ", report);
            AIToolsCommon.Log(msg);
            return new SuccessResponse("Pota look imported.", new { steps = report });
        }

        [MenuItem("AI Tools/Color Hunt/Import Pota Look")]
        public static void ImportMenu() => AIToolsCommon.Log(string.Join(" | ", Import()));

        public static List<string> Import()
        {
            var report = new List<string>();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Texture(LookFolder + "/label-atlas.png", mip: true, trilinear: true, aniso: 4, maxSize: 2048, uncompressed: true);
            foreach (var n in new[] { "star", "soft-disc", "soft-ring", "trail" })
                Texture($"{LookFolder}/{n}.png", mip: true, trilinear: false, aniso: 1, maxSize: 128, uncompressed: false);
            Texture(LookFolder + "/ball-skin.png", mip: true, trilinear: true, aniso: 4, maxSize: 512, uncompressed: false, repeatU: true);
            Texture(LookFolder + "/pano-a.png", mip: true, trilinear: true, aniso: 4, maxSize: 1024, uncompressed: false);
            foreach (var n in ColorHuntSceneTool.ItemNames)
                Texture($"Assets/_Project/Games/Pota/Art/esya-{n}.png", mip: true, trilinear: true, aniso: 4, maxSize: 256, uncompressed: false);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                var imp = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var st = imp.defaultSampleSettings;
                st.loadType = AudioClipLoadType.DecompressOnLoad;
                st.compressionFormat = AudioCompressionFormat.PCM;
                imp.defaultSampleSettings = st;
                imp.SaveAndReimport();
            }
            report.Add($"layer {RoomLayer} = {EnsureLayer(RoomLayer)}");

            var s = ColorHuntSceneTool.EnsureSettings(report);
            s.LabelAtlas = Load<Texture2D>(LookFolder + "/label-atlas.png");
            s.StarSprite = Load<Texture2D>(LookFolder + "/star.png");
            s.TrailSprite = Load<Texture2D>(LookFolder + "/trail.png");
            s.SoftDisc = Load<Texture2D>(LookFolder + "/soft-disc.png");
            s.SoftRing = Load<Texture2D>(LookFolder + "/soft-ring.png");
            s.Backboard = Load<Texture2D>(LookFolder + "/pano-a.png");
            s.Font = Load<Font>(FontPath);
            s.SalvoBall = BallMaterial(Load<Texture2D>(LookFolder + "/ball-skin.png"));
            s.PuzzleProfile = Profile();
            s.SfxBreak = Clip("blok-pop"); s.SfxSalvo = Clip("salvo"); s.SfxDrop = Clip("dustu"); s.SfxRotate = Clip("dondur");
            s.SfxMinus = Clip("eksi"); s.SfxWin = Clip("kazandi"); s.SfxLose = Clip("kaybetti");
            s.SfxCombo = new AudioClip[11];
            for (int d = 0; d < 11; d++) s.SfxCombo[d] = Clip($"kombo-{d}");
            s.SfxHit = new[] { Clip("carpma-0"), Clip("carpma-1"), Clip("carpma-2") };
            s.SfxSelect = Clip("dokun");
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            report.Add($"settings: atlas={s.LabelAtlas != null} font={(s.Font != null ? s.Font.name : "-")} profile={s.PuzzleProfile != null} " +
                       $"combo={System.Array.FindAll(s.SfxCombo, c => c != null).Length}/11 hit={System.Array.FindAll(s.SfxHit, c => c != null).Length}/3");
            return report;
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        private static AudioClip Clip(string name) => Load<AudioClip>($"{AudioFolder}/{name}.wav");

        private static void Texture(string path, bool mip, bool trilinear, int aniso, int maxSize, bool uncompressed, bool repeatU = false)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogWarning($"[ai_potalook_import] missing {path}"); return; }
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = mip;
            imp.filterMode = trilinear ? FilterMode.Trilinear : FilterMode.Bilinear;
            imp.anisoLevel = aniso;
            imp.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.maxTextureSize = maxSize;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        private static Material BallMaterial(Texture2D skin)
        {
            var m = Load<Material>(BallMaterialPath);
            bool create = m == null;
            if (create) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PotaBall" };
            m.SetTexture("_BaseMap", skin);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 1f - 0.55f);                 // MeshStandard roughness 0.55
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", new Color32(0x2a, 0x12, 0x00, 0xff));   // emissive #2a1200 (sRGB, like three)
            if (create) AssetDatabase.CreateAsset(m, BallMaterialPath);    // fully set up before it is written (CLAUDE.md lesson)
            else EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>efekt.ts SonIslem → URP: Neutral tonemapping, exposure log2(1.12), bloom, vignette (tuned side by side in Task 11).</summary>
        private static VolumeProfile Profile()
        {
            var p = Load<VolumeProfile>(ProfilePath);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, ProfilePath);
            }
            var tm = Get<Tonemapping>(p);
            tm.mode.Override(TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>(p);
            ca.postExposure.Override(Mathf.Log(1.12f, 2f));
            var bloom = Get<Bloom>(p);
            bloom.threshold.Override(Mathf.Pow(0.86f, 1f / 2.2f));      // UnrealBloom threshold 0.86 is linear; URP's is gamma
            bloom.intensity.Override(0.28f);                              // UnrealBloom strength 0.28 (different kernel: tuned by eye)
            bloom.scatter.Override(0.45f);                                // UnrealBloom radius 0.45
            bloom.downscale.Override(BloomDownscaleMode.Half);            // half resolution like Pota
            var v = Get<Vignette>(p);
            v.intensity.Override(0.32f);
            v.smoothness.Override(0.55f);
            EditorUtility.SetDirty(p);
            return p;
        }

        private static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (p.TryGet(out T c)) return c;
            c = p.Add<T>(false);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        /// <summary>Names a free user layer (ProjectSettings/TagManager.asset) unless it exists. Returns its index.</summary>
        public static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new System.InvalidOperationException($"no free layer for {name}");
        }
    }
}
```

- [ ] **Step 8: Compile, import, test**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookAssetsTests` (compiles the tool; tests still fail),
then `~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_potalook_import","parameters":{}}'`
Expected: `layer PotaRoom = <8..31> | … | settings: atlas=True font=LilitaOne-Regular profile=True combo=11/11 hit=3/3`.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookAssetsTests --no-compile`
Expected: 7/7 PASS.

- [ ] **Step 9: Commit**

```bash
git add Tools/potalook Assets/_Project/Games/Pota/Art Assets/_Project/Games/Pota/Audio Assets/_Project/Games/Pota/Settings \
  Assets/_Project/Games/Pota/Editor/PotaLookImportTool.cs* Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs \
  Assets/_Project/Games/Pota/Tests/EditMode ProjectSettings/TagManager.asset
git commit -m "feat: Pota look export — atlas, effect sprites, ball skin, sounds from a Pota copy; ai_potalook_import

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `PotaLook` — Pota's numbers and pure curves

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaLook.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/PotaLookTests.cs`

**Interfaces:**
- Produces (static class `Project.ColorHunt.PotaLook`): constants `PanoSize 8.8, FrameWidth 0.35, FrameOuter 9.5, GridPad 0.4, RimRadius 1.6,
  RimY -5.6, NetDepth 1.8, Block 0.9, FrontZ 0.45, Fov 38, WidthFill 0.985, BallPad 0.6, Edge 0.22, SwellPeakAt 0.55, ChainSwell 1.24,
  TapSwell 1.18, TapSwellTime 0.11, ChainTime 0.15, DropTimePerCell 0.25, RotateTime 0.42, RotateUpright 0.7, FlashDecay 4.5,
  SquashDecay 6, LandSquash 0.12, NeighbourSquash 0.5, LabelZ 0.58, ItemScale 0.72/0.88, NumberScale 0.44, ItemOffsetY 0.06,
  NumberOffset (0.26, −0.28), LabelQuad 0.92, PhoneCssWidth 390, HudCounterCss 63, HudTopPadCss 14, HudGapCss 10, ButtonBandCss 160,
  ButtonGapCss 34`; colours `Tones[5]` (SEKER_TONU), `Confetti[5]` (RENKLER), `ShortGrey #8792a6`, `NumberShort #ff7a72`, `ItemShort #7a8494`.
- Functions: `float GridScale(int n)`, `float RotationFit(int n, float angleRad)`, `Fit CameraFit(float aspect, float topCss, float bottomCss, float fov = 38)`
  (`Fit.Distance`, `Fit.TargetY`), `Vector3 CameraPosition(Vector3 anchor, Fit f)`, `float HudTopCss(float safeTopCss)`,
  `float Swell(float o, float peak)`, `float Smoothstep(float u)`, `float LabelCounterAngle(float boardAngle, float u)`,
  `float DropTravel(float t, float perCell = 0.25)`, `float StepDropTime(float cells, float perCell = 0.25)`,
  `Color BlockColor(Color tone, float flash, bool shortOfAmmo)` (linear); colours `Sky #8ec9f2`, `Fog #9ed3f5`, `FogStart 30`, `FogEnd 130`;
  `ShiverAmount 0.075`.

- [ ] **Step 1: Write the failing test**

`Assets/_Project/Games/Pota/Tests/EditMode/PotaLookTests.cs`:

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class PotaLookTests
    {
        [Test]
        public void GridScale_And_RotationFit()
        {
            Assert.AreEqual(1.2f, PotaLook.GridScale(7), 1e-5f, "sahaYerlesim.ts: 7×7 1.2");
            Assert.AreEqual(1.4f, PotaLook.GridScale(6), 1e-5f);
            Assert.AreEqual(1.68f, PotaLook.GridScale(5), 1e-5f);
            Assert.AreEqual(1f, PotaLook.RotationFit(7, 0f), 1e-6f);
            Assert.AreEqual(1f, PotaLook.RotationFit(7, Mathf.PI / 2f), 1e-5f);
            Assert.AreEqual(9.5f / (8.4f * Mathf.Sqrt(2f)), PotaLook.RotationFit(7, Mathf.PI / 4f), 1e-5f, "≈ 0.8 at 45°");
            Assert.AreEqual(PotaLook.RotationFit(7, Mathf.PI / 4f), PotaLook.RotationFit(5, Mathf.PI / 4f), 1e-5f, "grid is 8.4 wide at every size");
        }

        [Test]
        public void CameraFit_Portrait_IsWidthBound_AndCentresTheBandOnTheBoard()
        {
            var f = PotaLook.CameraFit(9f / 16f, 87f, 160f);
            float tan = Mathf.Tan(19f * Mathf.Deg2Rad);
            Assert.AreEqual(9.5f / (2f * tan * 0.5625f * 0.985f), f.Distance, 1e-3f, "panoUzakligi(9.5, 9/16, 38°, 0.985) ≈ 24.897");
            float h = 390f / 0.5625f;                                        // 693.3 CSS px tall
            float pxPerUnit = 390f / (2f * f.Distance * tan * 0.5625f);
            float expected = -0.3f + ((87f + h - 160f) * 0.5f - h * 0.5f) / pxPerUnit;
            Assert.AreEqual(expected, f.TargetY, 1e-4f);
            Assert.AreEqual(-1.2026f, f.TargetY, 2e-3f);
        }

        [Test]
        public void CameraFit_Landscape_IsBandBound()
        {
            var f = PotaLook.CameraFit(16f / 9f, 87f, 160f);
            float tan = Mathf.Tan(19f * Mathf.Deg2Rad), h = 390f / (16f / 9f);
            float band = Mathf.Max(120f, h - 247f);
            Assert.AreEqual(10.1f * h / (band * 2f * tan), f.Distance, 1e-3f, "uzakBoy wins on a short screen");
        }

        [Test]
        public void CameraPosition_LooksAtTheFrontPlane()
        {
            var f = new PotaLook.Fit { Distance = 20f, TargetY = -1f };
            var p = PotaLook.CameraPosition(new Vector3(0f, 8.5f, 30f), f);
            Assert.AreEqual(new Vector3(0f, 7.5f, 30f - 0.45f - 20f), p);
        }

        [Test]
        public void HudTop_UsesTheSafeAreaWhenLarger()
        {
            Assert.AreEqual(87f, PotaLook.HudTopCss(0f), 1e-4f);
            Assert.AreEqual(47f + 73f, PotaLook.HudTopCss(47f), 1e-4f, "iPhone notch: ≈ Pota's 118 fallback");
        }

        [Test]
        public void Swell_RisesLinearlyToThePeak_ThenCollapses()
        {
            Assert.AreEqual(1f, PotaLook.Swell(0f, 1.24f), 1e-6f);
            Assert.AreEqual(1.24f, PotaLook.Swell(0.55f, 1.24f), 1e-5f);
            Assert.AreEqual(1.12f, PotaLook.Swell(0.275f, 1.24f), 1e-5f);
            Assert.AreEqual(1.24f * Mathf.Pow(0.5f, 1.5f), PotaLook.Swell(0.775f, 1.24f), 1e-5f);
            Assert.AreEqual(0f, PotaLook.Swell(1f, 1.24f), 1e-6f);
            Assert.AreEqual(0.0605f, PotaLook.SwellPeakAt * PotaLook.TapSwellTime, 1e-6f, "tap peak at 60.5 ms");
        }

        [Test]
        public void LabelCounterAngle_StaysOnTheBoard_ThenStandsUp()
        {
            Assert.AreEqual(0f, PotaLook.LabelCounterAngle(-45f, 0.5f), 1e-6f, "first 70 %: labels ride the board");
            Assert.AreEqual(0f, PotaLook.LabelCounterAngle(-60f, 0.7f), 1e-6f);
            Assert.AreEqual(90f, PotaLook.LabelCounterAngle(-90f, 1f), 1e-5f, "end: upright on screen");
            Assert.AreEqual(45f * 0.5f, PotaLook.LabelCounterAngle(-45f, 0.85f), 1e-4f, "smoothstep(0.5) = 0.5");
        }

        [Test]
        public void Drops_UseConstantAcceleration()
        {
            Assert.AreEqual(1f, PotaLook.DropTravel(0.25f), 1e-5f, "one cell in 0.25 s");
            Assert.AreEqual(4f, PotaLook.DropTravel(0.5f), 1e-5f);
            Assert.AreEqual(0.25f * 2f + 0.06f, PotaLook.StepDropTime(4f), 1e-5f);
            Assert.AreEqual(0f, PotaLook.StepDropTime(0f));
        }

        [Test]
        public void BlockColor_FlashesInLinear_ThenGreysWhenShort()
        {
            var tone = PotaLook.Tones[0];
            var lin = tone.linear;
            Assert.AreEqual(lin, PotaLook.BlockColor(tone, 0f, false));
            var half = PotaLook.BlockColor(tone, 0.5f, false);
            Assert.AreEqual(Mathf.Lerp(lin.r, 1f, 0.5f) * 1.7f, half.r, 1e-5f, "lerp(tone, white, f)·(1 + 1.4f)");
            var shortC = PotaLook.BlockColor(tone, 0f, true);
            Assert.AreEqual(Mathf.Lerp(lin.g, PotaLook.ShortGrey.linear.g, 0.5f) * 0.62f, shortC.g, 1e-5f, "yapbozSahne.ts ornekYaz: soluk last");
        }
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests`
Expected: exit 2, `error CS0103: The name 'PotaLook' does not exist in the current context`.

- [ ] **Step 3: Write `PotaLook`**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaLook.cs`:

```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's current puzzle (Renk Avı, "balon" look, ~/projects/Pota c742a6a) as numbers and pure curves; every value names its
    /// source file. Units: world = the game root (Pota's pota group, 1 = 1 m; DEV_POTA sizes), cell = one board cell before the
    /// grid scale. Pota's board faces +z, Unity's faces −z (the camera is at −z). Colours are sRGB like Pota's hex values;
    /// <see cref="BlockColor"/> returns linear because three.js mixes them in linear space.
    /// </summary>
    public static class PotaLook
    {
        // sahaYerlesim.ts DEV_POTA, IZGARA_PAYI
        public const float PanoSize = 8.8f;
        public const float FrameWidth = 0.35f;
        public const float FrameOuter = PanoSize + 2f * FrameWidth;
        public const float GridPad = 0.4f;
        public const float RimRadius = 1.6f;
        public const float RimY = -5.6f;
        public const float NetDepth = 1.8f;

        // yapbozSahne.ts
        public const float Block = 0.9f;                // BLOK
        public const float FrontZ = 0.45f;              // Z_ON
        public const float Fov = 38f;                   // GORUS_ACISI
        public const float WidthFill = 0.985f;          // DOLGU
        public const float BallPad = 0.6f;              // TOP_PAYI
        public const float Edge = 0.22f;                // KENAR
        public const float LabelQuad = 0.92f;           // RAKAM_DUZLEM
        public const float LabelZ = FrontZ + 0.03f + 0.1f;   // etiketleriYaz zTaban (front + pillow bulge)
        public const float ItemScale = 0.72f / 0.88f;   // esyaOlcek: 72 % on screen, the atlas sticker fills 88 % of its cell
        public const float NumberScale = 0.44f;
        public const float ItemOffsetY = 0.06f;
        public static readonly Vector2 NumberOffset = new Vector2(0.26f, -0.28f);

        // yastik.ts YASTIK
        public const float PillowRadius = 0.22f;
        public const float PillowBulge = 0.1f;

        // renkAviFaz.ts RENK_AVI_HIS / HIS, ayar2
        public const float SwellPeakAt = 0.55f;         // balon.tepe
        public const float ChainSwell = 1.24f;          // balon.sisme
        public const float TapSwell = 1.18f;            // balon.dokunusSisme
        public const float TapSwellTime = 0.11f;        // balon.dokunusSure
        public const float ChainTime = 0.15f;           // ayar2.sure.zincir
        public const float DropTimePerCell = 0.25f;     // ayar2.sure.dusus
        public const float RotateTime = 0.42f;          // RENK_AVI_HIS.donus
        public const float RotateUpright = 0.7f;        // donusDiklesme
        public const float FlashDecay = 4.5f;           // gorselleriIlerlet
        public const float SquashDecay = 6f;
        public const float LandSquash = 0.12f;          // olcek = 1 − 0.12·carpma
        public const float NeighbourSquash = 0.5f;      // komsuDalga
        public const float ShiverAmount = 0.075f;       // HIS.titrem.genlik (time: ColorHuntSettings.InsufficientShake = titrem.sure)

        // index.html / renkAviFaz.ts boyutla: Pota's CSS is laid out for a 390-px-wide phone.
        public const float PhoneCssWidth = 390f;
        public const float HudCounterCss = 63f;         // .sayac: border 3 + pad 6 + small 13.2 + b 31.5 + pad 6 + border 3
        public const float HudTopPadCss = 14f;          // --ust: max(14px, safe-area-inset-top)
        public const float HudGapCss = 10f;             // boyutla: ust = counter bottom + 10
        public const float ButtonBandCss = 160f;        // dugmePayi
        public const float ButtonGapCss = 34f;          // dugmeBoslugu

        /// <summary>yapbozSahne.ts SEKER_TONU: red, blue, green, yellow, purple (the Unity palette order).</summary>
        public static readonly Color[] Tones = { Hex(0xe8352e), Hex(0x2a82e8), Hex(0x46bf36), Hex(0xf8c22a), Hex(0xa246db) };
        /// <summary>yapbozSahne.ts RENKLER: the confetti colours.</summary>
        public static readonly Color[] Confetti = { Hex(0xe8453c), Hex(0x2f7fd8), Hex(0x3cba6e), Hex(0xffb03a), Hex(0xa864d8) };
        public static readonly Color ShortGrey = Hex(0x8792a6);     // kul
        public static readonly Color NumberShort = Hex(0xff7a72);   // soluk rakam
        public static readonly Color ItemShort = Hex(0x7a8494);     // soluk eşya
        public static readonly Color Sky = Hex(0x8ec9f2);           // saha.ts scene.background
        public static readonly Color Fog = Hex(0x9ed3f5);           // saha.ts fog (30–130)
        public const float FogStart = 30f, FogEnd = 130f;

        /// <summary>three.js lights → URP: three's Lambert divides by π, URP's does not, so direct intensities are ÷π (sun 2.1 → 0.668).</summary>
        public const float ThreeToUnityLight = 1f / Mathf.PI;
        public static readonly Color SunColor = Hex(0xfff4e0);      // saha.ts güneş, 2.1
        public const float SunIntensity = 2.1f;
        /// <summary>saha.ts sun (−24, 26, 24) → (3, 2, −4) in Pota axes; Unity mirrors z.</summary>
        public static readonly Vector3 SunDirection = new Vector3(3f + 24f, 2f - 26f, 4f + 24f);
        /// <summary>
        /// Ambient for URP's trilight: saha.ts HemisphereLight #dcefff / #8a7a66 × 0.8 (÷π like every three light) plus the room
        /// environment's diffuse share (environment intensity 0.55 × the room's mean radiance ≈ 0.8, estimated from RoomEnvironment.js'
        /// light; tuned side by side in Task 11).
        /// </summary>
        public const float RoomDiffuse = 0.55f * 0.8f;
        public static Color AmbientSky => Hex(0xdcefff).linear * (0.8f * ThreeToUnityLight) + Grey(RoomDiffuse);
        public static Color AmbientGround => Hex(0x8a7a66).linear * (0.8f * ThreeToUnityLight) + Grey(RoomDiffuse);
        public static Color AmbientEquator => Color.Lerp(AmbientSky, AmbientGround, 0.5f);
        private static Color Grey(float v) => new Color(v, v, v, 0f);

        public static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);

        /// <summary>sahaYerlesim.ts izgaraOlcegiHesapla: the grid fills the backboard (8.4 wide at every size).</summary>
        public static float GridScale(int n) => (PanoSize - GridPad) / Mathf.Max(1, n);

        /// <summary>sahaYerlesim.ts donusSigdirma: a rotating grid shrinks to stay inside the frame (≈ 0.8 at 45°).</summary>
        public static float RotationFit(int n, float angleRad) =>
            Mathf.Min(1f, FrameOuter / (n * GridScale(n) * (Mathf.Abs(Mathf.Cos(angleRad)) + Mathf.Abs(Mathf.Sin(angleRad)))));

        public struct Fit { public float Distance; public float TargetY; }

        /// <summary>
        /// yapbozSahne.ts boyutla (kasasız): the frame's outer width fills <see cref="WidthFill"/> of the screen width, and frame + ball
        /// strip fit the band between the HUD (<paramref name="topCss"/>) and the button band (<paramref name="bottomCss"/>); the farther
        /// distance wins. Distances from the front plane (z = FrontZ); TargetY = where the camera looks (the band's centre on the board).
        /// </summary>
        public static Fit CameraFit(float aspect, float topCss, float bottomCss, float fov = Fov)
        {
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float h = PhoneCssWidth / aspect;
            float band = Mathf.Max(120f, h - topCss - bottomCss);
            float byWidth = FrameOuter / (2f * tan * aspect * WidthFill);            // panoUzakligi
            float byHeight = (FrameOuter + BallPad) * h / (band * 2f * tan);          // takimBoyu = panoBoy + 2·cerceve + TOP_PAYI
            float dist = Mathf.Max(byWidth, byHeight);
            float pxPerUnit = PhoneCssWidth / (2f * dist * tan * aspect);             // olcek
            float bandMid = (topCss + (h - bottomCss)) * 0.5f;                        // bantOrtaPx (from the top)
            return new Fit { Distance = dist, TargetY = -BallPad * 0.5f + (bandMid - h * 0.5f) / pxPerUnit };
        }

        /// <summary>The camera looks straight at (0, TargetY, front plane) from Distance in front of it (Unity: toward −z).</summary>
        public static Vector3 CameraPosition(Vector3 anchor, Fit f) => anchor + new Vector3(0f, f.TargetY, -(FrontZ + f.Distance));

        /// <summary>renkAviFaz.ts boyutla: the counters' bottom + 10 px (Pota's 118 fallback is this on a notched phone).</summary>
        public static float HudTopCss(float safeTopCss) => Mathf.Max(HudTopPadCss, safeTopCss) + HudCounterCss + HudGapCss;

        /// <summary>renkAviFaz.ts patlamaIlerlet: linear to <paramref name="peak"/> at 55 %, then peak·(1 − u)^1.5.</summary>
        public static float Swell(float o, float peak)
        {
            o = Mathf.Clamp01(o);
            return o < SwellPeakAt ? 1f + (peak - 1f) * (o / SwellPeakAt) : peak * Mathf.Pow(1f - (o - SwellPeakAt) / (1f - SwellPeakAt), 1.5f);
        }

        public static float Smoothstep(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        /// <summary>renkAviFaz.ts dönüş: labels ride the board for 70 %, then turn upright (degrees, relative to the board).</summary>
        public static float LabelCounterAngle(float boardAngle, float u) => -boardAngle * Smoothstep((u - RotateUpright) / (1f - RotateUpright));

        /// <summary>renkAviFaz.ts IVME = 2 / dusus²: cells travelled after t seconds.</summary>
        public static float DropTravel(float t, float perCell = DropTimePerCell) => 0.5f * (2f / (perCell * perCell)) * t * t;

        /// <summary>Step length for the longest drop (renkAviFaz.ts dususleriBaslat).</summary>
        public static float StepDropTime(float cells, float perCell = DropTimePerCell) => cells > 0f ? perCell * Mathf.Sqrt(cells) + 0.06f : 0f;

        /// <summary>yapbozSahne.ts ornekYaz, in linear: lerp(tone, white, min(1, f))·(1 + 1.4f); short of ammo: lerp(kul, 0.5)·0.62 last.</summary>
        public static Color BlockColor(Color tone, float flash, bool shortOfAmmo)
        {
            var c = Color.Lerp(tone.linear, Color.white, Mathf.Min(1f, flash)) * (1f + flash * 1.4f);
            if (shortOfAmmo) c = Color.Lerp(c, ShortGrey.linear, 0.5f) * 0.62f;
            c.a = 1f;
            return c;
        }
    }
}
```

- [ ] **Step 4: Run it to see it pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests`
Expected: 9/9 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaLook.cs* Assets/_Project/Games/Pota/Tests/EditMode/PotaLookTests.cs*
git commit -m "feat: PotaLook — Pota puzzle numbers and pure curves (grid fit, camera fit, swell, drops, colours)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Core — additive linear shake and time dips

**Files:**
- Modify: `Assets/_Project/Core/Scripts/Juice/CameraShake.cs`
- Modify: `Assets/_Project/Core/Scripts/Juice/TimeScaleController.cs`
- Test: `Assets/_Project/Core/Tests/EditMode/CameraShakeTests.cs` (new), `Assets/_Project/Core/Tests/PlayMode/TimeScaleControllerTests.cs`

**Interfaces:**
- Produces: `CameraShake.Add(float amount)` (Pota `sars`: additive, cap 0.5, linear decay 2.2/s on game time, offset uniform ±s/2 in x/y),
  `CameraShake.Linear` (current amount), `static float CameraShake.StepLinear(float amount, float dt)`; `Kick` unchanged (basket).
- Produces: `TimeScaleController.Dip(float scale, float seconds)` (Pota `zamanDusur`: while a dip runs the smaller scale wins and the
  later end wins; real-time countdown). `SlowMo` still wins over a dip; `HitStop` unchanged.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Core/Tests/EditMode/CameraShakeTests.cs`:

```csharp
using NUnit.Framework;
using Project.Juice;
using UnityEngine;

namespace Project.Tests
{
    public class CameraShakeTests
    {
        [Test]
        public void Add_Accumulates_UpToHalfAMetre()
        {
            var go = new GameObject("Cam");
            try
            {
                var s = go.AddComponent<CameraShake>();
                s.Add(0.08f);
                s.Add(0.07f);
                Assert.AreEqual(0.15f, s.Linear, 1e-6f, "yapbozSahne.ts sars: additive");
                for (int i = 0; i < 10; i++) s.Add(0.14f);
                Assert.AreEqual(0.5f, s.Linear, 1e-6f, "cap 0.5");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Linear_DecaysAt2Point2PerSecond_ToZero()
        {
            Assert.AreEqual(0.5f - 2.2f * 0.1f, CameraShake.StepLinear(0.5f, 0.1f), 1e-6f);
            Assert.AreEqual(0f, CameraShake.StepLinear(0.05f, 0.1f), 1e-6f);
        }
    }
}
```

Append to `TimeScaleControllerTests`:

```csharp
        [UnityTest]
        public IEnumerator Dip_KeepsTheSmallerScale_AndTheLaterEnd()
        {
            _go = new GameObject("TimeCtl");
            var ctl = _go.AddComponent<TimeScaleController>();
            yield return null;
            ctl.Dip(0.12f, 0.05f);                   // tap: renkAviFaz.ts HIS.hitStop
            Assert.AreEqual(0.12f, Time.timeScale, 1e-4f);
            ctl.Dip(0.06f, 0.2f);                    // chain step arriving during the tap's dip
            Assert.AreEqual(0.06f, Time.timeScale, 1e-4f, "smaller scale wins (zamanDusur)");
            ctl.Dip(0.12f, 0.01f);
            Assert.AreEqual(0.06f, Time.timeScale, 1e-4f, "a weaker, shorter dip changes nothing");
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.3f) yield return null;
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "time always comes back");
            ctl.Dip(0.12f, 0.05f);
            Assert.AreEqual(0.12f, Time.timeScale, 1e-4f, "an expired dip does not keep its old scale");
        }
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CameraShakeTests`
Expected: exit 2, `error CS1061: 'CameraShake' does not contain a definition for 'Add'`.

- [ ] **Step 3: Implement**

`CameraShake.cs` becomes:

```csharp
using UnityEngine;

namespace Project.Juice
{
    /// <summary>
    /// Camera offset from two channels: <see cref="Kick"/> (basket: random sphere, exponential decay, real time) and
    /// <see cref="Add"/> (Pota puzzle `sars`: additive, capped at 0.5, linear 2.2/s on game time so dips freeze it, uniform ±s/2 in x/y).
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public const float LinearCap = 0.5f;
        public const float LinearDecay = 2.2f;

        public float Decay = 9f;
        public Vector3 Offset { get; private set; }
        public float Linear { get; private set; }
        private float _amplitude;

        public void Kick(float amplitude) => _amplitude = Mathf.Max(_amplitude, amplitude);

        public void Add(float amount) => Linear = Mathf.Min(LinearCap, Linear + Mathf.Max(0f, amount));

        public static float StepLinear(float amount, float dt) => Mathf.Max(0f, amount - dt * LinearDecay);

        private void Update()
        {
            var kick = Vector3.zero;
            if (_amplitude < 0.0005f) _amplitude = 0f;
            else
            {
                kick = Random.insideUnitSphere * _amplitude;
                _amplitude *= Mathf.Exp(-Decay * Time.unscaledDeltaTime);
            }
            var lin = Vector3.zero;
            if (Linear > 0f)
            {
                lin = new Vector3((Random.value - 0.5f) * Linear, (Random.value - 0.5f) * Linear, 0f);
                Linear = StepLinear(Linear, Time.deltaTime);
            }
            Offset = kick + lin;
        }
    }
}
```

In `TimeScaleController.cs` add the dip state and method, and let `Apply` use it:

```csharp
        private float _dipUntil = -1f, _dipScale = 1f;

        /// <summary>
        /// Pota renkAviFaz.ts zamanDusur: game time runs at <paramref name="scale"/> for <paramref name="seconds"/> of real time.
        /// While a dip runs, the smaller scale and the later end win. Slow motion still wins over a dip.
        /// </summary>
        public void Dip(float scale, float seconds)
        {
            float now = Time.unscaledTime;
            _dipScale = now < _dipUntil ? Mathf.Min(_dipScale, scale) : scale;
            _dipUntil = Mathf.Max(_dipUntil, now + seconds);
            Apply();
        }
```

In `Clear()` add `_dipUntil = -1f; _dipScale = 1f;`. In `Apply()` replace the scale selection with:

```csharp
            if (now < _slowUntil) scale = _slowScale;
            else if (now < _stopUntil) scale = HitStopScale;
            else if (now < _dipUntil) scale = _dipScale;
```

- [ ] **Step 4: Run them to see them pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CameraShakeTests` → Expected: 2/2 PASS.
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.TimeScaleControllerTests --no-compile` → Expected: 2/2 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Scripts/Juice Assets/_Project/Core/Tests
git commit -m "feat(core): additive linear camera shake and time dips (Pota sars / zamanDusur)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 4: Block look — pillow mesh, atlas labels, candy tones

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/PillowBlockMesh.cs`, `Assets/_Project/Games/Pota/Scripts/ColorHunt/LabelAtlas.cs`
- Create: `Assets/_Project/Games/Pota/Shaders/PotaLabel.shader`
- Rewrite: `Assets/_Project/Games/Pota/Scripts/ColorHunt/BlockView.cs`
- Modify: `ColorHuntGame.cs` (block/label materials, `NewView`, `TickViews`, `Select`, `Tap`, puff position), `BallSalvo.cs:38`,
  `ColorHuntSettings.cs` (palette = Pota tones; remove `BlockSize, BlockDepth, BlockRadius, BlockFace, ItemOffsetY, ItemScale,
  NumberOffsetY, NumberScale, DimAmount`), `Editor/ColorHuntSceneTool.cs` (`EnsureSettings` without `BlockFace`),
  `Editor/PotaLookImportTool.cs` (palette)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/{PillowBlockMeshTests,LabelAtlasTests,BlockViewTests}.cs`

**Interfaces:**
- Consumes: `PotaLook` (Task 2), `ColorHuntSettings.LabelAtlas` (Task 1).
- Produces: `PillowBlockMesh.Create(float size = 0.9, float radius = 0.22, float bulge = 0.1, int segments = 16)`;
  `LabelAtlas.NumberCell(int hp)`, `LabelAtlas.ItemCell(int color)`, `LabelAtlas.UvRect(int cell)` → `Vector4(u, v, w, h)`;
  shader `Project/PotaLabel` (`_BaseMap`, instanced `_UvRect`, `_Color`);
  `BlockView.Init(ColorHuntSettings s, Mesh body, Material bodyMat, Material labelMat)`, `BlockView.Tick(float dt)`,
  fields `Scale` (entrance multiplier), `Flash`, `Squash`, `Shiver`, `Dim`, `Exploding`, `Swell` (−1 = none), `LabelAngle` (degrees);
  statics `BlockView.BodyMaterial()`, `BlockView.LabelMaterial(Texture atlas)`, `BlockView.RingMaterial()`.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Games/Pota/Tests/EditMode/PillowBlockMeshTests.cs`:

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class PillowBlockMeshTests
    {
        [Test]
        public void FrontBulges_ByPotasFormula_BackStaysFlat()
        {
            var box = RoundedBoxMesh.Create(Vector3.one * 0.9f, 0.22f, 16);
            var pillow = PillowBlockMesh.Create();
            var b = box.vertices; var p = pillow.vertices;
            Assert.AreEqual(b.Length, p.Length);
            for (int i = 0; i < b.Length; i++)
            {
                float front = -b[i].z;                                   // Unity's front is −z
                float expected = b[i].z;
                if (front > 0f)
                {
                    float u = b[i].x / 0.45f, w = b[i].y / 0.45f, d = front / 0.45f;
                    expected = -(front + 0.1f * Mathf.Max(0f, 1f - u * u) * Mathf.Max(0f, 1f - w * w) * d);   // yastik.ts
                }
                Assert.AreEqual(expected, p[i].z, 1e-5f, $"vertex {i}");
                Assert.AreEqual(b[i].x, p[i].x, 1e-6f);
                Assert.AreEqual(b[i].y, p[i].y, 1e-6f);
            }
            Object.DestroyImmediate(box);
            Object.DestroyImmediate(pillow);
        }

        [Test]
        public void FrontCentre_Is0Point55_NormalsAreUnit()
        {
            var m = PillowBlockMesh.Create();
            var v = m.vertices; var n = m.normals;
            float minZ = 0f, maxZ = 0f;
            for (int i = 0; i < v.Length; i++)
            {
                minZ = Mathf.Min(minZ, v[i].z); maxZ = Mathf.Max(maxZ, v[i].z);
                Assert.AreEqual(1f, n[i].magnitude, 1e-4f, $"normal {i}");
                if (Mathf.Abs(v[i].x) < 1e-4f && Mathf.Abs(v[i].y) < 1e-4f && v[i].z < 0f)
                    Assert.Less(Vector3.Distance(n[i], Vector3.back), 1e-4f, "front centre faces the camera");
            }
            Assert.AreEqual(-0.55f, minZ, 1e-4f, "0.45 + bulge 0.1");
            Assert.AreEqual(0.45f, maxZ, 1e-4f, "back is not bulged");
            Object.DestroyImmediate(m);
        }
    }
}
```

`Assets/_Project/Games/Pota/Tests/EditMode/LabelAtlasTests.cs`:

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class LabelAtlasTests
    {
        [Test]
        public void Numbers_ClampTo1To99()
        {
            Assert.AreEqual(0, LabelAtlas.NumberCell(1));
            Assert.AreEqual(6, LabelAtlas.NumberCell(7));
            Assert.AreEqual(98, LabelAtlas.NumberCell(99));
            Assert.AreEqual(98, LabelAtlas.NumberCell(150), "yapbozSahne.ts rakamHucresi: above 99 shows 99");
            Assert.AreEqual(0, LabelAtlas.NumberCell(0));
            Assert.AreEqual(0, LabelAtlas.NumberCell(-3));
        }

        [Test]
        public void ItemCells_FollowTheNumbers()
        {
            Assert.AreEqual(100, LabelAtlas.ItemCell(0));
            Assert.AreEqual(104, LabelAtlas.ItemCell(4));
        }

        [Test]
        public void UvRect_CountsRowsFromTheTopOfTheCanvas()
        {
            const float c = 186f / 2048f;
            Assert.AreEqual(new Vector4(0f, 1f - c, c, c), LabelAtlas.UvRect(0));
            var r = LabelAtlas.UvRect(100);                                    // column 1, row 9
            Assert.AreEqual(186f / 2048f, r.x, 1e-6f);
            Assert.AreEqual(1f - (9f * 186f + 186f) / 2048f, r.y, 1e-6f);
            Assert.AreEqual(c, r.z, 1e-6f);
        }
    }
}
```

`Assets/_Project/Games/Pota/Tests/EditMode/BlockViewTests.cs`:

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class BlockViewTests
    {
        private GameObject _go;
        private ColorHuntSettings _s;

        [SetUp]
        public void SetUp()
        {
            _s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            _go = new GameObject("Block");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_s);
        }

        private BlockView Make(Block b)
        {
            var v = _go.AddComponent<BlockView>();
            v.Init(_s, PillowBlockMesh.Create(), BlockView.BodyMaterial(), BlockView.LabelMaterial(null));
            v.SetBlock(b, 0);
            v.Tick(0f);
            return v;
        }

        private static Vector4 Prop(Transform t, string name)
        {
            var mpb = new MaterialPropertyBlock();
            t.GetComponent<Renderer>().GetPropertyBlock(mpb);
            return mpb.GetVector(name);
        }

        [Test]
        public void Number_SitsBottomRight_InFrontOfTheBulge_AndKeepsItsOffsetWhileSwelling()
        {
            var v = Make(new Block(0, 12));
            var number = _go.transform.Find("Number");
            Assert.AreEqual(0.26f, number.position.x, 1e-4f);
            Assert.AreEqual(-0.28f, number.position.y, 1e-4f);
            Assert.AreEqual(-0.59f, number.position.z, 1e-4f, "Z_ON + 0.03 + bulge 0.1, number 0.01 in front of the item");
            Assert.AreEqual(0.92f * 0.44f, number.lossyScale.x, 1e-4f);
            v.Swell = 1.24f;
            v.Tick(0f);
            Assert.AreEqual(0.26f, number.position.x, 1e-4f, "Pota does not push the corner offset out with the swell");
            Assert.AreEqual(-0.58f * 1.24f - 0.01f, number.position.z, 1e-4f);
            Assert.AreEqual(0.92f * 0.44f * 1.24f, number.lossyScale.x, 1e-4f, "labels grow with the block");
        }

        [Test]
        public void Item_IsBigAndCentredAbove()
        {
            Make(new Block(3, 5));
            var item = _go.transform.Find("Item");
            Assert.AreEqual(0f, item.position.x, 1e-4f);
            Assert.AreEqual(0.06f, item.position.y, 1e-4f);
            Assert.AreEqual(0.92f * 0.72f / 0.88f, item.lossyScale.x, 1e-4f);
            Assert.AreEqual(LabelAtlas.UvRect(103), Prop(item, "_UvRect"));
        }

        [Test]
        public void Number_AboveNinetyNine_ShowsNinetyNine()
        {
            Make(new Block(1, 150));
            Assert.AreEqual(LabelAtlas.UvRect(98), Prop(_go.transform.Find("Number"), "_UvRect"));
        }

        [Test]
        public void LabelAngle_TurnsTheCornerOffset()
        {
            var v = Make(new Block(0, 7));
            v.LabelAngle = 90f;
            v.Tick(0f);
            var number = _go.transform.Find("Number");
            Assert.AreEqual(0.28f, number.position.x, 1e-4f);
            Assert.AreEqual(0.26f, number.position.y, 1e-4f);
        }

        [Test]
        public void ShortOfAmmo_GreysTheBody_AndReddensTheNumber()
        {
            var v = Make(new Block(0, 7));
            v.Dim = true;
            v.Tick(0f);
            Assert.AreEqual((Vector4)PotaLook.BlockColor(PotaLook.Tones[0], 0f, true), Prop(_go.transform, "_BaseColor"));
            Assert.AreEqual((Vector4)PotaLook.NumberShort.linear, Prop(_go.transform.Find("Number"), "_Color"));
            Assert.AreEqual((Vector4)PotaLook.ItemShort.linear, Prop(_go.transform.Find("Item"), "_Color"));
        }

        [Test]
        public void LandingSquash_IsUniform()
        {
            var v = Make(new Block(0, 7));
            v.Squash = 1f;
            v.Tick(0f);
            Assert.Less(Vector3.Distance(Vector3.one * 0.88f, _go.transform.localScale), 1e-5f, "olcek = 1 − 0.12·carpma on every axis");
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PillowBlockMeshTests Project.Tests.LabelAtlasTests Project.Tests.BlockViewTests`
Expected: exit 2, `error CS0103: The name 'PillowBlockMesh' does not exist` (and `LabelAtlas`, `BlockView.BodyMaterial`).

- [ ] **Step 3: Write the mesh, the atlas map and the label shader**

`PillowBlockMesh.cs`:

```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// yastik.ts yastikGeometrisi: a rounded box whose front half (Unity −z) bulges by bulge·(1−u²)(1−w²)·(z/h), u = x/h, w = y/h;
    /// front-facing normals are bent by the slope (−∂f/∂x, −∂f/∂y). Position-only displacement: shared corners never crack.
    /// </summary>
    public static class PillowBlockMesh
    {
        public static Mesh Create(float size = PotaLook.Block, float radius = PotaLook.PillowRadius, float bulge = PotaLook.PillowBulge, int segments = 16)
        {
            var mesh = RoundedBoxMesh.Create(Vector3.one * size, radius, segments);
            var v = mesh.vertices;
            var n = mesh.normals;
            float h = size * 0.5f;
            for (int i = 0; i < v.Length; i++)
            {
                float z = -v[i].z;                               // Pota's +z front = Unity's −z
                if (z <= 0f || bulge == 0f) continue;
                float u = v[i].x / h, w = v[i].y / h, d = z / h;
                float a = Mathf.Max(0f, 1f - u * u), b = Mathf.Max(0f, 1f - w * w);
                v[i].z = -(z + bulge * a * b * d);
                float nz = -n[i].z;
                if (nz <= 0f) continue;
                float slopeX = bulge * (-2f * u / h) * b * d, slopeY = bulge * (-2f * w / h) * a * d;
                var pn = new Vector3(n[i].x - slopeX * nz, n[i].y - slopeY * nz, nz).normalized;
                n[i] = new Vector3(pn.x, pn.y, -pn.z);
            }
            mesh.vertices = v;
            mesh.normals = n;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.name = "PillowBlock";
            return mesh;
        }
    }
}
```

`LabelAtlas.cs`:

```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>yapbozSahne.ts EtiketAtlasi layout: 2048² canvas, 11 columns × 186 px; numbers 1–99 → cells 0–98, star 99, items 100–104.</summary>
    public static class LabelAtlas
    {
        public const int Size = 2048, Columns = 11, CellPx = 186, Star = 99, ItemBase = 100;

        /// <summary>rakamHucresi: min(99, max(1, n)) − 1.</summary>
        public static int NumberCell(int hp) => Mathf.Clamp(hp, 1, 99) - 1;

        public static int ItemCell(int color) => ItemBase + color;

        /// <summary>uvRect: (u, v, w, h) with v from the bottom (the canvas row counts from the top, like three's flipY).</summary>
        public static Vector4 UvRect(int cell)
        {
            int x = cell % Columns * CellPx, y = cell / Columns * CellPx;
            return new Vector4(x / (float)Size, 1f - (y + CellPx) / (float)Size, CellPx / (float)Size, CellPx / (float)Size);
        }
    }
}
```

`Assets/_Project/Games/Pota/Shaders/PotaLabel.shader`:

```hlsl
// Pota's label atlas quads (yapbozSahne.ts EtiketKatmani): atlas cell per instance (_UvRect), tint per instance (_Color, linear).
// Straight alpha like three's NormalBlending; GPU instancing keeps all block labels in a couple of draws.
Shader "Project/PotaLabel"
{
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _UvRect ("UV rect (u, v, w, h)", Vector) = (0, 0, 1, 1)
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Label"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UvRect)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                float4 r = UNITY_ACCESS_INSTANCED_PROP(Props, _UvRect);
                o.uv = r.xy + i.uv * r.zw;
                o.color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
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

- [ ] **Step 4: Rewrite `BlockView`**

`BlockView.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// One block in Pota's "balon" look: glossy pillow in its candy tone (linear colour per instance), the item sticker (72 %, centre,
    /// +0.06) and the hp number (44 %, bottom-right) from the label atlas, and the state the game drives every frame: flash, uniform
    /// squash, shiver, swell, the labels' counter-angle during rotation, dimming when the ammo does not cover the block.
    /// </summary>
    public class BlockView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static Mesh _quad;

        public Block Block { get; private set; }
        public int Cell;
        /// <summary>Home position (board-local cells) and the animated one.</summary>
        public Vector2 Home;
        public Vector2 Pos;
        /// <summary>Entrance multiplier (PotaFlow's reveal); 1 in play.</summary>
        public float Scale = 1f;
        public float Flash;
        public float Squash;
        public float Shiver;
        public bool Dim;
        public bool Exploding;
        /// <summary>Swell-then-burst scale while it runs (replaces the squash); −1 otherwise.</summary>
        public float Swell = -1f;
        /// <summary>Labels' angle relative to the board in degrees (Pota `donme`: rides the board, then stands up).</summary>
        public float LabelAngle;

        private MeshRenderer _body, _item, _number;
        private MaterialPropertyBlock _bodyMpb, _itemMpb, _numberMpb;
        private ColorHuntSettings _s;

        private static Mesh Quad => _quad != null ? _quad : (_quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx"));

        /// <summary>yapbozSahne.ts renkliMat: white MeshStandard, roughness 0.16, metalness 0.02; the tone comes per block.</summary>
        public static Material BodyMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PotaBlock" };
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 1f - 0.16f);
            m.SetFloat("_Metallic", 0.02f);
            return m;
        }

        public static Material LabelMaterial(Texture atlas)
        {
            var m = new Material(Shader.Find("Project/PotaLabel")) { name = "PotaLabel", enableInstancing = true };
            if (atlas != null) m.SetTexture("_BaseMap", atlas);
            return m;
        }

        /// <summary>yapbozSahne.ts secimHalka: white, opacity 0.9, unlit.</summary>
        public static Material RingMaterial()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "PotaSelection" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));
            return m;
        }

        public void Init(ColorHuntSettings s, Mesh body, Material bodyMat, Material labelMat)
        {
            _s = s;
            gameObject.AddComponent<MeshFilter>().sharedMesh = body;
            _body = gameObject.AddComponent<MeshRenderer>();
            _body.sharedMaterial = bodyMat;
            _body.shadowCastingMode = ShadowCastingMode.On;
            _body.receiveShadows = true;
            gameObject.AddComponent<BoxCollider>().size = Vector3.one * PotaLook.Block;
            _bodyMpb = new MaterialPropertyBlock();
            _itemMpb = new MaterialPropertyBlock();
            _numberMpb = new MaterialPropertyBlock();
            _item = Label("Item", labelMat, 2);
            _number = Label("Number", labelMat, 3);
        }

        private MeshRenderer Label(string name, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;       // Unity's quad faces −z, the camera
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortingOrder = order;                                // number after item (same block overlaps)
            return r;
        }

        public void SetBlock(Block b, int cell)
        {
            Block = b;
            Cell = cell;
            _itemMpb.SetVector(UvRectId, LabelAtlas.UvRect(LabelAtlas.ItemCell(b.Color)));
            _numberMpb.SetVector(UvRectId, LabelAtlas.UvRect(LabelAtlas.NumberCell(b.Hp)));
            Apply();
        }

        public Texture2D ItemIcon => _s.ItemIcon(Block.Color);

        /// <summary>Advance the transient state and write transform and colours (renkAviFaz.ts gorselleriIlerlet).</summary>
        public void Tick(float dt)
        {
            Flash = Mathf.Max(0f, Flash - dt * PotaLook.FlashDecay);
            Squash = Mathf.Max(0f, Squash - dt * PotaLook.SquashDecay);
            Shiver = Mathf.Max(0f, Shiver - dt);
            float t = _s.InsufficientShake;
            float shiverX = Shiver > 0f ? Mathf.Sin((t - Shiver) * 68f) * PotaLook.ShiverAmount * (Shiver / t) : 0f;
            transform.localPosition = new Vector3(Pos.x + shiverX, Pos.y, 0f);
            float own = Mathf.Max(0.001f, Swell >= 0f ? Swell : 1f - PotaLook.LandSquash * Squash);
            transform.localScale = Vector3.one * Mathf.Max(0.001f, own * Scale);
            PlaceLabels(own);
            Apply();
        }

        /// <summary>
        /// yapbozSahne.ts etiketleriYaz: the plane sits at (Z_ON + 0.03 + bulge)·max(1, scale), the quads grow with the block, the offsets
        /// do not (they are divided by the block's own scale here because the labels are its children).
        /// </summary>
        private void PlaceLabels(float own)
        {
            float a = LabelAngle * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            float z = -PotaLook.LabelZ * Mathf.Max(1f, own) / own;
            var rot = Quaternion.Euler(0f, 0f, LabelAngle);
            float ey = PotaLook.ItemOffsetY;
            var it = _item.transform;
            it.localPosition = new Vector3(-ey * sn / own, ey * cs / own, z);
            it.localRotation = rot;
            it.localScale = Vector3.one * (PotaLook.LabelQuad * PotaLook.ItemScale);
            var o = PotaLook.NumberOffset;
            var nt = _number.transform;
            nt.localPosition = new Vector3((o.x * cs - o.y * sn) / own, (o.x * sn + o.y * cs) / own, z - 0.01f / own);
            nt.localRotation = rot;
            nt.localScale = Vector3.one * (PotaLook.LabelQuad * PotaLook.NumberScale);
        }

        private void Apply()
        {
            _bodyMpb.SetVector(BaseColorId, PotaLook.BlockColor(_s.PaletteColor(Block.Color), Flash, Dim));
            _body.SetPropertyBlock(_bodyMpb);
            _itemMpb.SetVector(ColorId, Dim ? PotaLook.ItemShort.linear : Color.white);
            _item.SetPropertyBlock(_itemMpb);
            _numberMpb.SetVector(ColorId, Dim ? PotaLook.NumberShort.linear : Color.white);
            _number.SetPropertyBlock(_numberMpb);
        }
    }
}
```

- [ ] **Step 5: Wire it into the game, drop the old block fields**

`ColorHuntSettings.cs`:
- Palette default becomes Pota's tones (comment: `yapbozSahne.ts SEKER_TONU`):
  `public Color[] Palette = (Color[])PotaLook.Tones.Clone();` with header `[Header("Palette (Pota SEKER_TONU: red, blue, green, yellow, purple)")]`.
- Delete `BlockSize`, `BlockDepth`, `BlockRadius`, `BlockFace`, `ItemOffsetY`, `ItemScale`, `NumberOffsetY`, `NumberScale`, `DimAmount` and their tooltips.
- `InsufficientShake` tooltip: `"renkAviFaz.ts HIS.titrem.sure: shiver of a block the ammo does not cover."`

`ColorHuntGame.cs`:
- Fields: replace `private Material _blockMat, _textMat, _shadowMat, _itemMat;` and `private Font _font;` with `private Material _blockMat, _labelMat, _itemMat;`.
- `Start()`: delete the font/text-material lines (`_font = …` through `_font.RequestCharactersInTexture(…)`) and the `_blockMesh`/`_blockMat` lines; keep
  the `_itemMat` block (flyers, until Task 6); add after `if (Fx != null) Fx.ItemMaterial = _itemMat;`:

```csharp
            _blockMesh = PillowBlockMesh.Create();
            _shardMesh = RoundedBoxMesh.Create(Vector3.one, 0.22f, 3);
            _blockMat = BlockView.BodyMaterial();
            _labelMat = BlockView.LabelMaterial(S.LabelAtlas);
```

  and replace the selection ring creation with (yapbozSahne.ts secimHalka: torus 0.46 / 0.055, white 0.9):

```csharp
            _selectionRing = RimMeshBuilder.MeshObject("SelectionRing", BoardRoot, RimMeshBuilder.Torus(0.46f, 0.055f, 26, 8), BlockView.RingMaterial());
            _selectionRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _selectionRing.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selectionRing.SetActive(false);
```

- `Select`: ring position `new Vector3(v.Home.x, v.Home.y, -(PotaLook.FrontZ + 0.08f))`.
- `Tap`: `if (_views.TryGetValue(cell, out var v)) v.Shiver = S.InsufficientShake;` (no red flash: Pota's number is already red when short).
- `TickViews`: `foreach (var v in _views.Values) v.Tick(dt);`
- `StartDrops` puff position: `new Vector3(v.Home.x, v.Home.y - PotaLook.Block * 0.5f, 0f)`.
- `NewView`: `v.Init(S, _blockMesh, _blockMat, _labelMat);` and add `v.Swell = -1f; v.LabelAngle = 0f;` to the reset line.

`BallSalvo.cs:38`: replace `-S.BlockDepth * 0.5f - 0.18f` with `-PotaLook.FrontZ - 0.18f`.

`ColorHuntSceneTool.cs`: delete `BlockFacePath` and the `s.BlockFace` line; the report line drops `face=…`.

`PotaLookImportTool.Import()`: after `s.Font = …` add `s.Palette = (Color[])PotaLook.Tones.Clone();` and add `palette=pota` to the report.

- [ ] **Step 6: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PillowBlockMeshTests Project.Tests.LabelAtlasTests Project.Tests.BlockViewTests`
Expected: 11/11 PASS.
Run: `~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_potalook_import","parameters":{}}'` → Expected: `… palette=pota …`.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile` and `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile`
Expected: all pass (EditMode 102 + the new ones; PlayMode 31).

- [ ] **Step 7: Look at it**

Rebuild the scene (`execute_custom_tool ai_colorhunt_build_scene`), `manage_editor play`, wait for the round, then
`manage_camera {"action":"screenshot","capture_source":"game_view","include_image":true,"output_folder":"Temp/mcp_out"}`, `manage_editor stop`.
Expected: glossy pillow blocks in Pota's candy tones (still in the old frame and camera), each with a big item sticker and a small
outlined number at the bottom-right. Numbers red and blocks grey where the ammo does not cover them.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Games/Pota
git commit -m "feat: Color Hunt blocks in Pota's balloon look — pillow mesh, atlas labels, candy tones

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 5: The board's world — giant hoop at Pota's size, court, baked room reflection, Pota camera, the Color Hunt scene

**Files:**
- Create: `Assets/_Project/Games/Pota/Shaders/PotaRoom.shader`, `Assets/_Project/Games/Pota/Editor/PotaRoomBaker.cs`,
  `Assets/_Project/Games/Pota/Scripts/Flow/PuzzleStage.cs`
- Rewrite: `Scripts/Flow/GiantHoop.cs` (`Build()` at DEV_POTA), `Scripts/ColorHunt/ColorHuntCamera.cs` (Pota fit)
- Modify: `Scripts/Flow/CourtBuilder.cs` (centre overload), `Scripts/ColorHunt/BoardBuilder.cs` (scenery removed),
  `Scripts/ColorHunt/ColorHuntGame.cs` (no scenery, grid scale), `Scripts/ColorHunt/ColorHuntSettings.cs` (+`RoomReflection`;
  − `FrameColor, PlateColor, SkyTop, SkyBottom`), `Scripts/Flow/PotaFlowDirector.cs` (`Hoop.Build()`, no board-frame glow),
  `Editor/PotaLookImportTool.cs` (bake), `Editor/SceneParts.cs` (`CreateGlobalVolume(VolumeProfile…)`, `EnablePost`),
  `Editor/ColorHuntSceneTool.cs` (camera, sun, lighting, volume, stage), `Tests/PlayMode/Pota.Tests.PlayMode.asmdef` (+URP/Core refs)
- Test: `Tests/EditMode/{GiantHoopTests,ColorHuntStagingTests,BallStreamTests,PotaLookAssetsTests}.cs` (update),
  `Tests/EditMode/PotaRoomBakerTests.cs` (new), `Tests/PlayMode/ColorHuntStagingPlayTests.cs` (update), `Tests/PlayMode/ColorHuntScenePlayTests.cs` (new)

**Interfaces:**
- Consumes: `PotaLook` (Task 2; incl. `Sky`, `Fog`, `SunColor`, `SunIntensity`, `SunDirection`, `Ambient*`, `ThreeToUnityLight`),
  `ColorHuntSettings.Backboard/PuzzleProfile` (Task 1).
- Produces: `GiantHoop.Build()` (fields `GroundY`, `Backboard`, DEV_POTA defaults; `RimCenter`, `SlotPosition`, `SetGlow` as before);
  `PuzzleStage` (`Game`, `BuildOnStart`, `GroundBelow 8.5`, `CourtSize`, `CourtNear 21.6`, `BuildCourt`, `Build()`, `Hoop`, `Reflection`, `Court`);
  `CourtBuilder.Build(Transform parent, Vector2 size, Vector3 centre, Color floor, Color line)`;
  `ColorHuntCamera.FitPosition(float fov, float aspect, Vector3 anchor[, float topCss, float bottomCss])`, `FitFor(fov, aspect)`,
  `TopCss`, `BottomCss`, `TopCssNow`, `SafeTopCss()`; `ColorHuntSettings.RoomReflection` (Cubemap);
  `PotaRoomBaker.BuildRoom(Vector3 origin, int layer)`, `PotaRoomBaker.Bake(List<string> report)`;
  `SceneParts.CreateGlobalVolume(VolumeProfile profile, string name = "Global Volume", float weight = 1f)`, `SceneParts.EnablePost(Camera)`;
  `ColorHuntSceneTool.CreateCamera()`, `CreateSun()` (Pota sun), `ApplyPuzzleLighting()`.

- [ ] **Step 1: Write the failing tests**

`GiantHoopTests.cs` — replace `Build()` and `Rim_SitsBelowTheBoard_InFrontOfIt`/`Pole_StandsOnTheRoof`, add the frame test (pile tests stay):

```csharp
        private GiantHoop Build(float groundY = 0f)
        {
            _root = new GameObject("Game");
            _root.transform.position = new Vector3(0f, 8.5f, 30f);
            var hoop = new GameObject("GiantHoop").AddComponent<GiantHoop>();
            hoop.transform.SetParent(_root.transform, false);
            hoop.GroundY = groundY;
            hoop.Build();
            return hoop;
        }

        [Test]
        public void Rim_SitsAtPotasDevPota()
        {
            var h = Build();
            Assert.Less(Vector3.Distance(h.RimCenter, new Vector3(0f, 8.5f - 5.6f, 30f - 2.2f)), 1e-3f, "cemberY −5.6, R + 0.6 toward the camera");
        }

        [Test]
        public void Frame_OuterIs9Point5_AroundAn8Point8Glass()
        {
            var h = Build();
            var glass = h.transform.Find("Glass").GetComponent<Renderer>().bounds;
            Assert.AreEqual(8.8f, glass.size.x, 1e-3f);
            Assert.AreEqual(30.7f, glass.center.z, 1e-3f, "pano plane 0.7 behind the board centre");
            var top = h.transform.Find("FrameTop").GetComponent<Renderer>().bounds;
            Assert.AreEqual(9.5f, top.size.x, 1e-3f, "panoEn + 2·cerceve");
            Assert.AreEqual(8.5f + 4.75f, top.max.y, 1e-3f);
            Assert.AreEqual(0.9f, top.size.z, 1e-3f);
        }

        [Test]
        public void Pole_StandsOnTheGround()
        {
            Assert.AreEqual(0f, Build().transform.Find("Pole").GetComponent<Renderer>().bounds.min.y, 0.02f);
            Object.DestroyImmediate(_root);
            Assert.AreEqual(-3f, Build(-3f).transform.Find("Pole").GetComponent<Renderer>().bounds.min.y, 0.02f);
        }
```

`BallStreamTests.cs:33`: `hoop.Build(7);` → `hoop.Build();`

`ColorHuntStagingTests.cs` — replace the two fit tests:

```csharp
        [Test]
        public void FitPosition_MovesWithTheAnchor()
        {
            var home = ColorHuntCamera.FitPosition(38f, 9f / 16f, Vector3.zero);
            var anchor = new Vector3(0f, 8.5f, 30f);
            var moved = ColorHuntCamera.FitPosition(38f, 9f / 16f, anchor);
            Assert.That(Vector3.Distance(moved - anchor, home), Is.LessThan(1e-4f));
        }

        [Test]
        public void PotaFit_Portrait()
        {
            // 9:16, no notch (HUD band top 87 CSS px, buttons 160): 24.897 m in front of the front plane, looking 1.2 m below the board centre.
            var p = ColorHuntCamera.FitPosition(38f, 9f / 16f, Vector3.zero);
            Assert.AreEqual(-(0.45f + 24.897f), p.z, 0.01f);
            Assert.AreEqual(-1.2026f, p.y, 0.005f);
            Assert.AreEqual(0f, p.x, 1e-5f);
        }

        [Test]
        public void PotaFit_FrameOuterWidth_Fills98Point5PercentOfTheScreen()
        {
            var go = new GameObject("Cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 38f;
                cam.aspect = 9f / 16f;
                go.transform.position = ColorHuntCamera.FitPosition(38f, 9f / 16f, Vector3.zero);
                var right = cam.WorldToViewportPoint(new Vector3(4.75f, 0f, -0.45f));   // frame's outer edge on the front plane
                Assert.AreEqual(0.5f + 0.985f * 0.5f, right.x, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }
```

`PotaRoomBakerTests.cs`:

```csharp
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Tests
{
    public class PotaRoomBakerTests
    {
        private GameObject _room;

        [TearDown]
        public void TearDown()
        {
            if (_room != null) Object.DestroyImmediate(_room);
        }

        [Test]
        public void Room_IsThreesRoomEnvironment_MirroredInZ()
        {
            _room = PotaRoomBaker.BuildRoom(new Vector3(0f, -400f, 0f), 31);
            var renderers = _room.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(13, renderers.Length, "room + 6 boxes + 6 panels");
            foreach (var r in renderers) Assert.AreEqual(31, r.gameObject.layer, r.name);
            var room = _room.transform.Find("Room");
            Assert.Less(Vector3.Distance(room.position, new Vector3(-0.757f, -400f + 13.219f, -0.717f)), 1e-4f);
            var mat = room.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual((float)CullMode.Front, mat.GetFloat("_Cull"), "three BackSide: the inside faces the probe");
            Assert.AreEqual(900f, mat.GetFloat("_LightIntensity"));
            Assert.Less(Vector3.Distance((Vector3)mat.GetVector("_LightPos"), new Vector3(0.418f, -400f + 16.199f, -0.3f)), 1e-4f);
            var box1 = _room.transform.Find("Box1");
            Assert.AreEqual(0.195f * Mathf.Rad2Deg, Mathf.DeltaAngle(0f, box1.eulerAngles.y), 1e-3f, "three −0.195 rad about y, mirrored");
            float[] e = { 50f, 50f, 17f, 43f, 20f, 100f };
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(e[i], _room.transform.Find($"Light{i + 1}").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Emission"));
        }

        [Test]
        public void RoomShader_Exists() => Assert.IsNotNull(Shader.Find("Project/PotaRoom"));
    }
}
```

Append to `PotaLookAssetsTests.cs`:

```csharp
        [Test]
        public void RoomReflection_IsABakedSpecularCubemap()
        {
            var s = Settings();
            Assert.IsNotNull(s.RoomReflection, Hint);
            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.RoomReflection));
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            Assert.AreEqual(TextureImporterShape.TextureCube, ts.textureShape);
            Assert.AreEqual(TextureImporterCubemapConvolution.Specular, ts.cubemapConvolution);
        }
```

`ColorHuntStagingPlayTests.CameraFit_FollowsTheAnchor`: the expected line becomes
`var expected = ColorHuntCamera.FitPosition(cam.fieldOfView, cam.aspect, fit.Anchor, fit.TopCssNow, fit.BottomCss);`

`Tests/PlayMode/ColorHuntScenePlayTests.cs` (add `"Unity.RenderPipelines.Universal.Runtime"` and `"Unity.RenderPipelines.Core.Runtime"` to
`Pota.Tests.PlayMode.asmdef` references):

```csharp
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Flow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>The Color Hunt scene as saved: Pota's camera, post, lights and surroundings around the board.</summary>
    public class ColorHuntScenePlayTests
    {
        [UnityTest]
        public IEnumerator Scene_IsPotasStage()
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            var cam = Camera.main;
            Assert.AreEqual(38f, cam.fieldOfView, 1e-4f, "yapbozSahne.ts GORUS_ACISI");
            Assert.IsTrue(cam.GetUniversalAdditionalCameraData().renderPostProcessing, "post on: Pota's tonemap, bloom, vignette");
            Assert.AreSame(game.S.PuzzleProfile, Object.FindAnyObjectByType<Volume>().sharedProfile);
            Assert.AreEqual(PotaLook.Sky.r, cam.backgroundColor.r, 1e-3f);
            Assert.AreEqual(PotaLook.Sky.b, cam.backgroundColor.b, 1e-3f);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(30f, RenderSettings.fogStartDistance, 1e-4f);
            Assert.AreEqual(1.2f, game.BoardRoot.localScale.x, 1e-5f, "the grid fills the 8.8 backboard");
            var stage = Object.FindAnyObjectByType<PuzzleStage>();
            Assert.IsNotNull(stage.Hoop);
            Assert.Less(Vector3.Distance(stage.Hoop.RimCenter, game.transform.position + new Vector3(0f, -5.6f, -2.2f)), 1e-3f);
            Assert.IsNotNull(stage.Reflection, "baked room reflection over the board");
            Assert.IsTrue(stage.Reflection.bounds.Contains(game.CellWorld(0)));
            var fit = cam.GetComponent<ColorHuntCamera>();
            Assert.Less(Vector3.Distance(cam.transform.position, fit.FitFor(cam.fieldOfView, cam.aspect)), 0.6f, "shake aside");
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.GiantHoopTests Project.Tests.ColorHuntStagingTests Project.Tests.PotaRoomBakerTests`
Expected: exit 2 — `error CS7036` (`Build` needs `boardSize`), `error CS0117: 'GiantHoop' does not contain 'GroundY'`,
`'PotaRoomBaker' does not exist`.

- [ ] **Step 3: Room shader and baker**

`Assets/_Project/Games/Pota/Shaders/PotaRoom.shader`:

```hlsl
// three.js RoomEnvironment materials, for the Editor-only reflection bake (PotaRoomBaker): MeshStandardMaterial white, diffuse part,
// lit by PointLight(900 cd, distance 28, decay 2) with three's falloff 1/d² · saturate(1 − (d/r)⁴)²; the panels are
// MeshBasicMaterial colour 17–100 (HDR). No Unity lights, no ambient: exactly the room three renders into its PMREM.
Shader "Project/PotaRoom"
{
    Properties
    {
        _Albedo ("Albedo", Float) = 1
        _Emission ("Emission", Float) = 0
        _LightPos ("Light position (world)", Vector) = (0, 0, 0, 0)
        _LightIntensity ("Light intensity (cd)", Float) = 900
        _LightRange ("Light range", Float) = 28
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Room"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Albedo;
                float _Emission;
                float4 _LightPos;
                float _LightIntensity;
                float _LightRange;
                float _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            float4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);   // BackSide: three flips the normal of back faces
                float3 l = _LightPos.xyz - i.positionWS;
                float d = length(l);
                float ndl = saturate(dot(n, l / max(d, 1e-4)));
                float win = saturate(1.0 - pow(d / _LightRange, 4.0));
                float lit = _Albedo * INV_PI * _LightIntensity * ndl * win * win / max(d * d, 0.01);
                return float4((lit + _Emission).xxx, 1.0);
            }
            ENDHLSL
        }
    }
}
```

`Assets/_Project/Games/Pota/Editor/PotaRoomBaker.cs`:

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Bakes three.js' RoomEnvironment (examples/jsm/environments/RoomEnvironment.js, r180) — the room Pota's glossy blocks reflect
    /// (yapbozSahne.ts ortamUret) — into an HDR cubemap with specular convolution. The room is built far below the open scene on the
    /// PotaRoom layer (DontSave), baked by one reflection probe and destroyed. three's z is mirrored (Unity's board faces −z), so
    /// rotations about y flip sign.
    /// </summary>
    public static class PotaRoomBaker
    {
        public const string CubemapPath = PotaLookImportTool.LookFolder + "/PotaRoom.exr";
        public static readonly Vector3 Origin = new Vector3(0f, -400f, 0f);

        private static readonly Vector3 LightPos = new Vector3(0.418f, 16.199f, 0.300f);
        private static readonly (Vector3 p, float ry, Vector3 s)[] Boxes =
        {
            (new Vector3(-10.906f, 2.009f, 1.846f), -0.195f, new Vector3(2.328f, 7.905f, 4.651f)),
            (new Vector3(-5.607f, -0.754f, -0.758f), 0.994f, new Vector3(1.970f, 1.534f, 3.955f)),
            (new Vector3(6.167f, 0.857f, 7.803f), 0.561f, new Vector3(3.927f, 6.285f, 3.687f)),
            (new Vector3(-2.017f, 0.018f, 6.124f), 0.333f, new Vector3(2.002f, 4.566f, 2.064f)),
            (new Vector3(2.291f, -0.756f, -2.621f), -0.286f, new Vector3(1.546f, 1.552f, 1.496f)),
            (new Vector3(-2.193f, -0.369f, -5.547f), 0.516f, new Vector3(3.875f, 3.487f, 2.986f)),
        };
        private static readonly (Vector3 p, Vector3 s, float e)[] Panels =
        {
            (new Vector3(-16.116f, 14.37f, 8.208f), new Vector3(0.1f, 2.428f, 2.739f), 50f),
            (new Vector3(-16.109f, 18.021f, -8.207f), new Vector3(0.1f, 2.425f, 2.751f), 50f),
            (new Vector3(14.904f, 12.198f, -1.832f), new Vector3(0.15f, 4.265f, 6.331f), 17f),
            (new Vector3(-0.462f, 8.89f, 14.520f), new Vector3(4.38f, 5.441f, 0.088f), 43f),
            (new Vector3(3.235f, 11.486f, -12.541f), new Vector3(2.5f, 2.0f, 0.1f), 20f),
            (new Vector3(0f, 20f, 0f), new Vector3(1f, 0.1f, 1f), 100f),
        };

        public static Vector3 Mirror(Vector3 three) => new Vector3(three.x, three.y, -three.z);

        /// <summary>The room around <paramref name="origin"/> (three's cube camera), every renderer on <paramref name="layer"/>; never saved.</summary>
        public static GameObject BuildRoom(Vector3 origin, int layer)
        {
            var root = new GameObject("PotaRoomBake") { hideFlags = HideFlags.DontSave, layer = layer };
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var shader = Shader.Find("Project/PotaRoom");
            var light = origin + Mirror(LightPos);
            Material Mat(float albedo, float emission, CullMode cull)
            {
                var m = new Material(shader) { hideFlags = HideFlags.DontSave };
                m.SetFloat("_Albedo", albedo);
                m.SetFloat("_Emission", emission);
                m.SetVector("_LightPos", light);
                m.SetFloat("_LightIntensity", 900f);
                m.SetFloat("_LightRange", 28f);
                m.SetFloat("_Cull", (float)cull);
                return m;
            }
            Part(root, "Room", cube, Mat(1f, 0f, CullMode.Front), origin, new Vector3(-0.757f, 13.219f, 0.717f), 0f, new Vector3(31.713f, 28.305f, 28.591f), layer);
            var boxMat = Mat(1f, 0f, CullMode.Back);
            for (int i = 0; i < Boxes.Length; i++) Part(root, $"Box{i + 1}", cube, boxMat, origin, Boxes[i].p, Boxes[i].ry, Boxes[i].s, layer);
            for (int i = 0; i < Panels.Length; i++) Part(root, $"Light{i + 1}", cube, Mat(0f, Panels[i].e, CullMode.Back), origin, Panels[i].p, 0f, Panels[i].s, layer);
            return root;
        }

        private static void Part(GameObject root, string name, Mesh mesh, Material mat, Vector3 origin, Vector3 threePos, float threeRotY, Vector3 scale, int layer)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = layer };
            go.transform.SetParent(root.transform, false);
            go.transform.position = origin + Mirror(threePos);
            go.transform.rotation = Quaternion.Euler(0f, -threeRotY * Mathf.Rad2Deg, 0f);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>Builds the room, bakes <see cref="CubemapPath"/> (256², HDR, specular convolution), removes the room.</summary>
        public static Cubemap Bake(List<string> report)
        {
            int layer = PotaLookImportTool.EnsureLayer(PotaLookImportTool.RoomLayer);
            var room = BuildRoom(Origin, layer);
            var probeGo = new GameObject("PotaRoomProbe") { hideFlags = HideFlags.DontSave };
            try
            {
                probeGo.transform.position = Origin;
                var p = probeGo.AddComponent<ReflectionProbe>();
                p.mode = ReflectionProbeMode.Baked;
                p.resolution = 256;
                p.hdr = true;
                p.cullingMask = 1 << layer;
                p.clearFlags = ReflectionProbeClearFlags.SolidColor;
                p.backgroundColor = Color.black;
                p.nearClipPlane = 0.05f;
                p.farClipPlane = 80f;
                if (!Lightmapping.BakeReflectionProbe(p, CubemapPath)) throw new System.InvalidOperationException("reflection probe bake failed");
            }
            finally
            {
                Object.DestroyImmediate(probeGo);
                Object.DestroyImmediate(room);
            }
            var imp = (TextureImporter)AssetImporter.GetAtPath(CubemapPath);
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.textureShape = TextureImporterShape.TextureCube;
            ts.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
            ts.mipmapEnabled = true;
            ts.filterMode = FilterMode.Trilinear;
            imp.SetTextureSettings(ts);
            imp.SaveAndReimport();
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(CubemapPath);
            report.Add($"room reflection {CubemapPath} {(cube != null ? cube.width : 0)}²");
            return cube;
        }
    }
}
```

In `PotaLookImportTool.Import()`, before `var s = ColorHuntSceneTool.EnsureSettings(report);` add `var room = PotaRoomBaker.Bake(report);`
and after the palette line `s.RoomReflection = room;`. In `ColorHuntSettings` (Pota look assets group) add:

```csharp
        [Tooltip("three.js RoomEnvironment baked by ai_potalook_import (PotaRoomBaker): the blocks' reflections, intensity 0.45.")]
        public Cubemap RoomReflection;
```

- [ ] **Step 4: Giant hoop, court, stage**

`GiantHoop.cs` — replace the fields block and `Build`; keep `SlotPosition`, `EntryPoint`, `SinkPoint`, `RestingCapacity`, `AddBall`,
`AddToPile`, `ClearPile`, `Tick`, `Box`, `Emissive`; `SetGlow` loses its base glow; `Transparent` takes the backboard:

```csharp
    /// <summary>
    /// The giant hoop around the Color Hunt board at Pota's size (sahaYerlesim.ts DEV_POTA, pota.ts): frosted glass backboard 8.8²
    /// (pano-a.png, #bfe3ff, opacity 0.32) 0.7 behind the board centre, orange #ff8a2a frame 0.35 thick × 0.9 deep (outer 9.5), rim
    /// R 1.6 at y −5.6 sticking out R + 0.6 toward the camera, a 6-ring net 1.8 deep (PotaFlow's ammo hopper: up to
    /// <see cref="MaxPile"/> balls rest in it) and a grey pole down to <see cref="GroundY"/>. Child of the board's game root, which is
    /// never scaled (the grid scales inside the glass); local space = the root's (the board faces −z).
    /// </summary>
    public class GiantHoop : MonoBehaviour, IBallReceiver
    {
        public const int MaxPile = 10;

        public float RimRadius = PotaLook.RimRadius;
        public float RimTube = 0.1f;
        public float RimY = PotaLook.RimY;
        [Tooltip("Rim centre in front of the board (pota.ts: R + 0.6).")]
        public float RimForward = PotaLook.RimRadius + 0.6f;
        public float NetDepth = PotaLook.NetDepth;
        public int NetRings = 6;
        public float GlassSize = PotaLook.PanoSize;
        public float GlassZ = 0.7f;
        public float FrameWidth = PotaLook.FrameWidth;
        public float FrameDepth = 0.9f;
        public float FrameZ = 0.35f;
        public float PoleZ = 1.2f;
        public float PoleDiameter = 0.55f;
        [Tooltip("World height of the ground the pole stands on (PotaFlow: the roof, 0).")]
        public float GroundY;
        public float BallRadius = 0.24f;
        public Texture2D Backboard;
        public Color FrameColor = PotaLook.Hex(0xff8a2a);
        public Color GlassColor = new Color(0.749f, 0.890f, 1f, 0.32f);   // #bfe3ff, opacity 0.32
        public Color NetColor = PotaLook.Hex(0xf4f4f4);
        public Color PoleColor = PotaLook.Hex(0x8c8c8c);

        public Transform Rim { get; private set; }
        public Transform Net { get; private set; }
        public Vector3 RimCenter => Rim.position;
        public int Pile { get; private set; }

        private Material _frameMat, _rimMat;
        private float _kick;

        public void Build()
        {
            float half = GlassSize * 0.5f, outer = half + FrameWidth;
            _frameMat = Emissive(FrameColor, 0.5f, 0.1f);
            _rimMat = Emissive(FrameColor, 0.5f, 0.1f);
            var netMat = RimMeshBuilder.UrpLit(NetColor, 0.2f);
            var poleMat = RimMeshBuilder.UrpLit(PoleColor, 0.4f);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            var glass = RimMeshBuilder.MeshObject("Glass", transform, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), Transparent(GlassColor, Backboard));
            glass.transform.localPosition = new Vector3(0f, 0f, GlassZ);
            glass.transform.localScale = new Vector3(GlassSize, GlassSize, 1f);
            glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var barH = RoundedBoxMesh.Create(new Vector3(GlassSize + 2f * FrameWidth, FrameWidth, FrameDepth), 0.06f, 3);
            var barV = RoundedBoxMesh.Create(new Vector3(FrameWidth, GlassSize, FrameDepth), 0.06f, 3);
            float mid = half + FrameWidth * 0.5f;
            Bar("FrameTop", barH, new Vector3(0f, mid, FrameZ));
            Bar("FrameBottom", barH, new Vector3(0f, -mid, FrameZ));
            Bar("FrameLeft", barV, new Vector3(-mid, 0f, FrameZ));
            Bar("FrameRight", barV, new Vector3(mid, 0f, FrameZ));

            Rim = RimMeshBuilder.MeshObject("Rim", transform, RimMeshBuilder.Torus(RimRadius, RimTube, 48, 12), _rimMat).transform;
            Rim.localPosition = new Vector3(0f, RimY, -RimForward);
            float backZ = -RimForward + RimRadius;
            Box("RimStrut", cube, _rimMat, new Vector3(0f, (RimY - outer) * 0.5f, FrameZ), new Vector3(0.3f, Mathf.Abs(RimY + outer) + 0.12f, 0.2f));
            Box("RimBracket", cube, _rimMat, new Vector3(0f, RimY, (backZ + FrameZ) * 0.5f), new Vector3(0.4f, 0.12f, FrameZ - backZ));

            Net = new GameObject("Net").transform;
            Net.SetParent(Rim, false);
            NetBuilder.Build(Net, RimRadius * 0.98f, netMat, NetRings, 16, NetDepth / NetRings, 0.03f, 0.035f);

            // Pole from the ground up to the glass centre, plus an arm to the glass (the game root is never rotated or scaled).
            float ground = GroundY - transform.position.y;
            float len = Mathf.Max(0.01f, -ground);
            var pole = RimMeshBuilder.MeshObject("Pole", transform, cyl, poleMat);
            pole.transform.localPosition = new Vector3(0f, ground + len * 0.5f, PoleZ);
            pole.transform.localScale = new Vector3(PoleDiameter, len * 0.5f, PoleDiameter);
            Box("PoleArm", cube, poleMat, new Vector3(0f, 0f, (GlassZ + PoleZ) * 0.5f), new Vector3(0.25f, 0.25f, PoleZ - GlassZ + 0.1f));
            SetGlow(0f);
        }

        private void Bar(string name, Mesh mesh, Vector3 pos)
        {
            var go = RimMeshBuilder.MeshObject(name, transform, mesh, _frameMat);
            go.transform.localPosition = pos;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
```

with `SetGlow`: `var c = FrameColor * (1.2f * Mathf.Clamp01(g));` (no base glow: Pota's frame does not glow at rest), `Emissive(Color c,
float smoothness, float metallic)` setting `_Metallic`, and `Transparent(Color c, Texture2D map)` additionally doing
`if (map != null) m.SetTexture("_BaseMap", map);` with smoothness `1 − 0.25` (MeshPhysical roughness 0.25).
Add `using Project.ColorHunt;` is already there; add `using UnityEngine.Rendering;` for `ShadowCastingMode`.

`CourtBuilder.cs` — the existing method becomes the general one, the settings overload delegates:

```csharp
        /// <summary>PotaFlow's court from its settings (world: the terrace end at CourtStartZ).</summary>
        public static GameObject Build(Transform parent, PotaFlowSettings s) =>
            Build(parent, s.CourtSize, new Vector3(0f, 0f, s.CourtStartZ + s.CourtSize.y * 0.5f), s.CourtColor, s.CourtLineColor);

        /// <summary>Marked top centred at <paramref name="centre"/> (world; v = 1 toward +z, the hoop end) with a plain slab under it.</summary>
        public static GameObject Build(Transform parent, Vector2 size, Vector3 centre, Color floor, Color line)
        {
            var tex = CourtTexture(512, 1024, floor, line);
            var mat = BasketEnvironmentBuilder.TexturedLit(tex, Color.white, 0.25f);
            var top = RimMeshBuilder.MeshObject("Court", parent, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), mat);
            top.transform.position = centre;
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            top.transform.localScale = new Vector3(size.x, size.y, 1f);

            var edge = RimMeshBuilder.UrpLit(floor * 0.62f, 0.2f);
            var body = RimMeshBuilder.MeshObject("CourtBody", top.transform, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), edge);
            body.transform.SetPositionAndRotation(centre + new Vector3(0f, -0.201f, 0f), Quaternion.identity);
            body.transform.localScale = Vector3.one;
            var ls = body.transform.lossyScale;      // undo the quad's scale/rotation: size.x × 0.4 × size.y in world terms
            body.transform.localScale = new Vector3(size.x / ls.x, 0.4f / ls.y, size.y / ls.z);
            return top;
        }
```

`Assets/_Project/Games/Pota/Scripts/Flow/PuzzleStage.cs`:

```csharp
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Flow
{
    /// <summary>
    /// Pota's puzzle surroundings (saha.ts field): the giant hoop around the board, the court under it and the baked room reflection
    /// (yapbozSahne.ts ortamUret, envMapIntensity 0.45) as a custom probe over the board. Defaults = PotaFlow's layout measured from the
    /// board (8.5 m above the roof, court from 21.6 m in front of the board to 6.4 m behind it). The Color Hunt scene builds it at Start.
    /// </summary>
    public class PuzzleStage : MonoBehaviour
    {
        public ColorHuntGame Game;
        public bool BuildOnStart = true;
        [Tooltip("Ground below the board centre (PotaFlow: BoardAnchor.y above the roof).")]
        public float GroundBelow = 8.5f;
        public Vector2 CourtSize = new Vector2(15f, 28f);
        [Tooltip("How far the court reaches from the board toward the camera (PotaFlow: BoardAnchor.z − CourtStartZ).")]
        public float CourtNear = 21.6f;
        public bool BuildCourt = true;
        public Color CourtColor = new Color(0.851f, 0.467f, 0.227f);   // saha.ts zeminDokusu #d9773a
        public Color CourtLineColor = Color.white;
        public float ReflectionIntensity = 0.45f;
        public Vector3 ReflectionBox = new Vector3(26f, 26f, 26f);
        public float BallRadius = 0.19f;

        public GiantHoop Hoop { get; private set; }
        public ReflectionProbe Reflection { get; private set; }
        public GameObject Court { get; private set; }

        private void Start()
        {
            if (BuildOnStart) Build();
        }

        public void Build()
        {
            if (Hoop != null || Game == null) return;
            var root = Game.transform;
            var hoopGo = new GameObject("GiantHoop");
            hoopGo.transform.SetParent(root, false);
            Hoop = hoopGo.AddComponent<GiantHoop>();
            Hoop.GroundY = root.position.y - GroundBelow;
            Hoop.BallRadius = BallRadius;
            Hoop.Backboard = Game.S.Backboard;
            Hoop.Build();
            if (BuildCourt)
                Court = CourtBuilder.Build(transform, CourtSize, root.position + new Vector3(0f, -GroundBelow, CourtSize.y * 0.5f - CourtNear), CourtColor, CourtLineColor);
            if (Game.S.RoomReflection == null) { Debug.LogWarning("[PuzzleStage] no RoomReflection: run ai_potalook_import"); return; }
            var go = new GameObject("RoomReflection");
            go.transform.SetParent(transform, false);
            go.transform.position = root.position;
            Reflection = go.AddComponent<ReflectionProbe>();
            Reflection.mode = ReflectionProbeMode.Custom;
            Reflection.customBakedTexture = Game.S.RoomReflection;
            Reflection.intensity = ReflectionIntensity;
            Reflection.size = ReflectionBox;
            Reflection.boxProjection = false;
            Reflection.importance = 10;
        }
    }
}
```

`PotaFlowDirector.cs`: `Hoop.Build(Game.S.Size);` → `Hoop.Backboard = Game.S.Backboard; Hoop.Build();`; delete `GlowBoardFrame`, its two
calls (`GlowBoardFrame(glow);`, `GlowBoardFrame(0f);`) and the `_boardFrameMat` field (the board's own frame is gone; the hoop glows).

- [ ] **Step 5: Board without scenery, Pota camera**

`BoardBuilder.cs`: delete `Build` and `Bar` and `using Project.Basket.Visual;`; update the summary to "Board space helpers: cell
positions (cell (row, col) → x = col − (n−1)/2, y = (n−1)/2 − row, z = 0; the camera looks down +z), soft sprites". `Margin` stays
(salvo launcher and confetti until Tasks 7–8).

`ColorHuntGame.cs`: in `Start()` delete `BoardBuilder.Build(transform, S, S.Size);`; after `BoardRoot.SetParent(transform, false);` add
`BoardRoot.localScale = Vector3.one * PotaLook.GridScale(S.Size);`. In `Prepare` after `BuildViews(Board);` add
`BoardRoot.localScale = Vector3.one * PotaLook.GridScale(Board.Size);`.

`ColorHuntSettings.cs`: delete `FrameColor`, `PlateColor`, `SkyTop`, `SkyBottom`.

`ColorHuntCamera.cs`:

```csharp
using Project.Juice;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's puzzle camera (yapbozSahne.ts boyutla, kasasız): straight at the board; the frame's outer width fills 98.5 % of the screen
    /// width unless frame + ball strip do not fit the band between the HUD and the buttons (then the band decides). Applies the shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ColorHuntCamera : MonoBehaviour
    {
        public ColorHuntSettings S;
        [Tooltip("World position of the board's game root (PotaFlow places the board away from the origin).")]
        public Vector3 Anchor;
        [Tooltip("HUD band top in Pota CSS px; < 0 = the counters' bottom + 10 under the safe area (renkAviFaz.ts boyutla).")]
        public float TopCss = -1f;
        [Tooltip("Button band under the board in Pota CSS px (renkAviFaz.ts dugmePayi).")]
        public float BottomCss = PotaLook.ButtonBandCss;

        private Camera _cam;
        private CameraShake _shake;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _shake = GetComponent<CameraShake>();
        }

        private void LateUpdate()
        {
            transform.position = FitFor(_cam.fieldOfView, _cam.aspect) + (_shake != null ? _shake.Offset : Vector3.zero);
            transform.rotation = Quaternion.identity;
        }

        /// <summary>The HUD band's top in CSS px now (safe area included).</summary>
        public float TopCssNow => TopCss >= 0f ? TopCss : PotaLook.HudTopCss(SafeTopCss());

        /// <summary>This camera's fit (valid while the component is disabled: PotaFlow flies to it).</summary>
        public Vector3 FitFor(float fov, float aspect) => FitPosition(fov, aspect, Anchor, TopCssNow, BottomCss);

        public static Vector3 FitPosition(float fov, float aspect, Vector3 anchor) =>
            FitPosition(fov, aspect, anchor, PotaLook.HudTopCss(0f), PotaLook.ButtonBandCss);

        public static Vector3 FitPosition(float fov, float aspect, Vector3 anchor, float topCss, float bottomCss) =>
            PotaLook.CameraPosition(anchor, PotaLook.CameraFit(aspect, topCss, bottomCss, fov));

        /// <summary>The screen's top safe inset in Pota CSS px (the phone is 390 CSS px wide).</summary>
        public static float SafeTopCss() =>
            Screen.width <= 0 ? 0f : (Screen.height - Screen.safeArea.yMax) * PotaLook.PhoneCssWidth / Screen.width;
    }
}
```

- [ ] **Step 6: The Color Hunt scene**

`SceneParts.cs` (add `using UnityEngine.Rendering.Universal;`):

```csharp
        public static Volume CreateGlobalVolume(VolumeProfile profile, string name = "Global Volume", float weight = 1f)
        {
            var vol = new GameObject(name).AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight = weight;
            vol.sharedProfile = profile;
            return vol;
        }

        /// <summary>URP post is per camera and off by default: without this no Volume does anything (found 2026-09-30).</summary>
        public static void EnablePost(Camera cam) => cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
```

`ColorHuntSceneTool.cs`: replace `CreateSun` and `ApplyAmbient`, add `CreateCamera` and `ApplyPuzzleLighting`, and the body of `Build`
between `var scene = …` and `AIToolsCommon.EnsureFolder(…)` (add `using Project.Flow;`):

```csharp
        /// <summary>The puzzle camera: 38°, straight on (yapbozSahne.ts), sky #8ec9f2 (saha.ts), post on.</summary>
        public static Camera CreateCamera()
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = PotaLook.Fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PotaLook.Sky;
            cam.transform.position = new Vector3(0f, -1.2f, -25.35f);
            SceneParts.EnablePost(cam);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraShake>();
            return cam;
        }

        /// <summary>saha.ts sun: #fff4e0, 2.1 (÷π for URP), from (−24, 26, 24) toward (3, 2, −4), shadows.</summary>
        public static Light CreateSun()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = PotaLook.SunColor;
            light.intensity = PotaLook.SunIntensity * PotaLook.ThreeToUnityLight;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            lightGo.transform.rotation = Quaternion.LookRotation(PotaLook.SunDirection);
            return light;
        }

        /// <summary>saha.ts hemisphere + room environment as URP trilight ambient; fog #9ed3f5 30–130. RenderSettings colours are sRGB.</summary>
        public static void ApplyPuzzleLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = PotaLook.AmbientSky.gamma;
            RenderSettings.ambientEquatorColor = PotaLook.AmbientEquator.gamma;
            RenderSettings.ambientGroundColor = PotaLook.AmbientGround.gamma;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = PotaLook.Fog;
            RenderSettings.fogStartDistance = PotaLook.FogStart;
            RenderSettings.fogEndDistance = PotaLook.FogEnd;
        }
```

`Build()` body:

```csharp
            var cam = CreateCamera();
            var shake = cam.GetComponent<CameraShake>();
            var fit = cam.gameObject.AddComponent<ColorHuntCamera>();
            fit.S = settings;
            CreateSun();
            SceneParts.CreateGlobalVolume(settings.PuzzleProfile, "Puzzle Volume");

            var uiRoot = new GameObject("UI");
            var parts = BuildGame(settings, cam, shake, null, uiRoot.transform);
            SceneParts.CreateEventSystem(uiRoot.transform);
            var stage = new GameObject("Stage").AddComponent<PuzzleStage>();
            stage.Game = parts.Game;
            ApplyPuzzleLighting();
```

(`VolumeProfilePath`, `CreateFill` stay: PotaFlow still uses `CreateFill` until Task 10.)

- [ ] **Step 7: Bake, build, test**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.GiantHoopTests Project.Tests.ColorHuntStagingTests Project.Tests.PotaRoomBakerTests Project.Tests.BallStreamTests`
Expected: all PASS.
Run: `~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_potalook_import","parameters":{}}'`
Expected: `room reflection Assets/_Project/Games/Pota/Art/PotaLook/PotaRoom.exr 256²`. If the bake throws or the EXR is black, fall back
to rendering the six faces with a temporary 90° camera into an HDR `Cubemap` asset (same room, same layer) and ledger the ruling.
Run: `execute_custom_tool ai_colorhunt_build_scene`, then `ai_potaflow_build_scene`.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile` → Expected: all PASS (incl. `PotaLookAssetsTests` 8/8).
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → Expected: all PASS (incl. `ColorHuntScenePlayTests`, PotaFlow 14/14).

- [ ] **Step 8: Look at it**

`manage_editor play` on `ColorHunt.unity`, game-view screenshot to `Temp/mcp_out`, `manage_editor stop`.
Expected: the board inside the orange frame filling the width, frosted glass with the pano drawing behind the blocks, the rim and net
below with the court under them, sky blue behind; blocks show room-window highlights. Read the image; fix before committing if the
glass draws over the blocks (transparent sort) or the reflections are black.

- [ ] **Step 9: Commit**

```bash
git add Assets/_Project/Games/Pota ProjectSettings/TagManager.asset
git commit -m "feat: Color Hunt board inside Pota's giant hoop — DEV_POTA frame, court, baked room reflection, Pota camera fit, post on

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 6: HUD in Pota's style, stickers flying to their box, the count dropping per arrival

**Files:**
- Rewrite: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntHud.cs`
- Modify: `PotaLook.cs` (HUD colours, `CubicBezier`, `ItemFlight`), `ColorHuntSettings.cs` (+`ItemFlyTime`), `ColorHuntGame.cs`
  (shown-target model, `CountItem` replaces `FlyItem`, no `_itemMat`), `ColorHuntFx.cs` (flyers removed)
- Test: `Tests/EditMode/PotaLookTests.cs` (+2), `Tests/PlayMode/ColorHuntHudPlayTests.cs` (+3)

**Interfaces:**
- Consumes: `ColorHuntCamera.SafeTopCss()` (Task 5), `PotaLook` layout constants (Task 2), `ColorHuntGame.Cam/BoardRoot`.
- Produces: `ColorHuntHud.Css` (canvas units per CSS px), `ColorHuntHud.FlyItem(int color, Vector3 world, Action onArrive) → bool`,
  `ColorHuntHud.FlyingCount`, `ColorHuntHud.TargetText(int color)`; unchanged public API: `Bind`, `RetryOverride`, `SetRetryLabel`,
  `HideResult`, `ShowHint`, `SetIntro(p)`, `SetIntro(ammo, rest)`, `AmmoViewportPoint`, `PopAmmo`, `Create`; object names
  `AmmoPanel/Ammo`, `TargetPanel`, `RotateLeft`, `RotateRight`, `Hint`, `ResultPanel/Card/Retry/Label`.
- Produces on `ColorHuntGame`: `int ShownTarget(int color)`, `int ItemsInFlight`; `TargetsChanged` now fires when the shown counts change.
- Produces on `PotaLook`: `HudNavy, HudNavy2, HudPanelTop, HudButtonTop, HudGold, HudGold2, HudGreen, HudRed, HudOrange, HudOrangeTop,
  HudHint`; `float CubicBezier(float x1, float y1, float x2, float y2, float x)`; `FlightPose ItemFlight(float t)` (`Along`, `Lift` CSS px, `Scale`).

- [ ] **Step 1: Write the failing tests**

Append to `PotaLookTests.cs`:

```csharp
        [Test]
        public void CubicBezier_IsCssTiming()
        {
            Assert.AreEqual(0f, PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 0f), 1e-5f);
            Assert.AreEqual(1f, PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 1f), 1e-5f);
            Assert.AreEqual(0.5f, PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 0.5f), 1e-4f, "symmetric ease-in-out");
            Assert.Less(PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 0.25f), 0.2f, "slow start");
            Assert.AreEqual(0.25f, PotaLook.CubicBezier(1f / 3f, 1f / 3f, 2f / 3f, 2f / 3f, 0.25f), 1e-4f, "control points on the diagonal = linear");
        }

        [Test]
        public void ItemFlight_FollowsPotasKeyframes()
        {
            var a = PotaLook.ItemFlight(0f);
            Assert.AreEqual(0f, a.Along, 1e-6f);
            Assert.AreEqual(0.9f, a.Scale, 1e-5f);
            Assert.AreEqual(0f, a.Lift, 1e-5f);
            var b = PotaLook.ItemFlight(1f);
            Assert.AreEqual(1f, b.Along, 1e-4f);
            Assert.AreEqual(0.5f, b.Scale, 1e-3f);
            Assert.AreEqual(0f, b.Lift, 1e-2f);
            float t = 0f;
            while (PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, t) < 0.3f) t += 1e-4f;
            var k = PotaLook.ItemFlight(t);
            Assert.AreEqual(0.3f, k.Along, 2e-3f, "30 % of the way at the 0.3 keyframe");
            Assert.AreEqual(40f, k.Lift, 0.3f, "40 px up");
            Assert.AreEqual(1.35f, k.Scale, 3e-3f);
        }
```

Append to `ColorHuntHudPlayTests.cs` (inside the class; uses its `LoadPlaying`/`WaitUntil`):

```csharp
        private static int Remaining(ColorHuntGame game, int color)
        {
            foreach (var t in game.Board.Targets) if (t.Color == color) return Mathf.Max(0, t.Remaining);
            return 0;
        }

        /// <summary>The cheapest affordable block of a colour that is still wanted.</summary>
        private static int CheapTargetCell(ColorHuntGame game, out int color)
        {
            int best = -1, bestHp = int.MaxValue;
            color = -1;
            var b = game.Board;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var c = b.Cells[i];
                if (c.Hp > b.Ammo || c.Hp >= bestHp || Remaining(game, c.Color) <= 0) continue;
                bool wanted = false;
                foreach (var t in b.Targets) wanted |= t.Color == c.Color;
                if (!wanted) continue;
                best = i; bestHp = c.Hp; color = c.Color;
            }
            return best;
        }

        [UnityTest]
        public IEnumerator TargetCount_DropsPerArrival_ThenMatchesTheRules()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            int cell = CheapTargetCell(game, out int color);
            Assert.GreaterOrEqual(cell, 0, "no affordable target block on this board");
            int before = game.ShownTarget(color);
            Assert.AreEqual("broken", game.Tap(cell));
            bool lagged = false, flew = false;
            float t = 0f;
            while ((game.Busy || game.ItemsInFlight > 0) && t < 8f)
            {
                int rules = Remaining(game, color), shown = game.ShownTarget(color);
                Assert.GreaterOrEqual(shown, rules, "the shown count never goes below the rules");
                Assert.LessOrEqual(shown, before);
                lagged |= shown > rules;
                flew |= hud.FlyingCount > 0;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
            Assert.IsTrue(flew, "the sticker flew to the HUD");
            Assert.IsTrue(lagged, "the count waits for the sticker (renkAviFaz.ts hedefAzalt)");
            Assert.AreEqual(Remaining(game, color), game.ShownTarget(color), "settled and nothing flying: shown = rules");
            Assert.AreEqual(game.ShownTarget(color).ToString(), hud.TargetText(color));
        }

        [UnityTest]
        public IEnumerator NewRound_WhileAStickerFlies_KeepsTheNewCounts()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            int cell = CheapTargetCell(game, out _);
            Assert.GreaterOrEqual(cell, 0);
            game.Tap(cell);
            yield return WaitUntil(() => hud.FlyingCount > 0, 4f);
            Assert.Greater(hud.FlyingCount, 0);
            game.NewRound();
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.8f) yield return null;
            foreach (var tg in game.Board.Targets)
                Assert.AreEqual(Mathf.Max(0, tg.Remaining), game.ShownTarget(tg.Color), "an old flight must not lower the new board's count");
        }

        [UnityTest]
        public IEnumerator Layout_IsPotas_CountersOnTop_ArrowsRightUnderTheBackboard()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            yield return null;
            float k = ColorHuntHud.Css;
            var ammo = (RectTransform)hud.transform.Find("AmmoPanel");
            var targets = (RectTransform)hud.transform.Find("TargetPanel");
            var right = (RectTransform)hud.transform.Find("RotateRight");
            var left = (RectTransform)hud.transform.Find("RotateLeft");
            Assert.AreEqual(14f * k, ammo.anchoredPosition.x, 0.5f, "14 px from the left");
            Assert.AreEqual(-14f * k, targets.anchoredPosition.x, 0.5f, "14 px from the right");
            Assert.AreEqual(63f * k, ammo.sizeDelta.y, 1f, ".sayac height");
            Assert.AreEqual(64f * k, right.sizeDelta.x, 0.5f, ".dondur 64 px");
            Assert.AreEqual(-14f * k, right.anchoredPosition.x, 0.5f);
            Assert.AreEqual(right.anchoredPosition.y - 72f * k, left.anchoredPosition.y, 0.5f, "stacked: right on top, 8 px gap");
            var cam = game.Cam;
            var vp = cam.WorldToViewportPoint(game.transform.TransformPoint(new Vector3(0f, -4.75f, -0.45f)));
            float canvasH = ((RectTransform)hud.transform).rect.height;
            float expectedTop = Mathf.Min((1f - vp.y) * canvasH / k + 34f, canvasH / k - 150f);
            Assert.AreEqual(-expectedTop * k, right.anchoredPosition.y, 2f * k, "34 px under the backboard (index.html #yAlt)");
        }
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests`
Expected: exit 2, `error CS0117: 'PotaLook' does not contain a definition for 'CubicBezier'` (PlayMode tests: `ShownTarget`, `FlyingCount`).

- [ ] **Step 3: `PotaLook` additions**

```csharp
        // index.html :root and HUD rules
        public static readonly Color HudNavy = Hex(0x142850), HudNavy2 = Hex(0x1f3a6e), HudPanelTop = Hex(0x24407a), HudButtonTop = Hex(0x2b4d91),
            HudGold = Hex(0xffc83d), HudGold2 = Hex(0xb3600a), HudGreen = Hex(0x2fb36b), HudRed = Hex(0xff6b5e),
            HudOrange = Hex(0xff7a1a), HudOrangeTop = Hex(0xffb46b), HudHint = Hex(0x103055);

        public struct FlightPose { public float Along, Lift, Scale; }

        /// <summary>
        /// renkAviFaz.ts esyaUcur (WAAPI): keyframes 0 → scale 0.9; 0.3 → 30 % of the way, 40 CSS px up, scale 1.35; 1 → there, 0.5;
        /// the whole flight eased by cubic-bezier(.45, 0, .55, 1). <paramref name="t"/> = elapsed / esyaUcus.
        /// </summary>
        public static FlightPose ItemFlight(float t)
        {
            float p = CubicBezier(0.45f, 0f, 0.55f, 1f, Mathf.Clamp01(t));
            if (p < 0.3f)
            {
                float u = p / 0.3f;
                return new FlightPose { Along = 0.3f * u, Lift = 40f * u, Scale = Mathf.Lerp(0.9f, 1.35f, u) };
            }
            float w = (p - 0.3f) / 0.7f;
            return new FlightPose { Along = 0.3f + 0.7f * w, Lift = 40f * (1f - w), Scale = Mathf.Lerp(1.35f, 0.5f, w) };
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) at time x: bisection on the x curve (24 steps ≈ 6e-8).</summary>
        public static float CubicBezier(float x1, float y1, float x2, float y2, float x)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 24; i++)
            {
                float s = (lo + hi) * 0.5f;
                if (Bezier(x1, x2, s) < x) lo = s; else hi = s;
            }
            return Bezier(y1, y2, (lo + hi) * 0.5f);
        }

        private static float Bezier(float a, float b, float s)
        {
            float u = 1f - s;
            return 3f * u * u * s * a + 3f * u * s * s * b + s * s * s;
        }
```

`ColorHuntSettings.cs` (Timing): `[Tooltip("renkAviFaz.ts RENK_AVI_HIS.esyaUcus: sticker flight to the HUD, real time.")] public float ItemFlyTime = 0.55f;`

- [ ] **Step 4: The shown-target model in the game**

`ColorHuntGame.cs`:
- Fields: `private readonly Dictionary<int, int> _shown = new Dictionary<int, int>(); private int _flights, _flightGen; private bool _targetsPending;`
  and delete `_itemMat` (and its creation block in `Start`, and `if (Fx != null) Fx.ItemMaterial = _itemMat;`).
- Members:

```csharp
        public int ItemsInFlight => _flights;

        /// <summary>
        /// The target count the HUD shows (renkAviFaz.ts hedefAzalt / hedefHudYaz): one less per sticker that lands, never below the
        /// rules, and equal to the rules once the board has settled with no sticker in the air.
        /// </summary>
        public int ShownTarget(int color) => _shown.TryGetValue(color, out int v) ? v : Remaining(color);

        private int Remaining(int color)
        {
            if (Board != null) foreach (var t in Board.Targets) if (t.Color == color) return Mathf.Max(0, t.Remaining);
            return 0;
        }

        private void ResetShownTargets()
        {
            _shown.Clear();
            _flights = 0;
            _flightGen++;                              // a sticker still flying from the old board changes nothing
            _targetsPending = false;
            if (Board != null) foreach (var t in Board.Targets) _shown[t.Color] = Mathf.Max(0, t.Remaining);
            Raise(TargetsChanged);
        }

        /// <summary>A burst block of a target colour: its sticker flies to the HUD and lowers the count on arrival (at once if it cannot fly).</summary>
        private void CountItem(BlockView v, Vector3 world)
        {
            int color = v.Block.Color;
            if (!_shown.ContainsKey(color)) return;
            int gen = _flightGen;
            if (Hud != null && Hud.FlyItem(color, world, () =>
                {
                    if (gen != _flightGen) return;
                    _flights--;
                    LowerShown(color);
                    SyncTargetsIfSettled();
                }))
                _flights++;
            else LowerShown(color);
        }

        private void LowerShown(int color)
        {
            _shown[color] = Mathf.Max(Remaining(color), _shown[color] - 1);
            Raise(TargetsChanged);
        }

        private void SyncTargetsIfSettled()
        {
            if (Busy || _flights > 0 || Board == null) return;
            _targetsPending = false;
            bool changed = false;
            foreach (var t in Board.Targets)
            {
                int r = Mathf.Max(0, t.Remaining);
                if (_shown.TryGetValue(t.Color, out int s) && s == r) continue;
                _shown[t.Color] = r;
                changed = true;
            }
            if (changed) Raise(TargetsChanged);
        }
```

- `Prepare`: replace `Raise(TargetsChanged);` with `ResetShownTargets();`.
- `Tap`: after `_checkNeeded = true;` add `_targetsPending = true;`.
- `Update`: after `if (Current == State.Playing && !Busy && _checkNeeded) AfterSettle();` add
  `if (_targetsPending && !Busy && _flights == 0) SyncTargetsIfSettled();`.
- `FinishSalvo`: `FlyItem(view, pos);` → `CountItem(view, pos);`; delete `Raise(TargetsChanged);`.
- `StartStep`: `FlyItem(v, p);` → `CountItem(v, p);`; delete `Raise(TargetsChanged);`.
- Delete the old `FlyItem` method.

`ColorHuntFx.cs`: delete `ItemMaterial`, the `Flyer` class, `_flyers`, `_flyerPool`, `_flyerRoot`, `FlyItem`, `NewFlyer` and the flyer loop in `Update`.

- [ ] **Step 5: Rewrite the HUD**

`ColorHuntHud.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's puzzle HUD (index.html #hud2 / #yAlt, renkAviFaz.ts): navy gradient counters with a gold border and lip — CEPHANE
    /// top-left, HEDEF top-right (sticker + count, green ✓ when done) — the rotate arrows stacked on the right 34 px under the
    /// backboard, stickers flying from a burst block to their box (the count drops as each lands), the result overlay with retry.
    /// Built in code; sizes are Pota's CSS px on a 390-px-wide phone, <see cref="Css"/> canvas units each.
    /// </summary>
    public class ColorHuntHud : MonoBehaviour
    {
        /// <summary>Canvas units per Pota CSS px: the canvas is 1080 wide (width-matched), Pota's phone 390.</summary>
        public const float Css = 1080f / PotaLook.PhoneCssWidth;

        public ColorHuntGame Game;
        public ColorHuntSettings S;
        /// <summary>When set, the result panel's button calls this instead of dealing a new board (PotaFlow flies back to the basket).</summary>
        public Action RetryOverride;
        [Tooltip("Show the how-to-play hint (PotaFlow hides it: the giant hoop's net sits there).")]
        public bool ShowHint = true;

        // Serialized: the HUD is built once by the scene tool in Edit Mode and must survive scene load / Play Mode.
        [SerializeField] private Text _ammo;
        [SerializeField] private Text _hint;
        [SerializeField] private Text _resultTitle;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private RectTransform _targets;
        [SerializeField] private RectTransform _ammoPanel, _targetPanel, _right, _left, _flyers;
        [SerializeField] private Font _font;

        private class TargetRow { public int Color; public Image Icon; public Text Count, Tick; }
        private class Flyer { public RectTransform T; public Image Img; public Vector2 From, To; public float T0; public Action Done; public TargetRow Row; }

        private readonly List<TargetRow> _rows = new List<TargetRow>();
        private readonly Dictionary<Transform, float> _pops = new Dictionary<Transform, float>();
        private readonly List<Flyer> _flying = new List<Flyer>();
        private readonly Stack<Flyer> _flyerPool = new Stack<Flyer>();
        private float _introAmmo = 1f, _introRest = 1f;
        private Color _hintColor;
        private bool _hintCached;

        public int FlyingCount => _flying.Count;

        public void Bind(ColorHuntGame game)
        {
            Game = game;
            game.AmmoChanged += OnAmmo;
            game.TargetsChanged += OnTargets;
            game.RoundEnded += OnEnded;
            game.RoundStarted += OnStarted;
            WireButtons();
        }

        // Listeners added while the scene tool builds the HUD in Edit Mode are not saved with the scene: wire them here.
        private void WireButtons()
        {
            Wire("RotateLeft", () => { if (Game != null) Game.Rotate(Rotation.Counter); });
            Wire("RotateRight", () => { if (Game != null) Game.Rotate(Rotation.Clockwise); });
            Wire("ResultPanel/Card/Retry", Retry);
        }

        private void Wire(string path, UnityEngine.Events.UnityAction action)
        {
            var t = transform.Find(path);
            var button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) { Debug.LogWarning($"[ColorHuntHud] no button at {path}"); return; }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void Retry()
        {
            if (RetryOverride != null) { RetryOverride(); return; }
            if (Game != null) Game.NewRound();
        }

        public void SetRetryLabel(string text)
        {
            var t = transform.Find("ResultPanel/Card/Retry/Label");
            if (t != null) t.GetComponent<Text>().text = text;
        }

        public void HideResult()
        {
            if (_resultPanel != null) _resultPanel.SetActive(false);
        }

        /// <summary>Slides the HUD in (PotaFlow): p = 0 panels off-screen and hint hidden, p = 1 exactly home. Ease-out.</summary>
        public void SetIntro(float p) => SetIntro(p, p);

        /// <summary>Slides the ammo panel by <paramref name="ammo"/> and the rest (targets, arrows, hint) by <paramref name="rest"/>.</summary>
        public void SetIntro(float ammo, float rest)
        {
            _introAmmo = ammo;
            _introRest = rest;
            Layout();
        }

        private static float EaseOut(float p)
        {
            p = Mathf.Clamp01(p);
            return 1f - (1f - p) * (1f - p) * (1f - p);
        }

        /// <summary>
        /// Counters under the safe area 14 px in from the sides (#hud2), arrows on the right 34 px under the backboard but never lower than
        /// 150 px above the bottom (#yAlt), hint in the middle column; plus the intro offsets.
        /// </summary>
        private void Layout()
        {
            if (_ammoPanel == null) return;
            float top = Mathf.Max(PotaLook.HudTopPadCss, ColorHuntCamera.SafeTopCss()) * Css;
            float ea = EaseOut(_introAmmo), er = EaseOut(_introRest);
            _ammoPanel.anchoredPosition = new Vector2(14f * Css - 420f * (1f - ea), -top);
            _targetPanel.anchoredPosition = new Vector2(-14f * Css, -top + 320f * (1f - er));
            float arrows = ArrowsTopCss() * Css, drop = 340f * (1f - er);
            _right.anchoredPosition = new Vector2(-14f * Css, -arrows - drop);
            _left.anchoredPosition = new Vector2(-14f * Css, -arrows - 72f * Css - drop);
            if (_hint == null) return;
            if (!_hintCached) { _hintColor = _hint.color; _hintCached = true; }
            _hint.rectTransform.anchoredPosition = new Vector2(0f, -arrows - 6f * Css);
            var c = _hintColor;
            c.a *= ShowHint ? er : 0f;
            _hint.color = c;
        }

        /// <summary>index.html #yAlt top: min(backboard's outer bottom on screen + 34, screen − 10 − 140), CSS px.</summary>
        private float ArrowsTopCss()
        {
            float cssH = ((RectTransform)transform).rect.height / Css;
            float limit = cssH - 10f - 140f;
            var cam = Game != null ? Game.Cam : null;
            if (cam == null) return limit;
            var vp = cam.WorldToViewportPoint(Game.transform.TransformPoint(new Vector3(0f, -(PotaLook.PanoSize * 0.5f + PotaLook.FrameWidth), -PotaLook.FrontZ)));
            return vp.z <= 0f ? limit : Mathf.Min((1f - vp.y) * cssH + PotaLook.ButtonGapCss, limit);
        }

        /// <summary>The ammo number's position as a 0..1 fraction of the HUD canvas (bottom-left origin), for PotaFlow's badge.</summary>
        public Vector2 AmmoViewportPoint
        {
            get
            {
                if (_ammo == null) return new Vector2(0.15f, 0.93f);
                var canvasRt = (RectTransform)transform;
                Vector2 local = canvasRt.InverseTransformPoint(_ammo.rectTransform.position);
                var r = canvasRt.rect;
                return new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            }
        }

        /// <summary>index.html @keyframes pop on the ammo number (transition arrival).</summary>
        public void PopAmmo()
        {
            if (_ammo != null) _pops[_ammo.transform] = 0f;
        }

        public string TargetText(int color)
        {
            foreach (var row in _rows) if (row.Color == color) return row.Count.text;
            return null;
        }

        /// <summary>
        /// renkAviFaz.ts esyaUcur: the colour's sticker (56 px) flies from the block's screen point to its target icon in
        /// <see cref="ColorHuntSettings.ItemFlyTime"/> of real time; on arrival the icon and the count pop and <paramref name="onArrive"/> runs.
        /// False (nothing flies) when the colour has no box or the block is behind the camera.
        /// </summary>
        public bool FlyItem(int color, Vector3 world, Action onArrive)
        {
            var cam = Game != null ? Game.Cam : null;
            TargetRow row = null;
            foreach (var r in _rows) if (r.Color == color) row = r;
            if (cam == null || row == null || row.Icon.sprite == null || _flyers == null) return false;
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0f) return false;
            var canvasRt = (RectTransform)transform;
            var rect = canvasRt.rect;
            var f = _flyerPool.Count > 0 ? _flyerPool.Pop() : NewFlyer();
            f.From = new Vector2((vp.x - 0.5f) * rect.width, (vp.y - 0.5f) * rect.height);
            f.To = canvasRt.InverseTransformPoint(row.Icon.rectTransform.TransformPoint(row.Icon.rectTransform.rect.center));
            f.T0 = Time.unscaledTime;
            f.Done = onArrive;
            f.Row = row;
            f.Img.sprite = row.Icon.sprite;
            f.T.anchoredPosition = f.From;
            f.T.localScale = Vector3.one * 0.9f;
            f.T.gameObject.SetActive(true);
            _flying.Add(f);
            return true;
        }

        private Flyer NewFlyer()
        {
            var go = new GameObject("Sticker", typeof(RectTransform), typeof(Image), typeof(Shadow));
            go.transform.SetParent(_flyers, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * (56f * Css);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.4f);
            sh.effectDistance = new Vector2(0f, -3f * Css);
            return new Flyer { T = rt, Img = img };
        }

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<ColorHuntGame>();
            if (Game != null && Game.Board != null) OnStarted();
        }

        private void OnStarted()
        {
            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_hint != null) _hint.enabled = true;
            foreach (var f in _flying) { f.T.gameObject.SetActive(false); _flyerPool.Push(f); }
            _flying.Clear();
            BuildTargets();
            OnAmmo();
        }

        private void OnAmmo()
        {
            if (Game == null || _ammo == null) return;
            string s = Game.DisplayedAmmo.ToString();
            if (_ammo.text != s) _ammo.text = s;
        }

        private void OnTargets()
        {
            if (Game == null || Game.Board == null) return;
            foreach (var row in _rows)
            {
                int shown = Game.ShownTarget(row.Color);
                string s = shown.ToString();
                if (row.Count.text != s) { row.Count.text = s; _pops[row.Count.transform] = 0f; }
                bool done = shown == 0;
                row.Count.color = done ? PotaLook.HudGreen : PotaLook.HudGold;
                row.Tick.enabled = done;
            }
        }

        private void OnEnded(bool won, string reason)
        {
            if (_resultTitle != null)
            {
                _resultTitle.text = won ? "Hedef tamam!" : reason == "ammo" ? "Top bitti" : "Hamle kalmadı";
                _resultTitle.color = won ? PotaLook.HudGreen : PotaLook.HudRed;
            }
            if (_resultPanel != null) _resultPanel.SetActive(true);
        }

        private void Update()
        {
            if (Game != null && Game.Busy && _hint != null) _hint.enabled = false;
            float dt = Time.unscaledDeltaTime;
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                float t = (Time.unscaledTime - f.T0) / Mathf.Max(0.01f, S.ItemFlyTime);
                if (t >= 1f)
                {
                    f.T.gameObject.SetActive(false);
                    _flying.RemoveAt(i);
                    _flyerPool.Push(f);
                    _pops[f.Row.Icon.transform] = 0f;
                    f.Done?.Invoke();
                    continue;
                }
                var pose = PotaLook.ItemFlight(t);
                f.T.anchoredPosition = Vector2.Lerp(f.From, f.To, pose.Along) + new Vector2(0f, pose.Lift * Css);
                f.T.localScale = Vector3.one * pose.Scale;
            }
            if (_pops.Count == 0) return;
            var keys = new List<Transform>(_pops.Keys);
            foreach (var t in keys)
            {
                float p = _pops[t] + dt / 0.18f;                                   // @keyframes pop: 0.18 s, 1.25 at 40 %
                float k = p < 0.4f ? p / 0.4f : Mathf.Max(0f, (1f - p) / 0.6f);
                t.localScale = Vector3.one * (1f + 0.25f * k);
                if (p >= 1f) { t.localScale = Vector3.one; _pops.Remove(t); } else _pops[t] = p;
            }
        }

        private void LateUpdate() => Layout();

        /// <summary>#yHedefler: sticker 30 px + 5 + count 26 px (min 30), 10 px between targets; the panel is at least 120 wide.</summary>
        private void BuildTargets()
        {
            _rows.Clear();
            if (_targets == null || Game == null || Game.Board == null) return;
            foreach (Transform c in _targets) Destroy(c.gameObject);
            float x = 0f;
            foreach (var t in Game.Board.Targets)
            {
                var row = new TargetRow { Color = t.Color };
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Shadow));
                iconGo.transform.SetParent(_targets, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2((x + 15f) * Css, 0f);
                irt.sizeDelta = Vector2.one * (30f * Css);
                row.Icon = iconGo.GetComponent<Image>();
                row.Icon.preserveAspect = true;
                row.Icon.raycastTarget = false;
                var tex = S.ItemIcon(t.Color);
                if (tex != null) row.Icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                var sh = iconGo.GetComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
                sh.effectDistance = new Vector2(0f, -2f * Css);
                row.Count = MakeText(_targets, "Count", 26f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2((x + 35f) * Css, 0f),
                    new Vector2(40f * Css, 30f * Css), TextAnchor.MiddleLeft, PotaLook.HudGold, Game.ShownTarget(t.Color).ToString());
                row.Tick = MakeText(_targets, "Tick", 18f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2((x + 35f + 30f) * Css, 0f),
                    new Vector2(20f * Css, 30f * Css), TextAnchor.MiddleLeft, PotaLook.HudGreen, "✓");
                row.Tick.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");       // Lilita One has no ✓
                row.Tick.enabled = false;
                x += 65f + 10f;
                _rows.Add(row);
            }
            float content = Mathf.Max(120f, x - 10f);
            _targets.sizeDelta = new Vector2(content * Css, 32f * Css);
            _targetPanel.sizeDelta = new Vector2((content + 28f + 6f) * Css, 63.2f * Css);
            OnTargets();
        }

        // ---------- construction ----------

        public static ColorHuntHud Create(Transform parent, ColorHuntSettings s)
        {
            var font = s.Font != null ? s.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("ColorHuntHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;                     // Pota's CSS is width-bound
            var hud = canvasGo.AddComponent<ColorHuntHud>();
            hud.S = s;
            hud._font = font;
            var root = canvasGo.transform;

            // .sayac: gradient #24407a → #142850, 3 px gold border, radius 18, inset top highlight, 3 px #b3600a lip, soft shadow.
            var face = PanelSprite(128, Mathf.RoundToInt(18f * Css), PotaLook.HudPanelTop, PotaLook.HudNavy, PotaLook.HudGold, Mathf.RoundToInt(3f * Css), 0.18f, Mathf.RoundToInt(2f * Css));
            var lip = PanelSprite(128, Mathf.RoundToInt(18f * Css), PotaLook.HudGold2, PotaLook.HudGold2, PotaLook.HudGold2, 0, 0f, 0);
            var soft = ShadowSprite(128, Mathf.RoundToInt(18f * Css), Mathf.RoundToInt(12f * Css));

            hud._ammoPanel = Counter(root, "AmmoPanel", new Vector2(0f, 1f), new Vector2(98f, 63f), face, lip, soft);
            hud.MakeText(hud._ammoPanel, "Label", 11f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15.6f * Css),
                new Vector2(90f * Css, 14f * Css), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f), "CEPHANE");
            hud._ammo = hud.MakeText(hud._ammoPanel, "Ammo", 30f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -37.95f * Css),
                new Vector2(90f * Css, 32f * Css), TextAnchor.MiddleCenter, PotaLook.HudGold, "0");

            hud._targetPanel = Counter(root, "TargetPanel", new Vector2(1f, 1f), new Vector2(154f, 63.2f), face, lip, soft);
            hud.MakeText(hud._targetPanel, "Label", 11f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15.6f * Css),
                new Vector2(140f * Css, 14f * Css), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f), "HEDEF");
            var targetsGo = new GameObject("Targets", typeof(RectTransform));
            targetsGo.transform.SetParent(hud._targetPanel, false);
            hud._targets = (RectTransform)targetsGo.transform;
            hud._targets.anchorMin = hud._targets.anchorMax = new Vector2(0.5f, 1f);
            hud._targets.pivot = new Vector2(0.5f, 0.5f);
            hud._targets.anchoredPosition = new Vector2(0f, -39.2f * Css);
            hud._targets.sizeDelta = new Vector2(120f * Css, 32f * Css);

            // .dondur: 64 px circle, 4 px gold border, gradient #2b4d91 → #1f3a6e, 5 px #b3600a lip; pressed: #ffb46b → #ff7a1a.
            int d = Mathf.RoundToInt(64f * Css), bw = Mathf.RoundToInt(4f * Css);
            var circle = CircleSprite(d, PotaLook.HudButtonTop, PotaLook.HudNavy2, PotaLook.HudGold, bw);
            var pressed = CircleSprite(d, PotaLook.HudOrangeTop, PotaLook.HudOrange, PotaLook.HudGold, bw);
            var circleLip = CircleSprite(d, PotaLook.HudGold2, PotaLook.HudGold2, PotaLook.HudGold2, 0);
            hud._right = hud.ArrowButton(root, "RotateRight", circle, pressed, circleLip, s, false);
            hud._left = hud.ArrowButton(root, "RotateLeft", circle, pressed, circleLip, s, true);

            // #yIpucu: 13 px bold #103055 in the middle column.
            hud._hint = hud.MakeText(root, "Hint", 13f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(210f * Css, 40f * Css),
                TextAnchor.UpperCenter, PotaLook.HudHint, "Bloğa dokun: canı kadar top harcanır.\nOklar tahtayı çevirir, bedava.");
            hud._hint.horizontalOverflow = HorizontalWrapMode.Wrap;

            var flyers = new GameObject("Flyers", typeof(RectTransform));
            flyers.transform.SetParent(root, false);
            hud._flyers = (RectTransform)flyers.transform;
            hud._flyers.anchorMin = Vector2.zero;
            hud._flyers.anchorMax = Vector2.one;
            hud._flyers.offsetMin = hud._flyers.offsetMax = Vector2.zero;

            // Result overlay (Pota's result screens are a later step): dim + a counter-style card with the retry button.
            var overlay = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(root, false);
            var ort = (RectTransform)overlay.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = ort.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var card = Counter(overlay.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(260f, 150f), face, lip, soft);
            hud._resultTitle = hud.MakeText(card, "Title", 28f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f * Css),
                new Vector2(250f * Css, 40f * Css), TextAnchor.MiddleCenter, PotaLook.HudGold, "");
            var retry = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
            retry.transform.SetParent(card, false);
            var rrt = (RectTransform)retry.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 40f * Css);
            rrt.sizeDelta = new Vector2(150f * Css, 44f * Css);
            var rimg = retry.GetComponent<Image>();
            rimg.sprite = PanelSprite(128, Mathf.RoundToInt(14f * Css), PotaLook.HudOrangeTop, PotaLook.HudOrange, PotaLook.HudGold2, Mathf.RoundToInt(3f * Css), 0f, 0);
            rimg.type = Image.Type.Sliced;
            hud.MakeText(rrt, "Label", 20f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, rrt.sizeDelta, TextAnchor.MiddleCenter, Color.white, "Yeni tahta");
            hud._resultPanel = overlay;
            overlay.SetActive(false);
            return hud;
        }

        /// <summary>A .sayac-style box: soft shadow (0 6px 12px), lip (0 3px 0 #b3600a), face. Size in CSS px.</summary>
        private static RectTransform Counter(Transform parent, string name, Vector2 anchor, Vector2 sizeCss, Sprite face, Sprite lip, Sprite soft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = sizeCss * Css;
            Layer(rt, "Shadow", soft, new Color(0f, 0f, 0f, 0.25f), new Vector2(0f, -6f * Css), 12f * Css);
            Layer(rt, "Lip", lip, Color.white, new Vector2(0f, -3f * Css), 0f);
            Layer(rt, "Face", face, Color.white, Vector2.zero, 0f);
            return rt;
        }

        private static Image Layer(RectTransform parent, string name, Sprite sprite, Color color, Vector2 offset, float grow)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offset - Vector2.one * grow;
            rt.offsetMax = offset + Vector2.one * grow;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private RectTransform ArrowButton(Transform root, string name, Sprite face, Sprite pressed, Sprite lip, ColorHuntSettings s, bool left)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Button));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = Vector2.one * (64f * Css);
            Layer(rt, "Shadow", ShadowSprite(128, 60, 20), new Color(0f, 0f, 0f, 0.3f), new Vector2(0f, -10f * Css), 9f * Css).type = Image.Type.Simple;
            Layer(rt, "Lip", lip, Color.white, new Vector2(0f, -5f * Css), 0f).type = Image.Type.Simple;
            var faceImg = Layer(rt, "Face", face, Color.white, Vector2.zero, 0f);
            faceImg.type = Image.Type.Simple;
            faceImg.raycastTarget = true;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Shadow));
            icon.transform.SetParent(rt, false);
            var irt = (RectTransform)icon.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = Vector2.one * (40f * Css);
            irt.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);                 // #dondurSol img { scaleX(-1) }
            var iimg = icon.GetComponent<Image>();
            iimg.raycastTarget = false;
            iimg.preserveAspect = true;
            if (s.ArrowIcon != null) iimg.sprite = Sprite.Create(s.ArrowIcon, new Rect(0, 0, s.ArrowIcon.width, s.ArrowIcon.height), new Vector2(0.5f, 0.5f));
            var ish = icon.GetComponent<Shadow>();
            ish.effectColor = new Color(0f, 0f, 0f, 0.25f);
            ish.effectDistance = new Vector2(0f, -2f * Css);
            var button = go.GetComponent<Button>();
            button.targetGraphic = faceImg;
            button.transition = Selectable.Transition.SpriteSwap;
            var ss = button.spriteState;
            ss.pressedSprite = pressed;
            button.spriteState = ss;
            return rt;
        }

        private Text MakeText(Transform parent, string name, float sizeCss, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, TextAnchor align, Color color, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = Mathf.RoundToInt(sizeCss * Css);
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ---------- generated sprites (canvas units = pixels) ----------

        /// <summary>Rounded rectangle: vertical gradient, border, optional inset highlight under the top border; 9-sliced.</summary>
        public static Sprite PanelSprite(int size, int radius, Color top, Color bottom, Color border, int borderWidth, float highlight, int highlightWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                    float aa = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var fill = Color.Lerp(bottom, top, y / (size - 1f));
                    if (highlight > 0f && y >= size - borderWidth - highlightWidth) fill = Color.Lerp(fill, Color.white, highlight);
                    var c = Color.Lerp(border, fill, inner);
                    c.a = aa;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            float b = radius + borderWidth + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Soft rounded shadow (black, alpha fades over <paramref name="blur"/>), 9-sliced; tinted by the Image colour.</summary>
        public static Sprite ShadowSprite(int size, int radius, int blur)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius + blur) + blur;
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + blur) / (2f * blur)));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            float b = radius + 2f * blur + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static float RoundedDistance(float x, float y, int size, int radius)
        {
            float hx = size * 0.5f - radius, hy = size * 0.5f - radius;
            float dx = Mathf.Abs(x - size * 0.5f) - hx, dy = Mathf.Abs(y - size * 0.5f) - hy;
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius;
        }

        /// <summary>Circle with a vertical gradient and a border.</summary>
        public static Sprite CircleSprite(int size, Color top, Color bottom, Color border, int borderWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) - r;
                    float aa = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var col = Color.Lerp(border, Color.Lerp(bottom, top, y / (size - 1f)), inner);
                    col.a = aa;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
```

Remove the now-unused `TargetScreenPoint` and `PopTarget` callers (only the old `FlyItem` used them). `FlowBadge`/`PotaFlowDirector`
keep using `Hud.SetIntro`, `AmmoViewportPoint`, `PopAmmo`, `HideResult`, `SetRetryLabel`, `ShowHint` — unchanged.

- [ ] **Step 6: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests` → Expected: 11/11 PASS.
Rebuild both scenes (`ai_colorhunt_build_scene`, `ai_potaflow_build_scene`), then
`~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → Expected: all PASS (incl. the 3 new HUD tests, `ColorHuntStagingPlayTests`,
`PotaFlowPlayTests` 14/14).

- [ ] **Step 7: Look at it**

Play `ColorHunt.unity`, screenshot the game view before and 0.3 s after a tap (`ai_colorhunt_play {"action":"tap","cell":…}` then
`slowmo` 0.2 for the mid-flight frame), stop. Expected: CEPHANE top-left and HEDEF top-right in navy-gold boxes with Lilita numbers,
the two round arrows stacked on the right just under the orange frame, a sticker mid-air between the block and its HEDEF icon.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Games/Pota
git commit -m "feat: Color Hunt HUD in Pota's style; stickers fly to their target box and the count drops per arrival

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 7: Pota's burst — flashes, ring, glossy shards, star sparks, puff, hot points, light, paper confetti

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaFx.cs` (pure formulas), `Assets/_Project/Games/Pota/Shaders/PotaAdditive.shader`
- Rewrite: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntFx.cs`
- Modify: `ColorHuntSettings.cs` (+`FlashSize, FlashSizePerDepth, LightIntensityScale, PointsBase, PointsPerDepth, PointsMax, ConfettiCount`;
  − `SparksBase`), `ColorHuntGame.cs` (new Fx calls, `ConfettiOrigin` and `_shardMesh` removed), `BoardBuilder.cs` (− `SoftCircle`, `GradientTexture`)
- Test: `Tests/EditMode/PotaFxCurveTests.cs` (new), `Tests/PlayMode/PotaFxPlayTests.cs` (new), `Tests/EditMode/ColorHuntStagingTests.cs`
  (− `ConfettiOrigin_FollowsTheGameRoot`, now `PotaFxPlayTests.Confetti_FallsFromAboveTheBoard_WhereverItIs`)

**Interfaces:**
- Consumes: `ColorHuntSettings.SoftDisc/SoftRing/StarSprite` (Task 1), `PotaLook` (Task 2), `ColorHuntGame.BoardRoot` (scaled, Task 5).
- Produces (`ColorHuntFx`, positions in board cells, tones sRGB): `Build()`, `SetBoard(Transform board)`, `Burst(Vector2 cell, Color tone, int depth)`,
  `Glow(Vector2 cell, Color linearTone, float flash, float ring, float light, float lightTime)`,
  `Particles(Vector2 cell, Color tone, int count, float speed, float size)`, `Puff(Vector2 cell, float strength)`, `Confetti(int n, int count)`, `Clear()`.
- Produces (`PotaFx`): `Grow`, `StarSize`, `ShardScale`, `ShardCount(ColorHuntSettings, int)`, `StarCount(int)`, `PointCount(ColorHuntSettings, int)`,
  `NewShard(System.Random, int index, int depth)`, `NewStar(System.Random, int index)`, `NewPoint(System.Random, float speed, float size)`,
  `PointWorldSize(float size, float fov, float pixelHeight)`, `PointSprite(int size)`, `Icosphere(float radius)`; shader `Project/PotaAdditive`
  (`_BaseMap`, `_Intensity`; `Blend SrcAlpha One`, vertex colour × texture × intensity).

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Games/Pota/Tests/EditMode/PotaFxCurveTests.cs`:

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class PotaFxCurveTests
    {
        [Test]
        public void Grow_EasesOut()
        {
            Assert.AreEqual(0f, PotaFx.Grow(0f), 1e-6f);
            Assert.AreEqual(1f, PotaFx.Grow(1f), 1e-6f);
            Assert.AreEqual(1f - Mathf.Pow(0.5f, 2.2f), PotaFx.Grow(0.5f), 1e-6f, "patlamaEfekt.ts flaslariIlerlet");
        }

        [Test]
        public void StarSize_BlinksInsideItsEnvelope()
        {
            Assert.AreEqual(Mathf.Sin(0.12f * Mathf.PI) * 0.75f, PotaFx.StarSize(0f), 1e-5f, "visible at birth");
            for (float a = 0f; a <= 1f; a += 0.01f) Assert.That(PotaFx.StarSize(a), Is.InRange(-1e-4f, 1f));
            Assert.Less(PotaFx.StarSize(1f), 0.25f, "gone at the end");
        }

        [Test]
        public void ShardScale_HoldsThenShrinksTo0Point35()
        {
            Assert.AreEqual(1f, PotaFx.ShardScale(0f), 1e-6f);
            Assert.AreEqual(1f, PotaFx.ShardScale(0.375f), 1e-5f, "full size until 1/1.6 of the life is left");
            Assert.AreEqual(0.35f, PotaFx.ShardScale(1f), 1e-6f);
        }

        [Test]
        public void Counts_FollowTheDepth()
        {
            var s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.AreEqual(14, PotaFx.ShardCount(s, 0));
            Assert.AreEqual(34, PotaFx.ShardCount(s, 4));
            Assert.AreEqual(34, PotaFx.ShardCount(s, 9), "cap");
            Assert.AreEqual(16, PotaFx.StarCount(3));
            Assert.AreEqual(10, PotaFx.PointCount(s, 0));
            Assert.AreEqual(26, PotaFx.PointCount(s, 5), "HIS.parcacik cap");
            Object.DestroyImmediate(s);
        }

        [Test]
        public void Shards_FlyInPotasCone()
        {
            var r = new System.Random(1);
            for (int i = 0; i < 500; i++)
            {
                var p = PotaFx.NewShard(r, i, 0);
                var v = p.Velocity - new Vector3(0f, 1.2f, 0f);
                float along = v.z / 0.8f, side = new Vector2(v.x, v.y).magnitude;
                Assert.Greater(v.z, 0f, "toward the camera (Pota +z)");
                Assert.LessOrEqual(Mathf.Atan2(side, along), 0.62f * Mathf.PI * 0.5f + 1e-4f, "≈ 56° cone");
                Assert.That(new Vector2(side, along).magnitude, Is.InRange(3.5f - 1e-3f, 7.5f + 1e-3f));
                Assert.That(p.Life, Is.InRange(0.7f, 1.2f));
                Assert.That(p.Size.z, Is.InRange(0.16f * 0.45f - 1e-5f, 0.36f * 0.45f + 1e-5f), "flat");
                Assert.AreEqual(i % 5 == 3 ? 0.65f : i % 5 == 4 ? 1.35f : 1f, p.Tone);
            }
        }

        [Test]
        public void PointSprite_IsHotInTheMiddle()
        {
            var tex = PotaFx.PointSprite(64);
            var c = tex.GetPixel(32, 32);
            Assert.Greater(c.a, 0.95f);
            Assert.Greater(c.r, 0.95f, "(1 + 0.6k)/1.6 = 1 at the centre");
            Assert.Less(tex.GetPixel(32, 61).a, 0.1f, "fades out by r = 0.5");
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 1e-6f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void PointWorldSize_MatchesGlPointSize()
        {
            Assert.AreEqual(0.12f * 300f * 2f * Mathf.Tan(19f * Mathf.Deg2Rad) / 1920f, PotaFx.PointWorldSize(0.12f, 38f, 1920f), 1e-7f,
                "efekt.ts: gl_PointSize = size · 300 / depth");
        }

        [Test]
        public void Icosphere_IsARoundDetail1Icosahedron()
        {
            var m = PotaFx.Icosphere(0.62f);
            Assert.AreEqual(42, m.vertexCount, "12 + 30 edge midpoints");
            Assert.AreEqual(80 * 3, m.triangles.Length);
            var v = m.vertices; var n = m.normals;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.AreEqual(0.62f, v[i].magnitude, 1e-4f);
                Assert.Less(Vector3.Distance(n[i], v[i].normalized), 1e-4f, "three PolyhedronGeometry detail > 0: smooth normals");
            }
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var n0 = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Assert.Greater(Vector3.Dot(n0, v[t[i]] + v[t[i + 1]] + v[t[i + 2]]), 0f, $"face {i / 3} faces outward (Unity front face)");
            }
            Object.DestroyImmediate(m);
        }
    }
}
```

`Assets/_Project/Games/Pota/Tests/PlayMode/PotaFxPlayTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class PotaFxPlayTests
    {
        private GameObject _root;
        private ColorHuntSettings _s;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root);
            if (_s != null) Object.Destroy(_s);
        }

        private ColorHuntFx Make(out Transform board)
        {
            _s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            _root = new GameObject("Game");
            _root.transform.position = new Vector3(0f, 8.5f, 30f);
            board = new GameObject("Board").transform;
            board.SetParent(_root.transform, false);
            board.localScale = Vector3.one * 1.2f;
            var fx = new GameObject("Effects").AddComponent<ColorHuntFx>();
            fx.transform.SetParent(_root.transform, false);
            fx.S = _s;
            fx.Build();
            fx.SetBoard(board);
            return fx;
        }

        private static int Count(ColorHuntFx fx, string name) => fx.transform.Find(name).GetComponent<ParticleSystem>().particleCount;

        [UnityTest]
        public IEnumerator Burst_EmitsPotasLayers_ScaledByDepth()
        {
            var fx = Make(out var board);
            fx.Burst(Vector2.zero, PotaLook.Tones[1], 2);
            yield return null;
            Assert.AreEqual(24, Count(fx, "Shards"), "min(34, 14 + 5·2)");
            Assert.AreEqual(14, Count(fx, "Stars"), "10 + 2·2");
            Assert.AreEqual(1, Count(fx, "FlashWhite"));
            Assert.AreEqual(1, Count(fx, "FlashTone"));
            Assert.AreEqual(1, Count(fx, "Ring"));
            var light = fx.GetComponentInChildren<Light>();
            Assert.IsTrue(light.enabled);
            Assert.Greater(light.intensity, 0f);
            Assert.Less(light.transform.position.z, board.position.z - 2f, "1.9 cells in front of the flash plane");
            fx.Clear();
            yield return null;
            Assert.AreEqual(0, Count(fx, "Shards"));
            Assert.IsFalse(light.enabled);
        }

        [UnityTest]
        public IEnumerator Effects_RunOnGameTime_ADipFreezesThem()
        {
            var fx = Make(out _);
            fx.Burst(Vector2.zero, PotaLook.Tones[0], 0);         // the white flash lives 0.16 s
            Time.timeScale = 0.02f;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.3f) yield return null;
            Assert.AreEqual(1, Count(fx, "FlashWhite"), "0.3 s of real time is 0.006 s of game time");
        }

        [UnityTest]
        public IEnumerator Confetti_FallsFromAboveTheBoard_WhereverItIs()
        {
            var fx = Make(out var board);
            fx.Confetti(7, 220);
            yield return null;
            var ps = fx.transform.Find("Confetti").GetComponent<ParticleSystem>();
            var parts = new ParticleSystem.Particle[ps.particleCount];
            ps.GetParticles(parts);
            Assert.AreEqual(220, parts.Length, "RENK_AVI_HIS.konfeti");
            float frameTop = board.TransformPoint(new Vector3(0f, 3.5f + 0.22f, 0f)).y;
            foreach (var p in parts) Assert.Greater(p.position.y, frameTop, "from above the frame (n/2 + KENAR + 0.8)");
        }
    }
}
```

In `ColorHuntStagingTests.cs` delete `ConfettiOrigin_FollowsTheGameRoot`.

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaFxCurveTests`
Expected: exit 2, `error CS0103: The name 'PotaFx' does not exist`.

- [ ] **Step 3: Pure formulas and the additive shader**

`PotaFx.cs`:

```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's effect formulas, pure (patlamaEfekt.ts balon kipi, efekt.ts Parcaciklar). Vectors are in Pota's board axes: +z toward the
    /// camera (the caller mirrors z for Unity). Randomness comes from the caller's generator, one draw per Math.random in Pota.
    /// </summary>
    public static class PotaFx
    {
        public struct Shard { public Vector2 Offset; public Vector3 Velocity, Size, Rotation, Spin; public float Life, Tone; }
        public struct Star { public Vector3 Velocity; public float ZOffset, Size, Rotation, Life; public bool Warm; }
        public struct Point { public Vector3 Velocity; public float Life, Size; }

        private static float R(System.Random r) => (float)r.NextDouble();

        /// <summary>flaslariIlerlet: size = start + (end − start)·(1 − (1 − u)^2.2).</summary>
        public static float Grow(float u) => 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 2.2f);

        /// <summary>kivilcimlariIlerlet: sin(π·min(1, 0.12 + 0.95a))·(0.75 + 0.25·sin(38a)), a = age / life.</summary>
        public static float StarSize(float a)
        {
            a = Mathf.Clamp01(a);
            return Mathf.Sin(Mathf.PI * Mathf.Min(1f, 0.12f + a * 0.95f)) * (0.75f + 0.25f * Mathf.Sin(a * 38f));
        }

        /// <summary>guncelle: 0.35 + 0.65·min(1, remaining·1.6) (sonOlcek 0.35).</summary>
        public static float ShardScale(float a) => 0.35f + 0.65f * Mathf.Min(1f, (1f - Mathf.Clamp01(a)) * 1.6f);

        /// <summary>renkAviFaz.ts patlamaKatmanlari: min(parca.enCok, taban + d·derinlikBasi).</summary>
        public static int ShardCount(ColorHuntSettings s, int depth) => Mathf.Min(s.ShardsMax, s.ShardsBase + depth * s.ShardsPerDepth);

        /// <summary>patlat → kivilcimSac(…, 10 + d·2).</summary>
        public static int StarCount(int depth) => 10 + depth * 2;

        /// <summary>renkAviFaz.ts adimBaslat: min(HIS.parcacik.enCok, taban + d·derinlikBasi) points per chain burst.</summary>
        public static int PointCount(ColorHuntSettings s, int depth) => Mathf.Min(s.PointsMax, s.PointsBase + depth * s.PointsPerDepth);

        /// <summary>patlat: a cone of up to 0.62·90° from +z, 3.5–7.5 (+0.4/depth), +1.2 up; flat shell pieces 0.16–0.36; 0.7–1.2 s.</summary>
        public static Shard NewShard(System.Random r, int index, int depth)
        {
            float tone = index % 5 == 3 ? 0.65f : index % 5 == 4 ? 1.35f : 1f;
            float a = R(r) * Mathf.PI * 2f, tilt = R(r) * 0.62f * Mathf.PI * 0.5f, speed = 3.5f + R(r) * 4f + depth * 0.4f;
            var offset = new Vector2((R(r) - 0.5f) * 0.3f, (R(r) - 0.5f) * 0.3f);
            var v = new Vector3(Mathf.Cos(a) * Mathf.Sin(tilt) * speed, Mathf.Sin(a) * Mathf.Sin(tilt) * speed + 1.2f, Mathf.Cos(tilt) * speed * 0.8f);
            var rot = new Vector3(R(r), R(r), R(r)) * (Mathf.PI * 2f);
            var spin = new Vector3(R(r) - 0.5f, R(r) - 0.5f, R(r) - 0.5f) * 16f;
            float boy = 0.16f + R(r) * 0.2f;
            var size = new Vector3(boy * (0.8f + R(r) * 0.6f), boy * (0.55f + R(r) * 0.4f), boy * 0.45f);
            return new Shard { Offset = offset, Velocity = v, Rotation = rot, Spin = spin, Size = size, Life = 0.7f + R(r) * 0.5f, Tone = tone };
        }

        /// <summary>kivilcimSac: flat radial 3–7.5, 0.8–2.4 toward the camera, 0.36–0.66, 0.4–0.7 s; even ones white-yellow.</summary>
        public static Star NewStar(System.Random r, int index)
        {
            float a = R(r) * Mathf.PI * 2f, speed = 3f + R(r) * 4.5f;
            return new Star
            {
                ZOffset = R(r) * 0.3f,
                Velocity = new Vector3(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed, 0.8f + R(r) * 1.6f),
                Size = 0.36f + R(r) * 0.3f,
                Rotation = R(r) * Mathf.PI,
                Life = 0.4f + R(r) * 0.3f,
                Warm = index % 2 == 0,
            };
        }

        /// <summary>Parcaciklar.patlat: a, e = (rand − 0.3)·π/2, h = speed·(0.5 + rand); 0.45–0.8 s; size·(0.7–1.3).</summary>
        public static Point NewPoint(System.Random r, float speed, float size)
        {
            float a = R(r) * Mathf.PI * 2f, e = (R(r) - 0.3f) * Mathf.PI * 0.5f, h = speed * (0.5f + R(r));
            return new Point
            {
                Velocity = new Vector3(Mathf.Cos(a) * Mathf.Cos(e) * h, Mathf.Sin(e) * h + speed * 0.6f, Mathf.Sin(a) * Mathf.Cos(e) * h * 0.6f),
                Life = 0.45f + R(r) * 0.35f,
                Size = size * (0.7f + R(r) * 0.6f),
            };
        }

        /// <summary>efekt.ts PARCACIK_VS gl_PointSize = size·300/depth px → world size on a camera of this FOV and pixel height.</summary>
        public static float PointWorldSize(float size, float fov, float pixelHeight) =>
            size * 300f * 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, pixelHeight);

        /// <summary>efekt.ts PARCACIK_FS: k = smoothstep(0.5, 0.2, r), rgb·(1 + 0.6k), alpha k; rgb stored ÷1.6 (material ×1.6).</summary>
        public static Texture2D PointSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PotaPoint" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude;
                    float t = Mathf.Clamp01((r - 0.5f) / (0.2f - 0.5f));
                    float k = r > 0.5f ? 0f : t * t * (3f - 2f * t);
                    float rgb = (1f + 0.6f * k) / 1.6f;
                    tex.SetPixel(x, y, new Color(rgb, rgb, rgb, k));
                }
            tex.Apply();
            return tex;
        }

        /// <summary>three IcosahedronGeometry(radius, 1): 42 vertices, 80 faces, smooth normals (patlamaEfekt.ts balonKipi shards).</summary>
        public static Mesh Icosphere(float radius)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new System.Collections.Generic.Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i)) return i;
                v.Add((v[a] + v[b]) * 0.5f);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var verts = new Vector3[v.Count];
            var norms = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++) { norms[i] = v[i].normalized; verts[i] = norms[i] * radius; }
            // (b − a) × (c − a) points outward for every face above: Unity's front face (checked on face 0, 11, 5).
            var m = new Mesh { name = "PotaShard", vertices = verts, normals = norms, triangles = tris.ToArray() };
            m.RecalculateBounds();
            return m;
        }
    }
}
```

`Assets/_Project/Games/Pota/Shaders/PotaAdditive.shader`:

```hlsl
// Pota's additive effect materials (patlamaEfekt.ts `additive`: map + instance colour, AdditiveBlending = SrcAlpha, One; no fog).
// Particle vertex colours are 8-bit, so they carry Pota's (linear, up to 1.8) colour ÷ _Intensity.
Shader "Project/PotaAdditive"
{
    Properties
    {
        _BaseMap ("Sprite", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Additive"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(t.rgb * i.color.rgb * _Intensity, t.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
```

- [ ] **Step 4: Rewrite `ColorHuntFx`**

```csharp
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's burst layers on particle systems (patlamaEfekt.ts balon kipi, efekt.ts Parcaciklar, yapbozSahne.ts puf/konfeti): white and
    /// tone flash, ring, glossy flat shards, four-point star sparks, one point light, an additive puff sphere, hot points and paper
    /// confetti. Every particle is emitted with Pota's own formulas (<see cref="PotaFx"/>) at board-cell positions (Pota's tahtaGrup),
    /// so sizes follow the grid scale. Game time throughout: a hit-stop dip freezes them too.
    /// </summary>
    public class ColorHuntFx : MonoBehaviour
    {
        /// <summary>Additive materials multiply by this: 8-bit vertex colours carry Pota's value ÷ 2 (flash colours reach 1.8).</summary>
        public const float Headroom = 2f;
        /// <summary>Shard material colour ×1.35 (the lightest shards are tone·1.35).</summary>
        public const float ShardHeadroom = 1.35f;

        public ColorHuntSettings S;

        private ParticleSystem _flashWhite, _flashTone, _ring, _stars, _points, _puff, _shards, _confetti;
        private Light _light;
        private float _lightT, _lightTime, _lightPeak;
        private Transform _board;
        private float _s = 1f;
        private readonly System.Random _rng = new System.Random();
        private bool _built;

        public void Build()
        {
            if (_built) return;
            _built = true;
            var disc = Additive(S.SoftDisc, Headroom);
            _flashWhite = System("FlashWhite", disc, ParticleSystemRenderMode.Billboard, 12, 8);
            SizeOverLife(_flashWhite, u => Mathf.Lerp(0.5f / 1.9f, 1f, PotaFx.Grow(u)));
            AlphaOverLife(_flashWhite, u => 1f - u);
            _flashTone = System("FlashTone", disc, ParticleSystemRenderMode.Billboard, 12, 8);
            SizeOverLife(_flashTone, u => Mathf.Lerp(0.4f / 1.4f, 1f, PotaFx.Grow(u)));
            AlphaOverLife(_flashTone, u => 1f - u);
            _ring = System("Ring", Additive(S.SoftRing, Headroom), ParticleSystemRenderMode.Billboard, 12, 8);
            // 0.3 → ring size; the start ratio is depth 0's (a deeper ring starts ≤ 0.2 larger for one frame).
            SizeOverLife(_ring, u => Mathf.Lerp(0.3f / 3.4f, 1f, PotaFx.Grow(u)));
            AlphaOverLife(_ring, u => Mathf.Sqrt(Mathf.Max(0f, 1f - u)));
            _stars = System("Stars", Additive(S.StarSprite, Headroom), ParticleSystemRenderMode.Billboard, 72, 9);
            SizeOverLife(_stars, PotaFx.StarSize, 64);
            AlphaOverLife(_stars, a => 1f - a * a);
            var drag = _stars.limitVelocityOverLifetime;           // v *= 1 − 4·dt
            drag.enabled = true;
            drag.limit = 1000f;
            drag.drag = 4f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;
            _points = System("Points", Additive(PotaFx.PointSprite(64), 1.6f), ParticleSystemRenderMode.Billboard, 160, 6);
            SizeOverLife(_points, a => 0.6f + 0.4f * (1f - a));
            AlphaOverLife(_points, a => 1f - a);
            _puff = System("Puff", Additive(Texture2D.whiteTexture, 1f), ParticleSystemRenderMode.Mesh, 16, 5);
            _puff.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            SizeOverLife(_puff, a => 0.4f + 1.5f * a);
            AlphaOverLife(_puff, a => 0.9f * (1f - a));
            _shards = System("Shards", Lit(0.8f, 0f, ShardHeadroom), ParticleSystemRenderMode.Mesh, 160, 7);
            _shards.GetComponent<ParticleSystemRenderer>().mesh = PotaFx.Icosphere(0.62f);
            SizeOverLife(_shards, PotaFx.ShardScale);
            _confetti = System("Confetti", Lit(0.55f, 0.05f, 1f), ParticleSystemRenderMode.Mesh, 240, 7);
            _confetti.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var lightGo = new GameObject("BurstLight");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
            SetBoard(_board);
        }

        /// <summary>The board the effects live on (Pota's tahtaGrup, olcekAyarla): cell positions, grid scale for sizes, gravity, light range.</summary>
        public void SetBoard(Transform board)
        {
            _board = board;
            _s = board != null ? board.lossyScale.x : 1f;
            if (!_built) return;
            Gravity(_shards, 12f * _s);
            Gravity(_stars, 2.5f * _s);
            Gravity(_points, 6f);            // Parcaciklar lives in the unscaled root
            Gravity(_confetti, 2.2f);        // konfeti divides by the scale: world units
            _light.range = 6f * _s;          // ISIK_MENZIL · s
        }

        /// <summary>renkAviFaz.ts patlamaKatmanlari → patlat: glow (flashes, ring, light) + shards + star sparks.</summary>
        public void Burst(Vector2 cell, Color tone, int depth)
        {
            if (!_built) return;
            var c = tone.linear;
            Glow(cell, c, S.FlashSize + depth * S.FlashSizePerDepth, 3.4f + depth * 0.5f, S.LightFlashIntensity * (1f + depth * 0.25f), S.LightFlashTime);
            float z = PotaLook.FrontZ + 0.3f;
            int n = PotaFx.ShardCount(S, depth);
            for (int i = 0; i < n; i++)
            {
                var p = PotaFx.NewShard(_rng, i, depth);
                var col = c * (p.Tone / ShardHeadroom);
                col.a = 1f;
                _shards.Emit(new ParticleSystem.EmitParams
                {
                    position = World(new Vector3(cell.x + p.Offset.x, cell.y + p.Offset.y, z)),
                    velocity = WorldVector(p.Velocity),
                    startSize3D = p.Size * _s,
                    rotation3D = p.Rotation * Mathf.Rad2Deg,
                    angularVelocity3D = p.Spin * Mathf.Rad2Deg,
                    startLifetime = p.Life,
                    startColor = col,
                    applyShapeToPosition = false,
                }, 1);
            }
            int stars = PotaFx.StarCount(depth);
            for (int i = 0; i < stars; i++)
            {
                var st = PotaFx.NewStar(_rng, i);
                var col = st.Warm ? new Color(1.6f, 1.5f, 1.1f) : new Color(0.6f + c.r * 1.2f, 0.6f + c.g * 1.2f, 0.6f + c.b * 1.2f);
                col /= Headroom;
                col.a = 1f;
                _stars.Emit(new ParticleSystem.EmitParams
                {
                    position = World(new Vector3(cell.x, cell.y, z + 0.25f + st.ZOffset)),
                    velocity = WorldVector(st.Velocity),
                    startSize = st.Size * _s,
                    rotation = st.Rotation * Mathf.Rad2Deg,
                    angularVelocity = 2f / st.Life * Mathf.Rad2Deg,          // donus + 2·age/life
                    startLifetime = st.Life,
                    startColor = col,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>patlamaEfekt.ts parla: white flash 0.16 s (0.5F → 1.9F), tone flash 0.22 s (0.4F → 1.4F), ring 0.38 s, one point light.</summary>
        public void Glow(Vector2 cell, Color lin, float flash, float ring, float light, float lightTime)
        {
            if (!_built) return;
            float z = PotaLook.FrontZ + 0.3f;
            Emit1(_flashWhite, World(new Vector3(cell.x, cell.y, z)), flash * 1.9f * _s, 0.16f, new Color(1.7f, 1.7f, 1.6f) / Headroom);
            Emit1(_flashTone, World(new Vector3(cell.x, cell.y, z + 0.01f)), flash * 1.4f * _s, 0.22f, lin * (1.8f / Headroom));
            Emit1(_ring, World(new Vector3(cell.x, cell.y, z + 0.02f)), ring * _s, 0.38f,
                new Color(0.5f + lin.r * 1.2f, 0.5f + lin.g * 1.2f, 0.5f + lin.b * 1.2f) / Headroom);
            if (light <= 0f) return;
            _light.color = new Color(0.35f + lin.r * 0.65f, 0.35f + lin.g * 0.65f, 0.35f + lin.b * 0.65f).gamma;   // Light.color is sRGB
            _light.transform.position = World(new Vector3(cell.x, cell.y, z + 1.9f));
            // Pota keeps the stronger of a running and a new flash; ×s^1.6 compensates the scaled group (olcekAyarla). URP has no
            // decay exponent: LightIntensityScale maps three's 1/d^1.6 (÷π) onto URP's 1/d² at the block plane.
            _lightPeak = Mathf.Max(_lightT < _lightTime ? _lightPeak : 0f, light * Mathf.Pow(_s, 1.6f) * S.LightIntensityScale);
            _lightTime = lightTime;
            _lightT = 0f;
            _light.enabled = true;
            _light.intensity = _lightPeak;
        }

        /// <summary>yapbozSahne.ts parcala → efekt.ts Parcaciklar.patlat: hot additive points from the front plane (root units).</summary>
        public void Particles(Vector2 cell, Color tone, int count, float speed, float size)
        {
            if (!_built || count <= 0) return;
            var c = tone.linear;
            c.a = 1f;
            var cam = Camera.main;
            float px = cam != null ? cam.pixelHeight : 1920f, fov = cam != null ? cam.fieldOfView : PotaLook.Fov;
            var pos = World(new Vector3(cell.x, cell.y, PotaLook.FrontZ));
            for (int i = 0; i < count; i++)
            {
                var p = PotaFx.NewPoint(_rng, speed * _s, size * _s);
                _points.Emit(new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = new Vector3(p.Velocity.x, p.Velocity.y, -p.Velocity.z),
                    startSize = PotaFx.PointWorldSize(p.Size, fov, px),
                    startLifetime = p.Life,
                    startColor = c,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>yapbozSahne.ts pufPatlat: additive white sphere r 0.45, (0.4 + 1.5o)·strength over 0.36 s, 0.9 → 0, on the front plane.</summary>
        public void Puff(Vector2 cell, float strength)
        {
            if (!_built) return;
            Emit1(_puff, World(new Vector3(cell.x, cell.y, PotaLook.FrontZ)), 0.9f * strength * _s, 0.36f, Color.white);
        }

        /// <summary>yapbozSahne.ts konfeti → patlamaEfekt.ts konfeti: paper boxes from above the frame across n + 1, world-sized, 2–3.2 s.</summary>
        public void Confetti(int n, int count)
        {
            if (!_built) return;
            float top = n * 0.5f + PotaLook.Edge + 0.8f, width = n + 1f;
            for (int i = 0; i < count; i++)
            {
                var c = PotaLook.Confetti[i % PotaLook.Confetti.Length].linear;
                c.a = 1f;
                _confetti.Emit(new ParticleSystem.EmitParams
                {
                    position = World(new Vector3((R() - 0.5f) * width, top + R() * 1.5f, PotaLook.FrontZ + 0.2f + R() * 1.2f)),
                    velocity = new Vector3((R() - 0.5f) * 2.5f, -0.5f - R() * 1.5f, -(R() - 0.5f) * 0.6f),
                    startSize3D = new Vector3(0.22f, 0.14f, 0.03f),
                    rotation3D = new Vector3(R(), R(), R()) * 360f,
                    angularVelocity3D = new Vector3((R() - 0.5f) * 10f, (R() - 0.5f) * 10f, (R() - 0.5f) * 5f) * Mathf.Rad2Deg,
                    startLifetime = 2f + R() * 1.2f,
                    startColor = c,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>patlamaEfekt.ts temizle (new board).</summary>
        public void Clear()
        {
            if (!_built) return;
            foreach (var ps in new[] { _flashWhite, _flashTone, _ring, _stars, _points, _puff, _shards, _confetti }) ps.Clear();
            _lightPeak = 0f;
            _light.intensity = 0f;
            _light.enabled = false;
        }

        private void Update()
        {
            if (!_built || _lightPeak <= 0f) return;
            _lightT += Time.deltaTime;
            float u = Mathf.Min(1f, _lightT / Mathf.Max(0.01f, _lightTime));
            _light.intensity = _lightPeak * (1f - u) * (1f - u);
            if (u >= 1f) { _lightPeak = 0f; _light.intensity = 0f; _light.enabled = false; }
        }

        private float R() => (float)_rng.NextDouble();

        /// <summary>Pota board point (+z toward the camera) → world.</summary>
        private Vector3 World(Vector3 pota)
        {
            var local = new Vector3(pota.x, pota.y, -pota.z);
            return _board != null ? _board.TransformPoint(local) : transform.TransformPoint(local);
        }

        private Vector3 WorldVector(Vector3 pota)
        {
            var local = new Vector3(pota.x, pota.y, -pota.z);
            return _board != null ? _board.TransformVector(local) : local;
        }

        private static void Emit1(ParticleSystem ps, Vector3 pos, float size, float life, Color color)
        {
            color.a = 1f;
            ps.Emit(new ParticleSystem.EmitParams { position = pos, startSize = size, startLifetime = life, startColor = color, applyShapeToPosition = false }, 1);
        }

        private static void Gravity(ParticleSystem ps, float g)
        {
            var main = ps.main;
            main.gravityModifier = g / Mathf.Max(0.01f, -Physics.gravity.y);
        }

        private ParticleSystem System(string name, Material mat, ParticleSystemRenderMode mode, int max, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);        // AddComponent starts playing (CLAUDE.md lesson)
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startColor = Color.white;
            main.useUnscaledTime = false;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.applyActiveColorSpace = false;                                         // vertex colours are Pota's linear values
            r.alignment = mode == ParticleSystemRenderMode.Mesh ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            ps.Play();
            return ps;
        }

        private static Material Additive(Texture tex, float intensity)
        {
            var m = new Material(Shader.Find("Project/PotaAdditive"));
            m.SetTexture("_BaseMap", tex != null ? tex : Texture2D.whiteTexture);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        /// <summary>URP Particles/Lit (Pota MeshStandard: shards roughness 0.2, confetti 0.45 / metalness 0.05), colour ×<paramref name="scale"/>.</summary>
        private static Material Lit(float smoothness, float metallic, float scale)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetVector("_BaseColor", new Vector4(scale, scale, scale, 1f));      // a vector: no gamma conversion of the >1 factor
            return m;
        }

        private static void SizeOverLife(ParticleSystem ps, Func<float, float> f, int keys = 24)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= keys; i++) curve.AddKey(i / (float)keys, f(i / (float)keys));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void AlphaOverLife(ParticleSystem ps, Func<float, float> f)
        {
            var alphas = new GradientAlphaKey[8];
            for (int i = 0; i < 8; i++) alphas[i] = new GradientAlphaKey(Mathf.Clamp01(f(i / 7f)), i / 7f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }
    }
}
```

`ColorHuntSettings.cs` ([Header("Explosion")]): remove `SparksBase`; add

```csharp
        [Tooltip("RENK_AVI_HIS.flas: flash size 1.3 (+0.15 per chain depth).")]
        public float FlashSize = 1.3f;
        public float FlashSizePerDepth = 0.15f;
        [Tooltip("three's 1/d^1.6 light (÷π) on URP's 1/d² at the block plane (≈ 1.9·1.2 m): 2.28^0.4 / π ≈ 0.44. Tuned side by side.")]
        public float LightIntensityScale = 0.44f;
        [Tooltip("HIS.parcacik: hot points per chain burst, 10 + 4 per depth, at most 26.")]
        public int PointsBase = 10;
        public int PointsPerDepth = 4;
        public int PointsMax = 26;
        [Tooltip("RENK_AVI_HIS.konfeti.")]
        public int ConfettiCount = 220;
```

`ColorHuntGame.cs`:
- delete `_shardMesh` (field and `RoundedBoxMesh.Create(Vector3.one, 0.22f, 3)`), `ConfettiOrigin`;
- `Start`: `if (Fx != null) Fx.Build(_shardMesh);` → `if (Fx != null) { Fx.Build(); Fx.SetBoard(BoardRoot); }` (after the board scale is set);
- `Prepare`: after the scale line add `Fx?.SetBoard(BoardRoot); Fx?.Clear();`;
- `StartSalvo` ball arrival: `if (s.Arrived % 3 == 1) Fx?.Sparkle(hit, color);` →
  `if (s.Arrived % 3 == 1) Fx?.Particles(BoardRoot.InverseTransformPoint(hit), S.PaletteColor(v.Block.Color), 3, 1.5f, 0.07f);`
  (renkAviFaz.ts topVardi: `varan % 3 === 1`, `parcala(top.hx, top.hy, …, 3, 1.5, 0.07)` at the ball's own target); delete the local `color`;
- `FinishSalvo`: `Fx?.Explode(pos, …, 0);` → `var tone = S.PaletteColor(view.Block.Color); Fx?.Burst(view.Pos, tone, 0); Fx?.Puff(view.Pos, 1.15f); Fx?.Particles(view.Pos, tone, S.PointsBase + 4, 2.6f, 0.12f);`
- `StartStep`: `Fx?.Explode(p, …, st.Depth);` → `var tone = S.PaletteColor(v.Block.Color); Fx?.Burst(v.Pos, tone, st.Depth); Fx?.Puff(v.Pos, 1f + st.Depth * 0.12f); Fx?.Particles(v.Pos, tone, PotaFx.PointCount(S, st.Depth), 2.2f + st.Depth * 0.3f, 0.1f);`
- landing (`AdvanceViews`): replace the `Fx?.Puff(BoardRoot.TransformPoint(…), …)` line with
  `if (d.Distance >= 1f) Fx?.Puff(new Vector2(v.Home.x, v.Home.y - 0.45f), 0.4f + 0.08f * d.Distance);` (RENK_AVI_HIS.inisTozu);
- `End`: `Fx.Confetti(ConfettiOrigin, S.Palette, 260);` → `Fx.Confetti(Board.Size, S.ConfettiCount);`.

`BoardBuilder.cs`: delete `GradientTexture` and `SoftCircle` (no callers left).

- [ ] **Step 5: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaFxCurveTests Project.Tests.ColorHuntStagingTests`
Expected: all PASS (8 + 3).
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.PotaFxPlayTests --no-compile` → Expected: 3/3 PASS.
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → Expected: all PASS.

- [ ] **Step 6: Look at it**

Play `ColorHunt.unity`, `ai_colorhunt_play {"action":"slowmo","scale":0.1,"seconds":30}`, tap an affordable block, take game-view screenshots
about 0.55 s and 0.75 s real time after the salvo ends (= 0.05–0.07 s of game time after the burst), stop. Expected: a white-hot flash
with a tone flash and an expanding ring on the block, glossy flat shards in the block's colour flying toward the camera, four-point
star sparks, a soft white puff. If the flash or stars are blown out or grey, check `applyActiveColorSpace` and the headroom factor
before tuning numbers.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Games/Pota
git commit -m "feat: Pota's burst layers for Color Hunt — flashes, ring, glossy shards, star sparks, puff, points, light, paper confetti

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 8: Pota's salvo, trail, sounds and feel

**Files:**
- Rewrite: `Assets/_Project/Games/Pota/Scripts/ColorHunt/BallSalvo.cs`, `ColorHuntJuice.cs`
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaSounds.cs`
- Modify: `PotaLook.cs` (salvo/trail), `PotaAdditive.shader` (`_SwapUV`), `ColorHuntSettings.cs` (Juice header; − `ShakeBallHit, NeighbourRipple`;
  `RotateTime` 0.42), `ColorHuntGame.cs` (salvo from Pota's source, ring during the salvo, juice calls, no `_launcher`),
  `BoardBuilder.cs` (− `Margin`), `Editor/ColorHuntSceneTool.cs` (`BuildGame`: sounds, salvo ball), `Editor/PotaLookImportTool.cs` (`RotateTime` sync)
- Test: `Tests/EditMode/PotaLookTests.cs` (+2), `Tests/EditMode/PotaSoundsTests.cs` (new), `Tests/EditMode/ColorHuntJuiceTests.cs` (new),
  `Tests/PlayMode/PotaLookPlayTests.cs` (new: salvo)

**Interfaces:**
- Consumes: `CameraShake.Add`, `TimeScaleController.Dip` (Task 3); `ColorHuntFx.Particles` (Task 7); settings clips (Task 1).
- Produces: `PotaLook.SalvoPoint(Vector2 from, Vector2 to, float o)` (x, y cells; z = arc toward the camera), `PotaLook.SalvoScale(float o)`,
  `PotaLook.SalvoSource(int n)`, constants `SalvoArc 0.9, SalvoSpin 14, TrailSpacing 0.14, TrailPoints 10, TrailWidth 0.34,
  TrailBrightness 1.35, TrailColor #ffa040`.
- Produces: `BallSalvo.Board`, `BallSalvo.Fire(Vector2 source, Vector2 target, int cost, float duration, Action<Vector2> onArrive)` (cells),
  `Clear()`, `Tick(dt)`, `Busy`.
- Produces: `PotaSounds` (`S`; `Select()`, `Salvo()`, `Hit()`, `Break()`, `Combo(int depth)`, `Drop(float guc)`, `Rotate()`, `Minus()`, `Win()`, `Lose()`;
  statics `Allow(IDictionary<string, float> last, string id, float now)`, `DropVolume(float guc)`, `ComboIndex(int depth)`; `LastClip`, `LastVolume`).
- Produces: `ColorHuntJuice` (`Sounds`; `Select()`, `SalvoStart()`, `BallHit()`, `Break()`, `Chain(int depth, Vector3 at)`, `Landed(float cells)`,
  `Rotate()`, `Insufficient(Vector3 at, int cost)`, `Result(bool won)`).

- [ ] **Step 1: Write the failing tests**

Append to `PotaLookTests.cs`:

```csharp
        [Test]
        public void Salvo_BowsTowardTheCamera_AndSwells()
        {
            var a = new Vector2(0f, -3.95f); var b = new Vector2(1f, 2f);
            Assert.AreEqual(new Vector3(0f, -3.95f, 0f), PotaLook.SalvoPoint(a, b, 0f));
            Assert.Less(Vector3.Distance(new Vector3(1f, 2f, 0f), PotaLook.SalvoPoint(a, b, 1f)), 1e-5f);
            var mid = PotaLook.SalvoPoint(a, b, 0.5f);
            Assert.AreEqual(0.9f, mid.z, 1e-5f, "sin(π·0.5)·0.9 toward the camera");
            Assert.AreEqual(0.7f, PotaLook.SalvoScale(0f), 1e-6f);
            Assert.AreEqual(1.2f, PotaLook.SalvoScale(0.5f / 1.15f), 1e-5f);
            Assert.AreEqual(0.7f, PotaLook.SalvoScale(1f), 1e-5f);
            Assert.AreEqual(new Vector2(0f, -3.5f - 0.45f), PotaLook.SalvoSource(7), "topKaynagi: altKenar − TOP_PAYI·0.75");
        }
```

`Assets/_Project/Games/Pota/Tests/EditMode/PotaSoundsTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;

namespace Project.Tests
{
    public class PotaSoundsTests
    {
        [Test]
        public void RateLimits_AreSesTs()
        {
            var last = new Dictionary<string, float>();
            Assert.IsTrue(PotaSounds.Allow(last, "kombo", 1f));
            Assert.IsFalse(PotaSounds.Allow(last, "kombo", 1.079f), "SINIR kombo 80 ms");
            Assert.IsTrue(PotaSounds.Allow(last, "kombo", 1.081f));
            Assert.IsTrue(PotaSounds.Allow(last, "carpma", 2f));
            Assert.IsFalse(PotaSounds.Allow(last, "carpma", 2.05f), "carpma 60 ms");
            Assert.IsTrue(PotaSounds.Allow(last, "dondur", 3f));
            Assert.IsTrue(PotaSounds.Allow(last, "dondur", 3.001f), "no limit");
        }

        [Test]
        public void DropVolume_AndComboStep()
        {
            Assert.AreEqual(0.35f + 0.5f * 0.4f, PotaSounds.DropVolume(0.5f), 1e-6f, "ses.ts: dustu k = min(1, 0.35 + guc·0.4)");
            Assert.AreEqual(1f, PotaSounds.DropVolume(3f), 1e-6f);
            Assert.AreEqual(2, PotaSounds.ComboIndex(2));
            Assert.AreEqual(10, PotaSounds.ComboIndex(15), "MERDIVEN has 11 steps");
            Assert.AreEqual(0, PotaSounds.ComboIndex(-1));
        }
    }
}
```

`Assets/_Project/Games/Pota/Tests/EditMode/ColorHuntJuiceTests.cs`:

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using Project.Juice;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntJuiceTests
    {
        [Test]
        public void Shakes_AreHisSarsinti_AndAdd()
        {
            var go = new GameObject("Juice");
            var s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            try
            {
                var shake = go.AddComponent<CameraShake>();
                var j = go.AddComponent<ColorHuntJuice>();
                j.S = s; j.Shake = shake;
                j.Break();
                Assert.AreEqual(0.08f, shake.Linear, 1e-6f, "kirilma");
                j.Chain(3, Vector3.zero);
                Assert.AreEqual(0.08f + 0.07f + 2f * 0.03f, shake.Linear, 1e-6f, "zincir + (d − 1)·derinlikBasi, additive");
                j.Rotate();
                Assert.AreEqual(0.26f, shake.Linear, 1e-5f);
                j.Result(true);
                Assert.AreEqual(0.4f, shake.Linear, 1e-5f, "teslim 0.14");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(s);
            }
        }
    }
}
```

`Assets/_Project/Games/Pota/Tests/PlayMode/PotaLookPlayTests.cs`:

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
    /// <summary>Pota's puzzle timing in the Color Hunt scene as saved.</summary>
    public class PotaLookPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator LoadPlaying(Action<ColorHuntGame> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            ready(game);
        }

        private static int Affordable(ColorHuntGame game, int minHp)
        {
            var b = game.Board;
            for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i].Hp >= minHp && b.Cells[i].Hp <= b.Ammo) return i;
            return -1;
        }

        [UnityTest]
        public IEnumerator Salvo_FliesMinCost24Balls_OnTheBoard_ThenEnds()
        {
            ColorHuntGame game = null;
            yield return LoadPlaying(g => game = g);
            int cell = Affordable(game, 5);
            Assert.GreaterOrEqual(cell, 0);
            int cost = game.Board.Cells[cell].Hp;
            game.Tap(cell);
            yield return null;
            var balls = game.BoardRoot.GetComponentsInChildren<MeshRenderer>(true);
            int pooled = 0;
            foreach (var r in balls) if (r.name == "Ball") pooled++;
            Assert.AreEqual(Mathf.Min(24, cost), pooled, "renkAviFaz.ts: min(enCokTop, cost) balls, children of the board");
            yield return WaitUntil(() => !game.Salvo.Busy, 2f);
            Assert.IsFalse(game.Salvo.Busy, "all balls land within the 0.5 s salvo");
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests Project.Tests.PotaSoundsTests Project.Tests.ColorHuntJuiceTests`
Expected: exit 2 — `'PotaLook' does not contain 'SalvoPoint'`, `'PotaSounds' does not exist`, `'ColorHuntJuice' does not contain 'Break'`.

- [ ] **Step 3: `PotaLook` salvo helpers, shader UV swap**

```csharp
        // renkAviFaz.ts RENK_AVI_HIS.iz, salvo; patlamaEfekt.ts cizIzler
        public const float SalvoArc = 0.9f;             // iz.kavis
        public const float SalvoSpin = 14f;             // iz.donus (rad/s)
        public const float TrailSpacing = 0.14f;        // iz.aralik (cells)
        public const int TrailPoints = 10;              // iz.adet
        public const float TrailWidth = 0.34f;
        public const float TrailBrightness = 1.35f;
        public static readonly Color TrailColor = Hex(0xffa040);

        /// <summary>toplariIlerlet: smoothstep path in the board plane, bowing sin(πo)·0.9 toward the camera (z, Pota axes).</summary>
        public static Vector3 SalvoPoint(Vector2 from, Vector2 to, float o)
        {
            var p = Vector2.LerpUnclamped(from, to, Smoothstep(o));
            return new Vector3(p.x, p.y, Mathf.Sin(Mathf.PI * Mathf.Clamp01(o)) * SalvoArc);
        }

        /// <summary>toplariIlerlet: 0.7 + 0.5·sin(π·min(1, 1.15o)).</summary>
        public static float SalvoScale(float o) => 0.7f + 0.5f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, Mathf.Clamp01(o) * 1.15f));

        /// <summary>yapbozSahne.ts topKaynagi: (0, altKenar − TOP_PAYI·0.75), altKenar = −n/2.</summary>
        public static Vector2 SalvoSource(int n) => new Vector2(0f, -n * 0.5f - BallPad * 0.75f);
```

`PotaAdditive.shader`: add the property `[Toggle] _SwapUV ("Swap UV (Pota's trail texture runs along v)", Float) = 0`, add `float _SwapUV;`
to the CBUFFER, and sample with `float2 uv = lerp(i.uv, i.uv.yx, _SwapUV);`.

- [ ] **Step 4: `PotaSounds`**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// ses.ts on Unity: the clips ai_potalook_import rendered from Pota's own graph (samples wet through its room send, synth kombo /
    /// carpma / dokun), 2D, with Pota's volumes, sample rate jitter 0.97–1.03 (ornekCal) and rate limits (SINIR). Eight voices.
    /// </summary>
    public class PotaSounds : MonoBehaviour
    {
        public ColorHuntSettings S;

        /// <summary>ses.ts SINIR (seconds).</summary>
        private static readonly Dictionary<string, float> Limits = new Dictionary<string, float>
        {
            { "carpma", 0.06f }, { "eksi", 0.06f }, { "kirildi", 0.05f }, { "kombo", 0.08f }, { "dustu", 0.045f },
        };

        public AudioClip LastClip { get; private set; }
        public float LastVolume { get; private set; }

        private readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        private AudioSource[] _voices;
        private int _next;

        public static bool Allow(IDictionary<string, float> last, string id, float now)
        {
            if (Limits.TryGetValue(id, out float min) && last.TryGetValue(id, out float t) && now - t < min) return false;
            last[id] = now;
            return true;
        }

        public static float DropVolume(float guc) => Mathf.Min(1f, 0.35f + guc * 0.4f);
        public static int ComboIndex(int depth) => Mathf.Clamp(depth, 0, 10);

        public void Select() => Play("dokun", S.SfxSelect, 1f, false);
        public void Salvo() => Play("kuleAt", S.SfxSalvo, 1f, true);
        public void Hit() => Play("carpma", S.SfxHit != null && S.SfxHit.Length > 0 ? S.SfxHit[Random.Range(0, S.SfxHit.Length)] : null, 1f, false);
        public void Break() => Play("kirildi", S.SfxBreak, 1f, true);
        public void Combo(int depth) => Play("kombo", S.SfxCombo != null && S.SfxCombo.Length == 11 ? S.SfxCombo[ComboIndex(depth)] : null, 1f, false);
        public void Drop(float guc) => Play("dustu", S.SfxDrop, DropVolume(guc), true);
        public void Rotate() => Play("dondur", S.SfxRotate, 1f, true);
        public void Minus() => Play("eksi", S.SfxMinus, 1f, true);
        public void Win() => Play("kazandi", S.SfxWin, 1f, true);
        public void Lose() => Play("kaybetti", S.SfxLose, 1f, true);

        private void Awake()
        {
            _voices = new AudioSource[8];
            for (int i = 0; i < _voices.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _voices[i] = src;
            }
        }

        private void Play(string id, AudioClip clip, float volume, bool sample)
        {
            if (clip == null || !Allow(_last, id, Time.unscaledTime)) return;
            LastClip = clip;
            LastVolume = volume;
            if (_voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = sample ? Random.Range(0.97f, 1.03f) : 1f;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }
    }
}
```

- [ ] **Step 5: `ColorHuntJuice`**

```csharp
using Project.Juice;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Feel of Pota's puzzle (renkAviFaz.ts HIS): additive shake (sars), game-time dips (zamanDusur), Pota's sounds, the "×d" and
    /// "Yetmiyor" popups. The game calls these at Pota's moments (the swell peaks, ball arrivals, landings).
    /// </summary>
    public class ColorHuntJuice : MonoBehaviour
    {
        public ColorHuntSettings S;
        public CameraShake Shake;
        public TimeScaleController TimeCtl;
        public PopupText Popups;
        public PotaSounds Sounds;

        /// <summary>secimiSur: a block comes under the finger.</summary>
        public void Select() => Sounds?.Select();

        /// <summary>salvoBaslat: kuleAt.</summary>
        public void SalvoStart() => Sounds?.Salvo();

        /// <summary>topVardi: carpma (guc 0.8).</summary>
        public void BallHit() => Sounds?.Hit();

        /// <summary>salvoBitir kirilma (at the tapped block's swell peak): shake 0.08, ×0.12 for 0.05 s, kirildi.</summary>
        public void Break()
        {
            Shake?.Add(S.ShakeTap);
            TimeCtl?.Dip(S.HitStopTapScale, S.HitStopTap);
            Sounds?.Break();
        }

        /// <summary>adimBaslat adimTepe (a chain step's first peak): shake, ×0.06 dip, kombo(d), "×d" from depth 2.</summary>
        public void Chain(int depth, Vector3 at)
        {
            Shake?.Add(S.ShakeChain + (depth - 1) * S.ShakeChainPerDepth);
            TimeCtl?.Dip(S.HitStopChainScale, Mathf.Min(S.HitStopChainMax, S.HitStopChainBase + depth * S.HitStopChainPerDepth));
            Sounds?.Combo(depth);
            // index.html .patlama.kombo: gold 44 px; ≥ 4: .kombo.buyuk 62 px #ff7a3d.
            if (depth >= 2) Popups?.Show(at, $"×{depth}", depth >= 4 ? PotaLook.Hex(0xff7a3d) : PotaLook.HudGold, depth >= 4 ? 96 : 72, false, 0.9f);
        }

        /// <summary>gorselleriIlerlet: dustu from a fall of one cell or more, louder with the distance.</summary>
        public void Landed(float cells)
        {
            if (cells >= 1f) Sounds?.Drop(Mathf.Min(1f, 0.3f + cells * 0.2f));
        }

        public void Rotate()
        {
            Shake?.Add(S.ShakeRotate);
            Sounds?.Rotate();
        }

        /// <summary>dokunmaReddi: eksi and "Yetmiyor · n top" (red, rising).</summary>
        public void Insufficient(Vector3 at, int cost)
        {
            Sounds?.Minus();
            Popups?.Show(at, $"Yetmiyor · {cost} top", PotaLook.HudRed, 44);
        }

        public void Result(bool won)
        {
            if (won) { Shake?.Add(S.ShakeWin); Sounds?.Win(); }
            else Sounds?.Lose();
        }
    }
}
```

(`PopupText.Show(Vector3, string, Color, int, bool = …, float = …)` is Core's existing signature — keep the argument pattern the old
`Chain` used.)

- [ ] **Step 6: Salvo**

`BallSalvo.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's salvo (renkAviFaz.ts salvoBaslat / toplariIlerlet / topVardi, yapbozSahne.ts cizToplar, patlamaEfekt.ts cizIzler): up to 24
    /// skinned balls (r 0.19) leave the strip under the frame over the first 55 % of the salvo; each flies 42 % of it on a smoothstep
    /// path bowing sin(πo)·0.9 toward the camera, swells 0.7 → 1.2 → 0.7, spins 14 rad/s on three axes and drags a soft #ffa040
    /// ribbon over its last 10 positions (one every 0.14 cells). Positions are board cells: balls and ribbons are children of the board.
    /// </summary>
    public class BallSalvo : MonoBehaviour
    {
        public ColorHuntSettings S;
        public Material BallMaterial;
        [Tooltip("The board the balls fly over (ColorHuntGame sets its BoardRoot).")]
        public Transform Board;

        private class Ball
        {
            public Transform T;
            public LineRenderer Trail;
            public Vector2 From, To;
            public float Delay, Flight, Clock, Spin, Scale;
            public readonly List<Vector3> Points = new List<Vector3>(PotaLook.TrailPoints);
        }

        private readonly List<Ball> _active = new List<Ball>();
        private readonly Stack<Ball> _pool = new Stack<Ball>();
        private readonly System.Random _rng = new System.Random();
        private Action<Vector2> _onArrive;
        private Material _trailMat;

        public bool Busy => _active.Count > 0;

        public void Fire(Vector2 source, Vector2 target, int cost, float duration, Action<Vector2> onArrive)
        {
            _onArrive = onArrive;
            int n = Mathf.Max(1, Mathf.Min(S.SalvoMaxBalls, cost));
            float launch = duration * S.SalvoLaunchFraction, flight = duration * S.SalvoFlightFraction;
            for (int i = 0; i < n; i++)
            {
                float o = n == 1 ? 0f : (float)i / (n - 1);
                var b = _pool.Count > 0 ? _pool.Pop() : Create();
                b.From = new Vector2(source.x + (R() - 0.5f) * S.SalvoSpread, source.y);
                b.To = new Vector2(target.x + (R() - 0.5f) * S.SalvoScatter, target.y + (R() - 0.5f) * S.SalvoScatter);
                b.Delay = o * launch;
                b.Flight = flight;
                b.Clock = 0f;
                b.Spin = R() * 6f;
                b.Scale = 1f;
                b.Points.Clear();
                b.Trail.positionCount = 0;
                b.T.gameObject.SetActive(false);
                b.Trail.gameObject.SetActive(true);
                _active.Add(b);
            }
        }

        public void Clear()
        {
            foreach (var b in _active) Recycle(b);
            _active.Clear();
        }

        public void Tick(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var b = _active[i];
                b.Clock += dt;
                float o = (b.Clock - b.Delay) / b.Flight;
                if (o < 0f) continue;
                if (o >= 1f)
                {
                    _active.RemoveAt(i);
                    Recycle(b);
                    _onArrive?.Invoke(b.To);
                    continue;
                }
                if (!b.T.gameObject.activeSelf) b.T.gameObject.SetActive(true);
                var p = PotaLook.SalvoPoint(b.From, b.To, o);
                b.Scale = PotaLook.SalvoScale(o);
                b.Spin += dt * PotaLook.SalvoSpin;
                b.T.localPosition = Local(p);
                b.T.localScale = Vector3.one * (0.38f * b.Scale);                  // sphere r 0.19 · olcek
                b.T.localRotation = Quaternion.Euler(new Vector3(b.Spin, b.Spin * 0.6f, b.Spin * 0.3f) * Mathf.Rad2Deg);
                if (b.Points.Count == 0 || Vector2.Distance(p, b.Points[0]) >= PotaLook.TrailSpacing)
                {
                    b.Points.Insert(0, p);
                    if (b.Points.Count > PotaLook.TrailPoints) b.Points.RemoveAt(b.Points.Count - 1);
                }
                DrawTrail(b);
            }
        }

        private static Vector3 Local(Vector3 pota) => new Vector3(pota.x, pota.y, -(PotaLook.FrontZ + 0.2f + pota.z));   // cizToplar z

        /// <summary>cizIzler: width 0.34·max(0.3, scale)·(1 − age)^0.6, brightness 1.35·(1 − age)^0.8, #ffa040, izDokusu.</summary>
        private void DrawTrail(Ball b)
        {
            int n = b.Points.Count;
            if (n < 2) { b.Trail.positionCount = 0; return; }
            b.Trail.positionCount = n;
            for (int i = 0; i < n; i++) b.Trail.SetPosition(i, Local(b.Points[i]));
            b.Trail.widthMultiplier = PotaLook.TrailWidth * Mathf.Max(0.3f, b.Scale) * (Board != null ? Board.lossyScale.x : 1f);
        }

        private float R() => (float)_rng.NextDouble();

        private void Recycle(Ball b)
        {
            b.T.gameObject.SetActive(false);
            b.Trail.positionCount = 0;
            b.Trail.gameObject.SetActive(false);
            _pool.Push(b);
        }

        private Ball Create()
        {
            var parent = Board != null ? Board : transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            if (BallMaterial != null) r.sharedMaterial = BallMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);

            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(parent, false);
            var line = trailGo.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 1;
            line.sharedMaterial = TrailMaterial();
            var width = new AnimationCurve();
            var alphas = new GradientAlphaKey[8];
            for (int i = 0; i <= 10; i++) width.AddKey(i / 10f, Mathf.Pow(1f - i / 10f, 0.6f));
            for (int i = 0; i < 8; i++) alphas[i] = new GradientAlphaKey(Mathf.Pow(1f - i / 7f, 0.8f), i / 7f);
            line.widthCurve = width;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(PotaLook.TrailColor, 0f), new GradientColorKey(PotaLook.TrailColor, 1f) }, alphas);
            line.colorGradient = g;
            line.positionCount = 0;
            trailGo.SetActive(false);
            return new Ball { T = go.transform, Trail = line };
        }

        private Material TrailMaterial()
        {
            if (_trailMat != null) return _trailMat;
            _trailMat = new Material(Shader.Find("Project/PotaAdditive")) { name = "PotaTrail" };
            _trailMat.SetTexture("_BaseMap", S.TrailSprite != null ? S.TrailSprite : Texture2D.whiteTexture);
            _trailMat.SetFloat("_Intensity", PotaLook.TrailBrightness);
            _trailMat.SetFloat("_SwapUV", 1f);
            return _trailMat;
        }
    }
}
```

- [ ] **Step 7: Wire it into the game and the scene**

`ColorHuntSettings.cs`: replace the `[Header("Juice")]` group with

```csharp
        [Header("Juice (renkAviFaz.ts HIS)")]
        [Tooltip("hitStop: game time ×0.12 for 0.05 s at the tapped block's swell peak.")]
        public float HitStopTapScale = 0.12f;
        public float HitStopTap = 0.05f;
        [Tooltip("zincirStop: ×0.06 for min(0.18, 0.09 + 0.02·depth) at a chain step's peak.")]
        public float HitStopChainScale = 0.06f;
        public float HitStopChainBase = 0.09f;
        public float HitStopChainPerDepth = 0.02f;
        public float HitStopChainMax = 0.18f;
        [Tooltip("sarsinti: additive, capped at 0.5, decays 2.2/s.")]
        public float ShakeTap = 0.08f;
        public float ShakeChain = 0.07f;
        public float ShakeChainPerDepth = 0.03f;
        public float ShakeWin = 0.14f;
        public float ShakeRotate = 0.05f;
```

and `public float RotateTime = 0.42f;` with the tooltip `"renkAviFaz.ts RENK_AVI_HIS.donus: smoothstep turn."`.
`PotaLookImportTool.Import()`: after the palette line add `s.RotateTime = 0.42f;   // RENK_AVI_HIS.donus (the asset still had 0.34)`.

`BoardBuilder.cs`: delete `Margin`.

`ColorHuntGame.cs`:
- delete `_launcher` (field and its assignment in `Start`); in `Start` after the Fx lines add `if (Salvo != null) Salvo.Board = BoardRoot;`;
- add

```csharp
        /// <summary>yapbozSahne.ts secimGoster: the white ring on a cell (−1 hides it).</summary>
        private void ShowRing(int cell)
        {
            if (cell >= 0 && _views.TryGetValue(cell, out var v))
            {
                _selectionRing.SetActive(true);
                _selectionRing.transform.localPosition = new Vector3(v.Home.x, v.Home.y, -(PotaLook.FrontZ + 0.08f));
            }
            else _selectionRing.SetActive(false);
        }
```

  and make `Select` use it: `_selected = cell; ShowRing(cell); if (cell >= 0 && _views.TryGetValue(cell, out var v)) { v.Squash = Mathf.Max(v.Squash, 0.35f); Juice?.Select(); }`;
- `Tap` insufficient branch: `Juice?.Insufficient(BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.55f, -PotaLook.LabelZ)), r.Cost);`
  (compute inside the `TryGetValue` block; with no view use `CellWorld(cell)`);
- `StartSalvo` becomes:

```csharp
        private void StartSalvo(SalvoJob s)
        {
            s.Started = true;
            var view = ViewAt(s.Cell);
            var target = view != null ? view.Home : BoardBuilder.CellPosition(Board.Size, ColorHuntRules.Row(Board.Size, s.Cell), ColorHuntRules.Col(Board.Size, s.Cell));
            Salvo?.Fire(PotaLook.SalvoSource(Board.Size), target, s.Cost, S.SalvoDuration, hit =>
            {
                s.Arrived++;
                var v = ViewAt(s.Cell);
                if (v != null)
                {
                    v.Flash = Mathf.Max(v.Flash, 0.55f);
                    v.Squash = Mathf.Max(v.Squash, 0.5f);
                    if (s.Arrived % 3 == 1) Fx?.Particles(hit, S.PaletteColor(v.Block.Color), 3, 1.5f, 0.07f);
                }
                Juice?.BallHit();
            });
            if (view != null) { view.Flash = Mathf.Max(view.Flash, 0.4f); view.Squash = 0.35f; }
            ShowRing(s.Cell);
            Juice?.SalvoStart();
        }
```

- `FinishSalvo`: after `Salvo?.Clear();` add `ShowRing(-1);`; replace `Juice?.Tap(pos, s.Cost);` with `Juice?.Break();`;
- `StartStep`: replace `Juice?.Chain(st.Depth, center);` with the world point above the first burst block,
  `Juice?.Chain(st.Depth, BoardRoot.TransformPoint(new Vector3(first.x, first.y + 0.5f, -PotaLook.LabelZ)));` where `first` is the `Pos` of
  the first exploding view (keep `center` out; delete it);
- `RippleNeighbours`: `S.NeighbourRipple * 3f` → `PotaLook.NeighbourSquash`;
- landing: `Juice?.Landed(d.Distance);` stays (it now plays only from one cell up).

`ColorHuntSettings.cs`: delete `ShakeBallHit` and `NeighbourRipple`.

`ColorHuntSceneTool.BuildGame`: `salvo.BallMaterial = settings.SalvoBall;` (not `BallMaterial`); after `var chJuice = …` add

```csharp
            var soundsGo = new GameObject("PotaSounds");
            soundsGo.transform.SetParent(juice.Host.transform, false);
            var sounds = soundsGo.AddComponent<PotaSounds>();
            sounds.S = settings;
```

and set `chJuice.Sounds = sounds;` (drop `chJuice.Sfx = juice.Sfx;`: the puzzle no longer uses SynthSfx; PotaFlow's director keeps its own).

- [ ] **Step 8: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests Project.Tests.PotaSoundsTests Project.Tests.ColorHuntJuiceTests`
Expected: all PASS.
Run `ai_potalook_import`, rebuild both scenes, then `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile`
Expected: all PASS (incl. `PotaLookPlayTests.Salvo_FliesMinCost24Balls_OnTheBoard_ThenEnds`).

- [ ] **Step 9: Look and listen**

Play `ColorHunt.unity` at `slowmo` 0.2, tap a costly block, screenshot mid-salvo. Expected: skinned balls rising from under the frame
on arcs toward the camera, growing then shrinking, each with a short orange-gold ribbon. With Unity focused, tap once at normal speed:
salvo whoosh, the patter of hits, the pop; then chain steps climbing the pentatonic ladder.

- [ ] **Step 10: Commit**

```bash
git add Assets/_Project/Games/Pota
git commit -m "feat: Pota's salvo, ball trail, sounds and feel in Color Hunt (additive shake, dips, rendered Pota sounds)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 9: The animation queue in Pota's order — swell-then-burst, chain peaks, drops, rotation fit, win dispersal

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/SwellSet.cs`
- Modify: `ColorHuntGame.cs` (swells, loose tapped block, chain peaks, rotation, win), `ColorHuntSettings.cs` (+`TapSwellTime, TapSwell, ChainSwell`)
- Test: `Tests/EditMode/SwellSetTests.cs` (new), `Tests/PlayMode/PotaLookPlayTests.cs` (+2)

**Interfaces:**
- Consumes: `PotaLook.Swell/SwellPeakAt/Smoothstep/RotationFit/GridScale/LabelCounterAngle` (Task 2), `BlockView.Swell/LabelAngle` (Task 4),
  `ColorHuntFx.Burst/Puff/Particles/Confetti` (Task 7), `ColorHuntJuice.Break/Chain` (Task 8), `CountItem` (Task 6).
- Produces: `SwellSet<T>` (`Start(T key, float duration, float peak, bool loose, Action atPeak)`, `Advance(float dt, Action<T, float, float> apply,
  List<T> finishedLoose)`, `End(T key)`, `Clear()`, `Count`, `AnyLoose`, `Contains`); `ColorHuntGame.Busy` also covers a swelling tapped block.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Games/Pota/Tests/EditMode/SwellSetTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;

namespace Project.Tests
{
    public class SwellSetTests
    {
        [Test]
        public void Peak_FiresOnce_At55Percent()
        {
            var set = new SwellSet<object>();
            var k = new object();
            int fired = 0;
            float scale = 0f;
            set.Start(k, 0.15f, 1.24f, false, () => fired++);
            set.Advance(0.08f, (key, s, o) => scale = s, null);                 // o 0.533
            Assert.AreEqual(0, fired);
            Assert.Greater(scale, 1.2f);
            set.Advance(0.005f, null, null);                                    // o 0.567
            Assert.AreEqual(1, fired, "renkAviFaz.ts patlamaIlerlet: o ≥ tepe");
            set.Advance(0.05f, (key, s, o) => scale = s, null);
            Assert.AreEqual(1, fired, "once");
            Assert.Less(scale, 1.24f, "collapsing");
        }

        [Test]
        public void PeakFires_Once_EvenIfTheStepEndsFirst()
        {
            var set = new SwellSet<object>();
            var k = new object();
            int fired = 0;
            set.Start(k, 0.15f, 1.24f, false, () => fired++);
            set.Advance(0.03f, null, null);
            Assert.AreEqual(0, fired);
            set.End(k);
            Assert.AreEqual(1, fired, "gorselSil → tepeyiTetikle: the HUD count must not be lost");
            set.Advance(1f, null, null);
            set.End(k);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void NewBoard_ForgetsPendingBursts()
        {
            var set = new SwellSet<object>();
            int fired = 0;
            set.Start(new object(), 0.15f, 1.24f, true, () => fired++);
            set.Clear();
            set.Advance(1f, null, null);
            Assert.AreEqual(0, fired);
            Assert.IsFalse(set.AnyLoose);
        }

        [Test]
        public void LooseEntries_FinishAndReportBack()
        {
            var set = new SwellSet<object>();
            var k = new object();
            int fired = 0;
            var done = new List<object>();
            set.Start(k, 0.11f, 1.18f, true, () => fired++);
            Assert.IsTrue(set.AnyLoose, "a swelling tapped block keeps the board busy (sonenler)");
            set.Advance(0.07f, null, done);
            Assert.AreEqual(1, fired);
            Assert.IsEmpty(done);
            set.Advance(0.05f, null, done);
            CollectionAssert.AreEqual(new[] { k }, done);
            Assert.IsFalse(set.AnyLoose);
            Assert.AreEqual(0, set.Count);
        }
    }
}
```

Append to `PotaLookPlayTests.cs`:

```csharp
        [UnityTest]
        public IEnumerator TappedBlock_SwellsTo1Point18_AndBurstsAtItsPeak()
        {
            ColorHuntGame game = null;
            yield return LoadPlaying(g => game = g);
            int cell = Affordable(game, 1);
            var view = game.ViewAt(cell);
            var flash = game.Fx.transform.Find("FlashWhite").GetComponent<ParticleSystem>();
            game.Tap(cell);
            yield return WaitUntil(() => view.Swell >= 0f, 3f);
            float start = Time.time, maxScale = 0f, burstAt = -1f;
            while (view.Swell >= 0f && view.gameObject.activeSelf && Time.time - start < 1f)
            {
                maxScale = Mathf.Max(maxScale, view.transform.localScale.x);
                if (burstAt < 0f && flash.particleCount > 0) burstAt = Time.time - start;
                yield return null;
            }
            Assert.That(maxScale, Is.InRange(1.1f, 1.181f), "balon.dokunusSisme 1.18");
            Assert.That(burstAt, Is.InRange(0.03f, 0.1f), "the burst waits for the peak (0.0605 s of game time; frame granularity)");
        }

        [UnityTest]
        public IEnumerator Rotation_ShrinksToFit_LabelsRideThenStandUp()
        {
            ColorHuntGame game = null;
            yield return LoadPlaying(g => game = g);
            var v = game.ViewAt(0);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            float minScale = 10f, t0 = Time.time;
            bool rodeEarly = true, stoodLate = false;
            while (game.Busy && Time.time - t0 < 2f)
            {
                float u = (Time.time - t0) / game.S.RotateTime;
                minScale = Mathf.Min(minScale, game.BoardRoot.localScale.x);
                if (u < 0.65f && game.ViewAt(0) == v) rodeEarly &= Mathf.Abs(v.LabelAngle) < 1e-3f;
                if (u > 0.85f && u < 1f && game.ViewAt(0) == v) stoodLate |= Mathf.Abs(v.LabelAngle) > 30f;
                yield return null;
            }
            Assert.Less(minScale, 1.2f * 0.85f, "donusSigdirma ≈ 0.8 at 45°");
            Assert.IsTrue(rodeEarly, "labels ride the board for the first 70 %");
            Assert.IsTrue(stoodLate, "then turn upright");
            Assert.AreEqual(1.2f, game.BoardRoot.localScale.x, 1e-4f);
            Assert.AreEqual(0f, game.ViewAt(0).LabelAngle, 1e-4f, "new views after the turn");
        }
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SwellSetTests`
Expected: exit 2, `error CS0246: The type or namespace name 'SwellSet<>' could not be found`.

- [ ] **Step 3: `SwellSet`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's swell-then-burst bookkeeping (renkAviFaz.ts patlamaIlerlet / tepeyiTetikle / gorselSil / sonenler): each entry swells
    /// linearly to its peak scale at 55 % of its time, then collapses; its burst runs exactly once — at the peak, or first thing when the
    /// entry is ended early (the step moves on) — and never after <see cref="Clear"/> (a new board). Loose entries (the tapped block,
    /// already out of its cell) report back when they finish so the caller can recycle them.
    /// </summary>
    public class SwellSet<T> where T : class
    {
        private class Entry { public float Time, Duration, Peak; public Action AtPeak; public bool Loose; }

        private readonly Dictionary<T, Entry> _entries = new Dictionary<T, Entry>();
        private readonly List<T> _keys = new List<T>();

        public int Count => _entries.Count;

        public bool AnyLoose
        {
            get
            {
                foreach (var e in _entries.Values) if (e.Loose) return true;
                return false;
            }
        }

        public bool Contains(T key) => _entries.ContainsKey(key);

        public void Start(T key, float duration, float peak, bool loose, Action atPeak) =>
            _entries[key] = new Entry { Duration = Mathf.Max(1e-4f, duration), Peak = peak, Loose = loose, AtPeak = atPeak };

        /// <summary>Advances every entry and hands (key, scale, o) to <paramref name="apply"/>; finished loose keys go to <paramref name="finishedLoose"/>.</summary>
        public void Advance(float dt, Action<T, float, float> apply, List<T> finishedLoose)
        {
            _keys.Clear();
            _keys.AddRange(_entries.Keys);
            foreach (var k in _keys)
            {
                if (!_entries.TryGetValue(k, out var e)) continue;
                e.Time += dt;
                float o = Mathf.Min(1f, e.Time / e.Duration);
                apply?.Invoke(k, PotaLook.Swell(o, e.Peak), o);
                if (o >= PotaLook.SwellPeakAt) Fire(e);
                if (o >= 1f && e.Loose)
                {
                    _entries.Remove(k);
                    finishedLoose?.Add(k);
                }
            }
        }

        /// <summary>gorselSil: the block leaves its cell; a burst still waiting runs first.</summary>
        public void End(T key)
        {
            if (!_entries.TryGetValue(key, out var e)) return;
            _entries.Remove(key);
            Fire(e);
        }

        /// <summary>A new board (kur): forget everything, run nothing.</summary>
        public void Clear() => _entries.Clear();

        private static void Fire(Entry e)
        {
            var f = e.AtPeak;
            if (f == null) return;
            e.AtPeak = null;
            f();
        }
    }
}
```

- [ ] **Step 4: The queue**

`ColorHuntSettings.cs` (Timing):

```csharp
        [Tooltip("RENK_AVI_HIS.balon: the tapped block swells to 1.18 in 0.11 s and bursts at 55 %; chain blocks to 1.24 in ChainTime.")]
        public float TapSwellTime = 0.11f;
        public float TapSwell = 1.18f;
        public float ChainSwell = 1.24f;
```

`ColorHuntGame.cs`:
- Fields: delete `_explosions`; add

```csharp
        private readonly SwellSet<BlockView> _swells = new SwellSet<BlockView>();
        private readonly List<BlockView> _loose = new List<BlockView>();      // out of their cell, still swelling (Pota sonenler)
        private readonly List<BlockView> _finished = new List<BlockView>();
        private float _disperse = -1f;
```

- `Busy`: `public bool Busy => _jobs.Count > 0 || (Salvo != null && Salvo.Busy) || _swells.AnyLoose;`
- `RotateJob` case in `AdvanceJobs`:

```csharp
                case RotateJob r:
                    r.T += dt;
                    float u = Mathf.Min(1f, r.T / S.RotateTime);
                    float angle = r.Angle * PotaLook.Smoothstep(u);                         // RENK_AVI_HIS.donus, smoothstep
                    BoardRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
                    BoardRoot.localScale = Vector3.one * (PotaLook.GridScale(Board.Size) * PotaLook.RotationFit(Board.Size, angle * Mathf.Deg2Rad));
                    float labels = PotaLook.LabelCounterAngle(angle, u);                     // ride the board, stand up after 70 %
                    foreach (var v in _views.Values) v.LabelAngle = labels;
                    if (u >= 1f)
                    {
                        BoardRoot.localRotation = Quaternion.identity;
                        BoardRoot.localScale = Vector3.one * PotaLook.GridScale(Board.Size);
                        _jobs.RemoveAt(0);
                        var pending = new List<Job>(_jobs);
                        ClearViews();
                        _jobs.AddRange(pending);
                        BuildViews(r.Rotated);
                    }
                    break;
```

- `FinishSalvo`:

```csharp
        /// <summary>
        /// renkAviFaz.ts salvoBitir: the tapped block leaves its cell and swells to 1.18 in 0.11 s; at the peak — shake, ×0.12 dip,
        /// kirildi, puff 1.15, 14 hot points, the burst, the sticker. Its four neighbours flinch now. The steps queue behind it.
        /// </summary>
        private void FinishSalvo(SalvoJob s)
        {
            Salvo?.Clear();
            ShowRing(-1);
            var view = ViewAt(s.Cell);
            if (view != null)
            {
                _views.Remove(s.Cell);
                _loose.Add(view);
                var tone = S.PaletteColor(view.Block.Color);
                _swells.Start(view, S.TapSwellTime, S.TapSwell, true, () =>
                {
                    Juice?.Break();
                    Fx?.Puff(view.Pos, 1.15f);
                    Fx?.Particles(view.Pos, tone, S.PointsBase + 4, 2.6f, 0.12f);
                    Fx?.Burst(view.Pos, tone, 0);
                    CountItem(view, BoardRoot.TransformPoint(new Vector3(view.Pos.x, view.Pos.y, -PotaLook.FrontZ)));
                });
                RippleNeighbours(s.Cell);
            }
            else Juice?.Break();
            DisplayedAmmo = Board.Ammo;
            Raise(AmmoChanged);
            _jobs.RemoveAt(0);
            int depth = -1;
            foreach (var step in s.Steps)
            {
                depth++;
                _jobs.Add(new StepJob { Step = step, Depth = depth });
            }
            if (depth > LongestChain) LongestChain = depth;
        }
```

- `StartStep`:

```csharp
        /// <summary>
        /// renkAviFaz.ts adimBaslat: depth 0 drops at once; deeper steps swell every matched block to 1.24 over ChainTime. The first peak
        /// carries the step's feedback (shake, ×0.06 dip, kombo, "×d"); each block's peak its own puff, points, burst and sticker.
        /// </summary>
        private void StartStep(StepJob st)
        {
            st.Started = true;
            if (st.Depth == 0 || st.Step.Exploded.Count == 0) { StartDrops(st); return; }
            int d = st.Depth;
            bool peaked = false;
            BlockView first = null;
            void StepPeak()
            {
                if (peaked) return;
                peaked = true;
                var at = first != null ? first.Pos : Vector2.zero;
                Juice?.Chain(d, BoardRoot.TransformPoint(new Vector3(at.x, at.y + 0.5f, -PotaLook.LabelZ)));
            }
            int points = PotaFx.PointCount(S, d);
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
                    Fx?.Puff(v.Pos, 1f + d * 0.12f);
                    Fx?.Particles(v.Pos, tone, points, 2.2f + d * 0.3f, 0.1f);
                    Fx?.Burst(v.Pos, tone, d);
                    CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                });
            }
            if (first == null) StepPeak();
        }
```

- `StartDrops`: the first loop becomes

```csharp
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                _swells.End(v);                 // gorselSil: a burst that has not peaked yet runs now
                Recycle(v);
                _views.Remove(i);
            }
```

  and the drop time uses `PotaLook.StepDropTime(longest, S.DropTimePerCell)`.
- `AdvanceViews`: replace the `_explosions` loop with

```csharp
            _finished.Clear();
            _swells.Advance(dt, (v, scale, o) => { v.Swell = scale; v.Flash = Mathf.Max(v.Flash, o * o); }, _finished);
            foreach (var v in _finished) { _loose.Remove(v); Recycle(v); }
            if (_disperse >= 0f)
            {
                _disperse -= dt;
                if (_disperse < 0f) { foreach (var v in _views.Values) Recycle(v); _views.Clear(); _swells.Clear(); }
            }
```

  and the landing branch computes the fall with `PotaLook.DropTravel(d.T, S.DropTimePerCell)` (delete `_accel`).
- `TickViews`: also tick the loose ones: `foreach (var v in _loose) v.Tick(dt);`
- `End`:

```csharp
        private void End(bool won, string reason)
        {
            Current = State.Ended;
            Won = won;
            _endReason = reason;
            Select(-1);
            Juice?.Result(won);
            if (won) Disperse();
            _resultTimer = won ? S.WinWait : S.LoseWait;
        }

        /// <summary>
        /// renkAviFaz.ts tahtayiDagit: every block gives six hot points and swells to 1.24 over 0.5 s; the views go after 0.52 s. The paper
        /// confetti comes too (Pota drops it at the dunk, which is a later step; the spec keeps it at the win).
        /// </summary>
        private void Disperse()
        {
            foreach (var v in _views.Values)
            {
                Fx?.Particles(v.Pos, S.PaletteColor(v.Block.Color), 6, 3.5f, 0.1f);
                _swells.Start(v, 0.5f, S.ChainSwell, false, null);
            }
            Fx?.Confetti(Board.Size, S.ConfettiCount);
            _disperse = 0.52f;
        }
```

- `ClearViews`: add `_swells.Clear(); foreach (var v in _loose) Recycle(v); _loose.Clear(); _disperse = -1f;` and delete `_explosions.Clear();`.
- `NewView` reset line already clears `Swell`/`LabelAngle` (Task 4).

- [ ] **Step 5: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SwellSetTests` → Expected: 4/4 PASS.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile` → Expected: all PASS.
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → Expected: all PASS (incl. `PotaLookPlayTests` 3/3, HUD target tests, PotaFlow 14/14).

- [ ] **Step 6: Watch it**

Play `ColorHunt.unity`; `ai_colorhunt_play {"action":"auto","moves":6}`; watch in the Game view (focus Unity) and take a screenshot during
a chain. Expected: after each salvo the tapped block puffs up and pops; a chain step's blocks swell together and burst with one
"×d"; new blocks fall with a small squash; turning the board shrinks it inside the frame while the numbers ride along, then stand up.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Games/Pota
git commit -m "feat: Color Hunt animation queue in Pota's order — swell-then-burst peaks, chain feedback, rotation fit, win dispersal

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 10: PotaFlow — the same puzzle look after the basket, blended in during the flight

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/PuzzleLighting.cs`
- Modify: `Scripts/Flow/PotaFlowDirector.cs` (stage via `PuzzleStage`, `Look` blend), `Scripts/Flow/PotaFlowSettings.cs` (`BoardFov 38`,
  `BoardFogStart 30`, `BoardFogEnd 130`), `Editor/PotaFlowSceneTool.cs` (post on, no basket volume, lighting blend)
- Test: `Tests/EditMode/PuzzleLightingTests.cs` (new), `Tests/EditMode/BoardRevealTests.cs` (defaults), `Tests/PlayMode/PotaFlowPlayTests.cs` (+1)

**Interfaces:**
- Consumes: `PuzzleStage` (Task 5), `PotaLook.Sun*/Ambient*/Sky/Fog` (Tasks 2, 5), `ColorHuntSettings.PuzzleProfile` (Task 1), `SceneParts.EnablePost` (Task 5).
- Produces: `PuzzleLighting` (`Sun`, `Fill`, `Cam`, `Source`; `Tone`, `Post` volumes built in `Awake`; `Capture()`, `Blend(float f)`, `Current`;
  static `Split(VolumeProfile puzzle, out VolumeProfile tone, out VolumeProfile post)`); `PotaFlowDirector.Look`, `PotaFlowDirector.Stage`.

- [ ] **Step 1: Write the failing tests**

`BoardRevealTests.Settings_DefaultsMatchTheSpec`: `34f` → `38f` (yapbozSahne.ts GORUS_ACISI), `60f` → `30f`, `220f` → `130f` (saha.ts fog).

`Assets/_Project/Games/Pota/Tests/EditMode/PuzzleLightingTests.cs`:

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using Project.Flow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Tests
{
    public class PuzzleLightingTests
    {
        private GameObject _go;
        private Color _sky, _eq, _ground, _fog;

        [SetUp]
        public void SetUp()
        {
            _sky = RenderSettings.ambientSkyColor; _eq = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor; _fog = RenderSettings.fogColor;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            RenderSettings.ambientSkyColor = _sky; RenderSettings.ambientEquatorColor = _eq;
            RenderSettings.ambientGroundColor = _ground; RenderSettings.fogColor = _fog;
        }

        private static VolumeProfile Puzzle()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            p.Add<Bloom>(true);
            p.Add<Vignette>(true);
            p.Add<ColorAdjustments>(true);
            return p;
        }

        [Test]
        public void Split_PutsOnlyTonemappingInTheToneProfile()
        {
            PuzzleLighting.Split(Puzzle(), out var tone, out var post);
            Assert.IsTrue(tone.Has<Tonemapping>());
            Assert.AreEqual(1, tone.components.Count);
            Assert.IsFalse(post.Has<Tonemapping>());
            Assert.IsTrue(post.Has<Bloom>() && post.Has<Vignette>() && post.Has<ColorAdjustments>());
        }

        [Test]
        public void Blend_GoesFromTheBasketToPotasPuzzle()
        {
            _go = new GameObject("Look");
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(_go.transform);
            sun.intensity = 1.5f; sun.color = Color.white; sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(_go.transform);
            fill.intensity = 0.35f;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(_go.transform);
            cam.backgroundColor = Color.red;
            var look = _go.AddComponent<PuzzleLighting>();
            look.Sun = sun; look.Fill = fill; look.Cam = cam; look.Source = Puzzle();
            look.Build();
            look.Blend(0f);
            Assert.AreEqual(1.5f, sun.intensity, 1e-5f, "basket as approved");
            Assert.AreEqual(0f, look.Post.weight);
            Assert.AreEqual(0f, look.Tone.weight);
            look.Blend(0.4f);
            Assert.AreEqual(0.4f, look.Post.weight, 1e-5f, "exposure, bloom, vignette blend in");
            Assert.AreEqual(0f, look.Tone.weight, "Neutral switches on at the flight's midpoint");
            look.Blend(1f);
            Assert.AreEqual(PotaLook.SunIntensity / Mathf.PI, sun.intensity, 1e-4f, "saha.ts sun 2.1, ÷π");
            Assert.Less(Quaternion.Angle(sun.transform.rotation, Quaternion.LookRotation(PotaLook.SunDirection)), 0.01f);
            Assert.AreEqual(0f, fill.intensity, 1e-5f, "Pota has no fill light");
            Assert.AreEqual(1f, look.Tone.weight);
            Assert.AreEqual(PotaLook.Sky.b, cam.backgroundColor.b, 1e-4f);
            Assert.AreEqual(PotaLook.Fog.g, RenderSettings.fogColor.g, 1e-4f);
            look.Blend(0f);
            Assert.AreEqual(1.5f, sun.intensity, 1e-5f, "and back for the next basket round");
            Assert.AreEqual(Color.red, cam.backgroundColor);
        }
    }
}
```

Append to `PotaFlowPlayTests.cs` (uses its `Load` / `RunTransition` helpers):

```csharp
        [UnityTest]
        public IEnumerator Puzzle_HasPotasLook_AndTheBasketKeepsItsOwn()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            Assert.IsTrue(d.Cam.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.AreEqual(0f, d.Look.Post.weight, "basket phase: no puzzle post");
            Assert.AreEqual(1.2f, d.Game.BoardRoot.localScale.x, 1e-4f);
            Assert.IsNotNull(d.Stage.Reflection);
            d.BeginTransition(158);
            RunTransition(d);
            Assert.AreEqual(1f, d.Look.Post.weight, 1e-4f);
            Assert.AreEqual(1f, d.Look.Tone.weight);
            Assert.AreEqual(38f, d.Cam.fieldOfView, 1e-3f);
            Assert.Less(Vector3.Distance(d.Stage.Hoop.RimCenter, d.Game.transform.position + new Vector3(0f, -5.6f, -2.2f)), 1e-3f);
        }
```

(add `using UnityEngine.Rendering.Universal;` to `PotaFlowPlayTests.cs`).

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PuzzleLightingTests Project.Tests.BoardRevealTests`
Expected: exit 2, `'PuzzleLighting' does not exist` (after it compiles: `Settings_DefaultsMatchTheSpec` fails on 34 ≠ 38).

- [ ] **Step 3: `PuzzleLighting`**

```csharp
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Flow
{
    /// <summary>
    /// Blends PotaFlow's world from the approved basket lighting (f = 0) to Pota's puzzle (f = 1, saha.ts): sun, fill, trilight ambient,
    /// fog colour, sky colour and the puzzle post — exposure, bloom and vignette by weight; Neutral tonemapping is not interpolable, so
    /// it switches on at the flight's midpoint, where the camera moves fastest. The director drives <see cref="Blend"/>.
    /// </summary>
    public class PuzzleLighting : MonoBehaviour
    {
        public Light Sun;
        public Light Fill;
        public Camera Cam;
        [Tooltip("ColorHuntSettings.PuzzleProfile: split at runtime into a tonemapping volume and a post volume.")]
        public VolumeProfile Source;

        public Volume Tone { get; private set; }
        public Volume Post { get; private set; }
        public float Current { get; private set; } = -1f;

        private Quaternion _sunRot;
        private Color _sunColor, _ambSky, _ambEq, _ambGround, _fog, _sky;
        private float _sunI, _fillI;
        private bool _captured;

        private void Awake() => Build();

        public void Build()
        {
            if (Tone != null || Source == null) return;
            Split(Source, out var tone, out var post);
            Tone = MakeVolume("Puzzle Tone", tone);
            Post = MakeVolume("Puzzle Post", post);
        }

        private Volume MakeVolume(string name, VolumeProfile profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.weight = 0f;
            v.sharedProfile = profile;
            return v;
        }

        /// <summary>Two runtime profiles: the tonemapping override alone, and everything else.</summary>
        public static void Split(VolumeProfile puzzle, out VolumeProfile tone, out VolumeProfile post)
        {
            tone = ScriptableObject.CreateInstance<VolumeProfile>();
            if (puzzle.TryGet(out Tonemapping src)) tone.Add<Tonemapping>(false).mode.Override(src.mode.value);
            post = Object.Instantiate(puzzle);
            post.Remove<Tonemapping>();
        }

        /// <summary>Remembers the basket's lighting (called on the first blend).</summary>
        public void Capture()
        {
            _captured = true;
            if (Sun != null) { _sunRot = Sun.transform.rotation; _sunColor = Sun.color; _sunI = Sun.intensity; }
            if (Fill != null) _fillI = Fill.intensity;
            _ambSky = RenderSettings.ambientSkyColor;
            _ambEq = RenderSettings.ambientEquatorColor;
            _ambGround = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fogColor;
            if (Cam != null) _sky = Cam.backgroundColor;
        }

        public void Blend(float f)
        {
            if (!_captured) Capture();
            f = Mathf.Clamp01(f);
            Current = f;
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Slerp(_sunRot, Quaternion.LookRotation(PotaLook.SunDirection), f);
                Sun.color = Color.Lerp(_sunColor, PotaLook.SunColor, f);
                Sun.intensity = Mathf.Lerp(_sunI, PotaLook.SunIntensity * PotaLook.ThreeToUnityLight, f);
            }
            if (Fill != null) Fill.intensity = Mathf.Lerp(_fillI, 0f, f);
            RenderSettings.ambientSkyColor = Color.Lerp(_ambSky, PotaLook.AmbientSky.gamma, f);
            RenderSettings.ambientEquatorColor = Color.Lerp(_ambEq, PotaLook.AmbientEquator.gamma, f);
            RenderSettings.ambientGroundColor = Color.Lerp(_ambGround, PotaLook.AmbientGround.gamma, f);
            RenderSettings.fogColor = Color.Lerp(_fog, PotaLook.Fog, f);
            if (Cam != null) Cam.backgroundColor = Color.Lerp(_sky, PotaLook.Sky, f);
            if (Post != null) Post.weight = f;
            if (Tone != null) Tone.weight = f >= 0.5f ? 1f : 0f;
        }
    }
}
```

- [ ] **Step 4: Director and scene tool**

`PotaFlowSettings.cs`: `public float BoardFov = 38f;` (`// yapbozSahne.ts GORUS_ACISI`), `public float BoardFogStart = 30f;`,
`public float BoardFogEnd = 130f;` (`// saha.ts fog 30–130`). Sync the asset: `execute_code` with
`var so = new UnityEditor.SerializedObject(UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/_Project/Games/Pota/Settings/PotaFlowSettings.asset")); so.FindProperty("BoardFov").floatValue = 38f; so.FindProperty("BoardFogStart").floatValue = 30f; so.FindProperty("BoardFogEnd").floatValue = 130f; so.ApplyModifiedPropertiesWithoutUndo(); UnityEditor.AssetDatabase.SaveAssets();`

`PotaFlowDirector.cs`:
- fields: `[Header("Look")] public PuzzleLighting Look;` and `public PuzzleStage Stage { get; private set; }`;
- `Start`: replace `CourtBuilder.Build(transform, S);` and the five hoop lines with

```csharp
            var stageGo = new GameObject("Stage");
            stageGo.transform.SetParent(transform, false);
            Stage = stageGo.AddComponent<PuzzleStage>();
            Stage.BuildOnStart = false;
            Stage.Game = Game;
            Stage.GroundBelow = Game.transform.position.y;                      // the roof is world y = 0
            Stage.CourtSize = S.CourtSize;
            Stage.CourtNear = Game.transform.position.z - S.CourtStartZ;
            Stage.CourtColor = S.CourtColor;
            Stage.CourtLineColor = S.CourtLineColor;
            Stage.BallRadius = S.BallRadius;
            Stage.Build();
            Hoop = Stage.Hoop;
```

  and at the end of `Start` (before `EnterPhase(Phase.Basket)`) add `Look?.Blend(0f);`;
- `ApplyTransition`: after the two fog lines add `Look?.Blend(f);`
- `FinishTransition`: after the fog lines add `Look?.Blend(1f);`
- `ApplyReturn`: after the fog lines add `Look?.Blend(1f - u);`
- `FinishReturn`: after the fog lines add `Look?.Blend(0f);`

`PotaFlowSceneTool.Build`:
- `BasketSceneTool.CreateSun(); ColorHuntSceneTool.CreateFill();` → `var sun = BasketSceneTool.CreateSun(); var fill = ColorHuntSceneTool.CreateFill();`
- delete `SceneParts.CreateGlobalVolume(BasketSceneTool.VolumeProfilePath);` (post was never on, so it never applied; with post on it
  would change the approved basket) and add `SceneParts.EnablePost(cam);`
- after `d.S = flow;`:

```csharp
            var look = d.gameObject.AddComponent<PuzzleLighting>();
            look.Sun = sun;
            look.Fill = fill;
            look.Cam = cam;
            look.Source = board.PuzzleProfile;
            d.Look = look;
```

(`BasketSceneTool.CreateSun` must return the `Light` — it does, per its `public static Light CreateSun()` signature.)

- [ ] **Step 5: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PuzzleLightingTests Project.Tests.BoardRevealTests` → Expected: all PASS.
Sync the settings asset (Step 4 snippet), rebuild PotaFlow (`ai_potaflow_build_scene`), then
`~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → Expected: all PASS (PotaFlow 15/15).

- [ ] **Step 6: Record the transition**

Play `PotaFlow.unity`, run `execute_custom_tool {"tool_name":"ai_potaflow_record","parameters":{"action":"start","candidate":"A","count":158}}`,
poll `{"action":"status"}` until `result` starts with `done`, stop Play Mode, then build the contact sheet from `Temp/record/potaflow` with
`Tools/frame_sheet.py` (as in the PotaFlow README). Expected: the basket frames look as before (no bloom, same colours); during the flight the light warms toward
Pota's sun and the post fades in; the camera settles on the board inside the giant hoop at 38° with Pota's HUD sliding in.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Games/Pota
git commit -m "feat: PotaFlow lands on Pota's puzzle look — lighting and post blend in during the flight, 38° board camera, shared stage

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 11: Side by side with Pota — recordings, moment sheets, video, tuning by eye

**Files:**
- Create: `Assets/_Project/Games/Pota/Editor/ColorHuntRecordTool.cs` (`ai_colorhunt_record`), `Tools/potalook/record-pota.mjs`,
  `Tools/potalook/compare.py`, `Tools/potalook/test_compare.py`
- Modify: `ColorHuntGame.cs` (`Prepare(int ammo, uint seed, LevelDesign design)`)
- Test: `Tests/EditMode/ColorHuntRecordToolTests.cs` (script parser), `Tools/potalook/test_compare.py`
- Deliverables: `docs/screenshots/2026-09-30-potalook-{tap,rotate,win}.jpg`, `docs/screenshots/2026-09-30-potalook-side.mp4`

**Interfaces:**
- Consumes: everything above; `FrameRecorder` (`OpenFloatingGameView`, `Capture`, `Dispose`), `Tools/frame_sheet.py` (`load_frames`, `main`).
- Produces: `ColorHuntGame.Prepare(int ammo, uint seed, LevelDesign design)` (0 = settings seed / random; null = settings design);
  `ColorHuntRecordTool.Parse(string script)` → `List<ColorHuntRecordTool.Step>` (`Verb`, `Arg`, `Seconds`);
  `compare.py` (`MOMENTS`, `shifted_name(index, t, anchor)`, `main`).

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Games/Pota/Tests/EditMode/ColorHuntRecordToolTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Project.Editor.AITools;

namespace Project.Tests
{
    public class ColorHuntRecordToolTests
    {
        [Test]
        public void Parse_ReadsTheScript()
        {
            var steps = ColorHuntRecordTool.Parse("wait 0.6; select auto; tap 24 ;settle; rotate ccw");
            Assert.AreEqual(5, steps.Count);
            Assert.AreEqual("wait", steps[0].Verb);
            Assert.AreEqual(0.6f, steps[0].Seconds, 1e-6f);
            Assert.AreEqual("select", steps[1].Verb);
            Assert.AreEqual("auto", steps[1].Arg);
            Assert.AreEqual("tap", steps[2].Verb);
            Assert.AreEqual("24", steps[2].Arg);
            Assert.AreEqual("settle", steps[3].Verb);
            Assert.AreEqual("ccw", steps[4].Arg);
        }

        [Test]
        public void Parse_RejectsUnknownVerbs() => Assert.Throws<ArgumentException>(() => ColorHuntRecordTool.Parse("jump 3"));
    }
}
```

`Tools/potalook/test_compare.py`:

```python
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("compare", os.path.join(HERE, "compare.py"))
compare = importlib.util.module_from_spec(spec)
sys.path.insert(0, os.path.dirname(HERE))
spec.loader.exec_module(compare)


def test_shifted_name_puts_the_anchor_at_one_second():
    assert compare.shifted_name(3, 2.5, 2.0) == "f_00003_t1.500.png"
    assert compare.shifted_name(0, 1.2, 2.0) == "f_00000_t0.200.png"


def test_frames_before_the_window_are_skipped():
    assert compare.shifted_name(0, 0.5, 2.0) is None


def test_moments_cover_the_spec():
    assert set(compare.MOMENTS) == {"tap", "rotate", "win"}
    assert 0.5605 in compare.MOMENTS["tap"]
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntRecordToolTests` → Expected: exit 2, `'ColorHuntRecordTool' does not exist`.
Run: `~/.local/bin/uv run --with pytest --with pillow pytest Tools/potalook/test_compare.py -q` → Expected: errors, `compare.py` not found.

- [ ] **Step 3: Game hook, record tool**

`ColorHuntGame.cs`: `Prepare(int ammo)` becomes `public void Prepare(int ammo) => Prepare(ammo, 0, null);` and

```csharp
        /// <summary>Deals a board: <paramref name="seed"/> 0 = the settings' seed (random if 0 there); <paramref name="design"/> null = the settings' level.</summary>
        public void Prepare(int ammo, uint seed, LevelDesign design)
        {
            ClearViews();
            Seed = seed != 0 ? seed : S.Seed != 0 ? S.Seed : (uint)UnityEngine.Random.Range(1, int.MaxValue);
            Board = ColorHuntRules.Create(design ?? S.ToDesign(), ammo, Seed);
            BuildViews(Board);
            BoardRoot.localScale = Vector3.one * PotaLook.GridScale(Board.Size);
            Fx?.SetBoard(BoardRoot);
            Fx?.Clear();
            DisplayedAmmo = Board.Ammo;
            Current = State.Idle;
            _staged = true;
            Won = false;
            Rotations = 0; LongestChain = 0;
            _checkNeeded = true;
            _resultTimer = -1f;
            BoardRoot.localRotation = Quaternion.identity;
            Raise(RoundStarted);
            Raise(AmmoChanged);
            ResetShownTargets();
        }
```

`Assets/_Project/Games/Pota/Editor/ColorHuntRecordTool.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Records Color Hunt for the side-by-side with Pota: a scripted round at 30 fps (Time.captureDeltaTime, paused editor stepping), PNG
    /// frames with the HUD and events.json with the first tap / rotation / winning tap in frame time.
    /// </summary>
    [McpForUnityTool("ai_colorhunt_record", Description =
        "Record Color Hunt in Play Mode (ColorHunt.unity open) as PNG frames with UI at 30 fps. action='start' {script (';'-separated: " +
        "'wait S', 'select C|auto', 'tap C|auto', 'rotate cw|ccw', 'settle'; default: idle, select, tap, settle, rotate, settle, tap, settle), " +
        "seed (7), ammo (settings), win (false; true: one red target block, the first red tap wins), width (540), aspect (0.5625), tail (1.5 s), " +
        "name ('colorhunt')} | 'status' | 'cancel'. Frames: Temp/record/<name>/f_NNNN_tS.SSS.png + events.json {tap, rotate, win}.")]
    public static class ColorHuntRecordTool
    {
        public struct Step { public string Verb; public string Arg; public float Seconds; }

        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("Script, ';'-separated", Required = false)] public string script { get; set; }
            [ToolParameter("Board seed (default 7)", Required = false)] public int? seed { get; set; }
            [ToolParameter("Ammo (default: settings)", Required = false)] public int? ammo { get; set; }
            [ToolParameter("Win scenario (default false)", Required = false)] public bool? win { get; set; }
            [ToolParameter("Frame width px (default 540)", Required = false)] public int? width { get; set; }
            [ToolParameter("Frame aspect w/h (default 0.5625)", Required = false)] public float? aspect { get; set; }
            [ToolParameter("Seconds after the script (default 1.5)", Required = false)] public float? tail { get; set; }
            [ToolParameter("Folder under Temp/record (default 'colorhunt')", Required = false)] public string name { get; set; }
        }

        public const string DefaultScript = "wait 0.6; select auto; wait 0.3; tap auto; settle; wait 0.4; rotate cw; settle; wait 0.3; tap auto; settle";
        public const string WinScript = "wait 0.6; tap auto; wait 2.5";

        private static readonly HashSet<string> Verbs = new HashSet<string> { "wait", "select", "tap", "rotate", "settle" };
        private static List<Step> _steps;
        private static int _index, _stepFrames, _captured, _tailFrames, _width, _seed, _ammo;
        private static float _aspect, _tail;
        private static bool _running, _win, _dealt;
        private static string _dir = "Temp/record/colorhunt", _result = "";
        private static readonly Dictionary<string, float> _events = new Dictionary<string, float>();
        private static FrameRecorder _recorder;
        private static EditorWindow _floating;

        public static List<Step> Parse(string script)
        {
            var steps = new List<Step>();
            foreach (var raw in (script ?? "").Split(';'))
            {
                var parts = raw.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                string verb = parts[0].ToLowerInvariant(), arg = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
                if (!Verbs.Contains(verb)) throw new ArgumentException($"unknown step '{raw.Trim()}'");
                float seconds = verb == "wait" ? float.Parse(arg, CultureInfo.InvariantCulture) : 0f;
                steps.Add(new Step { Verb = verb, Arg = arg, Seconds = seconds });
            }
            return steps;
        }

        public static object HandleCommand(JObject p)
        {
            string action = (AIToolsCommon.GetString(p, "action", "start") ?? "start").ToLowerInvariant();
            if (action == "status") return new SuccessResponse(_running ? "recording" : "idle", Status());
            if (action == "cancel") { Stop("cancelled"); return new SuccessResponse("cancelled", Status()); }
            if (!EditorApplication.isPlaying) return new ErrorResponse("Enter Play Mode first with ColorHunt.unity open.");
            if (_running) return new ErrorResponse("Already recording; poll status or cancel.");
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            if (game == null) return new ErrorResponse("No ColorHuntGame in the running scene.");
            _win = p["win"] != null && p["win"].Type == JTokenType.Boolean && p["win"].Value<bool>();
            try { _steps = Parse(AIToolsCommon.GetString(p, "script", _win ? WinScript : DefaultScript)); }
            catch (Exception e) { return new ErrorResponse(e.Message); }
            _seed = AIToolsCommon.GetInt(p, "seed", 7);
            _ammo = AIToolsCommon.GetInt(p, "ammo", game.S.Ammo);
            _width = Mathf.Clamp(AIToolsCommon.GetInt(p, "width", 540), 120, 2160);
            _aspect = Mathf.Clamp(AIToolsCommon.GetFloat(p, "aspect", 0.5625f), 0.3f, 3f);
            _tail = Mathf.Max(0f, AIToolsCommon.GetFloat(p, "tail", 1.5f));
            _dir = "Temp/record/" + (AIToolsCommon.GetString(p, "name", "colorhunt") ?? "colorhunt");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _index = 0; _stepFrames = 0; _captured = 0; _tailFrames = 0; _dealt = false; _result = "";
            _events.Clear();
            Time.captureDeltaTime = 1f / 30f;
            _floating = FrameRecorder.OpenFloatingGameView(new Rect(1040f, 70f, 380f, 700f));
            EditorApplication.isPaused = true;
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return new SuccessResponse($"Recording Color Hunt to {_dir}.", Status());
        }

        private static void Stop(string why)
        {
            _running = false;
            _result = why;
            EditorApplication.update -= Tick;
            _recorder?.Dispose();
            _recorder = null;
            Time.captureDeltaTime = 0f;
            EditorApplication.isPaused = false;
            FrameRecorder.CloseFloatingGameView(_floating);
            _floating = null;
            if (Directory.Exists(_dir))
            {
                var json = new JObject();
                foreach (var kv in _events) json[kv.Key] = kv.Value;
                File.WriteAllText(Path.Combine(_dir, "events.json"), json.ToString());
            }
        }

        private static float Now => _captured / 30f;

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            if (game == null) { Stop("no ColorHuntGame"); return; }
            EditorApplication.Step();
            if (!_dealt)
            {
                if (game.BoardRoot == null) return;
                var design = game.S.ToDesign();
                if (_win) design.Targets = new[] { (0, 1) };
                game.Prepare(_ammo, (uint)_seed, design);
                game.Begin();
                _recorder = new FrameRecorder(game.Cam, _width, _aspect, true);
                _dealt = true;
                return;
            }
            Capture();
            if (_index >= _steps.Count)
            {
                if (++_tailFrames >= Mathf.RoundToInt(_tail * 30f)) Stop($"done: {_captured} frames");
                return;
            }
            var s = _steps[_index];
            bool next = true;
            switch (s.Verb)
            {
                case "wait":
                    next = ++_stepFrames >= Mathf.RoundToInt(s.Seconds * 30f);
                    break;
                case "select":
                    game.Select(Cell(game, s.Arg));
                    break;
                case "tap":
                    int cell = Cell(game, s.Arg);
                    game.Select(-1);
                    string key = _win ? "win" : "tap";
                    if (!_events.ContainsKey(key)) _events[key] = Now;
                    if (game.Tap(cell) != "broken") { Stop($"tap {cell} was not accepted"); return; }
                    break;
                case "rotate":
                    if (!_events.ContainsKey("rotate")) _events["rotate"] = Now;
                    game.Rotate(s.Arg == "ccw" ? Rotation.Counter : Rotation.Clockwise);
                    break;
                case "settle":
                    next = !game.Busy && game.ItemsInFlight == 0;
                    break;
            }
            if (next) { _index++; _stepFrames = 0; }
        }

        /// <summary>A number, or "auto": the affordable block with the longest chain (win: the first affordable red block).</summary>
        private static int Cell(ColorHuntGame game, string arg)
        {
            if (int.TryParse(arg, out int c)) return c;
            var b = game.Board;
            int best = -1, bestSteps = -1, bestHp = int.MaxValue;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var block = b.Cells[i];
                if (block.Hp > b.Ammo) continue;
                if (_win) { if (block.Color == 0) return i; continue; }
                int steps = ColorHuntRules.Break(b, i).Steps.Count;                     // pure: Break clones the board
                if (steps > bestSteps || (steps == bestSteps && block.Hp < bestHp)) { best = i; bestSteps = steps; bestHp = block.Hp; }
            }
            return best;
        }

        private static void Capture()
        {
            _recorder.Capture(Path.Combine(_dir, $"f_{_captured:0000}_t{Now:0.000}.png"));
            _captured++;
        }

        private static object Status() => new { running = _running, step = _index, steps = _steps?.Count ?? 0, frames = _captured, dir = _dir, result = _result, events = _events };
    }
}
```

- [ ] **Step 4: Pota side**

`Tools/potalook/record-pota.mjs`:

```js
// Records Pota's puzzle from the SCRATCH COPY (served by its vite) at 9:16 for the side-by-side with Unity. Never runs in ~/projects/Pota.
// Usage: node record-pota.mjs <url> <out dir> <play|win>  →  <out dir>/*.webm + <out dir>/events.json {tap, rotate, win} (s from video start)
import { chromium } from "/Users/tunaaltin17/projects/copy/node_modules/playwright/index.mjs";
import { writeFileSync } from "node:fs";
import { join } from "node:path";

const [url = "http://localhost:5199/", out, mode = "play"] = process.argv.slice(2);
if (!out) throw new Error("usage: node record-pota.mjs <url> <out dir> <play|win>");
const browser = await chromium.launch({ headless: false, args: ["--use-angle=metal", "--ignore-gpu-blocklist"] });
const ctx = await browser.newContext({
  viewport: { width: 390, height: 693 }, deviceScaleFactor: 2, hasTouch: true, locale: "tr-TR",
  recordVideo: { dir: out, size: { width: 780, height: 1386 } },
});
const t0 = Date.now();
const now = () => (Date.now() - t0) / 1000;
const page = await ctx.newPage();
const errors = [];
page.on("pageerror", (e) => errors.push(String(e)));
await page.goto(`${url}?test=renkavi`);
await page.waitForFunction(() => window.__pota?.faz2?.durum, null, { timeout: 20000 });

// A 7×7 level with no obstacle or special on the board: Pota's rules grew past Unity's, so moments are compared, not layouts.
let clean = false;
for (let tohum = 1; tohum <= 40 && !clean; tohum++) {
  await page.evaluate((t) => __pota.faz2.kur(9, 158, t, false, []), tohum);
  await page.waitForTimeout(400);
  clean = await page.evaluate(() => { const d = __pota.faz2.durum(); return d.boyut === 7 && d.engel === 0 && d.ozel === 0; });
}
if (!clean) throw new Error("no clean 7×7 board in 40 seeds");
// Win scenario: every target needs one more block (the copy's runtime state; the source stays untouched).
if (mode === "win") await page.evaluate(() => { for (const h of __pota.faz2.faz.alan.hedefler) h.kalan = 1; });

const settle = () => page.waitForFunction(() => { const d = __pota.faz2.durum(); return !d.kilit && d.hudBekleyen === 0; }, null, { timeout: 15000 });
// Longest-chain affordable block like ai_colorhunt_record "auto"; win: a block of the first target's colour.
const pick = () => page.evaluate(async (win) => {
  const R = await import("/src/renkAvi.ts");
  const alan = __pota.faz2.alan(), tahta = __pota.faz2.tahta().filter((b) => b.yeter);
  if (win) { const renk = alan.hedefler[0].renk; return (tahta.find((b) => b.renk === renk) ?? tahta[0]).hucre; }
  let best = -1, bestSteps = -1, bestHp = 1e9;
  for (const b of tahta) {
    const k = R.kir(alan, b.hucre);
    const steps = k?.adimlar?.length ?? 0;
    if (steps > bestSteps || (steps === bestSteps && b.can < bestHp)) { best = b.hucre; bestSteps = steps; bestHp = b.can; }
  }
  return best;
}, mode === "win");

const events = {};
await page.waitForTimeout(900);
if (mode === "win") {
  const c = await pick();
  events.win = now();
  await page.evaluate((h) => __pota.faz2.kir(h), c);
  await page.waitForTimeout(2800);
} else {
  let c = await pick();
  events.tap = now();
  await page.evaluate((h) => __pota.faz2.kir(h), c);
  await settle();
  await page.waitForTimeout(400);
  events.rotate = now();
  await page.evaluate(() => __pota.faz2.dondur("saat"));
  await settle();
  await page.waitForTimeout(300);
  c = await pick();
  await page.evaluate((h) => __pota.faz2.kir(h), c);
  await settle();
  await page.waitForTimeout(1500);
}
await ctx.close();
await browser.close();
writeFileSync(join(out, "events.json"), JSON.stringify(events));
console.log(JSON.stringify({ events, errors }));
```

(`renkAvi.ts` exports `kir(alan, hucre) → { sonuc, alan, adimlar, maliyet }` and `Hedef { tur, renk, kalan }` — checked in the copy, c742a6a.)

`Tools/potalook/compare.py`:

```python
#!/usr/bin/env python3
"""Pota | Unity at matched moments (spec 2026-09-30 §2.1).

Usage: uv run --with pillow Tools/potalook/compare.py OUT_PREFIX POTA_VIDEO POTA_EVENTS UNITY_FRAMES UNITY_EVENTS
For each anchor in both events files ("tap", "rotate", "win") writes OUT_PREFIX-<anchor>.jpg: two rows (Pota, Unity), one column per
moment (seconds after the anchor; the sheet labels them +1 s). Pota frames are extracted from the video at 30 fps with ffmpeg.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import frame_sheet  # noqa: E402  (Tools/frame_sheet.py)

FFMPEG = "/opt/homebrew/bin/ffmpeg"
# idle, salvo mid, tapped block's swell peak (0.5 + 0.11·0.55), burst +0.08 / +0.16, a landing, the sticker home (peak + 0.55)
MOMENTS = {
    "tap": [-0.3, 0.25, 0.5605, 0.64, 0.72, 0.9, 1.11],
    "rotate": [0.0, 0.21, 0.33, 0.5],
    "win": [0.56, 0.9, 1.5],
}
WINDOW = 1.0   # frames from 1 s before the anchor; names carry t − anchor + 1 so frame_sheet's regex sees no minus sign


def shifted_name(index, t, anchor):
    s = t - anchor + WINDOW
    return None if s < 0 else f"f_{index:05d}_t{s:.3f}.png"


def extract(video, folder, fps=30):
    raw = os.path.join(folder, "raw")
    os.makedirs(raw)
    subprocess.run([FFMPEG, "-loglevel", "error", "-i", video, "-vf", f"fps={fps}", os.path.join(raw, "r_%05d.png")], check=True)
    frames = []
    for i, n in enumerate(sorted(os.listdir(raw))):
        frames.append((os.path.join(raw, n), i / fps))
    return frames


def aligned(frames, anchor, folder):
    os.makedirs(folder)
    for i, (path, t) in enumerate(frames):
        name = shifted_name(i, t, anchor)
        if name:
            os.symlink(os.path.abspath(path), os.path.join(folder, name))
    return folder


def main(argv):
    if len(argv) != 5:
        sys.exit(__doc__)
    prefix, video, pota_events, unity_dir, unity_events = argv
    pe, ue = json.load(open(pota_events)), json.load(open(unity_events))
    work = tempfile.mkdtemp(prefix="potalook-")
    try:
        pota = extract(video, work)
        unity = frame_sheet.load_frames(unity_dir)
        for anchor, times in MOMENTS.items():
            if anchor not in pe or anchor not in ue:
                continue
            a = aligned(pota, pe[anchor], os.path.join(work, f"pota-{anchor}"))
            b = aligned(unity, ue[anchor], os.path.join(work, f"unity-{anchor}"))
            frame_sheet.main([f"{prefix}-{anchor}.jpg", f"Pota={a}", f"Unity={b}",
                              "--times", ",".join(f"{t + WINDOW:.4f}" for t in times), "--height", "640"])
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main(sys.argv[1:])
```

- [ ] **Step 5: Run the tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntRecordToolTests` → Expected: 2/2 PASS.
Run: `~/.local/bin/uv run --with pytest --with pillow pytest Tools/potalook/test_compare.py -q` → Expected: 3 passed.

- [ ] **Step 6: Record both**

Pota (the copy's vite on :5199 as in Task 1, started from `$S/pota-copy`):
`$NODE Tools/potalook/record-pota.mjs http://localhost:5199/ $S/potalook-pota-play play` and `… $S/potalook-pota-win win`.
Expected: one `.webm` and `events.json` each, `errors: []`. `git -C ~/projects/Pota status --porcelain` unchanged.
Unity: play `ColorHunt.unity`; `ai_colorhunt_record {"action":"start","name":"potalook-play"}`, poll `status` until `done`; then
`{"action":"start","name":"potalook-win","win":true}`; stop Play Mode.
Expected: `Temp/record/potalook-play/events.json` with `tap` and `rotate`, `Temp/record/potalook-win/events.json` with `win`.

- [ ] **Step 7: Sheets and the side-by-side video**

```bash
uv() { ~/.local/bin/uv "$@"; }
uv run --with pillow Tools/potalook/compare.py "$S/potalook" "$S/potalook-pota-play/"*.webm "$S/potalook-pota-play/events.json" Temp/record/potalook-play Temp/record/potalook-play/events.json
uv run --with pillow Tools/potalook/compare.py "$S/potalook" "$S/potalook-pota-win/"*.webm "$S/potalook-pota-win/events.json" Temp/record/potalook-win Temp/record/potalook-win/events.json
for k in tap rotate win; do Tools/keep_screenshot.sh "$S/potalook-$k.jpg" "2026-09-30-potalook-$k"; done
```

Side video (both 540×960, starting 1 s before each side's first tap, 12 s):

```bash
T=$(python3 -c "import json;print(json.load(open('$S/potalook-pota-play/events.json'))['tap']-1)")
U=$(python3 -c "import json;print(json.load(open('Temp/record/potalook-play/events.json'))['tap']-1)")
/opt/homebrew/bin/ffmpeg -y -loglevel error -ss "$T" -t 12 -i "$S/potalook-pota-play/"*.webm \
  -framerate 30 -pattern_type glob -i 'Temp/record/potalook-play/f_*.png' \
  -filter_complex "[0:v]fps=30,scale=540:960[a];[1:v]trim=start=$U:duration=12,setpts=PTS-STARTPTS,scale=540:960[b];[a][b]hstack=inputs=2" \
  -c:v libx264 -pix_fmt yuv420p -crf 26 docs/screenshots/2026-09-30-potalook-side.mp4
```

Expected: three JPGs (≤ ~200 KB each) and an MP4 under ~6 MB.

- [ ] **Step 8: Tune by eye (at most three rounds)**

Read the three sheets and watch the video. For each visible difference, first check it is not a layout difference (Pota's board has
other blocks) and not an engine difference the spec accepts (BRDF, bloom kernel, shadow filter). Then adjust only the values marked
"tuned side by side": `LightIntensityScale`, bloom `intensity`/`scatter` and vignette (`PotaLookImportTool.Profile`), `PotaLook.RoomDiffuse`,
`PuzzleStage.ReflectionIntensity`, the ring start ratio. Every change: new default in code with a comment "tuned 2026-09-30: …", a
ledger line `Task 11: Ruling: <value> <old → new> — <what the sheet showed> — <cost if wrong>`, re-import/rebuild, re-record, new sheets.
Stop after three rounds and list what still differs.

- [ ] **Step 9: Commit**

```bash
git add Tools/potalook Assets/_Project/Games/Pota docs/screenshots/2026-09-30-potalook-*
git commit -m "feat: ai_colorhunt_record and the Pota | Unity moment sheets and side-by-side video for the puzzle look

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Docs, backlog, memory

**Files:**
- Modify: `Assets/_Project/Games/Pota/README.md` (Color Hunt: Pota look section), `CLAUDE.md` §12, `~/projects/Blender MCP/docs/backlog.md`
- Create/modify: memory `colorhunt-pota-look-2026-09-30.md` + `MEMORY.md` line

- [ ] **Step 1: README**

Add a "Renk Avı — Pota'nın bulmaca görünümü (2026-09-30)" section to `Games/Pota/README.md`: what came from Pota (atlas, sprites, ball
skin, stickers, backboard, sounds — exported from a scratch copy by `Tools/potalook/run_export.sh`, imported by `ai_potalook_import`),
where the numbers live (`PotaLook` constants with sources, `ColorHuntSettings` feel values), the room reflection bake, the camera fit
(CSS px of a 390-px phone), the HUD, the animation order (swell → peak → burst), how to record and compare (`ai_colorhunt_record`,
`record-pota.mjs`, `compare.py`), and the known differences left after tuning (from Task 11 Step 8).

- [ ] **Step 2: CLAUDE.md §12**

- Date line: `_Son güncelleme: 2026-09-30 (Renk Avı Pota görünümü birebir; …)_`.
- AITools list: `ai_potalook_import`, `ai_colorhunt_record` (Pota-specific → mention under Pota's tools pointer).
- Lessons: "URP post is per camera and off by default — no Pota scene had it on until 2026-09-30 (`SceneParts.EnablePost`)";
  "realtime reflection probes are off in both quality levels: bake (`Lightmapping.BakeReflectionProbe`) and use a Custom probe";
  "particle vertex colours are 8-bit: HDR effect colours go through a material intensity (`applyActiveColorSpace = false`)";
  "three.js light intensities are ÷π in URP".
- Test counts from the last full runs; status: "Aktif: Renk Avı Pota görünümü birebir bitti; sıradaki: 'üstüne inşa' adayları (ayrı spec)".

- [ ] **Step 3: Backlog**

Append to `~/projects/Blender MCP/docs/backlog.md` (next U numbers): mobile texture compression for the label atlas and the room EXR;
three's point light decay 1.6 vs URP 1/d² (one scale factor); the ring's start ratio at depth > 0; confetti belongs to the dunk in
Pota (dunk is a later step); Pota's live room reverb is baked into the clips (no per-play variation); FrameRecorder puts post-processing
on the HUD in recordings; Pota HUD `.hedefKutu b` uses the body font (Fredoka) — Unity uses Lilita One; specials/obstacles/boosters
(scope B).

- [ ] **Step 4: Memory**

Write `colorhunt-pota-look-2026-09-30.md` (project): what shipped, the export/bake/record commands, the deviations (1–5 above) and
where the tuning numbers ended; update the `MEMORY.md` pointer line (replace the PotaFlow "next = port Pota's puzzle look" hook).

- [ ] **Step 5: Full suites and commit**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` and `~/.local/bin/uv run Tools/unity_tests.py PlayMode`
Expected: all PASS; write the counts into CLAUDE.md §12.

```bash
git add Assets/_Project/Games/Pota/README.md CLAUDE.md
git commit -m "docs: Color Hunt Pota look — README, CLAUDE.md status and lessons

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(The backlog lives in the Blender MCP repo: commit it there with `docs: backlog — Unity items from the Color Hunt Pota look`.)
