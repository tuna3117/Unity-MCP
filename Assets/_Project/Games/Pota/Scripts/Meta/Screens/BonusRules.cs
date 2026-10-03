using UnityEngine;

namespace Project.Meta
{
    /// <summary>
    /// Pota bonus.ts, ported: n columns × 3 rows of objects from a seeded stream (weights by column count), every spin costs a right,
    /// the whole grid is counted — coins pay 10 each from 3, four clocks give 30 min of infinite lives, four rights give 3 more.
    /// Winnings wait until the level is passed (the showcase never credits them).
    /// </summary>
    public static class BonusRules
    {
        public enum Kind { Item, Coin, Clock, Right }
        public struct Cell { public Kind Kind; public int Colour; }
        public struct Winnings
        {
            public int Coins, InfiniteLifeMinutes, ExtraRights;
            public bool Any => Coins > 0 || InfiniteLifeMinutes > 0;
        }
        public class State
        {
            public int Columns, Rows = Rows3, Rights = StartRights, Seed, Spins;
            public Cell[] Cells;
            public Winnings Winnings;
        }

        public const int Rows3 = 3, StartRights = 3, Colours = 5;
        public const int CoinThreshold = 3, CoinEach = 10, ClockThreshold = 4, ClockMinutes = 30, RightThreshold = 4, RightExtra = 3;

        private static (float item, float coin, float clock, float right) Weights(int columns) => columns <= 6 ? (0.68f, 0.14f, 0.09f, 0.09f) : (0.72f, 0.12f, 0.08f, 0.08f);

        private static float Next(ref uint s)
        {
            s = s * 1664525u + 1013904223u;
            return (s >> 8) / 16777216f;
        }

        /// <summary>A grid from <paramref name="seed"/>; <paramref name="next"/> is the seed of the following grid (the stream keeps going).</summary>
        public static Cell[] Grid(int columns, int rows, int seed, out int next)
        {
            var w = Weights(columns);
            uint s = (uint)seed * 2654435761u + 7u;
            var cells = new Cell[columns * rows];
            for (int i = 0; i < cells.Length; i++)
            {
                float v = Next(ref s);
                if (v < w.coin) cells[i] = new Cell { Kind = Kind.Coin };
                else if (v < w.coin + w.clock) cells[i] = new Cell { Kind = Kind.Clock };
                else if (v < w.coin + w.clock + w.right) cells[i] = new Cell { Kind = Kind.Right };
                else cells[i] = new Cell { Kind = Kind.Item, Colour = Mathf.Min(Colours - 1, (int)(Next(ref s) * Colours)) };
            }
            next = (int)(s & 0x7fffffff);
            return cells;
        }

        /// <summary>Counts the grid: coins × 10 from three, a fixed 30 min from four clocks, a fixed +3 from four rights.</summary>
        public static Winnings Evaluate(Cell[] cells)
        {
            int coins = 0, clocks = 0, rights = 0;
            foreach (var c in cells) { if (c.Kind == Kind.Coin) coins++; else if (c.Kind == Kind.Clock) clocks++; else if (c.Kind == Kind.Right) rights++; }
            return new Winnings
            {
                Coins = coins >= CoinThreshold ? coins * CoinEach : 0,
                InfiniteLifeMinutes = clocks >= ClockThreshold ? ClockMinutes : 0,
                ExtraRights = rights >= RightThreshold ? RightExtra : 0,
            };
        }

        /// <summary>Which cells paid (their kind reached its threshold).</summary>
        public static bool[] Winning(Cell[] cells)
        {
            var w = Evaluate(cells);
            var win = new bool[cells.Length];
            for (int i = 0; i < cells.Length; i++)
                win[i] = (cells[i].Kind == Kind.Coin && w.Coins > 0) || (cells[i].Kind == Kind.Clock && w.InfiniteLifeMinutes > 0) || (cells[i].Kind == Kind.Right && w.ExtraRights > 0);
            return win;
        }

        public static State Start(int columns, int seed)
        {
            var st = new State { Columns = columns, Seed = seed };
            st.Cells = Grid(columns, st.Rows, seed, out st.Seed);
            return st;
        }

        /// <summary>One spin: a right goes, a new grid, its winnings added (extra rights included). No rights → nothing happens.</summary>
        public static State Spin(State s, out Winnings added)
        {
            added = new Winnings();
            if (s.Rights <= 0) return s;
            var n = new State { Columns = s.Columns, Rows = s.Rows, Rights = s.Rights - 1, Spins = s.Spins + 1, Winnings = s.Winnings };
            n.Cells = Grid(s.Columns, s.Rows, s.Seed, out n.Seed);
            added = Evaluate(n.Cells);
            n.Rights += added.ExtraRights;
            n.Winnings = new Winnings
            {
                Coins = s.Winnings.Coins + added.Coins,
                InfiniteLifeMinutes = s.Winnings.InfiniteLifeMinutes + added.InfiniteLifeMinutes,
                ExtraRights = s.Winnings.ExtraRights + added.ExtraRights,
            };
            return n;
        }

        public static bool Over(State s) => s.Rights <= 0;

        private static int _showcase = -1;
        /// <summary>The first seed whose first 7-column spin pays coins, so the showcase and its recording show the flight.</summary>
        public static int ShowcaseSeed
        {
            get
            {
                if (_showcase >= 0) return _showcase;
                for (int seed = 1; seed < 100000; seed++)
                {
                    Spin(Start(7, seed), out var w);
                    if (w.Coins > 0) { _showcase = seed; break; }
                }
                return _showcase;
            }
        }

        public static string Text(Winnings w)
        {
            if (!w.Any && w.ExtraRights == 0) return "Bu sefer olmadı";
            var parts = new System.Collections.Generic.List<string>();
            if (w.Coins > 0) parts.Add("+" + w.Coins + " para");
            if (w.InfiniteLifeMinutes > 0) parts.Add("Sonsuz can " + w.InfiniteLifeMinutes + " dk");
            if (w.ExtraRights > 0) parts.Add("+" + w.ExtraRights + " hak");
            return "Kazanç: " + string.Join(" · ", parts);
        }
    }
}
