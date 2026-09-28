using System.Collections;
using NUnit.Framework;
using Project.Basket;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class BasketRoundPlayTests
    {
        private static IEnumerator LoadRound(System.Action<BasketRound> ready)
        {
            SceneManager.LoadScene("Basket");
            yield return null;
            yield return null;
            var round = Object.FindFirstObjectByType<BasketRound>();
            Assert.IsNotNull(round, "BasketRound not found in Basket scene");
            var input = Object.FindFirstObjectByType<ShotInput>();
            if (input != null) input.InputEnabled = false;
            ready(round);
        }

        private static IEnumerator WaitForDone(BasketRound round, float timeout = 45f)
        {
            float t = 0f;
            while (round.Current != BasketRound.State.Done && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MiddleHoop_Power06_BallsScore()
        {
            BasketRound round = null;
            yield return LoadRound(r => round = r);
            round.LoadLevel(1);
            yield return null;
            round.ShootProgrammatic(0f, 0.6f);
            yield return WaitForDone(round);
            Assert.AreEqual(BasketRound.State.Done, round.Current, "round did not finish");
            Assert.GreaterOrEqual(round.TopPasses, 7, $"top passes {round.TopPasses}");
            Assert.LessOrEqual(round.TopPasses, 10, $"top passes {round.TopPasses} (clones must not re-pass)");
            Assert.GreaterOrEqual(round.BasketCount, 8, $"basket {round.BasketCount}");
            Assert.LessOrEqual(round.BasketCount, 60, $"basket {round.BasketCount} (level 1 original max is 32)");
            Assert.IsFalse(round.Missed);
        }

        [UnityTest]
        public IEnumerator BetweenHoops_IsMissed()
        {
            BasketRound round = null;
            yield return LoadRound(r => round = r);
            round.LoadLevel(1);
            yield return null;
            round.ShootProgrammatic(0.7f, 0.6f);
            yield return WaitForDone(round);
            Assert.AreEqual(BasketRound.State.Done, round.Current, "round did not finish");
            Assert.AreEqual(0, round.TopPasses);
            Assert.IsTrue(round.Missed, "missed should be declared");
        }
    }
}
