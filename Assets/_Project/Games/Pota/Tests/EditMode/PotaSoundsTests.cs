using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;

namespace Project.Tests
{
    public class PotaSoundsTests
    {
        [Test]
        public void RateLimits_AreSesTs()
        {
            var last = new Dictionary<string, float>();
            Assert.IsTrue(PotaSounds.Allow(last, "kombo", 1f));
            Assert.IsFalse(PotaSounds.Allow(last, "kombo", 1.079f), "SINIR kombo 80 ms");
            Assert.IsTrue(PotaSounds.Allow(last, "kombo", 1.081f));
            Assert.IsTrue(PotaSounds.Allow(last, "carpma", 2f));
            Assert.IsFalse(PotaSounds.Allow(last, "carpma", 2.05f), "carpma 60 ms");
            Assert.IsTrue(PotaSounds.Allow(last, "dondur", 3f));
            Assert.IsTrue(PotaSounds.Allow(last, "dondur", 3.001f), "no limit");
        }

        [Test]
        public void DropVolume_AndComboStep()
        {
            Assert.AreEqual(0.35f + 0.5f * 0.4f, PotaSounds.DropVolume(0.5f), 1e-6f, "ses.ts: dustu k = min(1, 0.35 + guc·0.4)");
            Assert.AreEqual(1f, PotaSounds.DropVolume(3f), 1e-6f);
            Assert.AreEqual(2, PotaSounds.ComboIndex(2));
            Assert.AreEqual(10, PotaSounds.ComboIndex(15), "MERDIVEN has 11 steps");
            Assert.AreEqual(0, PotaSounds.ComboIndex(-1));
        }
    }
}
