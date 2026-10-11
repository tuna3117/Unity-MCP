using System;
using System.Collections.Generic;

namespace Project.ColorHunt
{
    /// <summary>
    /// One block on the board: palette index and hit points (balls needed to break it). A special block (Pota ozelBlok) has
    /// colour <see cref="ColorHuntRules.SpecialColor"/> and hp 0: it never joins a run and counts for no colour target.
    /// </summary>
    [Serializable]
    public struct Block
    {
        public int Color;
        public int Hp;
        public SpecialKind Special;
        public Block(int color, int hp) { Color = color; Hp = hp; Special = SpecialKind.None; }
        public bool IsSpecial => Special != SpecialKind.None;
        public static Block MakeSpecial(SpecialKind kind) => new Block(ColorHuntRules.SpecialColor, 0) { Special = kind };
    }

    [Serializable]
    public struct Target
    {
        public int Color;
        public int Remaining;
        public Target(int color, int remaining) { Color = color; Remaining = remaining; }
    }

    public struct Drop { public int From, To; }

    public struct Spawn
    {
        public int To;
        public Block Block;
        /// <summary>Cells the new block falls from above the frame.</summary>
        public int Distance;
    }

    /// <summary>
    /// One animation frame: <see cref="Exploded"/> go first (with <see cref="Triggered"/> specials firing and golden balls going
    /// <see cref="ToBasket"/>), <see cref="Born"/> specials take their cells, <see cref="Opened"/> gates open, then
    /// <see cref="Drops"/> and <see cref="Spawns"/> fall.
    /// </summary>
    public class Step
    {
        public readonly List<int> Exploded = new List<int>();
        public readonly List<Drop> Drops = new List<Drop>();
        public readonly List<Spawn> Spawns = new List<Spawn>();
        /// <summary>Specials born this step (Pota dogan), sorted by cell; the birth cell does not explode.</summary>
        public readonly List<Born> Born = new List<Born>();
        /// <summary>Specials fired by this step (Pota tetiklenen), sorted by cell: only the first step of a special tap has them.</summary>
        public readonly List<Trigger> Triggered = new List<Trigger>();
        /// <summary>Golden balls leaving the bottom row through their column's gate (Pota sepete), sorted by column.</summary>
        public readonly List<BasketDrop> ToBasket = new List<BasketDrop>();
        /// <summary>Gate columns opened this step (Pota acilan), in opening order.</summary>
        public readonly List<int> Opened = new List<int>();
        /// <summary>Pass balls that flew in this step (Pota pasTopu), in firing order: each ball's cell and the cell it hit (−1: none).</summary>
        public readonly List<PassHit> Passes = new List<PassHit>();
    }

    public class Match
    {
        public int Color;
        public bool Horizontal;
        public readonly List<int> Cells = new List<int>();
    }

    public enum Rotation { Clockwise, Counter }

    /// <summary>Level design: what <see cref="ColorHuntRules.Create"/> takes.</summary>
    public class LevelDesign
    {
        public int Size = 7;
        public int ColorCount = 5;
        public int HpMin = 3;
        public int HpMax = 14;
        public (int color, int count)[] Targets = { (0, 22), (1, 16) };
        /// <summary>Which specials are born (Pota ozellikler); none by default, the level the game ships today.</summary>
        public Features Features;
    }

    /// <summary>
    /// Board state. Plain data: cloned, never mutated by the rules. Rows are top to bottom (row 0 is the top).
    /// Randomness is explicit state (<see cref="Rng"/>): the same seed replays the same game.
    /// </summary>
    public class Board
    {
        public int Size;
        /// <summary>Size × Size, always full.</summary>
        public Block[] Cells;
        public int Ammo;
        public Target[] Targets;
        public int ColorCount;
        public int HpMin, HpMax;
        public uint Rng;
        public Features Features;
        /// <summary>Columns whose gate is open (Pota gecisler), in opening order. Gates sit on the frame: they never rotate.</summary>
        public List<int> Gates = new List<int>();
        /// <summary>Golden balls that went into the basket (Pota altin).</summary>
        public int Golden;
        /// <summary>Pota's "bomba" target: bombs still to fire (0 = none). Only the pass ball reads it so far (it flies to a bomb in a target).</summary>
        public int BombTarget;
        /// <summary>Pota's "cizgi" target: line balls still to fire (0 = none).</summary>
        public int LineTarget;

        public Board Clone()
        {
            return new Board
            {
                Size = Size,
                Cells = (Block[])Cells.Clone(),
                Ammo = Ammo,
                Targets = (Target[])Targets.Clone(),
                ColorCount = ColorCount,
                HpMin = HpMin,
                HpMax = HpMax,
                Rng = Rng,
                Features = Features,
                Gates = Gates != null ? new List<int>(Gates) : new List<int>(),
                Golden = Golden,
                BombTarget = BombTarget,
                LineTarget = LineTarget,
            };
        }
    }

    public struct BreakResult
    {
        /// <summary>Outcome == Broken.</summary>
        public bool Broken;
        public BreakOutcome Outcome;
        public Board Board;
        public List<Step> Steps;
        public int Cost;
    }

    /// <summary>
    /// Pure colour-hunt rules, a port of Pota's src/renkAvi.ts: the board never empties, a tap costs the
    /// block's hp in balls, columns collapse, random blocks enter from the top, 3+ runs explode for free,
    /// rotation is free and never shifts anything. Specials, gates and the basket: ColorHuntRules.Specials.cs.
    /// </summary>
    public static partial class ColorHuntRules
    {
        public const int MatchLength = 3;
        public const int ChainCap = 64;

        // ---------- random (mulberry32, same sequence as Pota's `tohumlu`) ----------

        public static uint Next(uint state, out double value)
        {
            unchecked
            {
                uint a = state + 0x6d2b79f5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                value = (t ^ (t >> 14)) / 4294967296.0;
                return a;
            }
        }

        /// <summary>Pota `tamsayi`: an int in [min, max].</summary>
        public static uint NextInt(uint state, int min, int max, out int value)
        {
            state = Next(state, out double v);
            value = min + (int)Math.Floor(v * (max - min + 1));
            return state;
        }

        // ---------- helpers ----------

        public static int Index(int n, int row, int col) => row * n + col;
        public static int Row(int n, int i) => i / n;
        public static int Col(int n, int i) => i % n;

        // ---------- matches ----------

        /// <summary>
        /// Horizontal and vertical runs of 3+ (no diagonals). Raw runs: an L/T cell can be in two runs. A special block (colour −1)
        /// never joins a run and breaks the run it sits in (Pota eslesmeleriBul).
        /// </summary>
        public static List<Match> FindMatches(Board b)
        {
            int n = b.Size;
            var list = new List<Match>();
            void Run(Func<int, int> cell, bool horizontal)
            {
                int start = 0;
                for (int k = 1; k <= n; k++)
                {
                    int color = b.Cells[cell(start)].Color;
                    bool same = k < n && color >= 0 && b.Cells[cell(k)].Color == color;
                    if (same) continue;
                    if (k - start >= MatchLength && color >= 0)
                    {
                        var m = new Match { Color = color, Horizontal = horizontal };
                        for (int j = start; j < k; j++) m.Cells.Add(cell(j));
                        list.Add(m);
                    }
                    start = k;
                }
            }
            for (int r = 0; r < n; r++) { int row = r; Run(c => Index(n, row, c), true); }
            for (int c = 0; c < n; c++) { int col = c; Run(r => Index(n, r, col), false); }
            return list;
        }

        public static bool HasTriple(Board b) => FindMatches(b).Count > 0;

        // ---------- setup ----------

        /// <summary>Pota eslesmeYapar: the colour would close a triple to the left or above, or (square) a 2×2 with its left, upper and upper-left neighbours.</summary>
        private static bool MakesMatch(List<Block> cells, int n, int r, int c, int color, bool square)
        {
            if (c >= 2 && cells[Index(n, r, c - 1)].Color == color && cells[Index(n, r, c - 2)].Color == color) return true;
            if (r >= 2 && cells[Index(n, r - 1, c)].Color == color && cells[Index(n, r - 2, c)].Color == color) return true;
            return square && r >= 1 && c >= 1 && cells[Index(n, r, c - 1)].Color == color && cells[Index(n, r - 1, c)].Color == color
                   && cells[Index(n, r - 1, c - 1)].Color == color;
        }

        /// <summary>
        /// Fills the board without triples (pairs allowed) and, on a Pass level, without 2×2 squares (Pota tahtaKur kareYok); with 3+ colours
        /// there is always a way out. Without Pass the rng use is exactly the old one. No gates yet.
        /// </summary>
        public static Board Create(LevelDesign d, int ammo, uint seed)
        {
            int n = d.Size;
            var cells = new List<Block>(n * n);
            uint rng = seed;
            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                {
                    int color = 0;
                    for (int attempt = 0; attempt < 32; attempt++)
                    {
                        rng = NextInt(rng, 0, d.ColorCount - 1, out color);
                        if (!MakesMatch(cells, n, r, c, color, d.Features.Pass)) break;
                    }
                    rng = NextInt(rng, d.HpMin, d.HpMax, out int hp);
                    cells.Add(new Block(color, hp));
                }
            }
            var targets = new Target[d.Targets.Length];
            for (int i = 0; i < targets.Length; i++) targets[i] = new Target(d.Targets[i].color, d.Targets[i].count);
            return new Board
            {
                Size = n, Cells = cells.ToArray(), Ammo = ammo, Targets = targets,
                ColorCount = d.ColorCount, HpMin = d.HpMin, HpMax = d.HpMax, Rng = rng,
                Features = d.Features, Gates = new List<int>(), Golden = 0,
            };
        }

        // ---------- rotation ----------

        /// <summary>
        /// 90° rotation: new[c·n + (n−1−r)] = old[r·n + c] (clockwise). The board is full, so nothing shifts. Gates and the
        /// golden count stay (they belong to the frame). <see cref="RotateAndSettle"/> also lets a golden ball into the basket.
        /// </summary>
        public static Board Rotate(Board b, Rotation dir)
        {
            int n = b.Size;
            var result = b.Clone();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    var block = b.Cells[Index(n, r, c)];
                    if (dir == Rotation.Clockwise) result.Cells[Index(n, c, n - 1 - r)] = block;
                    else result.Cells[Index(n, n - 1 - c, r)] = block;
                }
            return result;
        }

        // ---------- break and chain ----------

        /// <summary>
        /// Pota patlatVeDoldur without obstacles. Empties <paramref name="exploded"/> (counting colour targets) and the golden balls
        /// going <paramref name="toBasket"/>, puts the <paramref name="born"/> specials in their cells, closes the gate of a basket
        /// column that has no free golden ball left, opens one random gate-less column per newly born golden ball while free
        /// golden balls outnumber the open gates (these rng draws come BEFORE the refill), collapses the columns and refills them
        /// from the top. <paramref name="b"/> is never changed. A triggered bomb / line ball counts for the bomb / line target (Pota hedefDus;
        /// 0 on every board but the pass ball's); basket drops count for nothing (Pota's golden target is U25). Fired pass balls fly after the
        /// births (<see cref="FlyPasses"/>, spec 2026-10-11 §1.4): their hits join the step's explosions and triggers.
        /// </summary>
        private static Board ExplodeAndFill(Board b, List<int> exploded, List<Born> born, List<BasketDrop> toBasket,
                                            List<Trigger> triggered, out Step step)
        {
            int n = b.Size;
            var result = b.Clone();
            step = new Step();
            step.Exploded.AddRange(exploded);
            var temp = new Block?[n * n];
            for (int i = 0; i < temp.Length; i++) temp[i] = b.Cells[i];
            foreach (int i in exploded)
            {
                if (temp[i] == null) continue;
                CountTarget(result, temp[i].Value.Color);
                temp[i] = null;
            }
            if (triggered != null)
            {
                step.Triggered.AddRange(triggered);
                foreach (var t in triggered) CountSpecialTarget(result, t.Kind);
            }
            if (toBasket != null)
                foreach (var d in toBasket) { temp[d.Cell] = null; step.ToBasket.Add(d); }
            if (born != null)
                foreach (var d in born) { temp[d.Cell] = Block.MakeSpecial(d.Kind); step.Born.Add(d); }
            var passes = new List<int>();
            if (triggered != null) foreach (var t in triggered) if (t.Kind == SpecialKind.Pass) passes.Add(t.Cell);
            if (passes.Count > 0)
            {
                var exclude = new HashSet<int>(exploded);
                if (born != null) foreach (var d in born) exclude.Add(d.Cell);
                var fired = new List<Trigger>();
                var added = FlyPasses(result, temp, passes, exclude, step.Passes, fired);
                step.Triggered.AddRange(fired);                  // Pota: [...tetiklenen, ...tetiklenenEk]
                if (added.Count > 0)
                {
                    var all = new SortedSet<int>(step.Exploded);
                    foreach (int i in added) all.Add(i);
                    step.Exploded.Clear();
                    step.Exploded.AddRange(all);
                }
            }
            uint rng = b.Rng;
            var gates = new List<int>();
            foreach (int c in b.Gates)
            {
                bool closes = false;
                if (toBasket != null)
                    foreach (var d in toBasket)
                        if (d.Col == c && !FreeGoldenIn(temp, n, c)) { closes = true; break; }
                if (!closes) gates.Add(c);
            }
            if (born != null)
            {
                int golden = 0;
                foreach (var t in temp) if (t != null && t.Value.Special == SpecialKind.Golden) golden++;
                foreach (var d in born)
                {
                    if (d.Kind != SpecialKind.Golden || golden <= gates.Count) continue;
                    var free = new List<int>();
                    for (int c = 0; c < n; c++) if (!gates.Contains(c)) free.Add(c);
                    if (free.Count == 0) continue;
                    rng = NextInt(rng, 0, free.Count - 1, out int pick);
                    gates.Add(free[pick]);
                    step.Opened.Add(free[pick]);
                }
            }
            for (int c = 0; c < n; c++)
            {
                // Scan the column bottom-up: every block falls to the lowest free cell.
                int dest = n - 1;
                for (int r = n - 1; r >= 0; r--)
                {
                    var block = temp[Index(n, r, c)];
                    if (block == null) continue;
                    int from = Index(n, r, c), to = Index(n, dest, c);
                    result.Cells[to] = block.Value;
                    if (to != from) step.Drops.Add(new Drop { From = from, To = to });
                    dest--;
                }
                // Remaining top cells (0..dest) are filled with new blocks falling `dest + 1` cells from above the frame.
                int empty = dest + 1;
                for (int r = dest; r >= 0; r--)
                {
                    rng = NextInt(rng, 0, b.ColorCount - 1, out int color);
                    rng = NextInt(rng, b.HpMin, b.HpMax, out int hp);
                    var block = new Block(color, hp);
                    int to = Index(n, r, c);
                    result.Cells[to] = block;
                    step.Spawns.Add(new Spawn { To = to, Block = block, Distance = empty });
                }
            }
            result.Rng = rng;
            result.Gates = gates;
            result.Golden = b.Golden + (toBasket != null ? toBasket.Count : 0);
            return result;
        }

        private static void CountTarget(Board b, int color)
        {
            if (color < 0) return;                       // a special block counts for no colour target
            for (int t = 0; t < b.Targets.Length; t++)
            {
                if (b.Targets[t].Color != color) continue;
                b.Targets[t].Remaining = Math.Max(0, b.Targets[t].Remaining - 1);
                break;
            }
        }

        private static bool FreeGoldenIn(Block?[] cells, int n, int col)
        {
            for (int r = 0; r < n; r++)
            {
                var block = cells[Index(n, r, col)];
                if (block != null && block.Value.Special == SpecialKind.Golden) return true;
            }
            return false;
        }

        /// <summary>
        /// Pota zinciriCoz: until the board settles, first golden balls on an open gate go into the basket, then runs explode
        /// (free) and give birth to specials. New arrivals can make new runs.
        /// </summary>
        public static Board ResolveChains(Board b, List<Step> steps, int maxSteps = ChainCap)
        {
            var current = b;
            for (int k = 0; k < maxSteps; k++)
            {
                if (TryDropToBasket(current, out var dropped, out var drop)) { current = dropped; steps.Add(drop); continue; }
                var exploded = ResolveMatches(current, out var born);
                if (exploded.Count == 0 && born.Count == 0) break;
                current = ExplodeAndFill(current, exploded, born, null, null, out var step);
                steps.Add(step);
            }
            return current;
        }

        /// <summary>
        /// The game's only move: tap a block (Pota kir). A line, a bomb or a pass ball fires for free (its <see cref="Blast"/> area; a pass ball
        /// then flies to its target, <see cref="FlyPasses"/>). A normal
        /// block costs its hp: if ammo covers it the block goes, ammo drops, the column collapses, new blocks fall in and chains
        /// resolve; otherwise the SAME board comes back (Insufficient). A golden ball does nothing (Golden, same board).
        /// </summary>
        public static BreakResult Break(Board b, int cell)
        {
            if (cell < 0 || cell >= b.Cells.Length) return Refused(b, BreakOutcome.Insufficient, 0);
            var block = b.Cells[cell];
            if (block.Special == SpecialKind.Golden) return Refused(b, BreakOutcome.Golden, 0);
            Board after;
            Step first;
            if (block.IsSpecial)
            {
                var area = Blast(b, cell);
                after = ExplodeAndFill(b, area.Cells, null, null, area.Triggered, out first);
            }
            else
            {
                if (b.Ammo < block.Hp) return Refused(b, BreakOutcome.Insufficient, block.Hp);
                var paid = b.Clone();
                paid.Ammo -= block.Hp;
                after = ExplodeAndFill(paid, new List<int> { cell }, null, null, null, out first);
            }
            var steps = new List<Step> { first };
            after = ResolveChains(after, steps);
            // Pota's kir ends with hakemOyna, which (with no referee on the board) runs zinciriCoz once more: it only adds steps
            // when the first pass stopped at ChainCap.
            after = ResolveChains(after, steps);
            return new BreakResult { Broken = true, Outcome = BreakOutcome.Broken, Board = after, Steps = steps, Cost = block.IsSpecial ? 0 : block.Hp };
        }

        private static BreakResult Refused(Board b, BreakOutcome outcome, int cost) =>
            new BreakResult { Broken = false, Outcome = outcome, Board = b, Steps = new List<Step>(), Cost = cost };

        // ---------- state ----------

        public static bool Won(Board b)
        {
            foreach (var t in b.Targets) if (t.Remaining > 0) return false;
            return b.BombTarget <= 0 && b.LineTarget <= 0;
        }

        /// <summary>Pota enUcuz: the cheapest NORMAL block; specials (hp 0) do not count. int.MaxValue when only specials remain.</summary>
        public static int Cheapest(Board b)
        {
            int min = int.MaxValue;
            foreach (var c in b.Cells) if (!c.IsSpecial && c.Hp < min) min = c.Hp;
            return min;
        }

        /// <summary>Pota hamleVar: a line, a bomb or a pass ball can always be fired (free); otherwise ammo must cover the cheapest block.</summary>
        public static bool HasMove(Board b)
        {
            foreach (var c in b.Cells) if (c.IsSpecial && c.Special != SpecialKind.Golden) return true;
            return b.Ammo > 0 && b.Ammo >= Cheapest(b);
        }

        public static int RemainingTarget(Board b)
        {
            int sum = 0;
            foreach (var t in b.Targets) sum += Math.Max(0, t.Remaining);
            return sum + Math.Max(0, b.BombTarget) + Math.Max(0, b.LineTarget);
        }
    }
}
