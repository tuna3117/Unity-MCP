using System;
using NUnit.Framework;
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
    }
}
