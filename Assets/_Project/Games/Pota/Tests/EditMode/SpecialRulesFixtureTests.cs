using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>
    /// Replays games written by Pota's OWN rules (Tools/potalook/run_fixtures.sh runs special_fixtures.ts on a scratch copy of
    /// Pota's src/renkAvi.ts): after every move Unity's board and every step must equal Pota's, field by field. Features-off games
    /// pin today's level (rng use unchanged); the others pin births, triggers, chains, gates, the basket and rotate-and-settle.
    /// Born is compared on Cell and Kind (Group is Unity's own, for the look).
    /// </summary>
    public class SpecialRulesFixtureTests
    {
        public const string FixturePath = "_Project/Games/Pota/Tests/EditMode/Fixtures/special-rules.json";

        // ---------- the file (JsonUtility: field names as written by special_fixtures.ts) ----------

        [Serializable] public class FixtureFile { public int version; public string pota; public DesignDto design; public GameDto[] games; }
        [Serializable] public class DesignDto { public int size, colorCount, hpMin, hpMax, ammo; public TargetDto[] targets; }
        [Serializable] public class TargetDto { public int color, remaining; }
        [Serializable] public class FeaturesDto { public bool line, bomb, golden; }
        [Serializable] public class GameDto { public string name; public long seed; public FeaturesDto features; public bool dealt; public BoardDto start; public MoveDto[] moves; }
        [Serializable] public class BoardDto { public int size; public int[] colors, hp, specials; public int ammo; public long rng; public int[] gates; public int golden; public TargetDto[] targets; }
        [Serializable] public class MoveDto { public string kind; public int cell; public string dir; public string result; public int cost; public BoardDto rotated, after; public StepDto[] steps; }
        [Serializable] public class StepDto { public int[] exploded; public DropDto[] drops; public SpawnDto[] spawns; public CellKindDto[] born, triggered; public BasketDto[] toBasket; public int[] opened; }
        [Serializable] public class DropDto { public int from, to; }
        [Serializable] public class SpawnDto { public int to, color, hp, distance; }
        [Serializable] public class CellKindDto { public int cell, kind; }
        [Serializable] public class BasketDto { public int cell, col; }

        private static FixtureFile _fixture;

        public static FixtureFile Fixture
        {
            get
            {
                if (_fixture != null) return _fixture;
                string path = Path.Combine(Application.dataPath, FixturePath);
                if (!File.Exists(path)) throw new FileNotFoundException($"{path} is missing: run Tools/potalook/run_fixtures.sh <scratch>");
                return _fixture = JsonUtility.FromJson<FixtureFile>(File.ReadAllText(path));
            }
        }

        public static IEnumerable<TestCaseData> Games()
        {
            foreach (var g in Fixture.games) yield return new TestCaseData(g.name).SetName("Replay_" + g.name);
        }

        // ---------- tests ----------

        [TestCaseSource(nameof(Games))]
        public void Replay(string name)
        {
            var game = Array.Find(Fixture.games, g => g.name == name);
            var features = new Features { Line = game.features.line, Bomb = game.features.bomb, Golden = game.features.golden };
            var board = ToBoard(game.start, features);
            if (game.dealt) Same(game.start, ColorHuntRules.Create(Design(features), Fixture.design.ammo, (uint)game.seed), $"{name} start (Create, seed {game.seed})");
            for (int m = 0; m < game.moves.Length; m++)
            {
                var mv = game.moves[m];
                string at = $"{name} move {m} ({mv.kind} {(mv.kind == "tap" ? mv.cell.ToString() : mv.dir)})";
                List<Step> steps;
                if (mv.kind == "rotate")
                {
                    var r = ColorHuntRules.RotateAndSettle(board, mv.dir == "saat" ? Rotation.Clockwise : Rotation.Counter);
                    Same(mv.rotated, r.Rotated, at + " turned board");
                    board = r.Board;
                    steps = r.Steps;
                }
                else
                {
                    var r = ColorHuntRules.Break(board, mv.cell);
                    Assert.AreEqual(mv.result, Outcome(r.Outcome), at + ": outcome");
                    Assert.AreEqual(r.Outcome == BreakOutcome.Broken, r.Broken, at + ": Broken matches Outcome");
                    Assert.AreEqual(mv.cost, r.Cost, at + ": cost");
                    board = r.Board;
                    steps = r.Steps;
                }
                int common = Math.Min(mv.steps.Length, steps.Count);
                for (int s = 0; s < common; s++)
                {
                    string d = StepDiff(mv.steps[s], steps[s]);
                    if (d != null) Assert.Fail($"{at} step {s}: {d}");
                }
                Assert.AreEqual(mv.steps.Length, steps.Count, at + ": step count");
                Same(mv.after, board, at + " board after");
            }
        }

        [Test]
        public void Fixture_CoversEverySpecialPath()
        {
            var count = new Dictionary<string, int>();
            void Add(string key, int n = 1) => count[key] = (count.TryGetValue(key, out int old) ? old : 0) + n;
            foreach (var g in Fixture.games)
            {
                if (!g.features.line && !g.features.bomb && !g.features.golden) Add("off games");
                foreach (var m in g.moves)
                {
                    Add(m.kind == "tap" ? m.result : "rotate");
                    bool landed = false;
                    foreach (var s in m.steps)
                    {
                        foreach (var b in s.born) Add("born " + (SpecialKind)b.kind);
                        foreach (var t in s.triggered) Add("triggered " + (SpecialKind)t.kind);
                        if (s.triggered.Length >= 2) Add("chained");
                        Add("basket", s.toBasket.Length);
                        Add("opened", s.opened.Length);
                        landed |= s.toBasket.Length > 0;
                    }
                    if (m.kind == "rotate" && landed) Add("rotation into the basket");
                }
            }
            Assert.GreaterOrEqual(Get(count, "off games"), 4, "features-off games (today's level)");
            foreach (var key in new[] { "born LineV", "born LineH", "born Bomb", "born Golden", "triggered LineV", "triggered LineH",
                                        "triggered Bomb", "chained", "basket", "opened", "rotation into the basket", "altin",
                                        "yetmez", "kirildi", "rotate" })
                Assert.GreaterOrEqual(Get(count, key), 5, key);
        }

        // ---------- helpers ----------

        private static int Get(Dictionary<string, int> count, string key) => count.TryGetValue(key, out int n) ? n : 0;

        /// <summary>Pota KirmaSonucu.</summary>
        private static string Outcome(BreakOutcome o) => o == BreakOutcome.Broken ? "kirildi" : o == BreakOutcome.Golden ? "altin" : "yetmez";

        private static LevelDesign Design(Features features)
        {
            var d = Fixture.design;
            var targets = new (int color, int count)[d.targets.Length];
            for (int i = 0; i < targets.Length; i++) targets[i] = (d.targets[i].color, d.targets[i].remaining);
            return new LevelDesign { Size = d.size, ColorCount = d.colorCount, HpMin = d.hpMin, HpMax = d.hpMax, Targets = targets, Features = features };
        }

        private static Board ToBoard(BoardDto w, Features features)
        {
            var cells = new Block[w.colors.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = new Block(w.colors[i], w.hp[i]) { Special = (SpecialKind)w.specials[i] };
            var targets = new Target[w.targets.Length];
            for (int i = 0; i < targets.Length; i++) targets[i] = new Target(w.targets[i].color, w.targets[i].remaining);
            var d = Fixture.design;
            return new Board
            {
                Size = w.size, Cells = cells, Ammo = w.ammo, Targets = targets, ColorCount = d.colorCount, HpMin = d.hpMin, HpMax = d.hpMax,
                Rng = (uint)w.rng, Features = features, Gates = new List<int>(w.gates), Golden = w.golden,
            };
        }

        private static void Same(BoardDto want, Board got, string at)
        {
            string d = BoardDiff(want, got);
            if (d != null) Assert.Fail($"{at}: {d}");
        }

        /// <summary>The first field where Unity's board differs from Pota's, null when equal.</summary>
        private static string BoardDiff(BoardDto w, Board g)
        {
            if (w.size != g.Size || w.colors.Length != g.Cells.Length) return $"size: Pota {w.size}, Unity {g.Size}";
            for (int i = 0; i < g.Cells.Length; i++)
            {
                var c = g.Cells[i];
                if (w.specials[i] != (int)c.Special) return $"cell {i} special: Pota {(SpecialKind)w.specials[i]}, Unity {c.Special}";
                if (w.colors[i] != c.Color) return $"cell {i} colour: Pota {w.colors[i]}, Unity {c.Color}";
                if (w.hp[i] != c.Hp) return $"cell {i} hp: Pota {w.hp[i]}, Unity {c.Hp}";
            }
            if (w.ammo != g.Ammo) return $"ammo: Pota {w.ammo}, Unity {g.Ammo}";
            if ((uint)w.rng != g.Rng) return $"rng: Pota {(uint)w.rng}, Unity {g.Rng}";
            if (w.golden != g.Golden) return $"golden in the basket: Pota {w.golden}, Unity {g.Golden}";
            string gates = string.Join(",", w.gates), mine = string.Join(",", g.Gates);
            if (gates != mine) return $"gates: Pota [{gates}], Unity [{mine}]";
            if (w.targets.Length != g.Targets.Length) return $"targets: Pota {w.targets.Length}, Unity {g.Targets.Length}";
            for (int t = 0; t < g.Targets.Length; t++)
                if (w.targets[t].color != g.Targets[t].Color || w.targets[t].remaining != g.Targets[t].Remaining)
                    return $"target {t}: Pota {w.targets[t].color}×{w.targets[t].remaining}, Unity {g.Targets[t].Color}×{g.Targets[t].Remaining}";
            return null;
        }

        /// <summary>The first list where Unity's step differs from Pota's, null when equal.</summary>
        private static string StepDiff(StepDto w, Step g)
        {
            return Diff("exploded", Join(w.exploded, x => x.ToString()), Join(g.Exploded, x => x.ToString()))
                ?? Diff("drops", Join(w.drops, x => $"{x.from}>{x.to}"), Join(g.Drops, x => $"{x.From}>{x.To}"))
                ?? Diff("spawns", Join(w.spawns, x => $"{x.to}:{x.color}/{x.hp}/{x.distance}"),
                        Join(g.Spawns, x => $"{x.To}:{x.Block.Color}/{x.Block.Hp}/{x.Distance}"))
                ?? Diff("born", Join(w.born, x => $"{x.cell}:{(SpecialKind)x.kind}"), Join(g.Born, x => $"{x.Cell}:{x.Kind}"))
                ?? Diff("triggered", Join(w.triggered, x => $"{x.cell}:{(SpecialKind)x.kind}"), Join(g.Triggered, x => $"{x.Cell}:{x.Kind}"))
                ?? Diff("toBasket", Join(w.toBasket, x => $"{x.cell}@{x.col}"), Join(g.ToBasket, x => $"{x.Cell}@{x.Col}"))
                ?? Diff("opened", Join(w.opened, x => x.ToString()), Join(g.Opened, x => x.ToString()));
        }

        private static string Diff(string field, string pota, string unity) => pota == unity ? null : $"{field}: Pota [{pota}], Unity [{unity}]";

        private static string Join<T>(IEnumerable<T> items, Func<T, string> f)
        {
            var parts = new List<string>();
            if (items != null) foreach (var x in items) parts.Add(f(x));
            return string.Join(" ", parts);
        }
    }
}
