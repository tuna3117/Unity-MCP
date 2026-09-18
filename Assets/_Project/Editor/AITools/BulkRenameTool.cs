using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Renames many scene objects with a pattern. Pattern tokens:
    ///   {name}  original name, {i} running index, {i:00} zero-padded index.
    /// </summary>
    [McpForUnityTool("ai_bulk_rename", Description =
        "Rename many scene objects with a pattern. Tokens: {name} = original name, {i} = index, {i:00} = zero-padded index. " +
        "Select targets with targets[] (names/paths/ids), parent (+include_children), name_contains, or use_selection.")]
    public static class BulkRenameTool
    {
        public class Parameters
        {
            [ToolParameter("Name pattern, e.g. 'Enemy_{i:00}' or '{name}_Old'")]
            public string pattern { get; set; }

            [ToolParameter("Explicit targets: names, hierarchy paths or instance ids", Required = false)]
            public string[] targets { get; set; }

            [ToolParameter("Parent object whose children get renamed", Required = false)]
            public string parent { get; set; }

            [ToolParameter("With parent: also rename nested descendants", Required = false)]
            public bool? include_children { get; set; }

            [ToolParameter("Substring filter over every object in the loaded scenes", Required = false)]
            public string name_contains { get; set; }

            [ToolParameter("Use the current editor selection", Required = false)]
            public bool? use_selection { get; set; }

            [ToolParameter("First index value (default 1)", Required = false)]
            public int? start_index { get; set; }

            [ToolParameter("Save the active scene afterwards (default false)", Required = false)]
            public bool? save { get; set; }
        }

        private static readonly Regex IndexToken = new Regex(@"\{i(?::([0-9]+))?\}", RegexOptions.Compiled);

        public static object HandleCommand(JObject p)
        {
            string pattern = AIToolsCommon.GetString(p, "pattern");
            if (string.IsNullOrEmpty(pattern)) return new ErrorResponse("'pattern' is required.");

            var targets = AIToolsCommon.ResolveTargets(p);
            if (targets.Count == 0) return new ErrorResponse("No targets matched (use targets[], parent, name_contains or use_selection).");

            int start = AIToolsCommon.GetInt(p, "start_index", 1);
            var renamed = Rename(targets, pattern, start);
            AIToolsCommon.MarkDirty(AIToolsCommon.GetBool(p, "save", false));

            string msg = $"Renamed {renamed.Count} object(s) with pattern '{pattern}'.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { count = renamed.Count, renamed });
        }

        public static List<string> Rename(List<GameObject> targets, string pattern, int startIndex)
        {
            var ordered = targets.OrderBy(go => go, Comparer<GameObject>.Create(CompareHierarchyOrder)).ToList();
            var results = new List<string>();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("AI Bulk Rename");
            int index = startIndex;
            foreach (var go in ordered)
            {
                string newName = Expand(pattern, go.name, index++);
                Undo.RecordObject(go, "AI Bulk Rename");
                string old = go.name;
                go.name = newName;
                results.Add($"{old} -> {newName}");
            }
            return results;
        }

        public static string Expand(string pattern, string originalName, int index)
        {
            string s = pattern.Replace("{name}", originalName);
            s = IndexToken.Replace(s, m =>
            {
                string pad = m.Groups[1].Success ? m.Groups[1].Value : "";
                return pad.Length > 0 ? index.ToString(new string('0', pad.Length)) : index.ToString();
            });
            return s;
        }

        private static int CompareHierarchyOrder(GameObject a, GameObject b)
        {
            var ka = SiblingChain(a.transform);
            var kb = SiblingChain(b.transform);
            for (int i = 0; i < Mathf.Min(ka.Count, kb.Count); i++)
            {
                int c = ka[i].CompareTo(kb[i]);
                if (c != 0) return c;
            }
            return ka.Count.CompareTo(kb.Count);
        }

        private static List<int> SiblingChain(Transform t)
        {
            var chain = new List<int>();
            while (t != null) { chain.Add(t.GetSiblingIndex()); t = t.parent; }
            chain.Reverse();
            return chain;
        }

        [MenuItem("AI Tools/Selection/Rename Sequential ({name}_{i})")]
        public static void RenameSelectionMenu()
        {
            var targets = Selection.gameObjects.ToList();
            if (targets.Count == 0) { AIToolsCommon.Log("Nothing selected."); return; }
            var renamed = Rename(targets, "{name}_{i}", 1);
            AIToolsCommon.MarkDirty(false);
            AIToolsCommon.Log($"Renamed {renamed.Count} selected object(s).");
        }
    }
}
