using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class BallStreamTests
    {
        // Level 20: cage mouth front (0, −20.65, 1.6); giant hoop at BoardAnchor (0, 8.5, 30) → rim (0, 2.88, 27.8).
        private static readonly Vector3 Mouth = new Vector3(0f, -20.65f, 1.6f);
        private static readonly Vector3 RimPoint = new Vector3(0f, 2.88f, 27.8f);
        private static readonly Vector3 Kick = new Vector3(0f, 0f, 0.8f);
        private const float Apex = 2.88f + 9f;

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private BallStream Make(int count, out GiantHoop hoop)
        {
            _root = new GameObject("Flow");
            var game = new GameObject("Game");
            game.transform.SetParent(_root.transform, false);
            game.transform.position = new Vector3(0f, 8.5f, 30f);
            hoop = new GameObject("GiantHoop").AddComponent<GiantHoop>();
            hoop.transform.SetParent(game.transform, false);
            hoop.Build(7);
            var stream = new GameObject("BallStream").AddComponent<BallStream>();
            stream.transform.SetParent(_root.transform, false);
            stream.Prewarm(25);
            stream.Begin(new FlowTimeline(new FlowTiming(), count, FlowCandidate.Stream), Mouth, hoop);
            return stream;
        }

        [Test]
        public void Path_StartsAndEndsExactly_AndDropsStraightIntoTheRim()
        {
            Assert.Less(Vector3.Distance(BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 0f), Mouth), 1e-4f);
            Assert.Less(Vector3.Distance(BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 1f), RimPoint), 1e-4f);
            var d = (BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 1f) - BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, 0.97f)).normalized;
            Assert.Less(d.y, -0.95f, "the ball must come down through the rim, not slide in sideways");
        }

        [Test]
        public void Path_TopIsNearTheApex()
        {
            float top = float.MinValue;
            for (int i = 0; i <= 1000; i++) top = Mathf.Max(top, BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, i / 1000f).y);
            Assert.AreEqual(Apex, top, 0.6f);
        }

        [Test]
        public void Path_ClearsTheRoofAndTheHero()
        {
            for (int i = 0; i <= 1000; i++)
            {
                var p = BallStream.PathPoint(Mouth, RimPoint, Apex, Kick, i / 1000f);
                if (p.z >= 8.4f && p.z <= 26f) Assert.Greater(p.y, 5f, $"too close to the court at u={i / 1000f}");
                if (p.z >= 6f && p.z < 8.4f) Assert.Greater(p.y, 2.2f, $"through the hero on the terrace at u={i / 1000f}");
                if (p.y < 0f) Assert.Less(p.z, 6f, "must rise above the roof in front of the terrace");
            }
        }

        [Test]
        public void Tick_RaisesEveryLandingOnce_EvenWithOneBigStep()
        {
            var stream = Make(158, out var hoop);
            var landed = new List<int>();
            stream.Landed += landed.Add;
            stream.Tick(0f);
            stream.Tick(0.5f);
            stream.Tick(2.5f);
            CollectionAssert.AreEqual(Enumerable.Range(0, 25).ToList(), landed);
            stream.Tick(2.6f);
            Assert.AreEqual(25, landed.Count, "no landing raised twice");
            Assert.AreEqual(GiantHoop.MaxPile, hoop.Pile);
        }

        [Test]
        public void AfterTheLastLanding_TenBallsRestInTheirSlots()
        {
            var stream = Make(158, out var hoop);
            stream.Tick(0f);
            stream.Tick(2.5f);
            Assert.AreEqual(GiantHoop.MaxPile, stream.ActiveCount);
            var shown = stream.GetComponentsInChildren<MeshRenderer>(false);
            Assert.AreEqual(GiantHoop.MaxPile, shown.Length);
            for (int i = 0; i < GiantHoop.MaxPile; i++)
                Assert.IsTrue(shown.Any(r => Vector3.Distance(r.transform.position, hoop.SlotPosition(i)) < 1e-3f), $"slot {i} empty");
        }

        [Test]
        public void Launches_FollowTheTimeline()
        {
            var stream = Make(158, out _);
            stream.Tick(0.2f);
            Assert.AreEqual(0, stream.ActiveCount);
            stream.Tick(0.26f);
            Assert.AreEqual(1, stream.ActiveCount);
            stream.Tick(1.10f);
            Assert.AreEqual(25, stream.ActiveCount);
        }

        [Test]
        public void ThreeBaskets_ThreeBalls_AllResting()
        {
            var stream = Make(3, out var hoop);
            stream.Tick(0f);
            stream.Tick(2.5f);
            Assert.AreEqual(3, stream.ActiveCount);
            Assert.AreEqual(3, hoop.Pile);
            stream.Clear();
            Assert.AreEqual(0, stream.ActiveCount);
        }
    }
}
