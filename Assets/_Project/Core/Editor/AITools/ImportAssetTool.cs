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
        "Copies into Assets/_Project/Games/<game>/Art/<Kind>/<id>/ (dest overrides); re-import keeps GUIDs. " +
        "Then compares tris/bones/clip count with the manifest (matches=false lists the differences).")]
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

        public class Result
        {
            public string id, folder, main;
            public List<string> copied = new List<string>();
            public int manifestTris, unityTris, manifestBones, unityBones;
            public List<string> manifestClips = new List<string>(), unityClips = new List<string>();
            public List<string> differences = new List<string>();
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
                return new SuccessResponse(msg, new
                {
                    r.id, r.folder, r.main, r.copied, r.matches, r.differences, r.manifestTris, r.unityTris,
                    r.manifestBones, r.unityBones, r.manifestClips, r.unityClips,
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
            string glb = files.Value<string>("glb"), fbx = files.Value<string>("fbx");
            string mainName = !string.IsNullOrEmpty(glb) ? glb : fbx;
            if (string.IsNullOrEmpty(mainName)) throw new ArgumentException("manifest lists neither a glb nor an fbx.");

            var r = new Result { id = id, folder = dest ?? $"Assets/_Project/Games/{game}/Art/{kindFolder}/{id}" };
            AIToolsCommon.EnsureFolder(r.folder);
            string sourceDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            foreach (string name in new[] { glb, fbx, "manifest.json" }.Where(n => !string.IsNullOrEmpty(n)))
            {
                string source = name == "manifest.json" ? manifestPath : Path.Combine(sourceDir, name);
                if (!File.Exists(source)) throw new FileNotFoundException("Hand-off file missing: " + source);
                string target = $"{r.folder}/{name}";
                File.Copy(source, target, true); // in place: the existing .meta (GUID) stays
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                r.copied.Add(target);
            }
            r.main = $"{r.folder}/{mainName}";
            Measure(r);
            Compare(r, m["stats"] as JObject);
            return r;
        }

        private static void Measure(Result r)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(r.main);
            if (root == null) throw new IOException("Unity imported no model from " + r.main);
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            r.unityTris = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(skinned.Select(s => s.sharedMesh)).Where(mesh => mesh != null).Sum(Triangles);
            r.unityBones = skinned.SelectMany(s => s.bones).Where(b => b != null).Distinct().Count();
            r.unityClips = AssetDatabase.LoadAllAssetsAtPath(r.main).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name).OrderBy(n => n).ToList();
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
            if (r.manifestTris != r.unityTris) r.differences.Add($"tris manifest {r.manifestTris} vs Unity {r.unityTris}");
            if (r.manifestBones != r.unityBones) r.differences.Add($"bones manifest {r.manifestBones} vs Unity {r.unityBones}");
            if (r.manifestClips.Count != r.unityClips.Count)
                r.differences.Add($"clips manifest {r.manifestClips.Count} vs Unity {r.unityClips.Count}");
        }

        private static string Required(JObject m, string key)
        {
            string value = m.Value<string>(key);
            if (string.IsNullOrEmpty(value)) throw new ArgumentException($"manifest has no '{key}'.");
            return value;
        }
    }
}
