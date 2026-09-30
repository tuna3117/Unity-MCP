using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class StudioCurvesTests
    {
        private static float Max(System.Func<float, float> f)
        {
            float m = float.MinValue;
            for (int i = 0; i <= 200; i++) m = Mathf.Max(m, f(i / 200f));
            return m;
        }

        private static float Min(System.Func<float, float> f)
        {
            float m = float.MaxValue;
            for (int i = 0; i <= 200; i++) m = Mathf.Min(m, f(i / 200f));
            return m;
        }

        [Test]
        public void Launch_GrowsTo1Point4_ThenSettlesOn1Point2_WobbleDiesOut()
        {
            Assert.AreEqual(0.9f, StudioCurves.LaunchScale(0f), 1e-5f);
            Assert.AreEqual(1.4f, StudioCurves.LaunchScale(0.6f), 1e-5f);
            Assert.AreEqual(1.2f, StudioCurves.LaunchScale(1f), 1e-5f);
            Assert.AreEqual(1.4f, Max(StudioCurves.LaunchScale), 1e-3f);
            Assert.AreEqual(0f, StudioCurves.LaunchWobble(0f), 1e-5f);
            Assert.AreEqual(0f, StudioCurves.LaunchWobble(1f), 1e-4f);
            Assert.LessOrEqual(Max(u => Mathf.Abs(StudioCurves.LaunchWobble(u))), 12f);
        }

        [Test]
        public void Side_FansLeftAndRight_Widening_Capped()
        {
            Assert.AreEqual(0.6f, StudioCurves.Side(0), 1e-5f);
            Assert.AreEqual(-0.6f, StudioCurves.Side(1), 1e-5f);
            Assert.AreEqual(0.8f, StudioCurves.Side(2), 1e-5f);
            Assert.AreEqual(-0.8f, StudioCurves.Side(3), 1e-5f);
            Assert.AreEqual(1.2f, StudioCurves.Side(20), 1e-5f);
        }

        [Test]
        public void FlightPoint_StartsAndEndsOnTheEnds_AcceleratesIn_AndBowsToItsSide()
        {
            var from = new Vector2(0f, 0f);
            var to = new Vector2(0f, 100f);
            Assert.AreEqual(from, StudioCurves.FlightPoint(from, to, 1f, 0f));
            Assert.AreEqual(to, StudioCurves.FlightPoint(from, to, 1f, 1f));
            Assert.Less(StudioCurves.FlightPoint(from, to, 0f, 0.5f).y, 50f, "eased in: slower at the start");
            Assert.Less(StudioCurves.FlightPoint(from, to, 1f, 0.5f).x, 0f, "+side bows to the chord's left");
            Assert.Greater(StudioCurves.FlightPoint(from, to, -1f, 0.5f).x, 0f);
            Assert.AreEqual(1.2f, StudioCurves.FlightScale(0f), 1e-5f);
            Assert.AreEqual(0.55f, StudioCurves.FlightScale(1f), 1e-5f);
        }

        [Test]
        public void Arrival_SquashesWide_ThenSpringsBackToOne()
        {
            Assert.AreEqual(Vector2.one, StudioCurves.ArrivalSquash(0f));
            var early = StudioCurves.ArrivalSquash(0.2f);
            Assert.Greater(early.x, 1.2f);
            Assert.Less(early.y, 0.85f);
            var end = StudioCurves.ArrivalSquash(1f);
            Assert.AreEqual(1f, end.x, 1e-4f);
            Assert.AreEqual(1f, end.y, 1e-4f);
        }

        [Test]
        public void CounterRoll_OldGoesUpAndOut_NewComesUpFromBelow()
        {
            Assert.AreEqual(new Vector2(0f, -1f), StudioCurves.CounterRoll(0f));
            Assert.AreEqual(new Vector2(1f, 0f), StudioCurves.CounterRoll(1f));
            Assert.AreEqual(1f, StudioCurves.CounterFlash(0f), 1e-5f);
            Assert.AreEqual(0f, StudioCurves.CounterFlash(1f), 1e-5f);
        }

        [Test]
        public void Stamp_SlamsFrom1Point6_UndershootsTo0Point9_LandsOnOne()
        {
            Assert.AreEqual(1.6f, StudioCurves.Stamp(0f), 1e-5f);
            Assert.AreEqual(0.9f, Min(StudioCurves.Stamp), 1e-3f);
            Assert.AreEqual(1f, StudioCurves.Stamp(1f), 1e-5f);
            Assert.AreEqual(1f, StudioCurves.StampAlpha(0.25f), 1e-5f);
        }

        [Test]
        public void Press_SinksTo0Point92_ReleaseOvershootsAndSettles()
        {
            Assert.AreEqual(1f, StudioCurves.PressScale(0f), 1e-5f);
            Assert.AreEqual(0.92f, StudioCurves.PressScale(1f), 1e-5f);
            Assert.AreEqual(0.92f, StudioCurves.ReleaseScale(0f, 0.92f), 1e-5f);
            Assert.AreEqual(1f, StudioCurves.ReleaseScale(1f, 0.92f), 1e-5f);
            Assert.Greater(Max(u => StudioCurves.ReleaseScale(u, 0.92f)), 1.03f, "a springy overshoot");
            Assert.Less(Max(u => StudioCurves.ReleaseScale(u, 0.92f)), 1.06f);
        }

        [Test]
        public void Intro_PopsFromZeroThrough1Point15_BottomRowFirst_Within0Point6s()
        {
            Assert.AreEqual(0f, StudioCurves.IntroScale(0f), 1e-5f);
            Assert.AreEqual(1f, StudioCurves.IntroScale(1f), 1e-5f);
            Assert.AreEqual(1.15f, Max(StudioCurves.IntroScale), 1e-3f);
            Assert.AreEqual(0f, StudioCurves.IntroDelay(0), 1e-5f);
            Assert.AreEqual(0.18f, StudioCurves.IntroDelay(6), 1e-5f);
            Assert.LessOrEqual(StudioCurves.IntroTotal(7), 0.6f);
        }

        [Test]
        public void Hint_WigglesTwiceWithin4Degrees_AndStops()
        {
            Assert.AreEqual(0f, StudioCurves.HintWiggle(0f), 1e-5f);
            Assert.AreEqual(0f, StudioCurves.HintWiggle(1f), 1e-4f);
            Assert.LessOrEqual(Max(u => Mathf.Abs(StudioCurves.HintWiggle(u))), 4f);
            Assert.Greater(Max(u => Mathf.Abs(StudioCurves.HintWiggle(u))), 2f);
            Assert.AreEqual(5f, StudioCurves.HintDelay);
        }

        [Test]
        public void LowAmmo_IsLessThanTwoCheapestBlocks()
        {
            Assert.IsTrue(StudioCurves.LowAmmo(9, 5));
            Assert.IsFalse(StudioCurves.LowAmmo(10, 5));
            Assert.IsFalse(StudioCurves.LowAmmo(3, 0), "no block: nothing to warn about");
        }

        [Test]
        public void CollectPitch_RisesSixPercentPerArrival_CapsAtSeven()
        {
            Assert.AreEqual(1f, StudioCurves.CollectPitch(0), 1e-5f);
            Assert.AreEqual(1.12f, StudioCurves.CollectPitch(2), 1e-5f);
            Assert.AreEqual(1.42f, StudioCurves.CollectPitch(30), 1e-5f);
        }

        [Test]
        public void CandyWords_GrowWithDepth_Capped()
        {
            Assert.AreEqual(60f, StudioCurves.WordSizeCss(2), 1e-5f);
            Assert.AreEqual(76f, StudioCurves.WordSizeCss(9), 1e-5f);
            Assert.AreEqual(1.25f, Max(StudioCurves.WordScale), 1e-3f);
            Assert.AreEqual(0f, StudioCurves.WordAlpha(1f), 1e-5f);
            Assert.AreEqual(0.55f, StudioCurves.EdgeGlowAlpha(9), 1e-5f);
        }
    }
}
