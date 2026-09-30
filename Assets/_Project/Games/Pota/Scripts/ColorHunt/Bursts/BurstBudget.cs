namespace Project.ColorHunt
{
    /// <summary>Spec §1.5: live particles and pieces per look ≤ 1.5 × Pota's pools.</summary>
    public static class BurstBudget
    {
        /// <summary>ColorHuntFx pools: white flash 12, tone flash 12, ring 12, stars 72, points 160, puff 16, shards 160, confetti 240.</summary>
        public const int Pota = 12 + 12 + 12 + 72 + 160 + 16 + 160 + 240;
        public const int Max = Pota * 3 / 2;
    }
}
