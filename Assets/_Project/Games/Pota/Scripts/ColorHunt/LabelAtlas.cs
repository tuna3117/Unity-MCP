using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>yapbozSahne.ts EtiketAtlasi layout: 2048² canvas, 11 columns × 186 px; numbers 1–99 → cells 0–98, star 99, items 100–104.</summary>
    public static class LabelAtlas
    {
        public const int Size = 2048, Columns = 11, CellPx = 186, Star = 99, ItemBase = 100;

        /// <summary>rakamHucresi: min(99, max(1, n)) − 1.</summary>
        public static int NumberCell(int hp) => Mathf.Clamp(hp, 1, 99) - 1;

        public static int ItemCell(int color) => ItemBase + color;

        /// <summary>uvRect: (u, v, w, h) with v from the bottom (the canvas row counts from the top, like three's flipY).</summary>
        public static Vector4 UvRect(int cell)
        {
            int x = cell % Columns * CellPx, y = cell / Columns * CellPx;
            return new Vector4(x / (float)Size, 1f - (y + CellPx) / (float)Size, CellPx / (float)Size, CellPx / (float)Size);
        }
    }
}
