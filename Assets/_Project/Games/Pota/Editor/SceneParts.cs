using System.Collections.Generic;
using Project.Juice;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Editor.AITools
{
    /// <summary>One scene's juice components. One TimeScaleController per scene: two would fight over Time.timeScale.</summary>
    public sealed class JuiceSet
    {
        public GameObject Host;
        public TimeScaleController TimeCtl;
        public PopupText Popups;
        public SynthSfx Sfx;
        public CameraShake Shake;

        public static JuiceSet Create(GameObject host, Camera cam, CameraShake shake, Font font = null)
        {
            var set = new JuiceSet { Host = host, Shake = shake };
            set.TimeCtl = host.AddComponent<TimeScaleController>();
            set.Popups = host.AddComponent<PopupText>();
            set.Popups.Cam = cam;
            if (font != null) set.Popups.Font = font;
            set.Sfx = host.AddComponent<SynthSfx>();
            return set;
        }
    }

    /// <summary>Scene-building steps shared by the Pota scene tools.</summary>
    public static class SceneParts
    {
        /// <summary>The build list with <paramref name="path"/> present and enabled: appended if missing, never moved.</summary>
        public static EditorBuildSettingsScene[] WithScene(EditorBuildSettingsScene[] current, string path)
        {
            var list = new List<EditorBuildSettingsScene>(current ?? new EditorBuildSettingsScene[0]);
            int i = list.FindIndex(s => s.path == path);
            if (i < 0) list.Add(new EditorBuildSettingsScene(path, true));
            else if (!list[i].enabled) list[i] = new EditorBuildSettingsScene(path, true);
            return list.ToArray();
        }

        public static void AddToBuildSettings(string path) =>
            EditorBuildSettings.scenes = WithScene(EditorBuildSettings.scenes, path);

        /// <summary>Appends <paramref name="scenePath"/> to a GameProfile's scenes once (ai_build and the dev scene switcher take it from there).</summary>
        public static void AddToProfile(string profilePath, string scenePath, List<string> report)
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameProfile>(profilePath);
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (profile == null || sceneAsset == null) { report.Add($"{profilePath} or {scenePath} missing"); return; }
            if (System.Array.IndexOf(profile.Scenes, sceneAsset) >= 0) return;
            var so = new SerializedObject(profile);
            var list = so.FindProperty("Scenes");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sceneAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add($"{profile.name}: {sceneAsset.name} added");
        }

        public static Volume CreateGlobalVolume(string profilePath)
        {
            var volGo = new GameObject("Global Volume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            return vol;
        }

        public static Volume CreateGlobalVolume(VolumeProfile profile, string name = "Global Volume", float weight = 1f)
        {
            var vol = new GameObject(name).AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight = weight;
            vol.sharedProfile = profile;
            return vol;
        }

        /// <summary>URP post is per camera and off by default: without this no Volume does anything (found 2026-09-30).</summary>
        public static void EnablePost(Camera cam) => cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        public static GameObject CreateEventSystem(Transform parent)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(parent, false);
            return es;
        }
    }
}
