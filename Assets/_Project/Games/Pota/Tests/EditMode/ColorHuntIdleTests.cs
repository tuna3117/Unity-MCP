using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>The calm idles A–E for resting special balls (spec 2026-10-07): their curves, what each writes, and the switches around them.</summary>
    public class ColorHuntIdleTests
    {
        private const float Tau = 2f * Mathf.PI;

        [Test]
        public void Defaults_AreTheSpecsNumbers_AndTheAssetKeepsTheLooksOwnIdle()
        {
            var t = new IdleTuning();
            Assert.AreEqual((0.02f, 0.8f), (t.BreathAmount, t.BreathHz), "A ±2 %, 0.8 Hz");
            Assert.AreEqual((0.35f, 0.12f, 35f, 0.8f, 2.5f, 0.4f),
                (t.GlintTime, t.GlintWidth, t.GlintAngle, t.GlintBrightness, t.GlintEvery, t.GlintJitter), "B");
            Assert.AreEqual((0.36f, 0.05f, 0.5f, 1f), (t.RimRadius, t.RimThickness, t.RimPeak, t.RimHz), "C");
            Assert.AreEqual((0.22f, 0.35f, 1.8f, 0.5f, 20f, 70f),
                (t.TwinkleSize, t.TwinkleLife, t.TwinkleEvery, t.TwinkleJitter, t.TwinkleAngleMin, t.TwinkleAngleMax), "D");
            Assert.AreEqual((0.5f, 5f, 0.01f), (t.SwayHz, t.SwayDegrees, t.SwayBreath), "E");
            var asset = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>("Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset");
            Assert.IsNotNull(asset);
            Assert.AreEqual(SpecialIdle.Look, asset.SpecialIdle, "today's behaviour until Tuna picks one");
            Assert.IsNotNull(asset.Idle);
        }

        [Test]
        public void Phase_SpreadsNeighbours_InsideOneTurn()
        {
            for (int cell = 0; cell < 49; cell++)
            {
                float p = IdleCurves.Phase(cell);
                Assert.That(p, Is.InRange(0f, Tau), $"cell {cell}");
                float next = IdleCurves.Phase(cell + 1), below = IdleCurves.Phase(cell + 7);
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(p * Mathf.Rad2Deg, next * Mathf.Rad2Deg)), 30f, $"{cell} and its right neighbour");
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(p * Mathf.Rad2Deg, below * Mathf.Rad2Deg)), 30f, $"{cell} and the one below");
            }
        }

        [Test]
        public void Breath_IsTwoPercent_AtPointEightHz()
        {
            float lo = 2f, hi = 0f;
            for (int i = 0; i < 1000; i++)
            {
                float t = i * 0.005f, b = IdleCurves.Breath(t, 0.7f, 0.02f, 0.8f);
                lo = Mathf.Min(lo, b);
                hi = Mathf.Max(hi, b);
                Assert.AreEqual(b, IdleCurves.Breath(t + 1.25f, 0.7f, 0.02f, 0.8f), 1e-5f, "one breath every 1.25 s");
            }
            Assert.AreEqual(1.02f, hi, 1e-4f);
            Assert.AreEqual(0.98f, lo, 1e-4f);
        }

        [Test]
        public void Sway_RocksFiveDegrees_AtHalfAHertz()
        {
            float most = 0f;
            for (int i = 0; i < 1000; i++)
            {
                float t = i * 0.01f, a = IdleCurves.Sway(t, 1.3f, 5f, 0.5f);
                Assert.LessOrEqual(Mathf.Abs(a), 5f + 1e-5f);
                Assert.AreEqual(a, IdleCurves.Sway(t + 2f, 1.3f, 5f, 0.5f), 1e-4f, "one rock every 2 s");
                most = Mathf.Max(most, Mathf.Abs(a));
            }
            Assert.AreEqual(5f, most, 0.01f);
        }

        [Test]
        public void GlintSweep_CrossesTheWholeBall_FromOffItToOffIt()
        {
            Assert.AreEqual(-IdleCurves.GlintReach, IdleCurves.GlintSweep(0f), 1e-6f);
            Assert.AreEqual(IdleCurves.GlintReach, IdleCurves.GlintSweep(1f), 1e-6f);
            Assert.AreEqual(0f, IdleCurves.GlintSweep(0.5f), 1e-6f, "over the ball's centre halfway through");
            float last = -10f;
            for (int i = 0; i <= 50; i++)
            {
                float s = IdleCurves.GlintSweep(i / 50f);
                Assert.GreaterOrEqual(s, last, "one way only");
                last = s;
            }
            // the ball fills 88 % of its sticker (radius 0.44 quad units); the band (0.12 cells over a 0.857-cell sticker) must be off it
            float stickerCells = PotaLook.LabelQuad * PotaLook.SpecialScale, halfBand = 0.5f * 0.12f / stickerCells;
            Assert.Greater(IdleCurves.GlintReach - halfBand, 0.44f, "the sweep starts and ends with nothing on the ball");
            Assert.AreEqual(IdleCurves.GlintSweep(0.2f), -IdleCurves.GlintSweep(0.8f), 1e-6f, "eased the same in and out");
        }

        [Test]
        public void Rim_PulsesFromNothingToItsPeak_OnceASecond()
        {
            float lo = 1f, hi = 0f;
            for (int i = 0; i < 400; i++)
            {
                float t = i * 0.0025f, a = IdleCurves.RimAlpha(t, 2.1f, 0.5f, 1f);
                Assert.That(a, Is.InRange(0f, 0.5f));
                Assert.AreEqual(a, IdleCurves.RimAlpha(t + 1f, 2.1f, 0.5f, 1f), 1e-5f);
                lo = Mathf.Min(lo, a);
                hi = Mathf.Max(hi, a);
            }
            Assert.AreEqual(0f, lo, 1e-3f, "it disappears");
            Assert.AreEqual(0.5f, hi, 1e-3f, "peak opacity 0.5");
        }

        [Test]
        public void Intervals_AreRandom_ButInsideEveryPlusMinusJitter()
        {
            var r = new System.Random(5);
            float lo = 99f, hi = 0f, sum = 0f;
            const int n = 2000;
            for (int i = 0; i < n; i++)
            {
                float g = IdleCurves.NextInterval(r, 2.5f, 0.4f), d = IdleCurves.NextInterval(r, 1.8f, 0.5f);
                Assert.That(g, Is.InRange(2.1f, 2.9f), "B: 2.5 ± 0.4 s");
                Assert.That(d, Is.InRange(1.3f, 2.3f), "D: 1.8 ± 0.5 s");
                lo = Mathf.Min(lo, d);
                hi = Mathf.Max(hi, d);
                sum += g;
            }
            Assert.AreEqual(2.5f, sum / n, 0.03f, "centred on every");
            Assert.Less(lo, 1.35f, "it uses the whole range");
            Assert.Greater(hi, 2.25f);
            for (int cell = 0; cell < 49; cell++)
                Assert.That(IdleCurves.FirstDelay(cell, 1.8f), Is.InRange(0.25f, 0.25f + 1.8f), "the first one comes within one interval");
        }

        [Test]
        public void Twinkle_SitsUpperRight_GrowsAndFades()
        {
            var r = new System.Random(9);
            for (int i = 0; i < 500; i++) Assert.That(IdleCurves.TwinkleAngle(r, 20f, 70f), Is.InRange(20f, 70f));
            Assert.AreEqual(0f, IdleCurves.TwinkleAlpha(0f));
            Assert.AreEqual(1f, IdleCurves.TwinkleAlpha(0.5f), 1e-6f);
            Assert.AreEqual(0f, IdleCurves.TwinkleAlpha(1f));
            Assert.AreEqual(0.35f, IdleCurves.TwinkleScale(0f), 1e-6f);
            Assert.AreEqual(1f, IdleCurves.TwinkleScale(1f), 1e-6f);
            for (int i = 1; i <= 20; i++) Assert.Greater(IdleCurves.TwinkleScale(i / 20f), IdleCurves.TwinkleScale((i - 1) / 20f), "it grows");
            // at its largest, the star at the ball's edge stays inside the cell (no spill into a neighbour), even turned 15°
            var t = new IdleTuning();
            float reach = t.TwinkleRadius * Mathf.Cos(t.TwinkleAngleMin * Mathf.Deg2Rad) + 0.5f * t.TwinkleSize * Mathf.Sqrt(2f);
            Assert.Less(reach, 0.5f);
        }

        private static (GameObject, BlockView, SpecialIdleFx) Rig(int cell)
        {
            var go = new GameObject("IdleTestView");
            var v = go.AddComponent<BlockView>();
            v.Cell = cell;
            v.Scale = 0.9f; v.Flash = 0.2f; v.Squash = 0.1f; v.Bob = 0.03f; v.StickerSpin = 7f; v.Wiggle = 2f; v.Pulse = 0.05f; v.LabelAngle = 3f;
            var fx = new GameObject("IdleTestFx").AddComponent<SpecialIdleFx>();
            return (go, v, fx);
        }

        private static void AssertUntouched(BlockView v, string what, bool idleScale = true, bool spin = true)
        {
            Assert.AreEqual(0.9f, v.Scale, what + ": Scale");
            Assert.AreEqual(0.2f, v.Flash, what + ": Flash");
            Assert.AreEqual(0.1f, v.Squash, what + ": Squash");
            Assert.AreEqual(0.03f, v.Bob, what + ": Bob");
            Assert.AreEqual(2f, v.Wiggle, what + ": Wiggle");
            Assert.AreEqual(0.05f, v.Pulse, what + ": Pulse");
            Assert.AreEqual(3f, v.LabelAngle, what + ": LabelAngle");
            if (idleScale) Assert.AreEqual(1f, v.IdleScale, what + ": IdleScale");
            if (spin) Assert.AreEqual(7f, v.StickerSpin, what + ": StickerSpin");
        }

        [Test]
        public void Look_WritesNothing()
        {
            var (go, v, fx) = Rig(24);
            try
            {
                for (int i = 0; i < 30; i++) fx.Idle(v, 0.05f, SpecialKind.Bomb, SpecialIdle.Look);
                AssertUntouched(v, "Look");
                Assert.AreEqual(0, fx.Tracked, "Look keeps no state");
                Assert.AreEqual(0, fx.VisibleCount);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(fx.gameObject); }
        }

        [Test]
        public void Breath_WritesOnlyTheIdleScale_AndReleasePutsItBack()
        {
            var (go, v, fx) = Rig(0);
            try
            {
                float lo = 2f, hi = 0f;
                for (int i = 0; i < 80; i++)
                {
                    fx.Idle(v, 0.025f, SpecialKind.LineV, SpecialIdle.Breath);
                    lo = Mathf.Min(lo, v.IdleScale);
                    hi = Mathf.Max(hi, v.IdleScale);
                    AssertUntouched(v, "Breath", idleScale: false);
                }
                Assert.That(hi, Is.InRange(1.015f, 1.0201f), "breathes out to +2 %");
                Assert.That(lo, Is.InRange(0.9799f, 0.985f), "and in to −2 %");
                Assert.AreEqual(0, fx.VisibleCount, "A draws nothing");
                fx.Release(v);
                Assert.AreEqual(1f, v.IdleScale);
                AssertUntouched(v, "after Release");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(fx.gameObject); }
        }

        [Test]
        public void Sway_RocksTheSticker_TheGoldenBallStaysStill()
        {
            var (go, v, fx) = Rig(25);
            try
            {
                float most = 0f;
                for (int i = 0; i < 100; i++)
                {
                    fx.Idle(v, 0.02f, SpecialKind.Bomb, SpecialIdle.Sway);
                    most = Mathf.Max(most, Mathf.Abs(v.StickerSpin));
                    Assert.LessOrEqual(Mathf.Abs(v.StickerSpin), 5f + 1e-4f);
                    Assert.That(v.IdleScale, Is.InRange(0.9899f, 1.0101f), "±1 %");
                    AssertUntouched(v, "Sway", idleScale: false, spin: false);
                }
                Assert.Greater(most, 2f, "it rocks");
                fx.Clear();
                Assert.AreEqual(0f, v.StickerSpin, "Clear puts the sticker straight");
                Assert.AreEqual(1f, v.IdleScale);
                v.StickerSpin = 7f;
                for (int i = 0; i < 40; i++) fx.Idle(v, 0.05f, SpecialKind.Golden, SpecialIdle.Sway);
                AssertUntouched(v, "golden under E");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(fx.gameObject); }
        }

        [Test]
        public void SwitchingModes_ResetsWhatTheOldOneWrote()
        {
            var (go, v, fx) = Rig(10);
            try
            {
                for (int i = 0; i < 13; i++) fx.Idle(v, 0.05f, SpecialKind.LineH, SpecialIdle.Sway);
                Assert.AreNotEqual(7f, v.StickerSpin);
                fx.Idle(v, 0f, SpecialKind.LineH, SpecialIdle.Breath);
                Assert.AreEqual(0f, v.StickerSpin, "E's rock is gone under A");
                Assert.AreEqual(IdleCurves.Breath(0f, IdleCurves.Phase(10), 0.02f, 0.8f), v.IdleScale, 1e-6f, "A starts again at t = 0, at its cell's phase");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(fx.gameObject); }
        }

        [Test]
        public void RecordTool_ReadsSpecialIdle_AndTheIdleScenario()
        {
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "idle", "royal", "b", false, out var r));
            Assert.AreEqual(SpecialScenario.Idle, r.Scenario);
            Assert.AreEqual(SpecialIdle.Glint, r.Idle);
            Assert.AreEqual(SpecialLook.Royal, r.Look);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "bomb", null, false, out r));
            Assert.AreEqual(SpecialIdle.Look, r.Idle, "the look's own idle by default");
            var names = new[] { ("look", SpecialIdle.Look), ("breath", SpecialIdle.Breath), ("A", SpecialIdle.Breath), ("glint", SpecialIdle.Glint),
                ("rim", SpecialIdle.Rim), ("c", SpecialIdle.Rim), ("Twinkle", SpecialIdle.Twinkle), ("d", SpecialIdle.Twinkle), ("sway", SpecialIdle.Sway), ("E", SpecialIdle.Sway) };
            foreach (var (name, idle) in names)
            {
                Assert.IsTrue(ColorHuntRecordTool.TrySpecialIdle(name, out var got), name);
                Assert.AreEqual(idle, got, name);
            }
            StringAssert.Contains("unknown special_idle 'f'", ColorHuntRecordTool.ParseRequest("Specials", "idle", null, "f", false, out _));
            StringAssert.Contains("special_idle needs scene 'Specials'", ColorHuntRecordTool.ParseRequest(null, null, null, "rim", false, out _));
            Assert.AreEqual(6f, ColorHuntRecordTool.IdleTail);
        }

        [Test]
        public void IdleScenario_PutsFourSpecialsAroundTheCentre_AndTapsNothing()
        {
            var b = ColorHuntRules.FromPreset(SpecialScenarios.Board(SpecialScenario.Idle));
            Assert.AreEqual(SpecialKind.Bomb, b.Cells[23].Special);
            Assert.AreEqual(SpecialKind.LineV, b.Cells[25].Special);
            Assert.AreEqual(SpecialKind.Golden, b.Cells[17].Special);
            Assert.AreEqual(SpecialKind.LineH, b.Cells[31].Special);
            Assert.IsFalse(b.Cells[SpecialScenarios.IdleFocus].IsSpecial, "the centre the recordings zoom on is a plain block");
            CollectionAssert.AreEqual(new[] { 3 }, b.Gates, "the golden ball sits over its gate");
            Assert.IsEmpty(SpecialScenarios.Taps(SpecialScenario.Idle));
            Assert.AreEqual("Bekleme", SpecialScenarios.Title(SpecialScenario.Idle));
            Assert.AreEqual("Görünüm", SpecialsDemo.IdleButton(SpecialIdle.Look));
            Assert.AreEqual("B", SpecialsDemo.IdleButton(SpecialIdle.Glint));
            StringAssert.StartsWith("E ", SpecialsDemo.IdleTitle(SpecialIdle.Sway));
        }
    }
}
