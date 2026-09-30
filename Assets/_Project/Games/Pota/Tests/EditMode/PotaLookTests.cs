using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class PotaLookTests
    {
        [Test]
        public void GridScale_And_RotationFit()
        {
            Assert.AreEqual(1.2f, PotaLook.GridScale(7), 1e-5f, "sahaYerlesim.ts: 7×7 1.2");
            Assert.AreEqual(1.4f, PotaLook.GridScale(6), 1e-5f);
            Assert.AreEqual(1.68f, PotaLook.GridScale(5), 1e-5f);
            Assert.AreEqual(1f, PotaLook.RotationFit(7, 0f), 1e-6f);
            Assert.AreEqual(1f, PotaLook.RotationFit(7, Mathf.PI / 2f), 1e-5f);
            Assert.AreEqual(9.5f / (8.4f * Mathf.Sqrt(2f)), PotaLook.RotationFit(7, Mathf.PI / 4f), 1e-5f, "≈ 0.8 at 45°");
            Assert.AreEqual(PotaLook.RotationFit(7, Mathf.PI / 4f), PotaLook.RotationFit(5, Mathf.PI / 4f), 1e-5f, "grid is 8.4 wide at every size");
        }

        [Test]
        public void CameraFit_Portrait_IsWidthBound_AndCentresTheBandOnTheBoard()
        {
            var f = PotaLook.CameraFit(9f / 16f, 87f, 160f);
            float tan = Mathf.Tan(19f * Mathf.Deg2Rad);
            Assert.AreEqual(9.5f / (2f * tan * 0.5625f * 0.985f), f.Distance, 1e-3f, "panoUzakligi(9.5, 9/16, 38°, 0.985) ≈ 24.897");
            float h = 390f / 0.5625f;                                        // 693.3 CSS px tall
            float pxPerUnit = 390f / (2f * f.Distance * tan * 0.5625f);
            float expected = -0.3f + ((87f + h - 160f) * 0.5f - h * 0.5f) / pxPerUnit;
            Assert.AreEqual(expected, f.TargetY, 1e-4f);
            Assert.AreEqual(-1.2026f, f.TargetY, 2e-3f);
        }

        [Test]
        public void CameraFit_Landscape_IsBandBound()
        {
            var f = PotaLook.CameraFit(16f / 9f, 87f, 160f);
            float tan = Mathf.Tan(19f * Mathf.Deg2Rad), h = 390f / (16f / 9f);
            float band = Mathf.Max(120f, h - 247f);
            Assert.AreEqual(10.1f * h / (band * 2f * tan), f.Distance, 1e-3f, "uzakBoy wins on a short screen");
        }

        [Test]
        public void CameraPosition_LooksAtTheFrontPlane()
        {
            var f = new PotaLook.Fit { Distance = 20f, TargetY = -1f };
            var p = PotaLook.CameraPosition(new Vector3(0f, 8.5f, 30f), f);
            Assert.AreEqual(new Vector3(0f, 7.5f, 30f - 0.45f - 20f), p);
        }

        [Test]
        public void HudTop_UsesTheSafeAreaWhenLarger()
        {
            Assert.AreEqual(87f, PotaLook.HudTopCss(0f), 1e-4f);
            Assert.AreEqual(47f + 73f, PotaLook.HudTopCss(47f), 1e-4f, "iPhone notch: ≈ Pota's 118 fallback");
        }

        [Test]
        public void Swell_RisesLinearlyToThePeak_ThenCollapses()
        {
            Assert.AreEqual(1f, PotaLook.Swell(0f, 1.24f), 1e-6f);
            Assert.AreEqual(1.24f, PotaLook.Swell(0.55f, 1.24f), 1e-5f);
            Assert.AreEqual(1.12f, PotaLook.Swell(0.275f, 1.24f), 1e-5f);
            Assert.AreEqual(1.24f * Mathf.Pow(0.5f, 1.5f), PotaLook.Swell(0.775f, 1.24f), 1e-5f);
            Assert.AreEqual(0f, PotaLook.Swell(1f, 1.24f), 1e-6f);
            Assert.AreEqual(0.0605f, PotaLook.SwellPeakAt * PotaLook.TapSwellTime, 1e-6f, "tap peak at 60.5 ms");
        }

        [Test]
        public void LabelCounterAngle_StaysOnTheBoard_ThenStandsUp()
        {
            Assert.AreEqual(0f, PotaLook.LabelCounterAngle(-45f, 0.5f), 1e-6f, "first 70 %: labels ride the board");
            Assert.AreEqual(0f, PotaLook.LabelCounterAngle(-60f, 0.7f), 1e-6f);
            Assert.AreEqual(90f, PotaLook.LabelCounterAngle(-90f, 1f), 1e-5f, "end: upright on screen");
            Assert.AreEqual(45f * 0.5f, PotaLook.LabelCounterAngle(-45f, 0.85f), 1e-4f, "smoothstep(0.5) = 0.5");
        }

        [Test]
        public void Drops_UseConstantAcceleration()
        {
            Assert.AreEqual(1f, PotaLook.DropTravel(0.25f), 1e-5f, "one cell in 0.25 s");
            Assert.AreEqual(4f, PotaLook.DropTravel(0.5f), 1e-5f);
            Assert.AreEqual(0.25f * 2f + 0.06f, PotaLook.StepDropTime(4f), 1e-5f);
            Assert.AreEqual(0f, PotaLook.StepDropTime(0f));
        }

        [Test]
        public void BlockColor_FlashesInLinear_ThenGreysWhenShort()
        {
            var tone = PotaLook.Tones[0];
            var lin = tone.linear;
            Assert.AreEqual(lin, PotaLook.BlockColor(tone, 0f, false));
            var half = PotaLook.BlockColor(tone, 0.5f, false);
            Assert.AreEqual(Mathf.Lerp(lin.r, 1f, 0.5f) * 1.7f, half.r, 1e-5f, "lerp(tone, white, f)·(1 + 1.4f)");
            var shortC = PotaLook.BlockColor(tone, 0f, true);
            Assert.AreEqual(Mathf.Lerp(lin.g, PotaLook.ShortGrey.linear.g, 0.5f) * 0.62f, shortC.g, 1e-5f, "yapbozSahne.ts ornekYaz: soluk last");
        }
    }
}
