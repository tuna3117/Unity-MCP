# Pota In-Game Feel Layer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the thirteen feel moments F1–F13 of the spec (level intro, ammo tension, leftover finale, chain words, idle hint, landing bounce, board clip, sky life, lose moment, slingshot, miss, swish, count stamp) to Unity's Pota (Color Hunt, PotaFlow, Basket), switchable by `Feel.Enabled`, recorded side by side against Pota.

**Architecture:** Every moment is a small component subscribed to `ColorHuntGame` / `BasketRound` events, with its numbers in a `FeelTuning` block on the existing settings assets and its pure curves in a static `FeelCurves` class (EditMode-tested). The studio polish that already exists (intro cascade, idle hint, ammo pulse, chain words) is opened to the Pota look through `ColorHuntGame.Polish` (= `Studio || Feel.Enabled`). Blender supplies four assets (miss lettering, clouds, bird flock, city band) through the workshop's recipe/export contract and `ai_import_asset`. Recording tools get `feel` and scenario parameters; `compare.py` / `side_video.py` make the deliverables.

**Tech Stack:** Unity 6000.6.2f1, URP 17.6 (Render Graph, RenderObjects renderer feature), uGUI (legacy Text, Lilita One), Unity Test Framework (EditMode/PlayMode via `Tools/unity_tests.py`), Blender 5.2 workshop (`~/projects/Blender MCP`, `tools/run.sh`), MCP bridge tools (`refresh_unity`, `read_console`, `execute_custom_tool`).

**Spec:** `docs/superpowers/specs/2026-10-03-pota-ingame-feel-design.md`

## Global Constraints

- Feel off (`ColorHuntSettings.Feel.Enabled = false`, `BasketSettings.Feel.Enabled = false`) must reproduce today's behaviour exactly; all existing tests pass unchanged.
- Rules and numbers do not change; the leftover coins use Pota's formula `min(20, floor(remaining × 0.1))`.
- Pota (`~/projects/Pota`) is read-only; Pota recordings come from a scratch rsync copy only. No push, no paid calls, never bring Unity to the front.
- Unity 6 APIs: `FindAnyObjectByType`, `linearVelocity`; no `UnityEngine.Input`; new shaders are HLSL URP, SRP-batcher compatible; runtime materials come from templates (`RuntimeMaterial.From`, `PotaMaterialTemplates`) so player builds keep their shaders.
- All scene edits through editor tools/scripts (never YAML). `[SerializeField]` for every tool-assigned reference. `Undo` + save in editor tools.
- Script changes: `refresh_unity {"mode":"force","scope":"all","compile":"request","wait_for_ready":true}` then `read_console {"types":["error"],"filter_text":"error CS"}`. Never compile while in Play Mode (`manage_editor stop` first).
- Tests: `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Fixture ...]` from the project root. Blender: `tools/run.sh test` from `~/projects/Blender MCP`.
- Timing: real seconds via `Time.unscaledDeltaTime` for HUD/camera moments; board moments on game time (`ColorHuntGame` jobs) so slow-mo affects them as today.
- Commit after each task (English message, `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`).

## Review Focus

1. **Feel toggled while a round runs** (settings asset edited in Play): components must not leave half-states (red counter stuck, dimmed board, lean on the hero). Test: toggle `Feel.Enabled` off during F2/F9/F10 → state restored within one frame (Task 2, 5, 9).
2. **Win with 0 remaining ammo** (F3): finale must take zero flights, show no badge, and still end the round with `Disperse` + `RoundEnded`. Test in Task 4.
3. **Lose by "insufficient" with ammo > 0** (F9): remaining labels count targets not ammo; "Kıl payı!" only when total remaining ≤ 3. Test in Task 5.
4. **Shot released during the intro / tap during the finale**: `ColorHuntGame.Tap` returns a non-"broken" result and the recorder waits; `ShotInput` ignores a press that started before `Aim`. Tests in Task 3 and Task 9.
5. **Board rotation with the stencil mask** (F7): the mask follows the frame; after a 90° turn blocks inside the frame are still drawn (mask quad is square and centred). PlayMode test in Task 6.

---

### Task 1: Settings blocks, FeelCurves, polish gate (F4, F5, F2 pulse)

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Feel/FeelTuning.cs`
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Feel/FeelCurves.cs`
- Create: `Assets/_Project/Games/Pota/Scripts/Basket/Feel/BasketFeelTuning.cs`
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs` (add `public FeelTuning Feel = new FeelTuning();` after `Royal`)
- Modify: `Assets/_Project/Games/Pota/Scripts/Basket/BasketSettings.cs` (add `public BasketFeelTuning Feel = new BasketFeelTuning();` under a "Feel" header)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs:507` (add `public bool Polish => Studio || (S != null && S.Feel != null && S.Feel.Enabled);`; `StartIntro`, `TickHint` use `Polish`; the press path at :229 stays `Studio`)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Studio/AmmoCounter.cs` (pulse + roll under `game.Polish`; the cost chip/ghost stays `game.Studio`)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Bursts/PotaBurstStyle.cs` (attach `CandyHud` when `game.Polish`; on `ChainPeak(depth ≥ S.Feel.WordFromDepth)` call `Word`; 5+ → `TimeCtl.SlowMo(0.5f, 0.25f)` + confetti, as CandyBurst does)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntHint.cs` (`Best(Board b)` → score = steps + target items collected; keep the old signature)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/FeelCurvesTests.cs`, `ColorHuntHintTests.cs` (extend), `BurstStyleTests.cs` (extend)

**Interfaces:**
- Produces: `FeelTuning { bool Enabled = true; int WordFromDepth = 3; float IntroRibbonTime, IntroGoalFlyAt, IntroBoardAt, IntroSkipTime; float TensionEdgeAlpha = 0.18f; int FinaleMaxFlights = 14, FinaleMinFlights = 6; float FinaleGap = 0.08f, FinaleMax = 2f; float LandBounceTime = 0.18f; float LoseDim = 0.6f, LoseDimTime = 0.4f, LoseCamBack = 0.3f; int NearMissMax = 3; }`
- Produces: `BasketFeelTuning { bool Enabled = true; float LeanDeg = 12f, CrouchM = 0.06f, CamBackM = 0.4f, CamDownDeg = 2f, HoopGlowTime = 0.2f; float MissTime = 1f, MissCamSag = 0.2f, RimBlinkTime = 0.3f, SlumpDeg = 8f; float StampPeak = 2.2f, StampRest = 1.8f, CageGlowTime = 0.3f; }`
- Produces: `static class FeelCurves` with `int LeftoverCoins(int remaining)`, `int[] FinalePlan(int remaining, int minFlights, int maxFlights)` (per-flight ball counts, sum = remaining, length ∈ [min(remaining,min)…max], 0 flights for 0), `Vector2 LandBounce(float u)` (x,y scale: u=0 → (1.08, 0.86); u≈0.6 → y 1.04; u=1 → (1,1)), `float Lean(float pull)` (0..1 → 0..1 eased in-out), `float Stamp(float u)` (1 → peak → rest, overshoot curve), `float RibbonScale(float u)` (0 → 1.15 → 1), `float Breath(float t, float hz)` (0..1 sine), `float HintScore(int steps, int targetItems)` = steps + targetItems.
- Produces: `ColorHuntGame.Polish` consumed by every later task.

- [ ] **Step 1: Write failing EditMode tests**

```csharp
// FeelCurvesTests.cs
[Test] public void LeftoverCoins_IsPotaFormula() {
    Assert.AreEqual(0, FeelCurves.LeftoverCoins(9));
    Assert.AreEqual(1, FeelCurves.LeftoverCoins(10));
    Assert.AreEqual(14, FeelCurves.LeftoverCoins(140));
    Assert.AreEqual(20, FeelCurves.LeftoverCoins(400));
}
[TestCase(0, 0)] [TestCase(1, 1)] [TestCase(6, 6)] [TestCase(7, 7)] [TestCase(14, 14)] [TestCase(15, 14)] [TestCase(140, 14)]
public void FinalePlan_FlightCount(int remaining, int flights) => Assert.AreEqual(flights, FeelCurves.FinalePlan(remaining, 6, 14).Length);
[TestCase(1)] [TestCase(13)] [TestCase(140)] [TestCase(157)]
public void FinalePlan_SumsToRemaining(int n) { int s = 0; foreach (var b in FeelCurves.FinalePlan(n, 6, 14)) { Assert.Greater(b, 0); s += b; } Assert.AreEqual(n, s); }
[Test] public void LandBounce_SquashesThenOvershootsThenRests() {
    var a = FeelCurves.LandBounce(0f); Assert.Less(a.y, 0.9f); Assert.Greater(a.x, 1.05f);
    Assert.Greater(FeelCurves.LandBounce(0.6f).y, 1.02f);
    Assert.AreEqual(1f, FeelCurves.LandBounce(1f).y, 1e-4f); Assert.AreEqual(1f, FeelCurves.LandBounce(1f).x, 1e-4f);
}
[Test] public void Lean_IsMonotonicAndBounded() { float last = -1f; for (int i = 0; i <= 10; i++) { float v = FeelCurves.Lean(i / 10f); Assert.GreaterOrEqual(v, last); last = v; } Assert.AreEqual(1f, last, 1e-4f); }
[Test] public void Stamp_PeaksThenRests() { Assert.AreEqual(1f, FeelCurves.Stamp(0f), 1e-4f); Assert.Greater(FeelCurves.Stamp(0.35f), 2.0f); Assert.AreEqual(1.8f, FeelCurves.Stamp(1f), 1e-3f); }
[Test] public void RibbonScale_OvershootsOnce() { Assert.AreEqual(0f, FeelCurves.RibbonScale(0f), 1e-4f); Assert.Greater(FeelCurves.RibbonScale(0.6f), 1.05f); Assert.AreEqual(1f, FeelCurves.RibbonScale(1f), 1e-4f); }
// ColorHuntHintTests.cs (extend)
[Test] public void Best_PrefersTargetColourChainOnEqualSteps() { /* build two boards via Board fixture helpers already in the file: same chain length, one of target colour; assert the target one wins */ }
// BurstStyleTests.cs (extend)
[Test] public void PotaStyle_WithFeel_ShowsWordsFromDepth3() { /* instantiate game with S.Feel.Enabled = true, Look = Pota; call Style.ChainPeak(3,...); Assert.AreEqual("Süper!", hud.LastWord) ; depth 2 → null */ }
```

- [ ] **Step 2: Run tests, expect compile failures** (`FeelCurves` missing): `~/.local/bin/uv run Tools/unity_tests.py EditMode Pota.Tests.EditMode.FeelCurvesTests`
- [ ] **Step 3: Implement** `FeelTuning`, `BasketFeelTuning`, `FeelCurves` (pure math; `FinalePlan`: `flights = remaining <= min ? remaining : Mathf.Min(max, Mathf.Max(min, remaining))`, then distribute `remaining / flights` with the remainder on the first flights), settings fields, `Polish`, the three gate edits, hint scoring (`ColorHuntRules.Break(b,i)` result exposes `Steps` → count cells whose colour is a target with remaining > 0; add `TargetItems(Board, BreakResult)` helper in `ColorHuntHint`).
- [ ] **Step 4: Compile + run** `FeelCurvesTests ColorHuntHintTests BurstStyleTests StudioCurvesTests ColorHuntRulesTests`; then the whole EditMode suite.
- [ ] **Step 5: Commit** `feat(feel): settings blocks, FeelCurves, polish gate opens intro/hint/pulse/words to the Pota look`

---

### Task 2: F2 AmmoTension edge breath + single-move glow

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Feel/AmmoTension.cs`
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntHud.cs:82` `Bind` (create `AmmoTension` when `Game.Polish`, like `AmmoCounter`)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs` `TickHint` (if `AffordableCount == 1` and tension active → `HintDelay` = 0)
- Test: `Assets/_Project/Games/Pota/Tests/PlayMode/FeelPlayTests.cs` (new, fixture pattern from `StudioPolishPlayTests.cs`)

**Interfaces:**
- Produces: `AmmoTension : MonoBehaviour { void Init(ColorHuntHud hud); bool Active; float EdgeAlpha; }` — `Active` = `game.AmmoCounter.Pulsing`-equivalent rule (`ammo < 2 × cheapest`), `EdgeAlpha = Feel.TensionEdgeAlpha × (0.5 + 0.5·Breath(t,1))`. Edge image: full-screen `Image` with `CandyHud`'s edge sprite (expose `CandyHud.EdgeSprite()` static) tinted `PotaLook.NumberShort` red, `raycastTarget = false`, under the HUD panels.
- Produces: `ColorHuntGame.AffordableCount` (int, cells with `Hp <= Ammo`).

- [ ] **Step 1: Failing PlayMode test** `Tension_PulsesAtNineAmmo_StopsAtZero`: build scene via `ColorHuntStaging` helpers, `Prepare(9, seed 7)`, `Begin`; assert `hud.Tension.Active && hud.Tension.EdgeAlpha > 0`; set ammo to 0 via `game.ShowAmmo`/`End` path → `Active == false`. Also `Tension_Off_WhenFeelDisabled`.
- [ ] **Step 2: Run, expect fail.** `~/.local/bin/uv run Tools/unity_tests.py PlayMode Pota.Tests.PlayMode.FeelPlayTests`
- [ ] **Step 3: Implement** component (Update on unscaled time; clears alpha when inactive or round ended; destroys edge on disable).
- [ ] **Step 4: Run PlayMode FeelPlayTests + StudioPolishPlayTests.**
- [ ] **Step 5: Commit** `feat(feel): F2 ammo tension — red edge breath and instant hint on the last affordable block`

---

### Task 3: F1 LevelIntro and F6 landing bounce

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Feel/LevelIntro.cs`
- Modify: `ColorHuntGame.cs` `StartIntro`/`AdvanceIntro` (when `Feel.Enabled`: the cascade starts at `Feel.IntroBoardAt`; `Intro` stays true until `LevelIntro.Done`; `SkipIntro()` public), `NewRound` (ribbon shows `Level` text — add `public int LevelNumber = 20` to `ColorHuntSettings`), landing: where `DropState.Landed` sets `v.Squash` add `v.Bounce = 0f` when `Feel.Enabled`
- Modify: `BlockView.cs` (`public float Bounce = -1f;` in `Tick`: if `Bounce >= 0` scale by `FeelCurves.LandBounce(Bounce / Feel.LandBounceTime)` on x/y (z = x), then −1 when done; `ResetStudio` resets)
- Modify: `ColorHuntHud.cs` (expose `TargetRow` icon rect + `PopRow(color)`; `Bind` creates `LevelIntro` when `Game.Polish && Game.S.Feel.Enabled`)
- Modify: `ColorHuntInput.cs` (a press while `game.Intro` → `game.SkipIntro()`)
- Modify: `Flow/PotaFlowDirector.cs` (`ApplyTransition`: at `tl.StampAt` call `Hud.Intro.ShowRibbon()`, at HUD-targets-enter time `Hud.Intro.FlyGoals()`; PotaFlow never runs the standalone cascade)
- Test: `FeelPlayTests.cs` (+`Intro_TapsWait_ThenBoardReady_Under1_8s`, `Intro_Skip_FinishesWithin0_3s`), `BlockViewTests.cs` (+bounce resets)

**Interfaces:**
- Produces: `LevelIntro : MonoBehaviour { void Init(ColorHuntHud hud); void Play(); void ShowRibbon(); void FlyGoals(); void Skip(); bool Done; float Elapsed; }`. Ribbon: uGUI panel (`ColorHuntHud.PanelSprite`, navy/gold), text `"BÖLÜM {n}"` 36 css px; goal chips below (icon 56 css + count). Goal flight reuses `StudioCurves.FlightPoint` with `Side(i)`; arrival calls `hud.PopRow(color)` and `Collect.StampTick`-style ring (reuse `CollectFlight` ring sprite via a public `CollectFlight.Ring(Vector2 canvasPos)`).
- Timeline (`FeelTuning`): `IntroRibbonTime 0.0`, `IntroGoalFlyAt 0.70`, `IntroBoardAt 0.95`, ready ≤ 1.60; `Skip` compresses the remainder into `IntroSkipTime 0.3`.

- [ ] **Step 1: Failing tests** (PlayMode: `game.Tap(cell)` returns `"busy"` while `Intro`; after ≤ 1.8 s unscaled `Intro == false`; skip path).
- [ ] **Step 2: Run, expect fail.**
- [ ] **Step 3: Implement** `LevelIntro` + game/HUD hooks + bounce.
- [ ] **Step 4: Run** PlayMode `FeelPlayTests StudioPolishPlayTests ColorHuntPlayTests PotaFlowPlayTests`, EditMode `BlockViewTests`.
- [ ] **Step 5: Commit** `feat(feel): F1 level intro ribbon + goal flight + cascade under the Pota look; F6 directional landing bounce`

---

### Task 4: F3 LeftoverFinale

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Feel/LeftoverFinale.cs`
- Modify: `ColorHuntGame.cs:367` `End` (if won && Feel: `Finale.Play(Board.Ammo, onDone: () => { Disperse(); _resultTimer = S.WinWait; })`, result timer starts after the finale; else today's path). Add `public LeftoverFinale Finale` set by HUD `Bind`. Expose `BurstAt(int cell)` (the Pota burst used by `CellBurst`, no rule change: visual only, block view hidden after).
- Modify: `ColorHuntHud.cs` (coin badge: gold chip top-centre with rolling number via `RollWindow`; `Bind` creates `LeftoverFinale` when Feel)
- Uses: `ColorHuntSettings.RoyalCoin` mesh (exists), `SalvoBall` material + `BallSalvo` visuals (reuse `BallSalvo.Launch` if it accepts a target world point; else a tiny `FinaleBall` pooled quad path).
- Test: `FeelCurvesTests` (plan tests from Task 1 already), `FeelPlayTests.cs` (+`Finale_140Remaining_Shows14Coins_ThenEnds_Under2s`, +`Finale_ZeroRemaining_NoBadge_EndsImmediately`)

**Interfaces:**
- Produces: `LeftoverFinale : MonoBehaviour { void Init(ColorHuntHud hud, ColorHuntGame game); void Play(int remaining, Action done); bool Running; int CoinsShown; int Flights; }`.
- Sequence per spec §F3: panel pop (`hud.PopAmmo`), badge appear if coins > 0, flights at `FinaleGap`, each flight: ball from `hud.AmmoViewportPoint` (canvas→world ray at board z) to a random live cell (`game.RandomLiveCell(rng)`), on hit `game.BurstAt(cell)` + coin spawn (mesh, arc to badge canvas point via `StudioCurves.FlightPoint` in viewport space, 0.45 s), badge rolls `+1` per coin until `coins`, panel number steps down `plan[i]` per launch via `game.ShowAmmo`. Last arrival: badge `FeelCurves.Stamp`-style pop; then `done()`. Total ≤ `FinaleMax`.

- [ ] **Step 1: Failing PlayMode tests.** - [ ] **Step 2: Run, fail.** - [ ] **Step 3: Implement.** - [ ] **Step 4: Run PlayMode `FeelPlayTests ColorHuntPlayTests SpecialPlayTests`.** - [ ] **Step 5: Commit** `feat(feel): F3 leftover finale — remaining balls burst blocks into coins (Pota formula) before the win disperse`

---

### Task 5: F9 LoseMoment

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Feel/LoseMoment.cs`
- Modify: `ColorHuntGame.cs` `End(false)` → `Lose?.Play(reason)`; `BlockView.cs` (`public float Dim01` multiplies body colour toward grey via MPB — the `Dim` bool exists for unaffordable; add a separate float), `ColorHuntCamera.cs` (`public float BackOffset` added to the fit position along −forward, eased), `ColorHuntHud.cs` (per-row "N kaldı" label + red pulse; centre "Kıl payı!" text), `ColorHuntJuice.Result(false)` unchanged.
- Test: `FeelPlayTests.cs` (+`Lose_DimsBoard_LabelsRemaining_NearMissUnder3`, +`Lose_FeelOff_NoDim`)

**Interfaces:**
- Produces: `LoseMoment : MonoBehaviour { void Init(ColorHuntHud hud, ColorHuntGame game); void Play(string reason); bool NearMiss; string RemainingLabel(int color); }`. Remaining per colour = `Board.Targets[c].Remaining` (the same source the HUD rows use).

- [ ] Steps 1–5 as above; commit `feat(feel): F9 lose moment — board dims, remaining-goal labels pulse, "Kıl payı!" on near misses`

---

### Task 6: F7 BoardClip (stencil mask)

**Files:**
- Create: `Assets/_Project/Games/Pota/Shaders/PotaStencilMask.shader` (URP HLSL, `ColorMask 0`, `ZWrite Off`, `Stencil { Ref 1 Comp Always Pass Replace }`, queue `Geometry-10`, SRP-batcher CBUFFER)
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Feel/BoardClip.cs`
- Create: `Assets/_Project/Games/Pota/Editor/BoardClipInstaller.cs` (`[MenuItem("AI Tools/Rendering/Install Board Clip")]`, `[McpForUnityTool("ai_install_board_clip")]`): adds layer `PotaBoard` to TagManager if missing (as `PotaLookImportTool` added `PotaRoom`), for `Assets/Settings/PC_Renderer.asset` and `Mobile_Renderer.asset`: remove `PotaBoard` from `opaqueLayerMask`/`transparentLayerMask`, add two `RenderObjects` features ("BoardClip Opaque": event AfterRenderingOpaques, layer PotaBoard, queue opaque, stencil enabled Ref 1 Comp Equal; "BoardClip Transparent": AfterRenderingTransparents, queue transparent) — idempotent (skip when features named so exist).
- Modify: `BoardBuilder.cs`/`BlockView.cs` (block GameObjects and label quads on layer `PotaBoard` when the layer exists), `ColorHuntSceneTool.cs`, `PotaFlowSceneTool.cs`, `SpecialsSceneTool.cs` (call installer, add `BoardClip` to the board root), `PotaMaterialTemplates.cs` (+`StencilMaskTemplate` in settings `Material StencilMaskTemplate`)
- Test: `RuntimeShaderBuildTests` (template present), new EditMode `BoardClipInstallerTests` (`Install` twice → exactly two features per renderer, layer exists), PlayMode `FeelPlayTests` (+`BoardClip_MaskInsideFrame_FollowsRotation`: mask quad world size = `PanoSize − 2·FrameWidth`, rotation after `Rotate` equals frame)

**Interfaces:**
- Produces: `BoardClip : MonoBehaviour { void Build(ColorHuntGame game); Transform Mask; static bool Installed(); }`.

- [ ] Steps 1–5; commit `feat(feel): F7 board clip — refill blocks clipped to the frame with a stencil mask (renderer features installed by tool)`

---

### Task 7: Blender assets (miss lettering, clouds, birds, city band) + import

**Files (Blender MCP):**
- Modify: `tools/bl/vfx.py` (`_lettering` style `"miss"`: text `"KAÇTI!"`, red `#e8352e` gradient, navy ink, −8° skew; add to `RECIPES`)
- Create: `recipes/cloud_set.py` (`build(count=3, seed=7)` → objects `Cloud_0..2`, metaball blobs → mesh, `_toon` material white/light-blue shade, decimate to ≤ 400 tris each, origin bottom-centre, front −Y)
- Create: `recipes/bird_flock.py` (`build(count=5)` → one armature `Flock` with 5 birds (body + two wing planes, ≤ 120 tris each), bones `wing_L_i/wing_R_i`, action `flap` 12 frames at 24 fps looping ±35°; birds offset in a V)
- Create: `recipes/city_band.py` (`build(seed=7, radius=70, buildings=28)` → mesh `CityBand`, boxes 6–40 m on a 180° arc, window UV strip, two materials `city_wall` (Pota fog-tinted) / `city_glass` (emissive 0.15), ≤ 6 000 tris)
- Test: `tests/test_vfx.py` (+miss lettering tile alpha), `tests/test_recipes_feel.py` (make each, inspect tris/open edges/clip count)
- Unity: `ai_import_asset` for each manifest → `Games/Pota/Art/VFX/vfx-letter-miss/`, `Games/Pota/Art/Props/{cloud-set,bird-flock,city-band}/`; `ColorHuntSettings` fields `LetterMiss` (Texture2D), `Sky` block `{ GameObject[] Clouds; GameObject Birds; GameObject CityBand; }` filled by `StudioImportTool` (extend its mapping table).

- [ ] **Step 1: Blender tests failing** (`tools/run.sh test`). - [ ] **Step 2: Implement recipes** (`tools/run.sh make '{"name":"cloud_set","game":"Pota","id":"cloud-set","kind":"prop"}'`, `bird_flock` kind `animation`, `city_band` kind `prop`, `vfx_flipbook lettering id vfx-letter-miss params {"text":"KAÇTI!","style":"miss"} frames 1 frame_px 512 frame_h 256`). - [ ] **Step 3: Look at every `preview.png`** (Read tool) and fix shapes until they read as clouds/birds/city. - [ ] **Step 4: Import** into Unity, verify `matches`, assign settings. - [ ] **Step 5: Commit both repos** `feat(studio): miss lettering, cloud set, bird flock, city band recipes` / `feat(feel): import sky props and miss lettering`

---

### Task 8: F8 SkyLife

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/SkyLife.cs`
- Modify: `FeelCurves.cs` (+`Vector3 CloudDrift(int i, float t, float radius, float speed)`, `float BirdPassU(float t, float period, float duration)`), `PuzzleStage.cs` (`Build` adds `SkyLife` when `Game.S.Feel.Enabled`), `ColorHuntSceneTool.cs` / `PotaFlowSceneTool.cs` (stage builds it; Basket scene gets clouds + birds via `BasketEnvironmentBuilder` behind the tower)
- Test: `FeelCurvesTests` (+drift wraps, bird pass 0..1), PlayMode `FeelPlayTests` (+`SkyLife_ObjectsMove`: cloud position differs after 0.5 s, birds animate (`Animation.isPlaying` or playable time advances))

**Interfaces:**
- Produces: `SkyLife : MonoBehaviour { void Build(ColorHuntSettings s, Transform centre, float distance); int CloudCount; Transform Birds; }`; unscaled time; nothing in front of the board (y below board top − 2 m or behind z > board z + 20).

- [ ] Steps 1–5; commit `feat(feel): F8 sky life — drifting clouds, passing bird flock, city band behind the roof`

---

### Task 9: Basket F10 SlingshotFeel, F11 MissFeel, F12 SwishFeel, F13 count stamp

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Basket/Feel/SlingshotFeel.cs`, `MissFeel.cs`, `SwishFeel.cs`, `BasketCountFeel.cs`
- Modify: `ShotInput.cs` (+`public event Action<float, AimPoint, bool> Pulled;` — pull 0..1 = drag length / full-power length from `AimMapper`, aim, valid; fired every drag frame; `Released` on release), `Ball.cs` (+`public bool TouchedRim;` set in `OnRimContact` path, cleared on `Launch`), `BasketRound.cs` (expose `Hoop TargetHoopFor(AimPoint)` using `Layout` + `AimMapper`), `Hoop.cs` (+`SetGlow(float)` tinting rim material emission via MPB, +`Blink(Color, float)`), `PlayerActor.cs` (+`SetLean(float)`, `SetSlump(float)`; LateUpdate applies `Animator.GetBoneTransform(HumanBodyBones.Spine/Chest/Head/Hips)` rotations/offsets; `HandBall(bool)` creates/hides a ball mesh under `RightHand`), `RoundCamera.cs` (+`Vector3 PosOffset; float PitchOffset;` applied after the lerp, decays to 0), `BasketHud.cs` (RollWindow on `BasketText`, pop size `0.25 → 0.1` as landing rate rises; `FlowBadge` stamp on `RoundEnded` when `Feel` and `ShowResultPanel`), `BasketJuice.cs` (`OnMissed` → `MissFeel.Play()`; `OnHoopPassed` → `SwishFeel.Pass(hoop, ball)`), `BasketSceneTool.cs` (adds the four components), `BasketCage.cs` (+`Glow(float)`)
- Test: EditMode `BallTests` (new: `TouchedRim` set/cleared), `AimMapperTests` (+pull fraction), PlayMode `BasketFeelPlayTests.cs` (new: pull → lean > 0 and hand ball visible; shot → hand ball hidden within 0.1 s; `Feel.Enabled=false` mid-pull → lean 0 next frame; forced miss → `MissFeel.Playing`; clean pass fixture (ball dropped through the top hoop centre programmatically) → `SwishFeel.LastClean == true`; rim-touch fixture → false; round end → badge visible)

**Interfaces:**
- Produces: `SlingshotFeel { void Bind(BasketRound, ShotInput, PlayerActor, RoundCamera, AimPreview); float Pull; }`, `MissFeel { void Play(); bool Playing; }`, `SwishFeel { void Pass(Hoop, Ball); bool LastClean; }`, `BasketCountFeel { void Bind(BasketHud, BasketRound); }`.
- `AimPreview` gains `SetPower(float pull)` (width 0.06→0.11, alpha 0.6→1, marker pulse at pull ≥ 0.98).

- [ ] Steps 1–5 (TDD per component, one commit per component is fine): `feat(feel): F10 slingshot feel`, `feat(feel): F11 miss feel`, `feat(feel): F12 clean-pass SWISH`, `feat(feel): F13 rolling basket count and end stamp`

---

### Task 10: Recording parameters, scenarios, Pota captures, deliverables

**Files:**
- Modify: `Editor/ColorHuntRecordTool.cs` (+`feel` on|off → sets `S.Feel.Enabled` for the recording and restores after; ColorHunt-scene `scenario`: `intro` (script `wait 2.2`), `lowammo` (ammo 9, script `wait 0.6; tap auto; settle; wait 1.5`), `finale` (win with ammo 140), `lose` (ammo 9 board with no affordable move: seed search helper `ColorHuntStaging.LosingSeed(ammo)`), `chain` (seed 7 + the tap `ColorHuntHint.Best` with ≥ 4 steps), `idle` (`wait 7`)), `Editor/BasketRecordTool.cs` (+`feel`, `pull` (phase: drive `ShotInput.SimulatePull(u, aim)` for 1.2 s, hold 0.3, shoot), `miss` (aim x 2.5, power 1.0), `swish` (aim found by `ai_basket_sweep` data: x 0, power 0.62 on level 1 — verify)), `Tools/potalook/record-pota.mjs` (+scenarios intro/lose/win via `__pota` hooks already used for `sepetBitir`), `Tools/potalook/compare.py` + `side_video.py` (3 columns; already generic)
- Create: `Tools/feel_record.sh` (runs the Unity scenarios with feel off and on, Pota where applicable, builds sheets and videos under `docs/screenshots/2026-10-03-feel-*`)
- Test: `ColorHuntRecordToolTests` (+parse `feel`, scenario list), `SpecialsRecordPlayTests`-style smoke for one scenario

- [ ] Steps: tests → implement → run every scenario → **look at every sheet and video** (Read) → fix flaws found (loop) → commit `feat(feel): recording scenarios and Pota | before | after deliverables`

---

### Task 11: Docs, backlog, memory, hand-off draft

**Files:**
- Modify: `Assets/_Project/Games/Pota/README.md` (new section "Oyun içi his katmanı (2026-10-03)": switch, components, scenarios, deliverables, honest notes), `CLAUDE.md §12` (new tools/params, lessons), `~/projects/Blender MCP/docs/backlog.md` (tick U21? no — confetti stays at win; tick U27, U40, U47 with date+commit; add new deferred items), memory files (`MEMORY.md` pointer + project memory `pota-feel-layer-2026-10-03.md`)
- Create: `docs/handoff/2026-10-03-pota-oyun-ici-his.md` (for the Pota session: per moment timeline, formulas, assets, reference code paths; a "Tuna'nın seçimi" table left for Tuna to fill)

- [ ] Run full EditMode + PlayMode suites, record counts in README/CLAUDE.md. - [ ] Commit `docs: in-game feel layer notes, backlog, hand-off draft`.

---

## Self-review notes

- Spec coverage: F1 (T3), F2 (T1 pulse + T2), F3 (T4), F4 (T1), F5 (T1), F6 (T3), F7 (T6), F8 (T7+T8), F9 (T5), F10–F13 (T9), recording/deliverables (T10), docs (T11). Blender table (T7).
- Names used across tasks: `ColorHuntGame.Polish`, `FeelCurves.*`, `FeelTuning` fields, `LevelIntro.ShowRibbon/FlyGoals/Skip/Done`, `LeftoverFinale.Play(int, Action)`, `LoseMoment.Play(string)`, `BoardClip.Build/Installed`, `SkyLife.Build`, `ShotInput.Pulled`, `Ball.TouchedRim`, `Hoop.SetGlow/Blink`, `PlayerActor.SetLean/SetSlump/HandBall`, `RoundCamera.PosOffset/PitchOffset`.
- Review Focus tests are placed in Tasks 2, 3, 4, 5, 6, 9.
