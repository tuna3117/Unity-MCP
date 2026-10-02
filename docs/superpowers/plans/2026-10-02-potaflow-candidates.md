# PotaFlow candidates 1/2/3 — implementation plan (2026-10-02)

Spec: `docs/superpowers/specs/2026-10-02-potaflow-candidates-design.md`. Executed inline in one session (the code is one tightly
coupled director); each task ends with compile + the named tests and a commit. Rules from the 2026-09-29 plan still apply
(Pota read-only, no YAML edits, no compile in Play Mode, real-time transition, `Tools/unity_tests.py`).

- [ ] **T1 Timeline** — `FlowCandidate {Stream, Simple, BigShot, Dive, Stamp}`; `FlowTiming` + `BigShotTiming`/`DiveTiming`/`StampTiming`;
  `FlowTimeline`: `Ready`, `CountAt`, `RevealStart(ring)`, `AmmoProgress`, `HudProgress`, `BadgeDock/BadgeFly`, `CameraProgress` per candidate;
  `BallSchedule` (second flight) + `IBallSchedule`; `StampScale`, `RainOffset`, `DiveFov`, `SpeedLinesAlpha`, `SwishPop`.
  Tests: `FlowTimelineTests` (counter ends at N and never drops for all 5; second schedule visible = min(pile, 12) and lands inside its
  window; stamp/rain/dive curves at the edges; ready per candidate).
- [ ] **T2 Camera** — `CameraFlight.Path(CamPose[] pts, float u)` (N-point Catmull-Rom, length-split, exact at ends, passes the points),
  `CameraFlight.Sweep(a, b, u, rise)` (Pota pozKaris). Tests: `CameraFlightTests`.
- [ ] **T3 Pieces** — `BallStream.Begin(IBallSchedule, Func<int,Vector3>, IBallReceiver, float holdFrom)` (hold at source);
  `PlayerActor.PlayShoot()`; `BoardReveal.ApplyRain(game, tl, t)`; `FlowEffects` MonoBehaviour (speed-lines quad on the camera, swish ring,
  SWISH! letter, stamp puff + star) built from `ColorHuntSettings` textures/templates.
- [ ] **T4 Director** — candidate switch: camera path (BigShot 3-beat through the shot pose; Dive N-point path + FOV; Stamp sweep),
  hero turn to hoop + shoot, second stream bin → hoop (hoop pile shown), ammo/badge/HUD per candidate, effects triggers, rain reveal.
  `ai_potaflow_record` accepts `1|2|3`. PlayMode: `PotaFlowPlayTests` each candidate ends on a playable board with ammo = N, skip works.
- [ ] **T5 Look pass** — record each candidate (`ai_potaflow_record`, count 158), read frames, fix framing (shot pose vs tower face,
  dive path vs rim/net, stamp placement, rain speed) until each reads cleanly; U26 two frames.
- [ ] **T6 Deliver** — Pota transition from a scratch copy (vite :5199 + Playwright, `__pota.sepetBitir(158)`), A recording,
  `Tools/frame_sheet.py` sheet (Pota | A | 1 | 2 | 3) + ffmpeg hstack video → `docs/screenshots/2026-10-02-potaflow-candidates-*`;
  README/CLAUDE.md/backlog/memory; commit.
