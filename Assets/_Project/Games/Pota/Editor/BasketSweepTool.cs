using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Basket;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Fires a grid of programmatic shots in Play Mode and reports basket-count statistics,
    /// so the rebuilt round can be compared with the original game's measured table.
    /// </summary>
    [McpForUnityTool("ai_basket_sweep", Description =
        "Shot sweep for the basket round (Play Mode must be running). action='start' with level, xs[], powers[], offsets[] " +
        "queues xs×powers×offsets shots (each: ResetRound → ShootProgrammatic(x+offset, power) → wait for Done). " +
        "action='status' returns progress and min/p25/median/p75/max of the basket counts; action='cancel' stops.")]
    public static class BasketSweepTool
    {
        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("Level (default 1)", Required = false)] public int? level { get; set; }
            [ToolParameter("Hoop x targets (default [-1.4, 0, 1.4])", Required = false)] public float[] xs { get; set; }
            [ToolParameter("Swipe powers (default [0.3, 0.5, 0.6, 0.8, 1.0, 1.15])", Required = false)] public float[] powers { get; set; }
            [ToolParameter("Lateral offsets added to x (default [-0.15, 0, 0.15])", Required = false)] public float[] offsets { get; set; }
            [ToolParameter("Seconds to wait per shot before giving up (default 40)", Required = false)] public float? timeout_per_shot { get; set; }
        }

        private class Shot { public float X, Power, Offset; public int Basket = -1, Top = -1; public float Elapsed; public bool Missed; public string Note; }

        private static readonly List<Shot> Shots = new List<Shot>();
        private static int _index = -1;
        private static bool _running;
        private static int _phase; // 0 = reset, 1 = wait after reset, 2 = shoot, 3 = wait done
        private static double _phaseStart;
        private static float _timeout = 40f;
        private static int _level = 1;

        public static object HandleCommand(JObject p)
        {
            string action = (AIToolsCommon.GetString(p, "action", "start") ?? "start").ToLowerInvariant();
            switch (action)
            {
                case "status": return Status();
                case "cancel": Stop(); return new SuccessResponse("Sweep cancelled.", Status());
                default: return Start(p);
            }
        }

        private static object Start(JObject p)
        {
            if (!EditorApplication.isPlaying) return new ErrorResponse("Enter Play Mode first (manage_editor play), then start the sweep.");
            if (_running) return new ErrorResponse("A sweep is already running; poll with action='status' or cancel it.");
            var round = UnityEngine.Object.FindFirstObjectByType<BasketRound>();
            if (round == null) return new ErrorResponse("No BasketRound in the running scene (open Basket.unity).");

            _level = AIToolsCommon.GetInt(p, "level", 1);
            _timeout = AIToolsCommon.GetFloat(p, "timeout_per_shot", 40f);
            var xs = Floats(p, "xs", new[] { -1.4f, 0f, 1.4f });
            var powers = Floats(p, "powers", new[] { 0.3f, 0.5f, 0.6f, 0.8f, 1.0f, 1.15f });
            var offsets = Floats(p, "offsets", new[] { -0.15f, 0f, 0.15f });

            Shots.Clear();
            foreach (float x in xs) foreach (float pw in powers) foreach (float off in offsets)
                Shots.Add(new Shot { X = x, Power = pw, Offset = off });
            _index = 0; _phase = 0; _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            AIToolsCommon.Log($"Sweep started: level {_level}, {Shots.Count} shots.");
            return new SuccessResponse($"Sweep started with {Shots.Count} shots on level {_level}. Poll with action='status'.", new { shots = Shots.Count });
        }

        private static void Stop()
        {
            _running = false;
            EditorApplication.update -= Tick;
        }

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop(); return; }
            var round = UnityEngine.Object.FindFirstObjectByType<BasketRound>();
            if (round == null) { Stop(); return; }
            if (_index >= Shots.Count) { Stop(); AIToolsCommon.Log("Sweep finished: " + Summary()); return; }

            var shot = Shots[_index];
            double now = EditorApplication.timeSinceStartup;
            switch (_phase)
            {
                case 0:
                    round.LoadLevel(_level);
                    _phase = 1; _phaseStart = now;
                    break;
                case 1:
                    if (now - _phaseStart > 0.6) { round.ShootProgrammatic(shot.X + shot.Offset, shot.Power); _phase = 3; _phaseStart = now; }
                    break;
                case 3:
                    bool done = round.Current == BasketRound.State.Done;
                    bool timedOut = now - _phaseStart > _timeout;
                    if (done || timedOut)
                    {
                        shot.Basket = round.BasketCount; shot.Top = round.TopPasses; shot.Elapsed = round.Elapsed; shot.Missed = round.Missed;
                        shot.Note = timedOut ? "timeout" : "ok";
                        _index++; _phase = 0;
                    }
                    break;
            }
        }

        private static object Status()
        {
            var doneShots = Shots.Where(s => s.Basket >= 0).ToList();
            var data = new
            {
                running = _running,
                level = _level,
                total = Shots.Count,
                completed = doneShots.Count,
                stats = Stats(doneShots.Where(s => s.Power <= 1.05f).Select(s => s.Basket).ToList()),
                stats_all = Stats(doneShots.Select(s => s.Basket).ToList()),
                results = doneShots.Select(s => new { s.X, s.Offset, s.Power, s.Basket, s.Top, s.Missed, elapsed = Math.Round(s.Elapsed, 1), s.Note }).ToList(),
            };
            return new SuccessResponse(_running ? $"Sweep running: {doneShots.Count}/{Shots.Count}." : $"Sweep idle: {doneShots.Count}/{Shots.Count} done. {Summary()}", data);
        }

        private static object Stats(List<int> values)
        {
            if (values.Count == 0) return null;
            var v = values.OrderBy(x => x).ToList();
            float Q(double q) { double pos = (v.Count - 1) * q; int lo = (int)Math.Floor(pos); int hi = Math.Min(v.Count - 1, lo + 1); return (float)(v[lo] + (v[hi] - v[lo]) * (pos - lo)); }
            return new { count = v.Count, min = v.First(), p25 = Q(0.25), median = Q(0.5), p75 = Q(0.75), max = v.Last(), mean = (float)v.Average() };
        }

        private static string Summary()
        {
            var v = Shots.Where(s => s.Basket >= 0 && s.Power <= 1.05f).Select(s => s.Basket).OrderBy(x => x).ToList();
            if (v.Count == 0) return "no results";
            return $"in-band shots: min {v.First()} median {v[v.Count / 2]} max {v.Last()} (n={v.Count})";
        }

        private static float[] Floats(JObject p, string key, float[] fallback)
        {
            var token = p?[key];
            if (token is JArray arr && arr.Count > 0) return arr.Select(t => (float)t).ToArray();
            return fallback;
        }
    }
}
