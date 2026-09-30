using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Board space helpers: cell positions (cell (row, col) → x = col − (n−1)/2, y = (n−1)/2 − row, z = 0; the camera looks down +z)
    /// and soft sprites.
    /// </summary>
    public static class BoardBuilder
    {
        public static Vector2 CellPosition(int n, int row, int col) => new Vector2(col - (n - 1) * 0.5f, (n - 1) * 0.5f - row);
    }
}
