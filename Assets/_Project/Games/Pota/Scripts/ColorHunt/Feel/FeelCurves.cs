using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pure numbers and curves of the in-game feel layer (spec 2026-10-03). u ∈ [0, 1] is a phase's progress; everything here is
    /// deterministic and EditMode-tested (FeelCurvesTests).
    /// </summary>
    public static class FeelCurves
    {
        /// <summary>Pota akis.ts ODUL: leftover ammo → coins, min(20, floor(remaining × 0.1)).</summary>
        public static int LeftoverCoins(int remaining) => Mathf.Min(20, Mathf.FloorToInt(Mathf.Max(0, remaining) * 0.1f));

        /// <summary>
        /// F3: balls per flight. remaining ≤ minFlights → one ball per flight; otherwise clamp(remaining, minFlights, maxFlights) flights
        /// sharing the balls as evenly as possible (the remainder on the first flights). Empty for 0.
        /// </summary>
        public static int[] FinalePlan(int remaining, int minFlights, int maxFlights)
        {
            if (remaining <= 0) return new int[0];
            int flights = remaining <= minFlights ? remaining : Mathf.Clamp(remaining, minFlights, maxFlights);
            var plan = new int[flights];
            int each = remaining / flights, extra = remaining - each * flights;
            for (int i = 0; i < flights; i++) plan[i] = each + (i < extra ? 1 : 0);
            return plan;
        }

        /// <summary>F6: landing scale (x, y). u = 0 squashed (1.08, 0.86), a y overshoot to 1.04 around 0.6, (1, 1) at the end.</summary>
        public static Vector2 LandBounce(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.45f)
            {
                float k = StudioCurves.EaseOutCubic(u / 0.45f);
                return new Vector2(Mathf.Lerp(1.08f, 0.985f, k), Mathf.Lerp(0.86f, 1.04f, k));
            }
            float w = PotaLook.Smoothstep((u - 0.45f) / 0.55f);
            return new Vector2(Mathf.Lerp(0.985f, 1f, w), Mathf.Lerp(1.04f, 1f, w));
        }

        /// <summary>F10: pull 0..1 → lean weight 0..1, eased in-out so the first centimetres move little.</summary>
        public static float Lean(float pull) => PotaLook.Smoothstep(Mathf.Clamp01(pull));

        /// <summary>F13: a stamp's scale, 1 → peak (at 35 %) → rest, the fall eased out.</summary>
        public static float Stamp(float u, float peak, float rest)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.35f) return Mathf.Lerp(1f, peak, StudioCurves.EaseOutCubic(u / 0.35f));
            return Mathf.Lerp(peak, rest, StudioCurves.EaseOutCubic((u - 0.35f) / 0.65f));
        }

        /// <summary>F1: the ribbon's scale, 0 → 1.15 (at 60 %) → 1.</summary>
        public static float RibbonScale(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.6f) return Mathf.Lerp(0f, 1.15f, StudioCurves.EaseOutCubic(u / 0.6f));
            return Mathf.Lerp(1.15f, 1f, PotaLook.Smoothstep((u - 0.6f) / 0.4f));
        }

        /// <summary>F2/F5: seconds of idling before the hint; none when the last affordable block is all that is left under tension.</summary>
        public static float HintDelay(int affordable, bool tension) => tension && affordable == 1 ? 0f : StudioCurves.HintDelay;

        /// <summary>
        /// F8: cloud i of n on a ring of <paramref name="radius"/> around the origin, drifting at <paramref name="speed"/> m/s with a slow
        /// ±0.4 m bob; clouds are spread evenly and alternate heights through the caller.
        /// </summary>
        public static Vector3 CloudPose(int i, float t, int n, float radius, float speed)
        {
            float angle = (i + 0.5f) / Mathf.Max(1, n) * 2f * Mathf.PI + t * speed / Mathf.Max(1f, radius);
            float bob = 0.4f * Mathf.Sin(t * 0.21f + i * 1.7f);
            return new Vector3(Mathf.Cos(angle) * radius, bob, Mathf.Sin(angle) * radius);
        }

        /// <summary>F8: the bird pass progress 0..1 while a pass runs (first at <paramref name="first"/>, then every <paramref name="period"/>), −1 at rest.</summary>
        public static float BirdPass(float t, float first, float period, float duration)
        {
            if (t < first) return -1f;
            float local = (t - first) % period;
            return local <= duration ? local / duration : -1f;
        }

        /// <summary>A 0..1 sine breath at <paramref name="hz"/>: 0 at t = 0, 1 half a period later.</summary>
        public static float Breath(float t, float hz) => 0.5f - 0.5f * Mathf.Cos(t * hz * 2f * Mathf.PI);
    }
}
