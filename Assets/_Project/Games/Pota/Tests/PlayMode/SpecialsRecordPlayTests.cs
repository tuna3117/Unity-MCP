using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>What ai_colorhunt_record listens to in the Specials scene: the demo's taps, births, fires and the sound log (Task 12).</summary>
    public class SpecialsRecordPlayTests
    {
        private static IEnumerator Load(Action<SpecialsDemo> ready)
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "SpecialsDemo not found: run ai_specials_build_scene");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            ready(demo);
        }

        private static IEnumerator Run(SpecialsDemo demo, SpecialScenario s)
        {
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(s, true);
            yield return StudioLookPlayTests.WaitUntil(() => done, 20f);
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, $"{s} never finished");
        }

        [UnityTest]
        public IEnumerator Bomb_RaisesTapsAndFires_AndLogsTheBombSoundOnTheVoices()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Pota);
            var taps = new List<int>();
            var fires = new List<SpecialFire>();
            demo.Tapped += taps.Add;
            game.SpecialFired += fires.Add;
            var sounds = game.Juice.Sounds;
            sounds.StartLog(() => Time.unscaledTime);
            yield return Run(demo, SpecialScenario.Bomb);
            var log = sounds.StopLog();
            demo.Tapped -= taps.Add;
            game.SpecialFired -= fires.Add;
            CollectionAssert.AreEqual(SpecialScenarios.Taps(SpecialScenario.Bomb), taps);
            Assert.AreEqual(2, fires.Count, "the middle bomb, then the corner one");
            Assert.AreEqual(24, fires[0].Cell);
            Assert.AreEqual(48, fires[1].Cell);
            Assert.AreEqual(SpecialKind.Bomb, fires[1].Kind);
            var bombs = log.FindAll(e => e.Id == "patlama");
            Assert.AreEqual(2, bombs.Count, "Pota's bomba sample, once per bomb");
            Assert.AreSame(game.S.SfxBomb, bombs[0].Clip);
            for (int i = 0; i < log.Count; i++)
            {
                Assert.That(log[i].Voice, Is.InRange(0, 7), log[i].Id);
                if (i == 0) continue;
                Assert.AreEqual((log[i - 1].Voice + 1) % 8, log[i].Voice, "round-robin voices, as the mix emulates them");
                Assert.GreaterOrEqual(log[i].T, log[i - 1].T, "in time order");
            }
        }

        [UnityTest]
        public IEnumerator Birth_RaisesSpecialBorn_ForEachNewBall()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var game = demo.Game;
            demo.SetLook(SpecialLook.Pota);
            var born = new List<string>();
            void Born(SpecialKind kind, int cell) => born.Add($"{cell}:{kind}");
            game.SpecialBorn += Born;
            yield return Run(demo, SpecialScenario.Birth);
            game.SpecialBorn -= Born;
            CollectionAssert.AreEquivalent(new[] { "29:LineV", "43:Golden", "48:LineH", "32:Bomb" }, born, "SpecialScenariosTests' births");
        }

        [UnityTest]
        public IEnumerator ShowUi_HidesTheDemoRows_ForTheRecorder()
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            var canvas = GameObject.Find("SpecialsDemoUI").GetComponent<Canvas>();
            demo.ShowUi(false);
            Assert.IsFalse(canvas.enabled);
            Assert.IsFalse(demo.UiVisible);
            demo.ShowUi(true);
            Assert.IsTrue(canvas.enabled);
            Assert.IsTrue(demo.UiVisible);
        }
    }
}
