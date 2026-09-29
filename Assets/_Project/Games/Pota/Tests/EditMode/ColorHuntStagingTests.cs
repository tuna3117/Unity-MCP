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
            var home = ColorHuntCamera.FitPosition(7, 34f, 9f / 16f, Vector3.zero);
            var anchor = new Vector3(0f, 8.5f, 30f);
            var moved = ColorHuntCamera.FitPosition(7, 34f, 9f / 16f, anchor);
            Assert.That(Vector3.Distance(moved - anchor, home), Is.LessThan(1e-4f));
        }

        [Test]
        public void FitPosition_KeepsTheExistingPortraitFit()
        {
            // Unchanged fit: 7×7 board, FOV 34, 9:16 is width-limited → 24.85 m back, centre 0.15 m below the board centre.
            var p = ColorHuntCamera.FitPosition(7, 34f, 9f / 16f, Vector3.zero);
            Assert.AreEqual(-24.85f, p.z, 0.05f);
            Assert.AreEqual(-0.15f, p.y, 0.01f);
            Assert.AreEqual(0f, p.x, 1e-5f);
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
