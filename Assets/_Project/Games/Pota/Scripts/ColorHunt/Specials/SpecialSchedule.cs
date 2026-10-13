using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>The pass ball's two candidate looks (Pota spec 2026-10-11 §3): A "Pas" (the ball's own block flies), B "Pervane" (the Blender ball with its propeller).</summary>
    public enum PassLook { A, B }

    /// <summary>
    /// Pota OzelZamanlama's pass fields (src/ozelPlan.ts): takeoff in place, speed (cells/s, Euclidean), flight clipped to [min, max], hang over
    /// the target. The trigger plan and the flight read the same numbers, so the ball lands the frame its target bursts.
    /// </summary>
    [Serializable]
    public struct PassTiming
    {
        [Tooltip("pasKalkis: seconds in place before the ball leaves.")] public float Takeoff;
        [Tooltip("pasHiz: cells per second (Euclidean).")] public float Speed;
        [Tooltip("pasEnAz: the shortest flight (s).")] public float MinFlight;
        [Tooltip("pasEnCok: the longest flight (s).")] public float MaxFlight;
        [Tooltip("pasAsili: seconds over the target before the hit.")] public float Hang;

        /// <summary>ozelPlan.ts OZEL_ZAMANLAMA (:16-17): kalkış 0.1 + uçuş 0.2–0.45 at 14 cells/s → 0.30–0.55 s.</summary>
        public static readonly PassTiming A = new PassTiming { Takeoff = 0.1f, Speed = 14f, MinFlight = 0.2f, MaxFlight = 0.45f, Hang = 0f };
        /// <summary>ozelPlan.ts PAS_ZAMANLAMA_B (:19): kalkış 0.2, 12 cells/s, 0.25–0.5, asılı 0.08 → 0.53–0.78 s.</summary>
        public static readonly PassTiming B = new PassTiming { Takeoff = 0.2f, Speed = 12f, MinFlight = 0.25f, MaxFlight = 0.5f, Hang = 0.08f };

        /// <summary>ozelPlan.ts pasVarisi (:22-25): seconds from the pass ball's fire to its hit on <paramref name="to"/>.</summary>
        public float Arrival(int n, int from, int to)
        {
            float d = Mathf.Sqrt(Sq(to / n - from / n) + Sq(to % n - from % n));
            return Takeoff + Mathf.Min(MaxFlight, Mathf.Max(MinFlight, d / Mathf.Max(1e-3f, Speed))) + Hang;
        }

        private static float Sq(int x) => x * x;
    }

    /// <summary>
    /// The trigger step's timetable (spec §2, "sırayla patlama"): pure. The tapped special fires at the step's first peak; each fired
    /// special's area bursts after its lead plus a delay (a line: distance / LineSpeed; a bomb: Chebyshev ring × BombRing); a special in
    /// an area fires the moment its own cell bursts and its area follows from there (earliest arrival wins; each special fires once, so
    /// mutual chains end). Pota's timing (all zero) puts every cell at 0 and every fire at the first peak, exactly today's step. A plan
    /// longer than <see cref="MaxStep"/> is compressed linearly (<see cref="TriggerPlan.Pace"/>), keeping the order.
    /// Pass balls (Pota ozelPlan.ts ozelPlani, spec 2026-10-11 §3): a fired pass ball lands on its target <see cref="PassTiming.Arrival"/>
    /// later; a special target becomes a ROOT that fires at the landing (roots are never compressed: the compression scales each cell's
    /// delay after its own root, as Pota's kökler), a normal target bursts at the landing without a swell (<see cref="TriggerPlan.Hits"/>).
    /// Combos (Pota ozelPlan.ts komboEki, spec 2026-10-13 §3): both partners are roots at the first peak (the merge before it is the look's,
    /// never compressed); an area combo's centre sweeps its <see cref="ColorHuntRules.ComboCells"/> with <see cref="ComboDelay"/> and the
    /// partner opens only its own cell; a PassLine / PassBomb's carried special is a VIRTUAL special of its kind at the flight's target, a
    /// root at the landing (no target: at the centre, at the first peak); a PassPass sends three flights from the one centre.
    /// </summary>
    public static class SpecialSchedule
    {
        /// <summary>Global Constraints: a trigger step lasts at most this from its start to the drops.</summary>
        public const float MaxStep = 0.6f;

        /// <summary>The plan with look A's pass timing (no pass ball in the step: exactly the plan before the pass ball).</summary>
        public static TriggerPlan Plan(int n, Step step, int origin, SpecialTiming t, float swellTime) =>
            Plan(n, step, origin, t, swellTime, PassTiming.A);

        public static TriggerPlan Plan(int n, Step step, int origin, SpecialTiming t, float swellTime, PassTiming pass)
        {
            float peak = swellTime * PotaLook.SwellPeakAt;
            var extra = step.Combo.HasValue ? ComboExtra(step.Combo.Value) : null;
            // Combo: the two partners fire together at the first peak (Pota: both at t = 0).
            Dictionary<int, float> roots = step.Combo.HasValue
                ? new Dictionary<int, float> { [step.Combo.Value.Center] = peak, [step.Combo.Value.Partner] = peak } : null;
            // Pass flights: the ball's cell → its targets in order (a PassPass sends three from the one centre).
            var targets = new Dictionary<int, List<int>>();
            int flights = 0;
            foreach (var p in step.Passes)
            {
                if (p.Target < 0) continue;
                if (!targets.TryGetValue(p.Cell, out var list)) targets[p.Cell] = list = new List<int>();
                list.Add(p.Target);
                flights++;
            }
            if (targets.Count == 0) return Core(n, step, origin, t, swellTime, roots, extra);
            var specials = new HashSet<int>();
            foreach (var tr in step.Triggered) specials.Add(tr.Cell);
            if (extra?.Virtual != null) foreach (var kv in extra.Virtual) specials.Add(kv.Key);
            var exploded = new HashSet<int>(step.Exploded);
            for (int round = 0; round <= flights; round++)
            {
                var plan = Core(n, step, origin, t, swellTime, roots, extra);
                var landings = new Dictionary<int, float>();
                foreach (var f in plan.Fires)
                    if (targets.TryGetValue(f.Cell, out var list))
                        foreach (int target in list) landings[target] = f.Time + pass.Arrival(n, f.Cell, target);
                bool added = false;
                foreach (var kv in landings)
                {
                    if (!specials.Contains(kv.Key) || (roots != null && roots.ContainsKey(kv.Key))) continue;
                    if (roots == null)                                       // carry the tapped special's own root explicitly
                    {
                        roots = new Dictionary<int, float>();
                        int o = specials.Contains(origin) ? origin : step.Triggered.Count > 0 ? step.Triggered[0].Cell : -1;
                        if (o >= 0) roots[o] = peak;
                    }
                    roots[kv.Key] = kv.Value;
                    added = true;
                }
                if (added) continue;
                foreach (var kv in landings)
                {
                    if (specials.Contains(kv.Key) || !exploded.Contains(kv.Key)) continue;
                    plan.Hits[kv.Key] = kv.Value;
                    plan.End = Mathf.Max(plan.End, kv.Value + swellTime);
                }
                return plan;
            }
            throw new InvalidOperationException("SpecialSchedule: the pass roots did not settle");
        }

        private struct At { public float T, Root; }

        /// <summary>
        /// Pota CekirdekEk (komboEki): an area combo's centre sweeps the combo area (<see cref="ComboCenter"/>, ≥ 0), consumed partners open
        /// only their own cell, virtual specials (a carried one) fire with their own kind at their cell.
        /// </summary>
        private sealed class CoreExtra
        {
            public int ComboCenter = -1;
            public ComboKind ComboArea;
            public HashSet<int> Consumed;
            public Dictionary<int, SpecialKind> Virtual;
        }

        /// <summary>Pota komboEki.</summary>
        private static CoreExtra ComboExtra(ComboInfo k)
        {
            switch (k.Kind)
            {
                case ComboKind.Cross:
                case ComboKind.ThickCross:
                case ComboKind.BigBomb:
                    return new CoreExtra { ComboCenter = k.Center, ComboArea = k.Kind, Consumed = new HashSet<int> { k.Partner } };
                case ComboKind.PassPass:
                    return new CoreExtra { Consumed = new HashSet<int> { k.Center, k.Partner } };
                default:
                    int target = k.PassTargets != null && k.PassTargets.Length > 0 ? k.PassTargets[0] : -1;
                    return target >= 0
                        ? new CoreExtra { Consumed = new HashSet<int> { k.Center, k.Partner }, Virtual = new Dictionary<int, SpecialKind> { [target] = k.Carried } }
                        : new CoreExtra { Consumed = new HashSet<int> { k.Partner }, Virtual = new Dictionary<int, SpecialKind> { [k.Center] = k.Carried } };
            }
        }

        /// <summary>
        /// Pota komboGecikme (spec 2026-10-13 §3): seconds from an area combo's fire at <paramref name="center"/> to the burst of
        /// <paramref name="cell"/> — Cross / ThickCross: the earliest of its strips (one / three rows and columns) at the line's lead + distance /
        /// LineSpeed; BigBomb: the bomb's lead + Chebyshev ring × BombRing.
        /// </summary>
        public static float ComboDelay(int n, int center, ComboKind kind, int cell, SpecialTiming t)
        {
            int dr = Mathf.Abs(ColorHuntRules.Row(n, cell) - ColorHuntRules.Row(n, center));
            int dc = Mathf.Abs(ColorHuntRules.Col(n, cell) - ColorHuntRules.Col(n, center));
            if (kind == ComboKind.BigBomb) return t.BombLead + Mathf.Max(dr, dc) * t.BombRing;
            int width = kind == ComboKind.ThickCross ? 1 : 0;
            float best = float.PositiveInfinity;
            if (dr <= width) best = Mathf.Min(best, t.LineLead + (t.LineSpeed > 0f ? dc / t.LineSpeed : 0f));   // the row strip(s)
            if (dc <= width) best = Mathf.Min(best, t.LineLead + (t.LineSpeed > 0f ? dr / t.LineSpeed : 0f));   // the column strip(s)
            return best;
        }

        /// <summary>
        /// The plan from its roots: the tapped special at the first peak (<paramref name="roots"/> null), or every root at its own time.
        /// Each cell and fire remembers its root; the compression scales what comes after the root. <paramref name="extra"/>: a combo's areas.
        /// </summary>
        private static TriggerPlan Core(int n, Step step, int origin, SpecialTiming t, float swellTime, Dictionary<int, float> roots, CoreExtra extra = null)
        {
            var plan = new TriggerPlan();
            float peak = swellTime * PotaLook.SwellPeakAt;
            var exploded = new HashSet<int>(step.Exploded);
            var kinds = new Dictionary<int, SpecialKind>();
            foreach (var tr in step.Triggered) kinds[tr.Cell] = tr.Kind;
            if (extra?.Virtual != null) foreach (var kv in extra.Virtual) kinds[kv.Key] = kv.Value;
            var burst = new Dictionary<int, At>();                 // cell → its burst moment (and its root)
            var reach = new Dictionary<int, At>();                 // special → when it fires (earliest arrival so far)
            var fired = new HashSet<int>();
            var fireRoots = new List<float>();
            if (roots != null)
            {
                foreach (var kv in roots)
                {
                    if (!kinds.ContainsKey(kv.Key)) continue;
                    reach[kv.Key] = new At { T = kv.Value, Root = kv.Value };
                    burst[kv.Key] = new At { T = kv.Value, Root = kv.Value };
                }
            }
            else
            {
                if (!kinds.ContainsKey(origin)) origin = step.Triggered.Count > 0 ? step.Triggered[0].Cell : -1;
                if (origin >= 0) { reach[origin] = new At { T = peak, Root = peak }; burst[origin] = new At { T = peak, Root = peak }; }
            }
            while (true)
            {
                int next = -1;
                var best = new At { T = float.MaxValue };
                foreach (var kv in reach)
                {
                    if (fired.Contains(kv.Key)) continue;
                    if (kv.Value.T < best.T || (kv.Value.T == best.T && kv.Key < next)) { best = kv.Value; next = kv.Key; }
                }
                if (next < 0) break;
                fired.Add(next);
                var kind = kinds[next];
                bool comboArea = extra != null && extra.ComboCenter == next;
                var area = comboArea ? ColorHuntRules.ComboCells(n, next, extra.ComboArea)
                    : extra?.Consumed != null && extra.Consumed.Contains(next) ? new List<int> { next }
                    : ColorHuntRules.SingleArea(n, next, kind);
                int size = 0;
                foreach (int c in area)
                {
                    if (!exploded.Contains(c)) continue;             // a golden ball, or a cell the rules left out
                    size++;
                    float at = best.T + (comboArea ? ComboDelay(n, next, extra.ComboArea, c, t) : Delay(n, next, kind, c, t));
                    if (!burst.TryGetValue(c, out var b) || at < b.T) burst[c] = new At { T = at, Root = best.Root };
                    if (kinds.ContainsKey(c) && !fired.Contains(c) && (!reach.TryGetValue(c, out var r) || at < r.T)) reach[c] = new At { T = at, Root = best.Root };
                }
                plan.Fires.Add(new SpecialFire
                {
                    Cell = next, Kind = kind, Order = plan.Fires.Count, Time = best.T, Size = size,
                    At = BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, next), ColorHuntRules.Col(n, next)),
                });
                fireRoots.Add(best.Root);
            }
            // start = rootStart + pay, rootStart = root − peak (the tapped special: 0), pay = burst − root. One root at the first peak: start =
            // burst − peak, exactly the plan before roots.
            var pay = new Dictionary<int, At>();
            float last = 0f;
            foreach (int c in step.Exploded)
            {
                var b = burst.TryGetValue(c, out var found) ? found : new At { T = peak, Root = peak };
                var p = new At { T = b.T - b.Root, Root = b.Root - peak };
                pay[c] = p;
                plan.Start[c] = p.Root + p.T;
                last = Mathf.Max(last, p.T);
            }
            if (last > 0f && last + swellTime > MaxStep)
            {
                float k = Mathf.Max(0f, MaxStep - swellTime) / last;
                plan.Pace = k;
                foreach (int c in step.Exploded) plan.Start[c] = pay[c].Root + pay[c].T * k;
                for (int i = 0; i < plan.Fires.Count; i++)
                {
                    var f = plan.Fires[i];
                    f.Time = fireRoots[i] + (f.Time - fireRoots[i]) * k;
                    plan.Fires[i] = f;
                }
            }
            for (int i = 0; i < plan.Fires.Count; i++)
            {
                var f = plan.Fires[i];
                f.Pace = plan.Pace;
                plan.Fires[i] = f;
            }
            float end = 0f;
            foreach (int c in step.Exploded) end = Mathf.Max(end, plan.Start[c]);
            plan.End = end + swellTime;
            foreach (var f in plan.Fires) plan.End = Mathf.Max(plan.End, f.Time);
            return plan;
        }

        /// <summary>Seconds from a special's fire to the burst of <paramref name="cell"/> in its area (a pass ball's area is its own cell: 0).</summary>
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
