using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>The in-game feel layer under the Pota look (spec 2026-10-03): F2 ammo tension, …</summary>
    public class FeelPlayTests
    {
        private static IEnumerator Wait(float seconds) => StudioLookPlayTests.WaitUntil(() => false, seconds);

        /// <summary>The Pota look with the feel layer on, the same deal with ammo one short of twice the cheapest block.</summary>
        private static IEnumerator LoadLowAmmo(System.Action<ColorHuntGame, ColorHuntHud> ready)
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            game.S.Feel.Enabled = true;
            game.Prepare(158, 7, null);
            int cheapest = ColorHuntRules.Cheapest(game.Board);
            game.Prepare(2 * cheapest - 1, 7, null);
            game.Begin();
            yield return null;
            yield return null;
            ready(game, hud);
        }

        [UnityTest]
        public IEnumerator Tension_BreathesRedAtTheEdge_UnderThePotaLook()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            Assert.IsTrue(hud.AmmoCounter.Pulsing, "the counter pulses under the Pota look too");
            Assert.IsTrue(hud.Tension.Active);
            float most = 0f, t = 0f;
            while (t < 0.7f) { most = Mathf.Max(most, hud.Tension.EdgeAlpha); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(most, 0.1f, "the edge breath reached its peak within a period");
        }

        [UnityTest]
        public IEnumerator Tension_StopsWhenTheRoundEnds()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            game.Prepare(0, 7, null);
            game.Begin();                                   // no affordable move: the round ends at once
            yield return null;
            yield return null;
            Assert.AreNotEqual(ColorHuntGame.State.Playing, game.Current);
            Assert.IsFalse(hud.Tension.Active);
            Assert.AreEqual(0f, hud.Tension.EdgeAlpha, 1e-4f);
        }

        [UnityTest]
        public IEnumerator Tension_ClearsTheFrameAfterFeelIsTurnedOff()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            yield return Wait(0.3f);
            Assert.IsTrue(hud.Tension.Active);
            game.S.Feel.Enabled = false;
            yield return null;
            Assert.IsFalse(hud.Tension.Active);
            Assert.AreEqual(0f, hud.Tension.EdgeAlpha, 1e-4f);
            Assert.IsFalse(hud.AmmoCounter.Pulsing, "the Pota look without feel keeps today's gold counter");
            game.S.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator AffordableCount_CountsBlocksTheAmmoCovers()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadLowAmmo((g, h) => { game = g; hud = h; });
            int expected = 0;
            foreach (var c in game.Board.Cells) if (c.Hp <= game.Board.Ammo) expected++;
            Assert.AreEqual(expected, game.AffordableCount);
            Assert.Greater(expected, 0);
        }
    }
}
