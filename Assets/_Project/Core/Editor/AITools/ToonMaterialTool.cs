using System;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Gives a prefab the Core toon look: creates (or updates) a Project/Toon material whose base map comes from an explicit
    /// texture or from the first base-colour texture of a model, and assigns it to every renderer of the prefab.
    /// </summary>
    [McpForUnityTool("ai_toon_material", Description =
        "Apply the Project/Toon material to every renderer of a prefab. prefab: prefab path. texture: base map path, or " +
        "model: glb/fbx whose first base-colour texture is used. material: output .mat (default <prefab>_Toon.mat).")]
    public static class ToonMaterialTool
    {
        public class Parameters
        {
            [ToolParameter("Prefab asset path")] public string prefab { get; set; }
            [ToolParameter("Base map texture path", Required = false)] public string texture { get; set; }
            [ToolParameter("Model (glb/fbx) whose base-colour texture becomes the base map", Required = false)] public string model { get; set; }
            [ToolParameter("Output material path (default <prefab>_Toon.mat)", Required = false)] public string material { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            string prefab = AIToolsCommon.GetString(p, "prefab");
            if (string.IsNullOrEmpty(prefab)) return new ErrorResponse("'prefab' is required.");
            try
            {
                var mat = Apply(prefab, AIToolsCommon.GetString(p, "texture"), AIToolsCommon.GetString(p, "model"),
                    AIToolsCommon.GetString(p, "material"));
                string msg = $"{AssetDatabase.GetAssetPath(mat)} (base map {(mat.GetTexture("_BaseMap") != null ? mat.GetTexture("_BaseMap").name : "none")}) -> {prefab}";
                AIToolsCommon.Log("Toon material: " + msg);
                return new SuccessResponse(msg, new { material = AssetDatabase.GetAssetPath(mat), prefab });
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return new ErrorResponse(e.Message);
            }
        }

        public static Material Apply(string prefabPath, string texturePath = null, string modelPath = null, string materialPath = null)
        {
            var shader = Shader.Find("Project/Toon") ?? throw new InvalidOperationException("Shader Project/Toon not found.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) throw new ArgumentException("No prefab at " + prefabPath);
            Texture baseMap = null;
            if (!string.IsNullOrEmpty(texturePath))
                baseMap = AssetDatabase.LoadAssetAtPath<Texture>(texturePath)
                          ?? throw new ArgumentException("No texture at " + texturePath);
            else if (!string.IsNullOrEmpty(modelPath))
                baseMap = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Select(BaseTexture).FirstOrDefault(t => t != null)
                          ?? throw new ArgumentException("No base-colour texture in " + modelPath);
            materialPath = string.IsNullOrEmpty(materialPath)
                ? Path.ChangeExtension(prefabPath, null).Replace('\\', '/') + "_Toon.mat"
                : materialPath;
            // Set everything before the material reaches the disk: saving the prefab can reload a just-created
            // material from its file, and a base map set only in memory was lost on every second reuse of a path.
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool created = material == null;
            if (created) material = new Material(shader);
            else material.shader = shader;
            if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
            if (created) AssetDatabase.CreateAsset(material, materialPath);
            else
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // the character's meshes only: particles, trails and lines keep their own materials
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer))
                    renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>The base-colour texture of an imported material (glTFast "baseColorTexture", URP "_BaseMap", legacy "_MainTex").</summary>
        public static Texture BaseTexture(Material material)
        {
            if (material == null) return null;
            foreach (string name in material.GetTexturePropertyNames())
                if (name.IndexOf("baseColor", StringComparison.OrdinalIgnoreCase) >= 0 || name == "_BaseMap" || name == "_MainTex")
                {
                    var texture = material.GetTexture(name);
                    if (texture != null) return texture;
                }
            return null;
        }
    }
}
