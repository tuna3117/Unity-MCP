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
    /// The pass ball in the Specials scene as saved (Pota spec 2026-10-11 §3): both looks fly to the rules' target and land; the hit and
    /// a special target's fire come in the same frame (Review Focus 3: "varış = ateş aynı kare"); A lifts and turns the ball's own block,
    /// B hides it and flies the Blender ball with a turning propeller; nothing is left in the air or off the board.
    /// </summary>
    public class PassPlayTests
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

        private static IEnumerator PlayToTheEnd(SpecialsDemo demo, SpecialScenario s, float timeout = 25f)
        {
            bool done = false;
            void Done() => done = true;
            demo.ScenarioFinished += Done;
            demo.Play(s, true);
            yield return StudioLookPlayTests.WaitUntil(() => done, timeout);
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, $"{s} never finished");
        }

        private static void AssertSettled(ColorHuntGame game, string what)
        {
            Assert.IsFalse(game.Busy, what + ": busy");
            Assert.AreEqual(0, game.PassFlights.InFlight, what + ": a flight left in the air");
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsNotNull(v, $"{what}: no view at {i}");
                Assert.IsTrue(v.gameObject.activeSelf, $"{what}: view {i} hidden");
                Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
                Assert.AreEqual(0f, v.Lift, $"{what}: view {i} still lifted");
            }
        }

        [UnityTest]
        public IEnumerator Pass_LandsOnTheRulesTarget([Values] PassLook pass, [Values(SpecialLook.Pota, SpecialLook.Royal)] SpecialLook look)
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            demo.SetLook(look);
            demo.SetPassLook(pass);
            Assert.AreEqual(pass, demo.Game.PassLook);
            StringAssert.Contains(SpecialsDemo.PassTitle(pass), demo.LabelText);
            int landed = 0, cell = -1, target = -1;
            void Landed(int c, int t) { landed++; cell = c; target = t; }
            demo.Game.PassLanded += Landed;
            yield return PlayToTheEnd(demo, SpecialScenario.Pass);
            demo.Game.PassLanded -= Landed;
            Assert.AreEqual(1, landed, "one pass ball, one landing");
            Assert.AreEqual(37, cell, "born at the square's bottom-left");
            Assert.AreEqual(22, target, "Pota's pasHedefi: the nearest colour-2 block");
            AssertSettled(demo.Game, $"{look}/{pass}");
            Assert.AreEqual(5, demo.Game.Board.Targets[0].Remaining, "Pota's trace: 12 → 9 (the square's three 2s) → 5 (the hit 2 and a refill chain of three)");
        }

        [UnityTest]
        public IEnumerator PassBomb_TheBombFiresTheFrameTheBallLands([Values] PassLook pass)
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            demo.SetLook(SpecialLook.Royal);
            demo.SetPassLook(pass);
            int landFrame = -1, bombFrame = -1, launchFrame = -1;
            bool done = false;
            void Landed(int c, int t) => landFrame = Time.frameCount;
            void Fired(SpecialFire f)
            {
                if (f.Kind == SpecialKind.Bomb) bombFrame = Time.frameCount;
                if (f.Kind == SpecialKind.Pass) launchFrame = Time.frameCount;
            }
            void Done() => done = true;
            var game = demo.Game;
            game.PassLanded += Landed;
            game.SpecialFired += Fired;
            demo.ScenarioFinished += Done;
            demo.Play(SpecialScenario.PassBomb, true);
            // The game's own clock: Time.deltaTime clamped at 0.05 (ColorHuntGame.Update), at the game's time scale — a slow editor
            // stretches Time.time but not this. The flight ticks from the frame after its launch up to the landing frame.
            float flight = 0f, waited = 0f;
            while (!done && waited < 25f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                int f = Time.frameCount;
                if (launchFrame >= 0 && f > launchFrame && (landFrame < 0 || f == landFrame)) flight += Mathf.Min(Time.deltaTime, 0.05f);
            }
            game.PassLanded -= Landed;
            game.SpecialFired -= Fired;
            demo.ScenarioFinished -= Done;
            Assert.IsTrue(done, "PassBomb never finished");
            Assert.GreaterOrEqual(landFrame, 0, "the ball landed");
            Assert.AreEqual(landFrame, bombFrame, "the bomb fires the frame the ball lands — never before (altinBomba)");
            var timing = pass == PassLook.A ? PassTiming.A : PassTiming.B;
            Assert.AreEqual(timing.Arrival(7, 29, 19), flight, 0.1f, "the flight lasts pasVarisi on the game's clock (± a frame and the fire's hit-stop)");
            Assert.AreEqual(1, game.Board.BombTarget, "the bomb target counted the bomb once");
            AssertSettled(game, $"passBomb/{pass}");
        }

        [UnityTest]
        public IEnumerator Flight_ALiftsAndTurnsTheBlock_BHidesItAndSpinsThePropeller([Values] PassLook pass)
        {
            SpecialsDemo demo = null;
            yield return Load(d => demo = d);
            demo.SetLook(SpecialLook.Royal);
            demo.SetPassLook(pass);
            demo.Play(SpecialScenario.PassBomb, true);
            var game = demo.Game;
            yield return StudioLookPlayTests.WaitUntil(() => game.PassFlights.InFlight > 0, 10f);
            Assert.AreEqual(1, game.PassFlights.InFlight, "the tap launched the ball");
            float maxLift = 0f, lowAfterTop = float.MaxValue;
            Quaternion? firstProp = null;
            bool propTurned = false, blockHidden = true, modelShown = false;
            while (game.PassFlights.InFlight > 0)
            {
                maxLift = Mathf.Max(maxLift, game.PassFlights.FirstLift);
                if (maxLift > 2f && game.PassFlights.FirstLift >= 0f) lowAfterTop = Mathf.Min(lowAfterTop, game.PassFlights.FirstLift);
                var model = game.BoardRoot.Find("PassBall");
                if (model != null && model.gameObject.activeSelf)
                {
                    modelShown = true;
                    var prop = model.Find("Propeller");
                    if (prop != null)
                    {
                        if (firstProp == null) firstProp = prop.localRotation;
                        else if (Quaternion.Angle(firstProp.Value, prop.localRotation) > 1f) propTurned = true;
                    }
                }
                foreach (var v in game.GetComponentsInChildren<BlockView>(true))
                    if (v.Block.Special == SpecialKind.Pass && v.gameObject.activeSelf && v.Lift > 0f && pass == PassLook.B) blockHidden = false;
                yield return null;
            }
            if (pass == PassLook.A)
            {
                Assert.Greater(maxLift, 0.5f, "A: zipla 0.18 + kavis 0.6 toward the camera at mid-flight");
                Assert.Less(maxLift, 0.18f + 0.6f + 0.01f);
                Assert.IsFalse(modelShown, "A flies the block, not the model");
            }
            else
            {
                Assert.IsTrue(modelShown, "B flies the Blender ball (ColorHuntSettings.PassModel)");
                Assert.IsTrue(propTurned, "the propeller turns about its own axis");
                Assert.IsTrue(blockHidden, "B hides the ball's own block");
                Assert.Greater(maxLift, 1.3f + 1f, "B: yuksel 1.3 + kavis 1.6 at the top of the arc");
                Assert.Less(maxLift, 1.3f + 1.6f + 0.01f);
                Assert.GreaterOrEqual(lowAfterTop, 0.9f - 0.01f, "B: the dive ends on the target's face (temas 0.9), never inside the cube");
            }
            yield return StudioLookPlayTests.WaitUntil(() => demo.Finished, 15f);
            var ball = game.BoardRoot.Find("PassBall");
            Assert.IsTrue(ball == null || !ball.gameObject.activeSelf, "the model is put away at the hit");
        }
    }
}
