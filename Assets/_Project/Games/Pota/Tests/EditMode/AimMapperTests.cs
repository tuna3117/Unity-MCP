using NUnit.Framework;
using Project.Basket;

namespace Project.Tests
{
    public class AimMapperTests
    {
        private const float W = 390f, H = 844f;

        [Test]
        public void DeadZone_RejectsShortSwipe()
        {
            Assert.IsFalse(AimMapper.TryMap(0f, -23f, W, H, out _));
            Assert.IsTrue(AimMapper.TryMap(0f, -24f, W, H, out _));
        }

        [Test]
        public void Power_IsFractionOfScreen_AndClamped()
        {
            Assert.AreEqual(1f, AimMapper.Power(-H * 0.42f, H), 1e-4f);
            Assert.AreEqual(1.4f, AimMapper.Power(-2000f, H), 1e-4f);
        }

        [Test]
        public void AimY_HasFloorAboveRim()
        {
            Assert.AreEqual(3.15f, AimMapper.AimY(0.1f), 1e-4f);
            Assert.AreEqual(3.35f, AimMapper.AimY(0.6f), 1e-4f);
            Assert.AreEqual(3.35f + 0.6f, AimMapper.AimY(1.0f), 1e-4f);
        }

        [Test]
        public void LateralMapping_FullWidthIsFourMetres()
        {
            Assert.IsTrue(AimMapper.TryMap(W, -300f, W, H, out var aim));
            Assert.AreEqual(4f, aim.X, 1e-4f);
        }

        [Test]
        public void OverPower_FlagAboveOneMetre()
        {
            AimMapper.TryMap(0f, -H * 0.42f * 1.1f, W, H, out var strong);
            Assert.IsTrue(strong.OverPower);
            AimMapper.TryMap(0f, -H * 0.42f * 0.9f, W, H, out var normal);
            Assert.IsFalse(normal.OverPower);
        }

        [Test]
        public void FlightTime_ShorterForShortFlicks()
        {
            Assert.Less(AimMapper.FlightTime(0.1f), AimMapper.FlightTime(0.4f));
            Assert.AreEqual(1f, AimMapper.FlightTime(0.6f), 1e-4f);
        }
    }
}
