using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class HudSpriteTests
    {
        [Test]
        public void PanelSprite_WithAClearFill_IsOnlyItsBorder()
        {
            // the ✓ stamp's glow is a border-only panel over the target box: its inside must stay see-through
            var glow = ColorHuntHud.PanelSprite(128, 20, Color.clear, Color.clear, Color.white, 4, 0f, 0).texture;
            Assert.Less(glow.GetPixel(64, 64).a, 0.01f, "clear inside");
            Assert.Greater(glow.GetPixel(64, 2).a, 0.9f, "the border");
            var panel = ColorHuntHud.PanelSprite(128, 20, PotaLook.HudPanelTop, PotaLook.HudNavy, PotaLook.HudGold, 4, 0.18f, 2).texture;
            Assert.Greater(panel.GetPixel(64, 64).a, 0.99f, "an opaque fill stays opaque");
        }
    }
}
