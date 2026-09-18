using UnityEngine;

namespace Project.Basket
{
    /// <summary>End-of-round and "missed" predicates ported from the original game (kurallar.ts).</summary>
    public static class RoundRules
    {
        public const float MissedMarginBelowRim = 0.2f;
        public const float SettledPileSpeed = 0.25f;
        public const float QuietSecondsAfterLastEntry = 1.5f;

        public static bool FewLeft(int active, int total)
            => active == 0 || (total >= 20 && active <= Mathf.Max(2, Mathf.RoundToInt(total * 0.02f)));

        public static bool IsMissed(int inFlight, int topPasses, float highestBallY, float lowestTopRimY)
            => inFlight == 0 && topPasses == 0 && highestBallY <= lowestTopRimY - MissedMarginBelowRim;

        public static bool ShouldEnd(int active, int total, float pileMaxSpeed, float sinceLastEntry, float elapsed, float timeout = 30f)
            => elapsed >= timeout
               || (FewLeft(active, total) && (pileMaxSpeed < SettledPileSpeed || sinceLastEntry >= QuietSecondsAfterLastEntry));
    }
}
