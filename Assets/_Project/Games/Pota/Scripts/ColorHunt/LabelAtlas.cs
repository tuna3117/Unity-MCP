using System;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// yapbozSahne.ts EtiketAtlasi layout: 2048² canvas, 11 columns × 186 px; numbers 1–99 → cells 0–98, star 99, items 100–104,
    /// special balls 105–107 (HUCRE_OZEL; obstacles 108–110 are exported too, unused here).
    /// </summary>
    public static class LabelAtlas
    {
        public const int Size = 2048, Columns = 11, CellPx = 186, Star = 99, ItemBase = 100, SpecialBase = 105, PassCell = 111;

        /// <summary>A whole sprite drawn like an atlas special ("payli"): the sprite fills the middle 88 % of the quad (clamped, padded art).</summary>
        public static readonly Vector4 PaddedUv = new Vector4(-0.06f / 0.88f, -0.06f / 0.88f, 1f / 0.88f, 1f / 0.88f);

        /// <summary>rakamHucresi: min(99, max(1, n)) − 1.</summary>
        public static int NumberCell(int hp) => Mathf.Clamp(hp, 1, 99) - 1;

        public static int ItemCell(int color) => ItemBase + color;

        /// <summary>HUCRE_OZEL: çizgi 105 (both lines; LineH turns the sticker 90°), bomba 106, altın 107, pas topu 111 (Pota 2026-10-11; Unity's
        /// exported atlas predates it: BlockView draws the pass ball from ColorHuntSettings.PassSprite). Drawn "payli" (88 %).</summary>
        public static int SpecialCell(SpecialKind k)
        {
            switch (k)
            {
                case SpecialKind.LineV:
                case SpecialKind.LineH: return SpecialBase;
                case SpecialKind.Bomb: return SpecialBase + 1;
                case SpecialKind.Golden: return SpecialBase + 2;
                case SpecialKind.Pass: return PassCell;
                default: throw new ArgumentOutOfRangeException(nameof(k), k, "not a special ball");
            }
        }

        /// <summary>uvRect: (u, v, w, h) with v from the bottom (the canvas row counts from the top, like three's flipY).</summary>
        public static Vector4 UvRect(int cell)
        {
            int x = cell % Columns * CellPx, y = cell / Columns * CellPx;
            return new Vector4(x / (float)Size, 1f - (y + CellPx) / (float)Size, CellPx / (float)Size, CellPx / (float)Size);
        }
    }
}
