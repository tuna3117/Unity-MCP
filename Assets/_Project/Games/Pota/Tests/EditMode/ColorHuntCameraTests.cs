using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntCameraTests
    {
        [Test]
        public void Zoomed_OneIsTheFit_TwoHalvesTheDistanceTowardTheFocus()
        {
            var fit = new Vector3(0f, 1f, -20f);
            var focus = new Vector3(2f, 3f, -0.5f);
            Assert.AreEqual(fit, ColorHuntCamera.Zoomed(fit, focus, 1f));
            var z2 = ColorHuntCamera.Zoomed(fit, focus, 2f);
            Assert.AreEqual(1f, z2.x, 1e-5f);
            Assert.AreEqual(2f, z2.y, 1e-5f);
            Assert.AreEqual(-0.5f - 19.5f / 2f, z2.z, 1e-5f);
        }

        [Test]
        public void PushWeight_IsZeroAtBothEnds_OneInTheMiddle()
        {
            Assert.AreEqual(0f, ColorHuntCamera.PushWeight(0f));
            Assert.AreEqual(1f, ColorHuntCamera.PushWeight(0.5f), 1e-5f);
            Assert.AreEqual(0f, ColorHuntCamera.PushWeight(1f));
        }
    }
}
