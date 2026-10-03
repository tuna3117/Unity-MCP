using NUnit.Framework;
using Project.Meta;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The showcase's save rules (Pota akis.ts) and the main screen's pure curves.</summary>
    public class MetaDirectorTests
    {
        private static MetaSettings Settings()
        {
            var s = ScriptableObject.CreateInstance<MetaSettings>();
            s.Save = new SampleSave { Level = 20, Coins = 145, Lives = 3, Trophies = 11800, FirstTry = true, LeftoverBalls = 137 };
            return s;
        }

        [Test]
        public void Win_PaysLevelFirstTryAndLeftoverCoins_AddsTrophies_AdvancesTheLevel()
        {
            var s = Settings();
            var r = MetaRules.Win(s, score: 116, leftover: 137);
            Assert.AreEqual(15 + 10 + 13, r.CoinsGained, "15 level + 10 first try + floor(137 × 0.1) leftover (capped at 20)");
            Assert.AreEqual(145 + 38, r.Save.Coins);
            Assert.AreEqual(11800 + 116, r.Save.Trophies, "trophies grow by the level's best score gain");
            Assert.AreEqual(21, r.Save.Level);
            Assert.AreEqual(0, r.Save.BestScore, "the next level starts without a best (Pota keeps one per level)");
            Assert.AreEqual(0, r.LeagueBefore);
            Assert.AreEqual(0, r.LeagueAfter);
        }

        [Test]
        public void Win_ThatCrossesALeagueThreshold_Promotes()
        {
            var s = Settings();
            s.Save.Trophies = 12300;
            var r = MetaRules.Win(s, score: 200, leftover: 0);
            Assert.AreEqual(0, r.LeagueBefore);
            Assert.AreEqual(1, r.LeagueAfter);
            Assert.IsTrue(r.Promoted);
        }

        [Test]
        public void Lose_TakesALife_KeepsEverythingElse()
        {
            var s = Settings();
            var r = MetaRules.Lose(s);
            Assert.AreEqual(2, r.Lives);
            Assert.AreEqual(145, r.Coins);
            Assert.AreEqual(20, r.Level);
        }

        [Test]
        public void Medal_FollowsPotasThresholds()
        {
            Assert.AreEqual(0, MetaRules.Medal(116, 300, 420), "bronze");
            Assert.AreEqual(1, MetaRules.Medal(300, 300, 420), "silver at the threshold");
            Assert.AreEqual(2, MetaRules.Medal(421, 300, 420), "gold");
        }

        [Test]
        public void Breath_IsASlowUnitSine()
        {
            Assert.AreEqual(1f, MetaCurves.Breath(0f, 3.2f), 1e-4f);
            Assert.AreEqual(1.02f, MetaCurves.Breath(1.6f, 3.2f), 1e-3f);
            Assert.AreEqual(1f, MetaCurves.Breath(3.2f, 3.2f), 1e-3f);
        }

        [Test]
        public void Shine_SweepsLeftToRightThenRests()
        {
            Assert.AreEqual(-1f, MetaCurves.Shine(0f, 2.2f, 0.35f), 1e-4f, "parked left");
            Assert.AreEqual(0f, MetaCurves.Shine(0.175f, 2.2f, 0.35f), 1e-3f, "mid-sweep at the centre");
            Assert.AreEqual(1f, MetaCurves.Shine(0.35f, 2.2f, 0.35f), 1e-4f);
            Assert.AreEqual(-1f, MetaCurves.Shine(1.5f, 2.2f, 0.35f), 1e-4f, "resting until the next period");
            Assert.AreEqual(0f, MetaCurves.Shine(2.375f, 2.2f, 0.35f), 1e-3f, "sweeps again a period later");
        }

        [Test]
        public void Win_OnTheNextLevel_GainsTrophiesAgain_BestScoreIsPerLevel()
        {
            var s = ScriptableObject.CreateInstance<MetaSettings>();
            var first = MetaRules.Win(s, s.Save, 359, 137);
            Assert.AreEqual(359, first.TrophyGain);
            var second = MetaRules.Win(s, first.Save, 359, 137);
            Assert.AreEqual(359, second.TrophyGain, "Pota keeps the best score per level: a new level starts from 0");
            Assert.AreEqual(0, first.Save.BestScore, "the next level has no best yet");
        }
    }
}
