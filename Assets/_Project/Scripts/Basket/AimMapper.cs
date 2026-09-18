using UnityEngine;

namespace Project.Basket
{
    /// <summary>Result of mapping a swipe to a shot.</summary>
    public struct AimPoint
    {
        public float X;          // world x on the hoop plane (z = 0)
        public float Y;          // world y on the hoop plane
        public float Power;      // 0..1.4 (1 = 42 % of screen height)
        public bool OverPower;   // aim more than 1 m above the rim → "over the backboard"
        public float FlightTime; // seconds; short flicks fly faster and flatter
    }

    /// <summary>
    /// Swipe (pixels) → aim point. Numbers are the original game's (kurallar.ts AYAR.nisan),
    /// plus a variable flight time so that short swipes look different from medium ones.
    /// </summary>
    public static class AimMapper
    {
        public const float DeadZonePx = 24f;
        public const float LateralScale = 4f;
        public const float FullPowerFraction = 0.42f;
        public const float MaxPower = 1.4f;

        public const float RimY = 3.05f;
        public const float FloorAbove = 0.1f;
        public const float SwishAbove = 0.3f;
        public const float FullPower = 0.6f;
        public const float PowerRange = 1.5f;
        public const float OverPowerAbove = 1.0f;

        /// <summary>Power below which the aim height sits on the floor (rim + 0.1).</summary>
        public const float FloorPower = FullPower - (SwishAbove - FloorAbove) / PowerRange; // 0.4667

        public const float ShortFlightTime = 0.85f;
        public const float FlightTimeBase = 1.0f;

        public static float Power(float dy, float screenH) => Mathf.Min(MaxPower, -dy / (screenH * FullPowerFraction));

        public static float AimY(float power) => Mathf.Max(RimY + FloorAbove, RimY + SwishAbove + (power - FullPower) * PowerRange);

        public static float FlightTime(float power) => Mathf.Lerp(ShortFlightTime, FlightTimeBase, Mathf.Clamp01(power / FloorPower));

        public static AimPoint FromXPower(float x, float power)
        {
            float y = AimY(power);
            return new AimPoint { X = x, Y = y, Power = power, FlightTime = FlightTime(power), OverPower = y > RimY + OverPowerAbove };
        }

        public static bool TryMap(float dx, float dy, float screenW, float screenH, out AimPoint aim)
        {
            aim = default;
            if (-dy < DeadZonePx) return false;
            aim = FromXPower(dx / screenW * LateralScale, Power(dy, screenH));
            return true;
        }
    }
}
