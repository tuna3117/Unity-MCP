using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The hero's ball basket on the terrace: the balls from the cage drop into it and pile up (PotaFlow A).</summary>
    public class BallBinTests
    {
        // Level 20 cage mouth front; basket beside the hero (hero at (0, 0, 6.8), terrace rail at z 6.05, y 0.9).
        private static readonly Vector3 Mouth = new Vector3(0f, -20.65f, 1.6f);
        private static readonly Vector3 BinPos = new Vector3(1.4f, 0f, 7.5f);
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private BallBin MakeBin()
        {
            _root = new GameObject("Flow");
            var bin = new GameObject("BallBin").AddComponent<BallBin>();
            bin.transform.SetParent(_root.transform, false);
            bin.transform.position = BinPos;
            bin.Build();
            return bin;
        }

        [Test]
        public void EveryRestingSlot_IsInsideTheBasket_WithoutOverlap()
        {
            var bin = MakeBin();
            var bounds = bin.InnerBounds;
            for (int i = 0; i < bin.RestingCapacity; i++)
            {
                var p = bin.SlotPosition(i);
                Assert.IsTrue(bounds.Contains(p), $"slot {i} at {p} outside {bounds}");
                Assert.GreaterOrEqual(p.y - bin.BallRadius, bounds.min.y - 1e-3f, $"slot {i} sinks through the floor");
                for (int j = 0; j < i; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(p, bin.SlotPosition(j)), 2f * bin.BallRadius * 0.95f, $"slots {j},{i} overlap");
            }
            Assert.GreaterOrEqual(bin.RestingCapacity, 10, "a full-looking basket");
        }

        [Test]
        public void Pile_IsCapped_AndClears()
        {
            var bin = MakeBin();
            for (int i = 0; i < 30; i++) bin.AddBall();
            Assert.AreEqual(bin.RestingCapacity, bin.Pile);
            bin.Clear();
            Assert.AreEqual(0, bin.Pile);
        }

        [Test]
        public void StreamIntoTheBasket_ClearsTheRailAndTheHero_AndDropsStraightIn()
        {
            var bin = MakeBin();
            var s = ScriptableObject.CreateInstance<PotaFlowSettings>();
            try
            {
                for (int i = 0; i < 25; i++)
                {
                    var entry = bin.EntryPoint(i);
                    float apex = Mathf.Max(Mouth.y, entry.y) + s.ApexAbove;
                    for (int k = 0; k <= 400; k++)
                    {
                        var p = BallStream.PathPoint(Mouth, entry, apex, s.LaunchKick, k / 400f, s.BinEndLean);
                        if (p.z >= 5.8f && p.z <= 6.3f && Mathf.Abs(p.x) < 3.6f) Assert.Greater(p.y, 1.2f, $"ball {i} hits the rail");
                        float toHero = new Vector2(p.x, p.z - 6.8f).magnitude;
                        if (toHero < 0.6f) Assert.Greater(p.y, 2.2f, $"ball {i} goes through the hero");
                        if (p.y < 0f) Assert.Less(p.z, 5.8f, $"ball {i} under the terrace");
                    }
                    var d = (entry - BallStream.PathPoint(Mouth, entry, apex, s.LaunchKick, 0.97f, s.BinEndLean)).normalized;
                    Assert.Less(d.y, -0.85f, $"ball {i} must drop into the basket from above");
                    var dir = new Vector2(entry.x - Mouth.x, entry.z - Mouth.z).normalized;
                    Assert.Greater(new Vector2(d.x, d.z).magnitude, 0.2f, $"ball {i} must come in on a slant, not stack in a vertical column");
                    Assert.Greater(Vector2.Dot(new Vector2(d.x, d.z).normalized, dir), 0.5f, $"ball {i} slants the wrong way");
                    Assert.IsTrue(bin.InnerBounds.Contains(entry - Vector3.up * 0.3f), $"ball {i} entry not over the basket");
                }
            }
            finally { Object.DestroyImmediate(s); }
        }

        [Test]
        public void Stream_FillsTheBasket_AndRaisesEveryLandingOnce()
        {
            var bin = MakeBin();
            var stream = new GameObject("BallStream").AddComponent<BallStream>();
            stream.transform.SetParent(_root.transform, false);
            stream.Prewarm(25);
            stream.Begin(new FlowTimeline(new FlowTiming(), 158, FlowCandidate.Stream), Mouth, bin);
            var landed = new List<int>();
            stream.Landed += landed.Add;
            stream.Tick(0f);
            stream.Tick(2.5f);
            CollectionAssert.AreEqual(Enumerable.Range(0, 25).ToList(), landed);
            Assert.AreEqual(bin.RestingCapacity, bin.Pile);
            Assert.AreEqual(bin.RestingCapacity, stream.ActiveCount, "the rest vanish into the pile");
        }
    }
}
