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
        public void Score_IsStepsPlusTargetItemsCollected()
        {
            var b = Deal(158);
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i].Hp > b.Ammo) continue;
                var r = ColorHuntRules.Break(b, i);
                int items = 0;
                foreach (var step in r.Steps) foreach (int c in step.Exploded) if (IsTarget(b, b.Cells[c].Color)) items++;
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
