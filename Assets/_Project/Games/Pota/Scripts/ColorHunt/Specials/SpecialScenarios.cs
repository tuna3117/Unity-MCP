namespace Project.ColorHunt
{
    public enum SpecialScenario { Birth, Line, Bomb, Chain, Golden, Free, Idle, Pass, PassBomb }

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
        /// <summary>Pota's "bomba" / "cizgi" targets (0 = none): the pass ball flies to a bomb / line ball while one is in a target.</summary>
        public int BombTarget, LineTarget;
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

        /// <summary>
        /// Pas (Pota spec 2026-10-11): tap 31 (4, 3) — the 2 above drops in and closes a 2×2 of 2s at rows 4–5, cols 2–3 (nothing else lines
        /// up) → a pass ball at its bottom-left (5, 2) = 37 (Royal: the other three gather into it). It rests a second, then the tap on it:
        /// the target is colour 2 (one target only, so the flight is not to a neighbour) and the nearest 2 is (3, 1) = 22, √5 cells away.
        /// Then a refill chain in column 2. Pinned against Pota by PassRulesTests (Scenario_pass).
        /// </summary>
        private static readonly int[] PassGrid =
        {
            0, 1, 2, 3, 4, 0, 1,
            2, 3, 4, 0, 1, 2, 3,
            4, 0, 1, 3, 3, 4, 0,
            1, 2, 3, 2, 0, 1, 2,
            3, 4, 2, 1, 0, 3, 4,
            0, 1, 2, 2, 4, 0, 1,
            2, 3, 4, 0, 1, 2, 3,
        };

        /// <summary>
        /// Pas→Bomba: a pass ball at (4, 1) = 29, a bomb at (2, 5) = 19, and only a bomb target (2): the pass flies √20 cells to the bomb, which
        /// bursts its 3×3 the moment the ball lands (its plan root is the arrival). Pinned by PassRulesTests (Scenario_passBomb).
        /// </summary>
        private static readonly int[] PassBombGrid =
        {
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
             4,  0,  1,  2,  3, -1,  0,
             1,  2,  3,  4,  0,  1,  2,
             3, -1,  0,  1,  2,  3,  4,
             0,  1,  2,  3,  4,  0,  1,
             2,  3,  4,  0,  1,  2,  3,
        };

        /// <summary>The close-up cell of a scenario (the recorder's default focus): Pas between the square and the flight, Pas→Bomba mid-flight; −1 = its first tap.</summary>
        public static int Focus(SpecialScenario s) => s == SpecialScenario.Pass ? 30 : s == SpecialScenario.PassBomb ? 24 : s == SpecialScenario.Idle ? IdleFocus : -1;

        /// <summary>Game seconds the demo waits on a settled board before a scripted tap: Pas rests its newborn ball a second (spec §4 "bekleme 1 sn").</summary>
        public static float Beat(SpecialScenario s, float beat) => s == SpecialScenario.Pass ? 1f : beat;

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
                case SpecialScenario.Pass: return "Pas";
                case SpecialScenario.PassBomb: return "Pas→Bomba";
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
                case SpecialScenario.Pass:
                {
                    var p = Preset(PassGrid, new int[0]);
                    p.Features = Features.Full;
                    p.Targets = new[] { (2, 12) };
                    return p;
                }
                case SpecialScenario.PassBomb:
                {
                    var p = Preset(PassBombGrid, new int[0], (29, SpecialKind.Pass), (19, SpecialKind.Bomb));
                    p.Features = Features.Full;
                    p.Targets = new (int, int)[0];
                    p.BombTarget = 2;
                    return p;
                }
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
                case SpecialScenario.Pass: return new[] { 31, 37 };
                case SpecialScenario.PassBomb: return new[] { 29 };
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
