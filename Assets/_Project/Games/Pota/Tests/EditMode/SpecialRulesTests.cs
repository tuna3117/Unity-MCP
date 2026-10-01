using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;

namespace Project.Tests
{
    /// <summary>
    /// Pota's special balls, gates and basket (src/renkAvi.ts), hand cases. Boards and expected values are Pota's own
    /// (arac/renk-avi-test.ts) where Pota has the case; the rest were read from Pota's rules on a scratch copy.
    /// </summary>
    public class SpecialRulesTests
    {
        /// <summary>Pota renk-avi-test `tahta`: a digit is a colour (hp 1); C = LineV (cizgiDikey), Y = LineH, B = bomb, A = golden.
        /// Ammo 100, no targets, 5 colours, hp 1–1, rng 1, every feature on, no gates.</summary>
        private static Board Grid(params string[] rows) => Grid(Features.All, rows);

        private static Board Grid(Features features, params string[] rows)
        {
            int n = rows.Length;
            var cells = new Block[n * n];
            for (int r = 0; r < n; r++)
            {
                Assert.AreEqual(n, rows[r].Length, $"row {r} is not {n} long");
                for (int c = 0; c < n; c++)
                {
                    char ch = rows[r][c];
                    cells[r * n + c] = ch == 'C' ? Block.MakeSpecial(SpecialKind.LineV)
                                     : ch == 'Y' ? Block.MakeSpecial(SpecialKind.LineH)
                                     : ch == 'B' ? Block.MakeSpecial(SpecialKind.Bomb)
                                     : ch == 'A' ? Block.MakeSpecial(SpecialKind.Golden)
                                     : new Block(ch - '0', 1);
                }
            }
            return new Board { Size = n, Cells = cells, Ammo = 100, Targets = new Target[0], ColorCount = 5, HpMin = 1, HpMax = 1, Rng = 1, Features = features };
        }

        private static Board With(Board b, int colorCount, uint rng, params int[] gates)
        {
            b.ColorCount = colorCount;
            b.Rng = rng;
            b.Gates = new List<int>(gates);
            return b;
        }

        private static string Births(List<Born> born)
        {
            var parts = new List<string>();
            foreach (var x in born) parts.Add($"{x.Cell}:{x.Kind}");
            return string.Join(" ", parts);
        }

        private static string Fires(List<Trigger> fired)
        {
            var parts = new List<string>();
            foreach (var x in fired) parts.Add($"{x.Cell}:{x.Kind}");
            return string.Join(" ", parts);
        }

        private static int CountSpecial(Board b, SpecialKind kind)
        {
            int n = 0;
            foreach (var c in b.Cells) if (c.Special == kind) n++;
            return n;
        }

        // ---------- data ----------

        [Test]
        public void SpecialBlock_HasColourMinusOne_HpZero()
        {
            var s = Block.MakeSpecial(SpecialKind.Bomb);
            Assert.AreEqual((ColorHuntRules.SpecialColor, 0, SpecialKind.Bomb, true), (s.Color, s.Hp, s.Special, s.IsSpecial));
            var plain = new Block(2, 7);
            Assert.AreEqual((SpecialKind.None, false), (plain.Special, plain.IsSpecial));
            Assert.IsFalse(Features.None.Any);
            Assert.IsTrue(Features.All.Line && Features.All.Bomb && Features.All.Golden);
        }

        [Test]
        public void Create_TakesTheDesignsFeatures_NoGates_AndCloneCopiesGates()
        {
            var design = new LevelDesign { Features = Features.All };
            var b = ColorHuntRules.Create(design, 158, 7);
            Assert.AreEqual(Features.All, b.Features);
            Assert.AreEqual(0, b.Gates.Count);
            Assert.AreEqual(0, b.Golden);
            Assert.AreEqual(Features.None, ColorHuntRules.Create(new LevelDesign(), 158, 7).Features, "the shipped level: off");
            b.Gates.Add(3);
            var copy = b.Clone();
            copy.Gates.Add(5);
            CollectionAssert.AreEqual(new[] { 3 }, b.Gates, "the clone's gates are its own");
            CollectionAssert.AreEqual(new[] { 3, 5 }, copy.Gates);
        }

        // ---------- runs ----------

        [Test]
        public void Specials_NeverJoinARun_AndBreakTheRunTheySitIn()
        {
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("AAA", "012", "234")).Count, "three specials side by side");
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("A12", "A34", "A56")).Count, "three specials stacked");
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("0A00", "1231", "2312", "3123")).Count, "a special splits 0 _ 0 0");
            Assert.AreEqual(0, ColorHuntRules.FindMatches(Grid("CYB", "012", "234")).Count, "mixed specials");
        }

        // ---------- birth (eslesmeleriCoz) ----------

        [Test]
        public void HorizontalFour_BornLineV_AtTheLeftSecondCell()
        {
            var exploded = ColorHuntRules.ResolveMatches(Grid("00001", "12342", "23453", "34564", "45670"), out var born);
            Assert.AreEqual("1:LineV", Births(born));
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, exploded, "the birth cell does not explode");
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, born[0].Group);
        }

        [Test]
        public void VerticalFour_BornLineH_AtTheBottomCell()
        {
            var exploded = ColorHuntRules.ResolveMatches(Grid("01234", "02345", "03456", "04567", "15678"), out var born);
            Assert.AreEqual("15:LineH", Births(born));
            CollectionAssert.AreEqual(new[] { 0, 5, 10 }, exploded);
        }

        [Test]
        public void FiveOrMore_BornGolden_TheWholeGroupExplodes()
        {
            ColorHuntRules.ResolveMatches(Grid("00000", "12342", "23453", "34564", "45670"), out var five);
            Assert.AreEqual("1:Golden", Births(five), "horizontal 5");
            ColorHuntRules.ResolveMatches(Grid("012340", "123450", "234560", "345670", "456710", "567120"), out var six);
            Assert.AreEqual("35:Golden", Births(six), "vertical 6: the bottom cell");
            var arm = ColorHuntRules.ResolveMatches(Grid("00000", "02345", "03456", "34567", "45678"), out var withArm);
            Assert.AreEqual("1:Golden", Births(withArm), "5 + an arm of 3: golden, no bomb");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 10 }, arm, "the arm explodes too");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 10 }, withArm[0].Group);
            var four = ColorHuntRules.ResolveMatches(Grid("00000", "02345", "03456", "04567", "45678"), out var withFour);
            Assert.AreEqual("1:Golden", Births(withFour), "5 + an arm of 4: one golden, no line");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4, 5, 10, 15 }, four);
            var other = ColorHuntRules.ResolveMatches(Grid("12345", "23405", "34506", "45607", "00000"), out var otherWay);
            Assert.AreEqual("21:Golden", Births(otherWay));
            CollectionAssert.AreEqual(new[] { 8, 13, 18, 20, 22, 23, 24 }, other);
            var plus = ColorHuntRules.ResolveMatches(Grid("12034", "56078", "00000", "91023", "27045"), out var cross);
            Assert.AreEqual("11:Golden", Births(cross), "two crossing 5s: one golden, the row is scanned first");
            CollectionAssert.AreEqual(new[] { 2, 7, 10, 12, 13, 14, 17, 22 }, plus);
        }

        [Test]
        public void LAndT_BornBomb_AtTheSmallestIntersection()
        {
            List<Born> born;
            ColorHuntRules.ResolveMatches(Grid("01234", "02345", "00056", "34567", "45678"), out born);
            Assert.AreEqual("10:Bomb", Births(born), "L bottom-left");
            ColorHuntRules.ResolveMatches(Grid("00012", "02345", "03456", "34567", "45678"), out born);
            Assert.AreEqual("0:Bomb", Births(born), "L top-left");
            ColorHuntRules.ResolveMatches(Grid("21000", "23450", "34560", "34567", "45678"), out born);
            Assert.AreEqual("4:Bomb", Births(born), "L top-right");
            ColorHuntRules.ResolveMatches(Grid("12340", "23450", "34000", "34567", "45678"), out born);
            Assert.AreEqual("14:Bomb", Births(born), "L bottom-right");
            ColorHuntRules.ResolveMatches(Grid("00012", "20345", "30456", "34567", "45678"), out born);
            Assert.AreEqual("1:Bomb", Births(born), "T");
            var u = ColorHuntRules.ResolveMatches(Grid("12345", "67891", "02034", "05067", "00089"), out born);
            Assert.AreEqual("20:Bomb", Births(born), "U: one bomb at the smaller of two intersections");
            CollectionAssert.AreEqual(new[] { 10, 12, 15, 17, 21, 22 }, u);
            var shared = ColorHuntRules.ResolveMatches(Grid("10234", "20345", "30456", "00007", "45678"), out born);
            Assert.AreEqual("16:Bomb", Births(born), "two 4s whose birth cells coincide: one bomb");
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, shared);
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, born[0].Group);
        }

        [Test]
        public void ClosedFeature_SkipsItsStep()
        {
            var lineOnly = new Features { Line = true };
            var none = ColorHuntRules.ResolveMatches(Grid(Features.None, "00001", "12342", "23453", "34564", "45670"), out var born);
            Assert.AreEqual(0, born.Count, "line off: no birth");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, none);
            ColorHuntRules.ResolveMatches(Grid(lineOnly, "01234", "02345", "00056", "34567", "45678"), out born);
            Assert.AreEqual(0, born.Count, "bomb off, an L of two 3s: both explode plainly");
            var arm = ColorHuntRules.ResolveMatches(Grid(lineOnly, "00001", "02345", "03456", "34567", "45678"), out born);
            Assert.AreEqual("1:LineV", Births(born), "bomb off, an L whose arm is a 4: the 4 still makes a line");
            CollectionAssert.AreEqual(new[] { 0, 2, 3, 5, 10 }, arm);
            var five = ColorHuntRules.ResolveMatches(Grid(lineOnly, "00000", "12342", "23453", "34564", "45670"), out born);
            Assert.AreEqual(0, born.Count, "golden off: a 5 is not a 4");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, five);
            ColorHuntRules.ResolveMatches(Grid(new Features { Golden = true }, "00001", "12342", "23453", "34564", "45670"), out born);
            Assert.AreEqual(0, born.Count, "line off, golden on: a 4 makes nothing");
        }

        [Test]
        public void CrossingFours_WithTheBombClosed_BornTwoLines_WithSeparateGroups()
        {
            var lineOnly = new Features { Line = true };
            var exploded = ColorHuntRules.ResolveMatches(Grid(lineOnly, "12304", "23401", "34102", "00003", "41234"), out var born);
            Assert.AreEqual("16:LineV 18:LineH", Births(born));
            CollectionAssert.AreEqual(new[] { 3, 8, 13, 15, 17 }, exploded);
            CollectionAssert.AreEqual(new[] { 15, 17 }, born[0].Group);
            CollectionAssert.AreEqual(new[] { 3, 8, 13 }, born[1].Group);
            ColorHuntRules.ResolveMatches(Grid("12304", "23401", "34102", "00003", "41234"), out born);
            Assert.AreEqual("18:Bomb", Births(born), "bomb on: one bomb at the intersection");
            CollectionAssert.AreEqual(new[] { 3, 8, 13, 15, 16, 17 }, born[0].Group);
            var same = ColorHuntRules.ResolveMatches(Grid(lineOnly, "10234", "20345", "30456", "00007", "45678"), out born);
            Assert.AreEqual("16:LineH", Births(born), "the same birth cell from two lines: the column run (scanned last) wins");
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, same);
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 15, 17, 18 }, born[0].Group, "the look gathers both runs");
        }

        [Test]
        public void BornSpecial_TakesItsCell_AndTheChainGoesOn()
        {
            var b = Grid("0201", "2522", "6767", "0123");
            b.Ammo = 10;
            b.Targets = new[] { new Target(2, 9) };
            With(b, 9, 3);
            var r = ColorHuntRules.Break(b, 5);
            Assert.AreEqual(BreakOutcome.Broken, r.Outcome);
            Assert.AreEqual(2, r.Steps.Count, "the tap and the birth");
            Assert.AreEqual("5:LineV", Births(r.Steps[1].Born));
            CollectionAssert.AreEqual(new[] { 4, 6, 7 }, r.Steps[1].Exploded);
            Assert.AreEqual(3, r.Steps[1].Spawns.Count, "as many new blocks as exploded (the special keeps its cell)");
            Assert.AreEqual(SpecialKind.LineV, r.Board.Cells[5].Special);
            Assert.AreEqual(6, r.Board.Targets[0].Remaining, "the three 2s count, the special does not");
            Assert.AreEqual(9, r.Board.Ammo);
        }

        [Test]
        public void BornSpecial_FallsWhenItsColumnEmpties()
        {
            var steps = new List<Step>();
            var after = ColorHuntRules.ResolveChains(Grid("00012", "02345", "03456", "34567", "45678"), steps, 1);
            Assert.AreEqual("0:Bomb", Births(steps[0].Born), "born at its old place (cell 0)");
            Assert.AreEqual(SpecialKind.Bomb, after.Cells[10].Special, "it fell to the bottom of what was left of the column");
        }

        // ---------- firing (tekAlan, patlamaAlani, kir) ----------

        [Test]
        public void SingleArea_LinesAndBomb_ClippedAtTheEdges()
        {
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 16, 21 }, ColorHuntRules.SingleArea(5, 11, SpecialKind.LineV));
            CollectionAssert.AreEqual(new[] { 10, 11, 12, 13, 14 }, ColorHuntRules.SingleArea(5, 11, SpecialKind.LineH));
            CollectionAssert.AreEqual(new[] { 6, 7, 8, 11, 12, 13, 16, 17, 18 }, ColorHuntRules.SingleArea(5, 12, SpecialKind.Bomb));
            CollectionAssert.AreEqual(new[] { 0, 1, 5, 6 }, ColorHuntRules.SingleArea(5, 0, SpecialKind.Bomb));
            CollectionAssert.AreEqual(new[] { 18, 19, 23, 24 }, ColorHuntRules.SingleArea(5, 24, SpecialKind.Bomb));
            CollectionAssert.AreEqual(new[] { 37, 38, 39, 44, 45, 46 }, ColorHuntRules.SingleArea(7, 45, SpecialKind.Bomb), "bottom edge on 7×7");
            Assert.AreEqual(0, ColorHuntRules.SingleArea(5, 12, SpecialKind.Golden).Count);
        }

        [Test]
        public void Blast_CornerBomb_IsClipped_AndSetsOffTheLineInIt()
        {
            var a = ColorHuntRules.Blast(Grid("B1234", "1C345", "23456", "34567", "45678"), 0);
            CollectionAssert.AreEqual(new[] { 0, 1, 5, 6, 11, 16, 21 }, a.Cells, "2×2 + column 1");
            Assert.AreEqual("0:Bomb 6:LineV", Fires(a.Triggered), "both fired, in cell order");
        }

        [Test]
        public void Blast_ChainsThroughSpecials()
        {
            var a = ColorHuntRules.Blast(Grid("01234", "12345", "2C456", "3B567", "45678"), 11);
            CollectionAssert.AreEqual(new[] { 1, 6, 10, 11, 12, 15, 16, 17, 20, 21, 22 }, a.Cells);
            Assert.AreEqual("11:LineV 16:Bomb", Fires(a.Triggered));
            Assert.AreEqual(0, ColorHuntRules.Blast(Grid("01234", "12345", "2C456", "3B567", "45678"), 0).Cells.Count, "a plain block fires nothing");
        }

        [Test]
        public void Blast_LeavesGoldenOut_TheGoldenFallsInstead()
        {
            var b = With(Grid("01234", "1A345", "2C456", "34567", "45678"), 9, 6);
            CollectionAssert.AreEqual(new[] { 1, 11, 16, 21 }, ColorHuntRules.Blast(b, 11).Cells, "the golden cell (6) is not in the area");
            var r = ColorHuntRules.Break(b, 11);
            Assert.AreEqual(SpecialKind.Golden, r.Board.Cells[21].Special, "it did not explode: it fell to the bottom of its column");
            Assert.AreEqual(1, CountSpecial(r.Board, SpecialKind.Golden));
        }

        [Test]
        public void TapLine_IsFree_FiresItsColumn_CountsTargets()
        {
            var b = With(Grid("01234", "12345", "2C456", "34567", "45678"), 9, 4);
            b.Targets = new[] { new Target(2, 3) };
            var r = ColorHuntRules.Break(b, 11);
            Assert.AreEqual((BreakOutcome.Broken, true, 0, 100), (r.Outcome, r.Broken, r.Cost, r.Board.Ammo), "free");
            Assert.AreEqual(1, r.Steps.Count);
            CollectionAssert.AreEqual(new[] { 1, 6, 11, 16, 21 }, r.Steps[0].Exploded);
            Assert.AreEqual("11:LineV", Fires(r.Steps[0].Triggered));
            Assert.AreEqual(2, r.Board.Targets[0].Remaining, "the 2 in the column (cell 6) counted");
            b.Ammo = 0;
            Assert.AreEqual(BreakOutcome.Broken, ColorHuntRules.Break(b, 11).Outcome, "fires with no ammo too");
        }

        [Test]
        public void TapLine_ChainsIntoABomb_InOneStep()
        {
            var r = ColorHuntRules.Break(With(Grid("01234", "12345", "2C456", "3B567", "45678"), 9, 4), 11);
            CollectionAssert.AreEqual(new[] { 1, 6, 10, 11, 12, 15, 16, 17, 20, 21, 22 }, r.Steps[0].Exploded);
            Assert.AreEqual("11:LineV 16:Bomb", Fires(r.Steps[0].Triggered));
            Assert.AreEqual(0, CountSpecial(r.Board, SpecialKind.LineV) + CountSpecial(r.Board, SpecialKind.Bomb));
        }

        [Test]
        public void TapGolden_ReturnsGolden_SameBoard()
        {
            var b = Grid("01234", "1A345", "2C456", "34567", "45678");
            var r = ColorHuntRules.Break(b, 6);
            Assert.AreEqual(BreakOutcome.Golden, r.Outcome);
            Assert.IsFalse(r.Broken);
            Assert.AreSame(b, r.Board);
            Assert.AreEqual(0, r.Steps.Count);
            Assert.AreEqual(0, r.Cost);
        }

        [Test]
        public void TapPlain_TooExpensive_IsInsufficient()
        {
            var b = Grid("01234", "12345", "2C456", "34567", "45678");
            b.Ammo = 0;
            var r = ColorHuntRules.Break(b, 0);
            Assert.AreEqual((BreakOutcome.Insufficient, false, 1), (r.Outcome, r.Broken, r.Cost));
            Assert.AreSame(b, r.Board);
        }

        // ---------- gates and the basket ----------

        [Test]
        public void GoldenBirth_OpensAGate_FromTheRng_BeforeTheRefill()
        {
            var b = With(Grid("00000", "12342", "23453", "34564", "45670"), 9, 5);
            var steps = new List<Step>();
            var after = ColorHuntRules.ResolveChains(b, steps);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("1:Golden", Births(steps[0].Born));
            CollectionAssert.AreEqual(new[] { 3 }, steps[0].Opened, "Pota opens column 3 with rng 5");
            CollectionAssert.AreEqual(new[] { 3 }, after.Gates);
            Assert.AreEqual(0, after.Golden, "not in the basket yet");
            var spawned = new List<string>();
            foreach (var s in steps[0].Spawns) spawned.Add($"{s.To}:{s.Block.Color}");
            Assert.AreEqual("0:6 2:5 3:5 4:4", string.Join(" ", spawned), "the gate draw comes before the refill draws");
            Assert.AreEqual(3599190434u, after.Rng);
        }

        [Test]
        public void GoldenBirth_OpensNothing_WhenGatesSuffice()
        {
            var full = new List<Step>();
            var all = ColorHuntRules.ResolveChains(With(Grid("00000", "12342", "23453", "34564", "45670"), 9, 5, 0, 1, 2, 3, 4), full);
            Assert.AreEqual(0, full[0].Opened.Count, "every column already has a gate");
            Assert.AreEqual(5, all.Gates.Count);
            var ready = new List<Step>();
            ColorHuntRules.ResolveChains(With(Grid("00000", "12342", "23453", "34564", "45670"), 9, 5, 3), ready);
            Assert.AreEqual(0, ready[0].Opened.Count, "one golden ball, one gate");
        }

        [Test]
        public void GoldenOnTheBottomRow_AboveAnOpenGate_GoesIntoTheBasket()
        {
            var b = With(Grid("01234", "12345", "23456", "34567", "A5678"), 9, 2, 0);
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(b, out var after, out var step));
            Assert.AreEqual(1, step.ToBasket.Count);
            Assert.AreEqual((20, 0), (step.ToBasket[0].Cell, step.ToBasket[0].Col));
            Assert.AreEqual(0, step.Exploded.Count, "a landing, not an explosion");
            Assert.AreEqual(1, after.Golden);
            Assert.AreEqual(0, after.Gates.Count, "its column had no other golden ball: the gate closed");
            Assert.AreEqual(3, after.Cells[20].Color, "column 0 moved down one");
            Assert.AreEqual(1, step.Spawns.Count);
            Assert.AreEqual((0, 6, 1), (step.Spawns[0].To, step.Spawns[0].Block.Color, step.Spawns[0].Distance));
            Assert.AreEqual(3663131628u, after.Rng);
            Assert.IsFalse(ColorHuntRules.TryDropToBasket(With(Grid("01234", "12345", "23456", "34567", "A5678"), 9, 2, 1), out _, out _),
                           "the gate is on another column");
            var steps = new List<Step>();
            ColorHuntRules.ResolveChains(b, steps);
            Assert.AreEqual(20, steps[0].ToBasket[0].Cell, "ResolveChains looks at the basket first");
        }

        [Test]
        public void TwoGoldenInOneColumn_TheGateStaysOpenUntilTheLast()
        {
            var b = With(Grid("01234", "12345", "23456", "A4567", "A5678"), 9, 2, 0);
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(b, out var one, out var first));
            Assert.AreEqual(20, first.ToBasket[0].Cell);
            CollectionAssert.AreEqual(new[] { 0 }, one.Gates, "a free golden ball is left in the column: still open");
            Assert.AreEqual(0, first.Opened.Count);
            Assert.AreEqual(SpecialKind.Golden, one.Cells[20].Special, "the upper one slid down");
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(one, out var two, out _));
            Assert.AreEqual(2, two.Golden);
            Assert.AreEqual(0, two.Gates.Count, "the last one closed it");
            Assert.IsTrue(ColorHuntRules.TryDropToBasket(With(Grid("01234", "12345", "23456", "34567", "A5A78"), 9, 2, 0, 2), out var both, out var pair));
            Assert.AreEqual(2, pair.ToBasket.Count, "two gates, two landings in one step");
            Assert.AreEqual((20, 0, 22, 2), (pair.ToBasket[0].Cell, pair.ToBasket[0].Col, pair.ToBasket[1].Cell, pair.ToBasket[1].Col));
            Assert.AreEqual(2, both.Golden);
        }

        [Test]
        public void LineUnderAGolden_TheGoldenFallsThroughItsGate_InTheSameMove()
        {
            var r = ColorHuntRules.Break(With(Grid("01234", "1A345", "2C456", "34567", "45678"), 9, 6, 1), 11);
            Assert.AreEqual(2, r.Steps.Count, "the line, then the landing");
            Assert.AreEqual(0, r.Steps[0].ToBasket.Count);
            Assert.AreEqual((21, 1), (r.Steps[1].ToBasket[0].Cell, r.Steps[1].ToBasket[0].Col));
            Assert.AreEqual(1, r.Board.Golden);
            Assert.AreEqual(0, r.Board.Gates.Count);
            Assert.AreEqual(0, CountSpecial(r.Board, SpecialKind.Golden), "it left the board");
        }

        [Test]
        public void RotateAndSettle_DropsGoldenOnGate()
        {
            var b = With(Grid("A1234", "12345", "23456", "34567", "45678"), 9, 8, 0);
            var ccw = ColorHuntRules.RotateAndSettle(b, Rotation.Counter);
            Assert.AreEqual(SpecialKind.Golden, ccw.Rotated.Cells[20].Special, "the turned (unsettled) board has it bottom-left");
            Assert.AreEqual(1, ccw.Steps.Count);
            Assert.AreEqual((20, 0), (ccw.Steps[0].ToBasket[0].Cell, ccw.Steps[0].ToBasket[0].Col));
            Assert.AreEqual(1, ccw.Board.Golden);
            Assert.AreEqual(0, ccw.Board.Gates.Count);
            Assert.AreEqual(0, CountSpecial(ccw.Board, SpecialKind.Golden));
            var cw = ColorHuntRules.RotateAndSettle(b, Rotation.Clockwise);
            Assert.AreEqual(0, cw.Steps.Count, "clockwise takes it top-right: nothing lands");
            Assert.AreSame(cw.Rotated, cw.Board);
            Assert.AreEqual(0, cw.Board.Golden);
            b.Golden = 2;
            var turned = ColorHuntRules.Rotate(b, Rotation.Clockwise);
            CollectionAssert.AreEqual(new[] { 0 }, turned.Gates, "gates sit on the frame: a turn keeps them");
            Assert.AreEqual(2, turned.Golden);
        }

        // ---------- moves ----------

        [Test]
        public void HasMove_TrueWithOnlyASpecial()
        {
            var b = Grid("01234", "12345", "2C456", "34567", "45678");
            for (int i = 0; i < b.Cells.Length; i++) if (!b.Cells[i].IsSpecial) b.Cells[i].Hp = 9;
            b.Ammo = 1;
            Assert.AreEqual(9, ColorHuntRules.Cheapest(b), "the line's hp 0 does not count");
            Assert.IsTrue(ColorHuntRules.HasMove(b), "the line can still be fired");
            b.Ammo = 0;
            Assert.IsTrue(ColorHuntRules.HasMove(b), "with no ammo at all");
            var golden = Grid("A1234", "12345", "23456", "34567", "45678");
            for (int i = 0; i < golden.Cells.Length; i++) if (!golden.Cells[i].IsSpecial) golden.Cells[i].Hp = 9;
            golden.Ammo = 1;
            Assert.IsFalse(ColorHuntRules.HasMove(golden), "a golden ball is not a move");
        }

        [Test]
        public void Cheapest_IgnoresSpecials()
        {
            Assert.AreEqual(int.MaxValue, ColorHuntRules.Cheapest(Grid("CCC", "CCC", "CCC")), "only specials: no cheapest");
            var b = Grid("B12", "345", "67A");
            b.Cells[1].Hp = 4;
            for (int i = 2; i < 8; i++) b.Cells[i].Hp = 6;
            Assert.AreEqual(4, ColorHuntRules.Cheapest(b));
        }
    }
}
