using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>
    /// Every scene in Build Settings must load from disk and run a few seconds without logging an error or exception.
    /// Catches references that only break once a scene comes back from disk (e.g. a tool-set field that was not serialized).
    /// </summary>
    public class SceneBootTests
    {
        private const float RunSeconds = 3f;

        public static IEnumerable<string> BuildScenes()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (!path.Contains("InitTestScene")) yield return path; // the test runner's own temporary scene
            }
        }

        [UnityTest]
        public IEnumerator Boots_WithoutErrors([ValueSource(nameof(BuildScenes))] string scenePath)
        {
            SceneManager.LoadScene(scenePath);
            yield return null;
            Assert.AreEqual(scenePath, SceneManager.GetActiveScene().path, "scene did not load");
            float t = 0f;
            while (t < RunSeconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            // Any error or exception logged meanwhile fails the test (Unity Test Framework LogAssert).
        }
    }
}
