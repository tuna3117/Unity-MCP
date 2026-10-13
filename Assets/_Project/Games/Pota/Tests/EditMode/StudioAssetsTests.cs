using System.IO;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The Blender hand-offs for the studio and special looks are in the project and wired into the settings asset.</summary>
    public class StudioAssetsTests
    {
        private const string Hint = "run the Blender tools (plan Tasks 1–3), then execute_custom_tool ai_studio_import";
        private const string SpecialHint = "run the Blender tools (plan 2026-10-01 Tasks 7–8), then execute_custom_tool ai_studio_import";

        private static readonly string[] SpecialIds =
        {
            "vfx-fire-toon", "vfx-flame-jet", "vfx-dust-ring", "vfx-speed-lines", "vfx-floor-crack",
            "vfx-letter-slam", "vfx-letter-swish", "vfx-letter-combo",
            "vfx-ball-halves", "vfx-bomb-shell", "vfx-coin", "vfx-basketball",
        };

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

        [Test]
        public void SpecialHandoffs_AreImported_AndMatchTheirManifests()
        {
            foreach (string id in SpecialIds)
            {
                CollectionAssert.Contains(StudioImportTool.Ids, id, "ai_studio_import must bring " + id);
                string folder = StudioImportTool.Folder(id);
                Assert.IsTrue(File.Exists(folder + "/manifest.json"), id + ": " + SpecialHint);
                var r = ImportAssetTool.Verify(folder + "/manifest.json", folder);
                Assert.IsTrue(r.matches, id + ": " + string.Join("; ", r.differences));
            }
        }

        [Test]
        public void SpecialSheets_HaveTheContractSizes()
        {
            var s = Settings();
            var sheets = new (Texture2D tex, string field, int w, int h)[]
            {
                (s.RoyalFire, "RoyalFire", 1024, 1024), (s.FlameJet, "FlameJet", 1024, 512), (s.CourtDust, "CourtDust", 1024, 1024),
                (s.SpeedLines, "SpeedLines", 1024, 512), (s.FloorCrack, "FloorCrack", 512, 512),
                (s.LetterSlam, "LetterSlam", 512, 256), (s.LetterSwish, "LetterSwish", 512, 256), (s.LetterCombo, "LetterCombo", 512, 256),
            };
            foreach (var (tex, field, w, h) in sheets)
            {
                Assert.IsNotNull(tex, field + ": " + SpecialHint);
                Assert.AreEqual(w, tex.width, field);
                Assert.AreEqual(h, tex.height, field);
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                Assert.IsTrue(imp.alphaIsTransparency, field);
                Assert.IsTrue(imp.sRGBTexture, field + " holds colour: sRGB");
            }
            StringAssert.Contains("\"loop\": true", File.ReadAllText(StudioImportTool.Folder("vfx-flame-jet") + "/manifest.json"),
                "the flame jet is the looping sheet");
        }

        [Test]
        public void RoyalHalves_AreTwoHalvesWithTheirNoseAlongPlusX()
        {
            var model = Settings().RoyalHalves;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            CollectionAssert.AreEqual(new[] { "HalfA", "HalfB" }, parts.ConvertAll(p => p.Name));
            foreach (var p in parts)
            {
                var b = p.Mesh.bounds;
                Assert.AreEqual(0f, b.min.x, 0.002f, p.Name + ": the flat cap sits at x = 0 (origin at the cap centre)");
                Assert.AreEqual(0.225f, b.max.x, 0.005f, p.Name + ": the dome (nose) points to +X");
                Assert.AreEqual(0.45f, b.size.y, 0.01f, p.Name);
                Assert.AreEqual(0.45f, b.size.z, 0.01f, p.Name);
                int cap = 0;
                foreach (var n in p.Mesh.normals) if (n.x < -0.99f) cap++;
                Assert.Greater(cap, 8, p.Name + ": the cap faces −X");
                Assert.AreEqual(p.Mesh.vertexCount, p.Mesh.uv.Length, p.Name + " UV0");
                Assert.LessOrEqual((int)p.Mesh.GetIndexCount(0) / 3, 600, p.Name);
            }
            AssertSkin(model, 512, 256);
        }

        [Test]
        public void RoyalShell_IsEightPiecesOfABall()
        {
            var model = Settings().RoyalShell;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            Assert.AreEqual(8, parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                Assert.AreEqual($"piece_{i:00}", parts[i].Name);
                Assert.AreEqual(parts[i].Mesh.vertexCount, parts[i].Mesh.uv.Length, parts[i].Name + " UV0");
                Assert.Less(parts[i].Local.magnitude, 0.25f, parts[i].Name + ": its centre lies inside the 0.45 m ball");
                Assert.Greater(parts[i].Local.magnitude, 0.05f, parts[i].Name + ": a shell piece sits off the centre");
            }
            AssertSkin(model, 512, 256);
        }

        [Test]
        public void RoyalCoin_IsAGoldDiscFacingZ()
        {
            var model = Settings().RoyalCoin;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            Assert.AreEqual(1, parts.Count);
            var size = parts[0].Mesh.bounds.size;
            Assert.AreEqual(0.30f, size.x, 0.005f);
            Assert.AreEqual(0.30f, size.y, 0.005f);
            Assert.AreEqual(0.044f, size.z, 0.004f, "the coin's faces look along ±z");
            AssertSkin(model, 256, 128);
        }

        [Test]
        public void CourtBall_IsALowPolyBall()
        {
            var model = Settings().CourtBall;
            Assert.IsNotNull(model, SpecialHint);
            var parts = SpecialMeshes.Parts(model);
            Assert.AreEqual(1, parts.Count);
            Assert.LessOrEqual((int)parts[0].Mesh.GetIndexCount(0) / 3, 1500);
            Assert.AreEqual(0.45f, parts[0].Mesh.bounds.size.x, 0.01f);
            Assert.AreEqual(0.45f, parts[0].Mesh.bounds.size.y, 0.01f);
            AssertSkin(model, 512, 256);
        }

        /// <summary>The GLB carries its skin, and the toon material made from the template wears it.</summary>
        private static void AssertSkin(GameObject model, int w, int h)
        {
            var skin = SpecialMeshes.Skin(model);
            Assert.IsNotNull(skin, model.name + ": no embedded base-colour texture");
            Assert.AreEqual(w, skin.width, model.name);
            Assert.AreEqual(h, skin.height, model.name);
            var m = SpecialMeshes.Toon(Settings(), model, "test");
            Assert.AreEqual("Project/Toon", m.shader.name);
            Assert.AreEqual(skin, m.GetTexture("_BaseMap"));
            Object.DestroyImmediate(m);
        }

        private const string FeelHint = "run the Blender recipes (plan 2026-10-03 Task 7: cloud_set, bird_flock, city_band, lettering miss), then execute_custom_tool ai_studio_import";

        [Test]
        public void FeelSkyProps_AreImportedAndWired()
        {
            var s = Settings();
            Assert.IsNotNull(s.LetterMiss, FeelHint);
            Assert.AreEqual(512, s.LetterMiss.width);
            Assert.AreEqual(256, s.LetterMiss.height);
            Assert.IsNotNull(s.Clouds, FeelHint);
            Assert.AreEqual(3, s.Clouds.GetComponentsInChildren<MeshFilter>(true).Length, "three clouds");
            Assert.IsNotNull(s.Birds, FeelHint);
            Assert.IsNotNull(s.Birds.GetComponentInChildren<SkinnedMeshRenderer>(true), "a skinned flock");
            Assert.IsNotNull(s.BirdFlap, FeelHint);
            Assert.AreEqual("flap", s.BirdFlap.name);
            Assert.IsTrue(s.BirdFlap.isLooping, "the flap loops");
            Assert.IsNotNull(s.CityBand, FeelHint);
            Assert.GreaterOrEqual(s.CityBand.GetComponentsInChildren<MeshFilter>(true).Length, 28, "28 buildings plus window strips");
        }

        private const string ComboHint = "run the Blender lettering recipe (Pota plan 2026-10-13 Task 5), then execute_custom_tool ai_studio_import";

        /// <summary>Pota spec 2026-10-13 §3 (plan P2, P3, P6): five 1024×512 letterings, the toon ring, the pillow pieces — imported, matching, wired.</summary>
        [Test]
        public void ComboHandoffs_AreImportedAndWired()
        {
            foreach (string id in new[] { "vfx-letter-crossover", "vfx-letter-showtime", "vfx-letter-mega-slam", "vfx-letter-alley-oop",
                         "vfx-letter-fast-break", "vfx-ring-toon", "vfx-pillow-shatter" })
            {
                CollectionAssert.Contains(StudioImportTool.Ids, id, "ai_studio_import must bring " + id);
                string folder = StudioImportTool.Folder(id);
                Assert.IsTrue(File.Exists(folder + "/manifest.json"), id + ": " + ComboHint);
                var r = ImportAssetTool.Verify(folder + "/manifest.json", folder);
                Assert.IsTrue(r.matches, id + ": " + string.Join("; ", r.differences));
            }
            var s = Settings();
            foreach (var (tex, field) in new[] { (s.LetterCrossover, "LetterCrossover"), (s.LetterShowtime, "LetterShowtime"),
                         (s.LetterMegaSlam, "LetterMegaSlam"), (s.LetterAlleyOop, "LetterAlleyOop"), (s.LetterFastBreak, "LetterFastBreak") })
            {
                Assert.IsNotNull(tex, field + ": " + ComboHint);
                Assert.AreEqual(1024, tex.width, field);
                Assert.AreEqual(512, tex.height, field);
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                Assert.IsTrue(imp.alphaIsTransparency, field);
                Assert.IsTrue(imp.sRGBTexture, field + " holds colour: sRGB");
            }
            Assert.IsNotNull(s.ComboRing, ComboHint);
            Assert.AreEqual(s.CandyRing, s.ComboRing, "the ring is vfx-ring-toon (plan P2)");
            Assert.IsNotNull(s.ComboPieces, ComboHint);
            Assert.AreEqual(s.ShatterPieces, s.ComboPieces, "the chunks are vfx-pillow-shatter (plan P6)");
            Assert.GreaterOrEqual(SpecialMeshes.Parts(s.ComboPieces).Count, 3, "the first three pieces fly");
        }
    }
}
