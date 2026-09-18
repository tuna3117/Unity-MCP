using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Saves a scene object as a prefab asset and connects the scene instance to it.
    /// </summary>
    [McpForUnityTool("ai_create_prefab", Description =
        "Save a scene object (target = name/path/id) as a prefab asset and connect the scene instance to it. " +
        "Default prefab_path is Assets/_Project/Prefabs/<name>.prefab. Set overwrite=true to replace an existing asset.")]
    public static class PrefabFromSelectionTool
    {
        public const string DefaultFolder = "Assets/_Project/Prefabs";

        public class Parameters
        {
            [ToolParameter("Scene object to turn into a prefab (name, hierarchy path or instance id)")]
            public string target { get; set; }

            [ToolParameter("Prefab asset path, e.g. 'Assets/_Project/Prefabs/Enemy.prefab'", Required = false)]
            public string prefab_path { get; set; }

            [ToolParameter("Overwrite an existing prefab at prefab_path (default false)", Required = false)]
            public bool? overwrite { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            string target = AIToolsCommon.GetString(p, "target");
            if (string.IsNullOrEmpty(target)) return new ErrorResponse("'target' is required.");
            var go = AIToolsCommon.FindGameObject(target);
            if (go == null) return new ErrorResponse($"Scene object '{target}' not found.");

            string path = AIToolsCommon.GetString(p, "prefab_path");
            if (string.IsNullOrEmpty(path)) path = $"{DefaultFolder}/{go.name}.prefab";
            if (!path.EndsWith(".prefab")) path += ".prefab";

            bool overwrite = AIToolsCommon.GetBool(p, "overwrite", false);
            if (File.Exists(path) && !overwrite) return new ErrorResponse($"'{path}' already exists. Pass overwrite=true to replace it.");

            var prefab = Create(go, path);
            if (prefab == null) return new ErrorResponse($"Failed to save prefab at '{path}'.");

            string msg = $"Saved prefab '{path}' from '{go.name}' and connected the scene instance.";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { path, guid = AssetDatabase.AssetPathToGUID(path) });
        }

        public static GameObject Create(GameObject go, string path)
        {
            AIToolsCommon.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
            AIToolsCommon.MarkDirty(false);
            return prefab;
        }

        [MenuItem("AI Tools/Selection/Create Prefab From Selection")]
        public static void CreateFromSelectionMenu()
        {
            var go = Selection.activeGameObject;
            if (go == null) { AIToolsCommon.Log("Select a scene object first."); return; }
            string path = $"{DefaultFolder}/{go.name}.prefab";
            var prefab = Create(go, path);
            AIToolsCommon.Log(prefab != null ? $"Saved prefab '{path}'." : $"Failed to save prefab '{path}'.");
        }
    }
}
