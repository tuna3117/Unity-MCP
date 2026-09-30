using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ShatterTests
    {
        [Test]
        public void Debris_Falls_BouncesOffTheFrameBottom_AndStaysInside()
        {
            var d = new Debris { P = Vector3.zero, V = new Vector3(0f, -12f, 0f), R = Quaternion.identity, Life = 5f, Size = 1f };
            bool bounced = false;
            for (int i = 0; i < 300; i++)
            {
                ShatterDebris.Step(ref d, 1f / 60f, 0.2f);
                Assert.GreaterOrEqual(d.P.y, -(ShatterDebris.InnerHalf - 0.2f) - 1e-4f);
                bounced |= d.V.y > 0f;
            }
            Assert.IsTrue(bounced);
        }

        [Test]
        public void Debris_BouncesOffTheSides_AndTheGlass()
        {
            var d = new Debris { P = new Vector3(4f, 0f, 0.3f), V = new Vector3(20f, 0f, 20f), R = Quaternion.identity, Life = 5f, Size = 1f };
            ShatterDebris.Step(ref d, 0.1f, 0.2f);
            Assert.AreEqual(ShatterDebris.InnerHalf - 0.2f, d.P.x, 1e-4f);
            Assert.Less(d.V.x, 0f);
            Assert.AreEqual(ShatterDebris.GlassZ - 0.2f, d.P.z, 1e-4f);
            Assert.Less(d.V.z, 0f);
        }

        [Test]
        public void Launch_ThrowsOutAlongThePiece_AndTowardTheCamera()
        {
            var rng = new System.Random(3);
            var d = ShatterDebris.Launch(new Vector3(1f, 2f, 0f), new Vector3(0.3f, 0f, 0f), 1.2f, rng);
            Assert.AreEqual(new Vector3(1.36f, 2f, 0f), d.P);
            Assert.Greater(d.V.x, 0f);
            Assert.Less(d.V.z, 0f, "toward the camera (−z)");
            Assert.That(d.Life, Is.InRange(0.8f, 1.2f));
            Assert.AreEqual(1.2f, d.Size);
        }

        [Test]
        public void Shrink_HoldsThenVanishes()
        {
            Assert.AreEqual(1f, ShatterDebris.Shrink(0.5f, 1f));
            Assert.AreEqual(0.5f, ShatterDebris.Shrink(0.85f, 1f), 1e-4f);
            Assert.AreEqual(0f, ShatterDebris.Shrink(1f, 1f), 1e-4f);
        }

        private static GameObject Model(int pieces, bool frontUv)
        {
            var root = new GameObject("Model");
            for (int i = 0; i < pieces; i++)
            {
                var go = new GameObject("piece_" + i);
                go.transform.SetParent(root.transform);
                var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
                if (frontUv) mesh.uv2 = new[] { Vector2.right, Vector2.zero, Vector2.zero };
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
            }
            return root;
        }

        [Test]
        public void PiecesWithoutFrontUv_AreNotReady()
        {
            var bad = Model(10, false);
            var good = Model(10, true);
            try
            {
                Assert.IsFalse(ShatterBurst.PiecesReady(null));
                Assert.IsFalse(ShatterBurst.PiecesReady(bad));
                Assert.IsFalse(ShatterBurst.PiecesReady(Model(2, true)), "a real fracture has 4+ pieces");
                Assert.IsTrue(ShatterBurst.PiecesReady(good));
            }
            finally { Object.DestroyImmediate(bad); Object.DestroyImmediate(good); }
        }

        [Test]
        public void StickerPlacement_MatchesBlockViewsLabels()
        {
            Assert.AreEqual(0.5f, ShatterBurst.ItemPlace.x, 1e-5f);
            Assert.AreEqual(0.5f + 0.06f / 0.9f, ShatterBurst.ItemPlace.y, 1e-5f);
            Assert.AreEqual(0.92f * PotaLook.ItemScale / 0.9f, ShatterBurst.ItemPlace.z, 1e-5f);
            Assert.AreEqual(0.5f + 0.26f / 0.9f, ShatterBurst.NumberPlace.x, 1e-5f);
            Assert.AreEqual(0.5f - 0.28f / 0.9f, ShatterBurst.NumberPlace.y, 1e-5f);
            Assert.AreEqual(0.92f * 0.44f / 0.9f, ShatterBurst.NumberPlace.w, 1e-5f);
        }


        [Test]
        public void MountedPieces_ShowTheirStickerToTheCamera_Unmirrored()
        {
            // the workshop's front (−Y) imports as Unity +z; the board's blocks face the camera at −z
            var s = UnityEditor.AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");
            Assume.That(s != null && ShatterBurst.PiecesReady(s.ShatterPieces), "ai_studio_import ran");
            var normal = Vector3.zero;
            float xu = 0f, xx = 0f, mx = 0f, mu = 0f;
            int n = 0;
            foreach (var f in s.ShatterPieces.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = f.sharedMesh;
                var uv = m.uv; var uv2 = m.uv2; var nr = m.normals; var v = m.vertices;
                for (int i = 0; i < m.vertexCount; i++)
                {
                    if (uv2[i].x < 0.5f) continue;
                    normal += ShatterBurst.Mount * f.transform.TransformDirection(nr[i]);
                    float x = (ShatterBurst.Mount * f.transform.TransformPoint(v[i])).x;
                    mx += x; mu += uv[i].x; xu += x * uv[i].x; xx += x * x; n++;
                }
            }
            Assert.Greater(n, 0);
            Assert.Less(normal.normalized.z, -0.5f, "the sticker faces the camera");
            float cov = xu / n - (mx / n) * (mu / n);
            Assert.Greater(cov, 0f, "u grows to the right: the item is not mirrored");
        }
    }
}
