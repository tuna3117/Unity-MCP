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

        [Test]
        public void AxisShake_MovesOnlyAlongItsAxis_AtMostHalfTheAmount()
        {
            float most = 0f;
            for (int i = 0; i < 200; i++)
            {
                var o = ColorHuntCamera.AxisShakeOffset(Vector2.right, 0.12f, i * 0.0037f);
                Assert.AreEqual(0f, o.y, 1e-6f, "a row's rockets shake the camera sideways only");
                most = Mathf.Max(most, Mathf.Abs(o.x));
            }
            Assert.LessOrEqual(most, 0.06f + 1e-6f, "±amount/2 like CameraShake.Add");
            Assert.Greater(most, 0.05f);
            var up = ColorHuntCamera.AxisShakeOffset(new Vector2(0f, 3f), 0.12f, 0.25f / ColorHuntCamera.AxisShakeHz);
            Assert.AreEqual(0f, up.x, 1e-6f);
            Assert.AreEqual(0.06f, up.y, 1e-5f, "a quarter swing: the full half-amount; the axis is normalised");
        }
    }
}
