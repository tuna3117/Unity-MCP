using NUnit.Framework;
using Project.Juice;
using UnityEngine;

namespace Project.Tests
{
    public class SecondaryMotionTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Squash_KeepsTheVolume_AndSpringsBackWithAnOvershoot()
        {
            _go = new GameObject("wrapper");
            var ss = _go.AddComponent<SquashStretch>();
            ss.Pulse(-0.3f);
            var s = _go.transform.localScale;
            Assert.AreEqual(0.7f, s.y, 1e-4f);
            Assert.AreEqual(1f, s.x * s.y * s.z, 1e-3f, "volume kept");
            float max = 0f;
            for (int i = 0; i < 60; i++)
            {
                ss.Tick(1f / 60f);
                max = Mathf.Max(max, _go.transform.localScale.y);
            }
            Assert.Greater(max, 1.02f, "springs past rest (a little stretch after the squash)");
            for (int i = 0; i < 120; i++) ss.Tick(1f / 60f);
            Assert.AreEqual(1f, _go.transform.localScale.y, 0.01f, "back at rest after 3 s");
        }

        [Test]
        public void Land_SquashesByTheImpactSpeed_WithinTheLimit()
        {
            _go = new GameObject("wrapper");
            var ss = _go.AddComponent<SquashStretch>();
            ss.Land(4f);
            float soft = _go.transform.localScale.y;
            ss.Land(40f);
            Assert.Less(soft, 1f);
            Assert.AreEqual(0.65f, _go.transform.localScale.y, 1e-4f, "clamped at the limit");
        }

        [Test]
        public void SpringBone_LagsBehindAMove_ThenSettles()
        {
            _go = new GameObject("head");
            var a = new GameObject("tuft").transform;
            a.SetParent(_go.transform, false);
            a.localPosition = new Vector3(0f, 0.2f, 0f);
            var b = new GameObject("tip").transform;
            b.SetParent(a, false);
            b.localPosition = new Vector3(0f, 0.1f, 0f);
            var spring = a.gameObject.AddComponent<SpringBone>();
            Assert.AreEqual(2, spring.BoneCount);
            spring.Step(1f / 60f);
            Vector3 restUp = a.up;
            _go.transform.position += new Vector3(0.3f, 0f, 0f);   // the head jerks sideways
            spring.Step(1f / 60f);
            Assert.Greater(Vector3.Angle(restUp, a.up), 5f, "the tuft trails behind");
            Assert.Less(a.up.x, 0f, "leans away from the move");
            for (int i = 0; i < 300; i++) spring.Step(1f / 60f);
            Assert.Less(Vector3.Angle(Vector3.up, a.up), 8f, "settles near the animated pose (gravity sags it a little)");
        }
    }
}
