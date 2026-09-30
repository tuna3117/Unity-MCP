using System;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;

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
    }
}
