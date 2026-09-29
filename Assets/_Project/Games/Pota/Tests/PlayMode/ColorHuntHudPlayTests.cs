using System;
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>
    /// The HUD buttons must work in the scene as loaded from disk: listeners added while the scene tool builds the HUD
    /// in Edit Mode are not serialized, so the HUD wires them when it binds at runtime.
    /// </summary>
    public class ColorHuntHudPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator LoadPlaying(Action<ColorHuntGame, ColorHuntHud> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current, "round did not start");
            ready(game, Object.FindAnyObjectByType<ColorHuntHud>());
        }

        private static Button ButtonAt(ColorHuntHud hud, string path)
        {
            var t = hud.transform.Find(path);
            Assert.IsNotNull(t, $"HUD has no {path}");
            return t.GetComponent<Button>();
        }

        [UnityTest]
        public IEnumerator RotateRightButton_RotatesTheBoard()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            ButtonAt(hud, "RotateRight").onClick.Invoke();
            Assert.AreEqual(1, game.Rotations, "rotate button did nothing (listener lost when the scene was saved)");
        }

        [UnityTest]
        public IEnumerator RetryButton_DealsANewBoard_UnlessOverridden()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            int started = 0;
            game.RoundStarted += () => started++;
            var retry = ButtonAt(hud, "ResultPanel/Card/Retry");
            retry.onClick.Invoke();
            Assert.AreEqual(1, started, "retry did not deal a new board");

            int overridden = 0;
            hud.RetryOverride = () => overridden++;
            retry.onClick.Invoke();
            Assert.AreEqual(1, overridden, "RetryOverride not called");
            Assert.AreEqual(1, started, "the override must replace the new-board action");

            hud.SetRetryLabel("Tekrar at");
            Assert.AreEqual("Tekrar at", hud.transform.Find("ResultPanel/Card/Retry/Label").GetComponent<Text>().text);
            var panel = hud.transform.Find("ResultPanel").gameObject;
            panel.SetActive(true);
            hud.HideResult();
            Assert.IsFalse(panel.activeSelf, "HideResult must close the result panel");
        }
    }
}
