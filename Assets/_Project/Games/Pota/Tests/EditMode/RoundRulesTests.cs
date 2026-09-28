using NUnit.Framework;
using Project.Basket;

namespace Project.Tests
{
    public class RoundRulesTests
    {
        [Test]
        public void FewLeft_SmallRounds_NeedZero()
        {
            Assert.IsFalse(RoundRules.FewLeft(1, 8));
            Assert.IsTrue(RoundRules.FewLeft(0, 8));
        }

        [Test]
        public void FewLeft_LargeRounds_TwoPercent()
        {
            Assert.IsTrue(RoundRules.FewLeft(4, 200));
            Assert.IsFalse(RoundRules.FewLeft(5, 200));
            Assert.IsTrue(RoundRules.FewLeft(2, 40));
        }

        [Test]
        public void IsMissed_RequiresEveryBallBelowRim()
        {
            Assert.IsTrue(RoundRules.IsMissed(0, 0, 2.8f, 3.05f));
            Assert.IsFalse(RoundRules.IsMissed(0, 0, 2.9f, 3.05f));
            Assert.IsFalse(RoundRules.IsMissed(1, 0, 2.0f, 3.05f));
            Assert.IsFalse(RoundRules.IsMissed(0, 1, 2.0f, 3.05f));
        }

        [Test]
        public void ShouldEnd_Timeout()
        {
            Assert.IsTrue(RoundRules.ShouldEnd(5, 8, 3f, 0f, 30f));
            Assert.IsFalse(RoundRules.ShouldEnd(5, 8, 3f, 0f, 10f));
        }

        [Test]
        public void ShouldEnd_SettledPile()
        {
            Assert.IsTrue(RoundRules.ShouldEnd(0, 8, 0.1f, 0f, 5f));
            Assert.IsFalse(RoundRules.ShouldEnd(0, 8, 1.0f, 0.5f, 5f));
            Assert.IsTrue(RoundRules.ShouldEnd(0, 8, 1.0f, 1.6f, 5f));
        }
    }
}
