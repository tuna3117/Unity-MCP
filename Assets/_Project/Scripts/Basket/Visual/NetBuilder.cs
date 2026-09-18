using UnityEngine;

namespace Project.Basket.Visual
{
    /// <summary>Rings plus twisted strings hanging from the rim, all under one root for the wobble animation.</summary>
    public static class NetBuilder
    {
        public static void Build(Transform netRoot, float rimRadius, Material mat, int rings = 5, int strings = 12)
        {
            float drop = 0.11f;
            float[] radii = new float[rings + 1];
            float[] ys = new float[rings + 1];
            for (int k = 0; k <= rings; k++)
            {
                radii[k] = rimRadius * (1f - k * 0.11f);
                ys[k] = -drop * k;
            }
            for (int k = 1; k <= rings; k++)
            {
                var ring = RimMeshBuilder.MeshObject($"Ring{k}", netRoot, RimMeshBuilder.Torus(radii[k], 0.01f, 24, 6), mat);
                ring.transform.localPosition = new Vector3(0f, ys[k], 0f);
            }
            var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            for (int i = 0; i < strings; i++)
            {
                for (int k = 0; k < rings; k++)
                {
                    float a0 = (i + 0.5f * (k % 2)) / strings * Mathf.PI * 2f;
                    float a1 = (i + 0.5f * ((k + 1) % 2)) / strings * Mathf.PI * 2f;
                    var p0 = new Vector3(Mathf.Cos(a0) * radii[k], ys[k], Mathf.Sin(a0) * radii[k]);
                    var p1 = new Vector3(Mathf.Cos(a1) * radii[k + 1], ys[k + 1], Mathf.Sin(a1) * radii[k + 1]);
                    var seg = RimMeshBuilder.MeshObject($"S{i}_{k}", netRoot, cyl, mat);
                    var mid = (p0 + p1) * 0.5f;
                    var dir = p1 - p0;
                    seg.transform.localPosition = mid;
                    seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                    seg.transform.localScale = new Vector3(0.012f, dir.magnitude * 0.5f, 0.012f);
                }
            }
        }
    }
}
