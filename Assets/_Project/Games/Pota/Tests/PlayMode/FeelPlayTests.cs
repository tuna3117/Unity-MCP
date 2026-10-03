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
        public IEnumerator Intro_UnderThePotaLook_TapsWait_RibbonAndGoalsPlay_ReadyUnder1_8s()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            game.NewRound();
            yield return null;
            yield return null;
            Assert.IsTrue(game.Intro, "the feel layer's intro runs under the Pota look");
            Assert.IsTrue(game.Busy, "taps wait");
            Assert.IsFalse(hud.Intro.Done);
            Assert.IsTrue(hud.Intro.RibbonVisible, "the BÖLÜM ribbon is up");
            Assert.AreEqual("BÖLÜM " + game.S.LevelNumber, hud.Intro.Title);
            int n = game.Board.Size;
            var top = game.ViewAt(ColorHuntRules.Index(n, 0, 0));
            Assert.AreEqual(0f, top.Scale, 1e-5f, "blocks wait for the ribbon and the goals");
            float t = 0f;
            while (game.Intro && t < 1.8f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsFalse(game.Intro, "ready within 1.8 s");
            Assert.AreEqual(1f, top.Scale, 1e-5f);
            Assert.IsTrue(hud.Intro.Done);
            Assert.AreEqual(game.Board.Targets.Length, hud.Intro.GoalsArrived, "every goal chip flew to its row");
            Assert.IsFalse(hud.Intro.RibbonVisible);
        }

        [UnityTest]
        public IEnumerator Intro_Skip_FinishesWithin0_35s()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            game.NewRound();
            yield return Wait(0.2f);
            Assert.IsTrue(game.Intro);
            game.SkipIntro();
            float t = 0f;
            while (game.Intro && t < 0.35f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsFalse(game.Intro, "a tap compresses the rest of the intro");
            Assert.IsTrue(hud.Intro.Done);
        }

        [UnityTest]
        public IEnumerator Landing_BouncesDirectionally_UnderTheFeelLayer()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            int cell = ColorHuntHint.Best(game.Board);
            Assert.AreEqual("broken", game.Tap(cell));
            bool bounced = false;
            float t = 0f;
            while (t < 3f && !bounced)
            {
                for (int i = 0; i < game.Board.Cells.Length; i++)
                {
                    var v = game.ViewAt(i);
                    if (v != null && v.Bounce >= 0f && v.transform.localScale.x > v.transform.localScale.y * 1.1f) bounced = true;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(bounced, "a landing block was wider than tall");
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
