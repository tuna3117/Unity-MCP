using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Moves/renames assets and folders through AssetDatabase so GUIDs (and every scene/prefab reference) survive.
    /// Runs in order: create_folders, moves (stops at the first failure), delete_empty_folders, then re-syncs the
    /// Build Settings scene paths by GUID.
    /// </summary>
    [McpForUnityTool("ai_move_assets", Description =
        "Move or rename assets/folders safely (AssetDatabase, GUIDs kept). moves: [{from, to}] in order, missing parent " +
        "folders are created, stops at the first failure. create_folders: folders to create first. delete_empty_folders: " +
        "folders removed afterwards only if they hold no assets (hidden files like .gitkeep ignored). dry_run=true only validates.")]
    public static class MoveAssetsTool
    {
        public class Parameters
        {
            [ToolParameter("Ordered list of {\"from\": path, \"to\": path}", Required = false)]
            public JArray moves { get; set; }

            [ToolParameter("Folders to create before moving", Required = false)]
            public string[] create_folders { get; set; }

            [ToolParameter("Folders to delete after moving if they are empty", Required = false)]
            public string[] delete_empty_folders { get; set; }

            [ToolParameter("Validate only, change nothing (default false)", Required = false)]
            public bool? dry_run { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            bool dryRun = AIToolsCommon.GetBool(p, "dry_run", false);
            var moves = new List<KeyValuePair<string, string>>();
            if (p?["moves"] is JArray arr)
                foreach (var m in arr.OfType<JObject>())
                    moves.Add(new KeyValuePair<string, string>(m.Value<string>("from"), m.Value<string>("to")));

            var report = new List<string>();
            string error = Run(AIToolsCommon.GetStringList(p, "create_folders"), moves,
                AIToolsCommon.GetStringList(p, "delete_empty_folders"), dryRun, report);
            string summary = $"{(dryRun ? "[dry run] " : "")}{report.Count} step(s)" + (error != null ? $", stopped: {error}" : "");
            AIToolsCommon.Log("Move assets: " + summary);
            var data = new { steps = report, error };
            return error == null ? new SuccessResponse(summary, data) : (object)new ErrorResponse(summary, data);
        }

        public static string Run(List<string> createFolders, List<KeyValuePair<string, string>> moves,
            List<string> deleteIfEmpty, bool dryRun, List<string> report)
        {
            foreach (var folder in createFolders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                if (!dryRun) AIToolsCommon.EnsureFolder(folder);
                report.Add($"folder + {folder}");
            }

            var done = new List<KeyValuePair<string, string>>();
            foreach (var move in moves)
            {
                string from = move.Key, to = move.Value;
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return "each move needs 'from' and 'to'";
                // A dry run moves nothing, so a source that an earlier move would have produced is checked at its origin.
                string source = dryRun ? Origin(from, done) : from;
                done.Add(move);
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(source))) return $"'{from}' is not an asset";
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(to)) || File.Exists(to) || Directory.Exists(to))
                    return $"'{to}' already exists";
                if (dryRun) { report.Add($"move {from} -> {to}"); continue; }

                string parent = Path.GetDirectoryName(to)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(parent)) AIToolsCommon.EnsureFolder(parent);
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err)) return $"{from} -> {to}: {err}";
                report.Add($"move {from} -> {to}");
            }

            // Deepest first, so a parent emptied by deleting its children goes too.
            foreach (var folder in deleteIfEmpty.OrderByDescending(f => f.Length))
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                var leftovers = Directory.GetFileSystemEntries(folder)
                    .Select(Path.GetFileName)
                    .Where(n => !n.StartsWith(".") && !n.EndsWith(".meta"))
                    .ToList();
                if (leftovers.Count > 0) { report.Add($"kept {folder} (not empty: {string.Join(", ", leftovers)})"); continue; }
                if (!dryRun && !AssetDatabase.DeleteAsset(folder)) return $"could not delete '{folder}'";
                report.Add($"folder - {folder}");
            }

            if (!dryRun) SyncBuildScenes(report);
            AssetDatabase.SaveAssets();
            return null;
        }

        private static string Origin(string path, List<KeyValuePair<string, string>> done)
        {
            for (int i = done.Count - 1; i >= 0; i--)
            {
                string to = done[i].Value;
                if (path == to) path = done[i].Key;
                else if (path.StartsWith(to + "/")) path = done[i].Key + path.Substring(to.Length);
            }
            return path;
        }

        /// <summary>Rewrites Build Settings scene paths from their GUIDs (a moved scene keeps its GUID).</summary>
        private static void SyncBuildScenes(List<string> report)
        {
            var scenes = EditorBuildSettings.scenes;
            bool changed = false;
            for (int i = 0; i < scenes.Length; i++)
            {
                string current = AssetDatabase.GUIDToAssetPath(scenes[i].guid);
                if (string.IsNullOrEmpty(current) || current == scenes[i].path) continue;
                report.Add($"build scene {scenes[i].path} -> {current}");
                scenes[i] = new EditorBuildSettingsScene(current, scenes[i].enabled);
                changed = true;
            }
            if (changed) EditorBuildSettings.scenes = scenes;
        }
    }
}
