using UnityEngine;

namespace Project.Meta
{
    /// <summary>
    /// Pota's map layout (haritaYer.ts), pure, in css px with y growing downwards: the snake of nodes, the "Sen" pin's place beside
    /// its node (never on a node, never off the edge, farthest from the path), the dotted path between two nodes.
    /// </summary>
    public static class MapLayout
    {
        public const float Gap = 118f, Top = 90f, Bottom = 160f;
        public const float SenWidth = 64f, SenHeight = 79f, NodeRadius = 40f;
        public const float DotPeriod = 25f;                    // Pota: stroke-dasharray "1 24"
        private const float Edge = 4f, Enough = 34f;
        private static readonly float[] DX = { 78f, 92f }, DY = { -8f, -26f, 10f };

        /// <summary>Node <paramref name="n"/> (1-based) of <paramref name="last"/> on a map <paramref name="width"/> wide.</summary>
        public static Vector2 Node(int n, float width, int last) => new Vector2(width / 2f + Mathf.Sin(n * 0.85f) * width * 0.3f, Top + (last - n) * Gap);

        public static float ContentHeight(int last) => Top + (last - 1) * Gap + Bottom;

        /// <summary>Distance from point <paramref name="p"/> to the pin's box centred at (x, y); 0 inside.</summary>
        public static float BoxDistance(Vector2 p, float x, float y)
        {
            float dx = Mathf.Max(x - SenWidth / 2f - p.x, 0f, p.x - x - SenWidth / 2f);
            float dy = Mathf.Max(y - SenHeight / 2f - p.y, 0f, p.y - y - SenHeight / 2f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float SegmentDistance(Vector2? a, Vector2? b, float x, float y)
        {
            if (!a.HasValue || !b.HasValue) return float.PositiveInfinity;
            float best = float.PositiveInfinity;
            for (int i = 0; i <= 60; i++) best = Mathf.Min(best, BoxDistance(Vector2.Lerp(a.Value, b.Value, i / 60f), x, y));
            return best;
        }

        /// <summary>The pin's centre for node <paramref name="k"/> (Pota senYeri): beside it on the side the path does not take, 78 px out, 8 px up, unless that spot sits on a node, the edge or the path.</summary>
        public static Vector2 SenYeri(Vector2 k, Vector2? prev, Vector2? next, float width)
        {
            float preferred = next.HasValue && next.Value.x > k.x ? -1f : 1f;
            Vector2? best = null; float bestScore = float.NegativeInfinity;
            foreach (float side in new[] { preferred, -preferred })
                foreach (float dx in DX)
                    foreach (float dy in DY)
                    {
                        float x = k.x + side * dx, y = k.y + dy;
                        if (x - SenWidth / 2f < Edge || x + SenWidth / 2f > width - Edge) continue;
                        bool onNode = false;
                        foreach (var d in new[] { prev, (Vector2?)k, next }) if (d.HasValue && BoxDistance(d.Value, x, y) <= NodeRadius) onNode = true;
                        if (onNode) continue;
                        float far = Mathf.Min(SegmentDistance(prev, k, x, y), SegmentDistance(k, next, x, y));
                        float score = Mathf.Min(far, Enough) - (side != preferred ? 1f : 0f) - (dx != DX[0] ? 0.5f : 0f) - (dy != DY[0] ? 0.25f : 0f);
                        if (score > bestScore) { bestScore = score; best = new Vector2(x, y); }
                    }
            return best ?? new Vector2(k.x + preferred * DX[0], k.y + DY[0]);
        }

        /// <summary>The path's dots from <paramref name="a"/> to <paramref name="b"/>, every DotPeriod px, the ends (nodes) excluded.</summary>
        public static Vector2[] Dots(Vector2 a, Vector2 b)
        {
            float dist = Vector2.Distance(a, b);
            int n = Mathf.Max(0, Mathf.FloorToInt((dist - 0.01f) / DotPeriod));
            var dots = new Vector2[n];
            for (int i = 0; i < n; i++) dots[i] = Vector2.Lerp(a, b, (i + 1) * DotPeriod / dist);
            return dots;
        }
    }
}
