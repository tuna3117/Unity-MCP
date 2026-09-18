using System.Collections.Generic;
using UnityEngine;

namespace Project.Basket
{
    public enum HoopEffectKind { Multiply, Add }

    public struct HoopSpec
    {
        public float X, Y, Radius;
        public HoopEffectKind Kind;
        public int Amount;
        public bool IsTop;
        public int Row; // -1 for the top row, 0.. for rows below
    }

    /// <summary>Everything needed to build one level's hoop column.</summary>
    public sealed class HoopLayout
    {
        public int Level;
        public int BallCount;
        public int Rows;
        public List<HoopSpec> Hoops = new List<HoopSpec>();

        public float TopRimY = 3.05f;
        public float RowSpacing = 3f;
        public float WallX = 2.1f;
        public float BasketHeight = 4.6f;
        public float BasketDepth = 2f;

        public float LowestRowY => TopRimY - RowSpacing * Rows;
        public float BasketWidth => 3.6f + (Rows - 2) * 0.4f;
        public float BasketTopY => LowestRowY;                 // cage mouth right under the last row
        public float BasketFloorY => BasketTopY - BasketHeight;
    }

    /// <summary>Level rules ported from the original game (kurallar.ts): ball table, row count, seeded layout.</summary>
    public static class LevelRules
    {
        public static readonly int[] BallTable =
        {
            8, 8, 8, 9, 13, 13, 13, 11, 12, 16, 13, 12, 13, 12, 17, 12, 14, 16, 20, 7,
            20, 28, 22, 20, 26, 27, 28, 18, 28, 20, 31, 31, 29, 25, 39, 40, 34, 29, 25, 35,
        };

        private static readonly (bool pair, HoopEffectKind kind, int amount)[] RowPattern =
        {
            (true, HoopEffectKind.Multiply, 2),
            (false, HoopEffectKind.Add, 1),
            (true, HoopEffectKind.Multiply, 2),
            (false, HoopEffectKind.Add, 1),
            (true, HoopEffectKind.Add, 1),
            (false, HoopEffectKind.Multiply, 2),
            (true, HoopEffectKind.Add, 1),
            (false, HoopEffectKind.Add, 1),
        };

        public const float TopHoopRadius = 0.30f;
        public const float PairHoopRadius = 0.40f;
        public const float SingleHoopRadius = 0.42f;
        public const float PairOffset = 1.0f;
        public const float Jitter = 0.9f; // ±0.45

        public static int ClampLevel(int level) => Mathf.Clamp(level, 1, BallTable.Length);

        public static int BallCount(int level) => BallTable[ClampLevel(level) - 1];

        public static int RowCount(int level) => Mathf.Min(8, 2 + (ClampLevel(level) - 1) / 3);

        public static HoopLayout Build(int level)
        {
            level = ClampLevel(level);
            var layout = new HoopLayout { Level = level, BallCount = BallCount(level), Rows = RowCount(level) };
            var rng = new System.Random(7 + level);
            float Jit() => ((float)rng.NextDouble() - 0.5f) * Jitter;

            float[] topXs = { -1.4f, 0f, 1.4f };
            foreach (float x in topXs)
                layout.Hoops.Add(new HoopSpec { X = x, Y = layout.TopRimY, Radius = TopHoopRadius, Kind = HoopEffectKind.Multiply, Amount = 2, IsTop = true, Row = -1 });

            for (int row = 0; row < layout.Rows; row++)
            {
                var p = RowPattern[row % RowPattern.Length];
                float y = layout.TopRimY - layout.RowSpacing * (row + 1);
                if (p.pair)
                {
                    layout.Hoops.Add(new HoopSpec { X = -PairOffset + Jit(), Y = y, Radius = PairHoopRadius, Kind = p.kind, Amount = p.amount, Row = row });
                    layout.Hoops.Add(new HoopSpec { X = PairOffset + Jit(), Y = y, Radius = PairHoopRadius, Kind = p.kind, Amount = p.amount, Row = row });
                }
                else
                {
                    layout.Hoops.Add(new HoopSpec { X = Jit(), Y = y, Radius = SingleHoopRadius, Kind = p.kind, Amount = p.amount, Row = row });
                }
            }
            return layout;
        }
    }
}
