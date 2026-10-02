using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class CameraFlightTests
    {
        // Level 20, 9:16: basket end pose over the cage, side vista, board fit at BoardAnchor (0, 8.5, 30).
        private static readonly CamPose End = new CamPose(new Vector3(0f, -21.78f, 13.5f), new Vector3(0f, -23.18f, 0f), 60f);
        private static readonly CamPose Mid = new CamPose(new Vector3(8.5f, 4f, 4.5f), new Vector3(0.7f, 1f, 7.15f), 46f);   // vista on the hero and his basket
        private static readonly CamPose Board = new CamPose(new Vector3(0f, 8.35f, 5.15f), new Vector3(0f, 8.35f, 30f), 34f);
        private static readonly CamPose Aim = new CamPose(new Vector3(0f, 2.6f, 11f), new Vector3(0f, 3f, 0f), 60f);
        // PotaFlowSettings defaults: court 15 × 28 m from z 8.4, top at y 0, 0.4 m thick; +0.5 m margin all round.
        private static readonly Bounds Court = new Bounds(new Vector3(0f, -0.2f, 22.4f), new Vector3(16f, 1.4f, 29f));

        private static void AssertSame(CamPose want, CamPose got)
        {
            Assert.Less(Vector3.Distance(want.Pos, got.Pos), 1e-4f);
            Assert.Less(Vector3.Distance(want.Look, got.Look), 1e-4f);
            Assert.AreEqual(want.Fov, got.Fov, 1e-4f);
        }

        [Test]
        public void Endpoints_AreExact()
        {
            AssertSame(End, CameraFlight.Evaluate(End, Mid, Board, 0f));
            AssertSame(Board, CameraFlight.Evaluate(End, Mid, Board, 1f));
        }

        [Test]
        public void PassesThroughTheVista()
        {
            AssertSame(Mid, CameraFlight.Evaluate(End, Mid, Board, CameraFlight.Split(End, Mid, Board)));
        }

        [Test]
        public void Heading_TurnsHalfAWay_OneDirectionOnly()
        {
            foreach (var (a, b) in new[] { (End, Board), (Board, Aim) })
            {
                float prev = a.Yaw, total = 0f;
                for (int i = 1; i <= 400; i++)
                {
                    float yaw = CameraFlight.Evaluate(a, Mid, b, i / 400f).Yaw;
                    float d = Mathf.DeltaAngle(prev, yaw);
                    total += d;
                    prev = yaw;
                }
                Assert.AreEqual(180f, Mathf.Abs(total), 2f, "a half turn");
                prev = a.Yaw;
                float sign = Mathf.Sign(total);
                for (int i = 1; i <= 400; i++)
                {
                    float yaw = CameraFlight.Evaluate(a, Mid, b, i / 400f).Yaw;
                    Assert.GreaterOrEqual(sign * Mathf.DeltaAngle(prev, yaw), -0.05f, $"heading turned back at u={i / 400f}");
                    prev = yaw;
                }
            }
        }

        [Test]
        public void Fov_StaysBetweenTheEnds()
        {
            for (int i = 0; i <= 200; i++)
            {
                float fov = CameraFlight.Evaluate(End, Mid, Board, i / 200f).Fov;
                Assert.That(fov, Is.InRange(34f - 1e-3f, 60f + 1e-3f));   // 60 → 50 → 34
            }
        }

        [Test]
        public void Path_NeverCutsThroughTheCourt_EitherWay()
        {
            for (int i = 0; i <= 400; i++)
            {
                Assert.IsFalse(Court.Contains(CameraFlight.Evaluate(End, Mid, Board, i / 400f).Pos), $"forward u={i / 400f}");
                Assert.IsFalse(Court.Contains(CameraFlight.Evaluate(Board, Mid, Aim, i / 400f).Pos), $"return u={i / 400f}");
            }
        }

        [Test]
        public void LookLead_TurnsTowardTheBoardEarly_WithoutMovingTheEnds()
        {
            float split = CameraFlight.Split(End, Mid, Board);
            AssertSame(Board, CameraFlight.Evaluate(End, Mid, Board, 1f, 1f));
            AssertSame(Mid, CameraFlight.Evaluate(End, Mid, Board, split, 1f));
            float u = split + (1f - split) * 0.4f;
            var plain = CameraFlight.Evaluate(End, Mid, Board, u);
            var lead = CameraFlight.Evaluate(End, Mid, Board, u, 1f);
            Assert.Less(Vector3.Distance(plain.Pos, lead.Pos), 1e-4f, "only the look leads");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(lead.Yaw, Board.Yaw)), Mathf.Abs(Mathf.DeltaAngle(plain.Yaw, Board.Yaw)) - 5f,
                "with a lead the camera already faces the board more");
        }

        [Test]
        public void Settle_PushesForward_AndComesBack()
        {
            AssertSame(Board, CameraFlight.Settle(Board, 0f, 0.35f));
            AssertSame(Board, CameraFlight.Settle(Board, 1f, 0.35f));
            Assert.AreEqual(Board.Pos.z + 0.35f, CameraFlight.Settle(Board, 0.5f, 0.35f).Pos.z, 1e-4f);
        }

        // ---------- 2026-10-02 candidates ----------

        private static readonly CamPose[] Dive =
        {
            Mid,
            new CamPose(new Vector3(0f, 1.8f, 20f), new Vector3(0f, 2.9f, 27.8f), 62f),
            new CamPose(new Vector3(0f, 1.0f, 27.5f), new Vector3(0f, 8.9f, 28.5f), 70f),
            new CamPose(new Vector3(0f, 4.6f, 27.5f), new Vector3(0f, 8.5f, 30f), 66f),
            Board,
        };

        private static readonly Vector3[] Rise = { Vector3.zero, Vector3.zero, Vector3.up * 3.6f, Vector3.up * 3.6f, Vector3.zero };

        [Test]
        public void Path_IsExactAtTheEnds_AndPassesEveryPoint()
        {
            AssertSame(Dive[0], CameraFlight.Path(Dive, 0f));
            AssertSame(Dive[4], CameraFlight.Path(Dive, 1f));
            for (int i = 0; i < Dive.Length; i++) AssertSame(Dive[i], CameraFlight.Path(Dive, CameraFlight.PathU(Dive, i)));
            Assert.AreEqual(0f, CameraFlight.PathU(Dive, 0));
            Assert.AreEqual(1f, CameraFlight.PathU(Dive, 4), 1e-6f);
            float u2 = CameraFlight.PathU(Dive, 2), u3 = CameraFlight.PathU(Dive, 3);
            Assert.AreEqual((u2 + u3) * 0.5f, CameraFlight.PathU(Dive, 2, 0.5f), 1e-6f, "frac walks the segment");
        }

        [Test]
        public void Path_CrossesTheRimPlane_AtTheRiseMidpoint_AndStaysInsideTheRing()
        {
            float uRim = CameraFlight.PathUAtHeight(Dive, 2, 2.9f, Rise);
            var p = CameraFlight.Path(Dive, uRim, Rise);
            Assert.AreEqual(2.9f, p.Pos.y, 1e-3f, "at the rim's height");
            Assert.Greater(uRim, CameraFlight.PathU(Dive, 2));
            Assert.Less(uRim, CameraFlight.PathU(Dive, 3));
            Assert.Less(Mathf.Abs(p.Pos.x), 0.3f);
            Assert.Less(Mathf.Abs(p.Pos.z - 27.5f), 0.3f, "on the rise axis, inside R 1.6");
            // The rise itself stays inside the ring: every sample between the under and above points within 0.5 m of the axis.
            for (float u = CameraFlight.PathU(Dive, 2); u <= CameraFlight.PathU(Dive, 3); u += 0.002f)
            {
                var q = CameraFlight.Path(Dive, u, Rise);
                Assert.Less(new Vector2(q.Pos.x, q.Pos.z - 27.5f).magnitude, 0.05f, $"u={u}");
            }
        }

        [Test]
        public void Path_IsContinuous_AndNeverGoesUnderTheCourt()
        {
            var prev = CameraFlight.Path(Dive, 0f, Rise);
            for (float u = 0.002f; u <= 1f; u += 0.002f)
            {
                var p = CameraFlight.Path(Dive, u, Rise);
                Assert.Less(Vector3.Distance(prev.Pos, p.Pos), 0.4f, $"jump at u={u}");
                Assert.Greater(p.Pos.y, 0.3f, $"under the roof at u={u}");
                prev = p;
            }
            Assert.AreEqual(70f, CameraFlight.Path(Dive, CameraFlight.PathU(Dive, 2), Rise).Fov, 1e-4f);
            // Without the vertical tangents the rise bulges toward the backboard (the 2026-10-02 bug); with them it is a line.
            AssertSame(CameraFlight.Path(Dive, 0.5f), CameraFlight.Path(Dive, 0.5f, null));
        }

        [Test]
        public void Sweep_IsExactAtTheEnds_AndRisesInTheMiddle()
        {
            AssertSame(Mid, CameraFlight.Sweep(Mid, Board, 0f, 3.5f));
            AssertSame(Board, CameraFlight.Sweep(Mid, Board, 1f, 3.5f));
            var half = CameraFlight.Sweep(Mid, Board, 0.5f, 3.5f);
            Assert.AreEqual((Mid.Pos.y + Board.Pos.y) * 0.5f + 3.5f, half.Pos.y, 1e-4f, "Pota: sin(πu)·rise");
            Assert.AreEqual((Mid.Fov + Board.Fov) * 0.5f, half.Fov, 1e-4f);
        }
    }
}
