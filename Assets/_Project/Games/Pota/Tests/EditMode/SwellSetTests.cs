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
        public void TryCancel_DropsAWaitingBurst_NotOneThatRan()
        {
            var set = new SwellSet<object>();
            object a = new object(), b = new object();
            int firedA = 0, firedB = 0;
            set.Start(a, 0.15f, 1.24f, false, () => firedA++);
            set.Start(b, 0.15f, 1.24f, false, () => firedB++, 0.08f);
            set.Advance(0.01f, null, null);                                     // b peaks (o 0.6), a waits (o 0.067)
            Assert.AreEqual(1, firedB);
            Assert.IsTrue(set.TryCancel(a), "a special look takes the block over before its peak");
            Assert.IsFalse(set.Contains(a));
            Assert.IsFalse(set.TryCancel(b), "b has burst: it stays and collapses");
            Assert.IsTrue(set.Contains(b));
            set.Advance(1f, null, null);
            set.End(a);
            Assert.AreEqual(0, firedA, "a cancelled burst never runs");
            Assert.AreEqual(1, firedB);
            Assert.IsFalse(set.TryCancel(new object()), "nothing to cancel");
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

        [Test]
        public void Start_Elapsed_CountsAsAlreadyRun()
        {
            var set = new SwellSet<object>();
            object a = new object(), b = new object();
            int firedA = 0, firedB = 0;
            set.Start(a, 0.15f, 1.24f, false, () => firedA++, 0.08f);          // started 0.08 s ago
            set.Start(b, 0.15f, 1.24f, false, () => firedB++, -0.01f);         // starts 0.01 s into the next advance
            set.Advance(0.003f, null, null);                                    // a: o 0.553
            Assert.AreEqual(1, firedA);
            set.Advance(0.087f, null, null);                                    // b: 0.08 → o 0.533
            Assert.AreEqual(0, firedB);
            set.Advance(0.01f, null, null);                                     // b: o 0.6
            Assert.AreEqual(1, firedB);
        }
    }
}
