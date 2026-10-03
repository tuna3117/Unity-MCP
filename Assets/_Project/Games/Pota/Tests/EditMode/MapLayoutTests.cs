using NUnit.Framework;
using Project.Meta;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The map's node positions and the "Sen" pin's place (Pota haritaYer.ts), in css px.</summary>
    public class MapLayoutTests
    {
        private const float W = 390f;

        [Test]
        public void Node_MatchesPotasFormula()
        {
            var n1 = MapLayout.Node(1, W, 50);
            Assert.AreEqual(W / 2f + Mathf.Sin(0.85f) * W * 0.3f, n1.x, 0.01f);
            Assert.AreEqual(90f + 49f * 118f, n1.y, 0.01f);
            var n2 = MapLayout.Node(2, W, 50);
            Assert.AreEqual(W / 2f + Mathf.Sin(1.7f) * W * 0.3f, n2.x, 0.01f);
            Assert.AreEqual(90f + 48f * 118f, n2.y, 0.01f);
            Assert.AreEqual(90f, MapLayout.Node(50, W, 50).y, 0.01f, "the last node sits at the top");
            Assert.AreEqual(90f + 49f * 118f + MapLayout.Bottom, MapLayout.ContentHeight(50), 0.01f);
        }

        [Test]
        public void SenYeri_NeverOverlapsANode_StaysInsideTheWidth()
        {
            for (int n = 1; n <= 50; n++)
            {
                var k = MapLayout.Node(n, W, 50);
                Vector2? prev = n > 1 ? MapLayout.Node(n - 1, W, 50) : (Vector2?)null;
                Vector2? next = n < 50 ? MapLayout.Node(n + 1, W, 50) : (Vector2?)null;
                var p = MapLayout.SenYeri(k, prev, next, W);
                Assert.GreaterOrEqual(p.x - MapLayout.SenWidth / 2f, 0f, $"node {n}: pin off the left edge");
                Assert.LessOrEqual(p.x + MapLayout.SenWidth / 2f, W, $"node {n}: pin off the right edge");
                foreach (var d in new[] { prev, k, next })
                    if (d.HasValue) Assert.Greater(MapLayout.BoxDistance(d.Value, p.x, p.y), MapLayout.NodeRadius, $"node {n}: pin on a node");
            }
        }

        [Test]
        public void SenYeri_DefaultsBesideTheNode_OnTheSideThePathDoesNotTake()
        {
            var k = MapLayout.Node(25, W, 50);           // mid-map: the default spot is free of the edge, the nodes and the path
            var prev = MapLayout.Node(24, W, 50);
            var next = MapLayout.Node(26, W, 50);
            var p = MapLayout.SenYeri(k, prev, next, W);
            float side = next.x > k.x ? -1f : 1f;
            Assert.AreEqual(k.x + side * 78f, p.x, 0.01f);
            Assert.AreEqual(k.y - 8f, p.y, 0.01f);
        }

        [Test]
        public void SenYeri_MovesToTheOtherSide_WhenTheDefaultLeavesTheScreen()
        {
            var k = MapLayout.Node(10, W, 50);           // x ≈ 288: 78 to the right runs off the 390 width
            var p = MapLayout.SenYeri(k, MapLayout.Node(9, W, 50), MapLayout.Node(11, W, 50), W);
            Assert.Less(p.x, k.x);
        }

        [Test]
        public void Dots_FollowTheSegmentEvery25Px()
        {
            var a = new Vector2(0f, 0f); var b = new Vector2(0f, 100f);
            var dots = MapLayout.Dots(a, b);
            Assert.AreEqual(3, dots.Length, "100 px: dots at 25, 50, 75 (the ends are nodes)");
            Assert.AreEqual(25f, dots[0].y, 0.01f);
            Assert.AreEqual(75f, dots[2].y, 0.01f);
        }
    }
}
