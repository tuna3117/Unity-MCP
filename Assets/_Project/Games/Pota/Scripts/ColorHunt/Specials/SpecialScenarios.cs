namespace Project.ColorHunt
{
    public enum SpecialScenario { Birth, Line, Bomb, Chain, Golden, Free, Idle }

    /// <summary>A hand-authored board (the demo's scenarios, tests): turned into a Board by ColorHuntRules.FromPreset.</summary>
    public sealed class BoardPreset
    {
        public int Size = 7;
        public int ColorCount = 5;
        public int HpMin = 3, HpMax = 14;
        public int Ammo = 158;
        /// <summary>Row by row, row 0 at the top; -1 under a special (its kind is in <see cref="Specials"/>).</summary>
        public int[] Colors;
        /// <summary>Null: HpMin + (7·i mod (HpMax − HpMin + 1)).</summary>
        public int[] Hp;
        public SpecialKind[] Specials;
        /// <summary>Open gates (columns).</summary>
        public int[] Gates;
        public Features Features = Features.All;
        public uint Rng = 1;
        public (int color, int count)[] Targets = { (0, 22), (1, 16) };
    }

    /// <summary>
    /// The demo's boards (spec §1.3; Bekleme from spec 2026-10-07) and their scripted taps. Every grid is the base pattern (2r + c) mod 5 — no two neighbours alike,
    /// so nothing matches unless designed — with its specials (-1) and, for Birth, designed runs. The outcomes were checked against Pota's
    /// own src/renkAvi.ts (rng 1) and are pinned by SpecialScenariosTests.
    /// </summary>
    public static class SpecialScenarios
    {
        public const int Size = 7;

        /// <summary>
        /// Tap 44 (6, 2): the 0 above drops in → row 6 reads 0 0 0 0 0 → a golden ball at (6, 1) (it opens a gate: rng, column 3); column 2
        /// closing up lines row 4 up as 3 3 3 3 → a line ball (LineV) at (4, 1). Tap 34 (4, 6): column 6 closes up 1 1 1 1 → LineH at (6, 6).
        /// Tap 32 (4, 4): the 2 above drops between two 2s and onto two 2s → a T → a bomb at (4, 4).
        /// </summary>
        private static readonly int[] BirthGrid =
        {
            0, 1, 2, 3, 4, 0, 1,
            2, 3, 4, 0, 1, 2, 3,
            4, 0, 1, 2, 2, 4, 1,
            1, 2, 3, 2, 0, 1, 1,
            3, 3, 0, 3, 2, 2, 4,
            0, 1, 0, 3, 2, 0, 1,
            0, 0, 4, 0, 0, 2, 1,
        };

        /// <summary>LineH at (3, 1) clears row 3; the LineV at (2, 5) falls to (3, 5) and clears column 5.</summary>
        private static readonly int[] LineGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3, -1,  0,
             1, -1,  3,  4,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        /// <summary>A bomb in the middle (3, 3), one in the corner (6, 6) whose 3×3 is clipped to 2×2.</summary>
        private static readonly int[] BombGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3,  4,  0,
             1,  2,  3, -1,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2, -1,
        };

        /// <summary>LineH (3, 0) → the bomb (3, 4) in its row → the LineV (2, 5) in the bomb's 3×3 → column 5.</summary>
        private static readonly int[] ChainGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3, -1,  0,
            -1,  2,  3,  4, -1,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        /// <summary>Golden balls at (5, 3) and (6, 5), gates 3 and 5: tapping (6, 3) drops the first onto its gate; both fly, 0.12 s apart.</summary>
        private static readonly int[] GoldenGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3,  4,  0,
             1,  2,  3,  4,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2, -1,  4,  0,  1,
             2,  3,  4,  0,  1, -1,  3,
        };

        /// <summary>One of each: LineV (1, 1), golden (2, 4) over gate 4, bomb (3, 3), LineH (5, 5). No script: Tuna plays.</summary>
        private static readonly int[] FreeGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2, -1,  4,  0,  1,  2,  3,
             4,  0,  1,  2, -1,  4,  0,
             1,  2,  3, -1,  0,  1,  2,
             3,  4,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4, -1,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        /// <summary>
        /// Bekleme (spec 2026-10-07): the calm idles' stage — a bomb (3, 2) and a LineV (3, 4) either side of the centre, a golden ball
        /// (2, 3) above it over gate 3, a LineH (4, 3) below. Every special touches the centre cell, so a close-up on cell 24 shows all four
        /// and whether anything spills into a neighbour. No script: they just rest.
        /// </summary>
        private static readonly int[] IdleGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1, -1,  3,  4,  0,
             1,  2, -1,  4, -1,  1,  2,
             3,  4,  0, -1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        /// <summary>The Bekleme board's centre cell (3, 3): the idle recordings zoom on it.</summary>
        public const int IdleFocus = 24;

        public static string Title(SpecialScenario s)
        {
            switch (s)
            {
                case SpecialScenario.Birth: return "Doğuş";
                case SpecialScenario.Line: return "Çizgi";
                case SpecialScenario.Bomb: return "Bomba";
                case SpecialScenario.Chain: return "Zincir";
                case SpecialScenario.Golden: return "Altın";
                case SpecialScenario.Idle: return "Bekleme";
                default: return "Serbest";
            }
        }

        /// <summary>A fresh preset (callers may change it).</summary>
        public static BoardPreset Board(SpecialScenario s)
        {
            switch (s)
            {
                case SpecialScenario.Birth: return Preset(BirthGrid, new int[0]);
                case SpecialScenario.Line: return Preset(LineGrid, new int[0], (22, SpecialKind.LineH), (19, SpecialKind.LineV));
                case SpecialScenario.Bomb: return Preset(BombGrid, new int[0], (24, SpecialKind.Bomb), (48, SpecialKind.Bomb));
                case SpecialScenario.Chain: return Preset(ChainGrid, new int[0], (21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV));
                case SpecialScenario.Golden: return Preset(GoldenGrid, new[] { 3, 5 }, (38, SpecialKind.Golden), (47, SpecialKind.Golden));
                case SpecialScenario.Idle: return Preset(IdleGrid, new[] { 3 }, (23, SpecialKind.Bomb), (25, SpecialKind.LineV), (17, SpecialKind.Golden), (31, SpecialKind.LineH));
                default: return Preset(FreeGrid, new[] { 4 }, (8, SpecialKind.LineV), (18, SpecialKind.Golden), (24, SpecialKind.Bomb), (40, SpecialKind.LineH));
            }
        }

        /// <summary>The scripted taps, in order (cells as they are when each tap comes).</summary>
        public static int[] Taps(SpecialScenario s)
        {
            switch (s)
            {
                case SpecialScenario.Birth: return new[] { 44, 34, 32 };
                case SpecialScenario.Line: return new[] { 22, 26 };
                case SpecialScenario.Bomb: return new[] { 24, 48 };
                case SpecialScenario.Chain: return new[] { 21 };
                case SpecialScenario.Golden: return new[] { 45 };
                default: return new int[0];
            }
        }

        private static BoardPreset Preset(int[] grid, int[] gates, params (int cell, SpecialKind kind)[] specials)
        {
            var kinds = new SpecialKind[grid.Length];
            foreach (var (cell, kind) in specials) kinds[cell] = kind;
            return new BoardPreset { Colors = (int[])grid.Clone(), Specials = kinds, Gates = (int[])gates.Clone() };
        }
    }
}
