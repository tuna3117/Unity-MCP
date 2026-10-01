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
    /// <summary>Special taps, births, golden flights, the lock and resets in the Color Hunt scene as saved (Pota look), on staged boards.</summary>
    public class SpecialPlayTests
    {
        /// <summary>
        /// 7×7 colour (2r + c) mod 5 — no two neighbours alike, nothing matches by accident — hp 3 + 7i mod 12, rng 1, all specials on.
        /// The same boards as Task 6's presets (Pota's own renkAvi.ts gave the outcomes during planning).
        /// </summary>
        internal static Board Board7(int[] gates, params (int cell, SpecialKind kind)[] specials) => Board7(null, gates, specials);

        internal static Board Board7(int[] colors, int[] gates, params (int cell, SpecialKind kind)[] specials)
        {
            var cells = new Block[49];
            for (int i = 0; i < 49; i++) cells[i] = new Block(colors != null ? colors[i] : (2 * (i / 7) + i % 7) % 5, 3 + i * 7 % 12);
            foreach (var (cell, kind) in specials) cells[cell] = Block.MakeSpecial(kind);
            var b = new Board
            {
                Size = 7, Cells = cells, Ammo = 158, Targets = new[] { new Target(0, 22), new Target(1, 16) },
                ColorCount = 5, HpMin = 3, HpMax = 14, Rng = 1, Features = Features.All,
            };
            b.Gates.AddRange(gates);
            return b;
        }

        /// <summary>Task 6's Birth grid: tap 44 → a 5-run (golden at 43) and, as column 2 closes up, a 4-run in row 4 (LineV at 29).</summary>
        private static readonly int[] BirthGrid =
        {
            0, 1, 2, 3, 4, 0, 1,
            2, 3, 4, 0, 1, 2, 3,
            4, 0, 1, 2, 2, 4, 1,
            1, 2, 3, 2, 0, 1, 1,
            3, 3, 0, 3, 2, 2, 4,
            0, 1, 0, 3, 2, 0, 1,
            0, 0, 4, 0, 0, 2, 1,
        };

        private static IEnumerator Load(Action<ColorHuntGame> ready)
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            Assert.IsNotNull(game, "ColorHuntGame not found");
            yield return StudioLookPlayTests.WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            game.SetLook(PuzzleLook.Pota);
            game.SetSpecialLook(SpecialLook.Pota);
            ready(game);
        }

        private static void Deal(ColorHuntGame game, Board b)
        {
            game.Stage(b);
            game.Begin();
        }

        private static int FirstNormal(Board b)
        {
            for (int i = 0; i < b.Cells.Length; i++) if (!b.Cells[i].IsSpecial && b.Cells[i].Hp <= b.Ammo) return i;
            return -1;
        }

        private static void AssertViewsShowTheBoard(ColorHuntGame game, string what)
        {
            for (int i = 0; i < game.Board.Cells.Length; i++)
            {
                var v = game.ViewAt(i);
                Assert.IsNotNull(v, $"{what}: no view at {i}");
                Assert.AreEqual(game.Board.Cells[i].Special, v.Block.Special, $"{what}: cell {i}");
                Assert.AreEqual(game.Board.Cells[i].Color, v.Block.Color, $"{what}: cell {i} colour");
            }
        }

        [UnityTest]
        public IEnumerator SpecialTap_BurstsExactlyTheRulesArea_ForFree()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(new int[0], (21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV)));
            var expected = ColorHuntRules.Blast(game.Board, 21).Cells;
            Assert.AreEqual(17, expected.Count, "row 3 + the bomb's 3×3 + column 5");
            var burst = new List<int>();
            game.CellBurst += burst.Add;
            int ammo = game.Board.Ammo;
            Assert.AreEqual("broken", game.Tap(21));
            Assert.IsFalse(game.Salvo.Busy, "a special fires without a salvo");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            game.CellBurst -= burst.Add;
            Assert.IsFalse(game.Busy);
            Assert.GreaterOrEqual(burst.Count, expected.Count);
            CollectionAssert.AreEquivalent(expected, burst.GetRange(0, expected.Count), "the trigger step bursts the rules' area, each cell once");
            Assert.AreEqual(ammo, game.Board.Ammo, "free");
            Assert.AreEqual(ammo, game.DisplayedAmmo);
            Assert.GreaterOrEqual(game.LongestChain, 1, "a special's blast is depth 1");
            AssertViewsShowTheBoard(game, "after the chain");
        }

        [UnityTest]
        public IEnumerator Birth_ShowsTheSpecial_AndItFallsWithItsColumn()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(BirthGrid, new int[0]));
            Assert.AreEqual("broken", game.Tap(44));
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 8f);
            Assert.IsFalse(game.Busy);
            int lines = 0, goldens = 0;
            foreach (var c in game.Board.Cells)
            {
                if (c.Special == SpecialKind.LineV) lines++;
                if (c.Special == SpecialKind.Golden) goldens++;
            }
            Assert.AreEqual(1, lines, "the 4-run in row 4 left a line ball");
            Assert.AreEqual(1, goldens, "the 5-run in row 6 left a golden ball");
            AssertViewsShowTheBoard(game, "after the births");
            CollectionAssert.AreEqual(game.Board.Gates, game.Gates.Shown, "the golden birth opened a gate, and its ring shows");
        }

        [UnityTest]
        public IEnumerator Tap_WhileGoldenFlies_IsLocked()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(new[] { 3, 5 }, (38, SpecialKind.Golden), (47, SpecialKind.Golden)));
            CollectionAssert.AreEqual(new[] { 3, 5 }, game.Gates.Shown);
            Assert.AreEqual("golden", game.Tap(47), "a golden ball leaves only through its gate");
            Assert.IsFalse(game.Busy, "the refusal starts nothing");
            Assert.AreEqual("broken", game.Tap(45), "the block under the other golden ball");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 4f);
            Assert.AreEqual(2, game.GoldenInFlight, "both golden balls of the basket step are in the air");
            Assert.AreEqual(2, game.Specials.LiveCount, "the Pota look draws both");
            Assert.IsTrue(game.Busy);
            Assert.AreEqual("locked", game.Tap(10));
            Assert.IsFalse(game.Rotate(Rotation.Clockwise), "no turn while a golden ball flies");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight == 0, 4f);
            Assert.AreEqual(0, game.GoldenInFlight);
            Assert.AreEqual(2, game.GoldenLanded, "each ball lands once");
            Assert.IsNotNull(game.Hoop, "PuzzleStage builds the giant hoop under the game root");
            Assert.IsTrue(game.Hoop.Swinging, "fileSalla: the net swings on arrival");
            CollectionAssert.IsEmpty(game.Gates.Shown, "each ring closed as its ball passed");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.AreEqual(2, game.GoldenLanded, "nothing landed twice");
            Assert.AreEqual(2, game.Board.Golden);
            Assert.AreEqual("broken", game.Tap(FirstNormal(game.Board)), "unlocked once everything landed");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 10f);
        }

        [UnityTest]
        public IEnumerator Stage_WhileEffectsRun_ClearsEverything()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            var chain = Board7(new int[0], (21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV));
            // Golden balls in the air, then a new board.
            Deal(game, Board7(new[] { 3, 5 }, (38, SpecialKind.Golden), (47, SpecialKind.Golden)));
            Assert.AreEqual("broken", game.Tap(45));
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 4f);
            Assert.Greater(game.Specials.LiveCount, 0);
            Deal(game, chain);
            Assert.AreEqual(0, game.GoldenInFlight, "nothing flies over the new board");
            Assert.AreEqual(0, game.GoldenLanded);
            Assert.AreEqual(0, game.Specials.LiveCount, "the style dropped its balls");
            Assert.IsFalse(game.Busy, "the lock is released at once");
            CollectionAssert.IsEmpty(game.Gates.Shown, "the new board has no gates");
            yield return StudioLookPlayTests.WaitUntil(() => false, 1.5f);         // the old balls would have landed by now
            Assert.AreEqual(0, game.GoldenLanded, "no callback reached the new board");
            // A trigger plan running, then a new board.
            Assert.AreEqual("broken", game.Tap(21));
            yield return null;
            yield return null;
            Assert.IsTrue(game.Busy, "the trigger step runs");
            Deal(game, chain);
            Assert.IsFalse(game.Busy);
            yield return StudioLookPlayTests.WaitUntil(() => false, 1f);
            Assert.AreEqual(SpecialKind.LineH, game.Board.Cells[21].Special, "no fire of the old plan touched the new board");
            Assert.AreEqual("broken", game.Tap(21), "the new board plays");
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            Assert.IsFalse(game.Busy);
            AssertViewsShowTheBoard(game, "after the replay");
        }

        [UnityTest]
        public IEnumerator Rotate_GoldenOnGate_LandsInTheBasket()
        {
            ColorHuntGame game = null;
            yield return Load(g => game = g);
            Deal(game, Board7(new[] { 3 }, (27, SpecialKind.Golden)));              // (3, 6) turns clockwise onto (6, 3), above gate 3
            Assert.IsTrue(game.Rotate(Rotation.Clockwise));
            Assert.AreEqual(1, game.Board.Golden, "dondurVeOtur: the settled board already counts it");
            yield return StudioLookPlayTests.WaitUntil(() => Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity) > 30f, 2f);
            Assert.Greater(Quaternion.Angle(game.BoardRoot.localRotation, Quaternion.identity), 30f, "caught mid-turn");
            Assert.IsTrue(game.Gates.transform.rotation == game.transform.rotation, "the rings stay on the frame");
            CollectionAssert.Contains(game.Gates.Shown, 3, "open until the ball passes");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight > 0, 3f);
            Assert.AreEqual(1, game.GoldenInFlight, "the turn settled into a basket step");
            yield return StudioLookPlayTests.WaitUntil(() => game.GoldenInFlight == 0, 3f);
            Assert.AreEqual(1, game.GoldenLanded);
            yield return StudioLookPlayTests.WaitUntil(() => !game.Busy, 6f);
            CollectionAssert.DoesNotContain(game.Gates.Shown, 3, "closed when its ball passed");
            CollectionAssert.AreEquivalent(game.Board.Gates, game.Gates.Shown, "settled: the rings show the rules' gates");
            Assert.AreEqual(1, game.Rotations);
            AssertViewsShowTheBoard(game, "after the turn");
        }
    }
}
