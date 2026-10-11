using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>
    /// The pass ball (Pota spec 2026-10-11 §1) against Pota's OWN rules: Fixtures/pass-rules.json is written by Pota's src/renkAvi.ts
    /// (Tools/potalook/pass_fixtures.ts, run from the Pota worktree). Unity must give the same squares, births and groups
    /// (eslesmeleriCoz), the same pass targets (pasHedefi), the same tap steps (kir: exploded, triggered, pass hits, births, drops, spawns)
    /// and the same boards — on Task 1/3's boards, on deals with the pass on (tahtaKur), on random games with the pass on and on the
    /// Specials scenarios Pass / PassBomb. Golden is off in the replays: Pota's golden ball now fires on birth, Unity's still waits
    /// for a gate (U25).
    /// </summary>
    public class PassRulesTests
    {
        public const string FixturePath = "_Project/Games/Pota/Tests/EditMode/Fixtures/pass-rules.json";

        // ---------- the file (JsonUtility: field names as written by pass_fixtures.ts) ----------

        [Serializable] public class FixtureFile { public int version; public string pota; public DesignDto design; public MatchDto[] matches; public TapDto[] taps; public DealDto[] deals; public GameDto[] replays, scenarios; public int passFlights; }
        [Serializable] public class DesignDto { public int size, colorCount, hpMin, hpMax, ammo; public ColorTargetDto[] targets; }
        [Serializable] public class ColorTargetDto { public int color, count; }
        [Serializable] public class FeaturesDto { public bool line, bomb, golden, pass; }
        [Serializable] public class CellsDto { public int color, cell; public int[] cells; }
        [Serializable] public class CellKindDto { public int cell, kind; }
        [Serializable] public class PassDto { public int cell, target; }
        [Serializable] public class TargetDto { public string kind; public int color, count; }
        [Serializable] public class MatchDto { public string name; public string[] rows; public FeaturesDto features; public CellsDto[] squares; public bool hasMatch; public int[] exploded; public CellKindDto[] born; public CellsDto[] groups; }
        [Serializable] public class ProbeDto { public int[] exclude; public bool noTargets; public int expect; }
        [Serializable] public class BoardDto { public int size; public int[] colors, hp, specials, targetsLeft; public int ammo; public long rng; }
        [Serializable] public class StepDto { public int[] exploded; public CellKindDto[] triggered, born; public PassDto[] passes; public DropDto[] drops; public SpawnDto[] spawns; }
        [Serializable] public class DropDto { public int from, to; }
        [Serializable] public class SpawnDto { public int to, color, hp, distance; }
        [Serializable] public class TapDto { public string name; public string[] rows; public int colorCount, ammo, tap, rotatedSteps, cost; public long rng; public FeaturesDto features; public TargetDto[] targets; public BoardDto start, rotated, after; public string rotate, result; public ProbeDto[] probes; public StepDto[] steps; }
        [Serializable] public class DealDto { public long seed; public FeaturesDto features; public BoardDto board; }
        [Serializable] public class MoveDto { public string kind, dir, result; public int cell, cost; public StepDto[] steps; public BoardDto after; }
        [Serializable] public class GameDto { public string name; public long seed; public int ammo; public FeaturesDto features; public TargetDto[] targets; public BoardDto start; public MoveDto[] moves; }

        private static FixtureFile _fixture;

        public static FixtureFile Fixture
        {
            get
            {
                if (_fixture != null) return _fixture;
                string path = Path.Combine(Application.dataPath, FixturePath);
                if (!File.Exists(path)) throw new FileNotFoundException($"{path} is missing: run Tools/potalook/pass_fixtures.ts from the Pota worktree");
                return _fixture = JsonUtility.FromJson<FixtureFile>(File.ReadAllText(path));
            }
        }

        public static IEnumerable<TestCaseData> Matches() { foreach (var m in Fixture.matches) yield return new TestCaseData(m.name).SetName("Match_" + m.name); }
        public static IEnumerable<TestCaseData> Taps() { foreach (var t in Fixture.taps) yield return new TestCaseData(t.name).SetName("Tap_" + t.name); }
        public static IEnumerable<TestCaseData> Deals() { foreach (var d in Fixture.deals) yield return new TestCaseData(d.seed).SetName("Deal_" + d.seed); }
        public static IEnumerable<TestCaseData> Replays() { foreach (var g in Fixture.replays) yield return new TestCaseData(g.name).SetName("Replay_" + g.name); }
        public static IEnumerable<TestCaseData> Scenarios() { foreach (var g in Fixture.scenarios) yield return new TestCaseData(g.name).SetName("Scenario_" + g.name); }

        // ---------- boards ----------

        private static Features ToFeatures(FeaturesDto f) => new Features { Line = f.line, Bomb = f.bomb, Golden = f.golden, Pass = f.pass };

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
        public static Board Rows(string[] rows, Features f, int colorCount = 5, uint rng = 1, TargetDto[] targets = null)
        {
            int n = rows.Length;
            var p = new BoardPreset { Size = n, ColorCount = colorCount, HpMin = 1, HpMax = 1, Ammo = 100, Rng = rng, Features = f,
                Colors = new int[n * n], Specials = new SpecialKind[n * n], Hp = new int[n * n], Targets = new (int, int)[0] };
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    char ch = rows[r][c];
                    var kind = Letter(ch);
                    int i = r * n + c;
                    p.Specials[i] = kind;
                    p.Colors[i] = kind != SpecialKind.None ? ColorHuntRules.SpecialColor : ch - '0';
                    p.Hp[i] = 1;
                }
            if (targets != null) ApplyTargets(p, targets);
            return ColorHuntRules.FromPreset(p);
        }

        /// <summary>Pota's targets in order: colour ones into Targets, bomba/cizgi into BombTarget/LineTarget.</summary>
        private static void ApplyTargets(BoardPreset p, TargetDto[] targets)
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

        /// <summary>The remaining counts in Pota's target order.</summary>
        private static int[] Left(Board b, TargetDto[] targets)
        {
            var left = new int[targets.Length];
            int k = 0;
            for (int i = 0; i < targets.Length; i++)
                left[i] = targets[i].kind == "renk" ? b.Targets[k++].Remaining : targets[i].kind == "bomba" ? b.BombTarget : b.LineTarget;
            return left;
        }

        private static LevelDesign Design(Features f) => new LevelDesign
        {
            Size = Fixture.design.size, ColorCount = Fixture.design.colorCount, HpMin = Fixture.design.hpMin, HpMax = Fixture.design.hpMax,
            Targets = Array.ConvertAll(Fixture.design.targets, t => (t.color, t.count)), Features = f,
        };

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

        private static void Same(StepDto want, Step got, string at)
        {
            CollectionAssert.AreEqual(want.exploded, got.Exploded, at + ": exploded");
            CollectionAssert.AreEqual(Kinds(want.triggered), got.Triggered.ConvertAll(t => $"{t.Cell}:{(int)t.Kind}"), at + ": triggered (in firing order)");
            CollectionAssert.AreEqual(Array.ConvertAll(want.passes, p => $"{p.cell}->{p.target}"), got.Passes.ConvertAll(p => $"{p.Cell}->{p.Target}"), at + ": pass hits");
            CollectionAssert.AreEqual(Kinds(want.born), got.Born.ConvertAll(b => $"{b.Cell}:{(int)b.Kind}"), at + ": born");
            CollectionAssert.AreEqual(Array.ConvertAll(want.drops, d => $"{d.from}>{d.to}"), got.Drops.ConvertAll(d => $"{d.From}>{d.To}"), at + ": drops");
            CollectionAssert.AreEqual(Array.ConvertAll(want.spawns, s => $"{s.to}:{s.color}/{s.hp}/{s.distance}"),
                got.Spawns.ConvertAll(s => $"{s.To}:{s.Block.Color}/{s.Block.Hp}/{s.Distance}"), at + ": spawns");
            Assert.IsEmpty(got.ToBasket, at + ": no basket");
            Assert.IsEmpty(got.Opened, at + ": no gate");
        }

        private static string[] Kinds(CellKindDto[] l) => Array.ConvertAll(l, k => $"{k.cell}:{k.kind}");

        private static string Outcome(BreakOutcome o) => o == BreakOutcome.Broken ? "kirildi" : o == BreakOutcome.Insufficient ? "yetmez" : "altin";

        private static void Play(MoveDto mv, ref Board board, string at, TargetDto[] targets)
        {
            List<Step> steps;
            if (mv.kind == "rotate")
            {
                var r = ColorHuntRules.RotateAndSettle(board, mv.dir == "saat" ? Rotation.Clockwise : Rotation.Counter);
                board = r.Board;
                steps = r.Steps;
            }
            else
            {
                var r = ColorHuntRules.Break(board, mv.cell);
                Assert.AreEqual(mv.result, Outcome(r.Outcome), at + ": outcome");
                Assert.AreEqual(mv.cost, r.Cost, at + ": cost");
                board = r.Board;
                steps = r.Steps;
            }
            Assert.AreEqual(mv.steps.Length, steps.Count, at + ": step count");
            for (int s = 0; s < steps.Count; s++) Same(mv.steps[s], steps[s], $"{at} step {s}");
            Same(mv.after, board, at + " after", targets);
        }

        // ---------- tests ----------

        [Test]
        public void Fixture_IsPotas_AndFliesPasses()
        {
            StringAssert.Contains("renkAvi.ts", Fixture.pota, "written by Pota's own rules (commit + renkAvi.ts hash)");
            Assert.Greater(Fixture.passFlights, 0, "the replays fly pass balls");
            Assert.AreEqual(11, Fixture.matches.Length);
            Assert.AreEqual(7, Fixture.taps.Length);
        }

        [TestCaseSource(nameof(Matches))]
        public void Match(string name)
        {
            var m = Array.Find(Fixture.matches, x => x.name == name);
            var b = Rows(m.rows, ToFeatures(m.features));
            var squares = ColorHuntRules.FindSquares(b);
            CollectionAssert.AreEqual(Array.ConvertAll(m.squares, q => $"{q.color}:{string.Join(",", q.cells)}"),
                squares.ConvertAll(q => $"{q.Color}:{string.Join(",", q.Cells)}"), name + ": kareleriBul");
            Assert.AreEqual(m.hasMatch, ColorHuntRules.HasMatch(b), name + ": eslesmeVarMi");
            var exploded = ColorHuntRules.ResolveMatches(b, out var born);
            CollectionAssert.AreEqual(m.exploded, exploded, name + ": patlayan");
            CollectionAssert.AreEqual(Kinds(m.born), born.ConvertAll(x => $"{x.Cell}:{(int)x.Kind}"), name + ": dogan");
            CollectionAssert.AreEqual(Array.ConvertAll(m.groups, g => $"{g.cell}:{string.Join(",", g.cells)}"),
                born.ConvertAll(x => $"{x.Cell}:{string.Join(",", x.Group)}"), name + ": doğum grupları");
        }

        [TestCaseSource(nameof(Taps))]
        public void Tap(string name)
        {
            var t = Array.Find(Fixture.taps, x => x.name == name);
            var b = Rows(t.rows, ToFeatures(t.features), t.colorCount, (uint)t.rng, t.targets);
            Same(t.start, b, name + " start", t.targets);
            if (!string.IsNullOrEmpty(t.rotate))
            {
                var r = ColorHuntRules.RotateAndSettle(b, t.rotate == "saat" ? Rotation.Clockwise : Rotation.Counter);
                Assert.AreEqual(t.rotatedSteps, r.Steps.Count, name + ": a turn makes no match");
                b = r.Board;
                Same(t.rotated, b, name + " turned", t.targets);
                Assert.AreEqual(SpecialKind.Pass, b.Cells[t.tap].Special, name + ": the pass ball turned with the board");
            }
            foreach (var p in t.probes)
            {
                var probe = b;
                if (p.noTargets) { probe = b.Clone(); probe.Targets = new Target[0]; probe.BombTarget = 0; probe.LineTarget = 0; }
                Assert.AreEqual(p.expect, ColorHuntRules.PassTarget(probe, t.tap, new HashSet<int>(p.exclude)), $"{name}: pasHedefi (targets {(p.noTargets ? "none" : "on")})");
            }
            var k = ColorHuntRules.Break(b, t.tap);
            Assert.AreEqual(t.result, Outcome(k.Outcome), name + ": outcome");
            Assert.AreEqual(t.cost, k.Cost, name + ": the pass ball is free");
            Assert.AreEqual(t.steps.Length, k.Steps.Count, name + ": step count");
            for (int s = 0; s < k.Steps.Count; s++) Same(t.steps[s], k.Steps[s], $"{name} step {s}");
            Same(t.after, k.Board, name + " after", t.targets);
        }

        [TestCaseSource(nameof(Deals))]
        public void Deal(long seed)
        {
            var d = Array.Find(Fixture.deals, x => x.seed == seed);
            var b = ColorHuntRules.Create(Design(ToFeatures(d.features)), Fixture.design.ammo, (uint)seed);
            Same(d.board, b, $"tahtaKur seed {seed} (pass on: no square)");
            Assert.IsFalse(ColorHuntRules.HasMatch(b));
        }

        [TestCaseSource(nameof(Replays))]
        public void Replay(string name)
        {
            var g = Array.Find(Fixture.replays, x => x.name == name);
            var board = ColorHuntRules.Create(Design(ToFeatures(g.features)), g.ammo, (uint)g.seed);
            Same(g.start, board, name + " start");
            for (int m = 0; m < g.moves.Length; m++)
            {
                var mv = g.moves[m];
                Play(mv, ref board, $"{name} move {m} ({mv.kind} {(mv.kind == "tap" ? mv.cell.ToString() : mv.dir)})", null);
                Assert.IsFalse(ColorHuntRules.HasMatch(board), $"{name} move {m}: settled boards keep no match or square");
            }
        }

        [TestCaseSource(nameof(Scenarios))]
        public void Scenario(string name)
        {
            var g = Array.Find(Fixture.scenarios, x => x.name == name);
            var s = name == "pass" ? SpecialScenario.Pass : SpecialScenario.PassBomb;
            var board = ColorHuntRules.FromPreset(SpecialScenarios.Board(s));
            Same(g.start, board, name + " start", g.targets);
            var taps = SpecialScenarios.Taps(s);
            Assert.AreEqual(g.moves.Length, taps.Length, name + ": the demo taps Pota's script");
            for (int m = 0; m < g.moves.Length; m++)
            {
                Assert.AreEqual(g.moves[m].cell, taps[m], $"{name} tap {m}");
                Play(g.moves[m], ref board, $"{name} tap {m} ({taps[m]})", g.targets);
            }
        }

        // ---------- the port's own invariants ----------

        [Test]
        public void PassOff_SquareIsNoMatch_AndDealsStayAsBefore()
        {
            var b = Rows(new[] { "00123", "00456", "78912", "45678", "12345" }, Features.All);
            Assert.IsFalse(ColorHuntRules.HasMatch(b));
            Assert.IsEmpty(ColorHuntRules.ResolveMatches(b, out var born));
            Assert.IsEmpty(born);
            Assert.IsFalse(Features.All.Pass, "OZELLIK_HEPSI: no pass ball (levels 1–8)");
            Assert.IsTrue(Features.Full.Pass && Features.Full.Line && Features.Full.Bomb && Features.Full.Golden, "OZELLIK_TAM");
        }

        [Test]
        public void SingleArea_OfAPass_IsItsCell()
        {
            CollectionAssert.AreEqual(new[] { 12 }, ColorHuntRules.SingleArea(5, 12, SpecialKind.Pass));
        }

        // ---------- the trigger plan (Pota ozelPlani, spec 2026-10-11 §3) ----------

        private const float Swell = 0.15f, Eps = 1e-4f;
        private static float Peak => Swell * PotaLook.SwellPeakAt;
        private static readonly SpecialTiming RoyalLike = new SpecialTiming { LineSpeed = 6f / 0.32f, LineLead = 0.06f, BombLead = 0.18f, BombRing = 0.04f };

        /// <summary>The scenario's tap on its pass ball: the rules' first step.</summary>
        private static Step PassStep(SpecialScenario s)
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(s));
            var taps = SpecialScenarios.Taps(s);
            for (int i = 0; i < taps.Length - 1; i++) b = ColorHuntRules.Break(b, taps[i]).Board;
            return ColorHuntRules.Break(b, taps[taps.Length - 1]).Steps[0];
        }

        [Test]
        public void Plan_NormalTarget_BurstsAtTheLanding_WithoutASwell([Values] PassLook look)
        {
            var step = PassStep(SpecialScenario.Pass);
            var timing = look == PassLook.A ? PassTiming.A : PassTiming.B;
            var plan = SpecialSchedule.Plan(7, step, 37, SpecialTiming.Pota, Swell, timing);
            Assert.AreEqual(1, plan.Fires.Count, "the pass ball alone");
            Assert.AreEqual(37, plan.Fires[0].Cell);
            Assert.AreEqual(Peak, plan.Fires[0].Time, Eps, "the tapped special fires at the first peak");
            Assert.AreEqual(1, plan.Hits.Count);
            Assert.AreEqual(Peak + timing.Arrival(7, 37, 22), plan.Hits[22], Eps, "the target goes the moment the ball lands");
            Assert.AreEqual(Peak + timing.Arrival(7, 37, 22) + Swell, plan.End, Eps, "the drops wait for the landing (Pota: süre = varış + şişme)");
        }

        [Test]
        public void Plan_SpecialTarget_IsARoot_FiringAtTheLanding_Uncompressed([Values] PassLook look)
        {
            var step = PassStep(SpecialScenario.PassBomb);
            var timing = look == PassLook.A ? PassTiming.A : PassTiming.B;
            var plan = SpecialSchedule.Plan(7, step, 29, RoyalLike, Swell, timing);
            Assert.AreEqual(2, plan.Fires.Count);
            Assert.AreEqual(29, plan.Fires[0].Cell);
            Assert.AreEqual(Peak, plan.Fires[0].Time, Eps);
            Assert.AreEqual(19, plan.Fires[1].Cell, "the bomb");
            float landing = Peak + timing.Arrival(7, 29, 19);
            Assert.AreEqual(landing, plan.Fires[1].Time, Eps, "the bomb fires the moment the ball lands, never earlier (altinBomba: no effect before its cause)");
            Assert.IsEmpty(plan.Hits, "a special target is a root, not a hit");
            foreach (int c in ColorHuntRules.SingleArea(7, 19, SpecialKind.Bomb))
                Assert.GreaterOrEqual(plan.Start[c] + Peak, landing - Eps, $"cell {c} of the bomb's 3×3 bursts after the landing");
            Assert.GreaterOrEqual(plan.End, landing);
        }

        [Test]
        public void Plan_WithoutPasses_IsTheOldPlan()
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(SpecialScenario.Chain));
            var step = ColorHuntRules.Break(b, 21).Steps[0];
            var a = SpecialSchedule.Plan(7, step, 21, RoyalLike, Swell);
            var c = SpecialSchedule.Plan(7, step, 21, RoyalLike, Swell, PassTiming.B);
            Assert.AreEqual(a.End, c.End);
            Assert.AreEqual(a.Pace, c.Pace);
            Assert.IsEmpty(c.Hits);
            for (int i = 0; i < a.Fires.Count; i++) Assert.AreEqual(a.Fires[i].Time, c.Fires[i].Time);
        }

        [Test]
        public void PassArrival_IsPotasPasVarisi()
        {
            // ozelPlan.ts pasVarisi: kalkış + clamp(öklid / hız, enAz, enÇok) + asılı. A: 0.1 + [0.2, 0.45] (14 cells/s); B: 0.2 + [0.25, 0.5] (12) + 0.08.
            var a = PassTiming.A;
            var b = PassTiming.B;
            Assert.AreEqual(0.1f + 0.2f, a.Arrival(7, 37, 22), 1e-5f, "√5 cells: the minimum flight");
            Assert.AreEqual(0.1f + Mathf.Sqrt(20f) / 14f, a.Arrival(7, 29, 19), 1e-5f, "√20 cells");
            Assert.AreEqual(0.2f + 0.25f + 0.08f, b.Arrival(7, 37, 22), 1e-5f);
            Assert.AreEqual(0.2f + Mathf.Sqrt(20f) / 12f + 0.08f, b.Arrival(7, 29, 19), 1e-5f);
            Assert.AreEqual(0.1f + 0.45f, a.Arrival(7, 0, 48), 1e-5f, "long flights are clipped");
        }
    }
}
