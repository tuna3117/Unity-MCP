using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class FeelCurvesTests
    {
        [Test]
        public void LeftoverCoins_IsPotaFormula()
        {
            Assert.AreEqual(0, FeelCurves.LeftoverCoins(0));
            Assert.AreEqual(0, FeelCurves.LeftoverCoins(9));
            Assert.AreEqual(1, FeelCurves.LeftoverCoins(10));
            Assert.AreEqual(14, FeelCurves.LeftoverCoins(140));
            Assert.AreEqual(20, FeelCurves.LeftoverCoins(400));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(6, 6)]
        [TestCase(7, 7)]
        [TestCase(14, 14)]
        [TestCase(15, 14)]
        [TestCase(140, 14)]
        public void FinalePlan_FlightCount(int remaining, int flights) =>
            Assert.AreEqual(flights, FeelCurves.FinalePlan(remaining, 6, 14).Length);

        [TestCase(1)]
        [TestCase(13)]
        [TestCase(140)]
        [TestCase(157)]
        public void FinalePlan_SumsToRemaining_EveryFlightCarriesABall(int n)
        {
            int sum = 0;
            foreach (var balls in FeelCurves.FinalePlan(n, 6, 14)) { Assert.Greater(balls, 0); sum += balls; }
            Assert.AreEqual(n, sum);
        }

        [Test]
        public void LandBounce_SquashesThenOvershootsThenRests()
        {
            var a = FeelCurves.LandBounce(0f);
            Assert.Less(a.y, 0.9f);
            Assert.Greater(a.x, 1.05f);
            Assert.Greater(FeelCurves.LandBounce(0.6f).y, 1.02f);
            Assert.AreEqual(1f, FeelCurves.LandBounce(1f).y, 1e-4f);
            Assert.AreEqual(1f, FeelCurves.LandBounce(1f).x, 1e-4f);
        }

        [Test]
        public void Lean_IsMonotonicAndBounded()
        {
            float last = -1f;
            for (int i = 0; i <= 10; i++) { float v = FeelCurves.Lean(i / 10f); Assert.GreaterOrEqual(v, last); last = v; }
            Assert.AreEqual(0f, FeelCurves.Lean(0f), 1e-4f);
            Assert.AreEqual(1f, last, 1e-4f);
        }

        [Test]
        public void Stamp_PeaksThenRests()
        {
            Assert.AreEqual(1f, FeelCurves.Stamp(0f, 2.2f, 1.8f), 1e-4f);
            Assert.Greater(FeelCurves.Stamp(0.35f, 2.2f, 1.8f), 2.0f);
            Assert.AreEqual(1.8f, FeelCurves.Stamp(1f, 2.2f, 1.8f), 1e-3f);
        }

        [Test]
        public void RibbonScale_OvershootsOnce()
        {
            Assert.AreEqual(0f, FeelCurves.RibbonScale(0f), 1e-4f);
            Assert.Greater(FeelCurves.RibbonScale(0.6f), 1.05f);
            Assert.AreEqual(1f, FeelCurves.RibbonScale(1f), 1e-4f);
        }

        [Test]
        public void Breath_IsAUnitSineAtTheGivenRate()
        {
            Assert.AreEqual(0f, FeelCurves.Breath(0f, 1f), 1e-4f);
            Assert.AreEqual(1f, FeelCurves.Breath(0.5f, 1f), 1e-4f);
            Assert.AreEqual(0f, FeelCurves.Breath(1f, 1f), 1e-4f);
            Assert.AreEqual(1f, FeelCurves.Breath(0.25f, 2f), 1e-4f);
        }

        [Test]
        public void HintDelay_IsZeroForTheLastAffordableBlockUnderTension()
        {
            Assert.AreEqual(StudioCurves.HintDelay, FeelCurves.HintDelay(5, true), 1e-4f);
            Assert.AreEqual(StudioCurves.HintDelay, FeelCurves.HintDelay(1, false), 1e-4f);
            Assert.AreEqual(0f, FeelCurves.HintDelay(1, true), 1e-4f);
        }

        [Test]
        public void CloudPose_StaysOnItsRing_AndDrifts()
        {
            var a = FeelCurves.CloudPose(2, 0f, 6, 90f, 0.5f);
            var b = FeelCurves.CloudPose(2, 10f, 6, 90f, 0.5f);
            Assert.AreEqual(90f, new Vector2(a.x, a.z).magnitude, 1e-3f);
            Assert.AreEqual(90f, new Vector2(b.x, b.z).magnitude, 1e-3f);
            Assert.AreEqual(5f, Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)), 0.05f, "0.5 m/s along the ring for 10 s");
            Assert.Less(Mathf.Abs(a.y), 0.5f, "the bob is small");
            var other = FeelCurves.CloudPose(3, 0f, 6, 90f, 0.5f);
            Assert.Greater(Vector3.Distance(a, other), 30f, "clouds are spread around the ring");
        }

        [Test]
        public void BirdPass_IsMinusOneWhileResting_ThenRunsZeroToOne()
        {
            Assert.AreEqual(-1f, FeelCurves.BirdPass(1f, 4f, 22f, 9f), 1e-4f, "before the first pass");
            Assert.AreEqual(0f, FeelCurves.BirdPass(4f, 4f, 22f, 9f), 1e-4f);
            Assert.AreEqual(0.5f, FeelCurves.BirdPass(8.5f, 4f, 22f, 9f), 1e-4f);
            Assert.AreEqual(-1f, FeelCurves.BirdPass(14f, 4f, 22f, 9f), 1e-4f, "resting between passes");
            Assert.AreEqual(0f, FeelCurves.BirdPass(26f, 4f, 22f, 9f), 1e-4f, "the next pass a period later");
        }

        [Test]
        public void Settings_HaveFeelBlocksOnByDefault()
        {
            Assert.IsTrue(ScriptableObject.CreateInstance<ColorHuntSettings>().Feel.Enabled);
            Assert.IsTrue(ScriptableObject.CreateInstance<Project.Basket.BasketSettings>().Feel.Enabled);
            Assert.AreEqual(3, ScriptableObject.CreateInstance<ColorHuntSettings>().Feel.WordFromDepth);
        }

        [Test]
        public void Word_UnderThePotaLook_FromDepthThree_OnlyWithFeel()
        {
            var s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsNull(PotaBurstStyle.Word(s, 2));
            Assert.AreEqual("Süper!", PotaBurstStyle.Word(s, 3));
            Assert.AreEqual("Muhteşem!", PotaBurstStyle.Word(s, 6));
            s.Feel.Enabled = false;
            Assert.IsNull(PotaBurstStyle.Word(s, 4));
        }
    }
}
