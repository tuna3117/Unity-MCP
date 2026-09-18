using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Project.VFX;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Builds the starter VFX set programmatically: particle prefabs, URP particle materials,
    /// VfxDefinition assets, the VfxLibrary and a VfxService in the active scene.
    /// </summary>
    [McpForUnityTool("ai_vfx_create_starters", Description =
        "Create the starter VFX set (hit, explosion, pickup) as Particle System prefabs under Assets/_Project/VFX, " +
        "their VfxDefinition assets, the VfxLibrary asset and a VfxService object in the active scene. " +
        "overwrite=true rebuilds prefabs/materials/definitions that already exist.")]
    public static class VfxAuthoringTool
    {
        public const string VfxFolder = "Assets/_Project/VFX";
        public const string MaterialsFolder = VfxFolder + "/Materials";
        public const string PrefabsFolder = VfxFolder + "/Prefabs";
        public const string DefinitionsFolder = VfxFolder + "/Definitions";
        public const string LibraryPath = VfxFolder + "/VfxLibrary.asset";
        public const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

        public class Parameters
        {
            [ToolParameter("Rebuild assets that already exist (default false)", Required = false)]
            public bool? overwrite { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            bool overwrite = AIToolsCommon.GetBool(p, "overwrite", false);
            var report = CreateAll(overwrite);
            string msg = $"Starter VFX ready ({report.Count} step(s)). Use VfxService.Play(\"hit\"|\"explosion\"|\"pickup\", position, rotation).";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { steps = report, library = LibraryPath });
        }

        [MenuItem("AI Tools/VFX/Create Starter Effects (hit, explosion, pickup)")]
        public static void CreateStartersMenu()
        {
            var report = CreateAll(false);
            AIToolsCommon.Log("Starter VFX: " + string.Join(" | ", report));
        }

        public static List<string> CreateAll(bool overwrite)
        {
            var report = new List<string>();
            AIToolsCommon.EnsureFolder(MaterialsFolder);
            AIToolsCommon.EnsureFolder(PrefabsFolder);
            AIToolsCommon.EnsureFolder(DefinitionsFolder);

            var texture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            var additive = CreateParticleMaterial("VFX_Additive", texture, true, overwrite, report);
            var alpha = CreateParticleMaterial("VFX_Alpha", texture, false, overwrite, report);

            var hitPrefab = SavePrefab("VFX_Hit", overwrite, report, root => BuildHit(root, additive));
            var explosionPrefab = SavePrefab("VFX_Explosion", overwrite, report, root => BuildExplosion(root, additive, alpha));
            var pickupPrefab = SavePrefab("VFX_Pickup", overwrite, report, root => BuildPickup(root, additive));

            var defs = new List<VfxDefinition>
            {
                CreateDefinition("hit", hitPrefab, 0.6f, 4, 32, overwrite, report),
                CreateDefinition("explosion", explosionPrefab, 3f, 2, 8, overwrite, report),
                CreateDefinition("pickup", pickupPrefab, 1.2f, 3, 16, overwrite, report),
            };

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
                report.Add($"created {LibraryPath}");
            }
            foreach (var d in defs)
                if (d != null && !library.effects.Contains(d)) library.effects.Add(d);
            EditorUtility.SetDirty(library);

            EnsureServiceInScene(library, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        // ---------- Materials ----------

        private static Material CreateParticleMaterial(string name, Texture2D texture, bool additive, bool overwrite, List<string> report)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && !overwrite) return existing;

            var shader = Shader.Find(ParticleShader);
            var m = existing != null ? existing : new Material(shader);
            m.shader = shader;
            m.SetFloat("_Surface", 1f);                 // transparent
            m.SetFloat("_Blend", additive ? 2f : 0f);   // 2 = additive, 0 = alpha
            m.SetFloat("_ColorMode", 0f);               // multiply vertex color
            m.SetFloat("_SoftParticlesEnabled", 0f);
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            try
            {
                BaseShaderGUI.SetupMaterialBlendMode(m);
                ParticleGUI.SetMaterialKeywords(m);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(AIToolsCommon.LogPrefix + $"material keyword setup failed for {name}: {ex.Message}");
            }
            if (existing == null) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            report.Add($"{(existing == null ? "created" : "rebuilt")} {path}");
            return m;
        }

        // ---------- Prefabs ----------

        private static GameObject SavePrefab(string name, bool overwrite, List<string> report, System.Action<GameObject> build)
        {
            string path = $"{PrefabsFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite) return existing;

            var root = new GameObject(name);
            try
            {
                root.AddComponent<VfxInstance>();
                build(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Add($"{(existing == null ? "created" : "rebuilt")} {path}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ParticleSystem AddSystem(GameObject parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            return ps;
        }

        private static void Burst(ParticleSystem ps, int count, float time = 0f)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) });
        }

        private static void ColorOverLifetime(ParticleSystem ps, params (Color color, float time)[] keys)
        {
            var module = ps.colorOverLifetime;
            module.enabled = true;
            var colorKeys = new GradientColorKey[keys.Length];
            var alphaKeys = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                colorKeys[i] = new GradientColorKey(keys[i].color, keys[i].time);
                alphaKeys[i] = new GradientAlphaKey(keys[i].color.a, keys[i].time);
            }
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            module.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SizeOverLifetime(ParticleSystem ps, float start, float end)
        {
            var module = ps.sizeOverLifetime;
            module.enabled = true;
            module.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
        }

        private static void BuildHit(GameObject root, Material additive)
        {
            // Sparks flying out of the impact point (rotation = surface normal via LookRotation).
            var sparks = AddSystem(root, "Sparks", additive);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(3f, 1.8f, 0.6f, 1f), new Color(3f, 3f, 2f, 1f));
            main.gravityModifier = 1f;
            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            Burst(sparks, 18);
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            ColorOverLifetime(sparks, (Color.white, 0f), (new Color(1f, 0.6f, 0.2f, 1f), 0.6f), (new Color(1f, 0.3f, 0.1f, 0f), 1f));

            // Quick bright flash.
            var flash = AddSystem(root, "Flash", additive);
            var fm = flash.main;
            fm.startLifetime = 0.12f;
            fm.startSpeed = 0f;
            fm.startSize = 0.6f;
            fm.startColor = new Color(3f, 2.4f, 1.6f, 1f);
            Burst(flash, 1);
            SizeOverLifetime(flash, 0.4f, 1f);
            ColorOverLifetime(flash, (Color.white, 0f), (new Color(1f, 1f, 1f, 0f), 1f));
        }

        private static void BuildExplosion(GameObject root, Material additive, Material alpha)
        {
            var fire = AddSystem(root, "Fireball", additive);
            var fmain = fire.main;
            fmain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            fmain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            fmain.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            fmain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            fmain.startColor = new Color(3f, 1.6f, 0.4f, 1f);
            var fshape = fire.shape;
            fshape.enabled = true;
            fshape.shapeType = ParticleSystemShapeType.Sphere;
            fshape.radius = 0.3f;
            Burst(fire, 24);
            SizeOverLifetime(fire, 0.5f, 1.4f);
            ColorOverLifetime(fire, (new Color(1f, 1f, 0.8f, 1f), 0f), (new Color(1f, 0.5f, 0.1f, 1f), 0.4f), (new Color(0.3f, 0.05f, 0f, 0f), 1f));

            var smoke = AddSystem(root, "Smoke", alpha);
            var smain = smoke.main;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            smain.startSize = new ParticleSystem.MinMaxCurve(1f, 2f);
            smain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            smain.startColor = new Color(0.25f, 0.22f, 0.2f, 0.8f);
            smain.gravityModifier = -0.08f;
            var sshape = smoke.shape;
            sshape.enabled = true;
            sshape.shapeType = ParticleSystemShapeType.Sphere;
            sshape.radius = 0.4f;
            Burst(smoke, 14, 0.05f);
            SizeOverLifetime(smoke, 0.6f, 1.8f);
            ColorOverLifetime(smoke, (new Color(0.3f, 0.25f, 0.2f, 0.7f), 0f), (new Color(0.2f, 0.2f, 0.2f, 0.4f), 0.5f), (new Color(0.15f, 0.15f, 0.15f, 0f), 1f));

            var debris = AddSystem(root, "Debris", additive);
            var dmain = debris.main;
            dmain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            dmain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            dmain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            dmain.startColor = new Color(3f, 2f, 1f, 1f);
            dmain.gravityModifier = 1.5f;
            var dshape = debris.shape;
            dshape.enabled = true;
            dshape.shapeType = ParticleSystemShapeType.Sphere;
            dshape.radius = 0.2f;
            Burst(debris, 36);
            var drenderer = debris.GetComponent<ParticleSystemRenderer>();
            drenderer.renderMode = ParticleSystemRenderMode.Stretch;
            drenderer.velocityScale = 0.03f;
            drenderer.lengthScale = 2f;
            ColorOverLifetime(debris, (Color.white, 0f), (new Color(1f, 0.5f, 0.2f, 1f), 0.5f), (new Color(0.5f, 0.1f, 0f, 0f), 1f));

            var shock = AddSystem(root, "Shockwave", additive);
            var shmain = shock.main;
            shmain.startLifetime = 0.35f;
            shmain.startSpeed = 0f;
            shmain.startSize = 1f;
            shmain.startColor = new Color(2f, 1.6f, 1.2f, 0.8f);
            Burst(shock, 1);
            var shrenderer = shock.GetComponent<ParticleSystemRenderer>();
            shrenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            SizeOverLifetime(shock, 0.2f, 6f);
            ColorOverLifetime(shock, (new Color(1f, 1f, 1f, 0.9f), 0f), (new Color(1f, 0.8f, 0.6f, 0f), 1f));
        }

        private static void BuildPickup(GameObject root, Material additive)
        {
            var sparkles = AddSystem(root, "Sparkles", additive);
            var main = sparkles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 2.5f, 3f, 1f), new Color(2f, 3f, 3f, 1f));
            main.gravityModifier = -0.4f;
            var shape = sparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            Burst(sparkles, 22);
            SizeOverLifetime(sparkles, 1f, 0f);
            ColorOverLifetime(sparkles, (Color.white, 0f), (new Color(0.5f, 0.9f, 1f, 1f), 0.5f), (new Color(0.3f, 0.6f, 1f, 0f), 1f));

            var ring = AddSystem(root, "Ring", additive);
            var rmain = ring.main;
            rmain.startLifetime = 0.4f;
            rmain.startSpeed = 0f;
            rmain.startSize = 0.6f;
            rmain.startColor = new Color(0.8f, 2.4f, 3f, 0.9f);
            Burst(ring, 1);
            var rrenderer = ring.GetComponent<ParticleSystemRenderer>();
            rrenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            SizeOverLifetime(ring, 0.3f, 3f);
            ColorOverLifetime(ring, (new Color(1f, 1f, 1f, 0.9f), 0f), (new Color(0.5f, 0.8f, 1f, 0f), 1f));
        }

        // ---------- Definitions / library / scene ----------

        private static VfxDefinition CreateDefinition(string id, GameObject prefab, float lifetime, int prewarm, int maxPool, bool overwrite, List<string> report)
        {
            if (prefab == null) return null;
            string path = $"{DefinitionsFolder}/VFX_{id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<VfxDefinition>(path);
            bool isNew = def == null;
            if (def == null) def = ScriptableObject.CreateInstance<VfxDefinition>();
            if (isNew || overwrite)
            {
                def.id = id;
                def.prefab = prefab;
                def.lifetime = lifetime;
                def.prewarmCount = prewarm;
                def.maxPoolSize = maxPool;
                def.scale = 1f;
                if (isNew) AssetDatabase.CreateAsset(def, path);
                else EditorUtility.SetDirty(def);
                report.Add($"{(isNew ? "created" : "rebuilt")} {path}");
            }
            return def;
        }

        public static VfxService EnsureServiceInScene(VfxLibrary library, List<string> report)
        {
            var service = Object.FindFirstObjectByType<VfxService>();
            if (service == null)
            {
                var go = new GameObject("VfxService");
                Undo.RegisterCreatedObjectUndo(go, "Create VfxService");
                service = go.AddComponent<VfxService>();
                report.Add("created VfxService in active scene");
            }
            var so = new SerializedObject(service);
            var prop = so.FindProperty("library");
            if (prop != null && prop.objectReferenceValue != library)
            {
                prop.objectReferenceValue = library;
                so.ApplyModifiedProperties();
                report.Add("assigned VfxLibrary to VfxService");
            }
            AIToolsCommon.MarkDirty(false);
            return service;
        }
    }
}
