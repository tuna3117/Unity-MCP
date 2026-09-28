using System;
using System.Collections.Generic;

namespace Project.ColorHunt
{
    /// <summary>One block on the board: palette index and hit points (balls needed to break it).</summary>
    [Serializable]
    public struct Block
    {
        public int Color;
        public int Hp;
        public Block(int color, int hp) { Color = color; Hp = hp; }
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

    /// <summary>One animation frame: <see cref="Exploded"/> go first, then <see cref="Drops"/> and <see cref="Spawns"/> fall.</summary>
    public class Step
    {
        public readonly List<int> Exploded = new List<int>();
        public readonly List<Drop> Drops = new List<Drop>();
        public readonly List<Spawn> Spawns = new List<Spawn>();
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
            };
        }
    }

    public struct BreakResult
    {
        public bool Broken;
        public Board Board;
        public List<Step> Steps;
        public int Cost;
    }

    /// <summary>
    /// Pure colour-hunt rules, a port of Pota's src/renkAvi.ts: the board never empties, a tap costs the
    /// block's hp in balls, columns collapse, random blocks enter from the top, 3+ runs explode for free,
    /// rotation is free and never shifts anything.
    /// </summary>
    public static class ColorHuntRules
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

        /// <summary>Horizontal and vertical runs of 3+ (no diagonals). Raw runs: an L/T cell can be in two runs.</summary>
        public static List<Match> FindMatches(Board b)
        {
            int n = b.Size;
            var list = new List<Match>();
            void Run(Func<int, int> cell, bool horizontal)
            {
                int start = 0;
                for (int k = 1; k <= n; k++)
                {
                    bool same = k < n && b.Cells[cell(k)].Color == b.Cells[cell(start)].Color;
                    if (same) continue;
                    if (k - start >= MatchLength)
                    {
                        var m = new Match { Color = b.Cells[cell(start)].Color, Horizontal = horizontal };
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

        private static bool MakesTriple(List<Block> cells, int n, int r, int c, int color)
        {
            if (c >= 2 && cells[Index(n, r, c - 1)].Color == color && cells[Index(n, r, c - 2)].Color == color) return true;
            if (r >= 2 && cells[Index(n, r - 1, c)].Color == color && cells[Index(n, r - 2, c)].Color == color) return true;
            return false;
        }

        /// <summary>Fills the board without triples (pairs allowed); with 3+ colours there is always a way out.</summary>
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
                        if (!MakesTriple(cells, n, r, c, color)) break;
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
            };
        }

        // ---------- rotation ----------

        /// <summary>90° rotation: new[c·n + (n−1−r)] = old[r·n + c] (clockwise). The board is full, so nothing shifts.</summary>
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

        private static Board ExplodeAndFill(Board b, List<int> exploded, out Step step)
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
                int color = temp[i].Value.Color;
                for (int t = 0; t < result.Targets.Length; t++)
                {
                    if (result.Targets[t].Color != color) continue;
                    result.Targets[t].Remaining = Math.Max(0, result.Targets[t].Remaining - 1);
                    break;
                }
                temp[i] = null;
            }
            uint rng = b.Rng;
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
            return result;
        }

        /// <summary>Explodes triples until the board settles (free). New arrivals can make new triples.</summary>
        public static Board ResolveChains(Board b, List<Step> steps, int maxSteps = ChainCap)
        {
            var current = b;
            for (int k = 0; k < maxSteps; k++)
            {
                var matches = FindMatches(current);
                if (matches.Count == 0) break;
                var set = new SortedSet<int>();
                foreach (var m in matches) foreach (int i in m.Cells) set.Add(i);
                current = ExplodeAndFill(current, new List<int>(set), out var step);
                steps.Add(step);
            }
            return current;
        }

        /// <summary>
        /// The game's only move: tap a block. If ammo covers its hp the block goes, ammo drops, the column
        /// collapses, new blocks fall in and chains resolve. Otherwise the SAME board comes back.
        /// </summary>
        public static BreakResult Break(Board b, int cell)
        {
            if (cell < 0 || cell >= b.Cells.Length) return new BreakResult { Broken = false, Board = b, Steps = new List<Step>(), Cost = 0 };
            var block = b.Cells[cell];
            if (b.Ammo < block.Hp) return new BreakResult { Broken = false, Board = b, Steps = new List<Step>(), Cost = block.Hp };
            var paid = b.Clone();
            paid.Ammo -= block.Hp;
            var after = ExplodeAndFill(paid, new List<int> { cell }, out var first);
            var steps = new List<Step> { first };
            after = ResolveChains(after, steps);
            return new BreakResult { Broken = true, Board = after, Steps = steps, Cost = block.Hp };
        }

        // ---------- state ----------

        public static bool Won(Board b)
        {
            foreach (var t in b.Targets) if (t.Remaining > 0) return false;
            return true;
        }

        public static int Cheapest(Board b)
        {
            int min = int.MaxValue;
            foreach (var c in b.Cells) if (c.Hp < min) min = c.Hp;
            return min;
        }

        /// <summary>Ammo covers the cheapest block; otherwise the level is dead.</summary>
        public static bool HasMove(Board b) => b.Ammo > 0 && b.Ammo >= Cheapest(b);

        public static int RemainingTarget(Board b)
        {
            int sum = 0;
            foreach (var t in b.Targets) sum += Math.Max(0, t.Remaining);
            return sum;
        }
    }
}
