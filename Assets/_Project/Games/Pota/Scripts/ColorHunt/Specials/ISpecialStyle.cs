using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// What a special look draws (spec 2026-10-01 §2), the sibling of <see cref="IBurstStyle"/>. The game owns the replay of the rules,
    /// the rhythm (the trigger plan from <see cref="SpecialSchedule"/>, the swells, the drops), the gates, the counts, the lock and the
    /// golden balls' bookkeeping; a style only draws and sounds (through PotaSounds.PlayClip, so the recorder logs it). Spaces:
    /// BlockView.Pos, SpecialFire.At and the Gather target are board-local cells (ColorHuntGame.BoardRoot); GoldenRoute is game-root local
    /// (Unity −z toward the camera). Game time throughout: hit-stops and slow motion slow a style too. Normal blocks keep IBurstStyle.
    /// </summary>
    public interface ISpecialStyle
    {
        /// <summary>Its assets are in place. A style that is not ready is replaced by Pota's, with one warning.</summary>
        bool Ready { get; }
        /// <summary>Live particles and pieces this style owns (BurstBudget).</summary>
        int LiveCount { get; }
        /// <summary>How the trigger plan staggers a step (Pota: everything at once).</summary>
        SpecialTiming Timing { get; }
        /// <summary>Royal/Court: a matched group's blocks travel into the birth cell instead of bursting.</summary>
        bool GathersBirthGroup { get; }
        /// <summary>Royal/Court: a firing special's own block is hidden at its fire; the style draws it.</summary>
        bool ConsumesSpecialView { get; }
        void Build(ColorHuntGame game);
        void Activate(bool on);
        /// <summary>Drop every effect at once (a new board, a look switch); never call back afterwards.</summary>
        void Clear();
        /// <summary>Each frame for every special resting on the board (not dropping, not bursting). The game drives the golden pulse.</summary>
        void Idle(BlockView v, float dt);
        /// <summary>A birth group's block travels to <paramref name="to"/> (the birth cell, board-local) in <paramref name="duration"/> s; call <paramref name="arrived"/> once.</summary>
        void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived);
        /// <summary>At the drops' start; the view already shows the special and falls with its column afterwards.</summary>
        void Birth(BlockView v, SpecialKind kind);
        /// <summary>Once at a trigger step's start: how many specials fire in it and the board-local centre of its blocks.</summary>
        void TriggerStep(int specials, Vector2 centre);
        /// <summary>One special fires (the tapped one first), <see cref="SpecialFire.Time"/> s into the step.</summary>
        void Fire(SpecialFire fire);
        /// <summary>A golden ball leaves for the hoop: call <paramref name="passedGate"/>, then <paramref name="landed"/>, each exactly once.</summary>
        void GoldenFlight(GoldenRoute route, Action passedGate, Action landed);
    }

    /// <summary>How a look staggers a trigger step (spec §2 "sırayla patlama"). Default = Pota: everything at once.</summary>
    [Serializable]
    public struct SpecialTiming
    {
        /// <summary>Cells per second a line sweeps; 0 = the whole line at once (Pota).</summary>
        public float LineSpeed;
        /// <summary>Seconds from a line's fire to its first cells.</summary>
        public float LineLead;
        /// <summary>Seconds of wind-up before a bomb bursts.</summary>
        public float BombLead;
        /// <summary>Seconds between the bomb's centre and each further ring (Chebyshev distance).</summary>
        public float BombRing;
        /// <summary>Seconds a birth group takes into the birth cell (GathersBirthGroup); 0 = the step's ChainTime.</summary>
        public float GatherTime;

        public static readonly SpecialTiming Pota = default;
    }

    /// <summary>One special firing in a trigger step.</summary>
    public struct SpecialFire
    {
        public int Cell;
        public SpecialKind Kind;
        /// <summary>Board-local position of its cell.</summary>
        public Vector2 At;
        /// <summary>0 = the tapped special, then in firing order.</summary>
        public int Order;
        /// <summary>Seconds from the step's start to the moment its own block bursts (the tapped one: the step's first peak).</summary>
        public float Time;
        /// <summary>Cells of its own area that burst in this step (clipped at the edges, golden balls left out).</summary>
        public int Size;
        /// <summary>The plan's <see cref="TriggerPlan.Pace"/>: a style multiplies its sweep durations by it.</summary>
        public float Pace;
    }

    /// <summary>When each cell of a trigger step starts its swell, when each special fires, when the drops start.</summary>
    public sealed class TriggerPlan
    {
        /// <summary>Exploded cell → seconds from the step's start to its swell's start (it bursts ChainTime·0.55 later).</summary>
        public readonly Dictionary<int, float> Start = new Dictionary<int, float>();
        /// <summary>Sorted by Time; the tapped special first.</summary>
        public readonly List<SpecialFire> Fires = new List<SpecialFire>();
        /// <summary>The last start + the swell time (≥ every fire): the drops start here.</summary>
        public float End;
        /// <summary>1, or the factor every start and fire was scaled by so the step fits <see cref="SpecialSchedule.MaxStep"/>.</summary>
        public float Pace = 1f;
    }

    /// <summary>A golden ball's way from its cell through its gate into the hoop, game-root local (renkAviFaz.ts altinlariIlerlet).</summary>
    public struct GoldenRoute
    {
        /// <summary>The ball's cell on the board plane (z 0).</summary>
        public Vector3 From;
        /// <summary>Its gate ring (GateRings.RingAt). The drop uses only its y (Pota's halkaY) and keeps From's x and z.</summary>
        public Vector3 Gate;
        /// <summary>Where it lands (GoldenPath.HoopLocal).</summary>
        public Vector3 Hoop;
        /// <summary>Seconds it waits before leaving: the k-th ball of one step leaves k·0.12 s after the first.</summary>
        public float Delay;
        public int Col;
        /// <summary>Grid scale (game-root units per board cell); Pota's lifts and the ball are in cells. 0 counts as 1.</summary>
        public float Scale;
    }

    /// <summary>A look whose style is not written yet (or a future enum value): never ready, so the game plays Pota's specials.</summary>
    public sealed class NoSpecialStyle : ISpecialStyle
    {
        public bool Ready => false;
        public int LiveCount => 0;
        public SpecialTiming Timing => SpecialTiming.Pota;
        public bool GathersBirthGroup => false;
        public bool ConsumesSpecialView => false;
        public void Build(ColorHuntGame game) { }
        public void Activate(bool on) { }
        public void Clear() { }
        public void Idle(BlockView v, float dt) { }
        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived) => arrived?.Invoke(v);
        public void Birth(BlockView v, SpecialKind kind) { }
        public void TriggerStep(int specials, Vector2 centre) { }
        public void Fire(SpecialFire fire) { }
        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed) { passedGate?.Invoke(); landed?.Invoke(); }
    }

    /// <summary>Pota's own words for specials (src/dil.ts, tr). Pota says "ÇİZGİ TOPU!" for the line ball.</summary>
    public static class SpecialTexts
    {
        public const string Line = "ÇİZGİ TOPU!", Bomb = "BOMBA!", Golden = "ALTIN TOP!";
        /// <summary>dokun → "altin": the golden ball cannot be tapped away.</summary>
        public const string GoldenTap = "Altın top geçişten sepete inmeli";

        public static string Born(SpecialKind k) => k == SpecialKind.Golden ? Golden : k == SpecialKind.Bomb ? Bomb : Line;
    }
}
