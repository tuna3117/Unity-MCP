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
            string path = Path.Combine(dir, "manifest.json");
            File.WriteAllText(path, "{\"id\":\"x\",\"game\":\"Pota\",\"kind\":\"building\",\"files\":{\"glb\":\"x.glb\"}}");
            Assert.Throws<ArgumentException>(() => ImportAssetTool.Import(path, Dest));
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
    }
}
