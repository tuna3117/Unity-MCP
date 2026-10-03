using NUnit.Framework;
using Project.Meta;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>Pota's UI images and the fonts are in the project and wired into MetaSettings (ai_potameta_import).</summary>
    public class MetaAssetsTests
    {
        private const string Hint = "run execute_custom_tool ai_potameta_import";

        private static MetaSettings Settings() => AssetDatabase.LoadAssetAtPath<MetaSettings>(MetaSettings.AssetPath);

        [Test]
        public void Settings_AssetExists_WithTheSampleSave()
        {
            var s = Settings();
            Assert.IsNotNull(s, Hint);
            Assert.AreEqual(20, s.Save.Level);
            Assert.AreEqual(145, s.Save.Coins);
            Assert.AreEqual(5, s.LeagueThresholds.Length);
            Assert.AreEqual(12400, s.LeagueThresholds[1]);
        }

        [Test]
        public void Fonts_LilitaAndFredokaWeights()
        {
            var s = Settings();
            Assert.IsNotNull(s.Lilita, Hint);
            Assert.IsNotNull(s.FredokaSemiBold, Hint + " (Fredoka 600 instanced from the variable font)");
            Assert.IsNotNull(s.FredokaBold, Hint);
        }

        [Test]
        public void Images_EveryStyleGuideSpriteIsWired()
        {
            var s = Settings();
            Assert.AreEqual(4, s.MenuBackgrounds.Length, "anaekran 1..4");
            foreach (var bg in s.MenuBackgrounds) Assert.IsNotNull(bg, Hint);
            Assert.IsNotNull(s.MapBackground);
            Assert.IsNotNull(s.LoadingBackground);
            Assert.IsNotNull(s.Hero); Assert.IsNotNull(s.HeroMask);
            Assert.IsNotNull(s.Coin); Assert.IsNotNull(s.Heart); Assert.IsNotNull(s.Trophy); Assert.IsNotNull(s.Gear);
            Assert.IsNotNull(s.Basket); Assert.IsNotNull(s.Ball); Assert.IsNotNull(s.CoinPile);
            Assert.AreEqual(5, s.LeagueBadges.Length); foreach (var b in s.LeagueBadges) Assert.IsNotNull(b);
            Assert.AreEqual(3, s.Medals.Length); foreach (var m in s.Medals) Assert.IsNotNull(m);
            Assert.AreEqual(5, s.Items.Length); foreach (var i in s.Items) Assert.IsNotNull(i);
            Assert.AreEqual(3, s.TabIcons.Length); foreach (var t in s.TabIcons) Assert.IsNotNull(t);
            Assert.AreEqual(4, s.CategoryIcons.Length); foreach (var c in s.CategoryIcons) Assert.IsNotNull(c);
            Assert.AreEqual(3, s.Boosters.Length); foreach (var g in s.Boosters) Assert.IsNotNull(g);
            Assert.AreEqual(4, s.Clothes.Length); foreach (var g in s.Clothes) Assert.IsNotNull(g);
            Assert.AreEqual(1536, s.MenuBackgrounds[0].texture.width);
        }
    }
}
