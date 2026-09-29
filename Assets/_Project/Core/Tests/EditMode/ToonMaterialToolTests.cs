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

        [Test]
        public void Apply_KeepsBaseMap_WhenTheMaterialPathIsReused()
        {
            // The material was written to disk before its base map was set; when the same path was deleted and reused,
            // saving the prefab reloaded it from disk and every second Apply lost the texture.
            for (int round = 0; round < 3; round++)
            {
                var tex = new Texture2D(4, 4);
                File.WriteAllBytes(Root + "/base.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(Root + "/base.png");
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                PrefabUtility.SaveAsPrefabAsset(cube, Root + "/hero.prefab");
                Object.DestroyImmediate(cube);

                var material = ToonMaterialTool.Apply(Root + "/hero.prefab", texturePath: Root + "/base.png");

                Assert.IsNotNull(material.GetTexture("_BaseMap"), "round " + round);
                AssetDatabase.DeleteAsset(Root);
                AIToolsCommon.EnsureFolder(Root);
            }
        }

        [Test]
        public void ToonShader_HasDepthNormalsPass()
        {
            // Without it the character is missing from the depth-normals texture (SSAO, depth-reading effects).
            var material = new Material(Shader.Find("Project/Toon"));
            try { Assert.GreaterOrEqual(material.FindPass("DepthNormals"), 0); }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void Apply_MissingTexture_Throws()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrefabUtility.SaveAsPrefabAsset(cube, Root + "/hero.prefab");
            Object.DestroyImmediate(cube);
            Assert.Throws<System.ArgumentException>(() => ToonMaterialTool.Apply(Root + "/hero.prefab", texturePath: Root + "/nope.png"));
        }

        [Test]
        public void Apply_LeavesParticleRenderersAlone()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var fx = new GameObject("fx");
            fx.transform.SetParent(cube.transform);
            fx.AddComponent<ParticleSystem>();
            var particleMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(particleMaterial, Root + "/fx.mat");
            fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            PrefabUtility.SaveAsPrefabAsset(cube, Root + "/hero.prefab");
            Object.DestroyImmediate(cube);

            ToonMaterialTool.Apply(Root + "/hero.prefab");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/hero.prefab");
            Assert.AreEqual("Project/Toon", prefab.GetComponent<Renderer>().sharedMaterial.shader.name);
            Assert.AreEqual(particleMaterial, prefab.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial);
        }

        [Test]
        public void Apply_TakesTheBaseMapFromAModelsMaterial()
        {
            var tex = new Texture2D(4, 4);
            File.WriteAllBytes(Root + "/albedo.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(Root + "/albedo.png");
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            source.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/albedo.png"));
            AssetDatabase.CreateAsset(source, Root + "/model.mat");  // stands in for a glb/fbx's imported material
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrefabUtility.SaveAsPrefabAsset(cube, Root + "/hero.prefab");
            Object.DestroyImmediate(cube);

            var material = ToonMaterialTool.Apply(Root + "/hero.prefab", modelPath: Root + "/model.mat");

            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/albedo.png"), material.GetTexture("_BaseMap"));
        }
    }
}
