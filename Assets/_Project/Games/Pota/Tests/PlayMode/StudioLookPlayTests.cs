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
    /// <summary>The look switch in the Color Hunt scene as saved.</summary>
    public class StudioLookPlayTests
    {
        public static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        /// <summary>Loads ColorHunt, switches to <paramref name="look"/>, deals a fresh round and waits until it can be played.</summary>
        public static IEnumerator LoadLook(PuzzleLook look, Action<ColorHuntGame, ColorHuntHud> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            game.SetLook(look);
            game.NewRound();
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current, "round did not start");
            ready(game, Object.FindAnyObjectByType<ColorHuntHud>());
        }

        [UnityTest]
        public IEnumerator SavedScene_PlaysThePotaLook_ThroughTheSeam()
        {
            ColorHuntGame game = null;
            yield return LoadLook(PuzzleLook.Pota, (g, h) => game = g);
            Assert.IsInstanceOf<PotaBurstStyle>(game.Style);
            int cell = ColorHuntHint.Best(game.Board);
            Assert.AreEqual("broken", game.Tap(cell));
            int most = 0;
            float t = 0f;
            while (t < 1.2f) { most = Mathf.Max(most, game.Fx.LiveCount); t += Time.unscaledDeltaTime; yield return null; }
            Assert.Greater(most, 0, "Pota's burst layers drew");
            Assert.LessOrEqual(most, BurstBudget.Pota);
        }
    }
}
