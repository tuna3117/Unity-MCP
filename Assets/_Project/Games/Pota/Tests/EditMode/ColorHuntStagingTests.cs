using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class ColorHuntStagingTests
    {
        [Test]
        public void FitPosition_MovesWithTheAnchor()
        {
            var home = ColorHuntCamera.FitPosition(38f, 9f / 16f, Vector3.zero);
            var anchor = new Vector3(0f, 8.5f, 30f);
            var moved = ColorHuntCamera.FitPosition(38f, 9f / 16f, anchor);
            Assert.That(Vector3.Distance(moved - anchor, home), Is.LessThan(1e-4f));
        }

        [Test]
        public void PotaFit_Portrait()
        {
            // 9:16, no notch (HUD band top 87 CSS px, buttons 160): 24.897 m in front of the front plane, looking 1.2 m below the board centre.
            var p = ColorHuntCamera.FitPosition(38f, 9f / 16f, Vector3.zero);
            Assert.AreEqual(-(0.45f + 24.897f), p.z, 0.01f);
            Assert.AreEqual(-1.2026f, p.y, 0.005f);
            Assert.AreEqual(0f, p.x, 1e-5f);
        }

        [Test]
        public void PotaFit_FrameOuterWidth_Fills98Point5PercentOfTheScreen()
        {
            var go = new GameObject("Cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 38f;
                cam.aspect = 9f / 16f;
                go.transform.position = ColorHuntCamera.FitPosition(38f, 9f / 16f, Vector3.zero);
                var right = cam.WorldToViewportPoint(new Vector3(4.75f, 0f, -0.45f));   // frame's outer edge on the front plane
                Assert.AreEqual(0.5f + 0.985f * 0.5f, right.x, 1e-3f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ConfettiOrigin_FollowsTheGameRoot()
        {
            var go = new GameObject("Game");
            var settings = ScriptableObject.CreateInstance<ColorHuntSettings>();
            try
            {
                var game = go.AddComponent<ColorHuntGame>();
                game.S = settings;
                var atOrigin = game.ConfettiOrigin;
                go.transform.position = new Vector3(0f, 8.5f, 30f);
                Assert.That(Vector3.Distance(game.ConfettiOrigin, atOrigin + go.transform.position), Is.LessThan(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
