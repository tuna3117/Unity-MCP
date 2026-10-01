using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>yapbozSahne.ts gecisGoster: the golden ball's gate rings on the frame.</summary>
    public class GateRingsTests
    {
        private GameObject _go;
        private ColorHuntSettings _s;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_s != null) Object.DestroyImmediate(_s);
        }

        /// <summary>A game root (Start not run: Board is null, so the rings use S.Size = 7) with its GateRings child.</summary>
        private GateRings Make()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = _s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            var g = new GameObject("GateRings").AddComponent<GateRings>();
            g.transform.SetParent(_go.transform, false);
            g.Build(game);
            return g;
        }

        private static List<Transform> Active(GateRings g)
        {
            var list = new List<Transform>();
            foreach (Transform t in g.transform) if (t.gameObject.activeSelf) list.Add(t);
            return list;
        }

        [Test]
        public void RingAt_IsGecisGoster_InTheFrameGroup()
        {
            // 7×7: s = 1.2; x = (c − 3)·1.2; y = (panoAltKenar + 0.05)·s = −(4.4 + 0.35) + 0.06; z = −(Z_ON + 0.1)·s (Unity's front is −z).
            Assert.Less(Vector3.Distance(new Vector3(-3.6f, -4.69f, -0.66f), GateRings.RingAt(7, 0)), 1e-5f);
            Assert.Less(Vector3.Distance(new Vector3(2.4f, -4.69f, -0.66f), GateRings.RingAt(7, 5)), 1e-5f);
            // 5×5: s = 1.68 — the ring stays on the frame's edge, only its size follows the grid.
            Assert.Less(Vector3.Distance(new Vector3(0f, -4.75f + 0.084f, -0.924f), GateRings.RingAt(5, 2)), 1e-5f);
            Assert.AreEqual(-4.75f / 1.2f + 0.05f, GateRings.BottomEdge(7), 1e-5f, "panoAltKenar + 0.05 in board units (halkaY)");
        }

        [Test]
        public void Show_PlacesAPoolOfFour_AndHidesTheRest()
        {
            var g = Make();
            Assert.AreEqual(GateRings.Pool, g.transform.childCount);
            Assert.AreEqual(0, Active(g).Count, "built hidden");
            g.Show(new[] { 1, 5 });
            var rings = Active(g);
            Assert.AreEqual(2, rings.Count);
            Assert.Less(Vector3.Distance(GateRings.RingAt(7, 1), rings[0].localPosition), 1e-6f);
            Assert.Less(Vector3.Distance(GateRings.RingAt(7, 5), rings[1].localPosition), 1e-6f);
            Assert.AreEqual(GateRings.RingAt(7, 5), g.RingLocal(5), "no board yet: the settings' size (7)");
            CollectionAssert.AreEqual(new[] { 1, 5 }, g.Shown);
            Assert.AreEqual(1.2f, rings[0].localScale.x, 1e-5f, "grid scale × (1 + 0.08·sin 0)");
            g.Show(new[] { 0, 1, 2, 3, 4, 5 });
            Assert.AreEqual(4, Active(g).Count, "Pota's pool of 4");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, g.Shown);
            g.Show(new int[0]);
            Assert.AreEqual(0, Active(g).Count);
            Assert.AreEqual(0, g.Shown.Count);
        }

        [Test]
        public void Rings_AreTorus042_FacingTheCamera_InPotasGateColour()
        {
            var g = Make();
            g.Show(new[] { 3 });
            var ring = Active(g)[0];
            Assert.Greater(Mathf.Abs(Vector3.Dot(ring.up, Vector3.forward)), 0.999f, "the torus axis looks down the camera's z");
            var mesh = ring.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2f * (0.42f + 0.07f), mesh.bounds.size.x, 1e-3f, "TorusGeometry(0.42, 0.07)");
            // RimMeshBuilder's cross-section (sides = Radial = 10, Pota's own radialSegments) samples v at multiples of 36°, never
            // exactly 90°: its faceted mesh reaches 2·tube·sin(72°), not the idealized 2·tube of a smooth torus.
            Assert.AreEqual(2f * 0.07f * Mathf.Sin(72f * Mathf.Deg2Rad), mesh.bounds.size.y, 1e-4f);
            var mat = ring.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual("PotaGate", mat.name);
            Assert.AreEqual(3000, mat.renderQueue, "transparent");
            var c = mat.GetColor("_BaseColor");
            var want = PotaLook.Hex(0xffb03a);
            Assert.AreEqual(want.r, c.r, 2e-3f);
            Assert.AreEqual(want.g, c.g, 2e-3f);
            Assert.AreEqual(want.b, c.b, 2e-3f);
            Assert.AreEqual(0.95f, c.a, 2e-3f);
        }
    }
}
