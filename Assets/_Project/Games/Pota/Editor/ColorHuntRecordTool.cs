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
    /// Records Color Hunt for the side-by-side with Pota: a scripted round stepped frame by frame (paused editor, one capture per game frame), PNG
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
            _index = 0; _stepFrames = 0; _captured = 0; _tailFrames = 0; _dealt = false; _result = ""; _lastFrame = -1; _waits = 0; _tailStart = -1f;
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

        /// <summary>
        /// Recording time = unscaled time since the board was dealt. A stepped (paused) editor advances by Time.fixedDeltaTime per step,
        /// not by captureDeltaTime, so frames are not 1/30 s apart: every file name carries its own time.
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
            EditorApplication.Step();
            if (!_dealt)
            {
                if (game.BoardRoot == null) return;
                var design = game.S.ToDesign();
                if (_win) design.Targets = new[] { (0, 1) };
                game.Prepare(_ammo, (uint)_seed, design);
                game.Begin();
                _recorder = new FrameRecorder(game.Cam, _width, _aspect, true);
                _t0 = Time.unscaledTime;
                _dealt = true;
                return;
            }
            Capture();
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
            if (_win)
            {
                for (int i = 0; i < b.Cells.Length; i++) if (b.Cells[i].Hp <= b.Ammo && b.Cells[i].Color == 0) return i;
                return -1;
            }
            return ColorHuntHint.Best(b);
        }

        private static void Capture()
        {
            _recorder.Capture(Path.Combine(_dir, $"f_{_captured:0000}_t{Now:0.000}.png"));
            _captured++;
        }

        private static object Status() => new { running = _running, step = _index, steps = _steps?.Count ?? 0, frames = _captured, dir = _dir, result = _result, events = _events, gameTime = Time.time, frameCount = Time.frameCount, timeScale = Time.timeScale, captureDeltaTime = Time.captureDeltaTime };
    }
}
