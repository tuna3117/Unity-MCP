using Project.Basket.Visual;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Static scenery: navy back plate, orange rounded frame, launcher strip under the frame and the sky
    /// gradient behind everything. Board space: cell (row, col) → x = col − (n−1)/2, y = (n−1)/2 − row, z = 0;
    /// the camera looks down +z, so "toward the viewer" is −z.
    /// </summary>
    public static class BoardBuilder
    {
        public const float Margin = 0.42f;

        public static Vector2 CellPosition(int n, int row, int col) => new Vector2(col - (n - 1) * 0.5f, (n - 1) * 0.5f - row);

        public static Transform Build(Transform root, ColorHuntSettings s, int n)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(root, false);

            float outer = n + 2f * Margin;
            var plateMat = RimMeshBuilder.UrpLit(s.PlateColor, 0.25f);
            var plate = RimMeshBuilder.MeshObject("Plate", scenery, RoundedBoxMesh.Create(new Vector3(outer - 0.3f, outer - 0.3f, 0.5f), 0.08f, 4), plateMat);
            plate.transform.localPosition = new Vector3(0f, 0f, 0.62f);
            plate.GetComponent<MeshRenderer>().receiveShadows = true;

            var frameMat = RimMeshBuilder.UrpLit(s.FrameColor, 0.45f);
            var barMeshH = RoundedBoxMesh.Create(new Vector3(outer, Margin, 0.5f), 0.07f, 4);
            var barMeshV = RoundedBoxMesh.Create(new Vector3(Margin, n + 0.02f, 0.5f), 0.07f, 4);
            Bar(scenery, "FrameTop", barMeshH, frameMat, new Vector3(0f, n * 0.5f + Margin * 0.5f, 0.12f));
            Bar(scenery, "FrameBottom", barMeshH, frameMat, new Vector3(0f, -(n * 0.5f + Margin * 0.5f), 0.12f));
            Bar(scenery, "FrameLeft", barMeshV, frameMat, new Vector3(-(n * 0.5f + Margin * 0.5f), 0f, 0.12f));
            Bar(scenery, "FrameRight", barMeshV, frameMat, new Vector3(n * 0.5f + Margin * 0.5f, 0f, 0.12f));

            // Launcher strip: the balls come from here.
            var stripMat = RimMeshBuilder.UrpLit(new Color(0.16f, 0.30f, 0.55f), 0.35f);
            var strip = RimMeshBuilder.MeshObject("Launcher", scenery, RoundedBoxMesh.Create(new Vector3(n * 0.7f, 0.42f, 0.5f), 0.12f, 4), stripMat);
            strip.transform.localPosition = new Vector3(0f, -(n * 0.5f + Margin + 0.55f), 0.2f);
            var lipMat = RimMeshBuilder.UrpLit(s.GoldColor, 0.6f);
            var lip = RimMeshBuilder.MeshObject("LauncherLip", scenery, RoundedBoxMesh.Create(new Vector3(n * 0.7f + 0.1f, 0.08f, 0.56f), 0.03f, 2), lipMat);
            lip.transform.localPosition = strip.transform.localPosition + new Vector3(0f, 0.2f, 0f);

            // Sky: a big gradient quad far behind the plate (unlit).
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            Object.Destroy(sky.GetComponent<Collider>());
            sky.transform.SetParent(scenery, false);
            sky.transform.localPosition = new Vector3(0f, 0f, 40f);
            sky.transform.localScale = new Vector3(160f, 160f, 1f);
            var skyMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            skyMat.mainTexture = GradientTexture(s.SkyTop, s.SkyBottom);
            sky.GetComponent<MeshRenderer>().sharedMaterial = skyMat;
            sky.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return scenery;
        }

        private static void Bar(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = RimMeshBuilder.MeshObject(name, parent, mesh, mat);
            go.transform.localPosition = pos;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }

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
