using UnityEngine;

namespace Project.Basket.Visual
{
    /// <summary>
    /// The world around the column: textured facade shaft, buttresses, side wings with floor
    /// mouldings and windows, procedural city in three depth layers, sky plate, terrace and player.
    /// Mirrors the original game's sahne.ts layout.
    /// </summary>
    public static class BasketEnvironmentBuilder
    {
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        public static PlayerActor Build(Transform root, HoopLayout layout, BasketSettings s)
        {
            float top = layout.TopRimY + HoopColumnBuilder.TopMargin;
            float bottom = layout.BasketFloorY - 30f;
            float height = top - bottom;
            float midY = (top + bottom) * 0.5f;

            var env = new GameObject("Environment").transform;
            env.SetParent(root, false);

            // --- facade shaft (behind the hoops) ---
            var shaftMat = TexturedLit(s.FacadeTexture, new Color(0.85f, 0.82f, 0.78f), 0.15f);
            var shaft = Box(env, "Shaft", new Vector3(0f, midY, -s.SlabHalfDepth - 0.35f), new Vector3(layout.WallX * 2f + 0.6f, height, 0.5f), shaftMat);
            Tile(shaft, (layout.WallX * 2f + 0.6f) / 3.2f, height / 5.7f);

            // --- buttresses at the column edges (their inner faces are the physics walls) ---
            var stoneMat = RimMeshBuilder.UrpLit(new Color(0.93f, 0.9f, 0.84f), 0.25f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Box(env, "Buttress", new Vector3(sx * (layout.WallX + 0.25f), midY, -0.05f), new Vector3(0.5f, height, 1.1f), stoneMat);
            }

            // --- wings with mouldings and windows ---
            var wingMat = TexturedLit(s.WingTexture, new Color(0.9f, 0.85f, 0.8f), 0.15f);
            var glassMat = RimMeshBuilder.UrpLit(s.GlassColor, 0.9f);
            var mouldMat = RimMeshBuilder.UrpLit(new Color(0.96f, 0.94f, 0.9f), 0.3f);
            float wingW = 3.2f;
            foreach (float sx in new[] { -1f, 1f })
            {
                float wx = sx * (layout.WallX + 0.5f + wingW * 0.5f);
                var wing = Box(env, "Wing", new Vector3(wx, midY, -0.38f - 1.5f), new Vector3(wingW, height, 3f), wingMat);
                Tile(wing, wingW / 3.2f, height / 5.7f);
                for (float fy = layout.TopRimY + 1.5f; fy > bottom + 1f; fy -= 3f)
                {
                    Box(env, "Moulding", new Vector3(wx, fy, -0.30f), new Vector3(wingW + 0.1f, 0.15f, 0.25f), mouldMat);
                    foreach (float ox in new[] { -0.8f, 0.8f })
                        Box(env, "Window", new Vector3(wx + ox, fy - 1.5f, -0.26f), new Vector3(0.85f, 1.5f, 0.12f), glassMat);
                }
            }

            // --- city in three depth layers ---
            var cityA = TexturedLit(s.CityTexture, new Color(0.95f, 0.93f, 0.9f), 0.1f);
            var cityB = TexturedLit(s.FacadeTexture, new Color(0.9f, 0.9f, 0.92f), 0.1f);
            var rng = new System.Random(21);
            float cityBase = layout.BasketFloorY - 40f;
            float[] depths = { -16f, -30f, -48f };
            float[] spreads = { 22f, 34f, 48f };
            int[] counts = { 8, 12, 14 };
            for (int layer = 0; layer < depths.Length; layer++)
            {
                for (int i = 0; i < counts[layer]; i++)
                {
                    float w = 4f + (float)rng.NextDouble() * 6f;
                    float h = 26f + (float)rng.NextDouble() * 52f;
                    float d = 5f + (float)rng.NextDouble() * 5f;
                    float x = ((float)rng.NextDouble() - 0.5f) * 2f * spreads[layer];
                    if (layer == 0 && Mathf.Abs(x) < 9f) x += Mathf.Sign(x == 0f ? 1f : x) * 9f;
                    var tower = Box(env, $"Tower{layer}_{i}", new Vector3(x, cityBase + h * 0.5f, depths[layer] - d * 0.5f), new Vector3(w, h, d), i % 2 == 0 ? cityA : cityB);
                    Tile(tower, w / 3.2f, h / 5.7f);
                }
            }

            // --- sky plate ---
            if (s.SkyTexture != null)
            {
                var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sky.name = "SkyPlate";
                Object.Destroy(sky.GetComponent<Collider>());
                sky.transform.SetParent(env, false);
                sky.transform.position = new Vector3(0f, 44f, -60f);
                sky.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                sky.transform.localScale = new Vector3(45f * 2.2f, 80f, 1f);
                var skyMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                skyMat.SetTexture("_BaseMap", s.SkyTexture);
                sky.GetComponent<MeshRenderer>().sharedMaterial = skyMat;
            }

            // --- terrace the player stands on ---
            var terraceMat = RimMeshBuilder.UrpLit(s.TerraceColor, 0.2f);
            // Terrace top at y = 0: the player's hands end up at the launch height (1.9 m), like the original.
            Box(env, "Terrace", new Vector3(0f, -0.15f, 7.2f), new Vector3(7f, 0.3f, 2.4f), terraceMat);
            var railMat = RimMeshBuilder.UrpLit(new Color(0.2f, 0.2f, 0.22f), 0.5f);
            Box(env, "Rail", new Vector3(0f, 0.9f, 6.05f), new Vector3(7f, 0.06f, 0.06f), railMat);
            for (float px = -3.4f; px <= 3.4f; px += 1.7f)
                Box(env, "RailPost", new Vector3(px, 0.45f, 6.05f), new Vector3(0.05f, 0.9f, 0.05f), railMat);

            // --- player ---
            PlayerActor actor = null;
            if (s.PlayerPrefab != null)
            {
                var player = Object.Instantiate(s.PlayerPrefab, env);
                player.name = "Player";
                foreach (var c in player.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                var bounds = RenderBounds(player);
                float scale = bounds.size.y > 0.01f ? 1.85f / bounds.size.y : 1f;
                player.transform.localScale = Vector3.one * scale;
                bounds = RenderBounds(player);
                player.transform.position = new Vector3(0f, 0f - (bounds.min.y - player.transform.position.y), 6.8f);
                player.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // back to the camera (verified by screenshot)
                actor = player.AddComponent<PlayerActor>();
            }
            return actor;
        }

        private static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void Tile(GameObject go, float tx, float ty)
        {
            var mr = go.GetComponent<MeshRenderer>();
            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            mpb.SetVector(BaseMapSt, new Vector4(Mathf.Max(0.1f, tx), Mathf.Max(0.1f, ty), 0f, 0f));
            mr.SetPropertyBlock(mpb);
        }

        public static Material TexturedLit(Texture2D tex, Color tint, float smoothness)
        {
            var m = RimMeshBuilder.UrpLit(tint, smoothness);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.color = Color.white;
            }
            return m;
        }
    }
}
