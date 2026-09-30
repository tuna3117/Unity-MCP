using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class PotaFxCurveTests
    {
        [Test]
        public void Grow_EasesOut()
        {
            Assert.AreEqual(0f, PotaFx.Grow(0f), 1e-6f);
            Assert.AreEqual(1f, PotaFx.Grow(1f), 1e-6f);
            Assert.AreEqual(1f - Mathf.Pow(0.5f, 2.2f), PotaFx.Grow(0.5f), 1e-6f, "patlamaEfekt.ts flaslariIlerlet");
        }

        [Test]
        public void StarSize_BlinksInsideItsEnvelope()
        {
            Assert.AreEqual(Mathf.Sin(0.12f * Mathf.PI) * 0.75f, PotaFx.StarSize(0f), 1e-5f, "visible at birth");
            for (float a = 0f; a <= 1f; a += 0.01f) Assert.That(PotaFx.StarSize(a), Is.InRange(-1e-4f, 1f));
            Assert.Less(PotaFx.StarSize(1f), 0.25f, "gone at the end");
        }

        [Test]
        public void ShardScale_HoldsThenShrinksTo0Point35()
        {
            Assert.AreEqual(1f, PotaFx.ShardScale(0f), 1e-6f);
            Assert.AreEqual(1f, PotaFx.ShardScale(0.375f), 1e-5f, "full size until 1/1.6 of the life is left");
            Assert.AreEqual(0.35f, PotaFx.ShardScale(1f), 1e-6f);
        }

        [Test]
        public void Counts_FollowTheDepth()
        {
            var s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.AreEqual(14, PotaFx.ShardCount(s, 0));
            Assert.AreEqual(34, PotaFx.ShardCount(s, 4));
            Assert.AreEqual(34, PotaFx.ShardCount(s, 9), "cap");
            Assert.AreEqual(16, PotaFx.StarCount(3));
            Assert.AreEqual(10, PotaFx.PointCount(s, 0));
            Assert.AreEqual(26, PotaFx.PointCount(s, 5), "HIS.parcacik cap");
            Object.DestroyImmediate(s);
        }

        [Test]
        public void Shards_FlyInPotasCone()
        {
            var r = new System.Random(1);
            for (int i = 0; i < 500; i++)
            {
                var p = PotaFx.NewShard(r, i, 0);
                var v = p.Velocity - new Vector3(0f, 1.2f, 0f);
                float along = v.z / 0.8f, side = new Vector2(v.x, v.y).magnitude;
                Assert.Greater(v.z, 0f, "toward the camera (Pota +z)");
                Assert.LessOrEqual(Mathf.Atan2(side, along), 0.62f * Mathf.PI * 0.5f + 1e-4f, "≈ 56° cone");
                Assert.That(new Vector2(side, along).magnitude, Is.InRange(3.5f - 1e-3f, 7.5f + 1e-3f));
                Assert.That(p.Life, Is.InRange(0.7f, 1.2f));
                Assert.That(p.Size.z, Is.InRange(0.16f * 0.45f - 1e-5f, 0.36f * 0.45f + 1e-5f), "flat");
                Assert.AreEqual(i % 5 == 3 ? 0.65f : i % 5 == 4 ? 1.35f : 1f, p.Tone);
            }
        }

        [Test]
        public void PointSprite_IsHotInTheMiddle()
        {
            var tex = PotaFx.PointSprite(64);
            var c = tex.GetPixel(32, 32);
            Assert.Greater(c.a, 0.95f);
            Assert.Greater(c.r, 0.95f, "(1 + 0.6k)/1.6 = 1 at the centre");
            Assert.Less(tex.GetPixel(32, 61).a, 0.1f, "fades out by r = 0.5");
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 1e-6f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void PointWorldSize_MatchesGlPointSize()
        {
            Assert.AreEqual(0.12f * 300f * 2f * Mathf.Tan(19f * Mathf.Deg2Rad) / 1920f, PotaFx.PointWorldSize(0.12f, 38f, 1920f), 1e-7f,
                "efekt.ts: gl_PointSize = size · 300 / depth");
        }

        [Test]
        public void Icosphere_IsARoundDetail1Icosahedron()
        {
            var m = PotaFx.Icosphere(0.62f);
            Assert.AreEqual(42, m.vertexCount, "12 + 30 edge midpoints");
            Assert.AreEqual(80 * 3, m.triangles.Length);
            var v = m.vertices; var n = m.normals;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.AreEqual(0.62f, v[i].magnitude, 1e-4f);
                Assert.Less(Vector3.Distance(n[i], v[i].normalized), 1e-4f, "three PolyhedronGeometry detail > 0: smooth normals");
            }
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var n0 = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                Assert.Greater(Vector3.Dot(n0, v[t[i]] + v[t[i + 1]] + v[t[i + 2]]), 0f, $"face {i / 3} faces outward (Unity front face)");
            }
            Object.DestroyImmediate(m);
        }


        /// <summary>
        /// The shard material carries ShardHeadroom (vertex colours hold tone ÷ 1.35). _BaseColor is a Color property: Unity feeds the
        /// shader its linear value (<c>GetColor(...).linear</c>) whichever setter wrote it — a raw SetVector(1.35) reached the GPU as 1.99.
        /// </summary>
        [Test]
        public void ShardMaterial_FeedsTheShaderTheLinearHeadroom()
        {
            var m = ColorHuntFx.LitMaterial(null, 0.8f, 0f, ColorHuntFx.ShardHeadroom);
            try { Assert.AreEqual(ColorHuntFx.ShardHeadroom, m.GetColor("_BaseColor").linear.r, 0.01f); }
            finally { Object.DestroyImmediate(m); }
        }
    }
}
