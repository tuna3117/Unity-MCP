using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Shared helpers for the project AI tool layer (menu items + MCP custom tools).
    /// </summary>
    public static class AIToolsCommon
    {
        public const string LogPrefix = "[AI Tools] ";

        // ---------- Parameter helpers ----------

        public static string GetString(JObject p, string key, string fallback = null)
        {
            var token = p?[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return token.ToString();
        }

        public static int GetInt(JObject p, string key, int fallback)
        {
            var token = p?[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return int.TryParse(token.ToString(), out int v) ? v : fallback;
        }

        public static float GetFloat(JObject p, string key, float fallback)
        {
            var token = p?[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return float.TryParse(token.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
        }

        public static bool GetBool(JObject p, string key, bool fallback)
        {
            var token = p?[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            return bool.TryParse(token.ToString(), out bool v) ? v : fallback;
        }

        public static Vector3? GetVector3(JObject p, string key)
        {
            var token = p?[key];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token is JArray arr && arr.Count >= 3)
                return new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
            if (token is JObject o)
                return new Vector3(o.Value<float?>("x") ?? 0, o.Value<float?>("y") ?? 0, o.Value<float?>("z") ?? 0);
            return null;
        }

        public static List<string> GetStringList(JObject p, string key)
        {
            var token = p?[key];
            var result = new List<string>();
            if (token == null || token.Type == JTokenType.Null) return result;
            if (token is JArray arr) result.AddRange(arr.Select(t => t.ToString()));
            else result.Add(token.ToString());
            return result;
        }

        // ---------- Target resolution ----------

        /// <summary>
        /// Resolves scene GameObjects from the common target parameters:
        ///   targets: [name | hierarchy path | instance id, ...]
        ///   parent: name/path — all direct children (or all descendants if include_children)
        ///   name_contains: substring match over the active scenes
        ///   use_selection: true — current editor selection
        /// </summary>
        public static List<GameObject> ResolveTargets(JObject p, bool includeChildrenDefault = false)
        {
            var results = new List<GameObject>();
            var seen = new HashSet<int>();
            void Add(GameObject go)
            {
                if (go != null && seen.Add(go.GetHashCode())) results.Add(go);
            }

            foreach (string t in GetStringList(p, "targets"))
            {
                var go = FindGameObject(t);
                if (go != null) Add(go);
            }

            string parent = GetString(p, "parent");
            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = FindGameObject(parent);
                if (parentGo != null)
                {
                    bool deep = GetBool(p, "include_children", includeChildrenDefault);
                    foreach (Transform child in parentGo.transform)
                    {
                        if (deep) foreach (var tr in child.GetComponentsInChildren<Transform>(true)) Add(tr.gameObject);
                        else Add(child.gameObject);
                    }
                }
            }

            string contains = GetString(p, "name_contains");
            if (!string.IsNullOrEmpty(contains))
            {
                foreach (var go in AllSceneObjects())
                    if (go.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0) Add(go);
            }

            if (GetBool(p, "use_selection", false))
            {
                foreach (var go in Selection.gameObjects) Add(go);
            }

            return results;
        }

        public static GameObject FindGameObject(string nameOrPathOrId)
        {
            if (string.IsNullOrEmpty(nameOrPathOrId)) return null;

            if (int.TryParse(nameOrPathOrId, out int id))
            {
                var byId = MCPForUnity.Runtime.Helpers.UnityObjectIdCompat.InstanceIDToObjectCompat(id) as GameObject;
                if (byId != null) return byId;
            }

            var direct = GameObject.Find(nameOrPathOrId);
            if (direct != null) return direct;

            // Inactive objects: walk the loaded scenes.
            foreach (var go in AllSceneObjects())
            {
                if (go.name == nameOrPathOrId || GetHierarchyPath(go) == nameOrPathOrId) return go;
            }
            return null;
        }

        public static IEnumerable<GameObject> AllSceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                        yield return tr.gameObject;
            }
        }

        public static string GetHierarchyPath(GameObject go)
        {
            if (go == null) return null;
            var parts = new List<string>();
            var t = go.transform;
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ---------- Scene bookkeeping ----------

        /// <summary>Marks the active scene dirty and optionally saves it.</summary>
        public static void MarkDirty(bool save)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            EditorSceneManager.MarkSceneDirty(scene);
            if (save && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }

        public static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = System.IO.Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static void Log(string message) => Debug.Log(LogPrefix + message);
    }
}
