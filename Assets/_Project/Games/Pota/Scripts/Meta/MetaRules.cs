using UnityEngine;

namespace Project.Meta
{
    /// <summary>What a showcase win changed (Pota akis.ts fazBBitti), computed on a copy of the save.</summary>
    public struct WinResult
    {
        public SampleSave Save;
        public int Score, CoinsGained, LeftoverCoins, TrophyGain, LeagueBefore, LeagueAfter, MedalIndex;
        public bool Promoted;
    }

    /// <summary>Pota's reward and ranking rules, pure (akis.ts ODUL, skor.ts medal/league).</summary>
    public static class MetaRules
    {
        public static SampleSave Copy(SampleSave s) => new SampleSave
        {
            Level = s.Level, Coins = s.Coins, Lives = s.Lives, MaxLives = s.MaxLives, Trophies = s.Trophies, BestScore = s.BestScore, FirstTry = s.FirstTry,
            ScoreBlocks = s.ScoreBlocks, ScoreChain = s.ScoreChain, ScoreSpecial = s.ScoreSpecial, ScoreGold = s.ScoreGold, ScoreLeft = s.ScoreLeft,
            ScoreObstacle = s.ScoreObstacle, LeftoverBalls = s.LeftoverBalls, RemainingOnLoss = s.RemainingOnLoss, MedalSilver = s.MedalSilver, MedalGold = s.MedalGold,
        };

        /// <summary>0 bronze, 1 silver, 2 gold (skor.ts madalya: ≥ gold → gold, ≥ silver → silver, else bronze).</summary>
        public static int Medal(int score, int silver, int gold) => score >= gold ? 2 : score >= silver ? 1 : 0;

        /// <summary>The showcase win's score from the sample's lines (skor.ts skorHesapla; first try × 1.2, rounded).</summary>
        public static int Score(SampleSave s)
        {
            int sum = s.ScoreBlocks + s.ScoreChain + s.ScoreSpecial + s.ScoreGold + s.ScoreLeft + s.ScoreObstacle;
            return Mathf.RoundToInt(sum * (s.FirstTry ? 1.2f : 1f));
        }

        public static WinResult Win(MetaSettings settings, int score, int leftover)
        {
            var before = settings.Save;
            var save = Copy(before);
            int leftoverCoins = ColorHunt.FeelCurves.LeftoverCoins(leftover);
            int gained = settings.LevelReward + (before.FirstTry ? settings.FirstTryReward : 0) + leftoverCoins;
            int gain = Mathf.Max(0, score - before.BestScore);               // kupaArtisi: only a new best adds trophies
            save.Coins += gained;
            save.Trophies += gain;
            save.BestScore = Mathf.Max(before.BestScore, score);
            save.Level += 1;
            save.FirstTry = true;
            int lb = settings.LeagueOf(before.Trophies), la = settings.LeagueOf(save.Trophies);
            return new WinResult
            {
                Save = save, Score = score, CoinsGained = gained, LeftoverCoins = leftoverCoins, TrophyGain = gain,
                LeagueBefore = lb, LeagueAfter = la, Promoted = la > lb, MedalIndex = Medal(score, before.MedalSilver, before.MedalGold),
            };
        }

        public static SampleSave Lose(MetaSettings settings)
        {
            var save = Copy(settings.Save);
            save.Lives = Mathf.Max(0, save.Lives - 1);
            save.FirstTry = false;
            return save;
        }

        /// <summary>League bar fill 0..1 for a trophy total: position between this league's floor and the next's.</summary>
        public static float BarFill(MetaSettings settings, int trophies)
        {
            int l = settings.LeagueOf(trophies);
            if (l >= settings.LeagueThresholds.Length - 1) return 1f;
            float lo = settings.LeagueThresholds[l], hi = settings.LeagueThresholds[l + 1];
            return Mathf.Clamp01((trophies - lo) / Mathf.Max(1f, hi - lo));
        }

        /// <summary>Trophies still needed for the next league (0 at the top).</summary>
        public static int ToNextLeague(MetaSettings settings, int trophies)
        {
            int l = settings.LeagueOf(trophies);
            return l >= settings.LeagueThresholds.Length - 1 ? 0 : Mathf.Max(0, settings.LeagueThresholds[l + 1] - trophies);
        }
    }

    /// <summary>The main screen's pure curves (M7).</summary>
    public static class MetaCurves
    {
        /// <summary>1 → 1.02 → 1 over <paramref name="period"/> seconds (Pota vitrinNefes).</summary>
        public static float Breath(float t, float period) => 1f + 0.02f * (0.5f - 0.5f * Mathf.Cos(t / Mathf.Max(0.01f, period) * 2f * Mathf.PI));

        /// <summary>The shine's position −1 (left, parked) … +1 (right) during a <paramref name="sweep"/> every <paramref name="every"/> seconds; −1 while resting.</summary>
        public static float Shine(float t, float every, float sweep)
        {
            float local = t % Mathf.Max(0.01f, every);
            return local <= sweep ? Mathf.Lerp(-1f, 1f, local / sweep) : -1f;
        }
    }
}
