using System;
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
    /// Records the basket round as a PNG frame sequence (Game view, every Nth editor frame) while a programmatic
    /// shot plays out, so the round can be reviewed frame by frame (smoothness, hoop passes, cage fill, camera).
    /// Frames go to Temp/record/&lt;name&gt;/f_NNNN_tS.SSS.png; assemble with ffmpeg outside the editor.
    /// </summary>
    [McpForUnityTool("ai_basket_record", Description =
        "Record the basket round in Play Mode as PNG frames. action='start' {level, x, power, seconds, every, name}: LoadLevel → wait → ShootProgrammatic(x, power) → " +
        "capture every Nth frame until the round is Done or seconds elapse. action='status' | 'cancel'. Frames: Temp/record/<name>/.")]
    public static class BasketRecordTool
    {
        public class Parameters
        {
            [ToolParameter("'start' (default), 'status' or 'cancel'", Required = false)] public string action { get; set; }
            [ToolParameter("Level (default 1)", Required = false)] public int? level { get; set; }
            [ToolParameter("Hoop x target (default 0)", Required = false)] public float? x { get; set; }
            [ToolParameter("Swipe power (default 0.6)", Required = false)] public float? power { get; set; }
            [ToolParameter("Max seconds to record (default 20)", Required = false)] public float? seconds { get; set; }
            [ToolParameter("Capture every Nth frame (default 3)", Required = false)] public int? every { get; set; }
            [ToolParameter("Folder name under Temp/record (default 'basket')", Required = false)] public string name { get; set; }
            [ToolParameter("Capture width in px (default 540; height follows the camera aspect)", Required = false)] public int? width { get; set; }
        }

        private static bool _running;
        private static int _phase, _frame, _captured, _every = 3;
        private static double _phaseStart, _start;
        private static float _maxSeconds = 20f, _x, _power;
        private static int _level = 1;
        private static string _dir = "Temp/record/basket";
        private static string _result = "";
        private static int _width = 540;
        private static float _startGameTime;
        private static RenderTexture _rt;
        private static Texture2D _tex;
        private static int _lastGameFrame;

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
            _level = AIToolsCommon.GetInt(p, "level", 1);
            _x = AIToolsCommon.GetFloat(p, "x", 0f);
            _power = AIToolsCommon.GetFloat(p, "power", 0.6f);
            _maxSeconds = AIToolsCommon.GetFloat(p, "seconds", 20f);
            _every = Mathf.Max(1, AIToolsCommon.GetInt(p, "every", 3));
            _dir = "Temp/record/" + (AIToolsCommon.GetString(p, "name", "basket") ?? "basket");
            _width = Mathf.Clamp(AIToolsCommon.GetInt(p, "width", 540), 120, 2160);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            Directory.CreateDirectory(_dir);
            _phase = 0; _frame = 0; _captured = 0; _result = "";
            // Offline recording: every player-loop step advances exactly 1/30 s of game time, whatever the editor's
            // real frame rate, and the loop is stepped from here so the editor needs no focus (QueuePlayerLoopUpdate).
            Time.captureDeltaTime = 1f / 30f;
            // The editor only steps the player loop while a Game view repaints; with the main window on a sleeping
            // display or behind other apps nothing advances (seen: Time.frameCount stuck at 1). So: a small floating
            // Game view on the main display + pause/Step: every editor tick runs exactly one player frame.
            OpenFloatingGameView();
            EditorApplication.isPaused = true;
            _running = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return new SuccessResponse($"Recording to {_dir} (level {_level}, x {_x}, power {_power}, every {_every} frames, max {_maxSeconds}s).", Status());
        }

        private static void Stop(string why)
        {
            _running = false;
            _result = why;
            EditorApplication.update -= Tick;
            Time.captureDeltaTime = 0f;
            EditorApplication.isPaused = false;
            CloseFloatingGameView();
            if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); _rt = null; }
            if (_tex != null) { UnityEngine.Object.DestroyImmediate(_tex); _tex = null; }
        }

        private static EditorWindow _floating;

        private static void OpenFloatingGameView()
        {
            try
            {
                var type = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (type == null) return;
                _floating = ScriptableObject.CreateInstance(type) as EditorWindow;
                if (_floating == null) return;
                _floating.titleContent = new GUIContent("Game (record)");
                _floating.ShowUtility();
                _floating.position = new Rect(1040f, 70f, 380f, 700f);
                _floating.Repaint();
            }
            catch (Exception e) { Debug.LogWarning("[AI Tools] floating game view: " + e.Message); }
        }

        private static void CloseFloatingGameView()
        {
            try { if (_floating != null) { _floating.Close(); _floating = null; } }
            catch (Exception e) { Debug.LogWarning("[AI Tools] close floating game view: " + e.Message); }
        }

        /// <summary>Render the main camera into an offscreen texture and write a PNG — no Game view focus needed.</summary>
        private static void Capture(string path)
        {
            var cam = Camera.main;
            if (cam == null) return;
            int w = _width, h = Mathf.RoundToInt(_width / Mathf.Max(0.1f, cam.aspect));
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) { _rt.Release(); UnityEngine.Object.DestroyImmediate(_rt); }
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            }
            var prev = cam.targetTexture;
            cam.targetTexture = _rt;
            cam.Render();
            cam.targetTexture = prev;
            var active = RenderTexture.active;
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            _tex.Apply(false);
            RenderTexture.active = active;
            File.WriteAllBytes(path, _tex.EncodeToPNG());
        }

        private static void Tick()
        {
            if (!_running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
            var round = UnityEngine.Object.FindAnyObjectByType<BasketRound>();
            if (round == null) { Stop("no round"); return; }
            double now = EditorApplication.timeSinceStartup;
            EditorApplication.Step();
            switch (_phase)
            {
                case 0:
                    round.LoadLevel(_level);
                    _phase = 1; _frame = 0;
                    break;
                case 1:
                    if (++_frame >= 20) { round.ShootProgrammatic(_x, _power); _phase = 2; _start = now; _frame = 0; _startGameTime = Time.time; }
                    break;
                case 2:
                    if (_frame % _every == 0)
                    {
                        // Physics time: in pause/Step mode Time.time barely advances (~4 ms per step), round.Elapsed is the real clock.
                        float t = round.Elapsed;
                        string path = Path.Combine(_dir, $"f_{_captured:0000}_t{t:0.000}_b{round.BasketCount}.png");
                        Capture(path);
                        _captured++;
                    }
                    _frame++;
                    if (round.Current == BasketRound.State.Done) { Stop($"done: basket {round.BasketCount}, top passes {round.TopPasses}, elapsed {round.Elapsed:0.0}s, missed {round.Missed}"); return; }
                    if (round.Elapsed > _maxSeconds) { Stop($"timeout after {_maxSeconds}s game time: basket {round.BasketCount}"); return; }
                    break;
            }
        }

        private static object Status()
        {
            var round = UnityEngine.Object.FindAnyObjectByType<BasketRound>();
            return new
            {
                running = _running, phase = _phase, frames = _captured, dir = _dir, result = _result,
                editorPaused = EditorApplication.isPaused, playing = EditorApplication.isPlaying,
                timeScale = Time.timeScale, gameFrame = Time.frameCount, gameTime = Time.time,
                round = round == null ? null : new { state = round.Current.ToString(), basket = round.BasketCount, toThrow = round.BallsToThrow, inFlight = round.InFlight, topPasses = round.TopPasses, elapsed = round.Elapsed, missed = round.Missed, active = round.Pool != null ? round.Pool.Active.Count : -1 },
            };
        }
    }
}
