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
        /// <summary>Specials: the longest the recorder steps (uncaptured, unscaled s) waiting for the demo's start-up scenario to settle.</summary>
        public const float SettleTimeout = 20f;
        private const string ScenarioList = "birth, line, bomb, chain, golden";

        private static PuzzleLook _previousLook;
        private static SpecialLook _previousSpecialLook;
        private static bool _closeup, _restoreLook, _specials;
        private static SpecialsRequest _request;
        private static SpecialsDemo _demo;
        private static ColorHuntGame _hooked;
        private static PotaSounds _sounds;
        private static string _until = "finish";
        private static float _zoom = CloseupZoom, _stopAt = -1f, _previousFixed = 0.02f, _lastShot = -1f, _settleStart = -1f;
        private static int _focus = -1, _landed, _warm;
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

        /// <summary>
        /// Specials: the scene is quiet enough to stage the recorded scenario. The demo auto-plays its start scenario on entering Play; its
        /// last taps, stickers, golden balls and popups ("×2", "ÇİZGİ TOPU!" rise for 0.9 s on unscaled time) would fly into the first frames.
        /// </summary>
        public static bool Settled(bool demoFinished, bool busy, int itemsInFlight, int goldensInFlight, int popups) =>
            demoFinished && !busy && itemsInFlight == 0 && goldensInFlight == 0 && popups == 0;

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
            _stopAt = -1f; _landed = 0; _settleStart = -1f; _warm = 0;
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
            if (wasRunning) Time.fixedDeltaTime = _previousFixed;
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
        /// Recording time = unscaled time since the board was dealt. A stepped (paused) editor advances it by Time.fixedDeltaTime per step
        /// (held at the base step, see Tick) and game time by that × timeScale, so hit-stops and slow motion play as felt against this
        /// clock: every file name carries its own time, and the sound log uses this clock too.
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
            // A paused editor's Step advances unscaled time by fixedDeltaTime and game time by that × timeScale. TimeScaleController
            // rewrites fixedDeltaTime to base × scale when a dip or a look's slow motion starts, so a ×0.06 hit-stop would crawl in 0.0012-s
            // steps (Task 10). The recorder holds the step before every Step: the base (0.02 s), or CloseupTimeScale of it in a close-up
            // (0.006 s). Every clock keeps its ratio — game time still runs at timeScale against the recording clock — only the frames
            // stay evenly spaced.
            Time.fixedDeltaTime = _previousFixed * (_closeup ? CloseupTimeScale : 1f);
            EditorApplication.Step();
            if (!_dealt)
            {
                if (game.BoardRoot == null) return;
                if (_specials && !StartUpSettled(game)) return;
                Deal(game);
                return;
            }
            if (_warm > 0) { _warm--; return; }                  // the canvases just moved onto the camera lay out one frame late
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
            _warm = 1;
            _t0 = Time.unscaledTime;
            _sounds = game.Juice != null ? game.Juice.Sounds : null;
            if (_sounds != null) _sounds.StartLog(() => Now);
            _dealt = true;
        }

        /// <summary>Specials: <see cref="Settled"/> for the running scene, or true (with a warning) after <see cref="SettleTimeout"/>.</summary>
        private static bool StartUpSettled(ColorHuntGame game)
        {
            var canvas = game.Juice != null && game.Juice.Popups != null ? game.Juice.Popups.Canvas : null;
            int popups = canvas != null ? canvas.transform.childCount : 0;   // PopupText parents each live popup to its canvas
            if (Settled(_demo.Finished, game.Busy, game.ItemsInFlight, game.GoldenInFlight, popups)) return true;
            if (_settleStart < 0f) _settleStart = Time.unscaledTime;
            if (Time.unscaledTime - _settleStart < SettleTimeout) return false;
            Debug.LogWarning($"[ai_colorhunt_record] the demo's start-up scenario did not settle within {SettleTimeout:0} s; staging anyway.");
            return true;
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
        /// The longest time between two frames (meta.step) — the held step: 0.02, or 0.006 in a close-up; anything else means a paused Step
        /// no longer advances by fixedDeltaTime on this Unity.
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
