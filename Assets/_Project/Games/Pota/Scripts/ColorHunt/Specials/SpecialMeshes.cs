using System;
using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The Blender meshes of the special looks (ai_studio_import: RoyalHalves, RoyalShell, RoyalCoin, CourtBall). Each GLB carries its
    /// skin as an embedded base-colour texture on glTFast's own material ("baseColorTexture"); Core Project/Toon reads _BaseMap with
    /// UV0 (no vertex colours), so <see cref="Toon"/> moves the skin onto a toon material cloned from the settings' template.
    /// Axes: workshop front −Y → Unity +z and workshop +X → Unity −X, so the halves' noses point along local +X and the coin faces ±z.
    /// </summary>
    public static class SpecialMeshes
    {
        /// <summary>The workshop's cartoon ink #141b2e (sRGB).</summary>
        public static readonly Color Ink = new Color(0.078f, 0.106f, 0.18f);

        public struct Part
        {
            public string Name;
            public Mesh Mesh;
            /// <summary>The child's position in the model's space (shell pieces: the piece's centre).</summary>
            public Vector3 Local;
        }

        /// <summary>The first base-colour texture on the model's renderers; null when the model is missing or untextured.</summary>
        public static Texture Skin(GameObject model)
        {
            if (model == null) return null;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    foreach (string name in material.GetTexturePropertyNames())
                        if (name.IndexOf("baseColor", StringComparison.OrdinalIgnoreCase) >= 0 || name == "_BaseMap" || name == "_MainTex")
                        {
                            var texture = material.GetTexture(name);
                            if (texture != null) return texture;
                        }
                }
            return null;
        }

        /// <summary>Project/Toon from <see cref="ColorHuntSettings.ToonTemplate"/> wearing the model's skin, with an ink outline
        /// <paramref name="outline"/> metres wide.</summary>
        public static Material Toon(ColorHuntSettings s, GameObject model, string name, float outline = 0.008f)
        {
            var m = RuntimeMaterial.From(s != null ? s.ToonTemplate : null, "Project/Toon", name);
            var skin = Skin(model);
            if (skin != null) m.SetTexture("_BaseMap", skin);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_OutlineColor", Ink);
            m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        /// <summary>The model's meshes with their node names and positions, sorted by name (HalfA, HalfB; piece_00 …).</summary>
        public static List<Part> Parts(GameObject model)
        {
            var parts = new List<Part>();
            if (model == null) return parts;
            var root = model.transform;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    parts.Add(new Part { Name = filter.name, Mesh = filter.sharedMesh, Local = root.InverseTransformPoint(filter.transform.position) });
            parts.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return parts;
        }
    }
}
