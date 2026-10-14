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

        /// <summary>
        /// Pota Task 12: a kick that takes over a running push (the combo's merge push → its fire kick) starts at the push's weight and spot
        /// — no frame jumps more than 1 % of the way — peaks at its own amount; from nothing it is exactly the old sin push.
        /// </summary>
        [Test]
        public void KickWeight_FromNothingIsThePush_FromARunningPushHandsOverWithoutAJump()
        {
            for (int i = 0; i <= 100; i++)
            {
                float u = i / 100f;
                Assert.AreEqual(0.1f * ColorHuntCamera.PushWeight(u), ColorHuntCamera.KickWeight(u, 0f, 0.1f), 1e-8f, $"u {u} (float rounding only)");
            }
            Assert.AreEqual(0.06f, ColorHuntCamera.KickWeight(0f, 0.06f, 0.1f), 1e-7f, "starts where the merge push was");
            Assert.AreEqual(0.1f, ColorHuntCamera.KickWeight(0.5f, 0.06f, 0.1f), 1e-6f, "peaks at its own amount");
            Assert.AreEqual(0f, ColorHuntCamera.KickWeight(1f, 0.06f, 0.1f));
            float prev = 0.06f;
            for (int i = 1; i <= 9; i++)
            {
                float w = ColorHuntCamera.KickWeight(i / 60f / 0.3f, 0.06f, 0.1f);
                Assert.Less(Mathf.Abs(w - prev), 0.01f, $"60 Hz frame {i} of the 0.3 s kick");
                prev = w;
            }
            var a = new Vector3(1f, 2f, 3f);
            var b = new Vector3(4f, 5f, 6f);
            Assert.AreEqual(a, ColorHuntCamera.KickSpot(0f, a, b));
            Assert.AreEqual(b, ColorHuntCamera.KickSpot(0.5f, a, b));
            Assert.AreEqual(b, ColorHuntCamera.KickSpot(0.25f, b, b), "from nothing: the target all the way");
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
