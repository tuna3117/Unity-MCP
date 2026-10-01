using System;
using NUnit.Framework;
using Project.Editor;
using Project.Editor.AITools;
using UnityEditor;

namespace Project.Tests
{
    /// <summary>The demo scene is in the build and in Pota's profile (SceneBootTests then boots it; ai_build ships it in dev builds).</summary>
    public class SpecialsSceneToolTests
    {
        [Test]
        public void SpecialsScene_IsInBuildSettings_AndThePotaProfile()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(SpecialsSceneTool.ScenePath), "run ai_specials_build_scene");
            Assert.IsTrue(Array.Exists(EditorBuildSettings.scenes, s => s.path == SpecialsSceneTool.ScenePath && s.enabled));
            var profile = GameProfile.Find("Pota");
            Assert.IsNotNull(profile);
            Assert.IsTrue(Array.Exists(profile.Scenes, s => s != null && AssetDatabase.GetAssetPath(s) == SpecialsSceneTool.ScenePath));
            Assert.AreEqual("Assets/_Project/Games/Pota/Scenes/ColorHunt.unity", ColorHuntSceneTool.ScenePath, "the scene the demo copies");
        }
    }
}
