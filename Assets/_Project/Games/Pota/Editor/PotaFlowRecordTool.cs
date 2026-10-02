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
        "Record the PotaFlow transition in Play Mode (PotaFlow.unity open) as PNG frames with UI. action='start' {candidate:'A'|'B'|'1'|'2'|'3' (A stream, B simple, 1 big shot, 2 dive, 3 stamp; default: settings), " +
        "count (forced basket count; default the round's), shoot (default true: play basket `level` with a programmatic shot x/power and record from its end; " +
        "false: start from the aim pose), level (default settings), x (0), power (0.6), width (540), aspect (0.5625), tail (s after ready, 0.6), name ('potaflow')}. " +
        "action='status' | 'cancel'. Frames: Temp/record/<name>/f_NNNN_tS.SSS.png.")]
    public static class PotaFlowRecordTool
    {
        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("'A' (stream), 'B' (simple), '1' (big shot), '2' (dive) or '3' (stamp); default: the settings asset", Required = false)] public string candidate { get; set; }
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
            _candidate = ParseCandidate(cand);
            if (cand.Length > 0 && _candidate == null) return new ErrorResponse($"Unknown candidate '{cand}': use A, B, 1, 2 or 3.");
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

        /// <summary>A = Stream, B = Simple, 1 = BigShot, 2 = Dive, 3 = Stamp (or the enum names); empty = the settings asset.</summary>
        public static FlowCandidate? ParseCandidate(string cand)
        {
            switch ((cand ?? "").Trim().ToUpperInvariant())
            {
                case "A": case "STREAM": return FlowCandidate.Stream;
                case "B": case "SIMPLE": return FlowCandidate.Simple;
                case "1": case "BIGSHOT": return FlowCandidate.BigShot;
                case "2": case "DIVE": return FlowCandidate.Dive;
                case "3": case "STAMP": return FlowCandidate.Stamp;
                default: return null;
            }
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
