using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>
    /// The demo's hand-authored boards (Decision 6): each scripted tap produces its scenario's event. The pinned outcomes are what Pota's
    /// own src/renkAvi.ts gives on these grids with rng 1 (checked while planning). If one fails, suspect the rules port first (Task 2's
    /// fixtures); change a grid only once the port is proven right, and keep the test.
    /// </summary>
    public class SpecialScenariosTests
    {
        private static List<BreakResult> PlayAll(SpecialScenario s, out Board end)
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(s));
            var results = new List<BreakResult>();
            foreach (int cell in SpecialScenarios.Taps(s))
            {
                var r = ColorHuntRules.Break(b, cell);
                Assert.IsTrue(r.Broken, $"{s}: tap {cell} was not accepted ({r.Outcome})");
                results.Add(r);
                b = r.Board;
            }
            end = b;
            return results;
        }

        private static List<string> Births(BreakResult r)
        {
            var list = new List<string>();
            foreach (var step in r.Steps) foreach (var b in step.Born) list.Add($"{b.Cell}:{b.Kind}");
            return list;
        }

        private static List<string> Triggers(Step step)
        {
            var list = new List<string>();
            foreach (var t in step.Triggered) list.Add($"{t.Cell}:{t.Kind}");
            return list;
        }

        private static int[] Range(int from, int count, int stride = 1)
        {
            var a = new int[count];
            for (int i = 0; i < count; i++) a[i] = from + i * stride;
            return a;
        }

        [Test]
        public void EveryPreset_StartsSettled_WithItsSpecialsAndGates([Values] SpecialScenario s)
        {
            var p = SpecialScenarios.Board(s);
            Assert.AreEqual(SpecialScenarios.Size * SpecialScenarios.Size, p.Colors.Length);
            Assert.AreEqual(p.Colors.Length, p.Specials.Length);
            for (int i = 0; i < p.Colors.Length; i++)
                Assert.AreEqual(p.Colors[i] < 0, p.Specials[i] != SpecialKind.None, $"{s}: cell {i}: -1 in the grid ⇔ a special");
            var b = ColorHuntRules.FromPreset(p);
            Assert.IsEmpty(ColorHuntRules.FindMatches(b), $"{s}: a preset starts with no run");
            Assert.IsTrue(b.Features.Line && b.Features.Bomb && b.Features.Golden);
            Assert.AreEqual(1u, b.Rng);
            int goldens = 0;
            foreach (var c in b.Cells) if (c.Special == SpecialKind.Golden) goldens++;
            Assert.LessOrEqual(goldens, b.Gates.Count, $"{s}: every golden ball has a gate");
            foreach (int cell in SpecialScenarios.Taps(s)) Assert.That(cell, Is.InRange(0, 48));
            Assert.IsFalse(string.IsNullOrEmpty(SpecialScenarios.Title(s)));
        }

        [Test]
        public void FromPreset_BuildsThePresetsBoard()
        {
            var p = SpecialScenarios.Board(SpecialScenario.Golden);
            var b = ColorHuntRules.FromPreset(p);
            Assert.AreEqual(7, b.Size);
            Assert.AreEqual(158, b.Ammo);
            Assert.AreEqual(SpecialKind.Golden, b.Cells[38].Special);
            Assert.AreEqual(ColorHuntRules.SpecialColor, b.Cells[38].Color);
            Assert.AreEqual(0, b.Cells[38].Hp);
            Assert.AreEqual(new Block(1, 3 + 1 * 7 % 12), b.Cells[1], "colour from the grid, hp HpMin + 7i mod 12");
            CollectionAssert.AreEqual(new[] { 3, 5 }, b.Gates);
            p.Gates[0] = 6;
            CollectionAssert.AreEqual(new[] { 3, 5 }, b.Gates, "the board keeps its own copy");
            Assert.AreEqual(2, b.Targets.Length);
            Assert.AreEqual(new Target(0, 22), b.Targets[0]);
            Assert.AreEqual(0, b.Golden);
        }

        [Test]
        public void Birth_BearsALineVAGoldenALineHAndABomb()
        {
            var r = PlayAll(SpecialScenario.Birth, out var end);
            Assert.AreEqual(3, r.Count);
            CollectionAssert.AreEquivalent(new[] { "29:LineV", "43:Golden" }, Births(r[0]), "tap 44: the 5-run and, as column 2 closes up, row 4's 4-run");
            var opened = new List<int>();
            foreach (var step in r[0].Steps) opened.AddRange(step.Opened);
            CollectionAssert.AreEqual(new[] { 3 }, opened, "the golden ball opens a gate; Pota's rng (mulberry32, seed 1) picks column 3");
            CollectionAssert.AreEqual(new[] { "48:LineH" }, Births(r[1]), "tap 34: column 6 closes up into a vertical 4-run");
            CollectionAssert.AreEqual(new[] { "32:Bomb" }, Births(r[2]), "tap 32: a T of 2s");
            var kinds = new HashSet<SpecialKind>();
            foreach (var c in end.Cells) if (c.IsSpecial) kinds.Add(c.Special);
            CollectionAssert.AreEquivalent(new[] { SpecialKind.LineV, SpecialKind.LineH, SpecialKind.Bomb, SpecialKind.Golden }, kinds, "all four stay on the board");
        }

        [Test]
        public void Line_FiresTheRowThenTheColumn()
        {
            var r = PlayAll(SpecialScenario.Line, out var end);
            CollectionAssert.AreEqual(new[] { "22:LineH" }, Triggers(r[0].Steps[0]));
            CollectionAssert.AreEqual(Range(21, 7), r[0].Steps[0].Exploded, "row 3");
            CollectionAssert.AreEqual(new[] { "26:LineV" }, Triggers(r[1].Steps[0]), "the column ball fell one row with the row's collapse");
            CollectionAssert.AreEqual(Range(5, 7, 7), r[1].Steps[0].Exploded, "column 5");
            Assert.AreEqual(0, r[0].Cost + r[1].Cost, "free");
        }

        [Test]
        public void Bomb_BurstsItsThreeByThree_AndTheCornerClips()
        {
            var r = PlayAll(SpecialScenario.Bomb, out _);
            CollectionAssert.AreEqual(new[] { 16, 17, 18, 23, 24, 25, 30, 31, 32 }, r[0].Steps[0].Exploded);
            CollectionAssert.AreEqual(new[] { "24:Bomb" }, Triggers(r[0].Steps[0]));
            CollectionAssert.AreEqual(new[] { 40, 41, 47, 48 }, r[1].Steps[0].Exploded, "tekAlan clips at the corner");
        }

        [Test]
        public void Chain_LineSetsOffTheBomb_WhichSetsOffTheColumn()
        {
            var r = PlayAll(SpecialScenario.Chain, out _);
            var step = r[0].Steps[0];
            CollectionAssert.AreEqual(new[] { "19:LineV", "21:LineH", "25:Bomb" }, Triggers(step), "patlamaAlani: sorted by cell");
            CollectionAssert.AreEqual(new[] { 5, 12, 17, 18, 19, 21, 22, 23, 24, 25, 26, 27, 31, 32, 33, 40, 47 }, step.Exploded);
            var plan = SpecialSchedule.Plan(7, step, 21, SpecialTiming.Pota, 0.15f);
            Assert.AreEqual(3, plan.Fires.Count);
            Assert.AreEqual(21, plan.Fires[0].Cell, "the tapped line first");
        }

        [Test]
        public void Golden_BothBallsDropThroughTheirGates()
        {
            var r = PlayAll(SpecialScenario.Golden, out var end);
            Assert.AreEqual(2, r[0].Steps.Count, "the tap, then the basket");
            var basket = r[0].Steps[1];
            Assert.IsEmpty(basket.Exploded, "a pure basket step");
            Assert.AreEqual(2, basket.ToBasket.Count);
            Assert.AreEqual(45, basket.ToBasket[0].Cell, "the ball above the tapped block fell onto gate 3");
            Assert.AreEqual(3, basket.ToBasket[0].Col);
            Assert.AreEqual(47, basket.ToBasket[1].Cell);
            Assert.AreEqual(5, basket.ToBasket[1].Col);
            Assert.AreEqual(2, end.Golden);
            CollectionAssert.IsEmpty(end.Gates, "no other golden ball in those columns: both gates close");
        }

        /// <summary>
        /// Kombo (Pota spec 2026-10-13, Unity Task 7): each combo board's one tap merges its pair into the combo it names — Artı row 3 +
        /// column 3, Kalın artı three rows and three columns around 25, Dev bomba the 5×5 around 24, Pas+Çizgi carries the row ball to 10,
        /// Pas+Bomba (tapped on the BOMB) carries the bomb to 10, Pas+Pas fans three passes to 7, 12, 47. Probed with Pota's own kir first;
        /// every board but Dev bomba settles in its one step (the 5×5's refill matches once, nothing is born).
        /// </summary>
        [TestCase(SpecialScenario.ComboCross, ComboKind.Cross, 24, 25, SpecialKind.None, new int[0], 13, 1)]
        [TestCase(SpecialScenario.ComboThickCross, ComboKind.ThickCross, 25, 26, SpecialKind.None, new int[0], 33, 1)]
        [TestCase(SpecialScenario.ComboBigBomb, ComboKind.BigBomb, 24, 25, SpecialKind.None, new int[0], 25, 2)]
        [TestCase(SpecialScenario.ComboPassLine, ComboKind.PassLine, 29, 30, SpecialKind.LineH, new[] { 10 }, 9, 1)]
        [TestCase(SpecialScenario.ComboPassBomb, ComboKind.PassBomb, 31, 32, SpecialKind.Bomb, new[] { 10 }, 11, 1)]
        [TestCase(SpecialScenario.ComboPassPass, ComboKind.PassPass, 30, 31, SpecialKind.None, new[] { 7, 12, 47 }, 5, 1)]
        public void Combos_TapTheirCombo(SpecialScenario s, ComboKind kind, int center, int partner, SpecialKind carried, int[] targets, int exploded, int steps)
        {
            Assert.IsTrue(SpecialScenarios.IsCombo(s));
            CollectionAssert.AreEqual(new[] { center }, SpecialScenarios.Taps(s), "one tap, on the centre");
            Assert.IsTrue(SpecialScenarios.Board(s).Features.Combo, "Features.Full");
            var r = PlayAll(s, out var end);
            var step = r[0].Steps[0];
            Assert.IsTrue(step.Combo.HasValue, $"{s}: no combo");
            var k = step.Combo.Value;
            Assert.AreEqual(kind, k.Kind);
            Assert.AreEqual(center, k.Center);
            Assert.AreEqual(partner, k.Partner);
            Assert.AreEqual(carried, k.Carried);
            CollectionAssert.AreEqual(targets, k.PassTargets);
            Assert.AreEqual(exploded, step.Exploded.Count, $"{s}: exploded");
            Assert.AreEqual(steps, r[0].Steps.Count, $"{s}: steps");
            foreach (var st in r[0].Steps) Assert.IsEmpty(st.Born, $"{s}: nothing is born");
            Assert.AreEqual(0, r[0].Cost, "free");
            foreach (var c in end.Cells) Assert.IsFalse(c.IsSpecial, $"{s}: both partners are gone");
        }

        [Test]
        public void ComboLean_RestsThreePairs_AndHasNoScript()
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(SpecialScenario.ComboLean));
            Assert.IsTrue(b.Features.Combo);
            var pairs = ColorHuntRules.ComboPairs(b);
            CollectionAssert.AreEquivalent(new[] { 8, 9, 25, 32, 36, 37 }, pairs.Keys);
            Assert.AreEqual(9, pairs[8]);
            Assert.AreEqual(32, pairs[25]);
            Assert.AreEqual(37, pairs[36]);
            Assert.IsEmpty(SpecialScenarios.Taps(SpecialScenario.ComboLean), "the pairs rest and lean");
            Assert.AreEqual(24, SpecialScenarios.Focus(SpecialScenario.ComboLean));
        }

        /// <summary>Tuna 2026-10-14 (Pota Task 12): B Sinematik is the default — in code and in the scene's settings asset — and the lean stays.</summary>
        [Test]
        public void ComboDefaults_AreB_WithTheLeanOn()
        {
            var fresh = ScriptableObject.CreateInstance<ColorHuntSettings>();
            try
            {
                Assert.AreEqual(ComboLook.B, fresh.ComboLook, "code default");
                Assert.IsTrue(fresh.ComboLean);
            }
            finally { Object.DestroyImmediate(fresh); }
            var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");
            Assert.AreEqual(ComboLook.B, s.ComboLook, "ColorHuntSettings.asset");
            Assert.IsTrue(s.ComboLean, "ColorHuntSettings.asset");
        }

        /// <summary>
        /// Tuna 2026-10-14 (Pota Task 12): ALLEY-OOP! and FAST BREAK! sit in the board's lower half, as low as the lettering fits inside the
        /// frame (Pota yaziYeri), so they no longer cover the pass balls' flight; CROSSOVER!, SHOWTIME!, MEGA SLAM! stay 1.2 over the centre.
        /// </summary>
        [Test]
        public void ComboWord_PassCombosSitInTheLowerHalf_AreaCombosOverTheCentre()
        {
            var shared = new ComboShared();
            foreach (int n in new[] { 7, 8, 9 })
            foreach (var at in new[] { new Vector2(0f, 0f), new Vector2(-1f, 2f), new Vector2(2f, -1f) })
            foreach (ComboKind k in System.Enum.GetValues(typeof(ComboKind)))
            {
                float w = CourtSpecials.WordCells(k, shared), h = w * 0.5f;
                var spot = CourtSpecials.LetterSpot(CourtSpecials.WordAt(k, at, n), w, n);
                bool pass = k == ComboKind.PassLine || k == ComboKind.PassBomb || k == ComboKind.PassPass;
                if (!pass) { Assert.AreEqual(CourtSpecials.LetterSpot(at + new Vector2(0f, 1.2f), w, n), spot, $"{k} n {n} at {at}"); continue; }
                Assert.AreEqual(-(n * 0.5f - 0.4f - 0.6f * h), spot.y, 1e-5f, $"{k} n {n}: as low as it fits");
                Assert.Less(spot.y + h * 0.5f, 0f, $"{k} n {n}: the whole word is under the centre line");
                Assert.Greater(spot.y - h * 0.5f, -n * 0.5f, $"{k} n {n}: above the bottom frame");
                Assert.AreEqual(CourtSpecials.LetterSpot(at, w, n).x, spot.x, 1e-6f, "same column as the centre");
            }
        }

        [Test]
        public void IsCombo_IsTheSevenComboScenarios()
        {
            int combos = 0;
            foreach (SpecialScenario s in System.Enum.GetValues(typeof(SpecialScenario))) if (SpecialScenarios.IsCombo(s)) combos++;
            Assert.AreEqual(7, combos);
            Assert.IsFalse(SpecialScenarios.IsCombo(SpecialScenario.PassBomb));
            Assert.IsTrue(SpecialScenarios.IsCombo(SpecialScenario.ComboCross));
            Assert.AreEqual("Pas+Pas", SpecialScenarios.Title(SpecialScenario.ComboPassPass));
        }

        [Test]
        public void Free_HasOneOfEachSpecial_AndNoScript()
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(SpecialScenario.Free));
            Assert.AreEqual(SpecialKind.LineV, b.Cells[8].Special);
            Assert.AreEqual(SpecialKind.Golden, b.Cells[18].Special);
            Assert.AreEqual(SpecialKind.Bomb, b.Cells[24].Special);
            Assert.AreEqual(SpecialKind.LineH, b.Cells[40].Special);
            CollectionAssert.AreEqual(new[] { 4 }, b.Gates, "the golden ball's column");
            Assert.IsEmpty(SpecialScenarios.Taps(SpecialScenario.Free), "Tuna plays it");
        }
    }
}
