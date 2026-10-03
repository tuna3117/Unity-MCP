using UnityEngine;

namespace Project.Meta
{
    /// <summary>
    /// M9, pure: the bonus reels' spin. Pota's timing: column i travels 12 rows in 1.4 s + 0.12 s·i on cubic-bezier(.25,.9,.3,1)
    /// (fast start, long settle). The feel layer adds a 0.12 s squash (y 0.92) on each stop and a 1.1 overshoot of the stopped cells.
    /// </summary>
    public class ReelSpin
    {
        public const float Base = 1.4f, PerColumn = 0.12f, SquashTime = 0.12f, PopTime = 0.2f;
        public const int TravelRows = 12;

        public readonly MetaFeelTuning Feel;
        public readonly int Columns;

        public ReelSpin(MetaFeelTuning feel, int columns) { Feel = feel; Columns = columns; }

        /// <summary>Pota's cubic-bezier(.25,.9,.3,1), approximated.</summary>
        public static float Ease(float u) => 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 3.5f);

        public float StopAt(int column) => Base + PerColumn * column;
        public bool Stopped(int column, float t) => t >= StopAt(column);
        /// <summary>0 (top, the filler rows) … 1 (the result rows) of the column's travel.</summary>
        public float Offset(int column, float t) => Ease(t / StopAt(column));

        /// <summary>The column's y scale: a dip to 0.92 and back within SquashTime of its stop (feel only).</summary>
        public float Squash(int column, float t)
        {
            if (!Feel.Enabled) return 1f;
            float u = (t - StopAt(column)) / SquashTime;
            if (u < 0f || u >= 1f) return 1f;
            return u < 0.5f ? Mathf.Lerp(1f, 0.92f, u * 2f) : Mathf.Lerp(0.92f, 1f, (u - 0.5f) * 2f);
        }

        /// <summary>The stopped cells' scale: 1 → 1.1 → 1 within PopTime of the stop (feel only).</summary>
        public float Pop(int column, float t)
        {
            if (!Feel.Enabled) return 1f;
            float u = (t - StopAt(column)) / PopTime;
            if (u < 0f || u >= 1f) return 1f;
            return 1f + 0.1f * Mathf.Sin(u * Mathf.PI);
        }

        public float Done => StopAt(Columns - 1) + (Feel.Enabled ? PopTime : 0f);
    }
}
