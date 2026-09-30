namespace Project.ColorHunt
{
    /// <summary>The move a hint (and the recorder's "auto") points at.</summary>
    public static class ColorHuntHint
    {
        /// <summary>The affordable block whose break gives the longest chain (ties: the cheapest); −1 when nothing is affordable.</summary>
        public static int Best(Board b)
        {
            int best = -1, bestSteps = -1, bestHp = int.MaxValue;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var block = b.Cells[i];
                if (block.Hp > b.Ammo) continue;
                int steps = ColorHuntRules.Break(b, i).Steps.Count;           // pure: Break clones the board
                if (steps > bestSteps || (steps == bestSteps && block.Hp < bestHp)) { best = i; bestSteps = steps; bestHp = block.Hp; }
            }
            return best;
        }
    }
}
