using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>GoldenPath = renkAviFaz.ts altinlariIlerlet in game-root units (RENK_AVI_HIS.altinInis).</summary>
    public class GoldenPathTests
    {
        private const float Eps = 1e-4f;

        /// <summary>A golden ball at cell (5, 2) of a 7×7 (s = 1.2) through gate 2.</summary>
        private static GoldenRoute Route(float scale = 1.2f) => new GoldenRoute
        {
            From = new Vector3(-1.2f, -2.4f, 0f), Gate = GateRings.RingAt(7, 2), Hoop = GoldenPath.HoopLocal, Delay = 0.12f, Col = 2, Scale = scale,
        };

        private static void Near(Vector3 expected, Vector3 actual, string what) =>
            Assert.Less(Vector3.Distance(expected, actual), Eps, $"{what}: expected {expected}, got {actual}");

        [Test]
        public void Numbers_ArePotas()
        {
            Assert.AreEqual(0.32f, GoldenPath.GateTime, "halkaSure");
            Assert.AreEqual(0.5f, GoldenPath.HoopTime, "potaSure");
            Assert.AreEqual(1.4f, GoldenPath.Arc, "kavis");
            Assert.AreEqual(0.12f, GoldenPath.Gap, "aralik");
            Assert.AreEqual(0.3f, GoldenPath.Lift);
            Assert.AreEqual(0.82f, GoldenPath.Total, Eps);
            Near(new Vector3(0f, -5.6f, -2f), GoldenPath.HoopLocal, "DEV_POTA.cemberY, (cemberR + 0.6 − 0.2) toward the camera");
        }

        [Test]
        public void GateLeg_DropsStraightDown_EasingIn()
        {
            var r = Route();
            Near(r.From, GoldenPath.At(r, 0f), "start (Delay is the caller's)");
            Near(new Vector3(r.From.x, r.From.y + (r.Gate.y - r.From.y) * 0.25f, r.From.z), GoldenPath.At(r, GoldenPath.GateTime * 0.5f), "o² at half way");
            Near(new Vector3(r.From.x, r.Gate.y, r.From.z), GoldenPath.At(r, GoldenPath.GateTime), "at the ring's height, still on the board plane");
        }

        [Test]
        public void HoopLeg_LiftsAndArcsTowardTheCamera()
        {
            var r = Route();
            var mid = GoldenPath.At(r, GoldenPath.GateTime + GoldenPath.HoopTime * 0.5f);
            Near(new Vector3(r.From.x * 0.5f, (r.Gate.y + r.Hoop.y) * 0.5f + 0.3f * 1.2f, r.Hoop.z * 0.5f - 1.4f * 0.3f * 1.2f), mid, "sin(π/2) lift and arc");
            var unscaled = Route(0f);
            var mid1 = GoldenPath.At(unscaled, GoldenPath.GateTime + GoldenPath.HoopTime * 0.5f);
            Assert.AreEqual((r.Gate.y + r.Hoop.y) * 0.5f + 0.3f, mid1.y, Eps, "Scale 0 counts as 1");
        }

        [Test]
        public void ClampsAtTheHoop()
        {
            var r = Route();
            Near(r.Hoop, GoldenPath.At(r, GoldenPath.GateTime + GoldenPath.HoopTime), "arrival");
            Near(r.Hoop, GoldenPath.At(r, 9f), "after");
        }

        [Test]
        public void PastGate_AndLanded_AtPotasMoments()
        {
            Assert.IsFalse(GoldenPath.PastGate(GoldenPath.GateTime), "ta ≤ halkaSure: still on the way down");
            Assert.IsTrue(GoldenPath.PastGate(GoldenPath.GateTime + 1e-3f));
            Assert.IsFalse(GoldenPath.Landed(GoldenPath.Total - 1e-3f));
            Assert.IsTrue(GoldenPath.Landed(GoldenPath.Total));
        }
    }
}
