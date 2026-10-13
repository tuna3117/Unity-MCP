using System;
using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    public class ColorHuntRecordToolTests
    {
        [Test]
        public void Parse_ReadsTheScript()
        {
            var steps = ColorHuntRecordTool.Parse("wait 0.6; select auto; tap 24 ;settle; rotate ccw");
            Assert.AreEqual(5, steps.Count);
            Assert.AreEqual("wait", steps[0].Verb);
            Assert.AreEqual(0.6f, steps[0].Seconds, 1e-6f);
            Assert.AreEqual("select", steps[1].Verb);
            Assert.AreEqual("auto", steps[1].Arg);
            Assert.AreEqual("tap", steps[2].Verb);
            Assert.AreEqual("24", steps[2].Arg);
            Assert.AreEqual("settle", steps[3].Verb);
            Assert.AreEqual("ccw", steps[4].Arg);
        }

        [Test]
        public void Parse_RejectsUnknownVerbs() => Assert.Throws<ArgumentException>(() => ColorHuntRecordTool.Parse("jump 3"));

        [Test]
        public void TryLook_KnowsTheThreeLooks_AndTheirLetters()
        {
            Assert.IsTrue(ColorHuntRecordTool.TryLook("pota", out var pota));
            Assert.AreEqual(PuzzleLook.Pota, pota);
            Assert.IsTrue(ColorHuntRecordTool.TryLook("Candy", out var candy));
            Assert.AreEqual(PuzzleLook.Candy, candy);
            Assert.IsTrue(ColorHuntRecordTool.TryLook("a", out var a));
            Assert.AreEqual(PuzzleLook.Candy, a);
            Assert.IsTrue(ColorHuntRecordTool.TryLook("B", out var b));
            Assert.AreEqual(PuzzleLook.Shatter, b);
            Assert.IsFalse(ColorHuntRecordTool.TryLook("fire", out _));
            Assert.IsFalse(ColorHuntRecordTool.TryLook(null, out _));
        }

        [Test]
        public void CloseupFocus_IsTheFirstTapsCell()
        {
            Assert.AreEqual("43", ColorHuntRecordTool.FirstTap(ColorHuntRecordTool.Parse("wait 0.3; select 5; tap 43; tap auto")));
            Assert.AreEqual("auto", ColorHuntRecordTool.FirstTap(ColorHuntRecordTool.Parse("wait 0.3; tap auto")));
            Assert.AreEqual("auto", ColorHuntRecordTool.FirstTap(ColorHuntRecordTool.Parse("wait 1")), "no tap: the hint's block");
        }

        [Test]
        public void ParseRequest_DefaultsToTheColorHuntScene()
        {
            Assert.IsNull(ColorHuntRecordTool.ParseRequest(null, null, null, false, out var r));
            Assert.IsFalse(r.Specials);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("ColorHunt", "", null, true, out r), "script and win stay the ColorHunt scene's");
            Assert.IsFalse(r.Specials);
        }

        [Test]
        public void ParseRequest_Specials_ReadsTheScenarioAndTheSpecialLook()
        {
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "bomb", "1", false, out var r));
            Assert.IsTrue(r.Specials);
            Assert.AreEqual(SpecialScenario.Bomb, r.Scenario);
            Assert.AreEqual(SpecialLook.Royal, r.Look);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("specials", "Chain", "court", false, out r));
            Assert.AreEqual(SpecialScenario.Chain, r.Scenario);
            Assert.AreEqual(SpecialLook.Court, r.Look);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "golden", null, false, out r));
            Assert.AreEqual(SpecialLook.Pota, r.Look, "Pota by default");
            foreach (var s in new[] { "birth", "line", "bomb", "chain", "golden" })
                Assert.IsTrue(ColorHuntRecordTool.TryScenario(s, out _), s);
            Assert.IsTrue(ColorHuntRecordTool.TrySpecialLook("2", out var court));
            Assert.AreEqual(SpecialLook.Court, court);
            Assert.IsTrue(ColorHuntRecordTool.TrySpecialLook("Royal", out var royal));
            Assert.AreEqual(SpecialLook.Royal, royal);
        }

        [Test]
        public void ParseRequest_RejectsBadValues_WithAClearMessage()
        {
            StringAssert.Contains("unknown scene 'Basket' (ColorHunt, Specials)", ColorHuntRecordTool.ParseRequest("Basket", null, null, false, out _));
            StringAssert.Contains("unknown scenario 'fire' (birth, line, bomb, chain, golden",
                ColorHuntRecordTool.ParseRequest("Specials", "fire", null, false, out _));
            StringAssert.Contains("'free' has no script", ColorHuntRecordTool.ParseRequest("Specials", "free", null, false, out _));
            StringAssert.Contains("needs a scenario", ColorHuntRecordTool.ParseRequest("Specials", null, "royal", false, out _));
            StringAssert.Contains("unknown special_look '3' (pota, royal/1, court/2)",
                ColorHuntRecordTool.ParseRequest("Specials", "bomb", "3", false, out _));
            StringAssert.Contains("need scene 'Specials'", ColorHuntRecordTool.ParseRequest(null, "bomb", null, false, out _));
            StringAssert.Contains("need scene 'Specials'", ColorHuntRecordTool.ParseRequest("ColorHunt", null, "court", false, out _));
            StringAssert.Contains("script and win", ColorHuntRecordTool.ParseRequest("Specials", "bomb", null, true, out var r));
            Assert.IsFalse(r.Specials, "a refused request records nothing");
        }

        /// <summary>Pota spec 2026-10-13 (Unity Task 7): the combo scenarios, combo_look a|b and combo_lean on|off (restored after a recording).</summary>
        [Test]
        public void ParseCombo_ReadsTheComboScenariosLookAndLean()
        {
            var names = new Dictionary<string, SpecialScenario>
            {
                ["comboCross"] = SpecialScenario.ComboCross, ["comboThickCross"] = SpecialScenario.ComboThickCross,
                ["comboBigBomb"] = SpecialScenario.ComboBigBomb, ["comboPassLine"] = SpecialScenario.ComboPassLine,
                ["comboPassBomb"] = SpecialScenario.ComboPassBomb, ["comboPassPass"] = SpecialScenario.ComboPassPass,
                ["comboLean"] = SpecialScenario.ComboLean,
            };
            foreach (var kv in names)
            {
                Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", kv.Key, "court", false, out var r), kv.Key);
                Assert.AreEqual(kv.Value, r.Scenario, kv.Key);
            }
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "comboCross", "court", false, out var req));
            Assert.IsNull(ColorHuntRecordTool.ParseCombo(true, "b", "off", ref req));
            Assert.IsTrue(req.HasComboLook);
            Assert.AreEqual(ComboLook.B, req.ComboLook);
            Assert.IsTrue(req.HasComboLean);
            Assert.IsFalse(req.ComboLean);
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "comboCross", "court", false, out req));
            Assert.IsNull(ColorHuntRecordTool.ParseCombo(true, null, null, ref req));
            Assert.IsFalse(req.HasComboLook, "not given: the game's look stays");
            Assert.IsFalse(req.HasComboLean);
            StringAssert.Contains("unknown combo_look 'c'", ColorHuntRecordTool.ParseCombo(true, "c", null, ref req));
            StringAssert.Contains("combo_lean must be on or off", ColorHuntRecordTool.ParseCombo(true, null, "maybe", ref req));
            StringAssert.Contains("need scene 'Specials'", ColorHuntRecordTool.ParseCombo(false, "a", null, ref req));
            Assert.AreEqual(7.6f, ColorHuntRecordTool.LeanTail, 1e-6f, "three leans: 0, 3.5, 7 s");
        }

        [Test]
        public void Markers_CountUp_TapThenTap2()
        {
            var keys = new List<string>();
            for (int i = 0; i < 3; i++) keys.Add(ColorHuntRecordTool.NextKey(keys, "tap"));
            CollectionAssert.AreEqual(new[] { "tap", "tap2", "tap3" }, keys);
            Assert.AreEqual("fire", ColorHuntRecordTool.NextKey(keys, "fire"));
        }

        [Test]
        public void Specials_StagesOnlyOnceTheStartUpScenarioHasSettled()
        {
            // the demo auto-plays its start scenario on entering Play; what it leaves in the air must not fly into the first frames
            Assert.IsTrue(ColorHuntRecordTool.Settled(true, false, 0, 0, 0));
            Assert.IsFalse(ColorHuntRecordTool.Settled(false, false, 0, 0, 0), "the demo's own start-up taps are still coming");
            Assert.IsFalse(ColorHuntRecordTool.Settled(true, true, 0, 0, 0), "a step is running");
            Assert.IsFalse(ColorHuntRecordTool.Settled(true, false, 2, 0, 0), "stickers on their way to the HUD");
            Assert.IsFalse(ColorHuntRecordTool.Settled(true, false, 0, 1, 0), "a golden ball in the air");
            Assert.IsFalse(ColorHuntRecordTool.Settled(true, false, 0, 0, 1), "a popup (×2, ÇİZGİ TOPU!) still rising");
        }

        [Test]
        public void ClipKeys_OneFilePerClip_UniqueAndSafe()
        {
            var a = AudioClip.Create("kombo", 10, 1, 44100, false);
            var b = AudioClip.Create("kombo", 10, 1, 44100, false);
            var c = AudioClip.Create("a b/ç", 10, 1, 44100, false);
            try
            {
                var keys = ColorHuntRecordTool.ClipKeys(new[] { a, b, a, c, null });
                Assert.AreEqual(3, keys.Count);
                Assert.AreEqual("kombo", keys[a]);
                Assert.AreEqual("kombo-2", keys[b], "a second clip of the same name gets its own file");
                Assert.AreEqual("a_b__", keys[c]);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void ParseRequest_AcceptsTheFeelScenariosInTheColorHuntScene()
        {
            foreach (var name in new[] { "intro", "lowammo", "finale", "lose", "chain", "idle" })
            {
                Assert.IsNull(ColorHuntRecordTool.ParseRequest("ColorHunt", name, null, false, out var r), name);
                Assert.IsFalse(r.Specials, name);
                Assert.IsTrue(ColorHuntRecordTool.TryFeelScenario(name, out var preset), name);
                Assert.IsNotEmpty(preset.Script, name);
            }
            Assert.IsNotNull(ColorHuntRecordTool.ParseRequest("ColorHunt", "bomb", null, false, out _), "a Specials scenario still needs the Specials scene");
            Assert.IsNull(ColorHuntRecordTool.ParseRequest("Specials", "bomb", "royal", false, out var sp));
            Assert.IsTrue(sp.Specials);
        }

        [Test]
        public void FeelPresets_CarryTheirAmmoWinAndTail()
        {
            Assert.IsTrue(ColorHuntRecordTool.TryFeelScenario("finale", out var finale));
            Assert.IsTrue(finale.Win);
            Assert.AreEqual(140, finale.Ammo);
            Assert.IsTrue(ColorHuntRecordTool.TryFeelScenario("lose", out var lose));
            Assert.AreEqual(0, lose.Ammo);
            Assert.IsTrue(ColorHuntRecordTool.TryFeelScenario("lowammo", out var low));
            Assert.AreEqual(9, low.Ammo);
            Assert.IsTrue(ColorHuntRecordTool.TryFeelScenario("idle", out var idle));
            Assert.Greater(idle.Script.Length, 0);
            Assert.IsFalse(ColorHuntRecordTool.TryFeelScenario("nope", out _));
        }

        [Test]
        public void TryFeel_ReadsOnOff()
        {
            Assert.IsTrue(ColorHuntRecordTool.TryFeel("on", out bool on) && on);
            Assert.IsTrue(ColorHuntRecordTool.TryFeel("OFF", out bool off) && !off);
            Assert.IsFalse(ColorHuntRecordTool.TryFeel("maybe", out _));
            Assert.IsFalse(ColorHuntRecordTool.TryFeel(null, out _), "no value = leave the asset alone");
        }

        [Test]
        public void ChainSeed_FindsABoardWhoseBestTapChainsAtLeastFourSteps()
        {
            var design = ScriptableObject.CreateInstance<ColorHuntSettings>().ToDesign();
            uint seed = ColorHuntRecordTool.ChainSeed(design, 158, 4, 200);
            Assert.Greater(seed, 0u);
            var b = ColorHuntRules.Create(design, 158, seed);
            int best = ColorHuntHint.Best(b);
            Assert.GreaterOrEqual(ColorHuntRecordTool.ExplodingSteps(ColorHuntRules.Break(b, best)), 4, "four exploding steps = the tap and a depth-3 chain (Süper!)");
        }
    }
}
