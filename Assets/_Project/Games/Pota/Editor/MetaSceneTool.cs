using System.Collections.Generic;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Builds Scenes/Meta.unity, the menu/reward showcase (spec 2026-10-03 meta feel): a UI camera, the root canvas at 1080×1920
    /// (match width), the MetaDirector with every screen built from MetaSettings, EventSystem; adds the scene to Build Settings and the
    /// Pota profile; Game view 1170×2532. Run ai_potameta_import first.
    /// </summary>
    [McpForUnityTool("ai_meta_build_scene", Description = "Builds Scenes/Meta.unity (Pota menu/reward showcase) from Settings/MetaSettings.asset; adds it to Build Settings and PotaProfile.")]
    public static class MetaSceneTool
    {
        public const string ScenePath = "Assets/_Project/Games/Pota/Scenes/Meta.unity";

        public static object HandleCommand(JObject p) => new { success = true, report = Build() };

        [MenuItem("AI Tools/Pota/Build Meta Scene")]
        public static void Menu() => AIToolsCommon.Log(string.Join(" | ", Build()));

        public static List<string> Build()
        {
            var report = new List<string>();
            var settings = AssetDatabase.LoadAssetAtPath<MetaSettings>(MetaSettings.AssetPath);
            if (settings == null) { report.Add("no MetaSettings: run ai_potameta_import first"); return report; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MetaStyle.Sky;
            cam.orthographic = true;
            cam.cullingMask = 0;
            camGo.AddComponent<AudioListener>();
            var uiRoot = new GameObject("UI");
            SceneParts.CreateEventSystem(uiRoot.transform);
            var canvasGo = new GameObject("MetaRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(uiRoot.transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            // the screens are built at runtime (MetaDirector.Awake): kit components keep their references in private fields, which an
            // Edit Mode build would not serialize (CLAUDE.md lesson: tool-assigned private fields come back null)
            var directorGo = new GameObject("MetaDirector");
            var director = directorGo.AddComponent<MetaDirector>();
            director.S = settings;
            director.SetRoot((RectTransform)canvasGo.transform);
            report.Add("director placed (screens build on Play)");
            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add("saved " + ScenePath);
            SceneParts.AddToBuildSettings(ScenePath);
            SceneParts.AddToProfile(PotaFlowSceneTool.ProfilePath, ScenePath, report);
            AssetDatabase.SaveAssets();
            report.Add(GameViewPortrait.Apply(1170, 2532));
            return report;
        }
    }
}
