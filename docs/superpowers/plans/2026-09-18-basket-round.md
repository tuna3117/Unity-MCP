# Basket Round (Faz A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Execution note for this project:** one shared Unity Editor + one MCP bridge → tasks are executed inline and sequentially in this session (superpowers:executing-plans). Pure C# units are written test-first (EditMode); Unity components are verified with PlayMode tests and screenshots through the bridge.

**Goal:** Rebuild Pota's basketball round in this Unity 6 / URP project with real 3D physics, a depth-readable camera, a juice layer and the original art, keeping rules and economy identical.

**Architecture:** Pure rule classes (`AimMapper`, `TrajectorySolver`, `LevelRules`, `RoundRules`) are testable without Unity objects. A `BasketRound` state machine drives pooled `Ball` rigidbodies through runtime-built `Hoop`s and a `BasketCage`, raising events that a `BasketJuice` layer (camera, slow-mo, popups, SFX, VFX) and a `BasketHud` consume. Everything in the scene is created by an editor tool (`ai_basket_build_scene`) so the scene is reproducible; the hoop column itself is built at runtime from `HoopLayout` data per level.

**Tech Stack:** Unity 6000.6.2f1, URP 17.6, Input System 1.20, PhysX rigidbodies, `UnityEngine.Pool`, uGUI (legacy `Text`, no TMP setup needed), MCP for Unity bridge (`Tools/mcp_call.py`), Unity Test Framework (EditMode + PlayMode), `com.unity.cloud.gltfast` for the player GLB.

**Spec:** `docs/superpowers/specs/2026-09-18-basket-round-design.md`

## Global Constraints

- Numbers from spec §2 are the defaults of `BasketSettings`; never hard-code them elsewhere.
- Scene changes only through code/bridge (no hand-edited YAML). Assets moved/deleted only through `AssetDatabase`/bridge.
- Namespace `Project.Basket` (runtime, `Assets/_Project/Scripts/Basket/`), `Project.Editor.AITools` (editor). Tests under `Assets/_Project/Tests/EditMode` and `Assets/_Project/Tests/PlayMode`.
- After every script change: `refresh_unity` → `read_console` (filter `error CS`) must be empty before continuing.
- Every stage ends with Game View screenshots (`manage_camera screenshot`) reviewed by eye and a git commit.
- Levels 1–10 must play at 60 fps in the Editor; simulated rigidbodies capped at 400, total balls 900.
- Fixed timestep 1/120 s while a round is running.

---

## File structure

```
Assets/_Project/Scripts/Basket/
  AimMapper.cs          pure: swipe px → AimPoint (x, y, power, overPower, flightTime)
  TrajectorySolver.cs   pure: fixed-time parabola (launch velocity, position, apex)
  LevelRules.cs         pure: ball table, row count, seeded HoopLayout
  RoundRules.cs         pure: FewLeft / IsMissed / ShouldEnd
  BasketSettings.cs     ScriptableObject with every tunable
  Ball.cs               pooled rigidbody ball (phase gravity, slab entry, rim assist hook, stuck, trail, spin)
  BallPool.cs           ObjectPool<Ball> + caps
  Hoop.cs               rim colliders, backboard, pass trigger, effect, net wobble, badge
  BasketCage.cs         cage colliders, entry counting, pile metrics
  HoopColumnBuilder.cs  builds hoops/walls/cage from HoopLayout
  BasketRound.cs        state machine, spawning, ×/+ effects, end/missed, events, programmatic shot
  ShotInput.cs          Input System pointer → AimMapper → round; drives AimPreview
  AimPreview.cs         arc LineRenderer + landing ring (+ red over-power)
  RoundCamera.cs        pose follow (aim / flight / drop / basket), only-down rule, pile clamp
  CameraShake.cs        decaying positional noise
  TimeScaleController.cs slow-mo + hit-stop
  PopupText.cs          world→screen uGUI popups
  SynthSfx.cs           procedural AudioClips + rate limits
  BasketJuice.cs        subscribes round events → camera/shake/time/popups/VFX/SFX/net
  BasketHud.cs          counters, hint, missed label, result panel
  Visual/RimMeshBuilder.cs, Visual/NetBuilder.cs, Visual/FacadeBuilder.cs, Visual/CityBuilder.cs  (stage 3)
Assets/_Project/Editor/AITools/
  BasketSceneTool.cs    ai_basket_build_scene (+ menu)
  BasketSweepTool.cs    ai_basket_sweep (polling tool, Play Mode)
  BasketballTextureTool.cs  equirect seam texture generator (stage 3)
Assets/_Project/Tests/EditMode/  AimMapperTests.cs TrajectorySolverTests.cs LevelRulesTests.cs RoundRulesTests.cs
Assets/_Project/Tests/PlayMode/  Project.Tests.PlayMode.asmdef BasketRoundPlayTests.cs
Assets/_Project/Settings/BasketSettings.asset · Assets/_Project/Scenes/Basket.unity
```

---

## Stage 1 — Gray box

### Task 1: AimMapper (pure) + tests

**Files:** Create `Assets/_Project/Scripts/Basket/AimMapper.cs`, `Assets/_Project/Tests/EditMode/AimMapperTests.cs`

**Interfaces — Produces:**
```csharp
namespace Project.Basket {
  public struct AimPoint { public float X; public float Y; public float Power; public bool OverPower; public float FlightTime; }
  public static class AimMapper {
    public const float DeadZonePx = 24f, LateralScale = 4f, FullPowerFraction = 0.42f, MaxPower = 1.4f;
    public const float RimY = 3.05f, FloorAbove = 0.1f, SwishAbove = 0.3f, FullPower = 0.6f, PowerRange = 1.5f, OverPowerAbove = 1.0f;
    public const float FloorPower = 0.4667f;      // power where the floor stops clamping: 3.15 = 3.35 + (p-0.6)*1.5
    public const float ShortFlightTime = 0.85f, FlightTimeBase = 1.0f;
    public static float Power(float dy, float screenH);
    public static float AimY(float power);
    public static float FlightTime(float power);   // Lerp(0.85, 1.0, Clamp01(power / FloorPower)) → short flicks fly faster/flatter
    public static bool TryMap(float dx, float dy, float screenW, float screenH, out AimPoint aim);
  }
}
```

- [ ] **Step 1: failing tests**
```csharp
using NUnit.Framework; using Project.Basket;
public class AimMapperTests {
  [Test] public void DeadZone_RejectsShortSwipe() { Assert.IsFalse(AimMapper.TryMap(0, -23, 390, 844, out _)); Assert.IsTrue(AimMapper.TryMap(0, -24, 390, 844, out _)); }
  [Test] public void Power_IsFractionOfScreen_AndClamped() { Assert.AreEqual(1f, AimMapper.Power(-844*0.42f, 844), 1e-4); Assert.AreEqual(1.4f, AimMapper.Power(-2000, 844), 1e-4); }
  [Test] public void AimY_HasFloorAboveRim() { Assert.AreEqual(3.15f, AimMapper.AimY(0.1f), 1e-4); Assert.AreEqual(3.35f, AimMapper.AimY(0.6f), 1e-4); Assert.AreEqual(3.35f + 0.6f, AimMapper.AimY(1.0f), 1e-4); }
  [Test] public void LateralMapping_FullWidthIsFourMetres() { AimMapper.TryMap(390, -300, 390, 844, out var a); Assert.AreEqual(4f, a.X, 1e-4); }
  [Test] public void OverPower_FlagAboveOneMetre() { AimMapper.TryMap(0, -844*0.42f*1.1f, 390, 844, out var a); Assert.IsTrue(a.OverPower); AimMapper.TryMap(0, -844*0.42f*0.9f, 390, 844, out var b); Assert.IsFalse(b.OverPower); }
  [Test] public void FlightTime_ShorterForShortFlicks() { Assert.Less(AimMapper.FlightTime(0.1f), AimMapper.FlightTime(0.4f)); Assert.AreEqual(1f, AimMapper.FlightTime(0.6f), 1e-4); }
}
```
- [ ] **Step 2:** refresh → tests fail to compile (types missing). Expected.
- [ ] **Step 3: implementation**
```csharp
using UnityEngine;
namespace Project.Basket {
  public struct AimPoint { public float X; public float Y; public float Power; public bool OverPower; public float FlightTime; }
  public static class AimMapper {
    public const float DeadZonePx = 24f, LateralScale = 4f, FullPowerFraction = 0.42f, MaxPower = 1.4f;
    public const float RimY = 3.05f, FloorAbove = 0.1f, SwishAbove = 0.3f, FullPower = 0.6f, PowerRange = 1.5f, OverPowerAbove = 1.0f;
    public const float FloorPower = FullPower - (SwishAbove - FloorAbove) / PowerRange; // 0.4667
    public const float ShortFlightTime = 0.85f, FlightTimeBase = 1.0f;
    public static float Power(float dy, float screenH) => Mathf.Min(MaxPower, -dy / (screenH * FullPowerFraction));
    public static float AimY(float power) => Mathf.Max(RimY + FloorAbove, RimY + SwishAbove + (power - FullPower) * PowerRange);
    public static float FlightTime(float power) => Mathf.Lerp(ShortFlightTime, FlightTimeBase, Mathf.Clamp01(power / FloorPower));
    public static bool TryMap(float dx, float dy, float screenW, float screenH, out AimPoint aim) {
      aim = default; if (-dy < DeadZonePx) return false;
      float power = Power(dy, screenH);
      aim = new AimPoint { X = dx / screenW * LateralScale, Power = power, Y = AimY(power), FlightTime = FlightTime(power) };
      aim.OverPower = aim.Y > RimY + OverPowerAbove; return true;
    }
  }
}
```
- [ ] **Step 4:** refresh, `run_tests EditMode` → all AimMapper tests pass.
- [ ] **Step 5:** `git commit -m "feat(basket): AimMapper with tests"`

### Task 2: TrajectorySolver (pure) + tests

**Files:** Create `Assets/_Project/Scripts/Basket/TrajectorySolver.cs`, `Assets/_Project/Tests/EditMode/TrajectorySolverTests.cs`

**Interfaces — Produces:**
```csharp
public static class TrajectorySolver {
  public static Vector3 LaunchVelocity(Vector3 start, Vector3 target, float flightTime, float gravity); // (target-start)/T + (0, 0.5*g*T, 0)
  public static Vector3 PositionAt(Vector3 start, Vector3 velocity, float gravity, float t);           // start + v t - (0, 0.5 g t², 0)
  public static float ApexHeight(Vector3 start, Vector3 velocity, float gravity);                       // start.y + vy²/(2g) if vy>0 else start.y
}
```
- [ ] **Step 1: tests** — `LaunchVelocity_ReachesTargetAtFlightTime` (start (0,1.9,6.2), target (1.4,3.35,0), T 1, g 9.8 → PositionAt(T) ≈ target within 1e-3); `Apex_RisesWithLongerFlightTime` (T 0.85 vs 1.0 same target → apex(1.0) > apex(0.85)); `PositionAt_Zero_IsStart`.
- [ ] **Step 2:** refresh → fail. **Step 3:** implement per the formulas. **Step 4:** tests pass. **Step 5:** commit `feat(basket): TrajectorySolver`.

### Task 3: LevelRules + HoopLayout (pure) + tests

**Files:** Create `Assets/_Project/Scripts/Basket/LevelRules.cs`, `Assets/_Project/Tests/EditMode/LevelRulesTests.cs`

**Interfaces — Produces:**
```csharp
public enum HoopEffectKind { Multiply, Add }
public struct HoopSpec { public float X, Y, Radius; public HoopEffectKind Kind; public int Amount; public bool IsTop; public int Row; }
public sealed class HoopLayout {
  public int Level; public int BallCount; public int Rows; public List<HoopSpec> Hoops = new();
  public float TopRimY = 3.05f; public float RowSpacing = 3f; public float WallX = 2.1f;
  public float LowestRowY => TopRimY - RowSpacing * Rows;
  public float BasketWidth => 3.6f + (Rows - 2) * 0.4f; public float BasketHeight = 4.6f; public float BasketDepth = 2f;
  public float BasketTopY => LowestRowY;               // cage mouth sits right under the last row
  public float BasketFloorY => BasketTopY - BasketHeight;
}
public static class LevelRules {
  public static readonly int[] BallTable = {8,8,8,9,13,13,13,11,12,16,13,12,13,12,17,12,14,16,20,7,20,28,22,20,26,27,28,18,28,20,31,31,29,25,39,40,34,29,25,35};
  public static int BallCount(int level); public static int RowCount(int level);  // min(8, 2 + (level-1)/3)
  public static HoopLayout Build(int level);   // seed 7+level, System.Random
}
```
Row pattern (index → kind, effect): 0 cift ×2 · 1 tek +1 · 2 cift ×2 · 3 tek +1 · 4 cift +1 · 5 tek ×2 · 6 cift +1 · 7 tek +1. Top row: three hoops x −1.4/0/+1.4, R 0.30, ×2, IsTop. cift: x = ±1.0 + jitter, R 0.40; tek: x = jitter, R 0.42; jitter = (rnd−0.5)*0.9; y = TopRimY − RowSpacing*(row+1).

- [ ] **Step 1: tests** — `RowCount_Table` (L1→2, L3→2, L4→3, L19→8, L40→8); `BallCount_Table` (L1 8, L10 16, L20 7, L40 35); `Build_IsDeterministic` (two builds of L5 have identical hoop lists); `Build_HoopsStayInsideWalls` (all |x| + R < 2.1 for L1..40); `Build_TopRowIsThreeTimesTwo`.
- [ ] **Step 2:** refresh → fail. **Step 3:** implement. **Step 4:** pass. **Step 5:** commit `feat(basket): LevelRules and HoopLayout`.

### Task 4: RoundRules (pure) + tests

**Files:** Create `Assets/_Project/Scripts/Basket/RoundRules.cs`, `Assets/_Project/Tests/EditMode/RoundRulesTests.cs`

**Interfaces — Produces:**
```csharp
public static class RoundRules {
  public static bool FewLeft(int active, int total) => active == 0 || (total >= 20 && active <= Mathf.Max(2, Mathf.RoundToInt(total * 0.02f)));
  public static bool IsMissed(int inFlight, int topPasses, float highestBallY, float lowestTopRimY) => inFlight == 0 && topPasses == 0 && highestBallY <= lowestTopRimY - 0.2f;
  public static bool ShouldEnd(int active, int total, float pileMaxSpeed, float sinceLastEntry, float elapsed, float timeout = 30f)
    => elapsed >= timeout || (FewLeft(active, total) && (pileMaxSpeed < 0.25f || sinceLastEntry >= 1.5f));
}
```
- [ ] Tests: `FewLeft_SmallRounds_NeedZero` (active 1 total 8 → false; 0 → true), `FewLeft_LargeRounds_TwoPercent` (total 200 active 4 → true, 5 → false), `IsMissed_RequiresEveryBallBelowRim`, `ShouldEnd_Timeout`, `ShouldEnd_SettledPile`. Implement, pass, commit `feat(basket): RoundRules`.

### Task 5: BasketSettings + Ball + BallPool

**Files:** Create `BasketSettings.cs`, `Ball.cs`, `BallPool.cs`; asset `Assets/_Project/Settings/BasketSettings.asset` (created by Task 7's scene tool).

**Interfaces — Produces:**
```csharp
[CreateAssetMenu(menuName = "Project/Basket/Basket Settings")]
public class BasketSettings : ScriptableObject {
  [Header("Flight")] public Vector3 LaunchStart = new(0, 1.9f, 6.2f); public float FlightGravity = 9.8f; public float BallSpacing = 0.09f; public float AimJitter = 0.03f;
  [Header("Drop")] public float DropGravity = 5.0f; public float SlabEntryZ = 0.3f; public float EntryForwardDamping = 0.25f; public float SlabHalfDepth = 0.5f;
  [Header("Ball")] public float BallRadius = 0.12f; public float BallMass = 0.6f; public float Backspin = 9f; public float StuckSeconds = 3f; public float SlowSeconds = 0.6f; public float SlowSpeed = 0.15f;
  [Header("Rim")] public float RimTube = 0.03f; public int RimSegments = 12; public float RimAssistMinInward = 0.6f; public float RimUpSpeedCap = 2.6f; public bool FriendlyRim = true;
  [Header("Effects")] public float CloneScatter = 1.6f; public float AddDropSpeed = 3f; public int MaxBalls = 900; public int MaxSimulated = 400;
  [Header("Physics materials")] public PhysicsMaterial BallMaterial, RimMaterial, BackboardMaterial, WallMaterial, CageMaterial; // created by scene tool: bounciness ball .55 rim .5 board .5 wall .6 cage .12, friction .4, combine Average
  [Header("End")] public float RoundTimeout = 30f;
  [Header("Juice")] public float SwishSlowMoScale = 0.35f, SwishSlowMoSeconds = 0.4f, SwishShake = 0.04f, ShakeDecay = 9f, HitStopSeconds = 0.03f, PopupSeconds = 0.9f, PopupMinInterval = 0.35f;
  [Header("Camera")] public Vector3 AimCamPos = new(0, 2.6f, 11f); public Vector3 AimLookAt = new(0, 3.0f, 0); public float Fov = 60f; public float DropCamZ = 9.5f; public float BasketCamZ = 13.5f; public float AimSmoothing = 4f, DropSmoothing = 2.2f, BasketSmoothing = 2f;
}
public enum BallPhase { Flight, Drop, Piled, Frozen }
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class Ball : MonoBehaviour {
  public Rigidbody Body { get; private set; } public BallPhase Phase { get; private set; } public int Id; public float LastPassTime = -1f; public Hoop LastHoop;
  public event Action<Ball, Collision> RimContact;   // raised from OnCollisionEnter when the collider belongs to a Hoop rim
  public void Configure(BasketSettings s, int layerFlight, int layerDrop);
  public void Launch(Vector3 position, Vector3 velocity);        // Phase = Flight, layer = flight, spin around x = -Backspin
  public void Drop(Vector3 position, Vector3 velocity);          // Phase = Drop (clones / adds)
  public void EnterSlab();                                       // Phase = Drop, layer = drop, vz *= EntryForwardDamping
  public void MarkPiled(); public void Freeze();                 // Piled = in cage; Frozen = kinematic (asleep in cage)
  public bool IsSlow(float speed) ; public float StuckTimer; public float SlowTimer;  // updated by BasketRound
  void FixedUpdate() { float g = Phase == BallPhase.Flight ? s.FlightGravity : s.DropGravity; Body.AddForce(Vector3.down * g, ForceMode.Acceleration); if (Phase == Flight && transform.position.z < s.SlabEntryZ) EnterSlab(); }
}
public class BallPool : MonoBehaviour {
  public int ActiveCount, SimulatedCount; public IReadOnlyList<Ball> Active;
  public Ball Get(); public void Release(Ball b); public void ReleaseAll(); public bool CanSimulateMore => SimulatedCount < s.MaxSimulated;
}
```
Rigidbody setup: mass BallMass, drag 0, angularDrag 0.05, `useGravity=false`, interpolation Interpolate, collisionDetectionMode ContinuousDynamic, `maxAngularVelocity 50`. SphereCollider radius BallRadius with BallMaterial.

- [ ] Steps: write files → refresh → no compile errors → commit `feat(basket): settings, ball and pool`. (Behaviour is exercised by Task 7 PlayMode tests.)

### Task 6: Hoop, BasketCage, HoopColumnBuilder

**Files:** Create `Hoop.cs`, `BasketCage.cs`, `HoopColumnBuilder.cs`

**Interfaces — Produces:**
```csharp
public class Hoop : MonoBehaviour {
  public HoopSpec Spec { get; private set; } public Transform RimRoot, NetRoot, Badge;
  public event Action<Hoop, Ball> Passed; public event Action<Hoop, Ball> RimHit;
  public static Hoop Build(Transform parent, HoopSpec spec, BasketSettings s, int layerHoop);
  // rim: RimSegments capsule colliders around a circle of radius R at y (tube RimTube), RimMaterial, tagged "Rim"
  // backboard: BoxCollider 1.05 x 0.78 x 0.05 centred (0, +0.38, -R-0.06), BackboardMaterial; arm box
  // pass trigger: BoxCollider isTrigger size (2R, 0.10, 2R) at y - 0.06
  // gray-box visuals: rim = thin torus from RimMeshBuilder (stage 3) — in stage 1 use 12 small capsule primitives (MeshRenderer on the collider objects), backboard quad, badge = TextMesh "×2"/"+1"
  void OnTriggerEnter(Collider c): ball = c.GetComponentInParent<Ball>(); if ball && ball.Body.linearVelocity.y < 0 && HorizontalDist(ball) < R - s.BallRadius*0.5 && Time.time - ball.LastPassTime > 0.3 → ball.LastPassTime = Time.time; ball.LastHoop = this; Passed?.Invoke(this, ball)
  public void OnRimContact(Ball ball, Collision col)  // friendly rim + cap; RimHit?.Invoke
  public void Wobble();                                // stage 2: net scale animation 0.55 s
}
public class BasketCage : MonoBehaviour {
  public event Action<Ball> Entered; public int EnteredCount; public float PileTopY; public float MaxSpeed; public float LastEntryTime;
  public static BasketCage Build(Transform parent, HoopLayout layout, BasketSettings s);
  // colliders: floor (width x 0.1 x depth), left/right walls, front/back walls at z ±depth/2, full height; CageMaterial; entry trigger box at BasketTopY (width x 0.2 x depth)
  public void Track(IReadOnlyList<Ball> balls) // each FixedUpdate: MaxSpeed over Piled balls, PileTopY = max y of piled balls
}
public static class HoopColumnBuilder {
  public sealed class Result { public List<Hoop> Hoops; public BasketCage Cage; public Transform Root; }
  public static Result Build(Transform parent, HoopLayout layout, BasketSettings s, int layerHoop, int layerSlab);
  // walls: BoxColliders at x = ±WallX (thickness 0.2, from BasketFloorY to TopRimY+6, z from -SlabHalfDepth to +BasketDepth), WallMaterial
  // back wall (facade) at z = -SlabHalfDepth-0.1; front slab wall at z = +SlabHalfDepth+0.1 spanning y from BasketTopY+0.3 up to TopRimY+6 on layerSlab
  // gray-box visuals: walls as thin gray quads, cage as translucent box
}
```
Layers (created by the scene tool via TagManager): `BallFlight` (ignores `Slab`), `BallDrop`, `Hoop`, `Slab`. Collision matrix: BallFlight×Slab off; BallDrop×Slab on; balls collide with balls and hoops.

- [ ] Steps: write → refresh → no errors → commit `feat(basket): hoops, cage and column builder`.

### Task 7: BasketRound, ShotInput, AimPreview, BasketHud, scene tool, PlayMode tests

**Files:** Create `BasketRound.cs`, `ShotInput.cs`, `AimPreview.cs`, `BasketHud.cs`, `Assets/_Project/Editor/AITools/BasketSceneTool.cs`, `Assets/_Project/Tests/PlayMode/Project.Tests.PlayMode.asmdef`, `Assets/_Project/Tests/PlayMode/BasketRoundPlayTests.cs`; modify `Assets/_Project/Scripts/Project.Runtime.asmdef` (add `UnityEngine.UI`).

**Interfaces — Produces:**
```csharp
public class BasketRound : MonoBehaviour {
  public enum State { Aim, Flight, Drop, Settle, Done }
  public BasketSettings Settings; public BallPool Pool; public int Level = 1;
  public State Current { get; private set; } public HoopLayout Layout { get; private set; } public HoopColumnBuilder.Result Column { get; private set; }
  public int BasketCount { get; private set; } public int TopPasses { get; private set; } public bool Missed { get; private set; } public int TotalSpawned { get; private set; } public float Elapsed { get; private set; }
  public event Action<AimPoint> ShotFired; public event Action<Hoop, Ball, bool> HoopPassed; public event Action<Hoop, Ball> RimHit; public event Action<Ball> BallLanded; public event Action<int> RoundEnded; public event Action MissedDeclared; public event Action<State> StateChanged;
  public void LoadLevel(int level);       // destroys old column, builds layout + column, resets counters, state Aim
  public void Shoot(AimPoint aim);        // state Flight, StartCoroutine(SpawnBalls)
  public void ShootProgrammatic(float x, float power) => Shoot(new AimPoint{X=x, Power=power, Y=AimMapper.AimY(power), FlightTime=AimMapper.FlightTime(power), OverPower=AimMapper.AimY(power)>AimMapper.RimY+AimMapper.OverPowerAbove});
  public void ResetRound();               // pool.ReleaseAll, counters, state Aim (same level)
  public Vector3 MeanActivePosition { get; }  // for the camera
}
```
Behaviour: `SpawnBalls`: for i in 0..N-1: target = (aim.X + jitter, aim.Y, 0); v = TrajectorySolver.LaunchVelocity(LaunchStart, target, aim.FlightTime, FlightGravity); ball.Launch(LaunchStart, v); wait BallSpacing. `HoopPassed` handler: TopPasses++ if hoop.Spec.IsTop; Multiply n → spawn n−1 clones `ball.Drop(pos, new Vector3(v.x + (rnd−0.5)*CloneScatter, v.y*(0.8+rnd*0.3), v.z*0.5))` while pool.CanSimulateMore (else BasketCount += 1 per skipped clone, counted as "virtual"); Add n → n balls at (hoop.X, hoop.Y−0.15, 0) with (0, −AddDropSpeed, 0). Cage `Entered` → ball.MarkPiled(); BasketCount++; BallLanded. FixedUpdate: Elapsed += dt; stuck/slow timers per Drop ball (nudge with random 0.5 m/s impulse at 1.5 s, teleport to cage mouth at StuckSeconds); Flight→Drop when any ball entered slab; Drop→Settle when RoundRules.FewLeft; missed check each step with RoundRules.IsMissed (once → MissedDeclared); end when RoundRules.ShouldEnd → leftovers counted, state Done, RoundEnded(BasketCount). Frozen: piled ball with Body.IsSleeping() → Freeze() (kinematic) to keep SimulatedCount low. Time.fixedDeltaTime = 1/120 in Awake (restore on destroy).

```csharp
public class ShotInput : MonoBehaviour { public BasketRound Round; public AimPreview Preview; /* Pointer.current: press → store; drag → TryMap → Preview.Show; release → Round.Shoot */ }
public class AimPreview : MonoBehaviour { public void Show(AimPoint aim, BasketSettings s); public void Hide(); /* LineRenderer 27 points over FlightTime*1.1, width 0.05, white (red if OverPower); landing ring: flat cylinder r 0.25 at (X, Y, 0) */ }
public class BasketHud : MonoBehaviour { public Text BallsText, BasketText, HintText, MissedText, ResultText; public GameObject ResultPanel; public void Bind(BasketRound r); /* TOP shows N while aiming and 0 after release; SEPET pops on change; Kaçtı; result "Sepet doldu! N" with Retry button → ResetRound */ }
```
Scene tool `ai_basket_build_scene` (`[McpForUnityTool("ai_basket_build_scene")]`, param `level`, `open` bool): creates layers via TagManager (`BallFlight`, `BallDrop`, `Hoop`, `Slab`), sets `Physics.IgnoreLayerCollision(BallFlight, Slab, true)`, creates/loads `BasketSettings.asset` with 5 physics materials under `Assets/_Project/Settings/`, creates scene `Assets/_Project/Scenes/Basket.unity` with: Main Camera (FOV 60, RoundCamera + CameraShake added in stage 2), Directional Light, Global Volume (profile `Assets/Settings/SampleSceneProfile.asset`), `Round` object (BasketRound + BallPool + ShotInput + AimPreview), HUD canvas (legacy Text, LegacyRuntime.ttf), EventSystem (InputSystemUIInputModule), VfxService (library asset). Saves the scene. Idempotent (re-run rebuilds).

PlayMode tests (`Project.Tests.PlayMode.asmdef`: references Project.Runtime, UnityEngine.TestRunner, UnityEditor.TestRunner? — no, PlayMode: `UnityEngine.TestRunner` only + nunit; `includePlatforms` empty; `defineConstraints UNITY_INCLUDE_TESTS`):
```csharp
[UnityTest] public IEnumerator MiddleHoop_Power06_AllBallsScore() { load "Basket" scene (SceneManager.LoadScene, yield); round = FindFirstObjectByType<BasketRound>(); round.LoadLevel(1); round.ShootProgrammatic(0f, 0.6f); yield until round.Current == Done or 35 s; Assert.GreaterOrEqual(round.TopPasses, 7); Assert.GreaterOrEqual(round.BasketCount, 8); }
[UnityTest] public IEnumerator FarSideShot_IsMissed() { ShootProgrammatic(3.5f, 0.6f) → wait Done → Assert.IsTrue(round.Missed); }
```
- [ ] Steps: write files → refresh → no errors → run `ai_basket_build_scene` → `run_tests PlayMode` → pass → screenshots (aim view; mid-drop; pile) → commit `feat(basket): gray-box round, input, HUD, scene tool, play tests`.

### Task 8: Stage 1 checkpoint
- [ ] Take 3 Game View screenshots via a programmatic shot in Play Mode; show to Tuna; note observations in the plan; commit.

## Stage 2 — Camera + feel

### Task 9: RoundCamera + CameraShake
**Files:** Create `RoundCamera.cs`, `CameraShake.cs`; modify `BasketSceneTool.cs` (add to Main Camera).
```csharp
public class RoundCamera : MonoBehaviour { public BasketRound Round; public BasketSettings S; float camY = float.MaxValue;
  // Aim: pos S.AimCamPos, look S.AimLookAt, k = S.AimSmoothing
  // Flight: focus = first launched ball; pos = (0, max(S.AimCamPos.y, focus.y*0.6+1.5), S.AimCamPos.z), look at (focus.x*0.3, focus.y, 0)
  // Drop: mean = Round.MeanActivePosition; targetY = mean.y + 0.3; camY = min(camY, targetY); camY = max(camY, cage.PileTopY + 1.8); pos = (mean.x*0.15, camY, S.DropCamZ); look (0, camY - 0.8, 0); k = S.DropSmoothing
  // Settle/Done: y = max(floor+2.2, (pileTop+floor)/2 + 1.2); pos (0, y+0.8, S.BasketCamZ); look (0, y-0.6, 0); k = S.BasketSmoothing
  // blend: pos = Lerp(pos, target, 1 - exp(-k dt)); rotation via LookAt on the blended look point
}
public class CameraShake : MonoBehaviour { public void Kick(float amplitude); /* offset = Random.insideUnitSphere*amp each frame, amp *= exp(-decay dt); applied in LateUpdate after RoundCamera */ }
```
- [ ] Steps: write → refresh → play, screenshots at aim/flight/drop/basket → commit `feat(basket): round camera and shake`.

### Task 10: Juice layer
**Files:** Create `TimeScaleController.cs`, `PopupText.cs`, `SynthSfx.cs`, `BasketJuice.cs`; modify `Hoop.cs` (Wobble), `Ball.cs` (TrailRenderer during Flight), `BasketSceneTool.cs` (wire components, popup canvas), `VfxAuthoringTool.cs` (add definitions `swish`, `multiply`, `add`, `rim_hit`: swish = white/gold sparkle ring; multiply = gold burst 16; add = green burst 16; rim_hit = 6 orange sparks).
```csharp
public class TimeScaleController : MonoBehaviour { public void SlowMo(float scale, float seconds); public void HitStop(float seconds); /* Time.timeScale + fixedDeltaTime scaled; restores; slow-mo overrides hit-stop */ }
public class PopupText : MonoBehaviour { public Canvas Canvas; public Font Font; public void Show(Vector3 world, string text, Color color, int size, bool center = false); /* Text rises 60 px and fades over PopupSeconds; center = fixed at 50%/42% size 64 */ }
public class SynthSfx : MonoBehaviour { public void Play(string id, float pitch = 1f); /* clips built in Awake: shot (noise sweep 0.4 s), rim (sine 1900→1500 0.13 s), swish (noise + triangle), multiply (sine 520→980 0.06 s), add (arpeggio 659/784/988), land (sine 190→110 0.07 s); rate limits rim 70 ms, multiply 45 ms, land 40 ms */ }
public class BasketJuice : MonoBehaviour { /* on ShotFired: sfx shot; on HoopPassed(hoop, ball, isTopSwish): if first top swish → SlowMo(.35,.4) + Kick(.04) + popup center "×2" gold 64; else popup at hoop "×n"/"+n" (rate-limited per hoop PopupMinInterval) + VfxService.Play("multiply"/"add") + sfx + HitStop(.03); hoop.Wobble(); on RimHit: VfxService.Play("rim_hit") + sfx rim; on BallLanded: sfx land + HUD pop; on MissedDeclared: popup center "Kaçtı" red */ }
```
- [ ] Steps: write → refresh → play with spawner → screenshots → commit `feat(basket): juice layer`.

## Stage 3 — Visuals

### Task 11: Art pass
**Files:** Create `Visual/RimMeshBuilder.cs` (torus mesh), `Visual/NetBuilder.cs` (8 rings + 12 strings as thin cylinders, wobble by scaling NetRoot), `Visual/FacadeBuilder.cs` (shaft plane 4.9 m wide with `kule-cam.jpg` tiled 3.2×5.7, buttresses at ±2.6, wings at ±4.45 with `kule-renkli.jpg`, floor mouldings every 3 m, glass boxes), `Visual/CityBuilder.cs` (34 boxes in 3 depth layers z −16/−30/−48, heights 26–78, `cephe.jpg`/`kule-cam.jpg`), sky plate 45×80 at (0,44,−60) `sehir.jpg` unlit, terrace slab + railing + `saha.jpg`, player from `oyuncu.glb` (glTFast import, scale to 1.85 m, back to camera, hidden after shot), `BasketballTextureTool.cs` (equirect 1024×512: orange #f07a2a base, 3 black seam curves, emissive none), materials: rim `#ff6a1a` smoothness .7, backboard `pano.png` cutout, balls URP Lit smoothness .55, cage wires `#1a1a1a`, orange rails; fog `#9ed3f5` 26–115 linear; sun warm (`#fff4e0`, 2.1) soft shadows; skybox colour `#8ec9f2`.
- [ ] Steps: write builders → scene tool uses them → refresh → screenshots (aim, drop, basket) → compare with `arastirma/sim-ekran.png` by eye → commit `feat(basket): art pass with Pota assets`.

## Stage 4 — Measurement and tuning

### Task 12: Sweep tool + tuning + docs
**Files:** Create `Assets/_Project/Editor/AITools/BasketSweepTool.cs` (`[McpForUnityTool("ai_basket_sweep", RequiresPolling = true, PollAction = "status")]`, actions start/status; params level, xs (default [-1.4,0,1.4]), powers (default [0.3,0.5,0.6,0.7,0.9,1.0]), offsets (default [-0.2,0,0.2]); runs shots sequentially in Play Mode via EditorApplication.update, waits for Done, records BasketCount; status returns progress + min/p25/median/p75/max).
- [ ] Run sweep L1 → target median 15–27, min ≥ 8, max ≤ 40; tune `DropGravity`, `RimAssistMinInward`, `EntryForwardDamping`, `CloneScatter` if outside; re-run.
- [ ] Update CLAUDE.md §12 (basket scene, tools, sweep numbers); commit `feat(basket): sweep tool and tuning`.

---

## Self-review

- Spec coverage: §2 rules → Tasks 1–7; improvements (camera above rim, apex/flight time, real over-power, nudge-then-teleport, ball–ball, trail/marker/hit-stop/spark/tick/net) → Tasks 1, 5, 7, 9, 10; §4 visuals → Task 11; §5 tests → Tasks 1–4, 7, 12; §6 stages → checkpoints after Tasks 8, 10, 11, 12.
- Types are consistent: `AimPoint`, `HoopSpec/HoopLayout`, `Ball.Launch/Drop/EnterSlab`, `Hoop.Passed/RimHit`, `BasketCage.Entered`, `BasketRound` events used by `BasketJuice`/`BasketHud`/`RoundCamera`.
