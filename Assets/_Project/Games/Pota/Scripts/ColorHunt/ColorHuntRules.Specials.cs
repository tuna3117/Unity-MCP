using System;
using System.Collections.Generic;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's special balls (OzelTur). LineV = Pota cizgiDikey: clears its COLUMN (born from a horizontal run of 4). Pass = Pota pasTopu
    /// (spec 2026-10-11): born from a 2×2 square, it flies to ONE target and hits it like a golden ball would.
    /// </summary>
    public enum SpecialKind { None = 0, LineV = 1, LineH = 2, Bomb = 3, Golden = 4, Pass = 5 }

    /// <summary>Which specials a level gives birth to (Pota Ozellikler). A closed feature skips its birth step.</summary>
    [Serializable]
    public struct Features
    {
        public bool Line, Bomb, Golden;
        /// <summary>Pota pasTopu: a 2×2 square of one colour is a match (and the deal makes none). Off: a square is nothing, as before.</summary>
        public bool Pass;
        public bool Any => Line || Bomb || Golden || Pass;
        public static readonly Features None = default;
        /// <summary>Pota OZELLIK_HEPSI: line, bomb and golden — no pass ball (levels 1–8).</summary>
        public static readonly Features All = new Features { Line = true, Bomb = true, Golden = true };
        /// <summary>Pota OZELLIK_TAM: all four (levels 9–50).</summary>
        public static readonly Features Full = new Features { Line = true, Bomb = true, Golden = true, Pass = true };
    }

    /// <summary>A 2×2 square of one colour (Pota Kare): Cells = top-left, top-right, bottom-left, bottom-right; free normal blocks only.</summary>
    public struct Square { public int Color; public int[] Cells; }

    /// <summary>A pass ball's flight in a step (Pota PasVurusu): its cell and the cell it hits, step-start indices; Target −1 = nothing to hit.</summary>
    public struct PassHit { public int Cell; public int Target; }

    /// <summary>A special born in a step (Pota Dogan). <see cref="Group"/> is for the look only (not in Pota).</summary>
    public struct Born
    {
        public int Cell;
        public SpecialKind Kind;
        /// <summary>The exploded cells this birth gathers, sorted, birth cells excluded; groups of one step never share a cell.</summary>
        public int[] Group;
    }

    /// <summary>A special fired by a tap (Pota tetiklenen).</summary>
    public struct Trigger { public int Cell; public SpecialKind Kind; }

    /// <summary>A golden ball leaving bottom-row <see cref="Cell"/> through the gate of column <see cref="Col"/> (Pota sepete).</summary>
    public struct BasketDrop { public int Cell; public int Col; }

    /// <summary>Pota KirmaSonucu without "engel": kirildi, yetmez, altin.</summary>
    public enum BreakOutcome { Broken, Insufficient, Golden }

    /// <summary>Pota dondurVeOtur: <see cref="Rotated"/> is the turned board (the views are built from it), <see cref="Board"/>
    /// the settled one, <see cref="Steps"/> play after the turn.</summary>
    public struct RotateResult { public Board Rotated; public Board Board; public List<Step> Steps; }

    /// <summary>Pota patlamaAlani without ice: the cells a fired special clears and every special it sets off, both sorted.</summary>
    public struct BlastArea { public List<int> Cells; public List<Trigger> Triggered; }

    public static partial class ColorHuntRules
    {
        /// <summary>Pota OZEL_RENK: a special block's colour. It joins no run and counts for no target.</summary>
        public const int SpecialColor = -1;

        // ---------- birth (Pota eslesmeleriCoz, spec §1.2) ----------

        /// <summary>
        /// Pota eslesmeleriCoz: runs and (Pass on) 2×2 squares sharing a cell form one group, one special per group. 1) a run of ≥5
        /// (Golden on) → golden at that run's birth cell, the rest of the group explodes; 2) else runs that CROSS (Bomb on) → bomb at the
        /// smallest intersection cell (parallel runs joined only by a square do not cross: the step is skipped); 3) else every run of
        /// exactly 4 (Line on) → a line at its birth cell (horizontal run → LineV at its second cell, vertical run → LineH at its bottom
        /// cell); 4) no special born yet and a square in the group (Pass on) → ONE pass ball at the bottom-most square's bottom-left cell
        /// (the left one on a tie). A closed feature skips its step. Birth cells do not explode. Without squares this is the old rule
        /// exactly (two runs of one group always cross). Returns the exploded cells sorted; <paramref name="born"/> is sorted by cell.
        /// </summary>
        public static List<int> ResolveMatches(Board b, out List<Born> born)
        {
            var f = b.Features;
            int n = b.Size;
            var exploded = new SortedSet<int>();
            var births = new SortedDictionary<int, (SpecialKind kind, List<int> cells)>();
            void Birth(int cell, SpecialKind kind, List<int> cells)
            {
                // Pota keeps the last kind set on a cell (two crossing 4-runs with the bomb closed); the look gathers both runs.
                if (births.TryGetValue(cell, out var old))
                    foreach (int c in old.cells) if (!cells.Contains(c)) cells.Add(c);
                births[cell] = (kind, cells);
            }
            foreach (var shapes in GroupShapes(FindMatches(b), f.Pass ? FindSquares(b) : new List<Square>()))
            {
                var group = shapes.Runs;
                var groupCells = new List<int>();
                foreach (var m in group)
                    foreach (int c in m.Cells)
                    {
                        exploded.Add(c);
                        if (!groupCells.Contains(c)) groupCells.Add(c);
                    }
                foreach (var q in shapes.Squares)
                    foreach (int c in q.Cells)
                    {
                        exploded.Add(c);
                        if (!groupCells.Contains(c)) groupCells.Add(c);
                    }
                Match five = null;
                if (f.Golden) foreach (var m in group) if (m.Cells.Count >= 5) { five = m; break; }
                if (five != null) { Birth(BirthCell(five), SpecialKind.Golden, groupCells); continue; }
                if (group.Count >= 2 && f.Bomb)
                {
                    int best = -1;
                    for (int x = 0; x < group.Count; x++)
                        for (int y = x + 1; y < group.Count; y++)
                        {
                            int h = Intersection(group[x], group[y]);
                            if (h >= 0 && (best < 0 || h < best)) best = h;
                        }
                    if (best >= 0) { Birth(best, SpecialKind.Bomb, groupCells); continue; }
                }
                bool line = false;
                if (f.Line)
                    foreach (var m in group)
                        if (m.Cells.Count == 4)
                        {
                            Birth(BirthCell(m), m.Horizontal ? SpecialKind.LineV : SpecialKind.LineH, new List<int>(m.Cells));
                            line = true;
                        }
                if (!line && f.Pass && shapes.Squares.Count > 0) Birth(SquareBirthCell(n, shapes.Squares), SpecialKind.Pass, groupCells);
            }
            foreach (int c in births.Keys) exploded.Remove(c);
            born = new List<Born>();
            var taken = new HashSet<int>(births.Keys);
            foreach (var kv in births)
            {
                var group = new List<int>();
                foreach (int c in kv.Value.cells) if (taken.Add(c)) group.Add(c);
                group.Sort();
                born.Add(new Born { Cell = kv.Key, Kind = kv.Value.kind, Group = group.ToArray() });
            }
            return new List<int>(exploded);
        }

        /// <summary>Pota dogumYeri: a vertical run's bottom cell, a horizontal run's second cell from the left.</summary>
        private static int BirthCell(Match m) => m.Horizontal ? m.Cells[1] : m.Cells[m.Cells.Count - 1];

        /// <summary>Pota kesisiyor: the cell two crossing runs of one colour share, −1 if they do not cross.</summary>
        private static int Intersection(Match a, Match b)
        {
            if (a.Color != b.Color || a.Horizontal == b.Horizontal) return -1;
            foreach (int c in a.Cells) if (b.Cells.Contains(c)) return c;
            return -1;
        }

        /// <summary>Pota kareDogumYeri: the bottom-most square's bottom-left cell, the left one on a tie.</summary>
        private static int SquareBirthCell(int n, List<Square> squares)
        {
            int best = -1;
            foreach (var q in squares)
            {
                int c = q.Cells[2];
                if (best < 0 || Row(n, c) > Row(n, best) || (Row(n, c) == Row(n, best) && Col(n, c) < Col(n, best))) best = c;
            }
            return best;
        }

        private sealed class ShapeGroup
        {
            public readonly List<Match> Runs = new List<Match>();
            public readonly List<Square> Squares = new List<Square>();
        }

        /// <summary>
        /// Pota gruplaSekiller: runs and squares sharing a cell are one group (union–find over runs first, then squares); groups come in the
        /// order of their first shape, their runs in run order. Without squares: Pota gruplaKosular, as before.
        /// </summary>
        private static List<ShapeGroup> GroupShapes(List<Match> runs, List<Square> squares)
        {
            int total = runs.Count + squares.Count;
            var parent = new int[total];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            var owner = new Dictionary<int, int>();
            for (int i = 0; i < total; i++)
                foreach (int c in i < runs.Count ? (IEnumerable<int>)runs[i].Cells : squares[i - runs.Count].Cells)
                {
                    if (!owner.TryGetValue(c, out int o)) { owner[c] = i; continue; }
                    int ra = Find(o), rb = Find(i);
                    if (ra != rb) parent[ra] = rb;
                }
            var groups = new List<ShapeGroup>();
            var byRoot = new Dictionary<int, ShapeGroup>();
            for (int i = 0; i < total; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out var g)) { g = new ShapeGroup(); byRoot[root] = g; groups.Add(g); }
                if (i < runs.Count) g.Runs.Add(runs[i]);
                else g.Squares.Add(squares[i - runs.Count]);
            }
            return groups;
        }

        /// <summary>
        /// Pota kareleriBul: every 2×2 of four free normal blocks of one colour (specials join none), scanned row by row from the top-left.
        /// Squares may overlap (a 2×3 block is two squares). Only a Pass level calls it.
        /// </summary>
        public static List<Square> FindSquares(Board b)
        {
            int n = b.Size;
            var list = new List<Square>();
            int ColorAt(int i) => b.Cells[i].IsSpecial ? SpecialColor : b.Cells[i].Color;
            for (int r = 0; r < n - 1; r++)
                for (int c = 0; c < n - 1; c++)
                {
                    int a = Index(n, r, c), color = ColorAt(a);
                    if (color < 0) continue;
                    if (ColorAt(a + 1) == color && ColorAt(a + n) == color && ColorAt(a + n + 1) == color)
                        list.Add(new Square { Color = color, Cells = new[] { a, a + 1, a + n, a + n + 1 } });
                }
            return list;
        }

        /// <summary>Pota eslesmeVarMi: a run of 3+, or (Pass on) a 2×2 square.</summary>
        public static bool HasMatch(Board b) => FindMatches(b).Count > 0 || (b.Features.Pass && FindSquares(b).Count > 0);

        // ---------- firing (Pota tekAlan, patlamaAlani, spec §1.3) ----------

        /// <summary>Pota tekAlan: the cells one special covers on its own, clipped at the edges (LineV its column, LineH its row,
        /// Bomb its 3×3, Pass its own cell — its flight is ExplodeAndFill's). Golden and None cover nothing.</summary>
        public static List<int> SingleArea(int n, int cell, SpecialKind kind)
        {
            int r = Row(n, cell), c = Col(n, cell);
            var list = new List<int>();
            if (kind == SpecialKind.LineV) for (int rr = 0; rr < n; rr++) list.Add(Index(n, rr, c));
            else if (kind == SpecialKind.LineH) for (int cc = 0; cc < n; cc++) list.Add(Index(n, r, cc));
            else if (kind == SpecialKind.Bomb)
                for (int rr = Math.Max(0, r - 1); rr <= Math.Min(n - 1, r + 1); rr++)
                    for (int cc = Math.Max(0, c - 1); cc <= Math.Min(n - 1, c + 1); cc++) list.Add(Index(n, rr, cc));
            else if (kind == SpecialKind.Pass) list.Add(cell);
            return list;
        }

        /// <summary>
        /// Pota patlamaAlani (no ice): the area of the special at <paramref name="cell"/>, grown by every line or bomb inside it
        /// (each fires once). Golden balls are left out. Empty when <paramref name="cell"/> is not a line or a bomb.
        /// </summary>
        public static BlastArea Blast(Board b, int cell) => Blast(b.Size, i => b.Cells[i], cell);

        /// <summary>Blast over a step's working cells: <paramref name="at"/> gives null for an empty (or hidden) cell, which is skipped.</summary>
        private static BlastArea Blast(int n, Func<int, Block?> at, int cell)
        {
            var cells = new SortedSet<int>();
            var fired = new SortedDictionary<int, SpecialKind>();
            var queue = new Queue<int>();
            queue.Enqueue(cell);
            while (queue.Count > 0)
            {
                int h = queue.Dequeue();
                if (h < 0 || h >= n * n) continue;
                var block = at(h);
                if (block == null) continue;
                var kind = block.Value.Special;
                if (kind == SpecialKind.None || kind == SpecialKind.Golden || fired.ContainsKey(h)) continue;
                fired[h] = kind;
                foreach (int i in SingleArea(n, h, kind))
                {
                    var bi = at(i);
                    if (bi == null) continue;
                    var k = bi.Value.Special;
                    if (k == SpecialKind.Golden) continue;
                    cells.Add(i);
                    if (k != SpecialKind.None && !fired.ContainsKey(i)) queue.Enqueue(i);
                }
            }
            var area = new BlastArea { Cells = new List<int>(cells), Triggered = new List<Trigger>() };
            foreach (var kv in fired) area.Triggered.Add(new Trigger { Cell = kv.Key, Kind = kv.Value });
            return area;
        }

        // ---------- the pass ball's target (Pota pasHedefi, hedefteMi; spec 2026-10-11 §1.4) ----------

        /// <summary>
        /// Pota hedefteMi without obstacles: <paramref name="block"/> fits a target with something left — a colour target (a normal block
        /// of that colour), the bomb target (a bomb), the line target (a line ball).
        /// </summary>
        public static bool InTarget(Block block, Target[] targets, int bombTarget, int lineTarget)
        {
            if (block.Special == SpecialKind.Bomb) return bombTarget > 0;
            if (block.Special == SpecialKind.LineV || block.Special == SpecialKind.LineH) return lineTarget > 0;
            if (block.IsSpecial || targets == null) return false;
            foreach (var t in targets) if (t.Remaining > 0 && t.Color == block.Color) return true;
            return false;
        }

        /// <summary>
        /// Pota pasHedefi: the cell a pass ball at <paramref name="cell"/> flies to — over the WHOLE board, a block in a target first, then the
        /// smallest Chebyshev distance, then the smallest index; with nothing in a target, the nearest normal block the same way. Never the
        /// ball itself, an <paramref name="exclude"/>d cell or a golden ball (Unity has no obstacles yet). −1: nothing to hit. Pure, no rng.
        /// </summary>
        public static int PassTarget(Board b, int cell, ICollection<int> exclude)
        {
            var cells = new Block?[b.Cells.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = b.Cells[i];
            return PassTarget(b.Size, cells, b.Targets, b.BombTarget, b.LineTarget, cell, exclude);
        }

        private static int PassTarget(int n, Block?[] cells, Target[] targets, int bombTarget, int lineTarget, int cell, ICollection<int> exclude)
        {
            int r0 = Row(n, cell), c0 = Col(n, cell);
            int best = -1, bestD = 0;
            bool bestIn = false;
            for (int i = 0; i < cells.Length; i++)
            {
                var block = cells[i];
                if (block == null || i == cell || exclude.Contains(i) || block.Value.Special == SpecialKind.Golden) continue;
                bool inTarget = InTarget(block.Value, targets, bombTarget, lineTarget);
                if (!inTarget && block.Value.IsSpecial) continue;
                int d = Math.Max(Math.Abs(Row(n, i) - r0), Math.Abs(Col(n, i) - c0));
                if (best < 0 || (inTarget && !bestIn) || (inTarget == bestIn && d < bestD)) { best = i; bestD = d; bestIn = inTarget; }
            }
            return best;
        }

        /// <summary>Pota hedefDus for a fired special: a bomb counts for the bomb target, a line ball for the line target; nothing else.</summary>
        private static void CountSpecialTarget(Board b, SpecialKind kind)
        {
            if (kind == SpecialKind.Bomb) b.BombTarget = Math.Max(0, b.BombTarget - 1);
            else if (kind == SpecialKind.LineV || kind == SpecialKind.LineH) b.LineTarget = Math.Max(0, b.LineTarget - 1);
        }

        /// <summary>
        /// Pota patlatVeDoldur's pass waves (spec 2026-10-11 §1.4), after the step's own explosions and births: the fired pass balls fly in
        /// cell order, each to <see cref="PassTarget"/> over the working cells and the targets as counted so far, never to a cell the step
        /// already took (its explosions, births, earlier hits and their areas). The hit is a golden ball's: a normal block explodes (and
        /// counts), a special fires — its area (the excluded cells hidden) explodes at once, a pass ball in it flies in the next wave.
        /// Returns the exploded cells added; <paramref name="hits"/> and <paramref name="fired"/> get the flights and the specials set off.
        /// </summary>
        private static List<int> FlyPasses(Board result, Block?[] temp, List<int> passes, HashSet<int> exclude, List<PassHit> hits, List<Trigger> fired)
        {
            int n = result.Size;
            var added = new List<int>();
            void Explode(int i)
            {
                var block = temp[i];
                if (block == null) return;
                CountTarget(result, block.Value.Color);           // a special (colour −1) counts for no colour target
                temp[i] = null;
                added.Add(i);
            }
            passes.Sort();
            while (passes.Count > 0)
            {
                var next = new List<int>();
                foreach (int h in passes)
                {
                    exclude.Add(h);
                    int v = PassTarget(n, temp, result.Targets, result.BombTarget, result.LineTarget, h, exclude);
                    hits.Add(new PassHit { Cell = h, Target = v });
                    if (v < 0) continue;
                    exclude.Add(v);
                    if (!temp[v].Value.IsSpecial) { Explode(v); continue; }
                    int hit = v;
                    var area = Blast(n, i => i != hit && exclude.Contains(i) ? null : temp[i], v);
                    foreach (var t in area.Triggered)
                    {
                        fired.Add(t);
                        CountSpecialTarget(result, t.Kind);
                        if (t.Kind == SpecialKind.Pass) next.Add(t.Cell);
                    }
                    foreach (int i in area.Cells) { exclude.Add(i); Explode(i); }
                }
                next.Sort();
                passes = next;
            }
            return added;
        }

        // ---------- gates and the basket (Pota sepeteDusur, dondurVeOtur, spec §1.4) ----------

        /// <summary>
        /// Pota sepeteDusur: every golden ball on the bottom row whose column has an open gate goes into the basket (columns in
        /// ascending order), in one step. False (and <paramref name="after"/> = <paramref name="b"/>) when none can.
        /// </summary>
        public static bool TryDropToBasket(Board b, out Board after, out Step step)
        {
            int n = b.Size;
            var drops = new List<BasketDrop>();
            var cols = new List<int>(b.Gates);
            cols.Sort();
            foreach (int c in cols)
            {
                int i = Index(n, n - 1, c);
                if (b.Cells[i].Special == SpecialKind.Golden) drops.Add(new BasketDrop { Cell = i, Col = c });
            }
            if (drops.Count == 0) { after = b; step = null; return false; }
            after = ExplodeAndFill(b, new List<int>(), null, drops, null, out step);
            return true;
        }

        /// <summary>
        /// Pota dondurVeOtur: a turn makes no run (the board has none and specials join none) but can bring a golden ball onto an
        /// open gate: the turned board is settled with <see cref="ResolveChains"/>. Gates stay on the frame.
        /// </summary>
        public static RotateResult RotateAndSettle(Board b, Rotation dir)
        {
            var rotated = Rotate(b, dir);
            var steps = new List<Step>();
            var settled = ResolveChains(rotated, steps);
            return new RotateResult { Rotated = rotated, Board = settled, Steps = steps };
        }

        // ---------- presets (demo, tests, spec §1.3) ----------

        /// <summary>
        /// A hand-authored board (demo presets, tests): colours row by row (row 0 at the top), hp (null: HpMin + 7·i mod the span), a
        /// special where Specials says so (its colour and hp are ignored), the open gates, the rng state. Nothing is resolved: a preset
        /// must start without runs.
        /// </summary>
        public static Board FromPreset(BoardPreset p)
        {
            int n = p.Size, count = n * n;
            if (p.Colors == null || p.Colors.Length != count) throw new ArgumentException($"BoardPreset.Colors needs {count} entries", nameof(p));
            int span = Math.Max(1, p.HpMax - p.HpMin + 1);
            var cells = new Block[count];
            for (int i = 0; i < count; i++)
            {
                var kind = p.Specials != null && i < p.Specials.Length ? p.Specials[i] : SpecialKind.None;
                if (kind != SpecialKind.None) { cells[i] = Block.MakeSpecial(kind); continue; }
                int hp = p.Hp != null && i < p.Hp.Length ? p.Hp[i] : p.HpMin + i * 7 % span;
                cells[i] = new Block(p.Colors[i], hp);
            }
            var targets = new Target[p.Targets.Length];
            for (int t = 0; t < targets.Length; t++) targets[t] = new Target(p.Targets[t].color, p.Targets[t].count);
            var board = new Board
            {
                Size = n, Cells = cells, Ammo = p.Ammo, Targets = targets, ColorCount = p.ColorCount, HpMin = p.HpMin, HpMax = p.HpMax,
                Rng = p.Rng, Features = p.Features, BombTarget = p.BombTarget, LineTarget = p.LineTarget,
            };
            if (p.Gates != null) board.Gates.AddRange(p.Gates);
            return board;
        }
    }
}
