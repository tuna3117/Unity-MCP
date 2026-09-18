using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Finds missing scripts and dangling ("Missing") object references in scenes and prefabs.
    /// </summary>
    [McpForUnityTool("ai_scan_missing", Description =
        "Scan for missing (deleted) scripts and dangling object references. scope = 'scene' (loaded scenes, default), " +
        "'prefabs' (all prefabs under Assets/_Project) or 'all'. include_null=true also lists unassigned (None) object fields. " +
        "fix_missing_scripts=true removes the missing script components it finds.")]
    public static class SceneAuditTool
    {
        public class Parameters
        {
            [ToolParameter("'scene' (default), 'prefabs' or 'all'", Required = false)]
            public string scope { get; set; }

            [ToolParameter("Also report unassigned (None) object reference fields (default false, can be noisy)", Required = false)]
            public bool? include_null { get; set; }

            [ToolParameter("Remove components whose script is missing (default false)", Required = false)]
            public bool? fix_missing_scripts { get; set; }

            [ToolParameter("Maximum number of findings to return (default 200)", Required = false)]
            public int? max_results { get; set; }
        }

        public class Finding
        {
            public string kind;      // missing_script | missing_reference | null_reference
            public string where;     // scene path / prefab path
            public string objectPath;
            public string component;
            public string property;
        }

        public static object HandleCommand(JObject p)
        {
            string scope = (AIToolsCommon.GetString(p, "scope", "scene") ?? "scene").ToLowerInvariant();
            bool includeNull = AIToolsCommon.GetBool(p, "include_null", false);
            bool fix = AIToolsCommon.GetBool(p, "fix_missing_scripts", false);
            int max = Mathf.Max(1, AIToolsCommon.GetInt(p, "max_results", 200));

            var findings = new List<Finding>();
            int removed = 0;
            if (scope == "scene" || scope == "all")
            {
                foreach (var go in AIToolsCommon.AllSceneObjects())
                    ScanGameObject(go, go.scene.path, includeNull, fix, findings, ref removed);
                if (fix && removed > 0) AIToolsCommon.MarkDirty(false);
            }
            if (scope == "prefabs" || scope == "all")
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int before = removed;
                        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                            ScanGameObject(tr.gameObject, path, includeNull, fix, findings, ref removed);
                        if (fix && removed > before) PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            int total = findings.Count;
            if (findings.Count > max) findings = findings.GetRange(0, max);
            string msg = $"Scan ({scope}): {total} finding(s)" + (fix ? $", removed {removed} missing script component(s)." : ".");
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { total, returned = findings.Count, removed_missing_scripts = removed, findings });
        }

        private static void ScanGameObject(GameObject go, string where, bool includeNull, bool fix,
            List<Finding> findings, ref int removed)
        {
            string objectPath = AIToolsCommon.GetHierarchyPath(go);
            int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingScripts > 0)
            {
                findings.Add(new Finding { kind = "missing_script", where = where, objectPath = objectPath, component = $"x{missingScripts}" });
                if (fix)
                {
                    Undo.RegisterCompleteObjectUndo(go, "AI Remove Missing Scripts");
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                }
            }

            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.objectReferenceValue != null) continue;
                    bool dangling = prop.objectReferenceEntityIdValue != EntityId.None;
                    if (dangling)
                        findings.Add(new Finding { kind = "missing_reference", where = where, objectPath = objectPath, component = component.GetType().Name, property = prop.propertyPath });
                    else if (includeNull && !prop.propertyPath.StartsWith("m_"))
                        findings.Add(new Finding { kind = "null_reference", where = where, objectPath = objectPath, component = component.GetType().Name, property = prop.propertyPath });
                }
            }
        }

        [MenuItem("AI Tools/Audit/Scan Loaded Scenes For Missing Scripts & References")]
        public static void ScanSceneMenu()
        {
            var result = HandleCommand(new JObject { ["scope"] = "scene" });
            Debug.Log(AIToolsCommon.LogPrefix + JsonUtilityLite(result));
        }

        private static string JsonUtilityLite(object result)
        {
            try { return Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented); }
            catch { return result?.ToString(); }
        }
    }
}
