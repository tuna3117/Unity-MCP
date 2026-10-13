using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>
    /// The combo (Pota spec 2026-10-13 §1, §3) against Pota's OWN code: Fixtures/combo-rules.json is written by Pota's src/renkAvi.ts and
    /// src/ozelPlan.ts (Tools/potalook/combo_fixtures.ts, run from the Pota worktree). Unity must find the same partner (komboOrtagi), give
    /// the same tap steps (kir: exploded, triggered, pass hits, combo, births, drops, spawns) and boards on Pota's hand-traced boards (kombo I,
    /// kombo II, the K7 pass distance) and on random games with every combo (golden off: Unity's golden is the gate/basket one, U25), and
    /// schedule every special tap's first step as Pota does (ozelPlani), through the frame mapping in <see cref="Plan"/>.
    /// </summary>
    public class ComboRulesTests
    {
        public const string FixturePath = "_Project/Games/Pota/Tests/EditMode/Fixtures/combo-rules.json";

        // ---------- the file (JsonUtility: field names as written by combo_fixtures.ts) ----------

        [Serializable] public class FixtureFile
        {
            public int version; public string pota; public DesignDto design; public int minPassDistance, comboPassCount, passFlights;
            public PriorityDto[] priority; public KindOfDto[] kindsOf; public TapDto[] taps; public PartnerDto[] partners; public PairsDto[] pairs;
            public ClosureDto[] closures; public ComboCellsDto[] comboCells; public DelayDto[] delays; public PlanDto[] plans; public GameDto[] replays;
            public CountDto[] comboCounts;
        }
        [Serializable] public class DesignDto { public int size, colorCount, hpMin, hpMax, ammo; public ColorTargetDto[] targets; }
        [Serializable] public class ColorTargetDto { public int color, count; }
        [Serializable] public class FeaturesDto { public bool line, bomb, golden, pass, combo; }
        [Serializable] public class CellKindDto { public int cell, kind; }
        [Serializable] public class PassDto { public int cell, target; }
        [Serializable] public class ComboDto { public string kind; public int center, partner, carried; public int[] passTargets; }
        [Serializable] public class TargetDto { public string kind; public int color, count; }
        [Serializable] public class ProbeDto { public int[] exclude; public bool noTargets; public int expect; }
        [Serializable] public class BoardDto { public int size; public int[] colors, hp, specials, targetsLeft; public int ammo; public long rng; }
        [Serializable] public class StepDto { public int[] exploded; public CellKindDto[] triggered, born; public PassDto[] passes; public ComboDto combo; public DropDto[] drops; public SpawnDto[] spawns; }
        [Serializable] public class DropDto { public int from, to; }
        [Serializable] public class SpawnDto { public int to, color, hp, distance; }
        [Serializable] public class TapDto { public string name; public string[] rows; public int colorCount, ammo, tap, partner, cost; public long rng; public FeaturesDto features; public TargetDto[] targets; public BoardDto start, after; public string result; public ProbeDto[] probes; public StepDto[] steps; }
        [Serializable] public class PartnerDto { public string name; public string[] rows; public FeaturesDto features; public int cell, expect; }
        [Serializable] public class PairDto { public int cell, partner; }
        [Serializable] public class PairsDto { public string name; public string[] rows; public FeaturesDto features; public PairDto[] pairs; }
        [Serializable] public class ClosureDto { public string name; public string[] rows; public int[] start, cells; public CellKindDto[] consumed, triggered; }
        [Serializable] public class ComboCellsDto { public string kind; public int n, center; public int[] cells; }
        [Serializable] public class DelayDto { public string kind; public int n, center, cell; public double t; }
        [Serializable] public class KindOfDto { public int a, b; public string kind; }
        [Serializable] public class PriorityDto { public string kind; public int priority; }
        [Serializable] public class CountDto { public string kind; public int count; }
        [Serializable] public class TimingDto { public double lineSpeed, lineLead, bombLead, bombRing, cap, passTakeoff, passSpeed, passMin, passMax, passHang; }
        [Serializable] public class FireDto { public int cell, kind, order, size; public double t; }
        [Serializable] public class StartDto { public int cell; public double t; }
        [Serializable] public class PlanValuesDto { public FireDto[] fires; public StartDto[] starts; public double pace, end; }
        [Serializable] public class PlanDto { public string name; public int n, origin; public double swell, peakAt; public TimingDto timing; public StepDto step; public PlanValuesDto pota, flat; }
        [Serializable] public class MoveDto { public string kind, dir, result; public int cell, cost, partner; public StepDto[] steps; public BoardDto after; }
        [Serializable] public class GameDto { public string name; public long seed; public int ammo; public FeaturesDto features; public BoardDto start; public MoveDto[] moves; }

        private static FixtureFile _fixture;

        public static FixtureFile Fixture
        {
            get
            {
                if (_fixture != null) return _fixture;
                string path = Path.Combine(Application.dataPath, FixturePath);
                if (!File.Exists(path)) throw new FileNotFoundException($"{path} is missing: run Tools/potalook/combo_fixtures.ts from the Pota worktree");
                return _fixture = JsonUtility.FromJson<FixtureFile>(File.ReadAllText(path));
            }
        }

        public static IEnumerable<TestCaseData> Taps() { foreach (var t in Fixture.taps) yield return new TestCaseData(t.name).SetName("Tap_" + t.name); }
        public static IEnumerable<TestCaseData> Partners() { foreach (var p in Fixture.partners) yield return new TestCaseData(p.name).SetName("Partner_" + p.name); }
        public static IEnumerable<TestCaseData> PairSets() { foreach (var p in Fixture.pairs) yield return new TestCaseData(p.name).SetName("Pairs_" + p.name); }
        public static IEnumerable<TestCaseData> Closures() { foreach (var c in Fixture.closures) yield return new TestCaseData(c.name).SetName("Closure_" + c.name); }
        public static IEnumerable<TestCaseData> Plans() { foreach (var p in Fixture.plans) yield return new TestCaseData(p.name).SetName("Plan_" + p.name.Replace(' ', '_')); }
        public static IEnumerable<TestCaseData> Replays() { foreach (var g in Fixture.replays) yield return new TestCaseData(g.name).SetName("Replay_" + g.name); }

        // ---------- boards ----------

        private static Features ToFeatures(FeaturesDto f) => new Features { Line = f.line, Bomb = f.bomb, Golden = f.golden, Pass = f.pass, Combo = f.combo };

        private static SpecialKind Letter(char ch)
        {
            switch (ch)
            {
                case 'C': return SpecialKind.LineV;
                case 'Y': return SpecialKind.LineH;
                case 'B': return SpecialKind.Bomb;
                case 'A': return SpecialKind.Golden;
                case 'P': return SpecialKind.Pass;
                default: return SpecialKind.None;
            }
        }

        /// <summary>renk-avi-test.ts `tahta`: a digit per colour, letters for specials, hp 1, 100 balls.</summary>
        private static Board Rows(string[] rows, Features f, int colorCount = 5, uint rng = 1, TargetDto[] targets = null)
        {
            int n = rows.Length;
            var p = new BoardPreset { Size = n, ColorCount = colorCount, HpMin = 1, HpMax = 1, Ammo = 100, Rng = rng, Features = f,
                Colors = new int[n * n], Specials = new SpecialKind[n * n], Hp = new int[n * n], Targets = new (int, int)[0] };
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    var kind = Letter(rows[r][c]);
                    int i = r * n + c;
                    p.Specials[i] = kind;
                    p.Colors[i] = kind != SpecialKind.None ? ColorHuntRules.SpecialColor : rows[r][c] - '0';
                    p.Hp[i] = 1;
                }
            if (targets != null)
            {
                var colors = new List<(int, int)>();
                foreach (var t in targets)
                {
                    if (t.kind == "renk") colors.Add((t.color, t.count));
                    else if (t.kind == "bomba") p.BombTarget = t.count;
                    else if (t.kind == "cizgi") p.LineTarget = t.count;
                    else Assert.Fail($"target kind '{t.kind}' is not ported");
                }
                p.Targets = colors.ToArray();
            }
            return ColorHuntRules.FromPreset(p);
        }

        /// <summary>The remaining counts in Pota's target order.</summary>
        private static int[] Left(Board b, TargetDto[] targets)
        {
            var left = new int[targets.Length];
            int k = 0;
            for (int i = 0; i < targets.Length; i++)
                left[i] = targets[i].kind == "renk" ? b.Targets[k++].Remaining : targets[i].kind == "bomba" ? b.BombTarget : b.LineTarget;
            return left;
        }

        private static void Same(BoardDto want, Board got, string at, TargetDto[] targets = null)
        {
            Assert.AreEqual(want.size, got.Size, at + ": size");
            var colors = new int[got.Cells.Length];
            var hp = new int[got.Cells.Length];
            var specials = new int[got.Cells.Length];
            for (int i = 0; i < got.Cells.Length; i++) { colors[i] = got.Cells[i].Color; hp[i] = got.Cells[i].Hp; specials[i] = (int)got.Cells[i].Special; }
            CollectionAssert.AreEqual(want.specials, specials, at + ": specials");
            CollectionAssert.AreEqual(want.colors, colors, at + ": colours");
            CollectionAssert.AreEqual(want.hp, hp, at + ": hp");
            Assert.AreEqual(want.ammo, got.Ammo, at + ": ammo");
            Assert.AreEqual((uint)want.rng, got.Rng, at + ": rng");
            if (targets != null) CollectionAssert.AreEqual(want.targetsLeft, Left(got, targets), at + ": targets left");
            else
            {
                var left = new int[got.Targets.Length];
                for (int t = 0; t < left.Length; t++) left[t] = got.Targets[t].Remaining;
                CollectionAssert.AreEqual(want.targetsLeft, left, at + ": targets left");
            }
        }

        private static string[] Kinds(CellKindDto[] l) => Array.ConvertAll(l, k => $"{k.cell}:{k.kind}");

        private static void Same(StepDto want, Step got, string at)
        {
            CollectionAssert.AreEqual(want.exploded, got.Exploded, at + ": exploded");
            CollectionAssert.AreEqual(Kinds(want.triggered), got.Triggered.ConvertAll(t => $"{t.Cell}:{(int)t.Kind}"), at + ": triggered (in firing order)");
            CollectionAssert.AreEqual(Array.ConvertAll(want.passes, p => $"{p.cell}->{p.target}"), got.Passes.ConvertAll(p => $"{p.Cell}->{p.Target}"), at + ": pass hits");
            SameCombo(want.combo, got.Combo, at);
            CollectionAssert.AreEqual(Kinds(want.born), got.Born.ConvertAll(b => $"{b.Cell}:{(int)b.Kind}"), at + ": born");
            CollectionAssert.AreEqual(Array.ConvertAll(want.drops, d => $"{d.from}>{d.to}"), got.Drops.ConvertAll(d => $"{d.From}>{d.To}"), at + ": drops");
            CollectionAssert.AreEqual(Array.ConvertAll(want.spawns, s => $"{s.to}:{s.color}/{s.hp}/{s.distance}"),
                got.Spawns.ConvertAll(s => $"{s.To}:{s.Block.Color}/{s.Block.Hp}/{s.Distance}"), at + ": spawns");
            Assert.IsEmpty(got.ToBasket, at + ": no basket");
            Assert.IsEmpty(got.Opened, at + ": no gate");
        }

        private static void SameCombo(ComboDto want, ComboInfo? got, string at)
        {
            if (string.IsNullOrEmpty(want.kind)) { Assert.IsFalse(got.HasValue, at + ": no combo"); return; }
            Assert.IsTrue(got.HasValue, at + $": a {want.kind} combo");
            var c = got.Value;
            Assert.AreEqual(want.kind, c.Kind.ToString(), at + ": combo kind");
            Assert.AreEqual(want.center, c.Center, at + ": combo centre (the tapped cell)");
            Assert.AreEqual(want.partner, c.Partner, at + ": combo partner");
            Assert.AreEqual(want.carried, (int)c.Carried, at + ": carried special");
            CollectionAssert.AreEqual(want.passTargets, c.PassTargets, at + ": combo pass targets");
        }

        private static string Outcome(BreakOutcome o) => o == BreakOutcome.Broken ? "kirildi" : o == BreakOutcome.Insufficient ? "yetmez" : "altin";

        private static ComboKind Combo(string name) => (ComboKind)Enum.Parse(typeof(ComboKind), name);

        // ---------- the fixture itself ----------

        [Test]
        public void Fixture_IsPotas_AndTracesEveryCombo()
        {
            StringAssert.Contains("renkAvi.ts", Fixture.pota, "written by Pota's own rules (commit + renkAvi.ts hash)");
            StringAssert.Contains("ozelPlan.ts", Fixture.pota, "and Pota's own schedule (ozelPlan.ts hash)");
            Assert.AreEqual(6, Fixture.comboCounts.Length);
            foreach (var c in Fixture.comboCounts) Assert.Greater(c.count, 0, $"{c.kind}: traced");
            Assert.Greater(Fixture.passFlights, 0, "the replays fly pass balls");
            Assert.AreEqual(27, Fixture.taps.Length);
            Assert.Greater(Fixture.plans.Length, 0);
        }

        [Test]
        public void Constants_ArePotas()
        {
            Assert.AreEqual(Fixture.minPassDistance, ColorHuntRules.MinPassDistance, "PAS_EN_AZ_UZAKLIK");
            Assert.AreEqual(Fixture.comboPassCount, ColorHuntRules.ComboPassCount, "KOMBO_PAS_SAYISI");
            Assert.AreEqual(6, Fixture.priority.Length);
            foreach (var p in Fixture.priority) Assert.AreEqual(p.priority, ColorHuntRules.ComboPriority(Combo(p.kind)), $"KOMBO_ONCELIK.{p.kind}");
        }

        [Test]
        public void Features_FullHasTheCombo_AllAndNoneDoNot()
        {
            Assert.IsTrue(Features.Full.Combo, "OZELLIK_TAM.kombo (levels 9–50)");
            Assert.IsTrue(Features.Full.Line && Features.Full.Bomb && Features.Full.Golden && Features.Full.Pass);
            Assert.IsFalse(Features.All.Combo, "OZELLIK_HEPSI.kombo (levels 1–8): the old rule tests stay as they are");
            Assert.IsFalse(Features.None.Combo);
            Assert.IsFalse(new Features { Combo = true }.Any, "the combo is not a special of its own");
        }

        [Test]
        public void ComboKindOf_IsPotasKomboTuru()
        {
            Assert.AreEqual(25, Fixture.kindsOf.Length);
            foreach (var k in Fixture.kindsOf)
            {
                var got = ColorHuntRules.ComboKindOf((SpecialKind)k.a, (SpecialKind)k.b);
                Assert.AreEqual(k.kind, got.HasValue ? got.Value.ToString() : "", $"komboTuru({k.a}, {k.b})");
            }
            Assert.IsNull(ColorHuntRules.ComboKindOf(SpecialKind.None, SpecialKind.Bomb), "a normal block makes no combo");
        }

        [Test]
        public void ComboCells_AreKomboAlanHucreleri()
        {
            foreach (var c in Fixture.comboCells)
                CollectionAssert.AreEqual(c.cells, ColorHuntRules.ComboCells(c.n, c.center, Combo(c.kind)), $"{c.kind} at {c.center}");
            Assert.Throws<ArgumentException>(() => ColorHuntRules.ComboCells(7, 24, ComboKind.PassBomb), "a pass combo has no area of its own");
        }

        [Test]
        public void ComboDelay_IsKomboGecikme()
        {
            var t = FixtureTiming(Fixture.plans[0].timing);
            foreach (var d in Fixture.delays)
                Assert.AreEqual((float)d.t, SpecialSchedule.ComboDelay(d.n, d.center, Combo(d.kind), d.cell, t), 1e-5f, $"{d.kind} {d.center} → {d.cell}");
        }

        // ---------- rules ----------

        [TestCaseSource(nameof(Taps))]
        public void Tap(string name)
        {
            var t = Array.Find(Fixture.taps, x => x.name == name);
            var b = Rows(t.rows, ToFeatures(t.features), t.colorCount, (uint)t.rng, t.targets);
            Same(t.start, b, name + " start", t.targets);
            Assert.AreEqual(t.partner, ColorHuntRules.FindComboPartner(b, t.tap), name + ": komboOrtagi");
            foreach (var p in t.probes)
            {
                var probe = b;
                if (p.noTargets) { probe = b.Clone(); probe.Targets = new Target[0]; probe.BombTarget = 0; probe.LineTarget = 0; }
                Assert.AreEqual(p.expect, ColorHuntRules.PassTarget(probe, t.tap, new HashSet<int>(p.exclude)), $"{name}: pasHedefi (targets {(p.noTargets ? "none" : "on")})");
            }
            var k = ColorHuntRules.Break(b, t.tap);
            Assert.AreEqual(t.result, Outcome(k.Outcome), name + ": outcome");
            Assert.AreEqual(t.cost, k.Cost, name + ": a combo is free");
            Assert.AreEqual(t.steps.Length, k.Steps.Count, name + ": step count");
            for (int s = 0; s < k.Steps.Count; s++) Same(t.steps[s], k.Steps[s], $"{name} step {s}");
            Same(t.after, k.Board, name + " after", t.targets);
        }

        [TestCaseSource(nameof(Partners))]
        public void Partner(string name)
        {
            var p = Array.Find(Fixture.partners, x => x.name == name);
            var b = Rows(p.rows, ToFeatures(p.features), 1000, 3);
            Assert.AreEqual(p.expect, ColorHuntRules.FindComboPartner(b, p.cell), name + ": komboOrtagi");
        }

        [TestCaseSource(nameof(PairSets))]
        public void Pairs(string name)
        {
            var p = Array.Find(Fixture.pairs, x => x.name == name);
            var b = Rows(p.rows, ToFeatures(p.features), 1000, 3);
            var got = new List<string>();
            foreach (var kv in ColorHuntRules.ComboPairs(b)) got.Add($"{kv.Key}>{kv.Value}");
            CollectionAssert.AreEqual(Array.ConvertAll(p.pairs, x => $"{x.cell}>{x.partner}"), got, name + ": komboCiftleri");
        }

        [TestCaseSource(nameof(Closures))]
        public void Closure(string name)
        {
            var c = Array.Find(Fixture.closures, x => x.name == name);
            var b = Rows(c.rows, Features.Full, 1000, 3);
            var consumed = new Dictionary<int, SpecialKind>();
            foreach (var k in c.consumed) consumed[k.cell] = (SpecialKind)k.kind;
            var area = ColorHuntRules.Closure(b, c.start, consumed);
            CollectionAssert.AreEqual(c.cells, area.Cells, name + ": alanKapanisi cells");
            CollectionAssert.AreEqual(Kinds(c.triggered), area.Triggered.ConvertAll(t => $"{t.Cell}:{(int)t.Kind}"), name + ": alanKapanisi triggered");
        }

        [TestCaseSource(nameof(Replays))]
        public void Replay(string name)
        {
            var g = Array.Find(Fixture.replays, x => x.name == name);
            var d = Fixture.design;
            var design = new LevelDesign
            {
                Size = d.size, ColorCount = d.colorCount, HpMin = d.hpMin, HpMax = d.hpMax,
                Targets = Array.ConvertAll(d.targets, t => (t.color, t.count)), Features = ToFeatures(g.features),
            };
            var board = ColorHuntRules.Create(design, g.ammo, (uint)g.seed);
            Same(g.start, board, name + " start");
            for (int m = 0; m < g.moves.Length; m++)
            {
                var mv = g.moves[m];
                string at = $"{name} move {m} ({mv.kind} {(mv.kind == "tap" ? mv.cell.ToString() : mv.dir)})";
                List<Step> steps;
                if (mv.kind == "rotate")
                {
                    var r = ColorHuntRules.RotateAndSettle(board, mv.dir == "saat" ? Rotation.Clockwise : Rotation.Counter);
                    board = r.Board;
                    steps = r.Steps;
                }
                else
                {
                    Assert.AreEqual(mv.partner, ColorHuntRules.FindComboPartner(board, mv.cell), at + ": komboOrtagi");
                    var r = ColorHuntRules.Break(board, mv.cell);
                    Assert.AreEqual(mv.result, Outcome(r.Outcome), at + ": outcome");
                    Assert.AreEqual(mv.cost, r.Cost, at + ": cost");
                    board = r.Board;
                    steps = r.Steps;
                }
                Assert.AreEqual(mv.steps.Length, steps.Count, at + ": step count");
                for (int s = 0; s < steps.Count; s++) Same(mv.steps[s], steps[s], $"{at} step {s}");
                Same(mv.after, board, at + " after");
                Assert.IsFalse(ColorHuntRules.HasMatch(board), at + ": settled boards keep no match or square");
            }
        }

        [Test]
        public void ComboOff_TheOldRulesStay()
        {
            // Features.All (OZELLIK_HEPSI): a line next to a line fires alone, its column only (renk-avi-test kombo I (a) "kapalı").
            var t = Array.Find(Fixture.taps, x => x.name == "cross-off");
            Assert.IsFalse(t.features.combo);
            var b = Rows(t.rows, Features.All, t.colorCount, (uint)t.rng, t.targets);
            Assert.AreEqual(-1, ColorHuntRules.FindComboPartner(b, 24));
            Assert.IsNull(ColorHuntRules.Break(b, 24).Steps[0].Combo);
            Assert.IsEmpty(ColorHuntRules.ComboPairs(b));
        }

        // ---------- the trigger plan (Pota ozelPlani) ----------

        private static SpecialTiming FixtureTiming(TimingDto t) => new SpecialTiming
        {
            LineSpeed = (float)t.lineSpeed, LineLead = (float)t.lineLead, BombLead = (float)t.bombLead, BombRing = (float)t.bombRing,
        };

        private static Step ToStep(StepDto d)
        {
            var s = new Step();
            s.Exploded.AddRange(d.exploded);
            foreach (var t in d.triggered) s.Triggered.Add(new Trigger { Cell = t.cell, Kind = (SpecialKind)t.kind });
            foreach (var p in d.passes) s.Passes.Add(new PassHit { Cell = p.cell, Target = p.target });
            if (!string.IsNullOrEmpty(d.combo.kind))
                s.Combo = new ComboInfo { Kind = Combo(d.combo.kind), Center = d.combo.center, Partner = d.combo.partner, Carried = (SpecialKind)d.combo.carried, PassTargets = d.combo.passTargets };
            return s;
        }

        private const float PlanEps = 1e-5f;

        /// <summary>
        /// Unity's schedule against Pota's ozelPlani on the same step (the fixture's step, so a rules bug cannot hide here), with Pota's
        /// OZEL_ZAMANLAMA (= SpecialTiming line/bomb numbers, PassTiming.B, MaxStep 0.6) and Pota's swell 0.3. The two schedules use two
        /// frames: Pota fires the tapped special at the tap (t = 0) and starts a cell's swell so that its PEAK (sisme × tepe later) lands on
        /// the arrival, clamped at the root; Unity fires the tapped special at the step's first peak (swell × 0.55) and starts a cell's swell
        /// AT its arrival, and every fired special's own block bursts at its fire. Pota with tepe 0 (`flat`) is Unity's frame minus that
        /// peak, so, exactly: same fires (cell, kind, order, size), fire time = flat t + peak, same pace; a fired special's start = its flat
        /// fire time; a pass-hit normal block's Hits = flat start (the landing) + peak; every other start = flat start; End = the latest
        /// start (a hit: its landing) + swell. Pota's game plan (`pota`, tepe 0.55) shares the fires; their times agree when neither plan
        /// is compressed.
        /// </summary>
        [TestCaseSource(nameof(Plans))]
        public void Plan(string name)
        {
            var p = Array.Find(Fixture.plans, x => x.name == name);
            var pass = PassTiming.B;
            Assert.AreEqual((float)p.timing.passTakeoff, pass.Takeoff, 1e-6f, "pasKalkis");
            Assert.AreEqual((float)p.timing.passSpeed, pass.Speed, 1e-6f, "pasHiz");
            Assert.AreEqual((float)p.timing.passMin, pass.MinFlight, 1e-6f, "pasEnAz");
            Assert.AreEqual((float)p.timing.passMax, pass.MaxFlight, 1e-6f, "pasEnCok");
            Assert.AreEqual((float)p.timing.passHang, pass.Hang, 1e-6f, "pasAsili");
            Assert.AreEqual((float)p.timing.cap, SpecialSchedule.MaxStep, 1e-6f, "tavan");
            Assert.AreEqual((float)p.peakAt, PotaLook.SwellPeakAt, 1e-6f, "tepe");
            float swell = (float)p.swell, peak = swell * PotaLook.SwellPeakAt;
            var step = ToStep(p.step);
            var plan = SpecialSchedule.Plan(p.n, step, p.origin, FixtureTiming(p.timing), swell, pass);

            var flat = p.flat;
            Assert.AreEqual(flat.fires.Length, plan.Fires.Count, name + ": fires");
            Assert.AreEqual(p.pota.fires.Length, plan.Fires.Count, name + ": fires (game plan)");
            var fireAt = new Dictionary<int, float>();
            float end = 0f;
            for (int i = 0; i < plan.Fires.Count; i++)
            {
                var want = flat.fires[i];
                var got = plan.Fires[i];
                string at = $"{name}: fire {i}";
                Assert.AreEqual(want.cell, got.Cell, at + " cell");
                Assert.AreEqual(want.kind, (int)got.Kind, at + " kind");
                Assert.AreEqual(want.order, got.Order, at + " order");
                Assert.AreEqual(want.size, got.Size, at + " size");
                Assert.AreEqual((float)want.t + peak, got.Time, PlanEps, at + " time (flat t + peak)");
                var game = p.pota.fires[i];
                Assert.AreEqual($"{game.cell}:{game.kind}:{game.order}:{game.size}", $"{got.Cell}:{(int)got.Kind}:{got.Order}:{got.Size}", at + " (game plan)");
                if (p.pota.pace == 1.0 && flat.pace == 1.0) Assert.AreEqual((float)game.t + peak, got.Time, PlanEps, at + " time (game plan, uncompressed)");
                fireAt[got.Cell] = (float)want.t;
                end = Mathf.Max(end, got.Time);
            }
            Assert.AreEqual((float)flat.pace, plan.Pace, PlanEps, name + ": pace");

            var flatStart = new Dictionary<int, float>();
            foreach (var s in flat.starts) flatStart[s.cell] = (float)s.t;
            var exploded = new HashSet<int>(step.Exploded);
            var hits = new HashSet<int>();
            foreach (var h in step.Passes) if (h.Target >= 0 && exploded.Contains(h.Target) && !fireAt.ContainsKey(h.Target)) hits.Add(h.Target);
            CollectionAssert.AreEquivalent(step.Exploded, plan.Start.Keys, name + ": one start per exploded cell");
            CollectionAssert.AreEquivalent(hits, plan.Hits.Keys, name + ": pass hits on normal blocks");
            foreach (int c in step.Exploded)
            {
                string at = $"{name}: cell {c}";
                if (hits.Contains(c))
                {
                    Assert.AreEqual(flatStart[c] + peak, plan.Hits[c], PlanEps, at + " hit (the landing)");
                    end = Mathf.Max(end, Mathf.Max(plan.Hits[c], plan.Start[c]) + swell);
                    continue;
                }
                float want = fireAt.TryGetValue(c, out float fire) ? fire : flatStart[c];
                Assert.AreEqual(want, plan.Start[c], PlanEps, at + (fireAt.ContainsKey(c) ? " start (a fired special bursts at its fire)" : " start"));
                end = Mathf.Max(end, want + swell);
            }
            Assert.AreEqual(end, plan.End, PlanEps, name + ": end");
        }
    }
}
