using NUnit.Framework;
using Project.ColorHunt;
using Project.Juice;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntJuiceTests
    {
        [Test]
        public void Shakes_AreHisSarsinti_AndAdd()
        {
            var go = new GameObject("Juice");
            var s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            try
            {
                var shake = go.AddComponent<CameraShake>();
                var j = go.AddComponent<ColorHuntJuice>();
                j.S = s; j.Shake = shake;
                j.Break();
                Assert.AreEqual(0.08f, shake.Linear, 1e-6f, "kirilma");
                j.Chain(3, Vector3.zero);
                Assert.AreEqual(0.08f + 0.07f + 2f * 0.03f, shake.Linear, 1e-6f, "zincir + (d − 1)·derinlikBasi, additive");
                j.Rotate();
                Assert.AreEqual(0.26f, shake.Linear, 1e-5f);
                j.Result(true);
                Assert.AreEqual(0.4f, shake.Linear, 1e-5f, "teslim 0.14");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(s);
            }
        }
    }
}
