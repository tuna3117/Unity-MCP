using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class BurstStyleTests
    {
        private class FakeStyle : IBurstStyle
        {
            public bool IsReady = true;
            public int Built, Cleared, Taps;
            public bool Active;
            public bool Ready => IsReady;
            public bool ShowsChainNumber => false;
            public int LiveCount => 0;
            public void Build(ColorHuntGame game) => Built++;
            public void Activate(bool on) => Active = on;
            public void TapBurst(BlockView v, Color tone) => Taps++;
            public void StepBurst(BlockView v, Color tone, int depth) { }
            public void ChainPeak(int depth, Vector2 at, Color tone) { }
            public void Clear() => Cleared++;
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
        public void DefaultLook_IsPota_WhosStyleIsAlwaysReady()
        {
            var game = MakeGame();
            Assert.AreEqual(PuzzleLook.Pota, ScriptableObject.CreateInstance<ColorHuntSettings>().Look, "the settings default");
            Assert.AreEqual(PuzzleLook.Pota, game.Look);
            Assert.IsFalse(game.Studio);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style);
            Assert.IsTrue(game.Style.Ready);
            Assert.IsTrue(game.Style.ShowsChainNumber, "Pota shows its ×d popup");
        }

        [Test]
        public void MissingAssets_FallBackToPota()
        {
            var game = MakeGame();                            // in-memory settings: no flipbooks, no pieces
            LogAssert.Expect(LogType.Warning, new Regex("Candy look is missing assets"));
            game.SetLook(PuzzleLook.Candy);
            Assert.AreEqual(PuzzleLook.Candy, game.Look, "the look (and the studio polish) stays what was asked");
            Assert.IsTrue(game.Studio);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style, "the burst falls back to Pota's");
        }

        [Test]
        public void SetLook_ActivatesTheNewStyle_AndClearsTheOldOne()
        {
            var game = MakeGame();
            var candy = new FakeStyle();
            game.UseStyle(PuzzleLook.Candy, candy);
            Assert.AreEqual(1, candy.Built);
            game.SetLook(PuzzleLook.Candy);
            Assert.AreSame(candy, game.Style);
            Assert.IsTrue(candy.Active);
            game.SetLook(PuzzleLook.Pota);
            Assert.AreEqual(1, candy.Cleared);
            Assert.IsFalse(candy.Active);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style);
        }

        [Test]
        public void LookChanged_FiresOncePerChange()
        {
            var game = MakeGame();
            game.UseStyle(PuzzleLook.Candy, new FakeStyle());
            int fired = 0;
            game.LookChanged += () => fired++;
            game.SetLook(PuzzleLook.Candy);
            game.SetLook(PuzzleLook.Candy);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Budget_IsHalfAgainPotasPools()
        {
            Assert.AreEqual(12 + 12 + 12 + 72 + 160 + 16 + 160 + 240, BurstBudget.Pota);
            Assert.AreEqual(1026, BurstBudget.Max);
        }
    }
}
