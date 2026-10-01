using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// The trigger step's timetable (spec §2, "sırayla patlama"): pure. The tapped special fires at the step's first peak; each fired
    /// special's area bursts after its lead plus a delay (a line: distance / LineSpeed; a bomb: Chebyshev ring × BombRing); a special in
    /// an area fires the moment its own cell bursts and its area follows from there (earliest arrival wins; each special fires once, so
    /// mutual chains end). Pota's timing (all zero) puts every cell at 0 and every fire at the first peak, exactly today's step. A plan
    /// longer than <see cref="MaxStep"/> is compressed linearly (<see cref="TriggerPlan.Pace"/>), keeping the order.
    /// </summary>
    public static class SpecialSchedule
    {
        /// <summary>Global Constraints: a trigger step lasts at most this from its start to the drops.</summary>
        public const float MaxStep = 0.6f;

        public static TriggerPlan Plan(int n, Step step, int origin, SpecialTiming t, float swellTime)
        {
            var plan = new TriggerPlan();
            float peak = swellTime * PotaLook.SwellPeakAt;
            var exploded = new HashSet<int>(step.Exploded);
            var kinds = new Dictionary<int, SpecialKind>();
            foreach (var tr in step.Triggered) kinds[tr.Cell] = tr.Kind;
            if (!kinds.ContainsKey(origin)) origin = step.Triggered.Count > 0 ? step.Triggered[0].Cell : -1;
            var burst = new Dictionary<int, float>();              // cell → its burst moment
            var reach = new Dictionary<int, float>();              // special → when it fires (earliest arrival so far)
            var fired = new HashSet<int>();
            if (origin >= 0) { reach[origin] = peak; burst[origin] = peak; }
            while (true)
            {
                int next = -1;
                float best = float.MaxValue;
                foreach (var kv in reach)
                {
                    if (fired.Contains(kv.Key)) continue;
                    if (kv.Value < best || (kv.Value == best && kv.Key < next)) { best = kv.Value; next = kv.Key; }
                }
                if (next < 0) break;
                fired.Add(next);
                var kind = kinds[next];
                int size = 0;
                foreach (int c in ColorHuntRules.SingleArea(n, next, kind))
                {
                    if (!exploded.Contains(c)) continue;             // a golden ball, or a cell the rules left out
                    size++;
                    float at = best + Delay(n, next, kind, c, t);
                    if (!burst.TryGetValue(c, out float b) || at < b) burst[c] = at;
                    if (kinds.ContainsKey(c) && !fired.Contains(c) && (!reach.TryGetValue(c, out float r) || at < r)) reach[c] = at;
                }
                plan.Fires.Add(new SpecialFire
                {
                    Cell = next, Kind = kind, Order = plan.Fires.Count, Time = best, Size = size,
                    At = BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, next), ColorHuntRules.Col(n, next)),
                });
            }
            float last = 0f;
            foreach (int c in step.Exploded)
            {
                float start = burst.TryGetValue(c, out float b) ? b - peak : 0f;
                plan.Start[c] = start;
                last = Mathf.Max(last, start);
            }
            if (last > 0f && last + swellTime > MaxStep)
            {
                float k = Mathf.Max(0f, MaxStep - swellTime) / last;
                plan.Pace = k;
                foreach (int c in step.Exploded) plan.Start[c] *= k;
                for (int i = 0; i < plan.Fires.Count; i++)
                {
                    var f = plan.Fires[i];
                    f.Time = peak + (f.Time - peak) * k;
                    plan.Fires[i] = f;
                }
                last *= k;
            }
            for (int i = 0; i < plan.Fires.Count; i++)
            {
                var f = plan.Fires[i];
                f.Pace = plan.Pace;
                plan.Fires[i] = f;
            }
            plan.End = last + swellTime;
            foreach (var f in plan.Fires) plan.End = Mathf.Max(plan.End, f.Time);
            return plan;
        }

        /// <summary>Seconds from a special's fire to the burst of <paramref name="cell"/> in its area.</summary>
        private static float Delay(int n, int from, SpecialKind kind, int cell, SpecialTiming t)
        {
            int dr = Mathf.Abs(ColorHuntRules.Row(n, cell) - ColorHuntRules.Row(n, from));
            int dc = Mathf.Abs(ColorHuntRules.Col(n, cell) - ColorHuntRules.Col(n, from));
            switch (kind)
            {
                case SpecialKind.LineV: return t.LineLead + (t.LineSpeed > 0f ? dr / t.LineSpeed : 0f);   // the column
                case SpecialKind.LineH: return t.LineLead + (t.LineSpeed > 0f ? dc / t.LineSpeed : 0f);   // the row
                case SpecialKind.Bomb: return t.BombLead + Mathf.Max(dr, dc) * t.BombRing;
                default: return 0f;
            }
        }
    }
}
