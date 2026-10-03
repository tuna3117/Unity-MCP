using NUnit.Framework;
using Project.Basket;
using Project.Editor.AITools;

namespace Project.Tests
{
    /// <summary>ai_basket_record's feel modes (plan 2026-10-03 Task 10): pull, miss, swish aims and the feel switch.</summary>
    public class BasketRecordToolTests
    {
        [Test]
        public void Modes_AreKnown()
        {
            foreach (var m in new[] { "shot", "pull", "miss", "swish" }) Assert.IsTrue(BasketRecordTool.TryMode(m, out _), m);
            Assert.IsFalse(BasketRecordTool.TryMode("dunk", out _));
            Assert.IsTrue(BasketRecordTool.TryMode(null, out var d) && d == BasketRecordTool.Mode.Shot, "default: a plain shot");
        }

        [Test]
        public void MissAim_IsPastTheWall_SwishAim_IsAFullPowerShotAtATopHoop()
        {
            var layout = LevelRules.Build(1);
            var miss = BasketRecordTool.AimFor(BasketRecordTool.Mode.Miss, layout, 0f, 0.6f);
            Assert.Greater(System.Math.Abs(miss.X), layout.WallX, "a miss aims beyond the walls");
            var swish = BasketRecordTool.AimFor(BasketRecordTool.Mode.Swish, layout, 0f, 0.6f);
            bool onTop = false;
            foreach (var h in layout.Hoops) if (h.IsTop && System.Math.Abs(h.X - swish.X) < 1e-3f) onTop = true;
            Assert.IsTrue(onTop, "a swish aims at a top hoop's x");
            Assert.AreEqual(AimMapper.FullPower, swish.Power, 1e-4f);
            var shot = BasketRecordTool.AimFor(BasketRecordTool.Mode.Shot, layout, 0.3f, 0.7f);
            Assert.AreEqual(0.3f, shot.X, 1e-4f);
            Assert.AreEqual(0.7f, shot.Power, 1e-4f);
        }

        [Test]
        public void PullRamp_RisesToTheAimOverPullSeconds_ThenHolds()
        {
            Assert.AreEqual(0f, BasketRecordTool.PullRamp(0f, 1.2f), 1e-4f);
            Assert.AreEqual(0.5f, BasketRecordTool.PullRamp(0.6f, 1.2f), 1e-3f);
            Assert.AreEqual(1f, BasketRecordTool.PullRamp(1.2f, 1.2f), 1e-4f);
            Assert.AreEqual(1f, BasketRecordTool.PullRamp(1.4f, 1.2f), 1e-4f);
        }
    }
}
