using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The Blender hand-offs for the Candy and Shatter looks are in the project and wired into the settings asset.</summary>
    public class StudioAssetsTests
    {
        private const string Hint = "run the Blender tools (plan Tasks 1–3), then execute_custom_tool ai_studio_import";

        private static ColorHuntSettings Settings() =>
            AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");

        [Test]
        public void Flipbooks_Are4x4Of256_WithAlpha()
        {
            var s = Settings();
            foreach (var tex in new[] { s.CandyPuff, s.CandyRing, s.ShatterSmoke, s.ShockRing })
            {
                Assert.IsNotNull(tex, Hint);
                Assert.AreEqual(1024, tex.width, tex.name);
                Assert.AreEqual(1024, tex.height, tex.name);
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                Assert.IsTrue(imp.alphaIsTransparency, tex.name);
            }
            Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.ShockRing))).sRGBTexture,
                "the shock ring holds normals: linear");
        }

        [Test]
        public void ShatterPieces_AreTenMeshesWithTheFrontUvSet()
        {
            var model = Settings().ShatterPieces;
            Assert.IsNotNull(model, Hint);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            Assert.AreEqual(10, filters.Length);
            bool anyFront = false;
            foreach (var f in filters)
            {
                var mesh = f.sharedMesh;
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length, f.name + " UV0");
                Assert.AreEqual(mesh.vertexCount, mesh.uv2.Length, f.name + " UV1 (front flag)");
                foreach (var flag in mesh.uv2) anyFront |= flag.x > 0.5f;
            }
            Assert.IsTrue(anyFront, "no piece carries a front face");
        }
    }
}
