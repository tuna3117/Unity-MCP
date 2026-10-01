using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.ColorHunt;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>Creates (or rebuilds) the special-balls showcase: the Color Hunt scene's parts, no auto deal, and the SpecialsDemo.</summary>
    [McpForUnityTool("ai_specials_build_scene", Description =
        "Create or rebuild Assets/_Project/Games/Pota/Scenes/Specials.unity: the Color Hunt scene's parts (camera, light, volume, game, HUD without " +
        "the hint, stage) with ColorHuntGame.AutoStart off and a SpecialsDemo (scenario buttons, Pota | 1 | 2, Tekrar; built at runtime). " +
        "Adds the scene to Build Settings and PotaProfile. Refuses while playing or while the open scene has unsaved changes.")]
    public static class SpecialsSceneTool
    {
        public const string ScenePath = "Assets/_Project/Games/Pota/Scenes/Specials.unity";

        public class Parameters { }

        public static object HandleCommand(JObject p)
        {
            string refusal = Refusal();
            if (refusal != null) return new ErrorResponse(refusal);
            var report = Build();
            string msg = $"Specials scene built at {ScenePath}.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { scene = ScenePath, steps = report });
        }

        [MenuItem("AI Tools/Pota/Build Specials Scene")]
        public static void BuildMenu()
        {
            string refusal = Refusal();
            if (refusal != null) { Debug.LogWarning("[ai_specials_build_scene] " + refusal); return; }
            AIToolsCommon.Log(string.Join(" | ", Build()));
        }

        private static string Refusal()
        {
            if (EditorApplication.isPlaying) return "Leave Play Mode first (manage_editor stop).";
            var active = EditorSceneManager.GetActiveScene();
            return active.isDirty ? $"The open scene {active.path} has unsaved changes: save or discard them first." : null;
        }

        public static List<string> Build()
        {
            var report = new List<string>();
            var settings = ColorHuntSceneTool.EnsureSettings(report);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var parts = ColorHuntSceneTool.BuildPuzzleScene(settings);
            parts.Game.AutoStart = false;                 // SpecialsDemo stages the boards
            parts.Hud.ShowHint = false;                   // the demo's rows use the band under the board
            var demo = new GameObject("SpecialsDemo").AddComponent<SpecialsDemo>();
            demo.Game = parts.Game;

            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");
            SceneParts.AddToBuildSettings(ScenePath);
            SceneParts.AddToProfile(PotaFlowSceneTool.ProfilePath, ScenePath, report);
            AssetDatabase.SaveAssets();
            report.Add(GameViewPortrait.Apply(1080, 1920));
            return report;
        }
    }
}
