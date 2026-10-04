using System;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Project.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Turns a Blender workshop effect delivery (vfx-flipbook sheet or vfx-mesh, already imported by ai_import_asset) into a
    /// pooled effect: prefab + material + VfxDefinition + an entry in the game's VfxLibrary, so game code only writes
    /// VfxService.Play("fire_toon", position). Spec: docs/superpowers/specs/2026-10-04-studio-vfx-kit-design.md §3.4.
    /// </summary>
    [McpForUnityTool("ai_vfx_from_delivery", Description =
        "Make a pooled VfxService effect from a Blender effect delivery. folder: the delivery's Unity folder (holds manifest.json), " +
        "or manifest: absolute path of a hand-off (imported first). Flipbook sheet -> one-burst sheet particle (stills pop and fade, " +
        "loops cycle 4x); single mesh -> mesh particles; multi-piece mesh -> MeshBurst. Writes VFX_<Id>.prefab/.mat and <id>.asset to " +
        "out (default Games/<game>/VFX/<id>) and registers it in library (default Games/<game>/VFX/<game>VfxLibrary.asset, created " +
        "with the Core effects). Rebuilding an id updates the assets in place.")]
    public static class VfxFromDeliveryTool
    {
        public class Parameters
        {
            [ToolParameter("Unity folder of the imported delivery (contains manifest.json)", Required = false)] public string folder { get; set; }
            [ToolParameter("Absolute path to a hand-off manifest.json; imported with ai_import_asset first", Required = false)] public string manifest { get; set; }
            [ToolParameter("Effect id for VfxService.Play (default: delivery id without 'vfx-', '-' -> '_')", Required = false)] public string id { get; set; }
            [ToolParameter("Output folder (default Assets/_Project/Games/<game>/VFX/<id>)", Required = false)] public string @out { get; set; }
            [ToolParameter("VfxLibrary asset path (default Assets/_Project/Games/<game>/VFX/<game>VfxLibrary.asset)", Required = false)] public string library { get; set; }
            [ToolParameter("Size in metres (sheet height / mesh scale, default 1)", Required = false)] public float? size { get; set; }
            [ToolParameter("Particles per play (default 1 for sheets, 12 for a single mesh)", Required = false)] public int? count { get; set; }
            [ToolParameter("Sheet blending: alpha (default) or additive", Required = false)] public string blend { get; set; }
            [ToolParameter("HDR brightness multiplier for bloom (default 1)", Required = false)] public float? intensity { get; set; }
            [ToolParameter("Tint, #RRGGBB or #RRGGBBAA (default white)", Required = false)] public string color { get; set; }
            [ToolParameter("Sheets: camera (billboard, default) or ground (lies flat: cracks, dust rings); default from the manifest's flipbook.facing", Required = false)]
            public string facing { get; set; }
        }

        public class Options
        {
            public string id, outFolder, library;
            /// <summary>"camera" or "ground"; null = the manifest's flipbook.facing, else camera.</summary>
            public string facing;
            public float size = 1f, intensity = 1f;
            public int count = -1;
            public bool additive;
            public Color color = Color.white;
        }

        public class Result
        {
            public string id, kind, prefab, material, definition, library;
            public int frames, pieces;
            public float duration;
        }

        public static object HandleCommand(JObject p)
        {
            try
            {
                string folder = AIToolsCommon.GetString(p, "folder");
                string manifest = AIToolsCommon.GetString(p, "manifest");
                if (string.IsNullOrEmpty(folder) && string.IsNullOrEmpty(manifest))
                    return new ErrorResponse("Give 'folder' (imported delivery) or 'manifest' (hand-off to import).");
                if (string.IsNullOrEmpty(folder)) folder = ImportAssetTool.Import(manifest).folder;
                var o = new Options
                {
                    id = AIToolsCommon.GetString(p, "id"),
                    outFolder = AIToolsCommon.GetString(p, "out"),
                    library = AIToolsCommon.GetString(p, "library"),
                    size = AIToolsCommon.GetFloat(p, "size", 1f),
                    count = AIToolsCommon.GetInt(p, "count", -1),
                    intensity = AIToolsCommon.GetFloat(p, "intensity", 1f),
                };
                string blend = AIToolsCommon.GetString(p, "blend", "alpha");
                if (blend != "alpha" && blend != "additive") return new ErrorResponse("blend must be 'alpha' or 'additive'.");
                o.additive = blend == "additive";
                o.facing = AIToolsCommon.GetString(p, "facing");
                if (o.facing != null && o.facing != "camera" && o.facing != "ground")
                    return new ErrorResponse("facing must be 'camera' or 'ground'.");
                string color = AIToolsCommon.GetString(p, "color");
                if (!string.IsNullOrEmpty(color) && !ColorUtility.TryParseHtmlString(color, out o.color))
                    return new ErrorResponse($"Unreadable color '{color}' (use #RRGGBB or #RRGGBBAA).");
                var r = Build(folder, o);
                string msg = $"Effect '{r.id}' ({r.kind}) -> {r.prefab}, registered in {r.library}. Play: VfxService.Play(\"{r.id}\", position).";
                AIToolsCommon.Log("VFX from delivery: " + msg);
                return new SuccessResponse(msg, r);
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is JsonException)
            {
                return new ErrorResponse(e.Message);
            }
        }

        public static Result Build(string folder, Options o)
        {
            folder = (folder ?? "").Replace('\\', '/').TrimEnd('/');
            string manifestPath = folder + "/manifest.json";
            if (!AssetDatabase.IsValidFolder(folder) || !File.Exists(manifestPath))
                throw new ArgumentException($"No delivery at '{folder}' (expected an Assets/ folder holding manifest.json; import it with ai_import_asset).");
            var m = JObject.Parse(File.ReadAllText(manifestPath));
            string kind = m.Value<string>("kind"), game = m.Value<string>("game"), deliveryId = m.Value<string>("id");
            if (kind != "vfx-flipbook" && kind != "vfx-mesh")
                throw new ArgumentException($"'{deliveryId}' is a '{kind}' delivery; effects come from vfx-flipbook or vfx-mesh.");
            var files = m["files"] as JObject ?? throw new ArgumentException("manifest has no 'files'.");
            var fb = m["flipbook"] as JObject;
            if (kind == "vfx-flipbook")
            {
                if (fb == null) throw new ArgumentException("a vfx-flipbook manifest has no 'flipbook' block.");
                if (fb.Value<bool?>("linear") == true)
                    throw new ArgumentException($"'{deliveryId}' is a data sheet (linear: true, e.g. screen normals): it needs a game-specific shader, not a flipbook effect.");
            }

            var r = new Result { kind = kind };
            r.id = string.IsNullOrEmpty(o.id) ? DefaultId(deliveryId) : o.id;
            string outFolder = string.IsNullOrEmpty(o.outFolder) ? $"Assets/_Project/Games/{game}/VFX/{r.id}" : o.outFolder.TrimEnd('/');
            r.library = string.IsNullOrEmpty(o.library) ? $"Assets/_Project/Games/{game}/VFX/{game}VfxLibrary.asset" : o.library;
            string name = "VFX_" + Pascal(r.id);
            r.prefab = $"{outFolder}/{name}.prefab";
            r.definition = $"{outFolder}/{r.id}.asset";

            // Load everything the build needs before writing anything, so a bad delivery leaves no half-made assets.
            Texture2D sheet = null;
            GameObject model = null;
            if (kind == "vfx-flipbook")
            {
                sheet = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{files.Value<string>("sheet")}")
                        ?? throw new IOException($"No sheet texture in {folder} (re-import the delivery).");
            }
            else
            {
                string main = files.Value<string>("glb") ?? files.Value<string>("fbx");
                model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{main}")
                        ?? throw new IOException($"No model in {folder} (re-import the delivery).");
            }

            AIToolsCommon.EnsureFolder(outFolder);
            var root = new GameObject(name);
            try
            {
                if (sheet != null)
                {
                    r.material = $"{outFolder}/{name}.mat";
                    var mat = SheetMaterial(r.material, sheet, o);
                    BuildSheet(root, mat, fb, o, r);
                }
                else
                {
                    var meshes = model.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
                    if (meshes.Length == 0) throw new IOException($"'{deliveryId}' has no mesh.");
                    r.pieces = meshes.Length;
                    if (meshes.Length == 1) BuildMeshParticles(root, meshes[0], o, r);
                    else BuildPieces(root, model, o, r);
                }
                PrefabUtility.SaveAsPrefabAsset(root, r.prefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var def = AssetDatabase.LoadAssetAtPath<VfxDefinition>(r.definition);
            bool newDef = def == null;
            if (newDef) def = ScriptableObject.CreateInstance<VfxDefinition>();
            def.id = r.id;
            def.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab);
            def.lifetime = 0f;   // wait for the particles / pieces: exact for every kind
            def.prewarmCount = 2;
            def.maxPoolSize = 16;
            def.scale = 1f;
            if (newDef) AssetDatabase.CreateAsset(def, r.definition);
            else EditorUtility.SetDirty(def);
            Register(r.library, def);
            AssetDatabase.SaveAssets();
            return r;
        }

        /// <summary>"vfx-fire-toon" → "fire_toon".</summary>
        public static string DefaultId(string deliveryId)
        {
            string id = deliveryId ?? "effect";
            if (id.StartsWith("vfx-")) id = id.Substring(4);
            return id.Replace('-', '_');
        }

        private static string Pascal(string id) =>
            string.Concat(id.Split('_', '-', ' ').Where(s => s.Length > 0).Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));

        private static Material SheetMaterial(string path, Texture2D sheet, Options o)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(Shader.Find(FlipbookMaterial.ShaderName) ?? throw new IOException("Shader Project/Flipbook is missing."));
            else mat.shader = Shader.Find(FlipbookMaterial.ShaderName);
            mat.SetTexture("_BaseMap", sheet);
            mat.SetFloat("_Intensity", o.intensity);
            FlipbookMaterial.SetBlend(mat, o.additive);
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (isNew) AssetDatabase.CreateAsset(mat, path);   // every setting first (CLAUDE.md lesson)
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private static ParticleSystem NewSystem(GameObject go, float lifetime, int count, Options o)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // AddComponent starts playing
            var main = ps.main;
            main.duration = Mathf.Max(0.05f, lifetime);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startColor = o.color;
            main.maxParticles = Mathf.Max(1, count);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // VfxDefinition.scale and parent scale apply
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void BuildSheet(GameObject root, Material mat, JObject fb, Options o, Result r)
        {
            int cols = Math.Max(1, fb.Value<int?>("cols") ?? 1), rows = Math.Max(1, fb.Value<int?>("rows") ?? 1);
            int frames = Mathf.Clamp(fb.Value<int?>("frames") ?? cols * rows, 1, cols * rows);
            int px = fb.Value<int?>("frame_px") ?? 1, ph = fb.Value<int?>("frame_h") ?? px;
            float duration = fb.Value<float?>("duration_s") ?? 0.5f;
            bool loop = fb.Value<bool?>("loop") ?? false;
            int count = o.count > 0 ? o.count : 1;
            float lifetime = loop ? duration * 4f : duration;
            r.frames = frames;
            r.duration = lifetime;

            var ps = NewSystem(root, lifetime, count, o);
            var main = ps.main;
            if (count > 1)
            {
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * o.size, 1.5f * o.size);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.15f * o.size;
            }
            float aspect = ph > 0 ? (float)px / ph : 1f;
            if (Mathf.Abs(aspect - 1f) > 1e-3f)
            {
                main.startSize3D = true;
                main.startSizeX = o.size * aspect;
                main.startSizeY = o.size;
                main.startSizeZ = 1f;
            }
            else
            {
                main.startSize = o.size;
            }

            if (frames > 1)
            {
                var tsa = ps.textureSheetAnimation;
                tsa.enabled = true;
                tsa.mode = ParticleSystemAnimationMode.Grid;
                tsa.numTilesX = cols;
                tsa.numTilesY = rows;
                tsa.animation = ParticleSystemAnimationType.WholeSheet;
                tsa.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
                // frame = floor(value × cells): ending at frames/cells plays exactly the delivered frames, each for the same
                // time, and never reaches the empty trailing cells.
                tsa.frameOverTime = new ParticleSystem.MinMaxCurve((float)frames / (cols * rows), AnimationCurve.Linear(0f, 0f, 1f, 1f));
                tsa.startFrame = 0f;
                tsa.cycleCount = loop ? 4 : 1;
                // A sheet whose last frame still holds something (the fire's smoke wisp) would pop off when the particle
                // dies: fade the last 12 % of the life (an empty last frame is unaffected).
                var fade = ps.colorOverLifetime;
                fade.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
                fade.color = g;
            }
            else
            {
                // A still (lettering, crack, cloud, pointer) has no animation of its own: pop in, hold, fade out.
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0f), new Keyframe(0.15f, 1.15f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f)));
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            }
            string facing = o.facing ?? fb.Value<string>("facing") ?? "camera";
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = facing == "ground" ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = mat;
        }

        private static void BuildMeshParticles(GameObject root, MeshFilter mesh, Options o, Result r)
        {
            int count = o.count > 0 ? o.count : 12;
            const float lifetime = 1.2f;
            r.duration = lifetime;
            var ps = NewSystem(root, lifetime, count, o);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * o.size, 4f * o.size);
            main.startSize = o.size;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier = 1f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.05f * o.size;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // hemisphere opens up
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-2f * Mathf.PI, 2f * Mathf.PI);
            spin.y = new ParticleSystem.MinMaxCurve(-2f * Mathf.PI, 2f * Mathf.PI);
            spin.z = new ParticleSystem.MinMaxCurve(-2f * Mathf.PI, 2f * Mathf.PI);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f)));
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh.sharedMesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            var source = mesh.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = source != null ? source.sharedMaterial : null;
        }

        private static void BuildPieces(GameObject root, GameObject model, Options o, Result r)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localScale = Vector3.one * o.size;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            var burst = root.AddComponent<MeshBurst>();
            burst.Speed = 2.5f * o.size;
            r.duration = burst.Lifetime;
        }

        /// <summary>Adds (or replaces, by id) <paramref name="def"/> in the library; a new library starts with the Core effects.</summary>
        private static void Register(string libraryPath, VfxDefinition def)
        {
            var lib = AssetDatabase.LoadAssetAtPath<VfxLibrary>(libraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<VfxLibrary>();
                var core = AssetDatabase.LoadAssetAtPath<VfxLibrary>(VfxAuthoringTool.LibraryPath);
                if (core != null && libraryPath != VfxAuthoringTool.LibraryPath)
                    lib.effects.AddRange(core.effects.Where(e => e != null));
                AIToolsCommon.EnsureFolder(Path.GetDirectoryName(libraryPath).Replace('\\', '/'));
                lib.effects.RemoveAll(e => e == null || e.id == def.id);
                lib.effects.Add(def);
                AssetDatabase.CreateAsset(lib, libraryPath);
                return;
            }
            lib.effects.RemoveAll(e => e == null || (e.id == def.id && e != def));
            if (!lib.effects.Contains(def)) lib.effects.Add(def);
            EditorUtility.SetDirty(lib);
        }
    }
}
