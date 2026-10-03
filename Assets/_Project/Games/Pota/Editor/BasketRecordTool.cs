using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Basket;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Records the basket round as a PNG frame sequence (floating Game view, paused editor stepped one player frame per tick) while a
    /// programmatic shot plays out. Frames go to Temp/record/&lt;name&gt;/f_NNNN_tS.SSS_bN.png, named by the recording clock
    /// (1/30 s per stepped frame from the first capture); events.json holds the markers (start, pull, shot, miss, end) and meta.
    /// Feel layer (spec 2026-10-03 Task 10): mode pull (a slingshot ramp before the shot), miss (an aim past the walls), swish
    /// (a full-power shot at a top hoop's x); feel on|off switches the asset's feel layer for the recording and restores it.
    /// </summary>
    [McpForUnityTool("ai_basket_record", Description =
        "Record the basket round in Play Mode as PNG frames. action='start' {level, x, power, seconds, every, name, width, mode (shot|pull|miss|swish), " +
        "pull (ramp seconds, default 1.2), feel (on|off)}: LoadLevel → (pull ramp) → shoot → capture every Nth frame until the round is Done or seconds elapse. " +
        "action='status' | 'cancel'. Frames + events.json {start, pull, shot, miss, end, meta}: Temp/record/<name>/.")]
    public static class BasketRecordTool
    {
        public enum Mode { Shot, Pull, Miss, Swish }

        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("Level (default 1)", Required = false)] public int? level { get; set; }
            [ToolParameter("Hoop x target (default 0)", Required = false)] public float? x { get; set; }
            [ToolParameter("Swipe power (default 0.6)", Required = false)] public float? power { get; set; }
            [ToolParameter("Max seconds to record after the shot (default 20)", Required = false)] public float? seconds { get; set; }
            [ToolParameter("Capture every Nth frame (default 3)", Required = false)] public int? every { get; set; }
            [ToolParameter("Folder name under Temp/record (default 'basket')", Required = false)] public string name { get; set; }
            [ToolParameter("Capture width in px (default 540; height follows the camera aspect)", Required = false)] public int? width { get; set; }
            [ToolParameter("shot (default) | pull | miss | swish", Required = false)] public string mode { get; set; }
            [ToolParameter("pull: ramp seconds (default 1.2), then a 0.3 s hold", Required = false)] public float? pull { get; set; }
            [ToolParameter("Feel layer on|off for the recording (restored after)", Required = false)] public string feel { get; set; }
        }

        public const float HoldSeconds = 0.3f, Fps = 30f;

        private static bool _running;
        private static int _phase, _frame, _captured, _every = 3, _frames;
        private static float _maxSeconds = 20f, _x, _power, _pullSeconds = 1.2f;
        private static int _level = 1;
        private static string _dir = "Temp/record/basket";
        private static string _result = "";
        private static int _width = 540;
        private static float _shotTime, _t0 = -1f, _pullStart = -1f, _doneAt = -1f;
        private static FrameRecorder _recorder;
        private static Mode _mode;
        private static AimPoint _aim;
        private static bool _previousFeel, _restoreFeel;
        private static BasketRound _hooked;
        private static readonly Dictionary<string, float> _events = new Dictionary<string, float>();
        private static EditorWindow _floating;

        public static bool TryMode(string value, out Mode mode)
        {
            mode = Mode.Shot;
            if (string.IsNullOrWhiteSpace(value)) return true;
            switch (value.Trim().ToLowerInvariant())
            {
                case "shot": mode = Mode.Shot; return true;
                case "pull": mode = Mode.Pull; return true;
                case "miss": mode = Mode.Miss; return true;
                case "swish": mode = Mode.Swish; return true;
                default: return false;
            }
        }

        /// <summary>The aim a mode shoots: miss = 0.6 m past the wall at power 1; swish = the middle top hoop's x at FullPower; else x / power.</summary>
        public static AimPoint AimFor(Mode mode, HoopLayout layout, float x, float power)
        {
            switch (mode)
            {
                case Mode.Miss: return AimMapper.FromXPower(layout.WallX + 0.6f, 1f);
                case Mode.Swish:
                {
                    float best = 0f, bestDx = float.MaxValue;
                    foreach (var h in layout.Hoops) if (h.IsTop && Mathf.Abs(h.X) < bestDx) { bestDx = Mathf.Abs(h.X); best = h.X; }
                    return AimMapper.FromXPower(best, AimMapper.FullPower);
                }
                default: return AimMapper.FromXPower(x, power);
            }
        }

        /// <summary>The pull ramp 0..1 over <paramref name="pullSeconds"/>, held at 1 after.</summary>
        public static float PullRamp(float t, float pullSeconds) => Mathf.Clamp01(t / Mathf.Max(0.01f, pullSeconds));

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
            if (!EditorApplication.isPlaying) return new ErrorResponse("Enter Play Mode first (manage_editor play) with Basket.unity open.");
            if (_running) return new ErrorResponse("Already recording; poll status or cancel.");
            var round = UnityEngine.Object.FindAnyObjectByType<BasketRound>();
            if (round == null) return new ErrorResponse("No BasketRound in the running scene.");
            if (!TryMode(AIToolsCommon.GetString(p, "mode", null), out _mode)) return new ErrorResponse("mode must be shot, pull, miss or swish");
            string feelArg = AIToolsCommon.GetString(p, "feel", null);
            if (!string.IsNullOrWhiteSpace(feelArg) && !ColorHuntRecordTool.TryFeel(feelArg, out _)) return new ErrorResponse($"feel must be on or off, got '{feelArg}'");
            if (ColorHuntRecordTool.TryFeel(feelArg, out bool feelOn) && round.Settings != null && round.Settings.Feel != null)
            {
                _previousFeel = round.Settings.Feel.Enabled;
                _restoreFeel = true;
                round.Settings.Feel.Enabled = feelOn;
            }
            _level = AIToolsCommon.GetInt(p, "level", 1);
            _x = AIToolsCommon.GetFloat(p, "x", 0f);
            _power = AIToolsCommon.GetFloat(p, "power", 0.6f);
            _pullSeconds = Mathf.Max(0.1f, AIToolsCommon.GetFloat(p, "pull", 1.2f));
            _maxSeconds = AIToolsCommon.GetFloat(p, "seconds", 20f);
            _every = Mathf.Max(1, AIToolsCommon.GetInt(p, "every", 3));
            _dir = "Temp/record/" + (AIToolsCommon.GetString(p, "name", "basket") ?? "basket");
            _width = Mathf.Clamp(AIToolsCommon.GetInt(p, "width", 540), 120, 2160);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _phase = 0; _frame = 0; _captured = 0; _frames = 0; _result = ""; _shotTime = -1f; _t0 = -1f; _pullStart = -1f; _doneAt = -1f;
            _events.Clear();
            Hook(round);
            // Offline recording: every player-loop step advances exactly 1/30 s of game time, whatever the editor's
            // real frame rate, and the loop is stepped from here so the editor needs no focus (QueuePlayerLoopUpdate).
            Time.captureDeltaTime = 1f / Fps;
            // The editor only steps the player loop while a Game view repaints; with the main window on a sleeping
            // display or behind other apps nothing advances (seen: Time.frameCount stuck at 1). So: a small floating
            // Game view on the main display + pause/Step: every editor tick runs exactly one player frame.
            _floating = FrameRecorder.OpenFloatingGameView(new Rect(1040f, 70f, 380f, 700f));
            EditorApplication.isPaused = true;
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return new SuccessResponse($"Recording to {_dir} (level {_level}, mode {_mode}, x {_x}, power {_power}, every {_every} frames, max {_maxSeconds}s).", Status());
        }

        private static void Hook(BasketRound round)
        {
            Unhook();
            _hooked = round;
            round.MissedDeclared += OnMissed;
            round.RoundEnded += OnEnded;
        }

        private static void Unhook()
        {
            if (_hooked == null) return;
            _hooked.MissedDeclared -= OnMissed;
            _hooked.RoundEnded -= OnEnded;
            _hooked = null;
        }

        private static void OnMissed() { if (_running && !_events.ContainsKey("miss")) _events["miss"] = Now; }
        private static void OnEnded(int count) { if (_running && !_events.ContainsKey("end")) _events["end"] = Now; }

        /// <summary>
        /// The recording clock: game time since the first capture. A paused, stepped editor advances Time.time by only a few ms per Step
        /// (the first version counted frames at 1/30 s and ran 7× fast), so every frame name carries the game's own time.
        /// </summary>
        private static float Now => _t0 < 0f ? 0f : Time.time - _t0;

        private static void Stop(string why)
        {
            bool wasRunning = _running;
            _running = false;
            _result = why;
            EditorApplication.update -= Tick;
            Time.captureDeltaTime = 0f;
            EditorApplication.isPaused = false;
            FrameRecorder.CloseFloatingGameView(_floating);
            _floating = null;
            _recorder?.Dispose();
            _recorder = null;
            var round = _hooked;
            Unhook();
            if (_restoreFeel && round != null && round.Settings != null && round.Settings.Feel != null) round.Settings.Feel.Enabled = _previousFeel;
            _restoreFeel = false;
            if (!wasRunning || !Directory.Exists(_dir)) return;
            var json = new JObject();
            foreach (var kv in _events) json[kv.Key] = Math.Round(kv.Value, 4);
            if (!json.ContainsKey("end")) json["end"] = Math.Round(Now, 4);
            json["meta"] = new JObject
            {
                ["scene"] = "Basket", ["mode"] = _mode.ToString().ToLowerInvariant(), ["level"] = _level, ["result"] = why,
                ["feel"] = round != null && round.Settings != null && round.Settings.Feel != null && round.Settings.Feel.Enabled,
                ["speed"] = 1.0, ["step"] = Math.Round(1.0 / Fps, 4),
            };
            File.WriteAllText(Path.Combine(_dir, "events.json"), json.ToString());
        }

        private static void Capture(BasketRound round)
        {
            if (_t0 < 0f) _t0 = Time.time;
            if (_frames % _every == 0)
            {
                string path = Path.Combine(_dir, $"f_{_captured:0000}_t{Now:0.000}_b{round.BasketCount}.png");
                if (_recorder == null && Camera.main != null) _recorder = new FrameRecorder(Camera.main, _width, Camera.main.aspect, false);
                _recorder?.Capture(path);
                _captured++;
            }
            _frames++;
        }

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
            var round = UnityEngine.Object.FindAnyObjectByType<BasketRound>();
            if (round == null) { Stop("no round"); return; }
            EditorApplication.Step();
            switch (_phase)
            {
                case 0:
                    round.LoadLevel(_level);
                    _aim = AimFor(_mode, round.Layout, _x, _power);
                    _phase = 1; _frame = 0;
                    break;
                case 1:
                    // settle (camera, hero), then the pull ramp when asked; the clock starts with the first capture
                    if (_frame == 0) _events["start"] = 0f;
                    Capture(round);
                    _frame++;
                    if (_mode == Mode.Pull)
                    {
                        var input = UnityEngine.Object.FindAnyObjectByType<ShotInput>();
                        if (_pullStart < 0f && Now >= 0.4f) { _pullStart = Now; _events["pull"] = Now; }
                        if (_pullStart >= 0f)
                        {
                            float t = Now - _pullStart;
                            float u = PullRamp(t, _pullSeconds);
                            input?.SimulatePull(AimMapper.FromXPower(_aim.X, _aim.Power * Mathf.Lerp(0.15f, 1f, u)));
                            if (t >= _pullSeconds + HoldSeconds) { input?.SimulateRelease(); Shoot(round); }
                        }
                    }
                    else if (Now >= 0.6f) Shoot(round);
                    break;
                case 2:
                    Capture(round);
                    if (round.Current == BasketRound.State.Done && Now - _doneAt >= 1.2f)
                    {
                        Stop($"done: basket {round.BasketCount}, top passes {round.TopPasses}, elapsed {round.Elapsed:0.0}s, missed {round.Missed}");
                        return;
                    }
                    if (round.Current == BasketRound.State.Done && _doneAt < 0f) _doneAt = Now;   // a beat on the end stamp before closing
                    if (round.Elapsed > _maxSeconds) { Stop($"timeout after {_maxSeconds}s game time: basket {round.BasketCount}"); return; }
                    break;
            }
        }

        private static void Shoot(BasketRound round)
        {
            round.Shoot(_aim);
            _events["shot"] = Now;
            _shotTime = Now;
            _phase = 2;
        }

        private static object Status()
        {
            var round = UnityEngine.Object.FindAnyObjectByType<BasketRound>();
            return new
            {
                running = _running, phase = _phase, frames = _captured, dir = _dir, result = _result, mode = _mode.ToString().ToLowerInvariant(),
                state = round != null ? round.Current.ToString() : "", basket = round != null ? round.BasketCount : 0,
                timeScale = Time.timeScale, gameFrame = Time.frameCount, gameTime = Time.time,
            };
        }
    }
}
