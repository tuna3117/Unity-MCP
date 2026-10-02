using System;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>
    /// Which transition plays: A "Akan toplar" (Stream, Tuna's pick 2026-09-30), B "Sade" (Simple), and the three 2026-10-02 candidates
    /// that start like A (balls into the hero's basket) and then differ: 1 "Büyük şut" (BigShot: the hero shoots the basket's balls
    /// into the giant hoop), 2 "Çemberden dalış" (Dive: the camera chases over the court and rises through the rim),
    /// 3 "Pano damgası" (Stamp: one Pota-like sweep, the count stamps the glass, the blocks rain in).
    /// </summary>
    public enum FlowCandidate { Stream, Simple, BigShot, Dive, Stamp }

    /// <summary>Candidate 1 numbers (spec 2026-10-02 §3.1), real seconds from the basket round's end.</summary>
    [Serializable]
    public class BigShotTiming
    {
        [Tooltip("The hero turns from his basket to the giant hoop.")]
        public float TurnStart = 1.45f, TurnEnd = 1.65f;
        public float ShootAt = 1.55f;
        [Header("Balls: basket → hoop")]
        public float LaunchStart = 1.65f, LaunchInterval = 0.02f;
        [Min(1)] public int MaxVisible = 27;
        public float FirstLand = 2.45f, LastLand = 2.85f;
        [Header("Camera")]
        [Tooltip("Vista → shot pose (behind the hero, hoop ahead).")]
        public float CameraResume = 1.50f, CameraShot = 2.05f;
        [Tooltip("Shot pose → board.")]
        public float CameraPush = 2.55f, CameraEnd = 3.05f;
        [Header("Board and HUD")]
        public float AmmoStart = 2.10f;
        public float RevealStart = 2.60f;
        public float HudStart = 2.85f;
        public float Ready = 3.30f;
    }

    /// <summary>Candidate 2 numbers (spec §3.2).</summary>
    [Serializable]
    public class DiveTiming
    {
        [Tooltip("Chase: vista → low over the court → under the rim.")]
        public float ChaseStart = 1.50f;
        [Tooltip("The camera passes the rim's plane (swish).")]
        public float RimAt = 2.20f;
        [Tooltip("Pull-back to the board pose ends.")]
        public float CameraEnd = 2.90f;
        [Header("Speed lines")]
        public float SpeedFrom = 1.60f, SpeedTo = 2.25f;
        public float SpeedFade = 0.10f;
        [Tooltip("Flipbook loop length (8 frames).")]
        public float SpeedLoop = 0.25f;
        [Header("Swish")]
        public float SwishPopTime = 0.5f;
        public float SwishRingTime = 0.35f;
        [Header("Board and HUD")]
        public float RevealStart = 2.25f;
        public float HudStart = 2.55f;
        public float Ready = 3.10f;
    }

    /// <summary>Candidate 3 numbers (spec §3.3).</summary>
    [Serializable]
    public class StampTiming
    {
        [Tooltip("One sweep vista → board pose, Pota style (kameraYonetmeni: 1.4 s, rise 3.5 m).")]
        public float CameraStart = 1.45f, CameraEnd = 2.15f;
        public float Rise = 3.5f;
        [Header("Stamp")]
        public float StampAt = 2.15f;
        public float StampTime = 0.18f;
        public float StampPeak = 2.2f, StampRest = 1.8f;
        [Header("Block rain")]
        public float RainStart = 2.20f;
        public float RainTime = 0.40f;
        public float ColumnInterval = 0.04f;
        [Header("HUD")]
        public float AmmoStart = 2.40f;
        public float BadgeFlyStart = 2.45f, BadgeFlyEnd = 2.70f;
        public float HudStart = 2.55f;
        public float Ready = 2.90f;
    }

    /// <summary>
    /// Every time in the basket → board transition, in real (unscaled) seconds from the basket round's end. Tuned from the
    /// spec §4 table after the first recordings (2026-09-30): the balls land by 1.70 so they do not fall in front of the
    /// board's entrance, the camera settles on the hero before flying to the board, and the ammo panel comes in first so
    /// the count is readable while the balls land. The 2026-10-02 candidates keep this first part and add their own blocks.
    /// </summary>
    [Serializable]
    public class FlowTiming
    {
        [Header("Highlight")]
        public float HighlightEnd = 0.30f;
        [Header("Balls (A)")]
        public float LaunchStart = 0.25f;
        public float LaunchInterval = 0.022f;
        [Tooltip("Real-size balls (r 0.12): the basket holds 27 (3 × 3 × 3), all of them visibly flown.")]
        [Min(1)] public int MaxVisible = 27;
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
        [Header("Candidates (2026-10-02)")]
        public BigShotTiming BigShot = new BigShotTiming();
        public DiveTiming Dive = new DiveTiming();
        public StampTiming Stamp = new StampTiming();
    }

    /// <summary>When each visible ball of a flight launches and lands (the ball stream places balls from this).</summary>
    public interface IBallSchedule
    {
        int Visible { get; }
        float LaunchTime(int i);
        float LandTime(int i);
    }

    /// <summary>A flight of <see cref="Visible"/> balls: launches every interval from start, landings spread over first..last.</summary>
    public sealed class BallSchedule : IBallSchedule
    {
        public readonly float Start, Interval, First, Last;
        public readonly int MaxVisible;
        public int Visible { get; }

        public BallSchedule(float start, float interval, float first, float last, int visible, int maxVisible)
        {
            Start = start; Interval = interval; First = first; Last = last;
            MaxVisible = Mathf.Max(1, maxVisible);
            Visible = Mathf.Clamp(visible, 0, MaxVisible);
        }

        public float LaunchTime(int i) => Start + i * Interval;

        /// <summary>Spaced as if all MaxVisible balls flew, so a few balls still arrive early.</summary>
        public float LandTime(int i) => First + i * (Last - First) / Mathf.Max(1, MaxVisible - 1);

        public int LandedAt(float t)
        {
            int n = 0;
            while (n < Visible && LandTime(n) <= t) n++;
            return n;
        }
    }

    /// <summary>
    /// The transition for one basket count N as a pure function of t: when each visible ball launches and lands, how much
    /// of N the ammo counter shows, how far the camera has flown, how big each board ring is, how far the HUD has slid.
    /// No Unity objects: the director asks "what is true at t?" every frame, so a skip or a dropped frame cannot desync.
    /// </summary>
    public sealed class FlowTimeline : IBallSchedule
    {
        public readonly FlowTiming T;
        public readonly FlowCandidate Candidate;
        public readonly int Count;
        public readonly int Visible;
        /// <summary>Candidate 1's second flight, basket → giant hoop (Visible 0 for the others).</summary>
        public readonly BallSchedule Net;

        public FlowTimeline(FlowTiming timing, int count, FlowCandidate candidate)
        {
            T = timing;
            Candidate = candidate;
            Count = Mathf.Max(0, count);
            Visible = candidate == FlowCandidate.Simple ? 0 : Mathf.Min(Count, timing.MaxVisible);
            var b = timing.BigShot;
            Net = new BallSchedule(b.LaunchStart, b.LaunchInterval, b.FirstLand, b.LastLand,
                candidate == FlowCandidate.BigShot ? Mathf.Min(Count, b.MaxVisible) : 0, b.MaxVisible);
        }

        int IBallSchedule.Visible => Visible;

        public bool IsBigShot => Candidate == FlowCandidate.BigShot;
        public bool IsDive => Candidate == FlowCandidate.Dive;
        public bool IsStamp => Candidate == FlowCandidate.Stamp;

        /// <summary>Input opens here (per candidate).</summary>
        public float Ready => IsBigShot ? T.BigShot.Ready : IsDive ? T.Dive.Ready : IsStamp ? T.Stamp.Ready : T.Ready;

        /// <summary>When the camera reaches its last pose (the settle follows).</summary>
        public float CameraEndTime => IsBigShot ? T.BigShot.CameraEnd : IsDive ? T.Dive.CameraEnd : IsStamp ? T.Stamp.CameraEnd : T.CameraEnd;

        /// <summary>When the board starts to appear (ring pops; the stamp for 3).</summary>
        public float RevealStartTime => IsBigShot ? T.BigShot.RevealStart : IsDive ? T.Dive.RevealStart : IsStamp ? T.Stamp.StampAt : T.RevealStart;

        public float AmmoStartTime => IsBigShot ? T.BigShot.AmmoStart : IsStamp ? T.Stamp.AmmoStart : T.AmmoStart;

        public float HudStartTime => IsBigShot ? T.BigShot.HudStart : IsDive ? T.Dive.HudStart : IsStamp ? T.Stamp.HudStart : T.HudStart;

        public float LaunchTime(int i) => T.LaunchStart + i * T.LaunchInterval;

        /// <summary>Ball i reaches the basket. Spaced as if all MaxVisible balls flew, so a few balls still arrive early.</summary>
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

        /// <summary>
        /// The ammo counter at t: A (and 2, 3) sums the shares of the balls landed in the basket, 1 the shares of the balls landed in
        /// the giant hoop's net, B counts up with the flight.
        /// </summary>
        public int CountAt(float t)
        {
            if (Count == 0) return 0;
            if (IsBigShot && Net.Visible > 0) return (int)((long)Net.LandedAt(t) * Count / Net.Visible);
            if (Candidate != FlowCandidate.Simple && Visible > 0) return (int)((long)LandedAt(t) * Count / Visible);
            float u = Ramp(t, T.CountStart, T.CountEnd);     // B, or no visible balls
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

        /// <summary>The first beat alone (every candidate shares it): 0 → 1 from CameraStart to CameraMid, 1 after.</summary>
        public float FirstBeat(float t) => Smoother(Ramp(t, T.CameraStart, T.CameraMid));

        /// <summary>Candidate 1: 0 → <paramref name="split"/> (the shot pose) in CameraResume..CameraShot, hold, → 1 in CameraPush..CameraEnd.</summary>
        public float BigShotCamera(float t, float split)
        {
            var b = T.BigShot;
            if (t <= b.CameraResume) return 0f;
            if (t < b.CameraShot) return split * Smoother(Ramp(t, b.CameraResume, b.CameraShot));
            if (t <= b.CameraPush) return split;
            if (t >= b.CameraEnd) return 1f;
            return split + (1f - split) * Smoother(Ramp(t, b.CameraPush, b.CameraEnd));
        }

        /// <summary>
        /// Candidate 2: path u 0 → <paramref name="rimU"/> accelerating through the chase (ChaseStart..RimAt), then
        /// rimU → 1 easing out through the pull-back (RimAt..CameraEnd). Speed is continuous at the rim.
        /// </summary>
        public float DiveCamera(float t, float rimU)
        {
            var d = T.Dive;
            if (t <= d.ChaseStart) return 0f;
            if (t >= d.CameraEnd) return 1f;
            if (t < d.RimAt) { float u = Ramp(t, d.ChaseStart, d.RimAt); return rimU * u * u; }           // ease-in
            float v = Ramp(t, d.RimAt, d.CameraEnd);
            return rimU + (1f - rimU) * (1f - (1f - v) * (1f - v));                                       // ease-out
        }

        /// <summary>Candidate 3: the single sweep's smoothstep 0 → 1 (CameraStart..CameraEnd).</summary>
        public float StampCamera(float t) => Smooth(Ramp(t, T.Stamp.CameraStart, T.Stamp.CameraEnd));

        /// <summary>0 → 1 through the short push-and-settle after arrival; −1 outside it.</summary>
        public float SettleAt(float t)
        {
            float end = CameraEndTime;
            return t <= end || t >= end + T.SettleTime ? -1f : (t - end) / T.SettleTime;
        }

        public float HeroTurn(float t) => Smoother(Ramp(t, T.HeroStart, T.HeroEnd));

        /// <summary>Candidate 1: the hero turns from the basket to the giant hoop (0 → 1).</summary>
        public float HeroTurnHoop(float t) => IsBigShot ? Smoother(Ramp(t, T.BigShot.TurnStart, T.BigShot.TurnEnd)) : 0f;

        public float RingStart(int ring) => RevealStartTime + ring * T.RingInterval;

        /// <summary>Block scale of a ring: 0 before its start, overshoot to ~1.12, settle at 1 (ease-out-back, s = 1.9). Candidate 3 rains instead.</summary>
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

        /// <summary>Frame glow 0 → 1 → 0 starting with the reveal (3: with the stamp).</summary>
        public float FrameGlow(float t)
        {
            if (T.FrameGlowTime <= 0f) return 0f;
            float u = (t - RevealStartTime) / T.FrameGlowTime;
            return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
        }

        public float HudProgress(float t) => Ramp(t, HudStartTime, HudStartTime + T.HudTime);

        public float AmmoProgress(float t) => Ramp(t, AmmoStartTime, AmmoStartTime + T.AmmoTime);

        /// <summary>Badge moving from the cage to its flight spot (0 → 1).</summary>
        public float BadgeDock(float t) => Smoother(Ramp(t, T.HighlightEnd, T.BadgeDockEnd));

        /// <summary>Badge flying into the ammo panel (0 → 1): early in A and 2, on arrival in B, with the first net landing in 1, after the stamp in 3.</summary>
        public float BadgeFly(float t)
        {
            float a, b;
            switch (Candidate)
            {
                case FlowCandidate.Simple: a = T.BadgeFlyStartSimple; b = T.BadgeFlyEndSimple; break;
                case FlowCandidate.BigShot: a = T.BigShot.FirstLand - 0.22f; b = T.BigShot.FirstLand + 0.03f; break;
                case FlowCandidate.Stamp: a = T.Stamp.BadgeFlyStart; b = T.Stamp.BadgeFlyEnd; break;
                default: a = T.BadgeFlyStart; b = T.BadgeFlyEnd; break;
            }
            return Smoother(Ramp(t, a, b));
        }

        // ---------- candidate 2 ----------

        /// <summary>Speed-line strength 0..1: fades in at SpeedFrom, out at SpeedTo.</summary>
        public float SpeedLinesAlpha(float t)
        {
            var d = T.Dive;
            if (!IsDive || t <= d.SpeedFrom || t >= d.SpeedTo) return 0f;
            float fade = Mathf.Max(1e-3f, d.SpeedFade);
            return Mathf.Min(Mathf.Clamp01((t - d.SpeedFrom) / fade), Mathf.Clamp01((d.SpeedTo - t) / fade));
        }

        /// <summary>Speed-line flipbook frame (0..frames−1) at t, looping.</summary>
        public int SpeedLinesFrame(float t, int frames)
        {
            float loop = Mathf.Max(1e-3f, T.Dive.SpeedLoop);
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(t / loop, 1f) * frames), 0, Mathf.Max(0, frames - 1));
        }

        /// <summary>SWISH! lettering after the rim: x = scale (0 before; pops 0 → 1.15 → 1), y = alpha (fades over the last 30 %).</summary>
        public Vector2 SwishLetter(float t)
        {
            var d = T.Dive;
            float life = Mathf.Max(1e-3f, d.SwishPopTime);
            float u = (t - d.RimAt) / life;
            if (!IsDive || u <= 0f || u >= 1f) return Vector2.zero;
            float scale = u < 0.25f ? 1.15f * (1f - (1f - u / 0.25f) * (1f - u / 0.25f)) : Mathf.Lerp(1.15f, 1f, Mathf.Min(1f, (u - 0.25f) / 0.25f));
            float alpha = u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f;
            return new Vector2(scale, alpha);
        }

        /// <summary>Swish ring at the rim: 0..1 of its life after RimAt, −1 outside.</summary>
        public float SwishRing(float t)
        {
            var d = T.Dive;
            float u = (t - d.RimAt) / Mathf.Max(1e-3f, d.SwishRingTime);
            return !IsDive || u <= 0f || u >= 1f ? -1f : u;
        }

        // ---------- candidate 3 ----------

        /// <summary>Badge scale through the stamp: 1 before StampAt, 1 → peak → rest over StampTime, rest after.</summary>
        public float StampScale(float t)
        {
            var s = T.Stamp;
            if (!IsStamp || t <= s.StampAt) return 1f;
            float time = Mathf.Max(1e-3f, s.StampTime);
            float u = (t - s.StampAt) / time;
            if (u >= 1f) return s.StampRest;
            if (u < 0.4f) { float a = u / 0.4f; return Mathf.Lerp(1f, s.StampPeak, 1f - (1f - a) * (1f - a)); }
            return Mathf.Lerp(s.StampPeak, s.StampRest, (u - 0.4f) / 0.6f);
        }

        /// <summary>True once the stamp has hit (the puff and the glow fire then).</summary>
        public bool Stamped(float t) => IsStamp && t >= T.Stamp.StampAt;

        public float RainStart(int col) => T.Stamp.RainStart + col * T.Stamp.ColumnInterval;

        /// <summary>Column <paramref name="col"/>'s blocks (0..1 of their fall): 0 before its start, 1 landed.</summary>
        public float RainProgress(float t, int col) => Ramp(t, RainStart(col), RainStart(col) + T.Stamp.RainTime);

        /// <summary>How far above home a block of column <paramref name="col"/> hangs at t: (n + 1) cells down to 0 with constant acceleration.</summary>
        public float RainOffset(float t, int col, int boardSize)
        {
            float u = RainProgress(t, col);
            return (boardSize + 1) * (1f - u * u);
        }

        /// <summary>Visible once its column starts falling (hidden above the sky before that).</summary>
        public bool RainVisible(float t, int col) => t >= RainStart(col);

        /// <summary>Landing squash 1 → 0 after the column lands (BlockView's own decay rate).</summary>
        public float RainSquash(float t, int col)
        {
            float land = RainStart(col) + T.Stamp.RainTime;
            return t < land ? 0f : Mathf.Max(0f, 1f - (t - land) * 6f);
        }

        // ---------- end ----------

        /// <summary>Speed after a tap: at least MinSkipSpeed and fast enough to finish within MaxSkipSeconds.</summary>
        public float SkipSpeed(float t) => Mathf.Max(T.MinSkipSpeed, (Ready - t) / Mathf.Max(0.01f, T.MaxSkipSeconds));

        public static float Smoother(float u) => u * u * u * (u * (u * 6f - 15f) + 10f);

        public static float Smooth(float u) => u * u * (3f - 2f * u);

        /// <summary>0 → 1 as t goes a → b; a zero-length window (settings typed in the Inspector) is a step, never NaN.</summary>
        public static float Ramp(float t, float a, float b) => b > a ? Mathf.Clamp01((t - a) / (b - a)) : (t >= a ? 1f : 0f);
    }
}
