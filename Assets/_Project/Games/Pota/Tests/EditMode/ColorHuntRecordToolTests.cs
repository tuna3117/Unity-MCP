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
    }
}
