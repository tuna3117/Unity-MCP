using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Procedural rounded box: each cube face is a grid whose vertices are projected onto the rounded-box
    /// surface (clamp to the inner box, then push out by the corner radius). Per-face UVs 0..1 so a face
    /// texture lands squarely on every side.
    /// </summary>
    public static class RoundedBoxMesh
    {
        public static Mesh Create(Vector3 size, float radius, int segments = 6)
        {
            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f - 1e-4f);
            var half = size * 0.5f;
            var inner = half - Vector3.one * radius;
            int g = Mathf.Max(1, segments);
            int perFace = (g + 1) * (g + 1);
            var verts = new Vector3[perFace * 6];
            var norms = new Vector3[perFace * 6];
            var uvs = new Vector2[perFace * 6];
            var tris = new int[g * g * 6 * 6];

            Vector3[] normals = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            Vector3[] rights = { Vector3.left, Vector3.right, Vector3.right, Vector3.right, Vector3.back, Vector3.forward };
            Vector3[] ups = { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };

            int v = 0, t = 0;
            for (int f = 0; f < 6; f++)
            {
                var n = normals[f]; var r = rights[f]; var u = ups[f];
                int baseIndex = v;
                for (int y = 0; y <= g; y++)
                {
                    for (int x = 0; x <= g; x++)
                    {
                        float fx = (float)x / g, fy = (float)y / g;
                        // Cube-face point, then project onto the rounded box.
                        var p = Vector3.Scale(n, half) + Vector3.Scale(r, half) * (fx * 2f - 1f) + Vector3.Scale(u, half) * (fy * 2f - 1f);
                        var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var d = p - c;
                        var nn = d.sqrMagnitude > 1e-8f ? d.normalized : n;
                        verts[v] = c + nn * radius;
                        norms[v] = nn;
                        uvs[v] = new Vector2(fx, fy);
                        v++;
                    }
                }
                for (int y = 0; y < g; y++)
                {
                    for (int x = 0; x < g; x++)
                    {
                        int i0 = baseIndex + y * (g + 1) + x;
                        int i1 = i0 + 1, i2 = i0 + g + 1, i3 = i2 + 1;
                        tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                        tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                    }
                }
            }
            var mesh = new Mesh { name = "RoundedBox" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
