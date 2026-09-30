using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>Press feedback, ammo counter, board intro and idle hint (spec §3.2–3.5).</summary>
    public class StudioPolishPlayTests
    {
        private static IEnumerator Wait(float seconds) => StudioLookPlayTests.WaitUntil(() => false, seconds);

        [UnityTest]
        public IEnumerator Press_SinksTheBlock_ShowsItsCost_AndSpringsBack()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = ColorHuntHint.Best(game.Board);
            var view = game.ViewAt(cell);
            game.Select(cell);
            yield return Wait(0.2f);
            Assert.IsTrue(view.Pressed);
            Assert.AreEqual(StudioCurves.PressScale(1f), view.PressScale, 1e-3f);
            Assert.AreEqual("−" + game.Board.Cells[cell].Hp, hud.AmmoCounter.GhostText);
            Assert.IsFalse(hud.AmmoCounter.GhostShort);
            game.Select(-1);
            float most = 0f, t = 0f;
            while (t < 0.35f) { most = Mathf.Max(most, view.PressScale); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(most, 1.02f, "a springy release");
            Assert.AreEqual(1f, view.PressScale, 1e-4f);
            Assert.AreEqual("", hud.AmmoCounter.GhostText);
        }

        [UnityTest]
        public IEnumerator PressingABlockTheAmmoCannotCover_ShowsARedCost()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            game.Prepare(1, 7, null);
            game.Begin();
            yield return null;
            int cell = -1;
            for (int i = 0; i < game.Board.Cells.Length && cell < 0; i++) if (game.Board.Cells[i].Hp > 1) cell = i;
            Assert.GreaterOrEqual(cell, 0);
            game.Select(cell);
            yield return null;
            Assert.IsTrue(hud.AmmoCounter.GhostShort);
        }

        [UnityTest]
        public IEnumerator PotaLook_KeepsPotasSelection()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => { game = g; hud = h; });
            int cell = ColorHuntHint.Best(game.Board);
            game.Select(cell);
            yield return Wait(0.2f);
            Assert.IsFalse(game.ViewAt(cell).Pressed);
            Assert.AreEqual(1f, game.ViewAt(cell).PressScale, 1e-5f);
            Assert.AreEqual("", hud.AmmoCounter.GhostText);
        }

        [UnityTest]
        public IEnumerator Salvo_RollsTheAmmoDown()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            int cell = -1;
            for (int i = 0; i < game.Board.Cells.Length && cell < 0; i++)
                if (game.Board.Cells[i].Hp >= 5 && game.Board.Cells[i].Hp <= game.Board.Ammo) cell = i;
            Assert.AreEqual("broken", game.Tap(cell));
            bool rolled = false;
            float t = 0f;
            while (t < 0.6f) { rolled |= hud.AmmoCounter.Rolling; t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(rolled);
        }

        [UnityTest]
        public IEnumerator LowAmmo_Pulses()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            game.Prepare(158, 7, null);
            int cheapest = ColorHuntRules.Cheapest(game.Board);
            game.Prepare(2 * cheapest - 1, 7, null);
            game.Begin();
            Assume.That(ColorHuntRules.Cheapest(game.Board) * 2 > game.Board.Ammo, "the same deal with less ammo");
            yield return null;
            yield return null;
            Assert.IsTrue(hud.AmmoCounter.Pulsing);
        }


        [UnityTest]
        public IEnumerator RollingAmmo_IsClippedToItsSlot_OnlyInStudioLooks()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            yield return null;
            Assert.IsTrue(hud.AmmoCounter.Clipped, "the rolling number stays under the CEPHANE label");
            game.SetLook(PuzzleLook.Pota);
            yield return null;
            Assert.IsFalse(hud.AmmoCounter.Clipped, "Pota's ammo pop is not clipped");
        }

        [UnityTest]
        public IEnumerator Cost_SitsInADarkChip()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => { game = g; hud = h; });
            game.Select(ColorHuntHint.Best(game.Board));
            yield return Wait(0.2f);
            Assert.Greater(hud.AmmoCounter.ChipAlpha, 0.99f, "the cost reads on a navy chip, not on the sky");
            game.Select(-1);
            yield return Wait(0.3f);
            Assert.Less(hud.AmmoCounter.ChipAlpha, 0.01f);
        }

        [UnityTest]
        public IEnumerator Intro_PopsTheBoardInFromTheBottomRow()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            game.NewRound();
            yield return null;
            yield return null;
            Assert.IsTrue(game.Intro);
            Assert.IsTrue(game.Busy, "no taps during the intro");
            int n = game.Board.Size;
            var bottom = game.ViewAt(ColorHuntRules.Index(n, n - 1, 0));
            var top = game.ViewAt(ColorHuntRules.Index(n, 0, 0));
            Assert.Greater(bottom.Scale, top.Scale, "the bottom row pops first");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Intro, 1f);
            Assert.IsFalse(game.Intro);
            Assert.AreEqual(1f, top.Scale, 1e-5f);
            Assert.IsFalse(game.Busy);
        }

        [UnityTest]
        public IEnumerator IdleHint_WigglesTheBestBlock_AfterFiveSeconds_AndStopsOnTouch()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Candy, (g, h) => game = g);
            float start = Time.unscaledTime;
            yield return StudioLookPlayTests.WaitUntil(() => game.HintCell >= 0, 6.5f);
            Assert.GreaterOrEqual(Time.unscaledTime - start, StudioCurves.HintDelay - 0.1f);
            int cell = game.HintCell;
            Assert.AreEqual(ColorHuntHint.Best(game.Board), cell);
            var view = game.ViewAt(cell);
            bool wiggled = false;
            float t = 0f;
            while (t < 0.3f) { wiggled |= Mathf.Abs(view.Wiggle) > 0.5f; t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(wiggled);
            game.Select(cell);
            yield return null;
            Assert.AreEqual(-1, game.HintCell);
            Assert.AreEqual(0f, view.Wiggle);
        }

        [UnityTest]
        public IEnumerator PotaLook_DealsWithoutIntro_AndNeverHints()
        {
            ColorHuntGame game = null;
            yield return StudioLookPlayTests.LoadLook(PuzzleLook.Pota, (g, h) => game = g);
            game.NewRound();
            yield return null;
            Assert.IsFalse(game.Intro);
            yield return Wait(StudioCurves.HintDelay + 0.6f);
            Assert.AreEqual(-1, game.HintCell);
        }
    }
}
