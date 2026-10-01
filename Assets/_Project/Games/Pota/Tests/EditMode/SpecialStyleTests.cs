using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The special-look switch (mirrors BurstStyleTests): Pota by default, fallback with one warning, the old style cleared.</summary>
    public class SpecialStyleTests
    {
        private class FakeSpecial : ISpecialStyle
        {
            public bool IsReady = true;
            public int Built, Cleared, Fires;
            public bool Active;
            public bool Ready => IsReady;
            public int LiveCount => 0;
            public SpecialTiming Timing => SpecialTiming.Pota;
            public bool GathersBirthGroup => false;
            public bool ConsumesSpecialView => false;
            public void Build(ColorHuntGame game) => Built++;
            public void Activate(bool on) => Active = on;
            public void Clear() => Cleared++;
            public void Idle(BlockView v, float dt) { }
            public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived) => arrived(v);
            public void Birth(BlockView v, SpecialKind kind) { }
            public void TriggerStep(int specials, Vector2 centre) { }
            public void Fire(SpecialFire fire) => Fires++;
            public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed) { passedGate(); landed(); }
        }

        private GameObject _go;

        private ColorHuntGame MakeGame()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = ScriptableObject.CreateInstance<ColorHuntSettings>();
            return game;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void DefaultSpecialLook_IsPota_AllAtOnce()
        {
            var game = MakeGame();
            Assert.AreEqual(SpecialLook.Pota, ScriptableObject.CreateInstance<ColorHuntSettings>().SpecialLook, "the settings default");
            Assert.AreEqual(SpecialLook.Pota, game.SpecialLook);
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
            Assert.IsTrue(game.Specials.Ready);
            Assert.AreEqual(SpecialTiming.Pota, game.Specials.Timing, "every cell of a trigger step swells at once");
            Assert.IsFalse(game.Specials.GathersBirthGroup);
            Assert.IsFalse(game.Specials.ConsumesSpecialView);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(0, game.GoldenLanded);
        }

        [Test]
        public void MissingAssets_FallBackToPota_WithOneWarning()
        {
            var game = MakeGame();
            game.UseSpecialStyle(SpecialLook.Royal, new FakeSpecial { IsReady = false });
            LogAssert.Expect(LogType.Warning, new Regex("Royal special look is missing assets"));
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreEqual(SpecialLook.Royal, game.SpecialLook, "the look stays what was asked");
            Assert.IsInstanceOf<PotaSpecials>(game.Specials, "the drawing falls back to Pota's");
            game.SetSpecialLook(SpecialLook.Royal);                // the same look again: nothing, no second warning
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void UnwrittenLook_FallsBackToPota()
        {
            var game = MakeGame();                                 // in-memory settings: no Blender hand-offs
            LogAssert.Expect(LogType.Warning, new Regex("Court special look is missing assets"));
            game.SetSpecialLook(SpecialLook.Court);
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
        }

        [Test]
        public void SetSpecialLook_ClearsTheOldStyle()
        {
            var game = MakeGame();
            var royal = new FakeSpecial();
            game.UseSpecialStyle(SpecialLook.Royal, royal);
            Assert.AreEqual(1, royal.Built);
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreSame(royal, game.Specials);
            Assert.IsTrue(royal.Active);
            game.SetSpecialLook(SpecialLook.Pota);
            Assert.AreEqual(1, royal.Cleared, "the old style drops its effects");
            Assert.IsFalse(royal.Active, "and is deactivated");
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
        }

        [Test]
        public void SpecialLookChanged_FiresOncePerChange()
        {
            var game = MakeGame();
            game.UseSpecialStyle(SpecialLook.Royal, new FakeSpecial());
            int fired = 0;
            game.SpecialLookChanged += () => fired++;
            game.SetSpecialLook(SpecialLook.Royal);
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void SpecialSamples_PlayWithPotasJitter()
        {
            foreach (var id in new[] { "patlama", "cizgi", "ozelDogdu", "altinInis", "swish", "smac", "kalabalik" })
                Assert.IsTrue(PotaSounds.IsSample(id), $"ses.ts ORNEK {id}");
            Assert.IsFalse(PotaSounds.IsSample("zip"), "zip is a synth");
        }

        [Test]
        public void Zip_IsSesTsSweep()
        {
            var clip = PotaSounds.MakeZip();
            Assert.AreEqual(Mathf.CeilToInt(0.18f * 44100), clip.samples);
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float most = 0f;
            foreach (var s in data) most = Mathf.Max(most, Mathf.Abs(s));
            Assert.Greater(most, 0.12f, "gain 0.18 after the attack");
            Assert.LessOrEqual(most, 0.1801f);
            Object.DestroyImmediate(clip);
        }

        [Test]
        public void Texts_ArePotas()
        {
            Assert.AreEqual("ÇİZGİ TOPU!", SpecialTexts.Born(SpecialKind.LineV), "dil.ts ozelCizgi");
            Assert.AreEqual("ÇİZGİ TOPU!", SpecialTexts.Born(SpecialKind.LineH));
            Assert.AreEqual("BOMBA!", SpecialTexts.Born(SpecialKind.Bomb));
            Assert.AreEqual("ALTIN TOP!", SpecialTexts.Born(SpecialKind.Golden));
            Assert.AreEqual("Altın top geçişten sepete inmeli", SpecialTexts.GoldenTap, "dil.ts altinSepete");
        }
    }
}
