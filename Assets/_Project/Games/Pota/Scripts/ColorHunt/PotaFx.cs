using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's effect formulas, pure (patlamaEfekt.ts balon kipi, efekt.ts Parcaciklar). Vectors are in Pota's board axes: +z toward the
    /// camera (the caller mirrors z for Unity). Randomness comes from the caller's generator, one draw per Math.random in Pota.
    /// </summary>
    public static class PotaFx
    {
        public struct Shard { public Vector2 Offset; public Vector3 Velocity, Size, Rotation, Spin; public float Life, Tone; }
        public struct Star { public Vector3 Velocity; public float ZOffset, Size, Rotation, Life; public bool Warm; }
        public struct Point { public Vector3 Velocity; public float Life, Size; }

        private static float R(System.Random r) => (float)r.NextDouble();

        /// <summary>flaslariIlerlet: size = start + (end − start)·(1 − (1 − u)^2.2).</summary>
        public static float Grow(float u) => 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 2.2f);

        /// <summary>kivilcimlariIlerlet: sin(π·min(1, 0.12 + 0.95a))·(0.75 + 0.25·sin(38a)), a = age / life.</summary>
        public static float StarSize(float a)
        {
            a = Mathf.Clamp01(a);
            return Mathf.Sin(Mathf.PI * Mathf.Min(1f, 0.12f + a * 0.95f)) * (0.75f + 0.25f * Mathf.Sin(a * 38f));
        }

        /// <summary>guncelle: 0.35 + 0.65·min(1, remaining·1.6) (sonOlcek 0.35).</summary>
        public static float ShardScale(float a) => 0.35f + 0.65f * Mathf.Min(1f, (1f - Mathf.Clamp01(a)) * 1.6f);

        /// <summary>renkAviFaz.ts patlamaKatmanlari: min(parca.enCok, taban + d·derinlikBasi).</summary>
        public static int ShardCount(ColorHuntSettings s, int depth) => Mathf.Min(s.ShardsMax, s.ShardsBase + depth * s.ShardsPerDepth);

        /// <summary>patlat → kivilcimSac(…, 10 + d·2).</summary>
        public static int StarCount(int depth) => 10 + depth * 2;

        /// <summary>renkAviFaz.ts adimBaslat: min(HIS.parcacik.enCok, taban + d·derinlikBasi) points per chain burst.</summary>
        public static int PointCount(ColorHuntSettings s, int depth) => Mathf.Min(s.PointsMax, s.PointsBase + depth * s.PointsPerDepth);

        /// <summary>patlat: a cone of up to 0.62·90° from +z, 3.5–7.5 (+0.4/depth), +1.2 up; flat shell pieces 0.16–0.36; 0.7–1.2 s.</summary>
        public static Shard NewShard(System.Random r, int index, int depth)
        {
            float tone = index % 5 == 3 ? 0.65f : index % 5 == 4 ? 1.35f : 1f;
            float a = R(r) * Mathf.PI * 2f, tilt = R(r) * 0.62f * Mathf.PI * 0.5f, speed = 3.5f + R(r) * 4f + depth * 0.4f;
            var offset = new Vector2((R(r) - 0.5f) * 0.3f, (R(r) - 0.5f) * 0.3f);
            var v = new Vector3(Mathf.Cos(a) * Mathf.Sin(tilt) * speed, Mathf.Sin(a) * Mathf.Sin(tilt) * speed + 1.2f, Mathf.Cos(tilt) * speed * 0.8f);
            var rot = new Vector3(R(r), R(r), R(r)) * (Mathf.PI * 2f);
            var spin = new Vector3(R(r) - 0.5f, R(r) - 0.5f, R(r) - 0.5f) * 16f;
            float boy = 0.16f + R(r) * 0.2f;
            var size = new Vector3(boy * (0.8f + R(r) * 0.6f), boy * (0.55f + R(r) * 0.4f), boy * 0.45f);
            return new Shard { Offset = offset, Velocity = v, Rotation = rot, Spin = spin, Size = size, Life = 0.7f + R(r) * 0.5f, Tone = tone };
        }

        /// <summary>kivilcimSac: flat radial 3–7.5, 0.8–2.4 toward the camera, 0.36–0.66, 0.4–0.7 s; even ones white-yellow.</summary>
        public static Star NewStar(System.Random r, int index)
        {
            float a = R(r) * Mathf.PI * 2f, speed = 3f + R(r) * 4.5f;
            return new Star
            {
                ZOffset = R(r) * 0.3f,
                Velocity = new Vector3(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed, 0.8f + R(r) * 1.6f),
                Size = 0.36f + R(r) * 0.3f,
                Rotation = R(r) * Mathf.PI,
                Life = 0.4f + R(r) * 0.3f,
                Warm = index % 2 == 0,
            };
        }

        /// <summary>Parcaciklar.patlat: a, e = (rand − 0.3)·π/2, h = speed·(0.5 + rand); 0.45–0.8 s; size·(0.7–1.3).</summary>
        public static Point NewPoint(System.Random r, float speed, float size)
        {
            float a = R(r) * Mathf.PI * 2f, e = (R(r) - 0.3f) * Mathf.PI * 0.5f, h = speed * (0.5f + R(r));
            return new Point
            {
                Velocity = new Vector3(Mathf.Cos(a) * Mathf.Cos(e) * h, Mathf.Sin(e) * h + speed * 0.6f, Mathf.Sin(a) * Mathf.Cos(e) * h * 0.6f),
                Life = 0.45f + R(r) * 0.35f,
                Size = size * (0.7f + R(r) * 0.6f),
            };
        }

        /// <summary>efekt.ts PARCACIK_VS gl_PointSize = size·300/depth px → world size on a camera of this FOV and pixel height.</summary>
        public static float PointWorldSize(float size, float fov, float pixelHeight) =>
            size * 300f * 2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, pixelHeight);

        /// <summary>efekt.ts PARCACIK_FS: k = smoothstep(0.5, 0.2, r), rgb·(1 + 0.6k), alpha k; rgb stored ÷1.6 (material ×1.6).</summary>
        public static Texture2D PointSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PotaPoint" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude;
                    float t = Mathf.Clamp01((r - 0.5f) / (0.2f - 0.5f));
                    float k = r > 0.5f ? 0f : t * t * (3f - 2f * t);
                    float rgb = (1f + 0.6f * k) / 1.6f;
                    tex.SetPixel(x, y, new Color(rgb, rgb, rgb, k));
                }
            tex.Apply();
            return tex;
        }

        /// <summary>three IcosahedronGeometry(radius, 1): 42 vertices, 80 faces, smooth normals (patlamaEfekt.ts balonKipi shards).</summary>
        public static Mesh Icosphere(float radius)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new System.Collections.Generic.Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i)) return i;
                v.Add((v[a] + v[b]) * 0.5f);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var verts = new Vector3[v.Count];
            var norms = new Vector3[v.Count];
            for (int i = 0; i < v.Count; i++) { norms[i] = v[i].normalized; verts[i] = norms[i] * radius; }
            // (b − a) × (c − a) points outward for every face above: Unity's front face (checked on face 0, 11, 5).
            var m = new Mesh { name = "PotaShard", vertices = verts, normals = norms, triangles = tris.ToArray() };
            m.RecalculateBounds();
            return m;
        }
    }
}
