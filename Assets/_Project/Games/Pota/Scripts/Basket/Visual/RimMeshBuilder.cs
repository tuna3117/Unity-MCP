using UnityEngine;

namespace Project.Basket.Visual
{
    /// <summary>Procedural torus (rim, net rings) and helpers for gray-box visuals.</summary>
    public static class RimMeshBuilder
    {
        public static Mesh Torus(float radius, float tube, int segments = 32, int sides = 10)
        {
            var mesh = new Mesh { name = $"Torus_{radius:0.00}_{tube:0.000}" };
            int vertCount = (segments + 1) * (sides + 1);
            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            int vi = 0;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments * Mathf.PI * 2f;
                var center = new Vector3(Mathf.Cos(u) * radius, 0f, Mathf.Sin(u) * radius);
                var radial = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= sides; j++)
                {
                    float v = (float)j / sides * Mathf.PI * 2f;
                    var n = radial * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    verts[vi] = center + n * tube;
                    norms[vi] = n;
                    uvs[vi] = new Vector2((float)i / segments, (float)j / sides);
                    vi++;
                }
            }
            var tris = new int[segments * sides * 6];
            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j;
                    int b = a + sides + 1;
                    tris[ti++] = a; tris[ti++] = a + 1; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = a + 1; tris[ti++] = b + 1;
                }
            }
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material UrpLit(Color color, float smoothness = 0.5f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
