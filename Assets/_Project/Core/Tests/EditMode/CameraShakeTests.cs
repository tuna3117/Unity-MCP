using NUnit.Framework;
using Project.Juice;
using UnityEngine;

namespace Project.Tests
{
    public class CameraShakeTests
    {
        [Test]
        public void Add_Accumulates_UpToHalfAMetre()
        {
            var go = new GameObject("Cam");
            try
            {
                var s = go.AddComponent<CameraShake>();
                s.Add(0.08f);
                s.Add(0.07f);
                Assert.AreEqual(0.15f, s.Linear, 1e-6f, "yapbozSahne.ts sars: additive");
                for (int i = 0; i < 10; i++) s.Add(0.14f);
                Assert.AreEqual(0.5f, s.Linear, 1e-6f, "cap 0.5");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Linear_DecaysAt2Point2PerSecond_ToZero()
        {
            Assert.AreEqual(0.5f - 2.2f * 0.1f, CameraShake.StepLinear(0.5f, 0.1f), 1e-6f);
            Assert.AreEqual(0f, CameraShake.StepLinear(0.05f, 0.1f), 1e-6f);
        }
    }
}
