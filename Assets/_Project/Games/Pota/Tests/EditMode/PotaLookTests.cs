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

        [Test]
        public void CubicBezier_IsCssTiming()
        {
            Assert.AreEqual(0f, PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 0f), 1e-5f);
            Assert.AreEqual(1f, PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 1f), 1e-5f);
            Assert.AreEqual(0.5f, PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 0.5f), 1e-4f, "symmetric ease-in-out");
            Assert.Less(PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, 0.25f), 0.2f, "slow start");
            Assert.AreEqual(0.25f, PotaLook.CubicBezier(1f / 3f, 1f / 3f, 2f / 3f, 2f / 3f, 0.25f), 1e-4f, "control points on the diagonal = linear");
        }

        [Test]
        public void ItemFlight_FollowsPotasKeyframes()
        {
            var a = PotaLook.ItemFlight(0f);
            Assert.AreEqual(0f, a.Along, 1e-6f);
            Assert.AreEqual(0.9f, a.Scale, 1e-5f);
            Assert.AreEqual(0f, a.Lift, 1e-5f);
            var b = PotaLook.ItemFlight(1f);
            Assert.AreEqual(1f, b.Along, 1e-4f);
            Assert.AreEqual(0.5f, b.Scale, 1e-3f);
            Assert.AreEqual(0f, b.Lift, 1e-2f);
            float t = 0f;
            while (PotaLook.CubicBezier(0.45f, 0f, 0.55f, 1f, t) < 0.3f) t += 1e-4f;
            var k = PotaLook.ItemFlight(t);
            Assert.AreEqual(0.3f, k.Along, 2e-3f, "30 % of the way at the 0.3 keyframe");
            Assert.AreEqual(40f, k.Lift, 0.3f, "40 px up");
            Assert.AreEqual(1.35f, k.Scale, 3e-3f);
        }

        [Test]
        public void Salvo_BowsTowardTheCamera_AndSwells()
        {
            var a = new Vector2(0f, -3.95f); var b = new Vector2(1f, 2f);
            Assert.AreEqual(new Vector3(0f, -3.95f, 0f), PotaLook.SalvoPoint(a, b, 0f));
            Assert.Less(Vector3.Distance(new Vector3(1f, 2f, 0f), PotaLook.SalvoPoint(a, b, 1f)), 1e-5f);
            var mid = PotaLook.SalvoPoint(a, b, 0.5f);
            Assert.AreEqual(0.9f, mid.z, 1e-5f, "sin(π·0.5)·0.9 toward the camera");
            Assert.AreEqual(0.7f, PotaLook.SalvoScale(0f), 1e-6f);
            Assert.AreEqual(1.2f, PotaLook.SalvoScale(0.5f / 1.15f), 1e-5f);
            Assert.AreEqual(0.7f, PotaLook.SalvoScale(1f), 1e-5f);
            Assert.AreEqual(new Vector2(0f, -3.5f - 0.45f), PotaLook.SalvoSource(7), "topKaynagi: altKenar − TOP_PAYI·0.75");
        }

        [Test]
        public void SpecialTone_IsKoyuDoygunOfOzelRenkHex()
        {
            // yapbozSahne.ts OZEL_BLOK_TONU evaluated with Pota's three 0.180 (2026-10-01): #ff8a2a → #ec7b00, #2a2d36 → #242732, #ffc93c → #eeb900.
            Assert.AreEqual(PotaLook.Hex(0xec7b00), PotaLook.SpecialTone(SpecialKind.LineV));
            Assert.AreEqual(PotaLook.Hex(0xec7b00), PotaLook.SpecialTone(SpecialKind.LineH));
            Assert.AreEqual(PotaLook.Hex(0x242732), PotaLook.SpecialTone(SpecialKind.Bomb));
            Assert.AreEqual(PotaLook.Hex(0xeeb900), PotaLook.SpecialTone(SpecialKind.Golden));
            Assert.AreEqual(0xd81500u, PotaLook.DarkSaturated(0xe8352e), "three's HSL runs in its LINEAR working space");
            Assert.AreEqual(0x0074d7u, PotaLook.DarkSaturated(0x2a82e8));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PotaLook.SpecialTone(SpecialKind.None));
        }

        [Test]
        public void GoldenPulse_AndSpecialScale_ArePotas()
        {
            Assert.AreEqual(0.08f, PotaLook.GoldenPulse(0f), 1e-6f, "RENK_AVI_HIS.ozel.altinNabiz · (1 + sin 0)");
            Assert.AreEqual(0.16f, PotaLook.GoldenPulse(Mathf.PI / 10f), 1e-6f, "peak at 5t = π/2");
            Assert.AreEqual(0f, PotaLook.GoldenPulse(3f * Mathf.PI / 10f), 1e-6f);
            Assert.AreEqual(0.82f / 0.88f, PotaLook.SpecialScale, 1e-6f, "etiketleriYaz ozelOlcek = 0.82 / CIKARTMA_PAYI");
        }
    }
}
