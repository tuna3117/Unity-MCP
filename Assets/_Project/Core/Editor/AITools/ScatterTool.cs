using System.Collections.Generic;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Instantiates copies of a prefab (or scene object) on a surface or in a grid.
    /// </summary>
    [McpForUnityTool("ai_scatter", Description =
        "Scatter copies of a prefab asset (source = 'Assets/....prefab') or a scene object (source = name) inside an area. " +
        "mode 'random' (count copies at random XZ positions) or 'grid' (rows x cols with grid_spacing). " +
        "With surface=true (default) each copy is dropped onto the first collider hit by a downward ray. " +
        "Copies are parented under parent_name (default '<source>_Scatter').")]
    public static class ScatterTool
    {
        public class Parameters
        {
            [ToolParameter("Prefab asset path or scene object name to copy")]
            public string source { get; set; }

            [ToolParameter("Number of copies for mode=random (default 10)", Required = false)]
            public int? count { get; set; }

            [ToolParameter("'random' (default) or 'grid'", Required = false)]
            public string mode { get; set; }

            [ToolParameter("Area center [x,y,z] (default [0,0,0]); y is the fallback height when no surface is hit", Required = false)]
            public float[] center { get; set; }

            [ToolParameter("Area size [width_x, depth_z] (default [10,10])", Required = false)]
            public float[] size { get; set; }

            [ToolParameter("Spacing between copies for mode=grid (default 2)", Required = false)]
            public float? grid_spacing { get; set; }

            [ToolParameter("Raycast down onto colliders to find the ground (default true)", Required = false)]
            public bool? surface { get; set; }

            [ToolParameter("Rotate copies to match the surface normal (default false)", Required = false)]
            public bool? align_to_normal { get; set; }

            [ToolParameter("Random Y rotation per copy (default true)", Required = false)]
            public bool? random_rotation_y { get; set; }

            [ToolParameter("Minimum uniform scale (default 1)", Required = false)]
            public float? scale_min { get; set; }

            [ToolParameter("Maximum uniform scale (default 1)", Required = false)]
            public float? scale_max { get; set; }

            [ToolParameter("Name of the parent object that receives the copies", Required = false)]
            public string parent_name { get; set; }

            [ToolParameter("Random seed (default 0 = random)", Required = false)]
            public int? seed { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            string source = AIToolsCommon.GetString(p, "source");
            if (string.IsNullOrEmpty(source)) return new ErrorResponse("'source' is required.");

            GameObject template = null;
            bool isPrefabAsset = false;
            if (source.StartsWith("Assets/"))
            {
                template = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                isPrefabAsset = template != null;
            }
            if (template == null) template = AIToolsCommon.FindGameObject(source);
            if (template == null) return new ErrorResponse($"Source '{source}' not found as prefab asset or scene object.");

            var settings = new Settings
            {
                Count = Mathf.Max(1, AIToolsCommon.GetInt(p, "count", 10)),
                Mode = (AIToolsCommon.GetString(p, "mode", "random") ?? "random").ToLowerInvariant(),
                Center = ToVec3(p["center"], Vector3.zero),
                Size = ToVec2(p["size"], new Vector2(10, 10)),
                GridSpacing = Mathf.Max(0.01f, AIToolsCommon.GetFloat(p, "grid_spacing", 2f)),
                Surface = AIToolsCommon.GetBool(p, "surface", true),
                AlignToNormal = AIToolsCommon.GetBool(p, "align_to_normal", false),
                RandomRotationY = AIToolsCommon.GetBool(p, "random_rotation_y", true),
                ScaleMin = AIToolsCommon.GetFloat(p, "scale_min", 1f),
                ScaleMax = AIToolsCommon.GetFloat(p, "scale_max", 1f),
                ParentName = AIToolsCommon.GetString(p, "parent_name") ?? $"{template.name}_Scatter",
                Seed = AIToolsCommon.GetInt(p, "seed", 0),
            };

            var parent = Scatter(template, isPrefabAsset, settings, out int placed);
            AIToolsCommon.MarkDirty(false);
            string msg = $"Scattered {placed} copies of '{template.name}' under '{parent.name}' ({settings.Mode}).";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { placed, parent = AIToolsCommon.GetHierarchyPath(parent) });
        }

        public class Settings
        {
            public int Count = 10;
            public string Mode = "random";
            public Vector3 Center = Vector3.zero;
            public Vector2 Size = new Vector2(10, 10);
            public float GridSpacing = 2f;
            public bool Surface = true;
            public bool AlignToNormal = false;
            public bool RandomRotationY = true;
            public float ScaleMin = 1f;
            public float ScaleMax = 1f;
            public string ParentName = "Scatter";
            public int Seed = 0;
        }

        public static GameObject Scatter(GameObject template, bool isPrefabAsset, Settings s, out int placed)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("AI Scatter");

            var parent = GameObject.Find(s.ParentName);
            if (parent == null)
            {
                parent = new GameObject(s.ParentName);
                Undo.RegisterCreatedObjectUndo(parent, "AI Scatter");
            }

            var rng = s.Seed != 0 ? new System.Random(s.Seed) : new System.Random();
            var positions = new List<Vector3>();
            if (s.Mode == "grid")
            {
                int cols = Mathf.FloorToInt(s.Size.x / s.GridSpacing) + 1;
                int rows = Mathf.FloorToInt(s.Size.y / s.GridSpacing) + 1;
                float x0 = s.Center.x - (cols - 1) * s.GridSpacing * 0.5f;
                float z0 = s.Center.z - (rows - 1) * s.GridSpacing * 0.5f;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        positions.Add(new Vector3(x0 + c * s.GridSpacing, s.Center.y, z0 + r * s.GridSpacing));
            }
            else
            {
                for (int i = 0; i < s.Count; i++)
                {
                    float x = s.Center.x + ((float)rng.NextDouble() - 0.5f) * s.Size.x;
                    float z = s.Center.z + ((float)rng.NextDouble() - 0.5f) * s.Size.y;
                    positions.Add(new Vector3(x, s.Center.y, z));
                }
            }

            placed = 0;
            int index = 1;
            foreach (var pos in positions)
            {
                Vector3 finalPos = pos;
                Vector3 normal = Vector3.up;
                if (s.Surface)
                {
                    var origin = new Vector3(pos.x, pos.y + 500f, pos.z);
                    if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f))
                    {
                        finalPos = hit.point;
                        normal = hit.normal;
                    }
                }

                GameObject instance;
                if (isPrefabAsset)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
                }
                else
                {
                    var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(template);
                    instance = prefabSource != null
                        ? (GameObject)PrefabUtility.InstantiatePrefab(prefabSource)
                        : Object.Instantiate(template);
                }
                Undo.RegisterCreatedObjectUndo(instance, "AI Scatter");
                instance.name = $"{template.name}_{index++:000}";
                instance.transform.SetParent(parent.transform, true);
                instance.transform.position = finalPos;

                Quaternion rot = Quaternion.identity;
                if (s.AlignToNormal) rot = Quaternion.FromToRotation(Vector3.up, normal);
                if (s.RandomRotationY) rot *= Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                instance.transform.rotation = rot;

                float scale = Mathf.Lerp(s.ScaleMin, s.ScaleMax, (float)rng.NextDouble());
                instance.transform.localScale = Vector3.one * scale;
                placed++;
            }
            return parent;
        }

        private static Vector3 ToVec3(JToken t, Vector3 fallback)
        {
            if (t is JArray a && a.Count >= 3) return new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>());
            return fallback;
        }

        private static Vector2 ToVec2(JToken t, Vector2 fallback)
        {
            if (t is JArray a && a.Count >= 2) return new Vector2(a[0].Value<float>(), a[1].Value<float>());
            return fallback;
        }

        [MenuItem("AI Tools/Scatter/Scatter Selected Prefab (Grid, spacing 2)")]
        public static void ScatterSelectedPrefabMenu()
        {
            var prefab = Selection.activeObject as GameObject;
            if (prefab == null || !AssetDatabase.Contains(prefab))
            {
                AIToolsCommon.Log("Select a prefab asset in the Project window first.");
                return;
            }
            var settings = new Settings { Mode = "grid", GridSpacing = 2f, Size = new Vector2(8, 8), ParentName = $"{prefab.name}_Scatter" };
            Scatter(prefab, true, settings, out int placed);
            AIToolsCommon.MarkDirty(false);
            AIToolsCommon.Log($"Scattered {placed} copies of '{prefab.name}'.");
        }
    }
}
