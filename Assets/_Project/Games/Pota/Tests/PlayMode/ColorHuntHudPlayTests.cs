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

        private static int Remaining(ColorHuntGame game, int color)
        {
            foreach (var t in game.Board.Targets) if (t.Color == color) return Mathf.Max(0, t.Remaining);
            return 0;
        }

        /// <summary>The cheapest affordable block of a colour that is still wanted.</summary>
        private static int CheapTargetCell(ColorHuntGame game, out int color)
        {
            int best = -1, bestHp = int.MaxValue;
            color = -1;
            var b = game.Board;
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var c = b.Cells[i];
                if (c.Hp > b.Ammo || c.Hp >= bestHp || Remaining(game, c.Color) <= 0) continue;
                bool wanted = false;
                foreach (var t in b.Targets) wanted |= t.Color == c.Color;
                if (!wanted) continue;
                best = i; bestHp = c.Hp; color = c.Color;
            }
            return best;
        }

        [UnityTest]
        public IEnumerator TargetCount_DropsPerArrival_ThenMatchesTheRules()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            int cell = CheapTargetCell(game, out int color);
            Assert.GreaterOrEqual(cell, 0, "no affordable target block on this board");
            int before = game.ShownTarget(color);
            Assert.AreEqual("broken", game.Tap(cell));
            bool lagged = false, flew = false;
            float t = 0f;
            while ((game.Busy || game.ItemsInFlight > 0) && t < 8f)
            {
                int rules = Remaining(game, color), shown = game.ShownTarget(color);
                Assert.GreaterOrEqual(shown, rules, "the shown count never goes below the rules");
                Assert.LessOrEqual(shown, before);
                lagged |= shown > rules;
                flew |= hud.FlyingCount > 0;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
            Assert.IsTrue(flew, "the sticker flew to the HUD");
            Assert.IsTrue(lagged, "the count waits for the sticker (renkAviFaz.ts hedefAzalt)");
            Assert.AreEqual(Remaining(game, color), game.ShownTarget(color), "settled and nothing flying: shown = rules");
            Assert.AreEqual(game.ShownTarget(color).ToString(), hud.TargetText(color));
        }

        [UnityTest]
        public IEnumerator NewRound_WhileAStickerFlies_KeepsTheNewCounts()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            int cell = CheapTargetCell(game, out _);
            Assert.GreaterOrEqual(cell, 0);
            game.Tap(cell);
            yield return WaitUntil(() => hud.FlyingCount > 0, 4f);
            Assert.Greater(hud.FlyingCount, 0);
            game.NewRound();
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 0.8f) yield return null;
            foreach (var tg in game.Board.Targets)
                Assert.AreEqual(Mathf.Max(0, tg.Remaining), game.ShownTarget(tg.Color), "an old flight must not lower the new board's count");
        }

        [UnityTest]
        public IEnumerator Layout_IsPotas_CountersOnTop_ArrowsRightUnderTheBackboard()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            yield return null;
            float k = ColorHuntHud.Css;
            var ammo = (RectTransform)hud.transform.Find("AmmoPanel");
            var targets = (RectTransform)hud.transform.Find("TargetPanel");
            var right = (RectTransform)hud.transform.Find("RotateRight");
            var left = (RectTransform)hud.transform.Find("RotateLeft");
            Assert.AreEqual(14f * k, ammo.anchoredPosition.x, 0.5f, "14 px from the left");
            Assert.AreEqual(-14f * k, targets.anchoredPosition.x, 0.5f, "14 px from the right");
            Assert.AreEqual(63f * k, ammo.sizeDelta.y, 1f, ".sayac height");
            Assert.AreEqual(64f * k, right.sizeDelta.x, 0.5f, ".dondur 64 px");
            Assert.AreEqual(-14f * k, right.anchoredPosition.x, 0.5f);
            Assert.AreEqual(right.anchoredPosition.y - 72f * k, left.anchoredPosition.y, 0.5f, "stacked: right on top, 8 px gap");
            var cam = game.Cam;
            var vp = cam.WorldToViewportPoint(game.transform.TransformPoint(new Vector3(0f, -4.75f, -0.45f)));
            float canvasH = ((RectTransform)hud.transform).rect.height;
            float expectedTop = Mathf.Min((1f - vp.y) * canvasH / k + 34f, canvasH / k - 150f);
            Assert.AreEqual(-expectedTop * k, right.anchoredPosition.y, 2f * k, "34 px under the backboard (index.html #yAlt)");
        }
    }
}
