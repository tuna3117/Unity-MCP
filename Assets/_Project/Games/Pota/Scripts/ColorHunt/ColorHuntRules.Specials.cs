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
        /// <summary>
        /// Pota kombo (spec 2026-10-13 K1–K5): a tapped line / bomb / pass ball with another one in its four neighbours merges with it
        /// (<see cref="ColorHuntRules.FindComboPartner"/>). Not a special of its own: <see cref="Any"/> ignores it.
        /// </summary>
        public bool Combo;
        public bool Any => Line || Bomb || Golden || Pass;
        public static readonly Features None = default;
        /// <summary>Pota OZELLIK_HEPSI: line, bomb and golden — no pass ball, no combo (levels 1–8).</summary>
        public static readonly Features All = new Features { Line = true, Bomb = true, Golden = true };
        /// <summary>Pota OZELLIK_TAM: all four specials and the combo (levels 9–50).</summary>
        public static readonly Features Full = new Features { Line = true, Bomb = true, Golden = true, Pass = true, Combo = true };
    }

    /// <summary>
    /// Pota KomboTur (spec 2026-10-13 §1.3–1.4): Cross = arti (line + line: row + column), ThickCross = kalinArti (line + bomb: three rows +
    /// three columns), BigBomb = devBomba (bomb + bomb: 5×5), PassLine / PassBomb = pasCizgi / pasBomba (the pass ball carries the other
    /// special to its target and fires it there), PassPass = pasPas (three pass flights from the centre).
    /// </summary>
    public enum ComboKind { Cross, ThickCross, BigBomb, PassLine, PassBomb, PassPass }

    /// <summary>
    /// Pota Kombo: the combo of a tap's first step. <see cref="Center"/> is the tapped cell, <see cref="Partner"/> the neighbour it merged
    /// with (step-start indices). Pass combos: <see cref="Carried"/> is the special a PassLine / PassBomb carries (None otherwise) and
    /// <see cref="PassTargets"/> the flights' targets in order (−1: nothing to hit); empty for the area combos.
    /// </summary>
    public struct ComboInfo
    {
        public ComboKind Kind;
        public int Center, Partner;
        public SpecialKind Carried;
        public int[] PassTargets;
        public bool IsPass => Kind == ComboKind.PassLine || Kind == ComboKind.PassBomb || Kind == ComboKind.PassPass;
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

    /// <summary>Pota patlamaAlani / alanKapanisi without ice: the cells a fired special clears and every special it sets off, both sorted.</summary>
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

        /// <summary>Pota PAS_EN_AZ_UZAKLIK (spec 2026-10-13 K7, "hemen yakında patlıyor"): the Chebyshev distance a pass ball prefers to fly at least.</summary>
        public const int MinPassDistance = 3;

        /// <summary>
        /// Pota pasHedefi (spec 2026-10-11 §1.4, distance order 2026-10-13 K7): the cell a pass ball at <paramref name="cell"/> flies to — over the
        /// WHOLE board, a block in a target first; within one class the cells at Chebyshev ≥ <see cref="MinPassDistance"/> first and the NEAREST
        /// of them, with none that far the FARTHEST; then the smallest index. With nothing in a target, the normal blocks the same way. Never the
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
            bool bestIn = false, bestFar = false;
            for (int i = 0; i < cells.Length; i++)
            {
                var block = cells[i];
                if (block == null || i == cell || exclude.Contains(i) || block.Value.Special == SpecialKind.Golden) continue;
                bool inTarget = InTarget(block.Value, targets, bombTarget, lineTarget);
                if (!inTarget && block.Value.IsSpecial) continue;
                int d = Math.Max(Math.Abs(Row(n, i) - r0), Math.Abs(Col(n, i) - c0));
                bool far = d >= MinPassDistance;
                bool better = best < 0 || (inTarget != bestIn ? inTarget : far != bestFar ? far : far ? d < bestD : d > bestD);
                if (better) { best = i; bestD = d; bestIn = inTarget; bestFar = far; }
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
        /// A pass combo's flights (Pota komboUc, spec 2026-10-13 §1.4) come first, in the first wave, before the other passes: see
        /// <see cref="FlyCombo"/>; their targets go to <paramref name="comboTargets"/>.
        /// Returns the exploded cells added; <paramref name="hits"/> and <paramref name="fired"/> get the flights and the specials set off.
        /// </summary>
        private static List<int> FlyPasses(Board result, Block?[] temp, List<int> passes, HashSet<int> exclude, List<PassHit> hits, List<Trigger> fired,
                                           ComboFlight combo = null, List<int> comboTargets = null)
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
            // The area of a fired special (or a carried one) explodes at once; a pass ball in it flies in the next wave.
            void Area(BlastArea area, List<int> next)
            {
                foreach (var t in area.Triggered)
                {
                    fired.Add(t);
                    CountSpecialTarget(result, t.Kind);
                    if (t.Kind == SpecialKind.Pass) next.Add(t.Cell);
                }
                foreach (int i in area.Cells) { exclude.Add(i); Explode(i); }
            }
            // Pota vur: the hit of a pass ball (or of a three-pass flight).
            void Hit(int v, List<int> next)
            {
                exclude.Add(v);
                if (!temp[v].Value.IsSpecial) { Explode(v); return; }
                Area(Blast(n, i => i != v && exclude.Contains(i) ? null : temp[i], v), next);
            }
            passes.Sort();
            bool comboFirst = combo != null;
            while (passes.Count > 0 || comboFirst)
            {
                var next = new List<int>();
                if (comboFirst)
                {
                    comboFirst = false;
                    FlyCombo(result, temp, combo, exclude, hits, comboTargets, next, Hit, Area);
                }
                foreach (int h in passes)
                {
                    exclude.Add(h);
                    int v = PassTarget(n, temp, result.Targets, result.BombTarget, result.LineTarget, h, exclude);
                    hits.Add(new PassHit { Cell = h, Target = v });
                    if (v >= 0) Hit(v, next);
                }
                next.Sort();
                passes = next;
            }
            return added;
        }

        // ---------- combos (Pota komboTuru, komboOrtagi, komboCiftleri, komboAlanHucreleri, alanKapanisi; spec 2026-10-13 §1.2–1.4) ----------

        /// <summary>Pota KOMBO_PAS_SAYISI: the flights of a PassPass combo.</summary>
        public const int ComboPassCount = 3;

        /// <summary>A pass combo's flight in ExplodeAndFill (Pota PasKombo): the flights leave the centre; Carried fires at the target.</summary>
        private sealed class ComboFlight
        {
            public ComboKind Kind;
            public int Center, Partner;
            public SpecialKind Carried;
        }

        /// <summary>Pota komboSinifi: line / bomb / pass ball, −1 for anything else (a golden ball or a normal block makes no combo).</summary>
        private static int ComboClass(SpecialKind k) =>
            k == SpecialKind.LineV || k == SpecialKind.LineH ? 0 : k == SpecialKind.Bomb ? 1 : k == SpecialKind.Pass ? 2 : -1;

        /// <summary>Pota komboTuru: the combo two specials make, in either order; null when one is not a line, a bomb or a pass ball.</summary>
        public static ComboKind? ComboKindOf(SpecialKind a, SpecialKind b)
        {
            int x = ComboClass(a), y = ComboClass(b);
            if (x < 0 || y < 0) return null;
            if (x == 2 && y == 2) return ComboKind.PassPass;
            if (x == 2 || y == 2) return x == 1 || y == 1 ? ComboKind.PassBomb : ComboKind.PassLine;
            if (x == 1 && y == 1) return ComboKind.BigBomb;
            return x == 1 || y == 1 ? ComboKind.ThickCross : ComboKind.Cross;
        }

        /// <summary>Pota KOMBO_ONCELIK (K3): the smaller goes first — ThickCross 0, BigBomb 1, Cross 2, PassBomb 3, PassLine 4, PassPass 5.</summary>
        public static int ComboPriority(ComboKind k)
        {
            switch (k)
            {
                case ComboKind.ThickCross: return 0;
                case ComboKind.BigBomb: return 1;
                case ComboKind.Cross: return 2;
                case ComboKind.PassBomb: return 3;
                case ComboKind.PassLine: return 4;
                default: return 5;
            }
        }

        /// <summary>
        /// Pota komboOrtagi (K1, K3): with <see cref="Features.Combo"/> on and a line, a bomb or a pass ball at <paramref name="cell"/>, the
        /// neighbour (above, left, right, below; never diagonal) that is one too and makes the strongest combo (<see cref="ComboPriority"/>),
        /// the smaller index on a tie. A golden ball is never a partner (Pota's obstacle and referee cases do not exist here). −1: none. Pure.
        /// </summary>
        public static int FindComboPartner(Board b, int cell)
        {
            if (!b.Features.Combo || cell < 0 || cell >= b.Cells.Length) return -1;
            var kind = b.Cells[cell].Special;
            if (ComboClass(kind) < 0) return -1;
            int n = b.Size, r = Row(n, cell), c = Col(n, cell);
            int best = -1, bestPriority = int.MaxValue;
            foreach (var (dr, dc) in ComboNeighbours)
            {
                int rr = r + dr, cc = c + dc;
                if (rr < 0 || rr >= n || cc < 0 || cc >= n) continue;
                int i = Index(n, rr, cc);
                var combo = ComboKindOf(kind, b.Cells[i].Special);
                if (combo == null) continue;
                int priority = ComboPriority(combo.Value);
                if (best < 0 || priority < bestPriority || (priority == bestPriority && i < best)) { best = i; bestPriority = priority; }
            }
            return best;
        }

        /// <summary>Pota komboOrtagi's neighbour order: above, left, right, below.</summary>
        private static readonly (int dr, int dc)[] ComboNeighbours = { (-1, 0), (0, -1), (0, 1), (1, 0) };

        /// <summary>Pota komboCiftleri: every special on the board that can combo → its partner (<see cref="FindComboPartner"/>), by cell; the look (lean, hint) reads it.</summary>
        public static SortedDictionary<int, int> ComboPairs(Board b)
        {
            var pairs = new SortedDictionary<int, int>();
            for (int i = 0; i < b.Cells.Length; i++)
            {
                int partner = FindComboPartner(b, i);
                if (partner >= 0) pairs[i] = partner;
            }
            return pairs;
        }

        /// <summary>
        /// Pota komboAlanHucreleri (§1.3): an area combo's cells around <paramref name="center"/>, clipped at the edges, ascending — Cross its
        /// row + column, ThickCross three rows + three columns, BigBomb its 5×5. A pass combo has no area of its own (ArgumentException).
        /// </summary>
        public static List<int> ComboCells(int n, int center, ComboKind kind)
        {
            if (kind != ComboKind.Cross && kind != ComboKind.ThickCross && kind != ComboKind.BigBomb)
                throw new ArgumentException($"{kind} has no area of its own", nameof(kind));
            int r0 = Row(n, center), c0 = Col(n, center);
            var list = new List<int>();
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    int dr = Math.Abs(r - r0), dc = Math.Abs(c - c0);
                    bool inside = kind == ComboKind.BigBomb ? dr <= 2 && dc <= 2 : kind == ComboKind.ThickCross ? dr <= 1 || dc <= 1 : dr == 0 || dc == 0;
                    if (inside) list.Add(Index(n, r, c));
                }
            return list;
        }

        /// <summary>
        /// Pota alanKapanisi (§1.3, patlamaAlani's rule without ice): <paramref name="start"/>'s cells and every special they set off, grown by
        /// each one's own <see cref="SingleArea"/>. The <paramref name="consumed"/> specials (a combo's two) explode and fire but open NO area of
        /// their own. Golden balls are left out. Both lists sorted.
        /// </summary>
        public static BlastArea Closure(Board b, IEnumerable<int> start, IDictionary<int, SpecialKind> consumed) =>
            Closure(b.Size, i => b.Cells[i], start, consumed);

        private static BlastArea Closure(int n, Func<int, Block?> at, IEnumerable<int> start, IDictionary<int, SpecialKind> consumed)
        {
            var cells = new SortedSet<int>();
            var fired = new SortedDictionary<int, SpecialKind>();
            if (consumed != null)
                foreach (var kv in consumed) { cells.Add(kv.Key); fired[kv.Key] = kv.Value; }
            var queue = new Queue<int>();
            void Take(int i)
            {
                if (i < 0 || i >= n * n) return;
                var block = at(i);
                if (block == null) return;
                var kind = block.Value.Special;
                if (kind == SpecialKind.Golden) return;
                cells.Add(i);
                if (kind != SpecialKind.None && !fired.ContainsKey(i)) { fired[i] = kind; queue.Enqueue(i); }
            }
            foreach (int i in start) Take(i);
            while (queue.Count > 0)
            {
                int h = queue.Dequeue();
                foreach (int i in SingleArea(n, h, fired[h])) Take(i);
            }
            var area = new BlastArea { Cells = new List<int>(cells), Triggered = new List<Trigger>() };
            foreach (var kv in fired) area.Triggered.Add(new Trigger { Cell = kv.Key, Kind = kv.Value });
            return area;
        }

        /// <summary>
        /// Pota komboAdimi (§1.3–1.4), the first step of a combo tap: an area combo explodes the <see cref="Closure"/> of its
        /// <see cref="ComboCells"/> (the two partners consumed); a pass combo explodes the two partners and its flights leave the centre in
        /// the step's first pass wave (<see cref="FlyCombo"/>).
        /// </summary>
        private static Board ComboStep(Board b, int center, int partner, out Step step)
        {
            var mine = b.Cells[center].Special;
            var theirs = b.Cells[partner].Special;
            var kind = ComboKindOf(mine, theirs).Value;
            if (kind == ComboKind.PassLine || kind == ComboKind.PassBomb || kind == ComboKind.PassPass)
            {
                var carried = kind == ComboKind.PassPass ? SpecialKind.None : mine == SpecialKind.Pass ? theirs : mine;
                var partners = new List<Trigger> { new Trigger { Cell = center, Kind = mine }, new Trigger { Cell = partner, Kind = theirs } };
                partners.Sort((x, y) => x.Cell.CompareTo(y.Cell));
                var exploded = new List<int> { Math.Min(center, partner), Math.Max(center, partner) };
                var flight = new ComboFlight { Kind = kind, Center = center, Partner = partner, Carried = carried };
                return ExplodeAndFill(b, exploded, null, null, partners, out step, flight);
            }
            var area = Closure(b, ComboCells(b.Size, center, kind), new Dictionary<int, SpecialKind> { [center] = mine, [partner] = theirs });
            var after = ExplodeAndFill(b, area.Cells, null, null, area.Triggered, out step);
            step.Combo = new ComboInfo { Kind = kind, Center = center, Partner = partner, Carried = SpecialKind.None, PassTargets = new int[0] };
            return after;
        }

        /// <summary>
        /// Pota komboUc (§1.4): a pass combo's flights from the centre, in the first pass wave. PassPass: <see cref="ComboPassCount"/> flights,
        /// each a pass ball's hit (<see cref="PassTarget"/>, the earlier hits and their areas excluded; a special target fires). PassLine /
        /// PassBomb: one flight whose target is never a special (P1: the carried special fires there), then the carried special's
        /// <see cref="SingleArea"/> around the target explodes as a <see cref="Closure"/> (the step's cells hidden); with nothing to hit the
        /// carried special fires at the centre. The target is excluded after its area.
        /// </summary>
        private static void FlyCombo(Board result, Block?[] temp, ComboFlight combo, HashSet<int> exclude, List<PassHit> hits, List<int> comboTargets,
                                     List<int> next, Action<int, List<int>> hit, Action<BlastArea, List<int>> area)
        {
            int n = result.Size, m = combo.Center;
            exclude.Add(m);
            if (combo.Kind == ComboKind.PassPass)
            {
                for (int j = 0; j < ComboPassCount; j++)
                {
                    int v = PassTarget(n, temp, result.Targets, result.BombTarget, result.LineTarget, m, exclude);
                    comboTargets.Add(v);
                    hits.Add(new PassHit { Cell = m, Target = v });
                    if (v >= 0) hit(v, next);
                }
                return;
            }
            var noSpecial = new HashSet<int>(exclude);
            for (int i = 0; i < temp.Length; i++) if (temp[i] != null && temp[i].Value.IsSpecial) noSpecial.Add(i);
            int target = PassTarget(n, temp, result.Targets, result.BombTarget, result.LineTarget, m, noSpecial);
            comboTargets.Add(target);
            hits.Add(new PassHit { Cell = m, Target = target });
            area(Closure(n, i => exclude.Contains(i) ? null : temp[i], SingleArea(n, target >= 0 ? target : m, combo.Carried), null), next);
            if (target >= 0) exclude.Add(target);
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
