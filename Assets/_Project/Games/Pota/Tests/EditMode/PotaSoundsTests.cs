using System.Collections.Generic;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

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


        [Test]
        public void Select_IsPotasDokunSample_WithPitchJitter()
        {
            Assert.IsTrue(PotaSounds.IsSample("dokun"), "ses.ts ORNEK dokun → dokun.mp3 (played at 0.97–1.03)");
            foreach (var id in new[] { "kuleAt", "kirildi", "dustu", "dondur", "eksi", "kazandi", "kaybetti" }) Assert.IsTrue(PotaSounds.IsSample(id), id);
            foreach (var id in new[] { "kombo", "carpma" }) Assert.IsFalse(PotaSounds.IsSample(id), id + " is synthesised in ses.ts");
        }

        private static float Peak(AudioClip c)
        {
            var d = new float[c.samples];
            c.GetData(d, 0);
            float m = 0f;
            foreach (float s in d) m = Mathf.Max(m, Mathf.Abs(s));
            return m;
        }

        [Test]
        public void CollectAndDone_AreShortAudibleSynths()
        {
            var collect = PotaSounds.MakeCollect();
            Assert.AreEqual("topla", collect.name);
            Assert.AreEqual(0.14f, collect.length, 0.01f);
            Assert.Greater(Peak(collect), 0.2f);
            var done = PotaSounds.MakeDone();
            Assert.AreEqual("tamam", done.name);
            Assert.AreEqual(0.42f, done.length, 0.01f);
            Assert.Greater(Peak(done), 0.2f);
            Assert.LessOrEqual(Peak(done), 1f);
        }
    }
}
