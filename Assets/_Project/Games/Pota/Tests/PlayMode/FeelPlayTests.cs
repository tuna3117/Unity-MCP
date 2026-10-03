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

        /// <summary>A one-red-block level: tapping any affordable red block wins. Returns that cell.</summary>
        private static int StageOneRedTarget(ColorHuntGame game, int ammo)
        {
            var design = game.S.ToDesign();
            design.Targets = new[] { (0, 1) };
            game.Prepare(ammo, 7, design);
            game.Begin();
            for (int i = 0; i < game.Board.Cells.Length; i++)
                if (game.Board.Cells[i].Color == 0 && game.Board.Cells[i].Hp <= game.Board.Ammo) return i;
            return -1;
        }

        [UnityTest]
        public IEnumerator Finale_WithLeftoverAmmo_FliesBallsIntoBlocks_CoinsByPotasFormula_ThenTheRoundEnds()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            int cell = StageOneRedTarget(game, 140);
            Assume.That(cell >= 0, "an affordable red block");
            bool ended = false;
            game.RoundEnded += (won, reason) => ended = won;
            Assert.AreEqual("broken", game.Tap(cell));
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 6f);
            Assert.AreEqual(ColorHuntGame.State.Ended, game.Current);
            int remaining = game.Board.Ammo;
            Assume.That(remaining >= 10, "enough ammo left for coins");
            yield return null;
            Assert.IsTrue(hud.Finale.Running, "the finale starts at the win");
            Assert.IsFalse(ended, "the result waits for the finale");
            float t = 0f;
            while (hud.Finale.Running && t < 2.5f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsFalse(hud.Finale.Running, "the finale is over within the cap");
            Assert.LessOrEqual(t, game.S.Feel.FinaleMax + 0.3f);
            Assert.AreEqual(FeelCurves.LeftoverCoins(remaining), hud.Finale.CoinsShown);
            Assert.GreaterOrEqual(hud.Finale.Flights, game.S.Feel.FinaleMinFlights);
            Assert.LessOrEqual(hud.Finale.Flights, game.S.Feel.FinaleMaxFlights);
            Assert.AreEqual(0, game.DisplayedAmmo, "the counter rolled down to zero");
            yield return StudioLookPlayTests.WaitUntil(() => ended, game.S.WinWait + 0.6f);
            Assert.IsTrue(ended, "RoundEnded follows the finale and the disperse");
        }

        [UnityTest]
        public IEnumerator Finale_WithNoAmmoLeft_TakesNoFlights_ShowsNoBadge_AndEndsAtOnce()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 2f);
            int probe = StageOneRedTarget(game, 158);
            Assume.That(probe >= 0);
            int cell = StageOneRedTarget(game, game.Board.Cells[probe].Hp);   // the same deal with exactly that block's cost
            Assume.That(cell == probe, "the cheapest-covered red block is the same cell");
            bool ended = false;
            game.RoundEnded += (won, reason) => ended = won;
            Assert.AreEqual("broken", game.Tap(cell));
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Ended, 6f);
            Assert.AreEqual(0, game.Board.Ammo);
            yield return null;
            Assert.AreEqual(0, hud.Finale.Flights);
            Assert.IsFalse(hud.Finale.BadgeVisible);
            yield return StudioLookPlayTests.WaitUntil(() => ended, game.S.WinWait + 0.4f);
            Assert.IsTrue(ended);
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
