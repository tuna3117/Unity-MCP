using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;

namespace Project.Tests
{
    public class SwellSetTests
    {
        [Test]
        public void Peak_FiresOnce_At55Percent()
        {
            var set = new SwellSet<object>();
            var k = new object();
            int fired = 0;
            float scale = 0f;
            set.Start(k, 0.15f, 1.24f, false, () => fired++);
            set.Advance(0.08f, (key, s, o) => scale = s, null);                 // o 0.533
            Assert.AreEqual(0, fired);
            Assert.Greater(scale, 1.2f);
            set.Advance(0.005f, null, null);                                    // o 0.567
            Assert.AreEqual(1, fired, "renkAviFaz.ts patlamaIlerlet: o ≥ tepe");
            set.Advance(0.05f, (key, s, o) => scale = s, null);
            Assert.AreEqual(1, fired, "once");
            Assert.Less(scale, 1.24f, "collapsing");
        }

        [Test]
        public void PeakFires_Once_EvenIfTheStepEndsFirst()
        {
            var set = new SwellSet<object>();
            var k = new object();
            int fired = 0;
            set.Start(k, 0.15f, 1.24f, false, () => fired++);
            set.Advance(0.03f, null, null);
            Assert.AreEqual(0, fired);
            set.End(k);
            Assert.AreEqual(1, fired, "gorselSil → tepeyiTetikle: the HUD count must not be lost");
            set.Advance(1f, null, null);
            set.End(k);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void NewBoard_ForgetsPendingBursts()
        {
            var set = new SwellSet<object>();
            int fired = 0;
            set.Start(new object(), 0.15f, 1.24f, true, () => fired++);
            set.Clear();
            set.Advance(1f, null, null);
            Assert.AreEqual(0, fired);
            Assert.IsFalse(set.AnyLoose);
        }

        [Test]
        public void LooseEntries_FinishAndReportBack()
        {
            var set = new SwellSet<object>();
            var k = new object();
            int fired = 0;
            var done = new List<object>();
            set.Start(k, 0.11f, 1.18f, true, () => fired++);
            Assert.IsTrue(set.AnyLoose, "a swelling tapped block keeps the board busy (sonenler)");
            set.Advance(0.07f, null, done);
            Assert.AreEqual(1, fired);
            Assert.IsEmpty(done);
            set.Advance(0.05f, null, done);
            CollectionAssert.AreEqual(new[] { k }, done);
            Assert.IsFalse(set.AnyLoose);
            Assert.AreEqual(0, set.Count);
        }
    }
}
