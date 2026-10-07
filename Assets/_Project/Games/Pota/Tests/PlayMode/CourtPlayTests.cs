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
    /// <summary>
    /// 2 · Court in the scenes as saved: every demo scenario plays to its end under the budget, a restart mid-dunk leaves nothing,
    /// the golden ball lands once and swings the net once, the lettering stays in the frame and is gone after 0.5 s, a line sweeps
    /// outward in order. Any logged error or exception fails these tests (Unity Test Framework).
    /// </summary>
    public class CourtPlayTests
    {
        private static float Dt => Mathf.Min(Time.deltaTime, 0.05f);        // ColorHuntGame's own step

        private static IEnumerator LoadCourt(Action<ColorHuntGame, CourtSpecials> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            game.SetLook(PuzzleLook.Pota);
            game.SetSpecialLook(SpecialLook.Court);
            var court = game.Specials as CourtSpecials;
            Assert.IsNotNull(court, "the Court look fell back to Pota: run ai_studio_import (Task 9)");
            ready(game, court);
        }

        private static void Deal(ColorHuntGame game, Board b)
        {
            game.Stage(b);
            game.Begin();
        }

        private static Board Preset(SpecialScenario s) => ColorHuntRules.FromPreset(SpecialScenarios.Board(s));

        private static void AssertUnderTheCap(ColorHuntGame game, string what)
        {
            Assert.LessOrEqual(game.Specials.LiveCount, CourtSpecials.Cap, what + ": the Court look's own pieces");
            Assert.LessOrEqual(game.Specials.LiveCount + game.Fx.LiveCount, BurstBudget.Max, what + ": with Pota's bursts");
        }

        [UnityTest]
        public IEnumerator EveryScenario_PlaysToTheEnd_UnderTheCap()
        {
            SceneManager.LoadScene("Specials");
            yield return null;
            var demo = Object.FindAnyObjectByType<SpecialsDemo>();
            Assert.IsNotNull(demo, "run ai_specials_build_scene (Task 6)");
            yield return StudioLookPlayTests.WaitUntil(() => demo.Game.Board != null && demo.Game.Current == ColorHuntGame.State.Playing, 5f);
            demo.SetLook(SpecialLook.Court);
            var game = demo.Game;
            Assert.IsInstanceOf<CourtSpecials>(game.Specials, "no fallback to Pota");
            foreach (SpecialScenario s in Enum.GetValues(typeof(SpecialScenario)))
            {
                bool done = false;
                void Done() => done = true;
                demo.ScenarioFinished += Done;
                demo.Play(s, true);
                int most = 0;
                float t = 0f;
                while (!done && t < 25f)
                {
                    AssertUnderTheCap(game, $"{s} at {t:0.00} s");
                    most = Mathf.Max(most, game.Specials.LiveCount);
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                demo.ScenarioFinished -= Done;
                Assert.IsTrue(done, $"{s} never finished");
                Assert.IsFalse(game.Busy, s.ToString());
                Assert.AreEqual(0, game.GoldenInFlight, s.ToString());
                Assert.AreEqual(s == SpecialScenario.Golden ? 2 : 0, game.GoldenLanded, $"{s}: golden balls in the hoop");
                for (int i = 0; i < game.Board.Cells.Length; i++)
                    Assert.AreEqual(game.Board.Cells[i].Special, game.ViewAt(i).Block.Special, $"{s}: cell {i}");
                if (s != SpecialScenario.Free && s != SpecialScenario.Idle) Assert.Greater(most, 0, $"{s}: the Court look drew");   // no tap: nothing fires
                yield return StudioLookPlayTests.WaitUntil(() => game.Specials.LiveCount == 0, 4f);
                Assert.AreEqual(0, game.Specials.LiveCount, $"{s}: a Court effect never ended");
            }
        }

        [UnityTest]
        public IEnumerator Restart_MidDunk_LeavesNothing()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, Preset(SpecialScenario.Bomb));
            Assert.AreEqual("broken", game.Tap(24));
            yield return StudioLookPlayTests.WaitUntil(() => court.BallsLive > 0, 2f);
            Assert.AreEqual(1, court.BallsLive, "the dunk's ball is in the air");
            Assert.IsTrue(game.Busy);
            Deal(game, Preset(SpecialScenario.Bomb));                          // the demo's "Tekrar"
            Assert.AreEqual(0, court.LiveCount, "Clear dropped the ball, the quads and the particles");
            Assert.AreEqual(0, court.BallsLive);
            Assert.IsFalse(game.Busy, "the lock is released at once");
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);      // the old impact, SLAM! and flashbulbs would have come by now
            Assert.AreEqual(0, court.LiveCount, "nothing of the old dunk came back");
            Assert.AreEqual(0, court.LettersLive);
            for (int i = 0; i < game.Board.Cells.Length; i++)
                if (!game.Board.Cells[i].IsSpecial) Assert.AreEqual(0f, game.ViewAt(i).Bob, $"cell {i} kept a hop");
            Assert.AreEqual(SpecialKind.Bomb, game.Board.Cells[24].Special, "the old plan touched nothing");
            Assert.AreEqual("broken", game.Tap(24), "the new board plays");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.IsFalse(game.Busy);
        }

        [UnityTest]
        public IEnumerator Golden_LandsOnce_AndSwingsTheNetOnce()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, SpecialPlayTests.Board7(new[] { 3 }, (38, SpecialKind.Golden)));
            Assert.AreEqual("broken", game.Tap(45), "the block under the golden ball");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 4f);
            Assert.AreEqual(1, game.GoldenInFlight);
            Assert.AreEqual(1, court.BallsLive, "the Court look draws the golden ball");
            Assert.AreEqual(0, court.Landings);
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight == 0, 4f);
            Assert.AreEqual(1, game.GoldenLanded);
            Assert.AreEqual(1, court.Landings);
            Assert.AreEqual(1, court.NetSwings);
            Assert.IsNotNull(game.Hoop);
            Assert.IsTrue(game.Hoop.Swinging, "fileSalla");
            Assert.AreEqual(1, court.LettersLive, "SWISH!");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);
            Assert.AreEqual(1, game.GoldenLanded, "nothing landed twice");
            Assert.AreEqual(1, court.Landings);
            Assert.AreEqual(1, court.NetSwings, "the net swung once");
            Assert.AreEqual(1, game.Board.Golden);
            foreach (Transform ring in game.Hoop.Net) Assert.AreEqual(Vector3.one, ring.localScale, $"{ring.name}: the net wave settled");
        }

        [UnityTest]
        public IEnumerator GoldenVolley_ShowsOneSwish_ThatPopsAgain()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, SpecialPlayTests.Board7(new[] { 3, 5 }, (38, SpecialKind.Golden), (47, SpecialKind.Golden)));
            Assert.AreEqual("broken", game.Tap(45), "the block under the other golden ball: both drop in one basket step");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 4f);
            Assert.AreEqual(2, game.GoldenInFlight);
            int most = 0;
            float clock = 0f;
            while ((game.GoldenInFlight > 0 || court.LettersLive > 0) && clock < 4f)
            {
                most = Mathf.Max(most, court.LettersLive);
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(2, court.Landings, "both balls landed");
            Assert.AreEqual(1, most, "the second landing pops the SWISH! that is still up, never a second copy over it");
            Assert.AreEqual(0, court.LettersLive);
        }

        [UnityTest]
        public IEnumerator SecondDunk_MovesTheSlamLettering_ToItsOwnSpot()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, SpecialPlayTests.Board7(new int[0], (24, SpecialKind.Bomb), (25, SpecialKind.Bomb)));
            Assert.AreEqual("broken", game.Tap(24), "two bombs, 25 inside 24's 3×3: a chain of two dunks");

            int n = game.Board.Size;
            float w = game.S.Court.SlamLetterCells, h = w * 0.5f;
            float mx = Mathf.Max(0f, n * 0.5f - 0.4f - 0.6f * w), my = Mathf.Max(0f, n * 0.5f - 0.4f - 0.6f * h);
            Vector3 SlamSpot(int cell)
            {
                var cellPos = BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, cell), ColorHuntRules.Col(n, cell));
                var raw = cellPos + new Vector2(0f, 1.1f);
                var clamped = new Vector2(Mathf.Clamp(raw.x, -mx, mx), Mathf.Clamp(raw.y, -my, my));
                return game.BoardRoot.TransformPoint(new Vector3(clamped.x, clamped.y, -CourtSpecials.LetterZ));
            }
            var firstSpot = SlamSpot(24);
            var secondSpot = SlamSpot(25);

            bool sawBothAtOnce = false, sawTheSecondSpot = false;
            float clock = 0f;
            while (game.Busy && clock < 6f)
            {
                bool atFirst = false, atSecond = false;
                foreach (var t in court.Letters)
                {
                    if (Vector3.Distance(t.position, firstSpot) < 0.05f) atFirst = true;
                    if (Vector3.Distance(t.position, secondSpot) < 0.05f) atSecond = true;
                }
                if (atFirst && atSecond) sawBothAtOnce = true;
                if (atSecond) sawTheSecondSpot = true;
                clock += Dt;
                yield return null;
            }
            Assert.IsFalse(sawBothAtOnce, "never a SLAM! over both bombs at once (a stacked second copy)");
            Assert.IsTrue(sawTheSecondSpot, "the re-popped SLAM! moved to the second bomb's spot after its own impact");
        }

        [UnityTest]
        public IEnumerator Slam_LetteringIsInTheFrame_AndGoneAfterHalfASecond()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, Preset(SpecialScenario.Bomb));
            Assert.AreEqual("broken", game.Tap(24));
            yield return StudioLookPlayTests.WaitUntil(() => court.LettersLive > 0, 3f);
            Assert.AreEqual(1, court.LettersLive, "SLAM!");
            float age = 0f;
            bool looked = false;
            while (court.LettersLive > 0 && age < 1f)
            {
                AssertUnderTheCap(game, $"SLAM! at {age:0.00} s");
                if (!looked && age >= 0.1f)                                      // the 1.15 pop: the biggest it gets
                {
                    looked = true;
                    foreach (var q in court.Letters)
                        foreach (var corner in new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) })
                        {
                            var v = game.Cam.WorldToViewportPoint(q.TransformPoint(corner));
                            Assert.Greater(v.z, 0f, "in front of the camera");
                            Assert.That(v.x, Is.InRange(0f, 1f), $"{q.name}: not cut at the side");
                            Assert.That(v.y, Is.InRange(0f, 1f), $"{q.name}: not cut at the top or bottom");
                        }
                }
                age += Dt;
                yield return null;
            }
            Assert.IsTrue(looked, "the lettering lived long enough to be measured");
            Assert.AreEqual(0, court.LettersLive);
            Assert.That(age, Is.InRange(0.45f, 0.58f), "the lettering lives 0.5 s of game time");
        }

        [UnityTest]
        public IEnumerator Birth_GatheredBlocksHopInFrontOfTheBoard_AndLandFlat()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, Preset(SpecialScenario.Birth));
            Assert.AreEqual("broken", game.Tap(SpecialScenarios.Taps(SpecialScenario.Birth)[0]));
            float nearest = 0f, clock = 0f;
            while (game.Busy && clock < 8f)
            {
                foreach (var v in game.BoardRoot.GetComponentsInChildren<BlockView>(false))
                    nearest = Mathf.Min(nearest, v.transform.localPosition.z);
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsFalse(game.Busy);
            Assert.Less(nearest, -0.5f * game.S.Court.GatherLift, "a bounce pass lifts toward the camera, over the blocks it crosses");
            for (int i = 0; i < game.Board.Cells.Length; i++)
                Assert.AreEqual(0f, game.ViewAt(i).transform.localPosition.z, 1e-5f, $"cell {i}: back on the board plane");
        }

        private static void AssertRingsOnScreen(ColorHuntGame game, CourtSpecials court, string what)
        {
            foreach (var ring in court.Rings)
                foreach (var corner in new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) })
                {
                    var v = game.Cam.WorldToViewportPoint(ring.TransformPoint(corner));
                    Assert.Greater(v.z, 0f, $"{what}: in front of the camera");
                    Assert.That(v.x, Is.InRange(0f, 1f), $"{what}: not cut at the left or right");
                    Assert.That(v.y, Is.InRange(0f, 1f), $"{what}: not cut at the top or bottom");
                }
        }

        [UnityTest]
        public IEnumerator SwishRing_StaysInTheFrame_AtBothEndsOfARowAndAColumn()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });

            Deal(game, Preset(SpecialScenario.Line));
            Assert.AreEqual("broken", game.Tap(22), "LineH at (3, 1): a short run left, a long run right");
            float clock = 0f;
            while (game.Busy && clock < 6f)
            {
                AssertRingsOnScreen(game, court, $"row, {clock:0.00} s");
                clock += Dt;
                yield return null;
            }
            Assert.IsFalse(game.Busy);

            Deal(game, Preset(SpecialScenario.Line));
            Assert.AreEqual("broken", game.Tap(19), "LineV at (2, 5): a short run up, a long run down");
            clock = 0f;
            while (game.Busy && clock < 6f)
            {
                AssertRingsOnScreen(game, court, $"column, {clock:0.00} s");
                clock += Dt;
                yield return null;
            }
            Assert.IsFalse(game.Busy);
        }

        [UnityTest]
        public IEnumerator Line_TwoBallsSweepTheRow_AndItBurstsOutwardInOrder()
        {
            ColorHuntGame game = null;
            CourtSpecials court = null;
            yield return LoadCourt((g, c) => { game = g; court = c; });
            Deal(game, Preset(SpecialScenario.Line));
            var at = new Dictionary<int, float>();
            float clock = 0f;
            void Burst(int c) { if (!at.ContainsKey(c)) at[c] = clock; }
            game.CellBurst += Burst;
            Assert.AreEqual("broken", game.Tap(22), "LineH at (3, 1)");
            int balls = 0;
            while (game.Busy && clock < 6f)
            {
                AssertUnderTheCap(game, $"line at {clock:0.00} s");
                balls = Mathf.Max(balls, court.BallsLive);
                clock += Dt;
                yield return null;
            }
            game.CellBurst -= Burst;
            Assert.AreEqual(2, balls, "two balls, one to each end");
            for (int c = 21; c <= 27; c++) Assert.IsTrue(at.ContainsKey(c), $"cell {c} burst");
            Assert.Less(at[23], at[25], "outward: (3, 2) before (3, 4)");
            Assert.Less(at[25], at[27], "(3, 4) before (3, 6)");
            Assert.AreEqual(at[21], at[23], 0.06f, "one cell away on both sides: together");
            Assert.LessOrEqual(at[22], at[21], "the line ball goes first");
        }

        [UnityTest]
        public IEnumerator Idle_WaitsForTheStudioIntro()
        {
            ColorHuntGame game = null;
            yield return LoadCourt((g, c) => game = g);
            game.SetLook(PuzzleLook.Candy);                                    // a studio look: its board pops in row by row first
            game.Stage(Preset(SpecialScenario.Line));
            game.StartIntro();                                                 // NewRound's order: deal, intro, begin
            game.Begin();
            Assert.IsTrue(game.Intro, "the studio intro runs");
            Assert.AreEqual(ColorHuntGame.State.Playing, game.Current, "in play while the board pops in");
            var lines = new[] { game.ViewAt(22), game.ViewAt(19) };
            int frames = 0;
            float clock = 0f;
            while (game.Intro && clock < 3f)
            {
                foreach (var v in lines)
                {
                    Assert.AreEqual(0f, v.Bob, $"cell {v.Cell}: no dribble while the board pops in (Royal waits too)");
                    Assert.AreEqual(0f, v.Squash, $"cell {v.Cell}: no dribble squash during the intro");
                }
                frames++;
                clock += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsFalse(game.Intro);
            Assert.Greater(frames, 1, "the intro was watched");
            float most = 0f;
            yield return StudioLookPlayTests.WaitUntil(() =>
            {
                foreach (var v in lines) most = Mathf.Max(most, v.Bob);
                return most > 0f;
            }, 2f);
            Assert.Greater(most, 0f, "the specials dribble once the intro is over");
        }
    }
}
