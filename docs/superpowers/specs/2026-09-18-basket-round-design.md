# Basket round ("Faz A") rebuild in Unity — design

Date: 2026-09-18 · Decision (Tuna): direction **A = faithful+** (same rules, numbers, economy and art style; better physics, camera, feel, visuals) · reuse Pota assets · drop pacing left to me · PC first, phone later.

Source game: `~/projects/Pota` (three.js + hand-written 2D solver). Read-only reference; nothing there is modified. Rule numbers below come from `src/kurallar.ts`, `src/fizik.ts`, `src/oyun.ts` (see the exploration report summarised in this doc).

## 1. Goal and success criteria

Rebuild the basketball round as a Unity 6 / URP scene:

- Real 3D rigid-body physics (sphere balls, real rim, backboard, walls, basket, ball–ball everywhere).
- A camera that communicates depth (the original's documented #1 feel problem).
- A juice layer (slow-mo, shake, popups, particles, synthetic SFX, trail, spin, net wobble).
- Visual fidelity above the original using the original's own art (toy-diorama style).
- **Comparable economy:** a programmatic sweep of shots on level 1 must land basket counts in the original's measured band (min 12 · median ≈ 21 · max 32 for 8 starting balls). Levels 1–10 are the target range; higher levels must run but are not tuned.

Non-goals: tower/puzzle phase, menus, meta (lives, coins, IAP, ads), audio files, mobile builds (touch input is supported by design; building is a later step).

## 2. Rules carried over 1:1

```
flight:   T = 1.0 s fixed parabola, g = 9.8, start S = (0, 1.9, 6.2), target on plane z = 0
aim:      x = dx/W * 4.0 ; power = min(1.4, -dy/(H*0.42)) ; dead zone 24 px
          y = max(3.15, 3.35 + (power-0.6)*1.5)     // rim y = 3.05 ; over-power when y > rim+1.0 (power > ~1.07)
balls:    N = TOP_SAYISI[level] (8,8,8,9,13,13,13,11,12,16, ...), spawned 0.09 s apart, ±0.03 m x jitter
hoops:    top row R 0.30 at x = -1.4/0/+1.4, y = 3.05, all ×2
          rows below: rows(level) = min(8, 2 + floor((level-1)/3)), spacing 3.0 m,
          pattern top→bottom: cift ×2 · tek +1 · cift ×2 · tek +1 · cift +1 · tek ×2 · cift +1 · tek +1
          cift = two hoops at x = ±1.0 + jitter (R 0.40) ; tek = one hoop at jitter (R 0.42) ; jitter ±0.45, seed 7+level
pass:     ball crosses rim plane downward with horizontal distance < R - r*0.5
effects:  ×n → n-1 clones at the ball with vx scatter ±0.8, vy × (0.8..1.1) ; +n → n balls at (hoop.x, hoop.y-0.15) vy -3 ; every pass counts
drop:     gravity lower than flight so the eye can follow (original 4.2; ours tunable, default 5.0)
walls:    x = ±2.1 (bounce 0.6) ; slab z ∈ [-0.5, +0.5] (facade behind, invisible front wall)
rim:      tube r 0.03 ; bounce 0.5 ; friendly rim: inner-edge contact while moving down → inward velocity ≥ 0.6, no upward pop ; up-speed cap 2.6
basket:   width 3.6 + (rows-2)*0.4 (3.6..6.0) × 4.6 h × 2.0 d ; bounce 0.12 ; sleep when slow
missed:   no ball in flight, zero top passes, every ball ≥ 0.2 m below the lowest top rim
end:      (active balls ≤ max(2, 2% of total) or 0) and (pile max speed < 0.25 or 1.5 s since last entry) ; hard timeout 30 s ; leftovers are counted
juice:    first top swish → timeScale 0.35 for 0.4 s + 0.04 m shake (decay e^-9t) ; popup life 0.9 s ; one popup per hoop per 0.35 s
camera:   FOV 60 ; drop follows the balls' centre of mass at z 9.5, only downward ; basket pose at z 13.5
```

Deliberate differences (improvements):

- Aim camera sits above the rim line (pos ≈ (0, 2.6, 11), look-at (0, 3.0, 0)) so the rim reads as an ellipse; during flight the camera never drops below rim height.
- Short swipes get a visibly different arc: the aim floor stays at 3.15 but the arc **apex** rises with swipe length (higher, slower arc for longer swipes) — length becomes readable without changing where the ball lands.
- Over-power no longer teleports: the ball really hits the top of the backboard/frame and falls beside the hoop; the aim trail turns red past the threshold.
- Backboard, front frame and walls are real colliders; stuck balls are nudged by a small jitter impulse first and only teleported to the basket mouth after 3 s (safety net kept).
- Ball–ball collisions during the fall (not only in the basket).
- New feel: trail during flight, landing marker while dragging, 30 ms hit-stop on ×/+ passes, rim "clank" spark, basket tick sound, net wobble on pass.

## 3. Architecture (assembly `Project.Runtime`, namespace `Project.Basket`)

Pure (testable, no Unity objects): `AimMapper`, `TrajectorySolver`, `LevelRules` (ball table, row rules, seeded layout → `HoopLayout` data), `RoundRules` (end / missed predicates).

Scene components:

- `BasketSettings` (ScriptableObject): every number above, physics materials, prefab refs, juice values, camera poses.
- `BasketRound` (state machine Aim → Flight → Drop → Settle → Done): spawns balls, listens to hoop/basket events, applies ×/+, decides missed/end, raises events (`ShotFired`, `TopSwish`, `HoopPassed`, `BallLanded`, `RoundEnded`), exposes `ShootProgrammatic(x, power)` for tests/sweeps.
- `ShotInput` (Input System pointer: mouse or touch): press/drag/release → `AimMapper`; drives `AimPreview` (LineRenderer arc + landing ring, red when over-power).
- `Ball` (pooled): Rigidbody sphere r 0.12, phase-dependent gravity (`useGravity=false`, force per FixedUpdate), rim-assist hook, stuck timer, trail, spin; `BallPool` (`ObjectPool`, cap 900; sleeping balls in the basket become kinematic; simulated-body cap 400, beyond that new entries are counted and recycled).
- `Hoop`: rim (torus mesh + ring of capsule colliders), backboard (box + quad), net (procedural rings, scale wobble), badge (×n gold / +n green), pass trigger below the rim plane, `HoopEffect {Multiply n | Add n}`.
- `HoopColumnBuilder`: builds hoops, walls, slab, basket from `HoopLayout` (runtime, so any level can be loaded by number).
- `BasketCage`: wire mesh, colliders, entry trigger, pile top / max speed tracking.
- `RoundCamera`: custom smooth follow with poses (aim, flight, drop, basket), only-downward rule, pile clamp; `CameraShake` (positional noise, exponential decay). No Cinemachine: the behaviour is small and specific; a package would add surface without benefit.
- `Juice`: `TimeScaleController` (slow-mo + hit-stop queue), `PopupText` (world→screen, uGUI), `SynthSfx` (procedural AudioClips generated at start: shot, rim, swish, multiply, add, land; rate limits), particles through the existing `VfxService` (new definitions: swish, multiply, add, rim_hit).
- `BasketHud`: TOP / SEPET counters with pop, hint, "Kaçtı", result panel (Sepet doldu! N · Retry · Level ±).

Editor tools (`Project.Editor`, `AI Tools/Basket/…` + MCP custom tools): `ai_basket_build_scene` (creates/refreshes `Assets/_Project/Scenes/Basket.unity` with all objects and references), `ai_basket_sweep` (Play-Mode shot sweep → basket count table vs. the original band), `BasketballTextureGenerator` (equirect seam texture from the Pota ball palette).

## 4. Visuals (Pota assets, copied to `Assets/_Project/Art/Pota/`)

`pano.png` backboard · `kule-cam.jpg` hoop shaft · `kule-renkli.jpg` wings · `cephe.jpg` city towers · `sehir.jpg` sky plate · `saha.jpg` terrace · `top.png` palette reference · `mesh/oyuncu.glb` player (import with `com.unity.cloud.gltfast`, back to camera, hidden after the shot like the original) · fonts Lilita One / Fredoka for HUD/popups. Rim orange `#ff6a1a`, palette navy `#142850`, gold `#ffc83d`, green `#2fb36b`, cream `#fff4e0`; sky `#8ec9f2`, fog `#9ed3f5` 26–115 m; sun warm directional with soft shadows; glossy URP Lit materials (toy look); existing Global Volume (bloom/vignette/color).

## 5. Tests

- EditMode: `AimMapperTests` (dead zone, floor, clamp, over-power), `TrajectorySolverTests` (position at T = target, apex rises with power), `LevelRulesTests` (rows per level, ball table, seeded layout deterministic and within walls), `RoundRulesTests` (missed/end predicates).
- PlayMode: middle hoop at power 0.6 → ≥ 90 % of balls pass the top hoop and basket ≥ N; far side shot → Missed; 200-ball pile settles < 30 s and count is exact.
- Sweep: `ai_basket_sweep` on level 1 (3 hoops × 6 powers × 3 offsets) → median in 15–27, min ≥ 8, max ≤ 40; results recorded in CLAUDE.md.

## 6. Stages (each ends with screenshots shown to Tuna)

1. Gray box: builder, colliders, pool, input, trajectory, pass detection, ×/+, end, HUD. Tests a/b.
2. Camera + feel: poses, slow-mo, shake, popups, particles, SFX, trail, spin, net wobble, marker.
3. Visuals: assets, meshes, materials, lighting, player GLB.
4. Measurement + tuning: sweep vs. original table; commit; CLAUDE.md status.
