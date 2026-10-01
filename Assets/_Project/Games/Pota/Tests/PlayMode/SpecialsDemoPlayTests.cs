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
    /// <summary>The demo scene as saved: every scenario auto-plays to its end in every look; switching and replaying mid-effect is safe.</summary>
    public class SpecialsDemoPlayTests
    {
        private static IEnumerator Load(Action<SpecialsDemo> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene");
            Assert.IsNotNull(demo.Game, "the scene tool links the game");
            Assert.IsFalse(demo.Game.AutoStart, "the demo stages its own boards");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            Assert.AreEqual(ColorHuntGame.State.Playing, demo.Game.Current, "the start scenario was not staged");
            ready(demo);
        }

        private static IEnumerator PlayToTheEnd(SpecialsDemo demo, SpecialScenario s, float timeout = 25f)
        {
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(s, true);
            yield return StudioLookPlayTests.WaitUntil(() => done, timeout);
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, $"{s} never finished");
            Assert.IsTrue(demo.Finished);
            Assert.IsFalse(demo.AutoPlaying);
        }

        private static void AssertViewsShowTheBoard(ColorHuntGame game, string what)
        {
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsNotNull(v, $"{what}: no view at {i}");
                Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
            }
        }

        [UnityTest]
        public IEnumerator Start_StagesTheBirthScenario()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            Assert.AreEqual(SpecialScenario.Birth, demo.Current);
            StringAssert.Contains("Doğuş", demo.LabelText);
            StringAssert.Contains("Pota", demo.LabelText);
        }

        [UnityTest]
        public IEnumerator EveryScenario_AutoPlaysToTheEnd_InEveryLook([Values] SpecialLook look)
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            demo.SetLook(look);                              // Royal/Court fall back to Pota (one warning) until Tasks 10/11
            Assert.AreEqual(look, demo.Game.SpecialLook);
            foreach (SpecialScenario s in Enum.GetValues(typeof(SpecialScenario)))
            {
                yield return PlayToTheEnd(demo, s);
                var game = demo.Game;
                Assert.IsFalse(game.Busy, $"{look}/{s}");
                Assert.AreEqual(0, game.GoldenInFlight, $"{look}/{s}");
                Assert.AreEqual(s == SpecialScenario.Golden ? 2 : 0, game.GoldenLanded, $"{look}/{s}: golden balls in the hoop");
                AssertViewsShowTheBoard(game, $"{look}/{s}");
                StringAssert.Contains(SpecialScenarios.Title(s), demo.LabelText);
            }
        }

        [UnityTest]
        public IEnumerator SwitchingLookAndReplaying_MidScenario_IsSafe()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Pota);
            // A look switch while two golden balls fly: they land at once, the board unlocks.
            demo.Play(SpecialScenario.Golden, true);
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 8f);
            Assert.AreEqual(2, game.GoldenInFlight);
            demo.SetLook(SpecialLook.Royal);
            Assert.AreEqual(0, game.GoldenInFlight, "landed on the switch");
            Assert.AreEqual(2, game.GoldenLanded, "each counted once");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 6f);
            Assert.IsTrue(demo.Finished);
            Assert.AreEqual(2, game.GoldenLanded, "the old style called nothing back");
            // A look switch in the middle of a trigger step.
            demo.SetLook(SpecialLook.Pota);
            demo.Play(SpecialScenario.Chain, true);
            yield return StudioLookPlayTests.WaitUntil(() => game.Busy, 4f);
            demo.SetLook(SpecialLook.Court);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 8f);
            Assert.IsTrue(demo.Finished);
            AssertViewsShowTheBoard(game, "after the switch");
            // "Tekrar" while golden balls fly: a clean board, nothing lands on it.
            demo.SetLook(SpecialLook.Pota);
            demo.Play(SpecialScenario.Golden, true);
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 8f);
            demo.Play(SpecialScenario.Golden, true);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(0, game.GoldenLanded);
            Assert.IsFalse(game.Busy);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 12f);
            Assert.IsTrue(demo.Finished);
            Assert.AreEqual(2, game.GoldenLanded, "the replay lands its own two, no more");
        }

        [UnityTest]
        public IEnumerator Buttons_AreBoundAtRuntime()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            GameObject.Find("LookRoyal").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialLook.Royal, demo.Game.SpecialLook);
            GameObject.Find("LookPota").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialLook.Pota, demo.Game.SpecialLook);
            GameObject.Find("ScenarioBomb").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialScenario.Bomb, demo.Current);
            Assert.IsTrue(demo.AutoPlaying);
            yield return null;
            StringAssert.Contains("Bomba", demo.LabelText);
            GameObject.Find("Replay").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(SpecialScenario.Bomb, demo.Current, "Tekrar replays the current scenario");
            Assert.IsTrue(demo.AutoPlaying);
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 15f);
            Assert.IsTrue(demo.Finished);
        }
    }
}
