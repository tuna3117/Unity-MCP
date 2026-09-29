using NUnit.Framework;
using Project.Editor.AITools;
using UnityEngine;

namespace Project.Tests
{
    public class BasketSceneToolTests
    {
        // Rebuilding the scene used to reset BasketSettings.PlayerPrefab to the static oyuncu.glb, silently dropping the
        // animated player that ai_character_setup had assigned.
        [Test]
        public void KeepsAnAssignedPlayerPrefab()
        {
            var assigned = new GameObject("assigned");
            try { Assert.AreSame(assigned, BasketSceneTool.ChoosePlayerPrefab(assigned)); }
            finally { Object.DestroyImmediate(assigned); }
        }

        [Test]
        public void DefaultsToTheAnimatedHero()
        {
            var prefab = BasketSceneTool.ChoosePlayerPrefab(null);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponentInChildren<Animator>());
        }
    }
}
