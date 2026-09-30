using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The studio polish's numbers and pure curves (spec 2026-09-30 studio polish §3). Times are real seconds; u ∈ [0, 1] is a phase's
    /// progress; canvas distances are CSS px of the 390-px phone (× ColorHuntHud.Css on the canvas).
    /// </summary>
    public static class StudioCurves
    {
        // Sticker flight (§3.1)
        public const float LaunchTime = 0.12f, HangTime = 0.06f, TravelTime = 0.5f, StaggerTime = 0.05f;
        public const float StreamGap = 0.3f, LaunchLiftCss = 26f, SparkEvery = 0.035f, StreakWindow = 1f;
        public const float LaunchStart = 0.9f, LaunchPeak = 1.4f, LaunchEnd = 1.2f, ArrivalScale = 0.55f;
        public const float SquashTime = 0.22f, RingTime = 0.3f, RollTime = 0.18f, StampTime = 0.3f, GlowTime = 0.5f;
        // Press, intro, hint, ammo (§3.2–3.5)
        public const float PressTime = 0.08f, ReleaseTime = 0.2f, PressDepth = 0.08f;
        public const float IntroRowDelay = 0.03f, IntroTime = 0.3f, IntroPeak = 1.15f;
        public const float HintDelay = 5f, HintTime = 0.6f, HintAngle = 4f;
        public const float PulseHz = 1f;
        // Candy (A) chain (§2 A)
        public const float WordTime = 0.6f, WordRiseCss = 30f, EdgeTime = 0.35f;

        public static float EaseOutCubic(float u)
        {
            float v = 1f - Mathf.Clamp01(u);
            return 1f - v * v * v;
        }

        /// <summary>0.9 → 1.4 (eased out, at 60 % of the launch) → settles on 1.2.</summary>
        public static float LaunchScale(float u)
        {
            u = Mathf.Clamp01(u);
            return u < 0.6f ? Mathf.Lerp(LaunchStart, LaunchPeak, EaseOutCubic(u / 0.6f))
                            : Mathf.Lerp(LaunchPeak, LaunchEnd, PotaLook.Smoothstep((u - 0.6f) / 0.4f));
        }

        /// <summary>A ±12° wobble that dies out by the end of the launch.</summary>
        public static float LaunchWobble(float u)
        {
            u = Mathf.Clamp01(u);
            return 12f * Mathf.Sin(u * 2f * Mathf.PI) * (1f - u);
        }

        /// <summary>Stream order → fan side: +0.6, −0.6, +0.8, −0.8, … capped at ±1.2.</summary>
        public static float Side(int index)
        {
            float m = Mathf.Min(1.2f, 0.6f + 0.2f * (index / 2));
            return index % 2 == 0 ? m : -m;
        }

        /// <summary>Quadratic Bezier from → to, the control point off the chord by side·0.35·length (+side = the chord's left); eased in (u²).</summary>
        public static Vector2 FlightPoint(Vector2 from, Vector2 to, float side, float u)
        {
            float p = Mathf.Clamp01(u);
            p *= p;
            Vector2 control = FlightControl(from, to, side);
            float q = 1f - p;
            return q * q * from + 2f * q * p * control + p * p * to;
        }

        /// <summary>The bow's control point: the chord's middle pushed sideways by side · 0.35 · chord length.</summary>
        public static Vector2 FlightControl(Vector2 from, Vector2 to, float side)
        {
            Vector2 chord = to - from;
            Vector2 normal = chord.sqrMagnitude > 1e-6f ? new Vector2(-chord.y, chord.x).normalized : Vector2.zero;
            return (from + to) * 0.5f + normal * (side * 0.35f * chord.magnitude);
        }

        /// <summary>The side whose bow stays inside ±half less margin: as asked, else mirrored, else straight.</summary>
        public static float FitSide(Vector2 from, Vector2 to, float side, Vector2 half, float margin)
        {
            if (Inside(FlightControl(from, to, side), half, margin)) return side;
            if (Inside(FlightControl(from, to, -side), half, margin)) return -side;
            return 0f;
        }

        private static bool Inside(Vector2 p, Vector2 half, float margin) =>
            Mathf.Abs(p.x) <= half.x - margin && Mathf.Abs(p.y) <= half.y - margin;

        public static float FlightScale(float u) => Mathf.Lerp(LaunchEnd, ArrivalScale, Mathf.Pow(Mathf.Clamp01(u), 1.5f));
        public static float FlightTilt(float side, float u) => -side * 14f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

        /// <summary>Icon squash and stretch on arrival: wide and low early, springs back through tall to (1, 1).</summary>
        public static Vector2 ArrivalSquash(float u)
        {
            u = Mathf.Clamp01(u);
            float k = Mathf.Sin(u * Mathf.PI * 2.5f) * (1f - u);
            return new Vector2(1f + 0.35f * k, 1f - 0.25f * k);
        }

        /// <summary>Counter roll offsets in text heights: (old, new) — the old number slides up and out, the new one comes up from below.</summary>
        public static Vector2 CounterRoll(float u)
        {
            float e = EaseOutCubic(u);
            return new Vector2(e, e - 1f);
        }

        public static float CounterFlash(float u) => 1f - Mathf.Clamp01(u);

        /// <summary>✓ stamp: 1.6 → 0.9 (accelerating, half the time) → 1.</summary>
        public static float Stamp(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.5f) { float a = u / 0.5f; return Mathf.Lerp(1.6f, 0.9f, a * a); }
            return Mathf.Lerp(0.9f, 1f, PotaLook.Smoothstep((u - 0.5f) / 0.5f));
        }

        public static float StampAlpha(float u) => Mathf.Clamp01(u * 4f);
        public static float RingScale(float u) => Mathf.Lerp(0.6f, 1.9f, EaseOutCubic(u));
        public static float RingAlpha(float u) => 0.9f * (1f - Mathf.Clamp01(u));
        public static float GlowAlpha(float u) => 0.9f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

        public static float PressScale(float p) => 1f - PressDepth * EaseOutCubic(p);

        /// <summary>Release from <paramref name="from"/>: overshoots to ~1.04 and settles on 1.</summary>
        public static float ReleaseScale(float u, float from)
        {
            u = Mathf.Clamp01(u);
            return 1f - (1f - from) * (1f - u) * Mathf.Cos(2.2f * Mathf.PI * u);
        }

        public static float IntroDelay(int rowFromBottom) => IntroRowDelay * rowFromBottom;

        /// <summary>0 → 1.15 (eased out, 70 % of the time) → 1.</summary>
        public static float IntroScale(float u)
        {
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            return u < 0.7f ? Mathf.Lerp(0f, IntroPeak, EaseOutCubic(u / 0.7f))
                            : Mathf.Lerp(IntroPeak, 1f, PotaLook.Smoothstep((u - 0.7f) / 0.3f));
        }

        public static float IntroTotal(int size) => IntroDelay(size - 1) + IntroTime;

        public static float HintWiggle(float u)
        {
            u = Mathf.Clamp01(u);
            return HintAngle * Mathf.Sin(u * 4f * Mathf.PI) * (1f - u);
        }

        public static float HintGlow(float u) => 0.35f * Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

        public static float Pulse(float time) => 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * PulseHz * time);

        /// <summary>Plan decision 5: warn while the ammo covers less than two of the cheapest block (the last affordable tap).</summary>
        public static bool LowAmmo(int ammo, int cheapest) => cheapest > 0 && ammo < 2 * cheapest;

        public static float CollectPitch(int streak) => 1f + 0.06f * Mathf.Min(Mathf.Max(0, streak), 7);

        public static float WordSizeCss(int depth) => Mathf.Min(76f, 44f + 8f * depth);

        public static float WordScale(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.2f) return Mathf.Lerp(0.3f, 1.25f, EaseOutCubic(u / 0.2f));
            if (u < 0.35f) return Mathf.Lerp(1.25f, 1f, (u - 0.2f) / 0.15f);
            return 1f;
        }

        public static float WordAlpha(float u)
        {
            u = Mathf.Clamp01(u);
            return u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f;
        }

        public static float EdgeGlowAlpha(int depth) => Mathf.Min(0.55f, 0.18f + 0.08f * depth);
        public static float CandyShake(int depth) => 0.03f * depth;
    }
}
