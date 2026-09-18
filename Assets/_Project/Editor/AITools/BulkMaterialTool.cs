using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Assigns one material asset to the renderers of many objects.
    /// </summary>
    [McpForUnityTool("ai_bulk_assign_material", Description =
        "Assign a material asset (material_path) to the renderers of many objects. " +
        "Select targets with targets[] (names/paths/ids), parent (+include_children), name_contains, or use_selection. " +
        "slot = -1 assigns every material slot (default), otherwise only that slot index.")]
    public static class BulkMaterialTool
    {
        public class Parameters
        {
            [ToolParameter("Material asset path, e.g. 'Assets/_Project/Materials/Red.mat'")]
            public string material_path { get; set; }

            [ToolParameter("Explicit targets: names, hierarchy paths or instance ids", Required = false)]
            public string[] targets { get; set; }

            [ToolParameter("Parent object whose children are targeted", Required = false)]
            public string parent { get; set; }

            [ToolParameter("Also apply to renderers on child objects of each target (default true)", Required = false)]
            public bool? include_children { get; set; }

            [ToolParameter("Substring filter over every object in the loaded scenes", Required = false)]
            public string name_contains { get; set; }

            [ToolParameter("Use the current editor selection", Required = false)]
            public bool? use_selection { get; set; }

            [ToolParameter("Material slot index; -1 = all slots (default)", Required = false)]
            public int? slot { get; set; }

            [ToolParameter("Save the active scene afterwards (default false)", Required = false)]
            public bool? save { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            string materialPath = AIToolsCommon.GetString(p, "material_path");
            if (string.IsNullOrEmpty(materialPath)) return new ErrorResponse("'material_path' is required.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) return new ErrorResponse($"Material not found at '{materialPath}'.");

            var targets = AIToolsCommon.ResolveTargets(p, includeChildrenDefault: true);
            if (targets.Count == 0) return new ErrorResponse("No targets matched (use targets[], parent, name_contains or use_selection).");

            bool includeChildren = AIToolsCommon.GetBool(p, "include_children", true);
            int slot = AIToolsCommon.GetInt(p, "slot", -1);
            int updated = Assign(targets, material, includeChildren, slot);
            AIToolsCommon.MarkDirty(AIToolsCommon.GetBool(p, "save", false));

            string msg = $"Assigned '{material.name}' to {updated} renderer(s) on {targets.Count} target(s).";
            AIToolsCommon.Log(msg);
            return new SuccessResponse(msg, new { renderers = updated, targets = targets.Count });
        }

        public static int Assign(List<GameObject> targets, Material material, bool includeChildren, int slot)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("AI Bulk Assign Material");
            int updated = 0;
            foreach (var go in targets)
            {
                var renderers = includeChildren ? go.GetComponentsInChildren<Renderer>(true) : go.GetComponents<Renderer>();
                foreach (var r in renderers)
                {
                    var mats = r.sharedMaterials;
                    if (mats == null || mats.Length == 0) mats = new Material[1];
                    if (slot >= mats.Length) continue;
                    Undo.RecordObject(r, "AI Bulk Assign Material");
                    if (slot < 0) for (int i = 0; i < mats.Length; i++) mats[i] = material;
                    else mats[slot] = material;
                    r.sharedMaterials = mats;
                    updated++;
                }
            }
            return updated;
        }

        [MenuItem("AI Tools/Selection/Assign Selected Material To Selected Objects")]
        public static void AssignSelectionMenu()
        {
            var material = Selection.objects.OfType<Material>().FirstOrDefault();
            var targets = Selection.gameObjects.ToList();
            if (material == null || targets.Count == 0)
            {
                AIToolsCommon.Log("Select at least one Material asset and one scene object.");
                return;
            }
            int updated = Assign(targets, material, true, -1);
            AIToolsCommon.MarkDirty(false);
            AIToolsCommon.Log($"Assigned '{material.name}' to {updated} renderer(s).");
        }
    }
}
