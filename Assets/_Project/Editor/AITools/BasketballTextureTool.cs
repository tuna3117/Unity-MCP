using System.IO;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>Generates an equirectangular basketball texture (orange with dark seams) and a URP material.</summary>
    [McpForUnityTool("ai_basket_ball_texture", Description =
        "Generate Assets/_Project/Art/Generated/Basketball.png (equirect 1024x512, orange with seams) and Basketball.mat (URP Lit). overwrite=true regenerates.")]
    public static class BasketballTextureTool
    {
        public const string Folder = "Assets/_Project/Art/Generated";
        public const string TexturePath = Folder + "/Basketball.png";
        public const string MaterialPath = Folder + "/Basketball.mat";

        public class Parameters
        {
            [ToolParameter("Regenerate even if the files exist (default false)", Required = false)]
            public bool? overwrite { get; set; }
        }

        public static object HandleCommand(JObject p)
        {
            var mat = Ensure(AIToolsCommon.GetBool(p, "overwrite", false));
            return new SuccessResponse($"Basketball material ready at {MaterialPath}", new { texture = TexturePath, material = MaterialPath, ok = mat != null });
        }

        [MenuItem("AI Tools/Basket/Generate Basketball Texture")]
        public static void Menu() => Ensure(true);

        public static Material Ensure(bool overwrite)
        {
            AIToolsCommon.EnsureFolder(Folder);
            if (overwrite || AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null)
            {
                var tex = Generate(1024, 512);
                File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
                if (importer != null) { importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.SaveAndReimport(); }
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetTexture("_BaseMap", texture);
            mat.color = Color.white;
            mat.SetFloat("_Smoothness", 0.42f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Texture2D Generate(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var orange = new Color(0.94f, 0.48f, 0.16f);
            var seam = new Color(0.16f, 0.09f, 0.05f);
            var rng = new System.Random(3);
            float seamHalf = 0.007f; // in uv units
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;              // 0 bottom .. 1 top
                float lat = (v - 0.5f) * Mathf.PI;     // -pi/2 .. pi/2
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float lon = u * Mathf.PI * 2f;
                    // point on sphere
                    float cx = Mathf.Cos(lat) * Mathf.Cos(lon), cy = Mathf.Sin(lat), cz = Mathf.Cos(lat) * Mathf.Sin(lon);
                    // seams: equator (y=0), meridian pair (x=0), second meridian pair (z=0), plus two curved seams (great circles tilted 45deg)
                    float d = Mathf.Min(Mathf.Abs(cy), Mathf.Abs(cx));
                    d = Mathf.Min(d, Mathf.Abs(cz));
                    d = Mathf.Min(d, Mathf.Abs((cx + cz) * 0.7071f) * 0.5f + Mathf.Abs(cy) * 0.5f);
                    float pebble = 1f + ((float)rng.NextDouble() - 0.5f) * 0.10f;
                    var c = orange * pebble;
                    float s = Mathf.Clamp01((d - seamHalf * 1.4f) / (seamHalf * 0.8f));
                    c = Color.Lerp(seam, c, s);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
