using System;
using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class FlowTimelineTests
    {
        private static readonly FlowTiming T = new FlowTiming();
        private static readonly int[] EdgeCounts = { 0, 1, 2, 3, 24, 25, 26, 158, 400, 999, 1000, 5000 };

        [Test]
        public void Shares_SumToTheBasketCount_ForEveryCount()
        {
            for (int n = 0; n <= 999; n++)
            {
                int v = Math.Min(n, T.MaxVisible), sum = 0;
                for (int i = 0; i < v; i++) sum += FlowTimeline.Share(i, n, v);
                Assert.AreEqual(n, sum, $"N={n}");
            }
            foreach (int n in new[] { 5000, 100000 })
            {
                int sum = 0;
                for (int i = 0; i < T.MaxVisible; i++) sum += FlowTimeline.Share(i, n, T.MaxVisible);
                Assert.AreEqual(n, sum, $"N={n}");
            }
        }

        [Test]
        public void Counter_EndsExactlyAtN_ForBothCandidates()
        {
            foreach (var c in new[] { FlowCandidate.Stream, FlowCandidate.Simple })
                foreach (int n in EdgeCounts)
                    Assert.AreEqual(n, new FlowTimeline(T, n, c).CountAt(T.Ready), $"{c} N={n}");
        }

        [Test]
        public void Counter_StartsAtZero_AndNeverGoesDown()
        {
            foreach (var c in new[] { FlowCandidate.Stream, FlowCandidate.Simple })
                foreach (int n in EdgeCounts)
                {
                    var tl = new FlowTimeline(T, n, c);
                    Assert.AreEqual(0, tl.CountAt(0f), $"{c} N={n} at 0");
                    int prev = 0;
                    for (float t = 0f; t <= 3f; t += 1f / 120f)
                    {
                        int now = tl.CountAt(t);
                        Assert.GreaterOrEqual(now, prev, $"{c} N={n} went down at t={t:0.000}");
                        prev = now;
                    }
                }
        }

        [Test]
        public void VisibleBalls_AreCappedAt25_AndLandInsideTheWindow()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            Assert.AreEqual(25, tl.Visible);
            Assert.AreEqual(0.25f, tl.LaunchTime(0), 1e-5f);
            Assert.LessOrEqual(tl.LaunchTime(24), 1.10f + 1e-4f);
            Assert.AreEqual(1.15f, tl.LandTime(0), 1e-5f);
            Assert.AreEqual(2.20f, tl.LandTime(24), 1e-4f);
            for (int i = 0; i < 25; i++)
                Assert.Greater(tl.LandTime(i) - tl.LaunchTime(i), 0.85f, $"ball {i} flight too short");
            Assert.AreEqual(3, new FlowTimeline(T, 3, FlowCandidate.Stream).Visible);
            Assert.AreEqual(0, new FlowTimeline(T, 158, FlowCandidate.Simple).Visible, "B has no ball stream");
            Assert.AreEqual(0, new FlowTimeline(T, 0, FlowCandidate.Stream).Visible);
        }

        [Test]
        public void FewBalls_StillArriveEarly()
        {
            var tl = new FlowTimeline(T, 3, FlowCandidate.Stream);
            Assert.Less(tl.LandTime(2), 1.30f, "three balls must not be stretched over the whole landing window");
            Assert.AreEqual(3, tl.CountAt(1.30f));
        }

        [Test]
        public void Camera_StartsAfterTheHighlight_AndArrivesAt190()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.CameraProgress(0.30f), 1e-6f);
            Assert.AreEqual(1f, tl.CameraProgress(1.90f), 1e-6f);
            float prev = 0f;
            for (float t = 0f; t <= 2.5f; t += 0.01f)
            {
                float u = tl.CameraProgress(t);
                Assert.GreaterOrEqual(u, prev - 1e-6f);
                prev = u;
            }
        }

        [Test]
        public void Settle_OnlyRightAfterArrival()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(-1f, tl.SettleAt(1.89f));
            Assert.AreEqual(0.5f, tl.SettleAt(1.96f), 1e-3f);
            Assert.AreEqual(-1f, tl.SettleAt(2.03f));
        }

        [Test]
        public void Highlight_PopsTo125_ThenSettlesAtOne()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.HighlightScale(0f));
            float peak = 0f;
            for (float t = 0f; t <= 0.3f; t += 0.001f) peak = Mathf.Max(peak, tl.HighlightScale(t));
            Assert.That(peak, Is.InRange(1.2f, 1.26f));
            Assert.AreEqual(1f, tl.HighlightScale(0.30f), 1e-5f);
        }

        [Test]
        public void RevealScale_PopsFromZeroThroughAbout112ToOne()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.RevealScale(1.749f, 0));
            float peak = 0f;
            for (float t = 1.75f; t < 2.1f; t += 0.001f) peak = Mathf.Max(peak, tl.RevealScale(t, 0));
            Assert.That(peak, Is.InRange(1.11f, 1.13f));
            Assert.AreEqual(1f, tl.RevealScale(1.75f + T.PopTime, 0), 1e-5f);
            Assert.AreEqual(0f, tl.RevealScale(1.80f, 1), "ring 1 starts 0.07 s after ring 0");
            Assert.AreEqual(1.82f, tl.RingStart(1), 1e-5f);
        }

        [Test]
        public void LastRingHudAndBadge_FinishBeforeReady()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.LessOrEqual(tl.RingStart(3) + T.PopTime, T.Ready, "7×7 has rings 0..3");
            Assert.AreEqual(1f, tl.HudProgress(T.HudStart + T.HudTime), 1e-5f);
            Assert.LessOrEqual(T.HudStart + T.HudTime, T.Ready);
            Assert.LessOrEqual(T.BadgeFlyEnd, T.Ready);
            Assert.LessOrEqual(T.Ready, 2.6f, "spec: playable board within 2.6 s");
        }

        [Test]
        public void Skip_AnyTime_FinishesWithin035Seconds()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            foreach (float t in new[] { 0f, 0.3f, 1f, 2f, 2.45f })
            {
                float speed = tl.SkipSpeed(t);
                Assert.GreaterOrEqual(speed, 6f);
                Assert.LessOrEqual((T.Ready - t) / speed, 0.35f + 1e-4f, $"skip at {t}");
            }
        }

        [Test]
        public void FrameGlow_PulsesWithTheReveal()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(0f, tl.FrameGlow(1.7f));
            Assert.AreEqual(1f, tl.FrameGlow(1.95f), 1e-3f);
            Assert.AreEqual(0f, tl.FrameGlow(2.2f));
        }
    }
}
