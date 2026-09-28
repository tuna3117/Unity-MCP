using System.Collections.Generic;
using UnityEngine;

namespace Project.Basket.Visual
{
    /// <summary>
    /// Depth-tested materials for TextMesh labels. Unity's dynamic font atlas can be rebuilt at
    /// runtime, so every material created here is re-pointed at the new texture when that happens.
    /// </summary>
    public static class DepthTextMaterial
    {
        private static readonly List<(Font font, Material mat)> Materials = new List<(Font, Material)>();
        private static bool _subscribed;

        public static Material Create(Font font)
        {
            var shader = Shader.Find("Project/TextDepth");
            var mat = shader != null ? new Material(shader) : new Material(font.material);
            mat.mainTexture = font.material.mainTexture;
            Materials.Add((font, mat));
            if (!_subscribed)
            {
                Font.textureRebuilt += OnRebuilt;
                _subscribed = true;
            }
            return mat;
        }

        private static void OnRebuilt(Font font)
        {
            for (int i = Materials.Count - 1; i >= 0; i--)
            {
                var (f, m) = Materials[i];
                if (m == null) { Materials.RemoveAt(i); continue; }
                if (f == font) m.mainTexture = font.material.mainTexture;
            }
        }
    }
}
