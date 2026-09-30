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
        public void Best_IsTheAffordableBlockWithTheLongestChain_TiesGoToTheCheapest()
        {
            var b = Deal(158);
            int best = ColorHuntHint.Best(b);
            Assert.GreaterOrEqual(best, 0);
            int bestSteps = ColorHuntRules.Break(b, best).Steps.Count;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (b.Cells[i].Hp > b.Ammo) continue;
                int steps = ColorHuntRules.Break(b, i).Steps.Count;
                Assert.LessOrEqual(steps, bestSteps, $"cell {i} chains longer");
                if (steps == bestSteps) Assert.LessOrEqual(b.Cells[best].Hp, b.Cells[i].Hp, $"cell {i} is as long and cheaper");
            }
        }

        [Test]
        public void Best_IsMinusOne_WhenNothingIsAffordable() => Assert.AreEqual(-1, ColorHuntHint.Best(Deal(0)));
    }
}
