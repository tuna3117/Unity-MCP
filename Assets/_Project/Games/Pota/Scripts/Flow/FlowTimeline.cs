using System;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>Which transition plays (spec §4): A "Akan toplar" (balls stream into the giant hoop) or B "Sade".</summary>
    public enum FlowCandidate { Stream, Simple }

    /// <summary>Every time in the basket → board transition, in real (unscaled) seconds from the basket round's end.</summary>
    [Serializable]
    public class FlowTiming
    {
        [Header("Highlight")]
        public float HighlightEnd = 0.30f;
        [Header("Balls (A)")]
        public float LaunchStart = 0.25f;
        public float LaunchInterval = 0.035f;
        public int MaxVisible = 25;
        public float FirstLand = 1.15f;
        public float LastLand = 2.20f;
        [Header("Camera")]
        public float CameraStart = 0.30f;
        public float CameraEnd = 1.90f;
        public float SettleTime = 0.12f;
        [Header("Hero")]
        public float HeroStart = 0.40f;
        public float HeroEnd = 0.90f;
        [Header("Board")]
        public float RevealStart = 1.75f;
        public float RingInterval = 0.07f;
        public float PopTime = 0.28f;
        public float FrameGlowTime = 0.40f;
        [Header("HUD")]
        public float HudStart = 1.90f;
        public float HudTime = 0.35f;
        [Header("Count (B)")]
        public float CountEnd = 2.20f;
        [Header("Badge")]
        public float BadgeDockEnd = 0.80f;
        public float BadgeFlyStart = 1.95f;
        public float BadgeFlyEnd = 2.30f;
        [Header("End")]
        public float Ready = 2.50f;
        public float MinSkipSpeed = 6f;
        public float MaxSkipSeconds = 0.35f;
        [Header("Return")]
        public float ReturnTime = 1.20f;
    }

    /// <summary>
    /// The transition for one basket count N as a pure function of t: when each visible ball launches and lands, how much
    /// of N the ammo counter shows, how far the camera has flown, how big each board ring is, how far the HUD has slid.
    /// No Unity objects: the director asks "what is true at t?" every frame, so a skip or a dropped frame cannot desync.
    /// </summary>
    public sealed class FlowTimeline
    {
        public readonly FlowTiming T;
        public readonly FlowCandidate Candidate;
        public readonly int Count;
        public readonly int Visible;

        public FlowTimeline(FlowTiming timing, int count, FlowCandidate candidate)
        {
            T = timing;
            Candidate = candidate;
            Count = Mathf.Max(0, count);
            Visible = candidate == FlowCandidate.Stream ? Mathf.Min(Count, timing.MaxVisible) : 0;
        }

        public float Ready => T.Ready;

        public float LaunchTime(int i) => T.LaunchStart + i * T.LaunchInterval;

        /// <summary>Ball i reaches the rim. Spaced as if all MaxVisible balls flew, so a few balls still arrive early.</summary>
        public float LandTime(int i) => T.FirstLand + i * (T.LastLand - T.FirstLand) / Mathf.Max(1, T.MaxVisible - 1);

        /// <summary>Baskets carried by ball i: floor((i+1)N/V) − floor(iN/V), so the shares always sum to exactly N.</summary>
        public static int Share(int i, int count, int visible)
        {
            if (visible <= 0) return 0;
            return (int)((long)(i + 1) * count / visible - (long)i * count / visible);
        }

        public int LandedAt(float t)
        {
            int n = 0;
            while (n < Visible && LandTime(n) <= t) n++;
            return n;
        }

        /// <summary>The ammo counter at t: A sums the shares of the landed balls, B counts up with the flight.</summary>
        public int CountAt(float t)
        {
            if (Count == 0) return 0;
            if (Candidate == FlowCandidate.Stream) return (int)((long)LandedAt(t) * Count / Visible);
            float u = Mathf.Clamp01((t - T.CameraStart) / (T.CountEnd - T.CameraStart));
            return Mathf.RoundToInt(Count * u * u * (3f - 2f * u));
        }

        /// <summary>"N TOP" pop at the cage: 0 → 1.25 → 1 over the highlight.</summary>
        public float HighlightScale(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= T.HighlightEnd) return 1f;
            float u = t / T.HighlightEnd;
            if (u < 0.6f) { float a = u / 0.6f; return 1.25f * (1f - (1f - a) * (1f - a)); }
            return Mathf.Lerp(1.25f, 1f, (u - 0.6f) / 0.4f);
        }

        /// <summary>0 → 1 along the camera path, eased in and out, between CameraStart and CameraEnd.</summary>
        public float CameraProgress(float t) => Smoother(Mathf.Clamp01((t - T.CameraStart) / (T.CameraEnd - T.CameraStart)));

        /// <summary>0 → 1 through the short push-and-settle after arrival; −1 outside it.</summary>
        public float SettleAt(float t) =>
            t <= T.CameraEnd || t >= T.CameraEnd + T.SettleTime ? -1f : (t - T.CameraEnd) / T.SettleTime;

        public float HeroTurn(float t) => Smoother(Mathf.Clamp01((t - T.HeroStart) / (T.HeroEnd - T.HeroStart)));

        public float RingStart(int ring) => T.RevealStart + ring * T.RingInterval;

        /// <summary>Block scale of a ring: 0 before its start, overshoot to ~1.12, settle at 1 (ease-out-back, s = 1.9).</summary>
        public float RevealScale(float t, int ring)
        {
            float u = (t - RingStart(ring)) / T.PopTime;
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            const float s = 1.9f;                      // peak = 1 + 4s³ / (27(s+1)²) ≈ 1.121
            float x = u - 1f;
            return 1f + (s + 1f) * x * x * x + s * x * x;
        }

        /// <summary>Frame glow 0 → 1 → 0 starting with the reveal.</summary>
        public float FrameGlow(float t)
        {
            float u = (t - T.RevealStart) / T.FrameGlowTime;
            return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        public float HudProgress(float t) => Mathf.Clamp01((t - T.HudStart) / T.HudTime);

        /// <summary>Badge moving from the cage to its flight spot (0 → 1).</summary>
        public float BadgeDock(float t) => Smoother(Mathf.Clamp01((t - T.HighlightEnd) / (T.BadgeDockEnd - T.HighlightEnd)));

        /// <summary>Badge flying into the ammo panel (0 → 1).</summary>
        public float BadgeFly(float t) => Smoother(Mathf.Clamp01((t - T.BadgeFlyStart) / (T.BadgeFlyEnd - T.BadgeFlyStart)));

        /// <summary>Speed after a tap: at least MinSkipSpeed and fast enough to finish within MaxSkipSeconds.</summary>
        public float SkipSpeed(float t) => Mathf.Max(T.MinSkipSpeed, (T.Ready - t) / T.MaxSkipSeconds);

        public static float Smoother(float u) => u * u * u * (u * (u * 6f - 15f) + 10f);
    }
}
