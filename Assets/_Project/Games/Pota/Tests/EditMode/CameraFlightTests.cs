using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class CameraFlightTests
    {
        // Level 20, 9:16: basket end pose over the cage, side vista, board fit at BoardAnchor (0, 8.5, 30).
        private static readonly CamPose End = new CamPose(new Vector3(0f, -21.78f, 13.5f), new Vector3(0f, -23.18f, 0f), 60f);
        private static readonly CamPose Mid = new CamPose(new Vector3(13f, 7f, 17f), new Vector3(0f, 8f, 15f), 52f);
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
                Assert.That(fov, Is.InRange(34f - 1e-3f, 60f + 1e-3f));
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
        public void Settle_PushesForward_AndComesBack()
        {
            AssertSame(Board, CameraFlight.Settle(Board, 0f, 0.35f));
            AssertSame(Board, CameraFlight.Settle(Board, 1f, 0.35f));
            Assert.AreEqual(Board.Pos.z + 0.35f, CameraFlight.Settle(Board, 0.5f, 0.35f).Pos.z, 1e-4f);
        }
    }
}
