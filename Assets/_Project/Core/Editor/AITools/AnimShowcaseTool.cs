using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project.Editor.AITools
{
    /// <summary>Builds a showcase scene where a character prefab plays every clip in turn (<see cref="AnimShowcase"/>).</summary>
    [McpForUnityTool("ai_anim_showcase", Description =
        "Build a showcase scene for a character prefab (camera, light, floor, the prefab, AnimShowcase playing every clip in turn " +
        "with its name on screen). prefab: asset path; scene: .unity path to write (overwritten). Press Play to watch.")]
    public static class AnimShowcaseTool
    {
        public class Parameters
        {
            [ToolParameter("Character prefab asset path (with an Animator)")] public string prefab { get; set; }
            [ToolParameter("Scene path to write, e.g. Assets/_Project/Experiments/StudioAnim/StudioAnim.unity")] public string scene { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            string prefabPath = AIToolsCommon.GetString(p, "prefab"), scenePath = AIToolsCommon.GetString(p, "scene");
            if (string.IsNullOrEmpty(prefabPath) || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                return new ErrorResponse($"No prefab at '{prefabPath}'.");
            if (string.IsNullOrEmpty(scenePath) || !scenePath.StartsWith("Assets/") || !scenePath.EndsWith(".unity"))
                return new ErrorResponse("scene must be an Assets/... .unity path.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) return new ErrorResponse("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    return new ErrorResponse("An open scene has unsaved changes; save or discard them first (the showcase replaces the open scene).");
            Build(prefabPath, scenePath);
            string msg = $"Showcase scene {scenePath} plays every clip of {prefabPath}.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { scene = scenePath });
        }

        public static void Build(string prefabPath, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);   // after NewScene (it unloads unused assets)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            cam.fieldOfView = 35f;
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.58f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = Vector3.one * 0.6f;
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // Frame the character from its bounds: 3/4 front view, a little above the head.
            var b = new Bounds(hero.transform.position, Vector3.one);
            foreach (var r in hero.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float height = Mathf.Max(0.5f, b.size.y);
            camGo.transform.position = b.center + new Vector3(0.9f, 0.35f, -2.6f).normalized * height * 2.6f;
            camGo.transform.LookAt(b.center);
            var show = new GameObject("Showcase").AddComponent<AnimShowcase>();
            var so = new SerializedObject(show);
            so.FindProperty("animator").objectReferenceValue = hero.GetComponentInChildren<Animator>();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, scenePath);
        }
    }
}
