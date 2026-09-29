using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class GiantHoopTests
    {
        private GameObject _root;

        private GiantHoop Build()
        {
            _root = new GameObject("Game");
            _root.transform.position = new Vector3(0f, 8.5f, 30f);
            var hoop = new GameObject("GiantHoop").AddComponent<GiantHoop>();
            hoop.transform.SetParent(_root.transform, false);
            hoop.Build(7);
            return hoop;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Rim_SitsBelowTheBoard_InFrontOfIt()
        {
            var h = Build();
            Assert.Less(Vector3.Distance(h.RimCenter, new Vector3(0f, 8.5f - 3.92f - 1.7f, 27.8f)), 1e-3f);
        }

        [Test]
        public void Pole_StandsOnTheRoof()
        {
            var h = Build();
            var pole = h.transform.Find("Pole").GetComponent<Renderer>();
            Assert.AreEqual(0f, pole.bounds.min.y, 0.02f);
        }

        [Test]
        public void EveryPileSlot_IsInsideTheNet_WithoutOverlap()
        {
            var h = Build();
            for (int i = 0; i < GiantHoop.MaxPile; i++)
            {
                var local = h.Rim.InverseTransformPoint(h.SlotPosition(i));
                float depth = -local.y;
                Assert.That(depth, Is.InRange(h.BallRadius, h.NetDepth - h.BallRadius * 0.9f), $"slot {i} depth");
                float netR = h.RimRadius * (1f - 0.11f * h.NetRings * depth / h.NetDepth);
                float r = new Vector2(local.x, local.z).magnitude;
                Assert.LessOrEqual(r + h.BallRadius, netR + 0.01f, $"slot {i} pokes through the net");
                for (int j = 0; j < i; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(h.SlotPosition(i), h.SlotPosition(j)), 2f * h.BallRadius * 0.95f, $"slots {j},{i} overlap");
            }
        }

        [Test]
        public void Pile_BulgesTheNet_AndClears()
        {
            var h = Build();
            h.AddToPile();
            h.Tick(0.01f);
            Assert.AreEqual(1, h.Pile);
            Assert.Greater(h.Net.localScale.x, 1.05f);
            for (int i = 0; i < 20; i++) h.AddToPile();
            Assert.AreEqual(GiantHoop.MaxPile, h.Pile, "the pile is capped");
            h.ClearPile();
            Assert.AreEqual(0, h.Pile);
            Assert.AreEqual(Vector3.one, h.Net.localScale);
        }
    }
}
