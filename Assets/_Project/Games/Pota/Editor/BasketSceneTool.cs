using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.Basket;
using Project.Juice;
using Project.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>Creates (or rebuilds) the Basket scene, its settings asset, physics materials and layers.</summary>
    [McpForUnityTool("ai_basket_build_scene", Description =
        "Create or rebuild Assets/_Project/Games/Pota/Scenes/Basket.unity: layers (BallFlight, BallDrop, Hoop, Slab), " +
        "BasketSettings asset + physics materials, camera, light, volume, Round (BasketRound/BallPool/ShotInput/AimPreview), HUD, EventSystem, VfxService. " +
        "level = level to preselect (default 1). The hoop column itself is built at runtime.")]
    public static class BasketSceneTool
    {
        public const string ScenePath = "Assets/_Project/Games/Pota/Scenes/Basket.unity";
        public const string SettingsFolder = "Assets/_Project/Games/Pota/Settings";
        public const string SettingsPath = SettingsFolder + "/BasketSettings.asset";
        public const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";

        public class Parameters
        {
            [ToolParameter("Level to preselect on the round (default 1)", Required = false)]
            public int? level { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            int level = AIToolsCommon.GetInt(p, "level", 1);
            var report = Build(level);
            string msg = $"Basket scene built at {ScenePath} (level {level}).";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { scene = ScenePath, settings = SettingsPath, steps = report });
        }

        [MenuItem("AI Tools/Basket/Build Basket Scene")]
        public static void BuildMenu() => AIToolsCommon.Log(string.Join(" | ", Build(1)));

        public sealed class RoundParts
        {
            public BasketRound Round;
            public ShotInput Input;
            public BasketJuice Juice;
        }

        /// <summary>Layers, settings asset and physics materials (idempotent).</summary>
        public static BasketSettings PrepareAssets(List<string> report)
        {
            foreach (var name in new[] { "BallFlight", "BallDrop", "Hoop", "Slab" })
                if (EnsureLayer(name)) report.Add($"layer {name} created");
            return EnsureSettings(report);
        }

        /// <summary>Main Camera at the aim pose with AudioListener, CameraShake and RoundCamera.</summary>
        public static Camera CreateCamera(BasketSettings settings)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = settings.Fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.transform.position = settings.AimCamPos;
            cam.transform.LookAt(settings.AimLookAt);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = settings.SkyColor;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraShake>();
            var roundCam = camGo.AddComponent<RoundCamera>();
            roundCam.S = settings;
            return cam;
        }

        public static Light CreateSun()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.957f, 0.878f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            return light;
        }

        /// <summary>Round (pool, round, aim preview, shot input) and its BasketJuice on the scene's juice host.</summary>
        public static RoundParts BuildRound(BasketSettings settings, int level, RoundCamera roundCam, JuiceSet juice)
        {
            var roundGo = new GameObject("Round");
            var pool = roundGo.AddComponent<BallPool>();
            pool.Settings = settings;
            var round = roundGo.AddComponent<BasketRound>();
            round.Settings = settings;
            round.Pool = pool;
            round.Level = level;
            var previewGo = new GameObject("AimPreview");
            previewGo.transform.SetParent(roundGo.transform, false);
            var preview = previewGo.AddComponent<AimPreview>();
            var input = roundGo.AddComponent<ShotInput>();
            input.Round = round;
            input.Preview = preview;
            roundCam.Round = round;
            roundCam.S = settings;
            // feel layer (spec 2026-10-03 F10–F13); they bind to the round, input and camera in Start
            var sling = roundGo.AddComponent<SlingshotFeel>();
            sling.Round = round; sling.Input = input; sling.Preview = preview; sling.Cam = roundCam;
            var miss = roundGo.AddComponent<MissFeel>();
            miss.Round = round; miss.Cam = roundCam;
            roundGo.AddComponent<SwishFeel>().Round = round;
            roundGo.AddComponent<BasketCountFeel>().Round = round;

            var bj = juice.Host.AddComponent<BasketJuice>();
            bj.Round = round;
            bj.S = settings;
            bj.Shake = juice.Shake;
            bj.TimeCtl = juice.TimeCtl;
            bj.Popups = juice.Popups;
            bj.Sfx = juice.Sfx;
            return new RoundParts { Round = round, Input = input, Juice = bj };
        }

        public static void ApplyRenderSettings(BasketSettings settings)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = settings.FogColor;
            RenderSettings.fogStartDistance = settings.FogStart;
            RenderSettings.fogEndDistance = settings.FogEnd;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.94f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.72f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.54f, 0.48f, 0.4f);
        }

        public static List<string> Build(int level)
        {
            var report = new List<string>();
            var settings = PrepareAssets(report);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = CreateCamera(settings);
            CreateSun();
            SceneParts.CreateGlobalVolume(VolumeProfilePath);
            var juiceGo = new GameObject("Juice");
            var juice = JuiceSet.Create(juiceGo, cam, cam.GetComponent<CameraShake>());
            BuildRound(settings, level, cam.GetComponent<RoundCamera>(), juice);

            var uiRoot = new GameObject("UI");
            BasketHud.Create(uiRoot.transform);
            SceneParts.CreateEventSystem(uiRoot.transform);

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(VfxAuthoringTool.LibraryPath);
            if (library != null) VfxAuthoringTool.EnsureServiceInScene(library, report);
            else report.Add("VfxLibrary missing (run AI Tools/VFX/Create Starter Effects first)");

            ApplyRenderSettings(settings);

            AIToolsCommon.EnsureFolder("Assets/_Project/Games/Pota/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"saved {ScenePath}");
            SceneParts.AddToBuildSettings(ScenePath);
            report.Add("build settings: scene present");
            AssetDatabase.SaveAssets();
            return report;
        }

        private static BasketSettings EnsureSettings(List<string> report)
        {
            AIToolsCommon.EnsureFolder(SettingsFolder);
            var settings = AssetDatabase.LoadAssetAtPath<BasketSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BasketSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                report.Add($"created {SettingsPath}");
            }
            settings.BallMaterial = EnsurePhysicsMaterial("Basket_Ball", 0.55f, 0.4f, report);
            settings.RimMaterial = EnsurePhysicsMaterial("Basket_Rim", 0.5f, 0.3f, report);
            settings.BackboardMaterial = EnsurePhysicsMaterial("Basket_Backboard", 0.5f, 0.3f, report);
            settings.WallMaterial = EnsurePhysicsMaterial("Basket_Wall", 0.6f, 0.3f, report);
            settings.CageMaterial = EnsurePhysicsMaterial("Basket_Cage", 0.12f, 0.5f, report);

            const string art = "Assets/_Project/Games/Pota/Art/";
            settings.FacadeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "kule-cam.jpg");
            settings.WingTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "kule-renkli.jpg");
            settings.CityTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "cephe.jpg");
            settings.SkyTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "sehir.jpg");
            settings.BackboardTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "pano.png");
            settings.PlayerPrefab = ChoosePlayerPrefab(settings.PlayerPrefab);
            settings.BallVisualMaterial = BasketballTextureTool.Ensure(false);
            report.Add($"art refs: facade={settings.FacadeTexture != null} wing={settings.WingTexture != null} city={settings.CityTexture != null} sky={settings.SkyTexture != null} board={settings.BackboardTexture != null} player={settings.PlayerPrefab != null} ball={settings.BallVisualMaterial != null}");
            EditorUtility.SetDirty(settings);
            return settings;
        }

        public const string DefaultPlayerPrefab = "Assets/_Project/Games/Pota/Prefabs/PotaHeroToon.prefab";

        /// <summary>Keeps the player prefab already in the settings (ai_character_setup assigns it); an empty slot gets
        /// the animated toon hero. Rebuilding the scene must not swap the player back to an older model.</summary>
        public static GameObject ChoosePlayerPrefab(GameObject current) =>
            current != null ? current : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefab);

        private static PhysicsMaterial EnsurePhysicsMaterial(string name, float bounciness, float friction, List<string> report)
        {
            string path = $"{SettingsFolder}/{name}.physicMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(pm, path);
                report.Add($"created {path}");
            }
            pm.bounciness = bounciness;
            pm.dynamicFriction = friction;
            pm.staticFriction = friction;
            pm.bounceCombine = PhysicsMaterialCombine.Average;
            pm.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        /// <summary>Adds a user layer to TagManager if missing. Returns true when created.</summary>
        public static bool EnsureLayer(string name)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return false;
            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return false;
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = name;
                    tagManager.ApplyModifiedProperties();
                    return true;
                }
            }
            Debug.LogWarning(AIToolsCommon.LogPrefix + $"no free layer slot for '{name}'");
            return false;
        }
    }
}
