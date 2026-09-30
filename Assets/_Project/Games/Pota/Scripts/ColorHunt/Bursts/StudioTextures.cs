using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Small assets the studio looks make in code (cached): A's ray star and edge glow, a faceted candy chunk.</summary>
    public static class StudioTextures
    {
        private static Texture2D _rayStar, _edge;
        private static Sprite _edgeSprite;
        private static Mesh _chunk;

        /// <summary>White star: a soft core plus <paramref name="rays"/> sharp rays fading out; the alpha carries the shape.</summary>
        public static Texture2D RayStar(int size = 128, int rays = 8)
        {
            if (_rayStar != null && _rayStar.width == size) return _rayStar;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    float core = Mathf.Clamp01(1f - r / 0.28f);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * rays * 0.5f)), 28f) * Mathf.Sqrt(Mathf.Clamp01(1f - r));   // bright out to the tip
                    float alpha = Mathf.Clamp01(core * core + ray);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            _rayStar = Make("RayStar", size, px);
            return _rayStar;
        }

        /// <summary>Clear in the middle, opaque at the screen edges (squared ramp from 55 % of the way out).</summary>
        public static Texture2D EdgeGlow(int size = 256)
        {
            if (_edge != null && _edge.width == size) return _edge;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    float t = Mathf.Clamp01((Mathf.Max(dx, dy) - 0.55f) / 0.45f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(t * t * 255f));
                }
            _edge = Make("EdgeGlow", size, px);
            return _edge;
        }

        public static Sprite EdgeGlowSprite()
        {
            if (_edgeSprite != null) return _edgeSprite;
            var t = EdgeGlow();
            _edgeSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
            return _edgeSprite;
        }

        /// <summary>A faceted candy chunk: an icosahedron of radius 0.5 with split vertices (flat normals), wound for Unity.</summary>
        public static Mesh CandyChunk()
        {
            if (_chunk != null) return _chunk;
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var p = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < p.Length; i++) p[i] = p[i].normalized * 0.5f;
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var verts = new Vector3[60];
            var normals = new Vector3[60];
            var tris = new int[60];
            for (int k = 0; k < 20; k++)
            {
                Vector3 a = p[f[3 * k]], b = p[f[3 * k + 1]], c = p[f[3 * k + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, a + b + c) < 0f) { var s = b; b = c; c = s; n = -n; }
                verts[3 * k] = a; verts[3 * k + 1] = b; verts[3 * k + 2] = c;
                normals[3 * k] = normals[3 * k + 1] = normals[3 * k + 2] = n;
                tris[3 * k] = 3 * k; tris[3 * k + 1] = 3 * k + 1; tris[3 * k + 2] = 3 * k + 2;
            }
            _chunk = new Mesh { name = "CandyChunk", vertices = verts, normals = normals, triangles = tris };
            _chunk.RecalculateBounds();
            return _chunk;
        }

        private static Texture2D Make(string name, int size, Color32[] px)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
