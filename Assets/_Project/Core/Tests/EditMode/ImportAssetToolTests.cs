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
    }
}
