using System.Collections.Generic;
using Project.Basket;
using Project.ColorHunt;
using Project.Rendering;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Material templates for the materials Pota makes at runtime from a shader name. A player build keeps only the shaders some asset
    /// references (the 2026-09-28 iOS build had neither URP Unlit nor Project/TextDepth), so each runtime material is cloned from a
    /// template asset wired into ColorHuntSettings / BasketSettings (<see cref="RuntimeMaterial"/>). The templates are built by the same
    /// runtime code, so their keywords are the variant the game uses. Idempotent; run by ai_potalook_import.
    /// </summary>
    public static class PotaMaterialTemplates
    {
        public const string Folder = "Assets/_Project/Games/Pota/Materials/Templates";

        [MenuItem("AI Tools/Pota/Material Templates")]
        public static void Menu() => AIToolsCommon.Log(string.Join(" | ", Ensure()));

        public static List<string> Ensure()
        {
            var report = new List<string>();
            EnsureFolder(Folder);
            var label = Template("PotaLabel", BlockView.LabelMaterial(null, null));
            var additive = Template("PotaAdditive", RuntimeMaterial.From(null, "Project/PotaAdditive"));
            var particleLit = Template("PotaParticleLit", RuntimeMaterial.From(null, "Universal Render Pipeline/Particles/Lit"));
            var selection = Template("PotaSelection", BlockView.RingMaterial(null));
            var sky = Template("PotaSky", RuntimeMaterial.From(null, "Universal Render Pipeline/Unlit"));
            var textDepth = Template("PotaTextDepth", RuntimeMaterial.From(null, "Project/TextDepth"));
            var flipbook = Template("PotaFlipbook", RuntimeMaterial.From(null, "Project/PotaFlipbook"));
            var candyShard = Template("CandyShard", RuntimeMaterial.From(null, "Project/CandyShard"));
            var piece = Template("PotaPiece", RuntimeMaterial.From(null, "Project/PotaPiece"));
            var shock = Template("PotaShockRing", RuntimeMaterial.From(null, "Project/PotaShockRing"));
            var toon = Template("PotaToon", RuntimeMaterial.From(null, "Project/Toon"));
            var stencil = Template("PotaStencilMask", RuntimeMaterial.From(null, "Project/PotaStencilMask"));
            var glint = Template("PotaGlint", RuntimeMaterial.From(null, "Project/PotaGlint"));

            int colorHunt = 0, basket = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(ColorHuntSettings)))
            {
                var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>(AssetDatabase.GUIDToAssetPath(guid));
                s.LabelTemplate = label; s.AdditiveTemplate = additive; s.ParticleLitTemplate = particleLit; s.SelectionTemplate = selection;
                s.FlipbookTemplate = flipbook; s.CandyShardTemplate = candyShard; s.PieceTemplate = piece; s.ShockTemplate = shock;
                s.ToonTemplate = toon; s.StencilMaskTemplate = stencil; s.GlintTemplate = glint;
                EditorUtility.SetDirty(s);
                colorHunt++;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(BasketSettings)))
            {
                var s = AssetDatabase.LoadAssetAtPath<BasketSettings>(AssetDatabase.GUIDToAssetPath(guid));
                s.SkyTemplate = sky; s.TextDepthTemplate = textDepth;
                EditorUtility.SetDirty(s);
                basket++;
            }
            AssetDatabase.SaveAssets();
            report.Add($"material templates: {AssetDatabase.FindAssets("t:Material", new[] { Folder }).Length} in {Folder}, " +
                       $"wired into {colorHunt} ColorHuntSettings + {basket} BasketSettings");
            return report;
        }

        /// <summary>
        /// Writes <paramref name="fresh"/> as a new asset. An existing template with the same shader is kept as it is: copying a fresh
        /// runtime material onto it dropped what URP set when the asset was first imported (the ring's Transparent tag, its disabled
        /// ShadowCaster/DepthOnly passes, its 0.9 alpha). A template whose shader changed is written again.
        /// </summary>
        private static Material Template(string name, Material fresh)
        {
            string path = $"{Folder}/{name}.mat";
            fresh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null && existing.shader == fresh.shader)
            {
                Object.DestroyImmediate(fresh);
                return existing;
            }
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(fresh, path);                      // fully set up before it is written (CLAUDE.md lesson)
            return fresh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
