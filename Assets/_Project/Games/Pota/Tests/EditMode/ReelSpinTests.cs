using NUnit.Framework;
using Project.Meta;

namespace Project.Tests
{
    /// <summary>M9, pure: the bonus reels' spin (Pota's timing) plus the feel layer's stop squash and cell overshoot.</summary>
    public class ReelSpinTests
    {
        private static ReelSpin Make(bool on, int columns = 7) => new ReelSpin(new MetaFeelTuning { Enabled = on }, columns);

        [Test]
        public void Columns_StopLeftToRight_120msApart()
        {
            var r = Make(true);
            Assert.AreEqual(1.4f, r.StopAt(0), 1e-4f);
            Assert.AreEqual(1.4f + 0.12f * 6, r.StopAt(6), 1e-4f);
            for (int i = 1; i < 7; i++) Assert.Greater(r.StopAt(i), r.StopAt(i - 1));
            Assert.AreEqual(0f, r.Offset(0, 0f), 1e-4f, "starts at the top");
            Assert.AreEqual(1f, r.Offset(0, r.StopAt(0)), 1e-4f, "lands on the result rows");
            Assert.Less(r.Offset(3, r.StopAt(3) * 0.5f), 1f);
            Assert.Greater(r.Offset(3, r.StopAt(3) * 0.5f), 0.5f, "fast at first, slow at the end (Pota's bezier)");
            Assert.IsTrue(r.Stopped(2, r.StopAt(2)));
            Assert.IsFalse(r.Stopped(2, r.StopAt(2) - 0.01f));
        }

        [Test]
        public void Squash_And_Pop_FollowEachStop_ForTheSquashTime()
        {
            var r = Make(true);
            float s = r.StopAt(2);
            Assert.AreEqual(1f, r.Squash(2, s - 0.01f), 1e-4f, "no squash while spinning");
            Assert.Less(r.Squash(2, s + 0.03f), 1f, "squashed just after the stop");
            Assert.AreEqual(1f, r.Squash(2, s + ReelSpin.SquashTime + 0.01f), 1e-3f, "recovered");
            Assert.Greater(r.Pop(2, s + 0.06f), 1f, "cells overshoot after the stop");
            Assert.AreEqual(1f, r.Pop(2, s + ReelSpin.PopTime + 0.01f), 1e-3f);
            Assert.AreEqual(r.StopAt(6) + ReelSpin.PopTime, r.Done, 1e-4f);
        }

        [Test]
        public void FeelOff_SpinsLikePota_WithoutSquashOrPop()
        {
            var r = Make(false);
            Assert.AreEqual(1.4f, r.StopAt(0), 1e-4f, "the spin is Pota's own");
            Assert.AreEqual(1f, r.Squash(0, r.StopAt(0) + 0.03f), 1e-4f);
            Assert.AreEqual(1f, r.Pop(0, r.StopAt(0) + 0.06f), 1e-4f);
            Assert.AreEqual(r.StopAt(6), r.Done, 1e-4f);
        }
    }

    /// <summary>Pota bonus.ts, ported: the seeded grid and the payout thresholds.</summary>
    public class BonusRulesTests
    {
        [Test]
        public void Evaluate_PaysCoinsAtThree_ClockAtFour_RightsAtFour()
        {
            var cells = new BonusRules.Cell[21];
            for (int i = 0; i < 21; i++) cells[i] = new BonusRules.Cell { Kind = BonusRules.Kind.Item, Colour = i % 5 };
            Assert.AreEqual(0, BonusRules.Evaluate(cells).Coins);
            cells[0].Kind = cells[1].Kind = cells[2].Kind = BonusRules.Kind.Coin;
            Assert.AreEqual(30, BonusRules.Evaluate(cells).Coins, "3 coins → 3 × 10");
            cells[3].Kind = BonusRules.Kind.Coin;
            Assert.AreEqual(40, BonusRules.Evaluate(cells).Coins);
            cells[4].Kind = cells[5].Kind = cells[6].Kind = BonusRules.Kind.Clock;
            Assert.AreEqual(0, BonusRules.Evaluate(cells).InfiniteLifeMinutes, "3 clocks: under the threshold");
            cells[7].Kind = BonusRules.Kind.Clock;
            Assert.AreEqual(30, BonusRules.Evaluate(cells).InfiniteLifeMinutes);
            for (int i = 8; i < 12; i++) cells[i].Kind = BonusRules.Kind.Right;
            Assert.AreEqual(3, BonusRules.Evaluate(cells).ExtraRights);
        }

        [Test]
        public void Grid_IsDeterministicForASeed_AndMostlyItems()
        {
            var a = BonusRules.Grid(7, 3, 123, out int next);
            var b = BonusRules.Grid(7, 3, 123, out _);
            for (int i = 0; i < a.Length; i++) { Assert.AreEqual(a[i].Kind, b[i].Kind); Assert.AreEqual(a[i].Colour, b[i].Colour); }
            Assert.AreNotEqual(123, next);
            int items = 0;
            for (int s = 0; s < 50; s++) foreach (var c in BonusRules.Grid(7, 3, s, out _)) if (c.Kind == BonusRules.Kind.Item) items++;
            Assert.Greater(items / (50f * 21f), 0.6f, "items dominate (weights .72)");
        }

        [Test]
        public void Spin_SpendsARight_AddsTheWinnings_StopsAtZero()
        {
            var st = BonusRules.Start(7, BonusRules.ShowcaseSeed);
            Assert.AreEqual(3, st.Rights);
            st = BonusRules.Spin(st, out var result);
            Assert.AreEqual(2, st.Rights);
            Assert.Greater(result.Coins, 0, "the showcase seed's first spin pays coins (so the recording shows the flight)");
            Assert.AreEqual(result.Coins, st.Winnings.Coins);
            st = BonusRules.Spin(st, out _); st = BonusRules.Spin(st, out _);
            Assert.IsTrue(BonusRules.Over(st));
            var same = BonusRules.Spin(st, out var none);
            Assert.AreEqual(0, same.Rights);
            Assert.AreEqual(0, none.Coins);
        }
    }
}
