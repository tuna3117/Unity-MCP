using System.Collections.Generic;
using UnityEngine;

namespace Project.Basket.Visual
{
    /// <summary>Wire-cage visual (bars every 0.5 m, orange rails) merged into two meshes.</summary>
    public static class CageMeshBuilder
    {
        public static void Build(Transform parent, float width, float height, float depth, float floorY, Material wire, Material rail,
            float step = 0.5f, float bar = 0.05f, float railSize = 0.18f, float feet = 0.6f)
        {
            var bars = new List<CombineInstance>();
            var rails = new List<CombineInstance>();
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            float top = floorY + height;
            float hw = width * 0.5f, hd = depth * 0.5f;

            void Bar(Vector3 pos, Vector3 size, bool isRail)
            {
                var ci = new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(pos, Quaternion.identity, size) };
                (isRail ? rails : bars).Add(ci);
            }

            // verticals on all four faces
            for (float x = -hw; x <= hw + 0.001f; x += step)
            {
                Bar(new Vector3(x, floorY + height * 0.5f, hd), new Vector3(bar, height, bar), false);
                Bar(new Vector3(x, floorY + height * 0.5f, -hd), new Vector3(bar, height, bar), false);
            }
            for (float z = -hd + step; z < hd - 0.001f; z += step)
            {
                Bar(new Vector3(hw, floorY + height * 0.5f, z), new Vector3(bar, height, bar), false);
                Bar(new Vector3(-hw, floorY + height * 0.5f, z), new Vector3(bar, height, bar), false);
            }
            // horizontals
            for (float y = floorY; y <= top + 0.001f; y += step)
            {
                bool isRail = y >= top - 0.001f;
                float t = isRail ? railSize : bar;
                Bar(new Vector3(0f, y, hd), new Vector3(width + t, t, t), isRail);
                Bar(new Vector3(0f, y, -hd), new Vector3(width + t, t, t), isRail);
                Bar(new Vector3(hw, y, 0f), new Vector3(t, t, depth + t), isRail);
                Bar(new Vector3(-hw, y, 0f), new Vector3(t, t, depth + t), isRail);
            }
            // floor grid
            for (float x = -hw; x <= hw + 0.001f; x += step) Bar(new Vector3(x, floorY, 0f), new Vector3(bar, bar, depth), false);
            for (float z = -hd; z <= hd + 0.001f; z += step) Bar(new Vector3(0f, floorY, z), new Vector3(width, bar, bar), false);
            // feet
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Bar(new Vector3(sx * (hw - Mathf.Min(0.2f, hw * 0.3f)), floorY - feet * 0.5f, sz * (hd - Mathf.Min(0.2f, hd * 0.3f))),
                        new Vector3(Mathf.Min(0.25f, width * 0.1f), feet, Mathf.Min(0.25f, depth * 0.1f)), true);

            Merge(parent, "CageWire", bars, wire);
            Merge(parent, "CageRails", rails, rail);
        }

        private static void Merge(Transform parent, string name, List<CombineInstance> parts, Material mat)
        {
            if (parts.Count == 0) return;
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            RimMeshBuilder.MeshObject(name, parent, mesh, mat);
        }
    }
}
