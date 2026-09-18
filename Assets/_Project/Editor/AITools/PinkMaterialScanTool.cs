using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Finds materials that render pink under URP (missing/broken shaders or Built-in pipeline shaders).
    /// </summary>
    [McpForUnityTool("ai_scan_pink_materials", Description =
        "Find materials that render pink in URP: missing or erroring shaders, and Built-in pipeline shaders " +
        "(Standard, Legacy Shaders/..., passes tagged ForwardBase etc.). scope = 'scene' (materials on renderers in loaded scenes), " +
        "'assets' (every .mat under Assets, excluding Packages) or 'all' (default). fix=true runs Unity's " +
        "'Convert Selected Built-in Materials to URP' on the convertible ones.")]
    public static class PinkMaterialScanTool
    {
        public class Parameters
        {
            [ToolParameter("'scene', 'assets' or 'all' (default)", Required = false)]
            public string scope { get; set; }

            [ToolParameter("Convert Built-in Standard/Legacy materials to URP with Unity's converter (default false)", Required = false)]
            public bool? fix { get; set; }
        }

        // Color passes URP actually renders. Built-in shaders also carry ShadowCaster/Meta/DepthOnly
        // passes, so those must NOT count as "compatible" or Standard would slip through.
        private static readonly HashSet<string> UrpColorLightModes = new HashSet<string>
        {
            "", "UniversalForward", "UniversalForwardOnly", "UniversalGBuffer", "SRPDefaultUnlit", "Universal2D",
        };

        public class Finding
        {
            public string material;
            public string shader;
            public string reason;
            public bool convertible;
            public string[] usedBy;
        }

        public static object HandleCommand(JObject p)
        {
            string scope = (AIToolsCommon.GetString(p, "scope", "all") ?? "all").ToLowerInvariant();
            bool fix = AIToolsCommon.GetBool(p, "fix", false);

            var candidates = new Dictionary<Material, List<string>>();
            if (scope == "scene" || scope == "all")
            {
                foreach (var go in AIToolsCommon.AllSceneObjects())
                {
                    foreach (var r in go.GetComponents<Renderer>())
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) continue;
                            if (!candidates.TryGetValue(m, out var users)) candidates[m] = users = new List<string>();
                            users.Add(AIToolsCommon.GetHierarchyPath(go));
                        }
                }
            }
            if (scope == "assets" || scope == "all")
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (m != null && !candidates.ContainsKey(m)) candidates[m] = new List<string>();
                }
            }

            var findings = new List<Finding>();
            foreach (var kv in candidates)
            {
                if (IsPink(kv.Key, out string reason, out bool convertible))
                {
                    string path = AssetDatabase.GetAssetPath(kv.Key);
                    findings.Add(new Finding
                    {
                        material = string.IsNullOrEmpty(path) ? kv.Key.name + " (scene-only instance)" : path,
                        shader = kv.Key.shader != null ? kv.Key.shader.name : "<null>",
                        reason = reason,
                        convertible = convertible,
                        usedBy = kv.Value.Distinct().Take(10).ToArray(),
                    });
                }
            }

            int converted = 0;
            var conversionLog = new List<string>();
            if (fix)
            {
                var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(GraphicsSettings.currentRenderPipelineAssetType);
                if (upgraders == null || upgraders.Count == 0)
                {
                    conversionLog.Add("No material upgraders found for the current render pipeline.");
                }
                foreach (var f in findings.Where(f => f.convertible && f.material.StartsWith("Assets/")))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(f.material);
                    if (m == null || upgraders == null) continue;
                    string message = null;
                    bool ok = MaterialUpgrader.Upgrade(m, upgraders, MaterialUpgrader.UpgradeFlags.None, ref message);
                    if (ok) converted++;
                    conversionLog.Add($"{f.material}: {(ok ? "converted" : "skipped")} {message}".Trim());
                }
                if (converted > 0) AssetDatabase.SaveAssets();
            }

            string msg = $"Pink material scan ({scope}): {findings.Count} problem material(s) out of {candidates.Count} checked" +
                         (fix ? $", converter run on {converted}." : ".");
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { checked_count = candidates.Count, problems = findings.Count, converted, conversion_log = conversionLog, findings });
        }

        public static bool IsPink(Material m, out string reason, out bool convertible)
        {
            convertible = false;
            var shader = m.shader;
            if (shader == null) { reason = "shader missing"; return true; }
            if (shader.name == "Hidden/InternalErrorShader") { reason = "shader missing (InternalErrorShader)"; return true; }
            if (ShaderUtil.ShaderHasError(shader)) { reason = "shader has compile errors"; return true; }
            if (!shader.isSupported) { reason = "shader not supported on this platform/pipeline"; return true; }

            string n = shader.name;
            convertible = n == "Standard" || n == "Standard (Specular setup)" || n.StartsWith("Legacy Shaders/") ||
                          n.StartsWith("Mobile/") || n.StartsWith("Nature/") || n.StartsWith("Particles/");

            var lightMode = new ShaderTagId("LightMode");
            bool anyUrpPass = false;
            for (int i = 0; i < shader.passCount; i++)
            {
                string mode = shader.FindPassTagValue(i, lightMode).name;
                if (UrpColorLightModes.Contains(mode)) { anyUrpPass = true; break; }
            }
            if (!anyUrpPass)
            {
                reason = $"no URP-compatible pass (Built-in pipeline shader '{n}')";
                return true;
            }
            reason = null;
            return false;
        }

        [MenuItem("AI Tools/Audit/Scan For Pink (URP-incompatible) Materials")]
        public static void ScanMenu()
        {
            var result = HandleCommand(new JObject { ["scope"] = "all" });
            Debug.Log(AIToolsCommon.LogPrefix + Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
