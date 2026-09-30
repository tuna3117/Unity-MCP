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
            var sun = BasketSceneTool.CreateSun();
            var fill = ColorHuntSceneTool.CreateFill();
            SceneParts.EnablePost(cam);                  // only the puzzle volumes carry overrides: the basket looks as approved

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
            var look = d.gameObject.AddComponent<PuzzleLighting>();
            look.Sun = sun;
            look.Fill = fill;
            look.Cam = cam;
            look.Source = board.PuzzleProfile;
            d.Look = look;
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
