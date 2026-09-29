using System;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>Which transition plays (spec §4): A "Akan toplar" (balls stream into the giant hoop) or B "Sade".</summary>
    public enum FlowCandidate { Stream, Simple }

    /// <summary>
    /// Every time in the basket → board transition, in real (unscaled) seconds from the basket round's end. Tuned from the
    /// spec §4 table after the first recordings (2026-09-30): the balls land by 1.70 so they do not fall in front of the
    /// board's entrance, the camera settles on the hero before flying to the board, and the ammo panel comes in first so
    /// the count is readable while the balls land.
    /// </summary>
    [Serializable]
    public class FlowTiming
    {
        [Header("Highlight")]
        public float HighlightEnd = 0.30f;
        [Header("Balls (A)")]
        public float LaunchStart = 0.25f;
        public float LaunchInterval = 0.025f;
        [Min(1)] public int MaxVisible = 25;
        public float FirstLand = 0.95f;
        public float LastLand = 1.45f;
        [Header("Camera")]
        public float CameraStart = 0.30f;
        [Tooltip("First beat (rise to the vista on the hero) ends here.")]
        public float CameraMid = 1.10f;
        [Tooltip("Second beat (turn and fly to the board) starts here.")]
        public float CameraResume = 1.35f;
        public float CameraEnd = 1.95f;
        public float SettleTime = 0.12f;
        [Header("Hero")]
        [Tooltip("Hero, terrace and his basket reappear (hidden since the shot) before the camera reaches the roof.")]
        public float HeroRevealAt = 0.60f;
        [Tooltip("The hero turns to watch his basket fill.")]
        public float HeroStart = 0.90f;
        public float HeroEnd = 1.25f;
        [Header("Board")]
        public float RevealStart = 1.75f;
        public float RingInterval = 0.07f;
        public float PopTime = 0.28f;
        public float FrameGlowTime = 0.40f;
        [Header("HUD")]
        [Tooltip("Ammo panel slides in before the landings so the count is readable.")]
        public float AmmoStart = 0.90f;
        public float AmmoTime = 0.30f;
        [Tooltip("Target panel, arrows and hint come with the board.")]
        public float HudStart = 1.90f;
        public float HudTime = 0.35f;
        [Header("Count (B)")]
        public float CountStart = 0.95f;
        public float CountEnd = 2.20f;
        [Header("Badge")]
        public float BadgeDockEnd = 0.80f;
        [Tooltip("A: the badge flies into the ammo panel as it arrives, then the landings count up.")]
        public float BadgeFlyStart = 0.92f;
        public float BadgeFlyEnd = 1.15f;
        [Tooltip("B: the badge rides with the camera and flies into the panel on arrival.")]
        public float BadgeFlyStartSimple = 1.95f;
        public float BadgeFlyEndSimple = 2.30f;
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
            if (Candidate == FlowCandidate.Stream && Visible > 0) return (int)((long)LandedAt(t) * Count / Visible);
            float u = Ramp(t, T.CountStart, T.CountEnd);     // B, or A with no visible balls
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

        /// <summary>
        /// 0 → 1 along the camera path in two eased beats: 0 → <paramref name="split"/> (the vista) between CameraStart and
        /// CameraMid, a short hold, then → 1 between CameraResume and CameraEnd. <paramref name="split"/> is the path's u at
        /// the vista (<see cref="CameraFlight.Split"/>).
        /// </summary>
        public float CameraProgress(float t, float split)
        {
            if (t <= T.CameraStart) return 0f;
            if (t < T.CameraMid) return split * Smoother(Ramp(t, T.CameraStart, T.CameraMid));
            if (t <= T.CameraResume) return split;
            if (t >= T.CameraEnd) return 1f;
            return split + (1f - split) * Smoother(Ramp(t, T.CameraResume, T.CameraEnd));
        }

        /// <summary>0 → 1 through the short push-and-settle after arrival; −1 outside it.</summary>
        public float SettleAt(float t) =>
            t <= T.CameraEnd || t >= T.CameraEnd + T.SettleTime ? -1f : (t - T.CameraEnd) / T.SettleTime;

        public float HeroTurn(float t) => Smoother(Ramp(t, T.HeroStart, T.HeroEnd));

        public float RingStart(int ring) => T.RevealStart + ring * T.RingInterval;

        /// <summary>Block scale of a ring: 0 before its start, overshoot to ~1.12, settle at 1 (ease-out-back, s = 1.9).</summary>
        public float RevealScale(float t, int ring)
        {
            if (T.PopTime <= 0f) return t >= RingStart(ring) ? 1f : 0f;
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
            if (T.FrameGlowTime <= 0f) return 0f;
            float u = (t - T.RevealStart) / T.FrameGlowTime;
            return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        public float HudProgress(float t) => Ramp(t, T.HudStart, T.HudStart + T.HudTime);

        public float AmmoProgress(float t) => Ramp(t, T.AmmoStart, T.AmmoStart + T.AmmoTime);

        /// <summary>Badge moving from the cage to its flight spot (0 → 1).</summary>
        public float BadgeDock(float t) => Smoother(Ramp(t, T.HighlightEnd, T.BadgeDockEnd));

        /// <summary>Badge flying into the ammo panel (0 → 1): early in A, on arrival in B.</summary>
        public float BadgeFly(float t)
        {
            float a = Candidate == FlowCandidate.Stream ? T.BadgeFlyStart : T.BadgeFlyStartSimple;
            float b = Candidate == FlowCandidate.Stream ? T.BadgeFlyEnd : T.BadgeFlyEndSimple;
            return Smoother(Ramp(t, a, b));
        }

        /// <summary>Speed after a tap: at least MinSkipSpeed and fast enough to finish within MaxSkipSeconds.</summary>
        public float SkipSpeed(float t) => Mathf.Max(T.MinSkipSpeed, (T.Ready - t) / Mathf.Max(0.01f, T.MaxSkipSeconds));

        public static float Smoother(float u) => u * u * u * (u * (u * 6f - 15f) + 10f);

        /// <summary>0 → 1 as t goes a → b; a zero-length window (settings typed in the Inspector) is a step, never NaN.</summary>
        public static float Ramp(float t, float a, float b) => b > a ? Mathf.Clamp01((t - a) / (b - a)) : (t >= a ? 1f : 0f);
    }
}
