using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Basket;
using Project.ColorHunt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>Creates (or rebuilds) the Color Hunt scene and its settings asset.</summary>
    [McpForUnityTool("ai_colorhunt_build_scene", Description =
        "Create or rebuild Assets/_Project/Scenes/ColorHunt.unity: ColorHuntSettings asset (block face, font, arrow icon, ball material), " +
        "camera (fit-to-board + shake), light, volume, Game (rules/animation), Fx, Salvo, Juice, HUD, EventSystem. The board itself is built at runtime.")]
    public static class ColorHuntSceneTool
    {
        public const string ScenePath = "Assets/_Project/Scenes/ColorHunt.unity";
        public const string SettingsPath = "Assets/_Project/Settings/ColorHuntSettings.asset";
        public const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";
        public const string BlockFacePath = "Assets/_Project/Art/Pota/blok-a.jpg";
        public const string ArrowPath = "Assets/_Project/Art/Pota/ok-a.png";
        public const string FontPath = "Assets/_Project/Art/Fonts/ArialRoundedBold.ttf";
        public const string BallMaterialPath = "Assets/_Project/Art/Generated/Basketball.mat";

        public class Parameters { }

        public static object HandleCommand(JObject p)
        {
            var report = Build();
            string msg = $"Color Hunt scene built at {ScenePath}.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { scene = ScenePath, settings = SettingsPath, steps = report });
        }

        [MenuItem("AI Tools/Color Hunt/Build Scene")]
        public static void BuildMenu() => AIToolsCommon.Log(string.Join(" | ", Build()));

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

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            lightGo.transform.rotation = Quaternion.Euler(38f, -22f, 0f);
            var fillGo = new GameObject("Fill Light");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.75f, 0.85f, 1f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(-20f, 160f, 0f);

            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);

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
            var juiceGo = new GameObject("Juice");
            juiceGo.transform.SetParent(gameGo.transform, false);
            var timeCtl = juiceGo.AddComponent<TimeScaleController>();
            var popups = juiceGo.AddComponent<PopupText>();
            popups.Cam = cam;
            popups.Font = settings.Font;
            var sfx = juiceGo.AddComponent<SynthSfx>();
            var juice = juiceGo.AddComponent<ColorHuntJuice>();
            juice.S = settings; juice.Shake = shake; juice.TimeCtl = timeCtl; juice.Popups = popups; juice.Sfx = sfx;
            game.Fx = fx; game.Salvo = salvo; game.Juice = juice;
            var input = gameGo.AddComponent<ColorHuntInput>();
            input.Game = game; input.Cam = cam;

            var uiRoot = new GameObject("UI");
            var hud = ColorHuntHud.Create(uiRoot.transform, settings);
            hud.Game = game;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(uiRoot.transform, false);
            // HUD subscribes at runtime through a small bootstrap so the serialized reference is enough.
            uiRoot.AddComponent<ColorHuntHudBinder>().Hud = hud;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.90f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.70f, 0.72f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.40f);
            RenderSettings.fog = false;

            AIToolsCommon.EnsureFolder("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            report.Add(GameViewPortrait.Apply(1080, 1920));
            return report;
        }

        private static ColorHuntSettings EnsureSettings(List<string> report)
        {
            AIToolsCommon.EnsureFolder("Assets/_Project/Settings");
            var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>(SettingsPath);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<ColorHuntSettings>();
                AssetDatabase.CreateAsset(s, SettingsPath);
                report.Add($"created {SettingsPath}");
            }
            if (s.BlockFace == null) s.BlockFace = AssetDatabase.LoadAssetAtPath<Texture2D>(BlockFacePath);
            if (s.ArrowIcon == null) s.ArrowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ArrowPath);
            if (s.Font == null) s.Font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (s.BallMaterial == null) s.BallMaterial = AssetDatabase.LoadAssetAtPath<Material>(BallMaterialPath);
            EditorUtility.SetDirty(s);
            report.Add($"settings: face={(s.BlockFace != null)} arrow={(s.ArrowIcon != null)} font={(s.Font != null)} ball={(s.BallMaterial != null)}");
            return s;
        }
    }

    /// <summary>
    /// Drives the Color Hunt round in Play Mode for screenshots and checks: tap a cell, rotate, auto-play N moves,
    /// slow motion, status. Auto-play picks a cheap block of a target colour (or the cheapest block).
    /// </summary>
    [McpForUnityTool("ai_colorhunt_play", Description =
        "Drive the Color Hunt scene in Play Mode. action: 'status' (board, ammo, targets, busy) | 'tap' {cell} | 'rotate' {dir:'cw'|'ccw'} | " +
        "'auto' {moves} (queues N sensible moves, one per settle) | 'slowmo' {scale} (Time.timeScale for screenshots) | 'retry' (new board).")]
    public static class ColorHuntPlayTool
    {
        public class Parameters
        {
            [ToolParameter("status | tap | rotate | auto | slowmo | retry", Required = false)] public string action { get; set; }
            [ToolParameter("Cell index for tap (0..n²−1, row-major, row 0 = top)", Required = false)] public int? cell { get; set; }
            [ToolParameter("'cw' or 'ccw' for rotate", Required = false)] public string dir { get; set; }
            [ToolParameter("Number of moves for auto", Required = false)] public int? moves { get; set; }
            [ToolParameter("Time scale for slowmo", Required = false)] public float? scale { get; set; }
            [ToolParameter("Seconds the slow motion lasts (default 60)", Required = false)] public float? seconds { get; set; }
        }

        private static int _autoLeft;
        private static bool _autoRunning;
        private static System.Random _rng = new System.Random(3);

        public static object HandleCommand(JObject p)
        {
            if (!EditorApplication.isPlaying) return new ErrorResponse("Enter Play Mode first (manage_editor play) with ColorHunt.unity open.");
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            if (game == null) return new ErrorResponse("No ColorHuntGame in the running scene.");
            string action = (AIToolsCommon.GetString(p, "action", "status") ?? "status").ToLowerInvariant();
            switch (action)
            {
                case "tap":
                    {
                        int cell = AIToolsCommon.GetInt(p, "cell", -1);
                        string r = game.Tap(cell);
                        return new SuccessResponse($"tap {cell}: {r}", Status(game));
                    }
                case "rotate":
                    {
                        string d = (AIToolsCommon.GetString(p, "dir", "cw") ?? "cw").ToLowerInvariant();
                        bool ok = game.Rotate(d == "ccw" ? Rotation.Counter : Rotation.Clockwise);
                        return new SuccessResponse($"rotate {d}: {ok}", Status(game));
                    }
                case "auto":
                    _autoLeft = AIToolsCommon.GetInt(p, "moves", 5);
                    if (!_autoRunning) { _autoRunning = true; EditorApplication.update += AutoTick; }
                    return new SuccessResponse($"auto: {_autoLeft} moves queued", Status(game));
                case "slowmo":
                    {
                        float scale = Mathf.Clamp(AIToolsCommon.GetFloat(p, "scale", 1f), 0.01f, 1f);
                        float seconds = AIToolsCommon.GetFloat(p, "seconds", 60f);
                        if (game.Juice != null && game.Juice.TimeCtl != null) game.Juice.TimeCtl.SlowMo(scale, scale >= 1f ? 0f : seconds);
                        else Time.timeScale = scale;
                        return new SuccessResponse($"slow motion {scale} for {seconds}s", Status(game));
                    }
                case "retry":
                    game.NewRound();
                    return new SuccessResponse("new round", Status(game));
                default:
                    return new SuccessResponse("status", Status(game));
            }
        }

        private static void AutoTick()
        {
            if (!EditorApplication.isPlaying) { Stop(); return; }
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            if (game == null || _autoLeft <= 0 || game.Current != ColorHuntGame.State.Playing) { Stop(); return; }
            if (game.Busy) return;
            var b = game.Board;
            int best = -1, bestHp = int.MaxValue;
            var targetColors = new HashSet<int>();
            foreach (var t in b.Targets) if (t.Remaining > 0) targetColors.Add(t.Color);
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var c = b.Cells[i];
                if (c.Hp > b.Ammo) continue;
                int score = c.Hp + (targetColors.Contains(c.Color) ? 0 : 100) + _rng.Next(0, 3);
                if (score < bestHp) { bestHp = score; best = i; }
            }
            if (best < 0) { Stop(); return; }
            if (_rng.Next(0, 5) == 0) { game.Rotate(_rng.Next(0, 2) == 0 ? Rotation.Clockwise : Rotation.Counter); return; }
            game.Tap(best);
            _autoLeft--;
        }

        private static void Stop()
        {
            _autoRunning = false;
            EditorApplication.update -= AutoTick;
        }

        private static object Status(ColorHuntGame game)
        {
            var b = game.Board;
            var rows = new List<string>();
            if (b != null)
            {
                for (int r = 0; r < b.Size; r++)
                {
                    var parts = new List<string>();
                    for (int c = 0; c < b.Size; c++) { var cell = b.Cells[ColorHuntRules.Index(b.Size, r, c)]; parts.Add($"{cell.Color}:{cell.Hp}"); }
                    rows.Add(string.Join(" ", parts));
                }
            }
            var targets = new List<string>();
            if (b != null) foreach (var t in b.Targets) targets.Add($"{t.Color}:{t.Remaining}");
            return new
            {
                state = game.Current.ToString(),
                busy = game.Busy,
                ammo = b?.Ammo ?? 0,
                displayedAmmo = game.DisplayedAmmo,
                targets,
                won = game.Won,
                rotations = game.Rotations,
                longestChain = game.LongestChain,
                seed = game.Seed,
                timeScale = Time.timeScale,
                board = rows,
            };
        }
    }
}
