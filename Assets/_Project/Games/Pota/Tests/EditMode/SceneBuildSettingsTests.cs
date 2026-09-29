using System;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;

namespace Project.Tests
{
    public class SceneBuildSettingsTests
    {
        private static string[] Paths(EditorBuildSettingsScene[] s) => Array.ConvertAll(s, x => x.path);

        [Test]
        public void WithScene_AppendsOnce_AndKeepsTheOrder()
        {
            var a = new EditorBuildSettingsScene("Assets/A.unity", true);
            var b = new EditorBuildSettingsScene("Assets/B.unity", true);
            var result = SceneParts.WithScene(new[] { a, b }, "Assets/C.unity");
            CollectionAssert.AreEqual(new[] { "Assets/A.unity", "Assets/B.unity", "Assets/C.unity" }, Paths(result));
            var again = SceneParts.WithScene(result, "Assets/A.unity");
            CollectionAssert.AreEqual(Paths(result), Paths(again), "an existing scene must not move or duplicate");
        }

        [Test]
        public void WithScene_EnablesADisabledEntry()
        {
            var a = new EditorBuildSettingsScene("Assets/A.unity", false);
            var result = SceneParts.WithScene(new[] { a }, "Assets/A.unity");
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(result[0].enabled);
        }
    }
}
