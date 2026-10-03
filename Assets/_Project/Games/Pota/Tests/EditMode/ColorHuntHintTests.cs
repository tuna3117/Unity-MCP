using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntHintTests
    {
        private static Board Deal(int ammo) =>
            ColorHuntRules.Create(ScriptableObject.CreateInstance<ColorHuntSettings>().ToDesign(), ammo, 7);

        [Test]
        public void Score_IsStepsPlusTargetItemsTheBreakActuallyCollects()
        {
            var b = Deal(158);
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i].Hp > b.Ammo) continue;
                var r = ColorHuntRules.Break(b, i);
                // the items collected are what the targets lost, read from the break's own result board (cells after step 1 hold
                // dropped and spawned blocks, so reading colours through the pre-tap board at exploded indices counts the wrong blocks)
                int items = 0;
                for (int t = 0; t < b.Targets.Length; t++) items += Mathf.Max(0, b.Targets[t].Remaining) - Mathf.Max(0, r.Board.Targets[t].Remaining);
                Assert.AreEqual(r.Steps.Count + items, ColorHuntHint.Score(b, i), $"cell {i}");
            }
        }

        [Test]
        public void Best_IsTheHighestScore_TiesGoToTheCheapest()
        {
            var b = Deal(158);
            int best = ColorHuntHint.Best(b);
            int bestScore = ColorHuntHint.Score(b, best);
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i].Hp > b.Ammo) continue;
                int score = ColorHuntHint.Score(b, i);
                Assert.LessOrEqual(score, bestScore, $"cell {i} scores higher");
                if (score == bestScore) Assert.LessOrEqual(b.Cells[best].Hp, b.Cells[i].Hp, $"cell {i} scores the same and is cheaper");
            }
        }

        private static bool IsTarget(Board b, int color)
        {
            foreach (var t in b.Targets) if (t.Color == color && t.Remaining > 0) return true;
            return false;
        }

        [Test]
        public void Best_IsMinusOne_WhenNothingIsAffordable() => Assert.AreEqual(-1, ColorHuntHint.Best(Deal(0)));
    }
}
