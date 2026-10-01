using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// renkAviFaz.ts altinlariIlerlet in game-root units (RENK_AVI_HIS.altinInis): from its cell the ball drops straight to its ring's
    /// height in 0.32 s (ease-in o², x and z kept), then in 0.5 s it moves linearly to the hoop while lifting sin(πo)·0.3 cells and
    /// bowing sin(πo)·1.4·0.3 cells toward the camera; the next ball of the same step leaves 0.12 s later.
    /// </summary>
    public static class GoldenPath
    {
        public const float GateTime = 0.32f, HoopTime = 0.5f, Arc = 1.4f, Gap = 0.12f;
        /// <summary>The hoop leg's lift (board cells; the arc toward the camera is Arc times it).</summary>
        public const float Lift = 0.3f;
        /// <summary>DEV_POTA: the rim's centre y, (cemberR + 0.6 − 0.2) toward the camera (GiantHoop's rim sits at R + 0.6).</summary>
        public static readonly Vector3 HoopLocal = new Vector3(0f, PotaLook.RimY, -(PotaLook.RimRadius + 0.4f));

        public static float Total => GateTime + HoopTime;

        /// <summary>Where the ball is <paramref name="t"/> s after leaving (the caller subtracts Delay); clamps at the hoop.</summary>
        public static Vector3 At(GoldenRoute r, float t)
        {
            if (t <= 0f) return r.From;
            if (t >= GateTime + HoopTime) return r.Hoop;
            if (t <= GateTime)
            {
                float o = t / GateTime;
                return new Vector3(r.From.x, Mathf.LerpUnclamped(r.From.y, r.Gate.y, o * o), r.From.z);
            }
            float s = r.Scale > 0f ? r.Scale : 1f;
            float u = (t - GateTime) / HoopTime, bump = Mathf.Sin(Mathf.PI * u);
            return new Vector3(
                Mathf.LerpUnclamped(r.From.x, r.Hoop.x, u),
                Mathf.LerpUnclamped(r.Gate.y, r.Hoop.y, u) + bump * Lift * s,
                Mathf.LerpUnclamped(r.From.z, r.Hoop.z, u) - bump * Arc * Lift * s);
        }

        /// <summary>altinlariIlerlet: the ring closes once ta &gt; halkaSure.</summary>
        public static bool PastGate(float t) => t > GateTime;

        public static bool Landed(float t) => t >= GateTime + HoopTime;
    }
}
