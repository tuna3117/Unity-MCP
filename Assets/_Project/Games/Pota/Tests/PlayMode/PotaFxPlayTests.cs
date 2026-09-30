using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Juice;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class PotaFxPlayTests
    {
        private GameObject _root;
        private ColorHuntSettings _s;
        private TimeScaleController[] _paused = new TimeScaleController[0];

        [TearDown]
        public void TearDown()
        {
            foreach (var c in _paused) if (c != null) c.enabled = true;
            _paused = new TimeScaleController[0];
            Time.timeScale = 1f;
            if (_root != null) Object.Destroy(_root);
            if (_s != null) Object.Destroy(_s);
        }

        private ColorHuntFx Make(out Transform board)
        {
            _s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            _root = new GameObject("Game");
            _root.transform.position = new Vector3(0f, 8.5f, 30f);
            board = new GameObject("Board").transform;
            board.SetParent(_root.transform, false);
            board.localScale = Vector3.one * 1.2f;
            var fx = new GameObject("Effects").AddComponent<ColorHuntFx>();
            fx.transform.SetParent(_root.transform, false);
            fx.S = _s;
            fx.Build();
            fx.SetBoard(board);
            return fx;
        }

        private static int Count(ColorHuntFx fx, string name) => fx.transform.Find(name).GetComponent<ParticleSystem>().particleCount;

        [UnityTest]
        public IEnumerator Burst_EmitsPotasLayers_ScaledByDepth()
        {
            var fx = Make(out var board);
            fx.Burst(Vector2.zero, PotaLook.Tones[1], 2);
            yield return null;
            Assert.AreEqual(24, Count(fx, "Shards"), "min(34, 14 + 5·2)");
            Assert.AreEqual(14, Count(fx, "Stars"), "10 + 2·2");
            Assert.AreEqual(1, Count(fx, "FlashWhite"));
            Assert.AreEqual(1, Count(fx, "FlashTone"));
            Assert.AreEqual(1, Count(fx, "Ring"));
            var light = fx.GetComponentInChildren<Light>();
            Assert.IsTrue(light.enabled);
            Assert.Greater(light.intensity, 0f);
            Assert.Less(light.transform.position.z, board.position.z - 2f, "1.9 cells in front of the flash plane");
            fx.Clear();
            yield return null;
            Assert.AreEqual(0, Count(fx, "Shards"));
            Assert.IsFalse(light.enabled);
        }

        [UnityTest]
        public IEnumerator Effects_RunOnGameTime_ADipFreezesThem()
        {
            var fx = Make(out _);
            // A scene left open by an earlier test may hold a TimeScaleController: it writes Time.timeScale every frame.
            _paused = Object.FindObjectsByType<TimeScaleController>(FindObjectsInactive.Exclude);
            foreach (var c in _paused) c.enabled = false;
            fx.Burst(Vector2.zero, PotaLook.Tones[0], 0);         // the white flash lives 0.16 s
            Time.timeScale = 0.02f;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.3f) yield return null;
            Assert.AreEqual(1, Count(fx, "FlashWhite"), "0.3 s of real time is 0.006 s of game time");
        }

        [UnityTest]
        public IEnumerator Confetti_FallsFromAboveTheBoard_WhereverItIs()
        {
            var fx = Make(out var board);
            fx.Confetti(7, 220);
            yield return null;
            var ps = fx.transform.Find("Confetti").GetComponent<ParticleSystem>();
            var parts = new ParticleSystem.Particle[ps.particleCount];
            ps.GetParticles(parts);
            Assert.AreEqual(220, parts.Length, "RENK_AVI_HIS.konfeti");
            float frameTop = board.TransformPoint(new Vector3(0f, 3.5f + 0.22f, 0f)).y;
            foreach (var p in parts) Assert.Greater(p.position.y, frameTop, "from above the frame (n/2 + KENAR + 0.8)");
        }
    }
}
