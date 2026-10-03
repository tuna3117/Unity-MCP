using System;
using System.Collections;
using NUnit.Framework;
using Project.Basket;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The basket round's feel layer (spec 2026-10-03 F10–F13): slingshot, miss, clean-pass SWISH!, rolling count and end stamp.</summary>
    public class BasketFeelPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator Wait(float seconds) => WaitUntil(() => false, seconds);

        private class Parts { public BasketRound Round; public ShotInput Input; public SlingshotFeel Sling; public MissFeel Miss; public SwishFeel Swish; public BasketCountFeel Count; public BasketHud Hud; }

        private static IEnumerator LoadAim(Action<Parts> ready)
        {
            SceneManager.LoadScene("Basket");
            yield return null;
            var round = Object.FindAnyObjectByType<BasketRound>();
            Assert.IsNotNull(round, "BasketRound missing");
            round.Settings.Feel.Enabled = true;
            yield return WaitUntil(() => round.Column != null && round.Current == BasketRound.State.Aim, 5f);
            yield return Wait(0.6f);
            var p = new Parts
            {
                Round = round, Input = Object.FindAnyObjectByType<ShotInput>(), Sling = Object.FindAnyObjectByType<SlingshotFeel>(),
                Miss = Object.FindAnyObjectByType<MissFeel>(), Swish = Object.FindAnyObjectByType<SwishFeel>(),
                Count = Object.FindAnyObjectByType<BasketCountFeel>(), Hud = Object.FindAnyObjectByType<BasketHud>(),
            };
            Assert.IsNotNull(p.Sling, "the scene tool added SlingshotFeel");
            Assert.IsNotNull(p.Miss, "MissFeel");
            Assert.IsNotNull(p.Swish, "SwishFeel");
            Assert.IsNotNull(p.Count, "BasketCountFeel");
            ready(p);
        }

        [UnityTest]
        public IEnumerator Pull_LeansTheHero_ShowsTheHandBall_GlowsTheAimedHoop_PullsTheCamera()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            var actor = p.Round.Column.Player;
            Assert.IsTrue(actor.HandBallVisible, "the hero holds a ball while aiming");
            var aim = AimMapper.FromXPower(0f, 0.8f);
            p.Input.SimulatePull(aim);
            yield return null;
            yield return null;
            Assert.AreEqual(0.8f, p.Sling.Pull, 1e-3f);
            yield return Wait(0.3f);                                   // the lean eases in over 0.15 s
            Assert.Greater(actor.Lean, 0.5f, "the hero leans back with the pull");
            var cam = Object.FindAnyObjectByType<RoundCamera>();
            Assert.Greater(cam.PullOffset.magnitude, 0.05f, "the camera pulls back");
            var hoop = p.Round.TargetHoopFor(aim);
            Assert.IsNotNull(hoop);
            Assert.IsTrue(hoop.Spec.IsTop);
            yield return Wait(0.3f);
            Assert.Greater(hoop.Glow, 0.8f, "the aimed hoop glows");
            p.Input.SimulateRelease();
            yield return null;
            Assert.AreEqual(0f, p.Sling.Pull, 1e-3f);
            yield return Wait(0.4f);
            Assert.Less(actor.Lean, 0.05f, "the lean eases out after the release");
            Assert.Less(hoop.Glow, 0.1f);
        }

        [UnityTest]
        public IEnumerator Shot_HidesTheHandBall()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            var actor = p.Round.Column.Player;
            Assert.IsTrue(actor.HandBallVisible);
            p.Round.ShootProgrammatic(0f, 0.6f);
            yield return Wait(0.15f);
            Assert.IsFalse(actor.HandBallVisible, "the ball leaves the hand with the shot");
        }

        [UnityTest]
        public IEnumerator FeelOffMidPull_RestoresTheHeroAndCamera()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            p.Input.SimulatePull(AimMapper.FromXPower(0f, 0.9f));
            yield return Wait(0.3f);
            Assert.Greater(p.Round.Column.Player.Lean, 0.5f);
            p.Round.Settings.Feel.Enabled = false;
            yield return null;
            yield return null;
            Assert.AreEqual(0f, p.Round.Column.Player.Lean, 1e-3f);
            Assert.AreEqual(0f, Object.FindAnyObjectByType<RoundCamera>().PullOffset.magnitude, 1e-3f);
            Assert.IsFalse(p.Round.Column.Player.HandBallVisible, "no hand ball without the feel layer");
            p.Round.Settings.Feel.Enabled = true;
        }

        [UnityTest]
        public IEnumerator Miss_StampsKACTI_BlinksTheTopRims_SagsTheCamera()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            p.Miss.Play();
            yield return null;
            Assert.IsTrue(p.Miss.Playing);
            Assert.IsTrue(p.Miss.LetterShown, "the KAÇTI! lettering is up");
            bool anyBlink = false;
            foreach (var h in p.Round.Column.Hoops) if (h.Spec.IsTop) anyBlink |= h.Blinking;
            Assert.IsTrue(anyBlink, "the top rims blink red");
            yield return Wait(0.12f);
            Assert.Greater(-Object.FindAnyObjectByType<RoundCamera>().SagOffset.y, 0.05f, "the camera sags");
            yield return Wait(p.Round.Settings.Feel.MissTime + 0.3f);
            Assert.IsFalse(p.Miss.Playing);
            Assert.IsFalse(p.Miss.LetterShown);
        }

        [UnityTest]
        public IEnumerator Swish_OnlyForABallThatNeverTouchedTheRim()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            Hoop top = null;
            foreach (var h in p.Round.Column.Hoops) if (h.Spec.IsTop) { top = h; break; }
            Assert.IsNotNull(top);
            var ballGo = new GameObject("TestBall", typeof(Rigidbody), typeof(SphereCollider), typeof(Ball));
            var ball = ballGo.GetComponent<Ball>();
            ball.Configure(p.Round.Settings, p.Round.LayerFlight, p.Round.LayerDrop);
            ball.Launch(top.Center + Vector3.up * 2f, Vector3.zero);
            Assert.IsFalse(ball.TouchedRim, "a fresh launch has not touched a rim");
            p.Swish.Pass(top, ball);
            Assert.IsTrue(p.Swish.LastClean);
            Assert.IsTrue(p.Swish.LetterShown, "SWISH! over the hoop");
            ball.MarkRimTouched();
            p.Swish.Pass(top, ball);
            Assert.IsFalse(p.Swish.LastClean, "a rim-in is not a swish");
            ball.Launch(top.Center, Vector3.zero);
            Assert.IsFalse(ball.TouchedRim, "a new launch clears the flag");
            Object.Destroy(ballGo);
        }

        [UnityTest]
        public IEnumerator Count_RollKeepsTheNumberInsideItsWindow()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            yield return null;
            p.Hud.BeginRoll(3);
            yield return Wait(0.3f);
            var text = p.Hud.BasketText.rectTransform;
            var window = (RectTransform)text.parent;
            Assert.IsNotNull(window.GetComponent<UnityEngine.UI.RectMask2D>(), "the number rolls inside a clip window");
            var corners = new Vector3[4];
            text.GetWorldCorners(corners);
            var r = window.rect;
            foreach (var c in corners)
            {
                var local = (Vector2)window.InverseTransformPoint(c);
                Assert.IsTrue(local.x >= r.xMin - 1f && local.x <= r.xMax + 1f && local.y >= r.yMin - 1f && local.y <= r.yMax + 1f,
                    $"a corner of the number sits outside its window: {local} vs {r}");
            }
        }

        [UnityTest]
        public IEnumerator Sky_SurvivesARetry()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            Assert.AreEqual(1, Object.FindObjectsByType<Project.Flow.SkyLife>(FindObjectsSortMode.None).Length);
            p.Round.ResetRound();
            yield return null;
            yield return null;
            yield return null;
            var skies = Object.FindObjectsByType<Project.Flow.SkyLife>(FindObjectsSortMode.None);
            Assert.AreEqual(1, skies.Length, "one sky after the retry");
            Assert.AreEqual(6, skies[0].CloudCount, "its clouds are still there");
        }

        [UnityTest]
        public IEnumerator RoundEnd_StampsTheCountOverTheCage()
        {
            Parts p = null;
            yield return LoadAim(x => p = x);
            p.Count.Stamp(27);
            yield return null;
            Assert.IsTrue(p.Count.Badge.Visible);
            Assert.AreEqual("27 TOP", p.Count.Badge.Label);
            yield return Wait(p.Round.Settings.Feel.StampTime * 0.35f);
            Assert.Greater(p.Count.BadgeScale, 1.5f, "the stamp overshoots");
            yield return Wait(p.Round.Settings.Feel.StampTime);
            Assert.AreEqual(p.Round.Settings.Feel.StampRest, p.Count.BadgeScale, 0.05f);
        }
    }
}
