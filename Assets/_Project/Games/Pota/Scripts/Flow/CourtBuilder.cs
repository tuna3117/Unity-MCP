using Project.Basket.Visual;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>
    /// Rooftop court behind the terrace (Pota saha.ts look): orange floor with white lines drawn into a texture in code,
    /// key and three-point arc at the far end under the giant hoop. Top face at y = 0. No collider.
    /// </summary>
    public static class CourtBuilder
    {
        /// <summary>Marked top (a quad facing up, v = 1 at the hoop end) with a plain slab under it for the roof edges.</summary>
        public static GameObject Build(Transform parent, PotaFlowSettings s)
        {
            var tex = CourtTexture(512, 1024, s.CourtColor, s.CourtLineColor);
            var mat = BasketEnvironmentBuilder.TexturedLit(tex, Color.white, 0.25f);
            var centre = new Vector3(0f, 0f, s.CourtStartZ + s.CourtSize.y * 0.5f);
            var top = RimMeshBuilder.MeshObject("Court", parent, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), mat);
            top.transform.position = centre;
            top.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            top.transform.localScale = new Vector3(s.CourtSize.x, s.CourtSize.y, 1f);

            var edge = RimMeshBuilder.UrpLit(s.CourtColor * 0.62f, 0.2f);
            var body = RimMeshBuilder.MeshObject("CourtBody", top.transform, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), edge);
            body.transform.SetPositionAndRotation(centre + new Vector3(0f, -0.201f, 0f), Quaternion.identity);
            body.transform.localScale = Vector3.one;
            var ls = body.transform.lossyScale;      // undo the quad's scale/rotation: 15 × 0.4 × 28 m in world terms
            body.transform.localScale = new Vector3(s.CourtSize.x / ls.x, 0.4f / ls.y, s.CourtSize.y / ls.z);
            return top;
        }

        /// <summary>Court markings: v = 0 is the terrace end, v = 1 the hoop end.</summary>
        public static Texture2D CourtTexture(int w, int h, Color floor, Color line)
        {
            var px = new Color32[w * h];
            Color32 f = floor, l = line;
            for (int i = 0; i < px.Length; i++) px[i] = f;
            int lw = Mathf.Max(2, w / 96);

            void Fill(int x0, int y0, int x1, int y1)
            {
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                        px[y * w + x] = l;
            }

            void Circle(float cx, float cy, float r)
            {
                int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - lw)), y1 = Mathf.Min(h, Mathf.CeilToInt(cy + r + lw));
                int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - lw)), x1 = Mathf.Min(w, Mathf.CeilToInt(cx + r + lw));
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (Mathf.Abs(d - r) <= lw * 0.5f) px[y * w + x] = l;
                    }
            }

            Fill(0, 0, w, lw);                 // terrace end line
            Fill(0, h - lw, w, h);             // hoop end line
            Fill(0, 0, lw, h);                 // side lines
            Fill(w - lw, 0, w, h);
            Fill(0, h / 2 - lw / 2, w, h / 2 - lw / 2 + lw);   // half-court line
            Circle(w * 0.5f, h * 0.5f, w * 0.12f);              // centre circle
            int kx0 = Mathf.RoundToInt(w * 0.335f), kx1 = Mathf.RoundToInt(w * 0.665f), ky = Mathf.RoundToInt(h * 0.8f);
            Fill(kx0, ky, kx0 + lw, h);        // key
            Fill(kx1 - lw, ky, kx1, h);
            Fill(kx0, ky - lw / 2, kx1, ky - lw / 2 + lw);
            Circle(w * 0.5f, ky, w * 0.165f);  // free-throw circle
            Circle(w * 0.5f, h * 0.94f, w * 0.42f);   // three-point arc (clipped by the end line)

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "CourtTexture", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
