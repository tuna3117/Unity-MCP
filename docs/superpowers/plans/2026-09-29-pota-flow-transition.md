# PotaFlow — Basket → Board Transition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One Unity scene (`Games/Pota/Scenes/PotaFlow.unity`) where the basket round ends and the camera flies, in one continuous
~2.5 s shot, from the full cage to the Color Hunt board inside a giant hoop, with the baskets visibly streaming into the hoop's net
as the board's ammo, and back again after the puzzle — two choreographies (A "Akan toplar", B "Sade") recorded next to Pota's own
transition for Tuna to pick.

**Architecture:** The basket world stays pinned at the origin; the Color Hunt game root is only translated to `BoardAnchor`
(0, 8.5, 30). A pure `FlowTimeline` answers "what is true at t?" (ball launches/landings, ammo shares, camera progress, ring pops,
HUD slide), a pure `CameraFlight` gives the Catmull-Rom camera path, and a `PotaFlowDirector` state machine
(Basket → Transition → Puzzle → Result → Return) drives the one camera, both inputs, the giant hoop, the ball stream and the HUDs
from that timeline in real (unscaled) time. Existing Basket and Color Hunt code gets small, backward-compatible hooks.

**Tech Stack:** Unity 6000.6.2f1, URP 17.6 (Render Graph), Input System 1.20, uGUI (legacy `Text`), Unity Test Framework
(EditMode + PlayMode), CoplayDev MCP bridge v10.2.0 via `Tools/mcp_call.py`, Python 3 + numpy (path checks), ffmpeg (Homebrew),
Playwright from `~/projects/copy/node_modules` (Pota recording from a scratch copy).

**Spec:** `docs/superpowers/specs/2026-09-29-pota-flow-transition-design.md` (approved by Tuna 2026-09-29).

## Global Constraints

- `~/projects/Pota` is **read-only**: never write there (including `onizleme/`, `node_modules/.vite`, its tools' outputs). Run Pota only from a copy in the session scratchpad.
- The existing `Basket.unity` and `ColorHunt.unity` scenes keep working as before; their PlayMode tests stay green.
- Unity 6000.6.2f1 / URP 17.6 / Input System only (`UnityEngine.Input` throws). Unity 6 APIs: `FindAnyObjectByType`, `linearVelocity`.
- Never hand-edit `.unity`, `.prefab`, `.asset`, `.mat` YAML; scenes are built by scene tools, assets changed through Unity APIs.
- Never compile while the editor is in Play Mode (`Tools/unity_tests.py` stops Play Mode first).
- Runtime code: `Assets/_Project/Games/Pota/Scripts/Flow/`, namespace `Project.Flow`, assembly `Pota.Runtime`. Editor tools: `Games/Pota/Editor/`, namespace `Project.Editor.AITools`, assembly `Pota.Editor`. Tests: namespace `Project.Tests`.
- Transition time is **real (unscaled) seconds**, t = 0 at `BasketRound.RoundEnded(N)`; all numbers in `PotaFlowSettings` / `FlowTiming` with the spec §4 values: highlight 0–0.30, launches from 0.25 every 0.035 s, V = min(N, 25) visible balls, landings 1.15–2.20, camera 0.30–1.90 (FOV 60 → 52 → 34, 0.12 s settle), hero 0.40–0.90, board rings from 1.75 every 0.07 s popping 0 → 1.12 → 1 over 0.28 s, frame glow 0.4 s, HUD 1.90–2.25, ready 2.50.
- At most 25 visible balls, at most 10 resting in the net; landing shares sum to exactly N; ready ≤ 2.6 s; a tap makes the rest take ≤ 0.35 s.
- Sounds only from Core `SynthSfx` (`swish`, `shot`, `land`, `multiply`, `add`); no paid calls; no push.
- Commits: English, small, each ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Talk to Tuna in Turkish.
- Deliverables go to `docs/screenshots/2026-09-29-potaflow-*`.

## Review Focus

1. **Tap to skip, finger still down when the board becomes ready** → the held touch must not select or break a block; board input turns on only after release. Pinned by `PotaFlowPlayTests.HeldSkipTouch_DoesNotReachTheBoard` (Task 11).
2. **Basket counts at the edges: 0, 1–3, ≥ 400** → 0 ends in the "Top bitti" result and "Tekrar at" flies back; few balls still arrive quickly; 1000 counts up to exactly 1000. Pinned by `Counter_ReachesN_ForEdgeCounts` and `ZeroBaskets_LoseThenRetryFliesBack` (Task 11).
3. **Screen shape changes mid-flight (rotation, window resize)** → the flight still lands exactly on the board fit for the new aspect, no jump when `ColorHuntCamera` takes over. Pinned by `AspectChange_MidFlight_StillLandsOnTheFit` (Task 11).
4. **A second round end / retry / HUD button during the transition** → ignored; the staged board cannot be tapped or rotated before it is ready. Pinned by `SecondRoundEnd_AndHudTaps_DuringTransition_AreIgnored` (Task 11).
5. **Round ends during a swish slow-motion** (`Time.timeScale` 0.05–0.3) → the transition runs at full speed and play resumes at normal time. Pinned by `LeftoverSlowMotion_DoesNotSlowTheTransition` (Task 11).

## How to run things (every task)

- Unity must be open with the bridge up: `Tools/unity_up.sh` (proje kökünden). If a test job reports "did not start", focus Unity: `open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app`.
- **Compile + test (one command, created in Task 1):** `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Project.Tests.Fixture ...]`. It stops Play Mode, forces a recompile, prints `error CS` lines and exits 2 on compile errors, else runs the tests and exits 0 only if all passed. RED by compile error counts as RED when the test references an API that does not exist yet.
- MCP calls: `~/.local/bin/uv run Tools/mcp_call.py call <tool> '<json>'` (project root). Custom tools: `call execute_custom_tool '{"tool_name":"ai_…","parameters":{…}}'`.
- Screenshots: `manage_camera {"action":"screenshot","capture_source":"game_view","include_image":true,"output_folder":"Temp/mcp_out","screenshot_file_name":"…"}`; keep with `Tools/keep_screenshot.sh <src> <name>`.
- Work on `main`, commit per task, never push (Tuna's standing decision).

## File map

| File | Responsibility |
|---|---|
| `Tools/unity_tests.py` (new) | Compile + run Unity tests through the bridge, exit code for the ledger |
| `Core/Scripts/Juice/TimeScaleController.cs` | + `Clear()` |
| `Games/Pota/Scripts/ColorHunt/ColorHuntHud.cs` | buttons wired at runtime; `RetryOverride`, `SetRetryLabel`, `HideResult`, `PopAmmo`, `SetIntro`, `AmmoScreenPoint` |
| `Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs` | `AutoStart`, `NewRound(int)`, `Prepare`, `ShowAmmo`, `Begin`, `ConfettiOrigin` |
| `Games/Pota/Scripts/ColorHunt/ColorHuntCamera.cs` | `Anchor`, static `FitPosition`, `FitFor` |
| `Games/Pota/Scripts/Basket/{BasketHud,RoundCamera,PlayerActor}.cs` | `ShowResultPanel`; resume-from-transform + `LookPoint`; `Reveal()` |
| `Games/Pota/Scripts/Basket/Visual/NetBuilder.cs` | optional `drop`, `thickness`, `ringTube` |
| `Games/Pota/Editor/SceneParts.cs` (new) | `JuiceSet`, build-settings append, volume, EventSystem |
| `Games/Pota/Editor/{BasketSceneTool,ColorHuntSceneTool}.cs` | steps split into public helpers, behaviour unchanged, build settings appended |
| `Games/Pota/Scripts/Flow/FlowTimeline.cs` (new) | `FlowCandidate`, `FlowTiming`, `FlowTimeline` (pure) |
| `Games/Pota/Scripts/Flow/CameraFlight.cs` (new) | `CamPose`, `CameraFlight` (pure) |
| `Games/Pota/Scripts/Flow/GiantHoop.cs` (new) | giant hoop visuals, net hopper, glow |
| `Games/Pota/Scripts/Flow/BallStream.cs` (new) | pooled visual balls, deterministic path, landing events |
| `Games/Pota/Scripts/Flow/{BoardReveal,CourtBuilder,FlowBadge,PotaFlowSettings}.cs` (new) | ring pop, rooftop court, "N TOP" badge, settings asset |
| `Games/Pota/Scripts/Flow/PotaFlowDirector.cs` (new) | phase machine and transition/return playback |
| `Games/Pota/Editor/PotaFlowSceneTool.cs` (new) | `ai_potaflow_build_scene` |
| `Games/Pota/Editor/FrameRecorder.cs` (new), `BasketRecordTool.cs`, `PotaFlowRecordTool.cs` (new) | shared capture; `ai_potaflow_record` |
| Tests (new): `Pota/Tests/EditMode/{ColorHuntStagingTests,BasketHudTests,SceneBuildSettingsTests,FlowTimelineTests,CameraFlightTests,GiantHoopTests,BallStreamTests,BoardRevealTests,CourtBuilderTests,FrameRecorderTests}.cs`, `Pota/Tests/PlayMode/{ColorHuntHudPlayTests,ColorHuntStagingPlayTests,BasketHooksPlayTests,PotaFlowPlayTests}.cs`, `Core/Tests/PlayMode/TimeScaleControllerTests.cs` | |

(All `Games/…` and `Core/…` paths are under `Assets/_Project/`.)

---

### Task 1: Test runner helper + Color Hunt HUD buttons wired at runtime

The rotate and retry buttons get their listeners inside `ColorHuntHud.Create()`, which the scene tool runs in Edit Mode.
Non-persistent listeners are not saved with the scene, so in the scene as loaded from disk the buttons do nothing. PotaFlow
needs the retry button ("Tekrar at"), so fix this first.

**Files:**
- Create: `Tools/unity_tests.py`
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntHud.cs` (`Bind`, `Create`, `MakeRotateButton`, new members)
- Test: `Assets/_Project/Games/Pota/Tests/PlayMode/ColorHuntHudPlayTests.cs`

**Interfaces:**
- Produces: `ColorHuntHud.RetryOverride` (`System.Action`), `SetRetryLabel(string)`, `HideResult()`, `PopAmmo()`; button wiring inside `Bind(ColorHuntGame)`. Test runner `Tools/unity_tests.py MODE [names…]`.

- [ ] **Step 1: Write the test runner**

```python
#!/usr/bin/env python3
"""Compile, then run Unity tests through the MCP bridge; exit 0 only if every selected test passed.

Usage (project root):
  ~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Full.Name ...] [--no-compile]
Full.Name is a fixture or test full name (e.g. Project.Tests.FlowTimelineTests). Stops Play Mode first. Compile errors
(error CS...) fail the run (exit 2) before any test starts. Prints "<mode> passed/total" and every failure.
"""
import json
import os
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UV = os.path.expanduser("~/.local/bin/uv")


def call(tool, args, tries=6):
    """One MCP tool call; retries while the bridge is down (domain reload after a compile)."""
    last = ""
    for _ in range(tries):
        p = subprocess.run([UV, "run", "-q", "Tools/mcp_call.py", "call", tool, json.dumps(args)],
                           cwd=ROOT, capture_output=True, text=True, timeout=1500)
        text = p.stdout
        i = text.find("{")
        if i >= 0:
            try:
                return json.JSONDecoder().raw_decode(text[i:])[0]
            except json.JSONDecodeError:
                pass
        last = (text + p.stderr)[-800:]
        time.sleep(5)
    sys.exit(f"[unity_tests] {tool} failed: {last}")


def strings(node):
    if isinstance(node, str):
        yield node
    elif isinstance(node, dict):
        for v in node.values():
            yield from strings(v)
    elif isinstance(node, list):
        for v in node:
            yield from strings(v)


def compile_errors():
    call("refresh_unity", {"mode": "force", "scope": "all", "compile": "request", "wait_for_ready": True})
    time.sleep(2)
    reply = call("read_console", {"types": ["error"], "filter_text": "error CS"})
    return sorted({s.strip() for s in strings(reply) if "error CS" in s})


def main(argv):
    compile_first = "--no-compile" not in argv
    args = [a for a in argv if a != "--no-compile"]
    if not args or args[0] not in ("EditMode", "PlayMode"):
        sys.exit(__doc__)
    mode, names = args[0], args[1:]
    call("manage_editor", {"action": "stop"})
    if compile_first:
        errs = compile_errors()
        if errs:
            print("[unity_tests] compile errors:")
            for e in errs[:20]:
                print("  " + e)
            return 2
    req = {"mode": mode}
    if names:
        req["test_names"] = names
    job = (call("run_tests", req).get("data") or {}).get("job_id")
    if not job:
        sys.exit("[unity_tests] run_tests returned no job id")
    data = {}
    for _ in range(40):
        data = call("get_test_job", {"job_id": job, "wait_timeout": 60, "include_failed_tests": True}).get("data") or {}
        if data.get("status") != "running":
            break
    if data.get("error"):
        print(f"[unity_tests] {mode}: {data['error']}")
        return 1
    result = data.get("result") or {}
    summary = result.get("summary") or {}
    passed, failed = summary.get("passed", 0), summary.get("failed", 0)
    print(f"{mode} {passed}/{summary.get('total', 0)} passed ({summary.get('resultState', data.get('status'))})")
    for r in result.get("results") or []:
        name = r.get("fullName") or r.get("name") or json.dumps(r)[:200]
        print(f"  FAIL {name}: {(r.get('message') or '').strip()[:400]}")
    return 0 if failed == 0 and passed > 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
```

- [ ] **Step 2: Check the runner on a known fixture and on a missing one**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.LevelRulesTests; echo "exit $?"`
Expected: `EditMode 6/6 passed (Passed)` and `exit 0`.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.NoSuchFixture --no-compile; echo "exit $?"`
Expected: `EditMode 0/0 passed …` and `exit 1`.

- [ ] **Step 2b: Let the Pota PlayMode tests use uGUI**

`Assets/_Project/Games/Pota/Tests/PlayMode/Pota.Tests.PlayMode.asmdef` references only `Pota.Runtime`, `Project.Runtime` and the
test runner; the new tests use `Button` / `Text`. Add `"UnityEngine.UI"` to its `references` array (asmdef files are JSON, editing
them directly is fine).

- [ ] **Step 3: Write the failing rotate-button test**

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
    /// <summary>
    /// The HUD buttons must work in the scene as loaded from disk: listeners added while the scene tool builds the HUD
    /// in Edit Mode are not serialized, so the HUD wires them when it binds at runtime.
    /// </summary>
    public class ColorHuntHudPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator LoadPlaying(Action<ColorHuntGame, ColorHuntHud> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current, "round did not start");
            ready(game, Object.FindAnyObjectByType<ColorHuntHud>());
        }

        private static Button ButtonAt(ColorHuntHud hud, string path)
        {
            var t = hud.transform.Find(path);
            Assert.IsNotNull(t, $"HUD has no {path}");
            return t.GetComponent<Button>();
        }

        [UnityTest]
        public IEnumerator RotateRightButton_RotatesTheBoard()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            ButtonAt(hud, "RotateRight").onClick.Invoke();
            Assert.AreEqual(1, game.Rotations, "rotate button did nothing (listener lost when the scene was saved)");
        }
    }
}
```

- [ ] **Step 4: Run it and watch it fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ColorHuntHudPlayTests`
Expected: `PlayMode 0/1 passed`, `FAIL …RotateRightButton_RotatesTheBoard: rotate button did nothing … Expected: 1 But was: 0`.
(If it passes, the listeners did survive: ledger that finding, keep the runtime wiring anyway since Step 5's test needs the override.)

- [ ] **Step 5: Add the retry test (fails to compile until the API exists)**

Append inside the class:

```csharp
        [UnityTest]
        public IEnumerator RetryButton_DealsANewBoard_UnlessOverridden()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            int started = 0;
            game.RoundStarted += () => started++;
            var retry = ButtonAt(hud, "ResultPanel/Card/Retry");
            retry.onClick.Invoke();
            Assert.AreEqual(1, started, "retry did not deal a new board");

            int overridden = 0;
            hud.RetryOverride = () => overridden++;
            retry.onClick.Invoke();
            Assert.AreEqual(1, overridden, "RetryOverride not called");
            Assert.AreEqual(1, started, "the override must replace the new-board action");

            hud.SetRetryLabel("Tekrar at");
            Assert.AreEqual("Tekrar at", hud.transform.Find("ResultPanel/Card/Retry/Label").GetComponent<Text>().text);
            var panel = hud.transform.Find("ResultPanel").gameObject;
            panel.SetActive(true);
            hud.HideResult();
            Assert.IsFalse(panel.activeSelf, "HideResult must close the result panel");
        }
```

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ColorHuntHudPlayTests`
Expected: exit 2, `error CS1061: 'ColorHuntHud' does not contain a definition for 'RetryOverride'` (and `SetRetryLabel`, `HideResult`).

- [ ] **Step 6: Wire the buttons at runtime**

In `ColorHuntHud.cs`, add after the `S` field:

```csharp
        /// <summary>When set, the result panel's button calls this instead of dealing a new board (PotaFlow flies back to the basket).</summary>
        public System.Action RetryOverride;
```

Replace `Bind` with:

```csharp
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

        /// <summary>Short scale pop on the ammo number (transition arrival).</summary>
        public void PopAmmo()
        {
            if (_ammo != null) _pops[_ammo] = 1f;
        }
```

Delete the two edit-time listener lines: in `Create()` the line
`retry.GetComponent<Button>().onClick.AddListener(() => { if (hud.Game != null) hud.Game.NewRound(); });`
and in `MakeRotateButton()` the line
`button.onClick.AddListener(() => { if (Game != null) Game.Rotate(left ? Rotation.Counter : Rotation.Clockwise); });`
(keep the `var button = …` line, it sets the pressed colour).

- [ ] **Step 7: Run the fixture and the Color Hunt suite**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ColorHuntHudPlayTests Project.Tests.ColorHuntPlayTests`
Expected: exit 0, `PlayMode 3/3 passed`.

- [ ] **Step 8: Commit**

```bash
git add Tools/unity_tests.py Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntHud.cs Assets/_Project/Games/Pota/Tests/PlayMode/ColorHuntHudPlayTests.cs* Assets/_Project/Games/Pota/Tests/PlayMode/Pota.Tests.PlayMode.asmdef
git commit -q -m "fix: Color Hunt HUD wires its buttons at runtime; retry override, label, hide, ammo pop; unity_tests runner" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(Unity writes the new test's `.meta`; `git add …ColorHuntHudPlayTests.cs*` picks both up.)

---

### Task 2: `TimeScaleController.Clear()` (Core)

A swish slow-motion (0.05–0.3) can still be running when the basket round ends. The transition clock is unscaled anyway, but the
hero's animator, VFX and the board's staged views run on scaled time, so the director resets time when a phase starts.

**Files:**
- Modify: `Assets/_Project/Core/Scripts/Juice/TimeScaleController.cs`
- Test: `Assets/_Project/Core/Tests/PlayMode/TimeScaleControllerTests.cs`

**Interfaces:**
- Produces: `TimeScaleController.Clear()` — cancels slow-mo and hit-stop, `Time.timeScale = 1` and the base fixed step now.

- [ ] **Step 0: Let the Core PlayMode tests see the runtime**

`Assets/_Project/Core/Tests/PlayMode/Project.Tests.PlayMode.asmdef` references only the test runner (SceneBootTests needs nothing
else). Add `"Project.Runtime"` to its `references` array.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections;
using NUnit.Framework;
using Project.Juice;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class TimeScaleControllerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.Destroy(_go);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Clear_EndsSlowMotionNow_AndItStaysEnded()
        {
            _go = new GameObject("TimeCtl");
            var ctl = _go.AddComponent<TimeScaleController>();
            yield return null;                       // Start records the base fixed step
            float baseFixed = Time.fixedDeltaTime;
            ctl.SlowMo(0.2f, 10f);
            ctl.HitStop(10f);
            Assert.AreEqual(0.2f, Time.timeScale, 1e-4f);
            ctl.Clear();
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "Clear must restore normal time immediately");
            Assert.AreEqual(baseFixed, Time.fixedDeltaTime, 1e-6f);
            yield return null;                       // Update must not bring the old timers back
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.TimeScaleControllerTests`
Expected: exit 2, `error CS1061: 'TimeScaleController' does not contain a definition for 'Clear'`.

- [ ] **Step 3: Implement**

Add to `TimeScaleController` after `HitStop`:

```csharp
        /// <summary>Cancels any slow motion or hit-stop and restores normal time now (phase changes, e.g. PotaFlow's transition).</summary>
        public void Clear()
        {
            _slowUntil = -1f;
            _stopUntil = -1f;
            _slowScale = 1f;
            Time.timeScale = 1f;
            if (_baseFixed > 0f) Time.fixedDeltaTime = _baseFixed;
        }
```

- [ ] **Step 4: Run it and watch it pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.TimeScaleControllerTests`
Expected: exit 0, `PlayMode 1/1 passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Scripts/Juice/TimeScaleController.cs Assets/_Project/Core/Tests/PlayMode/TimeScaleControllerTests.cs* Assets/_Project/Core/Tests/PlayMode/Project.Tests.PlayMode.asmdef
git commit -q -m "feat: TimeScaleController.Clear ends slow motion and hit-stop immediately" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 3: Color Hunt staging, anchored camera fit, relative confetti, HUD slide-in

The director builds the board's round first (views at scale 0, input locked), counts the ammo up from outside, then starts play.
The board also lives at `BoardAnchor`, so the camera fit and the confetti origin must follow the game root.

**Files:**
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntGame.cs` (`Start`, `NewRound`, `Update`, `End`, new members)
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntCamera.cs`
- Modify: `Assets/_Project/Games/Pota/Scripts/ColorHunt/ColorHuntHud.cs` (`SetIntro`, `AmmoScreenPoint`)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/ColorHuntStagingTests.cs`, `Assets/_Project/Games/Pota/Tests/PlayMode/ColorHuntStagingPlayTests.cs`

**Interfaces:**
- Consumes: `ColorHuntHud.PopAmmo()` (Task 1).
- Produces:
  - `ColorHuntGame.AutoStart` (bool, default true), `NewRound()` / `NewRound(int ammo)`, `Prepare(int ammo)`, `ShowAmmo(int value)`, `Begin()`, `ConfettiOrigin` (Vector3).
  - `ColorHuntCamera.Anchor` (Vector3), `static Vector3 FitPosition(int n, float fov, float aspect, Vector3 anchor, float topBand = 1.25f, float bottomBand = 1.55f, float sidePad = 0.25f)`, `Vector3 FitFor(float fov, float aspect)`.
  - `ColorHuntHud.SetIntro(float p)` (0 = off-screen, 1 = exactly home), `Vector2 AmmoScreenPoint` (screen pixels of the ammo number).

- [ ] **Step 1: Write the failing EditMode tests**

```csharp
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntStagingTests
    {
        [Test]
        public void FitPosition_MovesWithTheAnchor()
        {
            var home = ColorHuntCamera.FitPosition(7, 34f, 9f / 16f, Vector3.zero);
            var anchor = new Vector3(0f, 8.5f, 30f);
            var moved = ColorHuntCamera.FitPosition(7, 34f, 9f / 16f, anchor);
            Assert.That(Vector3.Distance(moved - anchor, home), Is.LessThan(1e-4f));
        }

        [Test]
        public void FitPosition_KeepsTheExistingPortraitFit()
        {
            // Unchanged fit: 7×7 board, FOV 34, 9:16 is width-limited → 24.85 m back, centre 0.15 m below the board centre.
            var p = ColorHuntCamera.FitPosition(7, 34f, 9f / 16f, Vector3.zero);
            Assert.AreEqual(-24.85f, p.z, 0.05f);
            Assert.AreEqual(-0.15f, p.y, 0.01f);
            Assert.AreEqual(0f, p.x, 1e-5f);
        }

        [Test]
        public void ConfettiOrigin_FollowsTheGameRoot()
        {
            var go = new GameObject("Game");
            var settings = ScriptableObject.CreateInstance<ColorHuntSettings>();
            try
            {
                var game = go.AddComponent<ColorHuntGame>();
                game.S = settings;
                var atOrigin = game.ConfettiOrigin;
                go.transform.position = new Vector3(0f, 8.5f, 30f);
                Assert.That(Vector3.Distance(game.ConfettiOrigin, atOrigin + go.transform.position), Is.LessThan(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntStagingTests`
Expected: exit 2, `error CS0117: 'ColorHuntCamera' does not contain a definition for 'FitPosition'` and `'ColorHuntGame' does not contain a definition for 'ConfettiOrigin'`.

- [ ] **Step 3: Anchor the camera fit**

Replace the body of `ColorHuntCamera` after `_shake` with:

```csharp
        [Tooltip("World position of the board's game root (PotaFlow places the board away from the origin).")]
        public Vector3 Anchor;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _shake = GetComponent<CameraShake>();
        }

        private void LateUpdate()
        {
            var pos = FitFor(_cam.fieldOfView, _cam.aspect);
            transform.position = pos + (_shake != null ? _shake.Offset : Vector3.zero);
            transform.rotation = Quaternion.identity;
        }

        /// <summary>This camera's fit position with its own bands (valid while the component is disabled).</summary>
        public Vector3 FitFor(float fov, float aspect) =>
            FitPosition(S != null ? S.Size : 7, fov, aspect, Anchor, TopBand, BottomBand, SidePad);

        /// <summary>
        /// Camera position (looking +z) that fits an n×n board plus the launcher strip and HUD bands at this FOV and aspect,
        /// for a board whose game root sits at <paramref name="anchor"/>.
        /// </summary>
        public static Vector3 FitPosition(int n, float fov, float aspect, Vector3 anchor,
            float topBand = 1.25f, float bottomBand = 1.55f, float sidePad = 0.25f)
        {
            float halfW = n * 0.5f + BoardBuilder.Margin + sidePad;
            float top = n * 0.5f + BoardBuilder.Margin + topBand;
            float bottom = -(n * 0.5f + BoardBuilder.Margin + bottomBand);
            float halfH = (top - bottom) * 0.5f;
            float centerY = (top + bottom) * 0.5f;
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Max(halfW / (tan * aspect), halfH / tan) + 0.6f;
            return anchor + new Vector3(0f, centerY, -dist);
        }
```

- [ ] **Step 4: Relative confetti origin**

In `ColorHuntGame`, add under the `// ---------- public actions ----------` header:

```csharp
        /// <summary>Where the win confetti starts: above the board, relative to the game root (the board may sit anywhere).</summary>
        public Vector3 ConfettiOrigin =>
            transform.TransformPoint(new Vector3(0f, S.Size * 0.5f + BoardBuilder.Margin + 1.2f, -0.8f));
```

and in `End()` replace the two confetti lines with `Fx.Confetti(ConfettiOrigin, S.Palette, 260);`.

- [ ] **Step 5: Run the EditMode tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.ColorHuntStagingTests`
Expected: exit 0, `EditMode 3/3 passed`.

- [ ] **Step 6: Write the failing PlayMode staging tests**

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
    /// <summary>Staged rounds (PotaFlow): Prepare builds an idle, locked board; ShowAmmo drives the counter; Begin plays.</summary>
    public class ColorHuntStagingPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator LoadPlaying(Action<ColorHuntGame, ColorHuntHud> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            ready(game, Object.FindAnyObjectByType<ColorHuntHud>());
        }

        private static Text AmmoText(ColorHuntHud hud) => hud.transform.Find("AmmoPanel/Ammo").GetComponent<Text>();

        [UnityTest]
        public IEnumerator Prepare_StagesALockedBoard_ThenBeginPlaysIt()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });

            game.Prepare(37);
            Assert.AreEqual(ColorHuntGame.State.Idle, game.Current);
            Assert.AreEqual(37, game.Board.Ammo);
            Assert.IsNotNull(game.ViewAt(0), "views must exist while staged");
            Assert.AreEqual("locked", game.Tap(0));
            Assert.IsFalse(game.Rotate(Rotation.Clockwise), "rotation must be locked while staged");

            game.ShowAmmo(5);
            game.ViewAt(0).Scale = 0.5f;
            yield return null;
            Assert.AreEqual(5, game.DisplayedAmmo);
            Assert.AreEqual("5", AmmoText(hud).text);
            Assert.AreEqual(0.5f, game.ViewAt(0).transform.localScale.z, 1e-3f, "staged views must keep ticking");

            game.Begin();
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current);
            Assert.AreEqual(37, game.DisplayedAmmo);
            yield return null;
            Assert.AreEqual("37", AmmoText(hud).text);
        }

        [UnityTest]
        public IEnumerator NewRoundWithAmmo_StartsPlayingAtOnce()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            game.NewRound(12);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current);
            Assert.AreEqual(12, game.Board.Ammo);
        }

        [UnityTest]
        public IEnumerator SetIntro_SlidesThePanelsOut_AndBackExactly()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            var ammo = (RectTransform)hud.transform.Find("AmmoPanel");
            var targets = (RectTransform)hud.transform.Find("TargetPanel");
            var right = (RectTransform)hud.transform.Find("RotateRight");
            Vector2 ammoHome = ammo.anchoredPosition, targetsHome = targets.anchoredPosition, rightHome = right.anchoredPosition;

            hud.SetIntro(0f);
            Assert.Less(ammo.anchoredPosition.x, ammoHome.x - 300f, "ammo panel must leave to the left");
            Assert.Greater(targets.anchoredPosition.y, targetsHome.y + 250f, "target panel must leave upward");
            Assert.Less(right.anchoredPosition.y, rightHome.y - 250f, "rotate button must leave downward");

            hud.SetIntro(1f);
            Assert.AreEqual(ammoHome, ammo.anchoredPosition);
            Assert.AreEqual(targetsHome, targets.anchoredPosition);
            Assert.AreEqual(rightHome, right.anchoredPosition);

            Vector2 screen = hud.AmmoScreenPoint;
            Assert.That(screen.x, Is.InRange(0f, Screen.width / 2f), "ammo number sits on the left half");
            Assert.That(screen.y, Is.InRange(Screen.height / 2f, (float)Screen.height), "and on the upper half");

            hud.PopAmmo();
            yield return null;
            Assert.Greater(AmmoText(hud).transform.localScale.x, 1.01f, "ammo pop");
        }

        [UnityTest]
        public IEnumerator CameraFit_FollowsTheAnchor()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            var fit = Object.FindAnyObjectByType<ColorHuntCamera>();
            var cam = fit.GetComponent<Camera>();
            fit.Anchor = new Vector3(0f, 8.5f, 30f);
            yield return null;
            var expected = ColorHuntCamera.FitPosition(game.S.Size, cam.fieldOfView, cam.aspect, fit.Anchor);
            Assert.Less(Vector3.Distance(cam.transform.position, expected), 0.3f, "camera must fit the anchored board (shake aside)");
            Assert.Less(Vector3.Distance(fit.FitFor(cam.fieldOfView, cam.aspect), expected), 1e-4f);
            fit.Anchor = Vector3.zero;
        }
    }
}
```

- [ ] **Step 7: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ColorHuntStagingPlayTests`
Expected: exit 2, `error CS1061` for `Prepare`, `ShowAmmo`, `Begin`, `NewRound(int)` overload (`CS1501`), `SetIntro`, `AmmoScreenPoint`.

- [ ] **Step 8: Implement staging in `ColorHuntGame`**

Add fields after `public Camera Cam;`:

```csharp
        [Tooltip("Deal a board on Start. PotaFlow turns this off and stages the board itself (Prepare → Begin).")]
        public bool AutoStart = true;
```

and next to the other private fields: `private bool _staged;`

In `Start()` replace the final `NewRound();` with `if (AutoStart) NewRound();`.

Replace `NewRound()` with:

```csharp
        public void NewRound() => NewRound(S.Ammo);

        public void NewRound(int ammo)
        {
            Prepare(ammo);
            Begin();
        }

        /// <summary>
        /// Deals a board and builds its block views without starting play: views keep animating (PotaFlow scales them in),
        /// taps and rotations stay locked until <see cref="Begin"/>.
        /// </summary>
        public void Prepare(int ammo)
        {
            ClearViews();
            Seed = S.Seed != 0 ? S.Seed : (uint)UnityEngine.Random.Range(1, int.MaxValue);
            Board = ColorHuntRules.Create(S.ToDesign(), ammo, Seed);
            BuildViews(Board);
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
            Raise(TargetsChanged);
        }

        /// <summary>Shows a different number on the ammo counter while the board is staged (transition count-up).</summary>
        public void ShowAmmo(int value)
        {
            DisplayedAmmo = value;
            Raise(AmmoChanged);
        }

        /// <summary>Starts play on the staged board; the counter snaps to the real ammo.</summary>
        public void Begin()
        {
            if (Board == null) return;
            _staged = false;
            Current = State.Playing;
            DisplayedAmmo = Board.Ammo;
            Raise(AmmoChanged);
        }
```

In `Update()` replace the first line `if (Current == State.Idle) return;` with:

```csharp
            if (Current == State.Idle)
            {
                if (_staged) TickViews(Mathf.Min(Time.deltaTime, 0.05f));
                return;
            }
```

and replace its last two lines (`float comp = …; foreach (var v in _views.Values) v.Tick(dt, comp);`) with `TickViews(dt);`, then add:

```csharp
        private void TickViews(float dt)
        {
            float comp = -BoardRoot.localEulerAngles.z;
            foreach (var v in _views.Values) v.Tick(dt, comp);
        }
```

(Only `ColorHuntHud` listens to `RoundStarted`, and it does not look at `Current`, so raising it from `Prepare` is safe.)

- [ ] **Step 9: HUD slide-in and ammo screen point**

Add to `ColorHuntHud`:

```csharp
        private RectTransform _introAmmo, _introTargets, _introLeft, _introRight;
        private Vector2 _homeAmmo, _homeTargets, _homeLeft, _homeRight;
        private Color _hintHome;
        private bool _introCached;

        /// <summary>Slides the HUD in (PotaFlow): p = 0 panels off-screen and hint hidden, p = 1 exactly home. Ease-out.</summary>
        public void SetIntro(float p)
        {
            if (!_introCached) CacheIntro();
            p = Mathf.Clamp01(p);
            float e = 1f - (1f - p) * (1f - p) * (1f - p);
            float away = 1f - e;
            if (_introAmmo != null) _introAmmo.anchoredPosition = _homeAmmo + new Vector2(-420f * away, 0f);
            if (_introTargets != null) _introTargets.anchoredPosition = _homeTargets + new Vector2(0f, 320f * away);
            if (_introLeft != null) _introLeft.anchoredPosition = _homeLeft + new Vector2(0f, -340f * away);
            if (_introRight != null) _introRight.anchoredPosition = _homeRight + new Vector2(0f, -340f * away);
            if (_hint != null) { var c = _hintHome; c.a *= e; _hint.color = c; }
        }

        private void CacheIntro()
        {
            _introCached = true;
            _introAmmo = transform.Find("AmmoPanel") as RectTransform;
            _introTargets = transform.Find("TargetPanel") as RectTransform;
            _introLeft = transform.Find("RotateLeft") as RectTransform;
            _introRight = transform.Find("RotateRight") as RectTransform;
            if (_introAmmo != null) _homeAmmo = _introAmmo.anchoredPosition;
            if (_introTargets != null) _homeTargets = _introTargets.anchoredPosition;
            if (_introLeft != null) _homeLeft = _introLeft.anchoredPosition;
            if (_introRight != null) _homeRight = _introRight.anchoredPosition;
            if (_hint != null) _hintHome = _hint.color;
        }

        /// <summary>Screen pixels of the ammo number (overlay or camera canvas), for the transition badge to fly into.</summary>
        public Vector2 AmmoScreenPoint
        {
            get
            {
                if (_ammo == null) return Vector2.zero;
                var canvas = GetComponent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                return RectTransformUtility.WorldToScreenPoint(cam, _ammo.rectTransform.position);
            }
        }
```

- [ ] **Step 10: Run the staging tests and the whole Color Hunt suite**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.ColorHuntStagingPlayTests Project.Tests.ColorHuntHudPlayTests Project.Tests.ColorHuntPlayTests`
Expected: exit 0, `PlayMode 7/7 passed`.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode`
Expected: exit 0, all EditMode tests pass (52 = 49 before + 3).

- [ ] **Step 11: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/ColorHunt Assets/_Project/Games/Pota/Tests/EditMode/ColorHuntStagingTests.cs* Assets/_Project/Games/Pota/Tests/PlayMode/ColorHuntStagingPlayTests.cs*
git commit -q -m "feat: Color Hunt staged rounds (Prepare/ShowAmmo/Begin), anchored camera fit, relative confetti, HUD slide-in" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Basket hooks — result panel switch, camera resume, player reveal

**Files:**
- Modify: `Assets/_Project/Games/Pota/Scripts/Basket/BasketHud.cs`, `RoundCamera.cs`, `PlayerActor.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/BasketHudTests.cs`, `Assets/_Project/Games/Pota/Tests/PlayMode/BasketHooksPlayTests.cs`

**Interfaces:**
- Produces: `BasketHud.ShowResultPanel` (bool, default true); `RoundCamera.LookPoint` (Vector3, the smoothed look-at point); `RoundCamera` resumes from the camera's current transform whenever it is enabled; `PlayerActor.Reveal()` (show player + terrace pieces, base position/scale, Idle clip).

- [ ] **Step 1: Write the failing EditMode test**

```csharp
using System.Reflection;
using NUnit.Framework;
using Project.Basket;
using UnityEngine;

namespace Project.Tests
{
    public class BasketHudTests
    {
        [Test]
        public void RoundEnd_OpensTheResultPanel_OnlyWhenAllowed()
        {
            var root = new GameObject("UI");
            try
            {
                var hud = BasketHud.Create(root.transform);
                var onEnded = typeof(BasketHud).GetMethod("OnEnded", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(onEnded, "BasketHud.OnEnded(int) not found");
                hud.ResultPanel.SetActive(false);
                hud.ShowResultPanel = false;
                onEnded.Invoke(hud, new object[] { 42 });
                Assert.IsFalse(hud.ResultPanel.activeSelf, "PotaFlow: the basket result panel must stay closed");
                hud.ShowResultPanel = true;
                onEnded.Invoke(hud, new object[] { 42 });
                Assert.IsTrue(hud.ResultPanel.activeSelf, "Basket scene: the panel opens as before");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
```

- [ ] **Step 2: Run and watch it fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BasketHudTests`
Expected: exit 2, `error CS1061: 'BasketHud' does not contain a definition for 'ShowResultPanel'`.

- [ ] **Step 3: Implement the switch**

In `BasketHud` add after `public Button RetryButton;`:

```csharp
        [Tooltip("Open the result panel when the round ends (off in PotaFlow: the director flies on to the board).")]
        public bool ShowResultPanel = true;
```

and in `OnEnded` change the panel line to `if (ResultPanel != null && ShowResultPanel) ResultPanel.SetActive(true);`.

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BasketHudTests` → Expected: exit 0, `EditMode 1/1 passed`.

- [ ] **Step 4: Write the failing PlayMode tests**

```csharp
using System;
using System.Collections;
using NUnit.Framework;
using Project.Basket;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    public class BasketHooksPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator LoadAim(Action<BasketRound> ready)
        {
            SceneManager.LoadScene("Basket");
            yield return null;
            var round = Object.FindAnyObjectByType<BasketRound>();
            Assert.IsNotNull(round, "BasketRound missing");
            yield return WaitUntil(() => round.Column != null && round.Current == BasketRound.State.Aim, 5f);
            float until = Time.realtimeSinceStartup + 1f;          // let the camera settle on the aim pose (time, not frames)
            while (Time.realtimeSinceStartup < until) yield return null;
            ready(round);
        }

        [UnityTest]
        public IEnumerator RoundCamera_ResumesFromWhereTheCameraIs()
        {
            BasketRound round = null;
            yield return LoadAim(r => round = r);
            var rc = Object.FindAnyObjectByType<RoundCamera>();
            Assert.Less(Vector3.Distance(rc.LookPoint, round.Settings.AimLookAt), 0.5f, "LookPoint must be the aim look-at");

            rc.enabled = false;
            var far = new Vector3(40f, 30f, 60f);
            rc.transform.position = far;
            rc.enabled = true;
            yield return null;
            float fromFar = Vector3.Distance(rc.transform.position, far);
            float aimToFar = Vector3.Distance(round.Settings.AimCamPos, far);
            Assert.Less(fromFar, aimToFar * 0.5f, "camera jumped to a stale pose instead of blending from where it was");
        }

        [UnityTest]
        public IEnumerator PlayerReveal_ShowsTheHiddenPlayerAndStage()
        {
            BasketRound round = null;
            yield return LoadAim(r => round = r);
            var actor = round.Column.Player;
            Assert.IsNotNull(actor, "no player");
            actor.gameObject.SetActive(false);
            if (actor.AlsoHide != null) foreach (var go in actor.AlsoHide) if (go != null) go.SetActive(false);
            actor.Reveal();
            Assert.IsTrue(actor.gameObject.activeSelf);
            if (actor.AlsoHide != null) foreach (var go in actor.AlsoHide) if (go != null) Assert.IsTrue(go.activeSelf, go.name);
        }
    }
}
```

- [ ] **Step 5: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.BasketHooksPlayTests`
Expected: exit 2, `error CS1061` for `LookPoint` and `Reveal`.

- [ ] **Step 6: Implement `RoundCamera` resume + `LookPoint`**

In `RoundCamera`: delete the `_initialized` field and the whole `if (!_initialized) { … }` block in `LateUpdate`, then add:

```csharp
        /// <summary>The smoothed look-at point (PotaFlow starts its flight from here).</summary>
        public Vector3 LookPoint => _look;

        // Resume from wherever the camera is now (PotaFlow flies it back to the aim pose): no jump to a stale pose.
        private void OnEnable()
        {
            _pos = transform.position;
            _look = transform.position + transform.forward * 10f;
            _camY = float.MaxValue;
        }
```

(The scene tool already places the camera at the aim pose, so the first frame in the Basket scene is unchanged.)

- [ ] **Step 7: Implement `PlayerActor.Reveal()`**

Replace the body of the `if (s == BasketRound.State.Aim)` branch in `OnState` with a call to `Reveal();` and add:

```csharp
        /// <summary>Shows the player and its stage pieces at the base pose, idling (aim start; PotaFlow's transition).</summary>
        public void Reveal()
        {
            Show(true);
            transform.localScale = _baseScale;
            transform.position = _basePos;
            _hideAt = -1f;
            if (_animator != null) _animator.Play(IdleState, 0, 0f);
        }
```

- [ ] **Step 8: Run the hooks and the existing Basket tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.BasketHooksPlayTests Project.Tests.BasketRoundPlayTests`
Expected: exit 0, all pass (2 new + existing BasketRoundPlayTests).

- [ ] **Step 9: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/Basket Assets/_Project/Games/Pota/Tests/EditMode/BasketHudTests.cs* Assets/_Project/Games/Pota/Tests/PlayMode/BasketHooksPlayTests.cs*
git commit -q -m "feat: basket hooks for PotaFlow — optional result panel, camera resumes from its pose, player Reveal" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Scene tool helpers (behaviour unchanged) + build settings appended

PotaFlow's scene tool composes the Basket and Color Hunt set-ups; split their steps into public helpers. `BasketSceneTool` must
stop overwriting the build settings list.

**Files:**
- Create: `Assets/_Project/Games/Pota/Editor/SceneParts.cs`
- Modify: `Assets/_Project/Games/Pota/Editor/BasketSceneTool.cs` (`Build`), `ColorHuntSceneTool.cs` (`Build`, `EnsureSettings` → public)
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/SceneBuildSettingsTests.cs`

**Interfaces:**
- Produces:
  - `JuiceSet { GameObject Host; TimeScaleController TimeCtl; PopupText Popups; SynthSfx Sfx; CameraShake Shake; static JuiceSet Create(GameObject host, Camera cam, CameraShake shake, Font font = null) }`
  - `SceneParts.WithScene(EditorBuildSettingsScene[] current, string path)`, `SceneParts.AddToBuildSettings(string path)`, `SceneParts.CreateGlobalVolume(string profilePath)`, `SceneParts.CreateEventSystem(Transform parent)`
  - `BasketSceneTool.RoundParts { BasketRound Round; ShotInput Input; BasketJuice Juice; }`, `PrepareAssets(List<string>)`, `CreateCamera(BasketSettings)`, `CreateSun()`, `BuildRound(BasketSettings, int level, RoundCamera, JuiceSet)`, `ApplyRenderSettings(BasketSettings)`
  - `ColorHuntSceneTool.GameParts { ColorHuntGame Game; ColorHuntInput Input; ColorHuntHud Hud; ColorHuntJuice Juice; }`, `EnsureSettings(List<string>)` (public), `CreateSun()`, `CreateFill()`, `BuildGame(ColorHuntSettings, Camera, CameraShake, JuiceSet juiceOrNull, Transform uiRoot)`, `ApplyAmbient()`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;

namespace Project.Tests
{
    public class SceneBuildSettingsTests
    {
        private static string[] Paths(EditorBuildSettingsScene[] s) => Array.ConvertAll(s, x => x.path);

        [Test]
        public void WithScene_AppendsOnce_AndKeepsTheOrder()
        {
            var a = new EditorBuildSettingsScene("Assets/A.unity", true);
            var b = new EditorBuildSettingsScene("Assets/B.unity", true);
            var result = SceneParts.WithScene(new[] { a, b }, "Assets/C.unity");
            CollectionAssert.AreEqual(new[] { "Assets/A.unity", "Assets/B.unity", "Assets/C.unity" }, Paths(result));
            var again = SceneParts.WithScene(result, "Assets/A.unity");
            CollectionAssert.AreEqual(Paths(result), Paths(again), "an existing scene must not move or duplicate");
        }

        [Test]
        public void WithScene_EnablesADisabledEntry()
        {
            var a = new EditorBuildSettingsScene("Assets/A.unity", false);
            var result = SceneParts.WithScene(new[] { a }, "Assets/A.unity");
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(result[0].enabled);
        }
    }
}
```

- [ ] **Step 2: Run and watch it fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SceneBuildSettingsTests`
Expected: exit 2, `error CS0103: The name 'SceneParts' does not exist in the current context`.

- [ ] **Step 3: Write `SceneParts.cs`**

```csharp
using System.Collections.Generic;
using Project.Juice;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>One scene's juice components. One TimeScaleController per scene: two would fight over Time.timeScale.</summary>
    public sealed class JuiceSet
    {
        public GameObject Host;
        public TimeScaleController TimeCtl;
        public PopupText Popups;
        public SynthSfx Sfx;
        public CameraShake Shake;

        public static JuiceSet Create(GameObject host, Camera cam, CameraShake shake, Font font = null)
        {
            var set = new JuiceSet { Host = host, Shake = shake };
            set.TimeCtl = host.AddComponent<TimeScaleController>();
            set.Popups = host.AddComponent<PopupText>();
            set.Popups.Cam = cam;
            if (font != null) set.Popups.Font = font;
            set.Sfx = host.AddComponent<SynthSfx>();
            return set;
        }
    }

    /// <summary>Scene-building steps shared by the Pota scene tools.</summary>
    public static class SceneParts
    {
        /// <summary>The build list with <paramref name="path"/> present and enabled: appended if missing, never moved.</summary>
        public static EditorBuildSettingsScene[] WithScene(EditorBuildSettingsScene[] current, string path)
        {
            var list = new List<EditorBuildSettingsScene>(current ?? new EditorBuildSettingsScene[0]);
            int i = list.FindIndex(s => s.path == path);
            if (i < 0) list.Add(new EditorBuildSettingsScene(path, true));
            else if (!list[i].enabled) list[i] = new EditorBuildSettingsScene(path, true);
            return list.ToArray();
        }

        public static void AddToBuildSettings(string path) =>
            EditorBuildSettings.scenes = WithScene(EditorBuildSettings.scenes, path);

        public static Volume CreateGlobalVolume(string profilePath)
        {
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            return vol;
        }

        public static GameObject CreateEventSystem(Transform parent)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(parent, false);
            return es;
        }
    }
}
```

- [ ] **Step 4: Run it and watch it pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SceneBuildSettingsTests`
Expected: exit 0, `EditMode 2/2 passed`.

- [ ] **Step 5: Split `BasketSceneTool.Build` into helpers**

Add to `BasketSceneTool` (the bodies are the existing lines of `Build`, moved verbatim):

```csharp
        public sealed class RoundParts
        {
            public BasketRound Round;
            public ShotInput Input;
            public BasketJuice Juice;
        }

        /// <summary>Layers, settings asset and physics materials (idempotent).</summary>
        public static BasketSettings PrepareAssets(List<string> report)
        {
            foreach (var name in new[] { "BallFlight", "BallDrop", "Hoop", "Slab" })
                if (EnsureLayer(name)) report.Add($"layer {name} created");
            return EnsureSettings(report);
        }

        /// <summary>Main Camera at the aim pose with AudioListener, CameraShake and RoundCamera.</summary>
        public static Camera CreateCamera(BasketSettings settings)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = settings.Fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.transform.position = settings.AimCamPos;
            cam.transform.LookAt(settings.AimLookAt);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = settings.SkyColor;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraShake>();
            var roundCam = camGo.AddComponent<RoundCamera>();
            roundCam.S = settings;
            return cam;
        }

        public static Light CreateSun()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.957f, 0.878f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            return light;
        }

        /// <summary>Round (pool, round, aim preview, shot input) and its BasketJuice on the scene's juice host.</summary>
        public static RoundParts BuildRound(BasketSettings settings, int level, RoundCamera roundCam, JuiceSet juice)
        {
            var roundGo = new GameObject("Round");
            var pool = roundGo.AddComponent<BallPool>();
            pool.Settings = settings;
            var round = roundGo.AddComponent<BasketRound>();
            round.Settings = settings;
            round.Pool = pool;
            round.Level = level;
            var previewGo = new GameObject("AimPreview");
            previewGo.transform.SetParent(roundGo.transform, false);
            var preview = previewGo.AddComponent<AimPreview>();
            var input = roundGo.AddComponent<ShotInput>();
            input.Round = round;
            input.Preview = preview;
            roundCam.Round = round;
            roundCam.S = settings;

            var bj = juice.Host.AddComponent<BasketJuice>();
            bj.Round = round;
            bj.S = settings;
            bj.Shake = juice.Shake;
            bj.TimeCtl = juice.TimeCtl;
            bj.Popups = juice.Popups;
            bj.Sfx = juice.Sfx;
            return new RoundParts { Round = round, Input = input, Juice = bj };
        }

        public static void ApplyRenderSettings(BasketSettings settings)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = settings.FogColor;
            RenderSettings.fogStartDistance = settings.FogStart;
            RenderSettings.fogEndDistance = settings.FogEnd;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.94f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.72f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.54f, 0.48f, 0.4f);
        }
```

and make `Build(int level)` read:

```csharp
        public static List<string> Build(int level)
        {
            var report = new List<string>();
            var settings = PrepareAssets(report);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = CreateCamera(settings);
            CreateSun();
            SceneParts.CreateGlobalVolume(VolumeProfilePath);
            var juiceGo = new GameObject("Juice");
            var juice = JuiceSet.Create(juiceGo, cam, cam.GetComponent<CameraShake>());
            BuildRound(settings, level, cam.GetComponent<RoundCamera>(), juice);

            var uiRoot = new GameObject("UI");
            BasketHud.Create(uiRoot.transform);
            SceneParts.CreateEventSystem(uiRoot.transform);

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(VfxAuthoringTool.LibraryPath);
            if (library != null) VfxAuthoringTool.EnsureServiceInScene(library, report);
            else report.Add("VfxLibrary missing (run AI Tools/VFX/Create Starter Effects first)");

            ApplyRenderSettings(settings);

            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");
            SceneParts.AddToBuildSettings(ScenePath);
            report.Add("build settings: scene present");
            AssetDatabase.SaveAssets();
            return report;
        }
```

- [ ] **Step 6: Split `ColorHuntSceneTool.Build` into helpers**

Make `EnsureSettings` `public static`. Add (bodies moved verbatim from `Build`):

```csharp
        public sealed class GameParts
        {
            public ColorHuntGame Game;
            public ColorHuntInput Input;
            public ColorHuntHud Hud;
            public ColorHuntJuice Juice;
        }

        public static Light CreateSun()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            lightGo.transform.rotation = Quaternion.Euler(38f, -22f, 0f);
            return light;
        }

        public static Light CreateFill()
        {
            var fillGo = new GameObject("Fill Light");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.75f, 0.85f, 1f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(-20f, 160f, 0f);
            return fill;
        }

        /// <summary>
        /// Game root (rules/animation, Fx, salvo, input) and the HUD (+ binder) under <paramref name="uiRoot"/>.
        /// <paramref name="juice"/> null: a private Juice child with its own set (the Color Hunt scene); else the scene's shared set.
        /// </summary>
        public static GameParts BuildGame(ColorHuntSettings settings, Camera cam, CameraShake shake, JuiceSet juice, Transform uiRoot)
        {
            var gameGo = new GameObject("Game");
            var game = gameGo.AddComponent<ColorHuntGame>();
            game.S = settings;
            var fxGo = new GameObject("Effects");
            fxGo.transform.SetParent(gameGo.transform, false);
            var fx = fxGo.AddComponent<ColorHuntFx>();
            fx.S = settings;
            var salvoGo = new GameObject("Balls");
            salvoGo.transform.SetParent(gameGo.transform, false);
            var salvo = salvoGo.AddComponent<BallSalvo>();
            salvo.S = settings;
            salvo.BallMaterial = settings.BallMaterial;
            if (juice == null)
            {
                var juiceGo = new GameObject("Juice");
                juiceGo.transform.SetParent(gameGo.transform, false);
                juice = JuiceSet.Create(juiceGo, cam, shake, settings.Font);
            }
            var chJuice = juice.Host.AddComponent<ColorHuntJuice>();
            chJuice.S = settings; chJuice.Shake = juice.Shake; chJuice.TimeCtl = juice.TimeCtl; chJuice.Popups = juice.Popups; chJuice.Sfx = juice.Sfx;
            game.Fx = fx; game.Salvo = salvo; game.Juice = chJuice;
            var input = gameGo.AddComponent<ColorHuntInput>();
            input.Game = game; input.Cam = cam;

            var hud = ColorHuntHud.Create(uiRoot, settings);
            hud.Game = game;
            game.Hud = hud;
            game.Cam = cam;
            // HUD subscribes at runtime through a small bootstrap so the serialized reference is enough.
            uiRoot.gameObject.AddComponent<ColorHuntHudBinder>().Hud = hud;
            return new GameParts { Game = game, Input = input, Hud = hud, Juice = chJuice };
        }

        public static void ApplyAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.90f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.72f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.40f);
        }
```

and make `Build()` read:

```csharp
        public static List<string> Build()
        {
            var report = new List<string>();
            var settings = EnsureSettings(report);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = settings.SkyBottom;
            cam.transform.position = new Vector3(0f, 0f, -16f);
            camGo.AddComponent<AudioListener>();
            var shake = camGo.AddComponent<CameraShake>();
            var fit = camGo.AddComponent<ColorHuntCamera>();
            fit.S = settings;

            CreateSun();
            CreateFill();
            SceneParts.CreateGlobalVolume(VolumeProfilePath);

            var uiRoot = new GameObject("UI");
            BuildGame(settings, cam, shake, null, uiRoot.transform);
            SceneParts.CreateEventSystem(uiRoot.transform);

            ApplyAmbient();
            RenderSettings.fog = false;

            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");
            SceneParts.AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            report.Add(GameViewPortrait.Apply(1080, 1920));
            return report;
        }
```

Remove `using UnityEngine.EventSystems;` / `using UnityEngine.InputSystem.UI;` from either tool if the compiler reports them unused (warnings only; keep the build clean).

- [ ] **Step 7: Rebuild both scenes from the refactored tools**

Check the Basket scene's level first (read-only look at the YAML): `grep -m1 "  Level:" Assets/_Project/Games/Pota/Scenes/Basket.unity` → use that number as `L` (expected `1`).

Run (after compiling with `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.SceneBuildSettingsTests`):
```bash
~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_basket_build_scene","parameters":{"level":L}}'
~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_colorhunt_build_scene","parameters":{}}'
```
Expected: both `success: true`; build settings still `Basket.unity`, `ColorHunt.unity` in that order (`git diff ProjectSettings/EditorBuildSettings.asset` shows no path change).

- [ ] **Step 8: Run every PlayMode and EditMode test**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode` → Expected: exit 0, all pass (SceneBootTests, Basket, Color Hunt, hooks, time scale).
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` → Expected: exit 0, all pass.
Also look at both scenes once (`manage_scene load` each, Play, `manage_camera screenshot` game view, stop) and compare with `docs/screenshots` from before: same look.

- [ ] **Step 9: Commit**

```bash
git add Assets/_Project/Games/Pota/Editor Assets/_Project/Games/Pota/Scenes Assets/_Project/Games/Pota/Tests/EditMode/SceneBuildSettingsTests.cs* ProjectSettings/EditorBuildSettings.asset
git commit -q -m "refactor: Pota scene tools split into shared helpers (juice set, round, game, lights); build settings appended" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 6: `FlowTimeline` — the transition as a pure function of t

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/FlowTimeline.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/FlowTimelineTests.cs`

**Interfaces:**
- Produces (namespace `Project.Flow`):
  - `enum FlowCandidate { Stream, Simple }` (A "Akan toplar", B "Sade")
  - `[Serializable] class FlowTiming` with public float fields (defaults = spec §4): `HighlightEnd 0.30, LaunchStart 0.25, LaunchInterval 0.035, MaxVisible (int) 25, FirstLand 1.15, LastLand 2.20, CameraStart 0.30, CameraEnd 1.90, SettleTime 0.12, HeroStart 0.40, HeroEnd 0.90, RevealStart 1.75, RingInterval 0.07, PopTime 0.28, FrameGlowTime 0.40, HudStart 1.90, HudTime 0.35, CountEnd 2.20, BadgeDockEnd 0.80, BadgeFlyStart 1.95, BadgeFlyEnd 2.30, Ready 2.50, MinSkipSpeed 6, MaxSkipSeconds 0.35, ReturnTime 1.20`
  - `sealed class FlowTimeline(FlowTiming timing, int count, FlowCandidate candidate)`: fields `T`, `Candidate`, `Count`, `Visible`; `Ready`; `LaunchTime(int)`, `LandTime(int)`, `static Share(int i, int count, int visible)`, `LandedAt(float)`, `CountAt(float)`, `HighlightScale(float)`, `CameraProgress(float)`, `SettleAt(float)` (−1 outside), `HeroTurn(float)`, `RingStart(int)`, `RevealScale(float t, int ring)`, `FrameGlow(float)`, `HudProgress(float)`, `BadgeDock(float)`, `BadgeFly(float)`, `SkipSpeed(float)`, `static Smoother(float)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class FlowTimelineTests
    {
        private static readonly FlowTiming T = new FlowTiming();
        private static readonly int[] EdgeCounts = { 0, 1, 2, 3, 24, 25, 26, 158, 400, 999, 1000, 5000 };

        [Test]
        public void Shares_SumToTheBasketCount_ForEveryCount()
        {
            for (int n = 0; n <= 999; n++)
            {
                int v = Math.Min(n, T.MaxVisible), sum = 0;
                for (int i = 0; i < v; i++) sum += FlowTimeline.Share(i, n, v);
                Assert.AreEqual(n, sum, $"N={n}");
            }
            foreach (int n in new[] { 5000, 100000 })
            {
                int sum = 0;
                for (int i = 0; i < T.MaxVisible; i++) sum += FlowTimeline.Share(i, n, T.MaxVisible);
                Assert.AreEqual(n, sum, $"N={n}");
            }
        }

        [Test]
        public void Counter_EndsExactlyAtN_ForBothCandidates()
        {
            foreach (var c in new[] { FlowCandidate.Stream, FlowCandidate.Simple })
                foreach (int n in EdgeCounts)
                    Assert.AreEqual(n, new FlowTimeline(T, n, c).CountAt(T.Ready), $"{c} N={n}");
        }

        [Test]
        public void Counter_StartsAtZero_AndNeverGoesDown()
        {
            foreach (var c in new[] { FlowCandidate.Stream, FlowCandidate.Simple })
                foreach (int n in EdgeCounts)
                {
                    var tl = new FlowTimeline(T, n, c);
                    Assert.AreEqual(0, tl.CountAt(0f), $"{c} N={n} at 0");
                    int prev = 0;
                    for (float t = 0f; t <= 3f; t += 1f / 120f)
                    {
                        int now = tl.CountAt(t);
                        Assert.GreaterOrEqual(now, prev, $"{c} N={n} went down at t={t:0.000}");
                        prev = now;
                    }
                }
        }

        [Test]
        public void VisibleBalls_AreCappedAt25_AndLandInsideTheWindow()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            Assert.AreEqual(25, tl.Visible);
            Assert.AreEqual(0.25f, tl.LaunchTime(0), 1e-5f);
            Assert.LessOrEqual(tl.LaunchTime(24), 1.10f + 1e-4f);
            Assert.AreEqual(1.15f, tl.LandTime(0), 1e-5f);
            Assert.AreEqual(2.20f, tl.LandTime(24), 1e-4f);
            for (int i = 0; i < 25; i++)
                Assert.Greater(tl.LandTime(i) - tl.LaunchTime(i), 0.85f, $"ball {i} flight too short");
            Assert.AreEqual(3, new FlowTimeline(T, 3, FlowCandidate.Stream).Visible);
            Assert.AreEqual(0, new FlowTimeline(T, 158, FlowCandidate.Simple).Visible, "B has no ball stream");
            Assert.AreEqual(0, new FlowTimeline(T, 0, FlowCandidate.Stream).Visible);
        }

        [Test]
        public void FewBalls_StillArriveEarly()
        {
            var tl = new FlowTimeline(T, 3, FlowCandidate.Stream);
            Assert.Less(tl.LandTime(2), 1.30f, "three balls must not be stretched over the whole landing window");
            Assert.AreEqual(3, tl.CountAt(1.30f));
        }

        [Test]
        public void Camera_StartsAfterTheHighlight_AndArrivesAt190()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.CameraProgress(0.30f), 1e-6f);
            Assert.AreEqual(1f, tl.CameraProgress(1.90f), 1e-6f);
            float prev = 0f;
            for (float t = 0f; t <= 2.5f; t += 0.01f)
            {
                float u = tl.CameraProgress(t);
                Assert.GreaterOrEqual(u, prev - 1e-6f);
                prev = u;
            }
        }

        [Test]
        public void Settle_OnlyRightAfterArrival()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(-1f, tl.SettleAt(1.89f));
            Assert.AreEqual(0.5f, tl.SettleAt(1.96f), 1e-3f);
            Assert.AreEqual(-1f, tl.SettleAt(2.03f));
        }

        [Test]
        public void Highlight_PopsTo125_ThenSettlesAtOne()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.HighlightScale(0f));
            float peak = 0f;
            for (float t = 0f; t <= 0.3f; t += 0.001f) peak = Mathf.Max(peak, tl.HighlightScale(t));
            Assert.That(peak, Is.InRange(1.2f, 1.26f));
            Assert.AreEqual(1f, tl.HighlightScale(0.30f), 1e-5f);
        }

        [Test]
        public void RevealScale_PopsFromZeroThroughAbout112ToOne()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.RevealScale(1.749f, 0));
            float peak = 0f;
            for (float t = 1.75f; t < 2.1f; t += 0.001f) peak = Mathf.Max(peak, tl.RevealScale(t, 0));
            Assert.That(peak, Is.InRange(1.11f, 1.13f));
            Assert.AreEqual(1f, tl.RevealScale(1.75f + T.PopTime, 0), 1e-5f);
            Assert.AreEqual(0f, tl.RevealScale(1.80f, 1), "ring 1 starts 0.07 s after ring 0");
            Assert.AreEqual(1.82f, tl.RingStart(1), 1e-5f);
        }

        [Test]
        public void LastRingHudAndBadge_FinishBeforeReady()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.LessOrEqual(tl.RingStart(3) + T.PopTime, T.Ready, "7×7 has rings 0..3");
            Assert.AreEqual(1f, tl.HudProgress(T.HudStart + T.HudTime), 1e-5f);
            Assert.LessOrEqual(T.HudStart + T.HudTime, T.Ready);
            Assert.LessOrEqual(T.BadgeFlyEnd, T.Ready);
            Assert.LessOrEqual(T.Ready, 2.6f, "spec: playable board within 2.6 s");
        }

        [Test]
        public void Skip_AnyTime_FinishesWithin035Seconds()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            foreach (float t in new[] { 0f, 0.3f, 1f, 2f, 2.45f })
            {
                float speed = tl.SkipSpeed(t);
                Assert.GreaterOrEqual(speed, 6f);
                Assert.LessOrEqual((T.Ready - t) / speed, 0.35f + 1e-4f, $"skip at {t}");
            }
        }

        [Test]
        public void FrameGlow_PulsesWithTheReveal()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.FrameGlow(1.7f));
            Assert.AreEqual(1f, tl.FrameGlow(1.95f), 1e-3f);
            Assert.AreEqual(0f, tl.FrameGlow(2.2f));
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.FlowTimelineTests`
Expected: exit 2, `error CS0246: The type or namespace name 'FlowTiming' could not be found`.

- [ ] **Step 3: Implement**

```csharp
using System;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>Which transition plays (spec §4): A "Akan toplar" (balls stream into the giant hoop) or B "Sade".</summary>
    public enum FlowCandidate { Stream, Simple }

    /// <summary>Every time in the basket → board transition, in real (unscaled) seconds from the basket round's end.</summary>
    [Serializable]
    public class FlowTiming
    {
        [Header("Highlight")]
        public float HighlightEnd = 0.30f;
        [Header("Balls (A)")]
        public float LaunchStart = 0.25f;
        public float LaunchInterval = 0.035f;
        public int MaxVisible = 25;
        public float FirstLand = 1.15f;
        public float LastLand = 2.20f;
        [Header("Camera")]
        public float CameraStart = 0.30f;
        public float CameraEnd = 1.90f;
        public float SettleTime = 0.12f;
        [Header("Hero")]
        public float HeroStart = 0.40f;
        public float HeroEnd = 0.90f;
        [Header("Board")]
        public float RevealStart = 1.75f;
        public float RingInterval = 0.07f;
        public float PopTime = 0.28f;
        public float FrameGlowTime = 0.40f;
        [Header("HUD")]
        public float HudStart = 1.90f;
        public float HudTime = 0.35f;
        [Header("Count (B)")]
        public float CountEnd = 2.20f;
        [Header("Badge")]
        public float BadgeDockEnd = 0.80f;
        public float BadgeFlyStart = 1.95f;
        public float BadgeFlyEnd = 2.30f;
        [Header("End")]
        public float Ready = 2.50f;
        public float MinSkipSpeed = 6f;
        public float MaxSkipSeconds = 0.35f;
        [Header("Return")]
        public float ReturnTime = 1.20f;
    }

    /// <summary>
    /// The transition for one basket count N as a pure function of t: when each visible ball launches and lands, how much
    /// of N the ammo counter shows, how far the camera has flown, how big each board ring is, how far the HUD has slid.
    /// No Unity objects: the director asks "what is true at t?" every frame, so a skip or a dropped frame cannot desync.
    /// </summary>
    public sealed class FlowTimeline
    {
        public readonly FlowTiming T;
        public readonly FlowCandidate Candidate;
        public readonly int Count;
        public readonly int Visible;

        public FlowTimeline(FlowTiming timing, int count, FlowCandidate candidate)
        {
            T = timing;
            Candidate = candidate;
            Count = Mathf.Max(0, count);
            Visible = candidate == FlowCandidate.Stream ? Mathf.Min(Count, timing.MaxVisible) : 0;
        }

        public float Ready => T.Ready;

        public float LaunchTime(int i) => T.LaunchStart + i * T.LaunchInterval;

        /// <summary>Ball i reaches the rim. Spaced as if all MaxVisible balls flew, so a few balls still arrive early.</summary>
        public float LandTime(int i) => T.FirstLand + i * (T.LastLand - T.FirstLand) / Mathf.Max(1, T.MaxVisible - 1);

        /// <summary>Baskets carried by ball i: floor((i+1)N/V) − floor(iN/V), so the shares always sum to exactly N.</summary>
        public static int Share(int i, int count, int visible)
        {
            if (visible <= 0) return 0;
            return (int)((long)(i + 1) * count / visible - (long)i * count / visible);
        }

        public int LandedAt(float t)
        {
            int n = 0;
            while (n < Visible && LandTime(n) <= t) n++;
            return n;
        }

        /// <summary>The ammo counter at t: A sums the shares of the landed balls, B counts up with the flight.</summary>
        public int CountAt(float t)
        {
            if (Count == 0) return 0;
            if (Candidate == FlowCandidate.Stream) return (int)((long)LandedAt(t) * Count / Visible);
            float u = Mathf.Clamp01((t - T.CameraStart) / (T.CountEnd - T.CameraStart));
            return Mathf.RoundToInt(Count * u * u * (3f - 2f * u));
        }

        /// <summary>"N TOP" pop at the cage: 0 → 1.25 → 1 over the highlight.</summary>
        public float HighlightScale(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= T.HighlightEnd) return 1f;
            float u = t / T.HighlightEnd;
            if (u < 0.6f) { float a = u / 0.6f; return 1.25f * (1f - (1f - a) * (1f - a)); }
            return Mathf.Lerp(1.25f, 1f, (u - 0.6f) / 0.4f);
        }

        /// <summary>0 → 1 along the camera path, eased in and out, between CameraStart and CameraEnd.</summary>
        public float CameraProgress(float t) => Smoother(Mathf.Clamp01((t - T.CameraStart) / (T.CameraEnd - T.CameraStart)));

        /// <summary>0 → 1 through the short push-and-settle after arrival; −1 outside it.</summary>
        public float SettleAt(float t) =>
            t <= T.CameraEnd || t >= T.CameraEnd + T.SettleTime ? -1f : (t - T.CameraEnd) / T.SettleTime;

        public float HeroTurn(float t) => Smoother(Mathf.Clamp01((t - T.HeroStart) / (T.HeroEnd - T.HeroStart)));

        public float RingStart(int ring) => T.RevealStart + ring * T.RingInterval;

        /// <summary>Block scale of a ring: 0 before its start, overshoot to ~1.12, settle at 1 (ease-out-back, s = 1.9).</summary>
        public float RevealScale(float t, int ring)
        {
            float u = (t - RingStart(ring)) / T.PopTime;
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            const float s = 1.9f;                      // peak = 1 + 4s³ / (27(s+1)²) ≈ 1.121
            float x = u - 1f;
            return 1f + (s + 1f) * x * x * x + s * x * x;
        }

        /// <summary>Frame glow 0 → 1 → 0 starting with the reveal.</summary>
        public float FrameGlow(float t)
        {
            float u = (t - T.RevealStart) / T.FrameGlowTime;
            return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        public float HudProgress(float t) => Mathf.Clamp01((t - T.HudStart) / T.HudTime);

        /// <summary>Badge moving from the cage to its flight spot (0 → 1).</summary>
        public float BadgeDock(float t) => Smoother(Mathf.Clamp01((t - T.HighlightEnd) / (T.BadgeDockEnd - T.HighlightEnd)));

        /// <summary>Badge flying into the ammo panel (0 → 1).</summary>
        public float BadgeFly(float t) => Smoother(Mathf.Clamp01((t - T.BadgeFlyStart) / (T.BadgeFlyEnd - T.BadgeFlyStart)));

        /// <summary>Speed after a tap: at least MinSkipSpeed and fast enough to finish within MaxSkipSeconds.</summary>
        public float SkipSpeed(float t) => Mathf.Max(T.MinSkipSpeed, (T.Ready - t) / T.MaxSkipSeconds);

        public static float Smoother(float u) => u * u * u * (u * (u * 6f - 15f) + 10f);
    }
}
```

- [ ] **Step 4: Run and watch them pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.FlowTimelineTests`
Expected: exit 0, `EditMode 12/12 passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/Flow Assets/_Project/Games/Pota/Tests/EditMode/FlowTimelineTests.cs*
git commit -q -m "feat: PotaFlow timeline — ball launches/landings, exact ammo shares, camera, rings, HUD, skip" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `CameraFlight` — the camera path

Numbers checked before writing the plan (numpy, 401 samples): with the level-20 end pose, the side vista (13, 7, 17) and the
9:16 board fit, the path crosses the roof plane at x ≈ 10.8 (court half-width 7.5), and the heading turns 180° one way.

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/CameraFlight.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/CameraFlightTests.cs`

**Interfaces:**
- Produces: `struct CamPose { Vector3 Pos; Vector3 Look; float Fov; CamPose(Vector3, Vector3, float); Vector3 Forward; float Yaw }`; `static class CameraFlight { CamPose Evaluate(CamPose a, CamPose mid, CamPose b, float u); float Split(CamPose a, CamPose mid, CamPose b); CamPose Settle(CamPose b, float s, float amount); Vector3 CatmullRom(Vector3, Vector3, Vector3, Vector3, float) }`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class CameraFlightTests
    {
        // Level 20, 9:16: basket end pose over the cage, side vista, board fit at BoardAnchor (0, 8.5, 30).
        private static readonly CamPose End = new CamPose(new Vector3(0f, -21.78f, 13.5f), new Vector3(0f, -23.18f, 0f), 60f);
        private static readonly CamPose Mid = new CamPose(new Vector3(13f, 7f, 17f), new Vector3(0f, 8f, 15f), 52f);
        private static readonly CamPose Board = new CamPose(new Vector3(0f, 8.35f, 5.15f), new Vector3(0f, 8.35f, 30f), 34f);
        private static readonly CamPose Aim = new CamPose(new Vector3(0f, 2.6f, 11f), new Vector3(0f, 3f, 0f), 60f);
        // PotaFlowSettings defaults: court 15 × 28 m from z 8.4, top at y 0, 0.4 m thick; +0.5 m margin all round.
        private static readonly Bounds Court = new Bounds(new Vector3(0f, -0.2f, 22.4f), new Vector3(16f, 1.4f, 29f));

        private static void AssertSame(CamPose want, CamPose got)
        {
            Assert.Less(Vector3.Distance(want.Pos, got.Pos), 1e-4f);
            Assert.Less(Vector3.Distance(want.Look, got.Look), 1e-4f);
            Assert.AreEqual(want.Fov, got.Fov, 1e-4f);
        }

        [Test]
        public void Endpoints_AreExact()
        {
            AssertSame(End, CameraFlight.Evaluate(End, Mid, Board, 0f));
            AssertSame(Board, CameraFlight.Evaluate(End, Mid, Board, 1f));
        }

        [Test]
        public void PassesThroughTheVista()
        {
            AssertSame(Mid, CameraFlight.Evaluate(End, Mid, Board, CameraFlight.Split(End, Mid, Board)));
        }

        [Test]
        public void Heading_TurnsHalfAWay_OneDirectionOnly()
        {
            foreach (var (a, b) in new[] { (End, Board), (Board, Aim) })
            {
                float prev = a.Yaw, total = 0f;
                for (int i = 1; i <= 400; i++)
                {
                    float yaw = CameraFlight.Evaluate(a, Mid, b, i / 400f).Yaw;
                    float d = Mathf.DeltaAngle(prev, yaw);
                    total += d;
                    prev = yaw;
                }
                Assert.AreEqual(180f, Mathf.Abs(total), 2f, "a half turn");
                prev = a.Yaw;
                float sign = Mathf.Sign(total);
                for (int i = 1; i <= 400; i++)
                {
                    float yaw = CameraFlight.Evaluate(a, Mid, b, i / 400f).Yaw;
                    Assert.GreaterOrEqual(sign * Mathf.DeltaAngle(prev, yaw), -0.05f, $"heading turned back at u={i / 400f}");
                    prev = yaw;
                }
            }
        }

        [Test]
        public void Fov_StaysBetweenTheEnds()
        {
            for (int i = 0; i <= 200; i++)
            {
                float fov = CameraFlight.Evaluate(End, Mid, Board, i / 200f).Fov;
                Assert.That(fov, Is.InRange(34f - 1e-3f, 60f + 1e-3f));
            }
        }

        [Test]
        public void Path_NeverCutsThroughTheCourt_EitherWay()
        {
            for (int i = 0; i <= 400; i++)
            {
                Assert.IsFalse(Court.Contains(CameraFlight.Evaluate(End, Mid, Board, i / 400f).Pos), $"forward u={i / 400f}");
                Assert.IsFalse(Court.Contains(CameraFlight.Evaluate(Board, Mid, Aim, i / 400f).Pos), $"return u={i / 400f}");
            }
        }

        [Test]
        public void Settle_PushesForward_AndComesBack()
        {
            AssertSame(Board, CameraFlight.Settle(Board, 0f, 0.35f));
            AssertSame(Board, CameraFlight.Settle(Board, 1f, 0.35f));
            Assert.AreEqual(Board.Pos.z + 0.35f, CameraFlight.Settle(Board, 0.5f, 0.35f).Pos.z, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CameraFlightTests`
Expected: exit 2, `error CS0246: The type or namespace name 'CamPose' could not be found`.

- [ ] **Step 3: Implement**

```csharp
using UnityEngine;

namespace Project.Flow
{
    /// <summary>A camera pose: eye position, look-at point and vertical field of view.</summary>
    public struct CamPose
    {
        public Vector3 Pos;
        public Vector3 Look;
        public float Fov;

        public CamPose(Vector3 pos, Vector3 look, float fov)
        {
            Pos = pos;
            Look = look;
            Fov = fov;
        }

        public Vector3 Forward => (Look - Pos).normalized;

        /// <summary>Heading in degrees, atan2(x, z): 0 looks +z, ±180 looks −z.</summary>
        public float Yaw
        {
            get
            {
                var d = Look - Pos;
                return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
        }
    }

    /// <summary>
    /// The transition's camera path: Catmull-Rom through a → mid → b for the eye and, with the same timing, for the look-at
    /// point (phantom end points mirror the neighbours). Time is split by segment length so the speed has no kink at mid.
    /// FOV eases a → mid → b. Exact at u = 0 and u = 1.
    /// </summary>
    public static class CameraFlight
    {
        public static CamPose Evaluate(CamPose a, CamPose mid, CamPose b, float u)
        {
            if (u <= 0f) return a;
            if (u >= 1f) return b;
            float split = Split(a, mid, b);
            var pos = Through(a.Pos, mid.Pos, b.Pos, u, split);
            var look = Through(a.Look, mid.Look, b.Look, u, split);
            float fov = u < split
                ? Mathf.Lerp(a.Fov, mid.Fov, Smooth(u / split))
                : Mathf.Lerp(mid.Fov, b.Fov, Smooth((u - split) / (1f - split)));
            return new CamPose(pos, look, fov);
        }

        /// <summary>The u at which the path passes mid (share of the eye's path length before mid).</summary>
        public static float Split(CamPose a, CamPose mid, CamPose b)
        {
            float la = Vector3.Distance(a.Pos, mid.Pos), lb = Vector3.Distance(mid.Pos, b.Pos);
            return la + lb > 1e-4f ? la / (la + lb) : 0.5f;
        }

        /// <summary>After arrival: push along the view by amount·sin(πs) and come back (s 0 → 1).</summary>
        public static CamPose Settle(CamPose b, float s, float amount)
        {
            var push = b.Forward * (amount * Mathf.Sin(Mathf.Clamp01(s) * Mathf.PI));
            return new CamPose(b.Pos + push, b.Look + push, b.Fov);
        }

        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float s)
        {
            float s2 = s * s, s3 = s2 * s;
            return 0.5f * (2f * p1 + (p2 - p0) * s + (2f * p0 - 5f * p1 + 4f * p2 - p3) * s2 + (3f * p1 - p0 - 3f * p2 + p3) * s3);
        }

        private static Vector3 Through(Vector3 a, Vector3 m, Vector3 b, float u, float split)
        {
            var before = 2f * a - m;
            var after = 2f * b - m;
            return u < split
                ? CatmullRom(before, a, m, b, u / split)
                : CatmullRom(a, m, b, after, (u - split) / (1f - split));
        }

        private static float Smooth(float u) => u * u * (3f - 2f * u);
    }
}
```

- [ ] **Step 4: Run and watch them pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.CameraFlightTests`
Expected: exit 0, `EditMode 6/6 passed`. If `Path_NeverCutsThroughTheCourt_EitherWay` fails on the return path, move the default vista outward (MidPos x 13 → 16) in Task 10's settings and in this test's `Mid`, and ledger the ruling.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/Flow/CameraFlight.cs* Assets/_Project/Games/Pota/Tests/EditMode/CameraFlightTests.cs*
git commit -q -m "feat: PotaFlow camera flight — Catmull-Rom eye/look path, FOV blend, settle" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `GiantHoop` — backboard, frame, rim, net hopper, pole

Proportions from Pota (`src/pota.ts`, `src/sahaYerlesim.ts` DEV_POTA: rim R 1.6 at y −5.6 on an 8.8 board, forward R + 0.6,
6-ring net 1.8 deep, orange frame 0.35). Unity board cell = 1 m, board half = 3.5 + 0.42 → rim centre local (0, −5.62, −2.2),
world (0, 2.88, 27.8): a real hoop height above the court.

**Files:**
- Modify: `Assets/_Project/Games/Pota/Scripts/Basket/Visual/NetBuilder.cs` (optional parameters, defaults unchanged)
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/GiantHoop.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/GiantHoopTests.cs`

**Interfaces:**
- Produces: `NetBuilder.Build(Transform netRoot, float rimRadius, Material mat, int rings = 5, int strings = 12, float drop = 0.11f, float thickness = 0.012f, float ringTube = 0.01f)`; `GiantHoop` (MonoBehaviour): `const int MaxPile = 10`; fields `RimRadius 1.6, RimTube 0.075, RimDrop 1.7, RimForward 2.2, NetDepth 1.6, NetRings 6, GlassMargin 0.75, FrameWidth 0.35, BallRadius 0.24`, colours; `Transform Rim, Net`; `Vector3 RimCenter`; `int Pile`; `Build(int boardSize)`, `Vector3 SlotPosition(int i)`, `AddToPile()`, `ClearPile()`, `Tick(float dt)`, `SetGlow(float g)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class GiantHoopTests
    {
        private GameObject _root;

        private GiantHoop Build()
        {
            _root = new GameObject("Game");
            _root.transform.position = new Vector3(0f, 8.5f, 30f);
            var hoop = new GameObject("GiantHoop").AddComponent<GiantHoop>();
            hoop.transform.SetParent(_root.transform, false);
            hoop.Build(7);
            return hoop;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Rim_SitsBelowTheBoard_InFrontOfIt()
        {
            var h = Build();
            Assert.Less(Vector3.Distance(h.RimCenter, new Vector3(0f, 8.5f - 3.92f - 1.7f, 27.8f)), 1e-3f);
        }

        [Test]
        public void Pole_StandsOnTheRoof()
        {
            var h = Build();
            var pole = h.transform.Find("Pole").GetComponent<Renderer>();
            Assert.AreEqual(0f, pole.bounds.min.y, 0.02f);
        }

        [Test]
        public void EveryPileSlot_IsInsideTheNet_WithoutOverlap()
        {
            var h = Build();
            for (int i = 0; i < GiantHoop.MaxPile; i++)
            {
                var local = h.Rim.InverseTransformPoint(h.SlotPosition(i));
                float depth = -local.y;
                Assert.That(depth, Is.InRange(h.BallRadius, h.NetDepth - h.BallRadius * 0.9f), $"slot {i} depth");
                float netR = h.RimRadius * (1f - 0.11f * h.NetRings * depth / h.NetDepth);
                float r = new Vector2(local.x, local.z).magnitude;
                Assert.LessOrEqual(r + h.BallRadius, netR + 0.01f, $"slot {i} pokes through the net");
                for (int j = 0; j < i; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(h.SlotPosition(i), h.SlotPosition(j)), 2f * h.BallRadius * 0.95f, $"slots {j},{i} overlap");
            }
        }

        [Test]
        public void Pile_BulgesTheNet_AndClears()
        {
            var h = Build();
            h.AddToPile();
            h.Tick(0.01f);
            Assert.AreEqual(1, h.Pile);
            Assert.Greater(h.Net.localScale.x, 1.05f);
            for (int i = 0; i < 20; i++) h.AddToPile();
            Assert.AreEqual(GiantHoop.MaxPile, h.Pile, "the pile is capped");
            h.ClearPile();
            Assert.AreEqual(0, h.Pile);
            Assert.AreEqual(Vector3.one, h.Net.localScale);
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.GiantHoopTests`
Expected: exit 2, `error CS0246: The type or namespace name 'GiantHoop' could not be found`.

- [ ] **Step 3: NetBuilder parameters**

Change the signature and three literals in `NetBuilder.Build`:

```csharp
        public static void Build(Transform netRoot, float rimRadius, Material mat, int rings = 5, int strings = 12,
            float drop = 0.11f, float thickness = 0.012f, float ringTube = 0.01f)
```

Delete the line `float drop = 0.11f;`, use `RimMeshBuilder.Torus(radii[k], ringTube, 24, 6)` for the rings and
`new Vector3(thickness, dir.magnitude * 0.5f, thickness)` for the string scale. (Defaults equal the old literals: basket hoops unchanged.)

- [ ] **Step 4: Write `GiantHoop.cs`**

```csharp
using Project.Basket.Visual;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Flow
{
    /// <summary>
    /// The giant hoop around the Color Hunt board (Pota pota.ts proportions, board cell = 1 m): frosted glass backboard,
    /// orange frame, rim under the board sticking out toward the camera, a 6-ring net that is the ammo hopper (up to
    /// <see cref="MaxPile"/> balls rest in it, it bulges as they land) and a pole down to the rooftop (world y = 0).
    /// Built at runtime as a child of the board's game root; local space = board space (the board faces −z).
    /// </summary>
    public class GiantHoop : MonoBehaviour
    {
        public const int MaxPile = 10;

        public float RimRadius = 1.6f;
        public float RimTube = 0.075f;
        [Tooltip("Rim centre below the board's outer edge (Pota: rim y −5.6 on an 8.8 m board).")]
        public float RimDrop = 1.7f;
        [Tooltip("Rim centre in front of the board (Pota: R + 0.6).")]
        public float RimForward = 2.2f;
        public float NetDepth = 1.6f;
        public int NetRings = 6;
        public float GlassMargin = 0.75f;
        public float FrameWidth = 0.35f;
        public float BallRadius = 0.24f;
        public Color FrameColor = new Color(1f, 0.54f, 0.16f);
        public Color GlassColor = new Color(0.85f, 0.93f, 1f, 0.28f);
        public Color NetColor = new Color(0.97f, 0.97f, 0.95f);
        public Color PoleColor = new Color(0.16f, 0.2f, 0.3f);

        public Transform Rim { get; private set; }
        public Transform Net { get; private set; }
        public Vector3 RimCenter => Rim.position;
        public int Pile { get; private set; }

        private Material _frameMat, _rimMat;
        private float _kick;

        public void Build(int boardSize)
        {
            float edge = boardSize * 0.5f + BoardBuilder.Margin;
            float half = edge + GlassMargin;
            _frameMat = Emissive(FrameColor, 0.5f);
            _rimMat = Emissive(FrameColor, 0.6f);
            var glassMat = Transparent(GlassColor);
            var netMat = RimMeshBuilder.UrpLit(NetColor, 0.2f);
            var poleMat = RimMeshBuilder.UrpLit(PoleColor, 0.4f);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            Box("Glass", cube, glassMat, new Vector3(0f, 0f, 0.95f), new Vector3(half * 2f, half * 2f, 0.1f));
            float fw = FrameWidth;
            Box("FrameTop", cube, _frameMat, new Vector3(0f, half, 0.9f), new Vector3(half * 2f + fw, fw, 0.3f));
            Box("FrameBottom", cube, _frameMat, new Vector3(0f, -half, 0.9f), new Vector3(half * 2f + fw, fw, 0.3f));
            Box("FrameLeft", cube, _frameMat, new Vector3(-half, 0f, 0.9f), new Vector3(fw, half * 2f + fw, 0.3f));
            Box("FrameRight", cube, _frameMat, new Vector3(half, 0f, 0.9f), new Vector3(fw, half * 2f + fw, 0.3f));

            float rimY = -edge - RimDrop;
            Rim = RimMeshBuilder.MeshObject("Rim", transform, RimMeshBuilder.Torus(RimRadius, RimTube, 48, 12), _rimMat).transform;
            Rim.localPosition = new Vector3(0f, rimY, -RimForward);
            float backZ = -RimForward + RimRadius;
            Box("RimStrut", cube, _rimMat, new Vector3(0f, (rimY - half) * 0.5f, 0.9f), new Vector3(0.3f, Mathf.Abs(rimY + half) + 0.12f, 0.2f));
            Box("RimBracket", cube, _rimMat, new Vector3(0f, rimY, (backZ + 0.9f) * 0.5f), new Vector3(0.4f, 0.12f, 0.9f - backZ));

            Net = new GameObject("Net").transform;
            Net.SetParent(Rim, false);
            NetBuilder.Build(Net, RimRadius * 0.98f, netMat, NetRings, 16, NetDepth / NetRings, 0.03f, 0.022f);

            // Pole from the roof (world y = 0; the board root is never rotated or scaled) up to the glass centre, plus an arm.
            float ground = -transform.position.y;
            float len = -ground;
            var pole = RimMeshBuilder.MeshObject("Pole", transform, cyl, poleMat);
            pole.transform.localPosition = new Vector3(0f, ground + len * 0.5f, 1.9f);
            pole.transform.localScale = new Vector3(0.35f, len * 0.5f, 0.35f);
            Box("PoleArm", cube, poleMat, new Vector3(0f, 0f, 1.42f), new Vector3(0.25f, 0.25f, 0.95f));
            SetGlow(0f);
        }

        /// <summary>World position of pile slot i (0 = bottom): 3 + 4 + 3 balls in three layers inside the net cone.</summary>
        public Vector3 SlotPosition(int i)
        {
            i = Mathf.Clamp(i, 0, MaxPile - 1);
            int layer = i < 3 ? 0 : i < 7 ? 1 : 2;
            int first = layer == 0 ? 0 : layer == 1 ? 3 : 7;
            int count = layer == 1 ? 4 : 3;
            float depth = NetDepth - BallRadius * (1.2f + 1.7f * layer);
            float netR = RimRadius * (1f - 0.11f * NetRings * depth / NetDepth);     // NetBuilder's taper
            float r = Mathf.Max(0f, netR - BallRadius * 1.1f);
            float a = (i - first) * Mathf.PI * 2f / count + layer * 0.6f;
            return Rim.TransformPoint(new Vector3(Mathf.Cos(a) * r, -depth, Mathf.Sin(a) * r));
        }

        public void AddToPile()
        {
            Pile = Mathf.Min(MaxPile, Pile + 1);
            _kick = 1f;
        }

        public void ClearPile()
        {
            Pile = 0;
            _kick = 0f;
            if (Net != null) Net.localScale = Vector3.one;
        }

        /// <summary>Net shape: a fuller net is wider; every landing adds a short kick that decays.</summary>
        public void Tick(float dt)
        {
            _kick = Mathf.Max(0f, _kick - dt * 5f);
            float fill = Pile / (float)MaxPile;
            float w = 1f + 0.10f * fill + 0.08f * _kick;
            Net.localScale = new Vector3(w, 1f + 0.06f * fill + 0.05f * _kick, w);
        }

        /// <summary>Frame and rim glow 0 → 1 (HDR emission: blooms at the top).</summary>
        public void SetGlow(float g)
        {
            var c = FrameColor * (0.15f + 2.4f * Mathf.Clamp01(g));
            _frameMat.SetColor("_EmissionColor", c);
            _rimMat.SetColor("_EmissionColor", c);
        }

        private GameObject Box(string name, Mesh mesh, Material mat, Vector3 localPos, Vector3 size)
        {
            var go = RimMeshBuilder.MeshObject(name, transform, mesh, mat);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            return go;
        }

        private static Material Emissive(Color c, float smoothness)
        {
            var m = RimMeshBuilder.UrpLit(c, smoothness);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        private static Material Transparent(Color c)
        {
            var m = RimMeshBuilder.UrpLit(c, 0.9f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }
    }
}
```

- [ ] **Step 5: Run the hoop tests and the Basket tests (NetBuilder is shared)**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.GiantHoopTests` → Expected: exit 0, `EditMode 4/4 passed`.
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.BasketRoundPlayTests` → Expected: exit 0.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/Basket/Visual/NetBuilder.cs Assets/_Project/Games/Pota/Scripts/Flow/GiantHoop.cs* Assets/_Project/Games/Pota/Tests/EditMode/GiantHoopTests.cs*
git commit -q -m "feat: PotaFlow giant hoop — glass board, orange frame, rim, net hopper with pile slots and bulge, pole" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `BallStream` — candidate A's visible balls

Path checked before writing the plan (numpy): cubic Bézier with vertical end tangents, launch kick (0, 0, 0.8), apex = highest end
+ 9 m (+0…1.2 per ball), sources jittered z 1.3–1.9 in front of the cage, rim entry jittered ±0.56 m. Worst case: 2.25 m above the
terrace (hero's head ≈ 1.8 m), 6.5 m above the court, the ball is above the roof before z = 5.1 (terrace starts at 6), and it drops
into the rim straight down. A kick of 2.5 m came within 0.9 m of the terrace and was rejected.

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/BallStream.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/BallStreamTests.cs`

**Interfaces:**
- Consumes: `FlowTimeline` (Task 6: `Visible`, `LaunchTime`, `LandTime`), `GiantHoop` (Task 8: `RimCenter`, `RimRadius`, `NetDepth`, `SlotPosition`, `AddToPile`, `MaxPile`).
- Produces: `BallStream` (MonoBehaviour): fields `BallMaterial, BallRadius 0.24, ApexAbove 9, ApexJitter 1.2, LaunchKick (0,0,0.8), DropTime 0.15, Spread 0.6, TrailColor`; events `Action<int> Launched, Landed`; `int ActiveCount, Capacity`; `Prewarm(int)`, `Begin(FlowTimeline, Vector3 source, GiantHoop)`, `Tick(float t)`, `Clear()`, `static Vector3 PathPoint(Vector3 a, Vector3 b, float apexY, Vector3 kick, float u)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class BallStreamTests
    {
        // Level 20: cage mouth front (0, −20.65, 1.6); giant hoop at BoardAnchor (0, 8.5, 30) → rim (0, 2.88, 27.8).
        private static readonly Vector3 Mouth = new Vector3(0f, -20.65f, 1.6f);
        private static readonly Vector3 RimPoint = new Vector3(0f, 2.88f, 27.8f);
        private static readonly Vector3 Kick = new Vector3(0f, 0f, 0.8f);
        private const float Apex = 2.88f + 9f;

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private BallStream Make(int count, out GiantHoop hoop)
        {
            _root = new GameObject("Flow");
            var game = new GameObject("Game");
            game.transform.SetParent(_root.transform, false);
            game.transform.position = new Vector3(0f, 8.5f, 30f);
            hoop = new GameObject("GiantHoop").AddComponent<GiantHoop>();
            hoop.transform.SetParent(game.transform, false);
            hoop.Build(7);
            var stream = new GameObject("BallStream").AddComponent<BallStream>();
            stream.transform.SetParent(_root.transform, false);
            stream.Prewarm(25);
            stream.Begin(new FlowTimeline(new FlowTiming(), count, FlowCandidate.Stream), Mouth, hoop);
            return stream;
        }

        [Test]
        public void Path_StartsAndEndsExactly_AndDropsStraightIntoTheRim()
        {
            Assert.Less(Vector3.Distance(BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 0f), Mouth), 1e-4f);
            Assert.Less(Vector3.Distance(BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 1f), RimPoint), 1e-4f);
            var d = (BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 1f) - BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 0.97f)).normalized;
            Assert.Less(d.y, -0.95f, "the ball must come down through the rim, not slide in sideways");
        }

        [Test]
        public void Path_TopIsNearTheApex()
        {
            float top = float.MinValue;
            for (int i = 0; i <= 1000; i++) top = Mathf.Max(top, BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, i / 1000f).y);
            Assert.AreEqual(Apex, top, 0.6f);
        }

        [Test]
        public void Path_ClearsTheRoofAndTheHero()
        {
            for (int i = 0; i <= 1000; i++)
            {
                var p = BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, i / 1000f);
                if (p.z >= 8.4f && p.z <= 26f) Assert.Greater(p.y, 5f, $"too close to the court at u={i / 1000f}");
                if (p.z >= 6f && p.z < 8.4f) Assert.Greater(p.y, 2.2f, $"through the hero on the terrace at u={i / 1000f}");
                if (p.y < 0f) Assert.Less(p.z, 6f, "must rise above the roof in front of the terrace");
            }
        }

        [Test]
        public void Tick_RaisesEveryLandingOnce_EvenWithOneBigStep()
        {
            var stream = Make(158, out var hoop);
            var landed = new List<int>();
            stream.Landed += landed.Add;
            stream.Tick(0f);
            stream.Tick(0.5f);
            stream.Tick(2.5f);
            CollectionAssert.AreEqual(Enumerable.Range(0, 25).ToList(), landed);
            stream.Tick(2.6f);
            Assert.AreEqual(25, landed.Count, "no landing raised twice");
            Assert.AreEqual(GiantHoop.MaxPile, hoop.Pile);
        }

        [Test]
        public void AfterTheLastLanding_TenBallsRestInTheirSlots()
        {
            var stream = Make(158, out var hoop);
            stream.Tick(0f);
            stream.Tick(2.5f);
            Assert.AreEqual(GiantHoop.MaxPile, stream.ActiveCount);
            var shown = stream.GetComponentsInChildren<MeshRenderer>(false);
            Assert.AreEqual(GiantHoop.MaxPile, shown.Length);
            for (int i = 0; i < GiantHoop.MaxPile; i++)
                Assert.IsTrue(shown.Any(r => Vector3.Distance(r.transform.position, hoop.SlotPosition(i)) < 1e-3f), $"slot {i} empty");
        }

        [Test]
        public void Launches_FollowTheTimeline()
        {
            var stream = Make(158, out _);
            stream.Tick(0.2f);
            Assert.AreEqual(0, stream.ActiveCount);
            stream.Tick(0.26f);
            Assert.AreEqual(1, stream.ActiveCount);
            stream.Tick(1.10f);
            Assert.AreEqual(25, stream.ActiveCount);
        }

        [Test]
        public void ThreeBaskets_ThreeBalls_AllResting()
        {
            var stream = Make(3, out var hoop);
            stream.Tick(0f);
            stream.Tick(2.5f);
            Assert.AreEqual(3, stream.ActiveCount);
            Assert.AreEqual(3, hoop.Pile);
            stream.Clear();
            Assert.AreEqual(0, stream.ActiveCount);
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BallStreamTests`
Expected: exit 2, `error CS0246: The type or namespace name 'BallStream' could not be found`.

- [ ] **Step 3: Implement**

```csharp
using System;
using Project.Basket.Visual;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>
    /// Candidate A's visible balls: pooled spheres (no physics) with short trails. Positions are a pure function of the
    /// timeline time t, so a skip or a long frame cannot lose a ball: <see cref="Tick"/> raises <see cref="Landed"/> once for
    /// every ball whose rim time has passed since the previous tick. Path: cubic Bézier leaving the cage mouth straight up
    /// (a little forward), arcing over the roof and dropping straight down through the rim, then a short fall to its pile
    /// slot (the first <see cref="GiantHoop.MaxPile"/> balls) or out of sight into the net.
    /// </summary>
    public class BallStream : MonoBehaviour
    {
        public Material BallMaterial;
        public float BallRadius = 0.24f;
        [Tooltip("Path top = the higher end + this (spec: 9 m).")]
        public float ApexAbove = 9f;
        [Tooltip("Extra apex per ball, 0..this (never lower: lower paths graze the hero).")]
        public float ApexJitter = 1.2f;
        public Vector3 LaunchKick = new Vector3(0f, 0f, 0.8f);
        public float DropTime = 0.15f;
        [Tooltip("Source spread across the cage mouth (x ± this, z ± half of it).")]
        public float Spread = 0.6f;
        public Color TrailColor = new Color(1f, 0.62f, 0.25f, 0.8f);

        public event Action<int> Launched;
        public event Action<int> Landed;
        public int ActiveCount { get; private set; }
        public int Capacity => _balls.Length;

        private Transform[] _balls = new Transform[0];
        private TrailRenderer[] _trails = new TrailRenderer[0];
        private Vector3[] _from = new Vector3[0], _entry = new Vector3[0], _axis = new Vector3[0];
        private float[] _apex = new float[0];
        private FlowTimeline _tl;
        private GiantHoop _hoop;
        private float _lastT;
        private bool _running;

        /// <summary>Creates the ball objects up front (no allocation or hitch when the transition starts).</summary>
        public void Prewarm(int count)
        {
            int old = _balls.Length;
            if (count <= old) return;
            Array.Resize(ref _balls, count);
            Array.Resize(ref _trails, count);
            Array.Resize(ref _from, count);
            Array.Resize(ref _entry, count);
            Array.Resize(ref _axis, count);
            Array.Resize(ref _apex, count);
            var mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var mat = BallMaterial != null ? BallMaterial : RimMeshBuilder.UrpLit(new Color(0.93f, 0.45f, 0.13f), 0.35f);
            var trailMat = new Material(Shader.Find("Sprites/Default"));
            for (int i = old; i < count; i++)
            {
                var go = RimMeshBuilder.MeshObject($"FlowBall{i:00}", transform, mesh, mat);
                go.transform.localScale = Vector3.one * (BallRadius * 2f);
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMat;
                trail.time = 0.12f;
                trail.minVertexDistance = 0.05f;
                trail.widthMultiplier = BallRadius * 1.4f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.startColor = TrailColor;
                trail.endColor = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0f);
                trail.emitting = false;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                _balls[i] = go.transform;
                _trails[i] = trail;
            }
        }

        public void Begin(FlowTimeline tl, Vector3 source, GiantHoop hoop)
        {
            Prewarm(tl.Visible);
            Clear();
            _tl = tl;
            _hoop = hoop;
            _lastT = float.NegativeInfinity;
            _running = true;
            var rim = hoop.RimCenter;
            for (int i = 0; i < tl.Visible; i++)
            {
                _from[i] = source + new Vector3((Hash(i, 1) - 0.5f) * 2f * Spread, 0f, (Hash(i, 2) - 0.5f) * Spread);
                float a = Hash(i, 3) * Mathf.PI * 2f, r = Hash(i, 4) * hoop.RimRadius * 0.35f;
                _entry[i] = rim + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                _apex[i] = Mathf.Max(_from[i].y, _entry[i].y) + ApexAbove + Hash(i, 5) * ApexJitter;
                var axis = new Vector3(Hash(i, 6) - 0.5f, Hash(i, 7) - 0.5f, Hash(i, 8) - 0.5f);
                _axis[i] = axis.sqrMagnitude > 1e-4f ? axis.normalized : Vector3.up;
            }
        }

        /// <summary>Places every ball for timeline time t and raises launch/landing events crossed since the last tick.</summary>
        public void Tick(float t)
        {
            if (!_running) return;
            int active = 0;
            for (int i = 0; i < _tl.Visible; i++)
            {
                float launch = _tl.LaunchTime(i), land = _tl.LandTime(i);
                if (_lastT < launch && t >= launch) Launched?.Invoke(i);
                if (_lastT < land && t >= land)
                {
                    _hoop.AddToPile();
                    Landed?.Invoke(i);
                }
                bool resting = i < GiantHoop.MaxPile;
                bool visible = t >= launch && (resting || t < land + DropTime);
                var ball = _balls[i];
                if (ball.gameObject.activeSelf != visible)
                {
                    ball.gameObject.SetActive(visible);
                    if (visible) _trails[i].Clear();
                }
                if (!visible) continue;
                active++;
                if (t < land)
                {
                    float u = (t - launch) / (land - launch);
                    ball.position = PathPoint(_from[i], _entry[i], _apex[i], LaunchKick, u);
                    _trails[i].emitting = true;
                }
                else
                {
                    float d = Mathf.Clamp01((t - land) / DropTime);
                    var target = resting ? _hoop.SlotPosition(i) : _hoop.RimCenter + Vector3.down * (_hoop.NetDepth + BallRadius);
                    ball.position = Vector3.Lerp(_entry[i], target, d * d);
                    _trails[i].emitting = false;
                }
                ball.rotation = Quaternion.AngleAxis(t * 540f, _axis[i]);
            }
            ActiveCount = active;
            _lastT = t;
        }

        public void Clear()
        {
            _running = false;
            ActiveCount = 0;
            for (int i = 0; i < _balls.Length; i++)
            {
                _trails[i].emitting = false;
                _trails[i].Clear();
                _balls[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Cubic Bézier with vertical end tangents: leaves a upward (plus kick), comes down into b; top ≈ apexY.</summary>
        public static Vector3 PathPoint(Vector3 a, Vector3 b, float apexY, Vector3 kick, float u)
        {
            u = Mathf.Clamp01(u);
            var p1 = a + new Vector3(0f, (apexY - a.y) * 4f / 3f, 0f) + kick;
            var p2 = b + new Vector3(0f, (apexY - b.y) * 4f / 3f, 0f);
            float v = 1f - u;
            return v * v * v * a + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * b;
        }

        private static float Hash(int i, int k)
        {
            unchecked
            {
                uint h = (uint)(i * 73856093) ^ (uint)(k * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xffffff) / 16777216f;
            }
        }
    }
}
```

- [ ] **Step 4: Run and watch them pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BallStreamTests`
Expected: exit 0, `EditMode 7/7 passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/Flow/BallStream.cs* Assets/_Project/Games/Pota/Tests/EditMode/BallStreamTests.cs*
git commit -q -m "feat: PotaFlow ball stream — pooled balls on a deterministic arc into the giant hoop, every landing counted" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 10: Settings asset, rooftop court, board reveal rings, "N TOP" badge

The spec puts the court "behind the terrace (z 6 → 36)"; the terrace itself covers z 6–8.4 at y = 0, so the court starts at its
back edge (8.4) to avoid two coplanar floors fighting, and runs 28 m (Pota's court proportions) to z 36.4.
The badge test uses `UnityEngine.UI.Text`: add `"UnityEngine.UI"` to `Pota.Tests.EditMode.asmdef` references in Step 1.

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/PotaFlowSettings.cs`, `CourtBuilder.cs`, `BoardReveal.cs`, `FlowBadge.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/BoardRevealTests.cs`, `CourtBuilderTests.cs`

**Interfaces:**
- Consumes: `FlowTiming`, `FlowCandidate`, `FlowTimeline.RevealScale` (Task 6); `ColorHuntGame.Board`, `ViewAt`, `BlockView.Scale`; `ColorHuntRules.Row/Col`; `BasketEnvironmentBuilder.TexturedLit(Texture2D, Color, float)`; `RimMeshBuilder.MeshObject`.
- Produces:
  - `PotaFlowSettings` (ScriptableObject): `Candidate`, `Timing` (FlowTiming), `BasketLevel 20`, `BoardAnchor (0, 8.5, 30)`, `CourtSize (15, 28)`, `CourtStartZ 8.4`, `CourtColor #d9773a`, `CourtLineColor`, `BoardFov 34`, `MidPos (13, 7, 17)`, `MidLook (0, 8, 15)`, `MidFov 52`, `FovPunch 3`, `SettleAmount 0.35`, `BoardFogStart 60`, `BoardFogEnd 220`, `BallRadius 0.24`, `ApexAbove 9`, `LaunchKick (0, 0, 0.8)`, `HeroTurnDegrees 180`.
  - `CourtBuilder.Build(Transform parent, PotaFlowSettings s)` → `GameObject "Court"` (no collider); `CourtBuilder.CourtTexture(int w, int h, Color floor, Color line)`.
  - `BoardReveal.Ring(int size, int row, int col)`, `RingCount(int size)`, `Apply(ColorHuntGame, FlowTimeline, float t)`, `Finish(ColorHuntGame)`.
  - `FlowBadge.Create(Transform parent, Font font, Color color, Color outline)`, `Show(int count)`, `Hide()`, `Place(Vector2 screen, float scale, float alpha)`, `Label`, `Visible`, `Canvas`.

- [ ] **Step 1: Write the failing tests**

`BoardRevealTests.cs`:

```csharp
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class BoardRevealTests
    {
        [Test]
        public void Rings_CountOutFromTheCentre()
        {
            Assert.AreEqual(0, BoardReveal.Ring(7, 3, 3));
            Assert.AreEqual(1, BoardReveal.Ring(7, 2, 3));
            Assert.AreEqual(2, BoardReveal.Ring(7, 1, 4));
            Assert.AreEqual(3, BoardReveal.Ring(7, 0, 0));
            Assert.AreEqual(3, BoardReveal.Ring(7, 6, 2));
            Assert.AreEqual(4, BoardReveal.RingCount(7));
            Assert.AreEqual(0, BoardReveal.Ring(6, 2, 3), "even boards: the middle four are ring 0");
            Assert.AreEqual(3, BoardReveal.RingCount(6));
        }

        [Test]
        public void Badge_ShowsTheCount_ScalesAndFades()
        {
            var root = new GameObject("Flow");
            try
            {
                var badge = FlowBadge.Create(root.transform, null, Color.yellow, Color.blue);
                Assert.IsFalse(badge.Visible);
                badge.Show(158);
                Assert.IsTrue(badge.Visible);
                Assert.AreEqual("158 TOP", badge.Label);
                badge.Place(new Vector2(100f, 200f), 1.2f, 0.5f);
                var label = badge.transform.Find("Label");
                Assert.AreEqual(1.2f, label.localScale.x, 1e-5f);
                Assert.AreEqual(0.5f, label.GetComponent<UnityEngine.UI.Text>().color.a, 1e-3f);
                badge.Hide();
                Assert.IsFalse(badge.Visible);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Settings_DefaultsMatchTheSpec()
        {
            var s = ScriptableObject.CreateInstance<PotaFlowSettings>();
            try
            {
                Assert.AreEqual(FlowCandidate.Stream, s.Candidate);
                Assert.AreEqual(20, s.BasketLevel);
                Assert.AreEqual(new Vector3(0f, 8.5f, 30f), s.BoardAnchor);
                Assert.AreEqual(34f, s.BoardFov);
                Assert.AreEqual(52f, s.MidFov);
                Assert.AreEqual(60f, s.BoardFogStart);
                Assert.AreEqual(220f, s.BoardFogEnd);
                Assert.AreEqual(2.5f, s.Timing.Ready);
            }
            finally { Object.DestroyImmediate(s); }
        }
    }
}
```

`CourtBuilderTests.cs`:

```csharp
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class CourtBuilderTests
    {
        private static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        [Test]
        public void CourtTexture_WhiteLinesOnOrange()
        {
            var orange = new Color(0.851f, 0.467f, 0.227f);
            var tex = CourtBuilder.CourtTexture(256, 512, orange, Color.white);
            try
            {
                Assert.Less(Diff(tex.GetPixel(64, 128), orange), 0.03f, "floor");
                Assert.Less(Diff(tex.GetPixel(128, 256), Color.white), 0.03f, "half-court line");
                Assert.Less(Diff(tex.GetPixel(0, 300), Color.white), 0.03f, "side line");
                Assert.Less(Diff(tex.GetPixel(128, 511), Color.white), 0.03f, "end line under the hoop");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Court_IsTheRoofBehindTheTerrace_WithoutCollider()
        {
            var s = ScriptableObject.CreateInstance<PotaFlowSettings>();
            var root = new GameObject("Flow");
            try
            {
                var court = CourtBuilder.Build(root.transform, s);
                var b = court.GetComponent<Renderer>().bounds;
                Assert.AreEqual(0f, b.max.y, 1e-3f);
                Assert.AreEqual(8.4f, b.min.z, 1e-3f);
                Assert.AreEqual(36.4f, b.max.z, 1e-3f);
                Assert.AreEqual(7.5f, b.max.x, 1e-3f);
                Assert.IsNull(court.GetComponent<Collider>(), "taps and basket physics must not hit the court");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(s);
            }
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BoardRevealTests Project.Tests.CourtBuilderTests`
Expected: exit 2, `error CS0103: The name 'BoardReveal' does not exist`, `'FlowBadge'`, `'PotaFlowSettings'`, `'CourtBuilder'`.

- [ ] **Step 3: `PotaFlowSettings.cs`**

```csharp
using UnityEngine;

namespace Project.Flow
{
    /// <summary>Every PotaFlow number in one Inspector-editable asset (spec 2026-09-29 §3–4; Pota sources in comments).</summary>
    [CreateAssetMenu(menuName = "Project/Pota/Flow Settings", fileName = "PotaFlowSettings")]
    public class PotaFlowSettings : ScriptableObject
    {
        [Header("Choreography")]
        public FlowCandidate Candidate = FlowCandidate.Stream;
        public FlowTiming Timing = new FlowTiming();
        [Tooltip("Basket level played before the board (20: median 158 baskets = Pota level 20 ammo).")]
        public int BasketLevel = 20;

        [Header("World")]
        [Tooltip("Board game root. Translation only: the board faces −z, toward the tower and the hero.")]
        public Vector3 BoardAnchor = new Vector3(0f, 8.5f, 30f);
        public Vector2 CourtSize = new Vector2(15f, 28f);
        public float CourtStartZ = 8.4f;
        public Color CourtColor = new Color(0.851f, 0.467f, 0.227f);   // Pota saha.ts zeminDokusu #d9773a
        public Color CourtLineColor = Color.white;

        [Header("Camera")]
        public float BoardFov = 34f;
        public Vector3 MidPos = new Vector3(13f, 7f, 17f);
        public Vector3 MidLook = new Vector3(0f, 8f, 15f);
        public float MidFov = 52f;
        public float FovPunch = 3f;
        public float SettleAmount = 0.35f;
        public float BoardFogStart = 60f;
        public float BoardFogEnd = 220f;

        [Header("Balls")]
        public float BallRadius = 0.24f;
        public float ApexAbove = 9f;
        public Vector3 LaunchKick = new Vector3(0f, 0f, 0.8f);

        [Header("Hero")]
        public float HeroTurnDegrees = 180f;
    }
}
```

- [ ] **Step 4: `CourtBuilder.cs`**

```csharp
using Project.Basket.Visual;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>
    /// Rooftop court behind the terrace (Pota saha.ts look): orange floor with white lines drawn into a texture in code,
    /// key and three-point arc at the far end under the giant hoop. Top face at y = 0. No collider.
    /// </summary>
    public static class CourtBuilder
    {
        public static GameObject Build(Transform parent, PotaFlowSettings s)
        {
            var tex = CourtTexture(512, 1024, s.CourtColor, s.CourtLineColor);
            var mat = BasketEnvironmentBuilder.TexturedLit(tex, Color.white, 0.25f);
            var go = RimMeshBuilder.MeshObject("Court", parent, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), mat);
            go.transform.position = new Vector3(0f, -0.2f, s.CourtStartZ + s.CourtSize.y * 0.5f);
            go.transform.localScale = new Vector3(s.CourtSize.x, 0.4f, s.CourtSize.y);
            return go;
        }

        /// <summary>Court markings: v = 0 is the terrace end, v = 1 the hoop end.</summary>
        public static Texture2D CourtTexture(int w, int h, Color floor, Color line)
        {
            var px = new Color32[w * h];
            Color32 f = floor, l = line;
            for (int i = 0; i < px.Length; i++) px[i] = f;
            int lw = Mathf.Max(2, w / 96);

            void Fill(int x0, int y0, int x1, int y1)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                        px[y * w + x] = l;
            }

            void Circle(float cx, float cy, float r)
            {
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - lw)), y1 = Mathf.Min(h, Mathf.CeilToInt(cy + r + lw));
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - lw)), x1 = Mathf.Min(w, Mathf.CeilToInt(cx + r + lw));
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (Mathf.Abs(d - r) <= lw * 0.5f) px[y * w + x] = l;
                    }
            }

            Fill(0, 0, w, lw);                 // terrace end line
            Fill(0, h - lw, w, h);             // hoop end line
            Fill(0, 0, lw, h);                 // side lines
            Fill(w - lw, 0, w, h);
            Fill(0, h / 2 - lw / 2, w, h / 2 - lw / 2 + lw);   // half-court line
            Circle(w * 0.5f, h * 0.5f, w * 0.12f);              // centre circle
            int kx0 = Mathf.RoundToInt(w * 0.335f), kx1 = Mathf.RoundToInt(w * 0.665f), ky = Mathf.RoundToInt(h * 0.8f);
            Fill(kx0, ky, kx0 + lw, h);        // key
            Fill(kx1 - lw, ky, kx1, h);
            Fill(kx0, ky - lw / 2, kx1, ky - lw / 2 + lw);
            Circle(w * 0.5f, ky, w * 0.165f);  // free-throw circle
            Circle(w * 0.5f, h * 0.94f, w * 0.42f);   // three-point arc (clipped by the end line)

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "CourtTexture", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
```

- [ ] **Step 5: `BoardReveal.cs`**

```csharp
using Project.ColorHunt;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>Board entrance: blocks pop in ring by ring from the centre (Chebyshev rings), timed by the FlowTimeline.</summary>
    public static class BoardReveal
    {
        public static int Ring(int size, int row, int col)
        {
            float c = (size - 1) * 0.5f;
            return Mathf.FloorToInt(Mathf.Max(Mathf.Abs(row - c), Mathf.Abs(col - c)));
        }

        public static int RingCount(int size) => Ring(size, 0, 0) + 1;

        /// <summary>Sets every staged block view's scale for timeline time t (0.001 instead of 0: no degenerate matrices).</summary>
        public static void Apply(ColorHuntGame game, FlowTimeline tl, float t)
        {
            var board = game.Board;
            if (board == null) return;
            int n = board.Size;
            for (int cell = 0; cell < board.Cells.Length; cell++)
            {
                var v = game.ViewAt(cell);
                if (v == null) continue;
                int ring = Ring(n, ColorHuntRules.Row(n, cell), ColorHuntRules.Col(n, cell));
                v.Scale = Mathf.Max(0.001f, tl.RevealScale(t, ring));
            }
        }

        public static void Finish(ColorHuntGame game)
        {
            var board = game.Board;
            if (board == null) return;
            for (int cell = 0; cell < board.Cells.Length; cell++)
            {
                var v = game.ViewAt(cell);
                if (v != null) v.Scale = 1f;
            }
        }
    }
}
```

- [ ] **Step 6: `FlowBadge.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace Project.Flow
{
    /// <summary>
    /// The transition's "N TOP" label on its own overlay canvas (above both HUDs): pops at the cage, docks while the camera
    /// flies, then flies into the ammo panel. The text is set once per transition (no per-frame allocation).
    /// </summary>
    public class FlowBadge : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _canvasRt, _rt;
        private Text _text;
        private Color _color;

        public string Label => _text.text;
        public bool Visible => _canvas.enabled;
        public Canvas Canvas => _canvas;

        public static FlowBadge Create(Transform parent, Font font, Color color, Color outline)
        {
            var go = new GameObject("FlowBadge", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            label.transform.SetParent(go.transform, false);
            var rt = label.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 200f);
            var text = label.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 120;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            var o = label.GetComponent<Outline>();
            o.effectColor = outline;
            o.effectDistance = new Vector2(6f, -6f);

            var badge = go.AddComponent<FlowBadge>();
            badge._canvas = canvas;
            badge._canvasRt = (RectTransform)go.transform;
            badge._rt = rt;
            badge._text = text;
            badge._color = color;
            canvas.enabled = false;
            return badge;
        }

        public void Show(int count)
        {
            _text.text = count + " TOP";
            _canvas.enabled = true;
        }

        public void Hide() => _canvas.enabled = false;

        /// <summary>Screen pixels (bottom-left origin), uniform scale, alpha 0..1.</summary>
        public void Place(Vector2 screen, float scale, float alpha)
        {
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screen, cam, out var local))
                _rt.anchoredPosition = local;
            _rt.localScale = Vector3.one * scale;
            var c = _color;
            c.a = _color.a * Mathf.Clamp01(alpha);
            _text.color = c;
        }
    }
}
```

- [ ] **Step 7: Run and watch them pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.BoardRevealTests Project.Tests.CourtBuilderTests`
Expected: exit 0, `EditMode 5/5 passed`.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/Flow Assets/_Project/Games/Pota/Tests/EditMode/BoardRevealTests.cs* Assets/_Project/Games/Pota/Tests/EditMode/CourtBuilderTests.cs* Assets/_Project/Games/Pota/Tests/EditMode/Pota.Tests.EditMode.asmdef
git commit -q -m "feat: PotaFlow settings asset, rooftop court texture, ring reveal, N TOP badge" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: `PotaFlowDirector` + `ai_potaflow_build_scene` + flow PlayMode tests

The spec's "kafes kenarı parlar" (0–0.3 s) is played as the Core `pickup` flash at the cage mouth plus the badge pop: the cage has
no emissive material to pulse; Task 13 judges whether it reads. The spec's "alt halkalar şişer" is the whole net widening with
the fill plus a kick per landing (`GiantHoop.Tick`).

**Files:**
- Create: `Assets/_Project/Games/Pota/Scripts/Flow/PotaFlowDirector.cs`
- Create: `Assets/_Project/Games/Pota/Editor/PotaFlowSceneTool.cs`
- Create (by the tool): `Assets/_Project/Games/Pota/Scenes/PotaFlow.unity`, `Assets/_Project/Games/Pota/Settings/PotaFlowSettings.asset`; modifies `PotaProfile.asset` (Scenes list, via SerializedObject) and `ProjectSettings/EditorBuildSettings.asset` (append)
- Test: `Assets/_Project/Games/Pota/Tests/PlayMode/PotaFlowPlayTests.cs`

**Interfaces:**
- Consumes: everything above — `TimeScaleController.Clear`; `ColorHuntGame.Prepare/ShowAmmo/Begin/ViewAt/Board/BoardRoot/RoundEnded`; `ColorHuntHud.RetryOverride/SetRetryLabel/HideResult/PopAmmo/SetIntro/AmmoScreenPoint`; `ColorHuntCamera.Anchor/FitFor`; `BasketHud.ShowResultPanel`; `RoundCamera.LookPoint`; `PlayerActor.Reveal`; `BasketRound.RoundEnded/Column/Settings/ResetRound`; `FlowTimeline`, `CameraFlight`, `GiantHoop`, `BallStream`, `BoardReveal`, `CourtBuilder`, `FlowBadge`, `PotaFlowSettings`; `SceneParts`, `JuiceSet`, `BasketSceneTool.*`, `ColorHuntSceneTool.*`.
- Produces: `PotaFlowDirector` with `enum Phase { Basket, Transition, Puzzle, Result, Return }`, `Current`, `Clock`, `Count`, `Skipping`, `Timeline`, `Hoop`, `Stream`, `Badge`, `ActiveCandidate`, `CandidateOverride` (`FlowCandidate?`), `ForcedCount`, `FixedStep`, `PointerPressedThisFrame` / `PointerHeld` (`Func<bool>`), `event Action<Phase> PhaseChanged`, `bool BeginTransition(int)`, `void Skip()`, `bool ReturnToBasket()`, `void Advance(float dt)`; public refs `S, Round, Shot, RoundCam, BasketHud, Game, BoardInput, BoardCam, Hud, Cam, Shake, TimeCtl, Sfx`. MCP tool `ai_potaflow_build_scene`.

- [ ] **Step 1: Write the failing PlayMode tests**

```csharp
using System;
using System.Collections;
using NUnit.Framework;
using Project.Basket;
using Project.ColorHunt;
using Project.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Phase = Project.Flow.PotaFlowDirector.Phase;

namespace Project.Tests
{
    /// <summary>PotaFlow scene as loaded from disk: basket round end → transition → playable board → retry → basket.</summary>
    public class PotaFlowPlayTests
    {
        private const float Step = 1f / 60f;

        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator Load(Action<PotaFlowDirector> ready)
        {
            SceneManager.LoadScene("PotaFlow");
            yield return null;
            var d = Object.FindAnyObjectByType<PotaFlowDirector>();
            Assert.IsNotNull(d, "PotaFlowDirector missing");
            yield return WaitUntil(() => d.Hoop != null && d.Game.BoardRoot != null && d.Round.Column != null, 5f);
            Assert.IsNotNull(d.Game.BoardRoot, "board not built");
            Assert.AreEqual(Phase.Basket, d.Current);
            ready(d);
        }

        private static void RunTransition(PotaFlowDirector d)
        {
            int guard = 0;
            while (d.Current == Phase.Transition && guard++ < 1000) d.Advance(Step);
        }

        private static Button ButtonAt(ColorHuntHud hud, string path) => hud.transform.Find(path).GetComponent<Button>();

        [UnityTest]
        public IEnumerator Transition_EndsOnAPlayableBoard_WithTheBasketCountAsAmmo()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            float t0 = Time.realtimeSinceStartup;
            Assert.IsTrue(d.BeginTransition(158));
            Assert.AreEqual(Phase.Transition, d.Current);
            Assert.IsFalse(d.Shot.InputEnabled, "basket input off during the transition");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 3.5f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            Assert.LessOrEqual(Time.realtimeSinceStartup - t0, 3.0f, "spec: board playable within 3 s");
            yield return null;
            Assert.AreEqual(ColorHuntGame.State.Playing, d.Game.Current);
            Assert.AreEqual(158, d.Game.Board.Ammo);
            Assert.AreEqual(158, d.Game.DisplayedAmmo);
            Assert.AreEqual("158", d.Hud.transform.Find("AmmoPanel/Ammo").GetComponent<Text>().text);
            Assert.IsTrue(d.BoardInput.enabled, "board input on");
            Assert.IsFalse(d.Shot.InputEnabled, "basket input off");
            Assert.IsFalse(d.RoundCam.enabled);
            Assert.IsTrue(d.BoardCam.enabled);
            Assert.AreEqual(GiantHoop.MaxPile, d.Stream.ActiveCount, "ten balls rest in the net");
            Assert.IsFalse(d.Badge.Visible);
            for (int i = 0; i < d.Game.Board.Cells.Length; i++) Assert.AreEqual(1f, d.Game.ViewAt(i).Scale, 1e-5f, $"cell {i}");
            Assert.Less(Vector3.Distance(d.Cam.transform.position, d.BoardCam.FitFor(d.S.BoardFov, d.Cam.aspect)), 0.3f);
            Assert.AreEqual("Tekrar at", d.Hud.transform.Find("ResultPanel/Card/Retry/Label").GetComponent<Text>().text);
        }

        [UnityTest]
        public IEnumerator Skip_FinishesWithinASecond()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.BeginTransition(40);
            for (int i = 0; i < 5; i++) yield return null;
            float t0 = Time.realtimeSinceStartup;
            d.Skip();
            Assert.IsTrue(d.Skipping);
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 2f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            Assert.LessOrEqual(Time.realtimeSinceStartup - t0, 1.0f);
            Assert.AreEqual(40, d.Game.DisplayedAmmo);
        }

        [UnityTest]
        public IEnumerator HeldSkipTouch_DoesNotReachTheBoard()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            bool held = true;
            int presses = 1;
            d.PointerHeld = () => held;
            d.PointerPressedThisFrame = () => presses-- > 0;
            d.BeginTransition(20);
            yield return null;
            Assert.IsTrue(d.Skipping, "a tap during the transition skips");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 2f);
            yield return null;
            yield return null;
            Assert.IsFalse(d.BoardInput.enabled, "board input must wait until the finger lifts");
            held = false;
            yield return null;
            Assert.IsTrue(d.BoardInput.enabled);
        }

        [UnityTest]
        public IEnumerator Counter_ReachesN_ForEdgeCounts()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            foreach (int n in new[] { 1, 3, 1000 })
            {
                Assert.IsTrue(d.BeginTransition(n), $"N={n} did not start");
                int prev = 0;
                int guard = 0;
                while (d.Current == Phase.Transition && guard++ < 1000)
                {
                    d.Advance(Step);
                    Assert.GreaterOrEqual(d.Game.DisplayedAmmo, prev, $"N={n}: counter went down");
                    prev = d.Game.DisplayedAmmo;
                }
                Assert.AreEqual(Phase.Puzzle, d.Current);
                Assert.AreEqual(n, d.Game.Board.Ammo);
                Assert.AreEqual(n, d.Game.DisplayedAmmo);
                Assert.IsTrue(d.ReturnToBasket());
                guard = 0;
                while (d.Current == Phase.Return && guard++ < 1000) d.Advance(Step);
                Assert.AreEqual(Phase.Basket, d.Current);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ZeroBaskets_LoseThenRetryFliesBack()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            Assert.IsTrue(d.BeginTransition(0));
            yield return WaitUntil(() => d.Current == Phase.Result, 8f);
            Assert.AreEqual(Phase.Result, d.Current, "a board with 0 ammo ends at once");
            ButtonAt(d.Hud, "ResultPanel/Card/Retry").onClick.Invoke();
            Assert.AreEqual(Phase.Return, d.Current, "\"Tekrar at\" flies back");
            yield return WaitUntil(() => d.Current == Phase.Basket, 3f);
            Assert.AreEqual(Phase.Basket, d.Current);
            yield return null;
            Assert.AreEqual(BasketRound.State.Aim, d.Round.Current);
            Assert.IsTrue(d.Shot.InputEnabled);
            Assert.IsFalse(d.BoardInput.enabled);
            Assert.IsTrue(d.RoundCam.enabled);
            Assert.Less(Vector3.Distance(d.Cam.transform.position, d.Round.Settings.AimCamPos), 0.6f);
            Assert.AreEqual(d.Round.Settings.Fov, d.Cam.fieldOfView, 0.01f);
        }

        [UnityTest]
        public IEnumerator LeftoverSlowMotion_DoesNotSlowTheTransition()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.TimeCtl.SlowMo(0.05f, 10f);
            yield return null;
            Assert.AreEqual(0.05f, Time.timeScale, 1e-4f);
            float t0 = Time.realtimeSinceStartup;
            d.BeginTransition(20);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "the transition starts at normal time");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 4f);
            Assert.AreEqual(Phase.Puzzle, d.Current);
            Assert.LessOrEqual(Time.realtimeSinceStartup - t0, 3.0f);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f);
        }

        [UnityTest]
        public IEnumerator SecondRoundEnd_AndHudTaps_DuringTransition_AreIgnored()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.BeginTransition(30);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(d.BeginTransition(99), "a second round end must be ignored");
            Assert.IsFalse(d.ReturnToBasket(), "no return during the transition");
            ButtonAt(d.Hud, "RotateRight").onClick.Invoke();
            Assert.AreEqual(0, d.Game.Rotations, "the staged board cannot rotate");
            Assert.AreEqual("locked", d.Game.Tap(0), "the staged board cannot be tapped");
            yield return WaitUntil(() => d.Current == Phase.Puzzle, 3.5f);
            Assert.AreEqual(30, d.Game.Board.Ammo);
        }

        [UnityTest]
        public IEnumerator AspectChange_MidFlight_StillLandsOnTheFit()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            try
            {
                d.BeginTransition(20);
                for (int i = 0; i < 40; i++) d.Advance(Step);
                d.Cam.aspect = 16f / 9f;
                RunTransition(d);
                var want = d.BoardCam.FitFor(d.S.BoardFov, d.Cam.aspect);
                Assert.Less(Vector3.Distance(d.Cam.transform.position, want), 0.3f);
                yield return null;
                Assert.Less(Vector3.Distance(d.Cam.transform.position, want), 0.3f, "no jump when ColorHuntCamera takes over");
            }
            finally { d.Cam.ResetAspect(); }
        }

        [UnityTest]
        public IEnumerator CandidateB_HasNoStream_AndTheSameCount()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.CandidateOverride = FlowCandidate.Simple;
            Assert.IsTrue(d.BeginTransition(158));
            int maxActive = 0, guard = 0;
            while (d.Current == Phase.Transition && guard++ < 1000)
            {
                d.Advance(Step);
                maxActive = Mathf.Max(maxActive, d.Stream.ActiveCount);
            }
            Assert.AreEqual(0, maxActive, "B has no ball stream");
            Assert.AreEqual(158, d.Game.DisplayedAmmo);
        }

        [UnityTest]
        public IEnumerator Reveal_GrowsFromTheCentre()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.BeginTransition(10);
            d.Advance(1.80f);
            int n = d.Game.Board.Size, centre = -1;
            for (int i = 0; i < d.Game.Board.Cells.Length; i++)
                if (ColorHuntRules.Row(n, i) == n / 2 && ColorHuntRules.Col(n, i) == n / 2) centre = i;
            Assert.Greater(d.Game.ViewAt(centre).Scale, 0.2f, "centre ring is popping at 1.80 s");
            Assert.Less(d.Game.ViewAt(0).Scale, 0.01f, "corner ring has not started");
        }

        [UnityTest]
        public IEnumerator Transition_AllocatesAlmostNothingPerFrame()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.BeginTransition(158);
            for (int i = 0; i < 5; i++) d.Advance(Step);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 140; i++) d.Advance(Step);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Less(bytes, 8192, $"{bytes} bytes over 140 transition frames");
        }

        [UnityTest]
        public IEnumerator RealBasketRound_StartsTheTransition_WithItsCount()
        {
            PotaFlowDirector d = null;
            yield return Load(x => d = x);
            d.Round.LoadLevel(1);
            yield return WaitUntil(() => d.Round.Current == BasketRound.State.Aim, 3f);
            for (int i = 0; i < 10; i++) yield return null;
            d.Round.ShootProgrammatic(0f, 0.6f);
            yield return WaitUntil(() => d.Current != Phase.Basket, 45f);
            Assert.AreNotEqual(Phase.Basket, d.Current, "round end did not start the transition");
            Assert.AreEqual(d.Round.BasketCount, d.Count);
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.PotaFlowPlayTests`
Expected: exit 2, `error CS0246: The type or namespace name 'PotaFlowDirector' could not be found`.

- [ ] **Step 3: Write `PotaFlowDirector.cs`**

```csharp
using System;
using Project.Basket;
using Project.ColorHunt;
using Project.Juice;
using Project.VFX;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Flow
{
    /// <summary>
    /// PotaFlow's state machine: Basket → Transition → Puzzle → Result → (Return) → Basket. Owns the one camera during the
    /// transition and the return (RoundCamera / ColorHuntCamera drive it in their phases), gates both inputs per phase,
    /// hands the basket count to the board as ammo, and plays the transition from a pure <see cref="FlowTimeline"/>.
    /// Transition time is real time: a slow-motion left over from the basket round cannot slow it.
    /// </summary>
    public class PotaFlowDirector : MonoBehaviour
    {
        public enum Phase { Basket, Transition, Puzzle, Result, Return }

        public PotaFlowSettings S;
        [Header("Basket")]
        public BasketRound Round;
        public ShotInput Shot;
        public RoundCamera RoundCam;
        public BasketHud BasketHud;
        [Header("Board")]
        public ColorHuntGame Game;
        public ColorHuntInput BoardInput;
        public ColorHuntCamera BoardCam;
        public ColorHuntHud Hud;
        [Header("Shared")]
        public Camera Cam;
        public CameraShake Shake;
        public TimeScaleController TimeCtl;
        public SynthSfx Sfx;

        [Tooltip("≥ 0: use this basket count instead of the round's (recording, tests).")]
        public int ForcedCount = -1;
        [Tooltip("> 0: advance the clock by exactly this much per frame (frame-exact recording).")]
        public float FixedStep;

        /// <summary>Pointer state (tests replace these to simulate a new or held touch).</summary>
        public Func<bool> PointerPressedThisFrame = () => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        public Func<bool> PointerHeld = () => Pointer.current != null && Pointer.current.press.isPressed;

        /// <summary>Runtime override of the settings' candidate (record both without touching the asset).</summary>
        public FlowCandidate? CandidateOverride { get; set; }
        public FlowCandidate ActiveCandidate => CandidateOverride ?? S.Candidate;

        public Phase Current { get; private set; } = Phase.Basket;
        public float Clock { get; private set; }
        public int Count { get; private set; }
        public bool Skipping => _skipping;
        public FlowTimeline Timeline { get; private set; }
        public GiantHoop Hoop { get; private set; }
        public BallStream Stream { get; private set; }
        public FlowBadge Badge { get; private set; }
        public event Action<Phase> PhaseChanged;

        private CamPose _p0, _p1, _back;
        private Vector3 _source;
        private Vector2 _cageScreen;
        private float _fogStart0, _fogEnd0, _skipSpeed, _lastT;
        private bool _skipping, _heroShown, _badgeMerged, _revealSounded, _built;
        private Quaternion _heroRot0 = Quaternion.identity;
        private int _shownCount;
        private Canvas _basketCanvas, _boardCanvas;
        private Material _boardFrameMat;

        private void Start()
        {
            BoardCam.Anchor = Game.transform.position;
            _basketCanvas = BasketHud != null ? BasketHud.GetComponent<Canvas>() : null;
            _boardCanvas = Hud.GetComponent<Canvas>();
            if (BasketHud != null) BasketHud.ShowResultPanel = false;
            Hud.RetryOverride = () => ReturnToBasket();
            Hud.SetRetryLabel("Tekrar at");
            Hud.SetIntro(0f);

            CourtBuilder.Build(transform, S);
            var hoopGo = new GameObject("GiantHoop");
            hoopGo.transform.SetParent(Game.transform, false);
            Hoop = hoopGo.AddComponent<GiantHoop>();
            Hoop.BallRadius = S.BallRadius;
            Hoop.Build(Game.S.Size);

            var streamGo = new GameObject("BallStream");
            streamGo.transform.SetParent(transform, false);
            Stream = streamGo.AddComponent<BallStream>();
            Stream.BallMaterial = Game.S.BallMaterial;
            Stream.BallRadius = S.BallRadius;
            Stream.ApexAbove = S.ApexAbove;
            Stream.LaunchKick = S.LaunchKick;
            Stream.Prewarm(S.Timing.MaxVisible);
            Stream.Launched += OnLaunched;
            Stream.Landed += OnLanded;

            Badge = FlowBadge.Create(transform, Game.S.Font, Game.S.GoldColor, Game.S.NavyColor);

            Round.RoundEnded += OnRoundEnded;
            Game.RoundEnded += OnBoardEnded;
            _fogStart0 = RenderSettings.fogStartDistance;
            _fogEnd0 = RenderSettings.fogEndDistance;
            _built = true;
            EnterPhase(Phase.Basket);
        }

        private void OnDestroy()
        {
            if (Round != null) Round.RoundEnded -= OnRoundEnded;
            if (Game != null) Game.RoundEnded -= OnBoardEnded;
            if (Stream != null) { Stream.Launched -= OnLaunched; Stream.Landed -= OnLanded; }
        }

        private void OnRoundEnded(int count) => BeginTransition(ForcedCount >= 0 ? ForcedCount : count);

        private void OnBoardEnded(bool won, string reason)
        {
            if (Current == Phase.Puzzle) EnterPhase(Phase.Result);
        }

        /// <summary>Starts the basket → board transition with <paramref name="count"/> baskets. False if not in the Basket phase.</summary>
        public bool BeginTransition(int count)
        {
            if (!_built || Current != Phase.Basket || Round.Column == null) return false;
            if (Game.BoardRoot == null) { Debug.LogWarning("[PotaFlow] board not built yet"); return false; }
            Count = Mathf.Max(0, count);
            TimeCtl?.Clear();
            Timeline = new FlowTimeline(S.Timing, Count, ActiveCandidate);
            var look = RoundCam != null ? RoundCam.LookPoint : Cam.transform.position + Cam.transform.forward * 10f;
            _p0 = new CamPose(Cam.transform.position, look, Cam.fieldOfView);
            _p1 = new CamPose(S.MidPos, S.MidLook, S.MidFov);
            var cage = Round.Column.Cage;
            _source = new Vector3(0f, cage.TopY + 0.3f, cage.Depth * 0.5f + 0.6f);
            _cageScreen = Cam.WorldToScreenPoint(_source + Vector3.up * 1.2f);

            Game.Prepare(Count);
            Game.ShowAmmo(0);
            _shownCount = 0;
            BoardReveal.Apply(Game, Timeline, 0f);
            Hoop.ClearPile();
            Hoop.SetGlow(0f);
            if (Timeline.Visible > 0) Stream.Begin(Timeline, _source, Hoop);
            else Stream.Clear();
            Badge.Show(Count);
            Hud.HideResult();
            Hud.SetIntro(0f);

            var player = Round.Column.Player;
            _heroRot0 = player != null ? player.transform.rotation : Quaternion.identity;
            _heroShown = _skipping = _badgeMerged = _revealSounded = false;
            Clock = 0f;
            _lastT = 0f;
            EnterPhase(Phase.Transition);
            Sfx?.Play("swish");
            Shake?.Kick(0.18f);
            VfxService.Play("pickup", _source);
            ApplyTransition(0f);
            return true;
        }

        /// <summary>Plays the rest of the transition fast (≤ MaxSkipSeconds).</summary>
        public void Skip()
        {
            if (Current != Phase.Transition || _skipping) return;
            _skipping = true;
            _skipSpeed = Timeline.SkipSpeed(Clock);
        }

        /// <summary>"Tekrar at": fly back to the basket aim pose and restart the basket round. False unless on the board.</summary>
        public bool ReturnToBasket()
        {
            if (Current != Phase.Puzzle && Current != Phase.Result) return false;
            TimeCtl?.Clear();
            Hud.HideResult();
            _back = new CamPose(Cam.transform.position, Cam.transform.position + Cam.transform.forward * 20f, Cam.fieldOfView);
            Stream.Clear();
            Hoop.ClearPile();
            Badge.Hide();
            Clock = 0f;
            EnterPhase(Phase.Return);
            return true;
        }

        private void LateUpdate()
        {
            if (!_built) return;
            if (Current == Phase.Transition && PointerPressedThisFrame()) Skip();
            Advance(FixedStep > 0f ? FixedStep : Mathf.Min(Time.unscaledDeltaTime, 0.25f));
        }

        /// <summary>Advances the running phase by dt real seconds (LateUpdate calls this; tests call it directly).</summary>
        public void Advance(float dt)
        {
            switch (Current)
            {
                case Phase.Transition:
                    Clock += dt * (_skipping ? _skipSpeed : 1f);
                    ApplyTransition(Mathf.Min(Clock, Timeline.Ready));
                    if (Clock >= Timeline.Ready) FinishTransition();
                    break;
                case Phase.Puzzle:
                    if (!BoardInput.enabled && !PointerHeld()) BoardInput.enabled = true;
                    break;
                case Phase.Return:
                    Clock += dt;
                    ApplyReturn(Mathf.Min(Clock, S.Timing.ReturnTime));
                    if (Clock >= S.Timing.ReturnTime) FinishReturn();
                    break;
            }
        }

        private void ApplyTransition(float t)
        {
            var tl = Timeline;
            var T = S.Timing;

            var p2 = BoardPose();
            var pose = CameraFlight.Evaluate(_p0, _p1, p2, tl.CameraProgress(t));
            float settle = tl.SettleAt(t);
            if (settle >= 0f) pose = CameraFlight.Settle(p2, settle, S.SettleAmount);
            if (t < T.HighlightEnd) pose.Fov -= S.FovPunch * Mathf.Sin(Mathf.PI * t / T.HighlightEnd);
            ApplyPose(pose);

            float f = tl.CameraProgress(t);
            RenderSettings.fogStartDistance = Mathf.Lerp(_fogStart0, S.BoardFogStart, f);
            RenderSettings.fogEndDistance = Mathf.Lerp(_fogEnd0, S.BoardFogEnd, f);
            if (_basketCanvas != null) _basketCanvas.enabled = t < T.CameraStart;

            var player = Round.Column.Player;
            if (player != null)
            {
                if (!_heroShown && t >= T.HeroStart) { _heroShown = true; player.Reveal(); }
                if (_heroShown) player.transform.rotation = _heroRot0 * Quaternion.Euler(0f, S.HeroTurnDegrees * tl.HeroTurn(t), 0f);
            }

            Stream.Tick(t);
            Hoop.Tick(Mathf.Max(0f, t - _lastT));
            _lastT = t;
            int c = tl.CountAt(t);
            if (c != _shownCount) { _shownCount = c; Game.ShowAmmo(c); }

            PlaceBadge(t);
            BoardReveal.Apply(Game, tl, t);
            float glow = tl.FrameGlow(t);
            Hoop.SetGlow(glow);
            GlowBoardFrame(glow);
            if (!_revealSounded && t >= T.RevealStart) { _revealSounded = true; Sfx?.Play("multiply"); }
            Hud.SetIntro(tl.HudProgress(t));
        }

        private void PlaceBadge(float t)
        {
            if (_badgeMerged) return;
            var tl = Timeline;
            bool stream = ActiveCandidate == FlowCandidate.Stream;
            float dock = tl.BadgeDock(t), fly = tl.BadgeFly(t);
            var dockPt = new Vector2(Cam.pixelWidth * 0.5f, Cam.pixelHeight * (stream ? 0.84f : 0.62f));
            var p = Vector2.Lerp(Vector2.Lerp(_cageScreen, dockPt, dock), Hud.AmmoScreenPoint, fly);
            float flying = stream ? 0.72f : 1.15f + 0.12f * Mathf.Sin(Mathf.PI * tl.CameraProgress(t));
            float scale = t < S.Timing.HighlightEnd ? tl.HighlightScale(t) : Mathf.Lerp(1f, flying, dock) * Mathf.Lerp(1f, 0.45f, fly);
            float alpha = 1f - Mathf.Clamp01((fly - 0.7f) / 0.3f);
            Badge.Place(p, scale, alpha);
            if (fly >= 1f)
            {
                _badgeMerged = true;
                Badge.Hide();
                Hud.PopAmmo();
            }
        }

        private void FinishTransition()
        {
            BoardReveal.Finish(Game);
            Hoop.SetGlow(0f);
            GlowBoardFrame(0f);
            Hud.SetIntro(1f);
            Badge.Hide();
            RenderSettings.fogStartDistance = S.BoardFogStart;
            RenderSettings.fogEndDistance = S.BoardFogEnd;
            Cam.fieldOfView = S.BoardFov;
            Game.Begin();
            Hud.PopAmmo();
            Sfx?.Play("add");
            EnterPhase(Phase.Puzzle);
        }

        private void ApplyReturn(float t)
        {
            float u = FlowTimeline.Smoother(Mathf.Clamp01(t / S.Timing.ReturnTime));
            ApplyPose(CameraFlight.Evaluate(_back, _p1, AimPose(), u));
            RenderSettings.fogStartDistance = Mathf.Lerp(S.BoardFogStart, _fogStart0, u);
            RenderSettings.fogEndDistance = Mathf.Lerp(S.BoardFogEnd, _fogEnd0, u);
            Hud.SetIntro(1f - Mathf.Clamp01(t / 0.3f));
        }

        private void FinishReturn()
        {
            ApplyPose(AimPose());
            RenderSettings.fogStartDistance = _fogStart0;
            RenderSettings.fogEndDistance = _fogEnd0;
            var player = Round.Column != null ? Round.Column.Player : null;
            if (player != null) player.transform.rotation = _heroRot0;
            Hud.SetIntro(0f);
            EnterPhase(Phase.Basket);        // RoundCamera resumes from the aim pose it finds
            Round.ResetRound();
        }

        private void EnterPhase(Phase p)
        {
            Current = p;
            bool basket = p == Phase.Basket;
            if (Shot != null) Shot.InputEnabled = basket;
            if (RoundCam != null) RoundCam.enabled = basket;
            if (BoardInput != null) BoardInput.enabled = false;      // Puzzle turns it on once the finger is up
            if (BoardCam != null) BoardCam.enabled = p == Phase.Puzzle || p == Phase.Result;
            if (_basketCanvas != null) _basketCanvas.enabled = basket || (p == Phase.Transition && Clock < S.Timing.CameraStart);
            if (_boardCanvas != null) _boardCanvas.enabled = !basket;
            if (basket) Badge.Hide();
            PhaseChanged?.Invoke(p);
        }

        private CamPose BoardPose()
        {
            var pos = BoardCam.FitFor(S.BoardFov, Cam.aspect);
            return new CamPose(pos, pos + Vector3.forward * 20f, S.BoardFov);
        }

        private CamPose AimPose()
        {
            var bs = Round.Settings;
            return new CamPose(bs.AimCamPos, bs.AimLookAt, bs.Fov);
        }

        private void ApplyPose(CamPose p)
        {
            Cam.transform.position = p.Pos + (Shake != null ? Shake.Offset : Vector3.zero);
            Cam.transform.rotation = Quaternion.LookRotation(p.Look - p.Pos, Vector3.up);
            Cam.fieldOfView = p.Fov;
        }

        private void GlowBoardFrame(float g)
        {
            if (_boardFrameMat == null)
            {
                var bar = Game.transform.Find("Scenery/FrameTop");
                var r = bar != null ? bar.GetComponent<Renderer>() : null;
                if (r == null) return;
                _boardFrameMat = r.sharedMaterial;          // runtime material shared by the four frame bars
                _boardFrameMat.EnableKeyword("_EMISSION");
            }
            _boardFrameMat.SetColor("_EmissionColor", Game.S.FrameColor * (1.8f * g));
        }

        private void OnLaunched(int i)
        {
            if (i % 3 == 0) Sfx?.Play("shot", 1.15f, 0.35f);
        }

        private void OnLanded(int i) => Sfx?.Play("land", Mathf.Min(1.25f, 1f + 0.01f * i), 0.55f);
    }
}
```

- [ ] **Step 4: Write `PotaFlowSceneTool.cs`**

```csharp
using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Basket;
using Project.ColorHunt;
using Project.Flow;
using Project.Juice;
using Project.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>Creates (or rebuilds) the PotaFlow scene: basket round + rooftop + giant hoop + Color Hunt board, one camera.</summary>
    [McpForUnityTool("ai_potaflow_build_scene", Description =
        "Create or rebuild Assets/_Project/Games/Pota/Scenes/PotaFlow.unity: the basket round (level from PotaFlowSettings, default 20) at the origin, " +
        "the Color Hunt board translated to BoardAnchor (AutoStart off), one camera (RoundCamera + disabled ColorHuntCamera), one juice set, both HUDs, " +
        "one EventSystem, VfxService and the PotaFlowDirector. Court, giant hoop and ball stream are built at runtime. Adds the scene to Build Settings and PotaProfile.")]
    public static class PotaFlowSceneTool
    {
        public const string ScenePath = "Assets/_Project/Games/Pota/Scenes/PotaFlow.unity";
        public const string SettingsPath = "Assets/_Project/Games/Pota/Settings/PotaFlowSettings.asset";
        public const string ProfilePath = "Assets/_Project/Games/Pota/PotaProfile.asset";

        public class Parameters { }

        public static object HandleCommand(JObject p)
        {
            var report = Build();
            string msg = $"PotaFlow scene built at {ScenePath}.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { scene = ScenePath, settings = SettingsPath, steps = report });
        }

        [MenuItem("AI Tools/Pota/Build PotaFlow Scene")]
        public static void BuildMenu() => AIToolsCommon.Log(string.Join(" | ", Build()));

        public static List<string> Build()
        {
            var report = new List<string>();
            var basket = BasketSceneTool.PrepareAssets(report);
            var board = ColorHuntSceneTool.EnsureSettings(report);
            var flow = EnsureSettings(report);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = BasketSceneTool.CreateCamera(basket);
            var shake = cam.GetComponent<CameraShake>();
            var roundCam = cam.GetComponent<RoundCamera>();
            var boardCam = cam.gameObject.AddComponent<ColorHuntCamera>();
            boardCam.S = board;
            boardCam.Anchor = flow.BoardAnchor;
            boardCam.enabled = false;
            BasketSceneTool.CreateSun();
            ColorHuntSceneTool.CreateFill();
            SceneParts.CreateGlobalVolume(BasketSceneTool.VolumeProfilePath);

            var juiceGo = new GameObject("Juice");
            var juice = JuiceSet.Create(juiceGo, cam, shake, board.Font);
            var round = BasketSceneTool.BuildRound(basket, flow.BasketLevel, roundCam, juice);

            var uiRoot = new GameObject("UI");
            var basketHud = BasketHud.Create(uiRoot.transform);
            var game = ColorHuntSceneTool.BuildGame(board, cam, shake, juice, uiRoot.transform);
            game.Game.transform.position = flow.BoardAnchor;
            game.Game.AutoStart = false;
            SceneParts.CreateEventSystem(uiRoot.transform);

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(VfxAuthoringTool.LibraryPath);
            if (library != null) VfxAuthoringTool.EnsureServiceInScene(library, report);
            else report.Add("VfxLibrary missing (run AI Tools/VFX/Create Starter Effects first)");
            BasketSceneTool.ApplyRenderSettings(basket);

            var d = new GameObject("PotaFlow").AddComponent<PotaFlowDirector>();
            d.S = flow;
            d.Round = round.Round; d.Shot = round.Input; d.RoundCam = roundCam; d.BasketHud = basketHud;
            d.Game = game.Game; d.BoardInput = game.Input; d.BoardCam = boardCam; d.Hud = game.Hud;
            d.Cam = cam; d.Shake = shake; d.TimeCtl = juice.TimeCtl; d.Sfx = juice.Sfx;

            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");
            SceneParts.AddToBuildSettings(ScenePath);
            AddToProfile(report);
            AssetDatabase.SaveAssets();
            report.Add(GameViewPortrait.Apply(1080, 1920));
            return report;
        }

        private static PotaFlowSettings EnsureSettings(List<string> report)
        {
            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Settings");
            var s = AssetDatabase.LoadAssetAtPath<PotaFlowSettings>(SettingsPath);
            if (s != null) return s;
            s = ScriptableObject.CreateInstance<PotaFlowSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            report.Add($"created {SettingsPath}");
            return s;
        }

        private static void AddToProfile(List<string> report)
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameProfile>(ProfilePath);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (profile == null || sceneAsset == null) { report.Add("PotaProfile or scene asset missing"); return; }
            if (System.Array.IndexOf(profile.Scenes, sceneAsset) >= 0) return;
            var so = new SerializedObject(profile);
            var list = so.FindProperty("Scenes");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sceneAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add("PotaProfile: PotaFlow added");
        }
    }
}
```

- [ ] **Step 5: Compile, build the scene, then run the flow tests**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.FlowTimelineTests` (compiles everything; expected exit 0).
Run: `~/.local/bin/uv run Tools/mcp_call.py call execute_custom_tool '{"tool_name":"ai_potaflow_build_scene","parameters":{}}'`
Expected: `success: true`, steps include `saved …/PotaFlow.unity` and `PotaProfile: PotaFlow added`.
Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.PotaFlowPlayTests`
Expected: exit 0, `PlayMode 12/12 passed`. Failures here are code or plan defects: debug (superpowers:systematic-debugging); an allocation-test failure prints the byte count — find the allocating call (HUD `int.ToString` on count changes is expected ≈ 1 KB) before changing the threshold, and ledger any change.

- [ ] **Step 6: Full suites**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode` → Expected: exit 0 (SceneBootTests now also boots PotaFlow for 3 s without errors).
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` → Expected: exit 0.

- [ ] **Step 7: First look**

`manage_scene load` PotaFlow → `manage_editor play` → `execute_code` (`safety_checks=false` not needed):
```csharp
var t = System.Type.GetType("Project.Flow.PotaFlowDirector, Pota.Runtime");
var d = UnityEngine.Object.FindAnyObjectByType(t);
return (bool)t.GetMethod("BeginTransition").Invoke(d, new object[] { 158 });
```
Screenshots (`manage_camera screenshot game_view`, `output_folder: Temp/mcp_out`) right away, after ~1 s and after ~3 s; Read them. Expected: balls leaving the cage, a side view mid-flight, the board with the net under it. `manage_editor stop`. Any obvious breakage (pink material, board missing, camera inside geometry) is fixed now; composition tuning waits for Task 13.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Games/Pota/Scripts/Flow Assets/_Project/Games/Pota/Editor/PotaFlowSceneTool.cs* Assets/_Project/Games/Pota/Scenes/PotaFlow.unity* Assets/_Project/Games/Pota/Settings/PotaFlowSettings.asset* Assets/_Project/Games/Pota/PotaProfile.asset Assets/_Project/Games/Pota/Tests/PlayMode/PotaFlowPlayTests.cs* ProjectSettings/EditorBuildSettings.asset
git commit -q -m "feat: PotaFlow scene and director — basket round end flies into the board with the baskets as ammo, and back" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 12: `FrameRecorder` + `ai_potaflow_record`

Overlay canvases never reach a camera texture, so a camera-only capture (what `ai_basket_record` does) would miss the badge and the
HUD — half of this transition. `FrameRecorder` switches overlay canvases to Screen Space Camera for the recording and restores them.

**Files:**
- Create: `Assets/_Project/Games/Pota/Editor/FrameRecorder.cs`
- Modify: `Assets/_Project/Games/Pota/Editor/BasketRecordTool.cs` (use `FrameRecorder`, camera-only as before)
- Create: `Assets/_Project/Games/Pota/Editor/PotaFlowRecordTool.cs`
- Test: `Assets/_Project/Games/Pota/Tests/EditMode/FrameRecorderTests.cs`

**Interfaces:**
- Consumes: `PotaFlowDirector` (`CandidateOverride`, `ForcedCount`, `FixedStep`, `Current`, `Clock`, `Count`, `Round`, `Cam`, `BeginTransition`).
- Produces: `FrameRecorder(Camera cam, int width, float aspect, bool includeUi, IEnumerable<Canvas> canvases = null) : IDisposable` with `Width`, `Height`, `Capture(string path)`, `Dispose()`; `static EditorWindow FrameRecorder.OpenFloatingGameView(Rect)`, `static void CloseFloatingGameView(EditorWindow)`. MCP tool `ai_potaflow_record` (`action` start|status|cancel; `candidate` A|B; `count`; `shoot`; `level`; `x`; `power`; `width`; `aspect`; `tail`; `name`) writing `Temp/record/<name>/f_NNNN_tS.SSS.png` at 30 fps.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.IO;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEngine;

namespace Project.Tests
{
    public class FrameRecorderTests
    {
        private const string Dir = "Temp/test_record";

        [Test]
        public void Capture_WritesAPngOfTheRequestedSize()
        {
            var go = new GameObject("RecCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.red;
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, "frame.png");
            try
            {
                using (var rec = new FrameRecorder(cam, 64, 0.5f, false)) rec.Capture(path);
                Assert.IsNull(cam.targetTexture, "camera-only mode leaves the camera as it was");
                var tex = new Texture2D(2, 2);
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)));
                Assert.AreEqual(64, tex.width);
                Assert.AreEqual(128, tex.height);
                Assert.Greater(tex.GetPixel(32, 64).r, 0.6f, "red clear colour");
                Object.DestroyImmediate(tex);
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void WithUi_MovesOverlayCanvasesOntoTheCamera_AndBack()
        {
            var camGo = new GameObject("RecCam");
            var cam = camGo.AddComponent<Camera>();
            var canvasGo = new GameObject("Hud", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                using (new FrameRecorder(cam, 64, 1f, true, new[] { canvas }))
                {
                    Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
                    Assert.AreEqual(cam, canvas.worldCamera);
                    Assert.IsNotNull(cam.targetTexture);
                }
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.IsNull(cam.targetTexture);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.FrameRecorderTests`
Expected: exit 2, `error CS0246: The type or namespace name 'FrameRecorder' could not be found`.

- [ ] **Step 3: Write `FrameRecorder.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Offscreen frame capture for the record tools. Camera only: the camera renders into a texture just for each capture
    /// (the Game view keeps working). With UI: the camera renders into the texture for the whole recording and the
    /// Screen Space Overlay canvases move onto it as Screen Space Camera (overlay UI never reaches a camera texture);
    /// <see cref="Dispose"/> puts everything back. Post-processing then also touches the UI: frames are for review.
    /// </summary>
    public sealed class FrameRecorder : IDisposable
    {
        private readonly Camera _cam;
        private readonly bool _includeUi;
        private readonly RenderTexture _rt;
        private readonly Texture2D _tex;
        private readonly List<Canvas> _moved = new List<Canvas>();

        public int Width => _rt.width;
        public int Height => _rt.height;

        public FrameRecorder(Camera cam, int width, float aspect, bool includeUi, IEnumerable<Canvas> canvases = null)
        {
            _cam = cam;
            _includeUi = includeUi;
            int w = Mathf.Max(16, width), h = Mathf.Max(16, Mathf.RoundToInt(w / Mathf.Max(0.1f, aspect)));
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "FrameRecorder" };
            _tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            if (!includeUi) return;
            _cam.targetTexture = _rt;
            var list = canvases ?? UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in list)
            {
                if (c == null || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = _cam;
                c.planeDistance = _cam.nearClipPlane + 0.05f;
                _moved.Add(c);
            }
        }

        public void Capture(string path)
        {
            var prev = _cam.targetTexture;
            _cam.targetTexture = _rt;
            if (_includeUi) Canvas.ForceUpdateCanvases();
            _cam.Render();
            if (!_includeUi) _cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            _tex.Apply(false);
            RenderTexture.active = active;
            File.WriteAllBytes(path, _tex.EncodeToPNG());
        }

        public void Dispose()
        {
            foreach (var c in _moved)
            {
                if (c == null) continue;
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.worldCamera = null;
            }
            _moved.Clear();
            if (_cam != null && _cam.targetTexture == _rt) _cam.targetTexture = null;
            _rt.Release();
            UnityEngine.Object.DestroyImmediate(_rt);
            UnityEngine.Object.DestroyImmediate(_tex);
        }

        /// <summary>
        /// The editor only steps the player loop while a Game view repaints; with the main window asleep or behind other apps
        /// nothing advances. A small floating Game view keeps it going (pair with pause + EditorApplication.Step()).
        /// </summary>
        public static EditorWindow OpenFloatingGameView(Rect position)
        {
            try
            {
                var type = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (type == null) return null;
                var w = ScriptableObject.CreateInstance(type) as EditorWindow;
                if (w == null) return null;
                w.titleContent = new GUIContent("Game (record)");
                w.ShowUtility();
                w.position = position;
                w.Repaint();
                return w;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AI Tools] floating game view: " + e.Message);
                return null;
            }
        }

        public static void CloseFloatingGameView(EditorWindow w)
        {
            try { if (w != null) w.Close(); }
            catch (Exception e) { Debug.LogWarning("[AI Tools] close floating game view: " + e.Message); }
        }
    }
}
```

- [ ] **Step 4: Run and watch them pass**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode Project.Tests.FrameRecorderTests`
Expected: exit 0, `EditMode 2/2 passed`. (If `FindObjectsByType(FindObjectsInactive, FindObjectsSortMode)` is obsolete in 6.6, use the overload the compiler suggests and ledger it.)

- [ ] **Step 5: Move `BasketRecordTool` onto `FrameRecorder` (camera-only, same frames)**

In `BasketRecordTool`: delete `_rt`, `_tex`, `Capture`, `OpenFloatingGameView`, `CloseFloatingGameView`; add `private static FrameRecorder _recorder;`. In `Start` replace `OpenFloatingGameView();` with `_floating = FrameRecorder.OpenFloatingGameView(new Rect(1040f, 70f, 380f, 700f));`. In `Tick` phase 2 replace `Capture(path);` with:

```csharp
                        if (_recorder == null && Camera.main != null)
                            _recorder = new FrameRecorder(Camera.main, _width, Camera.main.aspect, false);
                        _recorder?.Capture(path);
```

In `Stop` replace the window/RT cleanup with:

```csharp
            FrameRecorder.CloseFloatingGameView(_floating);
            _floating = null;
            _recorder?.Dispose();
            _recorder = null;
```

Check it still records: open `Basket.unity`, `manage_editor play`, then
`execute_custom_tool {"tool_name":"ai_basket_record","parameters":{"level":1,"seconds":3,"every":6,"name":"basket-check"}}`, poll `{"action":"status"}` until `running: false`, `manage_editor stop`. Expected: `Temp/record/basket-check/` holds PNGs; Read one — the basket round as before.

- [ ] **Step 6: Write `PotaFlowRecordTool.cs`**

```csharp
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Flow;
using UnityEditor;
using UnityEngine;
using Phase = Project.Flow.PotaFlowDirector.Phase;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Records the PotaFlow transition frame by frame (30 fps game time, UI included) from the basket round's end to just
    /// after the board is ready: Temp/record/&lt;name&gt;/f_NNNN_tS.SSS.png (t = director clock). Assemble with ffmpeg.
    /// </summary>
    [McpForUnityTool("ai_potaflow_record", Description =
        "Record the PotaFlow transition in Play Mode (PotaFlow.unity open) as PNG frames with UI. action='start' {candidate:'A'|'B' (default: settings), " +
        "count (forced basket count; default the round's), shoot (default true: play basket `level` with a programmatic shot x/power and record from its end; " +
        "false: start from the aim pose), level (default settings), x (0), power (0.6), width (540), aspect (0.5625), tail (s after ready, 0.6), name ('potaflow')}. " +
        "action='status' | 'cancel'. Frames: Temp/record/<name>/f_NNNN_tS.SSS.png.")]
    public static class PotaFlowRecordTool
    {
        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("'A' (stream) or 'B' (simple); default: the settings asset", Required = false)] public string candidate { get; set; }
            [ToolParameter("Forced basket count (default: the round's own)", Required = false)] public int? count { get; set; }
            [ToolParameter("Play the basket round first (default true)", Required = false)] public bool? shoot { get; set; }
            [ToolParameter("Basket level (default: PotaFlowSettings.BasketLevel)", Required = false)] public int? level { get; set; }
            [ToolParameter("Shot x (default 0)", Required = false)] public float? x { get; set; }
            [ToolParameter("Shot power (default 0.6)", Required = false)] public float? power { get; set; }
            [ToolParameter("Frame width px (default 540)", Required = false)] public int? width { get; set; }
            [ToolParameter("Frame aspect w/h (default 0.5625 = 9:16)", Required = false)] public float? aspect { get; set; }
            [ToolParameter("Seconds to keep recording after the board is ready (default 0.6)", Required = false)] public float? tail { get; set; }
            [ToolParameter("Folder under Temp/record (default 'potaflow')", Required = false)] public string name { get; set; }
        }

        private static bool _running, _shoot;
        private static int _phase, _frame, _captured, _tailFrames, _count, _level, _width;
        private static float _x, _power, _aspect, _tail;
        private static FlowCandidate? _candidate;
        private static string _dir = "Temp/record/potaflow", _result = "";
        private static FrameRecorder _recorder;
        private static EditorWindow _floating;

        public static object HandleCommand(JObject p)
        {
            string action = (AIToolsCommon.GetString(p, "action", "start") ?? "start").ToLowerInvariant();
            switch (action)
            {
                case "status": return new SuccessResponse(_running ? "recording" : "idle", Status());
                case "cancel": Stop("cancelled"); return new SuccessResponse("cancelled", Status());
                default: return Start(p);
            }
        }

        private static object Start(JObject p)
        {
            if (!EditorApplication.isPlaying) return new ErrorResponse("Enter Play Mode first with PotaFlow.unity open.");
            if (_running) return new ErrorResponse("Already recording; poll status or cancel.");
            var d = Object.FindAnyObjectByType<PotaFlowDirector>();
            if (d == null) return new ErrorResponse("No PotaFlowDirector in the running scene.");
            string cand = (AIToolsCommon.GetString(p, "candidate", "") ?? "").ToUpperInvariant();
            _candidate = cand == "A" ? FlowCandidate.Stream : cand == "B" ? FlowCandidate.Simple : (FlowCandidate?)null;
            _count = AIToolsCommon.GetInt(p, "count", -1);
            _shoot = p["shoot"] == null || p["shoot"].Type != JTokenType.Boolean || p["shoot"].Value<bool>();
            _level = AIToolsCommon.GetInt(p, "level", d.S.BasketLevel);
            _x = AIToolsCommon.GetFloat(p, "x", 0f);
            _power = AIToolsCommon.GetFloat(p, "power", 0.6f);
            _width = Mathf.Clamp(AIToolsCommon.GetInt(p, "width", 540), 120, 2160);
            _aspect = Mathf.Clamp(AIToolsCommon.GetFloat(p, "aspect", 0.5625f), 0.3f, 3f);
            _tail = Mathf.Max(0f, AIToolsCommon.GetFloat(p, "tail", 0.6f));
            _dir = "Temp/record/" + (AIToolsCommon.GetString(p, "name", "potaflow") ?? "potaflow");
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _phase = 0; _frame = 0; _captured = 0; _tailFrames = 0; _result = "";
            Time.captureDeltaTime = 1f / 30f;
            _floating = FrameRecorder.OpenFloatingGameView(new Rect(1040f, 70f, 380f, 700f));
            EditorApplication.isPaused = true;
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return new SuccessResponse($"Recording PotaFlow to {_dir}.", Status());
        }

        private static void Stop(string why)
        {
            _running = false;
            _result = why;
            EditorApplication.update -= Tick;
            var d = Object.FindAnyObjectByType<PotaFlowDirector>();
            if (d != null) { d.FixedStep = 0f; d.CandidateOverride = null; d.ForcedCount = -1; }
            _recorder?.Dispose();
            _recorder = null;
            Time.captureDeltaTime = 0f;
            EditorApplication.isPaused = false;
            FrameRecorder.CloseFloatingGameView(_floating);
            _floating = null;
        }

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
            var d = Object.FindAnyObjectByType<PotaFlowDirector>();
            if (d == null) { Stop("no PotaFlowDirector"); return; }
            EditorApplication.Step();
            switch (_phase)
            {
                case 0:
                    if (d.Round.Column == null || d.Current != Phase.Basket) return;
                    d.CandidateOverride = _candidate;
                    d.ForcedCount = _count;
                    d.FixedStep = 1f / 30f;
                    _recorder = new FrameRecorder(d.Cam, _width, _aspect, true);
                    if (_shoot) { d.Round.LoadLevel(_level); _phase = 1; _frame = 0; }
                    else _phase = 3;
                    break;
                case 1:
                    if (++_frame >= 20) { d.Round.ShootProgrammatic(_x, _power); _phase = 2; }
                    break;
                case 2:
                    if (d.Current == Phase.Transition) { _phase = 4; Capture(d); }
                    else if (d.Round.Elapsed > 90f) Stop("basket round did not end within 90 s");
                    break;
                case 3:
                    if (!d.BeginTransition(_count >= 0 ? _count : 158)) { Stop("could not start the transition"); return; }
                    _phase = 4;
                    Capture(d);
                    break;
                case 4:
                    Capture(d);
                    if (d.Current != Phase.Transition && ++_tailFrames >= Mathf.RoundToInt(_tail * 30f))
                        Stop($"done: {_captured} frames, count {d.Count}, candidate {d.ActiveCandidate}");
                    break;
            }
        }

        private static void Capture(PotaFlowDirector d)
        {
            float t = d.Current == Phase.Transition ? d.Clock : d.Timeline.Ready + _tailFrames / 30f;
            _recorder.Capture(Path.Combine(_dir, $"f_{_captured:0000}_t{t:0.000}.png"));
            _captured++;
        }

        private static object Status()
        {
            var d = Object.FindAnyObjectByType<PotaFlowDirector>();
            return new
            {
                running = _running, phase = _phase, frames = _captured, dir = _dir, result = _result,
                director = d == null ? null : new { phase = d.Current.ToString(), clock = d.Clock, count = d.Count, candidate = d.ActiveCandidate.ToString() },
                basket = d == null || d.Round == null ? null : new { state = d.Round.Current.ToString(), elapsed = d.Round.Elapsed, baskets = d.Round.BasketCount },
            };
        }
    }
}
```

- [ ] **Step 7: Record once (smoke)**

`manage_scene load` `Assets/_Project/Games/Pota/Scenes/PotaFlow.unity` → `manage_editor play` → wait 3 s →
`execute_custom_tool {"tool_name":"ai_potaflow_record","parameters":{"candidate":"A","count":158,"shoot":false,"name":"potaflow-smoke"}}` →
poll `{"action":"status"}` every ~10 s until `running: false` → `manage_editor stop`.
Expected: `result` starts with `done:`; about 93 frames (2.5 s + 0.6 s at 30 fps) in `Temp/record/potaflow-smoke/`; Read three of them (first, middle, last): the badge and the HUD are visible, frames are 540×960.

- [ ] **Step 8: Run the EditMode suite and commit**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` → Expected: exit 0.

```bash
git add Assets/_Project/Games/Pota/Editor Assets/_Project/Games/Pota/Tests/EditMode/FrameRecorderTests.cs*
git commit -q -m "feat: ai_potaflow_record and a shared FrameRecorder (overlay UI moved onto the camera while recording)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Visual pass — record A and B, look, tune, keep the frames

"Kod derlendi ≠ doğru görünüyor." Each recording is judged frame by frame against the spec and tuned through `PotaFlowSettings`
(asset values changed from Unity, never by YAML), at most three rounds per candidate; every change gets a ledger line.

**Files:**
- Create: `Tools/frame_sheet.py`, `Tools/test_frame_sheet.py`
- Modify (values only, through Unity): `Assets/_Project/Games/Pota/Settings/PotaFlowSettings.asset`; code only if a defect shows (with a test)
- Create: `docs/screenshots/2026-09-29-potaflow-a-sheet.jpg`, `…-b-sheet.jpg`

**Interfaces:**
- Consumes: `ai_potaflow_record` (Task 12).
- Produces: `Tools/frame_sheet.py OUT.jpg LABEL=DIR [LABEL=DIR …] --times 0.2,0.8,1.3,1.8,2.3,2.6 --height 480` (picks, per row, the frame whose `_t<seconds>` is nearest each time; labels rows and columns). Library function `nearest(frames, t)` and `frame_time(name)`.

- [ ] **Step 1: Write the failing test for the sheet tool**

```python
import importlib.util
import os

spec = importlib.util.spec_from_file_location("frame_sheet", os.path.join(os.path.dirname(__file__), "frame_sheet.py"))
fs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fs)


def test_frame_time_reads_the_t_field():
    assert fs.frame_time("f_0012_t0.400.png") == 0.4
    assert fs.frame_time("f_0000_t12.034_b3.png") == 12.034
    assert fs.frame_time("notes.txt") is None


def test_nearest_picks_the_closest_frame():
    frames = [("a", 0.0), ("b", 0.2), ("c", 0.45)]
    assert fs.nearest(frames, 0.3) == "b"
    assert fs.nearest(frames, 0.4) == "c"
    assert fs.nearest(frames, 9.0) == "c"
```

Run: `~/.local/bin/uv run --with pillow --with pytest pytest -q Tools/test_frame_sheet.py`
Expected: FAIL (`FileNotFoundError` … `frame_sheet.py`).

- [ ] **Step 2: Write `Tools/frame_sheet.py`**

```python
#!/usr/bin/env python3
"""Contact sheet from recorded frame folders: one row per folder, one column per time.

Usage: uv run --with pillow Tools/frame_sheet.py OUT.jpg LABEL=DIR [LABEL=DIR ...] [--times 0.2,0.8,1.3,1.8,2.3,2.6] [--height 480]
Frames are PNGs named ..._t<seconds>... (ai_potaflow_record, ai_basket_record, or renamed ffmpeg output).
"""
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

_T = re.compile(r"_t(\d+(?:\.\d+)?)")


def frame_time(name):
    m = _T.search(name)
    return float(m.group(1)) if m and name.lower().endswith(".png") else None


def nearest(frames, t):
    return min(frames, key=lambda f: abs(f[1] - t))[0]


def load_frames(folder):
    out = []
    for n in sorted(os.listdir(folder)):
        t = frame_time(n)
        if t is not None:
            out.append((os.path.join(folder, n), t))
    if not out:
        sys.exit(f"no frames in {folder}")
    return out


def main(argv):
    times, height, rows, out = [0.2, 0.8, 1.3, 1.8, 2.3, 2.6], 480, [], None
    i = 0
    while i < len(argv):
        a = argv[i]
        if a == "--times":
            times = [float(x) for x in argv[i + 1].split(",")]
            i += 2
            continue
        if a == "--height":
            height = int(argv[i + 1])
            i += 2
            continue
        if out is None:
            out = a
        else:
            label, folder = a.split("=", 1)
            rows.append((label, load_frames(folder)))
        i += 1
    if out is None or not rows:
        sys.exit(__doc__)
    first = Image.open(rows[0][1][0][0])
    w = round(first.width * height / first.height)
    label_w, head_h = 150, 44
    sheet = Image.new("RGB", (label_w + w * len(times), head_h + height * len(rows)), (18, 22, 30))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("/System/Library/Fonts/Supplemental/Arial Bold.ttf", 28)
    except OSError:
        font = ImageFont.load_default()
    for c, t in enumerate(times):
        draw.text((label_w + c * w + 10, 8), f"{t:.1f} s", fill=(240, 240, 240), font=font)
    for r, (label, frames) in enumerate(rows):
        y = head_h + r * height
        draw.text((12, y + height // 2 - 14), label, fill=(255, 200, 60), font=font)
        for c, t in enumerate(times):
            img = Image.open(nearest(frames, t)).convert("RGB").resize((w, height), Image.LANCZOS)
            sheet.paste(img, (label_w + c * w, y))
    sheet.save(out, quality=85)
    print(f"{out}: {len(rows)} rows × {len(times)} times, {sheet.width}×{sheet.height}")


if __name__ == "__main__":
    main(sys.argv[1:])
```

Run: `~/.local/bin/uv run --with pillow --with pytest pytest -q Tools/test_frame_sheet.py` → Expected: `2 passed`.

- [ ] **Step 3: Install ffmpeg**

Run: `which ffmpeg || brew install ffmpeg` (spec default: Homebrew). Expected: `ffmpeg -version` prints a version. If Homebrew is missing, stop and tell Tuna (installing Homebrew is a system change).

- [ ] **Step 4: Record candidate A from a real basket round end**

PotaFlow open → `manage_editor play` → wait 3 s →
`execute_custom_tool {"tool_name":"ai_potaflow_record","parameters":{"candidate":"A","count":158,"name":"potaflow-a"}}` →
poll status until `running: false` (the level-20 basket round plays first: ~1–2 min) → `manage_editor stop`.
Then:
```bash
~/.local/bin/uv run --with pillow Tools/frame_sheet.py Temp/record/potaflow-a-sheet.jpg A=Temp/record/potaflow-a
ffmpeg -y -loglevel error -framerate 30 -pattern_type glob -i 'Temp/record/potaflow-a/f_*.png' -c:v libx264 -pix_fmt yuv420p -crf 23 Temp/record/potaflow-a.mp4
```
Read the sheet and at least eight individual frames (0.1, 0.3, 0.6, 1.0, 1.4, 1.8, 2.2, 2.6 s).

- [ ] **Step 5: Judge A against this checklist; tune and re-record (≤ 3 rounds)**

1. 0–0.3 s: camera over the full cage, "158 TOP" pops at the cage mouth, FOV punch visible, `pickup` flash reads.
2. 0.25–1.1 s: balls visibly leave the cage upward; they never pass through the tower's hoops, the terrace, the hero or the court.
3. Mid-flight (≈ 1.0–1.3 s): the frame shows the stream arcing toward the giant hoop (in portrait the tower and the hoop do not both fit: the stream is the subject). If the frame is empty sky or clipped geometry, move `MidPos` / `MidLook` / `MidFov`.
4. 1.15–2.2 s: balls drop through the rim into the net; the pile and the bulge read; the net is not hidden under the HUD's bottom buttons at 9:16.
5. 1.75–2.35 s: rings pop centre-out, the frame glows (bloom), no block pops at a wrong spot.
6. 1.9–2.3 s: HUD slides in; the badge flies into the ammo panel; the counter never jumps backward; ends at 158.
7. Every frame has something moving (no dead frame); nothing pink; fog does not wash out the board; the court looks like Pota's (orange, white lines; if its texture is rotated 90°, rotate the texture in `CourtTexture`, with a test).
8. After 2.5 s: the camera sits exactly on the board fit with no jump.

For every change: set the value from Unity (e.g. `execute_code`: `var s = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Games/Pota/Settings/PotaFlowSettings.asset"); var so = new SerializedObject(s); so.FindProperty("MidPos").vector3Value = new Vector3(…); so.ApplyModifiedProperties(); AssetDatabase.SaveAssets();`), re-record, ledger `Task 13: Ruling: <setting> <old> → <new> — <what the frame showed> — cost if wrong: one more tuning round`. A change of a code default (e.g. `FlowTiming`, `GiantHoop`) also updates the matching test and the Global Constraints stay true (ready ≤ 2.6 s, ≤ 25 balls, ≤ 0.35 s skip).

- [ ] **Step 6: Record and judge candidate B**

Same as Steps 4–5 with `"candidate":"B","name":"potaflow-b"`; B-specific checks: the badge travels with the camera and grows/shrinks, the counter runs 0 → 158 during the flight, the badge pops into the ammo panel.

- [ ] **Step 7: Re-run the flow tests after tuning**

Run: `~/.local/bin/uv run Tools/unity_tests.py PlayMode Project.Tests.PotaFlowPlayTests` → Expected: exit 0.
Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` → Expected: exit 0.

- [ ] **Step 8: Keep the sheets and commit**

```bash
cp Temp/record/potaflow-a-sheet.jpg docs/screenshots/2026-09-29-potaflow-a-sheet.jpg
~/.local/bin/uv run --with pillow Tools/frame_sheet.py docs/screenshots/2026-09-29-potaflow-b-sheet.jpg B=Temp/record/potaflow-b
git add Tools/frame_sheet.py Tools/test_frame_sheet.py docs/screenshots/2026-09-29-potaflow-a-sheet.jpg docs/screenshots/2026-09-29-potaflow-b-sheet.jpg Assets/_Project/Games/Pota
git commit -q -m "feat: PotaFlow visual pass — tuned flight, stream and reveal; A/B contact sheets" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(Sheets are ~1–2 MB JPEG at 480 px rows; if one is over 2 MB, re-save with `--height 360`.)

---

### Task 14: Pota's own transition, recorded from a copy, side by side

Pota stays untouched: it is copied into the session scratchpad and run from there; its `arac/` scripts are not run (they write
into Pota's `onizleme/`); our own script lives in the scratchpad.

**Files:**
- Create (scratch only): `$SCRATCH/pota-copy/` (rsync of `~/projects/Pota` without `.git`), `$SCRATCH/pota-record.mjs`, `$SCRATCH/pota-video/`
- Create: `docs/screenshots/2026-09-29-potaflow-sheet.jpg` (Pota / A / B rows), `docs/screenshots/2026-09-29-potaflow-side.mp4`

`$SCRATCH` = `/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/ddddd890-0b80-4935-84fd-734de99f5121/scratchpad`;
Node = `~/.nvm/versions/node/v22.23.2/bin/node`.

- [ ] **Step 1: Copy and serve Pota from the scratchpad**

```bash
S=/private/tmp/claude-501/-Users-tunaaltin17-projects-Unity-MCP/ddddd890-0b80-4935-84fd-734de99f5121/scratchpad
rsync -a --delete --exclude .git ~/projects/Pota/ "$S/pota-copy/"
git -C ~/projects/Pota status --porcelain | head -3      # must print nothing new because of us
cd "$S/pota-copy" && PATH=~/.nvm/versions/node/v22.23.2/bin:$PATH node node_modules/vite/bin/vite.js --port 5199 --strictPort
```
(the last command with `run_in_background: true`). Expected: vite prints `Local: http://localhost:5199/`.

- [ ] **Step 2: Write `$S/pota-record.mjs`**

```js
// Records Pota's basket → board transition (level 20, 158 baskets) from the scratch copy, 9:16 like the Unity frames.
// Usage: node pota-record.mjs http://localhost:5199/ <outDir>   → <outDir>/*.webm + prints {"offset": seconds}
import { chromium } from "/Users/tunaaltin17/projects/copy/node_modules/playwright/index.mjs";
const url = process.argv[2] ?? "http://localhost:5199/";
const out = process.argv[3];
const browser = await chromium.launch({ headless: false, args: ["--use-angle=metal", "--ignore-gpu-blocklist"] });
const ctx = await browser.newContext({
  viewport: { width: 405, height: 720 }, deviceScaleFactor: 2, hasTouch: true,
  recordVideo: { dir: out, size: { width: 810, height: 1440 } },
});
const t0 = Date.now();
const page = await ctx.newPage();
const errors = [];
page.on("pageerror", (e) => errors.push(String(e)));
await page.goto(url);
await page.evaluate(() => { localStorage.clear(); localStorage.setItem("pota.kayit.v1", JSON.stringify({ bolum: 20, ilk: false })); });
await page.goto(url);
await page.waitForTimeout(1800);
const click = (id) => page.evaluate((i) => document.getElementById(i).click(), id);
await click("bolumDugme");
await page.waitForTimeout(400);
await click("bolumBasket");
for (let i = 0; i < 60; i++) {
  if (await page.evaluate(() => __pota.ekran() === "fazA" && __pota.yonetmen.durum() === "bos")) break;
  await page.waitForTimeout(100);
}
await page.waitForTimeout(600);
const tCall = Date.now();
await page.evaluate(() => __pota.sepetBitir(158));
await page.waitForTimeout(3600);
await ctx.close();
await browser.close();
console.log(JSON.stringify({ offset: (tCall - t0) / 1000, errors }));
```

- [ ] **Step 3: Record, trim to the transition, extract 30 fps frames named by time**

```bash
cd "$S" && PATH=~/.nvm/versions/node/v22.23.2/bin:$PATH node pota-record.mjs http://localhost:5199/ "$S/pota-video"
# → {"offset": X, "errors": []}; use X below
V=$(ls "$S"/pota-video/*.webm | head -1)
mkdir -p "$S/pota-frames" && rm -f "$S"/pota-frames/*.png
ffmpeg -y -loglevel error -ss X -t 3.1 -i "$V" -vf "fps=30,scale=540:960" "$S/pota-frames/raw_%04d.png"
cd "$S/pota-frames" && i=0; for f in raw_*.png; do t=$(printf "%.3f" "$(echo "$i/30" | bc -l)"); mv "$f" "f_$(printf %04d $i)_t$t.png"; i=$((i+1)); done
```
Read the frames at 0.0, 0.5, 1.0 and 1.5 s: the first must still show the basket (the call moment); if the first frames are already mid-camera-move, lower `X` by 0.2 and redo the extraction. `errors` must be empty. Stop the vite server (TaskStop on the background task).

- [ ] **Step 4: Side-by-side sheet and video**

```bash
cd "/Users/tunaaltin17/projects/Unity MCP"
~/.local/bin/uv run --with pillow Tools/frame_sheet.py docs/screenshots/2026-09-29-potaflow-sheet.jpg Pota="$S/pota-frames" A=Temp/record/potaflow-a B=Temp/record/potaflow-b --height 400
ffmpeg -y -loglevel error -framerate 30 -pattern_type glob -i "$S/pota-frames/f_*.png" -c:v libx264 -pix_fmt yuv420p "$S/pota.mp4"
ffmpeg -y -loglevel error -framerate 30 -pattern_type glob -i 'Temp/record/potaflow-b/f_*.png' -c:v libx264 -pix_fmt yuv420p -crf 23 Temp/record/potaflow-b.mp4
ffmpeg -y -loglevel error -i "$S/pota.mp4" -i Temp/record/potaflow-a.mp4 -i Temp/record/potaflow-b.mp4 \
  -filter_complex "[0:v]scale=540:960,setsar=1[p];[1:v]scale=540:960,setsar=1[a];[2:v]scale=540:960,setsar=1[b];[p][a][b]hstack=inputs=3[v]" \
  -map "[v]" -c:v libx264 -pix_fmt yuv420p -crf 26 -shortest docs/screenshots/2026-09-29-potaflow-side.mp4
ls -la docs/screenshots/2026-09-29-potaflow-*
```
Expected: sheet with three rows (Pota, A, B) × six times; `…-side.mp4` 1620×960, about 3 s, under ~3 MB (raise `-crf` to 30 if larger). Read the sheet.
If `ffmpeg -filters | grep drawtext` shows drawtext, add the labels `Pota | A — Akan toplar | B — Sade` on top of each column; otherwise the order is stated in the README and the message to Tuna (ledger it).

- [ ] **Step 5: Confirm Pota is untouched and commit**

Run: `git -C ~/projects/Pota status --porcelain` → Expected: same output as before Step 1 (nothing from us).

```bash
git add docs/screenshots/2026-09-29-potaflow-sheet.jpg docs/screenshots/2026-09-29-potaflow-side.mp4
git commit -q -m "docs: PotaFlow vs Pota transition — side-by-side sheet and video" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Docs, backlog, memory

**Files:**
- Modify: `Assets/_Project/Games/Pota/README.md` (PotaFlow section), `CLAUDE.md` (§12), `~/projects/Blender MCP/docs/backlog.md` (anything deferred), memory (`MEMORY.md` + a PotaFlow project memory)

- [ ] **Step 1: Pota README** — new section "PotaFlow (sepet → tahta geçişi)": how to run (open `PotaFlow`, Play, swipe up; the round's end flies to the board; tap during the flight skips; "Tekrar at" flies back), what A and B are and how to switch (`PotaFlowSettings.Candidate`), where the numbers live (`PotaFlowSettings`, `FlowTiming`), tools (`ai_potaflow_build_scene`, `ai_potaflow_record`, `Tools/frame_sheet.py`), deliverables (`docs/screenshots/2026-09-29-potaflow-*`), tuning rulings from Task 13, and which Pota numbers were copied from where (`sahaYerlesim.ts` DEV_POTA, `pota.ts`, `saha.ts` #d9773a, `akis.ts` flow).
- [ ] **Step 2: `CLAUDE.md` §12** — AITools list: `ai_potaflow_build_scene`, `ai_potaflow_record`; "Testler": `Tools/unity_tests.py` usage and the new counts; Oyunlar/Pota line: PotaFlow exists, waiting for Tuna's A/B pick; lessons learned (e.g. overlay UI needs `FrameRecorder` to appear in recordings; HUD listeners must be wired at runtime).
- [ ] **Step 3: Backlog** — one line per deferred item (file + fix), e.g. anything from Task 13's checklist left for later, drawtext labels if missing, Pota port (spec §10 out of scope stays out: not a backlog item).
- [ ] **Step 4: Memory** — new `potaflow-transition-2026-09-29.md` (project): what exists, where, what Tuna must pick, the scratch-copy rule for Pota comparisons; add its line to `MEMORY.md`.
- [ ] **Step 5: Full suites once more, then commit**

Run: `~/.local/bin/uv run Tools/unity_tests.py EditMode` and `~/.local/bin/uv run Tools/unity_tests.py PlayMode` → Expected: both exit 0.

```bash
git add CLAUDE.md Assets/_Project/Games/Pota/README.md
git commit -q -m "docs: PotaFlow transition guide, tools and status" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git -C ~/projects/"Blender MCP" commit -qam "docs: backlog items from the PotaFlow transition" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
(The Blender MCP commit only if the backlog changed.)

---

## After the tasks (executing-plans)

Final whole-branch review on the most capable model with this plan's Review Focus, one fix pass for Critical/Important findings
(each with a RED→GREEN test), then the message to Tuna in Turkish: the sheet and the side-by-side video, how to try it in Unity,
the A/B question ("hangisi?"), rulings and deferred minors. No device build unless Tuna asks.
