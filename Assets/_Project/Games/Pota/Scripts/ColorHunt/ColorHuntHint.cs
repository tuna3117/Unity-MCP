namespace Project.ColorHunt
{
    /// <summary>The move a hint (and the recorder's "auto") points at.</summary>
    public static class ColorHuntHint
    {
        /// <summary>
        /// The affordable block with the highest <see cref="Score"/> (ties: the cheapest); −1 when nothing is affordable.
        /// Backlog U40 ruling (2026-10-03): chains that collect target items outrank equally long chains that do not.
        /// </summary>
        public static int Best(Board b)
        {
            int best = -1, bestScore = -1, bestHp = int.MaxValue;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var block = b.Cells[i];
                if (block.Hp > b.Ammo) continue;
                int score = Score(b, i);
                if (score > bestScore || (score == bestScore && block.Hp < bestHp)) { best = i; bestScore = score; bestHp = block.Hp; }
            }
            return best;
        }

        /// <summary>
        /// Chain steps + target items the break collects (Break is pure: it clones the board). The items are what the targets lost
        /// between the board and the break's result: reading colours through the pre-tap board at exploded indices counted dropped and
        /// spawned blocks under the wrong colour (review 2026-10-03).
        /// </summary>
        public static int Score(Board b, int cell)
        {
            var r = ColorHuntRules.Break(b, cell);
            int items = 0;
            for (int t = 0; t < b.Targets.Length && t < r.Board.Targets.Length; t++)
                items += System.Math.Max(0, b.Targets[t].Remaining) - System.Math.Max(0, r.Board.Targets[t].Remaining);
            return r.Steps.Count + items;
        }

        /// <summary>
        /// The first seed in 1..maxSeeds whose best affordable tap explodes blocks in at least minSteps steps (the tap's own step counted:
        /// depth-3 words need 4 exploding steps); 0 when none does. Recorder "chain" preset and tests.
        /// </summary>
        public static uint ChainSeed(LevelDesign design, int ammo, int minSteps, int maxSeeds)
        {
            for (uint seed = 1; seed <= maxSeeds; seed++)
            {
                var b = ColorHuntRules.Create(design, ammo, seed);
                int best = Best(b);
                if (best >= 0 && ExplodingSteps(ColorHuntRules.Break(b, best)) >= minSteps) return seed;
            }
            return 0;
        }

        /// <summary>Steps of a break that explode at least one block (drop-only steps do not count).</summary>
        public static int ExplodingSteps(BreakResult r)
        {
            int n = 0;
            foreach (var s in r.Steps) if (s.Exploded.Count > 0) n++;
            return n;
        }

        private static bool IsTarget(Board b, int color)
        {
            foreach (var t in b.Targets) if (t.Color == color && t.Remaining > 0) return true;
            return false;
        }
    }
}
