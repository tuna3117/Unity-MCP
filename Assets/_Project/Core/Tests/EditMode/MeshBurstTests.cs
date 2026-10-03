using NUnit.Framework;
using Project.VFX;
using UnityEngine;

namespace Project.Tests
{
    public class MeshBurstTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private MeshBurst Make(int seed = 7)
        {
            _root = new GameObject("burst");
            var burst = _root.AddComponent<MeshBurst>();
            burst.Seed = seed;
            for (int i = 0; i < 2; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(piece.GetComponent<Collider>());
                piece.name = "piece_" + i;
                piece.transform.SetParent(_root.transform, false);
                piece.transform.localPosition = new Vector3(i == 0 ? -0.3f : 0.3f, 0f, 0f);
                piece.transform.localScale = Vector3.one * 0.2f;
            }
            return burst;
        }

        private static Vector3 Pos(MeshBurst b, int i) => b.transform.GetChild(i).localPosition;

        [Test]
        public void EvaluateZero_IsTheRestPose()
        {
            var b = Make();
            b.Evaluate(0f);
            Assert.AreEqual(2, b.PieceCount);
            Assert.That(Vector3.Distance(Pos(b, 0), new Vector3(-0.3f, 0f, 0f)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(b.transform.GetChild(1).localScale, Vector3.one * 0.2f), Is.LessThan(1e-5f));
        }

        [Test]
        public void Pieces_FlyAwayFromTheCentre()
        {
            var b = Make();
            b.Evaluate(0.3f);
            Assert.Less(Pos(b, 0).x, -0.3f, "left piece moves left");
            Assert.Greater(Pos(b, 1).x, 0.3f, "right piece moves right");
        }

        [Test]
        public void AtTheEnd_EveryPieceIsGone()
        {
            var b = Make();
            b.Evaluate(b.Lifetime);
            for (int i = 0; i < 2; i++) Assert.That(b.transform.GetChild(i).localScale.magnitude, Is.LessThan(1e-4f));
        }

        [Test]
        public void Tick_EndsTheBurst_AndRestartPlaysAgainFromRest()
        {
            var b = Make();
            Assert.IsFalse(b.IsAlive, "not playing before Restart");
            b.Restart();
            Assert.IsTrue(b.IsAlive);
            b.Tick(0.5f);
            Assert.AreNotEqual(-0.3f, Pos(b, 0).x);
            b.Tick(b.Lifetime);
            Assert.IsFalse(b.IsAlive);
            b.Restart();
            Assert.That(Vector3.Distance(Pos(b, 0), new Vector3(-0.3f, 0f, 0f)), Is.LessThan(1e-5f), "replay starts at rest");
            b.Clear();
            Assert.IsFalse(b.IsAlive);
        }

        [Test]
        public void SameSeed_SameMotion_OtherSeed_Differs()
        {
            var a = Make(3);
            a.Evaluate(0.4f);
            Vector3 first = Pos(a, 0);
            Object.DestroyImmediate(_root);
            var b = Make(3);
            b.Evaluate(0.4f);
            Assert.That(Vector3.Distance(first, Pos(b, 0)), Is.LessThan(1e-5f));
            Object.DestroyImmediate(_root);
            var c = Make(99);
            c.Evaluate(0.4f);
            Assert.That(Vector3.Distance(first, Pos(c, 0)), Is.GreaterThan(1e-3f));
        }
    }
}
