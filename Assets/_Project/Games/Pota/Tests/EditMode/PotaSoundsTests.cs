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

        private static PotaSounds MakeSounds(out GameObject go)
        {
            go = new GameObject("Sounds");
            var sounds = go.AddComponent<PotaSounds>();                 // Awake does not run in Edit Mode: no voices
            sounds.S = ScriptableObject.CreateInstance<ColorHuntSettings>();
            return sounds;
        }

        [Test]
        public void SoundLog_IsOffUntilStarted_AndStampsEveryPlayedClip()
        {
            var sounds = MakeSounds(out var go);
            var sample = PotaSounds.MakeCollect();                       // stands in for an imported sample
            var synth = PotaSounds.MakeZip();                            // a runtime synth (AudioClip.Create), as Royal's and Court's
            try
            {
                sounds.S.SfxRotate = sample;
                sounds.Rotate();
                Assert.IsFalse(sounds.Logging);
                Assert.IsEmpty(sounds.StopLog(), "nothing is logged before StartLog");
                float now = 1.5f;
                sounds.StartLog(() => now);
                Assert.IsTrue(sounds.Logging);
                sounds.Rotate();
                now = 2.25f;
                sounds.PlayClip("swish", synth, 0.8f, 1.2f);
                sounds.PlayClip("zip", synth);
                var log = sounds.StopLog();
                Assert.IsFalse(sounds.Logging);
                Assert.AreEqual(3, log.Count);
                Assert.AreEqual("dondur", log[0].Id);
                Assert.AreSame(sample, log[0].Clip);
                Assert.AreEqual(1.5f, log[0].T, 1e-6f);
                Assert.AreEqual(1f, log[0].Volume, 1e-6f);
                Assert.That(log[0].Pitch, Is.InRange(0.97f, 1.03f), "a sample keeps Pota's jitter, logged as played");
                Assert.AreEqual(-1, log[0].Voice, "no voices outside Play Mode");
                Assert.AreEqual("swish", log[1].Id);
                Assert.AreSame(synth, log[1].Clip, "a synthesised clip is logged like a sample");
                Assert.AreEqual(2.25f, log[1].T, 1e-6f);
                Assert.AreEqual(0.8f, log[1].Volume, 1e-6f);
                Assert.AreEqual(1.2f, log[1].Pitch, 1e-6f, "a given pitch is used as given");
                Assert.AreEqual(1f, log[2].Pitch, 1e-6f, "a synth id at pitch 1 plays at 1");
                sounds.Rotate();
                Assert.AreEqual(3, log.Count, "StopLog hands the list over; later sounds are not added");
            }
            finally
            {
                Object.DestroyImmediate(sample);
                Object.DestroyImmediate(synth);
                Object.DestroyImmediate(sounds.S);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SoundLog_SkipsWhatTheRateLimitRefused()
        {
            var sounds = MakeSounds(out var go);
            var clip = PotaSounds.MakeCollect();
            try
            {
                sounds.StartLog(() => 0f);
                sounds.PlayClip("kombo", clip);
                sounds.PlayClip("kombo", clip);                           // the same unscaled time: SINIR kombo 80 ms refuses it
                Assert.AreEqual(1, sounds.StopLog().Count);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(sounds.S);
                Object.DestroyImmediate(go);
            }
        }
    }
}
