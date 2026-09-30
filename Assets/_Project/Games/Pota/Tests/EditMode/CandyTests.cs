using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class CandyTests
    {
        [Test]
        public void Words_GrowWithTheChain()
        {
            Assert.IsNull(CandyHud.WordFor(1));
            Assert.AreEqual("İyi!", CandyHud.WordFor(2));
            Assert.AreEqual("Süper!", CandyHud.WordFor(3));
            Assert.AreEqual("Harika!", CandyHud.WordFor(4));
            Assert.AreEqual("Muhteşem!", CandyHud.WordFor(5));
            Assert.AreEqual("Muhteşem!", CandyHud.WordFor(9));
            Assert.AreNotEqual(CandyHud.WordColor(2), CandyHud.WordColor(5));
        }

        [Test]
        public void RayStar_HasACore_EightRays_AndClearCorners()
        {
            var t = StudioTextures.RayStar(128, 8);
            Assert.Greater(t.GetPixel(64, 64).a, 0.95f, "core");
            Assert.Less(t.GetPixel(0, 0).a, 0.02f, "corner");
            Assert.Greater(t.GetPixel(64 + 44, 64).a, 0.5f, "a ray on the x axis");
            float between = t.GetPixel(64 + Mathf.RoundToInt(44 * Mathf.Cos(Mathf.PI / 8f)), 64 + Mathf.RoundToInt(44 * Mathf.Sin(Mathf.PI / 8f))).a;
            Assert.Less(between, 0.15f, "dark between two rays");
        }

        [Test]
        public void EdgeGlow_IsClearInTheMiddle_BrightAtTheEdges()
        {
            var t = StudioTextures.EdgeGlow(256);
            Assert.Less(t.GetPixel(128, 128).a, 0.01f);
            Assert.Greater(t.GetPixel(1, 128).a, 0.8f);
            Assert.Greater(t.GetPixel(128, 254).a, 0.8f);
        }

        [Test]
        public void CandyChunk_IsAFacetedIcosahedron_FacingOut()
        {
            var m = StudioTextures.CandyChunk();
            Assert.AreEqual(60, m.vertexCount);
            Assert.AreEqual(60, m.triangles.Length);
            var v = m.vertices;
            var n = m.normals;
            var tri = m.triangles;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Assert.AreEqual(n[tri[i]], n[tri[i + 1]], "flat facets");
                Vector3 centre = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
                Assert.Greater(Vector3.Dot(n[tri[i]], centre), 0f, "normals face out");
                Vector3 wound = Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]);
                Assert.Greater(Vector3.Dot(wound, n[tri[i]]), 0f, "Unity winding matches the normal");
            }
        }

        [Test]
        public void WithoutTheFlipbooks_CandyIsNotReady()
        {
            var go = new GameObject("Game");
            try
            {
                var game = go.AddComponent<ColorHuntGame>();
                game.S = ScriptableObject.CreateInstance<ColorHuntSettings>();
                Assert.IsFalse(CandyBurst.AssetsReady(game.S));
                var styleGo = new GameObject("CandyBurst");
                styleGo.transform.SetParent(go.transform);
                var candy = styleGo.AddComponent<CandyBurst>();
                candy.Build(game);
                Assert.IsFalse(candy.Ready);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
