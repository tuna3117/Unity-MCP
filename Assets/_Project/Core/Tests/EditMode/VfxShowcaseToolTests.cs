using System.IO;
using NUnit.Framework;
using Project.Editor.AITools;
using Project.VFX;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    public class VfxShowcaseToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__vfxshowcase_test__";

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void TheSavedSceneKeepsTheLibrary()
        {
            AIToolsCommon.EnsureFolder(Root);
            var lib = ScriptableObject.CreateInstance<VfxLibrary>();
            AssetDatabase.CreateAsset(lib, Root + "/Lib.asset");
            string guid = AssetDatabase.AssetPathToGUID(Root + "/Lib.asset");
            VfxShowcaseTool.Build(Root + "/Lib.asset", Root + "/Show.unity");
            StringAssert.Contains(guid, File.ReadAllText(Root + "/Show.unity"), "library reference saved in the scene");
            Assert.IsNotNull(Object.FindAnyObjectByType<VfxShowcase>());
        }
    }
}
