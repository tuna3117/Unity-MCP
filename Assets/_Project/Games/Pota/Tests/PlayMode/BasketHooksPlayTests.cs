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
    public class BasketHooksPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator LoadAim(Action<BasketRound> ready)
        {
            SceneManager.LoadScene("Basket");
            yield return null;
            var round = Object.FindAnyObjectByType<BasketRound>();
            Assert.IsNotNull(round, "BasketRound missing");
            yield return WaitUntil(() => round.Column != null && round.Current == BasketRound.State.Aim, 5f);
            float until = Time.realtimeSinceStartup + 1f;          // let the camera settle on the aim pose (time, not frames)
            while (Time.realtimeSinceStartup < until) yield return null;
            ready(round);
        }

        [UnityTest]
        public IEnumerator RoundCamera_ResumesFromWhereTheCameraIs()
        {
            BasketRound round = null;
            yield return LoadAim(r => round = r);
            var rc = Object.FindAnyObjectByType<RoundCamera>();
            Assert.Less(Vector3.Distance(rc.LookPoint, round.Settings.AimLookAt), 0.5f, "LookPoint must be the aim look-at");

            rc.enabled = false;
            var far = new Vector3(40f, 30f, 60f);
            rc.transform.position = far;
            rc.enabled = true;
            yield return null;
            float fromFar = Vector3.Distance(rc.transform.position, far);
            float aimToFar = Vector3.Distance(round.Settings.AimCamPos, far);
            Assert.Less(fromFar, aimToFar * 0.5f, "camera jumped to a stale pose instead of blending from where it was");
        }

        [UnityTest]
        public IEnumerator PlayerReveal_ShowsTheHiddenPlayerAndStage()
        {
            BasketRound round = null;
            yield return LoadAim(r => round = r);
            var actor = round.Column.Player;
            Assert.IsNotNull(actor, "no player");
            actor.gameObject.SetActive(false);
            if (actor.AlsoHide != null) foreach (var go in actor.AlsoHide) if (go != null) go.SetActive(false);
            actor.Reveal();
            Assert.IsTrue(actor.gameObject.activeSelf);
            if (actor.AlsoHide != null) foreach (var go in actor.AlsoHide) if (go != null) Assert.IsTrue(go.activeSelf, go.name);
        }
    }
}
