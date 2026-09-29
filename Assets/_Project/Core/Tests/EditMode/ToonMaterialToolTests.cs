using System.IO;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    public class ToonMaterialToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__toon_test__";

        [SetUp]
        public void SetUp() => AIToolsCommon.EnsureFolder(Root);

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        [Test]
        public void ToonShader_IsSupported() => Assert.IsTrue(Shader.Find("Project/Toon")?.isSupported ?? false);

        [Test]
        public void Apply_SetsToonMaterialWithTexture_OnEveryRenderer()
        {
            var tex = new Texture2D(4, 4);
            File.WriteAllBytes(Root + "/base.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(Root + "/base.png");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrefabUtility.SaveAsPrefabAsset(cube, Root + "/hero.prefab");
            Object.DestroyImmediate(cube);

            ToonMaterialTool.Apply(Root + "/hero.prefab", texturePath: Root + "/base.png");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/hero.prefab");
            var material = prefab.GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual("Project/Toon", material.shader.name);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/base.png"), material.GetTexture("_BaseMap"));
            Assert.IsTrue(File.Exists(Root + "/hero_Toon.mat"));
        }
    }
}
