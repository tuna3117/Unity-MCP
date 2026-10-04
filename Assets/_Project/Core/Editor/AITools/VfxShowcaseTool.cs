using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Builds a showcase scene for a VfxLibrary: camera, light, a floor for ground effects, a VfxService with the library and
    /// a <see cref="VfxShowcase"/> that plays every effect in turn. Press Play to look at a game's effects together.
    /// </summary>
    [McpForUnityTool("ai_vfx_showcase", Description =
        "Build a showcase scene for a VfxLibrary (camera, light, floor, VfxService, VfxShowcase cycling every effect). " +
        "library: VfxLibrary asset path; scene: .unity path to write (overwritten). Press Play to watch.")]
    public static class VfxShowcaseTool
    {
        public class Parameters
        {
            [ToolParameter("VfxLibrary asset path")] public string library { get; set; }
            [ToolParameter("Scene path to write, e.g. Assets/_Project/Experiments/StudioVfx/StudioVfx.unity")] public string scene { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            string libraryPath = AIToolsCommon.GetString(p, "library"), scenePath = AIToolsCommon.GetString(p, "scene");
            var library = string.IsNullOrEmpty(libraryPath) ? null : AssetDatabase.LoadAssetAtPath<VfxLibrary>(libraryPath);
            if (library == null) return new ErrorResponse($"No VfxLibrary at '{libraryPath}'.");
            if (string.IsNullOrEmpty(scenePath) || !scenePath.StartsWith("Assets/") || !scenePath.EndsWith(".unity"))
                return new ErrorResponse("scene must be an Assets/... .unity path.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) return new ErrorResponse("Stop Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    return new ErrorResponse("An open scene has unsaved changes; save or discard them first (the showcase replaces the open scene).");
            Build(libraryPath, scenePath);
            string msg = $"Showcase scene {scenePath} plays the {library.effects.Count} effects of {libraryPath} in turn.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { scene = scenePath, effects = library.effects.Count });
        }

        public static void Build(string libraryPath, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after NewScene: opening a scene unloads unused assets, and a library loaded before it came back as a
            // destroyed object (the scene was saved with an empty reference).
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(libraryPath);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            cam.fieldOfView = 40f;
            camGo.transform.position = new Vector3(0f, 1.6f, -4.2f);
            camGo.transform.LookAt(new Vector3(0f, 0.6f, 0f));
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = Vector3.one * 0.6f;
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var vfx = new GameObject("VfxService");
            var service = vfx.AddComponent<VfxService>();
            var so = new SerializedObject(service);
            so.FindProperty("library").objectReferenceValue = library;
            so.ApplyModifiedPropertiesWithoutUndo();
            var spot = new GameObject("Showcase");
            spot.transform.position = new Vector3(0f, 0.6f, 0f);
            spot.AddComponent<VfxShowcase>();
            EditorSceneManager.SaveScene(scene, scenePath);
        }
    }
}
