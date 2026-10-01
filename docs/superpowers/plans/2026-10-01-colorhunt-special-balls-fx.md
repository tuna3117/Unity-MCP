# Color Hunt — Special Balls and Royal Match–style Effect Candidates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unity Color Hunt gets Pota's special balls (column/row line, bomb, golden ball with gates and the basket) ported 1:1 and
proven against fixtures produced by Pota's own rules, a special-effect look switch — Pota (today's feedback, 1:1) / Royal (1, Royal
Match language) / Court (2, basketball language) — built with Blender flipbooks, meshes and lettering, a demo scene Tuna plays with,
and a silent Pota | 1 | 2 side video plus a voiced sequential video.

**Architecture:** The rules grow in a partial `ColorHuntRules` (`ColorHuntRules.Specials.cs`) and stay pure; a fixture generator runs
Pota's `src/renkAvi.ts` in a scratch copy and writes JSON that EditMode tests replay. `ColorHuntGame` replays the new step fields
(births, triggers, basket drops, opened gates) and asks an `ISpecialStyle` to draw them; a pure `SpecialSchedule` turns a trigger step
into per-cell start times (Pota: all at once; Royal/Court: rockets sweep, bomb waves, chained specials fire when reached). Normal block
bursts keep using `IBurstStyle` (Pota look). Blender hand-offs come in through `ai_studio_import`; the demo scene is built by
`ai_specials_build_scene`; recordings by `ai_colorhunt_record` with a sound log mixed by ffmpeg.

**Tech Stack:** Unity 6000.6.2f1, URP 17.6 (Forward+, Render Graph), hand-written URP HLSL, ParticleSystem (Shuriken), uGUI, Unity Test
Framework; Blender 5.2.2 LTS headless (`tools/run.sh`, EEVEE, bmesh, numpy); TypeScript via the Pota copy's `tsx`; Python + Pillow via
`uv`; ffmpeg (`/opt/homebrew/bin/ffmpeg`).

**Spec:** `docs/superpowers/specs/2026-10-01-colorhunt-special-balls-fx-design.md` (Tuna approved the two directions and section 1 on
2026-10-01: "evet devam edebilirsin").

## Global Constraints

- Rules are Pota's `src/renkAvi.ts` 1:1 for specials and gates only: no obstacles (file/buz/hakem), no boosters, no bonus, no special
  target types, no HUD golden counter (backlog U25 keeps them). Fixtures from Pota's own code are the referee.
- The level the game ships today (bölüm 20, `ColorHuntSettings.Specials` = all off) behaves exactly as before: every existing EditMode
  and PlayMode test stays green; with features off the rng consumption is unchanged.
- `ColorHuntSettings.SpecialLook` defaults to `Pota` (the winner becomes the default in a later round, not in this plan).
- Normal block bursts stay the Pota look in all three special looks; only special-ball moments change.
- Every trigger step lasts ≤ 0.6 s from the tap to the drops' start (`SpecialSchedule` tests pin it for a 7×7 line + bomb chain).
- Live particles + pieces per special look ≤ `BurstBudget.Max` (1026) including the Pota burst pools.
- Sounds: Pota's samples rendered through Pota's chain (`run_export.sh`); new Royal/Court sounds synthesised (`PotaSounds.Synth`). All
  special sounds go through `PotaSounds.PlayClip` so the recorder logs them. No paid calls, no AI services.
- `~/projects/Pota` is read-only: everything runs in a scratch copy (`rsync -a --exclude .git ~/projects/Pota/ "$S/pota-copy/"`).
- Unity 6 / URP only: HLSL + URP ShaderLibrary, `UniversalForward`, `CBUFFER_START(UnityPerMaterial)`; Input System only;
  `FindAnyObjectByType`.
- Every runtime material comes from a template asset through `RuntimeMaterial.From(template, "shader", "name")`; each new shader gets a
  template in `PotaMaterialTemplates` and a field in `ColorHuntSettings` (`RuntimeShaderBuildTests` stays green).
- Never hand-edit `.unity/.prefab/.asset/.mat` YAML; never `mv`/`rm` assets (use `AssetDatabase`); no compile in Play Mode; buttons are
  bound at runtime (listeners added in Edit Mode are not saved).
- Blender repo: tools are functions registered in `bl.TOOLS`, return JSON-able dicts and log one `[bl] …` line; new tool = failing test
  first (`tools/run.sh test <file>`); outputs under `assets/<game>/<id>/export/`; quote paths (`Blender MCP` has a space).
- Commits in English ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, per task, in each repo; never push.
- On-screen texts Turkish as Pota: "ÇİZGİ!", "BOMBA!", "ALTIN TOP!"; Court lettering is English as in the spec ("SLAM!", "SWISH!",
  "COMBO!").
- Deliverables: `docs/screenshots/2026-10-01-specials-*` (sheet, silent side video, voiced sequential video, close-ups).

## Review Focus

1. **Tapping while a trigger plan or a golden flight runs:** `Tap` returns `"locked"`, nothing fires twice, the golden still lands once.
   Pinned by `SpecialPlayTests.Tap_WhileGoldenFlies_IsLocked` (Task 5).
2. **Restarting a scenario or a new round while rockets, coins or a golden ball are in the air** (the demo's "Tekrar", the record tool):
   the style is cleared, no callback reaches the new board, the lock is released, `GoldenInFlight` is 0. Pinned by
   `SpecialPlayTests.Stage_WhileEffectsRun_ClearsEverything` (Task 5) and `RoyalPlayTests.Restart_MidRocket_LeavesNothing` (Task 10).
3. **Switching the special look mid-effect** (demo buttons): the old style is cleared and deactivated, the new one draws the next
   trigger, no orphaned pooled object. Pinned by `SpecialStyleTests.SetSpecialLook_ClearsTheOldStyle` (Task 5).
4. **Mutual chains** (a bomb in a line's path whose 3×3 holds that line, two lines crossing): every special fires exactly once, the plan
   terminates, every exploded cell has exactly one start time. Pinned by `SpecialScheduleTests.MutualChain_FiresEachOnce` (Task 5).
5. **Rotating with a golden ball on the bottom row above an open gate** and **a board whose only move is a special** (ammo 0): the
   rotation settles into a basket step and the gate rings stay on the frame; `HasMove` is true while a line or bomb remains. Pinned by
   `SpecialRulesTests.RotateAndSettle_DropsGoldenOnGate`, `SpecialRulesTests.HasMove_TrueWithOnlyASpecial` (Task 2) and
   `SpecialPlayTests.Rotate_GoldenOnGate_LandsInTheBasket` (Task 5).

## Decisions taken while planning (each is a ruling for the ledger)

1. **Fixtures come from Pota's own rules.** `Tools/potalook/special_fixtures.ts` imports the scratch copy's `src/renkAvi.ts` and runs
   with that copy's `tsx`; the JSON is committed under `Games/Pota/Tests/EditMode/Fixtures/`. Unity builds each start board from the
   JSON (not from `Create`) so a rules difference shows at the first diverging step.
2. **Specials fire only from a tap.** In Pota `patlamaAlani` runs only in `kir`; match steps never contain triggers (specials have colour
   −1 and never join a run). So only the first step of a special tap has a trigger plan; chain steps keep today's rhythm.
3. **`Born.Group`** (the other cells of the matched group) is added for the look only; Pota has no such field and the fixtures compare
   `Cell`/`Kind`, not `Group`.
4. **The Pota look draws the falling golden ball as a gold ball** (Pota pushes it into `topListesi` with scale 1.25 and colour
   `#ffc93c`), reusing the salvo ball's mesh and material tinted gold.
5. **No HUD for golden balls or special targets** (U25). `ColorHuntGame.GoldenLanded` counts landings for tests and the demo label.
6. **Demo boards are hand-authored presets** (`SpecialScenarios`), each pinned by an EditMode test that its scripted tap produces the
   scenario's event (a birth, a line, a bomb, a chain, a golden landing).
7. **Gate rings live on the game root, not the board**, so they do not turn with the board (Pota keeps them in `cerceveGrup`).
8. **Voiced video is sequential, the side video is silent:** three simultaneous soundtracks cannot be judged; the sequential video plays
   each scenario as Pota → 1 → 2 with the logged sounds mixed by ffmpeg (`adelay` + `amix`).

## How to run things

- Unity open with the bridge up (`Tools/unity_up.sh`). Compile + test: `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Project.Tests.Fixture ...]`
  (exit 2 on `error CS`). Pota tests live in assemblies `Pota.Tests.EditMode` / `Pota.Tests.PlayMode`, namespace `Project.ColorHunt.Tests`
  (check the existing files for the exact namespace before adding a fixture).
- MCP: `~/.local/bin/uv run Tools/mcp_call.py call <tool> '<json>'`; custom tools via `execute_custom_tool`; menu items via
  `execute_menu_item {"menu_path": "..."}`.
- Blender: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh <tool> '<json>'`, tests `tools/run.sh test [test_vfx.py]`.
- `$S` = `/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad`.
- Node: `~/.nvm/versions/node/v22.23.2/bin/node`; ffmpeg: `/opt/homebrew/bin/ffmpeg`.
- Work on `main` in both repos, commit per task, never push.

## File map

| File | Responsibility | Task |
|---|---|---|
| `Tools/potalook/special_fixtures.ts`, `Tools/potalook/run_fixtures.sh`, `Games/Pota/Tests/EditMode/Fixtures/special-rules.json` | Fixtures from Pota's rules | 1 |
| `Scripts/ColorHunt/ColorHuntRules.cs`, `Scripts/ColorHunt/ColorHuntRules.Specials.cs`, `Tests/EditMode/SpecialRulesTests.cs`, `Tests/EditMode/SpecialRulesFixtureTests.cs` | Specials, gates, basket, rotate-and-settle | 2 |
| `Tools/potalook/{export-page.ts,export.mjs,run_export.sh}`, `Editor/PotaLookImportTool.cs`, `Scripts/ColorHunt/LabelAtlas.cs`, `ColorHuntSettings.cs` | Special stickers in the label atlas, special sounds | 3 |
| `Scripts/ColorHunt/{BlockView,PotaLook,ColorHuntGame}.cs`, `Scripts/ColorHunt/Specials/GateRings.cs` | Specials drawn on the board, gate rings, `Stage(Board)` | 4 |
| `Scripts/ColorHunt/Specials/{ISpecialStyle,SpecialSchedule,GoldenPath,PotaSpecials}.cs`, `ColorHuntGame.cs`, `PotaSounds.cs` | Special tap, trigger plan, birth, golden flight, the Pota look | 5 |
| `Scripts/ColorHunt/Specials/{SpecialScenarios,SpecialsDemo}.cs`, `Editor/SpecialsSceneTool.cs`, `Scenes/Specials.unity` | Demo scene and presets | 6 |
| Blender `tools/bl/vfx.py`, `tests/test_vfx.py`, `assets/Pota/vfx-*` | Flipbooks: fire, flame jet, dust ring, speed lines | 7 |
| Blender `tools/bl/vfx.py`, `tools/bl/__init__.py`, `tests/test_vfx.py`, `assets/Pota/vfx-*` | Meshes and stills: ball halves, basketball, coin, bomb shell, floor crack, lettering | 8 |
| `Editor/StudioImportTool.cs`, `Editor/PotaMaterialTemplates.cs`, `ColorHuntSettings.cs`, `Art/VFX/<id>/*` | Import the Blender hand-offs, toon template | 9 |
| `Scripts/ColorHunt/Specials/RoyalSpecials.cs` (+ shader if needed) | 1 · Royal | 10 |
| `Scripts/ColorHunt/Specials/CourtSpecials.cs` (+ shader if needed) | 2 · Court | 11 |
| `Editor/ColorHuntRecordTool.cs`, `PotaSounds.cs`, `Tools/potalook/{seq_video.py,test_seq_video.py,side_video.py,compare.py}` | Recordings, sound log, sheet, side and voiced videos | 12 |
| `Games/Pota/README.md`, `CLAUDE.md`, Blender `docs/studyo.md`, backlog, memory | Docs | 13 |

(`Games/…`, `Core/…` are under `Assets/_Project/`; `Scripts/…`, `Editor/…`, `Tests/…`, `Scenes/…`, `Art/…` under
`Assets/_Project/Games/Pota/`.)

Task order: 1 → 2 → 3 → 4 → 5 → 6; 7 → 8 → 9 (Blender, independent of 1–6); 10 and 11 need 5, 6 and 9; 12 needs 10 and 11; 13 last.

## Shared contract (names every task uses)

Namespace `Project.ColorHunt` (assembly `Pota.Runtime`) unless stated.

**Rules (Task 2 produces):**
```csharp
public enum SpecialKind { None = 0, LineV = 1, LineH = 2, Bomb = 3, Golden = 4 }   // Pota cizgiDikey = LineV (clears the COLUMN)
// struct Block gains: public SpecialKind Special; public bool IsSpecial => Special != SpecialKind.None;
//   public static Block MakeSpecial(SpecialKind kind) => new Block(ColorHuntRules.SpecialColor, 0) { Special = kind };
[Serializable] public struct Features { public bool Line, Bomb, Golden; public bool Any => Line || Bomb || Golden;
    public static readonly Features None = default; public static readonly Features All = new Features { Line = true, Bomb = true, Golden = true }; }
public struct Born { public int Cell; public SpecialKind Kind; public int[] Group; }    // Group: the group's other cells, sorted, birth cell excluded
public struct Trigger { public int Cell; public SpecialKind Kind; }
public struct BasketDrop { public int Cell; public int Col; }
public enum BreakOutcome { Broken, Insufficient, Golden }
// class Step gains: public readonly List<Born> Born; List<Trigger> Triggered; List<BasketDrop> ToBasket; List<int> Opened;  (all new List<>(), never null)
// class Board gains: public Features Features; public List<int> Gates = new List<int>(); public int Golden;   (Clone copies Gates)
// class LevelDesign gains: public Features Features;
// struct BreakResult gains: public BreakOutcome Outcome;   (Broken == (Outcome == BreakOutcome.Broken))
public struct RotateResult { public Board Rotated; public Board Board; public List<Step> Steps; }
public struct BlastArea { public List<int> Cells; public List<Trigger> Triggered; }
public static partial class ColorHuntRules {
    public const int SpecialColor = -1;
    public static List<int> SingleArea(int n, int cell, SpecialKind kind);            // tekAlan, clipped at the edges; Golden → empty
    public static BlastArea Blast(Board b, int cell);                                  // patlamaAlani, without ice
    public static List<int> ResolveMatches(Board b, out List<Born> born);              // eslesmeleriCoz: sorted exploded cells
    public static bool TryDropToBasket(Board b, out Board after, out Step step);       // sepeteDusur
    public static Board ResolveChains(Board b, List<Step> steps, int maxSteps = ChainCap);   // basket first, then matches
    public static RotateResult RotateAndSettle(Board b, Rotation dir);                 // dondurVeOtur
    public static BreakResult Break(Board b, int cell);                                // + special branch (free) and Golden outcome
    // Cheapest ignores specials (int.MaxValue when none); HasMove is true while a non-golden special is on the board.
}
```

**Board look (Task 3–4 produce):**
```csharp
// LabelAtlas: public const int SpecialBase = 105;  public static int SpecialCell(SpecialKind k);   // LineV/LineH 105, Bomb 106, Golden 107
// PotaLook: public static Color SpecialTone(SpecialKind k);   // OZEL_RENK_HEX through koyuDoygun (sRGB)
// ColorHuntSettings: public SpecialLook SpecialLook = SpecialLook.Pota; public Features Specials;   (enum SpecialLook { Pota, Royal, Court })
//   AudioClip SfxBomb, SfxLine, SfxGoldenBorn, SfxGoldenLand, SfxSlam, SfxSwish, SfxCrowd;   (bomba, cizgi, altin-dogum, altin-inis, smac-vurus, file-swish, kalabalik)
// BlockView gains: public float Bob; public float StickerSpin;  (style-driven idle offsets, reset in ResetStudio)
// Scripts/ColorHunt/Specials/GateRings.cs: public class GateRings : MonoBehaviour { public void Build(ColorHuntGame game); public void Show(IReadOnlyList<int> cols); public void Flash(int col); public Vector3 RingLocal(int col); }
// ColorHuntGame gains: public void Stage(Board board);   // deal a given board (presets, fixtures), same events as Prepare
//                      public GateRings Gates { get; }
```

**Game ↔ style seam (Task 5 produces):**
```csharp
public interface ISpecialStyle {
    bool Ready { get; }
    int LiveCount { get; }
    SpecialTiming Timing { get; }
    bool GathersBirthGroup { get; }       // Royal/Court: the group's blocks travel into the birth cell instead of bursting
    bool ConsumesSpecialView { get; }     // Royal/Court: a firing special's own block is hidden; the style draws it
    void Build(ColorHuntGame game);
    void Activate(bool on);
    void Clear();                         // drop every effect, never call back afterwards
    void Idle(BlockView v, float dt);     // each frame for every special on the board (game time)
    void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived);   // call arrived exactly once
    void Birth(BlockView v, SpecialKind kind);                 // at the drops' start; the view already shows the special
    void TriggerStep(int specials, Vector2 centre);           // once at a trigger step's start (board-local centre)
    void Fire(SpecialFire fire);                               // one special fires (origin first)
    void GoldenFlight(GoldenRoute route, Action passedGate, Action landed);   // call each callback exactly once, in that order
}
// public enum SpecialLook { Pota, Royal, Court }   — declared by Task 3 in ColorHuntSettings.cs, not here
[Serializable] public struct SpecialTiming {
    public float LineSpeed;   // cells per second a line sweeps; 0 = the whole area at once (Pota)
    public float LineLead;    // s from a line's fire to its first cells
    public float BombLead;    // s of anticipation before a bomb bursts
    public float BombRing;    // s between the bomb's centre and each further ring (Chebyshev distance)
    public static readonly SpecialTiming Pota = default;
}
public struct SpecialFire { public int Cell; public SpecialKind Kind; public Vector2 At; public int Order; public float Time; public int Size; }
public sealed class TriggerPlan { public readonly Dictionary<int, float> Start = new Dictionary<int, float>(); public readonly List<SpecialFire> Fires = new List<SpecialFire>(); public float End; }
public static class SpecialSchedule {
    // Start[c] = when cell c's swell starts (s from the step's start); Fires sorted by Time; End = last Start + swellTime (≥ every fire).
    public static TriggerPlan Plan(int n, Step step, int origin, SpecialTiming t, float swellTime);
}
public struct GoldenRoute { public Vector3 From; public Vector3 Gate; public Vector3 Hoop; public float Delay; public int Col; }   // game-root local
public static class GoldenPath {   // Pota RENK_AVI_HIS.altinInis: halkaSure 0.32, potaSure 0.5, kavis 1.4, aralik 0.12
    public const float GateTime = 0.32f, HoopTime = 0.5f, Arc = 1.4f, Gap = 0.12f;
    public static Vector3 At(GoldenRoute r, float t);   // t from leaving (after Delay); clamps at the hoop
    public static bool PastGate(float t); public static bool Landed(float t);
}
// ColorHuntGame gains: public ISpecialStyle Specials { get; } public SpecialLook SpecialLook { get; }
//   public void SetSpecialLook(SpecialLook look); public void UseSpecialStyle(SpecialLook look, ISpecialStyle style); public event Action SpecialLookChanged;
//   public int GoldenLanded { get; } public int GoldenInFlight { get; }
//   Tap(cell) returns "broken" | "insufficient" | "locked" | "golden".
// PotaSounds gains: public void PlayClip(string id, AudioClip clip, float volume = 1f, float pitch = 1f);
```

**Demo (Task 6 produces):**
```csharp
public enum SpecialScenario { Birth, Line, Bomb, Chain, Golden, Free }
public sealed class BoardPreset { public int Size = 7; public int ColorCount = 5; public int HpMin = 3, HpMax = 14; public int Ammo = 158;
    public int[] Colors; public int[] Hp; public SpecialKind[] Specials; public int[] Gates; public Features Features = Features.All; public uint Rng = 1;
    public (int color, int count)[] Targets = { (0, 22), (1, 16) }; }
// ColorHuntRules.FromPreset(BoardPreset p) -> Board   (in ColorHuntRules.Specials.cs, Task 6 adds it)
public static class SpecialScenarios { public static BoardPreset Board(SpecialScenario s); public static int[] Taps(SpecialScenario s); }   // taps: cells in order
public class SpecialsDemo : MonoBehaviour { public ColorHuntGame Game; public void Play(SpecialScenario s, bool auto); public void SetLook(SpecialLook look);
    public SpecialScenario Current { get; } public bool AutoPlaying { get; } public event Action ScenarioFinished; }
```

**Blender hand-offs (Tasks 7–8 produce, Task 9 wires into `ColorHuntSettings`):**

| id | kind | settings field |
|---|---|---|
| `vfx-fire-toon` | vfx-flipbook 4×4, 256 px, 0.45 s | `Texture2D RoyalFire` |
| `vfx-flame-jet` | vfx-flipbook 4×2, 256 px, 0.3 s, loop | `Texture2D FlameJet` |
| `vfx-dust-ring` | vfx-flipbook 4×4, 256 px, 0.4 s | `Texture2D CourtDust` |
| `vfx-speed-lines` | vfx-flipbook 4×2, 256 px, 0.25 s | `Texture2D SpeedLines` |
| `vfx-floor-crack` | vfx-flipbook 1×1, 512 px | `Texture2D FloorCrack` |
| `vfx-letter-slam` / `-swish` / `-combo` | vfx-flipbook 1×1, 512×256 | `Texture2D LetterSlam, LetterSwish, LetterCombo` |
| `vfx-ball-halves` | vfx-mesh (glb, children `HalfA`, `HalfB`) | `GameObject RoyalHalves` |
| `vfx-bomb-shell` | vfx-mesh (glb, 8 children) | `GameObject RoyalShell` |
| `vfx-coin` | vfx-mesh (glb) | `GameObject RoyalCoin` |
| `vfx-basketball` | vfx-mesh (glb) | `GameObject CourtBall` |

Task 9 also adds `Material ToonTemplate` (Core `Project/Toon`); no loop template is needed (see Contract additions, Tasks 7–9).
Puff and ring reuse A's `CandyPuff` / `CandyRing` (already imported).

---

## Contract additions from the detailed tasks

The detailed tasks pin these on top of the Shared contract (no name above was renamed). Later tasks rely on them.

### Tasks 1–2

No name in the Shared contract changes. Task 2 pins these details the contract leaves open (later tasks may rely on them):

- **`Born.Group`** = the exploded cells this birth gathers, sorted, birth cells excluded: the whole connected group for `Golden`
  and `Bomb`, the run itself for a line (when two lines claim one cell, possible only with `Bomb` off, their runs merge).
  Births of one step never share a `Group` cell. With `Features.All` this is exactly "the group's other cells".
- **`Break` outcomes:** an out-of-range cell is `Insufficient` with `Cost` 0 (Pota `yetmez`); `Cost` is 0 for a fired line/bomb
  and for a golden tap; a refused tap (`Insufficient`, `Golden`) returns the same `Board` instance and an empty `Steps`.
- **`Break` resolves chains twice** (Pota `kir` → `hakemOyna` → `zinciriCoz` once more, no referees here): the second pass adds
  steps only when the first stopped at `ChainCap`. `RotateAndSettle` resolves once (Pota `dondurVeOtur`); when nothing settles,
  `RotateResult.Board` is the same instance as `Rotated` and `Steps` is empty.
- **`TryDropToBasket`** returns `false`, `after = b`, `step = null` when no golden ball sits on an open gate.
- **`Board.Gates` and `Step.Opened` keep Pota's order** (opening order, not sorted); `Step.ToBasket` is sorted by column,
  `Step.Born` and `Step.Triggered` by cell.
- **`Block(int color, int hp)`** sets `Special = SpecialKind.None` (C# 9 struct definite assignment).
- **Test side (namespace `Project.Tests`, assembly `Pota.Tests.EditMode`):** `SpecialRulesFixtureTests.FixturePath`
  (`"_Project/Games/Pota/Tests/EditMode/Fixtures/special-rules.json"`, relative to `Application.dataPath`),
  `SpecialRulesFixtureTests.Fixture` (the parsed file, cached) and its public nested `[Serializable]` DTOs (`FixtureFile`,
  `DesignDto`, `GameDto`, `MoveDto`, `BoardDto`, `StepDto`, …) can be reused by later tests (e.g. a PlayMode replay).
- **Fixture JSON keeps Pota's words:** tap `result` is `"kirildi" | "yetmez" | "altin"` (rotations: `""`), rotation `dir` is
  `"saat" | "ters"` (taps: `""`), specials are `SpecialKind` ints.

### Tasks 3–4

Everything in the "Board look" block is produced as written. Additions and placements, for the skeleton's contract:

```csharp
// ColorHuntSettings.cs (Task 3): the enum lives here, next to PuzzleLook. Task 5's block lists it; Task 5 must NOT declare it again.
public enum SpecialLook { Pota, Royal, Court }
// ColorHuntSettings.ToDesign() now copies Features = Specials (Task 2's LevelDesign.Features), so the level's switches reach Create.
// PotaLook (Task 4):
public const float SpecialScale = 0.82f / 0.88f;                  // ozelOlcek ÷ CIKARTMA_PAYI
public const float GoldenPulseAmount = 0.08f, GoldenPulseSpeed = 5f;   // RENK_AVI_HIS.ozel.altinNabiz, altinHiz
public static uint SpecialHex(SpecialKind k);                     // OZEL_RENK_HEX
public static uint DarkSaturated(uint hex);                       // koyuDoygun, three.js arithmetic
public static float GoldenPulse(float t);                         // 0.08·(1 + sin(5t))
// BlockView (Task 4):
public float Pulse;            // flash floor applied after the decay this frame; the GAME sets it every frame (golden pulse), 0 = none
public Color Tone { get; }     // sRGB body tone: SpecialTone for a special, palette colour otherwise (use it for a special's burst tone)
// GateRings (Task 4):
public IReadOnlyList<int> Shown { get; }                          // visible columns in pool order (≤ 4)
public static Vector3 RingAt(int n, int col);                     // ring centre, game-root units
public static float BottomEdge(int n);                            // panoAltKenar + 0.05 in BOARD units (Pota's halkaY)
public const int Pool = 4; public static readonly Color Tint /*#ffb03a*/, FlashTint /*#ffe27a*/;
```

### Tasks 5–6

Everything in the "Game ↔ style seam" and "Demo" blocks is produced as written (`SpecialLook` itself is Task 3's, in
`ColorHuntSettings.cs`; Task 5 does not declare it again). Additions and clarifications:

```csharp
// ---- Task 5 ----
// SpecialTiming gains:  public float GatherTime;   // s a birth group takes into the birth cell (GathersBirthGroup); 0 = the step's ChainTime
// SpecialFire gains:    public float Pace;         // = the plan's Pace; a style multiplies its sweep durations by it
// SpecialFire.Time      = the moment the special's OWN block bursts (= its cell's Start + ChainTime·0.55); the tapped one: the step's first peak
// SpecialFire.Size      = cells of its own area that burst in this step (clipped at the edges, golden balls left out)
// TriggerPlan gains:    public float Pace = 1f;    // 1, or the factor every start/fire was scaled by so End ≤ SpecialSchedule.MaxStep
// SpecialSchedule:      public const float MaxStep = 0.6f;
// GoldenRoute gains:    public float Scale;        // grid scale s (game-root units per board cell); Pota's lifts are in cells; 0 counts as 1
// GoldenRoute.From      = the ball's cell on the board plane (z 0); .Gate = GateRings.RingAt(n, col) (the drop uses only its y, Pota's halkaY)
// GoldenPath:           public const float Lift = 0.3f; public static readonly Vector3 HoopLocal /*(0, −5.6, −2.0)*/; public static float Total { get; }
//                       At(r, t): t counted from leaving (the caller subtracts Delay); a style draws the ball (FrontZ + 0.2)·Scale in front of it
// ISpecialStyle.cs also holds: public sealed class NoSpecialStyle : ISpecialStyle   (Ready false; completes callbacks at once)
//                       public static class SpecialTexts { Line = "ÇİZGİ TOPU!", Bomb = "BOMBA!", Golden = "ALTIN TOP!", GoldenTap; Born(kind) }
// PotaSpecials : MonoBehaviour, ISpecialStyle      (child "PotaSpecials" of the game root; Idle is empty: the game drives the golden pulse, Task 4)
// ColorHuntGame gains:  public event Action<int> CellBurst;      // a block burst (swell peak / consumed special's fire / gathered arrival), its step-start cell
//                       public GiantHoop Hoop { get; }           // PuzzleStage's giant hoop under the game root, or null
//                       public const float GoldenGrace = 3f;     // a style's flight must land ≤ 3 s after Pota's would; else the game lands it (warning)
// ColorHuntFx gains:    public void Glow(Vector2 cell, Color lin, float flash, float ring, float light, float lightTime, float z)   // z = Pota board z toward the camera
// ColorHuntJuice gains: public void GoldenTap(Vector3 at);       // eksi at 0.5 + "Altın top geçişten sepete inmeli"
// PotaSounds:           IsSample learns patlama, cizgi, ozelDogdu, altinInis, swish, smac, kalabalik (ses.ts ORNEK ids → 0.97–1.03 jitter);
//                       PlayClip(…, pitch: 1) = Pota's own pitch (jitter for samples), any other pitch is used as given;
//                       public static AudioClip MakeZip(); public void Zip();   // ses.ts "zip" synth, a gate opens (rate limit 40 ms)
// SwellSet.Start(key, duration, peak, loose, atPeak, float elapsed = 0f)        // a planned swell that started between two frames
// GiantHoop gains:      public void Swing(); public bool Swinging { get; }    // pota.ts fileSalla: the net swings 0.9 s (LateUpdate)
// ---- Task 6 ----
// SpecialScenarios:     public const int Size = 7; public static string Title(SpecialScenario s);   // Doğuş, Çizgi, Bomba, Zincir, Altın, Serbest
// SpecialsDemo gains:   public SpecialScenario StartScenario = Birth; public bool AutoPlayOnStart = true; public float Beat = 0.45f;
//                       public bool Finished { get; } public string LabelText { get; } public static string LookTitle(SpecialLook look);
// ColorHuntRules.FromPreset: Hp null → HpMin + (7·i mod (HpMax − HpMin + 1)); a special cell ignores Colors/Hp; Gates copied
// Pota.Editor:          SpecialsSceneTool.ScenePath = "Assets/_Project/Games/Pota/Scenes/Specials.unity" (ai_specials_build_scene)
//                       ColorHuntSceneTool.BuildPuzzleScene(ColorHuntSettings) -> GameParts   (Build() calls it; same scene as before)
//                       SceneParts.AddToProfile(string profilePath, string scenePath, List<string> report)   (PotaFlowSceneTool uses it)
```

### Tasks 7–9

Nothing in the shared contract is renamed. Added:

**Blender (`~/projects/Blender MCP`, Tasks 7–8)**
- `bl.vfx_flipbook(recipe, game, id, frames=16, cols=4, frame_px=256, duration=0.35, seed=7, out_dir=None, frame_h=None, params=None)`:
  `frame_h` = frame height in px (default `frame_px`; the lettering is 512×256), `params` = keyword arguments for the recipe
  (`lettering`: `text`, `style`). Manifest `flipbook` block always carries `"frame_h"` (int) and `"loop"` (bool) besides the old keys;
  `stats.textures[0].size` = `[cols·frame_px, rows·frame_h]`; `source.params` = `{seed, frames, **params}`; `checks.frames_empty`
  checks every frame for loops and stills (one-shots may fade out on their last frame, as before).
- A loop recipe (`RECIPES[…]["loop"] = True`, only `flame_jet`) samples u = i/frames (u = 1 ≡ u = 0); one-shots u = i/(frames−1).
- `bl.vfx.render_frame(recipe, u, frame_px=256, frame_h=None, seed=7, params=None, path=None) -> ndarray (h, w, 4)` (headless only).
- `RECIPES[…]["post"]`: optional `post(tile, params) -> tile` applied to each rendered frame (the lettering's 2D outline).
- `bl.vfx_mesh(recipe, game, id, seed=7, out_dir=None, preview=True) -> manifest` (`kind: vfx-mesh`, `checks.mesh` =
  `{objects, tris, open_edges, passed}`); recipes `ball_halves`, `basketball`, `coin`.
- `bl.vfx_fracture(…, shape="pillow"|"sphere", thickness=0.035, skin="bomb")`; the sphere path writes `checks.fracture` =
  `{shape, pieces, volume_ratio, outer_faces, inner_faces, wall_faces, passed}` (the pillow path is unchanged).
- `bl.vfx.ball_uv(x, y, z) -> (u, v)`, `bl.vfx.coin_uv(x, z) -> (u, v)`, `bl.vfx.SWATCH_U`, `bl.vfx.SWATCH_V`, `bl.vfx.SKINS`.
- Font: `fonts/LilitaOne-Regular.ttf` (+ `LilitaOne-OFL.txt`) copied into the workshop.

**Unity (Task 9)**
- `ImportAssetTool.Verify(string manifestPath, string folder) -> Result` (measures an imported hand-off, copies nothing);
  `MeasureSheet` honours `flipbook.frame_h`. `flipbook.loop` is informational: Unity reads nothing from it.
- `Project.ColorHunt.SpecialMeshes` (Pota.Runtime, `Scripts/ColorHunt/Specials/SpecialMeshes.cs`):
  `static Color Ink` (#141b2e); `struct Part { string Name; Mesh Mesh; Vector3 Local; }`;
  `static Texture Skin(GameObject model)`; `static Material Toon(ColorHuntSettings s, GameObject model, string name, float outline = 0.008f)`
  (`RuntimeMaterial.From(s.ToonTemplate, "Project/Toon", name)` + `_BaseMap` = skin + ink outline); `static List<Part> Parts(GameObject model)` (sorted by name).
- `StudioImportTool.Ids` has 17 ids (the 5 studio ids + the 12 below); template asset `Materials/Templates/PotaToon.mat`.
- **No `FlipbookLoopTemplate`:** a loop is the same `Project/PotaFlipbook` material; the particle's texture-sheet animation cycles it:
  `StudioParticles.Sheet(ps, 4, 2); var tsa = ps.textureSheetAnimation; tsa.cycleCount = Mathf.Max(1, Mathf.RoundToInt(life / 0.3f));`

**Conventions Tasks 10–11 rely on** (Blender front −Y → Unity +z, Blender +X → Unity −X, Blender +Z → Unity +y; glTFast negates x)
- `RoyalHalves`: children `HalfA`, `HalfB`; each a closed half ball of radius 0.225 m, **nose (dome) along local +X**, flat cap in the
  local x = 0 plane facing −X, origin at the cap centre, ≤ 512 tris each. HalfA is one side of the line-ball skin, HalfB the other
  side turned so its nose also points +X: the whole ball = HalfA with rotation q and HalfB with rotation q·Euler(0, 180, 0) at the same
  position. Their import `localPosition` is (0, +0.3, 0) / (0, −0.3, 0) (only so the preview shows both): set positions yourself.
- `RoyalShell`: children `piece_00`…`piece_07`, a 0.45 m charcoal ball with orange seams, 0.035 m thick; child `localPosition` = the
  piece's centre (Parts(..).Local), as `ShatterPieces`.
- `RoyalCoin`: one mesh, 0.30 m across Unity x/y, 0.044 m thick along z (faces ±z, both embossed).
- `CourtBall`: one mesh, 0.45 m, 960 tris, Pota's classic skin (#f07d1d / seams #241611).
- Every mesh: one glTFast material whose embedded skin `SpecialMeshes.Skin` finds (512×256; the coin 256×128).
- Flipbooks (all straight alpha, sRGB, frames row-major from the top-left):
  `RoyalFire` 16 frames 4×4, 0.45 s one-shot (white pop → lobed orange ball with yellow core and red-brown line → lobes drift apart
  and burn out → 4 grey smoke lobes on the last frames), centred, ~85 % of the frame at its widest;
  `FlameJet` 8 frames 4×2, **one loop = 0.3 s**, nozzle at frame (0.5, 0.9) (u, v from the bottom-left), flame points to frame −y,
  ~0.4 of the frame wide; `CourtDust` 16 frames 4×4, 0.4 s, face-on ring (lay it in the board plane), open centre;
  `SpeedLines` 8 frames 4×2, 0.25 s, horizontal streaks trailing to frame −x behind a ball at the right edge (u ≈ 0.95);
  `FloorCrack` one 512² still (fade it by alpha over 0.6 s); `LetterSlam/Swish/Combo` 512×256 stills — a billboard needs a 2:1 size
  (`main.startSize3D = true`, size (2, 1, 1)·s) or a 2:1 quad.

### Task 10

Everything Task 10 consumes from the shared contract and the Task 3–9 additions is used as written. Added:

```csharp
// ColorHuntCamera (not the seam; an additive camera channel): "eksen boyunca sarsıntı" — a shake along one axis,
// ±amount/2 at 18 Hz, fading 2.2/s of game time (like CameraShake.Add)
public const float AxisShakeHz = 18f;
public float AxisShakeAmount { get; }
public void AxisShake(Vector2 axis, float amount);
public static Vector2 AxisShakeOffset(Vector2 axis, float amount, float t);
// ColorHuntSettings
public RoyalTuning Royal = new RoyalTuning();   // [Serializable] class RoyalTuning lives in Specials/RoyalSpecials.cs: every Royal number
// ColorHuntGame.CreateSpecialStyle gains one line (inserted after the Pota case, like Court's):
//   case SpecialLook.Royal: return AddSpecialStyle<RoyalSpecials>("RoyalSpecials");
// Specials/RoyalSpecials.cs: [DefaultExecutionOrder(100)] public class RoyalSpecials : MonoBehaviour, ISpecialStyle
public const int FlashMax = 16, StarMax = 8, TwinkleMax = 96, SparkMax = 96, PuffMax = 32, FireMax = 4, RingMax = 6;
public const int RocketMax = 6, ProxyMax = 3, ShellMax = 24, CoinMax = 24, TokenMax = 4, PerRocket = 2;
public const int PoolCapacity /* 325 */, Cap /* 342 = BurstBudget.Max − BurstBudget.Pota */;
public const float ModelBall = 0.45f, ModelCoin = 0.30f, FireFill = 0.85f, JetLoop = 0.3f, FuseU = 0.79f, FuseV = 0.90f;
public const float StickerQuad /* LabelQuad · SpecialScale */, RocketZ /* FrontZ + 0.45 */;
public const float RocketSoundTime = 0.42f, FuseSoundTime = 0.22f, CoinSoundTime = 0.35f;
public static bool AssetsReady(ColorHuntSettings s);           // hand-offs + ToonTemplate/FlipbookTemplate/AdditiveTemplate (no Shader.Find)
public static SpecialTiming TimingOf(RoyalTuning t);
public static float RocketReach(SpecialFire f, SpecialTiming t, int cells);   // f.Time + (LineLead + cells/LineSpeed)·Pace = the plan's burst
public static float RocketCells(SpecialFire f, SpecialTiming t, float age);   // its inverse, age = s since the fire
public static float EdgeCells(int n);  public static Vector2 FuseTip { get; }  public static float PopScale(float u, float peak);
public static Vector4 JetFrame(float age);  public static Mesh JetQuad();  public static void CoinStep(ref Debris d, float dt, float gravity);
public static AudioClip MakeRocket(), MakeFuse(), MakeCoin();  // PotaSounds.PlayClip ids "roket", "fitil", "para"
public int RocketsLive, ProxiesLive, PiecesLive, CoinsLive, TokensLive, GatherCount { get; }  public float ScreenFlash { get; }
```

**The seam is unchanged:** no `ISpecialStyle.Arm`, no new `BlockView` member (Court, Task 11, does the same).
- **No `BlockView.IdleScale`.** Royal drives the existing `BlockView.Scale` (the entrance multiplier, 1 in play) for the birth pop, the
  bomb's breath and a gathered block's shrink. Nothing else writes `Scale` during play: the studio intro (`StartIntro`/`AdvanceIntro`) and
  PotaFlow's reveal run before `Begin`, and Royal's `Idle` does nothing while `game.Intro`; `NewView` resets `Scale = 1`, and `Clear()`
  puts 1 back on the specials it touched that are still on the board.

**No `Arm` hook.** Task 5 defines `SpecialFire.Time` as the moment the special's *own* block bursts and puts `BombLead` *after* it: the
bomb's 3×3 bursts at `Time + (BombLead + ring·BombRing)·Pace` (`SpecialScheduleTests.RoyalBomb_BurstsInRings_AfterItsWindUp`: centre
Start 0, ring 1 Start `BombLead + BombRing`). So the 0.18 s wind-up window lies between the fire and the explosion. The game hides the
consumed view at the fire; `RoyalSpecials` draws the wind-up on a pooled copy of that very block (same pillow mesh, body material, tone
and sticker), and explodes at `Time + BombLead·Pace`. An `Arm` call at the step's start would add only the tapped bomb's 0.08 s first
peak and nothing for a chained bomb. Court (Task 11) draws its dunk the same way, on a stand-in after the fire. (Should a later task add
a member to `ISpecialStyle`, it must also add it to `RoyalSpecials` and to `RoyalPlayTests.CountingRoyal`.)

### Task 11

Nothing in the shared contract is renamed. Added (namespace `Project.ColorHunt`, assembly `Pota.Runtime`):

```csharp
// Scripts/ColorHunt/Specials/CourtSpecials.cs
[Serializable] public class CourtTuning { /* every Court number, spec defaults; see Step 3 */ }
// ColorHuntSettings: public CourtTuning Court = new CourtTuning();      (after Task 9's `public GameObject CourtBall;`)
[DefaultExecutionOrder(100)] public class CourtSpecials : MonoBehaviour, ISpecialStyle   // child "CourtSpecials" of the game root
{
    public const int BallMax = 8, GoldenMax = 4, FlameMax = 8, SpeedMax = 8, RingMax = 12, CrackMax = 4, LetterMax = 4,
        DustMax = 16, FlashMax = 24, ConfettiMax = 60;
    public const int PoolTotal /* 148 */, Cap = BurstBudget.Max - BurstBudget.Pota /* 342 */;
    public const float StandInZ, BallZ, RingZ, DecalZ, FlashZ, LetterZ, Outline;            // Pota board depths (cells toward the camera)
    public const int RingOrder = 1, CrackOrder = 4, DustOrder = 6, SpeedOrder = 10, FlameOrder = 11, SwishOrder = 12, FlashOrder = 13, LetterOrder = 30;
    public struct DunkPose { public float Up, Toward, Scale, Squash, Spin; public bool Impact; }
    public static bool AssetsReady(ColorHuntSettings s);
    public static SpecialTiming TimingFor(CourtTuning t);    // LineSpeed 20, LineLead 0.08, BombLead 0.29, BombRing 0.03, GatherTime 0.22
    public static float LineDistance(SpecialFire f, CourtTuning t, float tau);   // cells a line ball covered tau s after its fire
    public static float ReachTime(SpecialFire f, CourtTuning t, float cells);    // step time the ball reaches a cell = the plan's burst
    public static float ImpactTime(SpecialFire f, CourtTuning t);                // f.Time + BombLead·Pace
    public static float BombBurstTime(SpecialFire f, CourtTuning t, int ring);   // f.Time + (BombLead + ring·BombRing)·Pace
    public static DunkPose Dunk(float tau, float pace, CourtTuning t);
    public static float LetterScale(float age, CourtTuning t); public static float LetterAlpha(float age, CourtTuning t);
    public static float NetRing(float time, int ring, CourtTuning t); public static float NetWaveTime(CourtTuning t, int rings);
    public static Vector2 GatherPos(Vector2 from, Vector2 to, float u, CourtTuning t);
    public static float IdleHop(float time, CourtTuning t);
    public static Vector4 Frame(int f, int cols, int rows);                       // _UvRect of a sheet frame, row-major from the top-left
    public static Texture2D RingTexture(int size = 128);                          // thin white court-line ring (procedural, cached)
    public static AudioClip MakePass();                                           // synth "court-pas", 0.24 s
    public static AudioClip ShortCrowd(AudioClip crowd, float seconds, float fade); // "kalabalik-kisa"
    public CourtTuning Tuning { get; } public int BallsLive { get; } public int LettersLive { get; }
    public IEnumerable<Transform> Letters { get; } public int NetSwings { get; } public int Landings { get; }
}
// ColorHuntGame.CreateSpecialStyle gains: case SpecialLook.Court: return AddSpecialStyle<CourtSpecials>("CourtSpecials");
```

- **Not added (rulings 1–2 below):** `ISpecialStyle.Arm` and `BlockView.IdleScale`. Court needs neither; if Task 10 adds them,
  Court is unaffected (an `Arm` added to the interface would need a no-op `public void Arm(SpecialFire fire) { }` in `CourtSpecials`:
  whichever of Tasks 10/11 lands second adds it).
- **Sound ids Court plays through `PotaSounds.PlayClip`** (Task 12's log): `"smac"` (`S.SfxSlam`, dunk impact), `"swish"` (`S.SfxSwish`,
  line ends at 0.7 and golden landing at 1.0), `"kalabalik"` (runtime clip **`kalabalik-kisa`**, 1.2 s cut of `S.SfxCrowd`, 0.8),
  `"courtPas"` (runtime synth **`court-pas`**, 0.8, one per line fire), `"altinInis"` (`S.SfxGoldenLand`); births keep Pota's
  `"ozelDogdu"` (`S.SfxGoldenBorn`) / `PotaSounds.Combo(2)`.

### Task 12

```csharp
// ---- Task 12 ----
// PotaSounds.cs (Project.ColorHunt):
//   public struct SoundLogEntry { public string Id; public AudioClip Clip; public float T, Volume, Pitch; public int Voice; }
//   PotaSounds: public bool Logging { get; }  public void StartLog(System.Func<float> clock);  public List<SoundLogEntry> StopLog();
//     Every sound that passes the rate limit is logged — the one private Play behind PlayClip, Combo, Drop, Zip, Collect… — with the
//     pitch it plays at (samples' 0.97–1.03 jitter included) and its voice 0–7 (-1 when there are no voices, i.e. outside Play Mode).
// ColorHuntGame: public event Action<SpecialKind, int> SpecialBorn;   // (kind, cell) right after ISpecialStyle.Birth
//                public event Action<SpecialFire> SpecialFired;      // right after ISpecialStyle.Fire
// SpecialsDemo:  public event Action<int> Tapped;                     // after every scripted Game.Tap(cell), whatever it returned
//                public void ShowUi(bool on);  public bool UiVisible { get; }   // the recorder hides the demo's rows (the HUD stays)
// Pota.Editor (Project.Editor.AITools):
//   WavWriter.Encode(float[] interleaved, int channels, int frequency) -> byte[]      (RIFF/WAVE PCM 16-bit)
//   WavWriter.TryWrite(AudioClip clip, string path, out string error) -> bool       (imported and AudioClip.Create clips alike)
//   ColorHuntRecordTool: public struct SpecialsRequest { public bool Specials; public SpecialScenario Scenario; public SpecialLook Look; }
//     ParseRequest(string scene, string scenario, string specialLook, bool scripted, out SpecialsRequest r) -> string   (null = ok, else the refusal)
//     TryScenario(string, out SpecialScenario) (birth|line|bomb|chain|golden), TrySpecialLook(string, out SpecialLook) (pota|royal|1|court|2),
//     NextKey(ICollection<string> keys, string key) ("tap", "tap2", …), ClipKeys(IEnumerable<AudioClip>) -> Dictionary<AudioClip, string>
//     ai_colorhunt_record params: scene ("ColorHunt" default | "Specials"), scenario, special_look, until ("finish"), zoom (2.4), focus (cell)
//     closeup (both scenes) now = zoom + fine steps: Time.fixedDeltaTime held at 0.3 × its value every step (no TimeScaleController.SlowMo);
//       the frames are played back slowed by seq_video (meta.speed 0.3)
//     events.json: markers in s (recording clock): tap, tap2…, rotate, win, born…, fire…, land…, finish, end;
//                  "meta" {scene, look, closeup, speed (1 | 0.3), step (longest s between frames: 0.02, close-up 0.006), result[, scenario, special_look]};
//                  "sounds" [{id, clip, t, volume, pitch, voice}]; sounds/<clip>.wav beside the frames
//   FrameRecorder: with UI and no explicit canvas list, every Capture also moves root overlay canvases created after the start
//     (Royal's "RoyalScreenFlash" canvas, a PopupText canvas built on its first popup); Dispose puts them back too
//   SpecialsCostTool (ai_specials_cost): struct Sample {T, Cpu, Render, Gpu; Draws, SetPass, Live}, struct Summary,
//     Summarize(IList<Sample>, float tap), Combine(IList<Summary>), Median, Percentile → Temp/specials_cost.json
// Tests: RecordableClockTests — nothing under Scripts/ColorHunt reads the wall clock (realtimeSinceStartup, DateTime.Now/UtcNow,
//   Stopwatch, Environment.TickCount): the stepped recorder cannot advance it
// Tools/potalook:
//   compare.py specials OUT_PREFIX SCENARIO LABEL=DIR…   → OUT_PREFIX-SCENARIO.jpg   (SPECIAL_MOMENTS, special_moment())
//   side_video.py groups OUT.mp4 --group TITLE LABEL=DIR… [--group …] [--anchor tap] [--before 0.6]   (+ encode(), group_frames(),
//     header_image(), index_at(), window(), parse_groups(), scaled(), HEAD_H)
//   seq_video.py OUT.mp4 "TITLE|LOOK=DIR"… [--card 0.7] [--silent] [--speed S] [--crf 28] [--sync-sheet PREFIX]
//     (speed: playback speed of every segment; default each recording's meta.speed, else 1)
//   record_specials.sh [all|record|build|cost]   (LOOKS, SCENARIOS, CLOSEUPS, CRF narrow or tune a run)
// Deliverables: docs/screenshots/2026-10-01-specials-sheet-{birth,line,bomb,chain,golden}.jpg (one moment sheet per scenario),
//   2026-10-01-specials-side.mp4, 2026-10-01-specials-voiced.mp4, 2026-10-01-specials-closeup-{royal,court}-{bomb,line}.mp4
// Ledger for Task 13: "Task 12: Measured: …" (the phone-cost numbers for the README "Ölçüm" line)
```

---

### Task 1: Fixtures from Pota's own rules

**Files:**
- Create: `Tools/potalook/special_fixtures.ts` (the generator; runs only inside the scratch copy as `arac/unity-special-fixtures.ts`)
- Create: `Tools/potalook/run_fixtures.sh` (copy Pota → scratch, run the generator with the copy's `tsx`, copy the JSON out)
- Output (committed): `Assets/_Project/Games/Pota/Tests/EditMode/Fixtures/special-rules.json` and the two `.meta` files Unity
  writes for it and its folder (`Fixtures.meta`, `special-rules.json.meta`)

**Interfaces:**
- Consumes (scratch copy of Pota, read-only): `src/renkAvi.ts` exports `tahtaKur, kir, dondurVeOtur, hazirAltin, kopyala,
  ozelBlok, rastgele, OZELLIK_HEPSI, OZELLIK_YOK` and the types `OzelTur, Ozellikler, RAdim, RAlan, RTasarim, Yon`.
- Produces (Task 2 consumes): `special-rules.json`, one line per game, readable with Unity's `JsonUtility` (objects and flat
  arrays only, no array of arrays):
  ```
  { "version": 1, "pota": "<Pota HEAD> renkAvi.ts <sha256[:12]>",
    "design": { "size": 7, "colorCount": 5, "hpMin": 3, "hpMax": 14, "ammo": 158, "targets": [{ "color": 0, "remaining": 22 }, { "color": 1, "remaining": 16 }] },
    "coverage": { "bornLineV", "bornLineH", "bornBomb", "bornGolden", "triggeredLineV", "triggeredLineH", "triggeredBomb",
                  "chained", "basket", "opened", "closed", "rotateBasket", "goldenTaps", "insufficient" },      // informational
    "games": [ { "name": "off-1" | "line-11" | "line-bomb-12" | "all-<seed>", "seed": <uint>, "features": { "line", "bomb", "golden" },
                 "dealt": <true when start == tahtaKur(design + features, 158, seed)>, "start": Board,
                 "moves": [ { "kind": "tap" | "rotate", "cell": <tap cell, −1 for a rotation>, "dir": "saat" | "ters" | "",
                              "result": "kirildi" | "yetmez" | "altin" | "", "cost": <Pota maliyet>,
                              "rotated": Board (rotations only: dondurVeOtur's donmus), "after": Board, "steps": [Step] } ] } ] }
  Board = { "size", "colors": [n²], "hp": [n²], "specials": [n²] (0 none, 1 LineV = cizgiDikey, 2 LineH, 3 Bomb, 4 Golden),
            "ammo", "rng" (uint32), "gates": [columns, opening order], "golden", "targets": [{ "color", "remaining" }] }
  Step  = { "exploded": [], "drops": [{ "from", "to" }], "spawns": [{ "to", "color", "hp", "distance" }], "born": [{ "cell", "kind" }],
            "triggered": [{ "cell", "kind" }], "toBasket": [{ "cell", "col" }], "opened": [] }
  ```
- Games: 4 × features off (`off-1…4`: today's level, plain random play, 15 % rotations), `line-11` (Pota level 5's feature set:
  line only) and `line-bomb-12` (level 6: line + bomb), then all-features games from seed 100 up until every counter reaches 5:
  every fourth seed plays the dealt board, the others get 3–6 random lines/bombs and 1–2 golden balls through Pota's own
  `hazirAltin` (which opens their gates). Special play looks ahead with Pota's `kir`/`dondurVeOtur` (a rotation into the basket
  first, then special taps, golden taps, taps that give birth to a golden ball, drop one into the basket, or give any birth).
  Every choice comes from Pota's `rastgele`, so the file is byte-identical on every run of the same `renkAvi.ts`. 25 moves per game.

- [ ] **Step 1: Write the generator**

`Tools/potalook/special_fixtures.ts`:
```ts
// Fixtures for Unity's Color Hunt rules, produced by Pota's OWN rules (src/renkAvi.ts). Spec 2026-10-01 special balls §3.
// Runs only inside a SCRATCH COPY of Pota: Tools/potalook/run_fixtures.sh copies this file to <copy>/arac/ and runs it with
// the copy's tsx. Never run it inside ~/projects/Pota.
//   tsx arac/unity-special-fixtures.ts <out.json>        (env POTA_REV = Pota's commit + renkAvi.ts hash, written into the file)
// Deterministic: the same Pota code writes the same bytes (no timestamps; every choice comes from Pota's own `rastgele`).
import { writeFileSync } from "node:fs";
import {
  dondurVeOtur, hazirAltin, kir, kopyala, ozelBlok, rastgele, tahtaKur,
  OZELLIK_HEPSI, OZELLIK_YOK, type OzelTur, type Ozellikler, type RAdim, type RAlan, type RTasarim, type Yon,
} from "../src/renkAvi";

const OUT = process.argv[2];
if (!OUT) throw new Error("usage: tsx arac/unity-special-fixtures.ts <out.json>");

// Unity's level (ColorHuntSettings defaults, "bölüm 20" in the spec): 7×7, 5 colours, hp 3–14, targets (0,22) (1,16), 158 balls.
const AMMO = 158;
const TASARIM: RTasarim = { boyut: 7, renkSayisi: 5, can: { alt: 3, ust: 14 }, hedefler: [{ renk: 0, adet: 22 }, { renk: 1, adet: 16 }] };
const MOVES = 25;
const NEED = 5;           // every counted event at least this often
const FIRST_SEED = 100;   // all-features games: seeds 100, 101, … until the coverage is reached
const MAX_GAMES = 200;    // … or this many games: then the generator fails

/** Unity SpecialKind values (None 0). Results ("kirildi" | "yetmez" | "altin") and turns ("saat" | "ters") stay Pota's words. */
const KIND: Record<OzelTur, number> = { cizgiDikey: 1, cizgiYatay: 2, bomba: 3, altin: 4 };

// ---------------------------------------------------------------- JSON shapes (Unity JsonUtility: objects and flat arrays only)

interface BoardJson {
  size: number; colors: number[]; hp: number[]; specials: number[]; ammo: number; rng: number;
  gates: number[]; golden: number; targets: { color: number; remaining: number }[];
}
interface StepJson {
  exploded: number[]; drops: { from: number; to: number }[]; spawns: { to: number; color: number; hp: number; distance: number }[];
  born: { cell: number; kind: number }[]; triggered: { cell: number; kind: number }[]; toBasket: { cell: number; col: number }[];
  opened: number[];
}
interface MoveJson {
  kind: "tap" | "rotate"; cell: number; dir: Yon | ""; result: string; cost: number;
  rotated?: BoardJson; after: BoardJson; steps: StepJson[];
}
interface GameJson {
  name: string; seed: number; features: { line: boolean; bomb: boolean; golden: boolean }; dealt: boolean;
  start: BoardJson; moves: MoveJson[];
}

function boardJson(a: RAlan): BoardJson {
  for (const b of a.hucreler) if (b.engel) throw new Error("obstacle on a fixture board");
  for (const h of a.hedefler) if (h.tur !== "renk") throw new Error(`non-colour target ${h.tur}`);
  return {
    size: a.boyut,
    colors: a.hucreler.map((b) => b.renk),
    hp: a.hucreler.map((b) => b.can),
    specials: a.hucreler.map((b) => (b.ozel ? KIND[b.ozel] : 0)),
    ammo: a.cephane,
    rng: a.rng >>> 0,
    gates: a.gecisler.slice(),
    golden: a.altin,
    targets: a.hedefler.map((h) => ({ color: h.renk, remaining: h.kalan })),
  };
}

function stepJson(s: RAdim): StepJson {
  if (s.fileHasar || s.engelKalkan || s.hakem) throw new Error("obstacle step in a fixture");
  return {
    exploded: s.patlayan.slice(),
    drops: s.dususler.map((d) => ({ from: d.kaynak, to: d.hedef })),
    spawns: s.yeniler.map((y) => {
      if (y.blok.ozel || y.blok.engel) throw new Error("a spawned block is special");
      return { to: y.hedef, color: y.blok.renk, hp: y.blok.can, distance: y.mesafe };
    }),
    born: (s.dogan ?? []).map((d) => ({ cell: d.hucre, kind: KIND[d.tur] })),
    triggered: (s.tetiklenen ?? []).map((d) => ({ cell: d.hucre, kind: KIND[d.tur] })),
    toBasket: (s.sepete ?? []).map((x) => ({ cell: x.hucre, col: x.sutun })),
    opened: (s.acilan ?? []).slice(),
  };
}

// ---------------------------------------------------------------- choices (Pota's rastgele, a state of its own)

class Zar {
  private s: number;
  constructor(tohum: number) { this.s = tohum >>> 0; }
  sayi(): number { const r = rastgele(this.s); this.s = r.durum; return r.deger; }
  tam(n: number): number { return Math.floor(this.sayi() * n); }
  sec<T>(l: T[]): T { return l[this.tam(l.length)]!; }
}

type Hamle = { tur: "tap"; hucre: number } | { tur: "rotate"; yon: Yon };
const YONLER: Yon[] = ["saat", "ters"];
const hucreler = (a: RAlan, f: (b: RAlan["hucreler"][number], i: number) => boolean): number[] =>
  a.hucreler.map((b, i) => (f(b, i) ? i : -1)).filter((i) => i >= 0);
const sepeteIner = (adimlar: RAdim[]): boolean => adimlar.some((s) => (s.sepete?.length ?? 0) > 0);

/**
 * One move. Plain play (`akilli` false): 15 % a rotation, else a tap on any cell. Special play looks ahead with Pota's own
 * kir / dondurVeOtur so births, triggers, golden taps, basket drops and rotations into the basket really happen.
 */
function hamleSec(a: RAlan, z: Zar, akilli: boolean): Hamle {
  const r = z.sayi();
  if (akilli) {
    const donus = YONLER.filter((y) => sepeteIner(dondurVeOtur(a, y).adimlar));
    if (donus.length && r < 0.8) return { tur: "rotate", yon: z.sec(donus) };
    const ozel = hucreler(a, (b) => !!b.ozel && b.ozel !== "altin");
    if (ozel.length && r < 0.3) return { tur: "tap", hucre: z.sec(ozel) };
    const altin = hucreler(a, (b) => b.ozel === "altin");
    if (altin.length && r < 0.34) return { tur: "tap", hucre: z.sec(altin) };
    if (r < 0.85) {
      // affordable taps by what they lead to: a golden birth first (the rarest), then a basket drop, then any birth
      const sonuc = hucreler(a, (b) => !b.ozel && b.can <= a.cephane).map((i) => ({ i, adimlar: kir(a, i).adimlar }));
      const olur = (f: (s: RAdim) => boolean) => sonuc.filter((x) => x.adimlar.some(f)).map((x) => x.i);
      for (const iyi of [olur((s) => !!s.dogan?.some((d) => d.tur === "altin")), olur((s) => (s.sepete?.length ?? 0) > 0),
                         olur((s) => (s.dogan?.length ?? 0) > 0)])
        if (iyi.length) return { tur: "tap", hucre: z.sec(iyi) };
    }
  }
  if (r >= 0.85) return { tur: "rotate", yon: z.sec(YONLER) };
  return { tur: "tap", hucre: z.tam(a.boyut * a.boyut) };
}

function oyna(ad: string, tohum: number, o: Ozellikler, baslangic: RAlan, dealt: boolean, akilli: boolean): GameJson {
  const z = new Zar(tohum * 7919 + 17);
  const moves: MoveJson[] = [];
  let a = baslangic;
  for (let m = 0; m < MOVES; m++) {
    const h = hamleSec(a, z, akilli);
    if (h.tur === "rotate") {
      const d = dondurVeOtur(a, h.yon);
      moves.push({ kind: "rotate", cell: -1, dir: h.yon, result: "", cost: 0, rotated: boardJson(d.donmus),
                   after: boardJson(d.alan), steps: d.adimlar.map(stepJson) });
      a = d.alan;
    } else {
      const k = kir(a, h.hucre);
      moves.push({ kind: "tap", cell: h.hucre, dir: "", result: k.sonuc, cost: k.maliyet, after: boardJson(k.alan),
                   steps: k.adimlar.map(stepJson) });
      a = k.alan;
    }
  }
  return { name: ad, seed: tohum, features: { line: o.cizgi, bomb: o.bomba, golden: o.altin }, dealt, start: boardJson(baslangic), moves };
}

/** 3–6 lines and bombs on random cells, then 1–2 golden balls through Pota's own booster (`hazirAltin` opens their gates). */
function yerlestir(a: RAlan, z: Zar): RAlan {
  let b = kopyala(a);
  const adet = 3 + z.tam(4);
  for (let k = 0; k < adet; k++) {
    const tur = z.sec<OzelTur>(["cizgiDikey", "cizgiYatay", "bomba"]);
    b.hucreler[z.sec(hucreler(b, (x) => !x.ozel))] = ozelBlok(tur);
  }
  const altin = 1 + z.tam(2);
  for (let k = 0; k < altin; k++) b = hazirAltin(b);
  return b;
}

// ---------------------------------------------------------------- coverage

const sayac = {
  bornLineV: 0, bornLineH: 0, bornBomb: 0, bornGolden: 0, triggeredLineV: 0, triggeredLineH: 0, triggeredBomb: 0,
  chained: 0, basket: 0, opened: 0, closed: 0, rotateBasket: 0, goldenTaps: 0, insufficient: 0,
};
const BORN: Record<number, keyof typeof sayac> = { 1: "bornLineV", 2: "bornLineH", 3: "bornBomb", 4: "bornGolden" };
const TRIGGERED: Record<number, keyof typeof sayac> = { 1: "triggeredLineV", 2: "triggeredLineH", 3: "triggeredBomb" };

function say(g: GameJson): void {
  let gates = g.start.gates;
  for (const m of g.moves) {
    for (const s of m.steps) {
      for (const b of s.born) sayac[BORN[b.kind]!]++;
      for (const t of s.triggered) sayac[TRIGGERED[t.kind]!]++;
      if (s.triggered.length >= 2) sayac.chained++;
      sayac.basket += s.toBasket.length;
      sayac.opened += s.opened.length;
    }
    sayac.closed += gates.filter((c) => !m.after.gates.includes(c)).length;
    gates = m.after.gates;
    if (m.kind === "rotate" && m.steps.some((s) => s.toBasket.length > 0)) sayac.rotateBasket++;
    if (m.result === "altin") sayac.goldenTaps++;
    if (m.result === "yetmez") sayac.insufficient++;
  }
}
const yeter = (): boolean => Object.values(sayac).every((v) => v >= NEED);

// ---------------------------------------------------------------- games

const CIZGI: Ozellikler = { cizgi: true, bomba: false, altin: false };
const CIZGI_BOMBA: Ozellikler = { cizgi: true, bomba: true, altin: false };
const games: GameJson[] = [];
const ekle = (g: GameJson) => { games.push(g); say(g); };

// (a) features off: the level Unity ships today. Plain random play; Unity's rng use must not move.
for (const tohum of [1, 2, 3, 4]) ekle(oyna(`off-${tohum}`, tohum, OZELLIK_YOK, tahtaKur(TASARIM, AMMO, tohum), true, false));
// (b) Pota's level 5 / 6 feature sets: a closed feature skips its step.
ekle(oyna("line-11", 11, CIZGI, tahtaKur({ ...TASARIM, ozellikler: CIZGI }, AMMO, 11), true, true));
ekle(oyna("line-bomb-12", 12, CIZGI_BOMBA, tahtaKur({ ...TASARIM, ozellikler: CIZGI_BOMBA }, AMMO, 12), true, true));
// (c) all features. Every fourth seed plays the dealt board as it is; the others get pre-placed specials and golden balls.
for (let tohum = FIRST_SEED; !yeter(); tohum++) {
  if (tohum >= FIRST_SEED + MAX_GAMES) throw new Error(`coverage not reached after ${MAX_GAMES} games: ${JSON.stringify(sayac)}`);
  const dealt = tahtaKur({ ...TASARIM, ozellikler: OZELLIK_HEPSI }, AMMO, tohum);
  const yalin = tohum % 4 === 0;
  ekle(oyna(`all-${tohum}`, tohum, OZELLIK_HEPSI, yalin ? dealt : yerlestir(dealt, new Zar(tohum)), yalin, true));
}

const design = {
  size: TASARIM.boyut, colorCount: TASARIM.renkSayisi, hpMin: TASARIM.can.alt, hpMax: TASARIM.can.ust, ammo: AMMO,
  targets: TASARIM.hedefler.map((h) => ({ color: h.renk ?? 0, remaining: h.adet })),
};
const head = JSON.stringify({ version: 1, pota: process.env.POTA_REV ?? "unknown", design, coverage: sayac, games: "@" });
const text = head.replace('"@"', "[\n" + games.map((g) => JSON.stringify(g)).join(",\n") + "\n]") + "\n";
writeFileSync(OUT, text);
const moves = games.reduce((n, g) => n + g.moves.length, 0);
const steps = games.reduce((n, g) => n + g.moves.reduce((k, m) => k + m.steps.length, 0), 0);
console.log(`special-rules.json: ${games.length} games, ${moves} moves, ${steps} steps, ${Math.round(text.length / 1024)} KB`);
console.log("coverage " + Object.entries(sayac).map(([k, v]) => `${k}=${v}`).join(" "));
```

- [ ] **Step 2: Write the runner**

`Tools/potalook/run_fixtures.sh` (it rsyncs first and then copies the generator in, on every run: `run_export.sh` wipes
extra files in the same copy with `--delete`):
```zsh
#!/bin/zsh
# Fixtures for Unity's Color Hunt special-ball rules, written by Pota's OWN src/renkAvi.ts (spec 2026-10-01 §3).
# ~/projects/Pota is READ-ONLY: everything runs in <scratch>/pota-copy (the same copy run_export.sh uses).
# Usage: Tools/potalook/run_fixtures.sh <scratch dir>     then commit Games/Pota/Tests/EditMode/Fixtures/special-rules.json
set -euo pipefail
SRC="$HOME/projects/Pota"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SCRATCH="${1:?usage: run_fixtures.sh <scratch dir>}"
COPY="$SCRATCH/pota-copy"
NODE="$HOME/.nvm/versions/node/v22.23.2/bin/node"
DEST="$ROOT/Assets/_Project/Games/Pota/Tests/EditMode/Fixtures"

# Anchored excludes: a bare "dist" would also skip node_modules/*/dist, where tsx keeps its code.
rsync -a --delete --exclude /.git --exclude /dist --exclude /ios "$SRC/" "$COPY/"
cp "$ROOT/Tools/potalook/special_fixtures.ts" "$COPY/arac/unity-special-fixtures.ts"
REV="$(git -C "$SRC" rev-parse --short HEAD) renkAvi.ts $(shasum -a 256 "$COPY/src/renkAvi.ts" | cut -c1-12)"
( cd "$COPY" && POTA_REV="$REV" exec "$NODE" node_modules/.bin/tsx arac/unity-special-fixtures.ts "$SCRATCH/special-rules.json" )
mkdir -p "$DEST"
cp "$SCRATCH/special-rules.json" "$DEST/special-rules.json"
echo "-> $DEST/special-rules.json ($REV)"
```
Run: `chmod +x Tools/potalook/run_fixtures.sh`

- [ ] **Step 3: Run it**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S=/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad
Tools/potalook/run_fixtures.sh "$S"
```
Expected (Pota `70278c6`, `renkAvi.ts d4f15293b1c8`; the first copy takes ~20 s, later runs are incremental):
```
special-rules.json: 22 games, 550 moves, 798 steps, 615 KB
coverage bornLineV=60 bornLineH=34 bornBomb=22 bornGolden=10 triggeredLineV=56 triggeredLineH=39 triggeredBomb=43 chained=23 basket=14 opened=9 closed=12 rotateBasket=5 goldenTaps=33 insufficient=7
-> /Users/tunaaltin17/projects/Unity MCP/Assets/_Project/Games/Pota/Tests/EditMode/Fixtures/special-rules.json (70278c6 renkAvi.ts d4f15293b1c8)
```
If Pota's `renkAvi.ts` changed since (another session works in Pota), the numbers differ; that is fine as long as the run ends
(every counter ≥ 5). `Error: coverage not reached after 200 games` means Pota's rules changed shape: read what changed in
`renkAvi.ts` before touching the generator, and note it in the ledger. The generator's other errors (`obstacle on a fixture
board`, `non-colour target …`, `obstacle step …`, `a spawned block is special`) mean a referee, obstacle or special target
leaked in, which this design never produces: stop and read Pota's change.

- [ ] **Step 4: Check the file is deterministic and JsonUtility-shaped**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S=/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad
F="Assets/_Project/Games/Pota/Tests/EditMode/Fixtures/special-rules.json"
shasum -a 256 "$F"; Tools/potalook/run_fixtures.sh "$S" > /dev/null; shasum -a 256 "$F"
grep -c '\[\[' "$F"
head -c 160 "$F"; echo
```
Expected: the same hash twice (`3783619e4311c80fcb96f4164aecd803013e9ae0b7dd352f1063d11c2d2b4491` at Pota 70278c6), `0` (no
array of arrays), and `{"version":1,"pota":"70278c6 renkAvi.ts d4f15293b1c8","design":{"size":7,"colorCount":5,"hpMin":3,"hpMax":14,"ammo":158,…`.

- [ ] **Step 5: Let Unity import it** (Unity writes the `.meta` files; never write them by hand)

```bash
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
~/.local/bin/uv run Tools/mcp_call.py call refresh_unity '{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}'
ls Assets/_Project/Games/Pota/Tests/EditMode/Fixtures.meta Assets/_Project/Games/Pota/Tests/EditMode/Fixtures/special-rules.json.meta
```
Expected: both paths listed. (Bridge down: `Tools/unity_up.sh` first.)

- [ ] **Step 6: Commit**

```bash
git add Tools/potalook/special_fixtures.ts Tools/potalook/run_fixtures.sh \
        Assets/_Project/Games/Pota/Tests/EditMode/Fixtures.meta Assets/_Project/Games/Pota/Tests/EditMode/Fixtures
git commit -m "test: Color Hunt special-ball fixtures written by Pota's own rules (scratch copy + tsx): 22 games, births, triggers, chains, gates, basket

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Port the special rules to C#

**Files:**
- Modify: `Scripts/ColorHunt/ColorHuntRules.cs` (whole file below; the class becomes `static partial`)
- Create: `Scripts/ColorHunt/ColorHuntRules.Specials.cs`
- Test: `Tests/EditMode/SpecialRulesTests.cs`, `Tests/EditMode/SpecialRulesFixtureTests.cs`

(All paths under `Assets/_Project/Games/Pota/`.)

Callers of `ColorHuntRules.*`, `Block`, `Step`, `Board`, `BreakResult` were checked; **none needs a change in this task**
(new fields have defaults, `Broken` stays):
- `ColorHuntGame.Tap` (`r.Broken`, `r.Cost`, `r.Steps`): a golden tap comes back `Broken == false` and would play the
  "insufficient" shiver; no golden exists while `Features` is off. Task 5 adds the `"golden"` answer.
- `ColorHuntGame.Rotate` keeps `ColorHuntRules.Rotate` (Task 5 switches it to `RotateAndSettle`); `ColorHuntGame.Prepare`
  calls `Create(design ?? S.ToDesign(), …)` and `ColorHuntSettings.ToDesign` leaves `Features` at `None`: today's level.
- `ColorHuntGame.AfterSettle` (`Won`, `HasMove`): `HasMove` is now also true while a line or bomb is on the board (wanted).
- `ColorHuntHint.Best` (`Hp > Ammo`, `Break(b, i).Steps.Count`): a special has hp 0, so it counts as affordable and its free
  blast is scored; a golden tap scores 0 steps. Only matters once specials exist.
- `Studio/AmmoCounter.cs` (`Cheapest`): `int.MaxValue` only on an all-special board; `StudioCurves.LowAmmo`
  (`cheapest > 0 && ammo < 2 * cheapest`) then overflows to `false`: harmless.
- `Editor/ColorHuntSceneTool.cs`, `Flow/BoardReveal.cs`, PlayMode tests (`Break`, `Cheapest`, `Index/Row/Col`) read only
  existing members. `Editor/ColorHuntRecordTool.cs` has its own nested `Step` struct: no clash.

**Interfaces:**
- Consumes: Task 1's `special-rules.json` (shape above).
- Produces (namespace `Project.ColorHunt`, assembly `Pota.Runtime`), exactly the Shared contract's Rules block:
  ```csharp
  public enum SpecialKind { None = 0, LineV = 1, LineH = 2, Bomb = 3, Golden = 4 }
  // struct Block: public SpecialKind Special; public bool IsSpecial; public static Block MakeSpecial(SpecialKind kind);
  [Serializable] public struct Features { public bool Line, Bomb, Golden; public bool Any; public static readonly Features None, All; }
  public struct Born { public int Cell; public SpecialKind Kind; public int[] Group; }
  public struct Trigger { public int Cell; public SpecialKind Kind; }
  public struct BasketDrop { public int Cell; public int Col; }
  public enum BreakOutcome { Broken, Insufficient, Golden }
  // class Step: public readonly List<Born> Born; List<Trigger> Triggered; List<BasketDrop> ToBasket; List<int> Opened;
  // class Board: public Features Features; public List<int> Gates = new List<int>(); public int Golden;   (Clone copies Gates)
  // class LevelDesign: public Features Features;      struct BreakResult: public BreakOutcome Outcome;
  public struct RotateResult { public Board Rotated; public Board Board; public List<Step> Steps; }
  public struct BlastArea { public List<int> Cells; public List<Trigger> Triggered; }
  public static partial class ColorHuntRules {
      public const int SpecialColor = -1;
      public static List<int> SingleArea(int n, int cell, SpecialKind kind);
      public static BlastArea Blast(Board b, int cell);
      public static List<int> ResolveMatches(Board b, out List<Born> born);
      public static bool TryDropToBasket(Board b, out Board after, out Step step);
      public static Board ResolveChains(Board b, List<Step> steps, int maxSteps = ChainCap);
      public static RotateResult RotateAndSettle(Board b, Rotation dir);
      public static BreakResult Break(Board b, int cell);
      // Cheapest ignores specials (int.MaxValue when none); HasMove is true while a non-golden special is on the board.
  }
  ```
- Produces (tests, namespace `Project.Tests`): `SpecialRulesFixtureTests.FixturePath`, `SpecialRulesFixtureTests.Fixture`, the DTOs.

- [ ] **Step 1: Write the failing hand tests**

The boards and expected values are Pota's own (`arac/renk-avi-test.ts`) where Pota has the case; the rest (gate column 3 with
rng 5, the spawned colours, the rng after a landing, crossing lines with the bomb closed) were read from Pota's rules on a
scratch copy while planning.

`Tests/EditMode/SpecialRulesTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;

namespace Project.Tests
{
    /// <summary>
    /// Pota's special balls, gates and basket (src/renkAvi.ts), hand cases. Boards and expected values are Pota's own
    /// (arac/renk-avi-test.ts) where Pota has the case; the rest were read from Pota's rules on a scratch copy.
    /// </summary>
    public class SpecialRulesTests
    {
        /// <summary>Pota renk-avi-test `tahta`: a digit is a colour (hp 1); C = LineV (cizgiDikey), Y = LineH, B = bomb, A = golden.
        /// Ammo 100, no targets, 5 colours, hp 1–1, rng 1, every feature on, no gates.</summary>
        private static Board Grid(params string[] rows) => Grid(Features.All, rows);

        private static Board Grid(Features features, params string[] rows)
        {
            int n = rows.Length;
            var cells = new Block[n * n];
            for (int r = 0; r < n; r++)
            {
                Assert.AreEqual(n, rows[r].Length, $"row {r} is not {n} long");
                for (int c = 0; c < n; c++)
                {
                    char ch = rows[r][c];
                    cells[r * n + c] = ch == 'C' ? Block.MakeSpecial(SpecialKind.LineV)
                                     : ch == 'Y' ? Block.MakeSpecial(SpecialKind.LineH)
                                     : ch == 'B' ? Block.MakeSpecial(SpecialKind.Bomb)
                                     : ch == 'A' ? Block.MakeSpecial(SpecialKind.Golden)
                                     : new Block(ch - '0', 1);
                }
            }
            return new Board { Size = n, Cells = cells, Ammo = 100, Targets = new Target[0], ColorCount = 5, HpMin = 1, HpMax = 1, Rng = 1, Features = features };
        }

        private static Board With(Board b, int colorCount, uint rng, params int[] gates)
        {
            b.ColorCount = colorCount;
            b.Rng = rng;
            b.Gates = new List<int>(gates);
            return b;
        }

        private static string Births(List<Born> born)
        {
            var parts = new List<string>();
            foreach (var x in born) parts.Add($"{x.Cell}:{x.Kind}");
            return string.Join(" ", parts);
        }

        private static string Fires(List<Trigger> fired)
        {
            var parts = new List<string>();
            foreach (var x in fired) parts.Add($"{x.Cell}:{x.Kind}");
            return string.Join(" ", parts);
        }

        private static int CountSpecial(Board b, SpecialKind kind)
        {
            int n = 0;
            foreach (var c in b.Cells) if (c.Special == kind) n++;
            return n;
        }

        // ---------- data ----------

        [Test]
        public void SpecialBlock_HasColourMinusOne_HpZero()
        {
            var s = Block.MakeSpecial(SpecialKind.Bomb);
            Assert.AreEqual((ColorHuntRules.SpecialColor, 0, SpecialKind.Bomb, true), (s.Color, s.Hp, s.Special, s.IsSpecial));
            var plain = new Block(2, 7);
            Assert.AreEqual((SpecialKind.None, false), (plain.Special, plain.IsSpecial));
            Assert.IsFalse(Features.None.Any);
            Assert.IsTrue(Features.All.Line && Features.All.Bomb && Features.All.Golden);
        }

        [Test]
        public void Create_TakesTheDesignsFeatures_NoGates_AndCloneCopiesGates()
        {
            var design = new LevelDesign { Features = Features.All };
            var b = ColorHuntRules.Create(design, 158, 7);
            Assert.AreEqual(Features.All, b.Features);
            Assert.AreEqual(0, b.Gates.Count);
            Assert.AreEqual(0, b.Golden);
            Assert.AreEqual(Features.None, ColorHuntRules.Create(new LevelDesign(), 158, 7).Features, "the shipped level: off");
            b.Gates.Add(3);
            var copy = b.Clone();
            copy.Gates.Add(5);
            CollectionAssert.AreEqual(new[] { 3 }, b.Gates, "the clone's gates are its own");
            CollectionAssert.AreEqual(new[] { 3, 5 }, copy.Gates);
        }

        // ---------- runs ----------

        [Test]
        public void Specials_NeverJoinARun_AndBreakTheRunTheySitIn()
        {
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("AAA", "012", "234")).Count, "three specials side by side");
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("A12", "A34", "A56")).Count, "three specials stacked");
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("0A00", "1231", "2312", "3123")).Count, "a special splits 0 _ 0 0");
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("CYB", "012", "234")).Count, "mixed specials");
        }

        // ---------- birth (eslesmeleriCoz) ----------

        [Test]
        public void HorizontalFour_BornLineV_AtTheLeftSecondCell()
        {
            var exploded = ColorHuntRules.ResolveMatches(Grid("00001", "12342", "23453", "34564", "45670"), out var born);
            Assert.AreEqual("1:LineV", Births(born));
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, exploded, "the birth cell does not explode");
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, born[0].Group);
        }

        [Test]
        public void VerticalFour_BornLineH_AtTheBottomCell()
        {
            var exploded = ColorHuntRules.ResolveMatches(Grid("01234", "02345", "03456", "04567", "15678"), out var born);
            Assert.AreEqual("15:LineH", Births(born));
            CollectionAssert.AreEqual(new[] { 0, 5, 10 }, exploded);
        }

        [Test]
        public void FiveOrMore_BornGolden_TheWholeGroupExplodes()
        {
            ColorHuntRules.ResolveMatches(Grid("00000", "12342", "23453", "34564", "45670"), out var five);
            Assert.AreEqual("1:Golden", Births(five), "horizontal 5");
            ColorHuntRules.ResolveMatches(Grid("012340", "123450", "234560", "345670", "456710", "567120"), out var six);
            Assert.AreEqual("35:Golden", Births(six), "vertical 6: the bottom cell");
            var arm = ColorHuntRules.ResolveMatches(Grid("00000", "02345", "03456", "34567", "45678"), out var withArm);
            Assert.AreEqual("1:Golden", Births(withArm), "5 + an arm of 3: golden, no bomb");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 10 }, arm, "the arm explodes too");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 10 }, withArm[0].Group);
            var four = ColorHuntRules.ResolveMatches(Grid("00000", "02345", "03456", "04567", "45678"), out var withFour);
            Assert.AreEqual("1:Golden", Births(withFour), "5 + an arm of 4: one golden, no line");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 10, 15 }, four);
            var other = ColorHuntRules.ResolveMatches(Grid("12345", "23405", "34506", "45607", "00000"), out var otherWay);
            Assert.AreEqual("21:Golden", Births(otherWay));
            CollectionAssert.AreEqual(new[] { 8, 13, 18, 20, 22, 23, 24 }, other);
            var plus = ColorHuntRules.ResolveMatches(Grid("12034", "56078", "00000", "91023", "27045"), out var cross);
            Assert.AreEqual("11:Golden", Births(cross), "two crossing 5s: one golden, the row is scanned first");
            CollectionAssert.AreEqual(new[] { 2, 7, 10, 12, 13, 14, 17, 22 }, plus);
        }

        [Test]
        public void LAndT_BornBomb_AtTheSmallestIntersection()
        {
            List<Born> born;
            ColorHuntRules.ResolveMatches(Grid("01234", "02345", "00056", "34567", "45678"), out born);
            Assert.AreEqual("10:Bomb", Births(born), "L bottom-left");
            ColorHuntRules.ResolveMatches(Grid("00012", "02345", "03456", "34567", "45678"), out born);
            Assert.AreEqual("0:Bomb", Births(born), "L top-left");
            ColorHuntRules.ResolveMatches(Grid("21000", "23450", "34560", "34567", "45678"), out born);
            Assert.AreEqual("4:Bomb", Births(born), "L top-right");
            ColorHuntRules.ResolveMatches(Grid("12340", "23450", "34000", "34567", "45678"), out born);
            Assert.AreEqual("14:Bomb", Births(born), "L bottom-right");
            ColorHuntRules.ResolveMatches(Grid("00012", "20345", "30456", "34567", "45678"), out born);
            Assert.AreEqual("1:Bomb", Births(born), "T");
            var u = ColorHuntRules.ResolveMatches(Grid("12345", "67891", "02034", "05067", "00089"), out born);
            Assert.AreEqual("20:Bomb", Births(born), "U: one bomb at the smaller of two intersections");
            CollectionAssert.AreEqual(new[] { 10, 12, 15, 17, 21, 22 }, u);
            var shared = ColorHuntRules.ResolveMatches(Grid("10234", "20345", "30456", "00007", "45678"), out born);
            Assert.AreEqual("16:Bomb", Births(born), "two 4s whose birth cells coincide: one bomb");
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, shared);
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, born[0].Group);
        }

        [Test]
        public void ClosedFeature_SkipsItsStep()
        {
            var lineOnly = new Features { Line = true };
            var none = ColorHuntRules.ResolveMatches(Grid(Features.None, "00001", "12342", "23453", "34564", "45670"), out var born);
            Assert.AreEqual(0, born.Count, "line off: no birth");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, none);
            ColorHuntRules.ResolveMatches(Grid(lineOnly, "01234", "02345", "00056", "34567", "45678"), out born);
            Assert.AreEqual(0, born.Count, "bomb off, an L of two 3s: both explode plainly");
            var arm = ColorHuntRules.ResolveMatches(Grid(lineOnly, "00001", "02345", "03456", "34567", "45678"), out born);
            Assert.AreEqual("1:LineV", Births(born), "bomb off, an L whose arm is a 4: the 4 still makes a line");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 5, 10 }, arm);
            var five = ColorHuntRules.ResolveMatches(Grid(lineOnly, "00000", "12342", "23453", "34564", "45670"), out born);
            Assert.AreEqual(0, born.Count, "golden off: a 5 is not a 4");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, five);
            ColorHuntRules.ResolveMatches(Grid(new Features { Golden = true }, "00001", "12342", "23453", "34564", "45670"), out born);
            Assert.AreEqual(0, born.Count, "line off, golden on: a 4 makes nothing");
        }

        [Test]
        public void CrossingFours_WithTheBombClosed_BornTwoLines_WithSeparateGroups()
        {
            var lineOnly = new Features { Line = true };
            var exploded = ColorHuntRules.ResolveMatches(Grid(lineOnly, "12304", "23401", "34102", "00003", "41234"), out var born);
            Assert.AreEqual("16:LineV 18:LineH", Births(born));
            CollectionAssert.AreEqual(new[] { 3, 8, 13, 15, 17 }, exploded);
            CollectionAssert.AreEqual(new[] { 15, 17 }, born[0].Group);
            CollectionAssert.AreEqual(new[] { 3, 8, 13 }, born[1].Group);
            ColorHuntRules.ResolveMatches(Grid("12304", "23401", "34102", "00003", "41234"), out born);
            Assert.AreEqual("18:Bomb", Births(born), "bomb on: one bomb at the intersection");
            CollectionAssert.AreEqual(new[] { 3, 8, 13, 15, 16, 17 }, born[0].Group);
            var same = ColorHuntRules.ResolveMatches(Grid(lineOnly, "10234", "20345", "30456", "00007", "45678"), out born);
            Assert.AreEqual("16:LineH", Births(born), "the same birth cell from two lines: the column run (scanned last) wins");
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, same);
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, born[0].Group, "the look gathers both runs");
        }

        [Test]
        public void BornSpecial_TakesItsCell_AndTheChainGoesOn()
        {
            var b = Grid("0201", "2522", "6767", "0123");
            b.Ammo = 10;
            b.Targets = new[] { new Target(2, 9) };
            With(b, 9, 3);
            var r = ColorHuntRules.Break(b, 5);
            Assert.AreEqual(BreakOutcome.Broken, r.Outcome);
            Assert.AreEqual(2, r.Steps.Count, "the tap and the birth");
            Assert.AreEqual("5:LineV", Births(r.Steps[1].Born));
            CollectionAssert.AreEqual(new[] { 4, 6, 7 }, r.Steps[1].Exploded);
            Assert.AreEqual(3, r.Steps[1].Spawns.Count, "as many new blocks as exploded (the special keeps its cell)");
            Assert.AreEqual(SpecialKind.LineV, r.Board.Cells[5].Special);
            Assert.AreEqual(6, r.Board.Targets[0].Remaining, "the three 2s count, the special does not");
            Assert.AreEqual(9, r.Board.Ammo);
        }

        [Test]
        public void BornSpecial_FallsWhenItsColumnEmpties()
        {
            var steps = new List<Step>();
            var after = ColorHuntRules.ResolveChains(Grid("00012", "02345", "03456", "34567", "45678"), steps, 1);
            Assert.AreEqual("0:Bomb", Births(steps[0].Born), "born at its old place (cell 0)");
            Assert.AreEqual(SpecialKind.Bomb, after.Cells[10].Special, "it fell to the bottom of what was left of the column");
        }

        // ---------- firing (tekAlan, patlamaAlani, kir) ----------

        [Test]
        public void SingleArea_LinesAndBomb_ClippedAtTheEdges()
        {
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 16, 21 }, ColorHuntRules.SingleArea(5, 11, SpecialKind.LineV));
            CollectionAssert.AreEqual(new[] { 10, 11, 12, 13, 14 }, ColorHuntRules.SingleArea(5, 11, SpecialKind.LineH));
            CollectionAssert.AreEqual(new[] { 6, 7, 8, 11, 12, 13, 16, 17, 18 }, ColorHuntRules.SingleArea(5, 12, SpecialKind.Bomb));
            CollectionAssert.AreEqual(new[] { 0, 1, 5, 6 }, ColorHuntRules.SingleArea(5, 0, SpecialKind.Bomb));
            CollectionAssert.AreEqual(new[] { 18, 19, 23, 24 }, ColorHuntRules.SingleArea(5, 24, SpecialKind.Bomb));
            CollectionAssert.AreEqual(new[] { 37, 38, 39, 44, 45, 46 }, ColorHuntRules.SingleArea(7, 45, SpecialKind.Bomb), "bottom edge on 7×7");
            Assert.AreEqual(0, ColorHuntRules.SingleArea(5, 12, SpecialKind.Golden).Count);
        }

        [Test]
        public void Blast_CornerBomb_IsClipped_AndSetsOffTheLineInIt()
        {
            var a = ColorHuntRules.Blast(Grid("B1234", "1C345", "23456", "34567", "45678"), 0);
            CollectionAssert.AreEqual(new[] { 0, 1, 5, 6, 11, 16, 21 }, a.Cells, "2×2 + column 1");
            Assert.AreEqual("0:Bomb 6:LineV", Fires(a.Triggered), "both fired, in cell order");
        }

        [Test]
        public void Blast_ChainsThroughSpecials()
        {
            var a = ColorHuntRules.Blast(Grid("01234", "12345", "2C456", "3B567", "45678"), 11);
            CollectionAssert.AreEqual(new[] { 1, 6, 10, 11, 12, 15, 16, 17, 20, 21, 22 }, a.Cells);
            Assert.AreEqual("11:LineV 16:Bomb", Fires(a.Triggered));
            Assert.AreEqual(0, ColorHuntRules.Blast(Grid("01234", "12345", "2C456", "3B567", "45678"), 0).Cells.Count, "a plain block fires nothing");
        }

        [Test]
        public void Blast_LeavesGoldenOut_TheGoldenFallsInstead()
        {
            var b = With(Grid("01234", "1A345", "2C456", "34567", "45678"), 9, 6);
            CollectionAssert.AreEqual(new[] { 1, 11, 16, 21 }, ColorHuntRules.Blast(b, 11).Cells, "the golden cell (6) is not in the area");
            var r = ColorHuntRules.Break(b, 11);
            Assert.AreEqual(SpecialKind.Golden, r.Board.Cells[21].Special, "it did not explode: it fell to the bottom of its column");
            Assert.AreEqual(1, CountSpecial(r.Board, SpecialKind.Golden));
        }

        [Test]
        public void TapLine_IsFree_FiresItsColumn_CountsTargets()
        {
            var b = With(Grid("01234", "12345", "2C456", "34567", "45678"), 9, 4);
            b.Targets = new[] { new Target(2, 3) };
            var r = ColorHuntRules.Break(b, 11);
            Assert.AreEqual((BreakOutcome.Broken, true, 0, 100), (r.Outcome, r.Broken, r.Cost, r.Board.Ammo), "free");
            Assert.AreEqual(1, r.Steps.Count);
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 16, 21 }, r.Steps[0].Exploded);
            Assert.AreEqual("11:LineV", Fires(r.Steps[0].Triggered));
            Assert.AreEqual(2, r.Board.Targets[0].Remaining, "the 2 in the column (cell 6) counted");
            b.Ammo = 0;
            Assert.AreEqual(BreakOutcome.Broken, ColorHuntRules.Break(b, 11).Outcome, "fires with no ammo too");
        }

        [Test]
        public void TapLine_ChainsIntoABomb_InOneStep()
        {
            var r = ColorHuntRules.Break(With(Grid("01234", "12345", "2C456", "3B567", "45678"), 9, 4), 11);
            CollectionAssert.AreEqual(new[] { 1, 6, 10, 11, 12, 15, 16, 17, 20, 21, 22 }, r.Steps[0].Exploded);
            Assert.AreEqual("11:LineV 16:Bomb", Fires(r.Steps[0].Triggered));
            Assert.AreEqual(0, CountSpecial(r.Board, SpecialKind.LineV) + CountSpecial(r.Board, SpecialKind.Bomb));
        }

        [Test]
        public void TapGolden_ReturnsGolden_SameBoard()
        {
            var b = Grid("01234", "1A345", "2C456", "34567", "45678");
            var r = ColorHuntRules.Break(b, 6);
            Assert.AreEqual(BreakOutcome.Golden, r.Outcome);
            Assert.IsFalse(r.Broken);
            Assert.AreSame(b, r.Board);
            Assert.AreEqual(0, r.Steps.Count);
            Assert.AreEqual(0, r.Cost);
        }

        [Test]
        public void TapPlain_TooExpensive_IsInsufficient()
        {
            var b = Grid("01234", "12345", "2C456", "34567", "45678");
            b.Ammo = 0;
            var r = ColorHuntRules.Break(b, 0);
            Assert.AreEqual((BreakOutcome.Insufficient, false, 1), (r.Outcome, r.Broken, r.Cost));
            Assert.AreSame(b, r.Board);
        }

        // ---------- gates and the basket ----------

        [Test]
        public void GoldenBirth_OpensAGate_FromTheRng_BeforeTheRefill()
        {
            var b = With(Grid("00000", "12342", "23453", "34564", "45670"), 9, 5);
            var steps = new List<Step>();
            var after = ColorHuntRules.ResolveChains(b, steps);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("1:Golden", Births(steps[0].Born));
            CollectionAssert.AreEqual(new[] { 3 }, steps[0].Opened, "Pota opens column 3 with rng 5");
            CollectionAssert.AreEqual(new[] { 3 }, after.Gates);
            Assert.AreEqual(0, after.Golden, "not in the basket yet");
            var spawned = new List<string>();
            foreach (var s in steps[0].Spawns) spawned.Add($"{s.To}:{s.Block.Color}");
            Assert.AreEqual("0:6 2:5 3:5 4:4", string.Join(" ", spawned), "the gate draw comes before the refill draws");
            Assert.AreEqual(3599190434u, after.Rng);
        }

        [Test]
        public void GoldenBirth_OpensNothing_WhenGatesSuffice()
        {
            var full = new List<Step>();
            var all = ColorHuntRules.ResolveChains(With(Grid("00000", "12342", "23453", "34564", "45670"), 9, 5, 0, 1, 2, 3, 4), full);
            Assert.AreEqual(0, full[0].Opened.Count, "every column already has a gate");
            Assert.AreEqual(5, all.Gates.Count);
            var ready = new List<Step>();
            ColorHuntRules.ResolveChains(With(Grid("00000", "12342", "23453", "34564", "45670"), 9, 5, 3), ready);
            Assert.AreEqual(0, ready[0].Opened.Count, "one golden ball, one gate");
        }

        [Test]
        public void GoldenOnTheBottomRow_AboveAnOpenGate_GoesIntoTheBasket()
        {
            var b = With(Grid("01234", "12345", "23456", "34567", "A5678"), 9, 2, 0);
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(b, out var after, out var step));
            Assert.AreEqual(1, step.ToBasket.Count);
            Assert.AreEqual((20, 0), (step.ToBasket[0].Cell, step.ToBasket[0].Col));
            Assert.AreEqual(0, step.Exploded.Count, "a landing, not an explosion");
            Assert.AreEqual(1, after.Golden);
            Assert.AreEqual(0, after.Gates.Count, "its column had no other golden ball: the gate closed");
            Assert.AreEqual(3, after.Cells[20].Color, "column 0 moved down one");
            Assert.AreEqual(1, step.Spawns.Count);
            Assert.AreEqual((0, 6, 1), (step.Spawns[0].To, step.Spawns[0].Block.Color, step.Spawns[0].Distance));
            Assert.AreEqual(3663131628u, after.Rng);
            Assert.IsFalse(ColorHuntRules.TryDropToBasket(With(Grid("01234", "12345", "23456", "34567", "A5678"), 9, 2, 1), out _, out _),
                           "the gate is on another column");
            var steps = new List<Step>();
            ColorHuntRules.ResolveChains(b, steps);
            Assert.AreEqual(20, steps[0].ToBasket[0].Cell, "ResolveChains looks at the basket first");
        }

        [Test]
        public void TwoGoldenInOneColumn_TheGateStaysOpenUntilTheLast()
        {
            var b = With(Grid("01234", "12345", "23456", "A4567", "A5678"), 9, 2, 0);
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(b, out var one, out var first));
            Assert.AreEqual(20, first.ToBasket[0].Cell);
            CollectionAssert.AreEqual(new[] { 0 }, one.Gates, "a free golden ball is left in the column: still open");
            Assert.AreEqual(0, first.Opened.Count);
            Assert.AreEqual(SpecialKind.Golden, one.Cells[20].Special, "the upper one slid down");
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(one, out var two, out _));
            Assert.AreEqual(2, two.Golden);
            Assert.AreEqual(0, two.Gates.Count, "the last one closed it");
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(With(Grid("01234", "12345", "23456", "34567", "A5A78"), 9, 2, 0, 2), out var both, out var pair));
            Assert.AreEqual(2, pair.ToBasket.Count, "two gates, two landings in one step");
            Assert.AreEqual((20, 0, 22, 2), (pair.ToBasket[0].Cell, pair.ToBasket[0].Col, pair.ToBasket[1].Cell, pair.ToBasket[1].Col));
            Assert.AreEqual(2, both.Golden);
        }

        [Test]
        public void LineUnderAGolden_TheGoldenFallsThroughItsGate_InTheSameMove()
        {
            var r = ColorHuntRules.Break(With(Grid("01234", "1A345", "2C456", "34567", "45678"), 9, 6, 1), 11);
            Assert.AreEqual(2, r.Steps.Count, "the line, then the landing");
            Assert.AreEqual(0, r.Steps[0].ToBasket.Count);
            Assert.AreEqual((21, 1), (r.Steps[1].ToBasket[0].Cell, r.Steps[1].ToBasket[0].Col));
            Assert.AreEqual(1, r.Board.Golden);
            Assert.AreEqual(0, r.Board.Gates.Count);
            Assert.AreEqual(0, CountSpecial(r.Board, SpecialKind.Golden), "it left the board");
        }

        [Test]
        public void RotateAndSettle_DropsGoldenOnGate()
        {
            var b = With(Grid("A1234", "12345", "23456", "34567", "45678"), 9, 8, 0);
            var ccw = ColorHuntRules.RotateAndSettle(b, Rotation.Counter);
            Assert.AreEqual(SpecialKind.Golden, ccw.Rotated.Cells[20].Special, "the turned (unsettled) board has it bottom-left");
            Assert.AreEqual(1, ccw.Steps.Count);
            Assert.AreEqual((20, 0), (ccw.Steps[0].ToBasket[0].Cell, ccw.Steps[0].ToBasket[0].Col));
            Assert.AreEqual(1, ccw.Board.Golden);
            Assert.AreEqual(0, ccw.Board.Gates.Count);
            Assert.AreEqual(0, CountSpecial(ccw.Board, SpecialKind.Golden));
            var cw = ColorHuntRules.RotateAndSettle(b, Rotation.Clockwise);
            Assert.AreEqual(0, cw.Steps.Count, "clockwise takes it top-right: nothing lands");
            Assert.AreSame(cw.Rotated, cw.Board);
            Assert.AreEqual(0, cw.Board.Golden);
            b.Golden = 2;
            var turned = ColorHuntRules.Rotate(b, Rotation.Clockwise);
            CollectionAssert.AreEqual(new[] { 0 }, turned.Gates, "gates sit on the frame: a turn keeps them");
            Assert.AreEqual(2, turned.Golden);
        }

        // ---------- moves ----------

        [Test]
        public void HasMove_TrueWithOnlyASpecial()
        {
            var b = Grid("01234", "12345", "2C456", "34567", "45678");
            for (int i = 0; i < b.Cells.Length; i++) if (!b.Cells[i].IsSpecial) b.Cells[i].Hp = 9;
            b.Ammo = 1;
            Assert.AreEqual(9, ColorHuntRules.Cheapest(b), "the line's hp 0 does not count");
            Assert.IsTrue(ColorHuntRules.HasMove(b), "the line can still be fired");
            b.Ammo = 0;
            Assert.IsTrue(ColorHuntRules.HasMove(b), "with no ammo at all");
            var golden = Grid("A1234", "12345", "23456", "34567", "45678");
            for (int i = 0; i < golden.Cells.Length; i++) if (!golden.Cells[i].IsSpecial) golden.Cells[i].Hp = 9;
            golden.Ammo = 1;
            Assert.IsFalse(ColorHuntRules.HasMove(golden), "a golden ball is not a move");
        }

        [Test]
        public void Cheapest_IgnoresSpecials()
        {
            Assert.AreEqual(int.MaxValue, ColorHuntRules.Cheapest(Grid("CCC", "CCC", "CCC")), "only specials: no cheapest");
            var b = Grid("B12", "345", "67A");
            b.Cells[1].Hp = 4;
            for (int i = 2; i < 8; i++) b.Cells[i].Hp = 6;
            Assert.AreEqual(4, ColorHuntRules.Cheapest(b));
        }
    }
}
```

- [ ] **Step 2: Write the fixture replay**

`Tests/EditMode/SpecialRulesFixtureTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>
    /// Replays games written by Pota's OWN rules (Tools/potalook/run_fixtures.sh runs special_fixtures.ts on a scratch copy of
    /// Pota's src/renkAvi.ts): after every move Unity's board and every step must equal Pota's, field by field. Features-off games
    /// pin today's level (rng use unchanged); the others pin births, triggers, chains, gates, the basket and rotate-and-settle.
    /// Born is compared on Cell and Kind (Group is Unity's own, for the look).
    /// </summary>
    public class SpecialRulesFixtureTests
    {
        public const string FixturePath = "_Project/Games/Pota/Tests/EditMode/Fixtures/special-rules.json";

        // ---------- the file (JsonUtility: field names as written by special_fixtures.ts) ----------

        [Serializable] public class FixtureFile { public int version; public string pota; public DesignDto design; public GameDto[] games; }
        [Serializable] public class DesignDto { public int size, colorCount, hpMin, hpMax, ammo; public TargetDto[] targets; }
        [Serializable] public class TargetDto { public int color, remaining; }
        [Serializable] public class FeaturesDto { public bool line, bomb, golden; }
        [Serializable] public class GameDto { public string name; public long seed; public FeaturesDto features; public bool dealt; public BoardDto start; public MoveDto[] moves; }
        [Serializable] public class BoardDto { public int size; public int[] colors, hp, specials; public int ammo; public long rng; public int[] gates; public int golden; public TargetDto[] targets; }
        [Serializable] public class MoveDto { public string kind; public int cell; public string dir; public string result; public int cost; public BoardDto rotated, after; public StepDto[] steps; }
        [Serializable] public class StepDto { public int[] exploded; public DropDto[] drops; public SpawnDto[] spawns; public CellKindDto[] born, triggered; public BasketDto[] toBasket; public int[] opened; }
        [Serializable] public class DropDto { public int from, to; }
        [Serializable] public class SpawnDto { public int to, color, hp, distance; }
        [Serializable] public class CellKindDto { public int cell, kind; }
        [Serializable] public class BasketDto { public int cell, col; }

        private static FixtureFile _fixture;

        public static FixtureFile Fixture
        {
            get
            {
                if (_fixture != null) return _fixture;
                string path = Path.Combine(Application.dataPath, FixturePath);
                if (!File.Exists(path)) throw new FileNotFoundException($"{path} is missing: run Tools/potalook/run_fixtures.sh <scratch>");
                return _fixture = JsonUtility.FromJson<FixtureFile>(File.ReadAllText(path));
            }
        }

        public static IEnumerable<TestCaseData> Games()
        {
            foreach (var g in Fixture.games) yield return new TestCaseData(g.name).SetName("Replay_" + g.name);
        }

        // ---------- tests ----------

        [TestCaseSource(nameof(Games))]
        public void Replay(string name)
        {
            var game = Array.Find(Fixture.games, g => g.name == name);
            var features = new Features { Line = game.features.line, Bomb = game.features.bomb, Golden = game.features.golden };
            var board = ToBoard(game.start, features);
            if (game.dealt) Same(game.start, ColorHuntRules.Create(Design(features), Fixture.design.ammo, (uint)game.seed), $"{name} start (Create, seed {game.seed})");
            for (int m = 0; m < game.moves.Length; m++)
            {
                var mv = game.moves[m];
                string at = $"{name} move {m} ({mv.kind} {(mv.kind == "tap" ? mv.cell.ToString() : mv.dir)})";
                List<Step> steps;
                if (mv.kind == "rotate")
                {
                    var r = ColorHuntRules.RotateAndSettle(board, mv.dir == "saat" ? Rotation.Clockwise : Rotation.Counter);
                    Same(mv.rotated, r.Rotated, at + " turned board");
                    board = r.Board;
                    steps = r.Steps;
                }
                else
                {
                    var r = ColorHuntRules.Break(board, mv.cell);
                    Assert.AreEqual(mv.result, Outcome(r.Outcome), at + ": outcome");
                    Assert.AreEqual(r.Outcome == BreakOutcome.Broken, r.Broken, at + ": Broken matches Outcome");
                    Assert.AreEqual(mv.cost, r.Cost, at + ": cost");
                    board = r.Board;
                    steps = r.Steps;
                }
                int common = Math.Min(mv.steps.Length, steps.Count);
                for (int s = 0; s < common; s++)
                {
                    string d = StepDiff(mv.steps[s], steps[s]);
                    if (d != null) Assert.Fail($"{at} step {s}: {d}");
                }
                Assert.AreEqual(mv.steps.Length, steps.Count, at + ": step count");
                Same(mv.after, board, at + " board after");
            }
        }

        [Test]
        public void Fixture_CoversEverySpecialPath()
        {
            var count = new Dictionary<string, int>();
            void Add(string key, int n = 1) => count[key] = (count.TryGetValue(key, out int old) ? old : 0) + n;
            foreach (var g in Fixture.games)
            {
                if (!g.features.line && !g.features.bomb && !g.features.golden) Add("off games");
                foreach (var m in g.moves)
                {
                    Add(m.kind == "tap" ? m.result : "rotate");
                    bool landed = false;
                    foreach (var s in m.steps)
                    {
                        foreach (var b in s.born) Add("born " + (SpecialKind)b.kind);
                        foreach (var t in s.triggered) Add("triggered " + (SpecialKind)t.kind);
                        if (s.triggered.Length >= 2) Add("chained");
                        Add("basket", s.toBasket.Length);
                        Add("opened", s.opened.Length);
                        landed |= s.toBasket.Length > 0;
                    }
                    if (m.kind == "rotate" && landed) Add("rotation into the basket");
                }
            }
            Assert.GreaterOrEqual(Get(count, "off games"), 4, "features-off games (today's level)");
            foreach (var key in new[] { "born LineV", "born LineH", "born Bomb", "born Golden", "triggered LineV", "triggered LineH",
                                        "triggered Bomb", "chained", "basket", "opened", "rotation into the basket", "altin",
                                        "yetmez", "kirildi", "rotate" })
                Assert.GreaterOrEqual(Get(count, key), 5, key);
        }

        // ---------- helpers ----------

        private static int Get(Dictionary<string, int> count, string key) => count.TryGetValue(key, out int n) ? n : 0;

        /// <summary>Pota KirmaSonucu.</summary>
        private static string Outcome(BreakOutcome o) => o == BreakOutcome.Broken ? "kirildi" : o == BreakOutcome.Golden ? "altin" : "yetmez";

        private static LevelDesign Design(Features features)
        {
            var d = Fixture.design;
            var targets = new (int color, int count)[d.targets.Length];
            for (int i = 0; i < targets.Length; i++) targets[i] = (d.targets[i].color, d.targets[i].remaining);
            return new LevelDesign { Size = d.size, ColorCount = d.colorCount, HpMin = d.hpMin, HpMax = d.hpMax, Targets = targets, Features = features };
        }

        private static Board ToBoard(BoardDto w, Features features)
        {
            var cells = new Block[w.colors.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = new Block(w.colors[i], w.hp[i]) { Special = (SpecialKind)w.specials[i] };
            var targets = new Target[w.targets.Length];
            for (int i = 0; i < targets.Length; i++) targets[i] = new Target(w.targets[i].color, w.targets[i].remaining);
            var d = Fixture.design;
            return new Board
            {
                Size = w.size, Cells = cells, Ammo = w.ammo, Targets = targets, ColorCount = d.colorCount, HpMin = d.hpMin, HpMax = d.hpMax,
                Rng = (uint)w.rng, Features = features, Gates = new List<int>(w.gates), Golden = w.golden,
            };
        }

        private static void Same(BoardDto want, Board got, string at)
        {
            string d = BoardDiff(want, got);
            if (d != null) Assert.Fail($"{at}: {d}");
        }

        /// <summary>The first field where Unity's board differs from Pota's, null when equal.</summary>
        private static string BoardDiff(BoardDto w, Board g)
        {
            if (w.size != g.Size || w.colors.Length != g.Cells.Length) return $"size: Pota {w.size}, Unity {g.Size}";
            for (int i = 0; i < g.Cells.Length; i++)
            {
                var c = g.Cells[i];
                if (w.specials[i] != (int)c.Special) return $"cell {i} special: Pota {(SpecialKind)w.specials[i]}, Unity {c.Special}";
                if (w.colors[i] != c.Color) return $"cell {i} colour: Pota {w.colors[i]}, Unity {c.Color}";
                if (w.hp[i] != c.Hp) return $"cell {i} hp: Pota {w.hp[i]}, Unity {c.Hp}";
            }
            if (w.ammo != g.Ammo) return $"ammo: Pota {w.ammo}, Unity {g.Ammo}";
            if ((uint)w.rng != g.Rng) return $"rng: Pota {(uint)w.rng}, Unity {g.Rng}";
            if (w.golden != g.Golden) return $"golden in the basket: Pota {w.golden}, Unity {g.Golden}";
            string gates = string.Join(",", w.gates), mine = string.Join(",", g.Gates);
            if (gates != mine) return $"gates: Pota [{gates}], Unity [{mine}]";
            if (w.targets.Length != g.Targets.Length) return $"targets: Pota {w.targets.Length}, Unity {g.Targets.Length}";
            for (int t = 0; t < g.Targets.Length; t++)
                if (w.targets[t].color != g.Targets[t].Color || w.targets[t].remaining != g.Targets[t].Remaining)
                    return $"target {t}: Pota {w.targets[t].color}×{w.targets[t].remaining}, Unity {g.Targets[t].Color}×{g.Targets[t].Remaining}";
            return null;
        }

        /// <summary>The first list where Unity's step differs from Pota's, null when equal.</summary>
        private static string StepDiff(StepDto w, Step g)
        {
            return Diff("exploded", Join(w.exploded, x => x.ToString()), Join(g.Exploded, x => x.ToString()))
                ?? Diff("drops", Join(w.drops, x => $"{x.from}>{x.to}"), Join(g.Drops, x => $"{x.From}>{x.To}"))
                ?? Diff("spawns", Join(w.spawns, x => $"{x.to}:{x.color}/{x.hp}/{x.distance}"),
                        Join(g.Spawns, x => $"{x.To}:{x.Block.Color}/{x.Block.Hp}/{x.Distance}"))
                ?? Diff("born", Join(w.born, x => $"{x.cell}:{(SpecialKind)x.kind}"), Join(g.Born, x => $"{x.Cell}:{x.Kind}"))
                ?? Diff("triggered", Join(w.triggered, x => $"{x.cell}:{(SpecialKind)x.kind}"), Join(g.Triggered, x => $"{x.Cell}:{x.Kind}"))
                ?? Diff("toBasket", Join(w.toBasket, x => $"{x.cell}@{x.col}"), Join(g.ToBasket, x => $"{x.Cell}@{x.Col}"))
                ?? Diff("opened", Join(w.opened, x => x.ToString()), Join(g.Opened, x => x.ToString()));
        }

        private static string Diff(string field, string pota, string unity) => pota == unity ? null : $"{field}: Pota [{pota}], Unity [{unity}]";

        private static string Join<T>(IEnumerable<T> items, Func<T, string> f)
        {
            var parts = new List<string>();
            if (items != null) foreach (var x in items) parts.Add(f(x));
            return string.Join(" ", parts);
        }
    }
}
```

- [ ] **Step 3: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialRulesTests Project.Tests.SpecialRulesFixtureTests`
Expected: exit 2, `[unity_tests] compile errors:` with lines such as `error CS0246: The type or namespace name 'Features' could
not be found` (also `'SpecialKind'`, `'Born'`, `'Trigger'`, `'BreakOutcome'`).

- [ ] **Step 4: Extend `ColorHuntRules.cs`**

Replace the whole of `Scripts/ColorHunt/ColorHuntRules.cs` with the listing below. Against today's file: `Block` gains
`Special`/`IsSpecial`/`MakeSpecial`; `Step` gains `Born`, `Triggered`, `ToBasket`, `Opened`; `LevelDesign` gains `Features`;
`Board` gains `Features`, `Gates`, `Golden` (copied by `Clone`); `BreakResult` gains `Outcome`; the class is `static partial`;
`FindMatches` skips colour −1; `Create` sets `Features` from the design, no gates; `ExplodeAndFill` is the full
`patlatVeDoldur` port without obstacles (born specials, basket drops, gate close, gate open from the rng **before** the
refill, golden count; helpers `CountTarget`, `FreeGoldenIn`); `ResolveChains` drops into the basket first and resolves runs
through `ResolveMatches`; `Break` fires a line/bomb for free, answers `Golden` for a golden ball and resolves twice;
`Cheapest`/`HasMove` follow `enUcuz`/`hamleVar`. `Next`, `NextInt`, `Index/Row/Col`, `MakesTriple`, `Rotate`'s body, `Won` and
`RemainingTarget` are unchanged (with features off the rng use is exactly today's: a 300-seed × 120-move comparison of the old
and new file agreed board for board while planning).

```csharp
using System;
using System.Collections.Generic;

namespace Project.ColorHunt
{
    /// <summary>
    /// One block on the board: palette index and hit points (balls needed to break it). A special block (Pota ozelBlok) has
    /// colour <see cref="ColorHuntRules.SpecialColor"/> and hp 0: it never joins a run and counts for no colour target.
    /// </summary>
    [Serializable]
    public struct Block
    {
        public int Color;
        public int Hp;
        public SpecialKind Special;
        public Block(int color, int hp) { Color = color; Hp = hp; Special = SpecialKind.None; }
        public bool IsSpecial => Special != SpecialKind.None;
        public static Block MakeSpecial(SpecialKind kind) => new Block(ColorHuntRules.SpecialColor, 0) { Special = kind };
    }

    [Serializable]
    public struct Target
    {
        public int Color;
        public int Remaining;
        public Target(int color, int remaining) { Color = color; Remaining = remaining; }
    }

    public struct Drop { public int From, To; }

    public struct Spawn
    {
        public int To;
        public Block Block;
        /// <summary>Cells the new block falls from above the frame.</summary>
        public int Distance;
    }

    /// <summary>
    /// One animation frame: <see cref="Exploded"/> go first (with <see cref="Triggered"/> specials firing and golden balls going
    /// <see cref="ToBasket"/>), <see cref="Born"/> specials take their cells, <see cref="Opened"/> gates open, then
    /// <see cref="Drops"/> and <see cref="Spawns"/> fall.
    /// </summary>
    public class Step
    {
        public readonly List<int> Exploded = new List<int>();
        public readonly List<Drop> Drops = new List<Drop>();
        public readonly List<Spawn> Spawns = new List<Spawn>();
        /// <summary>Specials born this step (Pota dogan), sorted by cell; the birth cell does not explode.</summary>
        public readonly List<Born> Born = new List<Born>();
        /// <summary>Specials fired by this step (Pota tetiklenen), sorted by cell: only the first step of a special tap has them.</summary>
        public readonly List<Trigger> Triggered = new List<Trigger>();
        /// <summary>Golden balls leaving the bottom row through their column's gate (Pota sepete), sorted by column.</summary>
        public readonly List<BasketDrop> ToBasket = new List<BasketDrop>();
        /// <summary>Gate columns opened this step (Pota acilan), in opening order.</summary>
        public readonly List<int> Opened = new List<int>();
    }

    public class Match
    {
        public int Color;
        public bool Horizontal;
        public readonly List<int> Cells = new List<int>();
    }

    public enum Rotation { Clockwise, Counter }

    /// <summary>Level design: what <see cref="ColorHuntRules.Create"/> takes.</summary>
    public class LevelDesign
    {
        public int Size = 7;
        public int ColorCount = 5;
        public int HpMin = 3;
        public int HpMax = 14;
        public (int color, int count)[] Targets = { (0, 22), (1, 16) };
        /// <summary>Which specials are born (Pota ozellikler); none by default, the level the game ships today.</summary>
        public Features Features;
    }

    /// <summary>
    /// Board state. Plain data: cloned, never mutated by the rules. Rows are top to bottom (row 0 is the top).
    /// Randomness is explicit state (<see cref="Rng"/>): the same seed replays the same game.
    /// </summary>
    public class Board
    {
        public int Size;
        /// <summary>Size × Size, always full.</summary>
        public Block[] Cells;
        public int Ammo;
        public Target[] Targets;
        public int ColorCount;
        public int HpMin, HpMax;
        public uint Rng;
        public Features Features;
        /// <summary>Columns whose gate is open (Pota gecisler), in opening order. Gates sit on the frame: they never rotate.</summary>
        public List<int> Gates = new List<int>();
        /// <summary>Golden balls that went into the basket (Pota altin).</summary>
        public int Golden;

        public Board Clone()
        {
            return new Board
            {
                Size = Size,
                Cells = (Block[])Cells.Clone(),
                Ammo = Ammo,
                Targets = (Target[])Targets.Clone(),
                ColorCount = ColorCount,
                HpMin = HpMin,
                HpMax = HpMax,
                Rng = Rng,
                Features = Features,
                Gates = Gates != null ? new List<int>(Gates) : new List<int>(),
                Golden = Golden,
            };
        }
    }

    public struct BreakResult
    {
        /// <summary>Outcome == Broken.</summary>
        public bool Broken;
        public BreakOutcome Outcome;
        public Board Board;
        public List<Step> Steps;
        public int Cost;
    }

    /// <summary>
    /// Pure colour-hunt rules, a port of Pota's src/renkAvi.ts: the board never empties, a tap costs the
    /// block's hp in balls, columns collapse, random blocks enter from the top, 3+ runs explode for free,
    /// rotation is free and never shifts anything. Specials, gates and the basket: ColorHuntRules.Specials.cs.
    /// </summary>
    public static partial class ColorHuntRules
    {
        public const int MatchLength = 3;
        public const int ChainCap = 64;

        // ---------- random (mulberry32, same sequence as Pota's `tohumlu`) ----------

        public static uint Next(uint state, out double value)
        {
            unchecked
            {
                uint a = state + 0x6d2b79f5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                value = (t ^ (t >> 14)) / 4294967296.0;
                return a;
            }
        }

        /// <summary>Pota `tamsayi`: an int in [min, max].</summary>
        public static uint NextInt(uint state, int min, int max, out int value)
        {
            state = Next(state, out double v);
            value = min + (int)Math.Floor(v * (max - min + 1));
            return state;
        }

        // ---------- helpers ----------

        public static int Index(int n, int row, int col) => row * n + col;
        public static int Row(int n, int i) => i / n;
        public static int Col(int n, int i) => i % n;

        // ---------- matches ----------

        /// <summary>
        /// Horizontal and vertical runs of 3+ (no diagonals). Raw runs: an L/T cell can be in two runs. A special block (colour −1)
        /// never joins a run and breaks the run it sits in (Pota eslesmeleriBul).
        /// </summary>
        public static List<Match> FindMatches(Board b)
        {
            int n = b.Size;
            var list = new List<Match>();
            void Run(Func<int, int> cell, bool horizontal)
            {
                int start = 0;
                for (int k = 1; k <= n; k++)
                {
                    int color = b.Cells[cell(start)].Color;
                    bool same = k < n && color >= 0 && b.Cells[cell(k)].Color == color;
                    if (same) continue;
                    if (k - start >= MatchLength && color >= 0)
                    {
                        var m = new Match { Color = color, Horizontal = horizontal };
                        for (int j = start; j < k; j++) m.Cells.Add(cell(j));
                        list.Add(m);
                    }
                    start = k;
                }
            }
            for (int r = 0; r < n; r++) { int row = r; Run(c => Index(n, row, c), true); }
            for (int c = 0; c < n; c++) { int col = c; Run(r => Index(n, r, col), false); }
            return list;
        }

        public static bool HasTriple(Board b) => FindMatches(b).Count > 0;

        // ---------- setup ----------

        private static bool MakesTriple(List<Block> cells, int n, int r, int c, int color)
        {
            if (c >= 2 && cells[Index(n, r, c - 1)].Color == color && cells[Index(n, r, c - 2)].Color == color) return true;
            if (r >= 2 && cells[Index(n, r - 1, c)].Color == color && cells[Index(n, r - 2, c)].Color == color) return true;
            return false;
        }

        /// <summary>Fills the board without triples (pairs allowed); with 3+ colours there is always a way out. No gates yet.</summary>
        public static Board Create(LevelDesign d, int ammo, uint seed)
        {
            int n = d.Size;
            var cells = new List<Block>(n * n);
            uint rng = seed;
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    int color = 0;
                    for (int attempt = 0; attempt < 32; attempt++)
                    {
                        rng = NextInt(rng, 0, d.ColorCount - 1, out color);
                        if (!MakesTriple(cells, n, r, c, color)) break;
                    }
                    rng = NextInt(rng, d.HpMin, d.HpMax, out int hp);
                    cells.Add(new Block(color, hp));
                }
            }
            var targets = new Target[d.Targets.Length];
            for (int i = 0; i < targets.Length; i++) targets[i] = new Target(d.Targets[i].color, d.Targets[i].count);
            return new Board
            {
                Size = n, Cells = cells.ToArray(), Ammo = ammo, Targets = targets,
                ColorCount = d.ColorCount, HpMin = d.HpMin, HpMax = d.HpMax, Rng = rng,
                Features = d.Features, Gates = new List<int>(), Golden = 0,
            };
        }

        // ---------- rotation ----------

        /// <summary>
        /// 90° rotation: new[c·n + (n−1−r)] = old[r·n + c] (clockwise). The board is full, so nothing shifts. Gates and the
        /// golden count stay (they belong to the frame). <see cref="RotateAndSettle"/> also lets a golden ball into the basket.
        /// </summary>
        public static Board Rotate(Board b, Rotation dir)
        {
            int n = b.Size;
            var result = b.Clone();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    var block = b.Cells[Index(n, r, c)];
                    if (dir == Rotation.Clockwise) result.Cells[Index(n, c, n - 1 - r)] = block;
                    else result.Cells[Index(n, n - 1 - c, r)] = block;
                }
            return result;
        }

        // ---------- break and chain ----------

        /// <summary>
        /// Pota patlatVeDoldur without obstacles. Empties <paramref name="exploded"/> (counting colour targets) and the golden balls
        /// going <paramref name="toBasket"/>, puts the <paramref name="born"/> specials in their cells, closes the gate of a basket
        /// column that has no free golden ball left, opens one random gate-less column per newly born golden ball while free
        /// golden balls outnumber the open gates (these rng draws come BEFORE the refill), collapses the columns and refills them
        /// from the top. <paramref name="b"/> is never changed. Triggered specials and basket drops count for no target here
        /// (Pota's bomb/line/golden targets are U25).
        /// </summary>
        private static Board ExplodeAndFill(Board b, List<int> exploded, List<Born> born, List<BasketDrop> toBasket,
                                            List<Trigger> triggered, out Step step)
        {
            int n = b.Size;
            var result = b.Clone();
            step = new Step();
            step.Exploded.AddRange(exploded);
            var temp = new Block?[n * n];
            for (int i = 0; i < temp.Length; i++) temp[i] = b.Cells[i];
            foreach (int i in exploded)
            {
                if (temp[i] == null) continue;
                CountTarget(result, temp[i].Value.Color);
                temp[i] = null;
            }
            if (triggered != null) step.Triggered.AddRange(triggered);
            if (toBasket != null)
                foreach (var d in toBasket) { temp[d.Cell] = null; step.ToBasket.Add(d); }
            if (born != null)
                foreach (var d in born) { temp[d.Cell] = Block.MakeSpecial(d.Kind); step.Born.Add(d); }
            uint rng = b.Rng;
            var gates = new List<int>();
            foreach (int c in b.Gates)
            {
                bool closes = false;
                if (toBasket != null)
                    foreach (var d in toBasket)
                        if (d.Col == c && !FreeGoldenIn(temp, n, c)) { closes = true; break; }
                if (!closes) gates.Add(c);
            }
            if (born != null)
            {
                int golden = 0;
                foreach (var t in temp) if (t != null && t.Value.Special == SpecialKind.Golden) golden++;
                foreach (var d in born)
                {
                    if (d.Kind != SpecialKind.Golden || golden <= gates.Count) continue;
                    var free = new List<int>();
                    for (int c = 0; c < n; c++) if (!gates.Contains(c)) free.Add(c);
                    if (free.Count == 0) continue;
                    rng = NextInt(rng, 0, free.Count - 1, out int pick);
                    gates.Add(free[pick]);
                    step.Opened.Add(free[pick]);
                }
            }
            for (int c = 0; c < n; c++)
            {
                // Scan the column bottom-up: every block falls to the lowest free cell.
                int dest = n - 1;
                for (int r = n - 1; r >= 0; r--)
                {
                    var block = temp[Index(n, r, c)];
                    if (block == null) continue;
                    int from = Index(n, r, c), to = Index(n, dest, c);
                    result.Cells[to] = block.Value;
                    if (to != from) step.Drops.Add(new Drop { From = from, To = to });
                    dest--;
                }
                // Remaining top cells (0..dest) are filled with new blocks falling `dest + 1` cells from above the frame.
                int empty = dest + 1;
                for (int r = dest; r >= 0; r--)
                {
                    rng = NextInt(rng, 0, b.ColorCount - 1, out int color);
                    rng = NextInt(rng, b.HpMin, b.HpMax, out int hp);
                    var block = new Block(color, hp);
                    int to = Index(n, r, c);
                    result.Cells[to] = block;
                    step.Spawns.Add(new Spawn { To = to, Block = block, Distance = empty });
                }
            }
            result.Rng = rng;
            result.Gates = gates;
            result.Golden = b.Golden + (toBasket != null ? toBasket.Count : 0);
            return result;
        }

        private static void CountTarget(Board b, int color)
        {
            if (color < 0) return;                       // a special block counts for no colour target
            for (int t = 0; t < b.Targets.Length; t++)
            {
                if (b.Targets[t].Color != color) continue;
                b.Targets[t].Remaining = Math.Max(0, b.Targets[t].Remaining - 1);
                break;
            }
        }

        private static bool FreeGoldenIn(Block?[] cells, int n, int col)
        {
            for (int r = 0; r < n; r++)
            {
                var block = cells[Index(n, r, col)];
                if (block != null && block.Value.Special == SpecialKind.Golden) return true;
            }
            return false;
        }

        /// <summary>
        /// Pota zinciriCoz: until the board settles, first golden balls on an open gate go into the basket, then runs explode
        /// (free) and give birth to specials. New arrivals can make new runs.
        /// </summary>
        public static Board ResolveChains(Board b, List<Step> steps, int maxSteps = ChainCap)
        {
            var current = b;
            for (int k = 0; k < maxSteps; k++)
            {
                if (TryDropToBasket(current, out var dropped, out var drop)) { current = dropped; steps.Add(drop); continue; }
                var exploded = ResolveMatches(current, out var born);
                if (exploded.Count == 0 && born.Count == 0) break;
                current = ExplodeAndFill(current, exploded, born, null, null, out var step);
                steps.Add(step);
            }
            return current;
        }

        /// <summary>
        /// The game's only move: tap a block (Pota kir). A line or a bomb fires for free (its <see cref="Blast"/> area). A normal
        /// block costs its hp: if ammo covers it the block goes, ammo drops, the column collapses, new blocks fall in and chains
        /// resolve; otherwise the SAME board comes back (Insufficient). A golden ball does nothing (Golden, same board).
        /// </summary>
        public static BreakResult Break(Board b, int cell)
        {
            if (cell < 0 || cell >= b.Cells.Length) return Refused(b, BreakOutcome.Insufficient, 0);
            var block = b.Cells[cell];
            if (block.Special == SpecialKind.Golden) return Refused(b, BreakOutcome.Golden, 0);
            Board after;
            Step first;
            if (block.IsSpecial)
            {
                var area = Blast(b, cell);
                after = ExplodeAndFill(b, area.Cells, null, null, area.Triggered, out first);
            }
            else
            {
                if (b.Ammo < block.Hp) return Refused(b, BreakOutcome.Insufficient, block.Hp);
                var paid = b.Clone();
                paid.Ammo -= block.Hp;
                after = ExplodeAndFill(paid, new List<int> { cell }, null, null, null, out first);
            }
            var steps = new List<Step> { first };
            after = ResolveChains(after, steps);
            // Pota's kir ends with hakemOyna, which (with no referee on the board) runs zinciriCoz once more: it only adds steps
            // when the first pass stopped at ChainCap.
            after = ResolveChains(after, steps);
            return new BreakResult { Broken = true, Outcome = BreakOutcome.Broken, Board = after, Steps = steps, Cost = block.IsSpecial ? 0 : block.Hp };
        }

        private static BreakResult Refused(Board b, BreakOutcome outcome, int cost) =>
            new BreakResult { Broken = false, Outcome = outcome, Board = b, Steps = new List<Step>(), Cost = cost };

        // ---------- state ----------

        public static bool Won(Board b)
        {
            foreach (var t in b.Targets) if (t.Remaining > 0) return false;
            return true;
        }

        /// <summary>Pota enUcuz: the cheapest NORMAL block; specials (hp 0) do not count. int.MaxValue when only specials remain.</summary>
        public static int Cheapest(Board b)
        {
            int min = int.MaxValue;
            foreach (var c in b.Cells) if (!c.IsSpecial && c.Hp < min) min = c.Hp;
            return min;
        }

        /// <summary>Pota hamleVar: a line or a bomb can always be fired (free); otherwise ammo must cover the cheapest block.</summary>
        public static bool HasMove(Board b)
        {
            foreach (var c in b.Cells) if (c.IsSpecial && c.Special != SpecialKind.Golden) return true;
            return b.Ammo > 0 && b.Ammo >= Cheapest(b);
        }

        public static int RemainingTarget(Board b)
        {
            int sum = 0;
            foreach (var t in b.Targets) sum += Math.Max(0, t.Remaining);
            return sum;
        }
    }
}
```

- [ ] **Step 5: Write `ColorHuntRules.Specials.cs`**

`Scripts/ColorHunt/ColorHuntRules.Specials.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace Project.ColorHunt
{
    /// <summary>Pota's special balls (OzelTur). LineV = Pota cizgiDikey: clears its COLUMN (born from a horizontal run of 4).</summary>
    public enum SpecialKind { None = 0, LineV = 1, LineH = 2, Bomb = 3, Golden = 4 }

    /// <summary>Which specials a level gives birth to (Pota Ozellikler). A closed feature skips its birth step.</summary>
    [Serializable]
    public struct Features
    {
        public bool Line, Bomb, Golden;
        public bool Any => Line || Bomb || Golden;
        public static readonly Features None = default;
        public static readonly Features All = new Features { Line = true, Bomb = true, Golden = true };
    }

    /// <summary>A special born in a step (Pota Dogan). <see cref="Group"/> is for the look only (not in Pota).</summary>
    public struct Born
    {
        public int Cell;
        public SpecialKind Kind;
        /// <summary>The exploded cells this birth gathers, sorted, birth cells excluded; groups of one step never share a cell.</summary>
        public int[] Group;
    }

    /// <summary>A special fired by a tap (Pota tetiklenen).</summary>
    public struct Trigger { public int Cell; public SpecialKind Kind; }

    /// <summary>A golden ball leaving bottom-row <see cref="Cell"/> through the gate of column <see cref="Col"/> (Pota sepete).</summary>
    public struct BasketDrop { public int Cell; public int Col; }

    /// <summary>Pota KirmaSonucu without "engel": kirildi, yetmez, altin.</summary>
    public enum BreakOutcome { Broken, Insufficient, Golden }

    /// <summary>Pota dondurVeOtur: <see cref="Rotated"/> is the turned board (the views are built from it), <see cref="Board"/>
    /// the settled one, <see cref="Steps"/> play after the turn.</summary>
    public struct RotateResult { public Board Rotated; public Board Board; public List<Step> Steps; }

    /// <summary>Pota patlamaAlani without ice: the cells a fired special clears and every special it sets off, both sorted.</summary>
    public struct BlastArea { public List<int> Cells; public List<Trigger> Triggered; }

    public static partial class ColorHuntRules
    {
        /// <summary>Pota OZEL_RENK: a special block's colour. It joins no run and counts for no target.</summary>
        public const int SpecialColor = -1;

        // ---------- birth (Pota eslesmeleriCoz, spec §1.2) ----------

        /// <summary>
        /// Pota eslesmeleriCoz: runs sharing a cell form one group, one special per group. 1) a run of ≥5 (Golden on) → golden at
        /// that run's birth cell, the rest of the group explodes; 2) else a group of ≥2 runs (Bomb on) → bomb at the smallest
        /// intersection cell; 3) else every run of exactly 4 (Line on) → a line at its birth cell (horizontal run → LineV at its
        /// second cell, vertical run → LineH at its bottom cell). A closed feature skips its step. Birth cells do not explode.
        /// Returns the exploded cells sorted; <paramref name="born"/> is sorted by cell.
        /// </summary>
        public static List<int> ResolveMatches(Board b, out List<Born> born)
        {
            var f = b.Features;
            var exploded = new SortedSet<int>();
            var births = new SortedDictionary<int, (SpecialKind kind, List<int> cells)>();
            void Birth(int cell, SpecialKind kind, List<int> cells)
            {
                // Pota keeps the last kind set on a cell (two crossing 4-runs with the bomb closed); the look gathers both runs.
                if (births.TryGetValue(cell, out var old))
                    foreach (int c in old.cells) if (!cells.Contains(c)) cells.Add(c);
                births[cell] = (kind, cells);
            }
            foreach (var group in GroupRuns(FindMatches(b)))
            {
                var groupCells = new List<int>();
                foreach (var m in group)
                    foreach (int c in m.Cells)
                    {
                        exploded.Add(c);
                        if (!groupCells.Contains(c)) groupCells.Add(c);
                    }
                Match five = null;
                if (f.Golden) foreach (var m in group) if (m.Cells.Count >= 5) { five = m; break; }
                if (five != null) { Birth(BirthCell(five), SpecialKind.Golden, groupCells); continue; }
                if (group.Count >= 2 && f.Bomb)
                {
                    int best = -1;
                    for (int x = 0; x < group.Count; x++)
                        for (int y = x + 1; y < group.Count; y++)
                        {
                            int h = Intersection(group[x], group[y]);
                            if (h >= 0 && (best < 0 || h < best)) best = h;
                        }
                    if (best >= 0) Birth(best, SpecialKind.Bomb, groupCells);
                    continue;
                }
                if (f.Line)
                    foreach (var m in group)
                        if (m.Cells.Count == 4) Birth(BirthCell(m), m.Horizontal ? SpecialKind.LineV : SpecialKind.LineH, new List<int>(m.Cells));
            }
            foreach (int c in births.Keys) exploded.Remove(c);
            born = new List<Born>();
            var taken = new HashSet<int>(births.Keys);
            foreach (var kv in births)
            {
                var group = new List<int>();
                foreach (int c in kv.Value.cells) if (taken.Add(c)) group.Add(c);
                group.Sort();
                born.Add(new Born { Cell = kv.Key, Kind = kv.Value.kind, Group = group.ToArray() });
            }
            return new List<int>(exploded);
        }

        /// <summary>Pota dogumYeri: a vertical run's bottom cell, a horizontal run's second cell from the left.</summary>
        private static int BirthCell(Match m) => m.Horizontal ? m.Cells[1] : m.Cells[m.Cells.Count - 1];

        /// <summary>Pota kesisiyor: the cell two crossing runs of one colour share, −1 if they do not cross.</summary>
        private static int Intersection(Match a, Match b)
        {
            if (a.Color != b.Color || a.Horizontal == b.Horizontal) return -1;
            foreach (int c in a.Cells) if (b.Cells.Contains(c)) return c;
            return -1;
        }

        /// <summary>Pota gruplaKosular: runs sharing a cell are one group (union–find); groups and their runs keep the run order.</summary>
        private static List<List<Match>> GroupRuns(List<Match> runs)
        {
            var parent = new int[runs.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            var owner = new Dictionary<int, int>();
            for (int i = 0; i < runs.Count; i++)
                foreach (int c in runs[i].Cells)
                {
                    if (!owner.TryGetValue(c, out int o)) { owner[c] = i; continue; }
                    int ra = Find(o), rb = Find(i);
                    if (ra != rb) parent[ra] = rb;
                }
            var groups = new List<List<Match>>();
            var byRoot = new Dictionary<int, List<Match>>();
            for (int i = 0; i < runs.Count; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out var list)) { list = new List<Match>(); byRoot[root] = list; groups.Add(list); }
                list.Add(runs[i]);
            }
            return groups;
        }

        // ---------- firing (Pota tekAlan, patlamaAlani, spec §1.3) ----------

        /// <summary>Pota tekAlan: the cells one special covers on its own, clipped at the edges (LineV its column, LineH its row,
        /// Bomb its 3×3). Golden and None cover nothing.</summary>
        public static List<int> SingleArea(int n, int cell, SpecialKind kind)
        {
            int r = Row(n, cell), c = Col(n, cell);
            var list = new List<int>();
            if (kind == SpecialKind.LineV) for (int rr = 0; rr < n; rr++) list.Add(Index(n, rr, c));
            else if (kind == SpecialKind.LineH) for (int cc = 0; cc < n; cc++) list.Add(Index(n, r, cc));
            else if (kind == SpecialKind.Bomb)
                for (int rr = Math.Max(0, r - 1); rr <= Math.Min(n - 1, r + 1); rr++)
                    for (int cc = Math.Max(0, c - 1); cc <= Math.Min(n - 1, c + 1); cc++) list.Add(Index(n, rr, cc));
            return list;
        }

        /// <summary>
        /// Pota patlamaAlani (no ice): the area of the special at <paramref name="cell"/>, grown by every line or bomb inside it
        /// (each fires once). Golden balls are left out. Empty when <paramref name="cell"/> is not a line or a bomb.
        /// </summary>
        public static BlastArea Blast(Board b, int cell)
        {
            var cells = new SortedSet<int>();
            var fired = new SortedDictionary<int, SpecialKind>();
            var queue = new Queue<int>();
            queue.Enqueue(cell);
            while (queue.Count > 0)
            {
                int h = queue.Dequeue();
                if (h < 0 || h >= b.Cells.Length) continue;
                var kind = b.Cells[h].Special;
                if (kind == SpecialKind.None || kind == SpecialKind.Golden || fired.ContainsKey(h)) continue;
                fired[h] = kind;
                foreach (int i in SingleArea(b.Size, h, kind))
                {
                    var k = b.Cells[i].Special;
                    if (k == SpecialKind.Golden) continue;
                    cells.Add(i);
                    if (k != SpecialKind.None && !fired.ContainsKey(i)) queue.Enqueue(i);
                }
            }
            var area = new BlastArea { Cells = new List<int>(cells), Triggered = new List<Trigger>() };
            foreach (var kv in fired) area.Triggered.Add(new Trigger { Cell = kv.Key, Kind = kv.Value });
            return area;
        }

        // ---------- gates and the basket (Pota sepeteDusur, dondurVeOtur, spec §1.4) ----------

        /// <summary>
        /// Pota sepeteDusur: every golden ball on the bottom row whose column has an open gate goes into the basket (columns in
        /// ascending order), in one step. False (and <paramref name="after"/> = <paramref name="b"/>) when none can.
        /// </summary>
        public static bool TryDropToBasket(Board b, out Board after, out Step step)
        {
            int n = b.Size;
            var drops = new List<BasketDrop>();
            var cols = new List<int>(b.Gates);
            cols.Sort();
            foreach (int c in cols)
            {
                int i = Index(n, n - 1, c);
                if (b.Cells[i].Special == SpecialKind.Golden) drops.Add(new BasketDrop { Cell = i, Col = c });
            }
            if (drops.Count == 0) { after = b; step = null; return false; }
            after = ExplodeAndFill(b, new List<int>(), null, drops, null, out step);
            return true;
        }

        /// <summary>
        /// Pota dondurVeOtur: a turn makes no run (the board has none and specials join none) but can bring a golden ball onto an
        /// open gate: the turned board is settled with <see cref="ResolveChains"/>. Gates stay on the frame.
        /// </summary>
        public static RotateResult RotateAndSettle(Board b, Rotation dir)
        {
            var rotated = Rotate(b, dir);
            var steps = new List<Step>();
            var settled = ResolveChains(rotated, steps);
            return new RotateResult { Rotated = rotated, Board = settled, Steps = steps };
        }
    }
}
```

- [ ] **Step 6: Run the new tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialRulesTests Project.Tests.SpecialRulesFixtureTests`
Expected: `EditMode 50/50 passed (Passed)` (27 hand cases, 22 `Replay_*` games, `Fixture_CoversEverySpecialPath`).

While planning, these exact four files were compiled as C# 9 in a scratch .NET harness (NUnit 3.14, `JsonUtility` shimmed with
System.Text.Json) against this exact JSON: all 50 passed, and planted mutations (gate draw after the refill, colour −1 joining
runs, a basket column always closing) each failed `Replay_all-*` cases. So a failure here first points at a Unity-side
difference (the `JsonUtility` read, `Application.dataPath`, the `.json` not imported): check `Fixture.games.Length == 22`. A
genuine rules difference names the game, move, step and field, e.g.
`all-101 move 3 (tap 33) board after: gates: Pota [4,0,1], Unity [4,0]`. Never edit the JSON or loosen a comparison: find the
diverging line in Pota's `renkAvi.ts` (`patlatVeDoldur`, `eslesmeleriCoz`, `patlamaAlani`, `sepeteDusur`, `zinciriCoz`, `kir`)
on the scratch copy, fix the port, rerun; three tries on the same difference → stop and report (CLAUDE.md §4).

- [ ] **Step 7: Every suite stays green**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile` → Expected: `EditMode 261/261 passed (Passed)` (211 on
2026-09-30 + 50; if the count before this task differed, it is that count + 50 with 0 failures).
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile` → Expected: `PlayMode 85/85 passed (Passed)` (today's
level has every feature off: Color Hunt and PotaFlow play as before).

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntRules.cs \
        Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntRules.Specials.cs* \
        Assets/_Project/Games/Pota/Tests/EditMode/SpecialRulesTests.cs* \
        Assets/_Project/Games/Pota/Tests/EditMode/SpecialRulesFixtureTests.cs*
git commit -m "feat: Color Hunt rules get Pota's special balls (lines, bomb, golden ball, gates, basket, rotate-and-settle), replayed 1:1 against Pota's fixtures; off in today's level

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Special stickers in the label atlas, special sounds

**Files:**
- Modify: `Tools/potalook/export-page.ts` (seven special samples through Pota's chain), `Tools/potalook/run_export.sh` (guard that all seven came out)
- Unchanged, checked: `Tools/potalook/export.mjs` (writes whatever the page returns); the atlas drawing in `export-page.ts` — cells 105–107
  are already drawn by Pota's own `EtiketAtlasi.resimCiz(105 + i, ozel-<ad>-a.png, "payli")` since `ececbd8` (measured 2026-10-01 in
  the committed `label-atlas.png`: cells 105/106/107 are 60 %/42 %/61 % opaque, outer 10 px empty). This task pins that with a test.
- Modify: `Assets/_Project/Games/Pota/Editor/PotaLookImportTool.cs`, `Assets/_Project/Games/Pota/Scripts/ColorHunt/LabelAtlas.cs`,
  `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/LabelAtlasTests.cs`, `Assets/_Project/Games/Pota/Tests/EditMode/PotaLookAssetsTests.cs`
- Output: `Assets/_Project/Games/Pota/Audio/PotaLook/{bomba,cizgi,altin-dogum,altin-inis,smac-vurus,file-swish,kalabalik}.wav` (+ `.meta`),
  `Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset`

**Interfaces:**
- Consumes (Task 2): `SpecialKind`, `Features` (`Features.None`), `LevelDesign.Features`.
- Produces (namespace `Project.ColorHunt`):
  ```csharp
  public enum SpecialLook { Pota, Royal, Court }                         // ColorHuntSettings.cs
  // LabelAtlas
  public const int SpecialBase = 105;
  public static int SpecialCell(SpecialKind k);                          // LineV/LineH 105, Bomb 106, Golden 107; None throws
  // ColorHuntSettings
  public Features Specials;                                              // all off by default = level 20 as today
  public SpecialLook SpecialLook = SpecialLook.Pota;
  public AudioClip SfxBomb, SfxLine, SfxGoldenBorn, SfxGoldenLand;      // bomba, cizgi, altin-dogum, altin-inis
  public AudioClip SfxSlam, SfxSwish, SfxCrowd;                         // smac-vurus, file-swish, kalabalik
  // ToDesign() copies Features = Specials
  ```
- Sound facts for Task 5/10/11 (ses.ts): `cal` plays all seven at gain `k = 1` (only `dustu`/`carpma`/`seritDur` scale), with the sample
  rate jitter 0.97–1.03 and no `SINIR` rate limit. ORNEK ids: `patlama`→bomba, `cizgi`→cizgi, `ozelDogdu`→altin-dogum,
  `altinInis`→altin-inis, `swish`→file-swish, `kalabalik`→kalabalik; `smac-vurus` is Pota's dunk music layer (`MUZIK.smac`, gain 0.45
  there) — the Court look plays it as an effect, so it is rendered through the effect chain like the others. `PotaSounds.Samples`
  should learn those ids in Task 5 so they get the jitter.

- [ ] **Step 1: Write the failing tests**

Add to `Tests/EditMode/LabelAtlasTests.cs` (inside the class):
```csharp
        [Test]
        public void SpecialCells_ArePotasHucreOzel()
        {
            Assert.AreEqual(105, LabelAtlas.SpecialBase);
            Assert.AreEqual(105, LabelAtlas.SpecialCell(SpecialKind.LineV), "yapbozSahne.ts HUCRE_OZEL.cizgi");
            Assert.AreEqual(105, LabelAtlas.SpecialCell(SpecialKind.LineH), "cizgiYatay draws the same sticker, turned 90°");
            Assert.AreEqual(106, LabelAtlas.SpecialCell(SpecialKind.Bomb));
            Assert.AreEqual(107, LabelAtlas.SpecialCell(SpecialKind.Golden));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LabelAtlas.SpecialCell(SpecialKind.None));
        }
```

Add to `Tests/EditMode/PotaLookAssetsTests.cs` (inside the class, after `LabelAtlas_HasInkInTheNumberAndItemCells`):
```csharp
        [Test]
        public void LabelAtlas_CarriesPotasSpecialStickers_Payli()
        {
            var tex = new Texture2D(2, 2);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(Look + "label-atlas.png")), Hint);
            try
            {
                foreach (var k in new[] { SpecialKind.LineV, SpecialKind.Bomb, SpecialKind.Golden })
                {
                    int c = LabelAtlas.SpecialCell(k);
                    Assert.Greater(OpaqueFraction(tex, c), 0.3f, $"{k}: cell {c} holds ozel-*-a.png (ozelleriGiydir)");
                    Assert.AreEqual(0f, EdgeAlpha(tex, c, 10), $"{k}: \"payli\" draws into the middle 88 %, the rim stays clear");
                }
            }
            finally { Object.DestroyImmediate(tex); }
        }

        /// <summary>Largest alpha in the outer <paramref name="px"/> pixels of atlas cell c (y from the TOP, like OpaqueFraction).</summary>
        private static float EdgeAlpha(Texture2D tex, int c, int px)
        {
            int x0 = c % 11 * 186, yTop = c / 11 * 186;
            float max = 0f;
            for (int y = 0; y < 186; y++)
                for (int x = 0; x < 186; x++)
                    if (x < px || y < px || x >= 186 - px || y >= 186 - px)
                        max = Mathf.Max(max, tex.GetPixel(x0 + x, 2047 - (yTop + y)).a);
            return max;
        }

        [Test]
        public void SpecialClips_ArePotasSamples_ThroughTheRoom()
        {
            var s = Settings();
            // ffprobe durations of ~/projects/Pota/public/assets/ses/<name>.mp3 (2026-10-01); the export adds the 0.35 s room tail.
            var expected = new (AudioClip clip, string name, float mp3)[]
            {
                (s.SfxBomb, "bomba", 3.0186f), (s.SfxLine, "cizgi", 1.5325f), (s.SfxGoldenBorn, "altin-dogum", 1.7647f),
                (s.SfxGoldenLand, "altin-inis", 2.9954f), (s.SfxSlam, "smac-vurus", 4.9280f), (s.SfxSwish, "file-swish", 1.6951f),
                (s.SfxCrowd, "kalabalik", 4.0171f),
            };
            foreach (var (clip, name, mp3) in expected)
            {
                Assert.IsNotNull(clip, $"{name}: {Hint}");
                Assert.AreEqual(name, clip.name);
                Assert.AreEqual(mp3 + 0.35f, clip.length, 0.03f, $"public/assets/ses/{name}.mp3 + the room tail, not a synth fallback");
            }
        }

        [Test]
        public void Settings_SpecialBallsAreOff_AndLookPota_InTodaysLevel()
        {
            var s = Settings();
            Assert.AreEqual(Features.None, s.Specials, "bölüm 20 ships without specials (spec §1.1)");
            Assert.AreEqual(SpecialLook.Pota, s.SpecialLook);
            Assert.AreEqual(s.Specials, s.ToDesign().Features, "ToDesign carries the switches to Create");
            var fresh = ScriptableObject.CreateInstance<ColorHuntSettings>();
            try
            {
                Assert.AreEqual(Features.None, fresh.Specials);
                Assert.AreEqual(SpecialLook.Pota, fresh.SpecialLook);
            }
            finally { Object.DestroyImmediate(fresh); }
        }
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.LabelAtlasTests Project.Tests.PotaLookAssetsTests`
Expected: exit 2, `error CS0117: 'LabelAtlas' does not contain a definition for 'SpecialCell'` and
`error CS1061: 'ColorHuntSettings' does not contain a definition for 'SfxBomb'` (and `Specials`, `SpecialLook`).

- [ ] **Step 3: `LabelAtlas` special cells**

Replace `Assets/_Project/Games/Pota/Scripts/ColorHunt/LabelAtlas.cs` with:
```csharp
using System;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// yapbozSahne.ts EtiketAtlasi layout: 2048² canvas, 11 columns × 186 px; numbers 1–99 → cells 0–98, star 99, items 100–104,
    /// special balls 105–107 (HUCRE_OZEL; obstacles 108–110 are exported too, unused here).
    /// </summary>
    public static class LabelAtlas
    {
        public const int Size = 2048, Columns = 11, CellPx = 186, Star = 99, ItemBase = 100, SpecialBase = 105;

        /// <summary>rakamHucresi: min(99, max(1, n)) − 1.</summary>
        public static int NumberCell(int hp) => Mathf.Clamp(hp, 1, 99) - 1;

        public static int ItemCell(int color) => ItemBase + color;

        /// <summary>HUCRE_OZEL: çizgi 105 (both lines; LineH turns the sticker 90°), bomba 106, altın 107. Drawn "payli" (88 %).</summary>
        public static int SpecialCell(SpecialKind k)
        {
            switch (k)
            {
                case SpecialKind.LineV:
                case SpecialKind.LineH: return SpecialBase;
                case SpecialKind.Bomb: return SpecialBase + 1;
                case SpecialKind.Golden: return SpecialBase + 2;
                default: throw new ArgumentOutOfRangeException(nameof(k), k, "not a special ball");
            }
        }

        /// <summary>uvRect: (u, v, w, h) with v from the bottom (the canvas row counts from the top, like three's flipY).</summary>
        public static Vector4 UvRect(int cell)
        {
            int x = cell % Columns * CellPx, y = cell / Columns * CellPx;
            return new Vector4(x / (float)Size, 1f - (y + CellPx) / (float)Size, CellPx / (float)Size, CellPx / (float)Size);
        }
    }
}
```

- [ ] **Step 4: Settings — `SpecialLook`, `Specials`, the seven clips**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs`:

1. Below `public enum PuzzleLook { Pota, Candy, Shatter }` add:
```csharp

    /// <summary>
    /// How special balls are born, idle, fire and land (spec 2026-10-01): Pota = today's feedback 1:1; Royal = 1 "Royal Match";
    /// Court = 2 "Basketbol". Normal block bursts keep the Pota look under all three.
    /// </summary>
    public enum SpecialLook { Pota, Royal, Court }
```
2. After the field `public bool StudioFlight = true;` add:
```csharp

        [Header("Special balls (spec 2026-10-01: Pota renkAvi.ts çizgi / bomba / altın)")]
        [Tooltip("Which specials are born (renkAvi.ts ozellikler). All off = today's level 20: same boards, same rng.")]
        public Features Specials;
        [Tooltip("Özel top görünümü: Pota (bugünkü, birebir), Royal (1 · Royal Match), Court (2 · Basketbol). Normal patlamalar hep Pota.")]
        public SpecialLook SpecialLook = SpecialLook.Pota;
```
3. After `public AudioClip SfxSelect;` add:
```csharp
        [Tooltip("ses.ts special-ball samples through Pota's effect chain: bomba (patlama), cizgi (cizgi), altin-dogum (ozelDogdu), altin-inis (altinInis).")]
        public AudioClip SfxBomb, SfxLine, SfxGoldenBorn, SfxGoldenLand;
        [Tooltip("Court look, same chain: smac-vurus (the dunk's hit, Pota's smac music layer), file-swish (swish), kalabalik (crowd).")]
        public AudioClip SfxSlam, SfxSwish, SfxCrowd;
```
4. `ToDesign` becomes:
```csharp
        public LevelDesign ToDesign()
        {
            var t = new (int, int)[Targets.Length];
            for (int i = 0; i < t.Length; i++) t[i] = (Targets[i].x, Targets[i].y);
            return new LevelDesign { Size = Size, ColorCount = ColorCount, HpMin = HpMin, HpMax = HpMax, Targets = t, Features = Specials };
        }
```

- [ ] **Step 5: Run — the atlas already carries the stickers, the clips are still missing**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.LabelAtlasTests Project.Tests.PotaLookAssetsTests`
Expected: `LabelAtlasTests` all pass; `LabelAtlas_CarriesPotasSpecialStickers_Payli` and `Settings_SpecialBallsAreOff_AndLookPota_InTodaysLevel`
pass; `SpecialClips_ArePotasSamples_ThroughTheRoom` fails with `bomba: run Tools/potalook/run_export.sh <scratch>, then
execute_custom_tool ai_potalook_import`.

- [ ] **Step 6: Export the seven samples through Pota's chain**

`Tools/potalook/export-page.ts`, in `sesler()`, replace the comment + `for (const ad of [...])` header of the sample loop:
```ts
  const cozucu = new OfflineAudioContext(2, RATE, RATE);
  // ses.ts ORNEK: these events play their mp3 at gain 1 (dokun too — the synth "dokun" is only the fallback when the sample is missing).
  // Special balls (2026-10-01): patlama → bomba, cizgi → cizgi, ozelDogdu → altin-dogum, altinInis → altin-inis; the Court look also
  // takes swish → file-swish, kalabalik, and the dunk's hit smac-vurus (Pota's MUZIK.smac layer; Court plays it as an effect, so it
  // goes through the same effect chain as the rest).
  for (const ad of ["blok-pop", "salvo", "dustu", "dondur", "eksi", "kazandi", "kaybetti", "dokun",
                    "bomba", "cizgi", "altin-dogum", "altin-inis", "smac-vurus", "file-swish", "kalabalik"]) {
```
(The loop body — `decodeAudioData`, `new Cevrimdisi(b.duration + 0.35, 300)`, `m.ornek(b)`, `wav(...)` — stays exactly as it is.)

`Tools/potalook/run_export.sh`, anchor the copy's excludes (a bare `dist` also skips `node_modules/*/dist`, so vite breaks on a fresh
copy — found while drafting Task 1, whose `run_fixtures.sh` uses the same anchored line). Replace
```zsh
rsync -a --delete --exclude .git --exclude dist "$SRC/" "$COPY/"
```
with
```zsh
rsync -a --delete --exclude /.git --exclude /dist "$SRC/" "$COPY/"
```

Then, right after the line `"$NODE" "$ROOT/Tools/potalook/export.mjs" "http://localhost:$PORT/deneme/potalook-export.html" "$OUT"` add:
```zsh
# Color Hunt special balls need all seven special samples (ses.ts ORNEK + the dunk's smac-vurus).
for n in bomba cizgi altin-dogum altin-inis smac-vurus file-swish kalabalik; do
  [ -f "$OUT/$n.wav" ] || { echo "missing $n.wav (check $SCRATCH/potalook-vite.log)" >&2; exit 1; }
done
```

Run (Pota stays read-only: the script works in a fresh `rsync --delete` copy):
```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
Tools/potalook/run_export.sh "$S"
git status --short Assets/_Project/Games/Pota/Art Assets/_Project/Games/Pota/Audio
```
Expected: one `name<TAB>bytes` line per file including `bomba.wav`, `cizgi.wav`, `altin-dogum.wav`, `altin-inis.wav`, `smac-vurus.wav`,
`file-swish.wav`, `kalabalik.wav`; last line `textures: 7  sounds: 29`. `git status` lists only the seven new `.wav` as untracked.
If `vite` fails with "Port 5199 is in use", stop the old server (`lsof -ti :5199 | xargs kill`) and run again. If a tracked file shows as
modified (`label-atlas.png`, a sticker, an older `.wav`), Pota changed since 2026-09-30 (its log showed no change to these files up to
`70278c6`): open the image / compare `git diff --stat`, write it in the ledger, keep it (the port follows Pota's current look) and run the
full EditMode + PlayMode suites in Step 9 instead of the listed fixtures.

- [ ] **Step 7: Check the samples are audible and unclipped**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run --with soundfile --with numpy python - <<'EOF'
import soundfile as sf, numpy as np
for n in ["bomba", "cizgi", "altin-dogum", "altin-inis", "smac-vurus", "file-swish", "kalabalik"]:
    d, sr = sf.read(f"Assets/_Project/Games/Pota/Audio/PotaLook/{n}.wav"); a = np.abs(d)
    print(f"{n:12s} {len(d)/sr:6.3f} s  {sr} Hz  ch={d.shape[1]}  peak={a.max():.3f}  clipped={float((a >= 0.999).mean()):.5f}")
EOF
```
Expected: 48000 Hz, 2 channels, each length = mp3 + 0.35 s (bomba 3.369, cizgi 1.883, altin-dogum 2.115, altin-inis 3.345, smac-vurus
5.278, file-swish 2.045, kalabalik 4.367 ± 0.03), peaks between 0.2 and 0.95 (the decoded mp3 peaks × 0.9 are 0.26–0.74, plus the
room send), `clipped=0.00000`.

- [ ] **Step 8: Wire the clips in `ai_potalook_import`**

`Assets/_Project/Games/Pota/Editor/PotaLookImportTool.cs`:

1. The attribute becomes:
```csharp
    [McpForUnityTool("ai_potalook_import", Description =
        "Import the Pota look export (Art/PotaLook, Audio/PotaLook, item stickers, Lilita One): texture/audio import settings, " +
        "PotaBall.mat, Settings/PotaPuzzleProfile.asset (Neutral ×1.12, bloom, vignette), the PotaRoom layer, ColorHuntSettings wiring " +
        "(including the special-ball samples: bomba, cizgi, altin-dogum, altin-inis, smac-vurus, file-swish, kalabalik).")]
```
2. Replace the block from `s.SfxSelect = Clip("dokun");` through the end of the `report.Add($"settings: …");` statement with:
```csharp
            s.SfxSelect = Clip("dokun");
            // Special balls (2026-10-01): ses.ts ORNEK patlama / cizgi / ozelDogdu / altinInis; the Court look's dunk hit, swish and crowd.
            s.SfxBomb = Clip("bomba"); s.SfxLine = Clip("cizgi"); s.SfxGoldenBorn = Clip("altin-dogum"); s.SfxGoldenLand = Clip("altin-inis");
            s.SfxSlam = Clip("smac-vurus"); s.SfxSwish = Clip("file-swish"); s.SfxCrowd = Clip("kalabalik");
            int specials = System.Array.FindAll(new[] { s.SfxBomb, s.SfxLine, s.SfxGoldenBorn, s.SfxGoldenLand, s.SfxSlam, s.SfxSwish, s.SfxCrowd },
                c => c != null).Length;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            report.Add($"settings: atlas={s.LabelAtlas != null} palette=pota font={(s.Font != null ? s.Font.name : "-")} profile={s.PuzzleProfile != null} " +
                       $"combo={System.Array.FindAll(s.SfxCombo, c => c != null).Length}/11 hit={System.Array.FindAll(s.SfxHit, c => c != null).Length}/3 " +
                       $"specials={specials}/7");
```
(The audio-import loop above already gives every clip in `Audio/PotaLook` DecompressOnLoad + PCM, the new ones included.)

- [ ] **Step 9: Compile, import, run the tests**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/mcp_call.py call manage_editor '{"action":"stop"}'
~/.local/bin/uv run Tools/mcp_call.py call refresh_unity '{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}'
~/.local/bin/uv run Tools/mcp_call.py call read_console '{"types":["error"],"filter_text":"error CS"}'
~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_potalook_import","parameters":{}}'
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.LabelAtlasTests Project.Tests.PotaLookAssetsTests --no-compile
~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile
```
Expected: no `error CS`; the import's steps end with `settings: atlas=True … combo=11/11 hit=3/3 specials=7/7`; both fixtures pass; the
full EditMode run passes (`ToDesign` now carries `Features.None`, nothing else changes; `RuntimeShaderBuildTests` untouched).

- [ ] **Step 10: Commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
git add Tools/potalook/export-page.ts Tools/potalook/run_export.sh \
  Assets/_Project/Games/Pota/Editor/PotaLookImportTool.cs \
  Assets/_Project/Games/Pota/Scripts/ColorHunt/LabelAtlas.cs Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs \
  Assets/_Project/Games/Pota/Tests/EditMode/LabelAtlasTests.cs Assets/_Project/Games/Pota/Tests/EditMode/PotaLookAssetsTests.cs \
  Assets/_Project/Games/Pota/Audio/PotaLook Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset
git status --short    # anything else the export touched was reviewed in Step 6: add it only if kept
git commit -m "feat: Color Hunt special balls — atlas cells 105–107 pinned, Pota's seven special samples exported and wired; SpecialLook and Specials settings (off)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Specials drawn on the board, gate rings, `Stage(Board)`

**Files:**
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaLook.cs` (special tone, sticker scale, golden pulse),
  `Assets/_Project/Games/Pota/Scripts/ColorHunt/BlockView.cs` (special body, sticker, no number, never dimmed, `Pulse`, `Bob`,
  `StickerSpin`, `Tone`), `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs` (`Gates`, `Stage`, shared `Deal`, the pulse
  clock, `Dim` skips specials)
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/GateRings.cs` (the folder `Specials/` is new here; Task 5 adds to it)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/PotaLookTests.cs`, `Assets/_Project/Games/Pota/Tests/EditMode/BlockViewTests.cs`,
  `Assets/_Project/Games/Pota/Tests/EditMode/GateRingsTests.cs` (new), `Assets/_Project/Games/Pota/Tests/PlayMode/SpecialBoardPlayTests.cs` (new)
- Output: `docs/screenshots/2026-10-01-specials-board.jpg`

**Interfaces:**
- Consumes: Task 2 — `SpecialKind`, `Block.Special`, `Block.IsSpecial`, `Block.MakeSpecial`, `Board.Gates` (`List<int>`, never null),
  `Board.Features`, `Board.Clone()` (copies `Gates`), `Features.All`; Task 3 — `LabelAtlas.SpecialCell`.
- Produces (namespace `Project.ColorHunt`):
  ```csharp
  // PotaLook
  public const float SpecialScale = 0.82f / 0.88f, GoldenPulseAmount = 0.08f, GoldenPulseSpeed = 5f;
  public static uint SpecialHex(SpecialKind k);  public static uint DarkSaturated(uint hex);
  public static Color SpecialTone(SpecialKind k);   // sRGB: lines #ec7b00, bomb #242732, golden #eeb900
  public static float GoldenPulse(float t);         // 0.08·(1 + sin 5t)
  // BlockView
  public float Pulse;  public float Bob;  public float StickerSpin;  public Color Tone { get; }   // Bob, StickerSpin reset in ResetStudio
  // GateRings (MonoBehaviour on the child "GateRings" of the game root, at its origin)
  public void Build(ColorHuntGame game);  public void Show(IReadOnlyList<int> cols);  public void Flash(int col);
  public Vector3 RingLocal(int col);  public IReadOnlyList<int> Shown { get; }
  public static Vector3 RingAt(int n, int col);  public static float BottomEdge(int n);
  // ColorHuntGame
  public void Stage(Board board);     // deals a copy of board; same reset and events as Prepare; Seed = 0; call Begin to play
  public GateRings Gates { get; }     // created in Start
  ```
- Rulings: (1) **The game drives the golden pulse.** One clock `_clock` (advanced by the views' own `dt`, so a frame-by-frame recording
  named by game time sees the right phase) gives every golden ball the same phase like Pota's shared `simdi`; `BlockView` only applies
  `Pulse` as a flash floor after its decay. Pota's `simdi` is the rAF wall clock and keeps running through a hit-stop; ours pauses for
  ≤ 0.18 s there, invisible at amplitude 0.08. Pota skips the pulse while a block drops or bursts; so does the game (`_drops`,
  `Exploding`). (2) **The gate material reuses the selection ring's template** (`SelectionTemplate`, URP Unlit transparent, through
  `BlockView.RingMaterial`) retinted `#ffb03a` at 0.95: no new shader, no new template, `RuntimeShaderBuildTests` unchanged. Pota's
  `toneMapped: false` has no effect there (its `OutputPass` tonemaps the whole frame), so the ring is tonemapped in Unity as in Pota.
  (3) **The special sticker is centred** (Pota adds no 0.06 lift for `ozel`), scale `0.92 · 0.82/0.88` (items: `0.92 · 0.72/0.88`),
  tint always white (Pota's grey `karart` is the tutorial only).
- Positions (for Task 5's `GoldenRoute` and Task 6): ring centre in game-root units = `(s·(c − (n−1)/2), −(4.4 + 0.35) + 0.05·s,
  −(0.45 + 0.1)·s)`, `s = PotaLook.GridScale(n)`; 7×7: `y = −4.69`, `z = −0.66`, x = −3.6 … +3.6. Pota's golden ball passes the ring at
  board-local `y = GateRings.BottomEdge(n)` with `z = 0` (not the ring's z).
- Left to Task 5 on purpose: `Tap` on a special, births, triggers, golden flight, `Gates.Show` as gates open/close, `Gates.Flash` when a
  golden ball passes; burst/disperse tones of specials must use `v.Tone` (a special's `Block.Color` is −1, `S.PaletteColor(-1)` is red).

- [ ] **Step 1: Write the failing EditMode tests**

Add to `Tests/EditMode/PotaLookTests.cs` (inside the class):
```csharp
        [Test]
        public void SpecialTone_IsKoyuDoygunOfOzelRenkHex()
        {
            // yapbozSahne.ts OZEL_BLOK_TONU evaluated with Pota's three 0.180 (2026-10-01): #ff8a2a → #ec7b00, #2a2d36 → #242732, #ffc93c → #eeb900.
            Assert.AreEqual(PotaLook.Hex(0xec7b00), PotaLook.SpecialTone(SpecialKind.LineV));
            Assert.AreEqual(PotaLook.Hex(0xec7b00), PotaLook.SpecialTone(SpecialKind.LineH));
            Assert.AreEqual(PotaLook.Hex(0x242732), PotaLook.SpecialTone(SpecialKind.Bomb));
            Assert.AreEqual(PotaLook.Hex(0xeeb900), PotaLook.SpecialTone(SpecialKind.Golden));
            Assert.AreEqual(0xd81500u, PotaLook.DarkSaturated(0xe8352e), "three's HSL runs in its LINEAR working space");
            Assert.AreEqual(0x0074d7u, PotaLook.DarkSaturated(0x2a82e8));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PotaLook.SpecialTone(SpecialKind.None));
        }

        [Test]
        public void GoldenPulse_AndSpecialScale_ArePotas()
        {
            Assert.AreEqual(0.08f, PotaLook.GoldenPulse(0f), 1e-6f, "RENK_AVI_HIS.ozel.altinNabiz · (1 + sin 0)");
            Assert.AreEqual(0.16f, PotaLook.GoldenPulse(Mathf.PI / 10f), 1e-6f, "peak at 5t = π/2");
            Assert.AreEqual(0f, PotaLook.GoldenPulse(3f * Mathf.PI / 10f), 1e-6f);
            Assert.AreEqual(0.82f / 0.88f, PotaLook.SpecialScale, 1e-6f, "etiketleriYaz ozelOlcek = 0.82 / CIKARTMA_PAYI");
        }
```

Add to `Tests/EditMode/BlockViewTests.cs` (inside the class):
```csharp
        [Test]
        public void Special_HidesTheNumber_WearsItsToneAndCentredSticker()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Bomb));
            Assert.IsFalse(_go.transform.Find("Number").GetComponent<MeshRenderer>().enabled, "özel top: rakam yok");
            var item = _go.transform.Find("Item");
            Assert.AreEqual(LabelAtlas.UvRect(106), Prop(item, "_UvRect"));
            Assert.AreEqual(0f, item.position.x, 1e-4f);
            Assert.AreEqual(0f, item.position.y, 1e-4f, "centred: no 0.06 lift");
            Assert.AreEqual(0.92f * 0.82f / 0.88f, item.lossyScale.x, 1e-4f, "ozelOlcek");
            Assert.AreEqual(0f, item.localEulerAngles.z, 1e-3f);
            Assert.AreEqual(PotaLook.SpecialTone(SpecialKind.Bomb), v.Tone);
            Assert.AreEqual((Vector4)PotaLook.BlockColor(PotaLook.SpecialTone(SpecialKind.Bomb), 0f, false), Prop(_go.transform, "_BaseColor"));
            Assert.AreEqual((Vector4)Color.white, Prop(item, "_Color"));
        }

        [Test]
        public void LineH_TurnsTheLineSticker90_LineVDoesNot()
        {
            Make(Block.MakeSpecial(SpecialKind.LineH));
            var item = _go.transform.Find("Item");
            Assert.AreEqual(LabelAtlas.UvRect(105), Prop(item, "_UvRect"));
            Assert.AreEqual(90f, item.localEulerAngles.z, 1e-3f, "cizgiYatay: donme + π/2");
            var v = _go.GetComponent<BlockView>();
            v.SetBlock(Block.MakeSpecial(SpecialKind.LineV), 0);
            v.Tick(0f);
            Assert.AreEqual(LabelAtlas.UvRect(105), Prop(item, "_UvRect"));
            Assert.AreEqual(0f, item.localEulerAngles.z, 1e-3f);
        }

        [Test]
        public void Special_IsNeverDimmed()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Golden));
            v.Dim = true;
            v.Tick(0f);
            Assert.AreEqual((Vector4)PotaLook.BlockColor(PotaLook.SpecialTone(SpecialKind.Golden), 0f, false), Prop(_go.transform, "_BaseColor"),
                "renkAviFaz.ts soluk: … && !g.blok.ozel");
            Assert.AreEqual((Vector4)Color.white, Prop(_go.transform.Find("Item"), "_Color"));
        }

        [Test]
        public void ANormalBlock_AfterASpecial_GetsItsNumberAndLiftBack()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Golden));
            v.SetBlock(new Block(2, 9), 0);                                    // a pooled view reused
            v.Tick(0f);
            var item = _go.transform.Find("Item");
            Assert.IsTrue(_go.transform.Find("Number").GetComponent<MeshRenderer>().enabled);
            Assert.AreEqual(LabelAtlas.UvRect(102), Prop(item, "_UvRect"));
            Assert.AreEqual(0.06f, item.position.y, 1e-4f);
            Assert.AreEqual(0.92f * 0.72f / 0.88f, item.lossyScale.x, 1e-4f);
            Assert.AreEqual(PotaLook.Tones[2], v.Tone);
        }

        [Test]
        public void Pulse_FloorsTheFlash_AfterTheDecay()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Golden));
            v.Pulse = 0.12f;
            v.Tick(0.1f);
            Assert.AreEqual(0.12f, v.Flash, 1e-6f);
            v.Flash = 0.5f;
            v.Tick(0.02f);
            Assert.AreEqual(0.5f - 0.02f * PotaLook.FlashDecay, v.Flash, 1e-6f, "a stronger flash decays as before");
            v.Pulse = 0f;
            v.Flash = 0f;
            v.Tick(0.02f);
            Assert.AreEqual(0f, v.Flash, 1e-6f);
        }

        [Test]
        public void BobAndStickerSpin_AddToTheIdlePose_AndResetStudioClearsThem()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.LineH));
            v.Bob = 0.1f;
            v.StickerSpin = 30f;
            v.Tick(0f);
            var item = _go.transform.Find("Item");
            Assert.AreEqual(0.1f, _go.transform.position.y, 1e-5f);
            Assert.AreEqual(120f, item.localEulerAngles.z, 1e-3f, "90° LineH + 30° spin");
            v.ResetStudio();
            v.Tick(0f);
            Assert.AreEqual(0f, v.Bob);
            Assert.AreEqual(0f, v.StickerSpin);
            Assert.AreEqual(0f, _go.transform.position.y, 1e-5f);
            Assert.AreEqual(90f, item.localEulerAngles.z, 1e-3f);
        }
```

Create `Tests/EditMode/GateRingsTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>yapbozSahne.ts gecisGoster: the golden ball's gate rings on the frame.</summary>
    public class GateRingsTests
    {
        private GameObject _go;
        private ColorHuntSettings _s;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_s != null) Object.DestroyImmediate(_s);
        }

        /// <summary>A game root (Start not run: Board is null, so the rings use S.Size = 7) with its GateRings child.</summary>
        private GateRings Make()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = _s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            var g = new GameObject("GateRings").AddComponent<GateRings>();
            g.transform.SetParent(_go.transform, false);
            g.Build(game);
            return g;
        }

        private static List<Transform> Active(GateRings g)
        {
            var list = new List<Transform>();
            foreach (Transform t in g.transform) if (t.gameObject.activeSelf) list.Add(t);
            return list;
        }

        [Test]
        public void RingAt_IsGecisGoster_InTheFrameGroup()
        {
            // 7×7: s = 1.2; x = (c − 3)·1.2; y = (panoAltKenar + 0.05)·s = −(4.4 + 0.35) + 0.06; z = −(Z_ON + 0.1)·s (Unity's front is −z).
            Assert.Less(Vector3.Distance(new Vector3(-3.6f, -4.69f, -0.66f), GateRings.RingAt(7, 0)), 1e-5f);
            Assert.Less(Vector3.Distance(new Vector3(2.4f, -4.69f, -0.66f), GateRings.RingAt(7, 5)), 1e-5f);
            // 5×5: s = 1.68 — the ring stays on the frame's edge, only its size follows the grid.
            Assert.Less(Vector3.Distance(new Vector3(0f, -4.75f + 0.084f, -0.924f), GateRings.RingAt(5, 2)), 1e-5f);
            Assert.AreEqual(-4.75f / 1.2f + 0.05f, GateRings.BottomEdge(7), 1e-5f, "panoAltKenar + 0.05 in board units (halkaY)");
        }

        [Test]
        public void Show_PlacesAPoolOfFour_AndHidesTheRest()
        {
            var g = Make();
            Assert.AreEqual(GateRings.Pool, g.transform.childCount);
            Assert.AreEqual(0, Active(g).Count, "built hidden");
            g.Show(new[] { 1, 5 });
            var rings = Active(g);
            Assert.AreEqual(2, rings.Count);
            Assert.Less(Vector3.Distance(GateRings.RingAt(7, 1), rings[0].localPosition), 1e-6f);
            Assert.Less(Vector3.Distance(GateRings.RingAt(7, 5), rings[1].localPosition), 1e-6f);
            Assert.AreEqual(GateRings.RingAt(7, 5), g.RingLocal(5), "no board yet: the settings' size (7)");
            CollectionAssert.AreEqual(new[] { 1, 5 }, g.Shown);
            Assert.AreEqual(1.2f, rings[0].localScale.x, 1e-5f, "grid scale × (1 + 0.08·sin 0)");
            g.Show(new[] { 0, 1, 2, 3, 4, 5 });
            Assert.AreEqual(4, Active(g).Count, "Pota's pool of 4");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, g.Shown);
            g.Show(new int[0]);
            Assert.AreEqual(0, Active(g).Count);
            Assert.AreEqual(0, g.Shown.Count);
        }

        [Test]
        public void Rings_AreTorus042_FacingTheCamera_InPotasGateColour()
        {
            var g = Make();
            g.Show(new[] { 3 });
            var ring = Active(g)[0];
            Assert.Greater(Mathf.Abs(Vector3.Dot(ring.up, Vector3.forward)), 0.999f, "the torus axis looks down the camera's z");
            var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2f * (0.42f + 0.07f), mesh.bounds.size.x, 1e-3f, "TorusGeometry(0.42, 0.07)");
            Assert.AreEqual(2f * 0.07f, mesh.bounds.size.y, 1e-3f);
            var mat = ring.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual("PotaGate", mat.name);
            Assert.AreEqual(3000, mat.renderQueue, "transparent");
            var c = mat.GetColor("_BaseColor");
            var want = PotaLook.Hex(0xffb03a);
            Assert.AreEqual(want.r, c.r, 2e-3f);
            Assert.AreEqual(want.g, c.g, 2e-3f);
            Assert.AreEqual(want.b, c.b, 2e-3f);
            Assert.AreEqual(0.95f, c.a, 2e-3f);
        }
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests Project.Tests.BlockViewTests Project.Tests.GateRingsTests`
Expected: exit 2, `error CS0117: 'PotaLook' does not contain a definition for 'SpecialTone'`, `error CS1061: 'BlockView' does not contain a
definition for 'Pulse'` (and `Bob`, `StickerSpin`, `Tone`), `error CS0246: The type or namespace name 'GateRings' could not be found`.

- [ ] **Step 3: `PotaLook` — special tone, sticker scale, golden pulse**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaLook.cs`, after `SalvoSource` (before the class's closing brace):
```csharp

        // yapbozSahne.ts special balls: OZEL_RENK_HEX → koyuDoygun → OZEL_BLOK_TONU; etiketleriYaz ozelOlcek. renkAviFaz.ts RENK_AVI_HIS.ozel.
        public const float SpecialScale = 0.82f / 0.88f;        // ozelOlcek: 82 % of the block on screen, drawn into 88 % of its cell
        public const float GoldenPulseAmount = 0.08f;           // ozel.altinNabiz
        public const float GoldenPulseSpeed = 5f;               // ozel.altinHiz (rad/s)

        /// <summary>yapbozSahne.ts OZEL_RENK_HEX: çizgi orange (both directions), bomba smoke, altın gold (sRGB).</summary>
        public static uint SpecialHex(SpecialKind k)
        {
            switch (k)
            {
                case SpecialKind.LineV:
                case SpecialKind.LineH: return 0xff8a2a;
                case SpecialKind.Bomb: return 0x2a2d36;
                case SpecialKind.Golden: return 0xffc93c;
                default: throw new System.ArgumentOutOfRangeException(nameof(k), k, "not a special ball");
            }
        }

        /// <summary>yapbozSahne.ts OZEL_BLOK_TONU = koyuDoygun(OZEL_RENK_HEX), sRGB: lines #ec7b00, bomb #242732, golden #eeb900.</summary>
        public static Color SpecialTone(SpecialKind k) => Hex(DarkSaturated(SpecialHex(k)));

        /// <summary>renkAviFaz.ts gorselleriIlerlet: a golden ball's flash floor 0.08·(1 + sin(5t)), t = the phase clock in seconds.</summary>
        public static float GoldenPulse(float t) => GoldenPulseAmount * (1f + Mathf.Sin(t * GoldenPulseSpeed));

        /// <summary>
        /// yapbozSahne.ts koyuDoygun in three.js's own arithmetic (doubles, like JS): hex → linear (SRGBToLinear), getHSL in that LINEAR
        /// working space, s ×1.25 (≤ 1), l ×0.82, setHSL, back to sRGB (LinearToSRGB, exponent 0.41666) and Math.round to 8 bits (getHex).
        /// </summary>
        public static uint DarkSaturated(uint hex)
        {
            double r = SrgbToLinear(((hex >> 16) & 0xff) / 255.0), g = SrgbToLinear(((hex >> 8) & 0xff) / 255.0), b = SrgbToLinear((hex & 0xff) / 255.0);
            double max = System.Math.Max(r, System.Math.Max(g, b)), min = System.Math.Min(r, System.Math.Min(g, b));
            double h = 0.0, s = 0.0, l = (min + max) / 2.0;
            if (max != min)
            {
                double d = max - min;
                s = l <= 0.5 ? d / (max + min) : d / (2.0 - max - min);
                if (max == r) h = (g - b) / d + (g < b ? 6.0 : 0.0);
                else if (max == g) h = (b - r) / d + 2.0;
                else h = (r - g) / d + 4.0;
                h /= 6.0;
            }
            s = System.Math.Min(1.0, s * 1.25);
            l *= 0.82;
            h -= System.Math.Floor(h);                                           // setHSL: euclideanModulo(h, 1), clamp s and l
            s = System.Math.Min(1.0, System.Math.Max(0.0, s));
            l = System.Math.Min(1.0, System.Math.Max(0.0, l));
            if (s == 0.0) r = g = b = l;
            else
            {
                double hi = l <= 0.5 ? l * (1.0 + s) : l + s - l * s, lo = 2.0 * l - hi;
                r = Hue(lo, hi, h + 1.0 / 3.0);
                g = Hue(lo, hi, h);
                b = Hue(lo, hi, h - 1.0 / 3.0);
            }
            return (Byte8(r) << 16) | (Byte8(g) << 8) | Byte8(b);
        }

        private static double SrgbToLinear(double c) => c < 0.04045 ? c * 0.0773993808 : System.Math.Pow(c * 0.9478672986 + 0.0521327014, 2.4);
        private static double LinearToSrgb(double c) => c < 0.0031308 ? c * 12.92 : 1.055 * System.Math.Pow(c, 0.41666) - 0.055;

        /// <summary>three.js hue2rgb(p = lo, q = hi, t).</summary>
        private static double Hue(double lo, double hi, double t)
        {
            if (t < 0.0) t += 1.0;
            if (t > 1.0) t -= 1.0;
            if (t < 1.0 / 6.0) return lo + (hi - lo) * 6.0 * t;
            if (t < 0.5) return hi;
            if (t < 2.0 / 3.0) return lo + (hi - lo) * 6.0 * (2.0 / 3.0 - t);
            return lo;
        }

        /// <summary>getHex: Math.round(clamp(sRGB·255, 0, 255)); JS rounds .5 up, so floor(v + 0.5).</summary>
        private static uint Byte8(double linear) =>
            (uint)System.Math.Floor(System.Math.Min(255.0, System.Math.Max(0.0, LinearToSrgb(linear) * 255.0)) + 0.5);
```
(Verified while planning: this arithmetic run as Python gives `ec7b00`, `242732`, `eeb900`, `d81500`, `0074d7`, exactly what Pota's
`koyuDoygun` returns with `three@0.180.0` from Pota's `node_modules`.)

- [ ] **Step 4: `BlockView` draws specials**

Replace `Assets/_Project/Games/Pota/Scripts/ColorHunt/BlockView.cs` with:
```csharp
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// One block in Pota's "balon" look: glossy pillow in its candy tone (linear colour per instance), the item sticker (72 %, centre,
    /// +0.06) and the hp number (44 %, bottom-right) from the label atlas, and the state the game drives every frame: flash, uniform
    /// squash, shiver, swell, the labels' counter-angle during rotation, dimming when the ammo does not cover the block. A special ball
    /// (yapbozSahne.ts etiketleriYaz, `h.blok.ozel`) wears OZEL_BLOK_TONU and its sticker (atlas 105–107) centred at 82 %, the
    /// horizontal line's turned 90°; it has no number and is never dimmed.
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
        /// <summary>Flash floor applied after the decay this frame: the game's golden pulse (renkAviFaz.ts altinNabiz); 0 = none.</summary>
        public float Pulse;
        /// <summary>Style-driven idle offset added to the y position (board cells); 0 under the Pota look.</summary>
        public float Bob;
        /// <summary>Style-driven idle turn: degrees added to the sticker's angle; 0 under the Pota look.</summary>
        public float StickerSpin;

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
            Bob = 0f;
            StickerSpin = 0f;
            _press = 0f;
            _release = -1f;
            PressScale = 1f;
        }

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

        public static Material LabelMaterial(Material template, Texture atlas)
        {
            var m = RuntimeMaterial.From(template, "Project/PotaLabel", "PotaLabel");
            m.enableInstancing = true;
            if (atlas != null) m.SetTexture("_BaseMap", atlas);
            return m;
        }

        /// <summary>yapbozSahne.ts secimHalka: white, opacity 0.9, unlit.</summary>
        public static Material RingMaterial(Material template)
        {
            var m = RuntimeMaterial.From(template, "Universal Render Pipeline/Unlit", "PotaSelection");
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
            int sticker = b.IsSpecial ? LabelAtlas.SpecialCell(b.Special) : LabelAtlas.ItemCell(b.Color);
            _itemMpb.SetVector(UvRectId, LabelAtlas.UvRect(sticker));
            _numberMpb.SetVector(UvRectId, LabelAtlas.UvRect(LabelAtlas.NumberCell(b.Hp)));
            _number.enabled = !b.IsSpecial;                        // etiketleriYaz: özel top → rakam yok
            Apply();
        }

        public Texture2D ItemIcon => _s.ItemIcon(Block.Color);

        /// <summary>The body tone (sRGB): OZEL_BLOK_TONU for a special, the palette colour otherwise (Pota blokRengi; bursts use it too).</summary>
        public Color Tone => Block.IsSpecial ? PotaLook.SpecialTone(Block.Special) : _s.PaletteColor(Block.Color);

        /// <summary>Advance the transient state and write transform and colours (renkAviFaz.ts gorselleriIlerlet).</summary>
        public void Tick(float dt)
        {
            Flash = Mathf.Max(0f, Flash - dt * PotaLook.FlashDecay);
            if (Pulse > 0f) Flash = Mathf.Max(Flash, Pulse);      // altinNabiz: after the decay, before drawing
            Squash = Mathf.Max(0f, Squash - dt * PotaLook.SquashDecay);
            Shiver = Mathf.Max(0f, Shiver - dt);
            float t = _s.InsufficientShake;
            float shiverX = Shiver > 0f ? Mathf.Sin((t - Shiver) * 68f) * PotaLook.ShiverAmount * (Shiver / t) : 0f;
            transform.localPosition = new Vector3(Pos.x + shiverX, Pos.y + Bob, 0f);
            float own = Mathf.Max(0.001f, Swell >= 0f ? Swell : 1f - PotaLook.LandSquash * Squash);
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
            PlaceLabels(own);
            Apply();
        }

        /// <summary>
        /// yapbozSahne.ts etiketleriYaz: the plane sits at (Z_ON + 0.03 + bulge)·max(1, scale), the quads grow with the block, the offsets
        /// do not (they are divided by the block's own scale here because the labels are its children). A special's sprite sits in the
        /// middle at ozelOlcek, cizgiYatay's turned +π/2; <see cref="StickerSpin"/> turns the sticker further.
        /// </summary>
        private void PlaceLabels(float own)
        {
            float a = LabelAngle * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            float z = -PotaLook.LabelZ * Mathf.Max(1f, own) / own;
            var rot = Quaternion.Euler(0f, 0f, LabelAngle);
            bool special = Block.IsSpecial;
            float ey = special ? 0f : PotaLook.ItemOffsetY;
            float turn = (Block.Special == SpecialKind.LineH ? 90f : 0f) + StickerSpin;
            var it = _item.transform;
            it.localPosition = new Vector3(-ey * sn / own, ey * cs / own, z);
            it.localRotation = Quaternion.Euler(0f, 0f, LabelAngle + turn);
            it.localScale = Vector3.one * (PotaLook.LabelQuad * (special ? PotaLook.SpecialScale : PotaLook.ItemScale));
            var o = PotaLook.NumberOffset;
            var nt = _number.transform;
            nt.localPosition = new Vector3((o.x * cs - o.y * sn) / own, (o.x * sn + o.y * cs) / own, z - 0.01f / own);
            nt.localRotation = rot;
            nt.localScale = Vector3.one * (PotaLook.LabelQuad * PotaLook.NumberScale);
        }

        private void Apply()
        {
            bool dim = Dim && !Block.IsSpecial;                   // renkAviFaz.ts soluk: … && !g.blok.ozel
            _bodyMpb.SetVector(BaseColorId, PotaLook.BlockColor(Tone, Flash, dim));
            _body.SetPropertyBlock(_bodyMpb);
            _itemMpb.SetVector(ColorId, dim ? PotaLook.ItemShort.linear : Color.white);
            _item.SetPropertyBlock(_itemMpb);
            _numberMpb.SetVector(ColorId, dim ? PotaLook.NumberShort.linear : Color.white);
            _number.SetPropertyBlock(_numberMpb);
        }
    }
}
```
(Against today's file the differences are: the class summary, `Pulse`/`Bob`/`StickerSpin` and their reset, `SetBlock`'s sticker cell and
number switch, `Tone`, the pulse line and `+ Bob` in `Tick`, `PlaceLabels`' special branch, and `Apply`'s `dim`/`Tone`. Everything
else is byte-for-byte today's.)

- [ ] **Step 5: `GateRings`**

Create `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/GateRings.cs`:
```csharp
using System.Collections.Generic;
using Project.Basket.Visual;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// yapbozSahne.ts gecisGoster: the golden ball's gates, glowing rings on the backboard's bottom edge under their column. Pota keeps a
    /// pool of four (one TorusGeometry(0.42, 0.07, 10, 28), one MeshBasicMaterial #ffb03a at opacity 0.95) in its frame group
    /// (cerceveGrup: scaled by the grid scale, never turned), so the rings neither turn nor shrink with a rotating board; they breathe
    /// 1 + 0.08·sin(5t) (ilerlet). This component sits on the child "GateRings" of the game root, at its origin: every position here is
    /// in game-root units (BoardRoot's unrotated placement: origin, scale GridScale(n)).
    /// </summary>
    public class GateRings : MonoBehaviour
    {
        public const int Pool = 4;
        public const float Radius = 0.42f, Tube = 0.07f;
        public const int Tubular = 28, Radial = 10;
        public const float Opacity = 0.95f;
        public const float Breath = 0.08f, BreathSpeed = 5f;
        /// <summary>altinlariIlerlet: flasGoster(bx, halkaY, #ffe27a, 0.9, 2.0, no light) as a golden ball passes its ring.</summary>
        public const float FlashSize = 0.9f, FlashRing = 2f;
        public static readonly Color Tint = PotaLook.Hex(0xffb03a);
        public static readonly Color FlashTint = PotaLook.Hex(0xffe27a);

        private ColorHuntGame _game;
        private readonly List<Transform> _rings = new List<Transform>();
        private readonly List<int> _shown = new List<int>();
        private float _t;

        /// <summary>The columns whose rings are visible, in pool order (at most <see cref="Pool"/>).</summary>
        public IReadOnlyList<int> Shown => _shown;

        public void Build(ColorHuntGame game)
        {
            _game = game;
            if (_rings.Count > 0) return;
            var mesh = RimMeshBuilder.Torus(Radius, Tube, Tubular, Radial);
            var mat = GateMaterial(game != null && game.S != null ? game.S.SelectionTemplate : null);
            for (int i = 0; i < Pool; i++)
            {
                var go = RimMeshBuilder.MeshObject("Gate" + i, transform, mesh, mat);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // RimMeshBuilder's torus lies in xz; three's faces the camera
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                _rings.Add(go.transform);
            }
        }

        /// <summary>The selection ring's URP Unlit transparent template (SelectionTemplate) retinted #ffb03a at 0.95: no new shader or template.</summary>
        public static Material GateMaterial(Material template)
        {
            var m = BlockView.RingMaterial(template);
            m.name = "PotaGate";
            var c = Tint;
            c.a = Opacity;
            m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>gecisGoster: ring i goes under column cols[i]; the rest of the pool hides. Null or empty hides them all.</summary>
        public void Show(IReadOnlyList<int> cols)
        {
            _shown.Clear();
            if (cols != null)
                for (int i = 0; i < cols.Count && _shown.Count < _rings.Count; i++) _shown.Add(cols[i]);
            for (int i = 0; i < _rings.Count; i++)
            {
                bool on = i < _shown.Count;
                _rings[i].gameObject.SetActive(on);
                if (on) _rings[i].localPosition = RingLocal(_shown[i]);
            }
            Breathe();
        }

        /// <summary>altinlariIlerlet: the golden ball passes the ring — ColorHuntFx's glow (no light) at the ring, board-local.</summary>
        public void Flash(int col)
        {
            var fx = _game != null ? _game.Fx : null;
            if (fx == null) return;
            int n = Size;
            float x = BoardBuilder.CellPosition(n, n - 1, col).x;
            fx.Glow(new Vector2(x, BottomEdge(n)), FlashTint.linear, FlashSize, FlashRing, 0f, 0f);
        }

        /// <summary>The ring centre of <paramref name="col"/> for the board on the table, in game-root units.</summary>
        public Vector3 RingLocal(int col) => RingAt(Size, col);

        /// <summary>gecisGoster (c − (n−1)/2, panoAltKenar + 0.05, Z_ON + 0.1) in the frame group, times its grid scale s; Unity's front is −z.</summary>
        public static Vector3 RingAt(int n, int col)
        {
            float s = PotaLook.GridScale(n);
            return new Vector3(BoardBuilder.CellPosition(n, n - 1, col).x * s, BottomEdge(n) * s, -(PotaLook.FrontZ + 0.1f) * s);
        }

        /// <summary>yapbozSahne.ts panoAltKenar + 0.05 in board units (kasasız: −(panoBoy/2 + cerceve)/s): Pota's halkaY.</summary>
        public static float BottomEdge(int n) => -(PotaLook.PanoSize * 0.5f + PotaLook.FrameWidth) / PotaLook.GridScale(n) + 0.05f;

        private int Size => _game != null && _game.Board != null ? _game.Board.Size : _game != null && _game.S != null ? _game.S.Size : 7;

        private void Update()
        {
            _t += Mathf.Min(Time.deltaTime, 0.05f);
            Breathe();
        }

        /// <summary>ilerlet: nabiz = 1 + 0.08·sin(5·gecisZaman) on each visible ring, on top of the frame group's grid scale.</summary>
        private void Breathe()
        {
            float k = PotaLook.GridScale(Size) * (1f + Breath * Mathf.Sin(_t * BreathSpeed));
            for (int i = 0; i < _shown.Count; i++) _rings[i].localScale = Vector3.one * k;
        }
    }
}
```

- [ ] **Step 6: Run the EditMode tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.PotaLookTests Project.Tests.BlockViewTests Project.Tests.GateRingsTests Project.Tests.RuntimeShaderBuildTests`
Expected: all pass (the existing `BlockViewTests` too: a normal block's labels, colours and squash are unchanged).

- [ ] **Step 7: `ColorHuntGame` — gates, `Stage`, the pulse clock, no dimming for specials**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs`:

1. After `public Transform BoardRoot { get; private set; }` add:
```csharp
        /// <summary>The golden ball's gate rings (yapbozSahne.ts gecisGoster), on the game root: they never turn with the board.</summary>
        public GateRings Gates { get; private set; }
```
2. Replace `private float _disperse = -1f;` with:
```csharp
        private float _disperse = -1f;
        /// <summary>The views' clock (s of game time): the golden pulse's phase, shared by every golden ball like Pota's `simdi`.</summary>
        private float _clock;
```
3. In `Start`, after `_selectionRing.SetActive(false);` add:
```csharp

            Gates = new GameObject("GateRings").AddComponent<GateRings>();
            Gates.transform.SetParent(transform, false);
            Gates.Build(this);
```
4. Replace the whole method `public void Prepare(int ammo, uint seed, LevelDesign design) { … }` (its doc comment stays above it) with:
```csharp
        public void Prepare(int ammo, uint seed, LevelDesign design)
        {
            ClearViews();
            Seed = seed != 0 ? seed : S.Seed != 0 ? S.Seed : (uint)UnityEngine.Random.Range(1, int.MaxValue);
            Deal(ColorHuntRules.Create(design ?? S.ToDesign(), ammo, Seed));
        }

        /// <summary>
        /// Deals a given board (demo presets, rule fixtures) instead of a fresh one: a copy goes on the table with the same reset and the
        /// same events as <see cref="Prepare(int, uint, LevelDesign)"/>, its gates show; <see cref="Seed"/> is 0. Call Begin to play.
        /// </summary>
        public void Stage(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            ClearViews();
            Seed = 0;
            Deal(board.Clone());
        }

        /// <summary>Puts a board on the table: views, grid scale, effects and styles cleared, gate rings, counters; then the round events.</summary>
        private void Deal(Board board)
        {
            Board = board;
            BuildViews(Board);
            BoardRoot.localScale = Vector3.one * PotaLook.GridScale(Board.Size);
            Fx?.SetBoard(BoardRoot);
            Fx?.Clear();
            _style?.Clear();
            Gates?.Show(Board.Gates);
            DisplayedAmmo = Board.Ammo;
            Current = State.Idle;
            _staged = true;
            Won = false;
            Rotations = 0; LongestChain = 0;
            _checkNeeded = true;
            _resultTimer = -1f;
            BoardRoot.localRotation = Quaternion.identity;
            ResetShownTargets(false);                   // before RoundStarted: the HUD builds its rows from the new counts, not the last round's
            Raise(RoundStarted);
            Raise(AmmoChanged);
            Raise(TargetsChanged);
        }
```
(Prepare's behaviour is unchanged: same order of `ClearViews`, seed, `Create`, and the same body after it, plus `Gates?.Show`, which
hides every ring for a board without gates.)
5. Replace `TickViews` with:
```csharp
        /// <summary>
        /// Ticks every view. renkAviFaz.ts gorselleriIlerlet: a golden ball that neither drops nor bursts pulses
        /// flas ≥ 0.08·(1 + sin 5t); the game owns the clock so all golden balls share the phase.
        /// </summary>
        private void TickViews(float dt)
        {
            _clock += dt;
            float pulse = PotaLook.GoldenPulse(_clock);
            foreach (var v in _views.Values)
            {
                v.Pulse = v.Block.Special == SpecialKind.Golden && !v.Exploding && !_drops.ContainsKey(v) ? pulse : 0f;
                v.Tick(dt);
            }
            foreach (var v in _loose)
            {
                v.Pulse = 0f;
                v.Tick(dt);
            }
        }
```
6. In `AdvanceViews`, replace `v.Dim = Current == State.Playing && !ammoCovers && !v.Exploding;` with:
```csharp
                v.Dim = Current == State.Playing && !ammoCovers && !v.Exploding && !v.Block.IsSpecial;   // soluk: … && !g.blok.ozel
```
7. In `NewView`, replace the reset line
`v.Home = home; v.Pos = home; v.Scale = 1f; v.Flash = 0f; v.Squash = 0f; v.Shiver = 0f; v.Exploding = false; v.Dim = false; v.Swell = -1f; v.LabelAngle = 0f;` with:
```csharp
            v.Home = home; v.Pos = home; v.Scale = 1f; v.Flash = 0f; v.Squash = 0f; v.Shiver = 0f; v.Exploding = false; v.Dim = false; v.Swell = -1f; v.LabelAngle = 0f; v.Pulse = 0f;
```
(`ResetStudio`, called right below, clears `Bob` and `StickerSpin`. `BuildViews` needs nothing else: a special comes in as a `Block`
and `SetBlock` draws it. `Tap`, `Rotate`, the jobs and the record tool are untouched: Task 5 adds the special flow.)

- [ ] **Step 8: Write the PlayMode test**

Create `Assets/_Project/Games/Pota/Tests/PlayMode/SpecialBoardPlayTests.cs`:
```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>A staged board with all four specials and two gates, in the Color Hunt scene as saved (Pota look).</summary>
    public class SpecialBoardPlayTests
    {
        private static readonly int[] GateCols = { 1, 5 };

        /// <summary>7×7: LineV (1, 1), LineH (2, 4), Bomb (3, 2), Golden (4, 5) — no golden ball on the bottom row (nothing reaches the basket, also after a turn).</summary>
        private static readonly Dictionary<int, SpecialKind> Specials = new Dictionary<int, SpecialKind>
        {
            { 8, SpecialKind.LineV }, { 18, SpecialKind.LineH }, { 23, SpecialKind.Bomb }, { 33, SpecialKind.Golden },
        };

        public static Board SpecialBoard(ColorHuntSettings s)
        {
            var b = ColorHuntRules.Create(s.ToDesign(), s.Ammo, 7u);
            foreach (var kv in Specials) b.Cells[kv.Key] = Block.MakeSpecial(kv.Value);
            b.Gates.AddRange(GateCols);
            b.Features = Features.All;
            return b;
        }

        private static List<Transform> ActiveRings(GateRings g)
        {
            var list = new List<Transform>();
            foreach (Transform t in g.transform) if (t.gameObject.activeSelf) list.Add(t);
            return list;
        }

        private static int SpecialViews(ColorHuntGame game)
        {
            int n = 0;
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                if (v != null && v.Block.IsSpecial) n++;
            }
            return n;
        }

        [UnityTest]
        public IEnumerator StagedSpecials_WearTheirStickers_AndTheGatesStayOnTheFrame()
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            Assert.AreEqual(7, game.S.Size, "the level this test is laid out for");

            // Stage: a copy, the same events as Prepare, staged until Begin.
            var board = SpecialBoard(game.S);
            int started = 0;
            void Count() => started++;
            game.RoundStarted += Count;
            game.Stage(board);
            game.RoundStarted -= Count;
            Assert.AreEqual(1, started, "Stage raises RoundStarted like Prepare");
            Assert.AreNotSame(board, game.Board, "Stage deals a copy");
            Assert.AreEqual(ColorHuntGame.State.Idle, game.Current, "staged until Begin");
            Assert.AreEqual(0u, game.Seed);
            game.Begin();
            yield return null;

            // Views: tone, sticker, no number, never dimmed; LineH turned.
            Assert.AreEqual(4, SpecialViews(game));
            foreach (var kv in Specials)
            {
                var v = game.ViewAt(kv.Key);
                Assert.AreEqual(kv.Value, v.Block.Special, $"cell {kv.Key}");
                Assert.AreEqual(PotaLook.SpecialTone(kv.Value), v.Tone);
                Assert.IsFalse(v.transform.Find("Number").GetComponent<MeshRenderer>().enabled, $"{kv.Value}: no number");
                Assert.IsFalse(v.Dim, $"{kv.Value}: never dimmed");
                var mpb = new MaterialPropertyBlock();
                v.transform.Find("Item").GetComponent<Renderer>().GetPropertyBlock(mpb);
                Assert.AreEqual(LabelAtlas.UvRect(LabelAtlas.SpecialCell(kv.Value)), mpb.GetVector("_UvRect"), $"{kv.Value}: sticker");
            }
            Assert.AreEqual(90f, game.ViewAt(18).transform.Find("Item").localEulerAngles.z, 1e-3f, "LineH: the line sticker turned 90°");

            // The golden ball pulses (0 … 0.16 over 2π/5 s of game time); the bomb does not.
            var golden = game.ViewAt(33);
            float most = 0f, least = 1f, clock = 0f, wall = 0f;
            while (clock < 1.4f && wall < 6f)
            {
                most = Mathf.Max(most, golden.Flash);
                least = Mathf.Min(least, golden.Flash);
                clock += Mathf.Min(Time.deltaTime, 0.05f);
                wall += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Greater(most, 0.14f, "altinNabiz reaches 0.16");
            Assert.Less(least, 0.03f, "and falls to 0");
            Assert.AreEqual(0f, game.ViewAt(23).Flash, 1e-6f, "only golden balls pulse");

            // Gates: two rings on the game root at columns 1 and 5, on the frame's bottom edge.
            Assert.AreSame(game.transform, game.Gates.transform.parent, "on the game root, not the board");
            CollectionAssert.AreEqual(GateCols, game.Gates.Shown);
            var rings = ActiveRings(game.Gates);
            Assert.AreEqual(2, rings.Count);
            for (int i = 0; i < rings.Count; i++)
                Assert.Less(Vector3.Distance(game.Gates.RingLocal(GateCols[i]), rings[i].localPosition), 1e-5f, $"ring {i}");
            Assert.AreEqual(-2.4f, rings[0].localPosition.x, 1e-4f, "column 1 of 7, s = 1.2");
            Assert.AreEqual(-4.69f, rings[0].localPosition.y, 1e-4f, "−(4.4 + 0.35) + 0.05·1.2");

            // Flash at a gate draws through ColorHuntFx.
            Assert.AreEqual(0, game.Fx.LiveCount, "Stage cleared the effects");
            game.Gates.Flash(GateCols[0]);
            yield return null;
            Assert.Greater(game.Fx.LiveCount, 0, "the gate flash drew");

            // The board turns; the rings stay where they are.
            var before = rings.ConvertAll(r => r.position);
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            yield return StudioLookPlayTests.WaitUntil(() => Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity) > 30f, 2f);
            Assert.Greater(Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity), 30f, "caught mid-turn");
            for (int i = 0; i < rings.Count; i++)
                Assert.Less(Vector3.Distance(before[i], rings[i].position), 1e-4f, $"ring {i} turned with the board");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 3f);
            Assert.IsFalse(game.Busy, "the turn never settled");
            for (int i = 0; i < rings.Count; i++)
            {
                Assert.IsTrue(rings[i].gameObject.activeSelf, $"ring {i} still shown");
                Assert.Less(Vector3.Distance(before[i], rings[i].position), 1e-4f, $"ring {i} moved");
            }
            Assert.AreEqual(4, SpecialViews(game), "the specials turned with the board");

            // A normal deal hides the gates and has no specials.
            game.Prepare(game.S.Ammo, 7, null);
            Assert.AreEqual(0, game.Gates.Shown.Count, "a normal deal has no gates");
            Assert.AreEqual(0, ActiveRings(game.Gates).Count);
            Assert.AreEqual(0, SpecialViews(game));
        }
    }
}
```

- [ ] **Step 9: Run the tests (new + every suite: Prepare was refactored)**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.SpecialBoardPlayTests
~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile
~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile
```
Expected: compiles (exit ≠ 2); `SpecialBoardPlayTests` passes; the full EditMode and PlayMode suites pass (every Color Hunt, PotaFlow,
Candy/Shatter and `SceneBootTests` fixture: today's boards have no specials and no gates, so the views and the scenes look as before).

- [ ] **Step 10: Look at it**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
cat > "$S/specials-shot.sh" <<'SH'
#!/bin/zsh
# Plays ColorHunt, stages the four-special board with gates under columns 1 and 5, takes a Game-view screenshot, leaves Play.
set -u
cd "/Users/tunaaltin17/projects/Unity MCP"
S="${0:A:h}"
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
python3 - "$S" <<'EOF'
import json, sys
d = sys.argv[1]
open_scene = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
              'if (s.path != "Assets/_Project/Games/Pota/Scenes/ColorHunt.unity") { if (s.isDirty) return "dirty: " + s.path; '
              'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Games/Pota/Scenes/ColorHunt.unity"); } '
              'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
stage = ('var game = UnityEngine.Object.FindAnyObjectByType<Project.ColorHunt.ColorHuntGame>(); '
         'var b = Project.ColorHunt.ColorHuntRules.Create(game.S.ToDesign(), game.S.Ammo, 7u); '
         'b.Cells[8] = Project.ColorHunt.Block.MakeSpecial(Project.ColorHunt.SpecialKind.LineV); '
         'b.Cells[18] = Project.ColorHunt.Block.MakeSpecial(Project.ColorHunt.SpecialKind.LineH); '
         'b.Cells[23] = Project.ColorHunt.Block.MakeSpecial(Project.ColorHunt.SpecialKind.Bomb); '
         'b.Cells[33] = Project.ColorHunt.Block.MakeSpecial(Project.ColorHunt.SpecialKind.Golden); '
         'b.Gates.Add(1); b.Gates.Add(5); b.Features = Project.ColorHunt.Features.All; '
         'game.Stage(b); game.Begin(); return "gates " + game.Gates.Shown.Count;')
json.dump({"action": "execute", "code": open_scene, "safety_checks": True}, open(d + "/open-colorhunt.json", "w"))
json.dump({"action": "execute", "code": stage, "safety_checks": True}, open(d + "/stage-specials.json", "w"))
EOF
M call execute_code @"$S/open-colorhunt.json" | grep '"result"' | grep -q ColorHunt || { echo "could not open ColorHunt (unsaved scene?)"; exit 1; }
M call manage_editor '{"action":"play"}' > /dev/null
for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
M call execute_code @"$S/stage-specials.json" | grep '"result"'
sleep 1.5
M call manage_camera '{"action":"screenshot","capture_source":"game_view","include_image":false,"output_folder":"Temp/mcp_out"}' | grep -o '[^" ]*\.png' | head -1
M call manage_editor '{"action":"stop"}' > /dev/null
SH
zsh "$S/specials-shot.sh"
```
Expected: `"result": "gates 2"` and a PNG path under `Temp/mcp_out/`. Open that PNG (Read tool) and check, against Pota's art
(`~/projects/Pota/public/assets/ozel-*-a.png`): an orange pillow with the vertical-arrow line sticker at row 2, column 2 (cell 8); the
same sticker turned to horizontal arrows at cell 18; a smoke-dark pillow with the bomb at cell 23; a gold pillow with the golden ball at
cell 33 (slightly brighter or not, depending on the pulse phase); none of them shows a number; all normal blocks look as before; two
orange rings straddle the frame's outer bottom edge under columns 2 and 6 (counting from 1), in front of the frame, not hidden by it,
the same size. A sticker that is mirrored, off-centre or the wrong size, or a ring hidden inside the frame, is a defect: fix it before
going on. Then keep it:
```bash
Tools/keep_screenshot.sh <the PNG path printed above> 2026-10-01-specials-board
```

- [ ] **Step 11: Commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
git add Assets/_Project/Games/Pota/Scripts/ColorHunt/BlockView.cs Assets/_Project/Games/Pota/Scripts/ColorHunt/PotaLook.cs \
  Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs \
  Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials.meta Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/GateRings.cs* \
  Assets/_Project/Games/Pota/Tests/EditMode/PotaLookTests.cs Assets/_Project/Games/Pota/Tests/EditMode/BlockViewTests.cs \
  Assets/_Project/Games/Pota/Tests/EditMode/GateRingsTests.cs* Assets/_Project/Games/Pota/Tests/PlayMode/SpecialBoardPlayTests.cs* \
  docs/screenshots/2026-10-01-specials-board.jpg
git status --short    # nothing else from this task should be left
git commit -m "feat: Color Hunt draws Pota's special balls (OZEL_BLOK_TONU, centred sticker, LineH turned, no number, golden pulse), gate rings on the frame, ColorHuntGame.Stage(Board)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Special tap, trigger plan, birth, golden flight, the Pota look

**Files:**
- Create: `Scripts/ColorHunt/Specials/ISpecialStyle.cs`, `Scripts/ColorHunt/Specials/SpecialSchedule.cs`,
  `Scripts/ColorHunt/Specials/GoldenPath.cs`, `Scripts/ColorHunt/Specials/PotaSpecials.cs`
- Modify: `Scripts/ColorHunt/ColorHuntGame.cs`, `Scripts/ColorHunt/ColorHuntFx.cs` (`Glow` at a depth), `Scripts/ColorHunt/ColorHuntJuice.cs`
  (`GoldenTap`), `Scripts/ColorHunt/PotaSounds.cs` (`PlayClip`, samples, `zip`), `Scripts/ColorHunt/SwellSet.cs` (`elapsed`),
  `Scripts/Flow/GiantHoop.cs` (`Swing`)
- Test: `Tests/EditMode/SpecialScheduleTests.cs`, `Tests/EditMode/GoldenPathTests.cs`, `Tests/EditMode/SpecialStyleTests.cs`,
  `Tests/EditMode/SwellSetTests.cs` (one test), `Tests/PlayMode/SpecialPlayTests.cs`

(All paths under `Assets/_Project/Games/Pota/`.)

**Interfaces:**
- Consumes: Task 2 — `SpecialKind`, `Block.Special/IsSpecial/MakeSpecial`, `Features`, `Born` (`Group`), `Trigger`, `BasketDrop`, `Step.Born/
  Triggered/ToBasket/Opened`, `Board.Gates/Golden/Features`, `BreakResult.Outcome` (`BreakOutcome.Golden`), `ColorHuntRules.SingleArea`,
  `Blast`, `RotateAndSettle` (`RotateResult`), `FindMatches`. Task 3 — `SpecialLook` (in `ColorHuntSettings.cs`),
  `ColorHuntSettings.SpecialLook`, `SfxBomb`, `SfxLine`, `SfxGoldenBorn`, `SfxGoldenLand`. Task 4 — `ColorHuntGame.Stage(Board)`,
  `Gates` (`GateRings.Show/Flash/Shown/RingAt/BottomEdge`), the private `Deal(Board)` that `Prepare` and `Stage` share, Task 4's
  `TickViews` (golden `Pulse` from the game's `_clock`), `PotaLook.SpecialHex`, `BlockView.Pulse`.
- Produces: the "Game ↔ style seam" block of the shared contract plus the Task 5 lines of **Contract additions** above.
- Rulings (ledger):
  1. **Bursts of special blocks use Pota's `blokHex`**: `PotaLook.Hex(PotaLook.SpecialHex(kind))` (OZEL_RENK_HEX), not `BlockView.Tone`
     (OZEL_BLOK_TONU = koyuDoygun, the body). Pota's `parcala`/`patlamaGoster`/`tahtayiDagit` pass `blokHex(g.blok)`, which is the raw
     hex for a special and the palette tone for a normal block. Never `S.PaletteColor(-1)` (red).
  2. **Line text is Pota's own "ÇİZGİ TOPU!"** (`src/dil.ts` `ozelCizgi`); the spec's "ÇİZGİ!" was a shorthand.
  3. **The golden HUD text "ALTIN TOP n/3" and the special targets stay out** (U25); the landing keeps Pota's flash, `altinInis` and the
     net swing (`fileSalla`), which this task adds to `GiantHoop`.
  4. **Gates opening play Pota's synth `zip`** (ses.ts `ton(300·r, 0.16, sine, 0.18, 900)`), synthesised like `topla`; the game plays it
     (gates are game-level, every look).
  5. **A look switch mid-effect lands the golden balls in the air at once** (counted, no effect) and completes gathered blocks: nothing
     waits for a cleared style. A new board (`Deal`) forgets them uncounted.
  6. **A plan longer than 0.6 s is compressed linearly** (`Pace` < 1) so every trigger step meets the Global Constraint; the styles get
     `Pace` with each fire.

- [ ] **Step 1: Write the failing EditMode tests**

`Tests/EditMode/SpecialScheduleTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>SpecialSchedule (spec §2 "sırayla patlama", Review Focus 4): Pota fires all at once; Royal-like timings sweep, ripple and chain on arrival.</summary>
    public class SpecialScheduleTests
    {
        private const float Swell = 0.15f, Eps = 1e-4f;
        private static float Peak => Swell * PotaLook.SwellPeakAt;

        /// <summary>Spec §2 "1 · Royal": a 0.06 s squash, the rocket crosses a 7×7 (6 cells) in 0.32 s; the bomb winds up 0.18 s, rings 0.04 s apart.</summary>
        private static readonly SpecialTiming Royal = new SpecialTiming { LineSpeed = 6f / 0.32f, LineLead = 0.06f, BombLead = 0.18f, BombRing = 0.04f };

        /// <summary>7×7 colour (2r + c) mod 5 (no two neighbours alike), hp 5, all specials on.</summary>
        private static Board Board7(params (int cell, SpecialKind kind)[] specials)
        {
            var cells = new Block[49];
            for (int i = 0; i < 49; i++) cells[i] = new Block((2 * (i / 7) + i % 7) % 5, 5);
            foreach (var (cell, kind) in specials) cells[cell] = Block.MakeSpecial(kind);
            return new Board
            {
                Size = 7, Cells = cells, Ammo = 158, Targets = new[] { new Target(0, 22), new Target(1, 16) },
                ColorCount = 5, HpMin = 3, HpMax = 14, Rng = 1, Features = Features.All,
            };
        }

        /// <summary>The trigger step the rules give for a tap on <paramref name="origin"/>: its area and the specials it sets off.</summary>
        private static Step Blast(Board b, int origin)
        {
            var area = ColorHuntRules.Blast(b, origin);
            var step = new Step();
            step.Exploded.AddRange(area.Cells);
            step.Triggered.AddRange(area.Triggered);
            return step;
        }

        private static TriggerPlan Plan(Board b, int origin, SpecialTiming t) => SpecialSchedule.Plan(7, Blast(b, origin), origin, t, Swell);

        [Test]
        public void PotaTiming_EveryCellAtOnce_EveryFireAtTheFirstPeak()
        {
            var b = Board7((21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV));
            var step = Blast(b, 21);
            var plan = SpecialSchedule.Plan(7, step, 21, SpecialTiming.Pota, Swell);
            Assert.AreEqual(17, step.Exploded.Count, "row 3 + the bomb's 3×3 + column 5");
            Assert.AreEqual(step.Exploded.Count, plan.Start.Count);
            foreach (int c in step.Exploded) Assert.AreEqual(0f, plan.Start[c], $"cell {c}");
            Assert.AreEqual(3, plan.Fires.Count);
            Assert.AreEqual(21, plan.Fires[0].Cell, "the tapped special fires first");
            Assert.AreEqual(0, plan.Fires[0].Order);
            foreach (var f in plan.Fires)
            {
                Assert.AreEqual(Peak, f.Time, Eps, $"{f.Kind} at {f.Cell}: renkAviFaz.ts adimTepe");
                Assert.AreEqual(1f, f.Pace);
            }
            Assert.AreEqual(Swell, plan.End, Eps, "the drops start after one ChainTime, as today");
            Assert.AreEqual(1f, plan.Pace);
        }

        [Test]
        public void Fire_CarriesKindPositionAndClippedAreaSize()
        {
            var plan = Plan(Board7((24, SpecialKind.Bomb)), 24, SpecialTiming.Pota);
            Assert.AreEqual(SpecialKind.Bomb, plan.Fires[0].Kind);
            Assert.AreEqual(BoardBuilder.CellPosition(7, 3, 3), plan.Fires[0].At);
            Assert.AreEqual(9, plan.Fires[0].Size);
            var corner = Plan(Board7((48, SpecialKind.Bomb)), 48, SpecialTiming.Pota);
            Assert.AreEqual(4, corner.Fires[0].Size, "tekAlan: clipped at the corner");
            Assert.AreEqual(4, corner.Start.Count);
        }

        [Test]
        public void GoldenBall_IsNeverInTheArea()
        {
            var plan = Plan(Board7((21, SpecialKind.LineH), (24, SpecialKind.Golden)), 21, Royal);
            Assert.IsFalse(plan.Start.ContainsKey(24), "patlamaAlani: altın alandan çıkar");
            Assert.AreEqual(1, plan.Fires.Count);
            Assert.AreEqual(6, plan.Fires[0].Size);
        }

        [Test]
        public void RoyalLine_SweepsByDistance_AfterItsLead()
        {
            var plan = Plan(Board7((21, SpecialKind.LineH)), 21, Royal);
            Assert.AreEqual(0f, plan.Start[21], Eps, "the line ball itself goes first");
            for (int k = 1; k < 7; k++) Assert.AreEqual(Royal.LineLead + k / Royal.LineSpeed, plan.Start[21 + k], Eps, $"(3, {k})");
            Assert.AreEqual(Royal.LineLead + 6f / Royal.LineSpeed + Swell, plan.End, Eps);
            var column = Plan(Board7((3, SpecialKind.LineV)), 3, Royal);
            for (int r = 1; r < 7; r++) Assert.AreEqual(Royal.LineLead + r / Royal.LineSpeed, column.Start[3 + 7 * r], Eps, $"({r}, 3)");
        }

        [Test]
        public void RoyalBomb_BurstsInRings_AfterItsWindUp()
        {
            var plan = Plan(Board7((24, SpecialKind.Bomb)), 24, Royal);
            Assert.AreEqual(0f, plan.Start[24], Eps);
            foreach (int c in new[] { 16, 17, 18, 23, 25, 30, 31, 32 })
                Assert.AreEqual(Royal.BombLead + Royal.BombRing, plan.Start[c], Eps, $"cell {c}: ring 1");
            Assert.AreEqual(Royal.BombLead + Royal.BombRing + Swell, plan.End, Eps);
        }

        [Test]
        public void Chain_FiresWhenReached_AndItsAreaFollows()
        {
            var plan = Plan(Board7((21, SpecialKind.LineH), (24, SpecialKind.Bomb)), 21, Royal);
            float reach = Royal.LineLead + 3f / Royal.LineSpeed;                       // the rocket gets to (3, 3)
            Assert.AreEqual(2, plan.Fires.Count);
            Assert.AreEqual(24, plan.Fires[1].Cell);
            Assert.AreEqual(1, plan.Fires[1].Order);
            Assert.AreEqual(Peak + reach, plan.Fires[1].Time, Eps, "the bomb fires the moment the rocket reaches it");
            Assert.AreEqual(reach, plan.Start[24], Eps);
            float ring = reach + Royal.BombLead + Royal.BombRing;
            Assert.AreEqual(ring, plan.Start[17], Eps, "(2, 3): only the bomb reaches it");
            Assert.AreEqual(Royal.LineLead + 2f / Royal.LineSpeed, plan.Start[23], Eps, "(3, 2): the rocket is earlier than the ring");
            Assert.AreEqual(Royal.LineLead + 4f / Royal.LineSpeed, plan.Start[25], Eps, "(3, 4)");
            Assert.AreEqual(ring + Swell, plan.End, Eps);
            Assert.LessOrEqual(plan.End, SpecialSchedule.MaxStep + Eps);
            Assert.AreEqual(1f, plan.Pace, "0.59 s: fits, not compressed");
        }

        [Test]
        public void LinePlusBombChain_7x7_EndsWithinTheCap()
        {
            // The worst 7×7 line + bomb chain: the rocket crosses the whole row to a bomb at its far end.
            var plan = Plan(Board7((21, SpecialKind.LineH), (27, SpecialKind.Bomb)), 21, Royal);
            float raw = Royal.LineLead + 6f / Royal.LineSpeed + Royal.BombLead + Royal.BombRing;    // 0.60 s of starts
            Assert.Greater(raw + Swell, SpecialSchedule.MaxStep, "uncompressed it would run long");
            Assert.AreEqual(SpecialSchedule.MaxStep, plan.End, Eps, "≤ 0.6 s from the tap to the drops (Global Constraints)");
            Assert.AreEqual((SpecialSchedule.MaxStep - Swell) / raw, plan.Pace, Eps);
            foreach (var f in plan.Fires) Assert.AreEqual(plan.Pace, f.Pace, Eps);
            Assert.Less(plan.Start[22], plan.Start[27]);
            Assert.Less(plan.Start[27], plan.Start[20], "(2, 6) bursts in the bomb's ring, after the bomb");
            Assert.AreEqual(plan.Fires[1].Time - Peak, plan.Start[27], Eps, "the bomb fires the moment its cell starts");
            // The Chain scenario's three specials (line → bomb → column) fit too.
            var chain = Plan(Board7((21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV)), 21, Royal);
            Assert.AreEqual(3, chain.Fires.Count);
            Assert.LessOrEqual(chain.End, SpecialSchedule.MaxStep + Eps);
        }

        [Test]
        public void MutualChain_FiresEachOnce()
        {
            var cases = new (string name, Board board, int origin)[]
            {
                ("a bomb in the line's path whose 3×3 holds the line", Board7((23, SpecialKind.LineH), (24, SpecialKind.Bomb)), 23),
                ("the same, tapped on the bomb", Board7((23, SpecialKind.LineH), (24, SpecialKind.Bomb)), 24),
                ("two lines in one row", Board7((21, SpecialKind.LineH), (25, SpecialKind.LineH)), 21),
                ("crossing lines", Board7((22, SpecialKind.LineH), (25, SpecialKind.LineV)), 22),
                ("line → bomb → column", Board7((21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV)), 21),
            };
            foreach (var timing in new[] { SpecialTiming.Pota, Royal })
                foreach (var (name, board, origin) in cases)
                {
                    var step = Blast(board, origin);
                    var plan = SpecialSchedule.Plan(7, step, origin, timing, Swell);
                    var fired = new HashSet<int>();
                    foreach (var f in plan.Fires) Assert.IsTrue(fired.Add(f.Cell), $"{name}: {f.Cell} fired twice");
                    Assert.AreEqual(step.Triggered.Count, plan.Fires.Count, $"{name}: every triggered special fires");
                    foreach (var tr in step.Triggered) Assert.IsTrue(fired.Contains(tr.Cell), $"{name}: {tr.Cell} never fired");
                    CollectionAssert.AreEquivalent(step.Exploded, plan.Start.Keys, $"{name}: exactly one start per exploded cell");
                    foreach (var kv in plan.Start) Assert.GreaterOrEqual(kv.Value, 0f, name);
                    for (int i = 1; i < plan.Fires.Count; i++) Assert.LessOrEqual(plan.Fires[i - 1].Time, plan.Fires[i].Time, $"{name}: sorted");
                    Assert.AreEqual(origin, plan.Fires[0].Cell, name);
                    Assert.LessOrEqual(plan.End, SpecialSchedule.MaxStep + Eps, name);
                }
        }
    }
}
```

`Tests/EditMode/GoldenPathTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>GoldenPath = renkAviFaz.ts altinlariIlerlet in game-root units (RENK_AVI_HIS.altinInis).</summary>
    public class GoldenPathTests
    {
        private const float Eps = 1e-4f;

        /// <summary>A golden ball at cell (5, 2) of a 7×7 (s = 1.2) through gate 2.</summary>
        private static GoldenRoute Route(float scale = 1.2f) => new GoldenRoute
        {
            From = new Vector3(-1.2f, -2.4f, 0f), Gate = GateRings.RingAt(7, 2), Hoop = GoldenPath.HoopLocal, Delay = 0.12f, Col = 2, Scale = scale,
        };

        private static void Near(Vector3 expected, Vector3 actual, string what) =>
            Assert.Less(Vector3.Distance(expected, actual), Eps, $"{what}: expected {expected}, got {actual}");

        [Test]
        public void Numbers_ArePotas()
        {
            Assert.AreEqual(0.32f, GoldenPath.GateTime, "halkaSure");
            Assert.AreEqual(0.5f, GoldenPath.HoopTime, "potaSure");
            Assert.AreEqual(1.4f, GoldenPath.Arc, "kavis");
            Assert.AreEqual(0.12f, GoldenPath.Gap, "aralik");
            Assert.AreEqual(0.3f, GoldenPath.Lift);
            Assert.AreEqual(0.82f, GoldenPath.Total, Eps);
            Near(new Vector3(0f, -5.6f, -2f), GoldenPath.HoopLocal, "DEV_POTA.cemberY, (cemberR + 0.6 − 0.2) toward the camera");
        }

        [Test]
        public void GateLeg_DropsStraightDown_EasingIn()
        {
            var r = Route();
            Near(r.From, GoldenPath.At(r, 0f), "start (Delay is the caller's)");
            Near(new Vector3(r.From.x, r.From.y + (r.Gate.y - r.From.y) * 0.25f, r.From.z), GoldenPath.At(r, GoldenPath.GateTime * 0.5f), "o² at half way");
            Near(new Vector3(r.From.x, r.Gate.y, r.From.z), GoldenPath.At(r, GoldenPath.GateTime), "at the ring's height, still on the board plane");
        }

        [Test]
        public void HoopLeg_LiftsAndArcsTowardTheCamera()
        {
            var r = Route();
            var mid = GoldenPath.At(r, GoldenPath.GateTime + GoldenPath.HoopTime * 0.5f);
            Near(new Vector3(r.From.x * 0.5f, (r.Gate.y + r.Hoop.y) * 0.5f + 0.3f * 1.2f, r.Hoop.z * 0.5f - 1.4f * 0.3f * 1.2f), mid, "sin(π/2) lift and arc");
            var unscaled = Route(0f);
            var mid1 = GoldenPath.At(unscaled, GoldenPath.GateTime + GoldenPath.HoopTime * 0.5f);
            Assert.AreEqual((r.Gate.y + r.Hoop.y) * 0.5f + 0.3f, mid1.y, Eps, "Scale 0 counts as 1");
        }

        [Test]
        public void ClampsAtTheHoop()
        {
            var r = Route();
            Near(r.Hoop, GoldenPath.At(r, GoldenPath.GateTime + GoldenPath.HoopTime), "arrival");
            Near(r.Hoop, GoldenPath.At(r, 9f), "after");
        }

        [Test]
        public void PastGate_AndLanded_AtPotasMoments()
        {
            Assert.IsFalse(GoldenPath.PastGate(GoldenPath.GateTime), "ta ≤ halkaSure: still on the way down");
            Assert.IsTrue(GoldenPath.PastGate(GoldenPath.GateTime + 1e-3f));
            Assert.IsFalse(GoldenPath.Landed(GoldenPath.Total - 1e-3f));
            Assert.IsTrue(GoldenPath.Landed(GoldenPath.Total));
        }
    }
}
```

`Tests/EditMode/SpecialStyleTests.cs`:
```csharp
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The special-look switch (mirrors BurstStyleTests): Pota by default, fallback with one warning, the old style cleared.</summary>
    public class SpecialStyleTests
    {
        private class FakeSpecial : ISpecialStyle
        {
            public bool IsReady = true;
            public int Built, Cleared, Fires;
            public bool Active;
            public bool Ready => IsReady;
            public int LiveCount => 0;
            public SpecialTiming Timing => SpecialTiming.Pota;
            public bool GathersBirthGroup => false;
            public bool ConsumesSpecialView => false;
            public void Build(ColorHuntGame game) => Built++;
            public void Activate(bool on) => Active = on;
            public void Clear() => Cleared++;
            public void Idle(BlockView v, float dt) { }
            public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived) => arrived(v);
            public void Birth(BlockView v, SpecialKind kind) { }
            public void TriggerStep(int specials, Vector2 centre) { }
            public void Fire(SpecialFire fire) => Fires++;
            public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed) { passedGate(); landed(); }
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
        public void DefaultSpecialLook_IsPota_AllAtOnce()
        {
            var game = MakeGame();
            Assert.AreEqual(SpecialLook.Pota, ScriptableObject.CreateInstance<ColorHuntSettings>().SpecialLook, "the settings default");
            Assert.AreEqual(SpecialLook.Pota, game.SpecialLook);
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
            Assert.IsTrue(game.Specials.Ready);
            Assert.AreEqual(SpecialTiming.Pota, game.Specials.Timing, "every cell of a trigger step swells at once");
            Assert.IsFalse(game.Specials.GathersBirthGroup);
            Assert.IsFalse(game.Specials.ConsumesSpecialView);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(0, game.GoldenLanded);
        }

        [Test]
        public void MissingAssets_FallBackToPota_WithOneWarning()
        {
            var game = MakeGame();
            game.UseSpecialStyle(SpecialLook.Royal, new FakeSpecial { IsReady = false });
            LogAssert.Expect(LogType.Warning, new Regex("Royal special look is missing assets"));
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreEqual(SpecialLook.Royal, game.SpecialLook, "the look stays what was asked");
            Assert.IsInstanceOf<PotaSpecials>(game.Specials, "the drawing falls back to Pota's");
            game.SetSpecialLook(SpecialLook.Royal);                // the same look again: nothing, no second warning
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnwrittenLook_FallsBackToPota()
        {
            var game = MakeGame();                                 // in-memory settings: no Blender hand-offs
            LogAssert.Expect(LogType.Warning, new Regex("Court special look is missing assets"));
            game.SetSpecialLook(SpecialLook.Court);
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
        }

        [Test]
        public void SetSpecialLook_ClearsTheOldStyle()
        {
            var game = MakeGame();
            var royal = new FakeSpecial();
            game.UseSpecialStyle(SpecialLook.Royal, royal);
            Assert.AreEqual(1, royal.Built);
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreSame(royal, game.Specials);
            Assert.IsTrue(royal.Active);
            game.SetSpecialLook(SpecialLook.Pota);
            Assert.AreEqual(1, royal.Cleared, "the old style drops its effects");
            Assert.IsFalse(royal.Active, "and is deactivated");
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
        }

        [Test]
        public void SpecialLookChanged_FiresOncePerChange()
        {
            var game = MakeGame();
            game.UseSpecialStyle(SpecialLook.Royal, new FakeSpecial());
            int fired = 0;
            game.SpecialLookChanged += () => fired++;
            game.SetSpecialLook(SpecialLook.Royal);
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void SpecialSamples_PlayWithPotasJitter()
        {
            foreach (var id in new[] { "patlama", "cizgi", "ozelDogdu", "altinInis", "swish", "smac", "kalabalik" })
                Assert.IsTrue(PotaSounds.IsSample(id), $"ses.ts ORNEK {id}");
            Assert.IsFalse(PotaSounds.IsSample("zip"), "zip is a synth");
        }

        [Test]
        public void Zip_IsSesTsSweep()
        {
            var clip = PotaSounds.MakeZip();
            Assert.AreEqual(Mathf.CeilToInt(0.18f * 44100), clip.samples);
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float most = 0f;
            foreach (var s in data) most = Mathf.Max(most, Mathf.Abs(s));
            Assert.Greater(most, 0.12f, "gain 0.18 after the attack");
            Assert.LessOrEqual(most, 0.1801f);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Texts_ArePotas()
        {
            Assert.AreEqual("ÇİZGİ TOPU!", SpecialTexts.Born(SpecialKind.LineV), "dil.ts ozelCizgi");
            Assert.AreEqual("ÇİZGİ TOPU!", SpecialTexts.Born(SpecialKind.LineH));
            Assert.AreEqual("BOMBA!", SpecialTexts.Born(SpecialKind.Bomb));
            Assert.AreEqual("ALTIN TOP!", SpecialTexts.Born(SpecialKind.Golden));
            Assert.AreEqual("Altın top geçişten sepete inmeli", SpecialTexts.GoldenTap, "dil.ts altinSepete");
        }
    }
}
```

Add to `Tests/EditMode/SwellSetTests.cs` (inside the class):
```csharp
        [Test]
        public void Start_Elapsed_CountsAsAlreadyRun()
        {
            var set = new SwellSet<object>();
            object a = new object(), b = new object();
            int firedA = 0, firedB = 0;
            set.Start(a, 0.15f, 1.24f, false, () => firedA++, 0.08f);          // started 0.08 s ago
            set.Start(b, 0.15f, 1.24f, false, () => firedB++, -0.01f);         // starts 0.01 s into the next advance
            set.Advance(0.003f, null, null);                                    // a: o 0.553
            Assert.AreEqual(1, firedA);
            set.Advance(0.087f, null, null);                                    // b: 0.08 → o 0.533
            Assert.AreEqual(0, firedB);
            set.Advance(0.01f, null, null);                                     // b: o 0.6
            Assert.AreEqual(1, firedB);
        }
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialScheduleTests Project.Tests.GoldenPathTests Project.Tests.SpecialStyleTests Project.Tests.SwellSetTests`
Expected: exit 2, `error CS0246: The type or namespace name 'SpecialTiming' could not be found` (and `TriggerPlan`, `GoldenRoute`,
`ISpecialStyle`, `PotaSpecials`, `SpecialTexts`), `error CS1501: No overload for method 'Start' takes 6 arguments`.

- [ ] **Step 3: The seam types**

First confirm Task 3 owns the enum: `grep -rn "enum SpecialLook" Assets/_Project/Games/Pota/Scripts` → exactly one hit, in
`ColorHuntSettings.cs`. Do not declare it again.

`Scripts/ColorHunt/Specials/ISpecialStyle.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// What a special look draws (spec 2026-10-01 §2), the sibling of <see cref="IBurstStyle"/>. The game owns the replay of the rules,
    /// the rhythm (the trigger plan from <see cref="SpecialSchedule"/>, the swells, the drops), the gates, the counts, the lock and the
    /// golden balls' bookkeeping; a style only draws and sounds (through PotaSounds.PlayClip, so the recorder logs it). Spaces:
    /// BlockView.Pos, SpecialFire.At and the Gather target are board-local cells (ColorHuntGame.BoardRoot); GoldenRoute is game-root local
    /// (Unity −z toward the camera). Game time throughout: hit-stops and slow motion slow a style too. Normal blocks keep IBurstStyle.
    /// </summary>
    public interface ISpecialStyle
    {
        /// <summary>Its assets are in place. A style that is not ready is replaced by Pota's, with one warning.</summary>
        bool Ready { get; }
        /// <summary>Live particles and pieces this style owns (BurstBudget).</summary>
        int LiveCount { get; }
        /// <summary>How the trigger plan staggers a step (Pota: everything at once).</summary>
        SpecialTiming Timing { get; }
        /// <summary>Royal/Court: a matched group's blocks travel into the birth cell instead of bursting.</summary>
        bool GathersBirthGroup { get; }
        /// <summary>Royal/Court: a firing special's own block is hidden at its fire; the style draws it.</summary>
        bool ConsumesSpecialView { get; }
        void Build(ColorHuntGame game);
        void Activate(bool on);
        /// <summary>Drop every effect at once (a new board, a look switch); never call back afterwards.</summary>
        void Clear();
        /// <summary>Each frame for every special resting on the board (not dropping, not bursting). The game drives the golden pulse.</summary>
        void Idle(BlockView v, float dt);
        /// <summary>A birth group's block travels to <paramref name="to"/> (the birth cell, board-local) in <paramref name="duration"/> s; call <paramref name="arrived"/> once.</summary>
        void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived);
        /// <summary>At the drops' start; the view already shows the special and falls with its column afterwards.</summary>
        void Birth(BlockView v, SpecialKind kind);
        /// <summary>Once at a trigger step's start: how many specials fire in it and the board-local centre of its blocks.</summary>
        void TriggerStep(int specials, Vector2 centre);
        /// <summary>One special fires (the tapped one first), <see cref="SpecialFire.Time"/> s into the step.</summary>
        void Fire(SpecialFire fire);
        /// <summary>A golden ball leaves for the hoop: call <paramref name="passedGate"/>, then <paramref name="landed"/>, each exactly once.</summary>
        void GoldenFlight(GoldenRoute route, Action passedGate, Action landed);
    }

    /// <summary>How a look staggers a trigger step (spec §2 "sırayla patlama"). Default = Pota: everything at once.</summary>
    [Serializable]
    public struct SpecialTiming
    {
        /// <summary>Cells per second a line sweeps; 0 = the whole line at once (Pota).</summary>
        public float LineSpeed;
        /// <summary>Seconds from a line's fire to its first cells.</summary>
        public float LineLead;
        /// <summary>Seconds of wind-up before a bomb bursts.</summary>
        public float BombLead;
        /// <summary>Seconds between the bomb's centre and each further ring (Chebyshev distance).</summary>
        public float BombRing;
        /// <summary>Seconds a birth group takes into the birth cell (GathersBirthGroup); 0 = the step's ChainTime.</summary>
        public float GatherTime;

        public static readonly SpecialTiming Pota = default;
    }

    /// <summary>One special firing in a trigger step.</summary>
    public struct SpecialFire
    {
        public int Cell;
        public SpecialKind Kind;
        /// <summary>Board-local position of its cell.</summary>
        public Vector2 At;
        /// <summary>0 = the tapped special, then in firing order.</summary>
        public int Order;
        /// <summary>Seconds from the step's start to the moment its own block bursts (the tapped one: the step's first peak).</summary>
        public float Time;
        /// <summary>Cells of its own area that burst in this step (clipped at the edges, golden balls left out).</summary>
        public int Size;
        /// <summary>The plan's <see cref="TriggerPlan.Pace"/>: a style multiplies its sweep durations by it.</summary>
        public float Pace;
    }

    /// <summary>When each cell of a trigger step starts its swell, when each special fires, when the drops start.</summary>
    public sealed class TriggerPlan
    {
        /// <summary>Exploded cell → seconds from the step's start to its swell's start (it bursts ChainTime·0.55 later).</summary>
        public readonly Dictionary<int, float> Start = new Dictionary<int, float>();
        /// <summary>Sorted by Time; the tapped special first.</summary>
        public readonly List<SpecialFire> Fires = new List<SpecialFire>();
        /// <summary>The last start + the swell time (≥ every fire): the drops start here.</summary>
        public float End;
        /// <summary>1, or the factor every start and fire was scaled by so the step fits <see cref="SpecialSchedule.MaxStep"/>.</summary>
        public float Pace = 1f;
    }

    /// <summary>A golden ball's way from its cell through its gate into the hoop, game-root local (renkAviFaz.ts altinlariIlerlet).</summary>
    public struct GoldenRoute
    {
        /// <summary>The ball's cell on the board plane (z 0).</summary>
        public Vector3 From;
        /// <summary>Its gate ring (GateRings.RingAt). The drop uses only its y (Pota's halkaY) and keeps From's x and z.</summary>
        public Vector3 Gate;
        /// <summary>Where it lands (GoldenPath.HoopLocal).</summary>
        public Vector3 Hoop;
        /// <summary>Seconds it waits before leaving: the k-th ball of one step leaves k·0.12 s after the first.</summary>
        public float Delay;
        public int Col;
        /// <summary>Grid scale (game-root units per board cell); Pota's lifts and the ball are in cells. 0 counts as 1.</summary>
        public float Scale;
    }

    /// <summary>A look whose style is not written yet (or a future enum value): never ready, so the game plays Pota's specials.</summary>
    public sealed class NoSpecialStyle : ISpecialStyle
    {
        public bool Ready => false;
        public int LiveCount => 0;
        public SpecialTiming Timing => SpecialTiming.Pota;
        public bool GathersBirthGroup => false;
        public bool ConsumesSpecialView => false;
        public void Build(ColorHuntGame game) { }
        public void Activate(bool on) { }
        public void Clear() { }
        public void Idle(BlockView v, float dt) { }
        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived) => arrived?.Invoke(v);
        public void Birth(BlockView v, SpecialKind kind) { }
        public void TriggerStep(int specials, Vector2 centre) { }
        public void Fire(SpecialFire fire) { }
        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed) { passedGate?.Invoke(); landed?.Invoke(); }
    }

    /// <summary>Pota's own words for specials (src/dil.ts, tr). Pota says "ÇİZGİ TOPU!" for the line ball.</summary>
    public static class SpecialTexts
    {
        public const string Line = "ÇİZGİ TOPU!", Bomb = "BOMBA!", Golden = "ALTIN TOP!";
        /// <summary>dokun → "altin": the golden ball cannot be tapped away.</summary>
        public const string GoldenTap = "Altın top geçişten sepete inmeli";

        public static string Born(SpecialKind k) => k == SpecialKind.Golden ? Golden : k == SpecialKind.Bomb ? Bomb : Line;
    }
}
```

- [ ] **Step 4: `SpecialSchedule` and `GoldenPath`**

`Scripts/ColorHunt/Specials/SpecialSchedule.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The trigger step's timetable (spec §2, "sırayla patlama"): pure. The tapped special fires at the step's first peak; each fired
    /// special's area bursts after its lead plus a delay (a line: distance / LineSpeed; a bomb: Chebyshev ring × BombRing); a special in
    /// an area fires the moment its own cell bursts and its area follows from there (earliest arrival wins; each special fires once, so
    /// mutual chains end). Pota's timing (all zero) puts every cell at 0 and every fire at the first peak, exactly today's step. A plan
    /// longer than <see cref="MaxStep"/> is compressed linearly (<see cref="TriggerPlan.Pace"/>), keeping the order.
    /// </summary>
    public static class SpecialSchedule
    {
        /// <summary>Global Constraints: a trigger step lasts at most this from its start to the drops.</summary>
        public const float MaxStep = 0.6f;

        public static TriggerPlan Plan(int n, Step step, int origin, SpecialTiming t, float swellTime)
        {
            var plan = new TriggerPlan();
            float peak = swellTime * PotaLook.SwellPeakAt;
            var exploded = new HashSet<int>(step.Exploded);
            var kinds = new Dictionary<int, SpecialKind>();
            foreach (var tr in step.Triggered) kinds[tr.Cell] = tr.Kind;
            if (!kinds.ContainsKey(origin)) origin = step.Triggered.Count > 0 ? step.Triggered[0].Cell : -1;
            var burst = new Dictionary<int, float>();              // cell → its burst moment
            var reach = new Dictionary<int, float>();              // special → when it fires (earliest arrival so far)
            var fired = new HashSet<int>();
            if (origin >= 0) { reach[origin] = peak; burst[origin] = peak; }
            while (true)
            {
                int next = -1;
                float best = float.MaxValue;
                foreach (var kv in reach)
                {
                    if (fired.Contains(kv.Key)) continue;
                    if (kv.Value < best || (kv.Value == best && kv.Key < next)) { best = kv.Value; next = kv.Key; }
                }
                if (next < 0) break;
                fired.Add(next);
                var kind = kinds[next];
                int size = 0;
                foreach (int c in ColorHuntRules.SingleArea(n, next, kind))
                {
                    if (!exploded.Contains(c)) continue;             // a golden ball, or a cell the rules left out
                    size++;
                    float at = best + Delay(n, next, kind, c, t);
                    if (!burst.TryGetValue(c, out float b) || at < b) burst[c] = at;
                    if (kinds.ContainsKey(c) && !fired.Contains(c) && (!reach.TryGetValue(c, out float r) || at < r)) reach[c] = at;
                }
                plan.Fires.Add(new SpecialFire
                {
                    Cell = next, Kind = kind, Order = plan.Fires.Count, Time = best, Size = size,
                    At = BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, next), ColorHuntRules.Col(n, next)),
                });
            }
            float last = 0f;
            foreach (int c in step.Exploded)
            {
                float start = burst.TryGetValue(c, out float b) ? b - peak : 0f;
                plan.Start[c] = start;
                last = Mathf.Max(last, start);
            }
            if (last > 0f && last + swellTime > MaxStep)
            {
                float k = Mathf.Max(0f, MaxStep - swellTime) / last;
                plan.Pace = k;
                foreach (int c in step.Exploded) plan.Start[c] *= k;
                for (int i = 0; i < plan.Fires.Count; i++)
                {
                    var f = plan.Fires[i];
                    f.Time = peak + (f.Time - peak) * k;
                    plan.Fires[i] = f;
                }
                last *= k;
            }
            for (int i = 0; i < plan.Fires.Count; i++)
            {
                var f = plan.Fires[i];
                f.Pace = plan.Pace;
                plan.Fires[i] = f;
            }
            plan.End = last + swellTime;
            foreach (var f in plan.Fires) plan.End = Mathf.Max(plan.End, f.Time);
            return plan;
        }

        /// <summary>Seconds from a special's fire to the burst of <paramref name="cell"/> in its area.</summary>
        private static float Delay(int n, int from, SpecialKind kind, int cell, SpecialTiming t)
        {
            int dr = Mathf.Abs(ColorHuntRules.Row(n, cell) - ColorHuntRules.Row(n, from));
            int dc = Mathf.Abs(ColorHuntRules.Col(n, cell) - ColorHuntRules.Col(n, from));
            switch (kind)
            {
                case SpecialKind.LineV: return t.LineLead + (t.LineSpeed > 0f ? dr / t.LineSpeed : 0f);   // the column
                case SpecialKind.LineH: return t.LineLead + (t.LineSpeed > 0f ? dc / t.LineSpeed : 0f);   // the row
                case SpecialKind.Bomb: return t.BombLead + Mathf.Max(dr, dc) * t.BombRing;
                default: return 0f;
            }
        }
    }
}
```

`Scripts/ColorHunt/Specials/GoldenPath.cs`:
```csharp
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// renkAviFaz.ts altinlariIlerlet in game-root units (RENK_AVI_HIS.altinInis): from its cell the ball drops straight to its ring's
    /// height in 0.32 s (ease-in o², x and z kept), then in 0.5 s it moves linearly to the hoop while lifting sin(πo)·0.3 cells and
    /// bowing sin(πo)·1.4·0.3 cells toward the camera; the next ball of the same step leaves 0.12 s later.
    /// </summary>
    public static class GoldenPath
    {
        public const float GateTime = 0.32f, HoopTime = 0.5f, Arc = 1.4f, Gap = 0.12f;
        /// <summary>The hoop leg's lift (board cells; the arc toward the camera is Arc times it).</summary>
        public const float Lift = 0.3f;
        /// <summary>DEV_POTA: the rim's centre y, (cemberR + 0.6 − 0.2) toward the camera (GiantHoop's rim sits at R + 0.6).</summary>
        public static readonly Vector3 HoopLocal = new Vector3(0f, PotaLook.RimY, -(PotaLook.RimRadius + 0.4f));

        public static float Total => GateTime + HoopTime;

        /// <summary>Where the ball is <paramref name="t"/> s after leaving (the caller subtracts Delay); clamps at the hoop.</summary>
        public static Vector3 At(GoldenRoute r, float t)
        {
            if (t <= 0f) return r.From;
            if (t >= GateTime + HoopTime) return r.Hoop;
            if (t <= GateTime)
            {
                float o = t / GateTime;
                return new Vector3(r.From.x, Mathf.LerpUnclamped(r.From.y, r.Gate.y, o * o), r.From.z);
            }
            float s = r.Scale > 0f ? r.Scale : 1f;
            float u = (t - GateTime) / HoopTime, bump = Mathf.Sin(Mathf.PI * u);
            return new Vector3(
                Mathf.LerpUnclamped(r.From.x, r.Hoop.x, u),
                Mathf.LerpUnclamped(r.Gate.y, r.Hoop.y, u) + bump * Lift * s,
                Mathf.LerpUnclamped(r.From.z, r.Hoop.z, u) - bump * Arc * Lift * s);
        }

        /// <summary>altinlariIlerlet: the ring closes once ta &gt; halkaSure.</summary>
        public static bool PastGate(float t) => t > GateTime;

        public static bool Landed(float t) => t >= GateTime + HoopTime;
    }
}
```

- [ ] **Step 5: Small additions — `SwellSet`, `ColorHuntFx.Glow` at a depth, `PotaSounds`, `ColorHuntJuice`, `GiantHoop`**

`Scripts/ColorHunt/SwellSet.cs`, replace `Start`:
```csharp
        /// <summary>
        /// Starts (or restarts) an entry. <paramref name="elapsed"/>: seconds it has already run — a planned swell whose start fell between
        /// two frames (negative: it starts that much into the next advance).
        /// </summary>
        public void Start(T key, float duration, float peak, bool loose, Action atPeak, float elapsed = 0f) =>
            _entries[key] = new Entry { Time = elapsed, Duration = Mathf.Max(1e-4f, duration), Peak = peak, Loose = loose, AtPeak = atPeak };
```

`Scripts/ColorHunt/ColorHuntFx.cs`, replace the method `Glow` (keep its doc comment) with:
```csharp
        public void Glow(Vector2 cell, Color lin, float flash, float ring, float light, float lightTime) =>
            Glow(cell, lin, flash, ring, light, lightTime, PotaLook.FrontZ + 0.3f);

        /// <summary>
        /// patlamaEfekt.ts parla at Pota depth <paramref name="z"/> (board cells toward the camera; the default is the block front + 0.3,
        /// the golden ball's landing flash is at the rim, in front of the board).
        /// </summary>
        public void Glow(Vector2 cell, Color lin, float flash, float ring, float light, float lightTime, float z)
        {
            if (!_built) return;
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
```
(The body is today's, with `float z = PotaLook.FrontZ + 0.3f;` turned into the parameter.)

`Scripts/ColorHunt/PotaSounds.cs`:
1. `Limits` gains `{ "zip", 0.04f }` (ses.ts SINIR zip 40):
```csharp
        private static readonly Dictionary<string, float> Limits = new Dictionary<string, float>
        {
            { "carpma", 0.06f }, { "eksi", 0.06f }, { "kirildi", 0.05f }, { "kombo", 0.08f }, { "dustu", 0.045f }, { "topla", 0.03f },
            { "zip", 0.04f },
        };
```
2. `Samples` gains the special ids (ses.ts ORNEK; Task 3's facts):
```csharp
        private static readonly HashSet<string> Samples = new HashSet<string>
        {
            "dokun", "kuleAt", "kirildi", "dustu", "dondur", "eksi", "kazandi", "kaybetti",
            "patlama", "cizgi", "ozelDogdu", "altinInis", "swish", "smac", "kalabalik",
        };
```
3. Field `private AudioClip _collect, _done;` becomes `private AudioClip _collect, _done, _zip;`, and `Awake` ends with `_zip = MakeZip();`.
4. After `MakeDone`:
```csharp
        /// <summary>
        /// ses.ts "zip" (a golden ball's gate opens): ton(300·r, 0.16, sine, 0.18, 900) — a sine sweeping 300 → 900 Hz exponentially over
        /// 0.16 s, gain 0.0001 → 0.18 in 5 ms then exponentially back to 0.0001. Pota's r (0.92–1.08) is the playback pitch here.
        /// </summary>
        public static AudioClip MakeZip() => Synth("zip", 0.18f, t =>
        {
            const float f0 = 300f, f1 = 900f, T = 0.16f, attack = 0.005f, peak = 0.18f, floor = 0.0001f;
            if (t > T) return 0f;
            float k = Mathf.Log(f1 / f0);
            float phase = 2f * Mathf.PI * f0 * T / k * (Mathf.Exp(k * t / T) - 1f);
            float g = t < attack ? floor * Mathf.Pow(peak / floor, t / attack) : peak * Mathf.Pow(floor / peak, (t - attack) / (T - attack));
            return g * Mathf.Sin(phase);
        });
```
5. Next to `Collect`/`TargetDone`:
```csharp
        /// <summary>A gate opens (renkAviFaz.ts dususleriBaslat acilan → ses.cal("zip")).</summary>
        public void Zip() => Play("zip", _zip, 1f, Random.Range(0.92f, 1.08f));

        /// <summary>
        /// A special-ball sound (spec 2026-10-01): Pota's samples (patlama, cizgi, ozelDogdu, altinInis, swish, smac, kalabalik) and the
        /// looks' synth clips share the voices and limits. Pitch 1 = Pota's own (0.97–1.03 for a sample, 1 for a synth); any other pitch is
        /// used as given.
        /// </summary>
        public void PlayClip(string id, AudioClip clip, float volume = 1f, float pitch = 1f) =>
            Play(id, clip, volume, Mathf.Approximately(pitch, 1f) ? -1f : pitch);
```

`Scripts/ColorHunt/ColorHuntJuice.cs`, after `Insufficient`:
```csharp
        /// <summary>dokun → "altin": eksi at half volume and "Altın top geçişten sepete inmeli" (white, the .uyari size) over the ball.</summary>
        public void GoldenTap(Vector3 at)
        {
            Sounds?.PlayClip("eksi", S.SfxMinus, 0.5f);
            Popups?.Show(at, SpecialTexts.GoldenTap, Color.white, 44);
        }
```

`Scripts/Flow/GiantHoop.cs`:
1. Field `private float _kick;` becomes `private float _kick, _swing = -1f;`.
2. After `SetGlow`:
```csharp
        /// <summary>pota.ts fileSalla: a golden ball went through, the net swings for 0.9 s (game time).</summary>
        public void Swing() => _swing = 0f;

        public bool Swinging => _swing >= 0f;

        /// <summary>pota.ts guncelle: net scale (1 + sin 24t·0.14·k, 1 + sin 18t·0.3·k, 1 + cos 24t·0.14·k), k = 1 − t/0.9; after Tick's pile shape.</summary>
        private void LateUpdate()
        {
            if (_swing < 0f || Net == null) return;
            _swing += Time.deltaTime;
            if (_swing > 0.9f) { _swing = -1f; Net.localScale = Vector3.one; return; }
            float t = _swing, k = 1f - t / 0.9f;
            Net.localScale = new Vector3(1f + Mathf.Sin(t * 24f) * 0.14f * k, 1f + Mathf.Sin(t * 18f) * 0.3f * k, 1f + Mathf.Cos(t * 24f) * 0.14f * k);
        }
```
(PotaFlow's director writes the pile shape in `Tick` during its phases; the swing overrides it for 0.9 s and the next `Tick` restores it.)

- [ ] **Step 6: The Pota look — `PotaSpecials`**

`Scripts/ColorHunt/Specials/PotaSpecials.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// The Pota look for special balls, renkAviFaz.ts 1:1 (spec 2026-10-01 §2 "Pota"). Birth (dususleriBaslat): flash 1.0, squash 0.6, a
    /// light flash (golden #ffe27a, else white; 1.6 / 3.4 / 5 / 0.2 s), altin-dogum or kombo(2), "ALTIN TOP!" / "BOMBA!" / "ÇİZGİ TOPU!"
    /// in gold. Idle: nothing here (the game drives the golden pulse). Fire (adimTepe, at the step's first peak): a bomb — #ffd28a
    /// 2.6 / 6.5 / 8 / 0.25 s, shake 0.2, bomba; a line — three #ffe0b0 1.2 / 2.2 / 3 / 0.15 s flashes at the line's ends and centre,
    /// shake 0.12, cizgi; and the ×0.12 dip for 0.06 s. Golden flight (altinlariIlerlet): the salvo ball tinted #ffc93c, ×1.25, spinning
    /// 9 rad/s, drops to its gate (the ring flashes), arcs into the hoop; on arrival a #ffe27a flash at the rim (1.8 / 4.2 / 5 / 0.22 s,
    /// sized in game-root units), altin-inis and the net swing. Every cell of a trigger step swells at once.
    /// </summary>
    public class PotaSpecials : MonoBehaviour, ISpecialStyle
    {
        // renkAviFaz.ts RENK_AVI_HIS.ozel / altinInis and the flasGoster arguments; yapbozSahne.ts cizToplar (TOP_TABAN).
        public const float LineShake = 0.12f, BombShake = 0.2f, HitStop = 0.06f, BornFlash = 1f, BornSquash = 0.6f;
        public const uint BombFlash = 0xffd28a, LineFlash = 0xffe0b0, GoldenFlash = 0xffe27a, White = 0xffffff;
        public const uint BallGold = 0xffc93c, BallBase = 0xf07d1d;
        public const float BallScale = 1.25f, BallRadius = 0.19f, BallSpin = 9f;

        private class Flight
        {
            public GoldenRoute Route;
            public float T, Spin;
            public bool Passed;
            public Action PassedGate, Landed;
            public Transform Ball;
        }

        private ColorHuntGame _game;
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly Stack<Transform> _balls = new Stack<Transform>();
        private Material _ballMat;
        private int _gen;

        public bool Ready => true;
        public int LiveCount => _flights.Count;
        public SpecialTiming Timing => SpecialTiming.Pota;
        public bool GathersBirthGroup => false;
        public bool ConsumesSpecialView => false;

        public void Build(ColorHuntGame game) => _game = game;

        public void Activate(bool on)
        {
            if (!on) Clear();
        }

        public void Clear()
        {
            _gen++;
            foreach (var f in _flights) Hide(f);
            _flights.Clear();
        }

        public void Idle(BlockView v, float dt) { }

        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived) => arrived?.Invoke(v);

        public void Birth(BlockView v, SpecialKind kind)
        {
            if (_game == null) return;
            v.Flash = BornFlash;
            v.Squash = BornSquash;
            bool golden = kind == SpecialKind.Golden;
            _game.Fx?.Glow(v.Pos, PotaLook.Hex(golden ? GoldenFlash : White).linear, 1.6f, 3.4f, 5f, 0.2f);
            var sounds = Sounds();
            if (sounds != null)
            {
                if (golden) sounds.PlayClip("ozelDogdu", _game.S.SfxGoldenBorn);
                else sounds.Combo(2);
            }
            var popups = _game.Juice != null ? _game.Juice.Popups : null;
            if (popups != null)
                popups.Show(_game.BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.6f, -PotaLook.LabelZ)), SpecialTexts.Born(kind),
                    PotaLook.HudGold, 72, false, 1.2f);                     // .patlama.kombo.dogum: gold 44 px, 1.2 s
        }

        public void TriggerStep(int specials, Vector2 centre) { }

        public void Fire(SpecialFire f)
        {
            if (_game == null) return;
            var fx = _game.Fx;
            var juice = _game.Juice;
            if (f.Kind == SpecialKind.Bomb)
            {
                fx?.Glow(f.At, PotaLook.Hex(BombFlash).linear, 2.6f, 6.5f, 8f, 0.25f);
                juice?.Shake?.Add(BombShake);
                Sounds()?.PlayClip("patlama", _game.S.SfxBomb);
            }
            else if (f.Kind == SpecialKind.LineV || f.Kind == SpecialKind.LineH)
            {
                float half = (_game.Board.Size - 1) * 0.5f;                  // the line's two ends and its centre
                foreach (float u in new[] { -half, 0f, half })
                    fx?.Glow(f.Kind == SpecialKind.LineV ? new Vector2(f.At.x, u) : new Vector2(u, f.At.y), PotaLook.Hex(LineFlash).linear, 1.2f, 2.2f, 3f, 0.15f);
                juice?.Shake?.Add(LineShake);
                Sounds()?.PlayClip("cizgi", _game.S.SfxLine);
            }
            juice?.TimeCtl?.Dip(_game.S.HitStopTapScale, HitStop);           // zamanDusur(HIS.hitStop.olcek, ozel.hitStop)
        }

        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
        {
            var f = new Flight { Route = route, PassedGate = passedGate, Landed = landed, Ball = TakeBall() };
            _flights.Add(f);
        }

        private void Update()
        {
            if (_flights.Count == 0 || _game == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            int gen = _gen;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _flights.Count) return;              // a callback dealt a new board
                var f = _flights[i];
                f.T += dt;
                float t = f.T - f.Route.Delay;
                if (t < 0f) continue;                                         // the step's next ball leaves 0.12 s later
                f.Spin += dt * BallSpin;
                float s = f.Route.Scale > 0f ? f.Route.Scale : 1f;
                if (f.Ball != null)
                {
                    f.Ball.gameObject.SetActive(true);
                    f.Ball.localPosition = GoldenPath.At(f.Route, t) + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * s);   // cizToplar z
                    f.Ball.localScale = Vector3.one * (2f * BallRadius * BallScale * s);
                    f.Ball.localRotation = Quaternion.Euler(new Vector3(f.Spin, f.Spin * 0.6f, f.Spin * 0.3f) * Mathf.Rad2Deg);
                }
                if (!f.Passed && GoldenPath.PastGate(t))
                {
                    f.Passed = true;
                    var gates = _game.Gates;
                    if (gates != null) gates.Flash(f.Route.Col);                 // flasGoster(bx, halkaY, #ffe27a, 0.9, 2.0)
                    f.PassedGate?.Invoke();
                    if (gen != _gen) return;
                }
                if (!GoldenPath.Landed(t)) continue;
                _flights.RemoveAt(i);
                Hide(f);
                Land(f.Route);
                f.Landed?.Invoke();
                if (gen != _gen) return;
            }
        }

        /// <summary>altinlariIlerlet arrival: flash at the rim (sizes ÷ s: they are game-root units), altinInis, fileSalla.</summary>
        private void Land(GoldenRoute r)
        {
            float s = r.Scale > 0f ? r.Scale : 1f;
            var p = _game.BoardRoot.InverseTransformPoint(_game.transform.TransformPoint(r.Hoop));
            _game.Fx?.Glow(new Vector2(p.x, p.y), PotaLook.Hex(GoldenFlash).linear, 1.8f / s, 4.2f / s, 5f, 0.22f, -p.z);
            Sounds()?.PlayClip("altinInis", _game.S.SfxGoldenLand);
            var hoop = _game.Hoop;
            if (hoop != null) hoop.Swing();
        }

        /// <summary>The salvo ball (sphere r 0.19, S.SalvoBall) tinted so its base orange becomes #ffc93c (ton = renk / TOP_TABAN, linear).</summary>
        private Transform TakeBall()
        {
            if (_balls.Count > 0) return _balls.Pop();
            var template = _game != null && _game.S != null ? _game.S.SalvoBall : null;
            if (template == null) return null;                                // not imported: the flight still runs, unseen
            if (_ballMat == null)
            {
                var gold = PotaLook.Hex(BallGold).linear;
                var basis = PotaLook.Hex(BallBase).linear;
                _ballMat = new Material(template) { name = "PotaGoldenBall" };
                _ballMat.SetColor("_BaseColor", new Color(gold.r / basis.r, gold.g / basis.g, gold.b / basis.b, 1f).gamma);   // SetColor linearises
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GoldenBall";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);                           // this object sits at the game root's origin
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _ballMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        private void Hide(Flight f)
        {
            if (f.Ball == null) return;
            f.Ball.gameObject.SetActive(false);
            _balls.Push(f.Ball);
            f.Ball = null;
        }

        private PotaSounds Sounds() => _game != null && _game.Juice != null ? _game.Juice.Sounds : null;

        private void OnDestroy()
        {
            if (_ballMat != null) Destroy(_ballMat);
        }
    }
}
```

- [ ] **Step 7: `ColorHuntGame` — special tap, plan, births, gathers, basket, gates, the look switch**

`Scripts/ColorHunt/ColorHuntGame.cs` (Task 4's version; read it first):

1. Usings: after `using Project.Basket.Visual;` add `using Project.Flow;`.

2. Replace the `Busy` line with:
```csharp
        public bool Busy => _jobs.Count > 0 || (Salvo != null && Salvo.Busy) || _swells.AnyLoose || _intro >= 0f || _goldens.Count > 0;
```

3. Replace `private class StepJob : Job { public Step Step; public bool Dropping; public float DropTime; public bool Started; public int Depth; }` with:
```csharp
        private class StepJob : Job
        {
            public Step Step; public bool Dropping; public float DropTime; public bool Started; public int Depth;
            /// <summary>The tapped special of a trigger step (−1: none).</summary>
            public int Origin = -1;
            /// <summary>When the drops start: ChainTime, a trigger plan's End, a gathering look's GatherTime.</summary>
            public float SwellEnd;
            public TriggerPlan Plan;
            public int NextFire;
            public Action Peak;
            public readonly List<PendingSwell> Pending = new List<PendingSwell>();
            public readonly HashSet<int> Consumed = new HashSet<int>();
        }
        private class PendingSwell { public int Cell; public BlockView View; public float At; }
```

4. `Start`: after `SetLook(S != null ? S.Look : PuzzleLook.Pota);` add:
```csharp
            SetSpecialLook(S != null ? S.SpecialLook : SpecialLook.Pota);
```

5. `Deal` (Task 4): replace `Gates?.Show(Board.Gates);` with:
```csharp
            ResetSpecials();                            // the special style, golden balls and gate rings start over (shows Board.Gates)
```

6. Replace `Tap` with:
```csharp
        /// <summary>Tap a cell. Returns "broken", "insufficient", "golden" (a golden ball leaves only through a gate) or "locked".</summary>
        public string Tap(int cell)
        {
            if (Current != State.Playing || Busy) return "locked";
            Select(-1);
            int ammoBefore = Board.Ammo;
            var r = ColorHuntRules.Break(Board, cell);
            if (r.Outcome == BreakOutcome.Golden)
            {
                var at = CellWorld(cell);
                if (_views.TryGetValue(cell, out var gv))
                {
                    gv.Shiver = S.InsufficientShake;                                                  // titrem
                    at = BoardRoot.TransformPoint(new Vector3(0f, gv.Pos.y + 0.55f, -PotaLook.LabelZ));   // dokun "altin": board centre, y + 0.55
                }
                Juice?.GoldenTap(at);
                return "golden";
            }
            if (!r.Broken)
            {
                var at = CellWorld(cell);
                if (_views.TryGetValue(cell, out var v))
                {
                    v.Shiver = S.InsufficientShake;
                    at = BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.55f, -PotaLook.LabelZ));   // dokunmaReddi: y + 0.55
                }
                Juice?.Insufficient(at, r.Cost);
                return "insufficient";
            }
            bool special = Board.Cells[cell].IsSpecial;
            Board = r.Board;
            _checkNeeded = true;
            _targetsPending = true;
            if (special)
            {
                QueueSteps(r.Steps, false, cell);       // dokun: özel top — free, no salvo, its blast plays as depth 1
                return "broken";
            }
            _jobs.Add(new SalvoJob { Cell = cell, Cost = r.Cost, AmmoBefore = ammoBefore, Steps = r.Steps });
            return "broken";
        }
```

7. Replace `Rotate` with:
```csharp
        /// <summary>dondurmeUygula: turn (free) and settle (dondurVeOtur) — a golden ball turned onto an open gate drops after the turn.</summary>
        public bool Rotate(Rotation dir)
        {
            if (Current != State.Playing || Busy) return false;
            Select(-1);
            var rr = ColorHuntRules.RotateAndSettle(Board, dir);
            Board = rr.Board;
            Rotations++;
            _checkNeeded = true;
            if (rr.Steps.Count > 0) _targetsPending = true;
            _jobs.Add(new RotateJob { Angle = dir == Rotation.Clockwise ? -90f : 90f, Rotated = rr.Rotated });
            QueueSteps(rr.Steps, false, -1);            // they play once the views are rebuilt from the turned board
            Juice?.Rotate();
            return true;
        }
```

8. `Update`: replace `AdvanceJobs(dt);` with:
```csharp
            AdvanceJobs(dt);
            TickGoldens(dt);
```

9. Replace Task 4's `TickViews` with:
```csharp
        /// <summary>
        /// Ticks every view. renkAviFaz.ts gorselleriIlerlet: a golden ball that neither drops nor bursts pulses flas ≥ 0.08·(1 + sin 5t)
        /// (the game owns the clock so all golden balls share the phase); every resting special gets the look's idle life.
        /// </summary>
        private void TickViews(float dt)
        {
            _clock += dt;
            float pulse = PotaLook.GoldenPulse(_clock);
            var specials = Specials;
            foreach (var v in _views.Values)
            {
                bool resting = !v.Exploding && !_drops.ContainsKey(v);
                v.Pulse = v.Block.Special == SpecialKind.Golden && resting ? pulse : 0f;
                if (v.Block.IsSpecial && resting) specials.Idle(v, dt);
                v.Tick(dt);
            }
            foreach (var v in _loose)
            {
                v.Pulse = 0f;
                v.Tick(dt);
            }
            foreach (var v in _gathering)
            {
                v.Pulse = 0f;
                v.Tick(dt);
            }
        }
```

10. `Disperse`: replace `Fx?.Particles(v.Pos, S.PaletteColor(v.Block.Color), 6, 3.5f, 0.1f);` with
`Fx?.Particles(v.Pos, BurstTone(v.Block), 6, 3.5f, 0.1f);` (tahtayiDagit: `blokHex`).

11. `AdvanceJobs`, replace the `case StepJob st:` block with:
```csharp
                case StepJob st:
                    if (!st.Started) StartStep(st);
                    st.T += dt;
                    if (!st.Dropping)
                    {
                        TickPlan(st, dt);
                        if (st.T >= st.SwellEnd) StartDrops(st);
                        break;
                    }
                    if (st.T >= st.DropTime)
                    {
                        foreach (var kv in _drops) { kv.Key.Pos = kv.Key.Home; }
                        _drops.Clear();
                        _jobs.RemoveAt(0);
                        if (_jobs.Count > 0) _jobs.Insert(0, new WaitJob { Duration = S.ChainTime });
                    }
                    break;
```

12. Replace `FinishSalvo` with:
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
                view.Dim = false;                                   // sonenler are drawn lit (soluk: false)
                var tone = S.PaletteColor(view.Block.Color);
                int cell = s.Cell;
                _swells.Start(view, S.TapSwellTime, S.TapSwell, true, () =>
                {
                    Juice?.Break();
                    Style.TapBurst(view, tone);
                    CountItem(view, BoardRoot.TransformPoint(new Vector3(view.Pos.x, view.Pos.y, -PotaLook.FrontZ)));
                    RaiseCellBurst(cell);
                });
                RippleNeighbours(s.Cell);
            }
            else Juice?.Break();
            DisplayedAmmo = Board.Ammo;
            Raise(AmmoChanged);
            _jobs.RemoveAt(0);
            QueueSteps(s.Steps, true, -1);
        }
```

13. After the method `AddStyle<T>` add the specials block:
```csharp
        // ---------- special balls (spec 2026-10-01) ----------

        public SpecialLook SpecialLook { get; private set; } = SpecialLook.Pota;
        /// <summary>What draws the special balls; a look whose assets are missing falls back to Pota's with one warning.</summary>
        public ISpecialStyle Specials => _special ?? (_special = ResolveSpecial(SpecialLook));
        public event Action SpecialLookChanged;
        /// <summary>A block burst — its swell peak, a consumed special's fire, a gathered block's arrival — with the cell it had at its step's start.</summary>
        public event Action<int> CellBurst;
        /// <summary>Golden balls that reached the hoop since the deal (no HUD for them yet: U25).</summary>
        public int GoldenLanded { get; private set; }
        /// <summary>Golden balls on their way to the hoop; the board is locked meanwhile (Pota kilitliMi).</summary>
        public int GoldenInFlight => _goldens.Count;
        /// <summary>The giant hoop around the board (PuzzleStage builds it under this root), or null.</summary>
        public GiantHoop Hoop => _hoop != null ? _hoop : (_hoop = GetComponentInChildren<GiantHoop>(true));
        /// <summary>A style's flight must land within this many seconds after Pota's would; otherwise the game lands it (one warning).</summary>
        public const float GoldenGrace = 3f;

        private ISpecialStyle _special;
        private readonly Dictionary<SpecialLook, ISpecialStyle> _specialStyles = new Dictionary<SpecialLook, ISpecialStyle>();
        private class GoldenState { public int Col; public bool Passed; public float Age, Life; }
        private readonly List<GoldenState> _goldens = new List<GoldenState>();
        /// <summary>Pota gorunenGecisler: the deal's gates, + opened ones at a step's drops, − one per golden ball that passed.</summary>
        private readonly List<int> _shownGates = new List<int>();
        /// <summary>Birth-group blocks travelling into their birth cell (gathering looks); the style moves them, the game ticks them.</summary>
        private readonly List<BlockView> _gathering = new List<BlockView>();
        private int _specialGen;
        private GiantHoop _hoop;

        /// <summary>Switches the special look (the demo's buttons, the record tool): the old style is cleared and deactivated.</summary>
        public void SetSpecialLook(SpecialLook look)
        {
            if (_special != null && look == SpecialLook) return;
            if (_special != null)
            {
                SettleSpecialWork();
                _special.Clear();
                _special.Activate(false);
            }
            SpecialLook = look;
            _special = ResolveSpecial(look);
            _special.Activate(true);
            Raise(SpecialLookChanged);
        }

        /// <summary>Registers (and builds) the style a special look uses; later looks and tests plug in here.</summary>
        public void UseSpecialStyle(SpecialLook look, ISpecialStyle style)
        {
            style.Build(this);
            _specialStyles[look] = style;
        }

        private ISpecialStyle ResolveSpecial(SpecialLook look)
        {
            if (!_specialStyles.TryGetValue(look, out var style)) UseSpecialStyle(look, style = CreateSpecialStyle(look));
            if (style.Ready) return style;
            Debug.LogWarning($"Color Hunt: the {look} special look is missing assets (run ai_studio_import); Pota's specials play instead.");
            return ResolveSpecial(SpecialLook.Pota);
        }

        private ISpecialStyle CreateSpecialStyle(SpecialLook look)
        {
            switch (look)
            {
                case SpecialLook.Pota: return AddSpecialStyle<PotaSpecials>("PotaSpecials");
                default: return new NoSpecialStyle();          // Royal: Task 10, Court: Task 11
            }
        }

        /// <summary>A MonoBehaviour special style on its own child of the game root (game-root space: the hoop's, not the board's).</summary>
        private T AddSpecialStyle<T>(string name) where T : MonoBehaviour, ISpecialStyle
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// renkAviFaz.ts adimlariSiraya: a salvo's first step is depth 0 (its block already burst); a special tap's and a rotation's steps
        /// start from 0 and go one deeper at every step with something to burst, so a special's blast plays at depth 1. A pure basket step
        /// keeps the depth (it is no chain).
        /// </summary>
        private void QueueSteps(List<Step> steps, bool salvo, int origin)
        {
            int depth = salvo ? -1 : 0;
            for (int k = 0; k < steps.Count; k++)
            {
                var step = steps[k];
                if (step.Exploded.Count > 0 || (k == 0 && salvo)) depth++;
                _jobs.Add(new StepJob { Step = step, Depth = depth, Origin = k == 0 ? origin : -1 });
            }
            if (depth > LongestChain) LongestChain = depth;
        }

        /// <summary>Pota blokHex: a normal block bursts in its palette tone, a special in OZEL_RENK_HEX (its body is the darker OZEL_BLOK_TONU).</summary>
        private Color BurstTone(Block b) => b.IsSpecial ? PotaLook.Hex(PotaLook.SpecialHex(b.Special)) : S.PaletteColor(b.Color);

        private void RaiseCellBurst(int cell)
        {
            try { CellBurst?.Invoke(cell); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>A new board: no callback of the old one counts, nothing flies, the rings show the board's gates.</summary>
        private void ResetSpecials()
        {
            _specialGen++;
            _goldens.Clear();
            GoldenLanded = 0;
            _special?.Clear();
            _shownGates.Clear();
            if (Board != null && Board.Gates != null) _shownGates.AddRange(Board.Gates);
            Gates?.Show(_shownGates);
        }

        /// <summary>A look switch mid-effect: golden balls in the air land now (counted, no effect), gathered blocks arrive now.</summary>
        private void SettleSpecialWork()
        {
            _specialGen++;
            foreach (var g in _goldens)
            {
                if (!g.Passed) CloseGate(g.Col);
                GoldenLanded++;
            }
            _goldens.Clear();
            foreach (var v in _gathering)
            {
                CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                Recycle(v);
            }
            _gathering.Clear();
        }

        private void OpenGates(List<int> opened)
        {
            if (opened == null || opened.Count == 0) return;
            _shownGates.AddRange(opened);
            Gates?.Show(_shownGates);
            Juice?.Sounds?.Zip();
        }

        /// <summary>altinlariIlerlet: the ring closes when its ball passes — one of them (the same step may have opened the column again).</summary>
        private void CloseGate(int col)
        {
            int k = _shownGates.IndexOf(col);
            if (k < 0) return;
            _shownGates.RemoveAt(k);
            Gates?.Show(_shownGates);
        }

        private void LaunchGolden(Vector2 cellPos, int col, float delay)
        {
            int n = Board.Size;
            var route = new GoldenRoute
            {
                From = transform.InverseTransformPoint(BoardRoot.TransformPoint(new Vector3(cellPos.x, cellPos.y, 0f))),
                Gate = GateRings.RingAt(n, col),
                Hoop = GoldenPath.HoopLocal,
                Delay = delay,
                Col = col,
                Scale = PotaLook.GridScale(n),
            };
            var state = new GoldenState { Col = col, Life = delay + GoldenPath.Total + GoldenGrace };
            _goldens.Add(state);
            int gen = _specialGen;
            Specials.GoldenFlight(route,
                () =>
                {
                    if (gen != _specialGen || state.Passed || !_goldens.Contains(state)) return;
                    state.Passed = true;
                    CloseGate(col);
                },
                () =>
                {
                    if (gen != _specialGen || !_goldens.Remove(state)) return;
                    if (!state.Passed) { state.Passed = true; CloseGate(col); }
                    GoldenLanded++;
                });
        }

        /// <summary>Lands a golden ball whose style never called back (a broken look must not lock the board for good).</summary>
        private void TickGoldens(float dt)
        {
            for (int i = _goldens.Count - 1; i >= 0; i--)
            {
                var g = _goldens[i];
                g.Age += dt;
                if (g.Age < g.Life) continue;
                Debug.LogWarning($"Color Hunt: the {SpecialLook} special look never landed the golden ball of column {g.Col}; landed now.");
                _goldens.RemoveAt(i);
                if (!g.Passed) CloseGate(g.Col);
                GoldenLanded++;
            }
        }

        /// <summary>Gathering looks: a birth group's other blocks travel into the birth cell instead of bursting (their stickers fly on arrival).</summary>
        private HashSet<int> GatherBirthGroups(StepJob st)
        {
            var gathered = new HashSet<int>();
            if (st.Step.Born.Count == 0 || !Specials.GathersBirthGroup) return gathered;
            var exploded = new HashSet<int>(st.Step.Exploded);
            int n = Board.Size, gen = _specialGen;
            float time = GatherDuration();
            foreach (var b in st.Step.Born)
            {
                if (b.Group == null) continue;
                var home = ViewAt(b.Cell);
                var to = home != null ? home.Home : BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, b.Cell), ColorHuntRules.Col(n, b.Cell));
                foreach (int g in b.Group)
                {
                    if (!exploded.Contains(g) || gathered.Contains(g)) continue;
                    var v = ViewAt(g);
                    if (v == null) continue;
                    gathered.Add(g);
                    _views.Remove(g);
                    _gathering.Add(v);
                    v.Exploding = true;
                    int cell = g;
                    Specials.Gather(v, to, time, arrived =>
                    {
                        if (gen != _specialGen || !_gathering.Remove(arrived)) return;
                        CountItem(arrived, BoardRoot.TransformPoint(new Vector3(arrived.Pos.x, arrived.Pos.y, -PotaLook.FrontZ)));
                        RaiseCellBurst(cell);
                        Recycle(arrived);
                    });
                }
            }
            return gathered;
        }

        private float GatherDuration() => Specials.Timing.GatherTime > 0f ? Specials.Timing.GatherTime : S.ChainTime;

        private void StartBurstSwell(StepJob st, int cell, BlockView v, float elapsed)
        {
            int d = st.Depth;
            var tone = BurstTone(v.Block);
            _swells.Start(v, S.ChainTime, S.ChainSwell, false, () =>
            {
                st.Peak?.Invoke();
                Style.StepBurst(v, tone, d);
                CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                RaiseCellBurst(cell);
            }, elapsed);
        }

        /// <summary>Starts the swells whose time has come (credited with the part of the frame they missed) and fires the specials that are due.</summary>
        private void TickPlan(StepJob st, float dt)
        {
            for (int k = 0; k < st.Pending.Count;)
            {
                var p = st.Pending[k];
                if (p.At > st.T) { k++; continue; }
                st.Pending.RemoveAt(k);
                StartBurstSwell(st, p.Cell, p.View, st.T - dt - p.At);
            }
            if (st.Plan == null) return;
            while (st.NextFire < st.Plan.Fires.Count && st.Plan.Fires[st.NextFire].Time <= st.T) FireSpecial(st, st.Plan.Fires[st.NextFire++]);
        }

        /// <summary>The drops start: every special that has not fired fires, every swell that has not started bursts now (End covers both; a long frame may not).</summary>
        private void FlushPlan(StepJob st)
        {
            if (st.Plan != null)
                while (st.NextFire < st.Plan.Fires.Count) FireSpecial(st, st.Plan.Fires[st.NextFire++]);
            foreach (var p in st.Pending) StartBurstSwell(st, p.Cell, p.View, 0f);   // StartDrops' End() bursts them at once
            st.Pending.Clear();
        }

        private void FireSpecial(StepJob st, SpecialFire f)
        {
            if (st.Consumed.Contains(f.Cell))
            {
                var v = ViewAt(f.Cell);
                if (v != null) v.gameObject.SetActive(false);      // the look draws it now; the drops recycle the view
                st.Peak?.Invoke();
                RaiseCellBurst(f.Cell);
            }
            Specials.Fire(f);
        }
```

14. Replace `StartStep` with:
```csharp
        /// <summary>
        /// renkAviFaz.ts adimBaslat: depth 0 and a pure basket step drop at once; deeper steps swell every block to 1.24 over ChainTime. The
        /// first peak carries the step's feedback (shake, ×0.06 dip, kombo, "×d"); each block's peak its own puff, points, burst and sticker.
        /// A trigger step (a special was tapped) follows the look's plan: each cell's swell starts at its planned time, each special fires at
        /// its own (Pota: all at once, every fire at the first peak, before the step's own feedback), the drops wait for the plan's end. A
        /// gathering look sends a birth group into its birth cell instead.
        /// </summary>
        private void StartStep(StepJob st)
        {
            st.Started = true;
            st.SwellEnd = S.ChainTime;
            if (st.Depth == 0 || st.Step.Exploded.Count == 0) { StartDrops(st); return; }
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
            st.Peak = () =>
            {
                if (peaked) return;
                peaked = true;
                var at = first != null ? first.Pos : Vector2.zero;
                Juice?.Chain(d, BoardRoot.TransformPoint(new Vector3(at.x, at.y + 0.5f, -PotaLook.LabelZ)), Style.ShowsChainNumber);
                Style.ChainPeak(d, centre, first != null ? BurstTone(first.Block) : Color.white);
            };
            var gathered = GatherBirthGroups(st);
            if (gathered.Count > 0) st.SwellEnd = Mathf.Max(st.SwellEnd, GatherDuration());
            if (st.Step.Triggered.Count > 0)
            {
                st.Plan = SpecialSchedule.Plan(Board.Size, st.Step, st.Origin, Specials.Timing, S.ChainTime);
                st.SwellEnd = Mathf.Max(st.SwellEnd, st.Plan.End);
                if (Specials.ConsumesSpecialView) foreach (var f in st.Plan.Fires) st.Consumed.Add(f.Cell);
                Specials.TriggerStep(st.Plan.Fires.Count, centre);
            }
            foreach (int i in st.Step.Exploded)
            {
                if (gathered.Contains(i)) continue;
                var v = ViewAt(i);
                if (v == null) continue;
                if (first == null) first = v;
                v.Exploding = true;
                if (st.Consumed.Contains(i)) continue;              // a firing special the look draws itself
                float at = st.Plan != null && st.Plan.Start.TryGetValue(i, out float planned) ? planned : 0f;
                st.Pending.Add(new PendingSwell { Cell = i, View = v, At = at });
            }
            TickPlan(st, 0f);                                      // the swells due at 0 start now, as before the plan existed
            if (first == null) st.Peak();
        }
```

15. Replace `StartDrops` with:
```csharp
        private void StartDrops(StepJob st)
        {
            FlushPlan(st);
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                _swells.End(v);                 // gorselSil: a burst that has not peaked yet runs now
                Recycle(v);
                _views.Remove(i);
            }
            int n = Board.Size;
            // dususleriBaslat: born specials take their cell's view (it then falls with its column), golden balls leave for the hoop
            // 0.12 s apart, newly opened gates light up — all before the columns fall.
            foreach (var b in st.Step.Born)
            {
                var v = ViewAt(b.Cell);
                if (v == null) continue;
                v.SetBlock(Block.MakeSpecial(b.Kind), b.Cell);
                Specials.Birth(v, b.Kind);
            }
            for (int k = 0; k < st.Step.ToBasket.Count; k++)
            {
                var bd = st.Step.ToBasket[k];
                var v = ViewAt(bd.Cell);
                var at = v != null ? v.Pos : BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, bd.Cell), ColorHuntRules.Col(n, bd.Cell));
                if (v != null) { _views.Remove(bd.Cell); Recycle(v); }
                LaunchGolden(at, bd.Col, k * GoldenPath.Gap);
            }
            OpenGates(st.Step.Opened);
            float longest = 0f;
            var moved = new Dictionary<int, BlockView>();
            foreach (var d in st.Step.Drops)
            {
                var v = ViewAt(d.From);
                if (v == null) continue;
                _views.Remove(d.From);
                int dist = ColorHuntRules.Row(n, d.To) - ColorHuntRules.Row(n, d.From);
                v.Home = BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, d.To), ColorHuntRules.Col(n, d.To));
                v.Cell = d.To;
                _drops[v] = new DropState { StartY = v.Pos.y, Distance = Mathf.Max(0, dist) };
                longest = Mathf.Max(longest, dist);
                moved[d.To] = v;
            }
            foreach (var sp in st.Step.Spawns)
            {
                int row = ColorHuntRules.Row(n, sp.To), col = ColorHuntRules.Col(n, sp.To);
                var v = NewView(sp.Block, sp.To, BoardBuilder.CellPosition(n, row, col));
                var start = BoardBuilder.CellPosition(n, row - sp.Distance, col);
                v.Pos = start;
                _drops[v] = new DropState { StartY = start.y, Distance = sp.Distance };
                longest = Mathf.Max(longest, sp.Distance);
                moved[sp.To] = v;
            }
            foreach (var kv in moved) _views[kv.Key] = kv.Value;
            st.Dropping = true;
            st.T = 0f;
            st.DropTime = PotaLook.StepDropTime(longest, S.DropTimePerCell);
        }
```
(From `float longest` down it is today's method.)

16. `ClearViews`: after `_loose.Clear();` add:
```csharp
            foreach (var v in _gathering) Recycle(v);
            _gathering.Clear();
```
(`ClearViews` also runs at the end of a turn; golden balls cannot fly then — `Busy` — and the golden bookkeeping is reset only by
`Deal` → `ResetSpecials`, so a turn never forgets a landing.)

- [ ] **Step 8: Run the EditMode tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialScheduleTests Project.Tests.GoldenPathTests Project.Tests.SpecialStyleTests Project.Tests.SwellSetTests Project.Tests.BurstStyleTests Project.Tests.PotaSoundsTests Project.Tests.ColorHuntJuiceTests Project.Tests.GiantHoopTests Project.Tests.RuntimeShaderBuildTests`
Expected: all pass. A failure in `MutualChain_FiresEachOnce` or `Chain_FiresWhenReached_AndItsAreaFollows` is a schedule bug (do not
loosen the test); a failure only in the cells listed by `Blast` is Task 2's (`SingleArea`/`Blast` must be Pota's `tekAlan`/`patlamaAlani`).

- [ ] **Step 9: Write the PlayMode tests**

`Tests/PlayMode/SpecialPlayTests.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>Special taps, births, golden flights, the lock and resets in the Color Hunt scene as saved (Pota look), on staged boards.</summary>
    public class SpecialPlayTests
    {
        /// <summary>
        /// 7×7 colour (2r + c) mod 5 — no two neighbours alike, nothing matches by accident — hp 3 + 7i mod 12, rng 1, all specials on.
        /// The same boards as Task 6's presets (Pota's own renkAvi.ts gave the outcomes during planning).
        /// </summary>
        internal static Board Board7(int[] gates, params (int cell, SpecialKind kind)[] specials) => Board7(null, gates, specials);

        internal static Board Board7(int[] colors, int[] gates, params (int cell, SpecialKind kind)[] specials)
        {
            var cells = new Block[49];
            for (int i = 0; i < 49; i++) cells[i] = new Block(colors != null ? colors[i] : (2 * (i / 7) + i % 7) % 5, 3 + i * 7 % 12);
            foreach (var (cell, kind) in specials) cells[cell] = Block.MakeSpecial(kind);
            var b = new Board
            {
                Size = 7, Cells = cells, Ammo = 158, Targets = new[] { new Target(0, 22), new Target(1, 16) },
                ColorCount = 5, HpMin = 3, HpMax = 14, Rng = 1, Features = Features.All,
            };
            b.Gates.AddRange(gates);
            return b;
        }

        /// <summary>Task 6's Birth grid: tap 44 → a 5-run (golden at 43) and, as column 2 closes up, a 4-run in row 4 (LineV at 29).</summary>
        private static readonly int[] BirthGrid =
        {
            0, 1, 2, 3, 4, 0, 1,
            2, 3, 4, 0, 1, 2, 3,
            4, 0, 1, 2, 2, 4, 1,
            1, 2, 3, 2, 0, 1, 1,
            3, 3, 0, 3, 2, 2, 4,
            0, 1, 0, 3, 2, 0, 1,
            0, 0, 4, 0, 0, 2, 1,
        };

        private static IEnumerator Load(Action<ColorHuntGame> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            game.SetLook(PuzzleLook.Pota);
            game.SetSpecialLook(SpecialLook.Pota);
            ready(game);
        }

        private static void Deal(ColorHuntGame game, Board b)
        {
            game.Stage(b);
            game.Begin();
        }

        private static int FirstNormal(Board b)
        {
            for (int i = 0; i < b.Cells.Length; i++) if (!b.Cells[i].IsSpecial && b.Cells[i].Hp <= b.Ammo) return i;
            return -1;
        }

        private static void AssertViewsShowTheBoard(ColorHuntGame game, string what)
        {
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsNotNull(v, $"{what}: no view at {i}");
                Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
                Assert.AreEqual(game.Board.Cells[i].Color, v.Block.Color, $"{what}: cell {i} colour");
            }
        }

        [UnityTest]
        public IEnumerator SpecialTap_BurstsExactlyTheRulesArea_ForFree()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(new int[0], (21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV)));
            var expected = ColorHuntRules.Blast(game.Board, 21).Cells;
            Assert.AreEqual(17, expected.Count, "row 3 + the bomb's 3×3 + column 5");
            var burst = new List<int>();
            game.CellBurst += burst.Add;
            int ammo = game.Board.Ammo;
            Assert.AreEqual("broken", game.Tap(21));
            Assert.IsFalse(game.Salvo.Busy, "a special fires without a salvo");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            game.CellBurst -= burst.Add;
            Assert.IsFalse(game.Busy);
            Assert.GreaterOrEqual(burst.Count, expected.Count);
            CollectionAssert.AreEquivalent(expected, burst.GetRange(0, expected.Count), "the trigger step bursts the rules' area, each cell once");
            Assert.AreEqual(ammo, game.Board.Ammo, "free");
            Assert.AreEqual(ammo, game.DisplayedAmmo);
            Assert.GreaterOrEqual(game.LongestChain, 1, "a special's blast is depth 1");
            AssertViewsShowTheBoard(game, "after the chain");
        }

        [UnityTest]
        public IEnumerator Birth_ShowsTheSpecial_AndItFallsWithItsColumn()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(BirthGrid, new int[0]));
            Assert.AreEqual("broken", game.Tap(44));
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 8f);
            Assert.IsFalse(game.Busy);
            int lines = 0, goldens = 0;
            foreach (var c in game.Board.Cells)
            {
                if (c.Special == SpecialKind.LineV) lines++;
                if (c.Special == SpecialKind.Golden) goldens++;
            }
            Assert.AreEqual(1, lines, "the 4-run in row 4 left a line ball");
            Assert.AreEqual(1, goldens, "the 5-run in row 6 left a golden ball");
            AssertViewsShowTheBoard(game, "after the births");
            CollectionAssert.AreEqual(game.Board.Gates, game.Gates.Shown, "the golden birth opened a gate, and its ring shows");
        }

        [UnityTest]
        public IEnumerator Tap_WhileGoldenFlies_IsLocked()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(new[] { 3, 5 }, (38, SpecialKind.Golden), (47, SpecialKind.Golden)));
            CollectionAssert.AreEqual(new[] { 3, 5 }, game.Gates.Shown);
            Assert.AreEqual("golden", game.Tap(47), "a golden ball leaves only through its gate");
            Assert.IsFalse(game.Busy, "the refusal starts nothing");
            Assert.AreEqual("broken", game.Tap(45), "the block under the other golden ball");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 4f);
            Assert.AreEqual(2, game.GoldenInFlight, "both golden balls of the basket step are in the air");
            Assert.AreEqual(2, game.Specials.LiveCount, "the Pota look draws both");
            Assert.IsTrue(game.Busy);
            Assert.AreEqual("locked", game.Tap(10));
            Assert.IsFalse(game.Rotate(Rotation.Clockwise), "no turn while a golden ball flies");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight == 0, 4f);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(2, game.GoldenLanded, "each ball lands once");
            Assert.IsNotNull(game.Hoop, "PuzzleStage builds the giant hoop under the game root");
            Assert.IsTrue(game.Hoop.Swinging, "fileSalla: the net swings on arrival");
            CollectionAssert.IsEmpty(game.Gates.Shown, "each ring closed as its ball passed");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.AreEqual(2, game.GoldenLanded, "nothing landed twice");
            Assert.AreEqual(2, game.Board.Golden);
            Assert.AreEqual("broken", game.Tap(FirstNormal(game.Board)), "unlocked once everything landed");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 10f);
        }

        [UnityTest]
        public IEnumerator Stage_WhileEffectsRun_ClearsEverything()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            var chain = Board7(new int[0], (21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV));
            // Golden balls in the air, then a new board.
            Deal(game, Board7(new[] { 3, 5 }, (38, SpecialKind.Golden), (47, SpecialKind.Golden)));
            Assert.AreEqual("broken", game.Tap(45));
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 4f);
            Assert.Greater(game.Specials.LiveCount, 0);
            Deal(game, chain);
            Assert.AreEqual(0, game.GoldenInFlight, "nothing flies over the new board");
            Assert.AreEqual(0, game.GoldenLanded);
            Assert.AreEqual(0, game.Specials.LiveCount, "the style dropped its balls");
            Assert.IsFalse(game.Busy, "the lock is released at once");
            CollectionAssert.IsEmpty(game.Gates.Shown, "the new board has no gates");
            yield return StudioLookPlayTests.WaitUntil(() => false, 1.5f);         // the old balls would have landed by now
            Assert.AreEqual(0, game.GoldenLanded, "no callback reached the new board");
            // A trigger plan running, then a new board.
            Assert.AreEqual("broken", game.Tap(21));
            yield return null;
            yield return null;
            Assert.IsTrue(game.Busy, "the trigger step runs");
            Deal(game, chain);
            Assert.IsFalse(game.Busy);
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);
            Assert.AreEqual(SpecialKind.LineH, game.Board.Cells[21].Special, "no fire of the old plan touched the new board");
            Assert.AreEqual("broken", game.Tap(21), "the new board plays");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.IsFalse(game.Busy);
            AssertViewsShowTheBoard(game, "after the replay");
        }

        [UnityTest]
        public IEnumerator Rotate_GoldenOnGate_LandsInTheBasket()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(new[] { 3 }, (27, SpecialKind.Golden)));              // (3, 6) turns clockwise onto (6, 3), above gate 3
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            Assert.AreEqual(1, game.Board.Golden, "dondurVeOtur: the settled board already counts it");
            yield return StudioLookPlayTests.WaitUntil(() => Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity) > 30f, 2f);
            Assert.Greater(Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity), 30f, "caught mid-turn");
            Assert.IsTrue(game.Gates.transform.rotation == game.transform.rotation, "the rings stay on the frame");
            CollectionAssert.Contains(game.Gates.Shown, 3, "open until the ball passes");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 3f);
            Assert.AreEqual(1, game.GoldenInFlight, "the turn settled into a basket step");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight == 0, 3f);
            Assert.AreEqual(1, game.GoldenLanded);
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            CollectionAssert.DoesNotContain(game.Gates.Shown, 3, "closed when its ball passed");
            CollectionAssert.AreEquivalent(game.Board.Gates, game.Gates.Shown, "settled: the rings show the rules' gates");
            Assert.AreEqual(1, game.Rotations);
            AssertViewsShowTheBoard(game, "after the turn");
        }
    }
}
```

- [ ] **Step 10: Run the PlayMode tests, then every suite**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.SpecialPlayTests
~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile
~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile
```
Expected: `SpecialPlayTests` 5/5; the full EditMode and PlayMode suites pass (Color Hunt, PotaFlow, Candy/Shatter, the HUD flights,
`SpecialBoardPlayTests`, `SceneBootTests`): with `ColorHuntSettings.Specials` off nothing is born, triggered or flies, the queue depths
are the old ones, `Rotate` settles into zero steps and draws no rng.

- [ ] **Step 11: Look at it**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
cat > "$S/special-look.sh" <<'SH'
#!/bin/zsh
# Plays ColorHunt in slow motion: the Chain board (line → bomb → column) tapped, then two golden balls through their gates.
set -u
cd "/Users/tunaaltin17/projects/Unity MCP"
S="${0:A:h}"
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
shot() { M call manage_camera '{"action":"screenshot","capture_source":"game_view","include_image":false,"output_folder":"Temp/mcp_out"}' | grep -o '[^" ]*\.png' | head -1; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
python3 - "$S" <<'EOF'
import json, sys
d = sys.argv[1]
open_scene = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
              'if (s.path != "Assets/_Project/Games/Pota/Scenes/ColorHunt.unity") { if (s.isDirty) return "dirty: " + s.path; '
              'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Games/Pota/Scenes/ColorHunt.unity"); } '
              'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
pre = ('var game = UnityEngine.Object.FindAnyObjectByType<Project.ColorHunt.ColorHuntGame>(); '
       'var b = Project.ColorHunt.ColorHuntRules.Create(game.S.ToDesign(), game.S.Ammo, 7u); b.Features = Project.ColorHunt.Features.All; ')
k = 'Project.ColorHunt.Block.MakeSpecial(Project.ColorHunt.SpecialKind.'
chain = pre + ('b.Cells[21] = ' + k + 'LineH); b.Cells[25] = ' + k + 'Bomb); b.Cells[19] = ' + k + 'LineV); '
               'game.Stage(b); game.Begin(); game.Juice.TimeCtl.SlowMo(0.15f, 60f); return game.Tap(21);')
golden = pre + ('b.Cells[38] = ' + k + 'Golden); b.Cells[47] = ' + k + 'Golden); b.Gates.Add(3); b.Gates.Add(5); '
                'game.Stage(b); game.Begin(); game.Juice.TimeCtl.SlowMo(0.25f, 60f); return game.Tap(45);')
for name, code in (("open-colorhunt", open_scene), ("tap-chain", chain), ("tap-golden", golden)):
    json.dump({"action": "execute", "code": code, "safety_checks": True}, open(f"{d}/{name}.json", "w"))
EOF
M call execute_code @"$S/open-colorhunt.json" | grep '"result"' | grep -q ColorHunt || { echo "could not open ColorHunt (unsaved scene?)"; exit 1; }
M call manage_editor '{"action":"play"}' > /dev/null
for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
M call execute_code @"$S/tap-chain.json" | grep '"result"'
for i in 1 2 3 4; do shot; done
sleep 3
M call execute_code @"$S/tap-golden.json" | grep '"result"'
sleep 4
for i in 1 2 3 4; do shot; done
M call manage_editor '{"action":"stop"}' > /dev/null
SH
zsh "$S/special-look.sh"
```
Expected: two `"result": "broken"` lines and eight PNG paths under `Temp/mcp_out/`. Open them (Read tool) and check against Pota's
`adimTepe` / `altinlariIlerlet`: in the first four, the bomb's big warm-white flash and ring around cell (3, 4), three smaller cream
flashes at both ends and the centre of row 3 and of column 5, every block of row 3, the 3×3 and column 5 swelling together with the
normal Pota burst (pieces in the specials' orange / smoke tones, not red); in the last four, a gold ball (bigger than a salvo ball,
its seams dark) under the board on its way through the ring of column 4 or 6, then arcing toward the rim, a yellow flash at the ring
as it passes and at the rim on arrival, the second ball trailing ~0.12 s behind. A red burst for a special, a ball that does not go
through its ring, a ring that stays lit after its ball passed, or flashes at the wrong end of a line are defects: fix them before
committing.

- [ ] **Step 12: Commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
P=Assets/_Project/Games/Pota
git add $P/Scripts/ColorHunt/Specials/ISpecialStyle.cs* $P/Scripts/ColorHunt/Specials/SpecialSchedule.cs* \
  $P/Scripts/ColorHunt/Specials/GoldenPath.cs* $P/Scripts/ColorHunt/Specials/PotaSpecials.cs* \
  $P/Scripts/ColorHunt/ColorHuntGame.cs $P/Scripts/ColorHunt/ColorHuntFx.cs $P/Scripts/ColorHunt/ColorHuntJuice.cs \
  $P/Scripts/ColorHunt/PotaSounds.cs $P/Scripts/ColorHunt/SwellSet.cs $P/Scripts/Flow/GiantHoop.cs \
  $P/Tests/EditMode/SpecialScheduleTests.cs* $P/Tests/EditMode/GoldenPathTests.cs* $P/Tests/EditMode/SpecialStyleTests.cs* \
  $P/Tests/EditMode/SwellSetTests.cs $P/Tests/PlayMode/SpecialPlayTests.cs*
git status --short    # nothing else from this task should be left
git commit -m "feat: Color Hunt special balls play — free special tap, trigger plan (SpecialSchedule), births, golden flight through the gates into the hoop, ISpecialStyle with Pota's look 1:1

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Demo scene and presets

**Files:**
- Create: `Scripts/ColorHunt/Specials/SpecialScenarios.cs` (`SpecialScenario`, `BoardPreset`, `SpecialScenarios`),
  `Scripts/ColorHunt/Specials/SpecialsDemo.cs`, `Editor/SpecialsSceneTool.cs`, `Scenes/Specials.unity` (by the tool)
- Modify: `Scripts/ColorHunt/ColorHuntRules.Specials.cs` (`FromPreset`), `Editor/ColorHuntSceneTool.cs` (`BuildPuzzleScene`),
  `Editor/SceneParts.cs` (`AddToProfile`), `Editor/PotaFlowSceneTool.cs` (uses it), `PotaProfile.asset` and
  `ProjectSettings/EditorBuildSettings.asset` (by the tool)
- Test: `Tests/EditMode/SpecialScenariosTests.cs`, `Tests/EditMode/SpecialsSceneToolTests.cs`, `Tests/PlayMode/SpecialsDemoPlayTests.cs`
- Output: `docs/screenshots/2026-10-01-specials-demo.jpg`, `docs/screenshots/2026-10-01-specials-golden.jpg`

(Paths under `Assets/_Project/Games/Pota/` unless they start with `ProjectSettings/` or `docs/`.)

**Interfaces:**
- Consumes: Task 2 (`Board`, `Block.MakeSpecial`, `Features`, `Break`, `FindMatches`), Task 4 (`Stage`, `Gates.Shown`), Task 5
  (`SetSpecialLook`, `GoldenLanded`, `GoldenInFlight`, `Busy`, `ItemsInFlight`, `SpecialSchedule`), `ColorHuntHud.PanelSprite/Css`,
  `ColorHuntSceneTool.EnsureSettings/BuildGame`, `SceneParts`, `GameViewPortrait`, `AIToolsCommon`.
- Produces: the "Demo" block of the shared contract plus the Task 6 lines of **Contract additions**.
- The presets were checked during planning by running Pota's own `src/renkAvi.ts` (scratch copy, Node type stripping) on these exact
  grids with rng 1: every event the tests pin below is Pota's outcome. Grid colours are the base pattern `(2r + c) mod 5` (no two
  neighbours alike, so nothing matches unless designed); `-1` marks a special's cell (its kind comes from `Specials`).
- Rulings: (1) the demo's UI is built by `SpecialsDemo` at runtime (no serialized UI, no Edit-Mode listeners); (2) the scene is the
  Color Hunt scene's contents (`BuildPuzzleScene`) with `AutoStart` off, the HUD hint hidden and a `SpecialsDemo` object; (3) scripted
  taps wait for a settled board (`!Busy`, no sticker in the air) plus `Beat` 0.45 s of game time, so the frame recorder can step it.

- [ ] **Step 1: Write the failing EditMode tests**

`Tests/EditMode/SpecialScenariosTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;

namespace Project.Tests
{
    /// <summary>
    /// The demo's hand-authored boards (Decision 6): each scripted tap produces its scenario's event. The pinned outcomes are what Pota's
    /// own src/renkAvi.ts gives on these grids with rng 1 (checked while planning). If one fails, suspect the rules port first (Task 2's
    /// fixtures); change a grid only once the port is proven right, and keep the test.
    /// </summary>
    public class SpecialScenariosTests
    {
        private static List<BreakResult> PlayAll(SpecialScenario s, out Board end)
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(s));
            var results = new List<BreakResult>();
            foreach (int cell in SpecialScenarios.Taps(s))
            {
                var r = ColorHuntRules.Break(b, cell);
                Assert.IsTrue(r.Broken, $"{s}: tap {cell} was not accepted ({r.Outcome})");
                results.Add(r);
                b = r.Board;
            }
            end = b;
            return results;
        }

        private static List<string> Births(BreakResult r)
        {
            var list = new List<string>();
            foreach (var step in r.Steps) foreach (var b in step.Born) list.Add($"{b.Cell}:{b.Kind}");
            return list;
        }

        private static List<string> Triggers(Step step)
        {
            var list = new List<string>();
            foreach (var t in step.Triggered) list.Add($"{t.Cell}:{t.Kind}");
            return list;
        }

        private static int[] Range(int from, int count, int stride = 1)
        {
            var a = new int[count];
            for (int i = 0; i < count; i++) a[i] = from + i * stride;
            return a;
        }

        [Test]
        public void EveryPreset_StartsSettled_WithItsSpecialsAndGates([Values] SpecialScenario s)
        {
            var p = SpecialScenarios.Board(s);
            Assert.AreEqual(SpecialScenarios.Size * SpecialScenarios.Size, p.Colors.Length);
            Assert.AreEqual(p.Colors.Length, p.Specials.Length);
            for (int i = 0; i < p.Colors.Length; i++)
                Assert.AreEqual(p.Colors[i] < 0, p.Specials[i] != SpecialKind.None, $"{s}: cell {i}: -1 in the grid ⇔ a special");
            var b = ColorHuntRules.FromPreset(p);
            Assert.IsEmpty(ColorHuntRules.FindMatches(b), $"{s}: a preset starts with no run");
            Assert.IsTrue(b.Features.Line && b.Features.Bomb && b.Features.Golden);
            Assert.AreEqual(1u, b.Rng);
            int goldens = 0;
            foreach (var c in b.Cells) if (c.Special == SpecialKind.Golden) goldens++;
            Assert.LessOrEqual(goldens, b.Gates.Count, $"{s}: every golden ball has a gate");
            foreach (int cell in SpecialScenarios.Taps(s)) Assert.That(cell, Is.InRange(0, 48));
            Assert.IsFalse(string.IsNullOrEmpty(SpecialScenarios.Title(s)));
        }

        [Test]
        public void FromPreset_BuildsThePresetsBoard()
        {
            var p = SpecialScenarios.Board(SpecialScenario.Golden);
            var b = ColorHuntRules.FromPreset(p);
            Assert.AreEqual(7, b.Size);
            Assert.AreEqual(158, b.Ammo);
            Assert.AreEqual(SpecialKind.Golden, b.Cells[38].Special);
            Assert.AreEqual(ColorHuntRules.SpecialColor, b.Cells[38].Color);
            Assert.AreEqual(0, b.Cells[38].Hp);
            Assert.AreEqual(new Block(1, 3 + 1 * 7 % 12), b.Cells[1], "colour from the grid, hp HpMin + 7i mod 12");
            CollectionAssert.AreEqual(new[] { 3, 5 }, b.Gates);
            p.Gates[0] = 6;
            CollectionAssert.AreEqual(new[] { 3, 5 }, b.Gates, "the board keeps its own copy");
            Assert.AreEqual(2, b.Targets.Length);
            Assert.AreEqual(new Target(0, 22), b.Targets[0]);
            Assert.AreEqual(0, b.Golden);
        }

        [Test]
        public void Birth_BearsALineVAGoldenALineHAndABomb()
        {
            var r = PlayAll(SpecialScenario.Birth, out var end);
            Assert.AreEqual(3, r.Count);
            CollectionAssert.AreEquivalent(new[] { "29:LineV", "43:Golden" }, Births(r[0]), "tap 44: the 5-run and, as column 2 closes up, row 4's 4-run");
            var opened = new List<int>();
            foreach (var step in r[0].Steps) opened.AddRange(step.Opened);
            CollectionAssert.AreEqual(new[] { 3 }, opened, "the golden ball opens a gate; Pota's rng (mulberry32, seed 1) picks column 3");
            CollectionAssert.AreEqual(new[] { "48:LineH" }, Births(r[1]), "tap 34: column 6 closes up into a vertical 4-run");
            CollectionAssert.AreEqual(new[] { "32:Bomb" }, Births(r[2]), "tap 32: a T of 2s");
            var kinds = new HashSet<SpecialKind>();
            foreach (var c in end.Cells) if (c.IsSpecial) kinds.Add(c.Special);
            CollectionAssert.AreEquivalent(new[] { SpecialKind.LineV, SpecialKind.LineH, SpecialKind.Bomb, SpecialKind.Golden }, kinds, "all four stay on the board");
        }

        [Test]
        public void Line_FiresTheRowThenTheColumn()
        {
            var r = PlayAll(SpecialScenario.Line, out var end);
            CollectionAssert.AreEqual(new[] { "22:LineH" }, Triggers(r[0].Steps[0]));
            CollectionAssert.AreEqual(Range(21, 7), r[0].Steps[0].Exploded, "row 3");
            CollectionAssert.AreEqual(new[] { "26:LineV" }, Triggers(r[1].Steps[0]), "the column ball fell one row with the row's collapse");
            CollectionAssert.AreEqual(Range(5, 7, 7), r[1].Steps[0].Exploded, "column 5");
            Assert.AreEqual(0, r[0].Cost + r[1].Cost, "free");
        }

        [Test]
        public void Bomb_BurstsItsThreeByThree_AndTheCornerClips()
        {
            var r = PlayAll(SpecialScenario.Bomb, out _);
            CollectionAssert.AreEqual(new[] { 16, 17, 18, 23, 24, 25, 30, 31, 32 }, r[0].Steps[0].Exploded);
            CollectionAssert.AreEqual(new[] { "24:Bomb" }, Triggers(r[0].Steps[0]));
            CollectionAssert.AreEqual(new[] { 40, 41, 47, 48 }, r[1].Steps[0].Exploded, "tekAlan clips at the corner");
        }

        [Test]
        public void Chain_LineSetsOffTheBomb_WhichSetsOffTheColumn()
        {
            var r = PlayAll(SpecialScenario.Chain, out _);
            var step = r[0].Steps[0];
            CollectionAssert.AreEqual(new[] { "19:LineV", "21:LineH", "25:Bomb" }, Triggers(step), "patlamaAlani: sorted by cell");
            CollectionAssert.AreEqual(new[] { 5, 12, 17, 18, 19, 21, 22, 23, 24, 25, 26, 27, 31, 32, 33, 40, 47 }, step.Exploded);
            var plan = SpecialSchedule.Plan(7, step, 21, SpecialTiming.Pota, 0.15f);
            Assert.AreEqual(3, plan.Fires.Count);
            Assert.AreEqual(21, plan.Fires[0].Cell, "the tapped line first");
        }

        [Test]
        public void Golden_BothBallsDropThroughTheirGates()
        {
            var r = PlayAll(SpecialScenario.Golden, out var end);
            Assert.AreEqual(2, r[0].Steps.Count, "the tap, then the basket");
            var basket = r[0].Steps[1];
            Assert.IsEmpty(basket.Exploded, "a pure basket step");
            Assert.AreEqual(2, basket.ToBasket.Count);
            Assert.AreEqual(45, basket.ToBasket[0].Cell, "the ball above the tapped block fell onto gate 3");
            Assert.AreEqual(3, basket.ToBasket[0].Col);
            Assert.AreEqual(47, basket.ToBasket[1].Cell);
            Assert.AreEqual(5, basket.ToBasket[1].Col);
            Assert.AreEqual(2, end.Golden);
            CollectionAssert.IsEmpty(end.Gates, "no other golden ball in those columns: both gates close");
        }

        [Test]
        public void Free_HasOneOfEachSpecial_AndNoScript()
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(SpecialScenario.Free));
            Assert.AreEqual(SpecialKind.LineV, b.Cells[8].Special);
            Assert.AreEqual(SpecialKind.Golden, b.Cells[18].Special);
            Assert.AreEqual(SpecialKind.Bomb, b.Cells[24].Special);
            Assert.AreEqual(SpecialKind.LineH, b.Cells[40].Special);
            CollectionAssert.AreEqual(new[] { 4 }, b.Gates, "the golden ball's column");
            Assert.IsEmpty(SpecialScenarios.Taps(SpecialScenario.Free), "Tuna plays it");
        }
    }
}
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialScenariosTests`
Expected: exit 2, `error CS0246: The type or namespace name 'SpecialScenario' could not be found` (and `BoardPreset`,
`SpecialScenarios`), `error CS0117: 'ColorHuntRules' does not contain a definition for 'FromPreset'`.

- [ ] **Step 3: Presets — `BoardPreset`, `SpecialScenarios`, `FromPreset`**

`Scripts/ColorHunt/Specials/SpecialScenarios.cs`:
```csharp
namespace Project.ColorHunt
{
    public enum SpecialScenario { Birth, Line, Bomb, Chain, Golden, Free }

    /// <summary>A hand-authored board (the demo's scenarios, tests): turned into a Board by ColorHuntRules.FromPreset.</summary>
    public sealed class BoardPreset
    {
        public int Size = 7;
        public int ColorCount = 5;
        public int HpMin = 3, HpMax = 14;
        public int Ammo = 158;
        /// <summary>Row by row, row 0 at the top; -1 under a special (its kind is in <see cref="Specials"/>).</summary>
        public int[] Colors;
        /// <summary>Null: HpMin + (7·i mod (HpMax − HpMin + 1)).</summary>
        public int[] Hp;
        public SpecialKind[] Specials;
        /// <summary>Open gates (columns).</summary>
        public int[] Gates;
        public Features Features = Features.All;
        public uint Rng = 1;
        public (int color, int count)[] Targets = { (0, 22), (1, 16) };
    }

    /// <summary>
    /// The demo's six boards (spec §1.3) and their scripted taps. Every grid is the base pattern (2r + c) mod 5 — no two neighbours alike,
    /// so nothing matches unless designed — with its specials (-1) and, for Birth, designed runs. The outcomes were checked against Pota's
    /// own src/renkAvi.ts (rng 1) and are pinned by SpecialScenariosTests.
    /// </summary>
    public static class SpecialScenarios
    {
        public const int Size = 7;

        /// <summary>
        /// Tap 44 (6, 2): the 0 above drops in → row 6 reads 0 0 0 0 0 → a golden ball at (6, 1) (it opens a gate: rng, column 3); column 2
        /// closing up lines row 4 up as 3 3 3 3 → a line ball (LineV) at (4, 1). Tap 34 (4, 6): column 6 closes up 1 1 1 1 → LineH at (6, 6).
        /// Tap 32 (4, 4): the 2 above drops between two 2s and onto two 2s → a T → a bomb at (4, 4).
        /// </summary>
        private static readonly int[] BirthGrid =
        {
            0, 1, 2, 3, 4, 0, 1,
            2, 3, 4, 0, 1, 2, 3,
            4, 0, 1, 2, 2, 4, 1,
            1, 2, 3, 2, 0, 1, 1,
            3, 3, 0, 3, 2, 2, 4,
            0, 1, 0, 3, 2, 0, 1,
            0, 0, 4, 0, 0, 2, 1,
        };

        /// <summary>LineH at (3, 1) clears row 3; the LineV at (2, 5) falls to (3, 5) and clears column 5.</summary>
        private static readonly int[] LineGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3, -1,  0,
             1, -1,  3,  4,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        /// <summary>A bomb in the middle (3, 3), one in the corner (6, 6) whose 3×3 is clipped to 2×2.</summary>
        private static readonly int[] BombGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3,  4,  0,
             1,  2,  3, -1,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2, -1,
        };

        /// <summary>LineH (3, 0) → the bomb (3, 4) in its row → the LineV (2, 5) in the bomb's 3×3 → column 5.</summary>
        private static readonly int[] ChainGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3, -1,  0,
            -1,  2,  3,  4, -1,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        /// <summary>Golden balls at (5, 3) and (6, 5), gates 3 and 5: tapping (6, 3) drops the first onto its gate; both fly, 0.12 s apart.</summary>
        private static readonly int[] GoldenGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3,  4,  0,
             1,  2,  3,  4,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2, -1,  4,  0,  1,
             2,  3,  4,  0,  1, -1,  3,
        };

        /// <summary>One of each: LineV (1, 1), golden (2, 4) over gate 4, bomb (3, 3), LineH (5, 5). No script: Tuna plays.</summary>
        private static readonly int[] FreeGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2, -1,  4,  0,  1,  2,  3,
             4,  0,  1,  2, -1,  4,  0,
             1,  2,  3, -1,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4, -1,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        public static string Title(SpecialScenario s)
        {
            switch (s)
            {
                case SpecialScenario.Birth: return "Doğuş";
                case SpecialScenario.Line: return "Çizgi";
                case SpecialScenario.Bomb: return "Bomba";
                case SpecialScenario.Chain: return "Zincir";
                case SpecialScenario.Golden: return "Altın";
                default: return "Serbest";
            }
        }

        /// <summary>A fresh preset (callers may change it).</summary>
        public static BoardPreset Board(SpecialScenario s)
        {
            switch (s)
            {
                case SpecialScenario.Birth: return Preset(BirthGrid, new int[0]);
                case SpecialScenario.Line: return Preset(LineGrid, new int[0], (22, SpecialKind.LineH), (19, SpecialKind.LineV));
                case SpecialScenario.Bomb: return Preset(BombGrid, new int[0], (24, SpecialKind.Bomb), (48, SpecialKind.Bomb));
                case SpecialScenario.Chain: return Preset(ChainGrid, new int[0], (21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV));
                case SpecialScenario.Golden: return Preset(GoldenGrid, new[] { 3, 5 }, (38, SpecialKind.Golden), (47, SpecialKind.Golden));
                default: return Preset(FreeGrid, new[] { 4 }, (8, SpecialKind.LineV), (18, SpecialKind.Golden), (24, SpecialKind.Bomb), (40, SpecialKind.LineH));
            }
        }

        /// <summary>The scripted taps, in order (cells as they are when each tap comes).</summary>
        public static int[] Taps(SpecialScenario s)
        {
            switch (s)
            {
                case SpecialScenario.Birth: return new[] { 44, 34, 32 };
                case SpecialScenario.Line: return new[] { 22, 26 };
                case SpecialScenario.Bomb: return new[] { 24, 48 };
                case SpecialScenario.Chain: return new[] { 21 };
                case SpecialScenario.Golden: return new[] { 45 };
                default: return new int[0];
            }
        }

        private static BoardPreset Preset(int[] grid, int[] gates, params (int cell, SpecialKind kind)[] specials)
        {
            var kinds = new SpecialKind[grid.Length];
            foreach (var (cell, kind) in specials) kinds[cell] = kind;
            return new BoardPreset { Colors = (int[])grid.Clone(), Specials = kinds, Gates = (int[])gates.Clone() };
        }
    }
}
```

`Scripts/ColorHunt/ColorHuntRules.Specials.cs` (Task 2's partial), inside `public static partial class ColorHuntRules` (add
`using System;` and `using System.Collections.Generic;` at the top if the file lacks them):
```csharp
        /// <summary>
        /// A hand-authored board (demo presets, tests): colours row by row (row 0 at the top), hp (null: HpMin + 7·i mod the span), a
        /// special where Specials says so (its colour and hp are ignored), the open gates, the rng state. Nothing is resolved: a preset
        /// must start without runs.
        /// </summary>
        public static Board FromPreset(BoardPreset p)
        {
            int n = p.Size, count = n * n;
            if (p.Colors == null || p.Colors.Length != count) throw new ArgumentException($"BoardPreset.Colors needs {count} entries", nameof(p));
            int span = Math.Max(1, p.HpMax - p.HpMin + 1);
            var cells = new Block[count];
            for (int i = 0; i < count; i++)
            {
                var kind = p.Specials != null && i < p.Specials.Length ? p.Specials[i] : SpecialKind.None;
                if (kind != SpecialKind.None) { cells[i] = Block.MakeSpecial(kind); continue; }
                int hp = p.Hp != null && i < p.Hp.Length ? p.Hp[i] : p.HpMin + i * 7 % span;
                cells[i] = new Block(p.Colors[i], hp);
            }
            var targets = new Target[p.Targets.Length];
            for (int t = 0; t < targets.Length; t++) targets[t] = new Target(p.Targets[t].color, p.Targets[t].count);
            var board = new Board
            {
                Size = n, Cells = cells, Ammo = p.Ammo, Targets = targets, ColorCount = p.ColorCount, HpMin = p.HpMin, HpMax = p.HpMax,
                Rng = p.Rng, Features = p.Features,
            };
            if (p.Gates != null) board.Gates.AddRange(p.Gates);
            return board;
        }
```

- [ ] **Step 4: Run the EditMode tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialScenariosTests`
Expected: all pass (`EveryPreset_…` ×6, `FromPreset_…` and the six scenario tests). If a pinned outcome differs, do not edit the expectation: compare
the failing tap with Task 2's fixture tests first (rng consumption, `dogumYeri`, `patlamaAlani` order); only a proven-right port with a
differing outcome justifies changing that grid (rerun the check against Pota in the scratch copy, then update grid, comment and test).

- [ ] **Step 5: Write the failing PlayMode test**

`Tests/PlayMode/SpecialsDemoPlayTests.cs`:
```csharp
using System;
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The demo scene as saved: every scenario auto-plays to its end in every look; switching and replaying mid-effect is safe.</summary>
    public class SpecialsDemoPlayTests
    {
        private static IEnumerator Load(Action<SpecialsDemo> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene");
            Assert.IsNotNull(demo.Game, "the scene tool links the game");
            Assert.IsFalse(demo.Game.AutoStart, "the demo stages its own boards");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, demo.Game.Current, "the start scenario was not staged");
            ready(demo);
        }

        private static IEnumerator PlayToTheEnd(SpecialsDemo demo, SpecialScenario s, float timeout = 25f)
        {
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(s, true);
            yield return StudioLookPlayTests.WaitUntil(() => done, timeout);
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, $"{s} never finished");
            Assert.IsTrue(demo.Finished);
            Assert.IsFalse(demo.AutoPlaying);
        }

        private static void AssertViewsShowTheBoard(ColorHuntGame game, string what)
        {
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsNotNull(v, $"{what}: no view at {i}");
                Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
            }
        }

        [UnityTest]
        public IEnumerator Start_StagesTheBirthScenario()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            Assert.AreEqual(SpecialScenario.Birth, demo.Current);
            StringAssert.Contains("Doğuş", demo.LabelText);
            StringAssert.Contains("Pota", demo.LabelText);
        }

        [UnityTest]
        public IEnumerator EveryScenario_AutoPlaysToTheEnd_InEveryLook([Values] SpecialLook look)
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            demo.SetLook(look);                              // Royal/Court fall back to Pota (one warning) until Tasks 10/11
            Assert.AreEqual(look, demo.Game.SpecialLook);
            foreach (SpecialScenario s in Enum.GetValues(typeof(SpecialScenario)))
            {
                yield return PlayToTheEnd(demo, s);
                var game = demo.Game;
                Assert.IsFalse(game.Busy, $"{look}/{s}");
                Assert.AreEqual(0, game.GoldenInFlight, $"{look}/{s}");
                Assert.AreEqual(s == SpecialScenario.Golden ? 2 : 0, game.GoldenLanded, $"{look}/{s}: golden balls in the hoop");
                AssertViewsShowTheBoard(game, $"{look}/{s}");
                StringAssert.Contains(SpecialScenarios.Title(s), demo.LabelText);
            }
        }

        [UnityTest]
        public IEnumerator SwitchingLookAndReplaying_MidScenario_IsSafe()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Pota);
            // A look switch while two golden balls fly: they land at once, the board unlocks.
            demo.Play(SpecialScenario.Golden, true);
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 8f);
            Assert.AreEqual(2, game.GoldenInFlight);
            demo.SetLook(SpecialLook.Royal);
            Assert.AreEqual(0, game.GoldenInFlight, "landed on the switch");
            Assert.AreEqual(2, game.GoldenLanded, "each counted once");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 6f);
            Assert.IsTrue(demo.Finished);
            Assert.AreEqual(2, game.GoldenLanded, "the old style called nothing back");
            // A look switch in the middle of a trigger step.
            demo.SetLook(SpecialLook.Pota);
            demo.Play(SpecialScenario.Chain, true);
            yield return StudioLookPlayTests.WaitUntil(() => game.Busy, 4f);
            demo.SetLook(SpecialLook.Court);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 8f);
            Assert.IsTrue(demo.Finished);
            AssertViewsShowTheBoard(game, "after the switch");
            // "Tekrar" while golden balls fly: a clean board, nothing lands on it.
            demo.SetLook(SpecialLook.Pota);
            demo.Play(SpecialScenario.Golden, true);
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 8f);
            demo.Play(SpecialScenario.Golden, true);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(0, game.GoldenLanded);
            Assert.IsFalse(game.Busy);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 12f);
            Assert.IsTrue(demo.Finished);
            Assert.AreEqual(2, game.GoldenLanded, "the replay lands its own two, no more");
        }

        [UnityTest]
        public IEnumerator Buttons_AreBoundAtRuntime()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            GameObject.Find("LookRoyal").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialLook.Royal, demo.Game.SpecialLook);
            GameObject.Find("LookPota").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialLook.Pota, demo.Game.SpecialLook);
            GameObject.Find("ScenarioBomb").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialScenario.Bomb, demo.Current);
            Assert.IsTrue(demo.AutoPlaying);
            yield return null;
            StringAssert.Contains("Bomba", demo.LabelText);
            GameObject.Find("Replay").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialScenario.Bomb, demo.Current, "Tekrar replays the current scenario");
            Assert.IsTrue(demo.AutoPlaying);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 15f);
            Assert.IsTrue(demo.Finished);
        }
    }
}
```

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.SpecialsDemoPlayTests`
Expected: exit 2, `error CS0246: The type or namespace name 'SpecialsDemo' could not be found`.

- [ ] **Step 6: `SpecialsDemo`**

`Scripts/ColorHunt/Specials/SpecialsDemo.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// The special-balls showcase (Scenes/Specials.unity, spec §1.3): a row of scenario buttons (Doğuş, Çizgi, Bomba, Zincir, Altın,
    /// Serbest), a row of look buttons (Pota | 1 | 2) with "Tekrar", and a label (look · scenario · golden balls in the hoop), bottom-left,
    /// clear of the rotate arrows. Play stages the scenario's preset and, when auto, taps its script one move at a time, each after the
    /// board has settled plus <see cref="Beat"/> (game time, so the frame recorder can step it). Built in code at runtime in ColorHuntHud's
    /// style: listeners added in Edit Mode are not saved with a scene.
    /// </summary>
    public class SpecialsDemo : MonoBehaviour
    {
        public ColorHuntGame Game;
        [Tooltip("The scenario staged when the scene starts.")]
        public SpecialScenario StartScenario = SpecialScenario.Birth;
        [Tooltip("Play the start scenario's taps by itself.")]
        public bool AutoPlayOnStart = true;
        [Tooltip("Game seconds between a settled board and the next scripted tap.")]
        public float Beat = 0.45f;

        public SpecialScenario Current { get; private set; }
        public bool AutoPlaying { get; private set; }
        /// <summary>The script has been played and the board has settled (no job, no golden ball, no sticker in the air).</summary>
        public bool Finished => _finished;
        public string LabelText => _label != null ? _label.text : "";
        public event Action ScenarioFinished;

        private int[] _taps = new int[0];
        private int _next;
        private float _wait;
        private bool _finished = true, _pending, _shownAny;
        private int _shownLanded;
        private SpecialLook _shownLook;
        private SpecialScenario _shownScenario;
        private Text _label;
        private Sprite _face, _faceOn;
        private readonly Dictionary<SpecialScenario, Image> _scenarioButtons = new Dictionary<SpecialScenario, Image>();
        private readonly Dictionary<SpecialLook, Image> _lookButtons = new Dictionary<SpecialLook, Image>();

        public static string LookTitle(SpecialLook look) => look == SpecialLook.Royal ? "1 · Royal" : look == SpecialLook.Court ? "2 · Basketbol" : "Pota";

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<ColorHuntGame>();
            BuildUi();
            Play(StartScenario, AutoPlayOnStart);
        }

        /// <summary>Stages <paramref name="s"/>'s board (now, or once the game has started) and, if <paramref name="auto"/>, plays its taps.</summary>
        public void Play(SpecialScenario s, bool auto)
        {
            Current = s;
            _taps = SpecialScenarios.Taps(s);
            _next = 0;
            _wait = Beat;
            AutoPlaying = auto && _taps.Length > 0;
            _finished = false;
            _pending = true;
            if (Game != null && Game.BoardRoot != null) StageScenario();
            _shownAny = false;
            RefreshLabel();
        }

        public void SetLook(SpecialLook look)
        {
            if (Game != null) Game.SetSpecialLook(look);
            _shownAny = false;
            RefreshLabel();
        }

        private void StageScenario()
        {
            _pending = false;
            Game.Stage(ColorHuntRules.FromPreset(SpecialScenarios.Board(Current)));
            Game.Begin();
        }

        private void Update()
        {
            if (Game == null) return;
            if (_pending)
            {
                if (Game.BoardRoot == null) return;            // the game's Start has not run yet
                StageScenario();
            }
            RefreshLabel();
            if (_finished) return;
            if (Game.Current == ColorHuntGame.State.Ended) { Finish(); return; }
            if (Game.Current != ColorHuntGame.State.Playing || Game.Busy || Game.ItemsInFlight > 0) { _wait = Beat; return; }
            if (AutoPlaying && _next < _taps.Length)
            {
                _wait -= Time.deltaTime;
                if (_wait > 0f) return;
                int cell = _taps[_next++];
                string r = Game.Tap(cell);
                if (r != "broken") Debug.LogWarning($"[SpecialsDemo] {Current}: the scripted tap on cell {cell} returned \"{r}\".");
                _wait = Beat;
                return;
            }
            Finish();
        }

        private void Finish()
        {
            _finished = true;
            AutoPlaying = false;
            try { ScenarioFinished?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void RefreshLabel()
        {
            if (Game == null || _label == null) return;
            if (_shownAny && _shownLanded == Game.GoldenLanded && _shownLook == Game.SpecialLook && _shownScenario == Current) return;
            _shownAny = true;
            _shownLanded = Game.GoldenLanded;
            _shownLook = Game.SpecialLook;
            _shownScenario = Current;
            _label.text = $"{LookTitle(_shownLook)}  ·  {SpecialScenarios.Title(Current)}  ·  sepette {_shownLanded}";
            foreach (var kv in _scenarioButtons) kv.Value.sprite = kv.Key == Current ? _faceOn : _face;
            foreach (var kv in _lookButtons) kv.Value.sprite = kv.Key == _shownLook ? _faceOn : _face;
        }

        // ---------- UI (Pota CSS px on a 390-px-wide phone, ColorHuntHud.Css canvas units each) ----------

        private void BuildUi()
        {
            var font = Game != null && Game.S != null && Game.S.Font != null ? Game.S.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("SpecialsDemoUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;                                         // over the HUD (0) and the popups (5)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;                                   // width-bound like the HUD
            float css = ColorHuntHud.Css;
            // .dondur's navy face with the gold border; the current choice in the pressed orange.
            _face = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(12f * css), PotaLook.HudButtonTop, PotaLook.HudNavy2, PotaLook.HudGold,
                Mathf.RoundToInt(2f * css), 0.18f, Mathf.RoundToInt(2f * css));
            _faceOn = ColorHuntHud.PanelSprite(128, Mathf.RoundToInt(12f * css), PotaLook.HudOrangeTop, PotaLook.HudOrange, PotaLook.HudGold,
                Mathf.RoundToInt(2f * css), 0f, 0);
            var root = (RectTransform)canvasGo.transform;
            var scenarios = (SpecialScenario[])Enum.GetValues(typeof(SpecialScenario));
            for (int i = 0; i < scenarios.Length; i++)
            {
                var s = scenarios[i];
                _scenarioButtons[s] = MakeButton(root, "Scenario" + s, SpecialScenarios.Title(s), new Vector2(14f + i * 46f, 14f), new Vector2(42f, 34f), font,
                    () => Play(s, true));
            }
            var looks = new[] { SpecialLook.Pota, SpecialLook.Royal, SpecialLook.Court };
            var names = new[] { "Pota", "1", "2" };
            for (int i = 0; i < looks.Length; i++)
            {
                var l = looks[i];
                _lookButtons[l] = MakeButton(root, "Look" + l, names[i], new Vector2(14f + i * 50f, 54f), new Vector2(46f, 34f), font, () => SetLook(l));
            }
            MakeButton(root, "Replay", "Tekrar", new Vector2(14f + 3 * 50f + 8f, 54f), new Vector2(78f, 34f), font, () => Play(Current, true));
            _label = MakeText(root, "Label", new Vector2(14f, 96f), new Vector2(300f, 20f), font, TextAnchor.MiddleLeft);
        }

        private Image MakeButton(RectTransform root, string name, string text, Vector2 posCss, Vector2 sizeCss, Font font, UnityAction onClick)
        {
            float css = ColorHuntHud.Css;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = posCss * css;
            rt.sizeDelta = sizeCss * css;
            var img = go.GetComponent<Image>();
            img.sprite = _face;
            img.type = Image.Type.Sliced;
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            var label = MakeText(rt, "Label", Vector2.zero, sizeCss, font, TextAnchor.MiddleCenter);
            label.text = text;
            return img;
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 posCss, Vector2 sizeCss, Font font, TextAnchor align)
        {
            float css = ColorHuntHud.Css;
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = posCss * css;
            rt.sizeDelta = sizeCss * css;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = Mathf.RoundToInt(12f * css);
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.16f, 0.31f, 0.9f);       // PopupText's navy edge
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
    }
}
```
(Six scenario buttons end at 14 + 5·46 + 42 = 286 CSS px; the rotate arrows start at 390 − 14 − 64 = 312: no overlap. Rows sit in the
bottom 120 px of the 160-px button band below the board.)

- [ ] **Step 7: The scene tool — reuse the Color Hunt scene parts**

`Editor/ColorHuntSceneTool.cs`: replace `Build()` with the two methods below (the scene it writes is unchanged):
```csharp
        /// <summary>
        /// The puzzle scene's contents into the open scene: camera (fit + shake, post), sun, puzzle volume, game + HUD (+ binder),
        /// EventSystem, the stage (giant hoop, court, room reflection) and the puzzle lighting. Color Hunt and the Specials demo share it.
        /// </summary>
        public static GameParts BuildPuzzleScene(ColorHuntSettings settings)
        {
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
            return parts;
        }

        public static List<string> Build()
        {
            var report = new List<string>();
            var settings = EnsureSettings(report);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildPuzzleScene(settings);

            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");
            SceneParts.AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            report.Add(GameViewPortrait.Apply(1080, 1920));
            return report;
        }
```

`Editor/SceneParts.cs`, inside `SceneParts` after `AddToBuildSettings`:
```csharp
        /// <summary>Appends <paramref name="scenePath"/> to a GameProfile's scenes once (ai_build and the dev scene switcher take it from there).</summary>
        public static void AddToProfile(string profilePath, string scenePath, List<string> report)
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameProfile>(profilePath);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (profile == null || sceneAsset == null) { report.Add($"{profilePath} or {scenePath} missing"); return; }
            if (System.Array.IndexOf(profile.Scenes, sceneAsset) >= 0) return;
            var so = new SerializedObject(profile);
            var list = so.FindProperty("Scenes");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sceneAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add($"{profile.name}: {sceneAsset.name} added");
        }
```

`Editor/PotaFlowSceneTool.cs`: replace `AddToProfile(report);` in `Build` with
`SceneParts.AddToProfile(ProfilePath, ScenePath, report);` and delete the private method `AddToProfile`.

`Editor/SpecialsSceneTool.cs`:
```csharp
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>Creates (or rebuilds) the special-balls showcase: the Color Hunt scene's parts, no auto deal, and the SpecialsDemo.</summary>
    [McpForUnityTool("ai_specials_build_scene", Description =
        "Create or rebuild Assets/_Project/Games/Pota/Scenes/Specials.unity: the Color Hunt scene's parts (camera, light, volume, game, HUD without " +
        "the hint, stage) with ColorHuntGame.AutoStart off and a SpecialsDemo (scenario buttons, Pota | 1 | 2, Tekrar; built at runtime). " +
        "Adds the scene to Build Settings and PotaProfile. Refuses while playing or while the open scene has unsaved changes.")]
    public static class SpecialsSceneTool
    {
        public const string ScenePath = "Assets/_Project/Games/Pota/Scenes/Specials.unity";

        public class Parameters { }

        public static object HandleCommand(JObject p)
        {
            string refusal = Refusal();
            if (refusal != null) return new ErrorResponse(refusal);
            var report = Build();
            string msg = $"Specials scene built at {ScenePath}.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { scene = ScenePath, steps = report });
        }

        [MenuItem("AI Tools/Pota/Build Specials Scene")]
        public static void BuildMenu()
        {
            string refusal = Refusal();
            if (refusal != null) { Debug.LogWarning("[ai_specials_build_scene] " + refusal); return; }
            AIToolsCommon.Log(string.Join(" | ", Build()));
        }

        private static string Refusal()
        {
            if (EditorApplication.isPlaying) return "Leave Play Mode first (manage_editor stop).";
            var active = EditorSceneManager.GetActiveScene();
            return active.isDirty ? $"The open scene {active.path} has unsaved changes: save or discard them first." : null;
        }

        public static List<string> Build()
        {
            var report = new List<string>();
            var settings = ColorHuntSceneTool.EnsureSettings(report);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parts = ColorHuntSceneTool.BuildPuzzleScene(settings);
            parts.Game.AutoStart = false;                 // SpecialsDemo stages the boards
            parts.Hud.ShowHint = false;                   // the demo's rows use the band under the board
            var demo = new GameObject("SpecialsDemo").AddComponent<SpecialsDemo>();
            demo.Game = parts.Game;

            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");
            SceneParts.AddToBuildSettings(ScenePath);
            SceneParts.AddToProfile(PotaFlowSceneTool.ProfilePath, ScenePath, report);
            AssetDatabase.SaveAssets();
            report.Add(GameViewPortrait.Apply(1080, 1920));
            return report;
        }
    }
}
```

`Tests/EditMode/SpecialsSceneToolTests.cs`:
```csharp
using System;
using NUnit.Framework;
using Project.Editor;
using Project.Editor.AITools;
using UnityEditor;

namespace Project.Tests
{
    /// <summary>The demo scene is in the build and in Pota's profile (SceneBootTests then boots it; ai_build ships it in dev builds).</summary>
    public class SpecialsSceneToolTests
    {
        [Test]
        public void SpecialsScene_IsInBuildSettings_AndThePotaProfile()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(SpecialsSceneTool.ScenePath), "run ai_specials_build_scene");
            Assert.IsTrue(Array.Exists(EditorBuildSettings.scenes, s => s.path == SpecialsSceneTool.ScenePath && s.enabled));
            var profile = GameProfile.Find("Pota");
            Assert.IsNotNull(profile);
            Assert.IsTrue(Array.Exists(profile.Scenes, s => s != null && AssetDatabase.GetAssetPath(s) == SpecialsSceneTool.ScenePath));
            Assert.AreEqual("Assets/_Project/Games/Pota/Scenes/ColorHunt.unity", ColorHuntSceneTool.ScenePath, "the scene the demo copies");
        }
    }
}
```

- [ ] **Step 8: Compile, build the scene, run the EditMode tests**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialScenariosTests Project.Tests.SceneBuildSettingsTests
~/.local/bin/uv run Tools/mcp_call.py call manage_editor '{"action":"stop"}'
~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_specials_build_scene","parameters":{}}'
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SpecialsSceneToolTests Project.Tests.RuntimeShaderBuildTests --no-compile
git status --short ProjectSettings Assets/_Project/Games/Pota/PotaProfile.asset Assets/_Project/Games/Pota/Scenes
```
Expected: the first run compiles and passes; the tool answers `"Specials scene built at Assets/_Project/Games/Pota/Scenes/Specials.unity."`
with steps that include `saved …Specials.unity` and `PotaProfile: Specials added` (if it refuses because of an unsaved scene, save or
discard that scene in Unity first — never discard silently: ask if it is not yours); the second run passes; `git status` shows
`EditorBuildSettings.asset` and `PotaProfile.asset` modified and `Specials.unity(.meta)` new — nothing else.

- [ ] **Step 9: Run the PlayMode tests (new, boot, everything)**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.SpecialsDemoPlayTests Project.Tests.SceneBootTests --no-compile
~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile
~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile
```
Expected: `SpecialsDemoPlayTests` 6/6 (Start, the three looks, the switch, the buttons); `SceneBootTests` now has a Specials case
(it enumerates Build Settings) and it passes; both full suites pass. Warnings "the Royal/Court special look is missing assets" are
expected until Tasks 10/11 and do not fail tests.

- [ ] **Step 10: Look at it**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
cat > "$S/specials-demo-shot.sh" <<'SH'
#!/bin/zsh
# Plays Specials.unity: a still of the Free board with the demo UI, then the Golden scenario in slow motion.
set -u
cd "/Users/tunaaltin17/projects/Unity MCP"
S="${0:A:h}"
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
shot() { M call manage_camera '{"action":"screenshot","capture_source":"game_view","include_image":false,"output_folder":"Temp/mcp_out"}' | grep -o '[^" ]*\.png' | head -1; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
python3 - "$S" <<'EOF'
import json, sys
d = sys.argv[1]
open_scene = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
              'if (s.path != "Assets/_Project/Games/Pota/Scenes/Specials.unity") { if (s.isDirty) return "dirty: " + s.path; '
              'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Games/Pota/Scenes/Specials.unity"); } '
              'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
demo = 'var d = UnityEngine.Object.FindAnyObjectByType<Project.ColorHunt.SpecialsDemo>(); '
free = demo + 'd.Play(Project.ColorHunt.SpecialScenario.Free, false); return d.LabelText;'
golden = demo + ('d.Game.Juice.TimeCtl.SlowMo(0.25f, 60f); d.Play(Project.ColorHunt.SpecialScenario.Golden, true); return d.LabelText;')
label = demo + 'return d.LabelText;'
for name, code in (("open-specials", open_scene), ("free", free), ("golden", golden), ("label", label)):
    json.dump({"action": "execute", "code": code, "safety_checks": True}, open(f"{d}/{name}.json", "w"))
EOF
M call execute_code @"$S/open-specials.json" | grep '"result"' | grep -q Specials || { echo "could not open Specials (unsaved scene?)"; exit 1; }
M call manage_editor '{"action":"play"}' > /dev/null
for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
M call execute_code @"$S/free.json" | grep '"result"'
sleep 2
shot
M call execute_code @"$S/golden.json" | grep '"result"'
sleep 5
for i in 1 2 3 4 5; do shot; done
sleep 4
M call execute_code @"$S/label.json" | grep '"result"'
M call manage_editor '{"action":"stop"}' > /dev/null
SH
zsh "$S/specials-demo-shot.sh"
```
Expected: `"result": "Pota  ·  Serbest  ·  sepette 0"`, six PNG paths, then a label ending `sepette 2`. Open the PNGs (Read tool):
1. The still: the Color Hunt board with the HUD (CEPHANE, HEDEF, the two arrows on the right), the four specials of the Free board
   (orange line ball with vertical arrows at row 2 column 2, gold ball at row 3 column 5, dark bomb in the middle, orange ball with
   horizontal arrows at row 6 column 6), one orange ring under column 5 on the frame, no hint card; at the bottom-left two rows of navy
   gold-edged buttons ("Doğuş … Serbest" with "Serbest" orange; "Pota" orange, "1", "2", "Tekrar") and the white label above them —
   all readable, none under the arrows, none over the board.
2. The Golden shots: the block under the column-4 gold ball bursts, the gold ball drops onto the ring, both gold balls fly to the rim
   0.12 s apart, the rings go out as they pass, a yellow flash at the rim, the net swinging.
Overlapping buttons, unreadable Turkish letters, a ring that does not go out or a ball that misses the rim are defects: fix before
committing. Keep the still and the best flight frame:
```bash
Tools/keep_screenshot.sh <the first PNG> 2026-10-01-specials-demo
Tools/keep_screenshot.sh <the clearest golden-flight PNG> 2026-10-01-specials-golden
```

- [ ] **Step 11: Commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
P=Assets/_Project/Games/Pota
git add $P/Scripts/ColorHunt/Specials/SpecialScenarios.cs* $P/Scripts/ColorHunt/Specials/SpecialsDemo.cs* \
  $P/Scripts/ColorHunt/ColorHuntRules.Specials.cs $P/Editor/SpecialsSceneTool.cs* $P/Editor/ColorHuntSceneTool.cs \
  $P/Editor/SceneParts.cs $P/Editor/PotaFlowSceneTool.cs $P/Scenes/Specials.unity* $P/PotaProfile.asset \
  ProjectSettings/EditorBuildSettings.asset \
  $P/Tests/EditMode/SpecialScenariosTests.cs* $P/Tests/EditMode/SpecialsSceneToolTests.cs* $P/Tests/PlayMode/SpecialsDemoPlayTests.cs* \
  docs/screenshots/2026-10-01-specials-demo.jpg docs/screenshots/2026-10-01-specials-golden.jpg
git status --short    # nothing else from this task should be left
git commit -m "feat: Specials demo scene — six hand-authored boards (birth, line, bomb, chain, golden, free) checked against Pota's rules, scripted auto-play, Pota | 1 | 2 look buttons and replay built at runtime

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Blender — loop/wide/param-aware flipbooks; fire, flame jet, dust ring, speed lines

**Repo:** `/Users/tunaaltin17/projects/Blender MCP` (quote the path: it has a space).

**Files:**
- Modify: `tools/bl/vfx.py` (`frame_h`, `params`, loops, `render_frame`, `post` hook, coloured ink, `_toon`, `_blobs`, four recipes)
- Test: `tests/test_vfx.py` (one existing test updated, four new)
- Output (committed): `assets/Pota/{vfx-fire-toon, vfx-flame-jet, vfx-dust-ring, vfx-speed-lines}/export/{<id>.png, preview.png, manifest.json}`

**Interfaces:**
- Consumes: `vfx_flipbook`, `RECIPES`, `_stage`, `_toon_material`, `_outline_material`, `_inked`, `_mesh_from`, `_ease_out`, `_smooth`
  (2026-09-30 plan Tasks 1–2, as committed).
- Produces: recipes `fire_toon`, `flame_jet` (`loop: True`), `dust_ring`, `speed_lines`; `render_frame`; manifest keys `frame_h`, `loop`
  (see Contract additions). Task 8 adds `floor_crack`, `lettering` (uses `frame_h`, `params`, `post`).

- [ ] **Step 1: Write the failing tests**

In `tests/test_vfx.py`, `FlipbookTests.test_manifest_is_a_flipbook_handoff`, replace the line
```python
        self.assertEqual(m["flipbook"], {"cols": 2, "rows": 2, "frames": 4, "frame_px": 64, "duration_s": 0.35, "linear": False})
```
with
```python
        self.assertEqual(m["flipbook"], {"cols": 2, "rows": 2, "frames": 4, "frame_px": 64, "frame_h": 64, "duration_s": 0.35,
                                         "linear": False, "loop": False})
        self.assertEqual(m["source"]["params"], {"seed": 7, "frames": 4})
```

Add after `_width` (module helpers):
```python
def _coverage(frame):
    """Share of a frame's pixels drawn above alpha 0.05 (vfx.DRAWN_ALPHA)."""
    return float((frame[..., 3] > 0.05).mean())


def _runs(mask, axis):
    """Mean length of the drawn runs along rows (axis 1: horizontal) or along columns (axis 0: vertical)."""
    lines = mask if axis == 1 else mask.T
    lengths = []
    for line in lines:
        run = 0
        for drawn in line:
            if drawn:
                run += 1
            elif run:
                lengths.append(run)
                run = 0
        if run:
            lengths.append(run)
    return float(np.mean(lengths)) if lengths else 0.0
```

Append the test class:
```python
class SpecialFlipbookTests(unittest.TestCase):
    """The special balls' flipbooks (plan 2026-10-01 Task 7), rendered small for speed: 4 frames on a 2×2 sheet."""

    def setUp(self):
        self.out = bl.util.resolve(".logs/vfx test/specials")
        shutil.rmtree(self.out, ignore_errors=True)

    def _make(self, recipe, px):
        m = bl.vfx_flipbook(recipe=recipe, game="_test", id="sp-" + recipe, frames=4, cols=2, frame_px=px, out_dir=self.out)
        return m, _pixels(os.path.join(self.out, "sp-{}.png".format(recipe)))

    def test_fire_pops_billows_orange_with_a_red_brown_line_and_ends_in_smoke(self):
        m, sheet = self._make("fire_toon", 96)
        self.assertEqual(m["checks"]["frames_empty"], [])
        self.assertFalse(m["flipbook"]["loop"])
        self.assertEqual(float(sheet[..., 3].min()), 0.0, "the background must be transparent")
        first, second = _frame(sheet, 0, 2, 2, 96), _frame(sheet, 1, 2, 2, 96)
        self.assertLess(_coverage(first), _coverage(second), "the fireball must grow out of the pop")
        px = sheet[sheet[..., 3] > 0.9][:, :3]
        r, g, b = px[:, 0], px[:, 1], px[:, 2]
        self.assertGreater(int(((r > 0.22) & (r < 0.5) & (g < 0.2) & (b < 0.15)).sum()), 20, "no dark red-brown ink line")
        self.assertGreater(int(((r > 0.95) & (g > 0.8) & (b < 0.75)).sum()), 20, "no yellow core")
        self.assertGreater(int(((r > 0.85) & (g > 0.3) & (g < 0.7) & (b < 0.3)).sum()), 20, "no orange body")
        last = _frame(sheet, 3, 2, 2, 96)
        smoke = last[last[..., 3] > 0.9][:, :3]
        self.assertGreater(len(smoke), 0, "the fire must end in smoke lobes")
        self.assertLess(float(np.abs(smoke[:, 0] - smoke[:, 2]).mean()), 0.12, "the last frame is grey smoke, not fire")

    def test_flame_jet_loops_from_a_nozzle_at_the_top(self):
        m, sheet = self._make("flame_jet", 64)
        self.assertTrue(m["flipbook"]["loop"])
        self.assertEqual(m["checks"]["frames_empty"], [], "a loop must draw every frame, the last one too")
        start = bl.vfx.render_frame("flame_jet", 0.0, frame_px=64)
        end = bl.vfx.render_frame("flame_jet", 1.0, frame_px=64)
        half = bl.vfx.render_frame("flame_jet", 0.5, frame_px=64)
        self.assertLess(float(np.abs(start - end).mean()), 0.002, "u = 1 must draw u = 0: the loop has a seam")
        self.assertGreater(float(np.abs(start - half).mean()), 0.01, "the flame must move")
        self.assertLess(float(np.abs(_frame(sheet, 2, 2, 2, 64) - half).mean()), 0.002, "a 4-frame loop samples u = 0, ¼, ½, ¾")
        rows = np.where((start[..., 3] > 0.5).any(axis=1))[0]        # Blender rows: from the bottom
        cols = np.where((start[..., 3] > 0.5).any(axis=0))[0]
        self.assertGreater(int(rows[-1]), 52, "the nozzle sits near the top of the frame")
        self.assertGreater(int(rows[-1] - rows[0]), 2 * int(cols[-1] - cols[0]), "a tall, narrow jet pointing down")

    def test_dust_ring_expands_with_an_open_centre_in_court_tan(self):
        m, sheet = self._make("dust_ring", 96)
        self.assertEqual(m["checks"]["frames_empty"], [])
        first, second = _frame(sheet, 0, 2, 2, 96), _frame(sheet, 1, 2, 2, 96)
        self.assertLess(_coverage(first), _coverage(second), "the ring must burst outward")
        self.assertEqual(float(second[48, 48, 3]), 0.0, "the ring's centre stays open")
        px = sheet[sheet[..., 3] > 0.9][:, :3]
        self.assertGreater(int(((px.max(axis=1) < 0.4) & (px[:, 0] > px[:, 2] + 0.05)).sum()), 10, "no warm ink line")
        tan = (px[:, 0] > 0.8) & (px[:, 1] > 0.6) & (px[:, 2] > 0.4) & (px[:, 2] < 0.75)
        self.assertGreater(int(tan.sum()), 20, "no court-tan dust")

    def test_speed_lines_are_horizontal_inked_streaks(self):
        m, sheet = self._make("speed_lines", 96)
        self.assertEqual(m["checks"]["frames_empty"], [])
        mask = _frame(sheet, 1, 2, 2, 96)[..., 3] > 0.5
        self.assertGreater(_runs(mask, 1), 4.0 * _runs(mask, 0), "the streaks must run along x")
        covered = sheet[sheet[..., 3] > 0.9][:, :3]
        self.assertLess(float(covered.min()), 0.3, "no ink")
        self.assertGreater(float(covered.max()), 0.9, "no white")
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test test_vfx.py`
Expected: 4 errors `ValueError: recipe must be one of puff_toon, ring_toon, shock_ring, smoke_lit, got 'fire_toon'` (and
`'flame_jet'`, `'dust_ring'`, `'speed_lines'`), 1 failure in `test_manifest_is_a_flipbook_handoff` (the dict lacks `frame_h`, `loop`).

- [ ] **Step 3: Implement in `tools/bl/vfx.py`**

(a) Imports: `from mathutils import Vector` becomes
```python
from mathutils import Matrix, Vector
```

(b) After `_smooth`, add:
```python
def _srgb(code):
    """'#rrggbb' → the display (sRGB) floats _toon_material, _flat_material and the ink colours take."""
    code = code.lstrip("#")
    return tuple(int(code[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def _lerp3(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))
```

(c) `_stage`: replace its first lines (from `def _stage(` to `r.resolution_x = r.resolution_y = frame_px`) with
```python
def _stage(frame_px, ortho, view_transform, sun_energy=3.0, frame_h=None):
    """Transparent EEVEE stage: orthographic camera at −Y looking +Y (the workshop's front), one sun from the upper left front.
    A wide frame (frame_h < frame_px) keeps `ortho` across its width."""
    scene = bpy.context.scene
    r = scene.render
    r.engine = "BLENDER_EEVEE"
    r.resolution_x = frame_px
    r.resolution_y = frame_h or frame_px
```
(the rest of `_stage` stays).

(d) Replace `_outline_material` and `_inked` with:
```python
def _outline_material(ink=None):
    """Inverted-hull ink: flat dark emission, back faces culled so only the silhouette shell shows. `ink` is a display colour
    (default OUTLINE, #141b2e)."""
    material = bpy.data.materials.new("vfx_outline")
    tree = node_tree(material)
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = OUTLINE if ink is None else (*(_linear(c) for c in ink), 1.0)
    tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    material.use_backface_culling = True
    return material


def _toon(obj, shade=(0.72, 0.77, 0.88), lit=(1.0, 1.0, 1.0)):
    """Toon body only (a layer drawn in front of an inked one needs no line of its own); colours as shown on screen."""
    obj.data.materials.append(_toon_material("vfx_toon", shade=shade, lit=lit))
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def _inked(obj, width, shade=(0.72, 0.77, 0.88), lit=(1.0, 1.0, 1.0), ink=None):
    """Toon body + a flipped solidify shell `width` m thick with the ink material (`ink` as shown on screen, default #141b2e)."""
    _toon(obj, shade, lit)
    obj.data.materials.append(_outline_material(ink))
    shell = obj.modifiers.new("vfx_outline", "SOLIDIFY")
    shell.thickness = width
    shell.offset = 1.0
    shell.use_flip_normals = True
    shell.material_offset = 1
    return obj
```
(`_inked(obj, width)` keeps today's white body and navy line, so `puff_toon`, `ring_toon` render as before.)

(e) After `_mesh_from`, add:
```python
def _blobs(name, elements, threshold=0.6):
    """Metaball elements (x, y, z, radius) → a mesh object. `name` is the metaball family: Blender merges every metaball object
    whose name shares the part before the first dot, so each layer needs its own name. An element shows smaller than its radius;
    neighbours merge into one body. Elements of radius ~0 are skipped; none left → an empty mesh object."""
    live = [e for e in elements if e[3] > 1e-4]
    if not live:
        obj = bpy.data.objects.new(name, bpy.data.meshes.new(name))
        bpy.context.scene.collection.objects.link(obj)
        return obj
    ball = bpy.data.metaballs.new(name)
    ball.resolution = ball.render_resolution = 0.025
    ball.threshold = threshold
    for x, y, z, radius in live:
        element = ball.elements.new()
        element.co = (x, y, z)
        element.radius = radius
    obj = bpy.data.objects.new(name, ball)
    bpy.context.scene.collection.objects.link(obj)
    return _mesh_from(obj)
```

(f) Before `RECIPES`, add the colours and the four recipes:
```python
# The special balls (plan 2026-10-01). Colours are display values; Royal's fire carries its own red-brown line, not the navy ink.
FIRE_INK = _srgb("#5a1a0c")
FIRE_LIT, FIRE_SHADE = _srgb("#ffa62b"), _srgb("#f26a1b")           # a fresh body: orange
FIRE_LIT_OLD, FIRE_SHADE_OLD = _srgb("#f0642a"), _srgb("#c4381c")   # burning out: red-orange
CORE_LIT, CORE_SHADE = _srgb("#fff27a"), _srgb("#ffcf3f")           # the yellow core (fire and flame jet)
HOT = _srgb("#fffbe6")                                               # the white-hot pop
SMOKE_LIT, SMOKE_SHADE, SMOKE_INK = _srgb("#cfcbd3"), _srgb("#938d9b"), _srgb("#3b3038")
JET_LIT, JET_SHADE = _srgb("#ffb02e"), _srgb("#ff7a1a")
DUST_LIT, DUST_SHADE, DUST_INK = _srgb("#f3cf9b"), _srgb("#d49a63"), _srgb("#4b2a16")   # court dirt, lighter than Pota's floor #d9773a


def _fire_toon(u, seed):
    """Royal Match TNT-style cartoon fireball (spec 1, the bomb): a white-hot pop, then nine orange lobes with a yellow core billow
    out, drift apart into separate lobes and burn out red, leaving four grey smoke lobes that rise and shrink. The body carries a
    dark red-brown line; the core and the pop sit in front of it (camera at −Y) without a line of their own."""
    rng = random.Random(seed)
    pop = _ease_out(min(1.0, u / 0.22))
    drift = _smooth(0.3, 1.0, u)
    burn = _smooth(0.42, 0.92, u)
    body, core, smoke = [], [], []
    for k in range(9):
        a = 2.0 * math.pi * k / 9.0 + rng.uniform(-0.25, 0.25)
        reach, grow = rng.uniform(0.85, 1.15), rng.uniform(0.85, 1.2)
        d = (0.1 + 0.42 * pop + 0.32 * drift) * reach                 # lobes keep travelling after the pop: they break away
        x, z = math.cos(a) * d, math.sin(a) * d + 0.16 * u
        r = (0.17 + 0.2 * pop) * grow * (1.0 - burn)
        body.append((x, 0.0, z, r))
        core.append((0.7 * x, -0.4, 0.7 * z + 0.02, 0.6 * r * (1.0 - _smooth(0.2, 0.65, u))))
    body.append((0.0, 0.0, 0.08 * u, (0.3 + 0.28 * pop) * (1.0 - _smooth(0.3, 0.72, u))))
    core.append((0.0, -0.4, 0.08 * u, (0.26 + 0.2 * pop) * (1.0 - _smooth(0.22, 0.62, u))))
    hot = [(0.0, -0.8, 0.04, 0.3 * (1.0 - _smooth(0.05, 0.38, u)))]
    for k in range(4):
        a = math.radians(55.0 + 23.0 * k) + rng.uniform(-0.15, 0.15)  # the upper half
        show = _smooth(0.5 + 0.05 * k, 0.78 + 0.04 * k, u) * (1.0 - 0.4 * _smooth(0.86, 1.0, u))
        d = 0.3 + 0.25 * drift
        smoke.append((math.cos(a) * d, 0.35, math.sin(a) * d + 0.1 + 0.3 * u, 0.3 * show * rng.uniform(0.85, 1.15)))
    lit, shade = _lerp3(FIRE_LIT, FIRE_LIT_OLD, burn), _lerp3(FIRE_SHADE, FIRE_SHADE_OLD, burn)
    return [_inked(_blobs("vfxFireSmoke", smoke), 0.035, shade=SMOKE_SHADE, lit=SMOKE_LIT, ink=SMOKE_INK),
            _inked(_blobs("vfxFireBody", body), 0.04, shade=shade, lit=lit, ink=FIRE_INK),
            _toon(_blobs("vfxFireCore", core), shade=CORE_SHADE, lit=CORE_LIT),
            _toon(_blobs("vfxFireHot", hot), shade=HOT, lit=HOT)]


def _flame_jet(u, seed):
    """Looping rocket flame (spec 1 and 2): a nozzle at the top centre (z 0.95, frame v ≈ 0.9) and a lobed cone of flame pointing
    down (−z). Periodic in u: blobs travel frac(i/7 + u) down the cone and wobble on whole harmonics of u, so u = 1 draws u = 0 and
    the sheet (frames at u = i/frames) loops without a seam. Each blob is born and dies at zero size, so its wrap never shows."""
    rng = random.Random(seed)
    top, length, n = 0.95, 1.95, 7
    body = [(0.0, 0.0, top - 0.04, 0.2)]                              # the nozzle never moves
    core = [(0.0, -0.3, top - 0.05, 0.13)]
    for i in range(n):
        size = rng.uniform(0.85, 1.15)
        wobble_k, wobble_p = rng.choice((1, 2, 3)), rng.uniform(0.0, 2.0 * math.pi)
        pulse_k, pulse_p = rng.choice((1, 2)), rng.uniform(0.0, 2.0 * math.pi)
        t = (i / float(n) + u) % 1.0                                   # 0 at the nozzle, 1 at the tail
        envelope = math.sin(math.pi * t ** 0.7)
        r = 0.3 * envelope * size * (1.0 + 0.12 * math.sin(2.0 * math.pi * pulse_k * u + pulse_p))
        x = 0.12 * t * math.sin(2.0 * math.pi * wobble_k * u + wobble_p)
        z = top - length * t
        body.append((x, 0.0, z, r))
        if t < 0.7:
            core.append((0.8 * x, -0.3, z + 0.03, 0.6 * r * (1.0 - t / 0.7)))
    return [_inked(_blobs("vfxJetBody", body), 0.03, shade=JET_SHADE, lit=JET_LIT, ink=FIRE_INK),
            _toon(_blobs("vfxJetCore", core), shade=CORE_SHADE, lit=CORE_LIT)]


def _dust_ring(u, seed):
    """Court's slam dust (spec 2): twelve chunky clumps in a ring laid on the board (face-on), bursting outward, puffing up and
    shrinking away; court tan with a warm line. The centre stays open."""
    rng = random.Random(seed)
    spread = _ease_out(u)
    rise = _ease_out(min(1.0, u / 0.3))
    fade = _smooth(0.45, 1.1, u)
    elements = []
    for k in range(12):
        a = 2.0 * math.pi * k / 12.0 + rng.uniform(-0.12, 0.12)
        d = (0.28 + 0.62 * spread) * rng.uniform(0.9, 1.1)
        for _ in range(2):
            b = a + rng.uniform(-0.17, 0.17)
            dd = d + rng.uniform(-0.05, 0.08)
            r = rng.uniform(0.14, 0.2) * (0.6 + 0.6 * rise) * (1.0 - fade)
            elements.append((math.cos(b) * dd, 0.0, math.sin(b) * dd, r))
    return [_inked(_blobs("vfxDust", elements), 0.03, shade=DUST_SHADE, lit=DUST_LIT, ink=DUST_INK)]


def _speed_lines(u, seed):
    """Anime speed streaks (spec 2, the line ball's trail): seven horizontal spindles behind a ball moving to +x (the frame's right
    edge). They shoot in from the right in the first 30 %, then thin out and slide away to the left. White, inked by RECIPES."""
    rng = random.Random(seed)
    shoot = 0.15 + 0.85 * _ease_out(min(1.0, u / 0.3))
    leave = _smooth(0.35, 1.0, u)
    objects = []
    for k in range(7):
        z = -0.75 + 1.5 * k / 6.0 + rng.uniform(-0.06, 0.06)
        head = 1.05 - 0.25 * abs(z) - rng.uniform(0.0, 0.15) - 1.2 * leave
        length = rng.uniform(0.9, 1.6) * shoot * (1.0 - 0.4 * leave)
        thick = rng.uniform(0.035, 0.06) * (1.0 - 0.75 * leave)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=8, radius=1.0, location=(head - length / 2.0, 0.0, z))
        obj = bpy.context.object
        obj.data.transform(Matrix.Diagonal((length / 2.0, thick, thick, 1.0)))   # in the mesh: the ink shell is in mesh units
        obj.data.update()
        objects.append(obj)
    return objects
```
and `RECIPES` becomes:
```python
RECIPES = {
    "puff_toon": {"build": _puff_toon, "ortho": 2.4, "view": "Standard", "linear": False, "ink": 0.035},
    "ring_toon": {"build": _ring_toon, "ortho": 2.4, "view": "Standard", "linear": False, "ink": 0.022},
    "smoke_lit": {"build": _smoke_lit, "ortho": 2.6, "view": "Standard", "linear": False, "ink": None},
    "shock_ring": {"build": _shock_ring, "ortho": 2.4, "view": "Raw", "linear": True, "ink": None},
    # the special balls (plan 2026-10-01); "ink": None where the recipe inks its own layers in their own colours
    "fire_toon": {"build": _fire_toon, "ortho": 2.6, "view": "Standard", "linear": False, "ink": None},
    "flame_jet": {"build": _flame_jet, "ortho": 2.4, "view": "Standard", "linear": False, "ink": None, "loop": True},
    "dust_ring": {"build": _dust_ring, "ortho": 2.6, "view": "Standard", "linear": False, "ink": None},
    "speed_lines": {"build": _speed_lines, "ortho": 2.4, "view": "Standard", "linear": False, "ink": 0.012},
}
```

(g) Replace `_tile` and `_compose` with:
```python
def _tile(path, w, h):
    image = bpy.data.images.load(path)
    pixels = np.empty(w * h * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)
    return pixels.reshape(h, w, 4)


def _compose(tiles, cols, rows, w, h):
    """Frames row-major from the top-left on a transparent sheet (Blender image rows start at the bottom)."""
    sheet = np.zeros((rows * h, cols * w, 4), dtype=np.float32)
    for index, pixels in enumerate(tiles):
        col, row = index % cols, index // cols
        y0 = (rows - 1 - row) * h
        sheet[y0:y0 + h, col * w:(col + 1) * w] = pixels
    return sheet
```

(h) Replace `vfx_flipbook` with `_spec`, `render_frame` and the new `vfx_flipbook`:
```python
def _spec(recipe):
    if recipe not in RECIPES:
        raise ValueError("recipe must be one of {}, got '{}'".format(", ".join(sorted(RECIPES)), recipe))
    return RECIPES[recipe]


def render_frame(recipe, u, frame_px=256, frame_h=None, seed=7, params=None, path=None):
    """One frame of `recipe` at time u (0 … 1; a loop recipe draws the same frame at u = 1 as at u = 0) as RGBA floats, rows from
    the bottom, after the recipe's `post` step. Resets the scene, so headless only (vfx_flipbook and the tests call it)."""
    if not bpy.app.background:
        raise RuntimeError("render_frame resets the scene: run it headless (tools/run.sh)")
    spec = _spec(recipe)
    frame_h = frame_h or frame_px
    new_scene()
    scene = _stage(frame_px, spec["ortho"], spec["view"], spec.get("sun", 3.0), frame_h)
    objects = spec["build"](u, seed, **(params or {}))
    if spec.get("ink"):
        for obj in objects:
            _inked(obj, spec["ink"])
    path = path or resolve(".logs/vfx/_frame.png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    tile = _tile(path, frame_px, frame_h)
    return spec["post"](tile, params or {}) if spec.get("post") else tile


def vfx_flipbook(recipe, game, id, frames=16, cols=4, frame_px=256, duration=0.35, seed=7, out_dir=None, frame_h=None,
                 params=None):
    if not bpy.app.background:
        return run_background("vfx_flipbook", {"recipe": recipe, "game": game, "id": id, "frames": frames, "cols": cols,
                                               "frame_px": frame_px, "duration": duration, "seed": seed, "out_dir": out_dir,
                                               "frame_h": frame_h, "params": params})
    spec = _spec(recipe)
    loop = bool(spec.get("loop"))
    frame_h = frame_h or frame_px
    rows = int(math.ceil(frames / float(cols)))
    out_dir = resolve(out_dir or "assets/{}/{}/export".format(game, id))
    os.makedirs(out_dir, exist_ok=True)
    frames_dir = resolve(".logs/vfx/{}".format(id))
    os.makedirs(frames_dir, exist_ok=True)
    tiles = []
    for index in range(frames):
        # a loop samples one period without its end (u = 1 is frame 0 again); a one-shot runs from its first to its last moment
        u = index / float(frames) if loop else index / float(max(1, frames - 1))
        tiles.append(render_frame(recipe, u, frame_px, frame_h, seed, params,
                                  os.path.join(frames_dir, "f_{:02d}.png".format(index))))
    sheet = _compose(tiles, cols, rows, frame_px, frame_h)
    _save(sheet, os.path.join(out_dir, id + ".png"))
    _save(_flatten(sheet, 0.14), os.path.join(out_dir, "preview.png"))
    coverage = [float((t[..., 3] > DRAWN_ALPHA).mean()) for t in tiles]
    checked = coverage if (loop or frames == 1) else coverage[:-1]   # a one-shot may have faded out on its last frame
    empty = [i for i, c in enumerate(checked) if c < DRAWN_SHARE]
    size = [cols * frame_px, rows * frame_h]
    manifest = {
        "id": id, "game": game, "kind": "vfx-flipbook", "version": 1,
        "files": {"glb": None, "fbx": None, "sheet": id + ".png", "previews": ["preview.png"]},
        "flipbook": {"cols": cols, "rows": rows, "frames": frames, "frame_px": frame_px, "frame_h": frame_h,
                     "duration_s": duration, "linear": spec["linear"], "loop": loop},
        "units": "px", "up": "+Y", "scale_applied": True,
        "stats": {"tris": 0, "materials": 0, "textures": [{"name": id + ".png", "size": size}], "bones": 0, "clips": [],
                  "size_m": [0, 0, 0]},
        "source": {"recipe": "vfx_flipbook:" + recipe, "copy_provenance": None, "inputs": [],
                   "params": dict({"seed": seed, "frames": frames}, **(params or {}))},
        "checks": {"frames_empty": empty, "coverage": [round(c, 4) for c in coverage], "passed": not empty},
        "made": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
    log("vfx_flipbook {} ({}): {} frames on {}x{} px{}, empty {} -> {}".format(
        id, recipe, frames, size[0], size[1], ", loop" if loop else "", empty, out_dir))
    return manifest
```

- [ ] **Step 4: Run the tests**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test test_vfx.py`
Expected: `Ran 14 tests … OK`.
- If `test_flame_jet_loops…` fails on the seam (mean diff ≥ 0.002), something in `_flame_jet` depends on u outside `frac(…)` or
  `sin(2π·k·u)` with whole k: find it, do not loosen the bound. If it fails on "the flame must move", the blobs are too small to
  change the silhouette between u = 0 and 0.5: raise the 0.3 radius factor, not the test.
- If the fire's ink count is low, the line is under a pixel at 96 px: check `_inked` was applied to the body (two material slots,
  SOLIDIFY) before thickening the line.

- [ ] **Step 5: Render Pota's four flipbooks and look at them**

```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
tools/run.sh vfx_flipbook '{"recipe": "fire_toon", "game": "Pota", "id": "vfx-fire-toon", "frames": 16, "cols": 4, "frame_px": 256, "duration": 0.45}'
tools/run.sh vfx_flipbook '{"recipe": "flame_jet", "game": "Pota", "id": "vfx-flame-jet", "frames": 8, "cols": 4, "frame_px": 256, "duration": 0.3}'
tools/run.sh vfx_flipbook '{"recipe": "dust_ring", "game": "Pota", "id": "vfx-dust-ring", "frames": 16, "cols": 4, "frame_px": 256, "duration": 0.4}'
tools/run.sh vfx_flipbook '{"recipe": "speed_lines", "game": "Pota", "id": "vfx-speed-lines", "frames": 8, "cols": 4, "frame_px": 256, "duration": 0.25}'
```
Expected: `[bl] vfx_flipbook vfx-fire-toon (fire_toon): 16 frames on 1024x1024 px, empty [] -> …`, `… vfx-flame-jet (flame_jet): 8 frames
on 1024x512 px, loop, empty [] -> …`, `… vfx-dust-ring …: 16 frames on 1024x1024 px …`, `… vfx-speed-lines …: 8 frames on 1024x512 px …`.

Open each `assets/Pota/<id>/export/preview.png` with the Read tool and check:
- **fire:** frame 0 a white-yellow ball with an orange rim and a red-brown line; frames 2–6 a big lobed orange ball with a yellow
  core (not a flat disc: the lobes must read); frames 8–12 the lobes drift apart, redden and shrink; the last frames are only grey
  smoke lobes with a dark line. Nothing touches the tile edges. Compare with Royal Match's TNT fireball: chunky, cartoon, two tones.
- **flame jet:** a nozzle blob at the top centre of every tile, a flame narrowing to a point near the bottom, a yellow core in the
  upper part, lobes moving down from tile to tile; tile 7 → tile 0 must look like the next step, not a jump.
- **dust ring:** a ring of tan clumps with a warm line, open centre, growing outward and shrinking to nothing by the end; no clump
  clipped by the tile edge.
- **speed lines:** white streaks with a thin navy line, entering from the right, then thinning and sliding left.
A visible flaw (lumpy body, broken line, clipped tile, a jump in the loop) is fixed in the recipe numbers before committing (quality
rule), with a ledger ruling naming the number changed; rerun Step 4 after any change.

- [ ] **Step 6: Full suite and commit**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test` → Expected: all pass (79 before + 4 = 83).
```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
git add tools/bl/vfx.py tests/test_vfx.py assets/Pota/vfx-fire-toon assets/Pota/vfx-flame-jet assets/Pota/vfx-dust-ring assets/Pota/vfx-speed-lines
git commit -m "feat: vfx_flipbook loops, wide frames and recipe params; Royal fire, flame jet, Court dust ring and speed lines

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Blender — meshes and stills: ball halves, basketball, coin, bomb shell, floor crack, lettering

**Repo:** `/Users/tunaaltin17/projects/Blender MCP`.

**Files:**
- Create: `fonts/LilitaOne-Regular.ttf`, `fonts/LilitaOne-OFL.txt` (copied from Unity MCP `Assets/_Project/Games/Pota/Art/Fonts/`; OFL)
- Modify: `tools/bl/vfx.py` (stills `floor_crack`, `lettering` + its 2D outline; ball skins; `vfx_mesh`; `vfx_fracture` sphere shell)
- Modify: `tools/bl/__init__.py` (import + `TOOLS`)
- Test: `tests/test_vfx.py`
- Output (committed): `assets/Pota/{vfx-floor-crack, vfx-letter-slam, vfx-letter-swish, vfx-letter-combo}/export/{<id>.png, preview.png, manifest.json}`,
  `assets/Pota/{vfx-ball-halves, vfx-basketball, vfx-coin, vfx-bomb-shell}/export/{<id>.glb, preview.png, manifest.json}`

**Interfaces:**
- Consumes: Task 7's `render_frame`, `frame_h`, `params`, `post`, `_srgb`, `_blobs`, `_toon`; `export` (manifest, preview); `_pillow`,
  `_seeds`, `_cell` (the pillow fracture, unchanged).
- Produces: recipes `floor_crack`, `lettering` (`params: {text, style}`, styles `slam`, `swish`, `combo`); `vfx_mesh`; `vfx_fracture`
  `shape="sphere"`; `ball_uv`, `coin_uv`, `SKINS`, `SWATCH_U`, `SWATCH_V` (see Contract additions).

**Decisions (ledger):**
1. **Skins are small textures embedded in the GLB, not vertex colours.** Core `Project/Toon` samples `_BaseMap` with UV0 and ignores
   vertex colours; glTFast keeps an embedded PNG as the material's `baseColorTexture`, which `SpecialMeshes.Toon` (Task 9) moves onto
   the toon material. One material and one texture per GLB.
2. **The ball skin is Pota's `topKureDokusu` layout in 3D** (equator, one great circle, two curved lens seams swinging 43.6° off the
   meridian at the equator), painted on a lat-long whose poles are ±X (the halves' noses); v 0.875–1 holds flat swatches for caps,
   inner faces and cut walls. Colours sampled from Pota's sprites: line ball base `#fd8232`, seams `#2a2626`, cap `#ffc07a`
   (`ozel-cizgi-a.png`); bomb base `#333436`, seams `#d56a30` (`ozel-bomba-a.png`); coin gold `#f6c630`, light `#fde962`,
   lines `#985808`, rim `#da8c1c` (`ozel-altin-a.png`); Court's ball = Pota's `KLASIK_DERI` `#f07d1d` / `#241611`. No baked
   top-light gradient: the toon shading lights the ball.
3. **Halves face −X in the workshop so they face +X in Unity** (glTFast negates x). The suggested "+X away from the cut" in workshop
   axes would arrive as −X; the Unity convention is the one Task 10 codes against. HalfB is the ball's other half turned 180° about Z
   (= Unity y), so both noses share one axis. Preview offsets: HalfA z +0.3, HalfB z −0.3.
4. **The lettering's outline is 2D** (a disc dilation of the rendered alpha + a shifted shadow, `post` step): even and thick, with no
   self-intersections in Lilita One's tight counters, which a 3D curve offset produces.
5. **The bomb shell is a spherical Voronoi cut of the sphere's surface, thickened inward along the radius.** Seeds on a sphere put
   every bisecting plane through the centre, so walls lie on the planes and pieces fit; cutting the hollow solid with `holes_fill`
   would fill the inner circle as a disc.

- [ ] **Step 1: Copy the game's font into the workshop**

```bash
mkdir -p "/Users/tunaaltin17/projects/Blender MCP/fonts"
cp "/Users/tunaaltin17/projects/Unity MCP/Assets/_Project/Games/Pota/Art/Fonts/LilitaOne-Regular.ttf" \
   "/Users/tunaaltin17/projects/Unity MCP/Assets/_Project/Games/Pota/Art/Fonts/LilitaOne-OFL.txt" \
   "/Users/tunaaltin17/projects/Blender MCP/fonts/"
ls "/Users/tunaaltin17/projects/Blender MCP/fonts"
```
Expected: `LilitaOne-OFL.txt  LilitaOne-Regular.ttf`. (Copying out of Unity's `Assets/` leaves its `.meta` files untouched.)

- [ ] **Step 2: Write the failing tests** (append to `tests/test_vfx.py`; add `import math` to its imports)

```python
BASE_DIR = (0.356, -0.815, 0.458)   # a ball direction between the seams
SEAM_DIR = (0.3, -0.954, 0.0)       # on the equator seam only


def _hex(code):
    code = code.lstrip("#")
    return np.array([int(code[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def _assert_color(case, rgb, code, tol=0.03):
    rgb = np.asarray(rgb, dtype=np.float32)
    case.assertLess(float(np.abs(rgb - _hex(code)).max()), tol, "{} vs {}".format(np.round(rgb, 3).tolist(), code))


def _image_pixels():
    """The texture the last loaded GLB brought, rows from the bottom."""
    image = next(i for i in bpy.data.images if i.size[0] > 0)
    w, h = image.size
    px = np.empty(w * h * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    return px.reshape(h, w, 4)


def _texel(img, u, v):
    h, w = img.shape[:2]
    return img[min(h - 1, int(v * h)), int((u % 1.0) * w) % w, :3]


def _rays(img, radius):
    """Drawn runs on a circle of `radius` px around the image centre: how many cracks cross it."""
    h, w = img.shape[:2]
    drawn = []
    for k in range(720):
        a = 2.0 * math.pi * k / 720.0
        x, y = int(w / 2.0 + radius * math.cos(a)), int(h / 2.0 + radius * math.sin(a))
        drawn.append(bool(img[y, x, 3] > 0.5))
    return sum(1 for k in range(720) if drawn[k] and not drawn[k - 1])


class StillTests(unittest.TestCase):
    def setUp(self):
        self.out = bl.util.resolve(".logs/vfx test/stills")
        shutil.rmtree(self.out, ignore_errors=True)

    def test_floor_crack_is_a_star_with_a_dark_crater_and_a_pale_edge(self):
        m = bl.vfx_flipbook(recipe="floor_crack", game="_test", id="crack", frames=1, cols=1, frame_px=128, out_dir=self.out)
        self.assertEqual(m["checks"]["frames_empty"], [])
        self.assertEqual((m["flipbook"]["frames"], m["flipbook"]["rows"]), (1, 1))
        img = _pixels(os.path.join(self.out, "crack.png"))
        self.assertEqual(img.shape, (128, 128, 4))
        self.assertGreater(float(img[64, 64, 3]), 0.9, "the crater")
        self.assertEqual(float(img[0, 0, 3]) + float(img[127, 127, 3]), 0.0, "the corners stay clear")
        self.assertGreaterEqual(_rays(img, 24), 7, "a star of cracks")
        px = img[img[..., 3] > 0.9][:, :3]
        self.assertGreater(int((px.max(axis=1) < 0.2).sum()), 50, "no dark crack")
        self.assertGreater(int((px.min(axis=1) > 0.8).sum()), 5, "no pale chipped edge")

    def test_lettering_is_a_wide_outlined_still(self):
        m = bl.vfx_flipbook(recipe="lettering", game="_test", id="slam", frames=1, cols=1, frame_px=128, frame_h=64,
                            out_dir=self.out, params={"text": "SLAM!", "style": "slam"})
        self.assertEqual((m["flipbook"]["frame_px"], m["flipbook"]["frame_h"]), (128, 64))
        self.assertEqual(m["stats"]["textures"], [{"name": "slam.png", "size": [128, 64]}])
        self.assertEqual((m["source"]["params"]["text"], m["source"]["params"]["style"]), ("SLAM!", "slam"))
        img = _pixels(os.path.join(self.out, "slam.png"))
        self.assertEqual(img.shape, (64, 128, 4))
        self.assertEqual(float(img[0, 0, 3]) + float(img[63, 127, 3]), 0.0, "the corners stay clear")
        cover = float((img[..., 3] > 0.5).mean())
        self.assertGreater(cover, 0.2)
        self.assertLess(cover, 0.8)
        cols = np.where((img[..., 3] > 0.5).any(axis=0))[0]
        self.assertGreater(int(cols[-1] - cols[0]), int(0.75 * 128), "the word fills the width")
        px = img[img[..., 3] > 0.9][:, :3]
        self.assertGreater(int(((px.max(axis=1) < 0.32) & (px[:, 2] > px[:, 0])).sum()), 30, "no navy outline")
        fill = (px[:, 0] > 0.9) & (px[:, 1] > 0.35) & (px[:, 1] < 0.9) & (px[:, 2] < 0.35)
        self.assertGreater(int(fill.sum()), 30, "no orange-yellow fill")

    def test_lettering_refuses_an_unknown_style(self):
        with self.assertRaises(ValueError):
            bl.vfx_flipbook(recipe="lettering", game="_test", id="x", frames=1, cols=1, frame_px=64, frame_h=32,
                            out_dir=self.out, params={"text": "BOOM!", "style": "boom"})
```

```python
class MeshTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.out = bl.util.resolve(".logs/vfx test/meshes")
        shutil.rmtree(cls.out, ignore_errors=True)
        cls.m = {recipe: bl.vfx_mesh(recipe=recipe, game="_test", id="m-" + recipe, out_dir=os.path.join(cls.out, recipe),
                                     preview=False)
                 for recipe in ("ball_halves", "basketball", "coin")}

    def _load(self, recipe):
        names = bl.load(os.path.join(self.out, recipe, "m-{}.glb".format(recipe)))
        return {n: bpy.data.objects[n] for n in names if bpy.data.objects[n].type == "MESH"}

    def test_ball_halves_are_two_closed_halves_with_their_nose_to_minus_x(self):
        m = self.m["ball_halves"]
        self.assertTrue(m["checks"]["passed"], m["checks"])
        self.assertEqual(m["checks"]["mesh"]["objects"], ["HalfA", "HalfB"])
        self.assertEqual(m["checks"]["mesh"]["open_edges"], 0)
        self.assertEqual([t["size"] for t in m["stats"]["textures"]], [[512, 256]])
        meshes = self._load("ball_halves")
        self.assertEqual(sorted(meshes), ["HalfA", "HalfB"])
        dome_v = {}
        for name, obj in meshes.items():
            xs = [v.co.x for v in obj.data.vertices]
            self.assertAlmostEqual(min(xs), -0.225, delta=0.003, msg=name + ": the nose points to −X (Unity +X)")
            self.assertAlmostEqual(max(xs), 0.0, delta=1e-4, msg=name + ": the flat cap sits on the origin")
            self.assertLessEqual(len(obj.data.polygons), 600, name)
            caps = [p for p in obj.data.polygons if p.normal.x > 0.99]
            self.assertGreater(len(caps), 8, name + ": a flat cap facing +X")
            layer = obj.data.uv_layers[0].data
            dome_v[name] = [layer[i].uv[1] for p in obj.data.polygons if p.normal.x <= 0.99 for i in p.loop_indices]
        self.assertLessEqual(max(dome_v["HalfA"]), 0.43 + 0.01, "HalfA wears the −X side of the skin")
        self.assertGreaterEqual(min(dome_v["HalfB"]), 0.43 - 0.01, "HalfB wears the +X side, turned to face −X")
        img = _image_pixels()
        _assert_color(self, _texel(img, *bl.vfx.ball_uv(*BASE_DIR)), "#fd8232")
        _assert_color(self, _texel(img, *bl.vfx.ball_uv(*SEAM_DIR)), "#2a2626")
        _assert_color(self, _texel(img, bl.vfx.SWATCH_U["cap"], bl.vfx.SWATCH_V), "#ffc07a")

    def test_basketball_is_a_low_poly_ball_in_potas_skin(self):
        m = self.m["basketball"]
        self.assertTrue(m["checks"]["passed"], m["checks"])
        self.assertLessEqual(m["stats"]["tris"], 1500)
        for size in m["stats"]["size_m"]:
            self.assertAlmostEqual(size, 0.45, delta=0.005)
        self.assertEqual(sorted(self._load("basketball")), ["Basketball"])
        img = _image_pixels()
        _assert_color(self, _texel(img, *bl.vfx.ball_uv(*BASE_DIR)), "#f07d1d")
        _assert_color(self, _texel(img, *bl.vfx.ball_uv(*SEAM_DIR)), "#241611")

    def test_coin_is_a_gold_disc_with_a_ball_emboss(self):
        m = self.m["coin"]
        self.assertTrue(m["checks"]["passed"], m["checks"])
        sx, sy, sz = m["stats"]["size_m"]
        self.assertAlmostEqual(sx, 0.30, delta=0.003)
        self.assertAlmostEqual(sz, 0.30, delta=0.003)
        self.assertAlmostEqual(sy, 0.044, delta=0.002, msg="the faces look along ±Y")
        self.assertLessEqual(m["stats"]["tris"], 1200)
        self.assertEqual(sorted(self._load("coin")), ["Coin"])
        img = _image_pixels()
        self.assertEqual(img.shape[:2], (128, 256))
        _assert_color(self, _texel(img, *bl.vfx.coin_uv(0.03, 0.04)), "#f6c630")
        _assert_color(self, _texel(img, *bl.vfx.coin_uv(0.0, 0.04)), "#985808")

    def test_unknown_mesh_recipe_is_refused(self):
        with self.assertRaises(ValueError):
            bl.vfx_mesh(recipe="hoop", game="_test", id="x", out_dir=os.path.join(self.out, "x"))


class ShellFractureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        bl.new_scene()
        cls.out = bl.util.resolve(".logs/vfx test/shell")
        shutil.rmtree(cls.out, ignore_errors=True)
        cls.m = bl.vfx_fracture(game="_test", id="shell", pieces=8, seed=3, size=0.45, shape="sphere", out_dir=cls.out,
                                preview=False)

    def test_eight_closed_pieces_fill_the_shell(self):
        f = self.m["checks"]["fracture"]
        self.assertEqual((f["shape"], f["pieces"], len(self.m["pieces"])), ("sphere", 8, 8))
        self.assertAlmostEqual(f["volume_ratio"], 1.0, delta=0.03)
        self.assertGreater(f["outer_faces"], 0)
        self.assertGreater(f["inner_faces"], 0)
        self.assertGreater(f["wall_faces"], 0)
        self.assertEqual(self.m["checks"]["non_manifold"], 0)
        self.assertTrue(self.m["checks"]["passed"])
        for size in self.m["stats"]["size_m"]:
            self.assertAlmostEqual(size, 0.45, delta=0.01)
        self.assertEqual(bl.inspect(os.path.join(self.out, "shell.glb"))["open_edges"], 0)

    def test_pieces_wear_the_bomb_skin(self):
        self.assertEqual([t["size"] for t in self.m["stats"]["textures"]], [[512, 256]])
        names = bl.load(os.path.join(self.out, "shell.glb"))
        self.assertEqual(sorted(n for n in names if bpy.data.objects[n].type == "MESH"),
                         ["piece_{:02d}".format(i) for i in range(8)])
        img = _image_pixels()
        _assert_color(self, _texel(img, *bl.vfx.ball_uv(*BASE_DIR)), "#333436")
        _assert_color(self, _texel(img, *bl.vfx.ball_uv(*SEAM_DIR)), "#d56a30")

    def test_same_seed_same_shell(self):
        again = bl.vfx_fracture(game="_test", id="shell2", pieces=8, seed=3, size=0.45, shape="sphere",
                                out_dir=bl.util.resolve(".logs/vfx test/shell2"), preview=False)
        self.assertEqual([p["volume"] for p in again["pieces"]], [p["volume"] for p in self.m["pieces"]])
```

- [ ] **Step 3: Run to see them fail**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test test_vfx.py`
Expected: `StillTests` errors `ValueError: recipe must be one of dust_ring, fire_toon, …, got 'floor_crack'` (and `'lettering'`;
`test_lettering_refuses_an_unknown_style` passes for the wrong reason until the recipe exists), `MeshTests` error in
`setUpClass` `AttributeError: module 'bl' has no attribute 'vfx_mesh'`, `ShellFractureTests` error
`TypeError: vfx_fracture() got an unexpected keyword argument 'shape'`.

- [ ] **Step 4: Stills — `floor_crack`, `lettering`** (in `tools/bl/vfx.py`)

(a) The util import becomes:
```python
from .util import log, node_tree, principled, resolve, run_background, world_bounds
```

(b) After `_speed_lines` (before `RECIPES`), add:
```python
CRACK_DARK, CRACK_EDGE = _srgb("#1d1626"), _srgb("#fff1d6")
FONT = "fonts/LilitaOne-Regular.ttf"                     # the game's font (OFL), copied from Unity MCP Games/Pota/Art/Fonts
LETTER_STYLES = {                                        # bottom → top fill
    "slam": ("#ff6a14", "#ffd23f"),                      # orange → yellow
    "swish": ("#3cc8f5", "#ffffff"),                     # cyan → white
    "combo": ("#e89a12", "#fff09a"),                     # deep gold → light gold
}
LETTER_INK, LETTER_SHADOW = _srgb("#16224a"), _srgb("#0a1024")


def _flat_material(name, color):
    """Unlit flat colour (display value)."""
    material = bpy.data.materials.new(name)
    tree = node_tree(material)
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (*(_linear(c) for c in color), 1.0)
    tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    return material


def _jagged(rng, start, angle, length, width, steps, jitter):
    """A crack's polyline in the board plane (x, z) from `start`, turning up to ±jitter per step, tapering to a point."""
    points, widths = [Vector(start)], [width]
    p, a = Vector(start), angle
    for step in range(steps):
        a += rng.uniform(-jitter, jitter)
        p = p + Vector((math.cos(a), math.sin(a))) * (length / steps)
        points.append(p.copy())
        widths.append(width * (1.0 - (step + 1) / float(steps)))
    return points, widths


def _strip(bm, points, widths, y, shift):
    """A flat tapering ribbon along the polyline at depth y, moved by `shift` (x, z)."""
    left, right = [], []
    for i, p in enumerate(points):
        along = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        side = Vector((-along.y, along.x)).normalized() * (widths[i] * 0.5)
        left.append(bm.verts.new((p.x + side.x + shift[0], y, p.y + side.y + shift[1])))
        right.append(bm.verts.new((p.x - side.x + shift[0], y, p.y - side.y + shift[1])))
    for i in range(len(points) - 1):
        if widths[i + 1] <= 1e-6:
            bm.faces.new((left[i], right[i], left[i + 1]))
        else:
            bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))


def _floor_crack(u, seed):
    """Court's slam (spec 2): a star-shaped crack decal — a dark crater, nine jagged tapering rays (most with a branch) and a pale
    chipped edge peeking out down-right, which reads as depth on any board colour. A still: u is ignored."""
    rng = random.Random(seed)
    crater = []
    for k in range(12):
        a = 2.0 * math.pi * k / 12.0
        r = 0.11 * rng.uniform(0.8, 1.25)
        crater.append(Vector((math.cos(a) * r, math.sin(a) * r)))
    rays = []
    for k in range(9):
        a = 2.0 * math.pi * k / 9.0 + rng.uniform(-0.2, 0.2)
        points, widths = _jagged(rng, Vector((0.0, 0.0)), a, rng.uniform(0.6, 0.95), rng.uniform(0.075, 0.1), 5, 0.35)
        rays.append((points, widths))
        if rng.random() < 0.75:
            b = a + rng.choice((-1.0, 1.0)) * rng.uniform(0.5, 0.8)
            rays.append(_jagged(rng, points[2], b, rng.uniform(0.22, 0.38), widths[2] * 0.9, 3, 0.3))
    out = []
    for name, color, y, grow, shift in (("vfx_crack", CRACK_DARK, 0.0, 1.0, (0.0, 0.0)),
                                        ("vfx_crack_edge", CRACK_EDGE, 0.02, 1.2, (0.018, -0.018))):
        bm = bmesh.new()
        bm.faces.new([bm.verts.new((p.x * grow + shift[0], y, p.y * grow + shift[1])) for p in crater])
        for points, widths in rays:
            _strip(bm, points, [w * grow for w in widths], y, shift)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(_flat_material(name, color))
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        out.append(obj)
    return out


def _gradient_toon(name, bottom, top, shade=(0.78, 0.76, 0.9), threshold=0.45):
    """Two-tone toon over a bottom→top gradient on the object's generated Y (the text's up): lit = the gradient, shade = the
    gradient × `shade` (a linear multiplier). `bottom`, `top` as shown on screen."""
    material = bpy.data.materials.new(name)
    tree = node_tree(material)
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    diffuse = tree.nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = tree.nodes.new("ShaderNodeShaderToRGB")
    tone = tree.nodes.new("ShaderNodeValToRGB")
    tone.color_ramp.interpolation = "CONSTANT"
    tone.color_ramp.elements[0].position = 0.0
    tone.color_ramp.elements[0].color = (*shade, 1.0)
    tone.color_ramp.elements[1].position = threshold
    tone.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1.0)
    coord = tree.nodes.new("ShaderNodeTexCoord")
    split = tree.nodes.new("ShaderNodeSeparateXYZ")
    gradient = tree.nodes.new("ShaderNodeValToRGB")
    gradient.color_ramp.elements[0].position = 0.15
    gradient.color_ramp.elements[0].color = (*(_linear(c) for c in bottom), 1.0)
    gradient.color_ramp.elements[1].position = 0.85
    gradient.color_ramp.elements[1].color = (*(_linear(c) for c in top), 1.0)
    multiply = tree.nodes.new("ShaderNodeVectorMath")
    multiply.operation = "MULTIPLY"
    emission = tree.nodes.new("ShaderNodeEmission")
    tree.links.new(diffuse.outputs["BSDF"], to_rgb.inputs["Shader"])
    tree.links.new(to_rgb.outputs["Color"], tone.inputs["Fac"])
    tree.links.new(coord.outputs["Generated"], split.inputs["Vector"])
    tree.links.new(split.outputs["Y"], gradient.inputs["Fac"])
    tree.links.new(gradient.outputs["Color"], multiply.inputs[0])
    tree.links.new(tone.outputs["Color"], multiply.inputs[1])
    tree.links.new(multiply.outputs["Vector"], emission.inputs["Color"])
    tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])
    return material


def _lettering(u, seed, text="SLAM!", style="slam"):
    """Court's lettering (spec 2): a chunky extruded word in Lilita One (the game's font), toon-lit over a bottom→top gradient,
    leaning back so the extrusion shows under the letters; centred and fitted into 1.74 × 0.74 m of the 2 × 1 m frame, leaving
    room for the outline and shadow the post step adds. A still: u is ignored."""
    if style not in LETTER_STYLES:
        raise ValueError("style must be one of {}, got '{}'".format(", ".join(sorted(LETTER_STYLES)), style))
    curve = bpy.data.curves.new("vfx_text", "FONT")
    curve.font = bpy.data.fonts.load(resolve(FONT), check_existing=True)
    curve.body = text
    curve.size = 1.0
    curve.align_x = "CENTER"
    curve.align_y = "CENTER"
    curve.space_character = 0.96
    curve.extrude = 0.12
    curve.bevel_depth = 0.035
    curve.bevel_resolution = 3
    bottom, top = LETTER_STYLES[style]
    curve.materials.append(_gradient_toon("vfx_letters", _srgb(bottom), _srgb(top)))
    obj = bpy.data.objects.new("vfx_text", curve)
    bpy.context.scene.collection.objects.link(obj)
    obj.rotation_euler = (math.radians(76.0), 0.0, math.radians(-4.0))   # faces −Y with its top leaning back, a slight tilt
    bpy.context.view_layer.update()
    lo, hi = world_bounds([obj])
    s = min(1.74 / (hi.x - lo.x), 0.74 / (hi.z - lo.z))
    obj.scale = (s, s, s)
    bpy.context.view_layer.update()
    lo, hi = world_bounds([obj])
    obj.location = (-(lo.x + hi.x) * 0.5, 0.0, -(lo.z + hi.z) * 0.5)
    return [obj]


def _shift(a, dx, dy):
    """`a` (rows from the bottom) moved dx px right and dy px up; what moves in is 0."""
    out = np.zeros_like(a)
    h, w = a.shape[:2]
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = a[ys, xs]
    return out


def _dilate(alpha, radius):
    """Max of `alpha` over a disc of `radius` px: the outline's reach, anti-aliased edges kept."""
    out = alpha.copy()
    for dy in range(-radius, radius + 1):
        for dx in range(-radius, radius + 1):
            if dx * dx + dy * dy <= radius * radius:
                out = np.maximum(out, _shift(alpha, dx, dy))
    return out


def _over(top_rgb, top_a, bottom_rgb, bottom_a):
    """Straight-alpha 'over'."""
    a = top_a + bottom_a * (1.0 - top_a)
    rgb = (top_rgb * top_a[..., None] + bottom_rgb * (bottom_a * (1.0 - top_a))[..., None]) / np.maximum(a, 1e-6)[..., None]
    return rgb, a


def _letter_outline(tile, params):
    """The lettering's post step: a navy outline (the letters' alpha dilated by 2.2 % of the width) and a darker drop shadow down
    right (1.2 % / 1.8 %), composited under the letters in 2D (plan decision 4)."""
    h, w = tile.shape[:2]
    ring = _dilate(tile[..., 3], max(2, int(round(0.022 * w))))
    shadow = _shift(ring, max(1, int(round(0.012 * w))), -max(1, int(round(0.018 * w))))
    ink = np.broadcast_to(np.array(LETTER_INK, dtype=np.float32), tile[..., :3].shape)
    dark = np.broadcast_to(np.array(LETTER_SHADOW, dtype=np.float32), tile[..., :3].shape)
    rgb, a = _over(ink, ring, dark, shadow)
    rgb, a = _over(tile[..., :3], tile[..., 3], rgb, a)
    out = np.empty_like(tile)
    out[..., :3] = rgb
    out[..., 3] = a
    return out
```
and add two entries at the end of `RECIPES`:
```python
    "floor_crack": {"build": _floor_crack, "ortho": 2.2, "view": "Standard", "linear": False, "ink": None},
    "lettering": {"build": _lettering, "ortho": 2.0, "view": "Standard", "linear": False, "ink": None, "post": _letter_outline},
```

- [ ] **Step 5: Skins and `vfx_mesh`** (in `tools/bl/vfx.py`, after `vfx_flipbook`)

```python
BALL_R = 0.225                         # 0.45 m: Court's ball, the line ball's halves, the bomb shell
SKIN_W, SKIN_H = 512, 256
SKIN_TOP = 0.86                        # the lat-long fills v 0 … 0.86; to 0.875 the pole row repeats (a filtering guard)
SWATCH_V = 0.9375                      # v 0.875 … 1: flat swatches; non-ball faces map to a swatch centre
SWATCH_U = {"cap": 0.125, "inner": 0.375, "wall": 0.625}
BALL_TAGS = {1: "cap", 2: "inner", 3: "wall"}   # face.material_index while building; 0 = the ball's skin
SEAM_K = math.radians(43.6)            # Pota topKureDokusu: the curved seams swing 62 px of 512 off the meridian at the equator
SEAM_HALF = 0.05                       # rad, half a seam (Pota's 7 px line on a 256 px height is 0.043)
SKINS = {                              # display colours sampled from Pota's sprites (plan decision 2)
    "line": {"base": "#fd8232", "seam": "#2a2626", "cap": "#ffc07a", "inner": "#ffc07a", "wall": "#e8702c"},      # ozel-cizgi-a
    "classic": {"base": "#f07d1d", "seam": "#241611", "cap": "#ffb36b", "inner": "#c95f17", "wall": "#f07d1d"},   # KLASIK_DERI
    "bomb": {"base": "#333436", "seam": "#d56a30", "cap": "#4a4b50", "inner": "#1b1b1e", "wall": "#4a4b50"},      # ozel-bomba-a
}
COIN_R = 0.15
COIN_PROFILE = (                       # (radius, y) from the front centre (−Y) round the rim to the back centre
    (0.0, -0.020), (0.080, -0.020), (0.084, -0.014), (0.112, -0.014), (0.116, -0.022), (0.140, -0.022), (0.150, -0.012),
    (0.150, 0.012), (0.140, 0.022), (0.116, 0.022), (0.112, 0.014), (0.084, 0.014), (0.080, 0.020), (0.0, 0.020))
COIN_COLORS = {"gold": "#f6c630", "light": "#fde962", "line": "#985808", "rim": "#da8c1c"}   # ozel-altin-a
COIN_RIM_UV = (0.75, 0.5)


def ball_uv(x, y, z):
    """UV of a direction on the ball skin: lat-long with the poles on ±X (the halves' noses), u = 0.5 at the front (−Y), the
    wrap at the back (+Y), v scaled into 0 … SKIN_TOP."""
    n = math.sqrt(x * x + y * y + z * z) or 1.0
    lat = math.asin(max(-1.0, min(1.0, x / n)))
    lon = math.atan2(z / n, -y / n)
    return lon / (2.0 * math.pi) + 0.5, (lat / math.pi + 0.5) * SKIN_TOP


def coin_uv(x, z):
    """UV of a point (m) on the coin's faces: the skin's left half is the face, COIN_R across."""
    return (x / COIN_R) * 0.25 + 0.25, (z / COIN_R) * 0.5 + 0.5


def _seam_distance(x, y, z):
    """Angular distance (rad) of unit directions to the nearest seam: the great circle x = 0, the equator z = 0 and the two
    lenses around ±X whose curves run pole to pole (±Z) at longitude ±SEAM_K·sin(polar angle)."""
    d = np.minimum(np.abs(np.arcsin(np.clip(x, -1.0, 1.0))), np.abs(np.arcsin(np.clip(z, -1.0, 1.0))))
    s = np.sqrt(x * x + y * y)                       # sin of the polar angle from ±Z
    for axis in (1.0, -1.0):
        lam = np.arctan2(y, axis * x)                # longitude around Z from +X (or −X)
        for side in (1.0, -1.0):
            d = np.minimum(d, np.abs(lam - side * SEAM_K * s) * s)
    return d


def _ball_skin(skin):
    """Pota's ball skin on the lat-long of ball_uv (display values, rows from the bottom) plus the swatch strip on top."""
    colors = {key: np.array(_srgb(code), dtype=np.float32) for key, code in SKINS[skin].items()}
    v = (np.arange(SKIN_H, dtype=np.float32) + 0.5) / SKIN_H
    u = (np.arange(SKIN_W, dtype=np.float32) + 0.5) / SKIN_W
    lat = np.clip((v / SKIN_TOP - 0.5) * np.pi, -0.5 * np.pi, 0.5 * np.pi)[:, None]
    lon = ((u - 0.5) * 2.0 * np.pi)[None, :]
    x = np.sin(lat) * np.ones_like(lon)
    y = -np.cos(lat) * np.cos(lon)
    z = np.cos(lat) * np.sin(lon)
    t = np.clip((_seam_distance(x, y, z) - SEAM_HALF) / 0.015, 0.0, 1.0)[..., None]
    t = t * t * (3.0 - 2.0 * t)
    pixels = np.ones((SKIN_H, SKIN_W, 4), dtype=np.float32)
    pixels[..., :3] = colors["seam"] * (1.0 - t) + colors["base"] * t
    strip = v >= 0.875
    pixels[strip, :, :3] = colors["base"]
    for tag, centre in SWATCH_U.items():
        pixels[strip, int((centre - 0.125) * SKIN_W):int((centre + 0.125) * SKIN_W), :3] = colors[tag]
    return pixels


def _coin_skin():
    """The coin's skin (256 × 128): left half the face — gold field, a light raised rim, the raised ball with its seams in dark
    amber (ring, cross, two arcs); right half the rim's side swatch."""
    h, w = 128, 256
    gold, light, line, rim = (np.array(_srgb(COIN_COLORS[k]), dtype=np.float32) for k in ("gold", "light", "line", "rim"))
    u = (np.arange(w, dtype=np.float32) + 0.5) / w
    v = (np.arange(h, dtype=np.float32) + 0.5) / h
    x = ((u[None, :] - 0.25) / 0.25) * COIN_R
    z = ((v[:, None] - 0.5) / 0.5) * COIN_R
    r = np.sqrt(x * x + z * z)
    inside = r < 0.08
    d = np.where(inside, np.minimum(np.abs(x), np.abs(z)), 1.0)
    for cx in (0.118, -0.118):
        d = np.where(inside, np.minimum(d, np.abs(np.hypot(x - cx, z) - 0.072)), d)
    d = np.minimum(d, np.abs(r - 0.079))                      # the ball's outline
    t = np.clip((d - 0.0045) / 0.002, 0.0, 1.0)[..., None]
    field = np.where((r > 0.112)[..., None], light, gold)
    pixels = np.ones((h, w, 4), dtype=np.float32)
    pixels[..., :3] = line * (1.0 - t) + field * t
    pixels[:, w // 2:, :3] = rim
    return pixels


def _skin_image(name, pixels, work):
    """Writes the skin as a PNG in `work`, loads and packs it, so the glTF exporter embeds it in the GLB."""
    path = os.path.join(work, name + ".png")
    _save(pixels, path)
    image = bpy.data.images.load(path)
    image.name = name
    image.pack()
    return image


def _skin_material(name, image):
    material = bpy.data.materials.new(name)
    node = principled(material)
    texture = node_tree(material).nodes.new("ShaderNodeTexImage")
    texture.image = image
    node_tree(material).links.new(texture.outputs["Color"], node.inputs["Base Color"])
    node.inputs["Roughness"].default_value = 0.6
    node.inputs["Metallic"].default_value = 0.0
    return material


def _skin_uvs(bm, uv):
    """Ball faces (tag 0) get ball_uv of their vertex directions from the bmesh origin (the ball's centre): a face across the back
    wrap gets u + 1 on its low side, a pole loop takes its face's mean u. Tagged faces get their swatch's centre."""
    for face in bm.faces:
        tag = BALL_TAGS.get(face.material_index)
        if tag is not None:
            for loop in face.loops:
                loop[uv].uv = (SWATCH_U[tag], SWATCH_V)
            continue
        entries = []
        for loop in face.loops:
            co = loop.vert.co
            u, v = ball_uv(co.x, co.y, co.z)
            entries.append([loop, u, v, abs(co.x) >= 0.9999 * co.length])
        free = [e for e in entries if not e[3]]
        if free and max(e[1] for e in free) - min(e[1] for e in free) > 0.5:
            for e in free:
                if e[1] < 0.5:
                    e[1] += 1.0
        mean = sum(e[1] for e in free) / len(free) if free else 0.5
        for loop, u, v, pole in entries:
            loop[uv].uv = (mean if pole else u, v)


def _sphere(radius, segments=32, rings=16):
    """A UV sphere with its poles on ±X (the halves' noses), as a bmesh."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius,
                              matrix=Matrix.Rotation(math.radians(90.0), 4, "Y"))
    return bm


def _object(name, bm, material, location=(0.0, 0.0, 0.0)):
    bm.normal_update()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _half_ball(keep_positive):
    """One half of the ball cut on the x = 0 seam: closed by a fan cap (tag 1) around the origin, the cap's centre."""
    bm = _sphere(BALL_R)
    doomed = [v for v in bm.verts if (v.co.x < -1e-6 if keep_positive else v.co.x > 1e-6)]
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")
    rim = [e for e in bm.edges if e.is_boundary]
    filled = bmesh.ops.holes_fill(bm, edges=rim, sides=0)["faces"]
    for face in bmesh.ops.poke(bm, faces=filled)["faces"]:
        face.material_index = 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm
```

```python
def _mesh_ball_halves(seed, work):
    """Royal's line ball split in two on its x = 0 seam (plan decision 3): HalfA is the −X half as it is, HalfB the +X half turned
    180° about Z, so both noses (domes) point to −X (Unity +X), both caps lie in x = 0 facing +X with the origin at their centre,
    and both wear their own side of one skin (UVs taken before the turn). Preview offsets z ±0.3."""
    material = _skin_material("ball_halves_skin", _skin_image("ball_halves_skin", _ball_skin("line"), work))
    out = []
    for name, keep_positive, z in (("HalfA", False, 0.3), ("HalfB", True, -0.3)):
        bm = _half_ball(keep_positive)
        _skin_uvs(bm, bm.loops.layers.uv.new("UVMap"))
        for face in bm.faces:
            face.smooth = face.material_index == 0
            face.material_index = 0
        if keep_positive:
            bmesh.ops.rotate(bm, verts=bm.verts, cent=(0.0, 0.0, 0.0), matrix=Matrix.Rotation(math.pi, 3, "Z"))
        out.append(_object(name, bm, material, (0.0, 0.0, z)))
    return out


def _mesh_basketball(seed, work):
    """Court's ball: a 0.45 m UV sphere (32 × 16, 960 tris) in Pota's classic skin, smooth shaded for the toon ramp."""
    material = _skin_material("basketball_skin", _skin_image("basketball_skin", _ball_skin("classic"), work))
    bm = _sphere(BALL_R)
    _skin_uvs(bm, bm.loops.layers.uv.new("UVMap"))
    for face in bm.faces:
        face.smooth = True
    return [_object("Basketball", bm, material)]


def _lathe(profile, segments):
    """A closed solid of revolution about Y from (radius, y) points; points on the axis (radius 0) become single vertices."""
    bm = bmesh.new()
    rings = []
    for r, y in profile:
        if r == 0.0:
            rings.append([bm.verts.new((0.0, y, 0.0))])
        else:
            rings.append([bm.verts.new((r * math.cos(2.0 * math.pi * s / segments), y,
                                        r * math.sin(2.0 * math.pi * s / segments))) for s in range(segments)])
    for a, b in zip(rings, rings[1:]):
        for s in range(segments):
            t = (s + 1) % segments
            if len(a) == 1:
                bm.faces.new((a[0], b[t], b[s]))
            elif len(b) == 1:
                bm.faces.new((a[s], a[t], b[0]))
            else:
                bm.faces.new((a[s], a[t], b[t], b[s]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    return bm


def _mesh_coin(seed, work):
    """Royal's golden-ball coin: a 0.30 m gold disc, 0.044 m thick, faces along ±Y (front −Y), a raised rim and a raised ball whose
    seams are painted (coin_uv); the back face reads unmirrored from behind. Faces flat, the rim's side smooth."""
    material = _skin_material("coin_skin", _skin_image("coin_skin", _coin_skin(), work))
    bm = _lathe(COIN_PROFILE, 36)
    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        flat = abs(face.normal.y) > 0.6
        face.smooth = not flat
        for loop in face.loops:
            co = loop.vert.co
            loop[uv].uv = coin_uv(co.x if face.normal.y < 0.0 else -co.x, co.z) if flat else COIN_RIM_UV
    return [_object("Coin", bm, material)]


MESH_RECIPES = {
    "ball_halves": {"build": _mesh_ball_halves, "objects": ["HalfA", "HalfB"], "tris_max": 600},
    "basketball": {"build": _mesh_basketball, "objects": ["Basketball"], "tris_max": 1500},
    "coin": {"build": _mesh_coin, "objects": ["Coin"], "tris_max": 1200},
}


def _mesh_checks(objects):
    """Triangles per object and edges not shared by exactly two faces after welding by position (0 = closed)."""
    tris, open_edges = {}, 0
    for obj in objects:
        tris[obj.name] = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        open_edges += sum(1 for e in bm.edges if len(e.link_faces) != 2)
        bm.free()
    return {"objects": sorted(tris), "tris": tris, "open_edges": open_edges}


def vfx_mesh(recipe, game, id, seed=7, out_dir=None, preview=True):
    """Small effect meshes with an embedded skin (plan decision 1), exported through bl.export as kind vfx-mesh."""
    if not bpy.app.background:
        return run_background("vfx_mesh", {"recipe": recipe, "game": game, "id": id, "seed": seed, "out_dir": out_dir,
                                           "preview": preview})
    if recipe not in MESH_RECIPES:
        raise ValueError("recipe must be one of {}, got '{}'".format(", ".join(sorted(MESH_RECIPES)), recipe))
    spec = MESH_RECIPES[recipe]
    new_scene()
    work = resolve(".logs/vfx/{}".format(id))
    os.makedirs(work, exist_ok=True)
    checks = _mesh_checks(spec["build"](seed, work))
    checks["passed"] = (checks["objects"] == sorted(spec["objects"]) and checks["open_edges"] == 0
                        and all(n <= spec["tris_max"] for n in checks["tris"].values()))
    out_dir = resolve(out_dir or "assets/{}/{}/export".format(game, id))
    manifest = export(out_dir=out_dir, id=id, game=game, kind="vfx-mesh",
                      source={"recipe": "vfx_mesh:" + recipe, "params": {"seed": seed}}, preview=preview)
    manifest["checks"]["mesh"] = checks
    manifest["checks"]["passed"] = bool(manifest["checks"]["passed"] and checks["passed"])
    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
    log("vfx_mesh {} ({}): {}, open edges {} -> {}".format(
        id, recipe, ", ".join("{} {} tris".format(k, v) for k, v in sorted(checks["tris"].items())), checks["open_edges"],
        out_dir))
    return manifest
```

- [ ] **Step 6: `vfx_fracture` learns the sphere shell** (in `tools/bl/vfx.py`)

Keep `_pillow`, `_seeds`, `_cell` as they are. Replace `vfx_fracture` with the four functions below (`_pillow_fracture` is today's
pillow body moved out unchanged, so the pillow tests and `vfx-pillow-shatter` stay the same):
```python
def _pillow_fracture(pieces, seed, size, radius, bulge, segments):
    """Pota's pillow block cut into Voronoi pieces (2026-09-30 plan Task 3): UV0 front planar, UV1.x front flag."""
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
            face.smooth = face.material_index == 0          # the block's skin shades smooth like PillowBlockMesh; cuts stay flat
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
    ratio = total / whole
    fracture = {"pieces": len(made), "volume_ratio": round(ratio, 4), "front_faces": front_faces,
                "front_uv_range": [round(lo, 4), round(hi, 4)]}
    fracture["passed"] = (len(made) == pieces and 0.97 <= ratio <= 1.03 and front_faces > 0
                          and lo >= -1e-4 and hi <= 1.0 + 1e-4)
    return made, fracture


def _shell_seeds(count, rng):
    """`count` directions spread evenly over the sphere (a Fibonacci spiral) under a random turn, each nudged a little."""
    turn = Matrix.Rotation(rng.uniform(0.0, 2.0 * math.pi), 3, "Z") @ Matrix.Rotation(rng.uniform(0.0, math.pi), 3, "X")
    golden = math.pi * (3.0 - math.sqrt(5.0))
    seeds = []
    for i in range(count):
        z = 1.0 - 2.0 * (i + 0.5) / count
        ring = math.sqrt(max(0.0, 1.0 - z * z))
        a = golden * i + rng.uniform(-0.2, 0.2)
        seeds.append((turn @ Vector((ring * math.cos(a), ring * math.sin(a), z))).normalized())
    return seeds


def _shell_piece(radius, inner, seeds, index):
    """Seed `index`'s piece of a hollow ball (plan decision 5): the sphere's surface cut by the planes through the centre halfway
    between its seed and every other seed (a spherical Voronoi cell), then thickened inward along the radius — an inner copy
    (tag 2, reversed) and walls on the cut planes (tag 3) — so neighbouring pieces fit. Outer faces keep tag 0. No normal
    recalculation: the uncut ball is two shells (outer, inner) and must keep the inner one facing inward."""
    bm = _sphere(radius)
    for j, other in enumerate(seeds):
        if j == index:
            continue
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=(0.0, 0.0, 0.0),
                               plane_no=(other - seeds[index]).normalized(), clear_outer=True)
    outer = list(bm.faces)
    rims = []
    for edge in bm.edges:
        if edge.is_boundary:
            loop = edge.link_loops[0]
            rims.append((loop.vert, loop.link_loop_next.vert))   # the outer face walks a → b
    twin = {vert: bm.verts.new(vert.co * (inner / radius)) for vert in list(bm.verts)}
    for face in outer:
        face.material_index = 0
        bm.faces.new([twin[vert] for vert in list(face.verts)[::-1]]).material_index = 2
    for a, b in rims:
        bm.faces.new((b, a, twin[a], twin[b])).material_index = 3   # walks b → a: the edge's other side
    bm.normal_update()
    return bm


def _shell_fracture(pieces, seed, radius, thickness, skin, work):
    """A hollow ball in `skin` cut into `pieces` shell pieces; one shared material with the embedded skin; origin = piece centre."""
    os.makedirs(work, exist_ok=True)
    name = skin + "_shell"
    material = _skin_material(name, _skin_image(name, _ball_skin(skin), work))
    inner = radius - thickness
    whole_bm = _shell_piece(radius, inner, [Vector((1.0, 0.0, 0.0))], 0)   # one seed: no cut, the whole shell
    whole = whole_bm.calc_volume(signed=False)
    whole_bm.free()
    seeds = _shell_seeds(pieces, random.Random(seed))
    made, total, complete = [], 0.0, True
    counts = {"outer_faces": 0, "inner_faces": 0, "wall_faces": 0}
    for index in range(pieces):
        bm = _shell_piece(radius, inner, seeds, index)
        tags = [face.material_index for face in bm.faces]
        counts["outer_faces"] += tags.count(0)
        counts["inner_faces"] += tags.count(2)
        counts["wall_faces"] += tags.count(3)
        complete = complete and tags.count(0) > 0 and tags.count(3) > 0
        _skin_uvs(bm, bm.loops.layers.uv.new("UVMap"))
        for face in bm.faces:
            face.smooth = face.material_index != 3
            face.material_index = 0
        volume = bm.calc_volume(signed=False)
        total += volume
        centre = sum((vert.co for vert in bm.verts), Vector()) / len(bm.verts)
        bmesh.ops.translate(bm, vec=-centre, verts=bm.verts)
        obj = _object("piece_{:02d}".format(index), bm, material, centre)
        made.append({"name": obj.name, "center": [round(c, 4) for c in centre], "volume": round(volume, 6)})
    ratio = total / whole
    fracture = dict(counts, shape="sphere", pieces=len(made), volume_ratio=round(ratio, 4))
    fracture["passed"] = len(made) == pieces and 0.97 <= ratio <= 1.03 and complete
    return made, fracture


def vfx_fracture(game, id, pieces=10, seed=7, size=0.9, radius=0.22, bulge=0.1, segments=5, out_dir=None, preview=True,
                 shape="pillow", thickness=0.035, skin="bomb"):
    if shape not in ("pillow", "sphere"):
        raise ValueError("shape must be 'pillow' or 'sphere', got '{}'".format(shape))
    if shape == "sphere" and skin not in SKINS:
        raise ValueError("skin must be one of {}, got '{}'".format(", ".join(sorted(SKINS)), skin))
    if shape == "sphere":
        params = {"pieces": pieces, "seed": seed, "size": size, "shape": shape, "thickness": thickness, "skin": skin}
    else:
        params = {"pieces": pieces, "seed": seed, "size": size, "radius": radius, "bulge": bulge, "segments": segments}
    if not bpy.app.background:
        return run_background("vfx_fracture", dict(params, game=game, id=id, out_dir=out_dir, preview=preview))
    new_scene()
    if shape == "sphere":
        made, fracture = _shell_fracture(pieces, seed, size * 0.5, thickness, skin, resolve(".logs/vfx/{}".format(id)))
    else:
        made, fracture = _pillow_fracture(pieces, seed, size, radius, bulge, segments)
    out_dir = resolve(out_dir or "assets/{}/{}/export".format(game, id))
    manifest = export(out_dir=out_dir, id=id, game=game, kind="vfx-mesh",
                      source={"recipe": "vfx_fracture", "params": params}, preview=preview)
    manifest["pieces"] = made
    manifest["checks"]["fracture"] = fracture
    manifest["checks"]["passed"] = bool(manifest["checks"]["passed"] and fracture["passed"])
    with open(os.path.join(out_dir, "manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
    log("vfx_fracture {} ({}): {} pieces, volume {:.3f} of the whole -> {}".format(
        id, shape, len(made), fracture["volume_ratio"], out_dir))
    return manifest
```

`tools/bl/__init__.py`: `from .vfx import vfx_flipbook, vfx_fracture` becomes `from .vfx import vfx_flipbook, vfx_fracture, vfx_mesh`, and the
`TOOLS` dict's last line becomes
```python
         "vfx_flipbook": vfx_flipbook, "vfx_fracture": vfx_fracture, "vfx_mesh": vfx_mesh}
```

- [ ] **Step 7: Run the tests**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test test_vfx.py` → Expected: `Ran 24 tests … OK`.
If something fails, fix the code, not the test:
- `open_edges` > 0 on the halves → the cap fan did not close the rim (check `holes_fill` got every boundary edge and `poke` returned
  faces); on the shell (`bl.inspect`) → a wall is missing or wound the same way as its outer face (compare `rims` with
  `edge.link_loops[0]`), or bisect left a sliver edge shorter than 1e-6 that the weld collapses (raise `dist` to 1e-5).
- `volume_ratio` far from 1 → the inner copy is not reversed (volume adds instead of subtracting): check `[::-1]`.
- A colour sample off by more than 0.03 → print `ball_uv(*BASE_DIR)` and the texel; a seam may pass closer than computed
  (`_seam_distance`) — move the sample direction only if the painted skin is right by eye in Step 9.
- Lettering coverage or width outside the bounds → read `.logs/vfx/slam/f_00.png` and fix the fit box in `_lettering`.

- [ ] **Step 8: Make Pota's hand-offs**

```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
tools/run.sh vfx_flipbook '{"recipe": "floor_crack", "game": "Pota", "id": "vfx-floor-crack", "frames": 1, "cols": 1, "frame_px": 512, "duration": 0.6}'
tools/run.sh vfx_flipbook '{"recipe": "lettering", "game": "Pota", "id": "vfx-letter-slam", "frames": 1, "cols": 1, "frame_px": 512, "frame_h": 256, "duration": 0.5, "params": {"text": "SLAM!", "style": "slam"}}'
tools/run.sh vfx_flipbook '{"recipe": "lettering", "game": "Pota", "id": "vfx-letter-swish", "frames": 1, "cols": 1, "frame_px": 512, "frame_h": 256, "duration": 0.5, "params": {"text": "SWISH!", "style": "swish"}}'
tools/run.sh vfx_flipbook '{"recipe": "lettering", "game": "Pota", "id": "vfx-letter-combo", "frames": 1, "cols": 1, "frame_px": 512, "frame_h": 256, "duration": 0.5, "params": {"text": "COMBO!", "style": "combo"}}'
tools/run.sh vfx_mesh '{"recipe": "ball_halves", "game": "Pota", "id": "vfx-ball-halves"}'
tools/run.sh vfx_mesh '{"recipe": "basketball", "game": "Pota", "id": "vfx-basketball"}'
tools/run.sh vfx_mesh '{"recipe": "coin", "game": "Pota", "id": "vfx-coin"}'
tools/run.sh vfx_fracture '{"game": "Pota", "id": "vfx-bomb-shell", "pieces": 8, "size": 0.45, "shape": "sphere", "thickness": 0.035, "skin": "bomb"}'
```
Expected lines: `[bl] vfx_flipbook vfx-floor-crack (floor_crack): 1 frames on 512x512 px, empty [] -> …`, three
`… vfx-letter-* (lettering): 1 frames on 512x256 px, empty [] -> …`, `[bl] vfx_mesh vfx-ball-halves (ball_halves): HalfA 512 tris,
HalfB 512 tris, open edges 0 -> …`, `… vfx-basketball (basketball): Basketball 960 tris, open edges 0 …`, `… vfx-coin (coin): Coin 864
tris, open edges 0 …`, `[bl] vfx_fracture vfx-bomb-shell (sphere): 8 pieces, volume ~1.000 of the whole -> …`, and an
`[bl] export … checks ok` line before each mesh. Every manifest's `checks.passed` is `true`
(`grep -h '"passed"' assets/Pota/vfx-{floor-crack,letter-*,ball-halves,basketball,coin,bomb-shell}/export/manifest.json`).

- [ ] **Step 9: Look at every preview** (Read tool on each `assets/Pota/<id>/export/preview.png`, and Pota's sprites
`~/projects/Pota/public/assets/ozel-{cizgi,bomba,altin}-a.png` beside them)

- **floor crack:** a dark irregular crater, nine jagged tapering rays (most with a branch), a thin pale edge on their lower right;
  reads as a crack, not a spider or a sun; nothing clipped.
- **letters:** SLAM! orange→yellow, SWISH! cyan→white, COMBO! gold, chunky Lilita One, a darker extrusion visible under the letters,
  an even thick navy outline and a shadow down-right; the counters of O, B, A stay open; no letter or outline clipped.
- **ball halves:** two domes stacked (z ±0.3), both noses to −X (the "right" view shows the round side of both); seams look like
  `ozel-cizgi-a`'s ball (equator, curved lens seam, the cut along a seam), lighter orange flat caps; same tone as the sprite.
- **basketball:** Pota's salvo ball: orange `#f07d1d`, dark seams, the curved seams on the sides; 960 tris is still round enough.
- **coin:** a gold disc with a raised rim and a raised ball with dark-amber seams (ring, cross, two arcs), darker rim side.
- **bomb shell:** the whole charcoal ball with orange seams like `ozel-bomba-a`, the wireframe tile shows eight shell pieces whose
  cut walls are visible; no holes, no faces poking out.
A visible flaw is fixed in the recipe numbers (quality rule) with a ledger ruling naming the number changed; rerun Steps 7–8.

- [ ] **Step 10: Full suite and commit**

Run: `cd "/Users/tunaaltin17/projects/Blender MCP" && tools/run.sh test` → Expected: all pass (83 + 10 = 93).
```bash
cd "/Users/tunaaltin17/projects/Blender MCP"
git add fonts tools/bl/vfx.py tools/bl/__init__.py tests/test_vfx.py \
  assets/Pota/vfx-floor-crack assets/Pota/vfx-letter-slam assets/Pota/vfx-letter-swish assets/Pota/vfx-letter-combo \
  assets/Pota/vfx-ball-halves assets/Pota/vfx-basketball assets/Pota/vfx-coin assets/Pota/vfx-bomb-shell
git commit -m "feat: vfx_mesh (ball halves, basketball, coin) with embedded Pota skins, sphere-shell vfx_fracture, crack and lettering stills

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
(The tool table in the workshop's `CLAUDE.md` is updated in Task 13.)

---

### Task 9: Unity — import the special-ball hand-offs, the toon template, `SpecialMeshes`

**Repo:** `/Users/tunaaltin17/projects/Unity MCP`.

**Files:**
- Modify: `Assets/_Project/Core/Editor/AITools/ImportAssetTool.cs` (`frame_h`, `Verify`)
- Test: `Assets/_Project/Core/Tests/EditMode/ImportAssetToolTests.cs`
- Modify: `Assets/_Project/Games/Pota/Editor/StudioImportTool.cs` (17 ids, new settings wiring)
- Modify: `Assets/_Project/Games/Pota/Editor/PotaMaterialTemplates.cs` (`PotaToon` template)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs` (12 asset fields + `ToonTemplate`)
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/SpecialMeshes.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/StudioAssetsTests.cs`, `PotaMaterialTemplatesTests.cs`; `RuntimeShaderBuildTests.cs`
  unchanged (it now covers `Project/Toon` through `SpecialMeshes`)
- Output: `Assets/_Project/Games/Pota/Art/VFX/{vfx-fire-toon, vfx-flame-jet, vfx-dust-ring, vfx-speed-lines, vfx-floor-crack,
  vfx-letter-slam, vfx-letter-swish, vfx-letter-combo, vfx-ball-halves, vfx-bomb-shell, vfx-coin, vfx-basketball}/*`,
  `Games/Pota/Materials/Templates/PotaToon.mat`, `Games/Pota/Settings/ColorHuntSettings.asset`

**Interfaces:**
- Consumes: the Blender hand-offs of Tasks 7–8 at `~/projects/Blender MCP/assets/Pota/<id>/export/manifest.json`.
- Produces: `ColorHuntSettings` fields `Texture2D RoyalFire, FlameJet, CourtDust, SpeedLines, FloorCrack, LetterSlam, LetterSwish,
  LetterCombo; GameObject RoyalHalves, RoyalShell, RoyalCoin, CourtBall; Material ToonTemplate` (shared contract), and
  `ImportAssetTool.Verify`, `SpecialMeshes` (Contract additions). Tasks 10–11 consume them.

**Decisions (ledger):**
1. **No `FlipbookLoopTemplate`.** `Project/PotaFlipbook` just samples the UVs the particle's texture-sheet animation gives it; a
   loop is `textureSheetAnimation.cycleCount = life / 0.3`. Same template, same material.
2. **Flipbook import settings stay as for the studio sheets** (`ConfigureSheet`: sRGB, alpha is transparency, clamp, mipmaps,
   uncompressed, no NPOT scaling); the stills (crack, letters) go through the same path as 1×1 flipbooks.
3. **`flipbook.frame_h` is read by `MeasureSheet`** (default `frame_px`), so 512×256 letters and 1024×512 sheets match;
   `flipbook.loop` is informational.
4. **The toon skin moves at runtime** (`SpecialMeshes.Toon`): the GLB's glTFast material keeps the embedded texture; the toon
   material is cloned from `ToonTemplate` (build-safe, `RuntimeShaderBuildTests`) and gets the skin as `_BaseMap`.
5. **`Verify`** lets EditMode tests prove `matches` on the imported files without copying from the workshop again.

- [ ] **Step 1: Write the failing importer tests** (add to `ImportAssetToolTests`, after `Flipbook_WithoutSheet_IsRefused`)

```csharp
        [Test]
        public void Flipbook_FrameHeight_MeasuresAWideSheet()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "handoff-flipbook-wide"));
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            File.WriteAllBytes(Path.Combine(dir, "fixture-flipbook.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), File.ReadAllText(FlipbookFixture).Replace(
                "\"cols\": 2, \"rows\": 2, \"frames\": 4, \"frame_px\": 64,",
                "\"cols\": 1, \"rows\": 1, \"frames\": 1, \"frame_px\": 64, \"frame_h\": 32,"));
            var r = ImportAssetTool.Import(Path.Combine(dir, "manifest.json"), Root + "/wide");
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            CollectionAssert.AreEqual(new[] { 64, 32 }, r.sheetSize);
            CollectionAssert.AreEqual(new[] { 64, 32 }, r.manifestSheetSize);
        }

        [Test]
        public void Verify_MeasuresAnImportedHandoff_WithoutCopying()
        {
            ImportAssetTool.Import(Fixture, Dest);
            var r = ImportAssetTool.Verify(Dest + "/manifest.json", Dest);
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            Assert.AreEqual(12, r.unityTris);
            Assert.AreEqual(Dest + "/handoff-cube.glb", r.main);
            Assert.IsEmpty(r.copied);
        }
```

- [ ] **Step 2: Write the failing template test** (add to `PotaMaterialTemplatesTests`; add `using Project.ColorHunt;`)

```csharp
        [Test]
        public void Ensure_WiresTheToonTemplate()
        {
            PotaMaterialTemplates.Ensure();
            var m = AssetDatabase.LoadAssetAtPath<Material>(PotaMaterialTemplates.Folder + "/PotaToon.mat");
            Assert.IsNotNull(m);
            Assert.AreEqual("Project/Toon", m.shader.name);
            var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");
            Assert.AreEqual(m, s.ToonTemplate, "ColorHuntSettings.ToonTemplate keeps Project/Toon in a Pota build");
        }
```

- [ ] **Step 3: Write the failing studio-asset tests** (`StudioAssetsTests.cs`)

Add `using System.IO;` and `using Project.Editor.AITools;` to its usings, change the class summary to "The Blender hand-offs for the
studio and special looks are in the project and wired into the settings asset.", and add inside the class:
```csharp
        private const string SpecialHint = "run the Blender tools (plan 2026-10-01 Tasks 7–8), then execute_custom_tool ai_studio_import";

        private static readonly string[] SpecialIds =
        {
            "vfx-fire-toon", "vfx-flame-jet", "vfx-dust-ring", "vfx-speed-lines", "vfx-floor-crack",
            "vfx-letter-slam", "vfx-letter-swish", "vfx-letter-combo",
            "vfx-ball-halves", "vfx-bomb-shell", "vfx-coin", "vfx-basketball",
        };

        [Test]
        public void SpecialHandoffs_AreImported_AndMatchTheirManifests()
        {
            foreach (string id in SpecialIds)
            {
                CollectionAssert.Contains(StudioImportTool.Ids, id, "ai_studio_import must bring " + id);
                string folder = StudioImportTool.Folder(id);
                Assert.IsTrue(File.Exists(folder + "/manifest.json"), id + ": " + SpecialHint);
                var r = ImportAssetTool.Verify(folder + "/manifest.json", folder);
                Assert.IsTrue(r.matches, id + ": " + string.Join("; ", r.differences));
            }
        }

        [Test]
        public void SpecialSheets_HaveTheContractSizes()
        {
            var s = Settings();
            var sheets = new (Texture2D tex, string field, int w, int h)[]
            {
                (s.RoyalFire, "RoyalFire", 1024, 1024), (s.FlameJet, "FlameJet", 1024, 512), (s.CourtDust, "CourtDust", 1024, 1024),
                (s.SpeedLines, "SpeedLines", 1024, 512), (s.FloorCrack, "FloorCrack", 512, 512),
                (s.LetterSlam, "LetterSlam", 512, 256), (s.LetterSwish, "LetterSwish", 512, 256), (s.LetterCombo, "LetterCombo", 512, 256),
            };
            foreach (var (tex, field, w, h) in sheets)
            {
                Assert.IsNotNull(tex, field + ": " + SpecialHint);
                Assert.AreEqual(w, tex.width, field);
                Assert.AreEqual(h, tex.height, field);
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                Assert.IsTrue(imp.alphaIsTransparency, field);
                Assert.IsTrue(imp.sRGBTexture, field + " holds colour: sRGB");
            }
            StringAssert.Contains("\"loop\": true", File.ReadAllText(StudioImportTool.Folder("vfx-flame-jet") + "/manifest.json"),
                "the flame jet is the looping sheet");
        }

        [Test]
        public void RoyalHalves_AreTwoHalvesWithTheirNoseAlongPlusX()
        {
            var model = Settings().RoyalHalves;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            CollectionAssert.AreEqual(new[] { "HalfA", "HalfB" }, parts.ConvertAll(p => p.Name));
            foreach (var p in parts)
            {
                var b = p.Mesh.bounds;
                Assert.AreEqual(0f, b.min.x, 0.002f, p.Name + ": the flat cap sits at x = 0 (origin at the cap centre)");
                Assert.AreEqual(0.225f, b.max.x, 0.005f, p.Name + ": the dome (nose) points to +X");
                Assert.AreEqual(0.45f, b.size.y, 0.01f, p.Name);
                Assert.AreEqual(0.45f, b.size.z, 0.01f, p.Name);
                int cap = 0;
                foreach (var n in p.Mesh.normals) if (n.x < -0.99f) cap++;
                Assert.Greater(cap, 8, p.Name + ": the cap faces −X");
                Assert.AreEqual(p.Mesh.vertexCount, p.Mesh.uv.Length, p.Name + " UV0");
                Assert.LessOrEqual((int)p.Mesh.GetIndexCount(0) / 3, 600, p.Name);
            }
            AssertSkin(model, 512, 256);
        }

        [Test]
        public void RoyalShell_IsEightPiecesOfABall()
        {
            var model = Settings().RoyalShell;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            Assert.AreEqual(8, parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                Assert.AreEqual($"piece_{i:00}", parts[i].Name);
                Assert.AreEqual(parts[i].Mesh.vertexCount, parts[i].Mesh.uv.Length, parts[i].Name + " UV0");
                Assert.Less(parts[i].Local.magnitude, 0.25f, parts[i].Name + ": its centre lies inside the 0.45 m ball");
                Assert.Greater(parts[i].Local.magnitude, 0.05f, parts[i].Name + ": a shell piece sits off the centre");
            }
            AssertSkin(model, 512, 256);
        }

        [Test]
        public void RoyalCoin_IsAGoldDiscFacingZ()
        {
            var model = Settings().RoyalCoin;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            Assert.AreEqual(1, parts.Count);
            var size = parts[0].Mesh.bounds.size;
            Assert.AreEqual(0.30f, size.x, 0.005f);
            Assert.AreEqual(0.30f, size.y, 0.005f);
            Assert.AreEqual(0.044f, size.z, 0.004f, "the coin's faces look along ±z");
            AssertSkin(model, 256, 128);
        }

        [Test]
        public void CourtBall_IsALowPolyBall()
        {
            var model = Settings().CourtBall;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            Assert.AreEqual(1, parts.Count);
            Assert.LessOrEqual((int)parts[0].Mesh.GetIndexCount(0) / 3, 1500);
            Assert.AreEqual(0.45f, parts[0].Mesh.bounds.size.x, 0.01f);
            Assert.AreEqual(0.45f, parts[0].Mesh.bounds.size.y, 0.01f);
            AssertSkin(model, 512, 256);
        }

        /// <summary>The GLB carries its skin, and the toon material made from the template wears it.</summary>
        private static void AssertSkin(GameObject model, int w, int h)
        {
            var skin = SpecialMeshes.Skin(model);
            Assert.IsNotNull(skin, model.name + ": no embedded base-colour texture");
            Assert.AreEqual(w, skin.width, model.name);
            Assert.AreEqual(h, skin.height, model.name);
            var m = SpecialMeshes.Toon(Settings(), model, "test");
            Assert.AreEqual("Project/Toon", m.shader.name);
            Assert.AreEqual(skin, m.GetTexture("_BaseMap"));
            Object.DestroyImmediate(m);
        }
```

- [ ] **Step 4: Run them to see them fail**

Run: `cd "/Users/tunaaltin17/projects/Unity MCP" && ~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ImportAssetToolTests Project.Tests.StudioAssetsTests Project.Tests.PotaMaterialTemplatesTests`
Expected: exit 2 with `error CS0117: 'ImportAssetTool' does not contain a definition for 'Verify'`,
`error CS1061: 'ColorHuntSettings' does not contain a definition for 'RoyalFire'` (and the other new fields) and
`error CS0103: The name 'SpecialMeshes' does not exist in the current context`.

- [ ] **Step 5: `ImportAssetTool`: `frame_h`, `Verify`**

Replace the whole `Import` method with:
```csharp
        public static Result Import(string manifestPath, string dest = null)
        {
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("No manifest at " + manifestPath);
            var m = JObject.Parse(File.ReadAllText(manifestPath));
            string id = Required(m, "id"), game = Required(m, "game"), kind = Required(m, "kind");
            if (!KindFolders.TryGetValue(kind, out string kindFolder))
                throw new ArgumentException($"Unknown kind '{kind}' (known: {string.Join(", ", KindFolders.Keys)}).");
            var files = m["files"] as JObject ?? throw new ArgumentException("manifest has no 'files'.");
            bool flipbook = kind == "vfx-flipbook";
            string mainName = MainName(files, flipbook);
            JObject fb = null;
            if (flipbook) fb = m["flipbook"] as JObject ?? throw new ArgumentException("a vfx-flipbook manifest has no 'flipbook' block.");

            var r = new Result { id = id, folder = dest ?? $"Assets/_Project/Games/{game}/Art/{kindFolder}/{id}" };
            AIToolsCommon.EnsureFolder(r.folder);
            string sourceDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            string glb = files.Value<string>("glb"), fbx = files.Value<string>("fbx");
            foreach (string name in new[] { glb, fbx, flipbook ? mainName : null, "manifest.json" }.Where(n => !string.IsNullOrEmpty(n)))
            {
                string source = name == "manifest.json" ? manifestPath : Path.Combine(sourceDir, name);
                if (!File.Exists(source)) throw new FileNotFoundException("Hand-off file missing: " + source);
                string target = $"{r.folder}/{name}";
                File.Copy(source, target, true); // in place: the existing .meta (GUID) stays
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                r.copied.Add(target);
            }
            r.main = $"{r.folder}/{mainName}";
            if (flipbook) ConfigureSheet(r.main, fb.Value<bool?>("linear") ?? false);
            Check(r, m, flipbook);
            return r;
        }

        /// <summary>
        /// Measures a hand-off already imported into <paramref name="folder"/> against its manifest, copying and reconfiguring
        /// nothing (tests: the special-look assets match what the workshop measured).
        /// </summary>
        public static Result Verify(string manifestPath, string folder)
        {
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("No manifest at " + manifestPath);
            var m = JObject.Parse(File.ReadAllText(manifestPath));
            var files = m["files"] as JObject ?? throw new ArgumentException("manifest has no 'files'.");
            bool flipbook = Required(m, "kind") == "vfx-flipbook";
            var r = new Result { id = Required(m, "id"), folder = folder };
            r.main = $"{folder}/{MainName(files, flipbook)}";
            Check(r, m, flipbook);
            return r;
        }

        private static string MainName(JObject files, bool flipbook)
        {
            string glb = files.Value<string>("glb"), fbx = files.Value<string>("fbx"), sheet = files.Value<string>("sheet");
            if (flipbook && string.IsNullOrEmpty(sheet)) throw new ArgumentException("a vfx-flipbook manifest lists no 'sheet'.");
            string main = flipbook ? sheet : !string.IsNullOrEmpty(glb) ? glb : fbx;
            if (string.IsNullOrEmpty(main)) throw new ArgumentException("manifest lists neither a glb nor an fbx.");
            return main;
        }

        private static void Check(Result r, JObject m, bool flipbook)
        {
            if (flipbook)
            {
                MeasureSheet(r, m["flipbook"] as JObject ?? throw new ArgumentException("a vfx-flipbook manifest has no 'flipbook' block."));
                return;
            }
            Measure(r);
            Compare(r, m["stats"] as JObject);
        }
```
In `MeasureSheet`, replace
```csharp
            int px = fb.Value<int?>("frame_px") ?? 0;
            r.sheetSize = new[] { tex.width, tex.height };
            r.manifestSheetSize = new[] { (fb.Value<int?>("cols") ?? 0) * px, (fb.Value<int?>("rows") ?? 0) * px };
```
with
```csharp
            int px = fb.Value<int?>("frame_px") ?? 0, ph = fb.Value<int?>("frame_h") ?? px;   // frame_h: wide stills (512×256)
            r.sheetSize = new[] { tex.width, tex.height };
            r.manifestSheetSize = new[] { (fb.Value<int?>("cols") ?? 0) * px, (fb.Value<int?>("rows") ?? 0) * ph };
```

- [ ] **Step 6: Settings fields** (`ColorHuntSettings.cs`)

After `public Material ShockTemplate;` add:
```csharp
        [Tooltip("Core Project/Toon: the special looks' Blender meshes with their embedded skin as base map (SpecialMeshes.Toon).")]
        public Material ToonTemplate;
```
After `public GameObject ShatterPieces;` add:
```csharp
        [Header("Special looks (Blender hand-offs, ai_studio_import; plan 2026-10-01 Tasks 7–9)")]
        [Tooltip("1 Royal: vfx_flipbook fire_toon, inked cartoon fireball, 4×4 frames of 256 px, 0.45 s one-shot; ends in grey smoke.")]
        public Texture2D RoyalFire;
        [Tooltip("1 + 2: vfx_flipbook flame_jet, LOOP of 4×2 frames of 256 px, one cycle 0.3 s; nozzle at frame (0.5, 0.9), flame toward −y.")]
        public Texture2D FlameJet;
        [Tooltip("2 Court: vfx_flipbook dust_ring, inked court-tan ring laid face-on, 4×4 frames of 256 px, 0.4 s; open centre.")]
        public Texture2D CourtDust;
        [Tooltip("2 Court: vfx_flipbook speed_lines, horizontal streaks trailing to −x behind a ball at the right edge, 4×2 of 256 px, 0.25 s.")]
        public Texture2D SpeedLines;
        [Tooltip("2 Court: vfx_flipbook floor_crack, one 512 px still (dark star crack, pale chipped edge); fade by alpha.")]
        public Texture2D FloorCrack;
        [Tooltip("2 Court: vfx_flipbook lettering, 512×256 stills in Lilita One with a navy outline: draw on a 2:1 quad or size3D.")]
        public Texture2D LetterSlam, LetterSwish, LetterCombo;
        [Tooltip("1 Royal: vfx_mesh ball_halves. Children HalfA, HalfB: nose along local +X, flat cap at x = 0 facing −X, origin at the " +
                 "cap centre; the whole ball = HalfB turned 180° about y against HalfA. Import offsets y ±0.3 are for the preview only.")]
        public GameObject RoyalHalves;
        [Tooltip("1 Royal: vfx_fracture sphere, a 0.45 m bomb in eight shell pieces piece_00…07 (origin = piece centre).")]
        public GameObject RoyalShell;
        [Tooltip("1 Royal: vfx_mesh coin, a 0.30 m gold coin with a raised ball, faces along ±z.")]
        public GameObject RoyalCoin;
        [Tooltip("2 Court: vfx_mesh basketball, a 0.45 m low-poly ball in Pota's classic skin.")]
        public GameObject CourtBall;
```

- [ ] **Step 7: `SpecialMeshes`** — create `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/SpecialMeshes.cs`
(the `Specials` folder exists if Task 5 ran; otherwise this creates it and Unity writes `Specials.meta`):
```csharp
using System;
using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The Blender meshes of the special looks (ai_studio_import: RoyalHalves, RoyalShell, RoyalCoin, CourtBall). Each GLB carries its
    /// skin as an embedded base-colour texture on glTFast's own material ("baseColorTexture"); Core Project/Toon reads _BaseMap with
    /// UV0 (no vertex colours), so <see cref="Toon"/> moves the skin onto a toon material cloned from the settings' template.
    /// Axes: workshop front −Y → Unity +z and workshop +X → Unity −X, so the halves' noses point along local +X and the coin faces ±z.
    /// </summary>
    public static class SpecialMeshes
    {
        /// <summary>The workshop's cartoon ink #141b2e (sRGB).</summary>
        public static readonly Color Ink = new Color(0.078f, 0.106f, 0.18f);

        public struct Part
        {
            public string Name;
            public Mesh Mesh;
            /// <summary>The child's position in the model's space (shell pieces: the piece's centre).</summary>
            public Vector3 Local;
        }

        /// <summary>The first base-colour texture on the model's renderers; null when the model is missing or untextured.</summary>
        public static Texture Skin(GameObject model)
        {
            if (model == null) return null;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    foreach (string name in material.GetTexturePropertyNames())
                        if (name.IndexOf("baseColor", StringComparison.OrdinalIgnoreCase) >= 0 || name == "_BaseMap" || name == "_MainTex")
                        {
                            var texture = material.GetTexture(name);
                            if (texture != null) return texture;
                        }
                }
            return null;
        }

        /// <summary>Project/Toon from <see cref="ColorHuntSettings.ToonTemplate"/> wearing the model's skin, with an ink outline
        /// <paramref name="outline"/> metres wide.</summary>
        public static Material Toon(ColorHuntSettings s, GameObject model, string name, float outline = 0.008f)
        {
            var m = RuntimeMaterial.From(s != null ? s.ToonTemplate : null, "Project/Toon", name);
            var skin = Skin(model);
            if (skin != null) m.SetTexture("_BaseMap", skin);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_OutlineColor", Ink);
            m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        /// <summary>The model's meshes with their node names and positions, sorted by name (HalfA, HalfB; piece_00 …).</summary>
        public static List<Part> Parts(GameObject model)
        {
            var parts = new List<Part>();
            if (model == null) return parts;
            var root = model.transform;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    parts.Add(new Part { Name = filter.name, Mesh = filter.sharedMesh, Local = root.InverseTransformPoint(filter.transform.position) });
            parts.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return parts;
        }
    }
}
```

- [ ] **Step 8: The toon template** (`PotaMaterialTemplates.cs`)

After `var shock = Template("PotaShockRing", RuntimeMaterial.From(null, "Project/PotaShockRing"));` add
```csharp
            var toon = Template("PotaToon", RuntimeMaterial.From(null, "Project/Toon"));
```
after `s.FlipbookTemplate = flipbook; s.CandyShardTemplate = candyShard; s.PieceTemplate = piece; s.ShockTemplate = shock;` add
```csharp
                s.ToonTemplate = toon;
```
and replace the report line `report.Add($"material templates: 10 in {Folder}, …` with a count that stays right when other tasks add
templates:
```csharp
            report.Add($"material templates: {AssetDatabase.FindAssets("t:Material", new[] { Folder }).Length} in {Folder}, " +
                       $"wired into {colorHunt} ColorHuntSettings + {basket} BasketSettings");
```
(If Task 3 or 5 added `Template(…)` lines or settings assignments here, keep them.)

- [ ] **Step 9: `ai_studio_import` brings the twelve new hand-offs** — `StudioImportTool.cs` becomes:
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
    /// Imports the Blender workshop's hand-offs for the studio looks (Candy A, Shatter B) and the special-ball looks (Royal 1,
    /// Court 2) through ai_import_asset and wires them into ColorHuntSettings. Idempotent: same ids keep their GUIDs.
    /// </summary>
    [McpForUnityTool("ai_studio_import", Description =
        "Import the Blender hand-offs of the studio looks (vfx-puff-toon, vfx-ring-toon, vfx-smoke-lit, vfx-shock-ring, " +
        "vfx-pillow-shatter) and the special-ball looks (vfx-fire-toon, vfx-flame-jet, vfx-dust-ring, vfx-speed-lines, " +
        "vfx-floor-crack, vfx-letter-slam/-swish/-combo, vfx-ball-halves, vfx-bomb-shell, vfx-coin, vfx-basketball) into " +
        "Games/Pota/Art/VFX and wire them into ColorHuntSettings. Params: workshop (default ~/projects/Blender MCP).")]
    public static class StudioImportTool
    {
        public const string DefaultWorkshop = "~/projects/Blender MCP";
        public static readonly string[] Ids =
        {
            "vfx-puff-toon", "vfx-ring-toon", "vfx-smoke-lit", "vfx-shock-ring", "vfx-pillow-shatter",
            "vfx-fire-toon", "vfx-flame-jet", "vfx-dust-ring", "vfx-speed-lines", "vfx-floor-crack",
            "vfx-letter-slam", "vfx-letter-swish", "vfx-letter-combo",
            "vfx-ball-halves", "vfx-bomb-shell", "vfx-coin", "vfx-basketball",
        };

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
                return new SuccessResponse("Studio and special looks imported.", new { steps = report });
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
            s.ShatterPieces = Model("vfx-pillow-shatter");
            s.RoyalFire = Sheet("vfx-fire-toon");
            s.FlameJet = Sheet("vfx-flame-jet");
            s.CourtDust = Sheet("vfx-dust-ring");
            s.SpeedLines = Sheet("vfx-speed-lines");
            s.FloorCrack = Sheet("vfx-floor-crack");
            s.LetterSlam = Sheet("vfx-letter-slam");
            s.LetterSwish = Sheet("vfx-letter-swish");
            s.LetterCombo = Sheet("vfx-letter-combo");
            s.RoyalHalves = Model("vfx-ball-halves");
            s.RoyalShell = Model("vfx-bomb-shell");
            s.RoyalCoin = Model("vfx-coin");
            s.CourtBall = Model("vfx-basketball");
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            report.Add($"settings: puff={s.CandyPuff != null} ring={s.CandyRing != null} smoke={s.ShatterSmoke != null} " +
                       $"shock={s.ShockRing != null} pieces={s.ShatterPieces != null}");
            report.Add($"special settings: fire={s.RoyalFire != null} jet={s.FlameJet != null} dust={s.CourtDust != null} " +
                       $"lines={s.SpeedLines != null} crack={s.FloorCrack != null} letters={s.LetterSlam != null && s.LetterSwish != null && s.LetterCombo != null} " +
                       $"halves={s.RoyalHalves != null} shell={s.RoyalShell != null} coin={s.RoyalCoin != null} ball={s.CourtBall != null}");
            report.AddRange(PotaMaterialTemplates.Ensure());
            return report;
        }

        private static Texture2D Sheet(string id) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder(id)}/{id}.png");

        private static GameObject Model(string id) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder(id)}/{id}.glb");
    }
}
```

- [ ] **Step 10: Compile, import, run the tests**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/mcp_call.py call manage_editor '{"action":"stop"}'
~/.local/bin/uv run Tools/mcp_call.py call refresh_unity '{"mode":"force","scope":"all","compile":"request","wait_for_ready":true}'
~/.local/bin/uv run Tools/mcp_call.py call read_console '{"types":["error"],"filter_text":"error CS"}'
~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_studio_import","parameters":{}}'
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ImportAssetToolTests Project.Tests.StudioAssetsTests Project.Tests.PotaMaterialTemplatesTests Project.Tests.RuntimeShaderBuildTests --no-compile
```
Expected: no `error CS`; the import reports `matches` for all 17 ids, `special settings: fire=True jet=True dust=True lines=True
crack=True letters=True halves=True shell=True coin=True ball=True` and `material templates: N in …/Templates` (N = 11 if no other task
added one); every listed test passes (`RuntimeShaderBuildTests` now finds `Project/Toon` through `SpecialMeshes` and must still pass:
the template keeps the shader in the build).
- A `tris manifest … vs Unity …` difference on a mesh means glTFast and Blender counted different triangles: read both numbers,
  compare with the workshop's `checks.mesh.tris`, fix the side that is wrong (an n-gon left untriangulated, a stray object), rerun
  Task 8 Step 8 and this step; do not accept a mismatch.
- A `sheet manifest … vs Unity …` difference on a letter or a 4×2 sheet means `frame_h` was not read: check Step 5's `MeasureSheet`.
- `RoyalHalves_…` failing on `b.min.x`/`b.max.x` means the nose is on −X in Unity: Task 8's halves were authored the other way
  round (glTFast negates x); fix the workshop (dome toward −X there), not this test.

Then the whole EditMode suite: `~/.local/bin/uv run Tools/unity_tests.py EditMode` → Expected: all pass (count = before + 9).

- [ ] **Step 11: Commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
git add Assets/_Project/Core/Editor/AITools/ImportAssetTool.cs Assets/_Project/Core/Tests/EditMode/ImportAssetToolTests.cs \
  Assets/_Project/Games/Pota/Editor/StudioImportTool.cs Assets/_Project/Games/Pota/Editor/PotaMaterialTemplates.cs \
  Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs \
  Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials.meta \
  Assets/_Project/Games/Pota/Tests/EditMode/StudioAssetsTests.cs Assets/_Project/Games/Pota/Tests/EditMode/PotaMaterialTemplatesTests.cs \
  Assets/_Project/Games/Pota/Materials/Templates Assets/_Project/Games/Pota/Art/VFX \
  Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset
git status --short
git commit -m "feat: ai_studio_import brings the special-ball hand-offs; Project/Toon template and SpecialMeshes; ai_import_asset reads frame_h and verifies in place

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Expected: `git status --short` before the commit lists only these paths (the 12 new `Art/VFX/<id>` folders with their `.meta`
files, `PotaToon.mat(.meta)`, `SpecialMeshes.cs(.meta)`); nothing else changed. The Core change to `ImportAssetTool` does not reach
`~/projects/Kaptan` by itself (Task 13 notes it in the backlog).

---

### Task 10: 1 · Royal — rockets, fireball, coins (`RoyalSpecials`)

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/RoyalSpecials.cs` (`RoyalTuning` + `RoyalSpecials`)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntCamera.cs` (`AxisShake`),
  `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs` (`Royal`), `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs`
  (one `CreateSpecialStyle` case)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/RoyalTests.cs` (new), `Assets/_Project/Games/Pota/Tests/EditMode/ColorHuntCameraTests.cs`,
  `Assets/_Project/Games/Pota/Tests/PlayMode/RoyalPlayTests.cs` (new)
- Output: `Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset` (serializes the `Royal` block),
  `docs/screenshots/2026-10-01-specials-royal-{line,bomb,birth,golden}.jpg`
- No new shader and no new material template: the halves, shell and coins use Task 9's `ToonTemplate` (`SpecialMeshes.Toon`), the
  flipbooks and the flame jet `FlipbookTemplate`, flashes/stars/sparks `AdditiveTemplate`, the bomb's wind-up copy reuses the hidden
  block's own materials, the screen flash is a uGUI `Image` (built-in UI shader). `RuntimeShaderBuildTests` sees no new name.

**Interfaces:**
- Consumes:
  - Task 2: `SpecialKind`, `Block.Special/IsSpecial/MakeSpecial`, `Board`, `Step` (`Exploded`, `Triggered`, `Born`), `Features.All`,
    `ColorHuntRules.SingleArea/Blast/Break/Index/Row/Col`, `Target`.
  - Task 3: `SpecialLook`, `ColorHuntSettings.SfxLine/SfxBomb/SfxGoldenBorn/SfxGoldenLand`, `LabelAtlas.SpecialCell`.
  - Task 4: `BlockView.Pulse/Bob/Tone/LabelAngle/Wiggle/Scale/Squash/Flash/Pos/Home/Exploding`, `ColorHuntGame.Intro`, `PotaLook.SpecialScale`, `GateRings.Flash`, `ColorHuntGame.Gates`.
  - Task 5: `ISpecialStyle`, `SpecialTiming` (+`GatherTime`), `SpecialFire` (`Time`, `Order`, `At`, `Pace`), `TriggerPlan` (`Start`,
    `Fires`, `Pace`), `SpecialSchedule.Plan/MaxStep`, `GoldenRoute` (+`Scale`), `GoldenPath.At/PastGate/Landed`, `PotaSpecials.GoldenFlash`,
    `SpecialTexts.Born`, `ColorHuntGame.Specials/SetSpecialLook/UseSpecialStyle/CellBurst/GoldenLanded/GoldenInFlight/Hoop/ViewAt`,
    `ColorHuntGame.CreateSpecialStyle/AddSpecialStyle`, `ColorHuntFx.Glow(…, z)`, `PotaSounds.PlayClip`, `GiantHoop.Swing`.
  - Task 6: `SpecialsDemo` (`Play`, `SetLook`, `Game`, `Finished`, `ScenarioFinished`), `SpecialScenario`, `SpecialScenarios.Board/Taps`,
    `ColorHuntRules.FromPreset`, `Scenes/Specials.unity`.
  - Task 9: `ColorHuntSettings.RoyalFire/FlameJet/RoyalHalves/RoyalShell/RoyalCoin/ToonTemplate`, `SpecialMeshes.Parts/Toon`; the studio
    `CandyPuff/CandyRing/FlipbookTemplate`; the Pota look's `SoftDisc/StarSprite/AdditiveTemplate`.
  - Existing: `StudioParticles`, `StudioTextures.RayStar`, `StudioCurves.EaseOutCubic`, `ShatterDebris`/`Debris`, `BurstBudget`,
    `ColorHuntJuice` (`Shake`, `TimeCtl.SlowMo/Dip`, `Popups`, `Sounds`), `PotaSounds.Synth/Combo`, `ColorHuntHud` (its canvas root).
- Produces: the **Contract additions** above.

**Rulings (ledger):**
1. **The rockets fly the schedule's own formula.** A rocket is `RocketCells(f, t, age) = max(0, age/Pace − LineLead)·LineSpeed` cells out,
   so it is over cell k exactly at `RocketReach = f.Time + (LineLead + k/LineSpeed)·Pace`, the plan's burst moment of that cell (pinned to
   1 ms for a whole 7×7 column and for a compressed line → bomb chain). Inside the board the sweep is linear (the blocks must burst under
   it); the spec's "ivmeli" shows past the last block, where the rocket speeds up (`ExitAccel` 60 cells/s²) to the frame edge.
2. **The style's clock is the step's clock.** `[DefaultExecutionOrder(100)]` runs `RoyalSpecials.Update` after `ColorHuntGame.Update`
   with the same `min(Time.deltaTime, 0.05)`. `TriggerStep` zeroes `_stepClock` at the step's start; a fire called the frame the step's
   clock passes `f.Time` starts its effect at `_stepClock − f.Time` (≤ 0 now), so after this frame's tick its age is exactly the step's
   overshoot `st.T − f.Time`.
3. **The bomb winds up on a copy of its block** (see "No `Arm` hook"). The copy takes the hidden view's mesh, body material, tone and
   sticker (`BlockView`'s `Item` child, by name, as the existing tests do), so the swap at the fire shows nothing; the 3×3's views are
   pushed out 0.1 cell at the explosion only while `Exploding` (this step's bursting blocks, recycled at the drops).
4. **The golden ball flies as a big spinning gold coin** (`RoyalCoin`, 0.85 cell): Pota's golden sticker (`ozel-altin-a.png`) is a
   coin-with-a-ball, so the coin token reads as the same object and the landing's coin burst continues it. `GateRings.Flash` is called by
   the style as in `PotaSpecials` (Task 5), plus a gold ray star and twinkles at the ring.
5. **The 2+ specials flash is a full-screen white `Image` under the HUD canvas** (first sibling: the counters stay readable). A
   camera-facing quad would need its own material template, a near-plane placement that follows the camera fit, and sorting against the
   particles; the `Image` costs one quad, uses the built-in UI shader and is captured by the frame recorder, which already moves the HUD
   canvas into its frames. It fades on game time (0.1 s ≈ 0.2 s real inside the ×0.5 slow motion it starts with). The flash and slow
   motion start at the step's second fire (the chain moment), not at the tap.
6. **Axis shake is a camera channel** (`ColorHuntCamera.AxisShake`), not Core's `CameraShake` (Core stays shared with Kaptan untouched);
   without a `ColorHuntCamera` the line falls back to `Shake.Add(0.12)`.
7. **The fuse tip is measured, not guessed:** the spark on Pota's `ozel-bomba-a.png` has its centroid at u 0.79, v 0.90 from the bottom
   (PIL, 2026-10-01; the brief's (0.73, 0.92) was an estimate). Drawn "payli" into the middle 88 % of the sticker quad
   (`LabelQuad · SpecialScale` = 0.857 cell): `FuseTip = ((0.06 + 0.88·0.79 − 0.5)·Q, (0.06 + 0.88·0.90 − 0.5)·Q)` ≈ (0.219, 0.302) cell.
8. **Ink weight** is `RoyalTuning.InkWidth` = 0.022 world units (Project/Toon's outline is world-space; A's candy chunks use 0.022), coins
   ×0.7. The spec's style-match risk is judged by eye in Step 10 against the glossy blocks.
10. **Royal's scale life rides `BlockView.Scale`** (see "The seam is unchanged"): the pop (0 → 1.2 → 1), the bomb's breath (±3 %), a
    gathered block's shrink (→ 0.35) write `Scale`; `Idle` skips while `game.Intro`.
9. **Synth sounds are deterministic** (PCG hash noise, no `Random`), so every recording sounds the same; samples keep Pota's jitter.

**Budget (spec §1.6, Global Constraints):** the Pota burst pools (`BurstBudget.Pota` = 684) still draw every normal block, so Royal's own
live particles and pieces must stay ≤ `BurstBudget.Max − BurstBudget.Pota` = 1026 − 684 = **342**. Every pool is capped:

| Pool | Max | Draws |
|---|---|---|
| `RoyalFlash` (additive soft disc) | 16 | white core flashes (birth, line, bomb 0.08 s), exit flashes |
| `RoyalStars` (`StudioTextures.RayStar`) | 8 | birth star, rocket exit, gate, landing |
| `RoyalTwinkles` (Pota star sprite) | 96 | birth twinkles, golden idle twinkles, glitter trail, gate, landing stars |
| `RoyalSparks` (stretched soft disc) | 96 | fuse (idle + wind-up), rocket trails and exits, bomb |
| `RoyalPuffs` (`CandyPuff`, grey) | 32 | rocket trail (one per half cell), bomb (4–6) |
| `RoyalFire` (`RoyalFire` 4×4) | 4 | fireballs |
| `RoyalRings` (`CandyRing` 4×4) | 6 | shock rings |
| rockets (half + flame jet, ×2) | 6 → 12 | three lines at once |
| bomb copies | 3 | wind-ups at once |
| shell pieces | 24 | three bombs × 8 |
| coins | 24 | two landings × 10 + spare |
| golden coin tokens | 4 | flights |
| **total** | **325 ≤ 342** | `RoyalSpecials.PoolCapacity ≤ RoyalSpecials.Cap` (EditMode) |

- [ ] **Step 1: Write the failing EditMode tests**

Create `Assets/_Project/Games/Pota/Tests/EditMode/RoyalTests.cs`:
```csharp
using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>1 · Royal (spec 2026-10-01 §2): readiness, the rockets keep the schedule's beat, the budget, the spec's numbers.</summary>
    public class RoyalTests
    {
        private const string SettingsPath = "Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset";
        private const string Hint = "run the Blender tools (plan 2026-10-01 Tasks 7–8), then execute_custom_tool ai_studio_import";
        private const float Swell = 0.15f;

        private GameObject _go;
        private ColorHuntSettings _memory;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_memory != null) Object.DestroyImmediate(_memory);
        }

        private RoyalSpecials Make(ColorHuntSettings s)
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = s;
            var style = new GameObject("RoyalSpecials");
            style.transform.SetParent(_go.transform, false);
            var royal = style.AddComponent<RoyalSpecials>();
            royal.Build(game);
            return royal;
        }

        /// <summary>7×7 colour (2r + c) mod 5 (no two neighbours alike), hp 5, all specials on (SpecialScheduleTests' board).</summary>
        private static Board Board7(params (int cell, SpecialKind kind)[] specials)
        {
            var cells = new Block[49];
            for (int i = 0; i < 49; i++) cells[i] = new Block((2 * (i / 7) + i % 7) % 5, 5);
            foreach (var (cell, kind) in specials) cells[cell] = Block.MakeSpecial(kind);
            return new Board
            {
                Size = 7, Cells = cells, Ammo = 158, Targets = new[] { new Target(0, 22), new Target(1, 16) },
                ColorCount = 5, HpMin = 3, HpMax = 14, Rng = 1, Features = Features.All,
            };
        }

        /// <summary>The plan the game makes for a tap on <paramref name="origin"/> (its area and triggers from the rules).</summary>
        private static TriggerPlan Plan(Board b, int origin, SpecialTiming t)
        {
            var area = ColorHuntRules.Blast(b, origin);
            var step = new Step();
            step.Exploded.AddRange(area.Cells);
            step.Triggered.AddRange(area.Triggered);
            return SpecialSchedule.Plan(7, step, origin, t, Swell);
        }

        [Test]
        public void WithoutTheHandoffs_RoyalIsNotReady()
        {
            _memory = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsFalse(RoyalSpecials.AssetsReady(null));
            Assert.IsFalse(RoyalSpecials.AssetsReady(_memory));
            var royal = Make(_memory);
            Assert.IsFalse(royal.Ready);
            Assert.IsFalse(royal.gameObject.activeSelf, "an unready style stays off");
            Assert.AreEqual(0, royal.LiveCount);
            Assert.AreEqual(RoyalSpecials.TimingOf(_memory.Royal), royal.Timing, "the timing never needs the assets");
        }

        [Test]
        public void WithTheImportedHandoffs_RoyalIsReady()
        {
            var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>(SettingsPath);
            Assert.IsNotNull(s, SettingsPath);
            Assert.IsTrue(RoyalSpecials.AssetsReady(s), Hint);
            var copy = Object.Instantiate(s);
            try
            {
                copy.ToonTemplate = null;
                Assert.IsFalse(RoyalSpecials.AssetsReady(copy), "no template: Project/Toon would come from Shader.Find, absent in a build");
            }
            finally { Object.DestroyImmediate(copy); }
            var royal = Make(s);
            Assert.IsTrue(royal.Ready);
            Assert.IsTrue(royal.GathersBirthGroup);
            Assert.IsTrue(royal.ConsumesSpecialView);
            Assert.IsFalse(royal.gameObject.activeSelf, "built off: the game activates the look it uses");
            Assert.AreEqual(7, royal.transform.childCount, "seven particle systems; the pooled meshes come on demand");
            Assert.AreEqual(0, royal.LiveCount);
        }

        [Test]
        public void TheGame_PlaysPotaForRoyal_UntilTheHandoffsAreIn()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = _memory = ScriptableObject.CreateInstance<ColorHuntSettings>();
            LogAssert.Expect(LogType.Warning, new Regex("Royal special look is missing assets"));
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreEqual(SpecialLook.Royal, game.SpecialLook);
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
            Assert.IsNotNull(_go.transform.Find("RoyalSpecials")?.GetComponent<RoyalSpecials>(), "CreateSpecialStyle builds Royal on its own child");
        }

        [Test]
        public void Rockets_ReachEveryCellOfAColumn_WhenTheScheduleBurstsIt()
        {
            var t = RoyalSpecials.TimingOf(new RoyalTuning());
            float peak = Swell * PotaLook.SwellPeakAt;
            var plan = Plan(Board7((3, SpecialKind.LineV)), 3, t);                 // (0, 3): LineV clears the column, one rocket down 6 cells
            Assert.AreEqual(1f, plan.Pace, "0.06 + 6/18.75 + 0.15 fits 0.6 s");
            var fire = plan.Fires[0];
            for (int r = 1; r < 7; r++)
            {
                int c = 3 + 7 * r;
                float burst = plan.Start[c] + peak;
                Assert.AreEqual(burst, RoyalSpecials.RocketReach(fire, t, r), 1e-3f, $"({r}, 3): the rocket is over it when it bursts");
                Assert.AreEqual(r, RoyalSpecials.RocketCells(fire, t, burst - fire.Time), 1e-3f * t.LineSpeed, $"({r}, 3)");
            }
            Assert.AreEqual(0f, RoyalSpecials.RocketCells(fire, t, t.LineLead * 0.5f), "still squashing");
            var mid = Plan(Board7((24, SpecialKind.LineV)), 24, t);                // (3, 3): two rockets, 3 cells each
            foreach (int r in new[] { 0, 1, 2, 4, 5, 6 })
                Assert.AreEqual(mid.Start[3 + 7 * r] + peak, RoyalSpecials.RocketReach(mid.Fires[0], t, Mathf.Abs(r - 3)), 1e-3f, $"({r}, 3) from the middle");
        }

        [Test]
        public void Rockets_KeepTheBeat_WhenTheStepIsCompressed()
        {
            var t = RoyalSpecials.TimingOf(new RoyalTuning());
            float peak = Swell * PotaLook.SwellPeakAt;
            var plan = Plan(Board7((21, SpecialKind.LineH), (27, SpecialKind.Bomb)), 21, t);    // the row to a bomb at its far end
            Assert.Less(plan.Pace, 1f, "compressed to SpecialSchedule.MaxStep");
            var fire = plan.Fires[0];
            Assert.AreEqual(plan.Pace, fire.Pace);
            for (int k = 1; k < 7; k++)
                Assert.AreEqual(plan.Start[21 + k] + peak, RoyalSpecials.RocketReach(fire, t, k), 1e-3f, $"(3, {k})");
            Assert.AreEqual(27, plan.Fires[1].Cell);
            Assert.AreEqual(plan.Fires[1].Time, RoyalSpecials.RocketReach(fire, t, 6), 1e-3f, "the bomb fires as the rocket reaches it");
            Assert.AreEqual(3.8125f, RoyalSpecials.EdgeCells(7), 1e-4f, "the frame's middle line: (4.4 + 0.175) / 1.2");
        }

        [Test]
        public void PoolCaps_FitTheBudget()
        {
            Assert.AreEqual(1026 - 684, RoyalSpecials.Cap, "BurstBudget.Max − Pota's burst pools (normal blocks keep bursting there)");
            Assert.AreEqual(325, RoyalSpecials.PoolCapacity);
            Assert.LessOrEqual(RoyalSpecials.PoolCapacity, RoyalSpecials.Cap);
            var t = new RoyalTuning();
            Assert.GreaterOrEqual(RoyalSpecials.RocketMax, 6, "three lines in one 7×7 step");
            Assert.GreaterOrEqual(RoyalSpecials.ShellMax, 2 * 8, "two bombs' shells at once");
            Assert.GreaterOrEqual(RoyalSpecials.CoinMax, 2 * t.LandCoins, "two golden balls landing 0.12 s apart");
            Assert.GreaterOrEqual(RoyalSpecials.TokenMax, 2);
            Assert.GreaterOrEqual(RoyalSpecials.FireMax, 2);
            Assert.GreaterOrEqual(RoyalSpecials.ProxyMax, 2);
        }

        [Test]
        public void TuningDefaults_AreTheSpecNumbers()
        {
            var t = new RoyalTuning();
            Assert.AreEqual(0.06f, t.LineLead, "Top 0,06 sn ezilip parlar");
            Assert.AreEqual(0.32f, 6f / t.LineSpeed, 1e-5f, "0,32 sn'de en uzak kenara (7×7: 6 hücre)");
            Assert.AreEqual(0.18f, t.BombLead, "Hazırlık 0,18 sn");
            Assert.AreEqual(0.04f, t.BombRing, "halka başına 0,04 sn");
            Assert.AreEqual(0.18f, t.GatherTime, "0,18 sn'de doğum hücresine çekilir");
            Assert.AreEqual(0.25f, t.PopTime, "taşarak (0,25 sn)");
            Assert.AreEqual(1.2f, t.PopPeak, "0 → 1,2 → 1");
            Assert.That(t.BirthTwinkles, Is.InRange(6, 8), "6–8 yıldızcık");
            Assert.AreEqual(3f, t.FuseSparksPerSecond, "saniyede ~3");
            Assert.AreEqual(0.03f, t.BreathAmount, "±%3");
            Assert.AreEqual(1.5f, t.ArrowGlowHz, "1,5 Hz");
            Assert.AreEqual(1f, t.GoldenTwinklesPerSecond, "saniyede ~1");
            Assert.AreEqual(0.5f, t.PuffEvery, "yarım hücrede bir");
            Assert.AreEqual(0.12f, t.LineShake, "eksen boyunca sarsıntı (0,12)");
            Assert.AreEqual(1.25f, t.BombSwell, "1,0 → 1,25 şişer");
            Assert.AreEqual(2, t.BombBlinks, "iki kez kırmızı-beyaz");
            Assert.AreEqual(0.08f, t.CoreFlashTime, "beyaz çekirdek flaşı (0,08 sn)");
            Assert.AreEqual(3.2f, t.FireCells, "~3,2 hücre");
            Assert.AreEqual(0.45f, t.FireTime, "0,45 sn");
            Assert.That(t.BombPuffs, Is.InRange(4, 6), "4–6 duman pufu");
            Assert.AreEqual(12, t.BombSparks, "12 kıvılcım");
            Assert.AreEqual(0.1f, t.PushCells, "0,1 hücre dışa itilir");
            Assert.AreEqual(0.2f, t.BombShake, "Sarsıntı 0,2");
            Assert.AreEqual(0.06f, t.BombHitStop, "0,06 sn anlık yavaşlama");
            Assert.AreEqual(0.5f, t.ComboSlowScale, "×0,5");
            Assert.AreEqual(0.2f, t.ComboSlowTime, "0,2 sn");
            Assert.AreEqual(0.1f, t.ScreenFlashTime, "0,1 sn beyaz ekran flaşı");
            Assert.That(t.LandCoins, Is.InRange(8, 12), "8–12 dönen altın para");
            var timing = RoyalSpecials.TimingOf(t);
            Assert.AreEqual(t.LineSpeed, timing.LineSpeed);
            Assert.AreEqual(t.LineLead, timing.LineLead);
            Assert.AreEqual(t.BombLead, timing.BombLead);
            Assert.AreEqual(t.BombRing, timing.BombRing);
            Assert.AreEqual(t.GatherTime, timing.GatherTime);
            _memory = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsNotNull(_memory.Royal, "a fresh settings asset carries the defaults");
            Assert.AreEqual(t.LineSpeed, _memory.Royal.LineSpeed);
        }

        [Test]
        public void FuseTip_SitsOnTheSpritesSpark()
        {
            float q = PotaLook.LabelQuad * PotaLook.SpecialScale;
            Assert.AreEqual(q, RoyalSpecials.StickerQuad, 1e-6f);
            var tip = RoyalSpecials.FuseTip;
            Assert.AreEqual((0.06f + 0.88f * 0.79f - 0.5f) * q, tip.x, 1e-5f, "ozel-bomba-a.png: spark centroid u 0.79, drawn payli into 88 %");
            Assert.AreEqual((0.06f + 0.88f * 0.90f - 0.5f) * q, tip.y, 1e-5f, "v 0.90 from the bottom");
            Assert.That(tip.x, Is.InRange(0.15f, 0.3f), "right of centre, inside the 0.9-cell block");
            Assert.That(tip.y, Is.InRange(0.25f, 0.4f), "above centre");
        }

        [Test]
        public void PopAndJet_Curves()
        {
            Assert.AreEqual(0f, RoyalSpecials.PopScale(0f, 1.2f), 1e-5f, "from nothing");
            Assert.AreEqual(1.2f, RoyalSpecials.PopScale(0.55f, 1.2f), 1e-4f, "taşarak: the overshoot");
            Assert.AreEqual(1f, RoyalSpecials.PopScale(1f, 1.2f), 1e-5f, "settles at its size");
            float most = 0f;
            for (int i = 0; i <= 100; i++) most = Mathf.Max(most, RoyalSpecials.PopScale(i / 100f, 1.2f));
            Assert.AreEqual(1.2f, most, 1e-3f);
            Assert.AreEqual(new Vector4(0.25f, 0.5f, 0f, 0.5f), RoyalSpecials.JetFrame(0f), "frame 0: the sheet's top-left");
            Assert.AreEqual(new Vector4(0.25f, 0.5f, 0.25f, 0f), RoyalSpecials.JetFrame(0.3f * 5.5f / 8f), "frame 5: second row, second column");
            Assert.AreEqual(RoyalSpecials.JetFrame(0.01f), RoyalSpecials.JetFrame(0.31f), "one loop = 0.3 s");
            var quad = RoyalSpecials.JetQuad();
            Assert.AreEqual(-0.9f, quad.bounds.min.y, 1e-5f, "the nozzle (0.5, 0.9) is the pivot; the flame runs down −y");
            Assert.AreEqual(0.1f, quad.bounds.max.y, 1e-5f);
            Assert.AreEqual(-0.5f, quad.bounds.min.x, 1e-5f);
            Assert.AreEqual(4, quad.colors.Length, "white vertex colours: PotaFlipbook multiplies by them");
        }

        [Test]
        public void SynthClips_HaveTheirLengths_AndStayUnclipped()
        {
            var clips = new (AudioClip clip, float seconds, string name)[]
            {
                (RoyalSpecials.MakeRocket(), RoyalSpecials.RocketSoundTime, "roket"),
                (RoyalSpecials.MakeFuse(), RoyalSpecials.FuseSoundTime, "fitil"),
                (RoyalSpecials.MakeCoin(), RoyalSpecials.CoinSoundTime, "para"),
            };
            foreach (var (clip, seconds, name) in clips)
            {
                try
                {
                    Assert.AreEqual(name, clip.name);
                    Assert.AreEqual(Mathf.CeilToInt(seconds * 44100), clip.samples, name);
                    var data = new float[clip.samples];
                    clip.GetData(data, 0);
                    float most = 0f;
                    foreach (var x in data) most = Mathf.Max(most, Mathf.Abs(x));
                    Assert.That(most, Is.InRange(0.05f, 0.9f), name + ": audible and never at the clamp");
                }
                finally { Object.DestroyImmediate(clip); }
            }
            var a = RoyalSpecials.MakeRocket();
            var b = RoyalSpecials.MakeRocket();
            try
            {
                var da = new float[a.samples];
                var db = new float[b.samples];
                a.GetData(da, 0);
                b.GetData(db, 0);
                CollectionAssert.AreEqual(da, db, "hash noise: every recording sounds the same");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
```

Add to `Assets/_Project/Games/Pota/Tests/EditMode/ColorHuntCameraTests.cs` (inside the class):
```csharp
        [Test]
        public void AxisShake_MovesOnlyAlongItsAxis_AtMostHalfTheAmount()
        {
            float most = 0f;
            for (int i = 0; i < 200; i++)
            {
                var o = ColorHuntCamera.AxisShakeOffset(Vector2.right, 0.12f, i * 0.0037f);
                Assert.AreEqual(0f, o.y, 1e-6f, "a row's rockets shake the camera sideways only");
                most = Mathf.Max(most, Mathf.Abs(o.x));
            }
            Assert.LessOrEqual(most, 0.06f + 1e-6f, "±amount/2 like CameraShake.Add");
            Assert.Greater(most, 0.05f);
            var up = ColorHuntCamera.AxisShakeOffset(new Vector2(0f, 3f), 0.12f, 0.25f / ColorHuntCamera.AxisShakeHz);
            Assert.AreEqual(0f, up.x, 1e-6f);
            Assert.AreEqual(0.06f, up.y, 1e-5f, "a quarter swing: the full half-amount; the axis is normalised");
        }
```

- [ ] **Step 2: Run them to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.RoyalTests Project.Tests.ColorHuntCameraTests`
Expected: exit 2 with `error CS0246: The type or namespace name 'RoyalSpecials' could not be found` (and `RoyalTuning`),
`error CS0117: 'ColorHuntCamera' does not contain a definition
for 'AxisShakeOffset'` (and `AxisShakeHz`), `error CS1061: 'ColorHuntSettings' does not contain a definition for 'Royal'`.

- [ ] **Step 3: `ColorHuntCamera.AxisShake`**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntCamera.cs`:
1. After `private float _pushAmount, _pushTime = -1f, _pushDuration = 1f;` add:
```csharp
        /// <summary>Royal's rockets: the axis shake swings at this rate (Hz).</summary>
        public const float AxisShakeHz = 18f;
        private Vector2 _axis;
        private float _axisAmount, _axisT;

        /// <summary>The axis shake's current size (Pota sars units; 0 = still).</summary>
        public float AxisShakeAmount => _axisAmount;

        /// <summary>
        /// Spec 2026-10-01 Royal "eksen boyunca sarsıntı": a shake along one screen axis, ±amount/2 like CameraShake.Add, swinging at
        /// <see cref="AxisShakeHz"/> and fading 2.2/s of game time (dips freeze it, as Pota's sars). A stronger call wins; capped at
        /// CameraShake.LinearCap. Adds to the camera's other shake.
        /// </summary>
        public void AxisShake(Vector2 axis, float amount)
        {
            if (axis.sqrMagnitude < 1e-8f || amount <= 0f) return;
            _axis = axis.normalized;
            _axisAmount = Mathf.Min(CameraShake.LinearCap, Mathf.Max(_axisAmount, amount));
            _axisT = 0f;
        }

        public static Vector2 AxisShakeOffset(Vector2 axis, float amount, float t) =>
            axis.normalized * (0.5f * amount * Mathf.Sin(2f * Mathf.PI * AxisShakeHz * t));
```
2. In `LateUpdate`, replace the line `transform.position = pos + (_shake != null ? _shake.Offset : Vector3.zero);` with the block
   below (if another task already changed that line, keep its terms and add `+ axisOffset` to them):
```csharp
            Vector3 axisOffset = Vector3.zero;
            if (_axisAmount > 0f)
            {
                _axisT += Time.deltaTime;
                axisOffset = AxisShakeOffset(_axis, _axisAmount, _axisT);
                _axisAmount = CameraShake.StepLinear(_axisAmount, Time.deltaTime);
            }
            transform.position = pos + (_shake != null ? _shake.Offset : Vector3.zero) + axisOffset;
```

- [ ] **Step 4: The settings field**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs`, right after Task 3's
`public SpecialLook SpecialLook = SpecialLook.Pota;` add:
```csharp
        [Tooltip("1 · Royal: every number of the Royal special look (timing, birth, idle, rockets, bomb, 2+ specials, golden landing, ink).")]
        public RoyalTuning Royal = new RoyalTuning();
```

- [ ] **Step 5: `RoyalTuning` and `RoyalSpecials`**

Create `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/RoyalSpecials.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// Every number of the Royal special look (spec 2026-10-01 §2 "1 · Royal Match"). Cells are board cells (× the grid scale on screen),
    /// times are game seconds. Edit them in ColorHuntSettings.Royal; the timing block feeds SpecialSchedule through RoyalSpecials.Timing.
    /// </summary>
    [Serializable]
    public class RoyalTuning
    {
        [Header("Timing (SpecialSchedule)")]
        [Tooltip("Line: the ball squashes and glows this long before the halves fly (s).")]
        public float LineLead = 0.06f;
        [Tooltip("Rocket speed in cells/s: 6 cells (a 7×7 row, edge to far edge) in 0.32 s.")]
        public float LineSpeed = 6f / 0.32f;
        [Tooltip("Bomb wind-up: swell, two red-white blinks, shiver, fuse sparks (s).")]
        public float BombLead = 0.18f;
        [Tooltip("Bomb wave: each further ring of the 3×3 bursts this much later (s).")]
        public float BombRing = 0.04f;
        [Tooltip("Birth: the group's blocks are pulled into the birth cell in this time (s).")]
        public float GatherTime = 0.18f;

        [Header("Birth")]
        [Tooltip("A gathered block's scale when it reaches the birth cell.")]
        public float GatherShrink = 0.35f;
        [Tooltip("A gathered block turns this far on its way (degrees, alternating sides).")]
        public float GatherSpin = 40f;
        [Tooltip("The special pops 0 → PopPeak → 1 in this time (s).")]
        public float PopTime = 0.25f;
        public float PopPeak = 1.2f;
        public float BirthFlashCells = 2.2f;
        public float BirthStarCells = 2.8f;
        [Tooltip("Twinkles around a birth (spec: 6–8).")]
        public int BirthTwinkles = 7;

        [Header("Idle")]
        public float FuseSparksPerSecond = 3f;
        [Tooltip("The bomb breathes ±this (3 %).")]
        public float BreathAmount = 0.03f;
        public float BreathHz = 1.1f;
        [Tooltip("The line ball's arrow glows at this rate (Hz) up to ArrowGlow (a flash floor).")]
        public float ArrowGlowHz = 1.5f;
        public float ArrowGlow = 0.14f;
        public float GoldenTwinklesPerSecond = 1f;

        [Header("Line: rockets")]
        [Tooltip("Diameter of the split ball and of the bomb's shell (cells).")]
        public float BallCells = 0.8f;
        public float SquashAxis = 0.72f;
        public float SquashSide = 1.18f;
        [Tooltip("The ball's base colour × this at the end of the squash (HDR: it blooms).")]
        public float LineGlow = 2.2f;
        [Tooltip("The flame jet's frame size (cells); the flame runs 0.9 of it behind the cap.")]
        public float JetCells = 1.8f;
        [Tooltip("A grey puff every this many cells (spec: half a cell).")]
        public float PuffEvery = 0.5f;
        public float SparkEvery = 0.2f;
        [Tooltip("Past its last block a rocket speeds up to the frame edge (cells/s²).")]
        public float ExitAccel = 60f;
        [Tooltip("After the exit flash the rocket shrinks away in this time (s).")]
        public float ExitTail = 0.08f;
        [Tooltip("Shake along the line's axis (Pota sars units).")]
        public float LineShake = 0.12f;

        [Header("Bomb")]
        public float BombSwell = 1.25f;
        public int BombBlinks = 2;
        public float CoreFlashCells = 2.6f;
        public float CoreFlashTime = 0.08f;
        [Tooltip("The inked fireball's width (cells); the flipbook fills 85 % of its frame.")]
        public float FireCells = 3.2f;
        public float FireTime = 0.45f;
        public float RingCells = 3.6f;
        [Tooltip("Grey puffs (spec: 4–6).")]
        public int BombPuffs = 5;
        public int BombSparks = 12;
        [Tooltip("ColorHuntFx point light (Pota's bomb flash uses 8).")]
        public float BombLight = 10f;
        public float BombLightTime = 0.3f;
        [Tooltip("The 3×3's blocks are pushed out this far as the bomb goes (cells).")]
        public float PushCells = 0.1f;
        public float BombShake = 0.2f;
        public float BombHitStop = 0.06f;

        [Header("2+ specials in one step")]
        public float ComboSlowScale = 0.5f;
        public float ComboSlowTime = 0.2f;
        public float ScreenFlashTime = 0.1f;
        public float ScreenFlashAlpha = 0.55f;

        [Header("Golden landing")]
        [Tooltip("The golden ball flies as a big spinning coin this wide (cells).")]
        public float GoldenCoinCells = 0.85f;
        [Tooltip("The flying coin's spin (degrees/s).")]
        public float TokenSpin = 540f;
        public float GlitterEvery = 0.025f;
        [Tooltip("Coins thrown from the rim (spec: 8–12).")]
        public int LandCoins = 10;
        public float CoinCells = 0.42f;
        public int LandStars = 10;

        [Header("Ink")]
        [Tooltip("Project/Toon outline in world units (A's candy chunks use 0.022).")]
        public float InkWidth = 0.022f;
    }

    /// <summary>
    /// 1 · Royal (spec 2026-10-01 §2 "1 · Royal Match"): glossy, inked, cartoon; everything pops in. Birth: the group's blocks are pulled
    /// into the birth cell (shrinking, turning) and the special pops 0 → 1.2 → 1 with a white core flash, an eight-ray star and twinkles.
    /// Idle: the bomb's fuse spits sparks and the bomb breathes, the line ball's arrow glows at 1.5 Hz, the golden ball twinkles. Line: the
    /// ball squashes and glows, splits into the two Blender halves that fly to both ends with a looping flame jet, grey puffs every half
    /// cell and sparks — the game bursts each block the moment a rocket is over it (the same formula as SpecialSchedule, Pace included) —
    /// and leave the frame with a small flash; the camera shakes along the axis. Bomb: a copy of the consumed block swells to 1.25, blinks
    /// red-white twice, shivers and spits fuse sparks for BombLead, then a white core flash, the inked fireball (Blender), a ring, eight shell
    /// pieces (Blender, ShatterDebris' deterministic motion), grey puffs, sparks, Pota's point light, a 0.1-cell push of the 3×3 and the
    /// game's ring wave. 2+ specials in a step: ×0.5 for 0.2 s and a 0.1 s white screen flash on the HUD canvas, at the chain moment.
    /// Golden: a big spinning coin drops through its gate leaving glitter, flashes gold there, arcs into the hoop and bursts into spinning
    /// coins and stars. Game time throughout; meshes live under this object (the game root's space), particles in world space.
    /// [DefaultExecutionOrder(100)] puts this Update after ColorHuntGame's, so a fire's effect runs on the step's own clock (see Fire).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RoyalSpecials : MonoBehaviour, ISpecialStyle
    {
        // Pools: Royal's own live particles and pieces stay ≤ Cap; Pota's burst pools (BurstBudget.Pota) draw the normal blocks.
        public const int FlashMax = 16, StarMax = 8, TwinkleMax = 96, SparkMax = 96, PuffMax = 32, FireMax = 4, RingMax = 6;
        public const int RocketMax = 6, ProxyMax = 3, ShellMax = 24, CoinMax = 24, TokenMax = 4;
        /// <summary>A rocket counts twice: its half ball and its flame jet.</summary>
        public const int PerRocket = 2;
        public const int PoolCapacity = FlashMax + StarMax + TwinkleMax + SparkMax + PuffMax + FireMax + RingMax
                                        + RocketMax * PerRocket + ProxyMax + ShellMax + CoinMax + TokenMax;
        /// <summary>Spec §1.6: a look's live particles ≤ BurstBudget.Max together with the Pota burst pools (342).</summary>
        public const int Cap = BurstBudget.Max - BurstBudget.Pota;

        /// <summary>Task 8's meshes: the halves and the shell make a 0.45 m ball, the coin is 0.30 m across.</summary>
        public const float ModelBall = 0.45f, ModelCoin = 0.30f;
        /// <summary>vfx-fire-toon reaches ~85 % of its frame at its widest.</summary>
        public const float FireFill = 0.85f;
        /// <summary>vfx-flame-jet: 4×2 frames, one loop 0.3 s, nozzle at frame (0.5, 0.9), the flame toward frame −y.</summary>
        public const float JetLoop = 0.3f;
        public const int JetCols = 4, JetRows = 2, JetFrames = JetCols * JetRows;
        public static readonly Vector2 JetNozzle = new Vector2(0.5f, 0.9f);
        /// <summary>Pota's ozel-bomba-a.png: the fuse spark's centroid, u 0.79 and v 0.90 from the bottom (measured 2026-10-01).</summary>
        public const float FuseU = 0.79f, FuseV = 0.90f;
        /// <summary>The special sticker's quad (cells): RAKAM_DUZLEM × ozelOlcek; the sprite fills its middle 88 % ("payli").</summary>
        public const float StickerQuad = PotaLook.LabelQuad * PotaLook.SpecialScale;
        /// <summary>Rockets fly this far in front of the board plane (cells): over the blocks they pass, not through them.</summary>
        public const float RocketZ = PotaLook.FrontZ + 0.45f;
        public const float RocketSoundTime = 0.42f, FuseSoundTime = 0.22f, CoinSoundTime = 0.35f;
        private const float SparkGravity = 7f, CoinGravity = 11f, SynthRate = 44100f;

        public static readonly Color BlinkRed = PotaLook.Hex(0xff3b2f);
        private static readonly Color Smoke = new Color(0.6f, 0.62f, 0.68f, 0.95f);       // CandyPuff × grey: cartoon smoke, its ink stays dark
        private static readonly Color SparkTone = new Color(1f, 0.78f, 0.38f, 1f);
        private static readonly Color FuseSpark = new Color(1f, 0.86f, 0.45f, 1f);
        private static readonly Color Gold = new Color(1f, 0.84f, 0.35f, 1f);
        private static readonly Color GoldWhite = new Color(1f, 0.93f, 0.62f, 1f);
        private static readonly Color StarWhite = new Color(1f, 0.97f, 0.88f, 1f);
        private static readonly Color FireGlow = PotaLook.Hex(0xffb347);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SheetStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly RoyalTuning Defaults = new RoyalTuning();
        private static Mesh _jetQuad;

        private class ViewFx { public SpecialKind Kind; public float Phase, Next, Pop = -1f, PopScale = 1f, Breath = 1f; }
        private class GatherFx { public BlockView V, Home; public Vector2 From, To; public float Age, Duration, Spin; public Action<BlockView> Arrived; }
        private class Rocket
        {
            public Transform Root, Half, Jet;
            public MeshFilter HalfF;
            public MeshRenderer HalfR, JetR;
            public SpecialFire Fire;
            public SpecialTiming Timing;
            public Vector2 Dir;
            public float Age, Last, Edge, NextPuff, NextSpark, Exited = -1f;
            public bool Launched, Leads;
        }
        private class Proxy { public Transform Root, Item; public MeshFilter BodyF, ItemF; public MeshRenderer BodyR, ItemR; public Color Tone; }
        private class BombFx { public SpecialFire Fire; public Proxy Proxy; public float Age, Lead, NextSpark; }
        private class Piece { public Transform T; public MeshFilter F; public Debris D; public float Radius; }
        private class Coin { public Transform T; public Debris D; }
        private class Flight
        {
            public GoldenRoute Route;
            public float T, Spin, NextGlitter;
            public bool Passed;
            public Action PassedGate, Landed;
            public Transform Token;
        }

        private ColorHuntGame _game;
        private bool _built;
        private ParticleSystem _flash, _stars, _twinkles, _sparks, _puffs, _fire, _rings;
        private readonly List<ParticleSystem> _systems = new List<ParticleSystem>();
        private readonly List<Material> _owned = new List<Material>();
        private Material _halfMat, _shellMat, _coinMat, _jetMat;
        private Mesh _halfA, _halfB, _coinMesh;
        private List<SpecialMeshes.Part> _shellParts = new List<SpecialMeshes.Part>();
        private AudioClip _rocketClip, _fuseClip, _coinClip;
        private Image _screenFlash;
        private float _screenAge = -1f;
        private MaterialPropertyBlock _mpb, _itemMpb;
        private readonly Dictionary<BlockView, ViewFx> _fx = new Dictionary<BlockView, ViewFx>();
        private readonly List<BlockView> _dead = new List<BlockView>();
        private readonly List<GatherFx> _gathers = new List<GatherFx>();
        private readonly List<Rocket> _rockets = new List<Rocket>();
        private readonly Stack<Rocket> _rocketPool = new Stack<Rocket>();
        private readonly List<BombFx> _bombs = new List<BombFx>();
        private readonly Stack<Proxy> _proxyPool = new Stack<Proxy>();
        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly Stack<Piece> _piecePool = new Stack<Piece>();
        private readonly List<Coin> _coins = new List<Coin>();
        private readonly Stack<Coin> _coinPool = new Stack<Coin>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly Stack<Transform> _tokenPool = new Stack<Transform>();
        private readonly System.Random _rng = new System.Random(29);
        private SpecialTiming _timing;
        private float _time, _stepClock;
        private bool _stepLive, _comboPending;
        private int _gen;

        public bool Ready { get; private set; }
        public bool GathersBirthGroup => true;
        public bool ConsumesSpecialView => true;
        public SpecialTiming Timing => TimingOf(Tune);
        public int LiveCount => !_built ? 0
            : StudioParticles.Count(_flash, _stars, _twinkles, _sparks, _puffs, _fire, _rings)
              + _rockets.Count * PerRocket + ProxiesLive + _pieces.Count + _coins.Count + TokensLive;
        public int RocketsLive => _rockets.Count;
        public int ProxiesLive { get { int n = 0; foreach (var b in _bombs) if (b.Proxy != null) n++; return n; } }
        public int PiecesLive => _pieces.Count;
        public int CoinsLive => _coins.Count;
        public int TokensLive { get { int n = 0; foreach (var f in _flights) if (f.Token != null) n++; return n; } }
        public int GatherCount => _gathers.Count;
        /// <summary>The 2+ specials screen flash's alpha now (0 = off).</summary>
        public float ScreenFlash => _screenFlash != null && _screenFlash.enabled ? _screenFlash.color.a : 0f;

        private RoyalTuning Tune => _game != null && _game.S != null && _game.S.Royal != null ? _game.S.Royal : Defaults;

        // ---------- pure parts (EditMode tests) ----------

        /// <summary>The Blender hand-offs and the material templates are in place (no Shader.Find at runtime).</summary>
        public static bool AssetsReady(ColorHuntSettings s)
        {
            if (s == null || s.RoyalFire == null || s.FlameJet == null || s.CandyPuff == null || s.CandyRing == null) return false;
            if (s.SoftDisc == null || s.StarSprite == null) return false;
            if (s.ToonTemplate == null || s.FlipbookTemplate == null || s.AdditiveTemplate == null) return false;
            var halves = SpecialMeshes.Parts(s.RoyalHalves);
            if (halves.Count != 2 || halves[0].Name != "HalfA" || halves[1].Name != "HalfB") return false;
            return SpecialMeshes.Parts(s.RoyalShell).Count >= 4 && SpecialMeshes.Parts(s.RoyalCoin).Count == 1;
        }

        public static SpecialTiming TimingOf(RoyalTuning t) => new SpecialTiming
        {
            LineSpeed = t.LineSpeed, LineLead = t.LineLead, BombLead = t.BombLead, BombRing = t.BombRing, GatherTime = t.GatherTime,
        };

        /// <summary>Step time when a line fired as <paramref name="f"/> is over the cell <paramref name="cells"/> away: SpecialSchedule's
        /// burst moment of that cell (Delay = LineLead + cells/LineSpeed, scaled by the plan's Pace).</summary>
        public static float RocketReach(SpecialFire f, SpecialTiming t, int cells) =>
            f.Time + (t.LineLead + (t.LineSpeed > 0f ? cells / t.LineSpeed : 0f)) * PaceOf(f);

        /// <summary>Cells a rocket has travelled <paramref name="age"/> s after its fire (0 while the ball squashes); the inverse of RocketReach.</summary>
        public static float RocketCells(SpecialFire f, SpecialTiming t, float age) =>
            Mathf.Max(0f, age / PaceOf(f) - t.LineLead) * t.LineSpeed;

        private static float PaceOf(SpecialFire f) => f.Pace > 0f ? f.Pace : 1f;

        /// <summary>The frame's middle line, in cells from the board centre: where a rocket leaves with its flash.</summary>
        public static float EdgeCells(int n) => (PotaLook.PanoSize * 0.5f + PotaLook.FrameWidth * 0.5f) / PotaLook.GridScale(n);

        /// <summary>The bomb sticker's fuse tip in cells from the block centre (block scale 1, sticker unturned).</summary>
        public static Vector2 FuseTip =>
            new Vector2((0.06f + 0.88f * FuseU - 0.5f) * StickerQuad, (0.06f + 0.88f * FuseV - 0.5f) * StickerQuad);

        /// <summary>The birth pop: 0 → <paramref name="peak"/> by 55 % (ease out), then down to 1 (smoothstep).</summary>
        public static float PopScale(float u, float peak)
        {
            u = Mathf.Clamp01(u);
            const float rise = 0.55f;
            if (u < rise) return peak * StudioCurves.EaseOutCubic(u / rise);
            return Mathf.Lerp(peak, 1f, PotaLook.Smoothstep((u - rise) / (1f - rise)));
        }

        /// <summary>The flame jet's _BaseMap_ST for its loop at <paramref name="age"/> s (frames row-major from the sheet's top-left).</summary>
        public static Vector4 JetFrame(float age)
        {
            int f = Mathf.FloorToInt(age / JetLoop * JetFrames) % JetFrames;
            if (f < 0) f += JetFrames;
            int col = f % JetCols, row = f / JetCols;
            return new Vector4(1f / JetCols, 1f / JetRows, col / (float)JetCols, 1f - (row + 1) / (float)JetRows);
        }

        /// <summary>One flame-jet frame as a 1 × 1 quad pivoted on the nozzle (0.5, 0.9): the flame runs down local −y; white colours.</summary>
        public static Mesh JetQuad()
        {
            if (_jetQuad != null) return _jetQuad;
            float x0 = -JetNozzle.x, x1 = 1f - JetNozzle.x, y0 = -JetNozzle.y, y1 = 1f - JetNozzle.y;
            _jetQuad = new Mesh
            {
                name = "RoyalJetQuad",
                vertices = new[] { new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f), new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
            };
            _jetQuad.RecalculateBounds();
            return _jetQuad;
        }

        /// <summary>A landing coin: thrown from the rim, gravity, its own spin; no walls (the hoop hangs below the frame).</summary>
        public static void CoinStep(ref Debris d, float dt, float gravity)
        {
            d.V.y -= gravity * dt;
            d.P += d.V * dt;
            d.R = Quaternion.Euler(d.W * dt) * d.R;
            d.Age += dt;
        }

        // ---------- ISpecialStyle ----------

        public void Build(ColorHuntGame game)
        {
            _game = game;
            if (_built) return;
            var s = game != null ? game.S : null;
            Ready = AssetsReady(s);
            if (!Ready) { gameObject.SetActive(false); return; }
            _built = true;
            _mpb = new MaterialPropertyBlock();
            _itemMpb = new MaterialPropertyBlock();
            _timing = Timing;
            _flash = Sys("RoyalFlash", Keep(StudioParticles.Additive(s, s.SoftDisc, 2f)), ParticleSystemRenderMode.Billboard, FlashMax, 9);
            StudioParticles.SizeOverLife(_flash, u => Mathf.Lerp(0.55f, 1f, StudioCurves.EaseOutCubic(u)));
            StudioParticles.AlphaOverLife(_flash, u => 1f - u);
            _stars = Sys("RoyalStars", Keep(StudioParticles.Additive(s, StudioTextures.RayStar(), 1.8f)), ParticleSystemRenderMode.Billboard, StarMax, 9);
            StudioParticles.SizeOverLife(_stars, u => Mathf.Lerp(0.4f, 1.1f, StudioCurves.EaseOutCubic(Mathf.Min(1f, u * 1.6f))));
            StudioParticles.AlphaOverLife(_stars, u => 1f - u);
            _twinkles = Sys("RoyalTwinkles", Keep(StudioParticles.Additive(s, s.StarSprite, 2f)), ParticleSystemRenderMode.Billboard, TwinkleMax, 9);
            StudioParticles.SizeOverLife(_twinkles, u => Mathf.Sin(u * Mathf.PI) * (0.75f + 0.25f * Mathf.Sin(u * 25f)));
            _sparks = Sys("RoyalSparks", Keep(StudioParticles.Additive(s, s.SoftDisc, 2f)), ParticleSystemRenderMode.Stretch, SparkMax, 8);
            var sr = _sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.05f;
            sr.lengthScale = 1.4f;
            StudioParticles.SizeOverLife(_sparks, u => 1f - u);
            _puffs = Sys("RoyalPuffs", Keep(StudioParticles.Flipbook(s, s.CandyPuff)), ParticleSystemRenderMode.Billboard, PuffMax, 5);
            StudioParticles.Sheet(_puffs, 4, 4);
            _fire = Sys("RoyalFire", Keep(StudioParticles.Flipbook(s, s.RoyalFire)), ParticleSystemRenderMode.Billboard, FireMax, 7);
            StudioParticles.Sheet(_fire, 4, 4);
            _rings = Sys("RoyalRings", Keep(StudioParticles.Flipbook(s, s.CandyRing)), ParticleSystemRenderMode.Billboard, RingMax, 6);
            StudioParticles.Sheet(_rings, 4, 4);
            var halves = SpecialMeshes.Parts(s.RoyalHalves);                 // sorted: HalfA, HalfB (AssetsReady checked the names)
            _halfA = halves[0].Mesh;
            _halfB = halves[1].Mesh;
            _shellParts = SpecialMeshes.Parts(s.RoyalShell);
            _coinMesh = SpecialMeshes.Parts(s.RoyalCoin)[0].Mesh;
            float ink = Tune.InkWidth;
            _halfMat = Keep(SpecialMeshes.Toon(s, s.RoyalHalves, "RoyalHalves", ink));
            _shellMat = Keep(SpecialMeshes.Toon(s, s.RoyalShell, "RoyalShell", ink));
            _coinMat = Keep(SpecialMeshes.Toon(s, s.RoyalCoin, "RoyalCoin", ink * 0.7f));
            _jetMat = Keep(StudioParticles.Flipbook(s, s.FlameJet));
            _rocketClip = MakeRocket();
            _fuseClip = MakeFuse();
            _coinClip = MakeCoin();
            BuildScreenFlash();
            Activate(false);
        }

        public void Activate(bool on)
        {
            if (!on) Clear();
            gameObject.SetActive(on && _built);
        }

        public void Clear()
        {
            _gen++;
            _gathers.Clear();
            foreach (var r in _rockets) HideRocket(r);
            _rockets.Clear();
            foreach (var b in _bombs) if (b.Proxy != null) HideProxy(b.Proxy);
            _bombs.Clear();
            foreach (var p in _pieces) HidePiece(p);
            _pieces.Clear();
            foreach (var c in _coins) HideCoin(c);
            _coins.Clear();
            foreach (var f in _flights) HideToken(f);
            _flights.Clear();
            foreach (var kv in _fx) if (Alive(kv.Key, kv.Value.Kind)) kv.Key.Scale = 1f;   // NewView resets a recycled view to 1 anyway
            _fx.Clear();
            foreach (var ps in _systems) if (ps != null) ps.Clear();
            _screenAge = -1f;
            if (_screenFlash != null) _screenFlash.enabled = false;
            _comboPending = false;
            _stepLive = false;
        }

        /// <summary>Each frame for every resting special: the bomb's fuse and breath, the line's arrow glow, the golden twinkles.</summary>
        public void Idle(BlockView v, float dt)
        {
            if (!_built || v == null || _game.Intro) return;              // the studio intro owns Scale while it runs
            var st = State(v);
            st.Kind = v.Block.Special;
            var tune = Tune;
            float t = _time + st.Phase;
            st.Breath = 1f;
            switch (st.Kind)
            {
                case SpecialKind.Bomb:
                    st.Breath = 1f + tune.BreathAmount * Mathf.Sin(2f * Mathf.PI * tune.BreathHz * t);
                    st.Next -= dt;
                    if (st.Next <= 0f) { st.Next = Interval(tune.FuseSparksPerSecond); FuseSparks(FuseWorld(v), 2, 1f); }
                    break;
                case SpecialKind.LineV:
                case SpecialKind.LineH:
                    v.Pulse = Mathf.Max(v.Pulse, tune.ArrowGlow * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * tune.ArrowGlowHz * t)));
                    break;
                case SpecialKind.Golden:
                    st.Next -= dt;
                    if (st.Next <= 0f) { st.Next = Interval(tune.GoldenTwinklesPerSecond); GoldTwinkle(v); }
                    break;
            }
            v.Scale = st.PopScale * st.Breath;
        }

        /// <summary>A birth group's block is pulled into the birth cell (ease in), shrinking and turning; arrived once, on arrival.</summary>
        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived)
        {
            if (v == null) return;
            if (!_built || _game == null) { arrived?.Invoke(v); return; }
            _gathers.Add(new GatherFx
            {
                V = v, Home = HomeView(to), From = v.Pos, To = to, Duration = Mathf.Max(0.01f, duration), Arrived = arrived,
                Spin = (v.Cell % 2 == 0 ? 1f : -1f) * Tune.GatherSpin,
            });
        }

        /// <summary>The special pops 0 → 1.2 → 1 with a white core flash, an eight-ray star, twinkles, Pota's sound and Pota's text.</summary>
        public void Birth(BlockView v, SpecialKind kind)
        {
            if (!_built || _game == null || v == null) return;
            var st = State(v);
            st.Kind = kind;
            st.Pop = 0f;
            st.PopScale = 0f;
            st.Breath = 1f;
            v.Scale = 0f;
            v.Flash = 1f;
            var tune = Tune;
            float s = Scale;
            bool golden = kind == SpecialKind.Golden;
            StudioParticles.Emit(_flash, World(v.Pos, PotaLook.FrontZ + 0.35f), tune.BirthFlashCells * s, 0.12f, Color.white);
            StudioParticles.Emit(_stars, World(v.Pos, PotaLook.FrontZ + 0.4f), tune.BirthStarCells * s, tune.PopTime, golden ? Gold : Color.white,
                Vector3.zero, R() * 45f);
            int n = Mathf.Max(0, tune.BirthTwinkles);
            for (int i = 0; i < n; i++)
            {
                float a = (i + R() * 0.7f) / Mathf.Max(1, n) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                StudioParticles.Emit(_twinkles, World(v.Pos + dir * (0.45f + 0.25f * R()), PotaLook.FrontZ + 0.45f), (0.22f + 0.14f * R()) * s,
                    0.45f + 0.25f * R(), golden ? GoldWhite : StarWhite, Vel(dir * (1f + 0.6f * R())), R() * 360f);
            }
            var sounds = Sounds();
            if (sounds != null)
            {
                if (golden) sounds.PlayClip("ozelDogdu", _game.S.SfxGoldenBorn);
                else sounds.Combo(2);
            }
            var popups = _game.Juice != null ? _game.Juice.Popups : null;
            if (popups != null)
                popups.Show(_game.BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.6f, -PotaLook.LabelZ)), SpecialTexts.Born(kind),
                    PotaLook.HudGold, 72, false, 1.2f);
        }

        /// <summary>A trigger step starts: the style's step clock restarts (Fire's reference) and 2+ specials arm the combo moment.</summary>
        public void TriggerStep(int specials, Vector2 centre)
        {
            _stepClock = 0f;
            _stepLive = true;
            _timing = Timing;
            _comboPending = specials >= 2;
        }

        /// <summary>
        /// One special fires. The game calls this from its Update the frame its step clock passes <see cref="SpecialFire.Time"/>; this
        /// Update runs after the game's with the same clamped dt, so an effect started at <c>_stepClock − Time</c> (≤ 0 now) is, after this
        /// frame's tick, exactly the step's overshoot old: a rocket is over each cell when the plan bursts it.
        /// </summary>
        public void Fire(SpecialFire f)
        {
            if (!_built || _game == null || _game.Board == null) return;
            float age = _stepLive ? Mathf.Clamp(_stepClock - f.Time, -0.05f, 0f) : 0f;
            if (_comboPending && f.Order >= 1)
            {
                _comboPending = false;
                Combo();
            }
            if (f.Kind == SpecialKind.LineV || f.Kind == SpecialKind.LineH) FireLine(f, age);
            else if (f.Kind == SpecialKind.Bomb) FireBomb(f, age);
        }

        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
        {
            if (!_built) { passedGate?.Invoke(); landed?.Invoke(); return; }
            _flights.Add(new Flight { Route = route, PassedGate = passedGate, Landed = landed, Token = TakeToken() });
        }

        // ---------- frame ----------

        private void Update()
        {
            if (!_built || _game == null || _game.BoardRoot == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);           // ColorHuntGame's own clamp: one clock for the plan and the rockets
            _time += dt;
            _stepClock += dt;
            int gen = _gen;
            TickGathers(dt, gen);
            if (gen != _gen) return;
            TickPops(dt);
            TickRockets(dt);
            TickBombs(dt);
            TickPieces(dt);
            TickCoins(dt);
            TickFlights(dt, gen);
            if (gen != _gen) return;
            TickScreenFlash(dt);
        }

        // ---------- birth and idle ----------

        private ViewFx State(BlockView v)
        {
            if (_fx.TryGetValue(v, out var st)) return st;
            st = new ViewFx { Kind = v.Block.Special, Phase = R() * 10f, Next = R() * 0.5f };
            _fx[v] = st;
            return st;
        }

        private bool Alive(BlockView v, SpecialKind kind) =>
            v != null && v.gameObject.activeInHierarchy && v.Block.Special == kind && _game.ViewAt(v.Cell) == v;

        /// <summary>Runs the birth pops (also while the special falls with its column) and forgets views that left the board.</summary>
        private void TickPops(float dt)
        {
            _dead.Clear();
            float time = Mathf.Max(1e-3f, Tune.PopTime), peak = Tune.PopPeak;
            foreach (var kv in _fx)
            {
                var v = kv.Key;
                var st = kv.Value;
                if (!Alive(v, st.Kind)) { _dead.Add(v); continue; }
                if (st.Pop < 0f) continue;
                st.Pop += dt;
                float u = st.Pop / time;
                st.PopScale = u >= 1f ? 1f : PopScale(u, peak);
                if (u >= 1f) st.Pop = -1f;
                v.Scale = st.PopScale * st.Breath;
            }
            foreach (var v in _dead) _fx.Remove(v);
        }

        private void TickGathers(float dt, int gen)
        {
            var tune = Tune;
            for (int i = _gathers.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _gathers.Count) return;
                var g = _gathers[i];
                g.Age += dt;
                float u = Mathf.Clamp01(g.Age / g.Duration), e = u * u;                 // pulled in: slow, then fast
                g.V.Pos = Vector2.LerpUnclamped(g.From, g.To, e);
                g.V.Scale = Mathf.Lerp(1f, tune.GatherShrink, e);
                g.V.Wiggle = g.Spin * e;
                g.V.Flash = Mathf.Max(g.V.Flash, 0.5f * e);
                if (u < 1f) continue;
                _gathers.RemoveAt(i);
                if (g.Home != null && g.Home.isActiveAndEnabled)                     // the birth cell gulps it
                {
                    g.Home.Flash = Mathf.Max(g.Home.Flash, 0.7f);
                    g.Home.Squash = Mathf.Max(g.Home.Squash, 0.6f);
                }
                StudioParticles.Emit(_twinkles, World(g.To, PotaLook.FrontZ + 0.4f), 0.3f * Scale, 0.3f, StarWhite, Vector3.zero, R() * 360f);
                g.Arrived?.Invoke(g.V);
            }
        }

        private BlockView HomeView(Vector2 to)
        {
            var board = _game.Board;
            if (board == null) return null;
            int n = board.Size;
            int col = Mathf.RoundToInt(to.x + (n - 1) * 0.5f), row = Mathf.RoundToInt((n - 1) * 0.5f - to.y);
            if (row < 0 || row >= n || col < 0 || col >= n) return null;
            return _game.ViewAt(ColorHuntRules.Index(n, row, col));
        }

        /// <summary>The bomb sticker's fuse tip in world space, following the view's scale, bob and label angle.</summary>
        private Vector3 FuseWorld(BlockView v)
        {
            float k = v.transform.localScale.x, a = v.LabelAngle * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            var tip = FuseTip * k;
            var at = new Vector2(v.Pos.x, v.Pos.y + v.Bob) + new Vector2(tip.x * cs - tip.y * sn, tip.x * sn + tip.y * cs);
            return World(at, PotaLook.LabelZ * Mathf.Max(1f, k));
        }

        private void FuseSparks(Vector3 at, int count, float speed)
        {
            float s = Scale;
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            for (int k = 0; k < count; k++)
            {
                float a = Mathf.PI * (0.15f + 0.7f * R());                        // up and to the sides
                var v = Vel(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ((1.4f + 1.2f * R()) * speed), 0.4f * R());
                StudioParticles.Emit(_sparks, at, (0.06f + 0.03f * R()) * s, 0.25f + 0.2f * R(), FuseSpark, v);
            }
        }

        private void GoldTwinkle(BlockView v)
        {
            float s = Scale, a = R() * Mathf.PI * 2f, r = 0.22f + 0.16f * R();
            var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            StudioParticles.Emit(_twinkles, World(new Vector2(v.Pos.x, v.Pos.y + v.Bob) + off, PotaLook.LabelZ + 0.05f), (0.2f + 0.12f * R()) * s,
                0.5f + 0.2f * R(), GoldWhite, Vel(new Vector2(0f, 0.3f)), R() * 360f);
        }

        // ---------- line: the split ball's rockets ----------

        private void FireLine(SpecialFire f, float age)
        {
            int n = _game.Board.Size;
            bool vertical = f.Kind == SpecialKind.LineV;                              // LineV clears the column: up and down
            var axis = vertical ? Vector2.up : Vector2.right;
            float coord = vertical ? f.At.y : f.At.x, half = (n - 1) * 0.5f, edge = EdgeCells(n);
            var q = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;   // HalfA's nose (+X) along +axis
            for (int side = 0; side < 2; side++)
            {
                var r = TakeRocket();
                if (r == null) break;                                                 // pool full: the blocks still burst on time
                float sign = side == 0 ? 1f : -1f;
                r.Fire = f;
                r.Timing = _timing;
                r.Dir = axis * sign;
                r.Age = age;
                r.Launched = false;
                r.Leads = side == 0;
                r.Exited = -1f;
                r.Last = half - sign * coord;                                        // cells to the last block on this side
                r.Edge = edge - sign * coord;                                        // cells to the frame edge
                r.NextPuff = Mathf.Max(0.05f, Tune.PuffEvery) * 0.5f;
                r.NextSpark = 0f;
                r.HalfF.sharedMesh = side == 0 ? _halfA : _halfB;
                r.Half.localRotation = side == 0 ? q : q * Quaternion.Euler(0f, 180f, 0f);   // noses out, caps together = the whole ball
                r.Jet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-r.Dir.y, -r.Dir.x) * Mathf.Rad2Deg + 90f);   // flame (−y) backward
                r.Jet.gameObject.SetActive(false);
                PoseRocket(r);
                r.Root.gameObject.SetActive(true);
                _rockets.Add(r);
            }
            StudioParticles.Emit(_flash, World(f.At, RocketZ + 0.1f), 1.6f * Scale, 0.1f, Color.white);
            Sounds()?.PlayClip("cizgi", _game.S.SfxLine);
        }

        /// <summary>The halves separate: jets on; the leading rocket of the pair shakes the camera along the axis and whooshes.</summary>
        private void Launch(Rocket r)
        {
            r.Launched = true;
            r.Jet.gameObject.SetActive(true);
            if (!r.Leads) return;
            var axis = _game.BoardRoot.TransformVector(new Vector3(r.Dir.x, r.Dir.y, 0f));
            var cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            if (cam != null) cam.AxisShake(new Vector2(axis.x, axis.y), Tune.LineShake);
            else _game.Juice?.Shake?.Add(Tune.LineShake);
            Sounds()?.PlayClip("roket", _rocketClip, 0.9f);
            float s = Scale;
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f * R()) / 6f * Mathf.PI * 2f;
                StudioParticles.Emit(_sparks, World(r.Fire.At, RocketZ), 0.08f * s, 0.25f + 0.1f * R(), SparkTone,
                    Vel(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (3f + 2f * R()), 0.5f));
            }
        }

        /// <summary>Cells travelled: the schedule's sweep inside the board, accelerating past the last block to the frame edge.</summary>
        private float Travel(Rocket r)
        {
            float d = RocketCells(r.Fire, r.Timing, r.Age);
            if (d <= r.Last || r.Timing.LineSpeed <= 0f) return d;
            float x = d - r.Last, tau = x / r.Timing.LineSpeed;
            return r.Last + x + 0.5f * Tune.ExitAccel * tau * tau;
        }

        private void TickRockets(float dt)
        {
            if (_rockets.Count == 0) return;
            float tail = Mathf.Max(1e-3f, Tune.ExitTail);
            for (int i = _rockets.Count - 1; i >= 0; i--)
            {
                var r = _rockets[i];
                r.Age += dt;
                if (!r.Launched && r.Age >= r.Timing.LineLead * PaceOf(r.Fire)) Launch(r);
                if (r.Launched)
                {
                    float d = Travel(r);
                    Trail(r, Mathf.Min(d, r.Edge));
                    if (r.Exited < 0f)
                    {
                        if (d >= r.Edge) { r.Exited = 0f; ExitFlash(r); }
                    }
                    else r.Exited += dt;
                    if (r.Exited >= tail) { HideRocket(r); _rockets.RemoveAt(i); continue; }
                }
                PoseRocket(r);
            }
        }

        private void PoseRocket(Rocket r)
        {
            var tune = Tune;
            float lead = Mathf.Max(1e-4f, r.Timing.LineLead * PaceOf(r.Fire)), rs = RootScale, ball = tune.BallCells / ModelBall * rs;
            Vector3 squash;
            float glow, d = 0f;
            if (!r.Launched)
            {
                float u = StudioCurves.EaseOutCubic(Mathf.Clamp01(r.Age / lead));      // "ezilip parlar"
                squash = new Vector3(Mathf.Lerp(1f, tune.SquashAxis, u), Mathf.Lerp(1f, tune.SquashSide, u), Mathf.Lerp(1f, tune.SquashSide, u));
                glow = Mathf.Lerp(1f, tune.LineGlow, u);
            }
            else
            {
                d = Travel(r);
                float since = r.Age - lead, k = Mathf.Clamp01(since / 0.08f);           // springs out of the squash into a speed stretch
                squash = new Vector3(Mathf.Lerp(tune.SquashAxis, 1.1f, k), Mathf.Lerp(tune.SquashSide, 0.94f, k), Mathf.Lerp(tune.SquashSide, 0.94f, k));
                glow = Mathf.Lerp(tune.LineGlow, 1f, Mathf.Clamp01(since / 0.12f));
            }
            float fade = r.Exited >= 0f ? Mathf.Clamp01(1f - r.Exited / Mathf.Max(1e-3f, tune.ExitTail)) : 1f;
            r.Root.localPosition = Local(r.Fire.At + r.Dir * d, RocketZ);
            r.Half.localScale = squash * (ball * fade);
            r.Jet.localPosition = new Vector3(0f, 0f, 0.05f * rs);                    // just behind the cap
            r.Jet.localScale = Vector3.one * (tune.JetCells * rs * fade);
            _mpb.Clear();
            _mpb.SetVector(BaseColorId, new Vector4(glow, glow, glow, 1f));            // linear, HDR: the toon skin × glow
            r.HalfR.SetPropertyBlock(_mpb);
            if (!r.Launched) return;
            _mpb.Clear();
            _mpb.SetVector(SheetStId, JetFrame(r.Age - lead));
            r.JetR.SetPropertyBlock(_mpb);
        }

        /// <summary>Grey inked puffs every PuffEvery cells and warm sparks every SparkEvery cells behind the rocket, up to the frame edge.</summary>
        private void Trail(Rocket r, float upTo)
        {
            var tune = Tune;
            float s = Scale, puffEvery = Mathf.Max(0.05f, tune.PuffEvery), sparkEvery = Mathf.Max(0.05f, tune.SparkEvery);
            var side = new Vector2(-r.Dir.y, r.Dir.x);
            while (r.NextPuff <= upTo)
            {
                var at = r.Fire.At + r.Dir * (r.NextPuff - 0.3f) + side * ((R() - 0.5f) * 0.25f);
                StudioParticles.Emit(_puffs, World(at, RocketZ - 0.15f), (0.7f + 0.3f * R()) * s, 0.45f + 0.15f * R(), Smoke,
                    Vel(-r.Dir * 0.4f + side * ((R() - 0.5f) * 0.6f)), R() * 360f);
                r.NextPuff += puffEvery;
            }
            if (r.NextSpark > upTo) return;
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            while (r.NextSpark <= upTo)
            {
                var at = r.Fire.At + r.Dir * (r.NextSpark - 0.35f);
                StudioParticles.Emit(_sparks, World(at, RocketZ), (0.07f + 0.03f * R()) * s, 0.25f + 0.15f * R(), SparkTone,
                    Vel(-r.Dir * (2.5f + 2f * R()) + side * ((R() - 0.5f) * 3f), 0.5f + R()));
                r.NextSpark += sparkEvery;
            }
        }

        /// <summary>"Kenarda roket küçük bir flaşla çerçeveden çıkar": a small flash and star on the frame's middle line.</summary>
        private void ExitFlash(Rocket r)
        {
            float s = Scale;
            var at = World(r.Fire.At + r.Dir * r.Edge, RocketZ + 0.1f);
            StudioParticles.Emit(_flash, at, 1.1f * s, 0.12f, new Color(1f, 0.95f, 0.8f, 1f));
            StudioParticles.Emit(_stars, at, 1.5f * s, 0.15f, Color.white, Vector3.zero, R() * 45f);
            for (int k = 0; k < 4; k++)
                StudioParticles.Emit(_sparks, at, 0.08f * s, 0.2f + 0.1f * R(), SparkTone,
                    Vel(r.Dir * (2f + 2f * R()) + new Vector2(-r.Dir.y, r.Dir.x) * ((R() - 0.5f) * 4f), 0.5f));
        }

        // ---------- bomb: wind-up on a copy of the block, then the fireball ----------

        private void FireBomb(SpecialFire f, float age)
        {
            var b = new BombFx { Fire = f, Age = age, Lead = Mathf.Max(0f, _timing.BombLead * PaceOf(f)) };
            var p = TakeProxy();
            if (p != null && Dress(p, _game.ViewAt(f.Cell))) b.Proxy = p;
            else if (p != null) _proxyPool.Push(p);
            _bombs.Add(b);
            if (b.Proxy != null) PoseBomb(b);
            Sounds()?.PlayClip("fitil", _fuseClip, 0.8f);
        }

        /// <summary>The hidden (consumed) view's own pillow mesh, body material, tone and bomb sticker on a pooled copy.</summary>
        private bool Dress(Proxy p, BlockView v)
        {
            if (v == null) return false;
            var bodyF = v.GetComponent<MeshFilter>();
            var bodyR = v.GetComponent<MeshRenderer>();
            var item = v.transform.Find("Item");
            var itemF = item != null ? item.GetComponent<MeshFilter>() : null;
            var itemR = item != null ? item.GetComponent<MeshRenderer>() : null;
            if (bodyF == null || bodyR == null || itemF == null || itemR == null) return false;
            p.BodyF.sharedMesh = bodyF.sharedMesh;
            p.BodyR.sharedMaterial = bodyR.sharedMaterial;
            p.ItemF.sharedMesh = itemF.sharedMesh;
            p.ItemR.sharedMaterial = itemR.sharedMaterial;
            p.Item.localPosition = new Vector3(0f, 0f, -PotaLook.LabelZ);
            p.Item.localRotation = Quaternion.Euler(0f, 0f, v.LabelAngle);
            p.Item.localScale = Vector3.one * StickerQuad;
            _itemMpb.Clear();
            _itemMpb.SetVector(UvRectId, LabelAtlas.UvRect(LabelAtlas.SpecialCell(SpecialKind.Bomb)));
            _itemMpb.SetVector(ColorId, Color.white);
            p.ItemR.SetPropertyBlock(_itemMpb);
            p.Tone = v.Tone;
            p.Root.gameObject.SetActive(true);
            return true;
        }

        private void BombPose(BombFx b, out float k, out float shiver)
        {
            float u = b.Lead > 0f ? Mathf.Clamp01(b.Age / b.Lead) : 1f;
            k = 1f + (Tune.BombSwell - 1f) * StudioCurves.EaseOutCubic(u);
            shiver = Mathf.Sin(b.Age * 70f) * 0.05f * u;
        }

        private void PoseBomb(BombFx b)
        {
            BombPose(b, out float k, out float shiver);
            float u = b.Lead > 0f ? Mathf.Clamp01(b.Age / b.Lead) : 1f;
            float h = Mathf.Sin(Mathf.PI * Mathf.Max(1, Tune.BombBlinks) * u);
            h *= h;                                                                       // BombBlinks humps: red → white-hot → back
            var p = b.Proxy;
            p.Root.localPosition = Local(b.Fire.At + new Vector2(shiver, 0f), 0f);
            p.Root.localScale = Vector3.one * (RootScale * k);
            _mpb.Clear();
            _mpb.SetVector(BaseColorId, PotaLook.BlockColor(Color.Lerp(p.Tone, BlinkRed, h), 0.9f * h * h, false));
            p.BodyR.SetPropertyBlock(_mpb);
        }

        private Vector3 FuseAt(BombFx b)
        {
            BombPose(b, out float k, out float shiver);
            return World(b.Fire.At + new Vector2(shiver, 0f) + FuseTip * k, PotaLook.LabelZ * k);
        }

        private void TickBombs(float dt)
        {
            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                var b = _bombs[i];
                b.Age += dt;
                if (b.Age >= b.Lead) { _bombs.RemoveAt(i); Explode(b); continue; }
                if (b.Proxy != null) PoseBomb(b);
                b.NextSpark -= dt;
                if (b.NextSpark > 0f) continue;
                b.NextSpark += 0.03f;                                                    // "fitil kıvılcım saçar"
                FuseSparks(FuseAt(b), 2, 1.6f);
            }
        }

        private void Explode(BombFx b)
        {
            if (b.Proxy != null) { HideProxy(b.Proxy); b.Proxy = null; }
            var f = b.Fire;
            var tune = Tune;
            float s = Scale;
            StudioParticles.Emit(_flash, World(f.At, PotaLook.FrontZ + 0.6f), tune.CoreFlashCells * s, tune.CoreFlashTime, Color.white);
            StudioParticles.Emit(_fire, World(f.At, PotaLook.FrontZ + 0.5f), tune.FireCells / FireFill * s, tune.FireTime, Color.white,
                Vector3.zero, (R() - 0.5f) * 30f);
            StudioParticles.Emit(_rings, World(f.At, PotaLook.FrontZ + 0.3f), tune.RingCells * s, 0.4f, new Color(1f, 0.93f, 0.82f, 1f));
            LaunchShell(f.At);
            int puffs = Mathf.Max(0, tune.BombPuffs);
            for (int k = 0; k < puffs; k++)
            {
                float a = (k + 0.5f * R()) / Mathf.Max(1, puffs) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                StudioParticles.Emit(_puffs, World(f.At + dir * (0.6f + 0.4f * R()), PotaLook.FrontZ + 0.35f), (1f + 0.4f * R()) * s,
                    0.6f + 0.2f * R(), Smoke, Vel(dir * 0.8f + new Vector2(0f, 0.4f)), R() * 360f);
            }
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            for (int k = 0; k < tune.BombSparks; k++)
            {
                float a = R() * Mathf.PI * 2f;
                StudioParticles.Emit(_sparks, World(f.At, PotaLook.FrontZ + 0.5f), (0.08f + 0.04f * R()) * s, 0.35f + 0.2f * R(), SparkTone,
                    Vel(new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.3f) * (5f + 3f * R()), 0.8f * R()));
            }
            _game.Fx?.Glow(f.At, FireGlow.linear, 0.9f, 0f, tune.BombLight, tune.BombLightTime);   // the strong point light (Pota's)
            Push(f);
            _game.Juice?.Shake?.Add(tune.BombShake);
            _game.Juice?.TimeCtl?.Dip(_game.S.HitStopTapScale, tune.BombHitStop);
            Sounds()?.PlayClip("patlama", _game.S.SfxBomb);
        }

        /// <summary>"3×3 içindeki bloklar 0,1 hücre dışa itilir": only this step's bursting views (recycled at the drops).</summary>
        private void Push(SpecialFire f)
        {
            int n = _game.Board.Size;
            foreach (int c in ColorHuntRules.SingleArea(n, f.Cell, SpecialKind.Bomb))
            {
                if (c == f.Cell) continue;
                var v = _game.ViewAt(c);
                if (v == null || !v.Exploding || !v.gameObject.activeSelf) continue;
                var away = v.Home - f.At;
                if (away.sqrMagnitude < 1e-6f) continue;
                v.Pos = v.Home + away.normalized * Tune.PushCells;
                v.Flash = Mathf.Max(v.Flash, 0.5f);
            }
        }

        /// <summary>The Blender bomb's eight shell pieces fly out, spinning, with ShatterDebris' deterministic motion (game-root space).</summary>
        private void LaunchShell(Vector2 at)
        {
            float bs = RootScale, ms = Tune.BallCells / ModelBall * bs;
            var centre = Local(at, 0.2f);
            foreach (var part in _shellParts)
            {
                var p = TakePiece();
                if (p == null) break;
                p.F.sharedMesh = part.Mesh;
                p.D = ShatterDebris.Launch(centre, part.Local * (ms / bs), bs, _rng);     // P = centre + Local·ms; speed ∝ the grid scale
                p.D.Size = ms;
                p.Radius = part.Mesh.bounds.extents.magnitude * ms;
                ShatterDebris.Step(ref p.D, 0f, p.Radius);
                PosePiece(p);
                p.T.gameObject.SetActive(true);
                _pieces.Add(p);
            }
        }

        private void TickPieces(float dt)
        {
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                var p = _pieces[i];
                ShatterDebris.Step(ref p.D, dt, p.Radius);
                if (p.D.Age >= p.D.Life) { HidePiece(p); _pieces.RemoveAt(i); continue; }
                PosePiece(p);
            }
        }

        private static void PosePiece(Piece p)
        {
            p.T.localPosition = p.D.P;
            p.T.localRotation = p.D.R;
            p.T.localScale = Vector3.one * (p.D.Size * ShatterDebris.Shrink(p.D.Age, p.D.Life));
        }

        // ---------- 2+ specials ----------

        /// <summary>
        /// The 2+ specials flash: one full-screen white Image under the HUD canvas (first sibling: the counters stay on top). Cheaper than a
        /// camera quad (no material template, no near-plane fit, no sorting against the particles); the frame recorder already moves the
        /// HUD canvas into its captures.
        /// </summary>
        private void BuildScreenFlash()
        {
            var hud = _game != null ? _game.Hud : null;
            if (hud == null) return;
            var go = new GameObject("RoyalScreenFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _screenFlash = go.GetComponent<Image>();
            _screenFlash.raycastTarget = false;
            _screenFlash.color = new Color(1f, 1f, 1f, 0f);
            _screenFlash.enabled = false;
        }

        private void Combo()
        {
            var tune = Tune;
            _game.Juice?.TimeCtl?.SlowMo(tune.ComboSlowScale, tune.ComboSlowTime);
            if (_screenFlash == null) return;
            _screenAge = 0f;
            _screenFlash.enabled = true;
            _screenFlash.color = new Color(1f, 1f, 1f, tune.ScreenFlashAlpha);
        }

        private void TickScreenFlash(float dt)
        {
            if (_screenAge < 0f || _screenFlash == null) return;
            _screenAge += dt;
            float u = _screenAge / Mathf.Max(1e-3f, Tune.ScreenFlashTime);
            if (u >= 1f) { _screenAge = -1f; _screenFlash.enabled = false; return; }
            _screenFlash.color = new Color(1f, 1f, 1f, Tune.ScreenFlashAlpha * (1f - u) * (1f - u));
        }

        // ---------- golden: coin flight, gate, coin burst ----------

        private void TickFlights(float dt, int gen)
        {
            var tune = Tune;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _flights.Count) return;                       // a callback dealt a new board
                var f = _flights[i];
                f.T += dt;
                float t = f.T - f.Route.Delay;
                if (t < 0f) continue;                                                // the step's next ball leaves 0.12 s later
                float rs = f.Route.Scale > 0f ? f.Route.Scale : 1f, s = Scale;
                var pos = GoldenPath.At(f.Route, t) + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * rs);   // Pota's cizToplar z
                f.Spin += dt * tune.TokenSpin;
                if (f.Token != null)
                {
                    f.Token.gameObject.SetActive(true);
                    f.Token.localPosition = pos;
                    f.Token.localRotation = Quaternion.Euler(0f, f.Spin, 0f);
                    f.Token.localScale = Vector3.one * (tune.GoldenCoinCells / ModelCoin * rs);
                }
                float every = Mathf.Max(0.005f, tune.GlitterEvery);
                f.NextGlitter -= dt;
                while (f.NextGlitter <= 0f)
                {
                    f.NextGlitter += every;
                    Glitter(pos, s);
                }
                if (!f.Passed && GoldenPath.PastGate(t))
                {
                    f.Passed = true;
                    var gates = _game.Gates;
                    if (gates != null) gates.Flash(f.Route.Col);                       // Pota's ring flash (#ffe27a), as PotaSpecials
                    GateBurst(pos, s);
                    f.PassedGate?.Invoke();
                    if (gen != _gen) return;
                }
                if (!GoldenPath.Landed(t)) continue;
                _flights.RemoveAt(i);
                HideToken(f);
                Land(f.Route);
                f.Landed?.Invoke();
                if (gen != _gen) return;
            }
        }

        private void Glitter(Vector3 local, float s)
        {
            var jitter = new Vector3((R() - 0.5f) * 0.35f, (R() - 0.5f) * 0.35f, 0f) * s;
            StudioParticles.Emit(_twinkles, transform.TransformPoint(local) + jitter, (0.16f + 0.14f * R()) * s, 0.35f + 0.2f * R(), GoldWhite,
                new Vector3(0f, -0.4f * s, 0f), R() * 360f);
        }

        private void GateBurst(Vector3 local, float s)
        {
            var at = transform.TransformPoint(local);
            StudioParticles.Emit(_stars, at, 1.6f * s, 0.18f, Gold, Vector3.zero, R() * 45f);
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f * R()) / 6f * Mathf.PI * 2f;
                StudioParticles.Emit(_twinkles, at, (0.2f + 0.12f * R()) * s, 0.4f + 0.2f * R(), GoldWhite,
                    transform.TransformVector(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f)) * (1.2f * s), R() * 360f);
            }
        }

        /// <summary>At the rim: Pota's gold glow, spinning coins thrown up, a gold star and twinkles, altin-inis + the coin clink, the net swings.</summary>
        private void Land(GoldenRoute r)
        {
            var tune = Tune;
            float rs = r.Scale > 0f ? r.Scale : 1f, s = Scale;
            var rim = r.Hoop + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * rs);
            var p = _game.BoardRoot.InverseTransformPoint(_game.transform.TransformPoint(r.Hoop));
            _game.Fx?.Glow(new Vector2(p.x, p.y), PotaLook.Hex(PotaSpecials.GoldenFlash).linear, 1.8f / rs, 4.2f / rs, 5f, 0.22f, -p.z);
            LaunchCoins(rim, rs);
            var at = transform.TransformPoint(rim);
            StudioParticles.Emit(_stars, at, 2.4f * s, 0.25f, Gold, Vector3.zero, R() * 45f);
            int stars = Mathf.Max(0, tune.LandStars);
            for (int k = 0; k < stars; k++)
            {
                float a = (k + 0.6f * R()) / Mathf.Max(1, stars) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.7f + 0.5f, -0.3f);
                StudioParticles.Emit(_twinkles, at, (0.26f + 0.16f * R()) * s, 0.55f + 0.25f * R(), GoldWhite,
                    transform.TransformVector(dir) * ((2f + 1.5f * R()) * rs), R() * 360f);
            }
            var sounds = Sounds();
            sounds?.PlayClip("altinInis", _game.S.SfxGoldenLand);
            sounds?.PlayClip("para", _coinClip, 0.85f);
            var hoop = _game.Hoop;
            if (hoop != null) hoop.Swing();
        }

        private void LaunchCoins(Vector3 rim, float rs)
        {
            var tune = Tune;
            int n = Mathf.Max(0, tune.LandCoins);
            float size = tune.CoinCells / ModelCoin * rs;
            for (int i = 0; i < n; i++)
            {
                var c = TakeCoin();
                if (c == null) break;
                float a = (i + 0.5f * R()) / Mathf.Max(1, n) * Mathf.PI * 2f;
                var v = new Vector3(Mathf.Cos(a) * (1.6f + 1.4f * R()), 3.2f + 2.2f * R() + Mathf.Sin(a) * 0.8f, -(0.8f + 1.4f * R())) * rs;
                var w = new Vector3((R() - 0.5f) * 360f, 540f + 360f * R(), (R() - 0.5f) * 180f);   // mostly a spin about y: the coin flips
                var turn = Quaternion.Euler(R() * 360f, R() * 360f, 0f);
                float life = 0.9f + 0.4f * R();
                c.D = new Debris { P = rim, V = v, W = w, R = turn, Age = 0f, Life = life, Size = size };
                PoseCoin(c);
                c.T.gameObject.SetActive(true);
                _coins.Add(c);
            }
        }

        private void TickCoins(float dt)
        {
            float g = CoinGravity * RootScale;
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                CoinStep(ref c.D, dt, g);
                if (c.D.Age >= c.D.Life) { HideCoin(c); _coins.RemoveAt(i); continue; }
                PoseCoin(c);
            }
        }

        private static void PoseCoin(Coin c)
        {
            c.T.localPosition = c.D.P;
            c.T.localRotation = c.D.R;
            c.T.localScale = Vector3.one * (c.D.Size * ShatterDebris.Shrink(c.D.Age, c.D.Life));
        }

        // ---------- pools (each capped; all under this object = the game root's space) ----------

        private Rocket TakeRocket()
        {
            if (_rockets.Count >= RocketMax) return null;
            if (_rocketPool.Count > 0) return _rocketPool.Pop();
            var root = new GameObject("Rocket").transform;
            root.SetParent(transform, false);
            var r = new Rocket { Root = root };
            var half = new GameObject("Half");
            half.transform.SetParent(root, false);
            r.Half = half.transform;
            r.HalfF = half.AddComponent<MeshFilter>();
            r.HalfR = half.AddComponent<MeshRenderer>();
            r.HalfR.sharedMaterial = _halfMat;
            r.HalfR.shadowCastingMode = ShadowCastingMode.On;
            var jet = new GameObject("Jet");
            jet.transform.SetParent(root, false);
            r.Jet = jet.transform;
            jet.AddComponent<MeshFilter>().sharedMesh = JetQuad();
            r.JetR = jet.AddComponent<MeshRenderer>();
            r.JetR.sharedMaterial = _jetMat;
            r.JetR.sortingOrder = 6;
            r.JetR.shadowCastingMode = ShadowCastingMode.Off;
            r.JetR.receiveShadows = false;
            root.gameObject.SetActive(false);
            return r;
        }

        private void HideRocket(Rocket r)
        {
            r.Root.gameObject.SetActive(false);
            _rocketPool.Push(r);
        }

        private Proxy TakeProxy()
        {
            if (ProxiesLive >= ProxyMax) return null;
            if (_proxyPool.Count > 0) return _proxyPool.Pop();
            var root = new GameObject("BombCopy").transform;
            root.SetParent(transform, false);
            var p = new Proxy { Root = root, BodyF = root.gameObject.AddComponent<MeshFilter>(), BodyR = root.gameObject.AddComponent<MeshRenderer>() };
            p.BodyR.shadowCastingMode = ShadowCastingMode.On;                          // as BlockView's body
            p.BodyR.receiveShadows = true;
            var item = new GameObject("Item");
            item.transform.SetParent(root, false);
            p.Item = item.transform;
            p.ItemF = item.AddComponent<MeshFilter>();
            p.ItemR = item.AddComponent<MeshRenderer>();
            p.ItemR.shadowCastingMode = ShadowCastingMode.Off;
            p.ItemR.receiveShadows = false;
            p.ItemR.lightProbeUsage = LightProbeUsage.Off;
            p.ItemR.reflectionProbeUsage = ReflectionProbeUsage.Off;
            p.ItemR.sortingOrder = 2;                                                   // BlockView's sticker order
            root.gameObject.SetActive(false);
            return p;
        }

        private void HideProxy(Proxy p)
        {
            p.Root.gameObject.SetActive(false);
            _proxyPool.Push(p);
        }

        private Piece TakePiece()
        {
            if (_pieces.Count >= ShellMax) return null;
            if (_piecePool.Count > 0) return _piecePool.Pop();
            var go = new GameObject("ShellPiece");
            go.transform.SetParent(transform, false);
            var p = new Piece { T = go.transform, F = go.AddComponent<MeshFilter>() };
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _shellMat;
            r.shadowCastingMode = ShadowCastingMode.On;
            go.SetActive(false);
            return p;
        }

        private void HidePiece(Piece p)
        {
            p.T.gameObject.SetActive(false);
            _piecePool.Push(p);
        }

        private Coin TakeCoin()
        {
            if (_coins.Count >= CoinMax) return null;
            if (_coinPool.Count > 0) return _coinPool.Pop();
            var go = new GameObject("Coin");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _coinMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _coinMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return new Coin { T = go.transform };
        }

        private void HideCoin(Coin c)
        {
            c.T.gameObject.SetActive(false);
            _coinPool.Push(c);
        }

        private Transform TakeToken()
        {
            if (TokensLive >= TokenMax) return null;
            if (_tokenPool.Count > 0) return _tokenPool.Pop();
            var go = new GameObject("GoldenCoin");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _coinMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _coinMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        private void HideToken(Flight f)
        {
            if (f.Token == null) return;
            f.Token.gameObject.SetActive(false);
            _tokenPool.Push(f.Token);
            f.Token = null;
        }

        // ---------- helpers ----------

        private ParticleSystem Sys(string name, Material m, ParticleSystemRenderMode mode, int max, int order)
        {
            var ps = StudioParticles.System(transform, name, m, mode, max, order);
            _systems.Add(ps);
            return ps;
        }

        private Material Keep(Material m)
        {
            _owned.Add(m);
            return m;
        }

        /// <summary>World size of one board cell (particles).</summary>
        private float Scale => _game.BoardRoot.lossyScale.x;
        /// <summary>Game-root units per board cell (meshes under this object, which sits at the game root's origin).</summary>
        private float RootScale => _game.BoardRoot.localScale.x;
        /// <summary>Board cell → world; <paramref name="z"/> in cells toward the camera (Pota's +z).</summary>
        private Vector3 World(Vector2 p, float z) => _game.BoardRoot.TransformPoint(new Vector3(p.x, p.y, -z));
        private Vector3 Local(Vector2 p, float z) => transform.InverseTransformPoint(World(p, z));
        /// <summary>A velocity in cells/s (plus toward the camera) → world units/s.</summary>
        private Vector3 Vel(Vector2 d, float towardCamera = 0f) => _game.BoardRoot.TransformVector(new Vector3(d.x, d.y, -towardCamera));
        private float R() => (float)_rng.NextDouble();
        private float Interval(float rate) => rate > 0f ? (0.7f + 0.6f * R()) / rate : 1e6f;
        private PotaSounds Sounds() => _game != null && _game.Juice != null ? _game.Juice.Sounds : null;

        private void OnDestroy()
        {
            foreach (var m in _owned) if (m != null) Destroy(m);
            foreach (var c in new[] { _rocketClip, _fuseClip, _coinClip }) if (c != null) Destroy(c);
            if (_screenFlash != null) Destroy(_screenFlash.gameObject);
        }

        // ---------- synth sounds (spec "Sesler": rocket "vınn", fuse hiss, coin clink) through PotaSounds.Synth ----------

        /// <summary>The rocket's "vınn": noise through a lowpass opening 900 → 3600 → 900 Hz, under a 260 → 720 Hz tone, 0.42 s.</summary>
        public static AudioClip MakeRocket()
        {
            float lp = 0f, phase = 0f;
            return PotaSounds.Synth("roket", RocketSoundTime, t =>
            {
                int i = Mathf.RoundToInt(t * SynthRate);
                float u = t / RocketSoundTime;
                float cutoff = Mathf.Lerp(900f, 3600f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.3f)));
                lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SynthRate)) * (Noise(i) - lp);
                float f = Mathf.Lerp(260f, 720f, Mathf.Min(1f, u * 2.2f)) * (1f - 0.25f * Mathf.Max(0f, u - 0.45f));
                phase += 2f * Mathf.PI * f / SynthRate;
                float env = Mathf.Min(1f, t / 0.025f) * Mathf.Pow(Mathf.Max(0f, 1f - u), 1.6f);
                return env * (0.55f * lp + 0.2f * Mathf.Sin(phase) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 32f * t)));
            });
        }

        /// <summary>The fuse's hiss: high-passed noise with sparse crackles, 0.22 s (covers the 0.18 s wind-up).</summary>
        public static AudioClip MakeFuse()
        {
            float lp = 0f;
            return PotaSounds.Synth("fitil", FuseSoundTime, t =>
            {
                int i = Mathf.RoundToInt(t * SynthRate);
                float n = Noise(i);
                lp += 0.12f * (n - lp);
                float hiss = n - lp;
                float crackle = Noise(i / 220 + 7919) > 0.8f ? Noise(i * 3 + 1) * Mathf.Exp(-(i % 220) / 30f) : 0f;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Clamp01((FuseSoundTime - t) / 0.05f);
                return env * (0.2f * hiss + 0.3f * crackle);
            });
        }

        /// <summary>The coin clink: two metallic strikes 0.07 s apart (C7/G7/E8, then D7/A7/F#8 partials), 0.35 s.</summary>
        public static AudioClip MakeCoin() => PotaSounds.Synth("para", CoinSoundTime, t =>
            0.16f * (Partial(t, 0f, 2093f, 18f) + 0.6f * Partial(t, 0f, 3136f, 24f) + 0.35f * Partial(t, 0f, 5274f, 30f)
                   + 0.8f * Partial(t, 0.07f, 2349f, 18f) + 0.5f * Partial(t, 0.07f, 3520f, 24f) + 0.3f * Partial(t, 0.07f, 5920f, 30f)));

        private static float Partial(float t, float t0, float f, float decay)
        {
            float u = t - t0;
            return u < 0f ? 0f : Mathf.Min(1f, u / 0.002f) * Mathf.Exp(-decay * u) * Mathf.Sin(2f * Mathf.PI * f * u);
        }

        /// <summary>Hash noise in [−1, 1] for sample i (PCG): the synth clips are identical every run, so recordings repeat.</summary>
        private static float Noise(int i)
        {
            unchecked
            {
                uint x = (uint)i * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return x / 4294967295f * 2f - 1f;
            }
        }
    }
}
```

- [ ] **Step 6: The game builds Royal**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs`, in `CreateSpecialStyle`, right after the line
`case SpecialLook.Pota: return AddSpecialStyle<PotaSpecials>("PotaSpecials");` insert:
```csharp
                case SpecialLook.Royal: return AddSpecialStyle<RoyalSpecials>("RoyalSpecials");
```
(One added line: it does not matter whether Task 11's Court case is already there. If the `default:` comment still says
`// Royal: Task 10, Court: Task 11`, make it `// Court: Task 11`, or drop it once Court's case exists.)

- [ ] **Step 7: Compile, serialize the new settings block, run the EditMode tests**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.RoyalTests Project.Tests.BlockViewTests Project.Tests.ColorHuntCameraTests \
  Project.Tests.SpecialScheduleTests Project.Tests.SpecialStyleTests Project.Tests.StudioAssetsTests Project.Tests.RuntimeShaderBuildTests
python3 - "$S" <<'EOF'
import json, sys
code = ('var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Project.ColorHunt.ColorHuntSettings>('
        '"Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset"); '
        'UnityEditor.EditorUtility.SetDirty(s); UnityEditor.AssetDatabase.SaveAssets(); '
        'return "Royal.LineSpeed " + s.Royal.LineSpeed + " BombLead " + s.Royal.BombLead;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1] + "/royal-save-settings.json", "w"))
EOF
~/.local/bin/uv run Tools/mcp_call.py call execute_code @"$S/royal-save-settings.json" | grep '"result"'
git diff --stat Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset
~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile
```
Expected: the first run compiles (exit ≠ 2) and every listed fixture passes — `RoyalTests` 10/10, `BlockViewTests` unchanged, the new
`ColorHuntCameraTests` case, `SpecialScheduleTests`/`SpecialStyleTests` unchanged (`UnwrittenLook_FallsBackToPota` still uses Court,
which stays `NoSpecialStyle`), `RuntimeShaderBuildTests` green (no new shader name). The save prints
`"result": "Royal.LineSpeed 18.75 BombLead 0.18"`; `git diff --stat` shows `ColorHuntSettings.asset` with only insertions (the `Royal:`
block). The full EditMode run passes.
- `WithTheImportedHandoffs_RoyalIsReady` failing on `AssetsReady` means a Task 9 field is empty: run `StudioAssetsTests`, then
  `execute_custom_tool ai_studio_import`; do not loosen `AssetsReady`.
- A `Rockets_…` failure is a formula mismatch with `SpecialSchedule.Delay` (Task 5): fix `RocketReach`/`RocketCells`, never the test.

- [ ] **Step 8: Write the PlayMode tests**

Create `Assets/_Project/Games/Pota/Tests/PlayMode/RoyalPlayTests.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>
    /// 1 · Royal in the Specials demo scene as saved (spec 2026-10-01 §2; Review Focus 2: restart mid-rocket). "Without errors" is the
    /// Test Framework's own rule: any Debug.LogError or exception fails the test.
    /// </summary>
    public class RoyalPlayTests
    {
        /// <summary>A real RoyalSpecials behind a counter: every Gather's arrived and every flight's callbacks are recorded.</summary>
        private sealed class CountingRoyal : ISpecialStyle
        {
            public readonly RoyalSpecials Inner;
            /// <summary>Per Gather call: how many times its arrived came.</summary>
            public readonly List<int> Arrivals = new List<int>();
            /// <summary>Per GoldenFlight call: "g" for passedGate, "l" for landed, in call order.</summary>
            public readonly List<string> Flights = new List<string>();

            public CountingRoyal(RoyalSpecials inner) { Inner = inner; }

            public bool Ready => Inner.Ready;
            public int LiveCount => Inner.LiveCount;
            public SpecialTiming Timing => Inner.Timing;
            public bool GathersBirthGroup => Inner.GathersBirthGroup;
            public bool ConsumesSpecialView => Inner.ConsumesSpecialView;
            public void Build(ColorHuntGame game) => Inner.Build(game);
            public void Activate(bool on) => Inner.Activate(on);
            public void Clear() => Inner.Clear();
            public void Idle(BlockView v, float dt) => Inner.Idle(v, dt);
            public void Birth(BlockView v, SpecialKind kind) => Inner.Birth(v, kind);
            public void TriggerStep(int specials, Vector2 centre) => Inner.TriggerStep(specials, centre);
            public void Fire(SpecialFire fire) => Inner.Fire(fire);

            public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived)
            {
                int id = Arrivals.Count;
                Arrivals.Add(0);
                Inner.Gather(v, to, duration, x => { Arrivals[id]++; arrived(x); });
            }

            public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
            {
                int id = Flights.Count;
                Flights.Add("");
                Inner.GoldenFlight(route, () => { Flights[id] += "g"; passedGate(); }, () => { Flights[id] += "l"; landed(); });
            }
        }

        private static IEnumerator Load(Action<SpecialsDemo> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene (Task 6)");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, demo.Game.Current, "the start scenario was not staged");
            ready(demo);
        }

        /// <summary>Registers a counting Royal before the look is first resolved (the scene starts on Pota) and switches to it.</summary>
        private static CountingRoyal UseCounting(SpecialsDemo demo)
        {
            var game = demo.Game;
            var go = new GameObject("RoyalSpecials");
            go.transform.SetParent(game.transform, false);
            var counting = new CountingRoyal(go.AddComponent<RoyalSpecials>());
            game.UseSpecialStyle(SpecialLook.Royal, counting);
            demo.SetLook(SpecialLook.Royal);
            Assert.AreSame(counting, game.Specials, "Royal is ready (Task 9's ai_studio_import ran) and did not fall back to Pota");
            return counting;
        }

        /// <summary>Plays <paramref name="s"/>'s script to its end; <paramref name="perFrame"/> runs every frame (budget checks, maxima).</summary>
        private static IEnumerator PlayToTheEnd(SpecialsDemo demo, SpecialScenario s, Action perFrame, float timeout = 25f)
        {
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(s, true);
            float t = 0f;
            while (!done && t < timeout)
            {
                perFrame?.Invoke();
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, $"{s} never finished under Royal");
        }

        private static void AssertViewsShowTheBoard(ColorHuntGame game, string what)
        {
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsNotNull(v, $"{what}: no view at {i}");
                Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
            }
        }

        private static void AssertBudget(RoyalSpecials royal, ColorHuntGame game, string what)
        {
            Assert.LessOrEqual(royal.LiveCount, RoyalSpecials.Cap, $"{what}: Royal's own particles and pieces");
            Assert.LessOrEqual(royal.LiveCount + game.Fx.LiveCount, BurstBudget.Max, $"{what}: with Pota's burst pools");
        }

        [UnityTest]
        public IEnumerator EveryScenario_PlaysToTheEnd_UnderRoyal_WithinTheBudget()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Royal);
            var royal = game.Specials as RoyalSpecials;
            Assert.IsNotNull(royal, "Royal is ready (Task 9's ai_studio_import ran) and did not fall back to Pota");
            Assert.AreEqual(RoyalSpecials.TimingOf(game.S.Royal), game.Specials.Timing);
            foreach (SpecialScenario s in Enum.GetValues(typeof(SpecialScenario)))
            {
                int gathers = 0, rockets = 0, copies = 0, pieces = 0, coins = 0, tokens = 0, slowFrames = 0;
                float flash = 0f;
                yield return PlayToTheEnd(demo, s, () =>
                {
                    AssertBudget(royal, game, s.ToString());
                    gathers = Mathf.Max(gathers, royal.GatherCount);
                    rockets = Mathf.Max(rockets, royal.RocketsLive);
                    copies = Mathf.Max(copies, royal.ProxiesLive);
                    pieces = Mathf.Max(pieces, royal.PiecesLive);
                    coins = Mathf.Max(coins, royal.CoinsLive);
                    tokens = Mathf.Max(tokens, royal.TokensLive);
                    flash = Mathf.Max(flash, royal.ScreenFlash);
                    if (Mathf.Abs(Time.timeScale - game.S.Royal.ComboSlowScale) < 0.01f) slowFrames++;   // dips are ×0.06/×0.12, never 0.5
                });
                Assert.IsFalse(game.Busy, s.ToString());
                Assert.AreEqual(0, game.GoldenInFlight, s.ToString());
                Assert.AreEqual(s == SpecialScenario.Golden ? 2 : 0, game.GoldenLanded, $"{s}: golden balls in the hoop");
                AssertViewsShowTheBoard(game, $"Royal/{s}");
                switch (s)
                {
                    case SpecialScenario.Birth:
                        Assert.Greater(gathers, 0, "Birth: the group travels into the birth cell");
                        break;
                    case SpecialScenario.Line:
                        Assert.GreaterOrEqual(rockets, 2, "Line: the ball splits into two rockets");
                        break;
                    case SpecialScenario.Bomb:
                        Assert.Greater(copies, 0, "Bomb: the wind-up draws the block's copy");
                        Assert.GreaterOrEqual(pieces, 8, "Bomb: eight shell pieces");
                        break;
                    case SpecialScenario.Chain:
                        Assert.Greater(rockets, 0, "Chain: rockets");
                        Assert.Greater(pieces, 0, "Chain: the bomb went");
                        Assert.Greater(slowFrames, 0, "Chain: 2+ specials → ×0.5 slow motion");
                        Assert.Greater(flash, 0f, "Chain: 2+ specials → white screen flash");
                        break;
                    case SpecialScenario.Golden:
                        Assert.Greater(tokens, 0, "Golden: the coin token flew");
                        Assert.GreaterOrEqual(coins, Mathf.Min(8, game.S.Royal.LandCoins), "Golden: coins burst from the rim");
                        break;
                }
            }
        }

        [UnityTest]
        public IEnumerator Restart_MidRocket_LeavesNothing()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Royal);
            var royal = game.Specials as RoyalSpecials;
            Assert.IsNotNull(royal, "Royal is ready");
            demo.Play(SpecialScenario.Line, true);
            yield return StudioLookPlayTests.WaitUntil(() => royal.RocketsLive > 0 && royal.LiveCount > royal.RocketsLive * RoyalSpecials.PerRocket, 8f);
            Assert.Greater(royal.RocketsLive, 0, "caught mid-rocket");
            Assert.IsTrue(game.Busy, "the trigger step runs");

            demo.Play(SpecialScenario.Line, false);                        // "Tekrar" without auto: a fresh board staged over the rockets
            int bursts = 0;
            void Count(int cell) => bursts++;
            game.CellBurst += Count;
            Assert.AreEqual(0, royal.LiveCount, "Clear dropped every rocket, jet, puff, spark and flash");
            Assert.AreEqual(0, royal.RocketsLive);
            Assert.AreEqual(0f, royal.ScreenFlash);
            Assert.IsFalse(game.Busy, "the lock is released at once");
            Assert.AreEqual(0, game.GoldenInFlight);
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);   // the old rockets would have crossed the frame by now
            game.CellBurst -= Count;
            Assert.AreEqual(0, bursts, "no fire of the old plan reached the new board");
            Assert.AreEqual(0, royal.LiveCount, "nothing came back (the Line board's two line balls idle without particles)");
            Assert.AreEqual(SpecialKind.LineH, game.Board.Cells[22].Special);
            Assert.AreEqual(SpecialKind.LineV, game.Board.Cells[19].Special);
            AssertViewsShowTheBoard(game, "after the restart");
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsTrue(v.gameObject.activeSelf, $"cell {i}: a consumed view of the old board must not linger hidden");
                Assert.AreEqual(1f, v.Scale, $"cell {i}: no pop or breath left on a fresh view");
            }

            Assert.AreEqual("broken", game.Tap(22), "the new board plays");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.IsFalse(game.Busy);
            AssertViewsShowTheBoard(game, "after the replay");
        }

        [UnityTest]
        public IEnumerator Gather_CallsArrivedOncePerGroupBlock()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            var counting = UseCounting(demo);
            // Every exploded group block of every birth in the Birth script (the rules' own answer). A salvo's step 0 is its own block
            // (depth 0, no gathering) and never bears a special.
            int expected = 0;
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(SpecialScenario.Birth));
            foreach (int cell in SpecialScenarios.Taps(SpecialScenario.Birth))
            {
                var r = ColorHuntRules.Break(b, cell);
                Assert.IsEmpty(r.Steps[0].Born, $"tap {cell}: step 0 is the salvo's block");
                for (int k = 1; k < r.Steps.Count; k++)
                {
                    var step = r.Steps[k];
                    var exploded = new HashSet<int>(step.Exploded);
                    var seen = new HashSet<int>();
                    foreach (var born in step.Born)
                        if (born.Group != null)
                            foreach (int g in born.Group)
                                if (exploded.Contains(g) && seen.Add(g)) expected++;
                }
                b = r.Board;
            }
            Assert.Greater(expected, 0, "the Birth script gathers groups");
            yield return PlayToTheEnd(demo, SpecialScenario.Birth, () => AssertBudget(counting.Inner, game, "Birth"));
            Assert.AreEqual(expected, counting.Arrivals.Count, "one Gather per exploded group block");
            CollectionAssert.AreEqual(Enumerable.Repeat(1, counting.Arrivals.Count).ToList(), counting.Arrivals, "arrived exactly once each");
            Assert.AreEqual(0, counting.Inner.GatherCount, "none left travelling");
            AssertViewsShowTheBoard(game, "after the births");
        }

        [UnityTest]
        public IEnumerator Golden_LandsOnce_WithACoinBurst()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            var counting = UseCounting(demo);
            int coins = 0, tokens = 0;
            yield return PlayToTheEnd(demo, SpecialScenario.Golden, () =>
            {
                AssertBudget(counting.Inner, game, "Golden");
                coins = Mathf.Max(coins, counting.Inner.CoinsLive);
                tokens = Mathf.Max(tokens, counting.Inner.TokensLive);
            });
            CollectionAssert.AreEqual(new[] { "gl", "gl" }, counting.Flights, "each ball: through its gate once, then landed once");
            Assert.AreEqual(2, game.GoldenLanded);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(2, tokens, "both balls flew as coins, 0.12 s apart");
            Assert.GreaterOrEqual(coins, Mathf.Min(8, game.S.Royal.LandCoins), "8–12 spinning coins at the rim");
            CollectionAssert.IsEmpty(game.Gates.Shown, "both rings closed as their balls passed");
            yield return StudioLookPlayTests.WaitUntil(() => counting.Inner.CoinsLive == 0, 3f);
            Assert.AreEqual(0, counting.Inner.CoinsLive, "the coins fade out");
            Assert.AreEqual(2, game.GoldenLanded, "nothing landed twice");
        }
    }
}
```

- [ ] **Step 9: Run the PlayMode tests (new, the specials suites, then everything)**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.RoyalPlayTests Project.Tests.SpecialsDemoPlayTests Project.Tests.SpecialPlayTests
~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile
~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile
```
Expected: `RoyalPlayTests` 4/4; `SpecialsDemoPlayTests` 6/6 (its Royal case now draws Royal instead of falling back; the Court case still
warns and falls back); `SpecialPlayTests` 5/5 (Pota look, unchanged); both full suites pass (under every other look nothing writes `BlockView.Scale` during play, and nothing
calls `AxisShake` or reads the new settings block).
- `EveryScenario_…` failing on `royal == null`: the Royal look fell back — read the warning in the console; Task 9's import is incomplete.
- A budget assertion failing: a mesh was created outside its `Take…` method or a pool constant changed; `PoolCapacity ≤ Cap` is pinned
  in EditMode, so the live count can only exceed it through a missing `Hide…` (look for the list it was not removed from).
- `Restart_MidRocket_LeavesNothing` failing on `bursts`: a fire of the old plan survived `Deal` — check Task 5's `ClearViews` clears
  `_jobs` and `ResetSpecials` calls `Clear()`; never relax the test.

- [ ] **Step 10: Look at it (Play Mode, stepped frame by frame)**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
cat > "$S/royal_look.py" <<'PY'
#!/usr/bin/env python3
"""Steps the Specials demo under 1 · Royal one frame per execute_code call (paused editor + EditorApplication.Step, CLAUDE.md) and
screenshots the moments named in PLAN. Prints 'tag<TAB>png' lines."""
import json, os, re, subprocess, time

ROOT = "/Users/tunaaltin17/projects/Unity MCP"
S = os.path.dirname(os.path.abspath(__file__))
UV = os.path.expanduser("~/.local/bin/uv")
NS = "Project.ColorHunt."

def mcp(*args):
    out = subprocess.run([UV, "run", "Tools/mcp_call.py", *args], cwd=ROOT, capture_output=True, text=True)
    return out.stdout + out.stderr

def code(name, src):
    path = os.path.join(S, f"royal-{name}.json")
    with open(path, "w") as f:
        json.dump({"action": "execute", "code": src, "safety_checks": True}, f)
    m = re.search(r'"result":\s*"((?:[^"\\]|\\.)*)"', mcp("call", "execute_code", "@" + path))
    return m.group(1) if m else ""

def shot(tag):
    out = mcp("call", "manage_camera", json.dumps({"action": "screenshot", "capture_source": "game_view",
                                                    "include_image": False, "output_folder": "Temp/mcp_out"}))
    m = re.search(r'[^" ]*\.png', out)
    print(f"{tag}\t{m.group(0) if m else 'NO SHOT: ' + out[:200]}", flush=True)

OPEN = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        'if (s.path != "Assets/_Project/Games/Pota/Scenes/Specials.unity") { if (s.isDirty) return "dirty: " + s.path; '
        'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Games/Pota/Scenes/Specials.unity"); } '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
STEP = ('UnityEditor.EditorApplication.isPaused = true; UnityEditor.EditorApplication.Step(); '
        'var g = UnityEngine.Object.FindAnyObjectByType<' + NS + 'ColorHuntGame>(); '
        'var r = g.Specials as ' + NS + 'RoyalSpecials; if (r == null) return "not-royal"; '
        'return "g" + r.GatherCount + " k" + r.RocketsLive + " x" + r.ProxiesLive + " p" + r.PiecesLive + " c" + r.CoinsLive '
        '+ " f" + g.GoldenInFlight + " n" + r.LiveCount + " b" + (g.Busy ? 1 : 0);')

def setup(scenario, cell):
    return ('UnityEditor.EditorApplication.isPaused = false; '
            'var d = UnityEngine.Object.FindAnyObjectByType<' + NS + 'SpecialsDemo>(); '
            'd.SetLook(' + NS + 'SpecialLook.Royal); d.Play(' + NS + 'SpecialScenario.' + scenario + ', false); '
            'var res = d.Game.Tap(' + str(cell) + '); UnityEditor.EditorApplication.isPaused = true; '
            'return res + " " + d.Game.Specials.GetType().Name;')

# scenario, its first scripted tap, [(key, frame offsets)]: a key's first true frame starts its shots; "!g" = the gathers ended.
PLAN = [
    ("Line", 22, [("k", [0, 3, 6, 9, 12])]),
    ("Bomb", 24, [("x", [0, 4, 7]), ("p", [0, 3, 7, 12])]),
    ("Chain", 21, [("k", [0, 4, 8, 12, 16, 20])]),
    ("Birth", 44, [("g", [0, 4]), ("!g", [0, 3, 6, 10])]),
    ("Golden", 45, [("f", [0, 10, 18, 30]), ("c", [0, 6, 12])]),
]

def value(state, key):
    m = re.search(r"\b" + key + r"(\d+)", state)
    return int(m.group(1)) if m else 0

def main():
    print("open:", code("open", OPEN), flush=True)
    mcp("call", "manage_editor", '{"action":"play"}')
    for _ in range(30):
        if '"is_playing": true' in mcp("read", "mcpforunity://editor/state"):
            break
        time.sleep(2)
    time.sleep(4)
    for scenario, cell, keys in PLAN:
        print(scenario, "setup:", code("setup-" + scenario.lower(), setup(scenario, cell)), flush=True)
        started, due, seen_gather = {}, {}, False
        for i in range(170):
            state = code("step", STEP)
            for key, offsets in keys:
                if key in started:
                    continue
                hit = (seen_gather and value(state, "g") == 0) if key == "!g" else value(state, key) > 0
                if hit:
                    started[key] = i
                    for o in offsets:
                        due.setdefault(i + o, []).append(f"{scenario}-{key.replace('!', 'end-')}+{o}")
            if value(state, "g") > 0:
                seen_gather = True
            for tag in due.pop(i, []):
                shot(f"{tag}  [{state}]")
            if len(started) == len(keys) and not due:
                break
    code("resume", 'UnityEditor.EditorApplication.isPaused = false; return "resumed";')
    mcp("call", "manage_editor", '{"action":"stop"}')

main()
PY
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
~/.local/bin/uv run Tools/mcp_call.py call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
python3 "$S/royal_look.py"
```
Expected: `open: Specials`, then per scenario `setup: broken RoyalSpecials` and about 30 lines `tag<TAB>Temp/mcp_out/…png  [g… k… …]`
(every `n` value ≤ 342). If a setup line says `not-royal` or `PotaSpecials`, Royal fell back (fix Task 9's import first). If
`execute_code` refuses `EditorApplication.Step` under `safety_checks`, rerun with `"safety_checks": false` in `code()`.

Open every PNG (Read tool) and check, against the spec's Royal table and the Pota blocks around:
1. **Line (LineH at (3, 1))**: `k+0` an orange basketball (not the pillow) at (3, 1), squashed horizontally and glowing white, a white
   flash; `k+3…+9` two half balls, **domes outward** (the left one's dome points left), flat caps toward each other with the flame jets
   coming out of the caps backward; grey inked puffs every half cell and warm sparks behind them; row 3's blocks bursting (Pota bursts)
   only behind the right rocket, never ahead of it; `k+12` a small flash on the right frame edge and the rocket shrinking out. The halves'
   ink line about as thick as the stickers' own outline (2–3 px at 1080 wide), not a fat black ring around a small ball.
2. **Bomb (centre)**: `x+0` the bomb block exactly as before the tap (same smoke tone, same sticker, same size: the copy is invisible);
   `x+4/+7` swollen (~1.25), red-to-white blink, sparks spitting from the **fuse tip on the sticker** (upper right), not from the block's
   centre; `p+0` white core flash; `p+3/+7` the inked fireball about **three cells across** (the 3×3), the ring, eight **black shell
   pieces with orange seams** spinning outward (none pink, none white), grey puffs, sparks; the 3×3's blocks bursting centre-out; `p+12`
   the fireball breaking into grey smoke lobes, pieces shrinking.
3. **Chain**: the row's rockets; the bomb at (3, 4) winding up as the right rocket passes it (the rocket keeps flying); later the column
   rockets of (2, 5). The white screen flash is on the overlay HUD canvas and does not appear in a camera capture: `RoyalPlayTests` pins it,
   Task 12's recording shows it.
4. **Birth**: `g+0/+4` the group's blocks shrinking and turning toward their birth cells; `end-g+0…+10` the specials (golden at (6, 1),
   LineV at (4, 1)) popping bigger than their cells with a white flash, an eight-ray star and twinkles, then settling at their size and
   falling with their columns; "ALTIN TOP!" / "ÇİZGİ TOPU!" in gold (Pota's popups).
5. **Golden**: `f+0…+30` a big gold coin (block-sized, spinning) dropping through the ring of column 4 with a glitter trail, a gold flash
   and ray star at the ring, the second coin ~0.12 s behind, an arc toward the rim; `c+0…+12` ~10 small gold coins and stars thrown up from
   the rim and falling, the gold glow, the net swinging.
Any of these wrong — a mirrored or backward half, a flame in front of the nose, a pink or white mesh, a fireball much smaller or larger
than the 3×3, a visible jump at the bomb's swap, coins missing — is a defect: fix it in `RoyalSpecials`/`RoyalTuning` (numbers in the
tuning, with a ledger note for Task 13), rerun Steps 7–10. Keep the four clearest moments:
```bash
Tools/keep_screenshot.sh <the Line k+6 PNG> 2026-10-01-specials-royal-line
Tools/keep_screenshot.sh <the Bomb p+3 PNG> 2026-10-01-specials-royal-bomb
Tools/keep_screenshot.sh <the Birth end-g+3 PNG> 2026-10-01-specials-royal-birth
Tools/keep_screenshot.sh <the Golden c+6 PNG> 2026-10-01-specials-royal-golden
```

- [ ] **Step 11: Commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
P=Assets/_Project/Games/Pota
git add $P/Scripts/ColorHunt/Specials/RoyalSpecials.cs* $P/Scripts/ColorHunt/ColorHuntCamera.cs \
  $P/Scripts/ColorHunt/ColorHuntSettings.cs $P/Scripts/ColorHunt/ColorHuntGame.cs $P/Settings/ColorHuntSettings.asset \
  $P/Tests/EditMode/RoyalTests.cs* $P/Tests/EditMode/ColorHuntCameraTests.cs \
  $P/Tests/PlayMode/RoyalPlayTests.cs* \
  docs/screenshots/2026-10-01-specials-royal-line.jpg docs/screenshots/2026-10-01-specials-royal-bomb.jpg \
  docs/screenshots/2026-10-01-specials-royal-birth.jpg docs/screenshots/2026-10-01-specials-royal-golden.jpg
git status --short    # nothing else from this task should be left
git commit -m "feat: 1 · Royal special look — gathering births with a pop, fuse sparks and a breathing bomb, split-ball rockets with flame jets on the schedule's beat, bomb wind-up and inked fireball with shell pieces, combo slow motion and screen flash, golden coin flight and coin burst

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: 2 · Court — fast break, dunk, swish (`CourtSpecials`)

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/CourtSpecials.cs` (`CourtTuning` + `CourtSpecials`)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs` (`Court`), `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs`
  (`CreateSpecialStyle`), `Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset` (re-saved so it serializes `Court`)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/CourtTests.cs`, `Assets/_Project/Games/Pota/Tests/PlayMode/CourtPlayTests.cs`
- Output: `docs/screenshots/2026-10-01-specials-court-{dunk,line,combo,golden,birth}.jpg`

**Interfaces:**
- Consumes: Task 2 — `SpecialKind`, `Board.Size/Cells/Golden`, `ColorHuntRules.SingleArea/Blast/Row/Col/Index/FromPreset` (Task 6);
  Task 3 — `SpecialLook`, `ColorHuntSettings.SfxSlam/SfxSwish/SfxCrowd/SfxGoldenLand/SfxGoldenBorn`; Task 4 — `BlockView.Bob/
  StickerSpin/Squash/Flash/Pos/Scale/Exploding/Cell/Block`, `GateRings.Flash/Pool`; Task 5 — `ISpecialStyle`, `SpecialTiming`
  (`GatherTime`), `SpecialFire` (`Time`, `Pace`, `Order`, `At`), `TriggerPlan`, `SpecialSchedule.Plan/MaxStep`, `GoldenRoute`
  (`Scale`), `GoldenPath.At/PastGate/Landed`, `SpecialTexts.Born`, `PotaSpecials.BallGold/BallBase`, `ColorHuntGame.Specials/
  SetSpecialLook/Hoop/Gates/CellBurst/GoldenLanded/GoldenInFlight/Stage/Begin/ViewAt/CreateSpecialStyle/AddSpecialStyle`,
  `PotaSounds.PlayClip/Combo/Synth`, `GiantHoop.Swing/Swinging/Net`; Task 6 — `SpecialScenario`, `SpecialScenarios.Board`,
  `SpecialsDemo` (`Play`, `SetLook`, `ScenarioFinished`, `Game`), `Scenes/Specials.unity`, `SpecialPlayTests.Board7` (internal, PlayMode
  assembly); Task 9 — `ColorHuntSettings.CourtBall/CourtDust/SpeedLines/FlameJet/FloorCrack/LetterSlam/LetterSwish/LetterCombo/
  ToonTemplate`, `SpecialMeshes.Parts/Toon`; existing — `StudioParticles`, `StudioTextures.RayStar`, `ColorHuntFx.LitMaterial`,
  `BurstBudget`, `ColorHuntCamera.Push`, `CameraShake.Add`, `TimeScaleController.SlowMo`, `PopupText.Show`, `RuntimeMaterial.From`,
  `ColorHuntSettings.LabelTemplate/FlipbookTemplate/AdditiveTemplate/ParticleLitTemplate`.
- Produces: the **Contract additions** above.
- The spec's Court table, as built (Pace = the plan's compression factor, 1 unless a chain runs long):

  | Moment | Court | Clock |
  |---|---|---|
  | Birth | group blocks bounce-pass into the birth cell (arc, one bounce, shrink), arrival once each; the special drops 0.45 cell onto its cell, squash 0.6, thin white court-line ring 0.9 → 2.4 cells, light `CourtDust`; Pota's text and birth sound | `GatherTime` 0.22 (step waits for it) |
  | Idle | non-golden specials dribble 2 Hz, 0.06 cell, squash 0.5 at each bottom (`BlockView.Squash`); golden sticker spins 40°/s and glows (the game's pulse) | style clock |
  | Line | view consumed at the fire; stand-in ball does one hard dribble (0.08 s squash), then two Blender basketballs fly to both ends at 20 cells/s with a yellower `FlameJet` tail (loop 0.3 s) and `SpeedLines` stretched along the path; passed blocks hop 0.15 cell and burst as the ball arrives; white swish ring + `file-swish` at the frame; pass whoosh synth at launch | `LineLead` 0.08, `LineSpeed` 20 → burst of cell k at `f.Time + (0.08 + k/20)·Pace` |
  | Bomb (dunk) | view consumed at the fire; ball hops toward the camera 0.14 s (1 → 1.6), hangs 0.08 s, slams 0.07 s; impact: `FloorCrack` decal over the blocks (fades 0.6 s), `CourtDust` ring, 3×3 hop 0.25 cell and burst 0.03 s after the impact, "SLAM!" (pop 0 → 1.15 → 1, gone at 0.5 s), 6–10 flashbulbs on the frame within 0.4 s, shake 0.24 + `ColorHuntCamera.Push`, `smac-vurus` | `BombLead` 0.29 = impact, `BombRing` 0.03 |
  | 2+ specials | at the second fire: `SlowMo(0.5, 0.2)`, "COMBO!", 1.2 s of `kalabalik` | — |
  | Golden | gold-tinted basketball rolls (spinning) down to its gate (gate flash), arcs into the hoop on Pota's `GoldenPath`; landing: `GiantHoop.Swing()` + the six net rings flex in turn, "SWISH!" over the rim, 28 gold confetti, `file-swish` + `altin-inis` | `GoldenPath` 0.32 + 0.5 s |

- Pools (all built once in `Build`): balls 8 (lines 2 per fire, dunk 1), golden balls 4 (= `GateRings.Pool`), flame quads 8,
  speed-line quads 8, ring quads 12, crack quads 4, letter quads 4, dust particles 16, flashbulb particles 24, gold confetti 60 →
  `PoolTotal` 148 ≤ `Cap` 342 = `BurstBudget.Max` 1026 − Pota burst pools 684. `LiveCount` = live balls + golden flights + live quads +
  particle counts, so `game.Fx.LiveCount + Specials.LiveCount ≤ 1026` always.
- Rulings (ledger):
  1. **No `ISpecialStyle.Arm`.** Everything Court shows before an impact happens after the fire (the consumed view is replaced by
     the stand-in ball at that moment: dribble, then sweep; hop, hang, slam). Nothing has to start at the step's start.
  2. **No `BlockView.IdleScale`.** The squash at the bottom of each dribble is Pota's own landing squash `BlockView.Squash`
     (`1 − 0.12·squash`, decays 6/s), raised with `Max` like the game's own uses; a second scale hook would duplicate it.
  3. **Quads are `Project/PotaLabel`** (the block labels' shader, through `S.LabelTemplate`): straight alpha, ZWrite off, Cull off,
     per-renderer `_UvRect` (sheet frame) and `_Color` (linear tint × alpha) through a `MaterialPropertyBlock`, so flipbook frames and
     fades need no vertex colours and no new shader or template. Built-in `Quad.fbx` faces −z (the camera), like the labels.
  4. **Draw order.** Transparent renderers sort by `sortingOrder` before distance, and depth-test only against opaques. The court
     ring (order 1) sits just in front of the block bodies and under the labels (2/3); the crack (order 4, `DecalZ` = front + 0.3,
     in front of a 1.24× swollen pillow's 0.68) draws over bodies and labels; the trails (10/11) over Pota's bursts (5–9);
     the lettering (order 30) at `LetterZ` = front + 2.6 cells, in front of every block and of the dunk ball at its nearest
     (0.2 + 2.0 + 0.41·1.6 = 2.86 < 3.05), so it covers blocks and particles.
  5. **One clock with the plan.** `[DefaultExecutionOrder(100)]` runs `CourtSpecials.Update` after `ColorHuntGame.Update`, both step
     `min(Time.deltaTime, 0.05)`; `TriggerStep` zeroes the step clock, so at the end of every frame it equals the game's step time
     `st.T`. A fire called between two frames starts at `stepClock − f.Time` (≤ 0) and is advanced with the frame, so a ball is
     exactly where `LineDistance` says when the game's planned swell peaks (the game credits the same part-frame to its swells).
  6. **The golden flight keeps Pota's `GoldenPath` timing** (lands when Pota's would, `GoldenGrace` never trips); Court changes the
     ball and the landing. The gate flash stays Pota's (`GateRings.Flash`).
  7. **Births keep Pota's text and birth sounds** (spec §2 common rules); Court's own sounds are the four in the spec.
  8. **The camera kick is `ColorHuntCamera.Push`** (real time, like Shatter's push); the combo slow motion is `TimeScaleController.SlowMo`
     (real seconds). Under Pause + Step both run on wall time between steps (Task 12 must know).
  9. **The short crowd is cut at `Build`** from `S.SfxCrowd` (first 1.2 s, 0.35 s fade) because `PlayClip` cannot stop a voice; if
     the clip's data is unreadable the whole sample plays.
  10. **Pool exhaustion skips that fire's drawing** (the plan still bursts every cell); a golden flight without a free ball flies
      unseen and still calls back on time.

- [ ] **Step 1: Write the failing EditMode tests**

`Assets/_Project/Games/Pota/Tests/EditMode/CourtTests.cs`:
```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>2 · Court (spec 2026-10-01 §2 "Basketbol"): its numbers, its clock against the trigger plan, the dunk, the pools, its assets.</summary>
    public class CourtTests
    {
        private const string SettingsPath = "Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset";
        private const float Swell = 0.15f, Ms = 1e-3f;
        private static float Peak => Swell * PotaLook.SwellPeakAt;

        private GameObject _go;
        private ColorHuntSettings _fresh;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_fresh != null) Object.DestroyImmediate(_fresh);
        }

        /// <summary>7×7 colour (2r + c) mod 5 (no two neighbours alike), hp 5, all specials on (SpecialScheduleTests' board).</summary>
        private static Board Board7(params (int cell, SpecialKind kind)[] specials)
        {
            var cells = new Block[49];
            for (int i = 0; i < 49; i++) cells[i] = new Block((2 * (i / 7) + i % 7) % 5, 5);
            foreach (var (cell, kind) in specials) cells[cell] = Block.MakeSpecial(kind);
            return new Board
            {
                Size = 7, Cells = cells, Ammo = 158, Targets = new[] { new Target(0, 22), new Target(1, 16) },
                ColorCount = 5, HpMin = 3, HpMax = 14, Rng = 1, Features = Features.All,
            };
        }

        /// <summary>The trigger step the rules give for a tap on <paramref name="origin"/>.</summary>
        private static Step Blast(Board b, int origin)
        {
            var area = ColorHuntRules.Blast(b, origin);
            var step = new Step();
            step.Exploded.AddRange(area.Cells);
            step.Triggered.AddRange(area.Triggered);
            return step;
        }

        private static TriggerPlan Plan(Board b, int origin, CourtTuning t) =>
            SpecialSchedule.Plan(7, Blast(b, origin), origin, CourtSpecials.TimingFor(t), Swell);

        private static ColorHuntSettings Asset() => AssetDatabase.LoadAssetAtPath<ColorHuntSettings>(SettingsPath);

        [Test]
        public void Tuning_DefaultsAreTheSpecs()
        {
            var t = new CourtTuning();
            Assert.AreEqual(0.22f, t.GatherTime, "Doğuş: 0,22 sn");
            Assert.AreEqual(0.6f, t.BornSquash, "çarpma 0,6");
            Assert.AreEqual(2f, t.IdleHz, "2 Hz dripling");
            Assert.AreEqual(0.08f, t.DribbleTime, "Çizgi: 0,08 sn ezilme");
            Assert.AreEqual(0.3f, t.LineSweep, "iki uca 0,3 sn");
            Assert.AreEqual(6f, t.SweepCells, "a 7×7's far edge");
            Assert.AreEqual(0.14f, t.HopTime, "smaç: 0,14 sn zıplar");
            Assert.AreEqual(1.6f, t.HopScale, "1 → 1,6");
            Assert.AreEqual(0.08f, t.HangTime, "0,08 sn asılı");
            Assert.AreEqual(0.07f, t.SlamTime, "0,07 sn çakılır");
            Assert.AreEqual(0.03f, t.BombRing);
            Assert.AreEqual(0.6f, t.CrackFade, "çatlak 0,6 sn'de söner");
            Assert.AreEqual(0.25f, t.BlockHop, "3×3 0,25 hücre fırlar");
            Assert.AreEqual(0.5f, t.LetterTime, "SLAM! 0,5 sn");
            Assert.AreEqual(1.15f, t.LetterPeak);
            Assert.AreEqual(6, t.FlashbulbsMin, "6–10 fotoğraf flaşı");
            Assert.AreEqual(10, t.FlashbulbsMax);
            Assert.AreEqual(0.4f, t.FlashbulbWindow);
            Assert.AreEqual(0.24f, t.SlamShake, "sarsıntı 0,24");
            Assert.AreEqual(0.5f, t.ComboSlowScale, "aynı yavaş çekim: ×0,5");
            Assert.AreEqual(0.2f, t.ComboSlowTime, "0,2 sn");
            Assert.AreEqual(0.4f, t.DustTime, "vfx-dust-ring: 0.4 s");
            Assert.AreEqual(0.3f, t.FlameLoop, "vfx-flame-jet: one loop 0.3 s");
            Assert.AreEqual(0.25f, t.SpeedTime, "vfx-speed-lines: 0.25 s");
            _fresh = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsNotNull(_fresh.Court, "ColorHuntSettings.Court starts at the defaults");
            Assert.AreEqual(t.HopScale, _fresh.Court.HopScale);
            Assert.IsNotNull(Asset().Court, "the settings asset carries Court");
        }

        [Test]
        public void Timing_ComesFromTheTuning_AndALoneSpecialFitsTheStep()
        {
            var t = new CourtTuning();
            var timing = CourtSpecials.TimingFor(t);
            Assert.AreEqual(20f, timing.LineSpeed, 1e-4f, "6 cells in 0.3 s");
            Assert.AreEqual(0.08f, timing.LineLead, 1e-6f, "the hard dribble");
            Assert.AreEqual(0.29f, timing.BombLead, 1e-5f, "0.14 + 0.08 + 0.07: the impact");
            Assert.AreEqual(0.03f, timing.BombRing, 1e-6f);
            Assert.AreEqual(0.22f, timing.GatherTime, 1e-6f);
            var edge = Plan(Board7((21, SpecialKind.LineH)), 21, t);
            Assert.AreEqual(1f, edge.Pace, "a line from the edge fits uncompressed");
            Assert.AreEqual(0.08f + 0.3f + Swell, edge.End, 1e-4f);
            var bomb = Plan(Board7((24, SpecialKind.Bomb)), 24, t);
            Assert.AreEqual(1f, bomb.Pace);
            Assert.AreEqual(0.29f + 0.03f + Swell, bomb.End, 1e-4f, "ring 1 starts 0.32 s after the fire");
            Assert.LessOrEqual(bomb.End, SpecialSchedule.MaxStep);
        }

        [Test]
        public void LineBalls_ReachEveryCellOfAColumn_WhenTheScheduleBurstsIt()
        {
            var t = new CourtTuning();
            var plan = Plan(Board7((24, SpecialKind.LineV)), 24, t);
            var fire = plan.Fires[0];
            Assert.AreEqual(7, plan.Start.Count, "column 3");
            Assert.AreEqual(1f, plan.Pace);
            for (int r = 0; r < 7; r++)
            {
                int c = 3 + 7 * r, k = Mathf.Abs(r - 3);
                if (k == 0) continue;
                float burst = plan.Start[c] + Peak;
                Assert.AreEqual(burst, CourtSpecials.ReachTime(fire, t, k), Ms, $"({r}, 3): the ball arrives as the cell bursts");
                Assert.AreEqual(k, CourtSpecials.LineDistance(fire, t, burst - fire.Time), Ms * 20f, $"({r}, 3): where the ball is at the burst");
            }
            Assert.AreEqual(0f, CourtSpecials.LineDistance(fire, t, 0.08f), 1e-4f, "still dribbling");
        }

        [Test]
        public void LineAndDunk_FollowACompressedChain()
        {
            var t = new CourtTuning();
            var plan = Plan(Board7((21, SpecialKind.LineH), (27, SpecialKind.Bomb)), 21, t);
            Assert.Less(plan.Pace, 1f, "0.08 + 0.3 + 0.29 + 0.03 + 0.15 > 0.6: compressed");
            Assert.AreEqual(SpecialSchedule.MaxStep, plan.End, 1e-4f);
            var line = plan.Fires[0];
            var bomb = plan.Fires[1];
            Assert.AreEqual(27, bomb.Cell);
            Assert.AreEqual(plan.Pace, line.Pace, 1e-6f);
            Assert.AreEqual(plan.Pace, bomb.Pace, 1e-6f);
            for (int k = 1; k <= 5; k++)
                Assert.AreEqual(plan.Start[21 + k] + Peak, CourtSpecials.ReachTime(line, t, k), Ms, $"(3, {k}): ball and burst, Pace included");
            Assert.AreEqual(CourtSpecials.ReachTime(line, t, 6), bomb.Time, Ms, "the bomb fires the moment the ball reaches it");
            foreach (int c in new[] { 19, 20, 33, 34 })
                Assert.AreEqual(plan.Start[c] + Peak, CourtSpecials.BombBurstTime(bomb, t, 1), Ms, $"cell {c}: ring 1, right after the impact");
            Assert.AreEqual(bomb.Time + 0.29f * plan.Pace, CourtSpecials.ImpactTime(bomb, t), Ms);
        }

        [Test]
        public void Dunk_HopsHangsSlams_ImpactAtBombLead_PeakScale16()
        {
            var t = new CourtTuning();
            float lead = CourtSpecials.TimingFor(t).BombLead;
            var start = CourtSpecials.Dunk(0f, 1f, t);
            Assert.AreEqual(1f, start.Scale);
            Assert.AreEqual(0f, start.Toward);
            Assert.IsFalse(start.Impact);
            float most = 0f, nearest = 0f;
            for (int i = 0; i <= 290; i++)
            {
                var p = CourtSpecials.Dunk(i * 0.001f, 1f, t);
                most = Mathf.Max(most, p.Scale);
                nearest = Mathf.Max(nearest, p.Toward);
            }
            Assert.AreEqual(1.6f, most, 1e-4f, "1 → 1.6");
            Assert.AreEqual(t.HopToward, nearest, 1e-4f, "toward the camera");
            var top = CourtSpecials.Dunk(0.14f, 1f, t);
            Assert.AreEqual(1.6f, top.Scale, 1e-4f, "the hop ends at 1.6");
            var hang = CourtSpecials.Dunk(0.18f, 1f, t);
            Assert.AreEqual(1.6f, hang.Scale, 1e-4f);
            Assert.AreEqual(t.HopToward, hang.Toward, 1e-4f, "it hangs");
            Assert.IsFalse(CourtSpecials.Dunk(lead - 1e-3f, 1f, t).Impact, "still slamming");
            var hit = CourtSpecials.Dunk(lead, 1f, t);
            Assert.IsTrue(hit.Impact, "impact at BombLead");
            Assert.AreEqual(0f, hit.Toward, 1e-5f, "back on the board");
            Assert.AreEqual(0f, hit.Up, 1e-5f);
            Assert.Less(hit.Squash, 1f, "flattened on the floor");
            Assert.IsFalse(CourtSpecials.Dunk(lead * 0.5f - 1e-3f, 0.5f, t).Impact, "a compressed chain dunks at Pace");
            Assert.IsTrue(CourtSpecials.Dunk(lead * 0.5f, 0.5f, t).Impact);
            Assert.AreEqual(1.6f, CourtSpecials.Dunk(0.07f, 0.5f, t).Scale, 1e-4f);
        }

        [Test]
        public void Lettering_PopsTo115_AndIsGoneAtHalfASecond()
        {
            var t = new CourtTuning();
            Assert.AreEqual(0f, CourtSpecials.LetterScale(0f, t));
            Assert.AreEqual(1.15f, CourtSpecials.LetterScale(0.1f, t), 1e-4f);
            Assert.AreEqual(1f, CourtSpecials.LetterScale(0.3f, t), 1e-4f);
            float most = 0f;
            for (int i = 0; i <= 500; i++) most = Mathf.Max(most, CourtSpecials.LetterScale(i * 0.001f, t));
            Assert.AreEqual(1.15f, most, 1e-3f, "0 → 1.15 → 1");
            Assert.AreEqual(1f, CourtSpecials.LetterAlpha(0.3f, t), 1e-4f);
            Assert.AreEqual(0f, CourtSpecials.LetterAlpha(0.5f, t), 1e-4f);
        }

        [Test]
        public void NetWave_FlexesTheSixRingsInTurn()
        {
            var t = new CourtTuning();
            Assert.AreEqual(0.5f, CourtSpecials.NetWaveTime(t, 6), 1e-5f);
            float last = -1f;
            for (int k = 0; k < 6; k++)
            {
                Assert.AreEqual(1f, CourtSpecials.NetRing(0f, k, t), 1e-6f, $"ring {k} at rest first");
                float peakAt = k * t.NetRingGap + t.NetRingTime * 0.5f;
                Assert.AreEqual(1f + t.NetRingAmount, CourtSpecials.NetRing(peakAt, k, t), 1e-4f, $"ring {k}");
                Assert.Greater(peakAt, last, "top to bottom, in turn");
                last = peakAt;
                Assert.AreEqual(1f, CourtSpecials.NetRing(CourtSpecials.NetWaveTime(t, 6), k, t), 1e-6f, $"ring {k} settled");
            }
        }

        [Test]
        public void GatherArc_LeavesItsCell_BouncesOnce_LandsInTheBirthCell()
        {
            var t = new CourtTuning();
            var from = new Vector2(-2f, 0f);
            var to = new Vector2(1f, 0f);
            Assert.AreEqual(from, CourtSpecials.GatherPos(from, to, 0f, t));
            Assert.AreEqual(to, CourtSpecials.GatherPos(from, to, 1f, t));
            Assert.AreEqual(0f, CourtSpecials.GatherPos(from, to, t.GatherBounceAt, t).y, 1e-4f, "touches down once on the way");
            Assert.Greater(CourtSpecials.GatherPos(from, to, t.GatherBounceAt * 0.5f, t).y, 0.5f, "a real arc first");
            Assert.AreEqual(t.GatherBounce, CourtSpecials.GatherPos(from, to, (1f + t.GatherBounceAt) * 0.5f, t).y, 1e-4f, "a small second hop");
        }

        [Test]
        public void Pools_StayUnderWhatThePotaPoolsLeave()
        {
            Assert.AreEqual(BurstBudget.Max - BurstBudget.Pota, CourtSpecials.Cap);
            Assert.LessOrEqual(CourtSpecials.PoolTotal, CourtSpecials.Cap, "spec §1.6: ≤ 1.5 × Pota with Pota's own bursts");
            Assert.GreaterOrEqual(CourtSpecials.BallMax, 3 * 2 + 2, "three lines and two dunks in one chain");
            Assert.GreaterOrEqual(CourtSpecials.GoldenMax, GateRings.Pool, "a golden ball per gate");
            Assert.GreaterOrEqual(CourtSpecials.FlashMax, 2 * new CourtTuning().FlashbulbsMax, "two dunks' flashbulbs");
            Assert.GreaterOrEqual(CourtSpecials.LetterMax, 3, "SLAM!, COMBO! and SWISH! at once");
            Assert.GreaterOrEqual(CourtSpecials.FlameMax, CourtSpecials.BallMax, "a tail per ball");
            Assert.GreaterOrEqual(CourtSpecials.SpeedMax, CourtSpecials.BallMax, "speed lines per ball");
        }

        [Test]
        public void Frame_IsRowMajorFromTheTopLeft()
        {
            Assert.AreEqual(new Vector4(0f, 0.5f, 0.25f, 0.5f), CourtSpecials.Frame(0, 4, 2));
            Assert.AreEqual(new Vector4(0.75f, 0.5f, 0.25f, 0.5f), CourtSpecials.Frame(3, 4, 2));
            Assert.AreEqual(new Vector4(0f, 0f, 0.25f, 0.5f), CourtSpecials.Frame(4, 4, 2));
            Assert.AreEqual(new Vector4(0.75f, 0f, 0.25f, 0.5f), CourtSpecials.Frame(7, 4, 2));
        }

        [Test]
        public void RingTexture_IsAThinWhiteRing()
        {
            var tex = CourtSpecials.RingTexture(128);
            Assert.AreEqual(0f, tex.GetPixel(64, 64).a, 1e-3f, "open centre");
            Assert.Greater(tex.GetPixel(64 + 58, 64).a, 0.9f, "the line at r ≈ 0.91");
            Assert.AreEqual(1f, tex.GetPixel(64 + 58, 64).r, 1e-3f, "white");
            Assert.AreEqual(0f, tex.GetPixel(64 + 44, 64).a, 1e-3f, "thin: clear at r ≈ 0.70");
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 1e-3f);
        }

        [Test]
        public void Sounds_PassWhooshAndShortCrowd()
        {
            var pass = CourtSpecials.MakePass();
            try
            {
                Assert.AreEqual("court-pas", pass.name);
                Assert.AreEqual(Mathf.CeilToInt(0.24f * 44100), pass.samples);
                var data = new float[pass.samples];
                pass.GetData(data, 0);
                float most = 0f;
                foreach (var x in data) most = Mathf.Max(most, Mathf.Abs(x));
                Assert.That(most, Is.InRange(0.08f, 0.95f), "audible, unclipped");
                Assert.Less(Mathf.Abs(data[data.Length - 1]), 0.01f, "fades out");
            }
            finally { Object.DestroyImmediate(pass); }
            var crowd = Asset().SfxCrowd;
            Assert.IsNotNull(crowd, "Task 3's kalabalik");
            var cut = CourtSpecials.ShortCrowd(crowd, 1.2f, 0.35f);
            try
            {
                Assert.AreNotSame(crowd, cut, "a readable sample is cut (Decompress On Load, PCM)");
                Assert.AreEqual("kalabalik-kisa", cut.name);
                Assert.AreEqual(1.2f, cut.length, 0.01f, "kısa seyirci sesi");
                Assert.AreEqual(crowd.channels, cut.channels);
                var tail = new float[cut.channels];
                cut.GetData(tail, cut.samples - 1);
                foreach (var x in tail) Assert.Less(Mathf.Abs(x), 0.01f, "faded to silence");
            }
            finally { if (cut != crowd) Object.DestroyImmediate(cut); }
        }

        [Test]
        public void Ready_NeedsEveryCourtAsset()
        {
            Assert.IsFalse(CourtSpecials.AssetsReady(null));
            _fresh = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsFalse(CourtSpecials.AssetsReady(_fresh), "in-memory settings: no Blender hand-offs");
            Assert.IsTrue(CourtSpecials.AssetsReady(Asset()), "run ai_studio_import (Task 9)");
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = _fresh;
            var court = new GameObject("CourtSpecials").AddComponent<CourtSpecials>();
            court.transform.SetParent(_go.transform, false);
            court.Build(game);
            Assert.IsFalse(court.Ready);
            Assert.IsFalse(court.gameObject.activeSelf, "an unready look stays off");
        }

        [Test]
        public void SettingsAsset_ResolvesTheCourtStyle()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = Asset();
            game.SetSpecialLook(SpecialLook.Court);
            var court = game.Specials as CourtSpecials;
            Assert.IsNotNull(court, "no fallback: the Court assets are in place");
            Assert.IsTrue(court.Ready);
            Assert.IsTrue(court.gameObject.activeSelf);
            Assert.AreSame(game.transform, court.transform.parent, "on the game root (the hoop's space)");
            Assert.AreEqual(CourtSpecials.TimingFor(game.S.Court), court.Timing, "the plan reads the settings' tuning");
            Assert.IsTrue(court.GathersBirthGroup);
            Assert.IsTrue(court.ConsumesSpecialView);
            Assert.AreEqual(0, court.LiveCount);
            game.SetSpecialLook(SpecialLook.Pota);
            Assert.IsFalse(court.gameObject.activeSelf, "switched off with the look");
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
        }
    }
}
```

- [ ] **Step 2: Write the failing PlayMode tests**

`Assets/_Project/Games/Pota/Tests/PlayMode/CourtPlayTests.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>
    /// 2 · Court in the scenes as saved: every demo scenario plays to its end under the budget, a restart mid-dunk leaves nothing,
    /// the golden ball lands once and swings the net once, the lettering stays in the frame and is gone after 0.5 s, a line sweeps
    /// outward in order. Any logged error or exception fails these tests (Unity Test Framework).
    /// </summary>
    public class CourtPlayTests
    {
        private static float Dt => Mathf.Min(Time.deltaTime, 0.05f);        // ColorHuntGame's own step

        private static IEnumerator LoadCourt(Action<ColorHuntGame, CourtSpecials> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            game.SetLook(PuzzleLook.Pota);
            game.SetSpecialLook(SpecialLook.Court);
            var court = game.Specials as CourtSpecials;
            Assert.IsNotNull(court, "the Court look fell back to Pota: run ai_studio_import (Task 9)");
            ready(game, court);
        }

        private static void Deal(ColorHuntGame game, Board b)
        {
            game.Stage(b);
            game.Begin();
        }

        private static Board Preset(SpecialScenario s) => ColorHuntRules.FromPreset(SpecialScenarios.Board(s));

        private static void AssertUnderTheCap(ColorHuntGame game, string what)
        {
            Assert.LessOrEqual(game.Specials.LiveCount, CourtSpecials.Cap, what + ": the Court look's own pieces");
            Assert.LessOrEqual(game.Specials.LiveCount + game.Fx.LiveCount, BurstBudget.Max, what + ": with Pota's bursts");
        }

        [UnityTest]
        public IEnumerator EveryScenario_PlaysToTheEnd_UnderTheCap()
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "run ai_specials_build_scene (Task 6)");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            demo.SetLook(SpecialLook.Court);
            var game = demo.Game;
            Assert.IsInstanceOf<CourtSpecials>(game.Specials, "no fallback to Pota");
            foreach (SpecialScenario s in Enum.GetValues(typeof(SpecialScenario)))
            {
                bool done = false;
                void Done() => done = true;
                demo.ScenarioFinished += Done;
                demo.Play(s, true);
                int most = 0;
                float t = 0f;
                while (!done && t < 25f)
                {
                    AssertUnderTheCap(game, $"{s} at {t:0.00} s");
                    most = Mathf.Max(most, game.Specials.LiveCount);
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                demo.ScenarioFinished -= Done;
                Assert.IsTrue(done, $"{s} never finished");
                Assert.IsFalse(game.Busy, s.ToString());
                Assert.AreEqual(0, game.GoldenInFlight, s.ToString());
                Assert.AreEqual(s == SpecialScenario.Golden ? 2 : 0, game.GoldenLanded, $"{s}: golden balls in the hoop");
                for (int i = 0; i < game.Board.Cells.Length; i++)
                    Assert.AreEqual(game.Board.Cells[i].Special, game.ViewAt(i).Block.Special, $"{s}: cell {i}");
                if (s != SpecialScenario.Free) Assert.Greater(most, 0, $"{s}: the Court look drew");
                yield return StudioLookPlayTests.WaitUntil(() => game.Specials.LiveCount == 0, 4f);
                Assert.AreEqual(0, game.Specials.LiveCount, $"{s}: a Court effect never ended");
            }
        }

        [UnityTest]
        public IEnumerator Restart_MidDunk_LeavesNothing()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, Preset(SpecialScenario.Bomb));
            Assert.AreEqual("broken", game.Tap(24));
            yield return StudioLookPlayTests.WaitUntil(() => court.BallsLive > 0, 2f);
            Assert.AreEqual(1, court.BallsLive, "the dunk's ball is in the air");
            Assert.IsTrue(game.Busy);
            Deal(game, Preset(SpecialScenario.Bomb));                          // the demo's "Tekrar"
            Assert.AreEqual(0, court.LiveCount, "Clear dropped the ball, the quads and the particles");
            Assert.AreEqual(0, court.BallsLive);
            Assert.IsFalse(game.Busy, "the lock is released at once");
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);      // the old impact, SLAM! and flashbulbs would have come by now
            Assert.AreEqual(0, court.LiveCount, "nothing of the old dunk came back");
            Assert.AreEqual(0, court.LettersLive);
            for (int i = 0; i < game.Board.Cells.Length; i++)
                if (!game.Board.Cells[i].IsSpecial) Assert.AreEqual(0f, game.ViewAt(i).Bob, $"cell {i} kept a hop");
            Assert.AreEqual(SpecialKind.Bomb, game.Board.Cells[24].Special, "the old plan touched nothing");
            Assert.AreEqual("broken", game.Tap(24), "the new board plays");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.IsFalse(game.Busy);
        }

        [UnityTest]
        public IEnumerator Golden_LandsOnce_AndSwingsTheNetOnce()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, SpecialPlayTests.Board7(new[] { 3 }, (38, SpecialKind.Golden)));
            Assert.AreEqual("broken", game.Tap(45), "the block under the golden ball");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 4f);
            Assert.AreEqual(1, game.GoldenInFlight);
            Assert.AreEqual(1, court.BallsLive, "the Court look draws the golden ball");
            Assert.AreEqual(0, court.Landings);
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight == 0, 4f);
            Assert.AreEqual(1, game.GoldenLanded);
            Assert.AreEqual(1, court.Landings);
            Assert.AreEqual(1, court.NetSwings);
            Assert.IsNotNull(game.Hoop);
            Assert.IsTrue(game.Hoop.Swinging, "fileSalla");
            Assert.AreEqual(1, court.LettersLive, "SWISH!");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);
            Assert.AreEqual(1, game.GoldenLanded, "nothing landed twice");
            Assert.AreEqual(1, court.Landings);
            Assert.AreEqual(1, court.NetSwings, "the net swung once");
            Assert.AreEqual(1, game.Board.Golden);
            foreach (Transform ring in game.Hoop.Net) Assert.AreEqual(Vector3.one, ring.localScale, $"{ring.name}: the net wave settled");
        }

        [UnityTest]
        public IEnumerator Slam_LetteringIsInTheFrame_AndGoneAfterHalfASecond()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, Preset(SpecialScenario.Bomb));
            Assert.AreEqual("broken", game.Tap(24));
            yield return StudioLookPlayTests.WaitUntil(() => court.LettersLive > 0, 3f);
            Assert.AreEqual(1, court.LettersLive, "SLAM!");
            float age = 0f;
            bool looked = false;
            while (court.LettersLive > 0 && age < 1f)
            {
                AssertUnderTheCap(game, $"SLAM! at {age:0.00} s");
                if (!looked && age >= 0.1f)                                      // the 1.15 pop: the biggest it gets
                {
                    looked = true;
                    foreach (var q in court.Letters)
                        foreach (var corner in new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) })
                        {
                            var v = game.Cam.WorldToViewportPoint(q.TransformPoint(corner));
                            Assert.Greater(v.z, 0f, "in front of the camera");
                            Assert.That(v.x, Is.InRange(0f, 1f), $"{q.name}: not cut at the side");
                            Assert.That(v.y, Is.InRange(0f, 1f), $"{q.name}: not cut at the top or bottom");
                        }
                }
                age += Dt;
                yield return null;
            }
            Assert.IsTrue(looked, "the lettering lived long enough to be measured");
            Assert.AreEqual(0, court.LettersLive);
            Assert.That(age, Is.InRange(0.45f, 0.58f), "the lettering lives 0.5 s of game time");
        }

        [UnityTest]
        public IEnumerator Line_TwoBallsSweepTheRow_AndItBurstsOutwardInOrder()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, Preset(SpecialScenario.Line));
            var at = new Dictionary<int, float>();
            float clock = 0f;
            void Burst(int c) { if (!at.ContainsKey(c)) at[c] = clock; }
            game.CellBurst += Burst;
            Assert.AreEqual("broken", game.Tap(22), "LineH at (3, 1)");
            int balls = 0;
            while (game.Busy && clock < 6f)
            {
                AssertUnderTheCap(game, $"line at {clock:0.00} s");
                balls = Mathf.Max(balls, court.BallsLive);
                clock += Dt;
                yield return null;
            }
            game.CellBurst -= Burst;
            Assert.AreEqual(2, balls, "two balls, one to each end");
            for (int c = 21; c <= 27; c++) Assert.IsTrue(at.ContainsKey(c), $"cell {c} burst");
            Assert.Less(at[23], at[25], "outward: (3, 2) before (3, 4)");
            Assert.Less(at[25], at[27], "(3, 4) before (3, 6)");
            Assert.AreEqual(at[21], at[23], 0.06f, "one cell away on both sides: together");
            Assert.LessOrEqual(at[22], at[21], "the line ball goes first");
        }
    }
}
```

- [ ] **Step 3: Run to see them fail**

Run: `cd "/Users/tunaaltin17/projects/Unity MCP" && ~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CourtTests`
Expected: exit 2, `error CS0246: The type or namespace name 'CourtTuning' could not be found`, `error CS0103: The name
'CourtSpecials' does not exist in the current context` and `error CS1061: 'ColorHuntSettings' does not contain a definition for 'Court'`.

- [ ] **Step 4: Settings field and the style's registration**

`Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntSettings.cs`, right after Task 9's `public GameObject CourtBall;` add:
```csharp

        [Header("Court special look (spec 2026-10-01 §2 · 2 Basketbol): game seconds, board cells")]
        [Tooltip("Every number of the Court look; the timing fields also drive the trigger plan (CourtSpecials.TimingFor).")]
        public CourtTuning Court = new CourtTuning();
```

`Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs`, Task 5's `CreateSpecialStyle` becomes:
```csharp
        private ISpecialStyle CreateSpecialStyle(SpecialLook look)
        {
            switch (look)
            {
                case SpecialLook.Pota: return AddSpecialStyle<PotaSpecials>("PotaSpecials");
                case SpecialLook.Royal: return AddSpecialStyle<RoyalSpecials>("RoyalSpecials");
                case SpecialLook.Court: return AddSpecialStyle<CourtSpecials>("CourtSpecials");
                default: return new NoSpecialStyle();
            }
        }
```
(Task 10 runs first and added the Royal case; this block keeps it. `SpecialStyleTests.UnwrittenLook_FallsBackToPota` keeps passing: with in-memory settings `CourtSpecials` is not ready, so the
game still warns "Court special look is missing assets" and plays Pota's.)

- [ ] **Step 5: `CourtSpecials`**

Create `Assets/_Project/Games/Pota/Scripts/ColorHunt/Specials/CourtSpecials.cs` (the `Specials/` folder exists since Task 4):
```csharp
using System;
using System.Collections.Generic;
using Project.Flow;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// Every number of the Court special look (spec 2026-10-01 §2 "2 · Basketbol"), in ColorHuntSettings.Court so it can be tuned in the
    /// Inspector. Times are game seconds; lengths are board cells (the grid scale applies) unless they say metres. The timing fields
    /// feed the trigger plan through <see cref="CourtSpecials.TimingFor"/>, so the balls, the dunk and the bursts share one clock.
    /// </summary>
    [Serializable]
    public class CourtTuning
    {
        [Header("Birth: the group bounces into the birth cell, the special lands")]
        [Tooltip("Doğuş: the group's blocks bounce into the birth cell in this time (SpecialTiming.GatherTime).")]
        public float GatherTime = 0.22f;
        [Tooltip("First hop of the bounce pass (cells; + 0.1 per cell travelled), the second hop, where the bounce is (0–1), shrink on arrival.")]
        public float GatherHop = 0.5f, GatherBounce = 0.18f, GatherBounceAt = 0.62f, GatherShrink = 0.45f;
        [Tooltip("The new special drops from LandDrop cells onto its cell in LandTime, then squashes (Pota çarpma 0.6) and flashes.")]
        public float LandDrop = 0.45f, LandTime = 0.1f, BornSquash = 0.6f, BornFlash = 0.7f;
        [Tooltip("White court-line ring under the landing: from → to (cells) in time.")]
        public float CourtRingFrom = 0.9f, CourtRingTo = 2.4f, CourtRingTime = 0.35f;
        [Tooltip("Light dust at the landing (cells); the dust flipbook's life (vfx-dust-ring: 0.4 s).")]
        public float BirthDust = 1.6f, DustTime = 0.4f;

        [Header("Idle: dribbling specials, a spinning golden ball")]
        [Tooltip("Bounces per second (dripling 2 Hz).")]
        public float IdleHz = 2f;
        [Tooltip("Hop height (cells) and the squash at the bottom (BlockView.Squash: 1 − 0.12·squash).")]
        public float IdleHop = 0.06f, IdleSquash = 0.5f;
        [Tooltip("Golden ball's sticker spin (degrees per second); it glows with the game's golden pulse.")]
        public float GoldenSpin = 40f;

        [Header("Line: a hard dribble, two balls on fire to both ends")]
        [Tooltip("The hard dribble before the balls leave (SpecialTiming.LineLead) and its squash.")]
        public float DribbleTime = 0.08f;
        [Tooltip("How flat the dribble squashes the stand-in ball (0–1).")]
        public float DribbleSquash = 0.4f;
        [Tooltip("A ball reaches the far edge of a 7×7 (6 cells) in LineSweep: LineSpeed = SweepCells / LineSweep (cells per second).")]
        public float LineSweep = 0.3f, SweepCells = 6f;
        [Tooltip("Ball sizes (cells): the stand-in on the cell, the flying balls; their spin (degrees per second).")]
        public float StandInCells = 0.82f, BallCells = 0.72f, BallSpin = 720f;
        [Tooltip("The frame edge lies this far past the last cell's edge (cells); a ball leaves over ExitCells, shrinking.")]
        public float EdgeMargin = 0.15f, ExitCells = 0.7f;
        [Tooltip("A passed block hops LineHop cells over LineHopTime, starting when the ball is KickLead cells away (it bursts on arrival).")]
        public float KickLead = 0.6f, LineHop = 0.15f, LineHopTime = 0.12f;
        [Tooltip("The flame tail (vfx-flame-jet): square quad size (cells), one loop (s).")]
        public float FlameCells = 1.8f, FlameLoop = 0.3f;
        [Tooltip("Flame tint (linear multiply): a little more yellow than the sheet.")]
        public Color FlameTint = new Color(1f, 0.92f, 0.55f, 1f);
        [Tooltip("Speed lines (vfx-speed-lines, one-shot): longest stretch along the path, height (cells), play time (s).")]
        public float SpeedLength = 2.6f, SpeedHeight = 0.9f, SpeedTime = 0.25f;
        [Tooltip("White swish ring where a ball meets the frame: from → to (cells) in time; file-swish volume.")]
        public float SwishRingFrom = 0.5f, SwishRingTo = 1.6f, SwishRingTime = 0.25f, SwishVolume = 0.7f;
        [Tooltip("The pass whoosh (synth court-pas) volume.")]
        public float PassVolume = 0.8f;

        [Header("Bomb: the dunk")]
        [Tooltip("Hop toward the camera (s).")]
        public float HopTime = 0.14f;
        [Tooltip("At the top of the hop: this scale, this high and this far toward the camera (cells).")]
        public float HopScale = 1.6f, HopUp = 0.35f, HopToward = 2f;
        [Tooltip("Hang in the air (s), slam down (s) to SlamScale; impact = Hop + Hang + Slam = SpecialTiming.BombLead.")]
        public float HangTime = 0.08f, SlamTime = 0.07f, SlamScale = 1.25f;
        [Tooltip("The ball lies flat on the floor for ImpactHold s, then is gone.")]
        public float ImpactHold = 0.05f, ImpactSquash = 0.55f;
        [Tooltip("Seconds between the impact and each further ring of the 3×3 (SpecialTiming.BombRing).")]
        public float BombRing = 0.03f;
        [Tooltip("Floor crack decal (cells) fading out in CrackFade s; the dust ring (cells).")]
        public float CrackCells = 2.6f, CrackFade = 0.6f, DustCells = 3.4f;
        [Tooltip("The 3×3 blocks hop this high (cells) over this time, bursting on the way up.")]
        public float BlockHop = 0.25f, BlockHopTime = 0.1f;
        [Tooltip("Lettering (SLAM!, COMBO!, SWISH!): life, pop 0 → peak → 1, widths in cells (the stills are 2:1).")]
        public float LetterTime = 0.5f, LetterPeak = 1.15f, SlamLetterCells = 4f, ComboLetterCells = 4.4f, SwishLetterCells = 3.6f;
        [Tooltip("Flashbulbs on the frame edges: how many (min–max).")]
        public int FlashbulbsMin = 6, FlashbulbsMax = 10;
        [Tooltip("Flashbulbs: within this window after the impact (s), size (cells), life (s).")]
        public float FlashbulbWindow = 0.4f, FlashbulbCells = 1.1f, FlashbulbLife = 0.14f;
        [Tooltip("Impact shake (additive) and the camera kick toward the impact (fraction of the way, real seconds).")]
        public float SlamShake = 0.24f, KickAmount = 0.08f, KickTime = 0.22f;

        [Header("2+ specials in one step")]
        [Tooltip("Slow motion (scale, real seconds); the short crowd: the first CrowdCut s of kalabalik, faded over CrowdFade s.")]
        public float ComboSlowScale = 0.5f;
        public float ComboSlowTime = 0.2f, CrowdCut = 1.2f, CrowdFade = 0.35f, CrowdVolume = 0.8f;

        [Header("Golden ball: rolls to the gate, swish into the hoop")]
        [Tooltip("Ball size (cells), roll spin (rad/s), SWISH! height above the rim (metres).")]
        public float GoldenBallCells = 0.7f;
        public float GoldenRoll = 12f, SwishLift = 1.4f;
        [Tooltip("Gold confetti pieces per landing.")]
        public int ConfettiCount = 28;
        [Tooltip("The six net rings flex in turn: amount, each ring's time (s), the gap between rings (s).")]
        public float NetRingAmount = 0.2f, NetRingTime = 0.25f, NetRingGap = 0.05f;
    }

    /// <summary>
    /// 2 · Court (spec 2026-10-01 §2 "Basketbol"): Pota's own world — speed lines, the court, the dunk, the net. Birth: the group's blocks
    /// bounce into the birth cell like bounce passes and the special lands with a squash, a white court-line ring and light dust. Idle:
    /// specials dribble at 2 Hz (a squash at the bottom), the golden ball spins its sticker and glows (the game's pulse). Line: one hard
    /// dribble, then two basketballs on fire (Blender ball, flame-jet tail, speed lines) fly to both ends; passed blocks hop and burst in
    /// order (the trigger plan's times, Pace included); a white swish ring where they meet the frame. Bomb = a dunk: the ball hops toward
    /// the camera (1 → 1.6), hangs, slams; the impact (at BombLead) cracks the floor, throws a dust ring, hops the 3×3 that then bursts,
    /// "SLAM!", flashbulbs on the frame, shake and a camera kick. 2+ specials in a step: slow motion, "COMBO!", a short crowd. Golden: the
    /// gold ball rolls down through its gate and arcs into the hoop; the net swings ring by ring, "SWISH!", gold confetti. Everything is
    /// pooled and on game time, in the game root's space (this object sits at its origin); materials come from the settings' templates.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after ColorHuntGame: the step clock equals the game's step time at the end of every frame
    public class CourtSpecials : MonoBehaviour, ISpecialStyle
    {
        public const int BallMax = 8, GoldenMax = 4, FlameMax = 8, SpeedMax = 8, RingMax = 12, CrackMax = 4, LetterMax = 4,
            DustMax = 16, FlashMax = 24, ConfettiMax = 60;
        /// <summary>Every pooled piece and particle this look can have alive at once.</summary>
        public const int PoolTotal = BallMax + GoldenMax + FlameMax + SpeedMax + RingMax + CrackMax + LetterMax + DustMax + FlashMax + ConfettiMax;
        /// <summary>Spec §1.6: this look's own live count stays under what the Pota burst pools (ColorHuntFx) leave of the budget.</summary>
        public const int Cap = BurstBudget.Max - BurstBudget.Pota;

        // Depths in Pota board cells toward the camera (block fronts 0.45, pillow bulge + 0.1, labels 0.58, a 1.24× swell's bulge 0.68).
        public const float StandInZ = 0.2f, BallZ = PotaLook.FrontZ + 0.55f, RingZ = PotaLook.FrontZ + 0.15f, DecalZ = PotaLook.FrontZ + 0.3f,
            FlashZ = PotaLook.FrontZ + 0.5f, LetterZ = PotaLook.FrontZ + 2.6f;
        // Transparent sorting: the court ring under the block labels (2/3), the crack over them, the trails over Pota's bursts (5–9),
        // the lettering over everything.
        public const int RingOrder = 1, CrackOrder = 4, DustOrder = 6, SpeedOrder = 10, FlameOrder = 11, SwishOrder = 12, FlashOrder = 13,
            LetterOrder = 30;
        /// <summary>Toon outline of the balls in world metres (Project/Toon extrudes along the world normal).</summary>
        public const float Outline = 0.02f;

        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Vector4 Whole = new Vector4(0f, 0f, 1f, 1f);
        private static readonly CourtTuning Defaults = new CourtTuning();
        private static readonly Color[] Gold = { PotaLook.Hex(0xffc93c), PotaLook.Hex(0xffe27a), PotaLook.Hex(0xeeb900), PotaLook.Hex(0xfff1b0) };
        private static Texture2D _ringTex;

        private enum Kind { Ring, Crack, Letter, Flame, Speed }
        private enum Mode { Line, Dunk }

        private sealed class Quad
        {
            public Transform T;
            public MeshRenderer R;
            public Kind Kind;
            public bool Live;
            public float Age, Life, From, To, Aspect = 1f, Turn;
            public Vector3 At;
        }

        private sealed class Ball
        {
            public Transform T;
            public bool Live;
            public Mode Mode;
            public float Age;
            public SpecialFire Fire;
            public Vector2 Dir;
            public float Edge;
            public int[] Cells;
            public int NextKick;
            public bool Lead, Whooshed, Edged, Impacted, SpeedDone;
            public Quad Flame, Speed;
        }

        private sealed class Flight
        {
            public GoldenRoute Route;
            public Action PassedGate, Landed;
            public Transform Ball;
            public float Age;
            public bool Passed;
        }

        private sealed class Hop { public BlockView V; public float Age, Height, Time; }
        private sealed class Gathering { public BlockView V; public Vector2 From, To; public float Age, Time; public bool Bounced; public Action<BlockView> Arrived; }
        private sealed class Flashbulb { public float Wait; public Vector2 At; }

        private ColorHuntGame _game;
        private bool _built, _crowdMade;
        private Mesh _ballMesh, _quadMesh;
        private float _ballSize = 0.45f;
        private Vector3 _ballCentre;
        private Material _ballMat, _goldMat, _ringMat, _crackMat, _slamMat, _swishMat, _comboMat, _flameMat, _speedMat;
        private readonly List<Material> _materials = new List<Material>();
        private MaterialPropertyBlock _mpb;
        private ParticleSystem _dust, _flash, _confetti;
        private AudioClip _pass, _crowd;
        private readonly Stack<Ball> _freeBalls = new Stack<Ball>();
        private readonly Stack<Transform> _freeGolden = new Stack<Transform>();
        private readonly Dictionary<Kind, Stack<Quad>> _freeQuads = new Dictionary<Kind, Stack<Quad>>();
        private readonly List<Ball> _balls = new List<Ball>();
        private readonly List<Quad> _quads = new List<Quad>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Hop> _hops = new List<Hop>();
        private readonly List<Gathering> _gathers = new List<Gathering>();
        private readonly List<Flashbulb> _bulbs = new List<Flashbulb>();
        private readonly Dictionary<BlockView, float> _landing = new Dictionary<BlockView, float>();
        private readonly List<BlockView> _keys = new List<BlockView>();
        private readonly System.Random _rng = new System.Random(23);
        private float _clock, _stepClock, _netT = -1f;
        private bool _stepActive, _comboArmed;
        private Vector2 _comboCentre;
        private GiantHoop _hoop;
        private int _gen;

        public bool Ready { get; private set; }
        public int LiveCount => _balls.Count + _flights.Count + _quads.Count + StudioParticles.Count(_dust, _flash, _confetti);
        public SpecialTiming Timing => TimingFor(Tuning);
        public bool GathersBirthGroup => true;
        public bool ConsumesSpecialView => true;
        /// <summary>The settings' tuning (ColorHuntSettings.Court), or the defaults.</summary>
        public CourtTuning Tuning => _game != null && _game.S != null && _game.S.Court != null ? _game.S.Court : Defaults;
        /// <summary>Basketballs on screen: line balls, dunk balls, golden flights.</summary>
        public int BallsLive => _balls.Count + _flights.Count;
        public int LettersLive
        {
            get
            {
                int n = 0;
                foreach (var q in _quads) if (q.Kind == Kind.Letter) n++;
                return n;
            }
        }
        /// <summary>The live lettering quads (SLAM!, COMBO!, SWISH!): unit quads scaled in the game root.</summary>
        public IEnumerable<Transform> Letters { get { foreach (var q in _quads) if (q.Kind == Kind.Letter) yield return q.T; } }
        /// <summary>GiantHoop.Swing calls made by golden landings.</summary>
        public int NetSwings { get; private set; }
        public int Landings { get; private set; }

        // ---------- pure timing and shapes (CourtTests) ----------

        public static bool AssetsReady(ColorHuntSettings s) =>
            s != null && s.CourtBall != null && s.CourtDust != null && s.SpeedLines != null && s.FlameJet != null && s.FloorCrack != null
            && s.LetterSlam != null && s.LetterSwish != null && s.LetterCombo != null && SpecialMeshes.Parts(s.CourtBall).Count > 0;

        /// <summary>The trigger plan's timing for this look: the balls, the dunk and SpecialSchedule share it.</summary>
        public static SpecialTiming TimingFor(CourtTuning t) => new SpecialTiming
        {
            LineSpeed = t.SweepCells / Mathf.Max(1e-3f, t.LineSweep),
            LineLead = t.DribbleTime,
            BombLead = t.HopTime + t.HangTime + t.SlamTime,
            BombRing = t.BombRing,
            GatherTime = t.GatherTime,
        };

        private static float PaceOf(SpecialFire f) => f.Pace > 0f ? f.Pace : 1f;

        /// <summary>Cells a line ball has covered <paramref name="tau"/> s after its fire (0 while it dribbles): SpecialSchedule's Delay inverted.</summary>
        public static float LineDistance(SpecialFire f, CourtTuning t, float tau)
        {
            var timing = TimingFor(t);
            return Mathf.Max(0f, (tau / PaceOf(f) - timing.LineLead) * timing.LineSpeed);
        }

        /// <summary>Seconds into the step when a line ball reaches the cell <paramref name="cells"/> away: that cell's burst in the plan.</summary>
        public static float ReachTime(SpecialFire f, CourtTuning t, float cells)
        {
            var timing = TimingFor(t);
            return f.Time + (timing.LineLead + cells / timing.LineSpeed) * PaceOf(f);
        }

        /// <summary>Seconds into the step when the dunk hits the floor.</summary>
        public static float ImpactTime(SpecialFire f, CourtTuning t) => f.Time + TimingFor(t).BombLead * PaceOf(f);

        /// <summary>Seconds into the step when ring <paramref name="ring"/> (Chebyshev distance) of the bomb's 3×3 bursts.</summary>
        public static float BombBurstTime(SpecialFire f, CourtTuning t, int ring) =>
            f.Time + (TimingFor(t).BombLead + ring * t.BombRing) * PaceOf(f);

        public struct DunkPose
        {
            /// <summary>Cells up from the bomb's cell; cells toward the camera.</summary>
            public float Up, Toward;
            /// <summary>Size × the stand-in's; Squash = height ÷ width (1 = round); Spin in degrees.</summary>
            public float Scale, Squash, Spin;
            public bool Impact;
        }

        /// <summary>The dunk <paramref name="tau"/> s after the bomb's fire: hop (ease-out) to 1.6 toward the camera, hang, slam (ease-in), flat at BombLead·pace.</summary>
        public static DunkPose Dunk(float tau, float pace, CourtTuning t)
        {
            pace = pace > 0f ? pace : 1f;
            float lead = TimingFor(t).BombLead;
            float impact = lead * pace, hop = t.HopTime * pace, hung = (t.HopTime + t.HangTime) * pace;
            var p = new DunkPose { Scale = 1f, Squash = 1f };
            if (tau <= 0f) return p;
            if (tau >= impact)
            {
                p.Scale = t.SlamScale;
                p.Squash = t.ImpactSquash;
                p.Spin = 720f;
                p.Impact = true;
                return p;
            }
            if (tau < hop)
            {
                float u = tau / hop, e = 1f - (1f - u) * (1f - u);
                p.Up = t.HopUp * e;
                p.Toward = t.HopToward * e;
                p.Scale = 1f + (t.HopScale - 1f) * e;
                p.Spin = 360f * e;
            }
            else if (tau < hung)
            {
                float u = (tau - hop) / Mathf.Max(1e-5f, hung - hop);
                p.Up = t.HopUp + 0.04f * Mathf.Sin(Mathf.PI * u);
                p.Toward = t.HopToward;
                p.Scale = t.HopScale;
                p.Spin = 360f + 60f * u;                                                // turns slowly while it hangs
            }
            else
            {
                float u = (tau - hung) / Mathf.Max(1e-5f, impact - hung), e = u * u;
                p.Up = t.HopUp * (1f - e);
                p.Toward = t.HopToward * (1f - e);
                p.Scale = Mathf.Lerp(t.HopScale, t.SlamScale, e);
                p.Spin = 420f + 300f * u;
            }
            return p;
        }

        /// <summary>Lettering size: 0 → peak in 0.1 s (ease-out), back to 1 by 0.2 s.</summary>
        public static float LetterScale(float age, CourtTuning t)
        {
            if (age <= 0f) return 0f;
            if (age < 0.1f)
            {
                float u = age / 0.1f;
                return t.LetterPeak * (1f - (1f - u) * (1f - u));
            }
            if (age < 0.2f) return Mathf.Lerp(t.LetterPeak, 1f, (age - 0.1f) / 0.1f);
            return 1f;
        }

        /// <summary>Lettering opacity: full for 75 % of its life, then out by LetterTime.</summary>
        public static float LetterAlpha(float age, CourtTuning t) => Mathf.Clamp01((t.LetterTime - age) / (0.25f * Mathf.Max(1e-3f, t.LetterTime)));

        /// <summary>Net ring <paramref name="ring"/> (0 = under the rim) widens 1 + amount·sin(πu) over its time, NetRingGap after the one above.</summary>
        public static float NetRing(float time, int ring, CourtTuning t)
        {
            float u = (time - ring * t.NetRingGap) / Mathf.Max(1e-3f, t.NetRingTime);
            return u <= 0f || u >= 1f ? 1f : 1f + t.NetRingAmount * Mathf.Sin(Mathf.PI * u);
        }

        public static float NetWaveTime(CourtTuning t, int rings) => Mathf.Max(0, rings - 1) * t.NetRingGap + t.NetRingTime;

        /// <summary>A bounce pass in the board plane: straight across, two parabolic hops in +y (the bounce at GatherBounceAt).</summary>
        public static Vector2 GatherPos(Vector2 from, Vector2 to, float u, CourtTuning t)
        {
            u = Mathf.Clamp01(u);
            var p = Vector2.LerpUnclamped(from, to, u);
            float at = Mathf.Clamp(t.GatherBounceAt, 0.05f, 0.95f), d = Vector2.Distance(from, to), h;
            if (u < at)
            {
                float a = u / at;
                h = (t.GatherHop + 0.1f * d) * 4f * a * (1f - a);
            }
            else
            {
                float b = (u - at) / (1f - at);
                h = t.GatherBounce * 4f * b * (1f - b);
            }
            return new Vector2(p.x, p.y + h);
        }

        /// <summary>The dribble: |sin| hops, IdleHz bounces a second, 0 at each bottom.</summary>
        public static float IdleHop(float time, CourtTuning t) => t.IdleHop * Mathf.Sin(Mathf.PI * Frac(time * t.IdleHz));

        /// <summary>UV rect (u, v, w, h; v from the bottom) of frame <paramref name="f"/> of a cols × rows sheet, row by row from the top-left.</summary>
        public static Vector4 Frame(int f, int cols, int rows)
        {
            int c = f % cols, r = f / cols;
            return new Vector4(c / (float)cols, 1f - (r + 1) / (float)rows, 1f / cols, 1f / rows);
        }

        /// <summary>A thin white court-line ring (radius 0.86–0.97 of the half size, anti-aliased): the births' ring and the swish ring.</summary>
        public static Texture2D RingTexture(int size = 128)
        {
            if (_ringTex != null && _ringTex.width == size) return _ringTex;
            var px = new Color32[size * size];
            float edge = 2f / size;                                                     // one pixel in the −1…1 square
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.86f) / edge + 0.5f) * Mathf.Clamp01((0.97f - r) / edge + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            _ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "CourtLineRing", wrapMode = TextureWrapMode.Clamp };
            _ringTex.SetPixels32(px);
            _ringTex.Apply(false, false);
            return _ringTex;
        }

        /// <summary>The pass "vıııt" (synth court-pas, 0.24 s): white noise through a one-pole band sweeping up and back, under a 320 → 960 Hz glide.</summary>
        public static AudioClip MakePass()
        {
            uint seed = 0x9E3779B9u;
            float low = 0f, lower = 0f;
            return PotaSounds.Synth("court-pas", 0.24f, t =>
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;                // xorshift: the same whoosh every time
                float noise = (seed & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
                float sweep = t < 0.12f ? t / 0.12f : 1f - (t - 0.12f) / 0.12f;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * (500f + 3500f * Mathf.Clamp01(sweep)) / 44100f);
                low += a * (noise - low);
                lower += a * 0.35f * (low - lower);
                float env = Mathf.Min(1f, t / 0.03f) * Mathf.Clamp01((0.24f - t) / 0.12f);
                float glide = Mathf.Sin(2f * Mathf.PI * (320f * t + 0.5f * (960f - 320f) / 0.24f * t * t));
                return env * (0.55f * (low - lower) + 0.12f * glide);
            });
        }

        /// <summary>"kalabalik-kisa": the first <paramref name="seconds"/> of the crowd sample, faded out over <paramref name="fade"/> s; the whole clip if its data cannot be read.</summary>
        public static AudioClip ShortCrowd(AudioClip crowd, float seconds, float fade)
        {
            if (crowd == null) return null;
            if (crowd.loadState != AudioDataLoadState.Loaded) crowd.LoadAudioData();
            int ch = Mathf.Max(1, crowd.channels), rate = crowd.frequency;
            int n = Mathf.Min(crowd.samples, Mathf.CeilToInt(seconds * rate));
            var data = new float[Mathf.Max(0, n) * ch];
            if (n <= 0 || !crowd.GetData(data, 0)) return crowd;
            int fadeN = Mathf.Clamp(Mathf.RoundToInt(fade * rate), 1, n);
            for (int i = n - fadeN; i < n; i++)
            {
                float g = (n - 1 - i) / (float)fadeN;
                for (int c = 0; c < ch; c++) data[i * ch + c] *= g;
            }
            var clip = AudioClip.Create("kalabalik-kisa", n, ch, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        // ---------- ISpecialStyle ----------

        public void Build(ColorHuntGame game)
        {
            _game = game;
            var s = game != null ? game.S : null;
            Ready = AssetsReady(s);
            if (!Ready) { gameObject.SetActive(false); return; }
            if (!_built)
            {
                _built = true;
                var part = SpecialMeshes.Parts(s.CourtBall)[0];
                _ballMesh = part.Mesh;
                _ballSize = Mathf.Max(1e-3f, _ballMesh.bounds.size.x);
                _ballCentre = _ballMesh.bounds.center;
                _quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");             // faces −z, the camera, like the block labels
                _ballMat = Keep(SpecialMeshes.Toon(s, s.CourtBall, "CourtBall", Outline));
                _goldMat = Keep(SpecialMeshes.Toon(s, s.CourtBall, "CourtGoldBall", Outline));
                var gold = PotaLook.Hex(PotaSpecials.BallGold).linear;
                var basis = PotaLook.Hex(PotaSpecials.BallBase).linear;                 // the skin's #f07d1d becomes #ffc93c
                _goldMat.SetColor("_BaseColor", new Color(gold.r / basis.r, gold.g / basis.g, gold.b / basis.b, 1f).gamma);   // SetColor linearises
                _ringMat = QuadMaterial(s, RingTexture(), "CourtLineRing");
                _crackMat = QuadMaterial(s, s.FloorCrack, "CourtCrack");
                _slamMat = QuadMaterial(s, s.LetterSlam, "CourtSlam");
                _swishMat = QuadMaterial(s, s.LetterSwish, "CourtSwish");
                _comboMat = QuadMaterial(s, s.LetterCombo, "CourtCombo");
                _flameMat = QuadMaterial(s, s.FlameJet, "CourtFlame");
                _speedMat = QuadMaterial(s, s.SpeedLines, "CourtSpeedLines");
                _mpb = new MaterialPropertyBlock();
                for (int i = 0; i < BallMax; i++) _freeBalls.Push(new Ball { T = NewBall("CourtBall" + i, _ballMat) });
                for (int i = 0; i < GoldenMax; i++) _freeGolden.Push(NewBall("CourtGoldBall" + i, _goldMat));
                Pool(Kind.Ring, RingMax);
                Pool(Kind.Crack, CrackMax);
                Pool(Kind.Letter, LetterMax);
                Pool(Kind.Flame, FlameMax);
                Pool(Kind.Speed, SpeedMax);
                _dust = StudioParticles.System(transform, "CourtDust", Keep(StudioParticles.Flipbook(s, s.CourtDust)), ParticleSystemRenderMode.Billboard, DustMax, DustOrder);
                StudioParticles.Sheet(_dust, 4, 4);
                _flash = StudioParticles.System(transform, "CourtFlashbulbs", Keep(StudioParticles.Additive(s, StudioTextures.RayStar(), 2f)),
                    ParticleSystemRenderMode.Billboard, FlashMax, FlashOrder);
                StudioParticles.SizeOverLife(_flash, u => u < 0.25f ? Mathf.Lerp(0.35f, 1f, u / 0.25f) : Mathf.Lerp(1f, 0.6f, (u - 0.25f) / 0.75f));
                StudioParticles.AlphaOverLife(_flash, u => 1f - u);
                _confetti = StudioParticles.System(transform, "CourtGoldConfetti", Keep(ColorHuntFx.LitMaterial(s.ParticleLitTemplate, 0.6f, 0.4f, 1f)),
                    ParticleSystemRenderMode.Mesh, ConfettiMax, 7, true);
                _confetti.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                StudioParticles.Gravity(_confetti, 6f);
                _pass = MakePass();
                _crowd = ShortCrowd(s.SfxCrowd, Tuning.CrowdCut, Tuning.CrowdFade);
                _crowdMade = _crowd != null && _crowd != s.SfxCrowd;
            }
            Activate(false);
        }

        public void Activate(bool on)
        {
            if (!on) Clear();
            gameObject.SetActive(on);
        }

        public void Clear()
        {
            _gen++;
            foreach (var q in _quads.ToArray()) RecycleQuad(q);
            foreach (var b in _balls)
            {
                b.Flame = null;
                b.Speed = null;
                HideBall(b);
            }
            _balls.Clear();
            foreach (var f in _flights) HideGolden(f);
            _flights.Clear();
            foreach (var h in _hops) if (h.V != null && h.V.Exploding) h.V.Bob = 0f;
            _hops.Clear();
            foreach (var kv in _landing) if (kv.Key != null) kv.Key.Bob = 0f;
            _landing.Clear();
            _gathers.Clear();                                                           // the game settles its gathered blocks itself
            _bulbs.Clear();
            foreach (var ps in new[] { _dust, _flash, _confetti }) if (ps != null) ps.Clear();
            ResetNet();
            ResetIdle();
            _stepActive = false;
            _comboArmed = false;
        }

        /// <summary>Non-golden specials dribble (|sin| hop, a squash at each bottom); the golden ball spins its sticker (it glows with the game's pulse).</summary>
        public void Idle(BlockView v, float dt)
        {
            if (!Ready || v == null || _landing.ContainsKey(v)) return;              // a landing special is the landing's
            var t = Tuning;
            if (v.Block.Special == SpecialKind.Golden)
            {
                v.Bob = 0f;
                v.StickerSpin = -Mathf.Repeat(_clock * t.GoldenSpin, 360f);
                return;
            }
            float time = _clock + 0.07f * (v.Cell % 4);                                 // a little out of step with each other
            v.Bob = IdleHop(time, t);
            float phase = Frac(time * t.IdleHz);
            if (phase < 0.15f) v.Squash = Mathf.Max(v.Squash, t.IdleSquash * (1f - phase / 0.15f));
            v.StickerSpin = 0f;
        }

        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived)
        {
            if (v == null) return;
            if (!Ready || !isActiveAndEnabled) { v.Pos = to; arrived?.Invoke(v); return; }
            _gathers.Add(new Gathering { V = v, From = v.Pos, To = to, Time = Mathf.Max(0.01f, duration), Arrived = arrived });
        }

        /// <summary>The special drops onto its cell (then squash, court ring, dust), with Pota's birth text and sound.</summary>
        public void Birth(BlockView v, SpecialKind kind)
        {
            if (_game == null || v == null) return;
            var t = Tuning;
            v.Flash = Mathf.Max(v.Flash, t.BornFlash);
            if (Ready && isActiveAndEnabled)
            {
                _landing[v] = 0f;
                v.Bob = t.LandDrop;
            }
            else v.Squash = Mathf.Max(v.Squash, t.BornSquash);
            var sounds = Sounds();
            if (sounds != null)
            {
                if (kind == SpecialKind.Golden) sounds.PlayClip("ozelDogdu", _game.S.SfxGoldenBorn);
                else sounds.Combo(2);
            }
            var popups = _game.Juice != null ? _game.Juice.Popups : null;
            if (popups != null)
                popups.Show(_game.BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.6f, -PotaLook.LabelZ)), SpecialTexts.Born(kind),
                    PotaLook.HudGold, 72, false, 1.2f);
        }

        /// <summary>The step starts: the step clock restarts with the game's (st.T = 0 here); 2+ specials arm the combo.</summary>
        public void TriggerStep(int specials, Vector2 centre)
        {
            _stepClock = 0f;
            _stepActive = true;
            _comboArmed = specials >= 2;
            _comboCentre = centre;
        }

        /// <summary>
        /// A special fires (its view is already hidden: ConsumesSpecialView). It started <c>stepClock − f.Time</c> s ago (≤ 0: planned
        /// between two frames); this frame's Update adds the frame, so its age equals the game's step time minus the planned fire.
        /// </summary>
        public void Fire(SpecialFire f)
        {
            if (!Ready || _game == null || _game.BoardRoot == null) return;
            float age = _stepActive ? Mathf.Clamp(_stepClock - f.Time, -0.05f, 0.5f) : 0f;
            if (f.Order == 1 && _comboArmed)
            {
                _comboArmed = false;
                Combo();
            }
            if (f.Kind == SpecialKind.Bomb) FireDunk(f, age);
            else if (f.Kind == SpecialKind.LineV || f.Kind == SpecialKind.LineH) FireLine(f, age);
        }

        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
        {
            _flights.Add(new Flight
            {
                Route = route, PassedGate = passedGate, Landed = landed, Ball = _freeGolden.Count > 0 ? _freeGolden.Pop() : null,
            });
        }

        // ---------- frame ----------

        private void Update()
        {
            if (!Ready || _game == null || _game.BoardRoot == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);                                // ColorHuntGame's own step
            var t = Tuning;
            _clock += dt;
            _stepClock += dt;
            int gen = _gen;
            TickGathers(dt, t);
            if (gen != _gen) return;
            TickLandings(dt, t);
            TickHops(dt);
            TickBalls(dt, t);
            TickFlights(dt, t);
            if (gen != _gen) return;
            TickQuads(dt, t);
            TickBulbs(dt, t);
            TickNet(dt, t);
        }

        private void TickGathers(float dt, CourtTuning t)
        {
            int gen = _gen;
            for (int i = _gathers.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _gathers.Count) return;
                var g = _gathers[i];
                g.Age += dt;
                float u = Mathf.Clamp01(g.Age / g.Time);
                bool alive = g.V != null && g.V.gameObject.activeSelf;
                if (alive)
                {
                    g.V.Pos = GatherPos(g.From, g.To, u, t);
                    g.V.Scale = 1f - t.GatherShrink * u * u;
                    if (!g.Bounced && u >= t.GatherBounceAt)
                    {
                        g.Bounced = true;
                        g.V.Squash = Mathf.Max(g.V.Squash, 1f);
                    }
                }
                if (u < 1f && alive) continue;
                _gathers.RemoveAt(i);
                Call(() => g.Arrived?.Invoke(g.V));                                     // exactly once (the game ignores a view it let go)
            }
        }

        private void TickLandings(float dt, CourtTuning t)
        {
            if (_landing.Count == 0) return;
            _keys.Clear();
            _keys.AddRange(_landing.Keys);
            foreach (var v in _keys)
            {
                if (v == null || !v.gameObject.activeSelf || !v.Block.IsSpecial) { _landing.Remove(v); continue; }
                float age = _landing[v] + dt;
                if (age < t.LandTime)
                {
                    _landing[v] = age;
                    float u = age / Mathf.Max(1e-3f, t.LandTime);
                    v.Bob = t.LandDrop * (1f - u * u);
                    continue;
                }
                _landing.Remove(v);
                v.Bob = 0f;
                v.Squash = Mathf.Max(v.Squash, t.BornSquash);
                Ring(v.Pos, t.CourtRingFrom, t.CourtRingTo, t.CourtRingTime, RingOrder, RingZ);
                StudioParticles.Emit(_dust, World(v.Pos, DecalZ), t.BirthDust * BoardScale, t.DustTime, new Color(1f, 1f, 1f, 0.8f), Vector3.zero, R() * 360f);
            }
        }

        private void TickHops(float dt)
        {
            for (int i = _hops.Count - 1; i >= 0; i--)
            {
                var h = _hops[i];
                if (h.V == null || !h.V.Exploding || !h.V.gameObject.activeSelf) { _hops.RemoveAt(i); continue; }   // recycled: NewView resets Bob
                h.Age += dt;
                if (h.Age >= h.Time) { h.V.Bob = 0f; _hops.RemoveAt(i); continue; }
                h.V.Bob = h.Height * Mathf.Sin(Mathf.PI * h.Age / h.Time);
            }
        }

        private void TickBalls(float dt, CourtTuning t)
        {
            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                var b = _balls[i];
                b.Age += dt;
                bool alive = b.Mode == Mode.Line ? StepLine(b, t) : StepDunk(b, t);
                if (alive) continue;
                _balls.RemoveAt(i);
                RecycleQuad(b.Flame);
                RecycleQuad(b.Speed);
                b.Flame = null;
                b.Speed = null;
                HideBall(b);
            }
        }

        private void FireLine(SpecialFire f, float age)
        {
            var t = Tuning;
            int n = BoardSize, row = ColorHuntRules.Row(n, f.Cell), col = ColorHuntRules.Col(n, f.Cell);
            bool column = f.Kind == SpecialKind.LineV;                                  // Pota cizgiDikey: the column
            for (int side = 0; side < 2; side++)
            {
                if (_freeBalls.Count == 0) return;                                      // ruling 10: the plan still bursts the cells
                var dir = column ? new Vector2(0f, side == 0 ? 1f : -1f) : new Vector2(side == 0 ? 1f : -1f, 0f);
                int cells = column ? (side == 0 ? row : n - 1 - row) : (side == 0 ? n - 1 - col : col);
                var b = TakeBall();
                b.Mode = Mode.Line;
                b.Fire = f;
                b.Age = age;
                b.Dir = dir;
                b.Edge = cells + 0.5f + t.EdgeMargin;
                b.Cells = LineCells(n, row, col, dir, cells);
                b.NextKick = 0;
                b.Lead = side == 0;                                                     // the first ball also plays the dribble
                b.Whooshed = side == 1;                                                 // one whoosh per fire
                b.Edged = false;
                b.Impacted = false;
                b.SpeedDone = false;
                b.Flame = null;
                b.Speed = null;
            }
        }

        /// <summary>The cells a ball passes, nearest first (row 0 is the top: +y is row − k).</summary>
        private static int[] LineCells(int n, int row, int col, Vector2 dir, int count)
        {
            var cells = new int[count];
            for (int k = 1; k <= count; k++)
                cells[k - 1] = ColorHuntRules.Index(n, row - Mathf.RoundToInt(dir.y) * k, col + Mathf.RoundToInt(dir.x) * k);
            return cells;
        }

        private bool StepLine(Ball b, CourtTuning t)
        {
            var f = b.Fire;
            float pace = PaceOf(f), lead = t.DribbleTime * pace;
            if (b.Age < lead)
            {
                bool show = b.Lead && b.Age >= 0f;                                     // the hard dribble on the special's cell
                b.T.gameObject.SetActive(show);
                if (show)
                {
                    float s = Mathf.Sin(Mathf.PI * Mathf.Clamp01(b.Age / Mathf.Max(1e-4f, lead)));
                    PlaceBall(b.T, f.At + new Vector2(0f, -0.12f * s), StandInZ, t.StandInCells, 1f + 0.5f * t.DribbleSquash * s,
                        1f - t.DribbleSquash * s, Quaternion.identity);
                }
                return true;
            }
            if (!b.Whooshed)
            {
                b.Whooshed = true;
                Sounds()?.PlayClip("courtPas", _pass, t.PassVolume);
            }
            float d = LineDistance(f, t, b.Age);
            while (b.NextKick < b.Cells.Length && d >= b.NextKick + 1 - t.KickLead) Kick(b.Cells[b.NextKick++], t.LineHop, t.LineHopTime);
            if (!b.Edged && d >= b.Edge)
            {
                b.Edged = true;
                Ring(f.At + b.Dir * b.Edge, t.SwishRingFrom, t.SwishRingTo, t.SwishRingTime, SwishOrder, DecalZ);
                Sounds()?.PlayClip("swish", _game.S.SfxSwish, t.SwishVolume);
            }
            if (d >= b.Edge + t.ExitCells) return false;
            float shrink = d > b.Edge ? 1f - (d - b.Edge) / Mathf.Max(1e-3f, t.ExitCells) : 1f;
            float flown = b.Age - lead, rise = Mathf.Clamp01(d / 0.6f);
            var at = f.At + b.Dir * d;
            float z = Mathf.Lerp(StandInZ, BallZ, rise);
            b.T.gameObject.SetActive(true);
            PlaceBall(b.T, at, z, Mathf.Lerp(t.StandInCells, t.BallCells, rise) * shrink, 1f, 1f,
                Quaternion.AngleAxis(flown * t.BallSpin, new Vector3(-b.Dir.y, b.Dir.x, 0f)));
            float bs = BoardScale, angle = Mathf.Atan2(b.Dir.y, b.Dir.x) * Mathf.Rad2Deg;
            // The flame tail: the sheet's nozzle (frame 0.5, 0.9) sits on the ball, its flame (frame −y) streams back along −dir.
            if (b.Flame == null) b.Flame = TakeQuad(Kind.Flame, _flameMat, FlameOrder);
            if (b.Flame != null)
            {
                float size = t.FlameCells * shrink;
                b.Flame.Age = flown;
                b.Flame.T.localPosition = Local(at - b.Dir * (0.4f * size), z - 0.05f);
                b.Flame.T.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                b.Flame.T.localScale = new Vector3(size * bs, size * bs, 1f);
                int frame = Mathf.FloorToInt(Frac(flown / Mathf.Max(1e-3f, t.FlameLoop)) * 8f) % 8;
                Draw(b.Flame, Frame(frame, 4, 2), t.FlameTint, 1f);
            }
            // The speed lines: the sheet's ball (u 0.95) on the ball, the streaks behind it, stretched over the path so far.
            if (b.Speed == null && !b.SpeedDone) b.Speed = TakeQuad(Kind.Speed, _speedMat, SpeedOrder);
            if (b.Speed != null)
            {
                if (flown >= t.SpeedTime)
                {
                    RecycleQuad(b.Speed);
                    b.Speed = null;
                    b.SpeedDone = true;
                }
                else
                {
                    float w = Mathf.Clamp(d + 0.6f, 1.2f, t.SpeedLength), h = t.SpeedHeight;
                    b.Speed.Age = flown;
                    b.Speed.T.localPosition = Local(at - b.Dir * (0.45f * w), z - 0.1f);
                    b.Speed.T.localRotation = Quaternion.Euler(0f, 0f, angle);
                    b.Speed.T.localScale = new Vector3(w * bs, h * bs, 1f);
                    int frame = Mathf.Min(7, Mathf.FloorToInt(flown / Mathf.Max(1e-3f, t.SpeedTime) * 8f));
                    Draw(b.Speed, Frame(frame, 4, 2), Color.white, 1f);
                }
            }
            return true;
        }

        private void FireDunk(SpecialFire f, float age)
        {
            if (_freeBalls.Count == 0) return;                                          // ruling 10
            var b = TakeBall();
            b.Mode = Mode.Dunk;
            b.Fire = f;
            b.Age = age;
            b.Impacted = false;
            b.Flame = null;
            b.Speed = null;
        }

        private bool StepDunk(Ball b, CourtTuning t)
        {
            var f = b.Fire;
            float pace = PaceOf(f);
            if (b.Age < 0f) { b.T.gameObject.SetActive(false); return true; }
            var pose = Dunk(b.Age, pace, t);
            if (pose.Impact)
            {
                if (!b.Impacted)
                {
                    b.Impacted = true;
                    Impact(f, t);
                }
                if (b.Age - TimingFor(t).BombLead * pace >= t.ImpactHold) return false;
            }
            b.T.gameObject.SetActive(true);
            var rot = pose.Impact ? Quaternion.identity : Quaternion.Euler(pose.Spin * 0.35f, pose.Spin, 0f);
            float wide = pose.Impact ? 1f + 0.6f * (1f - pose.Squash) : 1f;
            PlaceBall(b.T, f.At + new Vector2(0f, pose.Up - (pose.Impact ? 0.15f : 0f)), StandInZ + pose.Toward, t.StandInCells * pose.Scale,
                wide, pose.Squash, rot);
            return true;
        }

        /// <summary>The dunk hits the floor: crack, dust ring, the 3×3 hops (they burst BombRing·Pace later), SLAM!, flashbulbs, shake, kick, smac.</summary>
        private void Impact(SpecialFire f, CourtTuning t)
        {
            int n = BoardSize;
            float bs = BoardScale;
            var crack = TakeQuad(Kind.Crack, _crackMat, CrackOrder);
            if (crack != null)
            {
                crack.At = Local(f.At, DecalZ);
                crack.From = t.CrackCells * bs;
                crack.Life = t.CrackFade;
                crack.Turn = R() * 360f;
            }
            StudioParticles.Emit(_dust, World(f.At, DecalZ), t.DustCells * bs, t.DustTime, Color.white, Vector3.zero, R() * 360f);
            foreach (int c in ColorHuntRules.SingleArea(n, f.Cell, SpecialKind.Bomb)) if (c != f.Cell) Kick(c, t.BlockHop, t.BlockHopTime);
            Letter(_slamMat, f.At + new Vector2(0f, 1.1f), t.SlamLetterCells);
            int bulbs = t.FlashbulbsMin + _rng.Next(Mathf.Max(0, t.FlashbulbsMax - t.FlashbulbsMin) + 1);
            for (int i = 0; i < bulbs; i++) _bulbs.Add(new Flashbulb { Wait = R() * t.FlashbulbWindow, At = EdgePoint(n) });
            _game.Juice?.Shake?.Add(t.SlamShake);
            var cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            if (cam != null) cam.Push(World(f.At, PotaLook.FrontZ), t.KickAmount, t.KickTime);
            Sounds()?.PlayClip("smac", _game.S.SfxSlam);
        }

        /// <summary>2+ specials in one step, at the second fire: slow motion, COMBO! under the step's centre, a short crowd.</summary>
        private void Combo()
        {
            var t = Tuning;
            _game.Juice?.TimeCtl?.SlowMo(t.ComboSlowScale, t.ComboSlowTime);
            Letter(_comboMat, _comboCentre + new Vector2(0f, -1.4f), t.ComboLetterCells);
            Sounds()?.PlayClip("kalabalik", _crowd, t.CrowdVolume);
        }

        private void TickFlights(float dt, CourtTuning t)
        {
            int gen = _gen;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _flights.Count) return;
                var f = _flights[i];
                f.Age += dt;
                float tt = f.Age - f.Route.Delay;
                if (tt < 0f) continue;                                                  // the step's next ball leaves 0.12 s later
                float s = f.Route.Scale > 0f ? f.Route.Scale : 1f;
                if (f.Ball != null)
                {
                    f.Ball.gameObject.SetActive(true);
                    f.Ball.localPosition = GoldenPath.At(f.Route, tt) + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * s);
                    float roll = tt * t.GoldenRoll * Mathf.Rad2Deg;                     // rolls down the column, keeps spinning in the arc
                    f.Ball.localRotation = Quaternion.Euler(roll, 0f, roll * 0.3f);
                    f.Ball.localScale = Vector3.one * (t.GoldenBallCells * s / _ballSize);
                }
                if (!f.Passed && GoldenPath.PastGate(tt))
                {
                    f.Passed = true;
                    var gates = _game.Gates;
                    if (gates != null) gates.Flash(f.Route.Col);
                    Call(f.PassedGate);
                    if (gen != _gen) return;
                }
                if (!GoldenPath.Landed(tt)) continue;
                _flights.RemoveAt(i);
                HideGolden(f);
                Land(f.Route, t);
                Call(f.Landed);
                if (gen != _gen) return;
            }
        }

        /// <summary>Swish: the net swings and its six rings flex in turn, SWISH! over the rim, gold confetti, file-swish + altin-inis.</summary>
        private void Land(GoldenRoute r, CourtTuning t)
        {
            Landings++;
            float s = r.Scale > 0f ? r.Scale : 1f;
            var hoop = _game.Hoop;
            if (hoop != null)
            {
                hoop.Swing();
                NetSwings++;
                if (_hoop != null && _hoop != hoop) ResetNet();
                _hoop = hoop;
                _netT = 0f;
            }
            ShowLetter(_swishMat, r.Hoop + new Vector3(0f, t.SwishLift, -1f), t.SwishLetterCells * s);
            var at = transform.TransformPoint(r.Hoop + new Vector3(0f, 0.2f, 0f));
            for (int i = 0; i < t.ConfettiCount; i++)
            {
                float a = R() * Mathf.PI * 2f;
                var c = Gold[i % Gold.Length].linear;
                var v = new Vector3(Mathf.Cos(a) * (0.8f + 1.6f * R()), 3.2f + 2.4f * R(), -(0.4f + 1.2f * R()));
                StudioParticles.Emit3D(_confetti, at, transform.TransformVector(v), new Vector3(0.2f, 0.13f, 0.03f),
                    new Vector3(R(), R(), R()) * 360f, new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 900f, 1.4f + 0.6f * R(), c);
            }
            var sounds = Sounds();
            if (sounds == null) return;
            sounds.PlayClip("swish", _game.S.SfxSwish);
            sounds.PlayClip("altinInis", _game.S.SfxGoldenLand);
        }

        private void TickQuads(float dt, CourtTuning t)
        {
            for (int i = _quads.Count - 1; i >= 0; i--)
            {
                if (i >= _quads.Count) continue;
                var q = _quads[i];
                if (q.Kind == Kind.Flame || q.Kind == Kind.Speed) continue;            // their ball places them
                q.Age += dt;
                if (q.Age >= q.Life) { RecycleQuad(q); continue; }
                float u = q.Age / q.Life, size, alpha;
                switch (q.Kind)
                {
                    case Kind.Ring:
                        size = Mathf.Lerp(q.From, q.To, 1f - (1f - u) * (1f - u));
                        alpha = 0.95f * Mathf.Pow(1f - u, 1.5f);
                        break;
                    case Kind.Crack:
                        size = q.From * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(q.Age / 0.05f));
                        float fade = Mathf.Clamp01((q.Age - 0.1f) / Mathf.Max(1e-3f, q.Life - 0.1f));
                        alpha = (1f - fade) * (1f - fade);
                        break;
                    default:                                                            // lettering, 2:1
                        size = q.From * LetterScale(q.Age, t);
                        alpha = LetterAlpha(q.Age, t);
                        break;
                }
                q.T.localPosition = q.At;
                q.T.localRotation = Quaternion.Euler(0f, 0f, q.Turn);
                q.T.localScale = new Vector3(Mathf.Max(1e-4f, size), Mathf.Max(1e-4f, size / q.Aspect), 1f);
                Draw(q, Whole, Color.white, alpha);
            }
        }

        private void TickBulbs(float dt, CourtTuning t)
        {
            for (int i = _bulbs.Count - 1; i >= 0; i--)
            {
                var b = _bulbs[i];
                b.Wait -= dt;
                if (b.Wait > 0f) continue;
                _bulbs.RemoveAt(i);
                StudioParticles.Emit(_flash, World(b.At, FlashZ), t.FlashbulbCells * BoardScale, t.FlashbulbLife, new Color(1f, 0.97f, 0.9f, 1f),
                    Vector3.zero, R() * 45f);
            }
        }

        private void TickNet(float dt, CourtTuning t)
        {
            if (_netT < 0f) return;
            if (_hoop == null || _hoop.Net == null) { _netT = -1f; return; }
            _netT += dt;
            var net = _hoop.Net;
            for (int k = 0; k < net.childCount; k++)
            {
                float r = NetRing(_netT, k, t);
                net.GetChild(k).localScale = new Vector3(r, 1f, r);                    // the rings lie in xz under the rim
            }
            if (_netT >= NetWaveTime(t, net.childCount)) ResetNet();
        }

        // ---------- pieces ----------

        private void Kick(int cell, float height, float time)
        {
            var v = _game.ViewAt(cell);
            if (v == null || !v.Exploding || !v.gameObject.activeSelf) return;         // only blocks this step bursts (never a golden ball)
            _hops.Add(new Hop { V = v, Height = height, Time = Mathf.Max(0.01f, time) });
        }

        private void Ring(Vector2 cell, float from, float to, float time, int order, float z)
        {
            var q = TakeQuad(Kind.Ring, _ringMat, order);
            if (q == null) return;
            float bs = BoardScale;
            q.At = Local(cell, z);
            q.From = from * bs;
            q.To = to * bs;
            q.Life = time;
        }

        /// <summary>A 2:1 lettering quad <paramref name="widthCells"/> wide near <paramref name="at"/>, kept inside the board so it is never cut.</summary>
        private void Letter(Material mat, Vector2 at, float widthCells)
        {
            int n = BoardSize;
            float w = widthCells, h = widthCells * 0.5f;
            float mx = Mathf.Max(0f, n * 0.5f - 0.4f - 0.6f * w), my = Mathf.Max(0f, n * 0.5f - 0.4f - 0.6f * h);   // 0.6: the 1.15 pop + perspective
            ShowLetter(mat, Local(new Vector2(Mathf.Clamp(at.x, -mx, mx), Mathf.Clamp(at.y, -my, my)), LetterZ), w * BoardScale);
        }

        private void ShowLetter(Material mat, Vector3 local, float width)
        {
            var q = TakeQuad(Kind.Letter, mat, LetterOrder);
            if (q == null) return;
            q.At = local;
            q.From = width;
            q.Aspect = 2f;
            q.Life = Tuning.LetterTime;
        }

        /// <summary>A random point on the frame (board units): half the backboard plus half the frame bar, over the grid scale.</summary>
        private Vector2 EdgePoint(int n)
        {
            float half = (PotaLook.PanoSize + PotaLook.FrameWidth) * 0.5f / PotaLook.GridScale(n);
            float u = (R() * 2f - 1f) * half;
            switch (_rng.Next(4))
            {
                case 0: return new Vector2(u, half);
                case 1: return new Vector2(u, -half);
                case 2: return new Vector2(-half, u);
                default: return new Vector2(half, u);
            }
        }

        private void PlaceBall(Transform ball, Vector2 cell, float potaZ, float cells, float wide, float tall, Quaternion rot)
        {
            float k = cells * BoardScale / _ballSize;
            ball.localPosition = Local(cell, potaZ);
            ball.localRotation = rot;
            ball.localScale = new Vector3(wide * k, tall * k, wide * k);
        }

        private void Draw(Quad q, Vector4 uv, Color tint, float alpha)
        {
            _mpb.Clear();
            _mpb.SetVector(UvRectId, uv);
            _mpb.SetVector(ColorId, new Vector4(tint.r, tint.g, tint.b, tint.a * alpha));   // PotaLabel: linear tint × alpha
            q.R.SetPropertyBlock(_mpb);
        }

        private Ball TakeBall()
        {
            var b = _freeBalls.Pop();
            b.Live = true;
            _balls.Add(b);
            return b;
        }

        private void HideBall(Ball b)
        {
            if (!b.Live) return;
            b.Live = false;
            b.T.gameObject.SetActive(false);
            _freeBalls.Push(b);
        }

        private void HideGolden(Flight f)
        {
            if (f.Ball == null) return;
            f.Ball.gameObject.SetActive(false);
            _freeGolden.Push(f.Ball);
            f.Ball = null;
        }

        private Quad TakeQuad(Kind kind, Material mat, int order)
        {
            var free = _freeQuads[kind];
            if (free.Count == 0) return null;
            var q = free.Pop();
            q.Live = true;
            q.Age = 0f;
            q.Life = 1f;
            q.From = q.To = 1f;
            q.Aspect = 1f;
            q.Turn = 0f;
            q.R.sharedMaterial = mat;
            q.R.sortingOrder = order;
            q.T.localRotation = Quaternion.identity;
            q.T.gameObject.SetActive(true);
            _quads.Add(q);
            return q;
        }

        private void RecycleQuad(Quad q)
        {
            if (q == null || !q.Live) return;
            q.Live = false;
            q.T.gameObject.SetActive(false);
            _quads.Remove(q);
            _freeQuads[q.Kind].Push(q);
        }

        private void ResetNet()
        {
            if (_hoop != null && _hoop.Net != null)
                for (int k = 0; k < _hoop.Net.childCount; k++) _hoop.Net.GetChild(k).localScale = Vector3.one;
            _netT = -1f;
        }

        /// <summary>The board's resting specials lose this look's dribble and spin (a look switch, a new board).</summary>
        private void ResetIdle()
        {
            var board = _game != null ? _game.Board : null;
            if (board == null) return;
            for (int i = 0; i < board.Cells.Length; i++)
            {
                var v = _game.ViewAt(i);
                if (v == null || !v.Block.IsSpecial) continue;
                v.Bob = 0f;
                v.StickerSpin = 0f;
            }
        }

        private void Pool(Kind kind, int count)
        {
            var stack = new Stack<Quad>();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Court" + kind + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                stack.Push(new Quad { T = go.transform, R = r, Kind = kind });
            }
            _freeQuads[kind] = stack;
        }

        /// <summary>A pooled basketball: a pivot at the ball's centre, the mesh (Project/Toon with its embedded skin) under it.</summary>
        private Transform NewBall(string name, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            var mesh = new GameObject("Mesh");
            mesh.transform.SetParent(pivot, false);
            mesh.transform.localPosition = -_ballCentre;
            mesh.AddComponent<MeshFilter>().sharedMesh = _ballMesh;
            var r = mesh.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            pivot.gameObject.SetActive(false);
            return pivot;
        }

        /// <summary>Quads use the block labels' Project/PotaLabel (LabelTemplate): straight alpha, _UvRect frame, _Color tint × alpha per renderer.</summary>
        private Material QuadMaterial(ColorHuntSettings s, Texture tex, string name)
        {
            var m = RuntimeMaterial.From(s.LabelTemplate, "Project/PotaLabel", name);
            m.enableInstancing = true;
            m.SetTexture("_BaseMap", tex);
            return Keep(m);
        }

        private Material Keep(Material m)
        {
            _materials.Add(m);
            return m;
        }

        private int BoardSize => _game != null && _game.Board != null ? _game.Board.Size : _game != null && _game.S != null ? _game.S.Size : 7;

        /// <summary>Game-root units per board cell (the board's grid scale; the game root is never scaled).</summary>
        private float BoardScale => _game != null && _game.BoardRoot != null
            ? _game.BoardRoot.lossyScale.x / Mathf.Max(1e-4f, transform.lossyScale.x)
            : PotaLook.GridScale(BoardSize);

        /// <summary>Board cell + Pota depth (toward the camera) → world.</summary>
        private Vector3 World(Vector2 cell, float potaZ) => _game.BoardRoot.TransformPoint(new Vector3(cell.x, cell.y, -potaZ));

        /// <summary>Board cell + Pota depth → this object's space (the game root's).</summary>
        private Vector3 Local(Vector2 cell, float potaZ) => transform.InverseTransformPoint(World(cell, potaZ));

        private float R() => (float)_rng.NextDouble();

        private PotaSounds Sounds() => _game != null && _game.Juice != null ? _game.Juice.Sounds : null;

        private static void Call(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnDestroy()
        {
            foreach (var m in _materials) if (m != null) Destroy(m);
            if (_pass != null) Destroy(_pass);
            if (_crowdMade && _crowd != null) Destroy(_crowd);
        }
    }
}
```

- [ ] **Step 6: Compile, re-save the settings asset, run the EditMode tests**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
~/.local/bin/uv run Tools/mcp_call.py call manage_editor '{"action":"stop"}'
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CourtTests Project.Tests.SpecialStyleTests Project.Tests.SpecialScheduleTests Project.Tests.RuntimeShaderBuildTests Project.Tests.StudioAssetsTests
python3 - "$S" <<'EOF'
import json, sys
code = ('var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Project.ColorHunt.ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset"); '
        'UnityEditor.EditorUtility.SetDirty(s); UnityEditor.AssetDatabase.SaveAssets(); return s.Court.HopScale.ToString("0.00");')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1] + "/court-save-settings.json", "w"))
EOF
~/.local/bin/uv run Tools/mcp_call.py call execute_code @"$S/court-save-settings.json" | grep '"result"'
git diff --stat Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset
```
Expected: compiles (exit ≠ 2); `CourtTests` 14/14 and the listed fixtures pass (`RuntimeShaderBuildTests` finds only `Project/PotaLabel`,
`Project/PotaFlipbook`, `Project/PotaAdditive`, `Project/Toon`, URP `Particles/Lit` — all in the build through their templates);
`"result": "1.60"`; the asset diff is the new `Court:` block only (about 70 lines, every value the default). A failure in
`LineBalls_…` or `LineAndDunk_…` means `TimingFor` and the ball formulas disagree with `SpecialSchedule` (fix `CourtSpecials`, never
the schedule); `Sounds_…` failing with "a readable sample is cut" means `kalabalik.wav` is not Decompress On Load: re-run
`ai_potalook_import` (Task 3), do not loosen the test.

- [ ] **Step 7: Run the PlayMode tests, then every suite**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.CourtPlayTests --no-compile
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.SpecialsDemoPlayTests Project.Tests.SpecialPlayTests Project.Tests.SceneBootTests --no-compile
~/.local/bin/uv run Tools/unity_tests.py EditMode --no-compile
~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile
```
Expected: `CourtPlayTests` 5/5; `SpecialsDemoPlayTests` (its Court leg now plays this look: no "Court special look is missing
assets" warning in the log), `SpecialPlayTests` and `SceneBootTests` pass; both full suites pass. If
`Slam_LetteringIsInTheFrame_…` fails on a corner, lower `SlamLetterCells` or the clamp margin in `Letter` (and say so in the ledger);
if `EveryScenario_…` fails on the cap, a pool or emit count grew past `Cap`: shrink it, never raise `Cap`.

- [ ] **Step 8: Look at it** (Play Mode stepped one frame at a time; each `EditorApplication.Step()` advances 0.02 s of game time)

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S="/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad"
cat > "$S/court-look.sh" <<'SH'
#!/bin/zsh
# Specials.unity under the Court look, paused and stepped frame by frame; shoots the Main Camera (renders while paused) at the moments
# that matter. Prints "<scenario> <steps after the tap> <png>". Popups and the HUD are overlay UI: camera captures do not show them.
set -u
cd "/Users/tunaaltin17/projects/Unity MCP"
S="${0:A:h}"
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
shot() { M call manage_camera '{"action":"screenshot","capture_source":"game_view","camera":"Main Camera","include_image":false,"output_folder":"Temp/mcp_out"}' | grep -o '[^" ]*\.png' | head -1; }
step() { M call execute_code @"$S/court-step.json" > /dev/null; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
python3 - "$S" <<'EOF'
import json, sys
d = sys.argv[1]
def save(name, code):
    json.dump({"action": "execute", "code": code, "safety_checks": True}, open(f"{d}/{name}.json", "w"))
save("court-open", 'var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
     'if (s.path != "Assets/_Project/Games/Pota/Scenes/Specials.unity") { if (s.isDirty) return "dirty: " + s.path; '
     'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Games/Pota/Scenes/Specials.unity"); } '
     'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
save("court-step", 'UnityEditor.EditorApplication.isPaused = true; UnityEditor.EditorApplication.Step(); return UnityEngine.Time.time.ToString("0.00");')
save("court-unpause", 'UnityEditor.EditorApplication.isPaused = false; return "ok";')
for scen, cell in (("Bomb", 24), ("Line", 22), ("Chain", 21), ("Golden", 45), ("Birth", 44)):
    save("court-" + scen.lower(),
         'UnityEditor.EditorApplication.isPaused = true; '
         'var d = UnityEngine.Object.FindAnyObjectByType<Project.ColorHunt.SpecialsDemo>(); '
         'd.SetLook(Project.ColorHunt.SpecialLook.Court); '
         'd.Play(Project.ColorHunt.SpecialScenario.' + scen + ', false); '
         'var r = d.Game.Tap(' + str(cell) + '); '
         'return r + " " + d.Game.Specials.GetType().Name;')
EOF
M call execute_code @"$S/court-open.json" | grep '"result"' | grep -q Specials || { echo "could not open Specials (unsaved scene?)"; exit 1; }
M call manage_editor '{"action":"play"}' > /dev/null
for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
# The tapped special fires 0.0825 s (≈ 4 steps) in; a dunk lands 0.29 s later; a salvo tap's next step starts ≈ 0.96 s in.
for plan in "bomb 13 20 24 30" "line 12 16 23" "chain 12 18 24 30" "golden 60 75 90 96" "birth 52 60 64"; do
  set -- ${=plan}
  name=$1; shift
  M call execute_code @"$S/court-$name.json" | grep '"result"'
  n=0
  for at in "$@"; do
    while (( n < at )); do step; (( n++ )); done
    echo "$name $n $(shot)"
  done
done
M call execute_code @"$S/court-unpause.json" > /dev/null
M call manage_editor '{"action":"stop"}' > /dev/null
SH
zsh "$S/court-look.sh"
```
Expected: five `"result": "broken CourtSpecials"` lines and 18 `<scenario> <n> <png>` lines (PNGs under `Temp/mcp_out/`). Open each PNG
(Read tool), and Pota's `~/projects/Pota/public/assets/ozel-cizgi-a.png` beside them for the ball's orange. Check:
1. **bomb 13** (hang): an orange basketball with dark seams, clearly bigger than a block (×1.6 and nearer), above cell (3, 3); the bomb's
   own block is gone from its cell, the neighbours whole. **bomb 20/24** (impact): a dark star crack centred on (3, 3) **drawn over the
   blocks** (not hidden behind them), a tan inked dust ring around it, the 3×3 lifted and bursting in Pota's style, "SLAM!" large,
   whole and readable (no letter cut by the screen edge, not mirrored), white star flashes on the orange frame. **bomb 30**: SLAM! gone or
   fading, the crack fading, new blocks falling.
2. **line 12/16/23**: two orange balls in row 3 heading left and right, each with a yellow-orange flame tail pointing **backward**
   (the nozzle on the ball) and white speed streaks behind; the row bursting outward from (3, 1) in order; a white ring at the left
   frame edge by step 12, at the right one by step 23.
3. **chain 12–30**: "COMBO!" (gold) under the step's centre, readable; the bomb's dunk after it; a column of bursts in column 5.
4. **golden 60–96**: a gold basketball rolling down column 3 through the orange gate ring, arcing to the rim; at the landing "SWISH!"
   above the rim (whole), gold confetti rising from the hoop, the net rings visibly flexing.
5. **birth 52–64**: matched blocks hopping in arcs into their birth cells; the new specials dropping in with a thin white ring and light
   dust under them.
Nothing pink anywhere (pink = a material without its template's shader). A crack behind the blocks, a flame pointing forward, cut or
mirrored letters, a ball that is not Pota's orange, or a trail detached from its ball is a defect: fix it (Step 5), rerun Steps 6–7, look
again. If a moment was missed, change its step number in the plan line (each step is 0.02 s) and rerun only that scenario's line. Keep:
```bash
Tools/keep_screenshot.sh <the bomb 20 PNG> 2026-10-01-specials-court-dunk
Tools/keep_screenshot.sh <the line 16 PNG> 2026-10-01-specials-court-line
Tools/keep_screenshot.sh <the clearest chain PNG with COMBO!> 2026-10-01-specials-court-combo
Tools/keep_screenshot.sh <the golden landing PNG> 2026-10-01-specials-court-golden
Tools/keep_screenshot.sh <the birth 60 PNG> 2026-10-01-specials-court-birth
```

- [ ] **Step 9: Commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
P=Assets/_Project/Games/Pota
git add $P/Scripts/ColorHunt/Specials/CourtSpecials.cs* $P/Scripts/ColorHunt/ColorHuntSettings.cs $P/Scripts/ColorHunt/ColorHuntGame.cs \
  $P/Settings/ColorHuntSettings.asset \
  $P/Tests/EditMode/CourtTests.cs* $P/Tests/PlayMode/CourtPlayTests.cs* \
  docs/screenshots/2026-10-01-specials-court-dunk.jpg docs/screenshots/2026-10-01-specials-court-line.jpg \
  docs/screenshots/2026-10-01-specials-court-combo.jpg docs/screenshots/2026-10-01-specials-court-golden.jpg \
  docs/screenshots/2026-10-01-specials-court-birth.jpg
git status --short    # nothing else from this task should be left
git commit -m "feat: Color Hunt special look 2 · Court — bounce-pass births, dribbling specials, balls on fire with flame tails and speed lines, the dunk (crack, dust ring, SLAM!, flashbulbs), COMBO!, the golden swish with a net wave and gold confetti

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Expected: one commit; `git status --short` shows nothing from this task (the scratch scripts live outside the repo).

---

### Task 12: Recordings — sound log, Pota | 1 | 2 sheet and side video, voiced sequential video, close-ups, mobile cost

**Files:**
- Modify: `Scripts/ColorHunt/PotaSounds.cs` (sound log), `Scripts/ColorHunt/ColorHuntGame.cs` (`SpecialBorn`, `SpecialFired`),
  `Scripts/ColorHunt/Specials/SpecialsDemo.cs` (`Tapped`, `ShowUi`, `UiVisible`), `Editor/ColorHuntRecordTool.cs` (rewritten: Specials
  scenarios, markers, sound log, WAVs, `zoom`/`focus`/`until`)
- Modify: `Editor/FrameRecorder.cs` (canvases created after the start join at the next capture)
- Create: `Editor/WavWriter.cs`, `Editor/SpecialsCostTool.cs` (`ai_specials_cost`)
- Modify: `Tools/potalook/compare.py` (`specials`), `Tools/potalook/side_video.py` (rewritten: `groups` mode, `encode`),
  `Tools/potalook/record_unity.sh` (rewritten: scene from the parameters, summary line)
- Create: `Tools/potalook/seq_video.py`, `Tools/potalook/test_seq_video.py`, `Tools/potalook/record_specials.sh`
- Test: `Tests/EditMode/ColorHuntRecordToolTests.cs` (rewritten), `Tests/EditMode/PotaSoundsTests.cs` (+2), `Tests/EditMode/WavWriterTests.cs`,
  `Tests/EditMode/SpecialsCostToolTests.cs`, `Tests/EditMode/FrameRecorderTests.cs` (+1), `Tests/EditMode/RecordableClockTests.cs`,
  `Tests/PlayMode/SpecialsRecordPlayTests.cs`, `Tools/potalook/test_compare.py` (+2), `Tools/potalook/test_side_video.py` (+5)
- Output: `docs/screenshots/2026-10-01-specials-sheet-{birth,line,bomb,chain,golden}.jpg`, `2026-10-01-specials-side.mp4`,
  `2026-10-01-specials-voiced.mp4`, `2026-10-01-specials-closeup-{royal,court}-{bomb,line}.mp4`

(`Scripts/…`, `Editor/…`, `Tests/…` are under `Assets/_Project/Games/Pota/`; `Tools/…` and `docs/…` under the repo root.)

**Interfaces:**
- Consumes: Task 3 — `ColorHuntSettings.SfxBomb/SfxLine/SfxGoldenBorn/SfxGoldenLand/SfxSlam/SfxSwish/SfxCrowd` (`Audio/PotaLook/*.wav`,
  DecompressOnLoad + PCM, so `AudioClip.GetData` works). Task 5 — `PotaSounds.PlayClip` (and its private `Play`), `ColorHuntGame.Specials`,
  `SpecialLook`, `SetSpecialLook`, `GoldenLanded`, the `StartDrops` call `Specials.Birth(v, b.Kind)` and the `FireSpecial` call
  `Specials.Fire(f)`, `SpecialFire`, `PotaSpecials`, `ISpecialStyle.LiveCount`. Task 6 — `SpecialsDemo` (`Play(s, auto)`, `SetLook`,
  `ScenarioFinished`, `Finished`, `Game`, `BuildUi`/`Update` as drafted), `SpecialScenarios.Taps`, `SpecialScenario`, `Scenes/Specials.unity`
  in `PotaProfile` (so the Game view is 1170×2532 there). Tasks 10–11 — `RoyalSpecials`, `CourtSpecials` behind `ISpecialStyle`, all their
  sounds through `PotaSounds.PlayClip`: Royal ids `roket`, `fitil`, `para` (runtime clips from static `Make*`) beside Pota's samples; Court
  ids `smac`, `swish`, `kalabalik`, `courtPas`, `altinInis` (runtime clips `court-pas`, `kalabalik-kisa`); births `ozelDogdu`/`kombo`.
  Both combos call `TimeScaleController.SlowMo` (real time); Court's dunk camera `Push` and Royal's `ColorHuntCamera.AxisShake` run in
  `LateUpdate` on unscaled time; Royal's 2+ screen flash is the HUD-canvas Image `RoyalScreenFlash`; Court's lettering are world-space quads
  at sortingOrder 30; the dunk lands at fire + 0.29·Pace. Existing — `FrameRecorder`, `ColorHuntCamera.Zoom/ZoomFocus`,
  `TimeScaleController.Clear`, `GameViewPortrait.TrySet`, `AIToolsCommon`, `side_video.resample`, `compare.aligned/parse_rows`,
  `frame_sheet`, `keep_screenshot.sh`.
- Produces: the **Contract additions (Task 12)** above.
- Rulings (ledger):
  1. **The voiced mix is numpy, not ffmpeg `adelay` + `amix`** (Decision 8 / spec §4 name them). A sequential video carries a few hundred
     timed sounds; `amix` scales every input by the input count unless `normalize=0`, pitch needs `asetrate` + `aresample` per input, and a
     filter graph with hundreds of inputs is slow and hard to debug. numpy places every sample exactly, resamples for pitch, emulates
     PotaSounds' eight voices, and the mix plan is unit-tested; ffmpeg only encodes and muxes. Cost if wrong: none for the deliverable.
  2. **Frames, sounds and real-time effects share one clock; normal recordings need no shim.** A paused editor's `Step()` advances
     unscaled time by `Time.fixedDeltaTime` and game time by that × `timeScale` (CLAUDE.md §12; the 2026-09-30 close-up proves it:
     TimeScaleController's ×0.3 set `fixedDeltaTime` 0.006 → frames `t0.006, t0.012…`, and the 0.48-s board intro ended at 1.60 s =
     0.48 / 0.3). Against the recorder's clock `Now` (`Time.unscaledTime − t0`) game time therefore runs at `timeScale` exactly as on the
     device, and everything timed on `unscaledDeltaTime`/`unscaledTime` — Royal's and Court's combo `SlowMo`, Court's dunk `Push`, Royal's
     `AxisShake`, TimeScaleController's dips — keeps its real length; the video resampled by `Now` shows them as felt. The sound log is
     stamped with the same `Now`: a sound at `t` lands in tick `floor(30 t)`, its frame shows from that tick or the next — the "≤ 1 frame"
     sync the review checks. Only the wall clock (`realtimeSinceStartup`, `DateTime.Now`, `Stopwatch`) does not follow the steps:
     `RecordableClockTests` forbids it under `Scripts/ColorHunt/`. Cost if wrong: timing in the videos off by the slow-mo factor — the
     smoke step checks `meta.step`.
  3. **One moment sheet per scenario** (`-sheet-<scenario>.jpg`, rows Pota / 1 / 2): one 15-row sheet would be unreadable after
     `keep_screenshot.sh`'s 1280-px cap.
  4. **Specials recordings use the phone aspect** (PotaProfile 1170×2532 → `aspect` 0.4623, 540×1168 frames): "letters cut by the frame"
     and "golden landing visible" are questions about the phone frame. The ColorHunt scene's default stays 0.5625.
  5. **The recorder hides the demo's button rows** (`ShowUi(false)`, restored at the end); the game HUD stays.
  6. **Markers come from events, not from the script:** `SpecialsDemo.Tapped`, `ColorHuntGame.SpecialBorn/SpecialFired`, the
     `GoldenLanded` counter (polled once per frame), `ScenarioFinished`.
  7. **Close-ups are Royal and Court only** (spec §1.4 "her adayın"), first trigger only (`until: tap2`, tail 0): bomb ×2.4 on the bomb,
     line ×1.5 on the row's centre (cell 24) so both rockets stay in frame. **They slow down by fine steps, not by `SlowMo`:** the recorder
     holds `Time.fixedDeltaTime` at 0.3 × its value before every step, so game time, real-time effects and the styles' own slow motion all
     advance in 0.006-s steps of the same clock, and `seq_video` plays them back at `meta.speed` 0.3. The old way
     (`TimeScaleController.SlowMo(0.3, 3600)`) slowed game time only — Court's `Push` and Royal's `AxisShake` ran 3.3× too fast against it
     — and a style's own `SlowMo` replaced it (TimeScaleController keeps one slow motion), so a combo would have ended the close-up's ×0.3.
     This also changes ColorHunt-scene close-ups (none are rebuilt; the 2026-09-30 ones stay as made).
  8. **Canvases created after the recorder starts are moved at the next capture** (`FrameRecorder`): Royal's `RoyalScreenFlash` (or a
     PopupText canvas built on its first popup) would otherwise be missing from the frames. Order stays the device's: moved canvases sort
     at their order + 1000, above Court's world-space letters (30) and every particle (5–9), as overlay UI is on the phone.
  9. **The sound check is by sound id**, not by clip name (Court's `court-pas` and `kalabalik-kisa` are runtime clips whose names Task 11
     owns); the expected ids per look and scenario are listed in Step 15.
  10. **A segment's sounds fade out (0.25 s) during the next title card**; a sound whose voice is taken stops there (5-ms ramp), as on the
      device.
  11. **Phone cost is a delta**: idle beat vs the second after the tap, and each look vs Pota, Mobile RP asset, Game view 1170×2532, three
      Chain runs per look; editor Metal is not a phone, so the deltas are the signal.

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/ColorHuntRecordToolTests.cs` (whole file):
```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEngine;
using Object = UnityEngine.Object;

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

        [Test]
        public void CloseupFocus_IsTheFirstTapsCell()
        {
            Assert.AreEqual("43", ColorHuntRecordTool.FirstTap(ColorHuntRecordTool.Parse("wait 0.3; select 5; tap 43; tap auto")));
            Assert.AreEqual("auto", ColorHuntRecordTool.FirstTap(ColorHuntRecordTool.Parse("wait 0.3; tap auto")));
            Assert.AreEqual("auto", ColorHuntRecordTool.FirstTap(ColorHuntRecordTool.Parse("wait 1")), "no tap: the hint's block");
        }

        [Test]
        public void ParseRequest_DefaultsToTheColorHuntScene()
        {
            Assert.IsNull(ColorHuntRecordTool.ParseRequest(null, null, null, false, out var r));
            Assert.IsFalse(r.Specials);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("ColorHunt", "", null, true, out r), "script and win stay the ColorHunt scene's");
            Assert.IsFalse(r.Specials);
        }

        [Test]
        public void ParseRequest_Specials_ReadsTheScenarioAndTheSpecialLook()
        {
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "bomb", "1", false, out var r));
            Assert.IsTrue(r.Specials);
            Assert.AreEqual(SpecialScenario.Bomb, r.Scenario);
            Assert.AreEqual(SpecialLook.Royal, r.Look);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("specials", "Chain", "court", false, out r));
            Assert.AreEqual(SpecialScenario.Chain, r.Scenario);
            Assert.AreEqual(SpecialLook.Court, r.Look);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "golden", null, false, out r));
            Assert.AreEqual(SpecialLook.Pota, r.Look, "Pota by default");
            foreach (var s in new[] { "birth", "line", "bomb", "chain", "golden" })
                Assert.IsTrue(ColorHuntRecordTool.TryScenario(s, out _), s);
            Assert.IsTrue(ColorHuntRecordTool.TrySpecialLook("2", out var court));
            Assert.AreEqual(SpecialLook.Court, court);
            Assert.IsTrue(ColorHuntRecordTool.TrySpecialLook("Royal", out var royal));
            Assert.AreEqual(SpecialLook.Royal, royal);
        }

        [Test]
        public void ParseRequest_RejectsBadValues_WithAClearMessage()
        {
            StringAssert.Contains("unknown scene 'Basket' (ColorHunt, Specials)", ColorHuntRecordTool.ParseRequest("Basket", null, null, false, out _));
            StringAssert.Contains("unknown scenario 'fire' (birth, line, bomb, chain, golden",
                ColorHuntRecordTool.ParseRequest("Specials", "fire", null, false, out _));
            StringAssert.Contains("'free' has no script", ColorHuntRecordTool.ParseRequest("Specials", "free", null, false, out _));
            StringAssert.Contains("needs a scenario", ColorHuntRecordTool.ParseRequest("Specials", null, "royal", false, out _));
            StringAssert.Contains("unknown special_look '3' (pota, royal/1, court/2)",
                ColorHuntRecordTool.ParseRequest("Specials", "bomb", "3", false, out _));
            StringAssert.Contains("need scene 'Specials'", ColorHuntRecordTool.ParseRequest(null, "bomb", null, false, out _));
            StringAssert.Contains("need scene 'Specials'", ColorHuntRecordTool.ParseRequest("ColorHunt", null, "court", false, out _));
            StringAssert.Contains("script and win", ColorHuntRecordTool.ParseRequest("Specials", "bomb", null, true, out var r));
            Assert.IsFalse(r.Specials, "a refused request records nothing");
        }

        [Test]
        public void Markers_CountUp_TapThenTap2()
        {
            var keys = new List<string>();
            for (int i = 0; i < 3; i++) keys.Add(ColorHuntRecordTool.NextKey(keys, "tap"));
            CollectionAssert.AreEqual(new[] { "tap", "tap2", "tap3" }, keys);
            Assert.AreEqual("fire", ColorHuntRecordTool.NextKey(keys, "fire"));
        }

        [Test]
        public void ClipKeys_OneFilePerClip_UniqueAndSafe()
        {
            var a = AudioClip.Create("kombo", 10, 1, 44100, false);
            var b = AudioClip.Create("kombo", 10, 1, 44100, false);
            var c = AudioClip.Create("a b/ç", 10, 1, 44100, false);
            try
            {
                var keys = ColorHuntRecordTool.ClipKeys(new[] { a, b, a, c, null });
                Assert.AreEqual(3, keys.Count);
                Assert.AreEqual("kombo", keys[a]);
                Assert.AreEqual("kombo-2", keys[b], "a second clip of the same name gets its own file");
                Assert.AreEqual("a_b__", keys[c]);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }
    }
}
```

`Tests/EditMode/PotaSoundsTests.cs` — add inside the class, after `CollectAndDone_AreShortAudibleSynths`:
```csharp
        private static PotaSounds MakeSounds(out GameObject go)
        {
            go = new GameObject("Sounds");
            var sounds = go.AddComponent<PotaSounds>();                 // Awake does not run in Edit Mode: no voices
            sounds.S = ScriptableObject.CreateInstance<ColorHuntSettings>();
            return sounds;
        }

        [Test]
        public void SoundLog_IsOffUntilStarted_AndStampsEveryPlayedClip()
        {
            var sounds = MakeSounds(out var go);
            var sample = PotaSounds.MakeCollect();                       // stands in for an imported sample
            var synth = PotaSounds.MakeZip();                            // a runtime synth (AudioClip.Create), as Royal's and Court's
            try
            {
                sounds.S.SfxRotate = sample;
                sounds.Rotate();
                Assert.IsFalse(sounds.Logging);
                Assert.IsEmpty(sounds.StopLog(), "nothing is logged before StartLog");
                float now = 1.5f;
                sounds.StartLog(() => now);
                Assert.IsTrue(sounds.Logging);
                sounds.Rotate();
                now = 2.25f;
                sounds.PlayClip("swish", synth, 0.8f, 1.2f);
                sounds.PlayClip("zip", synth);
                var log = sounds.StopLog();
                Assert.IsFalse(sounds.Logging);
                Assert.AreEqual(3, log.Count);
                Assert.AreEqual("dondur", log[0].Id);
                Assert.AreSame(sample, log[0].Clip);
                Assert.AreEqual(1.5f, log[0].T, 1e-6f);
                Assert.AreEqual(1f, log[0].Volume, 1e-6f);
                Assert.That(log[0].Pitch, Is.InRange(0.97f, 1.03f), "a sample keeps Pota's jitter, logged as played");
                Assert.AreEqual(-1, log[0].Voice, "no voices outside Play Mode");
                Assert.AreEqual("swish", log[1].Id);
                Assert.AreSame(synth, log[1].Clip, "a synthesised clip is logged like a sample");
                Assert.AreEqual(2.25f, log[1].T, 1e-6f);
                Assert.AreEqual(0.8f, log[1].Volume, 1e-6f);
                Assert.AreEqual(1.2f, log[1].Pitch, 1e-6f, "a given pitch is used as given");
                Assert.AreEqual(1f, log[2].Pitch, 1e-6f, "a synth id at pitch 1 plays at 1");
                sounds.Rotate();
                Assert.AreEqual(3, log.Count, "StopLog hands the list over; later sounds are not added");
            }
            finally
            {
                Object.DestroyImmediate(sample);
                Object.DestroyImmediate(synth);
                Object.DestroyImmediate(sounds.S);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SoundLog_SkipsWhatTheRateLimitRefused()
        {
            var sounds = MakeSounds(out var go);
            var clip = PotaSounds.MakeCollect();
            try
            {
                sounds.StartLog(() => 0f);
                sounds.PlayClip("kombo", clip);
                sounds.PlayClip("kombo", clip);                           // the same unscaled time: SINIR kombo 80 ms refuses it
                Assert.AreEqual(1, sounds.StopLog().Count);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(sounds.S);
                Object.DestroyImmediate(go);
            }
        }
```

`Tests/EditMode/WavWriterTests.cs`:
```csharp
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The recorder's sound files: 16-bit PCM WAV from AudioClip.GetData (Task 12).</summary>
    public class WavWriterTests
    {
        private const string Dir = "Temp/test_wav";

        private static string Ascii(byte[] b, int at) => Encoding.ASCII.GetString(b, at, 4);

        [Test]
        public void Encode_WritesA16BitPcmHeader_AndEverySample()
        {
            var bytes = WavWriter.Encode(new[] { 0f, 0.5f, -1f, 1.5f, 0.25f, -0.25f }, 2, 48000);
            Assert.AreEqual(44 + 12, bytes.Length);
            Assert.AreEqual("RIFF", Ascii(bytes, 0));
            Assert.AreEqual(36 + 12, BitConverter.ToInt32(bytes, 4));
            Assert.AreEqual("WAVE", Ascii(bytes, 8));
            Assert.AreEqual("fmt ", Ascii(bytes, 12));
            Assert.AreEqual(16, BitConverter.ToInt32(bytes, 16));
            Assert.AreEqual(1, BitConverter.ToInt16(bytes, 20), "PCM");
            Assert.AreEqual(2, BitConverter.ToInt16(bytes, 22), "channels");
            Assert.AreEqual(48000, BitConverter.ToInt32(bytes, 24));
            Assert.AreEqual(48000 * 2 * 2, BitConverter.ToInt32(bytes, 28), "byte rate");
            Assert.AreEqual(4, BitConverter.ToInt16(bytes, 32), "block align");
            Assert.AreEqual(16, BitConverter.ToInt16(bytes, 34), "bits");
            Assert.AreEqual("data", Ascii(bytes, 36));
            Assert.AreEqual(12, BitConverter.ToInt32(bytes, 40));
            Assert.AreEqual(0, BitConverter.ToInt16(bytes, 44));
            Assert.AreEqual(16384, BitConverter.ToInt16(bytes, 46), "0.5");
            Assert.AreEqual(-32767, BitConverter.ToInt16(bytes, 48), "-1");
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 50), "1.5 is clamped");
        }

        [Test]
        public void TryWrite_ASynthClip_RoundTripsItsLength()
        {
            var clip = PotaSounds.MakeCollect();                          // AudioClip.Create: mono, 44.1 kHz
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, "topla.wav");
            try
            {
                Assert.IsTrue(WavWriter.TryWrite(clip, path, out string error), error);
                var bytes = File.ReadAllBytes(path);
                Assert.AreEqual(1, BitConverter.ToInt16(bytes, 22));
                Assert.AreEqual(44100, BitConverter.ToInt32(bytes, 24));
                Assert.AreEqual(clip.samples * 2, BitConverter.ToInt32(bytes, 40), "one 16-bit sample per frame");
                Assert.AreEqual(44 + clip.samples * 2, bytes.Length);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void TryWrite_RefusesANullClip()
        {
            Assert.IsFalse(WavWriter.TryWrite(null, Path.Combine(Dir, "none.wav"), out string error));
            StringAssert.Contains("no clip", error);
        }
    }
}
```

`Tests/EditMode/SpecialsCostToolTests.cs`:
```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Project.Editor.AITools;

namespace Project.Tests
{
    /// <summary>ai_specials_cost's arithmetic: the idle beat before the tap against the second after it, runs combined (Task 12).</summary>
    public class SpecialsCostToolTests
    {
        private static SpecialsCostTool.Sample S(float t, float cpu, float gpu, int live = 0, int draws = 0) =>
            new SpecialsCostTool.Sample { T = t, Cpu = cpu, Gpu = gpu, Render = -1f, Draws = draws, SetPass = draws / 4, Live = live };

        [Test]
        public void Summarize_SplitsTheBeatBeforeTheTap_FromTheSecondAfter()
        {
            var samples = new List<SpecialsCostTool.Sample>
            {
                S(0.05f, 30f, 30f),                                       // staging, before the idle window
                S(0.30f, 4f, 2f), S(0.35f, 4f, 2f), S(0.40f, 5f, 3f),      // the demo's beat before the tap at 0.5
                S(0.50f, 6f, 4f, 300, 120), S(0.60f, 9f, 7f, 900, 160), S(0.70f, 6f, 4f, 500, 140), S(1.40f, 5f, 3f, 100, 110),
                S(2.00f, 40f, 40f, 2000, 500),                            // after the window
            };
            var s = SpecialsCostTool.Summarize(samples, 0.5f);
            Assert.AreEqual(4f, s.IdleCpu, 1e-4f, "median of the beat");
            Assert.AreEqual(2f, s.IdleGpu, 1e-4f);
            Assert.AreEqual(8.55f, s.PeakCpu, 1e-4f, "p95 of 5, 6, 6, 9");
            Assert.AreEqual(9f, s.MaxCpu, 1e-4f);
            Assert.AreEqual(7f, s.MaxGpu, 1e-4f);
            Assert.AreEqual(900, s.MaxLive);
            Assert.AreEqual(160, s.MaxDraws);
            Assert.AreEqual(40, s.MaxSetPass);
            Assert.AreEqual(4, s.Frames);
        }

        [Test]
        public void Summarize_MarksAMissingGpuTime_AsMinusOne()
        {
            var s = SpecialsCostTool.Summarize(new List<SpecialsCostTool.Sample> { S(0.3f, 4f, 0f), S(0.6f, 6f, 0f) }, 0.5f);
            Assert.AreEqual(-1f, s.IdleGpu);
            Assert.AreEqual(-1f, s.PeakGpu);
            Assert.AreEqual(-1f, s.MaxGpu);
            Assert.AreEqual(6f, s.PeakCpu, 1e-4f);
        }

        [Test]
        public void MedianAndPercentile()
        {
            Assert.AreEqual(2.5f, SpecialsCostTool.Median(new List<float> { 4f, 1f, 2f, 3f }), 1e-6f);
            Assert.AreEqual(1.9f, SpecialsCostTool.Percentile(new List<float> { 1f, 2f }, 0.9f), 1e-6f);
            Assert.AreEqual(-1f, SpecialsCostTool.Median(new List<float>()));
        }

        [Test]
        public void Combine_TakesTheMedianOfTheRuns_AndTheWorstLivePeak()
        {
            var runs = new List<SpecialsCostTool.Summary>
            {
                new SpecialsCostTool.Summary { PeakGpu = 3f, MaxLive = 100, Frames = 30 },
                new SpecialsCostTool.Summary { PeakGpu = 9f, MaxLive = 300, Frames = 31 },
                new SpecialsCostTool.Summary { PeakGpu = 4f, MaxLive = 200, Frames = 29 },
            };
            var c = SpecialsCostTool.Combine(runs);
            Assert.AreEqual(4f, c.PeakGpu, 1e-6f);
            Assert.AreEqual(300, c.MaxLive, "BurstBudget is a ceiling: the worst run counts");
            Assert.AreEqual(90, c.Frames);
        }
    }
}
```

`Tests/EditMode/FrameRecorderTests.cs` — add inside the class, after `WithUi_MovedCanvasesStayAboveWorldEffects_AndKeepTheirOrder`:
```csharp
        [Test]
        public void WithUi_MovesCanvasesCreatedAfterItStarted_AtTheNextCapture()
        {
            // Royal's screen flash (or a PopupText canvas) may be built on the first combo, after the recorder started (Task 12).
            var camGo = new GameObject("RecCam");
            var cam = camGo.AddComponent<Camera>();
            GameObject lateGo = null;
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, "late.png");
            try
            {
                using (var rec = new FrameRecorder(cam, 64, 1f, true))
                {
                    lateGo = new GameObject("Late", typeof(Canvas));
                    var late = lateGo.GetComponent<Canvas>();
                    late.renderMode = RenderMode.ScreenSpaceOverlay;
                    late.sortingOrder = 7;
                    rec.Capture(path);
                    Assert.AreEqual(RenderMode.ScreenSpaceCamera, late.renderMode, "joined at the capture after it appeared");
                    Assert.AreEqual(cam, late.worldCamera);
                    Assert.AreEqual(1007, late.sortingOrder, "above the world's effects and Court's letters (30)");
                }
                var canvas = lateGo.GetComponent<Canvas>();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode, "put back like the others");
                Assert.AreEqual(7, canvas.sortingOrder);
            }
            finally
            {
                if (lateGo != null) Object.DestroyImmediate(lateGo);
                Object.DestroyImmediate(camGo);
                if (File.Exists(path)) File.Delete(path);
            }
        }
```

`Tests/EditMode/RecordableClockTests.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Project.Tests
{
    /// <summary>
    /// ai_colorhunt_record steps the paused editor: game time and unscaled time follow the steps, the wall clock does not. Color Hunt's
    /// code — the special looks included — must time itself with deltaTime / unscaledDeltaTime / unscaledTime only (Task 12).
    /// </summary>
    public class RecordableClockTests
    {
        private static readonly string[] WallClock = { "realtimeSinceStartup", "DateTime.Now", "DateTime.UtcNow", "Stopwatch", "Environment.TickCount" };

        [Test]
        public void ColorHuntCode_NeverReadsTheWallClock()
        {
            var hits = new List<string>();
            foreach (var path in Directory.GetFiles("Assets/_Project/Games/Pota/Scripts/ColorHunt", "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                    foreach (var token in WallClock)
                        if (lines[i].Contains(token)) hits.Add($"{path}:{i + 1}: {token}");
            }
            Assert.IsEmpty(hits, "the frame recorder cannot step these:\n" + string.Join("\n", hits));
        }
    }
}
```

`Tests/PlayMode/SpecialsRecordPlayTests.cs`:
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>What ai_colorhunt_record listens to in the Specials scene: the demo's taps, births, fires and the sound log (Task 12).</summary>
    public class SpecialsRecordPlayTests
    {
        private static IEnumerator Load(Action<SpecialsDemo> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            ready(demo);
        }

        private static IEnumerator Run(SpecialsDemo demo, SpecialScenario s)
        {
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(s, true);
            yield return StudioLookPlayTests.WaitUntil(() => done, 20f);
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, $"{s} never finished");
        }

        [UnityTest]
        public IEnumerator Bomb_RaisesTapsAndFires_AndLogsTheBombSoundOnTheVoices()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Pota);
            var taps = new List<int>();
            var fires = new List<SpecialFire>();
            demo.Tapped += taps.Add;
            game.SpecialFired += fires.Add;
            var sounds = game.Juice.Sounds;
            sounds.StartLog(() => Time.unscaledTime);
            yield return Run(demo, SpecialScenario.Bomb);
            var log = sounds.StopLog();
            demo.Tapped -= taps.Add;
            game.SpecialFired -= fires.Add;
            CollectionAssert.AreEqual(SpecialScenarios.Taps(SpecialScenario.Bomb), taps);
            Assert.AreEqual(2, fires.Count, "the middle bomb, then the corner one");
            Assert.AreEqual(24, fires[0].Cell);
            Assert.AreEqual(48, fires[1].Cell);
            Assert.AreEqual(SpecialKind.Bomb, fires[1].Kind);
            var bombs = log.FindAll(e => e.Id == "patlama");
            Assert.AreEqual(2, bombs.Count, "Pota's bomba sample, once per bomb");
            Assert.AreSame(game.S.SfxBomb, bombs[0].Clip);
            for (int i = 0; i < log.Count; i++)
            {
                Assert.That(log[i].Voice, Is.InRange(0, 7), log[i].Id);
                if (i == 0) continue;
                Assert.AreEqual((log[i - 1].Voice + 1) % 8, log[i].Voice, "round-robin voices, as the mix emulates them");
                Assert.GreaterOrEqual(log[i].T, log[i - 1].T, "in time order");
            }
        }

        [UnityTest]
        public IEnumerator Birth_RaisesSpecialBorn_ForEachNewBall()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Pota);
            var born = new List<string>();
            void Born(SpecialKind kind, int cell) => born.Add($"{cell}:{kind}");
            game.SpecialBorn += Born;
            yield return Run(demo, SpecialScenario.Birth);
            game.SpecialBorn -= Born;
            CollectionAssert.AreEquivalent(new[] { "29:LineV", "43:Golden", "48:LineH", "32:Bomb" }, born, "SpecialScenariosTests' births");
        }

        [UnityTest]
        public IEnumerator ShowUi_HidesTheDemoRows_ForTheRecorder()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var canvas = GameObject.Find("SpecialsDemoUI").GetComponent<Canvas>();
            demo.ShowUi(false);
            Assert.IsFalse(canvas.enabled);
            Assert.IsFalse(demo.UiVisible);
            demo.ShowUi(true);
            Assert.IsTrue(canvas.enabled);
            Assert.IsTrue(demo.UiVisible);
        }
    }
}
```

`Tools/potalook/test_compare.py` — append:
```python
def test_special_moments_cover_the_scenarios():
    assert set(compare.SPECIAL_MOMENTS) == {"birth", "line", "bomb", "chain", "golden"}
    assert compare.special_moment("birth")[0] == "born"
    assert compare.special_moment("golden")[0] == "land"
    assert {compare.special_moment(s)[0] for s in ("line", "bomb", "chain")} == {"tap"}
    for _, times in compare.SPECIAL_MOMENTS.values():
        assert min(times) >= -compare.WINDOW, "aligned() drops frames more than WINDOW before the anchor"


def test_an_unknown_special_scenario_is_refused():
    with pytest.raises(ValueError):
        compare.special_moment("free")
```

`Tools/potalook/test_side_video.py` — add `import json` and `from PIL import Image` to the imports, then append:
```python
def test_groups_parse_a_title_and_its_columns():
    assert side.parse_groups([["Bomba", "Pota=a", "1 · Royal=b"]]) == [("Bomba", [("Pota", "a"), ("1 · Royal", "b")])]
    with pytest.raises(ValueError):
        side.parse_groups([["Bomba"]])


def test_group_window_runs_from_before_the_anchors_to_the_longest_column():
    assert side.window([1.0, 1.5], [3.0, 2.8], 0.6) == (-0.6, pytest.approx(2.0))


def test_index_at_clamps_to_the_recording():
    assert side.index_at(1.234, 50) == 37
    assert side.index_at(-0.1, 50) == 0
    assert side.index_at(10.0, 50) == 49


def test_group_frames_align_every_column_at_its_own_anchor(tmp_path):
    def rec(name, anchor):
        folder = tmp_path / name
        folder.mkdir()
        for i in range(11):                                         # frames every 0.1 s, red = 20 per frame
            Image.new("RGB", (4, 8), (i * 20, 0, 0)).save(folder / f"f_{i:04d}_t{i / 10:.3f}.png")
        (folder / "events.json").write_text(json.dumps({"tap": anchor, "sounds": []}))
        return str(folder)

    imgs = list(side.group_frames("Bomba", [("Pota", rec("a", 0.5)), ("1", rec("b", 0.2))], 4, 8, 0.1, "tap"))
    assert len(imgs) == 28, "0.1 s before the taps to b's end (1.0 − 0.2 = 0.8 s), both ends included, at 30 fps"
    assert imgs[0].size == (8, side.HEAD_H + 8)
    assert imgs[0].getpixel((1, side.HEAD_H + 2))[0] == 80, "a shows 0.4 s"
    assert imgs[0].getpixel((5, side.HEAD_H + 2))[0] == 20, "b shows 0.1 s"
    assert imgs[-1].getpixel((5, side.HEAD_H + 2))[0] == 200, "b ends on its last frame"


@pytest.mark.skipif(not os.path.exists(side.FFMPEG), reason="ffmpeg missing")
def test_encode_pipes_frames_into_an_mp4(tmp_path):
    out = str(tmp_path / "x.mp4")
    side.encode((Image.new("RGB", (32, 32), (i * 40, 0, 0)) for i in range(5)), (32, 32), out)
    assert os.path.getsize(out) > 0
```

`Tools/potalook/test_seq_video.py`:
```python
import importlib.util
import json
import os
import subprocess
import sys

import numpy as np
import pytest
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
spec = importlib.util.spec_from_file_location("seq_video", os.path.join(HERE, "seq_video.py"))
seq = importlib.util.module_from_spec(spec)
spec.loader.exec_module(seq)


def frames(folder, times, size=(4, 8)):
    os.makedirs(folder, exist_ok=True)
    for i, t in enumerate(times):
        Image.new("RGB", size, (i * 20, 0, 0)).save(os.path.join(folder, f"f_{i:04d}_t{t:.3f}.png"))
    return str(folder)


def segment(folder, sounds, n_ticks):
    s = seq.Segment("Bomba", "Pota", folder)
    s.ticks = ["x"] * n_ticks
    s.sounds = sounds
    return s


def sound(clip, t, voice, volume=1.0, pitch=1.0, sid=None):
    return {"id": sid or clip, "clip": clip, "t": t, "volume": volume, "pitch": pitch, "voice": voice}


def tone(path, value, n, rate=48000, channels=1):
    seq.write_wav(str(path), np.full((n, channels), value, np.float32), rate)
    return str(path)


def test_segments_parse_title_look_and_folder_in_order():
    got = seq.parse_segments(["Bomba|Pota=Temp/record/a", "Bomba|1 · Royal=Temp/record/b", "Zincir=c"])
    assert [(s.title, s.look, s.folder) for s in got] == [
        ("Bomba", "Pota", "Temp/record/a"), ("Bomba", "1 · Royal", "Temp/record/b"), ("Zincir", "", "c")]
    assert got[1].label == "Bomba · 1 · Royal"
    assert got[2].label == "Zincir"


def test_a_segment_without_a_folder_is_refused():
    with pytest.raises(ValueError):
        seq.parse_segments(["Bomba|Pota"])


def test_layout_puts_a_card_before_every_segment_in_order():
    slots, total = seq.layout([1.0, 1.5, 0.5], 0.5)
    assert [(s.card, s.body, s.end) for s in slots] == [
        (pytest.approx(0.0), pytest.approx(0.5), pytest.approx(1.5)),
        (pytest.approx(1.5), pytest.approx(2.0), pytest.approx(3.5)),
        (pytest.approx(3.5), pytest.approx(4.0), pytest.approx(4.5))]
    assert total == pytest.approx(4.5)


def test_ticks_resample_the_recording_by_the_clock_in_the_frame_names(tmp_path):
    # hit-stops crowd frames together (0.020, 0.021, 0.022 …); the video takes one per 1/30 s of that clock
    folder = frames(tmp_path / "rec", [0.0, 0.02, 0.021, 0.022, 0.1])
    got = [os.path.basename(p) for p in seq.ticks(folder)]
    assert got == ["f_0000_t0.000.png", "f_0003_t0.022.png", "f_0003_t0.022.png", "f_0004_t0.100.png"]


def test_a_slow_segment_plays_its_clock_at_its_speed(tmp_path):
    # a close-up steps 0.006 s at a time and says speed 0.3 in its meta: one video tick per 0.01 s of its clock
    folder = frames(tmp_path / "cu", [0.0, 0.006, 0.012, 0.018, 0.024, 0.030])
    with open(os.path.join(folder, "events.json"), "w") as f:
        json.dump({"meta": {"speed": 0.3}, "sounds": [sound("bomba", 0.03, 0)]}, f)
    seg = seq.load(seq.parse_segments([f"Bomba|1={folder}"])[0])
    assert seg.speed == pytest.approx(0.3)
    assert [os.path.basename(p) for p in seg.ticks] == [
        "f_0000_t0.000.png", "f_0001_t0.006.png", "f_0003_t0.018.png", "f_0005_t0.030.png"]
    slots, total = seq.layout([len(seg.ticks) / seq.FPS], 0.0)
    assert seq.mix_plan([seg], slots, total)[0].at == pytest.approx(0.1), "0.03 s of its clock is 0.1 s of video"
    assert seq.load(seq.parse_segments([f"Bomba|1={folder}"])[0], speed=1.0).speed == 1.0, "--speed overrides the meta"


def test_mix_plan_places_each_sound_at_its_segment_body_plus_its_time():
    a = segment("A", [sound("kirildi", 0.1, 0)], 30)
    b = segment("B", [sound("bomba", 0.25, 1, volume=0.8, pitch=1.1, sid="patlama")], 45)
    slots, total = seq.layout([1.0, 1.5], 0.5)
    plan = seq.mix_plan([a, b], slots, total)
    assert [e.path for e in plan] == [os.path.join("A", "sounds", "kirildi.wav"), os.path.join("B", "sounds", "bomba.wav")]
    assert plan[0].at == pytest.approx(0.6)
    assert plan[1].at == pytest.approx(2.25)
    assert (plan[1].volume, plan[1].pitch) == (pytest.approx(0.8), pytest.approx(1.1))


def test_mix_plan_cuts_a_taken_voice_and_ends_a_segment_in_the_next_card():
    a = segment("A", [sound("kombo", 0.1, 2), sound("kirildi", 0.2, 3), sound("dustu", 0.4, 2)], 30)
    b = segment("B", [sound("bomba", 0.0, 0)], 15)
    slots, total = seq.layout([1.0, 0.5], 0.5)
    plan = {(os.path.basename(e.path), round(e.at, 3)): e for e in seq.mix_plan([a, b], slots, total)}
    kombo = plan[("kombo.wav", 0.6)]
    assert kombo.stop == pytest.approx(0.9) and kombo.fade == seq.VOICE_FADE, "voice 2 plays dustu at 0.4 s"
    kirildi = plan[("kirildi.wav", 0.7)]
    assert kirildi.stop == pytest.approx(2.0) and kirildi.fade == seq.WINDOW_FADE, "A's sounds end as B's frames start"
    assert plan[("bomba.wav", 2.0)].stop == pytest.approx(total), "the last segment runs to the end"


def test_render_mix_honours_time_pitch_and_volume(tmp_path):
    path = tone(tmp_path / "a.wav", 0.5, 4800)                            # 0.1 s of 0.5, mono 48 kHz
    track, gain = seq.render_mix([seq.MixEvent(0.1, path, 0.5, 2.0, 1.0, seq.WINDOW_FADE)], 0.5)
    assert track.shape == (24000, 2)
    assert gain == 1.0
    assert np.allclose(track[:4800], 0.0)
    assert np.allclose(track[4800:7200], 0.25, atol=1e-3), "pitch 2 plays the 0.1 s clip in 0.05 s, volume 0.5"
    assert np.allclose(track[7200:], 0.0)


def test_render_mix_resamples_other_rates_and_fades_a_cut(tmp_path):
    path = tone(tmp_path / "b.wav", 0.4, 44100, rate=44100, channels=2)  # 1 s, stereo 44.1 kHz
    track, _ = seq.render_mix([seq.MixEvent(0.0, path, 1.0, 1.0, 0.5, 0.1)], 1.0)
    assert np.allclose(track[:19200], 0.4, atol=1e-3), "44.1 kHz plays at its own speed on the 48 kHz track"
    assert track[23999, 0] == pytest.approx(0.0, abs=1e-3), "cut at 0.5 s through a 0.1 s fade"
    assert np.allclose(track[24000:], 0.0)


def test_render_mix_lowers_a_clipping_sum_to_the_peak(tmp_path):
    path = tone(tmp_path / "c.wav", 0.9, 4800)
    events = [seq.MixEvent(0.0, path, 1.0, 1.0, 1.0, seq.WINDOW_FADE)] * 2
    track, gain = seq.render_mix(events, 0.2)
    assert gain == pytest.approx(seq.PEAK / 1.8, rel=1e-3)
    assert np.abs(track).max() == pytest.approx(seq.PEAK, rel=1e-3)


def test_a_missing_wav_is_skipped(tmp_path, capsys):
    track, gain = seq.render_mix([seq.MixEvent(0.0, str(tmp_path / "nope.wav"), 1.0, 1.0, 1.0, 0.1)], 0.1)
    assert np.allclose(track, 0.0) and gain == 1.0
    assert "missing" in capsys.readouterr().out


def test_label_strip_card_and_even_sizes():
    assert seq.strip(540, "Bomba · 1 · Royal").size == (540, seq.STRIP_H)
    assert seq.card(540, 1224, "Bomba", "1 · Royal").size == (540, 1224)
    assert (seq.even(1169), seq.even(1224)) == (1168, 1224)


def test_sync_rows_take_the_frames_around_each_special_sound():
    s = seq.Segment("Bomba", "Pota", "A")
    s.ticks = [f"t{i}" for i in range(10)]
    s.sounds = [sound("dustu", 0.1, 0), sound("bomba", 0.1, 1, sid="patlama")]
    rows = seq.sync_rows([s])
    assert len(rows) == 1, "drops are routine"
    assert rows[0][2] == ["t2", "t3", "t4"]


def test_video_frames_are_the_card_then_the_strip_over_each_tick(tmp_path):
    a = seq.load(seq.parse_segments([f"Bomba|Pota={frames(tmp_path / 'a', [0.0, 0.05, 0.1])}"])[0])
    imgs = list(seq.video_frames([a], (4, 8 + seq.STRIP_H), 2))
    assert len(imgs) == 2 + len(a.ticks) == 6
    assert all(im.size == (4, 8 + seq.STRIP_H) for im in imgs)
    assert imgs[2].getpixel((1, seq.STRIP_H + 2))[0] == 0
    assert imgs[-1].getpixel((1, seq.STRIP_H + 2))[0] == 40


@pytest.mark.skipif(not os.path.exists(seq.FFMPEG), reason="ffmpeg missing")
def test_main_builds_a_voiced_video_and_a_sync_sheet(tmp_path):
    rec = frames(tmp_path / "rec", [0.0, 0.1, 0.2, 0.3], size=(32, 32))
    os.makedirs(os.path.join(rec, "sounds"))
    tone(os.path.join(rec, "sounds", "bomba.wav"), 0.3, 4800)
    with open(os.path.join(rec, "events.json"), "w") as f:
        json.dump({"tap": 0.1, "meta": {"scenario": "bomb"}, "sounds": [sound("bomba", 0.1, 0, sid="patlama")]}, f)
    out = str(tmp_path / "v.mp4")
    seq.main([out, f"Bomba|Pota={rec}", "--card", "0.2", "--sync-sheet", str(tmp_path / "sync")])
    probe = subprocess.run([os.path.join(os.path.dirname(seq.FFMPEG), "ffprobe"), "-v", "error", "-show_entries",
                            "stream=codec_type", "-of", "csv=p=0", out], capture_output=True, text=True, check=True)
    assert sorted(probe.stdout.split()) == ["audio", "video"]
    assert os.path.exists(str(tmp_path / "sync-00-bomb-pota.jpg"))
```

- [ ] **Step 2: Run to see them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntRecordToolTests Project.Tests.PotaSoundsTests Project.Tests.WavWriterTests Project.Tests.SpecialsCostToolTests Project.Tests.FrameRecorderTests Project.Tests.RecordableClockTests`
Expected: exit 2 with `error CS0117: 'ColorHuntRecordTool' does not contain a definition for 'ParseRequest'` (and `TryScenario`,
`TrySpecialLook`, `NextKey`, `ClipKeys`), `error CS0246: The type or namespace name 'WavWriter' could not be found`, `… 'SpecialsCostTool'
could not be found`, `error CS1061: 'PotaSounds' does not contain a definition for 'StartLog'` (and `Logging`, `StopLog`). The PlayMode file
fails in the same compile (`'SpecialsDemo' does not contain a definition for 'Tapped'`, `'ColorHuntGame' … 'SpecialFired'`).
`FrameRecorderTests.WithUi_MovesCanvasesCreatedAfterItStarted_AtTheNextCapture` compiles today and fails once the rest compiles (the late
canvas stays Screen Space Overlay). `RecordableClockTests` passes from the start: it guards the code Tasks 10–11 added and anything later.
Run: `~/.local/bin/uv run --with pytest --with pillow --with numpy pytest -q Tools/potalook`
Expected: `ERROR collecting Tools/potalook/test_seq_video.py` (`FileNotFoundError: … seq_video.py`), and
`AttributeError: module 'compare' has no attribute 'SPECIAL_MOMENTS'` / `'special_moment'`, `module 'side_video' has no attribute
'parse_groups'` (and `window`, `index_at`, `group_frames`, `HEAD_H`, `encode`) in the other two files.

- [ ] **Step 3: The sound log — `PotaSounds`**

`Scripts/ColorHunt/PotaSounds.cs`:

1. Above `public class PotaSounds : MonoBehaviour` (inside the namespace):
```csharp
    /// <summary>One sound that played (the recorder's log): id, clip, the log clock's time, volume, the pitch it played at, its voice (-1: none).</summary>
    public struct SoundLogEntry
    {
        public string Id;
        public AudioClip Clip;
        public float T, Volume, Pitch;
        public int Voice;
    }
```

2. After `public float LastVolume { get; private set; }`:
```csharp
        private List<SoundLogEntry> _log;
        private System.Func<float> _logClock;

        /// <summary>True while a recording logs the sounds (ai_colorhunt_record).</summary>
        public bool Logging => _log != null;

        /// <summary>
        /// Logs every sound that plays from now on — whatever path it came by (PlayClip, Combo, Drop, Zip, Collect…), after the rate limits —
        /// stamped with <paramref name="clock"/> (the recorder passes its frame clock; null: unscaled time).
        /// </summary>
        public void StartLog(System.Func<float> clock)
        {
            _log = new List<SoundLogEntry>();
            _logClock = clock ?? (() => Time.unscaledTime);
        }

        /// <summary>Stops logging and hands over what played since StartLog (empty if it never started).</summary>
        public List<SoundLogEntry> StopLog()
        {
            var log = _log ?? new List<SoundLogEntry>();
            _log = null;
            _logClock = null;
            return log;
        }
```

3. Replace the private `Play` method with:
```csharp
        private void Play(string id, AudioClip clip, float volume, float pitch = -1f)
        {
            if (clip == null || !Allow(_last, id, Time.unscaledTime)) return;
            LastClip = clip;
            LastVolume = volume;
            if (_voices == null && _log == null) return;
            float p = pitch > 0f ? pitch : IsSample(id) ? Random.Range(0.97f, 1.03f) : 1f;
            _log?.Add(new SoundLogEntry { Id = id, Clip = clip, T = _logClock(), Volume = volume, Pitch = p, Voice = _voices != null ? _next : -1 });
            if (_voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = p;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }
```
(Same behaviour as before when nothing logs: the jitter is drawn only when a voice plays or the log is on, so `Random` is consumed exactly
as today in Play Mode and in the existing Edit Mode tests.)

- [ ] **Step 4: The markers — `ColorHuntGame` and `SpecialsDemo` hooks**

`Scripts/ColorHunt/ColorHuntGame.cs` (Task 5's version, after Tasks 10–11):

1. After `public event Action<int> CellBurst;`:
```csharp
        /// <summary>A special ball was born (its kind and cell), right after the look drew it — the recorder's "born" marker.</summary>
        public event Action<SpecialKind, int> SpecialBorn;
        /// <summary>A special fired, right after the look's Fire — the recorder's "fire" marker.</summary>
        public event Action<SpecialFire> SpecialFired;
```
2. In `StartDrops`, find
```csharp
                v.SetBlock(Block.MakeSpecial(b.Kind), b.Cell);
                Specials.Birth(v, b.Kind);
```
and replace it with
```csharp
                v.SetBlock(Block.MakeSpecial(b.Kind), b.Cell);
                Specials.Birth(v, b.Kind);
                try { SpecialBorn?.Invoke(b.Kind, b.Cell); }
                catch (Exception e) { Debug.LogException(e); }
```
3. In `FireSpecial`, replace its last line `Specials.Fire(f);` with
```csharp
            Specials.Fire(f);
            try { SpecialFired?.Invoke(f); }
            catch (Exception e) { Debug.LogException(e); }
```

`Scripts/ColorHunt/Specials/SpecialsDemo.cs` (Task 6's version):

1. After `public event Action ScenarioFinished;`:
```csharp
        /// <summary>A scripted tap was made (its cell), whatever Tap returned — the recorder's "tap" marker.</summary>
        public event Action<int> Tapped;
        /// <summary>Whether the demo's rows and label show (the recorder hides them; the HUD stays).</summary>
        public bool UiVisible => _showUi;
```
2. After `private Text _label;`:
```csharp
        private Canvas _canvas;
        private bool _showUi = true;
```
3. After the method `SetLook`:
```csharp
        /// <summary>Shows or hides the demo's buttons and label (before the UI is built, the choice waits for it).</summary>
        public void ShowUi(bool on)
        {
            _showUi = on;
            if (_canvas != null) _canvas.enabled = on;
        }
```
4. In `Update`, replace
```csharp
                int cell = _taps[_next++];
                string r = Game.Tap(cell);
                if (r != "broken") Debug.LogWarning($"[SpecialsDemo] {Current}: the scripted tap on cell {cell} returned \"{r}\".");
```
with
```csharp
                int cell = _taps[_next++];
                string r = Game.Tap(cell);
                if (r != "broken") Debug.LogWarning($"[SpecialsDemo] {Current}: the scripted tap on cell {cell} returned \"{r}\".");
                try { Tapped?.Invoke(cell); }
                catch (Exception e) { Debug.LogException(e); }
```
5. In `BuildUi`, replace `canvas.sortingOrder = 20;                                         // over the HUD (0) and the popups (5)` with
```csharp
            canvas.sortingOrder = 20;                                         // over the HUD (0) and the popups (5)
            _canvas = canvas;
            canvas.enabled = _showUi;
```

- [ ] **Step 5: `FrameRecorder` adopts late canvases; `WavWriter`**

`Editor/FrameRecorder.cs`:
1. After `private readonly List<int> _orders = new List<int>();`:
```csharp
        /// <summary>With UI and no explicit list: look for overlay canvases created after the start at every capture.</summary>
        private readonly bool _scan;
```
2. In the constructor, replace everything from `_cam.targetTexture = _rt;` to the end of the constructor with:
```csharp
            _cam.targetTexture = _rt;
            _scan = canvases == null;
            Adopt(canvases ?? UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include));
        }

        /// <summary>Moves the root Screen Space Overlay canvases of <paramref name="list"/> onto the camera, above the world's effects.</summary>
        private void Adopt(IEnumerable<Canvas> list)
        {
            foreach (var c in list)
            {
                if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = _cam;
                c.planeDistance = _cam.nearClipPlane + 0.05f;
                _orders.Add(c.sortingOrder);
                c.sortingOrder += AboveWorld;
                _moved.Add(c);
            }
        }
```
(the loop is the constructor's former body, moved into `Adopt`).
3. First line of `Capture`:
```csharp
            if (_scan) Adopt(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include));   // a canvas built since (Royal's screen flash, a popup canvas)
```
A canvas this recorder moved is Screen Space Camera, so `Adopt` skips it; `Dispose` already restores every canvas in `_moved`.

`Editor/WavWriter.cs`:
```csharp
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// 16-bit PCM WAV files for the recorder's sounds: imported clips (Pota's samples, DecompressOnLoad + PCM) and clips synthesised at
    /// runtime (AudioClip.Create) alike, through AudioClip.GetData.
    /// </summary>
    public static class WavWriter
    {
        /// <summary>RIFF/WAVE, PCM 16-bit little-endian. <paramref name="interleaved"/>: samples in −1..1 (clamped), channels interleaved.</summary>
        public static byte[] Encode(float[] interleaved, int channels, int frequency)
        {
            int n = interleaved != null ? interleaved.Length : 0;
            using (var ms = new MemoryStream(44 + n * 2))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + n * 2);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)channels);
                w.Write(frequency);
                w.Write(frequency * channels * 2);
                w.Write((short)(channels * 2));
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(n * 2);
                for (int i = 0; i < n; i++) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(interleaved[i], -1f, 1f) * 32767f));
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Writes <paramref name="clip"/> as a WAV at <paramref name="path"/>; false with the reason when it cannot be read.</summary>
        public static bool TryWrite(AudioClip clip, string path, out string error)
        {
            error = null;
            if (clip == null) { error = "no clip"; return false; }
            try
            {
                if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData())
                {
                    error = $"{clip.name}: its audio data did not load (load type {clip.loadType})";
                    return false;
                }
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0))
                {
                    error = $"{clip.name}: GetData failed (load type {clip.loadType}; the recorder needs DecompressOnLoad)";
                    return false;
                }
                File.WriteAllBytes(path, Encode(data, clip.channels, clip.frequency));
                return true;
            }
            catch (Exception e)
            {
                error = $"{clip.name}: {e.Message}";
                return false;
            }
        }
    }
}
```

- [ ] **Step 6: The record tool — `Editor/ColorHuntRecordTool.cs`** (whole file; the ColorHunt scene's script path is unchanged, its
  close-ups now use fine steps like the Specials ones)

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
    /// Records Color Hunt frame by frame for the side-by-sides (paused editor, one capture per game frame): PNG frames with the HUD named by
    /// the recording clock, events.json with the moments as markers and every sound that played (PotaSounds' log, stamped with the same
    /// clock), and each played clip as a WAV. Scene "ColorHunt": a scripted round. Scene "Specials" (spec 2026-10-01 §4 "Kayıt"):
    /// SpecialsDemo stages a special-ball scenario in a special look and taps its own script; the demo and the game raise the markers.
    /// </summary>
    [McpForUnityTool("ai_colorhunt_record", Description =
        "Record Color Hunt in Play Mode as PNG frames with UI (frame-stepped), a sound log and the sounds as WAV. action='start' | 'status' | " +
        "'cancel'. scene 'ColorHunt' (default, ColorHunt.unity open): script (';'-separated: 'wait S', 'select C|auto', 'tap C|auto', " +
        "'rotate cw|ccw', 'settle'; default: idle, select, tap, settle, rotate, settle, tap, settle), seed (7), ammo (settings), win (false; " +
        "true: one red target block, the first red tap wins), tail (1.5 s). scene 'Specials' (Specials.unity open): scenario " +
        "(birth|line|bomb|chain|golden), special_look (pota|royal or 1|court or 2; default pota), until (the marker to stop at, default " +
        "'finish' = the demo's ScenarioFinished; e.g. 'tap2'), tail (0.6 s). Both: width (540), aspect (0.5625), look (pota|candy|shatter), " +
        "closeup (zoom on focus; steps of 0.3× fixedDeltaTime, play the frames back at meta.speed 0.3), zoom (2.4), focus (cell; default the " +
        "first tap's), name ('colorhunt'). Out: Temp/record/<name>/f_NNNN_tS.SSS.png, events.json {markers in s: tap, tap2…, rotate, win, " +
        "born…, fire…, land…, finish, end; meta {…, speed, step}; sounds: [{id, clip, t, volume, pitch, voice}]}, sounds/<clip>.wav.")]
    public static class ColorHuntRecordTool
    {
        public struct Step { public string Verb; public string Arg; public float Seconds; }

        /// <summary>What a start asks for: the ColorHunt scene's script, or a Specials scenario in a special look.</summary>
        public struct SpecialsRequest { public bool Specials; public SpecialScenario Scenario; public SpecialLook Look; }

        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("'ColorHunt' (default) or 'Specials' (the special-balls demo)", Required = false)] public string scene { get; set; }
            [ToolParameter("Specials: birth | line | bomb | chain | golden", Required = false)] public string scenario { get; set; }
            [ToolParameter("Specials: pota (default) | royal (1) | court (2)", Required = false)] public string special_look { get; set; }
            [ToolParameter("Specials: the marker to stop at, then the tail (default 'finish'; e.g. 'tap2')", Required = false)] public string until { get; set; }
            [ToolParameter("ColorHunt: script, ';'-separated", Required = false)] public string script { get; set; }
            [ToolParameter("ColorHunt: board seed (default 7)", Required = false)] public int? seed { get; set; }
            [ToolParameter("ColorHunt: ammo (default: settings)", Required = false)] public int? ammo { get; set; }
            [ToolParameter("ColorHunt: win scenario (default false)", Required = false)] public bool? win { get; set; }
            [ToolParameter("Frame width px (default 540)", Required = false)] public int? width { get; set; }
            [ToolParameter("Frame aspect w/h (default 0.5625)", Required = false)] public float? aspect { get; set; }
            [ToolParameter("Seconds after the script or the until marker (default 1.5; Specials 0.6)", Required = false)] public float? tail { get; set; }
            [ToolParameter("Folder under Temp/record (default 'colorhunt')", Required = false)] public string name { get; set; }
            [ToolParameter("pota | candy (a) | shatter (b); default: the game's current look", Required = false)] public string look { get; set; }
            [ToolParameter("zoom on the focus cell and record in 0.3× steps (played back slowed)", Required = false)] public bool? closeup { get; set; }
            [ToolParameter("Close-up zoom (default 2.4)", Required = false)] public float? zoom { get; set; }
            [ToolParameter("Close-up focus cell (default: the first tap's)", Required = false)] public int? focus { get; set; }
        }

        public const string DefaultScript = "wait 0.6; select auto; wait 0.3; tap auto; settle; wait 0.4; rotate cw; settle; wait 0.3; tap auto; settle";
        public const string WinScript = "wait 0.6; tap auto; wait 2.5";
        /// <summary>
        /// Close-up: zoom, and the playback speed its frames are made for — every step is CloseupTimeScale of a normal one (fixedDeltaTime),
        /// so game time, real-time effects and the looks' own slow motion all keep their ratios, only finer.
        /// </summary>
        public const float CloseupZoom = 2.4f, CloseupTimeScale = 0.3f;
        /// <summary>Specials: seconds recorded after the stop marker, and the longest a recording may run (recording clock).</summary>
        public const float SpecialsTail = 0.6f, SpecialsTimeout = 40f;
        private const string ScenarioList = "birth, line, bomb, chain, golden";

        private static PuzzleLook _previousLook;
        private static SpecialLook _previousSpecialLook;
        private static bool _closeup, _restoreLook, _specials;
        private static SpecialsRequest _request;
        private static SpecialsDemo _demo;
        private static ColorHuntGame _hooked;
        private static PotaSounds _sounds;
        private static string _until = "finish";
        private static float _zoom = CloseupZoom, _stopAt = -1f, _previousFixed = 0.02f, _lastShot = -1f;
        private static int _focus = -1, _landed;
        private static readonly List<float> _gaps = new List<float>();

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

        /// <summary>The scripted demo scenarios, any case ("free" has no script, so it is not one).</summary>
        public static bool TryScenario(string s, out SpecialScenario scenario)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "birth": scenario = SpecialScenario.Birth; return true;
                case "line": scenario = SpecialScenario.Line; return true;
                case "bomb": scenario = SpecialScenario.Bomb; return true;
                case "chain": scenario = SpecialScenario.Chain; return true;
                case "golden": scenario = SpecialScenario.Golden; return true;
                default: scenario = SpecialScenario.Birth; return false;
            }
        }

        /// <summary>"pota" | "royal" / "1" | "court" / "2", any case.</summary>
        public static bool TrySpecialLook(string s, out SpecialLook look)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "pota": look = SpecialLook.Pota; return true;
                case "royal": case "1": look = SpecialLook.Royal; return true;
                case "court": case "2": look = SpecialLook.Court; return true;
                default: look = SpecialLook.Pota; return false;
            }
        }

        /// <summary>
        /// Reads scene / scenario / special_look. Null when the request is fine, else the refusal (the tool returns it as the error).
        /// <paramref name="scripted"/>: a script or win was given (ColorHunt only: the Specials scene plays the scenario's own taps).
        /// </summary>
        public static string ParseRequest(string scene, string scenario, string specialLook, bool scripted, out SpecialsRequest request)
        {
            request = new SpecialsRequest { Look = SpecialLook.Pota };
            string sc = (scene ?? "").Trim();
            bool specials = sc.Equals("specials", StringComparison.OrdinalIgnoreCase);
            if (sc.Length > 0 && !specials && !sc.Equals("colorhunt", StringComparison.OrdinalIgnoreCase))
                return $"unknown scene '{scene}' (ColorHunt, Specials)";
            bool hasScenario = !string.IsNullOrWhiteSpace(scenario), hasLook = !string.IsNullOrWhiteSpace(specialLook);
            if (!specials)
                return hasScenario || hasLook ? "scenario and special_look need scene 'Specials' (Specials.unity open)" : null;
            if (scripted) return "script and win are for scene 'ColorHunt'; scene 'Specials' plays the scenario's own taps";
            if (!hasScenario) return $"scene 'Specials' needs a scenario ({ScenarioList})";
            if (!TryScenario(scenario, out var s)) return $"unknown scenario '{scenario}' ({ScenarioList}; 'free' has no script to record)";
            var look = SpecialLook.Pota;
            if (hasLook && !TrySpecialLook(specialLook, out look)) return $"unknown special_look '{specialLook}' (pota, royal/1, court/2)";
            request = new SpecialsRequest { Specials = true, Scenario = s, Look = look };
            return null;
        }

        /// <summary>The first "tap" is "tap", the next "tap2", "tap3"…</summary>
        public static string NextKey(ICollection<string> keys, string key)
        {
            if (!keys.Contains(key)) return key;
            for (int i = 2; ; i++)
                if (!keys.Contains(key + i)) return key + i;
        }

        /// <summary>A file name per distinct clip: its name with anything but ASCII letters, digits, '-' and '_' made '_'; a second clip of the same name gets "-2".</summary>
        public static Dictionary<AudioClip, string> ClipKeys(IEnumerable<AudioClip> clips)
        {
            var keys = new Dictionary<AudioClip, string>();
            var used = new HashSet<string>();
            foreach (var c in clips)
            {
                if (c == null || keys.ContainsKey(c)) continue;
                var sb = new StringBuilder();
                foreach (char ch in string.IsNullOrEmpty(c.name) ? "clip" : c.name)
                    sb.Append((char.IsLetterOrDigit(ch) && ch < 128) || ch == '-' || ch == '_' ? ch : '_');
                string key = sb.ToString(), k = key;
                for (int i = 2; !used.Add(k); i++) k = $"{key}-{i}";
                keys[c] = k;
            }
            return keys;
        }

        private static readonly HashSet<string> Verbs = new HashSet<string> { "wait", "select", "tap", "rotate", "settle" };
        private static List<Step> _steps;
        private static int _index, _stepFrames, _captured, _tailFrames, _width, _seed, _ammo, _lastFrame, _waits;
        private static float _aspect, _tail, _t0, _stepStart, _tailStart = -1f;
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

        /// <summary>The first tap's cell argument ("auto" when the script taps nothing): the close-up zooms on it.</summary>
        public static string FirstTap(List<Step> steps)
        {
            foreach (var s in steps) if (s.Verb == "tap") return string.IsNullOrEmpty(s.Arg) ? "auto" : s.Arg;
            return "auto";
        }

        public static object HandleCommand(JObject p)
        {
            string action = (AIToolsCommon.GetString(p, "action", "start") ?? "start").ToLowerInvariant();
            if (action == "status") return new SuccessResponse(_running ? "recording" : "idle", Status());
            if (action == "cancel") { Stop("cancelled"); return new SuccessResponse("cancelled", Status()); }
            if (!EditorApplication.isPlaying)
                return new ErrorResponse("Enter Play Mode first with ColorHunt.unity (scene 'ColorHunt') or Specials.unity (scene 'Specials') open.");
            if (_running) return new ErrorResponse("Already recording; poll status or cancel.");
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            if (game == null) return new ErrorResponse("No ColorHuntGame in the running scene.");
            _win = p["win"] != null && p["win"].Type == JTokenType.Boolean && p["win"].Value<bool>();
            string refusal = ParseRequest(AIToolsCommon.GetString(p, "scene", null), AIToolsCommon.GetString(p, "scenario", null),
                AIToolsCommon.GetString(p, "special_look", null), p["script"] != null || _win, out _request);
            if (refusal != null) return new ErrorResponse(refusal);
            _specials = _request.Specials;
            _demo = null;
            if (_specials)
            {
                _demo = Object.FindAnyObjectByType<SpecialsDemo>();
                if (_demo == null) return new ErrorResponse("No SpecialsDemo in the running scene: open Specials.unity (ai_specials_build_scene) and enter Play Mode.");
                _steps = new List<Step>();
            }
            else
            {
                try { _steps = Parse(AIToolsCommon.GetString(p, "script", _win ? WinScript : DefaultScript)); }
                catch (Exception e) { return new ErrorResponse(e.Message); }
            }
            string lookArg = AIToolsCommon.GetString(p, "look", null);
            if (!string.IsNullOrEmpty(lookArg) && !TryLook(lookArg, out _))
                return new ErrorResponse($"unknown look '{lookArg}' (pota, candy/a, shatter/b)");
            if (_specials)
            {
                _previousSpecialLook = game.SpecialLook;
                _demo.SetLook(_request.Look);
                if (_request.Look != SpecialLook.Pota && game.Specials is PotaSpecials)
                {
                    _demo.SetLook(_previousSpecialLook);
                    return new ErrorResponse($"The {_request.Look} special look fell back to Pota (missing assets: run ai_studio_import); nothing recorded.");
                }
            }
            _previousLook = game.Look;
            _restoreLook = true;
            if (!string.IsNullOrEmpty(lookArg) && TryLook(lookArg, out var look)) game.SetLook(look);
            _closeup = AIToolsCommon.GetBool(p, "closeup", false);
            // the unscaled base step: a dip of the demo's start-up scenario may be running (TimeScaleController sets fixedDeltaTime = base × scale)
            _previousFixed = Time.timeScale > 1e-3f ? Time.fixedDeltaTime / Time.timeScale : Time.fixedDeltaTime;
            _gaps.Clear();
            _lastShot = -1f;
            _zoom = Mathf.Clamp(AIToolsCommon.GetFloat(p, "zoom", CloseupZoom), 1f, 6f);
            _focus = AIToolsCommon.GetInt(p, "focus", -1);
            _until = (AIToolsCommon.GetString(p, "until", "finish") ?? "finish").Trim().ToLowerInvariant();
            _seed = AIToolsCommon.GetInt(p, "seed", 7);
            _ammo = AIToolsCommon.GetInt(p, "ammo", game.S.Ammo);
            _width = Mathf.Clamp(AIToolsCommon.GetInt(p, "width", 540), 120, 2160);
            _aspect = Mathf.Clamp(AIToolsCommon.GetFloat(p, "aspect", 0.5625f), 0.3f, 3f);
            _tail = Mathf.Max(0f, AIToolsCommon.GetFloat(p, "tail", _specials ? SpecialsTail : 1.5f));
            _dir = "Temp/record/" + (AIToolsCommon.GetString(p, "name", "colorhunt") ?? "colorhunt");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _index = 0; _stepFrames = 0; _captured = 0; _tailFrames = 0; _dealt = false; _result = ""; _lastFrame = -1; _waits = 0; _tailStart = -1f;
            _stopAt = -1f; _landed = 0;
            _events.Clear();
            Time.captureDeltaTime = 1f / 30f;
            _floating = FrameRecorder.OpenFloatingGameView(new Rect(1040f, 70f, 380f, 700f));
            EditorApplication.isPaused = true;
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            string what = _specials ? $"{_request.Scenario} in the {_request.Look} special look" : "Color Hunt";
            return new SuccessResponse($"Recording {what} to {_dir}.", Status());
        }

        private static void Stop(string why)
        {
            bool wasRunning = _running;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            float end = _dealt ? Now : 0f;
            Unhook();
            var log = _sounds != null ? _sounds.StopLog() : new List<SoundLogEntry>();
            _sounds = null;
            var meta = new JObject
            {
                ["scene"] = _specials ? "Specials" : "ColorHunt",
                ["look"] = game != null ? game.Look.ToString() : "",
                ["closeup"] = _closeup,
                ["speed"] = Math.Round(_closeup ? CloseupTimeScale : 1f, 3),   // seq_video plays the frames back at this speed
                ["step"] = Math.Round(LargestGap(), 4),                   // 0.02 (0.006 in a close-up); else the stepping changed
                ["result"] = why,
            };
            if (_specials)
            {
                meta["scenario"] = _request.Scenario.ToString().ToLowerInvariant();
                meta["special_look"] = _request.Look.ToString().ToLowerInvariant();
            }
            if (game != null && _restoreLook)
            {
                game.SetLook(_previousLook);
                if (_specials && _demo != null)
                {
                    _demo.SetLook(_previousSpecialLook);
                    _demo.ShowUi(true);
                }
                var fit = game.Cam != null ? game.Cam.GetComponent<ColorHuntCamera>() : null;
                if (fit != null) fit.Zoom = 1f;
                if (_closeup) game.Juice?.TimeCtl?.Clear();
            }
            if (_closeup) Time.fixedDeltaTime = _previousFixed;
            _closeup = false;
            _restoreLook = false;
            _running = false;
            _result = why;
            EditorApplication.update -= Tick;
            _recorder?.Dispose();
            _recorder = null;
            Time.captureDeltaTime = 0f;
            EditorApplication.isPaused = false;
            FrameRecorder.CloseFloatingGameView(_floating);
            _floating = null;
            if (wasRunning && Directory.Exists(_dir))
            {
                var json = new JObject();
                foreach (var kv in _events) json[kv.Key] = kv.Value;
                if (_dealt) json["end"] = end;
                json["meta"] = meta;
                json["sounds"] = WriteSounds(_dir, log);
                File.WriteAllText(Path.Combine(_dir, "events.json"), json.ToString());
            }
            _demo = null;
        }

        /// <summary>
        /// Recording time = unscaled time since the board was dealt. A stepped (paused) editor advances it by Time.fixedDeltaTime per step,
        /// which TimeScaleController scales with timeScale, so frames crowd in hit-stops and slow motion while game time keeps its ratio to
        /// this clock: every file name carries its own time, and the sound log uses this clock too.
        /// </summary>
        private static float Now => Time.unscaledTime - _t0;

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            if (game == null) { Stop("no ColorHuntGame"); return; }
            // EditorApplication.update runs more often than the stepped player loop: act once per new game frame, then step again.
            if (Time.frameCount == _lastFrame)
            {
                if (++_waits > 600) Stop("the player loop stopped stepping");
                return;
            }
            _waits = 0;
            _lastFrame = Time.frameCount;
            // A paused editor's Step advances unscaled time by fixedDeltaTime and game time by that × timeScale. A close-up holds the step at
            // CloseupTimeScale of a normal one before every step (TimeScaleController rewrites fixedDeltaTime when a look's slow motion or a
            // dip starts or ends), so every clock keeps its ratio and the frames come 0.006 s apart.
            if (_closeup) Time.fixedDeltaTime = _previousFixed * CloseupTimeScale;
            EditorApplication.Step();
            if (!_dealt)
            {
                if (game.BoardRoot == null) return;
                Deal(game);
                return;
            }
            Capture();
            if (_specials) { TickSpecials(game); return; }
            if (_index >= _steps.Count)
            {
                if (_tailStart < 0f) _tailStart = Now;
                _tailFrames++;
                if (Now - _tailStart >= _tail) Stop($"done: {_captured} frames, {Now:0.00} s");
                return;
            }
            var s = _steps[_index];
            bool next = true;
            switch (s.Verb)
            {
                case "wait":
                    if (_stepFrames++ == 0) _stepStart = Now;
                    next = Now - _stepStart >= s.Seconds;
                    break;
                case "select":
                    game.Select(Cell(game, s.Arg));
                    break;
                case "tap":
                    if (game.Intro) { next = false; break; }        // taps wait for the board intro
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

        /// <summary>The first stepped frame with a board: deal (ColorHunt) or stage the scenario (Specials), frame the close-up, start the clock and the sound log.</summary>
        private static void Deal(ColorHuntGame game)
        {
            int focus = _focus;
            if (_specials)
            {
                _demo.ShowUi(false);
                Hook(game);
                _demo.Play(_request.Scenario, true);           // stages the preset now (the board root exists); the demo taps its script
                var taps = SpecialScenarios.Taps(_request.Scenario);
                if (focus < 0 && taps.Length > 0) focus = taps[0];
                _landed = game.GoldenLanded;
            }
            else
            {
                var design = game.S.ToDesign();
                if (_win) design.Targets = new[] { (0, 1) };
                game.Prepare(_ammo, (uint)_seed, design);
                game.StartIntro();                                  // studio looks deal like NewRound
                game.Begin();
            }
            if (_closeup)
            {
                if (focus < 0) focus = Cell(game, FirstTap(_steps));
                var fit = game.Cam.GetComponent<ColorHuntCamera>();
                if (fit != null && focus >= 0) { fit.Zoom = _zoom; fit.ZoomFocus = game.CellWorld(focus); }
            }
            _recorder = new FrameRecorder(game.Cam, _width, _aspect, true);
            _t0 = Time.unscaledTime;
            _sounds = game.Juice != null ? game.Juice.Sounds : null;
            if (_sounds != null) _sounds.StartLog(() => Now);
            _dealt = true;
        }

        /// <summary>Specials: golden landings become markers; the recording ends <see cref="_tail"/> after the until marker (or "finish").</summary>
        private static void TickSpecials(ColorHuntGame game)
        {
            while (_landed < game.GoldenLanded) { _landed++; Mark("land"); }
            if (_stopAt < 0f)
            {
                float at = float.MaxValue;
                if (_events.TryGetValue(_until, out float u)) at = u;
                if (_events.TryGetValue("finish", out float f)) at = Mathf.Min(at, f);
                if (at < float.MaxValue) _stopAt = at + _tail;
            }
            if (_stopAt >= 0f && Now >= _stopAt) { Stop($"done: {_captured} frames, {Now:0.00} s"); return; }
            if (Now > SpecialsTimeout) Stop($"timeout: neither '{_until}' nor 'finish' within {SpecialsTimeout:0} s");
        }

        private static void Hook(ColorHuntGame game)
        {
            Unhook();
            _hooked = game;
            game.SpecialBorn += OnBorn;
            game.SpecialFired += OnFired;
            if (_demo == null) return;
            _demo.Tapped += OnTapped;
            _demo.ScenarioFinished += OnFinished;
        }

        private static void Unhook()
        {
            if (_hooked != null)
            {
                _hooked.SpecialBorn -= OnBorn;
                _hooked.SpecialFired -= OnFired;
                _hooked = null;
            }
            if (_demo == null) return;
            _demo.Tapped -= OnTapped;
            _demo.ScenarioFinished -= OnFinished;
        }

        private static void OnTapped(int cell) => Mark("tap");
        private static void OnBorn(SpecialKind kind, int cell) => Mark("born");
        private static void OnFired(SpecialFire fire) => Mark("fire");

        private static void OnFinished()
        {
            if (_running && _dealt && !_events.ContainsKey("finish")) _events["finish"] = Now;
        }

        private static void Mark(string key)
        {
            if (_running && _dealt) _events[NextKey(_events.Keys, key)] = Now;
        }

        /// <summary>A number, or "auto": the affordable block with the longest chain (win: the first affordable red block).</summary>
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

        private static void Capture()
        {
            float now = Now;
            if (_lastShot >= 0f) _gaps.Add(now - _lastShot);
            _lastShot = now;
            _recorder.Capture(Path.Combine(_dir, $"f_{_captured:0000}_t{now:0.000}.png"));
            _captured++;
        }

        /// <summary>
        /// The longest time between two frames (meta.step) — the step at timeScale 1: 0.02, or 0.006 in a close-up (dips and slow motion
        /// only make steps shorter, so the longest one is the base step).
        /// </summary>
        private static float LargestGap()
        {
            float most = 0f;
            foreach (float g in _gaps) most = Mathf.Max(most, g);
            return most;
        }

        /// <summary>events.json "sounds" in play order, and sounds/&lt;clip&gt;.wav once per distinct clip (synthesised ones included).</summary>
        private static JArray WriteSounds(string dir, List<SoundLogEntry> log)
        {
            var list = new JArray();
            if (log.Count == 0) return list;
            var keys = ClipKeys(log.ConvertAll(e => e.Clip));
            string folder = Path.Combine(dir, "sounds");
            Directory.CreateDirectory(folder);
            foreach (var kv in keys)
                if (!WavWriter.TryWrite(kv.Key, Path.Combine(folder, kv.Value + ".wav"), out string error))
                    Debug.LogWarning($"[ai_colorhunt_record] sound '{kv.Value}' not written: {error}");
            foreach (var e in log)
            {
                if (e.Clip == null) continue;
                list.Add(new JObject
                {
                    ["id"] = e.Id, ["clip"] = keys[e.Clip], ["t"] = Math.Round(e.T, 4), ["volume"] = Math.Round(e.Volume, 4),
                    ["pitch"] = Math.Round(e.Pitch, 4), ["voice"] = e.Voice,
                });
            }
            return list;
        }

        private static object Status() => new
        {
            running = _running, scene = _specials ? "Specials" : "ColorHunt", scenario = _specials ? _request.Scenario.ToString() : "",
            special_look = _specials ? _request.Look.ToString() : "", until = _until, step = _index, steps = _steps?.Count ?? 0,
            frames = _captured, dir = _dir, result = _result, events = _events, look = Object.FindAnyObjectByType<ColorHuntGame>()?.Look.ToString(),
            closeup = _closeup, gameTime = Time.time, frameCount = Time.frameCount, timeScale = Time.timeScale, captureDeltaTime = Time.captureDeltaTime,
        };
    }
}
```

- [ ] **Step 7: The phone-cost tool — `Editor/SpecialsCostTool.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Project.Editor.AITools
{
    /// <summary>
    /// The phone cost of the special looks (spec 2026-10-01 §5 "Telefon maliyeti"; CLAUDE.md §12: measure with the Mobile asset, then put
    /// the PC one back). In Play Mode with Specials.unity, swaps Mobile_RPAsset into QualitySettings.renderPipeline, sets the Game view to
    /// 1170×2532, plays a scenario (Chain: line → bomb → column, the heaviest moment) a few times per special look in real time and samples
    /// every frame: CPU main thread (minus the present wait), render thread and GPU from FrameTimingManager (the "Main Thread" profiler
    /// counter when frame timing is off), draw and SetPass calls from the Game view's stats, live particles + the style's pieces. Per run:
    /// the demo's idle beat before the tap (median) against the second after it (p95, max); runs combined by median, the live peak by max.
    /// Writes Temp/specials_cost.json. The pipeline asset, the demo UI and the special look come back at the end, on cancel, on leaving Play
    /// Mode and after a domain reload (SessionState).
    /// </summary>
    [McpForUnityTool("ai_specials_cost", Description =
        "Phone cost of the special looks (Play Mode, Specials.unity open, Unity in front): swaps the URP Mobile asset in, sets the Game " +
        "view to 1170x2532, plays a scenario per look a few times in real time and samples every frame (CPU main thread, render thread, " +
        "GPU; draw/SetPass calls; live particles + the style's pieces). action='start' {looks ('pota,royal,court'), runs (3), scenario " +
        "('chain')} | 'status' | 'cancel'. Restores the pipeline asset, the demo UI and the special look. Writes Temp/specials_cost.json.")]
    public static class SpecialsCostTool
    {
        public const string MobileAsset = "Assets/Settings/Mobile_RPAsset.asset";
        public const string OutPath = "Temp/specials_cost.json";
        public const int PhoneWidth = 1170, PhoneHeight = 2532;
        /// <summary>Idle: this long before the tap (the demo's 0.45-s beat on a settled board). Peak: this long from the tap. Then the tail after ScenarioFinished.</summary>
        public const float IdleWindow = 0.35f, PeakWindow = 1f, Tail = 0.3f, RunTimeout = 20f;
        private const string PendingKey = "Project.SpecialsCost.PreviousPipeline";

        public struct Sample
        {
            public float T, Cpu, Render, Gpu;            // s from the run's start; ms (≤ 0: not measured)
            public int Draws, SetPass, Live;
        }

        public struct Summary
        {
            public int Frames;
            public float IdleCpu, IdleGpu, PeakCpu, PeakGpu, MaxCpu, MaxGpu, PeakRender;   // ms; -1: not measured
            public int MaxDraws, MaxSetPass, MaxLive;
        }

        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("Special looks, comma-separated (default 'pota,royal,court')", Required = false)] public string looks { get; set; }
            [ToolParameter("Runs per look (default 3)", Required = false)] public int? runs { get; set; }
            [ToolParameter("Scenario (default 'chain': line → bomb → column, the heaviest moment)", Required = false)] public string scenario { get; set; }
        }

        public static float Median(List<float> values) => Percentile(values, 0.5f);

        /// <summary>Linear-interpolated percentile (p in 0..1); -1 for no values.</summary>
        public static float Percentile(List<float> values, float p)
        {
            if (values == null || values.Count == 0) return -1f;
            var s = new List<float>(values);
            s.Sort();
            float idx = Mathf.Clamp01(p) * (s.Count - 1);
            int lo = Mathf.FloorToInt(idx), hi = Mathf.Min(lo + 1, s.Count - 1);
            return Mathf.Lerp(s[lo], s[hi], idx - lo);
        }

        public static Summary Summarize(IList<Sample> samples, float tap)
        {
            var idleCpu = new List<float>();
            var idleGpu = new List<float>();
            var peakCpu = new List<float>();
            var peakGpu = new List<float>();
            var peakRender = new List<float>();
            var r = new Summary();
            foreach (var x in samples)
            {
                if (x.T >= tap - IdleWindow && x.T < tap)
                {
                    if (x.Cpu > 0f) idleCpu.Add(x.Cpu);
                    if (x.Gpu > 0f) idleGpu.Add(x.Gpu);
                }
                else if (x.T >= tap && x.T <= tap + PeakWindow)
                {
                    r.Frames++;
                    if (x.Cpu > 0f) peakCpu.Add(x.Cpu);
                    if (x.Gpu > 0f) peakGpu.Add(x.Gpu);
                    if (x.Render > 0f) peakRender.Add(x.Render);
                    r.MaxDraws = Mathf.Max(r.MaxDraws, x.Draws);
                    r.MaxSetPass = Mathf.Max(r.MaxSetPass, x.SetPass);
                    r.MaxLive = Mathf.Max(r.MaxLive, x.Live);
                }
            }
            r.IdleCpu = Median(idleCpu);
            r.IdleGpu = Median(idleGpu);
            r.PeakCpu = Percentile(peakCpu, 0.95f);
            r.PeakGpu = Percentile(peakGpu, 0.95f);
            r.PeakRender = Percentile(peakRender, 0.95f);
            r.MaxCpu = Percentile(peakCpu, 1f);
            r.MaxGpu = Percentile(peakGpu, 1f);
            return r;
        }

        /// <summary>Runs of one look: timings by median (ignoring unmeasured), counts by the worst run, frames summed.</summary>
        public static Summary Combine(IList<Summary> runs)
        {
            float M(Func<Summary, float> f)
            {
                var list = new List<float>();
                foreach (var run in runs) { float v = f(run); if (v >= 0f) list.Add(v); }
                return Median(list);
            }
            var c = new Summary
            {
                IdleCpu = M(x => x.IdleCpu), IdleGpu = M(x => x.IdleGpu), PeakCpu = M(x => x.PeakCpu), PeakGpu = M(x => x.PeakGpu),
                MaxCpu = M(x => x.MaxCpu), MaxGpu = M(x => x.MaxGpu), PeakRender = M(x => x.PeakRender),
            };
            foreach (var run in runs)
            {
                c.Frames += run.Frames;
                c.MaxDraws = Mathf.Max(c.MaxDraws, run.MaxDraws);
                c.MaxSetPass = Mathf.Max(c.MaxSetPass, run.MaxSetPass);
                c.MaxLive = Mathf.Max(c.MaxLive, run.MaxLive);
            }
            return c;
        }

        // ---------- the measurement ----------

        private static bool _running;
        private static string _result = "", _size = "";
        private static SpecialsDemo _demo;
        private static ColorHuntGame _game;
        private static readonly List<SpecialLook> _looks = new List<SpecialLook>();
        private static SpecialScenario _scenario = SpecialScenario.Chain;
        private static int _runs = 3, _lookIndex, _run, _lastFrame = -1, _refresh;
        private static float _runStart, _tap = -1f, _finished = -1f;
        private static SpecialLook _previousLook;
        private static ProfilerRecorder _mainThread;
        private static ParticleSystem[] _systems = new ParticleSystem[0];
        private static readonly FrameTiming[] _timing = new FrameTiming[1];
        private static readonly List<Sample> _samples = new List<Sample>();
        private static readonly Dictionary<SpecialLook, List<Summary>> _results = new Dictionary<SpecialLook, List<Summary>>();
        private static readonly List<string> _notes = new List<string>();
        private static PropertyInfo _draws, _setPass;

        public static object HandleCommand(JObject p)
        {
            string action = (AIToolsCommon.GetString(p, "action", "start") ?? "start").ToLowerInvariant();
            if (action == "status") return new SuccessResponse(_running ? "measuring" : "idle", Status());
            if (action == "cancel") { Finish("cancelled"); return new SuccessResponse("cancelled", Status()); }
            if (!EditorApplication.isPlaying) return new ErrorResponse("Enter Play Mode with Specials.unity open first.");
            if (_running) return new ErrorResponse("Already measuring; poll status or cancel.");
            _demo = Object.FindAnyObjectByType<SpecialsDemo>();
            if (_demo == null || _demo.Game == null) return new ErrorResponse("No SpecialsDemo in the running scene: open Specials.unity.");
            _game = _demo.Game;
            _looks.Clear();
            foreach (var part in (AIToolsCommon.GetString(p, "looks", "pota,royal,court") ?? "").Split(','))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                if (!ColorHuntRecordTool.TrySpecialLook(part, out var look)) return new ErrorResponse($"unknown special look '{part.Trim()}' (pota, royal/1, court/2)");
                _looks.Add(look);
            }
            if (_looks.Count == 0) return new ErrorResponse("no special look to measure");
            string scenario = AIToolsCommon.GetString(p, "scenario", "chain");
            if (!ColorHuntRecordTool.TryScenario(scenario, out _scenario)) return new ErrorResponse($"unknown scenario '{scenario}' (birth, line, bomb, chain, golden)");
            _runs = Mathf.Clamp(AIToolsCommon.GetInt(p, "runs", 3), 1, 10);
            var mobile = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(MobileAsset);
            if (mobile == null) return new ErrorResponse($"{MobileAsset} not found");
            var previous = QualitySettings.renderPipeline;
            SessionState.SetString(PendingKey, previous != null ? AssetDatabase.GetAssetPath(previous) : "");
            QualitySettings.renderPipeline = mobile;
            GameViewPortrait.TrySet(PhoneWidth, PhoneHeight, true, out _size);
            _previousLook = _game.SpecialLook;
            _demo.ShowUi(false);
            _demo.Tapped -= OnTapped;
            _demo.Tapped += OnTapped;
            _demo.ScenarioFinished -= OnFinished;
            _demo.ScenarioFinished += OnFinished;
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            var stats = typeof(EditorWindow).Assembly.GetType("UnityEditor.UnityStats");
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            _draws = stats?.GetProperty("drawCalls", Any);
            _setPass = stats?.GetProperty("setPassCalls", Any);
            _results.Clear();
            _notes.Clear();
            _lookIndex = 0;
            _run = 0;
            _result = "";
            EditorApplication.isPaused = false;
            _running = true;
            BeginRun();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            return new SuccessResponse($"Measuring {_scenario} ×{_runs} in {string.Join(", ", _looks)} with {mobile.name} at {_size}.", Status());
        }

        private static void BeginRun()
        {
            var look = _looks[_lookIndex];
            if (_run == 0)
            {
                _demo.SetLook(look);
                if (look != SpecialLook.Pota && _game.Specials is PotaSpecials)
                    _notes.Add($"{look} fell back to Pota (missing assets): its numbers are Pota's");
            }
            _samples.Clear();
            _tap = -1f;
            _finished = -1f;
            _lastFrame = Time.frameCount;
            _systems = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude);
            _runStart = Time.unscaledTime;
            _demo.Play(_scenario, true);
        }

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying || _demo == null) { Finish("play mode ended"); return; }
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;
            float t = Time.unscaledTime - _runStart;
            _samples.Add(Measure(t));
            bool timedOut = t > RunTimeout;
            if ((_finished >= 0f && t - _finished >= Tail) || timedOut) EndRun(timedOut);
        }

        private static Sample Measure(float t)
        {
            var s = new Sample { T = t, Cpu = -1f, Render = -1f, Gpu = -1f };
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
            {
                var f = _timing[0];
                s.Cpu = (float)(f.cpuMainThreadFrameTime - f.cpuMainThreadPresentWaitTime);
                s.Render = (float)f.cpuRenderThreadFrameTime;
                s.Gpu = (float)f.gpuFrameTime;
            }
            if (s.Cpu <= 0f && _mainThread.Valid && _mainThread.LastValue > 0) s.Cpu = _mainThread.LastValue * 1e-6f;
            if (++_refresh % 10 == 0) _systems = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude);
            int live = _game != null ? _game.Specials.LiveCount : 0;
            foreach (var ps in _systems) if (ps != null) live += ps.particleCount;
            s.Live = live;
            s.Draws = Stat(_draws);
            s.SetPass = Stat(_setPass);
            return s;
        }

        private static int Stat(PropertyInfo p)
        {
            try { return p != null ? Convert.ToInt32(p.GetValue(null)) : -1; }
            catch (Exception) { return -1; }
        }

        private static void EndRun(bool timedOut)
        {
            var look = _looks[_lookIndex];
            if (!_results.TryGetValue(look, out var list)) _results[look] = list = new List<Summary>();
            if (_tap < 0f || timedOut) _notes.Add($"{look} run {_run + 1}: {(_tap < 0f ? "no tap" : "timed out")}, left out");
            else list.Add(Summarize(_samples, _tap));
            if (++_run < _runs) { BeginRun(); return; }
            _run = 0;
            if (++_lookIndex < _looks.Count) { BeginRun(); return; }
            Finish("done");
        }

        private static void OnTapped(int cell)
        {
            if (_running && _tap < 0f) _tap = Time.unscaledTime - _runStart;
        }

        private static void OnFinished()
        {
            if (_running) _finished = Time.unscaledTime - _runStart;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Finish("play mode ended");
        }

        private static void Finish(string why)
        {
            bool wasRunning = _running;
            _running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            RestorePipeline();
            if (_demo != null)
            {
                _demo.Tapped -= OnTapped;
                _demo.ScenarioFinished -= OnFinished;
                if (EditorApplication.isPlaying) _demo.SetLook(_previousLook);
                _demo.ShowUi(true);
            }
            if (_mainThread.Valid) _mainThread.Dispose();
            _result = why;
            if (wasRunning) Write(why);
        }

        private static void RestorePipeline()
        {
            string pending = SessionState.GetString(PendingKey, null);
            if (pending == null) return;
            QualitySettings.renderPipeline = pending.Length > 0 ? AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pending) : null;
            SessionState.EraseString(PendingKey);
        }

        /// <summary>A domain reload in the middle of a measurement must not leave the Mobile asset in the quality settings.</summary>
        [InitializeOnLoadMethod]
        private static void RestoreAfterReload()
        {
            if (SessionState.GetString(PendingKey, null) == null) return;
            Debug.LogWarning("[ai_specials_cost] a measurement was cut by a domain reload: the render pipeline asset is put back.");
            RestorePipeline();
        }

        private static void Write(string why)
        {
            var looks = new JObject();
            var combined = new Dictionary<SpecialLook, Summary>();
            foreach (var look in _looks)
            {
                if (!_results.TryGetValue(look, out var runs) || runs.Count == 0 || combined.ContainsKey(look)) continue;
                var c = Combine(runs);
                combined[look] = c;
                looks[look.ToString()] = ToJson(c, runs.Count);
            }
            if (combined.TryGetValue(SpecialLook.Pota, out var pota))
                foreach (var kv in combined)
                {
                    if (kv.Key == SpecialLook.Pota) continue;
                    ((JObject)looks[kv.Key.ToString()])["vs_pota"] = new JObject
                    {
                        ["peak_gpu_ms"] = Diff(kv.Value.PeakGpu, pota.PeakGpu),
                        ["peak_cpu_ms"] = Diff(kv.Value.PeakCpu, pota.PeakCpu),
                        ["max_live_ratio"] = pota.MaxLive > 0 ? new JValue(Math.Round((double)kv.Value.MaxLive / pota.MaxLive, 2)) : JValue.CreateNull(),
                    };
                }
            var json = new JObject
            {
                ["result"] = why, ["scenario"] = _scenario.ToString(), ["runs"] = _runs,
                ["pipeline"] = Path.GetFileNameWithoutExtension(MobileAsset), ["game_view"] = _size,
                ["frame_timing"] = FrameTimingManager.IsFeatureEnabled(), ["looks"] = looks, ["notes"] = new JArray(_notes.ToArray()),
            };
            File.WriteAllText(OutPath, json.ToString());
            AIToolsCommon.Log($"[ai_specials_cost] {why}: {OutPath}");
        }

        private static JToken Diff(float a, float b) => a < 0f || b < 0f ? JValue.CreateNull() : new JValue(Math.Round(a - b, 3));

        private static JObject ToJson(Summary s, int runs) => new JObject
        {
            ["runs"] = runs, ["frames"] = s.Frames,
            ["idle_cpu_ms"] = Math.Round(s.IdleCpu, 3), ["peak_cpu_ms"] = Math.Round(s.PeakCpu, 3), ["max_cpu_ms"] = Math.Round(s.MaxCpu, 3),
            ["idle_gpu_ms"] = Math.Round(s.IdleGpu, 3), ["peak_gpu_ms"] = Math.Round(s.PeakGpu, 3), ["max_gpu_ms"] = Math.Round(s.MaxGpu, 3),
            ["peak_render_ms"] = Math.Round(s.PeakRender, 3),
            ["max_draw_calls"] = s.MaxDraws, ["max_setpass_calls"] = s.MaxSetPass, ["max_live"] = s.MaxLive,
        };

        private static object Status() => new
        {
            running = _running, look = _lookIndex < _looks.Count ? _looks[_lookIndex].ToString() : "", run = _run, runs = _runs,
            samples = _samples.Count, result = _result, output = OutPath,
        };
    }
}
```

- [ ] **Step 8: Compile and run the Unity tests**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntRecordToolTests Project.Tests.PotaSoundsTests Project.Tests.WavWriterTests Project.Tests.SpecialsCostToolTests Project.Tests.FrameRecorderTests Project.Tests.RecordableClockTests
~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.SpecialsRecordPlayTests Project.Tests.SpecialsDemoPlayTests --no-compile
```
Expected: the first compiles cleanly and prints `EditMode 28/28` (record tool 9, sounds 6, WAV 3, cost 4, frame recorder 5, clock 1; a
`RecordableClockTests` failure names the file and line of a Royal/Court wall-clock read: change it to `Time.unscaledTime`); the second
`PlayMode 9/9` (the three new, Task 6's six). A failing `Bomb_RaisesTapsAndFires…` with fires in another order or a missing `patlama` means
Task 5's `FireSpecial`/`PotaSpecials.Fire` differs from the draft: read it and fix the hook placement, not the test.

- [ ] **Step 9: The Python side — `compare.py specials`, `side_video.py groups`, `seq_video.py`**

`Tools/potalook/compare.py`:
1. Append to the module docstring (before the closing `"""`):
```text

Special balls (spec 2026-10-01): uv run --with pillow Tools/potalook/compare.py specials OUT_PREFIX SCENARIO LABEL=DIR [LABEL=DIR ...]
writes OUT_PREFIX-SCENARIO.jpg: one row per Unity dir (the Pota look is a Unity row; no three.js video), one column per SPECIAL_MOMENTS
time after the scenario's anchor marker (birth: born, golden: land, the rest: tap; the sheet labels them +1 s).
```
2. After `ANCHORS = {"chain": "tap"}`:
```python
# special balls (spec 2026-10-01 §2): (anchor marker, seconds after it). Line/bomb: the squash or wind-up, the sweep or the blast, the
# drops; chain: the three fires within 0.6 s; birth: the gather (Royal/Court) into the pop; golden: the gate, the arc, the landing.
SPECIAL_MOMENTS = {
    "birth": ("born", [-0.25, -0.12, 0.0, 0.06, 0.15, 0.3, 0.5]),
    "line": ("tap", [0.0, 0.08, 0.17, 0.26, 0.36, 0.5, 0.75]),
    "bomb": ("tap", [0.0, 0.08, 0.17, 0.26, 0.36, 0.5, 0.75]),
    "chain": ("tap", [0.0, 0.1, 0.2, 0.3, 0.42, 0.55, 0.8]),
    "golden": ("land", [-0.8, -0.5, -0.25, 0.0, 0.1, 0.25, 0.5]),
}


def special_moment(scenario):
    """(anchor, times) of a special-ball scenario; ValueError for any other name."""
    if scenario not in SPECIAL_MOMENTS:
        raise ValueError(f"unknown scenario '{scenario}' ({', '.join(SPECIAL_MOMENTS)})")
    return SPECIAL_MOMENTS[scenario]
```
3. Before `def main(argv):`:
```python
def specials_main(argv):
    """compare.py specials OUT_PREFIX SCENARIO LABEL=DIR [LABEL=DIR ...] → OUT_PREFIX-SCENARIO.jpg (rows = dirs, columns = moments)."""
    if len(argv) < 3:
        sys.exit(specials_main.__doc__)
    prefix, scenario = argv[:2]
    anchor, times = special_moment(scenario)
    rows = parse_rows(argv[2:])
    work = tempfile.mkdtemp(prefix="specials-")
    try:
        columns = []
        for i, (label, folder) in enumerate(rows):
            ev = json.load(open(os.path.join(folder, "events.json")))
            if anchor not in ev:
                marks = sorted(k for k, v in ev.items() if isinstance(v, (int, float)))
                raise ValueError(f"{folder}: no '{anchor}' marker in events.json (has {marks})")
            columns.append(f"{label}={aligned(frame_sheet.load_frames(folder), ev[anchor], os.path.join(work, f'row{i}'))}")
        out = f"{prefix}-{scenario}.jpg"
        frame_sheet.main([out, *columns, "--times", ",".join(f"{t + WINDOW:.4f}" for t in times), "--height", "400"])
        return out
    finally:
        shutil.rmtree(work, ignore_errors=True)
```
4. First lines of `main`:
```python
def main(argv):
    if argv and argv[0] == "specials":
        specials_main(argv[1:])
        return
    if len(argv) < 4:
        sys.exit(__doc__)
```
(the rest of `main` unchanged).

`Tools/potalook/side_video.py` (whole file; the Pota | Unity mode is unchanged):
```python
#!/usr/bin/env python3
"""Looks side by side in one video.

Pota (three.js) | Unity: every column is cut around the same events (tap, rotate, …) and the cuts are joined, so the columns' moments
line up even though their boards differ; a label strip sits on top.
Usage: uv run --with pillow Tools/potalook/side_video.py OUT.mp4 --pota VIDEO EVENTS --unity LABEL=DIR [LABEL=DIR ...]
       [--segments tap:-1:3.4,rotate:-0.5:2.5] [--width 540] [--height 960]

Groups (Unity only, silent; the special balls' Pota | 1 | 2 per scenario): one recording per column and group, groups one after another.
Usage: uv run --with pillow Tools/potalook/side_video.py groups OUT.mp4 --group TITLE LABEL=DIR [LABEL=DIR ...] [--group ...]
       [--anchor tap] [--before 0.6] [--width 540] [--height H] [--crf 28]
Each group's columns are aligned at their own anchor marker and run until the longest one ends (a shorter one holds its last frame).

Unity DIRs hold ai_colorhunt_record frames and events.json; the frames are named by the recording clock (the time as felt: they crowd
together in hit-stops and slow motion), so each column is resampled to one frame per 1/30 s of that clock.
"""
import argparse
import functools
import json
import math
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
FPS = 30
HEAD_H = 56
BG = (16, 22, 34)
GOLD = (255, 200, 61)
FONT = os.path.join(os.path.dirname(os.path.dirname(HERE)), "Assets/_Project/Games/Pota/Art/Fonts/LilitaOne-Regular.ttf")


def resample(frames, fps):
    """[(path, t), …] sorted by t → one path per 1/fps tick from 0 to the last frame: the latest frame at or before each tick."""
    out, k = [], 0
    last = frames[-1][1]
    i = 0
    while i / fps <= last + 1e-6:
        tick = i / fps + 1e-6
        while k + 1 < len(frames) and frames[k + 1][1] <= tick:
            k += 1
        out.append(frames[k][0])
        i += 1
    return out


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
        if start < -1e-3:
            raise ValueError(f"the {name} window starts {-start:.2f} s before the recording")
        out.append((max(0.0, start), events[name] + after))           # events are floats: 0.999999 − 1 is the start
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


def header_image(labels, width):
    """A HEAD_H-tall strip, one centred gold label per column of `width`."""
    strip = Image.new("RGB", (width * len(labels), HEAD_H), BG)
    draw = ImageDraw.Draw(strip)
    font = ImageFont.truetype(FONT, 34)
    for i, text in enumerate(labels):
        w = draw.textlength(text, font=font)
        draw.text((i * width + (width - w) / 2, 10), text, font=font, fill=GOLD)
    return strip


def header(labels, width, out):
    header_image(labels, width).save(out)


def index_at(t, count, fps=FPS):
    """The tick of a resampled recording that shows its time t (clamped to the recording)."""
    return max(0, min(count - 1, int(math.floor(t * fps + 1e-6))))


def window(anchors, ends, before):
    """(start, end) relative to each column's anchor: `before` s ahead of the anchors to the latest column's end."""
    return -before, max(e - a for a, e in zip(anchors, ends))


def parse_groups(groups):
    """[["Bomba", "Pota=a", "1 · Royal=b"], …] → [("Bomba", [("Pota", "a"), ("1 · Royal", "b")]), …]."""
    out = []
    for g in groups:
        if len(g) < 2:
            raise ValueError(f"a group needs a title and at least one LABEL=DIR, got {g}")
        out.append((g[0], compare.parse_rows(g[1:])))
    return out


@functools.lru_cache(maxsize=24)
def scaled(path, width, height):
    img = Image.open(path).convert("RGB")
    return img if img.size == (width, height) else img.resize((width, height), Image.LANCZOS)


def group_frames(title, columns, width, height, before, anchor="tap"):
    """One group: columns [(label, dir)] aligned at their own `anchor` marker, each resampled at 30 fps, under a "title · label" strip."""
    ticks, anchors, ends = [], [], []
    for _, folder in columns:
        events = json.load(open(os.path.join(folder, "events.json")))
        if anchor not in events:
            raise ValueError(f"{folder}: no '{anchor}' marker in events.json")
        t = resample(compare.frame_sheet.load_frames(folder), FPS)
        ticks.append(t)
        anchors.append(float(events[anchor]))
        ends.append((len(t) - 1) / FPS)
    start, end = window(anchors, ends, before)
    head = header_image([f"{title} · {label}" for label, _ in columns], width)
    for j in range(int(round((end - start) * FPS)) + 1):
        r = start + j / FPS
        frame = Image.new("RGB", (width * len(columns), HEAD_H + height), BG)
        frame.paste(head, (0, 0))
        for c, t in enumerate(ticks):
            frame.paste(scaled(t[index_at(anchors[c] + r, len(t))], width, height), (c * width, HEAD_H))
        yield frame


def encode(frames, size, out, audio=None, crf=28):
    """Pipes PIL frames (all of `size`, even sides) to ffmpeg as raw RGB at 30 fps; muxes `audio` (a WAV) when given."""
    cmd = [FFMPEG, "-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24",
           "-s", f"{size[0]}x{size[1]}", "-framerate", str(FPS), "-i", "-"]
    if audio:
        cmd += ["-i", audio]
    cmd += ["-c:v", "libx264", "-crf", str(crf), "-preset", "slow", "-pix_fmt", "yuv420p"]
    cmd += ["-c:a", "aac", "-b:a", "128k", "-shortest"] if audio else ["-an"]
    cmd += ["-movflags", "+faststart", out]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    try:
        for img in frames:
            proc.stdin.write(img.tobytes())
    finally:
        proc.stdin.close()
    if proc.wait() != 0:
        raise RuntimeError(f"ffmpeg failed for {out}")


def groups_main(argv):
    ap = argparse.ArgumentParser(prog="side_video.py groups", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("--group", nargs="+", action="append", required=True, metavar="TITLE LABEL=DIR")
    ap.add_argument("--anchor", default="tap")
    ap.add_argument("--before", type=float, default=0.6)
    ap.add_argument("--width", type=int, default=540)
    ap.add_argument("--height", type=int)
    ap.add_argument("--crf", type=int, default=28)
    a = ap.parse_args(argv)
    groups = parse_groups(a.group)
    if len({len(cols) for _, cols in groups}) != 1:
        raise ValueError("every group needs the same number of columns")
    if a.height is None:
        first = Image.open(compare.frame_sheet.load_frames(groups[0][1][0][1])[0][0])
        a.height = round(first.height * a.width / first.width)
    a.width -= a.width % 2
    a.height -= a.height % 2
    size = (a.width * len(groups[0][1]), HEAD_H + a.height)

    def frames():
        for title, columns in groups:
            yield from group_frames(title, columns, a.width, a.height, a.before, a.anchor)

    encode(frames(), size, a.out, crf=a.crf)
    print(a.out)


def main(argv):
    if argv and argv[0] == "groups":
        groups_main(argv[1:])
        return
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
        for c, (_, folder) in enumerate(rows):
            columns.append(trims(json.load(open(os.path.join(folder, "events.json"))), segments))
            steady = os.path.join(work, f"col{c}")
            os.makedirs(steady)
            for i, path in enumerate(resample(compare.frame_sheet.load_frames(folder), FPS)):
                os.symlink(os.path.abspath(path), os.path.join(steady, f"s_{i:05d}.png"))
            inputs += ["-framerate", str(FPS), "-i", os.path.join(steady, "s_%05d.png")]
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

`Tools/potalook/seq_video.py`:
```python
#!/usr/bin/env python3
"""Recordings one after another in one voiced video (spec 2026-10-01 §1.4: every scenario as Pota, then 1, then 2).

Usage: uv run --with pillow --with numpy Tools/potalook/seq_video.py OUT.mp4 "TITLE|LOOK=DIR" ["TITLE|LOOK=DIR" ...]
       [--card 0.7] [--silent] [--speed S] [--crf 28] [--sync-sheet PREFIX]
Each DIR is an ai_colorhunt_record folder: frames f_NNNN_tS.SSS.png named by the recording clock, events.json with "sounds"
[{id, clip, t, volume, pitch, voice}], "meta" and sounds/<clip>.wav. Each segment opens with a title card (TITLE, LOOK under it) and
shows its frames under a label strip, resampled to 30 fps by the clock in their names (side_video.resample), so slow motion and
hit-stops play as felt; a close-up (meta.speed 0.3, or --speed) is played back that much slower. The sounds are mixed in numpy into one
48 kHz stereo track at their logged times — pitch by resampling, volume, PotaSounds' eight voices (a sound stops when its voice is
taken) — and a segment's sounds fade out during the next card; ffmpeg only encodes and muxes.
Why not ffmpeg adelay + amix: a video carries a few hundred timed sounds; amix scales every input by the input count unless told not to,
pitch needs asetrate + aresample per input, and a graph with hundreds of inputs is slow to build and hard to debug.
--sync-sheet PREFIX writes PREFIX-<NN>-<scenario>-<look>.jpg per segment: for each special sound, the frames one tick before, at and after.
"""
import argparse
import json
import os
import re
import shutil
import sys
import tempfile
import wave
from dataclasses import dataclass, field

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import compare  # noqa: E402
import frame_sheet  # noqa: E402
import side_video  # noqa: E402

FFMPEG = side_video.FFMPEG
FPS = side_video.FPS
STRIP_H = side_video.HEAD_H
BG = side_video.BG
GOLD = side_video.GOLD
RATE = 48000
PEAK = 0.95            # the mix is lowered as a whole when its sum would pass this
VOICE_FADE = 0.005     # a taken voice stops at once (a 5-ms ramp against the click)
WINDOW_FADE = 0.25     # a segment's sounds fade out during the next title card
# routine sounds the sync sheet leaves out: taps, ball hits, normal bursts, combos, drops, rotations, stickers, round ends
SYNC_SKIP = {"dokun", "kuleAt", "carpma", "kirildi", "kombo", "dustu", "dondur", "eksi", "topla", "tamam", "kazandi", "kaybetti"}


@dataclass
class Segment:
    title: str
    look: str
    folder: str
    speed: float = 1.0                           # playback speed: 0.3 plays a close-up's 0.01 s of clock per video frame
    ticks: list = field(default_factory=list)    # one frame path per 1/FPS s of video
    sounds: list = field(default_factory=list)   # events.json "sounds"
    meta: dict = field(default_factory=dict)     # events.json "meta"

    @property
    def label(self):
        return f"{self.title} · {self.look}" if self.look else self.title


@dataclass
class Slot:
    card: float    # the title card starts
    body: float    # the first frame
    end: float     # after the last frame


@dataclass
class MixEvent:
    at: float      # output seconds
    path: str      # the WAV
    volume: float
    pitch: float
    stop: float    # output seconds where it is cut (its voice is taken, or its segment's window ends)
    fade: float    # fade-out length when cut


def even(n):
    return n - n % 2


def parse_segments(args):
    """["Bomba|Pota=Temp/record/a", …] → [Segment("Bomba", "Pota", "Temp/record/a"), …] in order."""
    out = []
    for label, folder in compare.parse_rows(args):
        title, _, look = label.partition("|")
        out.append(Segment(title.strip(), look.strip(), folder))
    return out


def ticks(folder, fps=FPS, speed=1.0):
    """One frame per video tick: every speed/fps s of the recording clock."""
    return side_video.resample(frame_sheet.load_frames(folder), fps / speed)


def load(seg, speed=None):
    """Reads the segment's events.json; speed: given, else the recording's meta.speed, else 1."""
    path = os.path.join(seg.folder, "events.json")
    events = json.load(open(path)) if os.path.exists(path) else {}
    seg.sounds = events.get("sounds", [])
    seg.meta = events.get("meta", {})
    seg.speed = float(speed or seg.meta.get("speed") or 1.0)
    seg.ticks = ticks(seg.folder, FPS, seg.speed)
    return seg


def layout(durations, card):
    """Each segment: `card` s of title card, then its frames. → ([Slot…], total seconds)."""
    slots, t = [], 0.0
    for d in durations:
        slots.append(Slot(t, t + card, t + card + d))
        t += card + d
    return slots, t


def mix_plan(segments, slots, total):
    """Every logged sound at its segment's first frame + its time (÷ speed); cut where its voice plays the next sound, else at the next card's end."""
    events = []
    for k, (seg, slot) in enumerate(zip(segments, slots)):
        window = slots[k + 1].body if k + 1 < len(slots) else total
        ordered = sorted(seg.sounds, key=lambda s: float(s["t"]))
        for i, s in enumerate(ordered):
            stop, fade = window, WINDOW_FADE
            voice = s.get("voice", -1)
            if voice is not None and voice >= 0:
                for later in ordered[i + 1:]:
                    if later.get("voice", -1) == voice:
                        if slot.body + float(later["t"]) / seg.speed < stop:
                            stop, fade = slot.body + float(later["t"]) / seg.speed, VOICE_FADE
                        break
            events.append(MixEvent(slot.body + float(s["t"]) / seg.speed, os.path.join(seg.folder, "sounds", s["clip"] + ".wav"),
                                   float(s.get("volume", 1.0)), float(s.get("pitch", 1.0)), stop, fade))
    return events


def read_wav(path):
    with wave.open(path, "rb") as w:
        channels, width, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        if width != 2:
            raise ValueError(f"{path}: {8 * width}-bit, expected 16-bit PCM")
        data = np.frombuffer(w.readframes(n), dtype="<i2").astype(np.float32) / 32768.0
    return data.reshape(-1, channels), rate


def write_wav(path, samples, rate=RATE):
    pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).round().astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(samples.shape[1])
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())


def render_mix(events, total, rate=RATE):
    """One stereo float track of `total` s → (track, gain); gain < 1 when the sum had to come down to PEAK."""
    n_total = int(round(total * rate))
    out = np.zeros((n_total, 2), np.float32)
    cache = {}
    for e in events:
        if not os.path.exists(e.path):
            print(f"missing sound {e.path}: skipped")
            continue
        if e.path not in cache:
            cache[e.path] = read_wav(e.path)
        data, sr = cache[e.path]
        if data.shape[1] == 1:
            data = np.repeat(data, 2, axis=1)
        elif data.shape[1] > 2:
            data = data[:, :2]
        if len(data) == 0:
            continue
        step = e.pitch * sr / rate                      # AudioSource.pitch p plays the clip p× faster
        count = int((len(data) - 1) / step) + 1
        start = max(0, int(round(e.at * rate)))
        stop = min(int(round(e.stop * rate)), n_total)
        length = min(count, stop - start)
        if length <= 0:
            continue
        pos = np.arange(length) * step
        src = np.arange(len(data))
        chunk = np.stack([np.interp(pos, src, data[:, c]) for c in (0, 1)], axis=1) * e.volume
        if length < count:                              # cut: fade instead of clicking
            f = min(length, max(1, int(round(e.fade * rate))))
            chunk[-f:] *= np.linspace(1.0, 0.0, f)[:, None]
        out[start:start + length] += chunk
    peak = float(np.abs(out).max()) if n_total else 0.0
    gain = min(1.0, PEAK / peak) if peak > 0 else 1.0
    return out * gain, gain


def font(size):
    return ImageFont.truetype(side_video.FONT, size)


def strip(width, text):
    img = Image.new("RGB", (width, STRIP_H), BG)
    d = ImageDraw.Draw(img)
    f = font(34)
    d.text(((width - d.textlength(text, font=f)) / 2, 10), text, font=f, fill=GOLD)
    return img


def card(width, height, title, look):
    img = Image.new("RGB", (width, height), BG)
    d = ImageDraw.Draw(img)
    y = height * 0.38
    for text, f, color, dy in ((title, font(96), GOLD, 0), (look, font(52), (240, 240, 240), 120)):
        if text:
            d.text(((width - d.textlength(text, font=f)) / 2, y + dy), text, font=f, fill=color)
    return img


def video_frames(segments, size, card_ticks):
    """Each segment: its title card for card_ticks frames, then every tick's frame under the label strip."""
    width, height = size
    body = height - STRIP_H
    for seg in segments:
        title_card = card(width, height, seg.title, seg.look)
        for _ in range(card_ticks):
            yield title_card
        head = strip(width, seg.label)
        last, frame = None, None
        for path in seg.ticks:
            if path != last:
                frame = Image.new("RGB", size, BG)
                frame.paste(head, (0, 0))
                frame.paste(side_video.scaled(path, width, body), (0, STRIP_H))
                last = path
            yield frame


def slug(text):
    table = str.maketrans("çğıöşüÇĞİÖŞÜ", "cgiosuCGIOSU")
    return re.sub(r"[^a-z0-9]+", "-", text.translate(table).lower()).strip("-") or "segment"


def sync_rows(segments):
    """[(segment, sound, [tick before, tick at, tick after])] for every sound that is not routine."""
    rows = []
    for seg in segments:
        n = len(seg.ticks)
        for s in seg.sounds:
            if s.get("id") in SYNC_SKIP or n == 0:
                continue
            i = side_video.index_at(float(s["t"]) / seg.speed, n)
            rows.append((seg, s, [seg.ticks[max(0, i - 1)], seg.ticks[i], seg.ticks[min(n - 1, i + 1)]]))
    return rows


def sync_sheet(rows, out, height=320):
    first = Image.open(rows[0][2][0])
    w = round(first.width * height / first.height)
    label_w = 420
    sheet = Image.new("RGB", (label_w + 3 * w, 44 + height * len(rows)), BG)
    d = ImageDraw.Draw(sheet)
    f = font(26)
    for c, name in enumerate(("-1 kare", "ses anı", "+1 kare")):
        d.text((label_w + c * w + 10, 8), name, font=f, fill=(240, 240, 240))
    for r, (seg, s, paths) in enumerate(rows):
        y = 44 + r * height
        d.text((12, y + height // 2 - 34), seg.label, font=f, fill=GOLD)
        d.text((12, y + height // 2 + 2), f"{s['id']} @ {float(s['t']):.3f} s", font=f, fill=(240, 240, 240))
        for c, p in enumerate(paths):
            sheet.paste(side_video.scaled(p, w, height), (label_w + c * w, y))
    sheet.save(out, quality=85)


def sync_sheets(segments, prefix):
    written = []
    for k, seg in enumerate(segments):
        rows = sync_rows([seg])
        if not rows:
            continue
        path = f"{prefix}-{k:02d}-{seg.meta.get('scenario') or slug(seg.title)}-{slug(seg.look)}.jpg"
        sync_sheet(rows, path)
        written.append(path)
    return written


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("out")
    ap.add_argument("segments", nargs="+", metavar="TITLE|LOOK=DIR")
    ap.add_argument("--card", type=float, default=0.7, help="title card seconds before each segment (0: none)")
    ap.add_argument("--silent", action="store_true")
    ap.add_argument("--speed", type=float, help="playback speed of every segment (default: each recording's meta.speed, else 1)")
    ap.add_argument("--crf", type=int, default=28)
    ap.add_argument("--sync-sheet", metavar="PREFIX")
    a = ap.parse_args(argv)
    segments = [load(s, a.speed) for s in parse_segments(a.segments)]
    first = Image.open(segments[0].ticks[0])
    size = (even(first.width), even(first.height + STRIP_H))
    card_ticks = max(0, int(round(a.card * FPS)))
    slots, total = layout([len(s.ticks) / FPS for s in segments], card_ticks / FPS)
    work = tempfile.mkdtemp(prefix="seq-")
    try:
        audio = None
        if not a.silent:
            plan = mix_plan(segments, slots, total)
            track, gain = render_mix(plan, total)
            audio = os.path.join(work, "mix.wav")
            write_wav(audio, track)
            print(f"mix: {len(plan)} sounds over {total:.2f} s, gain {gain:.2f}")
        side_video.encode(video_frames(segments, size, card_ticks), size, a.out, audio=audio, crf=a.crf)
        for seg, slot in zip(segments, slots):
            print(f"{slot.body:7.2f} s  {seg.label}  ({len(seg.ticks)} frames, {len(seg.sounds)} sounds)")
        if a.sync_sheet:
            for path in sync_sheets(segments, a.sync_sheet):
                print(path)
        print(a.out)
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main(sys.argv[1:])
```

- [ ] **Step 10: Run the Python tests**

Run: `~/.local/bin/uv run --with pytest --with pillow --with numpy pytest -q Tools/potalook Tools/test_frame_sheet.py`
Expected: all pass — `test_compare.py` 9, `test_side_video.py` 11, `test_seq_video.py` 15, `test_frame_sheet.py` unchanged. A failing
`test_main_builds_a_voiced_video…` with `Unknown encoder` means the ffmpeg build lost libx264/aac: check
`/opt/homebrew/bin/ffmpeg -hide_banner -encoders | grep -E "libx264|aac"` (both were there on 2026-10-01).

- [ ] **Step 11: The scripts — `record_unity.sh` (whole file) and `record_specials.sh`**

`Tools/potalook/record_unity.sh`:
```bash
#!/bin/zsh
# Records Color Hunt with ai_colorhunt_record: opens the scene the parameters name ("scene": ColorHunt (default) or Specials; refuses if
# the open scene has unsaved changes), enters Play, records, leaves Play, prints the markers and the clips that played.
# Usage: Tools/potalook/record_unity.sh NAME ['{"look":"candy","win":true,…}' | '{"scene":"Specials","scenario":"bomb","special_look":"royal"}']
#   → Temp/record/NAME/ (frames, events.json, sounds/*.wav)
set -u
cd "${0:A:h:h:h}"
name=$1
extra="${2:-}"
[[ -z "$extra" ]] && extra='{}'
scene=$(python3 -c '
import json, sys
s = (json.loads(sys.argv[1]).get("scene") or "ColorHunt").lower()
if s not in ("colorhunt", "specials"):
    sys.exit(f"unknown scene {s!r} (ColorHunt, Specials)")
print("Specials" if s == "specials" else "ColorHunt")
' "$extra") || exit 1
M() { ~/.local/bin/uv run Tools/mcp_call.py "$@" 2>&1; }
open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
tmp=$(mktemp -d)
python3 - "$tmp/open.json" "$scene" <<'EOF'
import json, sys
path = f"Assets/_Project/Games/Pota/Scenes/{sys.argv[2]}.unity"
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        f'if (s.path != "{path}") {{ if (s.isDirty) return "dirty: " + s.path; '
        f'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{path}"); }} '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
M call execute_code @"$tmp/open.json" | grep '"result"' | grep -q "$scene" || { echo "could not open $scene (unsaved scene?)"; exit 1; }
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
python3 - "Temp/record/$name/events.json" <<'EOF'
import json, sys
try:
    ev = json.load(open(sys.argv[1]))
except (OSError, ValueError) as e:
    sys.exit(f"no events: {e}")
marks = {k: round(v, 3) for k, v in ev.items() if isinstance(v, (int, float)) and not isinstance(v, bool)}
sounds = ev.get("sounds", [])
print(json.dumps(marks), "|", ev.get("meta", {}).get("result", ""), "|", len(sounds), "sounds:", " ".join(sorted({s["clip"] for s in sounds})))
EOF
rm -rf "$tmp"
```

`Tools/potalook/record_specials.sh`:
```bash
#!/bin/zsh
# Special balls (spec 2026-10-01 §1.4, plan Task 12): records the five scenarios in the three special looks and the Royal/Court close-ups
# with record_unity.sh, then builds the per-scenario moment sheets, the silent Pota | 1 | 2 side video, the voiced sequential video (each
# scenario as Pota → 1 → 2) and the close-up videos into docs/screenshots/2026-10-01-specials-*; 'cost' measures the phone cost.
# Usage: Tools/potalook/record_specials.sh [all|record|build|cost]
#   LOOKS="royal" SCENARIOS="bomb line" CLOSEUPS=0 Tools/potalook/record_specials.sh record   → re-record a subset after a fix
#   CRF=30 Tools/potalook/record_specials.sh build                                            → smaller videos
set -u
cd "${0:A:h:h:h}"
stage=${1:-all}
LOOKS=(${=LOOKS:-pota royal court})
SCENARIOS=(${=SCENARIOS:-birth line bomb chain golden})
CLOSEUPS=${CLOSEUPS:-1}
CRF=${CRF:-28}
ASPECT=0.4623                         # PotaProfile 1170×2532 → 540 × 1168 frames
UV=~/.local/bin/uv
R=Temp/record
OUT=docs/screenshots/2026-10-01-specials
ALL=(birth line bomb chain golden)
CLOSE=(royal court)
typeset -A TITLE LOOK
TITLE=(birth "Doğuş" line "Çizgi" bomb "Bomba" chain "Zincir" golden "Altın")
LOOK=(pota "Pota" royal "1 · Royal" court "2 · Basketbol")
M() { $UV run Tools/mcp_call.py "$@" 2>&1; }

recorded() {   # NAME → exit 0 when the recording ended with "done"
  python3 - "$R/$1/events.json" <<'EOF'
import json, sys
try:
    ev = json.load(open(sys.argv[1]))
except (OSError, ValueError) as e:
    sys.exit(f"{sys.argv[1]}: {e}")
result = ev.get("meta", {}).get("result", "")
sys.exit(0 if result.startswith("done") else f"{sys.argv[1]}: {result or 'no result'}")
EOF
}

record() {     # NAME JSON
  Tools/potalook/record_unity.sh "$1" "$2" && recorded "$1" || { echo "recording $1 failed"; exit 1; }
}

if [[ $stage == all || $stage == record ]]; then
  df -h . | tail -1
  for s in $SCENARIOS; do
    for l in $LOOKS; do
      record sp-$s-$l "{\"scene\":\"Specials\",\"scenario\":\"$s\",\"special_look\":\"$l\",\"aspect\":$ASPECT}"
    done
  done
  if [[ $CLOSEUPS == 1 ]]; then
    for l in ${LOOKS:*CLOSE}; do
      [[ ${SCENARIOS[(Ie)bomb]} -gt 0 ]] && record sp-closeup-$l-bomb \
        "{\"scene\":\"Specials\",\"scenario\":\"bomb\",\"special_look\":\"$l\",\"aspect\":$ASPECT,\"closeup\":true,\"until\":\"tap2\",\"tail\":0}"
      [[ ${SCENARIOS[(Ie)line]} -gt 0 ]] && record sp-closeup-$l-line \
        "{\"scene\":\"Specials\",\"scenario\":\"line\",\"special_look\":\"$l\",\"aspect\":$ASPECT,\"closeup\":true,\"zoom\":1.5,\"focus\":24,\"until\":\"tap2\",\"tail\":0}"
    done
  fi
  echo "all recorded"
fi

if [[ $stage == all || $stage == build ]]; then
  for s in $ALL; do for l in pota royal court; do
    [[ -f $R/sp-$s-$l/events.json ]] || { echo "missing $R/sp-$s-$l: record it first"; exit 1; }
  done; done
  for s in $ALL; do
    $UV run -q --with pillow Tools/potalook/compare.py specials $R/specials $s \
      "Pota=$R/sp-$s-pota" "1 Royal=$R/sp-$s-royal" "2 Court=$R/sp-$s-court" || exit 1
    Tools/keep_screenshot.sh $R/specials-$s.jpg 2026-10-01-specials-sheet-$s || exit 1
  done
  groups=()
  for s in $ALL; do
    groups+=(--group "${TITLE[$s]}" "Pota=$R/sp-$s-pota" "1 · Royal=$R/sp-$s-royal" "2 · Basketbol=$R/sp-$s-court")
  done
  $UV run -q --with pillow Tools/potalook/side_video.py groups $OUT-side.mp4 "${groups[@]}" --crf $CRF || exit 1
  segs=()
  for s in $ALL; do for l in pota royal court; do segs+=("${TITLE[$s]}|${LOOK[$l]}=$R/sp-$s-$l"); done; done
  $UV run -q --with pillow --with numpy Tools/potalook/seq_video.py $OUT-voiced.mp4 "${segs[@]}" --crf $CRF \
    --sync-sheet $R/specials-sync || exit 1
  for l in $CLOSE; do
    for s in bomb line; do
      [[ -f $R/sp-closeup-$l-$s/events.json ]] || { echo "missing close-up $l $s"; exit 1; }
      $UV run -q --with pillow --with numpy Tools/potalook/seq_video.py $OUT-closeup-$l-$s.mp4 \
        "${TITLE[$s]} ×0,3|${LOOK[$l]}=$R/sp-closeup-$l-$s" --silent --card 0 --crf 26 || exit 1
    done
  done
  ls -la $OUT-*
fi

if [[ $stage == cost ]]; then
  open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app
  M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
  before=$(git status --short ProjectSettings)
  tmp=$(mktemp -d)
  python3 - "$tmp/open.json" <<'EOF'
import json, sys
path = "Assets/_Project/Games/Pota/Scenes/Specials.unity"
code = ('var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(); '
        f'if (s.path != "{path}") {{ if (s.isDirty) return "dirty: " + s.path; '
        f'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("{path}"); }} '
        'return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name;')
json.dump({"action": "execute", "code": code, "safety_checks": True}, open(sys.argv[1], "w"))
EOF
  M call execute_code @"$tmp/open.json" | grep '"result"' | grep -q Specials || { echo "could not open Specials (unsaved scene?)"; exit 1; }
  M call manage_editor '{"action":"play"}' > /dev/null
  for i in {1..30}; do sleep 2; M read mcpforunity://editor/state | grep -q '"is_playing": true' && break; done; sleep 4
  open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app     # real time: Unity stays in front with the Game view showing
  M call execute_custom_tool '{"tool_name":"ai_specials_cost","parameters":{"looks":"pota,royal,court","runs":3,"scenario":"chain"}}' | grep '"message"' | head -1
  for i in {1..100}; do
    sleep 3
    r=$(M call execute_custom_tool '{"tool_name":"ai_specials_cost","parameters":{"action":"status"}}' | grep '"result"' | head -1)
    echo "$r" | grep -q '"result": "[a-z]' && { echo "$r"; break; }
  done
  M call manage_editor '{"action":"stop"}' > /dev/null; sleep 3
  rm -rf "$tmp"
  cat Temp/specials_cost.json
  after=$(git status --short ProjectSettings)
  [[ "$before" == "$after" ]] || { echo "ProjectSettings changed during the measurement:"; echo "$after"; exit 1; }
fi
```
Run: `chmod +x Tools/potalook/record_specials.sh Tools/potalook/record_unity.sh && zsh -n Tools/potalook/record_specials.sh && zsh -n Tools/potalook/record_unity.sh`
Expected: no output (both parse).

- [ ] **Step 12: Commit the tools**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
P=Assets/_Project/Games/Pota
git add $P/Scripts/ColorHunt/PotaSounds.cs $P/Scripts/ColorHunt/ColorHuntGame.cs $P/Scripts/ColorHunt/Specials/SpecialsDemo.cs \
  $P/Editor/ColorHuntRecordTool.cs $P/Editor/FrameRecorder.cs $P/Editor/WavWriter.cs* $P/Editor/SpecialsCostTool.cs* \
  $P/Tests/EditMode/ColorHuntRecordToolTests.cs $P/Tests/EditMode/PotaSoundsTests.cs $P/Tests/EditMode/WavWriterTests.cs* \
  $P/Tests/EditMode/SpecialsCostToolTests.cs* $P/Tests/EditMode/FrameRecorderTests.cs $P/Tests/EditMode/RecordableClockTests.cs* \
  $P/Tests/PlayMode/SpecialsRecordPlayTests.cs* \
  Tools/potalook/compare.py Tools/potalook/test_compare.py Tools/potalook/side_video.py Tools/potalook/test_side_video.py \
  Tools/potalook/seq_video.py Tools/potalook/test_seq_video.py Tools/potalook/record_unity.sh Tools/potalook/record_specials.sh
git status --short    # nothing else from this task should be left
git commit -m "feat: record the special balls — Specials scenarios in ai_colorhunt_record, sound log with WAVs, fine-step close-ups, late canvases in FrameRecorder, voiced sequential video, Pota | 1 | 2 groups, moment sheets, phone-cost tool

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 13: Smoke — one recording end to end**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S=/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad
Tools/potalook/record_unity.sh sp-smoke '{"scene":"Specials","scenario":"bomb","special_look":"pota","aspect":0.4623}'
ls Temp/record/sp-smoke/sounds
~/.local/bin/uv run -q --with pillow --with numpy Tools/potalook/seq_video.py $S/smoke.mp4 "Bomba|Pota=Temp/record/sp-smoke" --sync-sheet $S/smoke-sync
/opt/homebrew/bin/ffprobe -v error -show_entries stream=codec_type,width,height,duration -of csv=p=0 $S/smoke.mp4
Tools/potalook/record_unity.sh sp-smoke-cu '{"scene":"Specials","scenario":"bomb","special_look":"pota","aspect":0.4623,"closeup":true,"until":"tap2","tail":0}'
python3 -c 'import json; m = [json.load(open(f"Temp/record/{n}/events.json"))["meta"] for n in ("sp-smoke", "sp-smoke-cu")]; print([(x["speed"], x["step"]) for x in m])'
~/.local/bin/uv run -q --with pillow --with numpy Tools/potalook/seq_video.py $S/smoke-cu.mp4 "Bomba ×0,3|Pota=Temp/record/sp-smoke-cu" --silent --card 0
~/.local/bin/uv run Tools/mcp_call.py call read_console '{"types":["error","warning"],"count":20}'
```
Expected: the tool answers `Recording Bomb in the Pota special look to Temp/record/sp-smoke.`, the status ends `done: N frames, …`, and the
summary line reads like `{"tap": 0.5…, "fire": 0.6…, "tap2": …, "fire2": …, "finish": …, "end": …} | done: … | K sounds: bomba …`
— `fire` within ~0.1 s after `tap` (Pota fires at the first peak, ChainTime·0.55 = 0.083 s), `fire2` likewise after `tap2`, `end` =
`finish` + 0.6. `sounds/` holds `bomba.wav` and the routine clips (kombo, dustu…); seq_video prints `mix: K sounds …, gain …` and ffprobe
lists `video,540,1224,…` and `audio,,,…` with durations within 0.1 s. Read `$S/smoke-sync-00-bomb-pota.jpg`: both `patlama` rows show
the bomb's orange flash in the "ses anı" or "+1 kare" thumbnail. The meta line prints `[(1.0, 0.02), (0.3, 0.006)]`: the normal
recording steps 0.02 s, the close-up 0.006 s (rule 2's model of the stepped editor holds); if the close-up shows 0.02, Step ignores
`fixedDeltaTime` on this Unity — then frames come 0.02 apart and `smoke-cu.mp4` stutters (each frame held ~2 ticks): fall back to
`SlowMo(CloseupTimeScale, 3600)` re-applied every tick for close-ups and note in the ledger that real-time effects run unslowed in
close-ups. Open three frames of `smoke-cu.mp4` (25/50/75 %): zoomed on the middle bomb, its flash and puff in slow motion. No new errors
in the console; the warnings may only be known noise (`FSTimeGet`, `UAC0005`). If the markers, sounds or sync are off, fix it now (the
tool or the hooks) before recording everything.

- [ ] **Step 14: Record everything and build the deliverables**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S=/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad
Tools/potalook/record_specials.sh record > $S/record-specials.log 2>&1     # run in the background (~40 min); Unity in front
tail -5 $S/record-specials.log                                              # until it ends with "all recorded"
Tools/potalook/record_specials.sh build
```
Expected: 15 `done` summaries + 4 close-ups, then `all recorded` (a `recording … failed` line stops the script: read that recording's
`meta.result` and the console, fix, re-run with `LOOKS=… SCENARIOS=…`). `build` prints five `docs/screenshots/2026-10-01-specials-sheet-*.jpg
(… KB)` lines, `docs/screenshots/2026-10-01-specials-side.mp4`, the mix line and 15 segment lines (`  0.70 s  Doğuş · Pota  (…)` …) with
the sync sheet paths, four close-up paths, and the `ls`. Size budget: sheets ≤ 400 KB each, side ≤ 8 MB, voiced ≤ 12 MB, each close-up
≤ 3 MB; over budget → `CRF=30 Tools/potalook/record_specials.sh build`.

- [ ] **Step 15: Check the markers, the sounds and the audio track**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
python3 - <<'EOF'
import json, os
# sound ids each recording must contain at least (spec §2 "Sesler"; Task 5's Pota ids, Task 10's Royal synths, Task 11's Court ids;
# if Task 10/11 as built use other ids, align this table with their code first and say so in the ledger)
want = {
    "pota": {"birth": {"ozelDogdu"}, "line": {"cizgi"}, "bomb": {"patlama"}, "chain": {"cizgi", "patlama"}, "golden": {"altinInis"}},
    "royal": {"birth": {"ozelDogdu"}, "line": {"cizgi", "roket"}, "bomb": {"patlama", "fitil"}, "chain": {"cizgi", "patlama"},
              "golden": {"altinInis", "para"}},
    "court": {"birth": {"ozelDogdu"}, "line": {"courtPas", "swish"}, "bomb": {"smac"}, "chain": {"kalabalik"}, "golden": {"swish"}},
}
for s in ("birth", "line", "bomb", "chain", "golden"):
    for look in ("pota", "royal", "court"):
        d = f"Temp/record/sp-{s}-{look}"
        ev = json.load(open(f"{d}/events.json"))
        marks = {k: v for k, v in ev.items() if isinstance(v, (int, float)) and not isinstance(v, bool)}
        clips = {x["id"] for x in ev["sounds"]}
        nowav = sorted({x["clip"] for x in ev["sounds"] if not os.path.exists(f"{d}/sounds/{x['clip']}.wav")})
        taps = sorted(v for k, v in marks.items() if k.startswith("tap"))
        late = [round(f - max(t for t in taps if t <= f), 3) for k, f in marks.items()
                if k.startswith("fire") and any(t <= f for t in taps) and f - max(t for t in taps if t <= f) > 0.6 + 1 / 30]
        missing = want[look].get(s, set()) - clips
        print(f"{s:6} {look:5} " + " ".join(f"{k}={v:.2f}" for k, v in sorted(marks.items(), key=lambda kv: kv[1]))
              + f" | {len(ev['sounds'])} sounds" + (f"  MISSING {sorted(missing)}" if missing else "")
              + (f"  NO WAV {nowav}" if nowav else "") + (f"  LATE FIRES {late}" if late else ""))
EOF
/opt/homebrew/bin/ffprobe -v error -show_entries stream=codec_type,duration -of csv=p=0 docs/screenshots/2026-10-01-specials-voiced.mp4
/opt/homebrew/bin/ffmpeg -hide_banner -i docs/screenshots/2026-10-01-specials-voiced.mp4 -vn -af volumedetect -f null - 2>&1 | grep -E "mean_volume|max_volume"
```
Expected: 15 lines, none with `MISSING`, `NO WAV` (every clip, the runtime ones — Royal's `roket`/`fitil`/`para`, Court's `court-pas`,
`kalabalik-kisa` — written from `AudioClip.GetData`) or `LATE FIRES` (every fire ≤ 0.6 s + one frame after its tap — the Global Constraint); births
(`born`) in Birth, two `land` in Golden, three `fire` in Chain; video and audio durations within 0.1 s; `max_volume` between −12 and
−0.5 dB and `mean_volume` above −40 dB (the mix line's gain ≥ 0.5; lower means a pile-up worth a look). A `MISSING` id means that look
does not play the sound the spec gives it, or plays it through something other than `PotaSounds.PlayClip`: a defect for Step 17.

- [ ] **Step 16: Look**

Read (Read tool) the five sheets `docs/screenshots/2026-10-01-specials-sheet-{birth,line,bomb,chain,golden}.jpg` (columns are anchor + t,
labelled t + 1 s), every `Temp/record/specials-sync-*.jpg`, and four frames of each close-up:
```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
S=/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/1e912194-e731-4cf5-8037-0bef4c9cbae1/scratchpad
for f in docs/screenshots/2026-10-01-specials-closeup-*.mp4 docs/screenshots/2026-10-01-specials-side.mp4; do
  d=$(/opt/homebrew/bin/ffprobe -v error -show_entries format=duration -of csv=p=0 $f)
  for k in 0.2 0.4 0.6 0.8; do
    /opt/homebrew/bin/ffmpeg -hide_banner -loglevel error -y -ss $(python3 -c "print($d*$k)") -i $f -frames:v 1 "$S/look-$(basename $f .mp4)-$k.png"
  done
done
ls $S/look-*.png
```
Check each against spec §2 and write every finding as `D<n>: <what> — <where (sheet/column, sync row, close-up frame)>`:
- **D1 rocket/cell sync** (Royal and Court line, chain): a cell bursts as the rocket/ball reaches it — never before it, never more than a
  cell behind; the far cell bursts when it arrives; Royal's rocket leaves the frame with its small flash, Court's ball hits the frame with
  the white swish ring.
- **D2 fire size** (Royal bomb): the fireball at its largest ≈ 3.2 cells across (count the blocks it covers in the close-up), ink edge
  visible, gone by ≈ 0.45 s; it covers neither the HUD nor the far edge of the board.
- **D3 letters cut by the frame:** "ÇİZGİ TOPU!", "BOMBA!", "ALTIN TOP!" (all looks) and Court's "SLAM!", "COMBO!", "SWISH!" are whole
  inside the 540 × 1168 frame at their largest, Turkish İ/Ç/Ş drawn right, not under a HUD panel.
- **D4 sound sync within one frame:** every sync-sheet row shows its event (flash, launch, impact, landing, net) in "ses anı" or "+1
  kare"; an event only in "-1 kare" means the sound is late, one beyond "+1" means it is early.
- **D5 golden landing visible:** in the Golden sheet's `land` column (1.0 s) the ball is in the hoop on screen with its flash (Royal:
  coins; Court: the net wave + "SWISH!"), not below the frame or behind the HUD.
- **D6 chain on arrival:** in the Chain sheet the bomb bursts when the rocket/ball reaches it and the column fires when the blast reaches
  it (Royal/Court); Pota fires all three at once.
- **D7 normal bursts unchanged:** the Birth sheet's ordinary blocks burst with Pota's flash/puff in all three rows (spec §1.5).
- **D8 nothing broken:** no pink or black squares, no effect drawn behind the board, no particle over HUD text, Court's letters over
  the particles and under the HUD, no frame where the board is unreadable, gate rings on the frame and not turning with the board.
- **D9 audio:** Step 15 clean; no segment's sound runs into the next segment's frames (fades end in the card).
- **D10 screen-wide moments:** Royal's 2+ special white screen flash (`RoyalScreenFlash`) shows in the Chain sheet/side video (a frame
  without it where the slow motion starts means its canvas was not moved onto the camera); Royal's axis shake and Court's dunk camera
  push move the whole frame, the HUD steady.

- [ ] **Step 17: Fix, re-record, re-check — at most three rounds**

For each defect: change the owning number or line (Royal: `RoyalSpecials.cs` / its `ColorHuntSettings` fields; Court: `CourtSpecials.cs`;
sync: where the style calls `PotaSounds.PlayClip`; letters: the popup/lettering placement; budget: the pool caps), run that look's PlayMode
fixture from Task 10/11 (e.g. `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.RoyalPlayTests`), re-record only what changed
(`LOOKS="royal" SCENARIOS="line chain" Tools/potalook/record_specials.sh record`; close-ups follow with the same `LOOKS`/`SCENARIOS`), then
`Tools/potalook/record_specials.sh build` and repeat Steps 15–16 for the affected rows. Per fix, a ledger line
`Task 12: Ruling: <what> <old → new> — <what the sheet/close-up showed> — <cost if wrong>` and a commit:
```bash
git add <the changed files>
git commit -m "fix: <look> special — <what changed> (seen in the 2026-10-01 specials sheets)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
After the third round stop. Every defect still visible goes to the ledger as `Task 12: Open: D<n> …` for Task 13 to put in the backlog as a
U item (file, problem, proposal, test) and into the report to Tuna. Never call the videos finished while a listed defect is visible
without saying so.

- [ ] **Step 18: Phone cost**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
Tools/potalook/record_specials.sh cost
```
Do not touch the Mac while it runs (~1 min: 3 looks × 3 Chain runs in real time). Expected: the tool's `Measuring Chain ×3 in Pota,
Royal, Court with Mobile_RPAsset at …1170x2532…`, a status with `"result": "done"`, then `Temp/specials_cost.json` with `"result": "done"`,
`"pipeline": "Mobile_RPAsset"`, `"notes": []` and per look `idle_*`, `peak_*`, `max_*`, `max_draw_calls` > 0, `max_live`, and for Royal and
Court a `vs_pota` block; the script ends without "ProjectSettings changed". Then:
- `max_draw_calls` 0 → the Game view did not render (hidden tab or Unity behind another window): bring it up and run `cost` again.
- `"frame_timing": false` → GPU columns are `-1`: note "GPU not measured (Frame Timing Stats off in the editor)" and use the CPU numbers;
  do not turn the Player setting on for this.
- `max_live_ratio` > 1.5 or `max_live` > 1026 for Royal/Court → a BurstBudget defect: back to Step 17.
- a `vs_pota.peak_gpu_ms` above ~1 ms → note it as a phone risk for the backlog (Task 13), as for B's shock ring on 2026-09-30.
Write the ledger line Task 13 copies into the README "Ölçüm" bullet:
`Task 12: Measured: Mobile_RPAsset, Game view 1170×2532, Chain ×3 per look (editor Metal, not a phone) — GPU idle/peak p95: Pota a/b ms,
Royal c/d ms (+x vs Pota), Court e/f ms (+y); CPU main peak p95: …; draw calls max …; live peak P/R/C (ratio r/c).`
`git status --short ProjectSettings` → as before the run (the measurement restores the pipeline asset).

- [ ] **Step 19: Full suites and the deliverables commit**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run Tools/unity_tests.py EditMode
~/.local/bin/uv run Tools/unity_tests.py PlayMode --no-compile
~/.local/bin/uv run --with pytest --with pillow --with numpy pytest -q Tools/potalook Tools/test_frame_sheet.py
git add docs/screenshots/2026-10-01-specials-sheet-*.jpg docs/screenshots/2026-10-01-specials-side.mp4 \
  docs/screenshots/2026-10-01-specials-voiced.mp4 docs/screenshots/2026-10-01-specials-closeup-*.mp4
git status --short    # only files of this task; nothing under ProjectSettings
git commit -m "docs: special balls — Pota | 1 | 2 moment sheets, silent side video, voiced sequential video, Royal and Court close-ups

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Expected: both Unity suites pass (note the counts in the ledger for Task 13's CLAUDE.md line); pytest all green; one commit with the 5
sheets and 6 videos. `Temp/record/sp-*` (several GB of PNG frames) stays out of git; delete those folders only after Tuna has seen the
videos, in case a cut needs rebuilding.

---

### Task 13: Docs, backlog, memory

**Files:**
- Modify: `Assets/_Project/Games/Pota/README.md` (new section "Renk Avı: özel toplar ve efekt adayları")
- Modify: `CLAUDE.md` §12 (date line, AITools list: `ai_specials_build_scene`, record tool params, test counts, general lessons learned during Tasks 1–12)
- Modify: `/Users/tunaaltin17/projects/Blender MCP/docs/studyo.md` (roadmap item 4 "efekt hattı": second step done), `/Users/tunaaltin17/projects/Blender MCP/CLAUDE.md` (new recipes/tools), `/Users/tunaaltin17/projects/Blender MCP/docs/backlog.md` (U25 narrowed: specials and gates done; obstacles, boosters, bonus, special targets, HUD golden counter remain; new U items for anything deferred in Tasks 1–12)
- Create: memory file `/Users/tunaaltin17/.claude/projects/-Users-tunaaltin17-projects-Unity-MCP/memory/specials-fx-2026-10-01.md` + pointer line in `MEMORY.md`

**Interfaces:**
- Consumes: everything Tasks 1–12 produced (names, tool params, deliverable paths, test counts).
- Produces: nothing for code.

- [ ] **Step 1: README section**

Append to `Assets/_Project/Games/Pota/README.md` a section with this structure (fill each bullet with the facts as built; Turkish, like the rest of the README):

```markdown
## Renk Avı: özel toplar ve efekt adayları (2026-10-01)

Tuna'nın isteği: Pota'nın özel toplarına (çizgi, bomba, altın) Royal Match tarzı efektler, Blender + Unity ile iki aday, Tuna seçer.
Tasarım `docs/superpowers/specs/2026-10-01-colorhunt-special-balls-fx-design.md`, plan `docs/superpowers/plans/2026-10-01-colorhunt-special-balls-fx.md`.

- **Kurallar:** Pota `src/renkAvi.ts`'in özel top ve geçiş kısmı birebir (`ColorHuntRules.Specials.cs`). Doğrulama Pota'nın kendi koduyla
  üretilen fikstürlerle (`Tools/potalook/run_fixtures.sh <scratch>` → `Tests/EditMode/Fixtures/special-rules.json`,
  `SpecialRulesFixtureTests`). Bölüm 20'de kapalı (`ColorHuntSettings.Specials`); engeller, güçlendiriciler, bonus, özel hedefler U25'te.
- **Görünüm seçimi:** `ColorHuntSettings.SpecialLook` = Pota / Royal (1) / Court (2); kod `Scripts/ColorHunt/Specials/`
  (`ISpecialStyle`, `SpecialSchedule`, `GoldenPath`, `PotaSpecials`, `RoyalSpecials`, `CourtSpecials`, `GateRings`).
- **Vitrin:** `Scenes/Specials.unity` (`ai_specials_build_scene`): senaryo düğmeleri (Doğuş, Çizgi, Bomba, Zincir, Altın, Serbest),
  görünüm düğmeleri (Pota | 1 | 2), Tekrar.
- **Blender varlıkları:** `vfx-fire-toon`, `vfx-flame-jet`, `vfx-dust-ring`, `vfx-speed-lines`, `vfx-floor-crack`, `vfx-letter-*`,
  `vfx-ball-halves`, `vfx-bomb-shell`, `vfx-coin`, `vfx-basketball` → `Art/VFX/<id>/` (`ai_studio_import`).
- **Kayıt:** `ai_colorhunt_record` `{"scene":"Specials","scenario":...,"special_look":...}`; sesler `events.json`'a düşer;
  `Tools/potalook/seq_video.py` sesli sıralı video, `side_video.py` sessiz üç sütun.
- **Teslim:** `docs/screenshots/2026-10-01-specials-*` (levha, yan yana video, sesli sıralı video, yakın planlar).
- **Ölçüm:** Mobile RP asset'iyle en ağır an (bomba + 2 çizgi zinciri) kare süresi: <Task 12'de ölçülen değer>.
- **Bekleyen:** Tuna'nın seçimi (1 ya da 2); seçilen ayrı bir cila turunda varsayılan olur ve bölüm tablosuyla oyuna girer (U25).
```

The `<Task 12'de ölçülen değer>` slot is filled with the number Task 12 measured (e.g. "+0,4 ms, 1170×2532"); if Task 12 could not measure, write why.

- [ ] **Step 2: CLAUDE.md §12**

Edit `CLAUDE.md` §12:
- `_Son güncelleme:` line → `2026-10-01 (özel toplar: Pota kuralları birebir, Royal ve Basketbol efekt adayları, vitrin sahnesi; Tuna'nın seçimi bekleniyor).`
- Under "Oyuna özel araçlar" add `ai_specials_build_scene` and the new `ai_colorhunt_record` params.
- Under "Testler" replace the counts with the final EditMode/PlayMode counts from the last full runs of Task 12 (`~/.local/bin/uv run Tools/unity_tests.py EditMode` and `PlayMode`), and the Blender `tools/run.sh test` count.
- Under "Genel dersler" add one bullet per non-obvious lesson met in Tasks 1–12 (only lessons that are not already listed and that a future session would trip on; skip if none).
- "Aktif oyun / mevcut aşama": add a sentence: special balls + two effect candidates ready (`docs/screenshots/2026-10-01-specials-*`), waiting for Tuna's pick.

- [ ] **Step 3: Blender workshop docs and backlog**

- `docs/studyo.md` item 4: append "İkinci adım bitti (2026-10-01): özel top efektleri — `fire_toon`, `flame_jet`, `dust_ring`, `speed_lines`, `floor_crack`, `lettering` tarifleri, `vfx_mesh`/küre kabuk kırığı; Unity'de Royal ve Court görünümleri; sıradaki: Tuna'nın seçtiğinin cilası."
- `CLAUDE.md` (Blender repo): list the new recipes and tools with one line each (parameters as built in Tasks 7–8).
- `docs/backlog.md`: rewrite U25's text to "Engeller (file/buz/hakem), güçlendiriciler, bonus, özel hedef türleri ve HUD altın sayacı Unity'de yok; özel toplar ve geçişler 2026-10-01'de geldi." and add `U44+` items for anything Tasks 1–12 deferred (each with file, problem, proposal, test, in the existing format). Do not mark anything done that was not verified.

- [ ] **Step 4: Memory**

Write `/Users/tunaaltin17/.claude/projects/-Users-tunaaltin17-projects-Unity-MCP/memory/specials-fx-2026-10-01.md`:

```markdown
---
name: specials-fx-2026-10-01
description: Pota special balls (line/bomb/golden + gates) ported 1:1 to Unity Color Hunt with Pota-generated fixtures; Royal (1) and Court (2) effect candidates + demo scene; awaiting Tuna's pick
metadata:
  type: project
---

Tuna asked (2026-10-01) for Royal Match–style effects on Pota's special balls using Blender + Unity, two examples to choose from.
Picked directions: 1 Royal Match language, 2 basketball language. Built: rules port (fixtures from Pota's own renkAvi.ts in a scratch
copy), `SpecialLook` switch (Pota/Royal/Court), `Scenes/Specials.unity` demo, Blender recipes/meshes, voiced sequential + silent side
videos `docs/screenshots/2026-10-01-specials-*`.

**Why:** the pick is Tuna's; the next session starts from their verdict on the videos.
**How to apply:** ask for the verdict first; the winner becomes the default in a polish round, then specials go into the level table
(U25 rest: obstacles, boosters, special targets). Related: [[studio-polish-2026-09-30]], [[studio-pipeline]], [[feedback-quality-over-cost]].
```

Append to `MEMORY.md`: `- [Special balls FX 2026-10-01](specials-fx-2026-10-01.md) — Pota specials ported 1:1 + Royal/Court effect candidates + demo scene; awaiting Tuna's pick`

- [ ] **Step 5: Commit both repos**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP" && git add Assets/_Project/Games/Pota/README.md CLAUDE.md && git commit -m "docs: special balls and effect candidates — README, project status

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
cd "/Users/tunaaltin17/projects/Blender MCP" && git add docs/studyo.md CLAUDE.md docs/backlog.md && git commit -m "docs: special-ball effect assets — roadmap step 2, recipes, backlog

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
Expected: one commit in each repo; `git status` clean in both (memory files live outside the repos).

