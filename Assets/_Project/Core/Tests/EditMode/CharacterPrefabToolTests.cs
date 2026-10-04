using System.IO;
using System.Linq;
using NUnit.Framework;
using Project.Dev;
using Project.Editor.AITools;
using Project.Juice;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Tests
{
    public class CharacterPrefabToolTests
    {
        private const string Root = "Assets/_Project/Experiments/__charprefab_test__";

        private static string Fixture => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "fixtures", "handoff-rig-clips", "manifest.json"));

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Root);

        private static string Model()
        {
            ImportAssetTool.Import(Fixture, Root + "/delivery");
            return Root + "/delivery/handoff-rig-clips.fbx";
        }

        [Test]
        public void Prefab_HasABodyWrapper_AndAnAnimatorWithOneStatePerClip()
        {
            var r = CharacterPrefabTool.Build(Model(), Root + "/Hero.prefab", toon: false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab);
            var body = prefab.transform.Find("Body");
            Assert.IsNotNull(body.GetComponent<SquashStretch>());
            var animator = body.Find("Model").GetComponent<Animator>();
            Assert.IsNotNull(animator);
            Assert.IsFalse(animator.applyRootMotion);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Hero.controller");
            Assert.AreSame(ctrl, animator.runtimeAnimatorController);
            var names = ctrl.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            CollectionAssert.AreEqual(new[] { "bend", "twist" }, names, "manifest order");
            Assert.AreEqual("bend", ctrl.layers[0].stateMachine.defaultState.name);
        }

        [Test]
        public void Rebuild_KeepsGuids_AndDoesNotDuplicateStates()
        {
            string model = Model();
            CharacterPrefabTool.Build(model, Root + "/Hero.prefab", toon: false);
            string g1 = AssetDatabase.AssetPathToGUID(Root + "/Hero.prefab"), g2 = AssetDatabase.AssetPathToGUID(Root + "/Hero.controller");
            var r = CharacterPrefabTool.Build(model, Root + "/Hero.prefab", toon: false);
            Assert.AreEqual(g1, AssetDatabase.AssetPathToGUID(Root + "/Hero.prefab"));
            Assert.AreEqual(g2, AssetDatabase.AssetPathToGUID(Root + "/Hero.controller"));
            Assert.AreEqual(2, r.states.Count);
            Assert.AreEqual(2, AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Hero.controller").layers[0].stateMachine.states.Length);
        }

        [Test]
        public void Toon_IsAppliedToTheModel()
        {
            var r = CharacterPrefabTool.Build(Model(), Root + "/Hero.prefab", toon: true);
            var renderer = AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab).GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual("Project/Toon", renderer.sharedMaterial.shader.name);
        }

        [Test]
        public void Showcase_SavesTheCharacterAndItsAnimator()
        {
            var r = CharacterPrefabTool.Build(Model(), Root + "/Hero.prefab", toon: false);
            AnimShowcaseTool.Build(r.prefab, Root + "/Show.unity");
            EditorSceneManager.OpenScene(Root + "/Show.unity");
            var show = Object.FindAnyObjectByType<AnimShowcase>();
            Assert.IsNotNull(show);
            Assert.IsNotNull(show.Animator, "animator reference saved in the scene");
        }
    }
}
