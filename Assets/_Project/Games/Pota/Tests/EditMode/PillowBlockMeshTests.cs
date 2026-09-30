using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class PillowBlockMeshTests
    {
        [Test]
        public void FrontBulges_ByPotasFormula_BackStaysFlat()
        {
            var box = RoundedBoxMesh.Create(Vector3.one * 0.9f, 0.22f, 16);
            var pillow = PillowBlockMesh.Create();
            var b = box.vertices; var p = pillow.vertices;
            Assert.AreEqual(b.Length, p.Length);
            for (int i = 0; i < b.Length; i++)
            {
                float front = -b[i].z;                                   // Unity's front is −z
                float expected = b[i].z;
                if (front > 0f)
                {
                    float u = b[i].x / 0.45f, w = b[i].y / 0.45f, d = front / 0.45f;
                    expected = -(front + 0.1f * Mathf.Max(0f, 1f - u * u) * Mathf.Max(0f, 1f - w * w) * d);   // yastik.ts
                }
                Assert.AreEqual(expected, p[i].z, 1e-5f, $"vertex {i}");
                Assert.AreEqual(b[i].x, p[i].x, 1e-6f);
                Assert.AreEqual(b[i].y, p[i].y, 1e-6f);
            }
            Object.DestroyImmediate(box);
            Object.DestroyImmediate(pillow);
        }

        [Test]
        public void FrontCentre_Is0Point55_NormalsAreUnit()
        {
            var m = PillowBlockMesh.Create();
            var v = m.vertices; var n = m.normals;
            float minZ = 0f, maxZ = 0f;
            for (int i = 0; i < v.Length; i++)
            {
                minZ = Mathf.Min(minZ, v[i].z); maxZ = Mathf.Max(maxZ, v[i].z);
                Assert.AreEqual(1f, n[i].magnitude, 1e-4f, $"normal {i}");
                if (Mathf.Abs(v[i].x) < 1e-4f && Mathf.Abs(v[i].y) < 1e-4f && v[i].z < 0f)
                    Assert.Less(Vector3.Distance(n[i], Vector3.back), 1e-4f, "front centre faces the camera");
            }
            Assert.AreEqual(-0.55f, minZ, 1e-4f, "0.45 + bulge 0.1");
            Assert.AreEqual(0.45f, maxZ, 1e-4f, "back is not bulged");
            Object.DestroyImmediate(m);
        }
    }
}
