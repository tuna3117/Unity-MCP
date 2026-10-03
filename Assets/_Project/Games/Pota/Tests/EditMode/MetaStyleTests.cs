using NUnit.Framework;
using Project.Meta;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The menu/reward kit's pure curves and numbers (spec 2026-10-03 meta feel M1, M3).</summary>
    public class MetaStyleTests
    {
        [Test]
        public void Press_SinksTo094_Release_OvershootsThenRests()
        {
            Assert.AreEqual(1f, MetaStyle.PressScale(0f), 1e-4f);
            Assert.AreEqual(0.94f, MetaStyle.PressScale(1f), 1e-4f);
            float most = 0f;
            for (int i = 0; i <= 20; i++) most = Mathf.Max(most, MetaStyle.ReleaseScale(i / 20f));
            Assert.Greater(most, 1.02f, "a springy release");
            Assert.AreEqual(1f, MetaStyle.ReleaseScale(1f), 1e-4f);
        }

        [Test]
        public void WindowOpen_StartsSmall_PeaksOver1_Rests()
        {
            Assert.AreEqual(0.85f, MetaStyle.WindowOpen(0f), 1e-4f);
            float most = 0f;
            for (int i = 0; i <= 20; i++) most = Mathf.Max(most, MetaStyle.WindowOpen(i / 20f));
            Assert.Greater(most, 1.03f);
            Assert.AreEqual(1f, MetaStyle.WindowOpen(1f), 1e-4f);
        }

        [Test]
        public void RibbonDrop_FallsFromAbove_Overshoots()
        {
            Assert.AreEqual(-30f, MetaStyle.RibbonDropCss(0f), 1e-3f);
            float lowest = 0f;
            for (int i = 0; i <= 20; i++) lowest = Mathf.Min(lowest, MetaStyle.RibbonDropCss(i / 20f));
            Assert.Less(lowest, -0.5f, "a touch past home");
            Assert.AreEqual(0f, MetaStyle.RibbonDropCss(1f), 1e-3f);
        }

        [Test]
        public void Backdrop_FadesToPotasOverlay() => Assert.AreEqual(0.62f, MetaStyle.BackdropAlpha(1f), 1e-4f);

        [Test]
        public void CoinPlan_SendsEightToTwelveCoins()
        {
            Assert.AreEqual(8, CoinPlan.Count(1));
            Assert.AreEqual(8, CoinPlan.Count(15));
            Assert.AreEqual(10, CoinPlan.Count(50));
            Assert.AreEqual(12, CoinPlan.Count(500));
            Assert.AreEqual(0, CoinPlan.Count(0));
        }

        [Test]
        public void FeelOff_ZeroesEveryDuration()
        {
            var feel = new MetaFeelTuning();
            Assert.AreEqual(0.28f, feel.T(feel.WindowOpen), 1e-4f);
            feel.Enabled = false;
            Assert.AreEqual(0f, feel.T(feel.WindowOpen), 1e-4f);
            Assert.AreEqual(0f, feel.T(0.5f), 1e-4f);
        }

        [Test]
        public void KupaKisa_ShortensThousandsLikePota()
        {
            Assert.AreEqual("9999", MetaStyle.KupaKisa(9999));
            Assert.AreEqual("13,5K", MetaStyle.KupaKisa(13500));
            Assert.AreEqual("154K", MetaStyle.KupaKisa(154200));
        }

        [Test]
        public void Palette_IsPotas()
        {
            Assert.AreEqual(new Color32(20, 40, 80, 255), (Color32)MetaStyle.Navy);
            Assert.AreEqual(new Color32(255, 200, 61, 255), (Color32)MetaStyle.Gold);
            Assert.AreEqual(new Color32(255, 122, 26, 255), (Color32)MetaStyle.Orange);
            Assert.AreEqual(new Color32(47, 179, 107, 255), (Color32)MetaStyle.Green);
            Assert.AreEqual(1080f / 390f, MetaStyle.Css, 1e-4f);
        }
    }
}
