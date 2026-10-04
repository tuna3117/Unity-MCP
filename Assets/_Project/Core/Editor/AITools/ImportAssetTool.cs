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
            [ToolParameter("Instead of importing: re-apply the flipbook import settings to every delivered sheet under this Assets/ folder",
                Required = false)]
            public string reapply { get; set; }
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
            string reapply = AIToolsCommon.GetString(p, "reapply");
            if (!string.IsNullOrEmpty(reapply))
            {
                if (!AssetDatabase.IsValidFolder(reapply)) return new ErrorResponse($"No folder '{reapply}'.");
                var sheets = ReapplySheetSettings(reapply);
                AIToolsCommon.Log($"Import asset: re-applied flipbook settings to {sheets.Count} sheets under {reapply}");
                return new SuccessResponse($"Re-applied flipbook settings to {sheets.Count} sheets.", new { sheets });
            }
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
            string fbx = files.Value<string>("fbx");
            if (!flipbook && !string.IsNullOrEmpty(fbx)) ConfigureModel($"{r.folder}/{fbx}", m, r);
            Check(r, m, flipbook);
            return r;
        }

        /// <summary>
        /// FBX model import settings from the manifest (backlog U1): rig type from <c>import.rig</c> (generic by default when the
        /// delivery has bones — Generic keeps bone scale curves for squash/stretch; humanoid builds an avatar from the model) and
        /// one clip per <c>clips_meta</c> entry, named as the manifest names it, with its loop flag. glb keeps glTFast's defaults.
        /// </summary>
        private static void ConfigureModel(string path, JObject m, Result r)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) return;
            var stats = m["stats"] as JObject;
            int bones = stats?.Value<int?>("bones") ?? 0;
            string rig = (m["import"] as JObject)?.Value<string>("rig") ?? (bones > 0 ? "generic" : "none");
            switch (rig)
            {
                case "generic": imp.animationType = ModelImporterAnimationType.Generic; break;
                case "humanoid":
                    imp.animationType = ModelImporterAnimationType.Human;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    break;
                case "none": imp.animationType = ModelImporterAnimationType.None; break;
                default: throw new ArgumentException($"import.rig must be generic, humanoid or none (got '{rig}').");
            }
            var meta = m["clips_meta"] as JArray;
            imp.importAnimation = rig != "none" && (meta?.Count > 0 || (stats?["clips"] as JArray)?.Count > 0);
            if (imp.importAnimation && meta != null && meta.Count > 0)
            {
                var takes = imp.defaultClipAnimations;
                var clips = new List<ModelImporterClipAnimation>();
                foreach (var entry in meta.OfType<JObject>())
                {
                    string name = entry.Value<string>("name");
                    // Blender names FBX takes "<armature>|<action>"; the manifest names the action.
                    var take = takes.FirstOrDefault(t => t.takeName == name || t.name == name || t.takeName.EndsWith("|" + name) || t.name.EndsWith("|" + name));
                    if (take == null)
                    {
                        r.differences.Add($"clip '{name}' of clips_meta is not in {Path.GetFileName(path)}");
                        continue;
                    }
                    take.name = name;
                    take.loopTime = entry.Value<bool?>("loop") ?? false;
                    take.loopPose = false;
                    clips.Add(take);
                }
                imp.clipAnimations = clips.ToArray();
            }
            imp.SaveAndReimport();
            if (rig == "humanoid")
            {
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                    r.differences.Add($"{Path.GetFileName(path)}: no valid humanoid avatar (bone names or T-pose)");
            }
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
                r.models.Add(Measure(path, name, m["stats"] as JObject));
            }
            var main = r.models.First(s => $"{r.folder}/{s.file}" == r.main);
            r.unityTris = main.tris;
            r.unityBones = main.bones;
            r.unityClips = main.clips;
            Compare(r, m["stats"] as JObject);
        }

        /// <summary>
        /// Re-applies <see cref="ConfigureSheet"/> to every vfx-flipbook delivery (its manifest.json) under
        /// <paramref name="folder"/>: deliveries imported before a settings change pick it up without a new hand-off.
        /// </summary>
        public static List<string> ReapplySheetSettings(string folder)
        {
            var done = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("manifest t:TextAsset", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path) != "manifest.json") continue;
                JObject m;
                try { m = JObject.Parse(File.ReadAllText(path)); }
                catch (JsonException) { continue; }
                if (m.Value<string>("kind") != "vfx-flipbook" || !(m["files"] is JObject files)) continue;
                string sheet = $"{Path.GetDirectoryName(path).Replace('\\', '/')}/{files.Value<string>("sheet")}";
                if (!(AssetImporter.GetAtPath(sheet) is TextureImporter)) continue;
                ConfigureSheet(sheet, (m["flipbook"] as JObject)?.Value<bool?>("linear") ?? false);
                done.Add(sheet);
            }
            done.Sort(StringComparer.Ordinal);
            return done;
        }

        /// <summary>Phone platforms that get a compressed override (the editor keeps the exact sheet).</summary>
        public static readonly string[] PhonePlatforms = { "iPhone", "Android" };

        /// <summary>
        /// Particle flipbook import: straight alpha, clamped, mipmapped; sRGB unless the sheet holds data. Uncompressed in
        /// the editor, ASTC on phones: 6×6 for colour (≈3.6 bpp; a 1024² sheet drops from 5.3 MB to ~0.6 MB with mips),
        /// 4×4 for data sheets such as the shock ring's screen normals, whose gradients band at coarser blocks.
        /// </summary>
        private static void ConfigureSheet(string path, bool linear)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            // A game may turn a delivered still into a UI sprite (Pota's menu clouds and tutorial hand): re-imports and
            // reapply keep the consumer's type and settings and only add the phone compression (2026-10-04: resetting them
            // dropped the sprites and the menu clouds stopped).
            if (imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.sRGBTexture = !linear;
                imp.npotScale = TextureImporterNPOTScale.None;
            }
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            foreach (string platform in PhonePlatforms)
            {
                var s = imp.GetPlatformTextureSettings(platform);
                s.overridden = true;
                s.maxTextureSize = imp.maxTextureSize;
                s.format = linear ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
                s.compressionQuality = (int)TextureCompressionQuality.Best;
                imp.SetPlatformTextureSettings(s);
            }
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

        private static ModelStats Measure(string path, string file, JObject stats)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) throw new IOException("Unity imported no model from " + path);
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            return new ModelStats
            {
                file = file,
                tris = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                    .Concat(skinned.Select(s => s.sharedMesh)).Where(mesh => mesh != null).Sum(Triangles),
                bones = skinned.Length > 0 ? skinned.SelectMany(s => s.bones).Where(b => b != null).Distinct().Count()
                    : SkeletonBones(root, stats),
                clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__")).Select(c => c.name).OrderBy(n => n).ToList(),
            };
        }

        /// <summary>
        /// Bones of a delivery without a skinned mesh (an animation-only skeleton, backlog U4): the transforms named in the
        /// manifest's <c>stats.bone_names</c>, else everything below the armature node(s) (the import root's direct children).
        /// A delivery the manifest says has no bones counts 0.
        /// </summary>
        private static int SkeletonBones(GameObject root, JObject stats)
        {
            if ((stats?.Value<int?>("bones") ?? 0) == 0) return 0;
            var below = root.GetComponentsInChildren<Transform>(true).Where(t => t != root.transform).ToList();
            var names = (stats["bone_names"] as JArray)?.Select(t => t.ToString()).ToList();
            if (names != null && names.Count > 0) return below.Count(t => names.Contains(t.name));
            return below.Count(t => t.parent != root.transform);
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
