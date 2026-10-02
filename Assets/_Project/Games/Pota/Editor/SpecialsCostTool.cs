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
        /// <summary>
        /// SessionState keys: the flag says a measurement swapped the asset in, the string holds the previous asset's path ("" = none).
        /// A flag, because SessionState.GetString returns "" for a missing key whatever the default — a null check would restore "none".
        /// </summary>
        private const string PendingKey = "Project.SpecialsCost.PreviousPipeline", PendingFlag = "Project.SpecialsCost.Pending";

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
            SwapIn(mobile);
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
            RestorePending();
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

        /// <summary>Puts <paramref name="asset"/> into the current quality level, remembering (SessionState) the one it replaces.</summary>
        public static void SwapIn(RenderPipelineAsset asset)
        {
            var previous = QualitySettings.renderPipeline;
            if (!SessionState.GetBool(PendingFlag, false))           // a second swap keeps the first previous asset
            {
                SessionState.SetString(PendingKey, previous != null ? AssetDatabase.GetAssetPath(previous) : "");
                SessionState.SetBool(PendingFlag, true);
            }
            QualitySettings.renderPipeline = asset;
        }

        /// <summary>Puts back the pipeline asset SwapIn replaced; false when nothing was pending.</summary>
        public static bool RestorePending()
        {
            if (!SessionState.GetBool(PendingFlag, false)) return false;
            string pending = SessionState.GetString(PendingKey, "");
            QualitySettings.renderPipeline = pending.Length > 0 ? AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pending) : null;
            SessionState.EraseString(PendingKey);
            SessionState.EraseBool(PendingFlag);
            return true;
        }

        /// <summary>A domain reload in the middle of a measurement must not leave the Mobile asset in the quality settings.</summary>
        [InitializeOnLoadMethod]
        private static void RestoreAfterReload()
        {
            if (!SessionState.GetBool(PendingFlag, false)) return;
            Debug.LogWarning("[ai_specials_cost] a measurement was cut by a domain reload: the render pipeline asset is put back.");
            RestorePending();
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
