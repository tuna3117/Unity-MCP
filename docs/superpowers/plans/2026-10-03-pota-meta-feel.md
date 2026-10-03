# Pota Menu / Reward Layer Feel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild Pota's menu/reward screens 1:1 in a Unity showcase scene (`Scenes/Meta.unity`) and add the feel moments M1–M11 of the spec behind `MetaSettings.Feel.Enabled`, recorded as Pota | Önce | Sonra.

**Architecture:** A uGUI kit (`MetaStyle` sprites, `MetaButton`, `MetaWindow`, `RollingNumber`, `CoinFlight`, `Toast`, `ScreenSlide`) that every screen composes; pure sequence classes (`WinSequence`, `MapLayout`, `MapProgress`, `ReelSpin`, `CoinPlan`) hold every timing and are EditMode-tested; `MetaDirector` drives a sample save through the showcase flow; `ai_potameta_import` brings Pota's UI images and the Fredoka font; `ai_meta_build_scene` builds the scene; `ai_meta_record` + `Tools/meta_record.sh` make the deliverables.

**Tech Stack:** Unity 6000.6.2f1, uGUI legacy `Text` (Lilita One + Fredoka static instances via fonttools), URP, Unity Test Framework via `Tools/unity_tests.py`, Blender workshop (`vfx_flipbook` stills), MCP bridge.

**Spec:** `docs/superpowers/specs/2026-10-03-pota-meta-feel-design.md`; style guide `docs/reference/2026-10-03-pota-meta-ui-style.md` (every colour, size and asset name comes from there).

## Global Constraints

- Feel off (`MetaSettings.Feel.Enabled = false`) = Pota's instant behaviour (no transitions); every duration 0.
- Pota is read-only (copy its `public/assets` images; never write there). No push, no paid calls, never bring Unity to the front.
- 390 css px scale: canvas `CanvasScaler` reference 1080×1920 match width; `MetaStyle.Css = 1080/390` like `ColorHuntHud.Css`.
- Scene edits through tools only; `[SerializeField]` for tool-assigned references; buttons wired at runtime in `Bind` (editor-added listeners are not saved).
- Compile check after every script change (`refresh_unity` force + `read_console error CS`); no compile in Play Mode.
- Commit after each task with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

1. Feel toggled while a window is open or a coin flight runs: no stuck overlay, no coins left mid-air, counters land on the true value (Task 2, 4).
2. Win with a medal already gold / league bar exactly at a threshold: no double promotion, bar never exceeds 1 (Task 4).
3. Map progression from level 50 (last node) and from level 1 (no previous node): no index errors, pin placement valid (Task 5).
4. Bonus with 0 wins ("Bu sefer olmadı") and with a +N cell: payout flight only when coins > 0 (Task 6).
5. Market purchase with insufficient coins: shake + red blink, no coin flight, no state change (Task 6).

---

### Task 1: Assets, settings, style kit (M1, M3 core)

**Files:**
- Create: `Assets/_Project/Games/Pota/Editor/PotaMetaImportTool.cs` (`ai_potameta_import`: copies the style guide §12 images from `~/projects/Pota/public/assets/` into `Games/Pota/Art/Meta/`, sets importers (Sprite, no mips, alpha), downloads Fredoka variable TTF from Google Fonts OFL and instances `wght` 600/700 with fonttools (`uv run --with fonttools python -m fontTools.varLib.instancer`), writes `Art/Fonts/Fredoka-SemiBold.ttf`, `Fredoka-Bold.ttf` + OFL txt; wires `MetaSettings`)
- Create: `Assets/_Project/Games/Pota/Scripts/Meta/MetaSettings.cs` (`Feel` block `MetaFeelTuning` + sample save `SampleSave { Level=20, Coins=145, Lives=3, Trophies=11800, BestScore=0, FirstTry=true }` + thresholds + sprite/font refs + palette)
- Create: `Assets/_Project/Games/Pota/Scripts/Meta/Ui/MetaStyle.cs` (colours from §0, `Css`, sprite makers: `Panel(top,bottom,border,radius,borderW)`, `Ribbon`, `Circle`, `Shadow`; `PressScale(u)`, `ReleaseScale(u)`, `WindowOpen(u)`, `RibbonDrop(u)`, `BackdropAlpha(u)`)
- Create: `Scripts/Meta/Ui/MetaButton.cs` (uGUI Button + 3D shadow layer; press/release curves; `Pressed`, `Scale`), `MetaWindow.cs` (`Open/Close`, backdrop, box, ribbon, close ×; `IsOpen`, `Phase`), `RollingNumber.cs` (`Set(int, roll:bool)`, `Value`, `Rolling`), `CoinFlight.cs` (`Fly(from, to, count, onEach, onDone)`, `CoinPlan.Count(amount)` = clamp(amount/5, 8, 12) pure), `Toast.cs`, `ScreenSlide.cs` (`Show(page, from:bottom)`/`Hide`)
- Test: `Tests/EditMode/MetaStyleTests.cs` (curves: press 0.94 at u=1, release peaks >1.02 then 1, window open peaks ~1.05 then 1, ribbon drop overshoots, backdrop 0→0.62; `CoinPlan.Count` bounds; Feel off → all durations 0 via `MetaFeelTuning.Scaled(seconds)`), `Tests/EditMode/MetaAssetsTests.cs` (every §12 sprite and both fonts present in `MetaSettings` after import)

**Interfaces:**
- Produces: `MetaFeelTuning { bool Enabled=true; float WindowOpen=0.28f, WindowClose=0.15f, Backdrop=0.18f, RibbonDrop=0.2f, Press=0.06f, Release=0.18f, Slide=0.25f, CoinFlight=0.45f, CoinStagger=0.04f, CoinBurst=0.15f, Roll=0.14f, ToastHold=1.2f; float T(float s) => Enabled ? s : 0f; }`
- Produces: `MetaStyle` constants (`Navy`, `Navy2`, `Orange`, `Orange2`, `Gold`, `Gold2`, `Green`, `Green2`, `Cream`), sprite makers, curves; `MetaButton.Create(parent, kind: Big|Small|Chip|Round, color: Green|Orange|Blue|Navy, label, css size)`; `MetaWindow.Create(parent, titleCss, widthCss=343)`; `RollingNumber.Attach(Text)`; `CoinFlight.Create(canvasRoot, coinSprite)`.

- [ ] **Step 1: Write the failing tests** (EditMode: curves, CoinPlan, Feel.T, assets wired). Run: `uv run Tools/unity_tests.py EditMode Project.Tests.MetaStyleTests Project.Tests.MetaAssetsTests` → compile errors / nulls.
- [ ] **Step 2: Implement** settings, style, kit, import tool; run `ai_potameta_import`; assets land in `Art/Meta/`, fonts in `Art/Fonts/`.
- [ ] **Step 3: Run the tests** → pass. Whole EditMode suite green.
- [ ] **Step 4: Commit** `feat(meta): Pota UI assets + Fredoka, MetaSettings, style kit (windows, buttons, rolling numbers, coin flight)`

---

### Task 2: Scene tool, main screen (M7), tab bar, screen slides (M2), settings, toast

**Files:**
- Create: `Editor/MetaSceneTool.cs` (`ai_meta_build_scene`: camera (UI only, solid colour `#8ec9f2`), EventSystem, `MetaRoot` canvas 1080×1920 match-width, `MetaDirector` + screens, Build Settings + `PotaProfile`; Game view 1170×2532)
- Create: `Scripts/Meta/MetaDirector.cs` (state: Menu|Map|Market|Level|Win|Lose|League|Promotion|Bonus|Settings; sample save mutation rules: win → coins += 15 (+10 first try) + leftover coins (FeelCurves.LeftoverCoins), trophies += score gain, level++; lose → lives−1; `OnWin(score, leftover)`, `OnLose(remaining)`; events `StateChanged`, `CoinsChanged`, `TrophiesChanged`, `LivesChanged`)
- Create: `Scripts/Meta/Screens/MenuScreen.cs` (background by level, top strip badges (avatar+level pill, league badge+short trophies `KupaKisa`, coins+plus, lives+timer, gear), hero cut-out breathing + ball hop, Play button pulse + shine sweep, strip entrance), `TabBar.cs` (Market/Ana/Harita, selected card), `SettingsWindow.cs`, `DevStrip.cs` (Kazan / Kaybet / Terfi / Bonus / Öğretici / Feel toggle)
- Test: `Tests/EditMode/MetaDirectorTests.cs` (win/lose save rules, `KupaKisa(13500)=="13,5K"`, hero breath curve), `Tests/PlayMode/MetaPlayTests.cs` (scene loads; menu shows "Bölüm 20", coins "145"; tab to Map slides in ≤ Slide+0.1 s and back; settings opens/closes; Feel off → slide instant; toast appears and leaves)

- [ ] Steps: failing tests → implement → build scene via tool → tests green → commit `feat(meta): Meta scene, main screen with living hero and shine, tab bar slides, settings, toast`

---

### Task 3: Level window, win window (M4), lose window (M5)

**Files:**
- Create: `Scripts/Meta/Screens/LevelWindow.cs`, `WinWindow.cs`, `WinSequence.cs` (pure: `Stage(t)` → ribbon/stamp/score fraction/medal (bronze→silver→gold by score crossing `Medal thresholds` as fraction of the count-up)/trophy roll/league bar fill 0..1 (old→new, overflow flag)/coins burst/continue pulse), `LoseWindow.cs`
- Modify: `MetaDirector.cs` (Oyna → "game" fade → DevStrip result; Win → WinWindow with `WinSequence` numbers; Lose → LoseWindow)
- Test: `Tests/EditMode/WinSequenceTests.cs` (score reaches total at 0.45+0.8; medal flips at the threshold-crossing times and never flips back; bar fill = (new−floor)/(ceil−floor) clamped, overflow when league changes; Feel off → everything at its end state at t=0), `MetaPlayTests` (+ win window: score text equals total after 1.4 s, medal sprite is silver for the sample, coin badge rolled to 145+15+10+leftover, Devam pulses; lose: remaining "3" + "Kıl payı!" tag, orange button pulses; Feel toggled off mid-sequence → final state next frame)

- [ ] Steps as above; commit `feat(meta): level window; win window with medal escalation, rolling trophies, league bar, coin flight; lose window with near-miss tag`

---

### Task 4: Map + progression (M6)

**Files:**
- Create: `Scripts/Meta/Screens/MapScreen.cs`, `MapLayout.cs` (pure: Pota formula `x = W/2 + sin(n·0.85)·W·0.3`, `y = 90 + (50−n)·118`; `SenYeri` port of `senYeri` with DX/DY candidates), `MapProgress.cs` (pure timeline: node turn-green 0.2, medal stamp 0.3, dots lit sequence 0.04 each, pin hops 3 in 0.7 s, unlock at T, pulse after), `MapPin.cs`
- Modify: `MetaDirector.cs` (after Devam: Map with progression from level−1 to level)
- Test: `Tests/EditMode/MapLayoutTests.cs` (positions match Pota for n=1,2,50; `SenYeri` never overlaps a node (distance > 40) and stays inside the width; level 1 has no previous, level 50 no next), `MapProgressTests.cs` (ordering of events, total ≤ 2.2 s, Feel off → instant), `MetaPlayTests` (+ map progression ends with node 21 unlocked and pulsing, pin beside it)

- [ ] Steps; commit `feat(meta): level map 1:1 and the progression after a win (medal stamp, lit path, hopping pin, unlock burst)`

---

### Task 5: League/promotion (M8), bonus (M9), market (M10)

**Files:**
- Create: `Scripts/Meta/Screens/LeagueWindow.cs`, `PromotionWindow.cs`, `BonusWindow.cs` + `ReelSpin.cs` (pure: duration 1400 + 120·col, ease `cubic-bezier(.25,.9,.3,1)` approximated by `MetaStyle.ReelEase`, stop squash 0.12 s, result rows), `MarketScreen.cs` (tabs, showcase with cut-out hero + podium, slot column, chips 5-col, big button, cards; catalogue prices from the style guide/Pota `katalog.ts`), purchase celebration
- Test: `Tests/EditMode/ReelSpinTests.cs` (stop order, squash window, Feel off instant), `MetaPlayTests` (+ promotion: new badge scale peaks > 1.2 then 1, old gone; bonus: 3 reels stop in order, win cells glow, coins fly when payout > 0; market: buy 120-coin jersey → coins 145→25 after flight, chip "Senin", hero hop; insufficient → shake, no change)

- [ ] Steps; commit `feat(meta): league + promotion, bonus reels, market with purchase celebration`

---

### Task 6: Blender hand + cloud still, tutorial hand (M11), menu clouds (M7)

**Files (Blender MCP):** `tools/bl/vfx.py` recipes `hand_pointer` (inked toon hand, index finger up, 512², `RECIPES` entry) and `cloud_still` (soft white cloud, 512×256); tests in `tests/test_vfx.py`
**Files (Unity):** `StudioImportTool.cs` ids + `MetaSettings.HandSprite/CloudSprite`; `Scripts/Meta/Screens/TutorialHand.cs` (`PointAt(RectTransform)`, `TapLoop`, `ArcMove(from, to)`), `MenuScreen` clouds (2 sprites drifting 0.3 m/s equivalent 12 css px/s)
- Test: Blender tests (alpha coverage, outline), `MetaPlayTests` (+ hand reaches the target within 0.3 s and taps every 1 s; clouds move)

- [ ] Steps; commit both repos.

---

### Task 7: Recording + deliverables

**Files:**
- Create: `Editor/MetaRecordTool.cs` (`ai_meta_record {scenario: menu|level|win|lose|map|promotion|bonus|market|tutorial, feel: on|off}`; drives `MetaDirector` script per scenario, markers in events.json: `open`, `score`, `medal`, `coins`, `unlock`, `stop`, `buy`, `tap`, `end`), `Tools/meta_record.sh` (records off/on for each scenario, Pota column from `~/projects/Pota/onizleme/` stills (copied to Temp), `side_video.py groups` + `frame_sheet.py --anchor`, `docs/screenshots/2026-10-03-meta-*`)
- Test: `Tests/EditMode/MetaRecordToolTests.cs` (scenario list, feel parse)
- [ ] Record, LOOK at every sheet/video, fix flaws, commit `feat(meta): recording scenarios and Pota | Önce | Sonra deliverables`

---

### Task 8: Docs, backlog, memory, combined hand-off

- README section "Menü ve ödül katmanı (2026-10-03)", CLAUDE.md §12, backlog items, memory; `docs/handoff/2026-10-03-pota-menu-odul-his.md` (M1–M11 timings, assets, reference code; combined delivery note with the F1–F13 hand-off).
- Full EditMode + PlayMode runs; commit.

## Self-review

- Spec coverage: M1 (T1), M2 (T2), M3 (T1 core + T3/T5 uses), M4 (T3), M5 (T3), M6 (T4), M7 (T2 + T6 clouds), M8/M9/M10 (T5), M11 (T6), import/scene/record (T1/T2/T7), docs (T8).
- Names: `MetaSettings.Feel` (`MetaFeelTuning.T`), `MetaStyle`, `MetaButton`, `MetaWindow`, `RollingNumber`, `CoinFlight`, `Toast`, `ScreenSlide`, `MetaDirector`, `WinSequence`, `MapLayout`, `MapProgress`, `ReelSpin`.
- Review Focus tests sit in T3 (1, 2), T4 (3), T5 (4, 5).
