using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// yastik.ts yastikGeometrisi: a rounded box whose front half (Unity −z) bulges by bulge·(1−u²)(1−w²)·(z/h), u = x/h, w = y/h;
    /// front-facing normals are bent by the slope (−∂f/∂x, −∂f/∂y). Position-only displacement: shared corners never crack.
    /// </summary>
    public static class PillowBlockMesh
    {
        public static Mesh Create(float size = PotaLook.Block, float radius = PotaLook.PillowRadius, float bulge = PotaLook.PillowBulge, int segments = 16)
        {
            var mesh = RoundedBoxMesh.Create(Vector3.one * size, radius, segments);
            var v = mesh.vertices;
            var n = mesh.normals;
            float h = size * 0.5f;
            for (int i = 0; i < v.Length; i++)
            {
                float z = -v[i].z;                               // Pota's +z front = Unity's −z
                if (z <= 0f || bulge == 0f) continue;
                float u = v[i].x / h, w = v[i].y / h, d = z / h;
                float a = Mathf.Max(0f, 1f - u * u), b = Mathf.Max(0f, 1f - w * w);
                v[i].z = -(z + bulge * a * b * d);
                float nz = -n[i].z;
                if (nz <= 0f) continue;
                float slopeX = bulge * (-2f * u / h) * b * d, slopeY = bulge * (-2f * w / h) * a * d;
                var pn = new Vector3(n[i].x - slopeX * nz, n[i].y - slopeY * nz, nz).normalized;
                n[i] = new Vector3(pn.x, pn.y, -pn.z);
            }
            mesh.vertices = v;
            mesh.normals = n;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mesh.name = "PillowBlock";
            return mesh;
        }
    }
}
