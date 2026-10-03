using System;
using System.IO;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    public class ImportAssetToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__handoff_test__";
        private const string Dest = Root + "/cube";

        private static string Fixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-cube", "manifest.json"));

        private static string RigFixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-rig", "manifest.json"));

        [Test]
        public void RiggedFixture_BonesAndClipsMatch()
        {
            var r = ImportAssetTool.Import(RigFixture, Root + "/rig");
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            Assert.AreEqual(3, r.unityBones);
            Assert.AreEqual(1, r.unityClips.Count);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void Import_CopiesFiles_AndMatchesManifest()
        {
            var r = ImportAssetTool.Import(Fixture, Dest);
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            Assert.AreEqual(12, r.unityTris);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(Dest + "/handoff-cube.glb"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(Dest + "/manifest.json"));
        }

        [Test]
        public void Reimport_KeepsGuid()
        {
            ImportAssetTool.Import(Fixture, Dest);
            string before = AssetDatabase.AssetPathToGUID(Dest + "/handoff-cube.glb");
            ImportAssetTool.Import(Fixture, Dest);
            Assert.AreEqual(before, AssetDatabase.AssetPathToGUID(Dest + "/handoff-cube.glb"));
        }

        [Test]
        public void UnknownKind_Throws()
        {
            string dir = Path.Combine(Path.GetTempPath(), "handoff_bad_kind");
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "manifest.json");
                File.WriteAllText(path, "{\"id\":\"x\",\"game\":\"Pota\",\"kind\":\"building\",\"files\":{\"glb\":\"x.glb\"}}");
                Assert.Throws<ArgumentException>(() => ImportAssetTool.Import(path, Dest));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
            Assert.IsFalse(Directory.Exists(dir), "the test leaves no temp folder behind");
        }

        [Test]
        public void Folder_EmptyOrMissingDest_IsTheDefault()
        {
            Assert.AreEqual("Assets/_Project/Games/Pota/Art/Props/hoop", ImportAssetTool.TargetFolder(null, "Pota", "Props", "hoop"));
            Assert.AreEqual("Assets/_Project/Games/Pota/Art/Props/hoop", ImportAssetTool.TargetFolder("", "Pota", "Props", "hoop"));
            Assert.AreEqual("Assets/_Project/Games/Pota/Art/Props/hoop", ImportAssetTool.TargetFolder("  ", "Pota", "Props", "hoop"));
            Assert.AreEqual("Assets/X/y", ImportAssetTool.TargetFolder("Assets/X/y/", "Pota", "Props", "hoop"));
        }

        [Test]
        public void Folder_OutsideAssets_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => ImportAssetTool.TargetFolder("/tmp/x", "Pota", "Props", "hoop"));
            Assert.Throws<ArgumentException>(() => ImportAssetTool.TargetFolder("Packages/x", "Pota", "Props", "hoop"));
            Assert.Throws<ArgumentException>(() => ImportAssetTool.TargetFolder("Assets/../x", "Pota", "Props", "hoop"));
            Assert.Throws<ArgumentException>(() => ImportAssetTool.Import(Fixture, "/tmp/x"));
        }

        private static string RigFbxFixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-rig-fbx", "manifest.json"));

        [Test]
        public void GlbAndFbx_AreEachMeasured_AndMatch()
        {
            var r = ImportAssetTool.Import(RigFbxFixture, Root + "/both");
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            CollectionAssert.AreEquivalent(new[] { "handoff-rig-fbx.glb", "handoff-rig-fbx.fbx" }, r.models.ConvertAll(m => m.file));
            foreach (var m in r.models)
            {
                Assert.AreEqual(164, m.tris, m.file);
                Assert.AreEqual(3, m.bones, m.file);
                Assert.AreEqual(1, m.clips.Count, m.file);
            }
        }

        [Test]
        public void Redelivery_WithoutTheFbx_RemovesTheStaleFbx()
        {
            ImportAssetTool.Import(RigFbxFixture, Root + "/stale");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/stale/handoff-rig-fbx.fbx"));
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "handoff-rig-glb-only"));
            Directory.CreateDirectory(dir);
            File.Copy(Path.Combine(Path.GetDirectoryName(RigFbxFixture), "handoff-rig-fbx.glb"), Path.Combine(dir, "handoff-rig-fbx.glb"), true);
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                File.ReadAllText(RigFbxFixture).Replace("\"fbx\": \"handoff-rig-fbx.fbx\"", "\"fbx\": null"));
            var r = ImportAssetTool.Import(Path.Combine(dir, "manifest.json"), Root + "/stale");
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/stale/handoff-rig-fbx.fbx"), "stale fbx removed");
            CollectionAssert.AreEqual(new[] { Root + "/stale/handoff-rig-fbx.fbx" }, r.removed);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/stale/handoff-rig-fbx.glb"));
        }

        [Test]
        public void Redelivery_KeepsFilesTheManifestNeverListed()
        {
            ImportAssetTool.Import(Fixture, Dest);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, Dest + "/handoff-cube_Toon.mat");
            var r = ImportAssetTool.Import(Fixture, Dest);
            Assert.IsEmpty(r.removed);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(Dest + "/handoff-cube_Toon.mat"), "materials made beside a hand-off stay");
        }

        [Test]
        public void DefaultFolder_FollowsGameAndKind()
        {
            Assert.AreEqual("Props", ImportAssetTool.KindFolders["prop"]);
            Assert.AreEqual("Characters", ImportAssetTool.KindFolders["character"]);
        }
    
        private static string FlipbookFixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures",
            "handoff-flipbook", "manifest.json"));

        /// <summary>A copy of the flipbook fixture with <paramref name="edit"/> applied to its manifest text, in Temp.</summary>
        private static string FlipbookVariant(string name, Func<string, string> edit)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "handoff-flipbook-" + name));
            Directory.CreateDirectory(dir);
            string src = Path.GetDirectoryName(FlipbookFixture);
            File.Copy(Path.Combine(src, "fixture-flipbook.png"), Path.Combine(dir, "fixture-flipbook.png"), true);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), edit(File.ReadAllText(FlipbookFixture)));
            return Path.Combine(dir, "manifest.json");
        }

        [Test]
        public void Flipbook_CopiesTheSheet_ImportsItForParticles_AndMatches()
        {
            var r = ImportAssetTool.Import(FlipbookFixture, Root + "/flipbook");
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            CollectionAssert.AreEqual(new[] { 128, 128 }, r.sheetSize);
            CollectionAssert.AreEqual(new[] { 128, 128 }, r.manifestSheetSize);
            string sheet = Root + "/flipbook/fixture-flipbook.png";
            Assert.AreEqual(sheet, r.main);
            var imp = (TextureImporter)AssetImporter.GetAtPath(sheet);
            Assert.IsTrue(imp.alphaIsTransparency);
            Assert.AreEqual(TextureWrapMode.Clamp, imp.wrapMode);
            Assert.IsTrue(imp.sRGBTexture, "linear: false → sRGB");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/flipbook/manifest.json"));
        }

        [Test]
        public void Flipbook_Linear_ImportsWithoutSrgb()
        {
            string manifest = FlipbookVariant("linear", m => m.Replace("\"linear\": false", "\"linear\": true"));
            ImportAssetTool.Import(manifest, Root + "/linear");
            var imp = (TextureImporter)AssetImporter.GetAtPath(Root + "/linear/fixture-flipbook.png");
            Assert.IsFalse(imp.sRGBTexture);
            foreach (string platform in new[] { "iPhone", "Android" })
            {
                var s = imp.GetPlatformTextureSettings(platform);
                Assert.IsTrue(s.overridden, platform);
                Assert.AreEqual(TextureImporterFormat.ASTC_4x4, s.format, platform + ": data sheets keep 8 bpp");
            }
        }

        [Test]
        public void Flipbook_IsAstcCompressedOnPhones()
        {
            ImportAssetTool.Import(FlipbookFixture, Root + "/astc");
            var imp = (TextureImporter)AssetImporter.GetAtPath(Root + "/astc/fixture-flipbook.png");
            foreach (string platform in new[] { "iPhone", "Android" })
            {
                var s = imp.GetPlatformTextureSettings(platform);
                Assert.IsTrue(s.overridden, platform);
                Assert.AreEqual(TextureImporterFormat.ASTC_6x6, s.format, platform);
            }
            Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression, "the editor keeps the exact sheet");
        }

        [Test]
        public void Reapply_ConfiguresEveryFlipbookUnderAFolder_AndSkipsModels()
        {
            ImportAssetTool.Import(FlipbookFixture, Root + "/again/flipbook");
            ImportAssetTool.Import(Fixture, Root + "/again/cube");
            var imp = (TextureImporter)AssetImporter.GetAtPath(Root + "/again/flipbook/fixture-flipbook.png");
            imp.ClearPlatformTextureSettings("iPhone");
            imp.SaveAndReimport();
            var done = ImportAssetTool.ReapplySheetSettings(Root + "/again");
            CollectionAssert.AreEqual(new[] { Root + "/again/flipbook/fixture-flipbook.png" }, done);
            imp = (TextureImporter)AssetImporter.GetAtPath(Root + "/again/flipbook/fixture-flipbook.png");
            Assert.AreEqual(TextureImporterFormat.ASTC_6x6, imp.GetPlatformTextureSettings("iPhone").format);
        }

        [Test]
        public void Flipbook_WrongFrameSize_IsADifference()
        {
            string manifest = FlipbookVariant("size", m => m.Replace("\"frame_px\": 64", "\"frame_px\": 32"));
            var r = ImportAssetTool.Import(manifest, Root + "/size");
            Assert.IsFalse(r.matches);
            StringAssert.Contains("sheet", string.Join("; ", r.differences));
        }

        [Test]
        public void Flipbook_WithoutSheet_IsRefused()
        {
            string manifest = FlipbookVariant("nosheet", m => m.Replace("\"sheet\": \"fixture-flipbook.png\", ", ""));
            Assert.Throws<ArgumentException>(() => ImportAssetTool.Import(manifest, Root + "/nosheet"));
        }

        [Test]
        public void Flipbook_FrameHeight_MeasuresAWideSheet()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "handoff-flipbook-wide"));
            Directory.CreateDirectory(dir);
            var tex = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            File.WriteAllBytes(Path.Combine(dir, "fixture-flipbook.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), File.ReadAllText(FlipbookFixture).Replace(
                "\"cols\": 2, \"rows\": 2, \"frames\": 4, \"frame_px\": 64,",
                "\"cols\": 1, \"rows\": 1, \"frames\": 1, \"frame_px\": 64, \"frame_h\": 32,"));
            var r = ImportAssetTool.Import(Path.Combine(dir, "manifest.json"), Root + "/wide");
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            CollectionAssert.AreEqual(new[] { 64, 32 }, r.sheetSize);
            CollectionAssert.AreEqual(new[] { 64, 32 }, r.manifestSheetSize);
        }

        [Test]
        public void Verify_MeasuresAnImportedHandoff_WithoutCopying()
        {
            ImportAssetTool.Import(Fixture, Dest);
            var r = ImportAssetTool.Verify(Dest + "/manifest.json", Dest);
            Assert.IsTrue(r.matches, string.Join("; ", r.differences));
            Assert.AreEqual(12, r.unityTris);
            Assert.AreEqual(Dest + "/handoff-cube.glb", r.main);
            Assert.IsEmpty(r.copied);
        }
    }
}
