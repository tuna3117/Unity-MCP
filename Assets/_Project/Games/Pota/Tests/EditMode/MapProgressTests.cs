using NUnit.Framework;
using Project.Meta;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>M6, pure: the order and the length of the map's progression after a win.</summary>
    public class MapProgressTests
    {
        private static MapProgress Make(bool on, int dots = 6)
        {
            var s = ScriptableObject.CreateInstance<MetaSettings>();
            s.Feel = new MetaFeelTuning { Enabled = on };
            return new MapProgress(s.Feel, dots);
        }

        [Test]
        public void Events_AreInOrder_AndTheWholeThingEndsWithin2_2s()
        {
            var p = Make(true);
            Assert.Less(p.ScrollEnd, p.GreenAt);
            Assert.LessOrEqual(p.GreenAt, p.DotsAt);
            Assert.LessOrEqual(p.DotsAt, p.PinAt);
            Assert.Less(p.PinAt, p.UnlockAt);
            Assert.Less(p.UnlockAt, p.Done);
            Assert.LessOrEqual(p.Done, 2.2f);
        }

        [Test]
        public void Frames_StartEmpty_EndComplete()
        {
            var p = Make(true);
            var a = p.Stage(0f);
            Assert.AreEqual(0f, a.Green, 1e-4f);
            Assert.AreEqual(0f, a.Medal, 1e-4f);
            Assert.AreEqual(0, a.DotsLit);
            Assert.AreEqual(0f, a.Pin, 1e-4f);
            Assert.AreEqual(1f, a.Lock, 1e-4f);
            Assert.AreEqual(0f, a.Number, 1e-4f);
            Assert.IsFalse(a.Unlocked);
            Assert.AreEqual(0f, a.Scroll, 1e-4f, "starts a node below the previous one");
            var z = p.Stage(p.Done + 0.5f);
            Assert.AreEqual(1f, z.Green, 1e-4f);
            Assert.AreEqual(1f, z.Medal, 1e-4f);
            Assert.AreEqual(6, z.DotsLit);
            Assert.AreEqual(1f, z.Pin, 1e-4f);
            Assert.AreEqual(0f, z.Lock, 1e-4f);
            Assert.AreEqual(1f, z.Number, 1e-4f);
            Assert.IsTrue(z.Unlocked);
            Assert.AreEqual(2f, z.Scroll, 1e-4f, "ends centred on the new node");
        }

        [Test]
        public void Dots_LightOneByOne_DotStepApart()
        {
            var p = Make(true, 6);
            Assert.AreEqual(1, p.Stage(p.DotsAt + 0.001f).DotsLit);
            Assert.AreEqual(2, p.Stage(p.DotsAt + p.Feel.DotStep + 0.001f).DotsLit);
            Assert.AreEqual(6, p.Stage(p.DotsAt + 5f * p.Feel.DotStep + 0.001f).DotsLit);
        }

        [Test]
        public void Pin_HopsThreeTimes_LandsWithASquash()
        {
            var p = Make(true);
            int ups = 0; float last = 0f;
            for (float t = p.PinAt; t <= p.PinAt + p.Feel.PinHop; t += 0.005f)
            {
                float h = p.Stage(t).PinHeight;
                if (h > 0.5f && last <= 0.5f) ups++;
                last = h;
            }
            Assert.AreEqual(3, ups);
            Assert.Less(p.Stage(p.PinAt + p.Feel.PinHop + 0.02f).PinSquash, 1f, "lands squashed");
            Assert.AreEqual(1f, p.Stage(p.Done + 1f).PinSquash, 1e-3f);
        }

        [Test]
        public void FeelOff_IsCompleteAtOnce()
        {
            var p = Make(false);
            var z = p.Stage(0f);
            Assert.AreEqual(1f, z.Green, 1e-4f);
            Assert.AreEqual(6, z.DotsLit);
            Assert.AreEqual(1f, z.Pin, 1e-4f);
            Assert.IsTrue(z.Unlocked);
            Assert.AreEqual(0f, z.Ring, 1e-4f, "no burst");
            Assert.AreEqual(0f, p.Done, 1e-4f);
        }
    }
}
