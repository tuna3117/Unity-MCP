using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class GiantHoopTests
    {
        private GameObject _root;

        private GiantHoop Build(float groundY = 0f)
        {
            _root = new GameObject("Game");
            _root.transform.position = new Vector3(0f, 8.5f, 30f);
            var hoop = new GameObject("GiantHoop").AddComponent<GiantHoop>();
            hoop.transform.SetParent(_root.transform, false);
            hoop.GroundY = groundY;
            hoop.Build();
            return hoop;
        }

        [Test]
        public void Rim_SitsAtPotasDevPota()
        {
            var h = Build();
            Assert.Less(Vector3.Distance(h.RimCenter, new Vector3(0f, 8.5f - 5.6f, 30f - 2.2f)), 1e-3f, "cemberY −5.6, R + 0.6 toward the camera");
        }

        [Test]
        public void Frame_OuterIs9Point5_AroundAn8Point8Glass()
        {
            var h = Build();
            var glass = h.transform.Find("Glass").GetComponent<Renderer>().bounds;
            Assert.AreEqual(8.8f, glass.size.x, 1e-3f);
            Assert.AreEqual(30.7f, glass.center.z, 1e-3f, "pano plane 0.7 behind the board centre");
            var top = h.transform.Find("FrameTop").GetComponent<Renderer>().bounds;
            Assert.AreEqual(9.5f, top.size.x, 1e-3f, "panoEn + 2·cerceve");
            Assert.AreEqual(8.5f + 4.75f, top.max.y, 1e-3f);
            Assert.AreEqual(0.9f, top.size.z, 1e-3f);
        }

        [Test]
        public void Pole_StandsOnTheGround()
        {
            Assert.AreEqual(0f, Build().transform.Find("Pole").GetComponent<Renderer>().bounds.min.y, 0.02f);
            Object.DestroyImmediate(_root);
            Assert.AreEqual(-3f, Build(-3f).transform.Find("Pole").GetComponent<Renderer>().bounds.min.y, 0.02f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
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
