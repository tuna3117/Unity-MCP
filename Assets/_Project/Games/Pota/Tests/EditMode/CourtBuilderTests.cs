using NUnit.Framework;
using Project.Flow;
using UnityEngine;

namespace Project.Tests
{
    public class CourtBuilderTests
    {
        private static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        [Test]
        public void CourtTexture_WhiteLinesOnOrange()
        {
            var orange = new Color(0.851f, 0.467f, 0.227f);
            var tex = CourtBuilder.CourtTexture(256, 512, orange, Color.white);
            try
            {
                Assert.Less(Diff(tex.GetPixel(64, 128), orange), 0.03f, "floor");
                Assert.Less(Diff(tex.GetPixel(128, 256), Color.white), 0.03f, "half-court line");
                Assert.Less(Diff(tex.GetPixel(0, 300), Color.white), 0.03f, "side line");
                Assert.Less(Diff(tex.GetPixel(128, 511), Color.white), 0.03f, "end line under the hoop");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Court_IsTheRoofBehindTheTerrace_WithoutCollider()
        {
            var s = ScriptableObject.CreateInstance<PotaFlowSettings>();
            var root = new GameObject("Flow");
            try
            {
                var court = CourtBuilder.Build(root.transform, s);
                var b = court.GetComponent<Renderer>().bounds;
                Assert.AreEqual(0f, b.max.y, 1e-3f);
                Assert.AreEqual(8.4f, b.min.z, 1e-3f);
                Assert.AreEqual(36.4f, b.max.z, 1e-3f);
                Assert.AreEqual(7.5f, b.max.x, 1e-3f);
                Assert.IsNull(court.GetComponent<Collider>(), "taps and basket physics must not hit the court");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(s);
            }
        }
    }
}
