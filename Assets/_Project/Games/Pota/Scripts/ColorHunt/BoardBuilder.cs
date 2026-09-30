using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Board space helpers: cell positions (cell (row, col) → x = col − (n−1)/2, y = (n−1)/2 − row, z = 0; the camera looks down +z)
    /// and soft sprites.
    /// </summary>
    public static class BoardBuilder
    {
        public const float Margin = 0.42f;

        public static Vector2 CellPosition(int n, int row, int col) => new Vector2(col - (n - 1) * 0.5f, (n - 1) * 0.5f - row);

        public static Texture2D GradientTexture(Color top, Color bottom)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply();
            return tex;
        }

        /// <summary>Soft radial sprite (flash / puff) and a soft ring (shockwave), generated once at runtime.</summary>
        public static Texture2D SoftCircle(int size = 128, float inner = 0f, float outer = 1f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a;
                    if (inner <= 0f) a = Mathf.Clamp01(1f - d / outer);
                    else
                    {
                        float mid = (inner + outer) * 0.5f, half = (outer - inner) * 0.5f;
                        a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / half);
                    }
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return tex;
        }
    }
}
