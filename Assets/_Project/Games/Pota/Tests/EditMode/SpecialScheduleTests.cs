using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>SpecialSchedule (spec §2 "sırayla patlama", Review Focus 4): Pota fires all at once; Royal-like timings sweep, ripple and chain on arrival.</summary>
    public class SpecialScheduleTests
    {
        private const float Swell = 0.15f, Eps = 1e-4f;
        private static float Peak => Swell * PotaLook.SwellPeakAt;

        /// <summary>Spec §2 "1 · Royal": a 0.06 s squash, the rocket crosses a 7×7 (6 cells) in 0.32 s; the bomb winds up 0.18 s, rings 0.04 s apart.</summary>
        private static readonly SpecialTiming Royal = new SpecialTiming { LineSpeed = 6f / 0.32f, LineLead = 0.06f, BombLead = 0.18f, BombRing = 0.04f };

        /// <summary>7×7 colour (2r + c) mod 5 (no two neighbours alike), hp 5, all specials on.</summary>
        private static Board Board7(params (int cell, SpecialKind kind)[] specials)
        {
            var cells = new Block[49];
            for (int i = 0; i < 49; i++) cells[i] = new Block((2 * (i / 7) + i % 7) % 5, 5);
            foreach (var (cell, kind) in specials) cells[cell] = Block.MakeSpecial(kind);
            return new Board
            {
                Size = 7, Cells = cells, Ammo = 158, Targets = new[] { new Target(0, 22), new Target(1, 16) },
                ColorCount = 5, HpMin = 3, HpMax = 14, Rng = 1, Features = Features.All,
            };
        }

        /// <summary>The trigger step the rules give for a tap on <paramref name="origin"/>: its area and the specials it sets off.</summary>
        private static Step Blast(Board b, int origin)
        {
            var area = ColorHuntRules.Blast(b, origin);
            var step = new Step();
            step.Exploded.AddRange(area.Cells);
            step.Triggered.AddRange(area.Triggered);
            return step;
        }

        private static TriggerPlan Plan(Board b, int origin, SpecialTiming t) => SpecialSchedule.Plan(7, Blast(b, origin), origin, t, Swell);

        [Test]
        public void PotaTiming_EveryCellAtOnce_EveryFireAtTheFirstPeak()
        {
            var b = Board7((21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV));
            var step = Blast(b, 21);
            var plan = SpecialSchedule.Plan(7, step, 21, SpecialTiming.Pota, Swell);
            Assert.AreEqual(17, step.Exploded.Count, "row 3 + the bomb's 3×3 + column 5");
            Assert.AreEqual(step.Exploded.Count, plan.Start.Count);
            foreach (int c in step.Exploded) Assert.AreEqual(0f, plan.Start[c], $"cell {c}");
            Assert.AreEqual(3, plan.Fires.Count);
            Assert.AreEqual(21, plan.Fires[0].Cell, "the tapped special fires first");
            Assert.AreEqual(0, plan.Fires[0].Order);
            foreach (var f in plan.Fires)
            {
                Assert.AreEqual(Peak, f.Time, Eps, $"{f.Kind} at {f.Cell}: renkAviFaz.ts adimTepe");
                Assert.AreEqual(1f, f.Pace);
            }
            Assert.AreEqual(Swell, plan.End, Eps, "the drops start after one ChainTime, as today");
            Assert.AreEqual(1f, plan.Pace);
        }

        [Test]
        public void Fire_CarriesKindPositionAndClippedAreaSize()
        {
            var plan = Plan(Board7((24, SpecialKind.Bomb)), 24, SpecialTiming.Pota);
            Assert.AreEqual(SpecialKind.Bomb, plan.Fires[0].Kind);
            Assert.AreEqual(BoardBuilder.CellPosition(7, 3, 3), plan.Fires[0].At);
            Assert.AreEqual(9, plan.Fires[0].Size);
            var corner = Plan(Board7((48, SpecialKind.Bomb)), 48, SpecialTiming.Pota);
            Assert.AreEqual(4, corner.Fires[0].Size, "tekAlan: clipped at the corner");
            Assert.AreEqual(4, corner.Start.Count);
        }

        [Test]
        public void GoldenBall_IsNeverInTheArea()
        {
            var plan = Plan(Board7((21, SpecialKind.LineH), (24, SpecialKind.Golden)), 21, Royal);
            Assert.IsFalse(plan.Start.ContainsKey(24), "patlamaAlani: altın alandan çıkar");
            Assert.AreEqual(1, plan.Fires.Count);
            Assert.AreEqual(6, plan.Fires[0].Size);
        }

        [Test]
        public void RoyalLine_SweepsByDistance_AfterItsLead()
        {
            var plan = Plan(Board7((21, SpecialKind.LineH)), 21, Royal);
            Assert.AreEqual(0f, plan.Start[21], Eps, "the line ball itself goes first");
            for (int k = 1; k < 7; k++) Assert.AreEqual(Royal.LineLead + k / Royal.LineSpeed, plan.Start[21 + k], Eps, $"(3, {k})");
            Assert.AreEqual(Royal.LineLead + 6f / Royal.LineSpeed + Swell, plan.End, Eps);
            var column = Plan(Board7((3, SpecialKind.LineV)), 3, Royal);
            for (int r = 1; r < 7; r++) Assert.AreEqual(Royal.LineLead + r / Royal.LineSpeed, column.Start[3 + 7 * r], Eps, $"({r}, 3)");
        }

        [Test]
        public void RoyalBomb_BurstsInRings_AfterItsWindUp()
        {
            var plan = Plan(Board7((24, SpecialKind.Bomb)), 24, Royal);
            Assert.AreEqual(0f, plan.Start[24], Eps);
            foreach (int c in new[] { 16, 17, 18, 23, 25, 30, 31, 32 })
                Assert.AreEqual(Royal.BombLead + Royal.BombRing, plan.Start[c], Eps, $"cell {c}: ring 1");
            Assert.AreEqual(Royal.BombLead + Royal.BombRing + Swell, plan.End, Eps);
        }

        [Test]
        public void Chain_FiresWhenReached_AndItsAreaFollows()
        {
            var plan = Plan(Board7((21, SpecialKind.LineH), (24, SpecialKind.Bomb)), 21, Royal);
            float reach = Royal.LineLead + 3f / Royal.LineSpeed;                       // the rocket gets to (3, 3)
            Assert.AreEqual(2, plan.Fires.Count);
            Assert.AreEqual(24, plan.Fires[1].Cell);
            Assert.AreEqual(1, plan.Fires[1].Order);
            Assert.AreEqual(Peak + reach, plan.Fires[1].Time, Eps, "the bomb fires the moment the rocket reaches it");
            Assert.AreEqual(reach, plan.Start[24], Eps);
            float ring = reach + Royal.BombLead + Royal.BombRing;
            Assert.AreEqual(ring, plan.Start[17], Eps, "(2, 3): only the bomb reaches it");
            Assert.AreEqual(Royal.LineLead + 2f / Royal.LineSpeed, plan.Start[23], Eps, "(3, 2): the rocket is earlier than the ring");
            Assert.AreEqual(Royal.LineLead + 4f / Royal.LineSpeed, plan.Start[25], Eps, "(3, 4)");
            Assert.AreEqual(ring + Swell, plan.End, Eps);
            Assert.LessOrEqual(plan.End, SpecialSchedule.MaxStep + Eps);
            Assert.AreEqual(1f, plan.Pace, "0.59 s: fits, not compressed");
        }

        [Test]
        public void LinePlusBombChain_7x7_EndsWithinTheCap()
        {
            // The worst 7×7 line + bomb chain: the rocket crosses the whole row to a bomb at its far end.
            var plan = Plan(Board7((21, SpecialKind.LineH), (27, SpecialKind.Bomb)), 21, Royal);
            float raw = Royal.LineLead + 6f / Royal.LineSpeed + Royal.BombLead + Royal.BombRing;    // 0.60 s of starts
            Assert.Greater(raw + Swell, SpecialSchedule.MaxStep, "uncompressed it would run long");
            Assert.AreEqual(SpecialSchedule.MaxStep, plan.End, Eps, "≤ 0.6 s from the tap to the drops (Global Constraints)");
            Assert.AreEqual((SpecialSchedule.MaxStep - Swell) / raw, plan.Pace, Eps);
            foreach (var f in plan.Fires) Assert.AreEqual(plan.Pace, f.Pace, Eps);
            Assert.Less(plan.Start[22], plan.Start[27]);
            Assert.Less(plan.Start[27], plan.Start[20], "(2, 6) bursts in the bomb's ring, after the bomb");
            Assert.AreEqual(plan.Fires[1].Time - Peak, plan.Start[27], Eps, "the bomb fires the moment its cell starts");
            // The Chain scenario's three specials (line → bomb → column) fit too.
            var chain = Plan(Board7((21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV)), 21, Royal);
            Assert.AreEqual(3, chain.Fires.Count);
            Assert.LessOrEqual(chain.End, SpecialSchedule.MaxStep + Eps);
        }

        [Test]
        public void MutualChain_FiresEachOnce()
        {
            var cases = new (string name, Board board, int origin)[]
            {
                ("a bomb in the line's path whose 3×3 holds the line", Board7((23, SpecialKind.LineH), (24, SpecialKind.Bomb)), 23),
                ("the same, tapped on the bomb", Board7((23, SpecialKind.LineH), (24, SpecialKind.Bomb)), 24),
                ("two lines in one row", Board7((21, SpecialKind.LineH), (25, SpecialKind.LineH)), 21),
                ("crossing lines", Board7((22, SpecialKind.LineH), (25, SpecialKind.LineV)), 22),
                ("line → bomb → column", Board7((21, SpecialKind.LineH), (25, SpecialKind.Bomb), (19, SpecialKind.LineV)), 21),
            };
            foreach (var timing in new[] { SpecialTiming.Pota, Royal })
                foreach (var (name, board, origin) in cases)
                {
                    var step = Blast(board, origin);
                    var plan = SpecialSchedule.Plan(7, step, origin, timing, Swell);
                    var fired = new HashSet<int>();
                    foreach (var f in plan.Fires) Assert.IsTrue(fired.Add(f.Cell), $"{name}: {f.Cell} fired twice");
                    Assert.AreEqual(step.Triggered.Count, plan.Fires.Count, $"{name}: every triggered special fires");
                    foreach (var tr in step.Triggered) Assert.IsTrue(fired.Contains(tr.Cell), $"{name}: {tr.Cell} never fired");
                    CollectionAssert.AreEquivalent(step.Exploded, plan.Start.Keys, $"{name}: exactly one start per exploded cell");
                    foreach (var kv in plan.Start) Assert.GreaterOrEqual(kv.Value, 0f, name);
                    for (int i = 1; i < plan.Fires.Count; i++) Assert.LessOrEqual(plan.Fires[i - 1].Time, plan.Fires[i].Time, $"{name}: sorted");
                    Assert.AreEqual(origin, plan.Fires[0].Cell, name);
                    Assert.LessOrEqual(plan.End, SpecialSchedule.MaxStep + Eps, name);
                }
        }
    }
}
