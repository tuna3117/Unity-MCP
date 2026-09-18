using NUnit.Framework;
using Project.Basket;
using UnityEngine;

namespace Project.Tests
{
    public class TrajectorySolverTests
    {
        private static readonly Vector3 Start = new Vector3(0f, 1.9f, 6.2f);
        private static readonly Vector3 Target = new Vector3(1.4f, 3.35f, 0f);

        [Test]
        public void LaunchVelocity_ReachesTargetAtFlightTime()
        {
            var v = TrajectorySolver.LaunchVelocity(Start, Target, 1f, 9.8f);
            var p = TrajectorySolver.PositionAt(Start, v, 9.8f, 1f);
            Assert.AreEqual(Target.x, p.x, 1e-3f);
            Assert.AreEqual(Target.y, p.y, 1e-3f);
            Assert.AreEqual(Target.z, p.z, 1e-3f);
        }

        [Test]
        public void Apex_RisesWithLongerFlightTime()
        {
            var fast = TrajectorySolver.LaunchVelocity(Start, Target, 0.85f, 9.8f);
            var slow = TrajectorySolver.LaunchVelocity(Start, Target, 1.0f, 9.8f);
            Assert.Greater(TrajectorySolver.ApexHeight(Start, slow, 9.8f), TrajectorySolver.ApexHeight(Start, fast, 9.8f));
        }

        [Test]
        public void PositionAt_Zero_IsStart()
        {
            var v = TrajectorySolver.LaunchVelocity(Start, Target, 1f, 9.8f);
            Assert.AreEqual(Start, TrajectorySolver.PositionAt(Start, v, 9.8f, 0f));
        }
    }
}
