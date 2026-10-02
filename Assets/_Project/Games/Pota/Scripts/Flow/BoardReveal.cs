using Project.ColorHunt;
using UnityEngine;

namespace Project.Flow
{
    /// <summary>Board entrance: blocks pop in ring by ring from the centre (Chebyshev rings), timed by the FlowTimeline.</summary>
    public static class BoardReveal
    {
        public static int Ring(int size, int row, int col)
        {
            float c = (size - 1) * 0.5f;
            return Mathf.FloorToInt(Mathf.Max(Mathf.Abs(row - c), Mathf.Abs(col - c)));
        }

        public static int RingCount(int size) => Ring(size, 0, 0) + 1;

        /// <summary>Sets every staged block view's scale for timeline time t (0.001 instead of 0: no degenerate matrices).</summary>
        public static void Apply(ColorHuntGame game, FlowTimeline tl, float t)
        {
            var board = game.Board;
            if (board == null) return;
            int n = board.Size;
            for (int cell = 0; cell < board.Cells.Length; cell++)
            {
                var v = game.ViewAt(cell);
                if (v == null) continue;
                int ring = Ring(n, ColorHuntRules.Row(n, cell), ColorHuntRules.Col(n, cell));
                v.Scale = Mathf.Max(0.001f, tl.RevealScale(t, ring));
            }
        }

        /// <summary>
        /// Candidate 3's block rain: every column's blocks hang (n + 1) cells above home, hidden, then fall with constant
        /// acceleration column by column and squash on landing (Pota's own LandSquash through BlockView.Squash).
        /// </summary>
        public static void ApplyRain(ColorHuntGame game, FlowTimeline tl, float t)
        {
            var board = game.Board;
            if (board == null) return;
            int n = board.Size;
            for (int cell = 0; cell < board.Cells.Length; cell++)
            {
                var v = game.ViewAt(cell);
                if (v == null) continue;
                int col = ColorHuntRules.Col(n, cell);
                v.Pos = v.Home + new Vector2(0f, tl.RainOffset(t, col, n));
                v.Scale = tl.RainVisible(t, col) ? 1f : 0.001f;
                v.Squash = tl.RainSquash(t, col);
            }
        }

        public static void Finish(ColorHuntGame game)
        {
            var board = game.Board;
            if (board == null) return;
            for (int cell = 0; cell < board.Cells.Length; cell++)
            {
                var v = game.ViewAt(cell);
                if (v == null) continue;
                v.Scale = 1f;
                v.Pos = v.Home;
                v.Squash = 0f;
            }
        }
    }
}
