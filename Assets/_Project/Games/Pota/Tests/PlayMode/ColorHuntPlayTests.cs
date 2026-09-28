using System;
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>
    /// Plays the Color Hunt scene as loaded from disk: three taps on affordable target blocks must each settle,
    /// spend ammo, bring the HUD ammo along and count toward the targets (guards the 2026-09-23 "busy" lock).
    /// </summary>
    public class ColorHuntPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static int TargetsLeft(Board b)
        {
            int sum = 0;
            foreach (var t in b.Targets) sum += t.Remaining;
            return sum;
        }

        private static int CheapestTargetCell(Board b)
        {
            int best = -1;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var c = b.Cells[i];
                if (c.Hp > b.Ammo) continue;
                bool wanted = false;
                foreach (var t in b.Targets) wanted |= t.Color == c.Color && t.Remaining > 0;
                if (wanted && (best < 0 || c.Hp < b.Cells[best].Hp)) best = i;
            }
            return best;
        }

        [UnityTest]
        public IEnumerator ThreeTaps_Settle_SpendAmmo_AndCountTowardTargets()
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found in ColorHunt scene");
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current, "round did not start");
            int ammoStart = game.Board.Ammo;
            int targetsStart = TargetsLeft(game.Board);

            for (int move = 0; move < 3 && game.Current == ColorHuntGame.State.Playing; move++)
            {
                yield return WaitUntil(() => !game.Busy, 20f);
                Assert.IsFalse(game.Busy, $"move {move}: animation queue never settled");
                int cell = CheapestTargetCell(game.Board);
                Assert.GreaterOrEqual(cell, 0, "no affordable target block");
                Assert.AreEqual("broken", game.Tap(cell), $"move {move}: tap on cell {cell}");
            }

            yield return WaitUntil(() => !game.Busy, 20f);
            Assert.IsFalse(game.Busy, "animation queue never settled after the last move");
            Assert.Less(game.Board.Ammo, ammoStart, "ammo was not spent");
            Assert.AreEqual(game.Board.Ammo, game.DisplayedAmmo, "HUD ammo did not catch up with the board");
            Assert.Less(TargetsLeft(game.Board), targetsStart, "targets did not go down");
        }
    }
}
