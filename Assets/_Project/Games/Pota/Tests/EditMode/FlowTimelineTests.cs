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
            Assert.AreEqual(14, tl.Visible, "fewer, separate balls read better than a chain (spec cap 25)");
            Assert.LessOrEqual(tl.Visible, 25);
            Assert.AreEqual(0.25f, tl.LaunchTime(0), 1e-5f);
            Assert.LessOrEqual(tl.LaunchTime(tl.Visible - 1), 0.85f + 1e-4f);
            Assert.AreEqual(0.95f, tl.LandTime(0), 1e-5f, "balls land in the hero's basket while the camera shows him");
            Assert.AreEqual(1.45f, tl.LandTime(tl.Visible - 1), 1e-4f);
            for (int i = 0; i < tl.Visible; i++)
                Assert.Greater(tl.LandTime(i) - tl.LaunchTime(i), 0.55f, $"ball {i} flight too short");
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
        public void Camera_RisesToTheVista_Pauses_ThenFliesToTheBoard()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            const float split = 0.68f;
            Assert.AreEqual(0f, tl.CameraProgress(0.30f, split), 1e-6f);
            Assert.AreEqual(split, tl.CameraProgress(T.CameraMid, split), 1e-5f, "first beat ends on the vista");
            Assert.AreEqual(split, tl.CameraProgress((T.CameraMid + T.CameraResume) * 0.5f, split), 1e-5f, "short hold on the vista");
            Assert.AreEqual(1f, tl.CameraProgress(T.CameraEnd, split), 1e-6f);
            float prev = 0f;
            for (float t = 0f; t <= 2.5f; t += 0.01f)
            {
                float u = tl.CameraProgress(t, split);
                Assert.GreaterOrEqual(u, prev - 1e-6f);
                prev = u;
            }
        }

        [Test]
        public void Landings_HappenWhileTheCameraIsOnTheHero()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            Assert.GreaterOrEqual(tl.LandTime(0), T.HeroRevealAt, "the basket must be visible before the first ball lands");
            Assert.LessOrEqual(tl.LandTime(0), T.CameraMid, "first balls land as the camera settles on the hero");
            Assert.LessOrEqual(tl.LandTime(tl.Visible - 1), T.CameraResume + 0.15f, "last balls land before the camera has turned away");
        }

        [Test]
        public void Landings_FinishBeforeTheBoardAppears()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            Assert.LessOrEqual(tl.LandTime(tl.Visible - 1), T.RevealStart, "falling balls would hide the board's entrance");
        }

        [Test]
        public void AmmoPanel_IsInBeforeTheCountRuns_TheRestComesWithTheBoard()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stream);
            Assert.LessOrEqual(T.AmmoStart, T.FirstLand, "the count must be readable while balls land");
            Assert.AreEqual(1f, tl.AmmoProgress(T.AmmoStart + T.AmmoTime), 1e-5f);
            Assert.AreEqual(0f, tl.HudProgress(T.AmmoStart + T.AmmoTime), 1e-5f, "targets and arrows wait for the board");
            var b = new FlowTimeline(T, 158, FlowCandidate.Simple);
            Assert.AreEqual(0, b.CountAt(T.CountStart));
            Assert.Greater(b.CountAt(T.AmmoStart + T.AmmoTime), 0, "B counts while the panel is in");
        }

        [Test]
        public void Badge_FliesEarlyInA_AtArrivalInB()
        {
            var a = new FlowTimeline(T, 158, FlowCandidate.Stream);
            var b = new FlowTimeline(T, 158, FlowCandidate.Simple);
            Assert.AreEqual(1f, a.BadgeFly(T.BadgeFlyEnd), 1e-5f);
            Assert.AreEqual(0f, b.BadgeFly(T.BadgeFlyEnd), 1e-5f);
            Assert.AreEqual(1f, b.BadgeFly(T.BadgeFlyEndSimple), 1e-5f);
        }

        [Test]
        public void Settle_OnlyRightAfterArrival()
        {
            var tl = new FlowTimeline(T, 10, FlowCandidate.Stream);
            Assert.AreEqual(-1f, tl.SettleAt(T.CameraEnd - 0.01f));
            Assert.AreEqual(0.5f, tl.SettleAt(T.CameraEnd + T.SettleTime * 0.5f), 1e-3f);
            Assert.AreEqual(-1f, tl.SettleAt(T.CameraEnd + T.SettleTime + 0.01f));
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
        public void DegenerateSettings_NeverDivideByZero()
        {
            // Values someone may type into PotaFlowSettings: no balls, zero-length windows.
            var t = new FlowTiming { MaxVisible = 0, HeroEnd = 0.95f, HeroStart = 0.95f, CountStart = 1f, CountEnd = 1f, PopTime = 0f,
                FrameGlowTime = 0f, AmmoTime = 0f, HudTime = 0f, CameraMid = 0.30f, CameraResume = 1.9f, MaxSkipSeconds = 0f };
            foreach (var c in new[] { FlowCandidate.Stream, FlowCandidate.Simple })
            {
                var tl = new FlowTimeline(t, 158, c);
                for (float x = 0f; x <= 3f; x += 0.05f)
                {
                    foreach (float v in new[] { tl.CameraProgress(x, 0.6f), tl.HeroTurn(x), tl.RevealScale(x, 1), tl.FrameGlow(x),
                                 tl.AmmoProgress(x), tl.HudProgress(x), tl.BadgeDock(x), tl.BadgeFly(x), tl.SkipSpeed(x) })
                        Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), $"{c} t={x}");
                    Assert.DoesNotThrow(() => tl.CountAt(x));
                }
                Assert.AreEqual(158, tl.CountAt(t.Ready), $"{c} still ends at N");
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

        // ---------- 2026-10-02 candidates ----------

        private static readonly FlowCandidate[] All = { FlowCandidate.Stream, FlowCandidate.Simple, FlowCandidate.BigShot, FlowCandidate.Dive, FlowCandidate.Stamp };
        private static readonly FlowCandidate[] New = { FlowCandidate.BigShot, FlowCandidate.Dive, FlowCandidate.Stamp };

        [Test]
        public void Candidates_CounterEndsAtN_AndNeverDrops()
        {
            foreach (var c in New)
                foreach (int n in EdgeCounts)
                {
                    var tl = new FlowTimeline(T, n, c);
                    Assert.AreEqual(0, tl.CountAt(0f), $"{c} N={n} at 0");
                    int prev = 0;
                    for (float t = 0f; t <= tl.Ready + 0.5f; t += 1f / 120f)
                    {
                        int now = tl.CountAt(t);
                        Assert.GreaterOrEqual(now, prev, $"{c} N={n} went down at t={t:0.000}");
                        prev = now;
                    }
                    Assert.AreEqual(n, tl.CountAt(tl.Ready), $"{c} N={n} at ready");
                }
        }

        [Test]
        public void Candidates_ShareTheFirstSecondWithA()
        {
            var a = new FlowTimeline(T, 158, FlowCandidate.Stream);
            foreach (var c in New)
            {
                var tl = new FlowTimeline(T, 158, c);
                Assert.AreEqual(a.Visible, tl.Visible, $"{c}: same balls into the basket");
                for (int i = 0; i < tl.Visible; i++)
                {
                    Assert.AreEqual(a.LaunchTime(i), tl.LaunchTime(i), 1e-6f);
                    Assert.AreEqual(a.LandTime(i), tl.LandTime(i), 1e-6f);
                }
                for (float t = 0f; t <= T.CameraMid; t += 0.01f)
                {
                    Assert.AreEqual(a.FirstBeat(t), tl.FirstBeat(t), 1e-6f, $"{c} camera at {t}");
                    Assert.AreEqual(a.HeroTurn(t), tl.HeroTurn(t), 1e-6f);
                    Assert.AreEqual(a.HighlightScale(t), tl.HighlightScale(t), 1e-6f);
                    Assert.AreEqual(a.BadgeDock(t), tl.BadgeDock(t), 1e-6f);
                }
            }
        }

        [Test]
        public void BigShot_SecondFlight_TakesTheBasketsPile_AndCountsOnlyAtTheNet()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.BigShot);
            var b = T.BigShot;
            Assert.AreEqual(12, tl.Net.Visible, "the basket holds 12 resting balls");
            Assert.AreEqual(3, new FlowTimeline(T, 3, FlowCandidate.BigShot).Net.Visible);
            Assert.AreEqual(0, new FlowTimeline(T, 158, FlowCandidate.Stream).Net.Visible, "only candidate 1 shoots again");
            Assert.GreaterOrEqual(b.LaunchStart, T.LastLand, "the pile must be complete before it flies");
            Assert.GreaterOrEqual(b.TurnStart, T.HeroEnd, "the hero finishes turning to the basket first");
            Assert.LessOrEqual(b.ShootAt, b.LaunchStart);
            Assert.AreEqual(b.FirstLand, tl.Net.LandTime(0), 1e-5f);
            Assert.AreEqual(b.LastLand, tl.Net.LandTime(tl.Net.Visible - 1), 1e-4f);
            for (int i = 0; i < tl.Net.Visible; i++) Assert.Greater(tl.Net.LandTime(i) - tl.Net.LaunchTime(i), 0.5f, $"ball {i}");
            Assert.AreEqual(0, tl.CountAt(T.LastLand + 0.2f), "basket landings do not count in 1");
            Assert.AreEqual(0, tl.CountAt(b.FirstLand - 0.01f));
            Assert.Greater(tl.CountAt(b.FirstLand), 0);
            Assert.AreEqual(158, tl.CountAt(b.LastLand));
            Assert.LessOrEqual(b.AmmoStart + T.AmmoTime, b.FirstLand, "the panel is in before the first net landing");
            Assert.AreEqual(1f, tl.BadgeFly(b.FirstLand + 0.03f), 1e-5f, "the badge merges with the first net landing");
            Assert.AreEqual(1f, tl.HeroTurnHoop(b.TurnEnd), 1e-5f);
            Assert.AreEqual(0f, new FlowTimeline(T, 158, FlowCandidate.Dive).HeroTurnHoop(3f));
        }

        [Test]
        public void BigShot_Camera_HoldsOnTheShotPose_WhileTheBallsFly()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.BigShot);
            var b = T.BigShot;
            const float split = 0.4f;
            Assert.AreEqual(0f, tl.BigShotCamera(b.CameraResume, split));
            Assert.AreEqual(split, tl.BigShotCamera(b.CameraShot, split), 1e-5f);
            Assert.AreEqual(split, tl.BigShotCamera((b.CameraShot + b.CameraPush) * 0.5f, split), 1e-5f);
            Assert.AreEqual(1f, tl.BigShotCamera(b.CameraEnd, split), 1e-5f);
            Assert.LessOrEqual(b.CameraShot, b.LaunchStart + 0.4f, "the shot pose is reached as the balls leave");
            Assert.AreEqual(b.CameraEnd, tl.CameraEndTime);
            Assert.AreEqual(b.Ready, tl.Ready);
            Assert.LessOrEqual(tl.RingStart(3) + T.PopTime, tl.Ready);
            Assert.LessOrEqual(b.HudStart + T.HudTime, tl.Ready);
        }

        [Test]
        public void Dive_Camera_AcceleratesToTheRim_ThenEasesOut_WithSpeedLinesAndSwish()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Dive);
            var d = T.Dive;
            const float rimU = 0.7f;
            Assert.AreEqual(0f, tl.DiveCamera(d.ChaseStart, rimU));
            Assert.AreEqual(rimU, tl.DiveCamera(d.RimAt, rimU), 1e-5f, "the rim is crossed exactly at RimAt");
            Assert.AreEqual(1f, tl.DiveCamera(d.CameraEnd, rimU), 1e-5f);
            float prev = 0f;
            for (float t = 0f; t <= 3.2f; t += 0.005f) { float u = tl.DiveCamera(t, rimU); Assert.GreaterOrEqual(u, prev - 1e-6f); prev = u; }
            float early = tl.DiveCamera(d.ChaseStart + 0.1f, rimU), late = tl.DiveCamera(d.RimAt - 0.1f, rimU);
            Assert.Less(early, late - early, "accelerating: the last 0.1 s before the rim covers more than the first");
            Assert.AreEqual(0f, tl.SpeedLinesAlpha(d.SpeedFrom));
            Assert.AreEqual(1f, tl.SpeedLinesAlpha((d.SpeedFrom + d.SpeedTo) * 0.5f), 1e-5f);
            Assert.AreEqual(0f, tl.SpeedLinesAlpha(d.SpeedTo));
            Assert.AreEqual(0f, new FlowTimeline(T, 158, FlowCandidate.Stream).SpeedLinesAlpha(1.9f), "only the dive has speed lines");
            Assert.AreEqual(0, tl.SpeedLinesFrame(0f, 8));
            Assert.AreEqual(7, tl.SpeedLinesFrame(d.SpeedLoop * 0.99f, 8));
            Assert.AreEqual(0, tl.SpeedLinesFrame(d.SpeedLoop, 8), "loops");
            Assert.AreEqual(Vector2.zero, tl.SwishLetter(d.RimAt - 0.01f));
            float peak = 0f;
            for (float t = d.RimAt; t < d.RimAt + d.SwishPopTime; t += 0.002f) peak = Mathf.Max(peak, tl.SwishLetter(t).x);
            Assert.That(peak, Is.InRange(1.1f, 1.2f));
            Assert.AreEqual(1f, tl.SwishLetter(d.RimAt + d.SwishPopTime * 0.5f).y, 1e-5f);
            Assert.AreEqual(Vector2.zero, tl.SwishLetter(d.RimAt + d.SwishPopTime + 0.01f));
            Assert.AreEqual(-1f, tl.SwishRing(d.RimAt - 0.01f));
            Assert.AreEqual(0.5f, tl.SwishRing(d.RimAt + d.SwishRingTime * 0.5f), 1e-4f);
            Assert.AreEqual(d.RevealStart, tl.RevealStartTime);
            Assert.GreaterOrEqual(d.RevealStart, d.RimAt, "the blocks pop while the camera pulls back");
            Assert.LessOrEqual(tl.RingStart(3) + T.PopTime, tl.Ready);
            Assert.AreEqual(d.Ready, tl.Ready);
        }

        [Test]
        public void Stamp_Sweep_Stamp_Rain_ThenTheBadgeFlies()
        {
            var tl = new FlowTimeline(T, 158, FlowCandidate.Stamp);
            var s = T.Stamp;
            Assert.AreEqual(0f, tl.StampCamera(s.CameraStart));
            Assert.AreEqual(0.5f, tl.StampCamera((s.CameraStart + s.CameraEnd) * 0.5f), 1e-5f);
            Assert.AreEqual(1f, tl.StampCamera(s.CameraEnd), 1e-5f);
            Assert.AreEqual(1f, tl.StampScale(s.StampAt - 0.01f));
            float peak = 0f;
            for (float t = s.StampAt; t <= s.StampAt + s.StampTime; t += 0.001f) peak = Mathf.Max(peak, tl.StampScale(t));
            Assert.AreEqual(s.StampPeak, peak, 0.02f);
            Assert.AreEqual(s.StampRest, tl.StampScale(s.StampAt + s.StampTime + 0.01f), 1e-5f);
            Assert.IsFalse(tl.Stamped(s.StampAt - 0.01f));
            Assert.IsTrue(tl.Stamped(s.StampAt));
            Assert.AreEqual(s.StampAt, tl.RevealStartTime, "the glow fires with the stamp");
            Assert.GreaterOrEqual(s.RainStart, s.StampAt);
            Assert.AreEqual(8f, tl.RainOffset(s.RainStart, 0, 7), 1e-5f, "7×7: hangs 8 cells above home");
            Assert.AreEqual(0f, tl.RainOffset(s.RainStart + s.RainTime, 0, 7), 1e-5f);
            float half = tl.RainOffset(s.RainStart + s.RainTime * 0.5f, 0, 7);
            Assert.Greater(half, 4f, "constant acceleration: slow at first");
            Assert.IsFalse(tl.RainVisible(s.RainStart - 0.01f, 0));
            Assert.IsTrue(tl.RainVisible(s.RainStart, 0));
            Assert.IsFalse(tl.RainVisible(s.RainStart, 1), "columns start one after another");
            Assert.AreEqual(8f, tl.RainOffset(s.RainStart + s.ColumnInterval, 1, 7), 1e-5f);
            Assert.AreEqual(0f, tl.RainSquash(s.RainStart + s.RainTime - 0.01f, 0));
            Assert.AreEqual(1f, tl.RainSquash(s.RainStart + s.RainTime, 0), 1e-5f);
            Assert.Less(tl.RainSquash(s.RainStart + s.RainTime + 0.1f, 0), 0.5f);
            Assert.LessOrEqual(tl.RainStart(6) + s.RainTime, tl.Ready, "the last column lands before input opens");
            Assert.AreEqual(0f, tl.BadgeFly(s.BadgeFlyStart));
            Assert.AreEqual(1f, tl.BadgeFly(s.BadgeFlyEnd), 1e-5f);
            Assert.GreaterOrEqual(s.BadgeFlyStart, s.StampAt + s.StampTime, "the stamp rests before it flies");
            Assert.LessOrEqual(s.AmmoStart + T.AmmoTime, s.BadgeFlyEnd, "the panel is in when the badge arrives");
            Assert.AreEqual(158, tl.CountAt(s.AmmoStart), "the count is complete (basket landings) before the panel shows");
            Assert.AreEqual(s.Ready, tl.Ready);
        }

        [Test]
        public void Candidates_Skip_AnyTime_FinishesWithin035Seconds()
        {
            foreach (var c in All)
            {
                var tl = new FlowTimeline(T, 158, c);
                for (float t = 0f; t < tl.Ready; t += 0.1f)
                {
                    float speed = tl.SkipSpeed(t);
                    Assert.GreaterOrEqual(speed, 6f);
                    Assert.LessOrEqual((tl.Ready - t) / speed, 0.35f + 1e-4f, $"{c} skip at {t}");
                }
            }
        }

        [Test]
        public void Candidates_DegenerateSettings_NeverDivideByZero()
        {
            var t = new FlowTiming { MaxVisible = 0, PopTime = 0f, FrameGlowTime = 0f, AmmoTime = 0f, HudTime = 0f, MaxSkipSeconds = 0f };
            t.BigShot.MaxVisible = 1; t.BigShot.CameraShot = t.BigShot.CameraResume; t.BigShot.LastLand = t.BigShot.FirstLand;
            t.Dive.RimAt = t.Dive.ChaseStart; t.Dive.SpeedFade = 0f; t.Dive.SpeedLoop = 0f; t.Dive.SwishPopTime = 0f; t.Dive.SwishRingTime = 0f;
            t.Stamp.StampTime = 0f; t.Stamp.RainTime = 0f; t.Stamp.CameraEnd = t.Stamp.CameraStart;
            foreach (var c in New)
            {
                var tl = new FlowTimeline(t, 158, c);
                for (float x = 0f; x <= 4f; x += 0.05f)
                {
                    foreach (float v in new[] { tl.BigShotCamera(x, 0.5f), tl.DiveCamera(x, 0.6f), tl.StampCamera(x), tl.HeroTurnHoop(x), tl.RevealScale(x, 1),
                                 tl.FrameGlow(x), tl.AmmoProgress(x), tl.HudProgress(x), tl.BadgeFly(x), tl.SkipSpeed(x), tl.SpeedLinesAlpha(x),
                                 tl.SwishLetter(x).x, tl.SwishRing(x), tl.StampScale(x), tl.RainOffset(x, 3, 7), tl.RainSquash(x, 2), tl.SettleAt(x) })
                        Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), $"{c} t={x}");
                    Assert.DoesNotThrow(() => tl.CountAt(x));
                    Assert.DoesNotThrow(() => tl.SpeedLinesFrame(x, 8));
                }
                Assert.AreEqual(158, tl.CountAt(tl.Ready), $"{c} still ends at N");
            }
        }
    }
}
