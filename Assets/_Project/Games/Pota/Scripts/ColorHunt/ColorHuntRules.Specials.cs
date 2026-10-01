using System;
using System.Collections.Generic;

namespace Project.ColorHunt
{
    /// <summary>Pota's special balls (OzelTur). LineV = Pota cizgiDikey: clears its COLUMN (born from a horizontal run of 4).</summary>
    public enum SpecialKind { None = 0, LineV = 1, LineH = 2, Bomb = 3, Golden = 4 }

    /// <summary>Which specials a level gives birth to (Pota Ozellikler). A closed feature skips its birth step.</summary>
    [Serializable]
    public struct Features
    {
        public bool Line, Bomb, Golden;
        public bool Any => Line || Bomb || Golden;
        public static readonly Features None = default;
        public static readonly Features All = new Features { Line = true, Bomb = true, Golden = true };
    }

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
        /// Pota eslesmeleriCoz: runs sharing a cell form one group, one special per group. 1) a run of ≥5 (Golden on) → golden at
        /// that run's birth cell, the rest of the group explodes; 2) else a group of ≥2 runs (Bomb on) → bomb at the smallest
        /// intersection cell; 3) else every run of exactly 4 (Line on) → a line at its birth cell (horizontal run → LineV at its
        /// second cell, vertical run → LineH at its bottom cell). A closed feature skips its step. Birth cells do not explode.
        /// Returns the exploded cells sorted; <paramref name="born"/> is sorted by cell.
        /// </summary>
        public static List<int> ResolveMatches(Board b, out List<Born> born)
        {
            var f = b.Features;
            var exploded = new SortedSet<int>();
            var births = new SortedDictionary<int, (SpecialKind kind, List<int> cells)>();
            void Birth(int cell, SpecialKind kind, List<int> cells)
            {
                // Pota keeps the last kind set on a cell (two crossing 4-runs with the bomb closed); the look gathers both runs.
                if (births.TryGetValue(cell, out var old))
                    foreach (int c in old.cells) if (!cells.Contains(c)) cells.Add(c);
                births[cell] = (kind, cells);
            }
            foreach (var group in GroupRuns(FindMatches(b)))
            {
                var groupCells = new List<int>();
                foreach (var m in group)
                    foreach (int c in m.Cells)
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
                    if (best >= 0) Birth(best, SpecialKind.Bomb, groupCells);
                    continue;
                }
                if (f.Line)
                    foreach (var m in group)
                        if (m.Cells.Count == 4) Birth(BirthCell(m), m.Horizontal ? SpecialKind.LineV : SpecialKind.LineH, new List<int>(m.Cells));
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

        /// <summary>Pota gruplaKosular: runs sharing a cell are one group (union–find); groups and their runs keep the run order.</summary>
        private static List<List<Match>> GroupRuns(List<Match> runs)
        {
            var parent = new int[runs.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            var owner = new Dictionary<int, int>();
            for (int i = 0; i < runs.Count; i++)
                foreach (int c in runs[i].Cells)
                {
                    if (!owner.TryGetValue(c, out int o)) { owner[c] = i; continue; }
                    int ra = Find(o), rb = Find(i);
                    if (ra != rb) parent[ra] = rb;
                }
            var groups = new List<List<Match>>();
            var byRoot = new Dictionary<int, List<Match>>();
            for (int i = 0; i < runs.Count; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out var list)) { list = new List<Match>(); byRoot[root] = list; groups.Add(list); }
                list.Add(runs[i]);
            }
            return groups;
        }

        // ---------- firing (Pota tekAlan, patlamaAlani, spec §1.3) ----------

        /// <summary>Pota tekAlan: the cells one special covers on its own, clipped at the edges (LineV its column, LineH its row,
        /// Bomb its 3×3). Golden and None cover nothing.</summary>
        public static List<int> SingleArea(int n, int cell, SpecialKind kind)
        {
            int r = Row(n, cell), c = Col(n, cell);
            var list = new List<int>();
            if (kind == SpecialKind.LineV) for (int rr = 0; rr < n; rr++) list.Add(Index(n, rr, c));
            else if (kind == SpecialKind.LineH) for (int cc = 0; cc < n; cc++) list.Add(Index(n, r, cc));
            else if (kind == SpecialKind.Bomb)
                for (int rr = Math.Max(0, r - 1); rr <= Math.Min(n - 1, r + 1); rr++)
                    for (int cc = Math.Max(0, c - 1); cc <= Math.Min(n - 1, c + 1); cc++) list.Add(Index(n, rr, cc));
            return list;
        }

        /// <summary>
        /// Pota patlamaAlani (no ice): the area of the special at <paramref name="cell"/>, grown by every line or bomb inside it
        /// (each fires once). Golden balls are left out. Empty when <paramref name="cell"/> is not a line or a bomb.
        /// </summary>
        public static BlastArea Blast(Board b, int cell)
        {
            var cells = new SortedSet<int>();
            var fired = new SortedDictionary<int, SpecialKind>();
            var queue = new Queue<int>();
            queue.Enqueue(cell);
            while (queue.Count > 0)
            {
                int h = queue.Dequeue();
                if (h < 0 || h >= b.Cells.Length) continue;
                var kind = b.Cells[h].Special;
                if (kind == SpecialKind.None || kind == SpecialKind.Golden || fired.ContainsKey(h)) continue;
                fired[h] = kind;
                foreach (int i in SingleArea(b.Size, h, kind))
                {
                    var k = b.Cells[i].Special;
                    if (k == SpecialKind.Golden) continue;
                    cells.Add(i);
                    if (k != SpecialKind.None && !fired.ContainsKey(i)) queue.Enqueue(i);
                }
            }
            var area = new BlastArea { Cells = new List<int>(cells), Triggered = new List<Trigger>() };
            foreach (var kv in fired) area.Triggered.Add(new Trigger { Cell = kv.Key, Kind = kv.Value });
            return area;
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
    }
}
