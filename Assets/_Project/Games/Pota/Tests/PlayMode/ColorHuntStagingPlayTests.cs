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
    /// <summary>Staged rounds (PotaFlow): Prepare builds an idle, locked board; ShowAmmo drives the counter; Begin plays.</summary>
    public class ColorHuntStagingPlayTests
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
            ready(game, Object.FindAnyObjectByType<ColorHuntHud>());
        }

        private static Text AmmoText(ColorHuntHud hud) => hud.transform.Find("AmmoPanel/Ammo").GetComponent<Text>();

        [UnityTest]
        public IEnumerator Prepare_StagesALockedBoard_ThenBeginPlaysIt()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });

            game.Prepare(37);
            Assert.AreEqual(ColorHuntGame.State.Idle, game.Current);
            Assert.AreEqual(37, game.Board.Ammo);
            Assert.IsNotNull(game.ViewAt(0), "views must exist while staged");
            Assert.AreEqual("locked", game.Tap(0));
            Assert.IsFalse(game.Rotate(Rotation.Clockwise), "rotation must be locked while staged");

            game.ShowAmmo(5);
            game.ViewAt(0).Scale = 0.5f;
            yield return null;
            Assert.AreEqual(5, game.DisplayedAmmo);
            Assert.AreEqual("5", AmmoText(hud).text);
            Assert.AreEqual(0.5f, game.ViewAt(0).transform.localScale.z, 1e-3f, "staged views must keep ticking");

            game.Begin();
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current);
            Assert.AreEqual(37, game.DisplayedAmmo);
            yield return null;
            Assert.AreEqual("37", AmmoText(hud).text);
        }

        [UnityTest]
        public IEnumerator NewRoundWithAmmo_StartsPlayingAtOnce()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            game.NewRound(12);
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current);
            Assert.AreEqual(12, game.Board.Ammo);
        }

        [UnityTest]
        public IEnumerator SetIntro_SlidesThePanelsOut_AndBackExactly()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            var ammo = (RectTransform)hud.transform.Find("AmmoPanel");
            var targets = (RectTransform)hud.transform.Find("TargetPanel");
            var right = (RectTransform)hud.transform.Find("RotateRight");
            Vector2 ammoHome = ammo.anchoredPosition, targetsHome = targets.anchoredPosition, rightHome = right.anchoredPosition;

            hud.SetIntro(0f);
            Assert.Less(ammo.anchoredPosition.x, ammoHome.x - 300f, "ammo panel must leave to the left");
            Assert.Greater(targets.anchoredPosition.y, targetsHome.y + 250f, "target panel must leave upward");
            Assert.Less(right.anchoredPosition.y, rightHome.y - 250f, "rotate button must leave downward");

            hud.SetIntro(1f);
            Assert.AreEqual(ammoHome, ammo.anchoredPosition);
            Assert.AreEqual(targetsHome, targets.anchoredPosition);
            Assert.AreEqual(rightHome, right.anchoredPosition);

            Vector2 screen = hud.AmmoScreenPoint;
            Assert.That(screen.x, Is.InRange(0f, Screen.width / 2f), "ammo number sits on the left half");
            Assert.That(screen.y, Is.InRange(Screen.height / 2f, (float)Screen.height), "and on the upper half");

            hud.PopAmmo();
            yield return null;
            Assert.Greater(AmmoText(hud).transform.localScale.x, 1.01f, "ammo pop");
        }

        [UnityTest]
        public IEnumerator CameraFit_FollowsTheAnchor()
        {
            ColorHuntGame game = null; ColorHuntHud hud = null;
            yield return LoadPlaying((g, h) => { game = g; hud = h; });
            var fit = Object.FindAnyObjectByType<ColorHuntCamera>();
            var cam = fit.GetComponent<Camera>();
            fit.Anchor = new Vector3(0f, 8.5f, 30f);
            yield return null;
            var expected = ColorHuntCamera.FitPosition(game.S.Size, cam.fieldOfView, cam.aspect, fit.Anchor);
            Assert.Less(Vector3.Distance(cam.transform.position, expected), 0.3f, "camera must fit the anchored board (shake aside)");
            Assert.Less(Vector3.Distance(fit.FitFor(cam.fieldOfView, cam.aspect), expected), 1e-4f);
            fit.Anchor = Vector3.zero;
        }
    }
}
