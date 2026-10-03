using NUnit.Framework;
using Project.Meta;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>M4: the win window's pure timeline (score count-up, medal escalation, trophy roll, league bar, coins, continue pulse).</summary>
    public class WinSequenceTests
    {
        private static MetaFeelTuning Feel(bool on) => new MetaFeelTuning { Enabled = on };

        private static WinSequence Make(bool on, int score = 116, int silver = 100, int gold = 110, int trophiesBefore = 11800, int gain = 116)
        {
            var s = ScriptableObject.CreateInstance<MetaSettings>();
            s.Feel = Feel(on);
            return new WinSequence(s, score, silver, gold, trophiesBefore, trophiesBefore + gain, coins: 38);
        }

        [Test]
        public void Score_CountsUpAfterScoreAt_ReachesTheTotal()
        {
            var w = Make(true);
            var f = w.Feel;
            Assert.AreEqual(0, w.Stage(f.ScoreAt - 0.01f).Score);
            Assert.Less(w.Stage(f.ScoreAt + f.ScoreTime * 0.5f).Score, 116);
            Assert.Greater(w.Stage(f.ScoreAt + f.ScoreTime * 0.5f).Score, 0);
            Assert.AreEqual(116, w.Stage(f.ScoreAt + f.ScoreTime).Score);
            Assert.AreEqual(116, w.Stage(10f).Score);
        }

        [Test]
        public void Medal_EscalatesAsTheCountCrossesTheThresholds_NeverFlipsBack()
        {
            var w = Make(true, score: 116, silver: 100, gold: 110);
            var f = w.Feel;
            Assert.AreEqual(-1, w.Stage(0.1f).Medal, "no medal before it appears");
            Assert.AreEqual(0, w.Stage(f.ScoreAt + 0.05f).Medal, "bronze first");
            int last = -1;
            for (float t = 0f; t <= 3f; t += 0.02f)
            {
                int m = w.Stage(t).Medal;
                Assert.GreaterOrEqual(m, last, $"medal fell back at {t:0.00}");
                last = m;
            }
            Assert.AreEqual(2, w.Stage(3f).Medal, "gold at the end (116 ≥ 110)");
            Assert.Greater(w.MedalFlipAt(1), f.ScoreAt, "silver flips during the count-up");
            Assert.Greater(w.MedalFlipAt(2), w.MedalFlipAt(1), "gold after silver");
        }

        [Test]
        public void Medal_StaysBronze_WhenTheScoreIsUnderSilver()
        {
            var w = Make(true, score: 90, silver: 100, gold: 110);
            Assert.AreEqual(0, w.Stage(3f).Medal);
            Assert.Less(w.MedalFlipAt(1), 0f, "no silver flip");
        }

        [Test]
        public void LeagueBar_FillsFromOldToNew_OverflowsOnPromotion()
        {
            var s = ScriptableObject.CreateInstance<MetaSettings>();
            s.Feel = Feel(true);
            var w = new WinSequence(s, 200, 100, 110, 12300, 12500, coins: 15);
            var f = w.Feel;
            float before = MetaRules.BarFill(s, 12300);
            Assert.AreEqual(before, w.Stage(f.TrophyAt).Bar, 1e-3f, "starts at the old fill");
            Assert.IsTrue(w.Promoted);
            Assert.AreEqual(1f, w.Stage(f.TrophyAt + f.BarTime).Bar, 1e-3f, "fills to the brim when a league is crossed");
            Assert.LessOrEqual(w.Stage(5f).Bar, 1f);
        }

        [Test]
        public void Coins_BurstAtCoinsAt_ContinuePulsesAtContinueAt()
        {
            var w = Make(true);
            var f = w.Feel;
            Assert.IsFalse(w.Stage(f.CoinsAt - 0.01f).CoinsFlying);
            Assert.IsTrue(w.Stage(f.CoinsAt + 0.01f).CoinsFlying);
            Assert.IsFalse(w.Stage(f.ContinueAt - 0.01f).ContinuePulse);
            Assert.IsTrue(w.Stage(f.ContinueAt + 0.01f).ContinuePulse);
            Assert.Greater(w.Stage(f.StampAt + 0.1f).Stamp, 0f);
            Assert.AreEqual(0f, w.Stage(0f).Stamp, 1e-4f);
        }

        [Test]
        public void FeelOff_IsPotasWindow_ScoreCountsUp0_8s_RestAtOnce_NoPulse()
        {
            var w = Make(false);
            var a = w.Stage(0f);
            Assert.AreEqual(0, a.Score, "Pota's counter starts at 0");
            Assert.AreEqual(2, a.Medal, "Pota writes the final medal at once");
            Assert.AreEqual(1f, a.Stamp, 1e-4f);
            Assert.IsTrue(a.CoinsFlying);
            Assert.IsFalse(a.ContinuePulse, "Pota's Devam does not pulse");
            Assert.AreEqual(w.BarAfter, a.Bar, 1e-4f);
            Assert.AreEqual(1f, a.LinesAlpha, 1e-4f);
            int mid = w.Stage(WinSequence.PotaCount * 0.5f).Score;
            Assert.Greater(mid, 0); Assert.Less(mid, 116);
            Assert.AreEqual(116, w.Stage(WinSequence.PotaCount).Score, "0.8 s later the total (Pota sayac)");
            Assert.Greater(w.Stage(WinSequence.PotaCount + 0.05f).ScoreScale, 1f, "Pota pops the counter at the end");
            Assert.Greater(w.Stage(WinSequence.PotaCount + 0.05f).MedalPop, 1f, "and the medal");
            Assert.AreEqual(1f, w.Stage(5f).ScoreScale, 1e-4f);
        }

    }
}
