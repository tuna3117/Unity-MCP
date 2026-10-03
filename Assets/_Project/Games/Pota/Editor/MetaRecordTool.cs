using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Meta;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Records the menu/reward showcase (Scenes/Meta.unity, Play Mode) frame by frame for the Pota | Önce | Sonra deliverables: a
    /// paused editor stepped one game frame at a time, every frame captured with the UI named by the recording clock, events.json with
    /// the moments as markers. A scenario is a script of director calls (menu, level, win, lose, map, promotion, bonus, market, tutorial);
    /// `feel` on|off sets the layer for the recording and restores the asset's value after.
    /// </summary>
    [McpForUnityTool("ai_meta_record", Description =
        "Records a menu/reward scenario of the Meta scene frame by frame (Play Mode): {\"scenario\": \"menu|level|win|lose|map|promotion|bonus|market|tutorial\", " +
        "\"feel\": \"on|off\", \"name\"?: \"meta-win-on\", \"width\"?: 540, \"tail\"?: 0.6}. Frames + events.json under Temp/record/<name>/. " +
        "{\"action\": \"status\"} / {\"action\": \"cancel\"}.")]
    public static class MetaRecordTool
    {
        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("menu | level | win | lose | map | promotion | bonus | market | tutorial", Required = false)] public string scenario { get; set; }
            [ToolParameter("Feel layer on|off for the recording (the asset's value is restored after)", Required = false)] public string feel { get; set; }
            [ToolParameter("Folder name under Temp/record (default meta-<scenario>-<feel>)", Required = false)] public string name { get; set; }
            [ToolParameter("Frame width in px (default 540; the height follows the 1170×2532 phone)", Required = false)] public int width { get; set; }
            [ToolParameter("Seconds recorded after the script ends (default 0.6)", Required = false)] public float tail { get; set; }
        }

        public static readonly string[] Scenarios = { "menu", "level", "win", "lose", "map", "promotion", "bonus", "market", "tutorial" };
        public const float PhoneAspect = 1170f / 2532f;

        /// <summary>One line of a scenario: runs once (<c>Act</c>), then holds the script until <c>Until</c> says so (null = next frame).</summary>
        public class Step
        {
            public string Name;
            public Action<MetaDirector> Act;
            public Func<MetaDirector, bool> Until;
            public float Seconds;                       // a plain wait
        }

        private static Step Do(string name, Action<MetaDirector> act) => new Step { Name = name, Act = act };
        private static Step Wait(float seconds) => new Step { Name = "wait", Seconds = seconds };
        private static Step Until(string name, Func<MetaDirector, bool> until) => new Step { Name = name, Until = until };

        public static bool TryFeel(string s, out bool on)
        {
            on = false;
            if (string.IsNullOrWhiteSpace(s)) return false;
            string v = s.Trim().ToLowerInvariant();
            if (v == "on") { on = true; return true; }
            if (v == "off") return true;
            return false;
        }

        /// <summary>The marker the deliverables align each scenario on (present with the feel layer on and off).</summary>
        public static string Anchor(string scenario)
        {
            switch (scenario) { case "bonus": return "spin"; case "market": return "buy"; case "map": return "devam"; default: return "open"; }
        }

        /// <summary>The markers a scenario raises.</summary>
        public static List<string> Markers(string scenario)
        {
            switch (scenario)
            {
                case "level": return new List<string> { "open", "play" };
                case "win": return new List<string> { "open", "score", "medal", "coins", "continue", "devam" };
                case "map": return new List<string> { "open", "devam", "map", "unlock" };
                case "promotion": return new List<string> { "open", "devam", "league" };
                case "bonus": return new List<string> { "open", "spin", "stop" };
                case "market": return new List<string> { "open", "tap", "buy", "fail" };
                case "tutorial": return new List<string> { "open", "tap" };
                default: return new List<string> { "open" };
            }
        }

        /// <summary>The director calls of a scenario; null for an unknown one.</summary>
        public static List<Step> Script(string scenario, bool feel)
        {
            switch (scenario)
            {
                case "menu": return new List<Step>
                {
                    Wait(0.1f), Do("open", d => { d.ResetSave(); d.Menu.Enter(); Mark("open"); }), Wait(7f),
                };
                case "level": return new List<Step>
                {
                    Wait(0.3f), Do("open", d => { Mark("open"); d.Menu.Play.Tap(); }), Wait(2.2f),
                    Do("play", d => { Mark("play"); d.Level.Play.Tap(); }), Wait(1.6f),
                };
                case "win": return new List<Step>
                {
                    Wait(0.3f), Do("open", d =>
                    {
                        Mark("open"); d.ShowWin();
                        var f = d.S.Feel; var seq = d.Win.Sequence;
                        float t0 = Now;
                        _events["score"] = t0 + f.T(f.ScoreAt); _events["coins"] = t0 + f.T(f.CoinsAt); _events["continue"] = t0 + f.T(f.ContinueAt);
                        float flip = seq.MedalFlipAt(seq.FinalMedal); _events["medal"] = t0 + (feel && flip >= 0f ? flip : 0f);
                    }),
                    Until("sequence", d => d.Win.Elapsed > d.S.Feel.T(d.S.Feel.ContinueAt) + (feel ? 1.4f : 2.2f)),
                    Do("devam", d => { Mark("devam"); d.Win.Continue.Tap(); }),
                    Until("closed", d => d.Win.Window.State == MetaWindow.Phase.Closed), Wait(0.4f),
                };
                case "map": return new List<Step>
                {
                    Wait(0.3f), Do("open", d => { Mark("open"); d.ShowWin(); }), Wait(0.2f),
                    Do("devam", d => { Mark("devam"); d.Win.Continue.Tap(); }),
                    Until("closed", d => d.Win.Window.State == MetaWindow.Phase.Closed),
                    Do("map", d => { if (!feel) d.Tabs.Select(TabBar.Tab.Map); Mark("map"); }),
                    Until("unlock", d =>
                    {
                        int n = Mathf.Min(d.Save.Level, d.S.MapLevels);
                        var st = d.Map.StateOf(n);
                        if (st == MapScreen.NodeState.Current || (d.Save.Level > d.S.MapLevels && st == MapScreen.NodeState.Passed)) { Mark("unlock"); return true; }
                        return false;
                    }),
                    Until("settled", d => !d.Map.Progressing), Wait(1.2f),
                };
                case "lose": return new List<Step>
                {
                    Wait(0.3f), Do("open", d => { Mark("open"); d.ShowLose(); }), Wait(2.6f),
                };
                case "promotion": return new List<Step>
                {
                    Wait(0.3f), Do("open", d => { Mark("open"); d.ShowPromotion(); }), Wait(2.8f),
                    Do("devam", d => { Mark("devam"); d.Promotion.Continue.Tap(); }),
                    Until("closed", d => d.Promotion.Window.State == MetaWindow.Phase.Closed), Wait(0.4f),
                    Do("league", d => { Mark("league"); d.OpenLeague(0); }), Wait(1.8f),
                };
                case "bonus": return new List<Step>
                {
                    Wait(0.3f), Do("open", d => { Mark("open"); d.ShowBonus(); }), Wait(1f),
                    Do("spin", d => { Mark("spin"); d.Bonus.Play.Tap(); }),
                    Until("stop", d => { if (!d.Bonus.Spinning) { Mark("stop"); return true; } return false; }),
                    Until("coins", d => !d.Coins.Running), Wait(1.2f),
                };
                case "market": return new List<Step>
                {
                    Wait(0.3f), Do("open", d => { Mark("open"); d.Tabs.Select(TabBar.Tab.Market); }), Wait(1f),
                    Do("tap", d => { Mark("tap"); d.Market.TapChip(1); }), Wait(1f),
                    Do("buy", d => { Mark("buy"); d.Market.Big.Tap(); }),
                    Until("bought", d => !d.Market.Purchasing && !d.Coins.Running), Wait(1f),
                    Do("try", d => d.Market.TapChip(4)), Wait(0.6f),
                    Do("fail", d => { Mark("fail"); d.Market.Big.Tap(); }), Wait(1.2f),
                };
                case "tutorial": return feel ? new List<Step>
                {
                    Wait(0.3f), Do("open", d => { Mark("open"); d.ShowTutorial(); }),
                    Until("tap", d => { if (d.Hand.TapCount >= 1) { Mark("tap"); return true; } return false; }),
                    Until("gone", d => !d.Hand.Visible), Wait(0.5f),
                } : new List<Step>
                {
                    // Pota's ring blinks on Oyna; it never taps, so the script holds a few seconds and takes it down
                    Wait(0.3f), Do("open", d => { Mark("open"); Mark("tap"); d.ShowTutorial(); }), Wait(3.5f), Do("hide", d => d.Hand.Hide()), Wait(0.3f),
                };
                default: return null;
            }
        }

        // ---------- the recording ----------

        private static bool _running;
        private static string _dir = "", _scenario = "", _result = "";
        private static FrameRecorder _recorder;
        private static EditorWindow _floating;
        private static List<Step> _steps;
        private static int _index, _captured, _lastFrame, _waits, _warm;
        private static float _t0, _stepStart = -1f, _tail, _tailStart = -1f, _previousFixed;
        private static int _width;
        private static bool _feel, _previousFeel, _restoreFeel, _started;
        private static Camera _cam;
        private static int _previousMask;
        private static readonly Dictionary<string, float> _events = new Dictionary<string, float>();

        private static float Now => Time.unscaledTime - _t0;
        private static void Mark(string name) { if (!_events.ContainsKey(name)) _events[name] = Now; }

        public static object HandleCommand(JObject p)
        {
            string action = (AIToolsCommon.GetString(p, "action", "start") ?? "start").Trim().ToLowerInvariant();
            if (action == "status") return new SuccessResponse(_running ? "recording" : "idle", Status());
            if (action == "cancel") { Stop("cancelled"); return new SuccessResponse("cancelled", Status()); }
            if (_running) return new ErrorResponse("A recording is running; {\"action\":\"status\"} or {\"action\":\"cancel\"}.");
            if (!EditorApplication.isPlaying) return new ErrorResponse("Enter Play Mode in Scenes/Meta.unity first.");
            var director = Object.FindAnyObjectByType<MetaDirector>();
            if (director == null) return new ErrorResponse("No MetaDirector in the scene: open Scenes/Meta.unity (ai_meta_build_scene).");
            _scenario = (AIToolsCommon.GetString(p, "scenario", "menu") ?? "menu").Trim().ToLowerInvariant();
            if (Array.IndexOf(Scenarios, _scenario) < 0) return new ErrorResponse($"scenario must be one of {string.Join(", ", Scenarios)}, got '{_scenario}'");
            string feelArg = AIToolsCommon.GetString(p, "feel", null);
            if (!string.IsNullOrWhiteSpace(feelArg) && !TryFeel(feelArg, out _)) return new ErrorResponse($"feel must be on or off, got '{feelArg}'");
            _feel = director.S.Feel.Enabled;
            TryFeel(feelArg, out bool feelOn);
            bool overrideFeel = !string.IsNullOrWhiteSpace(feelArg);
            if (overrideFeel) _feel = feelOn;
            _width = Mathf.Clamp(AIToolsCommon.GetInt(p, "width", 540), 120, 2160);
            _tail = Mathf.Max(0f, AIToolsCommon.GetFloat(p, "tail", 0.6f));
            string name = AIToolsCommon.GetString(p, "name", null) ?? $"meta-{_scenario}-{(_feel ? "on" : "off")}";
            if (name.Contains("..") || name.Contains("/") || name.Contains("\\")) return new ErrorResponse("name must be a plain folder name under Temp/record");
            _dir = "Temp/record/" + name;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            if (overrideFeel) { _previousFeel = director.S.Feel.Enabled; _restoreFeel = true; director.S.Feel.Enabled = feelOn; }   // after the file work: a throw above must not leave the asset overridden
            _steps = Script(_scenario, _feel);
            _events.Clear();
            _index = 0; _captured = 0; _lastFrame = -1; _waits = 0; _warm = 0; _stepStart = -1f; _tailStart = -1f; _result = ""; _started = false;
            _previousFixed = Time.timeScale > 1e-3f ? Time.fixedDeltaTime / Time.timeScale : Time.fixedDeltaTime;
            director.ResetSave();
            Time.captureDeltaTime = 1f / 30f;
            _floating = FrameRecorder.OpenFloatingGameView(new Rect(1040f, 70f, 380f, 700f));
            EditorApplication.isPaused = true;
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return new SuccessResponse($"Recording meta '{_scenario}' (feel {(_feel ? "on" : "off")}) to {_dir}.", Status());
        }

        [MenuItem("AI Tools/Pota/Meta/Record menu (feel on)")]
        public static void MenuRecord() => HandleCommand(new JObject { ["scenario"] = "menu", ["feel"] = "on" });

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            try { TickOnce(); }
            catch (Exception e) { Stop("error: " + e.Message); }        // a throwing step must not leave the editor paused and stepping forever
        }

        private static void TickOnce()
        {
            if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
            var d = Object.FindAnyObjectByType<MetaDirector>();
            if (d == null) { Stop("no MetaDirector"); return; }
            if (Time.frameCount == _lastFrame)
            {
                if (++_waits > 600) Stop("the player loop stopped stepping");
                return;
            }
            _waits = 0;
            _lastFrame = Time.frameCount;
            Time.fixedDeltaTime = _previousFixed;                   // every step the base 0.02 s (a dip must not shrink the stepping)
            EditorApplication.Step();
            if (!_started)
            {
                var cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
                if (cam == null) { Stop("no camera"); return; }
                _cam = cam; _previousMask = cam.cullingMask;
                cam.cullingMask = -1;                                // the scene's UI camera culls everything (overlay canvases need no camera); the recorder's canvases do
                _recorder = new FrameRecorder(cam, _width, PhoneAspect, true);
                _warm = 1;
                _t0 = Time.unscaledTime;
                _events["start"] = 0f;
                _started = true;
                return;
            }
            if (_warm > 0) { _warm--; return; }
            _recorder.Capture(Path.Combine(_dir, $"f_{_captured:0000}_t{Now:0.000}.png"));
            _captured++;
            if (_index >= _steps.Count)
            {
                if (_tailStart < 0f) _tailStart = Now;
                if (Now - _tailStart >= _tail) Stop($"done: {_captured} frames, {Now:0.00} s");
                return;
            }
            var s = _steps[_index];
            bool next = true;
            if (s.Act != null) s.Act(d);
            else if (s.Until != null) next = s.Until(d);
            else
            {
                if (_stepStart < 0f) _stepStart = Now;
                next = Now - _stepStart >= s.Seconds;
            }
            if (next) { _index++; _stepStart = -1f; }
            else if (Now > 40f) Stop($"step '{s.Name}' never finished");
        }

        private static void Stop(string why)
        {
            bool wasRunning = _running;
            float end = _started ? Now : 0f;
            var d = Object.FindAnyObjectByType<MetaDirector>();
            if (d != null && _restoreFeel && d.S != null && d.S.Feel != null) d.S.Feel.Enabled = _previousFeel;
            _restoreFeel = false;
            if (wasRunning) Time.fixedDeltaTime = _previousFixed;
            _running = false;
            _result = why;
            EditorApplication.update -= Tick;
            _recorder?.Dispose();
            _recorder = null;
            if (_cam != null) { _cam.cullingMask = _previousMask; _cam = null; }
            Time.captureDeltaTime = 0f;
            EditorApplication.isPaused = false;
            FrameRecorder.CloseFloatingGameView(_floating);
            _floating = null;
            if (wasRunning && Directory.Exists(_dir))
            {
                var json = new JObject();
                foreach (var kv in _events) json[kv.Key] = Math.Round(kv.Value, 3);
                json["end"] = Math.Round(end, 3);
                json["meta"] = new JObject { ["scene"] = "Meta", ["scenario"] = _scenario, ["feel"] = _feel, ["step"] = 0.02, ["speed"] = 1, ["result"] = why };
                File.WriteAllText(Path.Combine(_dir, "events.json"), json.ToString());
            }
            AIToolsCommon.Log($"ai_meta_record {_scenario}: {why}");
        }

        private static object Status() => new
        {
            running = _running, scenario = _scenario, feel = _feel, step = _index, steps = _steps?.Count ?? 0, frames = _captured, dir = _dir,
            result = _result, events = _events, frameCount = Time.frameCount,
        };
    }
}
