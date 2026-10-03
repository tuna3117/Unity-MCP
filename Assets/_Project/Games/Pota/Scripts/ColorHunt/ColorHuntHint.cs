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

        /// <summary>Chain steps + exploded cells whose colour is still a target (Break is pure: it clones the board).</summary>
        public static int Score(Board b, int cell)
        {
            var r = ColorHuntRules.Break(b, cell);
            int items = 0;
            foreach (var step in r.Steps)
                foreach (int c in step.Exploded)
                    if (IsTarget(b, b.Cells[c].Color)) items++;
            return r.Steps.Count + items;
        }

        private static bool IsTarget(Board b, int color)
        {
            foreach (var t in b.Targets) if (t.Color == color && t.Remaining > 0) return true;
            return false;
        }
    }
}
