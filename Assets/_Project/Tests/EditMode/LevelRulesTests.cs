using System.Linq;
using NUnit.Framework;
using Project.Basket;

namespace Project.Tests
{
    public class LevelRulesTests
    {
        [Test]
        public void RowCount_Table()
        {
            Assert.AreEqual(2, LevelRules.RowCount(1));
            Assert.AreEqual(2, LevelRules.RowCount(3));
            Assert.AreEqual(3, LevelRules.RowCount(4));
            Assert.AreEqual(8, LevelRules.RowCount(19));
            Assert.AreEqual(8, LevelRules.RowCount(40));
        }

        [Test]
        public void BallCount_Table()
        {
            Assert.AreEqual(8, LevelRules.BallCount(1));
            Assert.AreEqual(16, LevelRules.BallCount(10));
            Assert.AreEqual(7, LevelRules.BallCount(20));
            Assert.AreEqual(35, LevelRules.BallCount(40));
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var a = LevelRules.Build(5);
            var b = LevelRules.Build(5);
            Assert.AreEqual(a.Hoops.Count, b.Hoops.Count);
            for (int i = 0; i < a.Hoops.Count; i++)
            {
                Assert.AreEqual(a.Hoops[i].X, b.Hoops[i].X, 1e-6f);
                Assert.AreEqual(a.Hoops[i].Y, b.Hoops[i].Y, 1e-6f);
                Assert.AreEqual(a.Hoops[i].Kind, b.Hoops[i].Kind);
                Assert.AreEqual(a.Hoops[i].Amount, b.Hoops[i].Amount);
            }
        }

        [Test]
        public void Build_HoopsStayInsideWalls()
        {
            for (int level = 1; level <= 40; level++)
            {
                var layout = LevelRules.Build(level);
                foreach (var h in layout.Hoops)
                    Assert.Less(System.Math.Abs(h.X) + h.Radius, layout.WallX, $"level {level} hoop at {h.X} r {h.Radius}");
            }
        }

        [Test]
        public void Build_TopRowIsThreeTimesTwo()
        {
            var layout = LevelRules.Build(1);
            var top = layout.Hoops.Where(h => h.IsTop).OrderBy(h => h.X).ToList();
            Assert.AreEqual(3, top.Count);
            Assert.AreEqual(-1.4f, top[0].X, 1e-6f);
            Assert.AreEqual(0f, top[1].X, 1e-6f);
            Assert.AreEqual(1.4f, top[2].X, 1e-6f);
            Assert.IsTrue(top.All(h => h.Kind == HoopEffectKind.Multiply && h.Amount == 2 && h.Y == 3.05f));
            Assert.AreEqual(8, layout.BallCount);
            Assert.AreEqual(2, layout.Rows);
        }

        [Test]
        public void Build_RowsFollowPattern()
        {
            var layout = LevelRules.Build(19); // 8 rows
            var rows = layout.Hoops.Where(h => !h.IsTop).GroupBy(h => h.Row).OrderBy(g => g.Key).ToList();
            Assert.AreEqual(8, rows.Count);
            int[] expectedCounts = { 2, 1, 2, 1, 2, 1, 2, 1 };
            HoopEffectKind[] expectedKinds = { HoopEffectKind.Multiply, HoopEffectKind.Add, HoopEffectKind.Multiply, HoopEffectKind.Add, HoopEffectKind.Add, HoopEffectKind.Multiply, HoopEffectKind.Add, HoopEffectKind.Add };
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(expectedCounts[i], rows[i].Count(), $"row {i}");
                Assert.IsTrue(rows[i].All(h => h.Kind == expectedKinds[i]), $"row {i} kind");
                Assert.IsTrue(rows[i].All(h => System.Math.Abs(h.Y - (3.05f - 3f * (i + 1))) < 1e-4f), $"row {i} y");
            }
        }
    }
}
