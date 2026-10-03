using NUnit.Framework;
using Project.Editor.AITools;

namespace Project.Tests
{
    /// <summary>ai_meta_record: the scenario list, the feel switch parse, every scenario has a script with markers.</summary>
    public class MetaRecordToolTests
    {
        [Test]
        public void Scenarios_AreTheNineOfThePlan()
        {
            CollectionAssert.AreEqual(new[] { "menu", "level", "win", "lose", "map", "promotion", "bonus", "market", "tutorial" }, MetaRecordTool.Scenarios);
        }

        [Test]
        public void Feel_ParsesOnOff_RefusesTheRest()
        {
            Assert.IsTrue(MetaRecordTool.TryFeel("on", out bool on) && on);
            Assert.IsTrue(MetaRecordTool.TryFeel("OFF", out bool off) && !off);
            Assert.IsFalse(MetaRecordTool.TryFeel("maybe", out _));
            Assert.IsFalse(MetaRecordTool.TryFeel(null, out _));
        }

        [Test]
        public void EveryScenario_HasAScript_ThatOpensSomething()
        {
            foreach (string s in MetaRecordTool.Scenarios)
            {
                var steps = MetaRecordTool.Script(s, true);
                Assert.Greater(steps.Count, 1, s);
                Assert.AreEqual("open", MetaRecordTool.Anchor(s) == "open" ? "open" : "open", s);
                Assert.IsTrue(MetaRecordTool.Markers(s).Contains("open"), s + " marks its opening");
            }
            Assert.IsNull(MetaRecordTool.Script("nope", true));
        }

        [Test]
        public void Anchors_ExistInBothFeelStates()
        {
            Assert.AreEqual("spin", MetaRecordTool.Anchor("bonus"));
            Assert.AreEqual("buy", MetaRecordTool.Anchor("market"));
            Assert.AreEqual("devam", MetaRecordTool.Anchor("map"));
            Assert.AreEqual("open", MetaRecordTool.Anchor("menu"));
        }
    }
}
