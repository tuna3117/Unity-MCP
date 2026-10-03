using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Imports a Blender workshop hand-off (manifest.json next to a glb/fbx; ~/projects/Blender MCP/docs/studyo.md)
    /// into Games/&lt;Game&gt;/Art/&lt;Kind&gt;/&lt;id&gt;/ and compares what Unity imported with the manifest's numbers.
    /// Re-importing an id overwrites the files in place, so GUIDs and scene references survive.
    /// </summary>
    [McpForUnityTool("ai_import_asset", Description =
        "Import a Blender workshop hand-off. manifest: absolute path to manifest.json (the glb/fbx sits next to it). " +
        "Copies into Assets/_Project/Games/<game>/Art/<Kind>/<id>/ (dest overrides, must be under Assets/); re-import keeps GUIDs " +
        "and removes files the previous delivery listed but this one does not (removed). " +
        "Then compares tris/bones/clip count of every model file with the manifest (matches=false lists the differences).")]
    public static class ImportAssetTool
    {
        public class Parameters
        {
            [ToolParameter("Absolute path to the hand-off manifest.json")] public string manifest { get; set; }
            [ToolParameter("Target folder; default Assets/_Project/Games/<game>/Art/<Kind>/<id>", Required = false)]
            public string dest { get; set; }
        }

        public static readonly Dictionary<string, string> KindFolders = new Dictionary<string, string>
        {
            { "prop", "Props" }, { "character", "Characters" }, { "animation", "Animations" },
            { "vfx-mesh", "VFX" }, { "vfx-flipbook", "VFX" },
        };

        /// <summary>What Unity imported from one model file of the delivery (glb and fbx are measured separately).</summary>
        public class ModelStats
        {
            public string file;
            public int tris, bones;
            public List<string> clips = new List<string>();
        }

        public class Result
        {
            public string id, folder, main;
            public List<string> copied = new List<string>(), removed = new List<string>();
            public List<ModelStats> models = new List<ModelStats>();
            public int manifestTris, unityTris, manifestBones, unityBones;
            public List<string> manifestClips = new List<string>(), unityClips = new List<string>();
            public List<string> differences = new List<string>();
            public int[] sheetSize, manifestSheetSize;
            public bool matches => differences.Count == 0;
        }

        public static object HandleCommand(JObject p)
        {
            string manifest = AIToolsCommon.GetString(p, "manifest");
            if (string.IsNullOrEmpty(manifest)) return new ErrorResponse("'manifest' is required.");
            try
            {
                var r = Import(manifest, AIToolsCommon.GetString(p, "dest"));
                string msg = $"{r.id} -> {r.folder}: {r.unityTris} tris, {r.unityBones} bones, {r.unityClips.Count} clips, " +
                             (r.matches ? "matches the manifest" : "DIFFERS: " + string.Join("; ", r.differences));
                AIToolsCommon.Log("Import asset: " + msg);
                if (r.removed.Count > 0) msg += $"; removed stale {string.Join(", ", r.removed)}";
                return new SuccessResponse(msg, new
                {
                    r.id, r.folder, r.main, r.copied, r.removed, r.models, r.matches, r.differences, r.manifestTris, r.unityTris,
                    r.manifestBones, r.unityBones, r.manifestClips, r.unityClips, r.sheetSize, r.manifestSheetSize,
                });
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is JsonException)
            {
                return new ErrorResponse(e.Message);
            }
        }

        public static Result Import(string manifestPath, string dest = null)
        {
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("No manifest at " + manifestPath);
            var m = JObject.Parse(File.ReadAllText(manifestPath));
            string id = Required(m, "id"), game = Required(m, "game"), kind = Required(m, "kind");
            if (!KindFolders.TryGetValue(kind, out string kindFolder))
                throw new ArgumentException($"Unknown kind '{kind}' (known: {string.Join(", ", KindFolders.Keys)}).");
            var files = m["files"] as JObject ?? throw new ArgumentException("manifest has no 'files'.");
            bool flipbook = kind == "vfx-flipbook";
            string mainName = MainName(files, flipbook);
            JObject fb = null;
            if (flipbook) fb = m["flipbook"] as JObject ?? throw new ArgumentException("a vfx-flipbook manifest has no 'flipbook' block.");

            var r = new Result { id = id, folder = TargetFolder(dest, game, kindFolder, id) };
            AIToolsCommon.EnsureFolder(r.folder);
            var previous = DeliveredFiles($"{r.folder}/manifest.json");
            string sourceDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            var delivered = DeliveredFiles(files, flipbook);
            foreach (string name in delivered.Append("manifest.json"))
            {
                string source = name == "manifest.json" ? manifestPath : Path.Combine(sourceDir, name);
                if (!File.Exists(source)) throw new FileNotFoundException("Hand-off file missing: " + source);
                string target = $"{r.folder}/{name}";
                File.Copy(source, target, true); // in place: the existing .meta (GUID) stays
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                r.copied.Add(target);
            }
            foreach (string stale in previous.Except(delivered))
            {
                string path = $"{r.folder}/{stale}";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null && AssetDatabase.DeleteAsset(path)) r.removed.Add(path);
            }
            r.main = $"{r.folder}/{mainName}";
            if (flipbook) ConfigureSheet(r.main, fb.Value<bool?>("linear") ?? false);
            Check(r, m, flipbook);
            return r;
        }

        /// <summary>
        /// The asset folder a delivery goes to: <paramref name="dest"/> when given (must stay under Assets/), otherwise
        /// Assets/_Project/Games/&lt;game&gt;/Art/&lt;Kind&gt;/&lt;id&gt;.
        /// </summary>
        public static string TargetFolder(string dest, string game, string kindFolder, string id)
        {
            if (string.IsNullOrWhiteSpace(dest)) return $"Assets/_Project/Games/{game}/Art/{kindFolder}/{id}";
            string folder = dest.Trim().Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/") || folder.Split('/').Any(part => part == ".." || part == "." || part.Length == 0))
                throw new ArgumentException($"dest must be a folder under Assets/ (got '{dest}').");
            return folder;
        }

        /// <summary>The model/sheet files a manifest delivers (not previews, not the manifest itself).</summary>
        private static List<string> DeliveredFiles(JObject files, bool flipbook)
        {
            var names = new List<string> { files.Value<string>("glb"), files.Value<string>("fbx") };
            if (flipbook) names.Add(files.Value<string>("sheet"));
            return names.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        }

        /// <summary>The files an earlier delivery into this folder listed; empty when there was none.</summary>
        private static List<string> DeliveredFiles(string oldManifestPath)
        {
            if (!File.Exists(oldManifestPath)) return new List<string>();
            try
            {
                var old = JObject.Parse(File.ReadAllText(oldManifestPath));
                return old["files"] is JObject files ? DeliveredFiles(files, old.Value<string>("kind") == "vfx-flipbook") : new List<string>();
            }
            catch (JsonException)
            {
                return new List<string>();   // an unreadable old manifest: remove nothing
            }
        }

        /// <summary>
        /// Measures a hand-off already imported into <paramref name="folder"/> against its manifest, copying and reconfiguring
        /// nothing (tests: the special-look assets match what the workshop measured).
        /// </summary>
        public static Result Verify(string manifestPath, string folder)
        {
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("No manifest at " + manifestPath);
            var m = JObject.Parse(File.ReadAllText(manifestPath));
            var files = m["files"] as JObject ?? throw new ArgumentException("manifest has no 'files'.");
            bool flipbook = Required(m, "kind") == "vfx-flipbook";
            var r = new Result { id = Required(m, "id"), folder = folder };
            r.main = $"{folder}/{MainName(files, flipbook)}";
            Check(r, m, flipbook);
            return r;
        }

        private static string MainName(JObject files, bool flipbook)
        {
            string glb = files.Value<string>("glb"), fbx = files.Value<string>("fbx"), sheet = files.Value<string>("sheet");
            if (flipbook && string.IsNullOrEmpty(sheet)) throw new ArgumentException("a vfx-flipbook manifest lists no 'sheet'.");
            string main = flipbook ? sheet : !string.IsNullOrEmpty(glb) ? glb : fbx;
            if (string.IsNullOrEmpty(main)) throw new ArgumentException("manifest lists neither a glb nor an fbx.");
            return main;
        }

        private static void Check(Result r, JObject m, bool flipbook)
        {
            if (flipbook)
            {
                MeasureSheet(r, m["flipbook"] as JObject ?? throw new ArgumentException("a vfx-flipbook manifest has no 'flipbook' block."));
                return;
            }
            var files = (JObject)m["files"];
            foreach (string name in DeliveredFiles(files, false))
            {
                string path = $"{r.folder}/{name}";
                if (path != r.main && AssetDatabase.LoadMainAssetAtPath(path) == null) continue;   // Verify on a folder that dropped the fbx
                r.models.Add(Measure(path, name));
            }
            var main = r.models.First(s => $"{r.folder}/{s.file}" == r.main);
            r.unityTris = main.tris;
            r.unityBones = main.bones;
            r.unityClips = main.clips;
            Compare(r, m["stats"] as JObject);
        }

        /// <summary>Particle flipbook import: straight alpha, clamped, mipmapped, uncompressed; sRGB unless the sheet holds data.</summary>
        private static void ConfigureSheet(string path, bool linear)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.sRGBTexture = !linear;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        private static void MeasureSheet(Result r, JObject fb)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(r.main) ?? throw new IOException("Unity imported no texture from " + r.main);
            int px = fb.Value<int?>("frame_px") ?? 0, ph = fb.Value<int?>("frame_h") ?? px;   // frame_h: wide stills (512×256)
            r.sheetSize = new[] { tex.width, tex.height };
            r.manifestSheetSize = new[] { (fb.Value<int?>("cols") ?? 0) * px, (fb.Value<int?>("rows") ?? 0) * ph };
            if (r.sheetSize[0] != r.manifestSheetSize[0] || r.sheetSize[1] != r.manifestSheetSize[1])
                r.differences.Add($"sheet manifest {r.manifestSheetSize[0]}x{r.manifestSheetSize[1]} vs Unity {tex.width}x{tex.height}");
        }

        private static ModelStats Measure(string path, string file)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) throw new IOException("Unity imported no model from " + path);
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return new ModelStats
            {
                file = file,
                tris = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                    .Concat(skinned.Select(s => s.sharedMesh)).Where(mesh => mesh != null).Sum(Triangles),
                bones = skinned.SelectMany(s => s.bones).Where(b => b != null).Distinct().Count(),
                clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name).OrderBy(n => n).ToList(),
            };
        }

        private static int Triangles(Mesh mesh)
        {
            int total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) total += (int)mesh.GetIndexCount(i) / 3;
            return total;
        }

        private static void Compare(Result r, JObject stats)
        {
            if (stats == null) { r.differences.Add("manifest has no 'stats'"); return; }
            r.manifestTris = stats.Value<int?>("tris") ?? -1;
            r.manifestBones = stats.Value<int?>("bones") ?? 0;
            r.manifestClips = (stats["clips"] as JArray)?.Select(t => t.ToString()).OrderBy(n => n).ToList()
                              ?? new List<string>();
            foreach (var s in r.models)
            {
                string who = r.models.Count > 1 ? s.file + ": " : "";
                if (r.manifestTris != s.tris) r.differences.Add($"{who}tris manifest {r.manifestTris} vs Unity {s.tris}");
                if (r.manifestBones != s.bones) r.differences.Add($"{who}bones manifest {r.manifestBones} vs Unity {s.bones}");
                if (r.manifestClips.Count != s.clips.Count)
                    r.differences.Add($"{who}clips manifest {r.manifestClips.Count} vs Unity {s.clips.Count}");
            }
        }

        private static string Required(JObject m, string key)
        {
            string value = m.Value<string>(key);
            if (string.IsNullOrEmpty(value)) throw new ArgumentException($"manifest has no '{key}'.");
            return value;
        }
    }
}
