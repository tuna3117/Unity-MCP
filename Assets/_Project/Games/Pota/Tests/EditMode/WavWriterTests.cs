using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>The recorder's sound files: 16-bit PCM WAV from AudioClip.GetData (Task 12).</summary>
    public class WavWriterTests
    {
        private const string Dir = "Temp/test_wav";

        private static string Ascii(byte[] b, int at) => Encoding.ASCII.GetString(b, at, 4);

        [Test]
        public void Encode_WritesA16BitPcmHeader_AndEverySample()
        {
            var bytes = WavWriter.Encode(new[] { 0f, 0.5f, -1f, 1.5f, 0.25f, -0.25f }, 2, 48000);
            Assert.AreEqual(44 + 12, bytes.Length);
            Assert.AreEqual("RIFF", Ascii(bytes, 0));
            Assert.AreEqual(36 + 12, BitConverter.ToInt32(bytes, 4));
            Assert.AreEqual("WAVE", Ascii(bytes, 8));
            Assert.AreEqual("fmt ", Ascii(bytes, 12));
            Assert.AreEqual(16, BitConverter.ToInt32(bytes, 16));
            Assert.AreEqual(1, BitConverter.ToInt16(bytes, 20), "PCM");
            Assert.AreEqual(2, BitConverter.ToInt16(bytes, 22), "channels");
            Assert.AreEqual(48000, BitConverter.ToInt32(bytes, 24));
            Assert.AreEqual(48000 * 2 * 2, BitConverter.ToInt32(bytes, 28), "byte rate");
            Assert.AreEqual(4, BitConverter.ToInt16(bytes, 32), "block align");
            Assert.AreEqual(16, BitConverter.ToInt16(bytes, 34), "bits");
            Assert.AreEqual("data", Ascii(bytes, 36));
            Assert.AreEqual(12, BitConverter.ToInt32(bytes, 40));
            Assert.AreEqual(0, BitConverter.ToInt16(bytes, 44));
            Assert.AreEqual(16384, BitConverter.ToInt16(bytes, 46), "0.5");
            Assert.AreEqual(-32767, BitConverter.ToInt16(bytes, 48), "-1");
            Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 50), "1.5 is clamped");
        }

        [Test]
        public void TryWrite_ASynthClip_RoundTripsItsLength()
        {
            var clip = PotaSounds.MakeCollect();                          // AudioClip.Create: mono, 44.1 kHz
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, "topla.wav");
            try
            {
                Assert.IsTrue(WavWriter.TryWrite(clip, path, out string error), error);
                var bytes = File.ReadAllBytes(path);
                Assert.AreEqual(1, BitConverter.ToInt16(bytes, 22));
                Assert.AreEqual(44100, BitConverter.ToInt32(bytes, 24));
                Assert.AreEqual(clip.samples * 2, BitConverter.ToInt32(bytes, 40), "one 16-bit sample per frame");
                Assert.AreEqual(44 + clip.samples * 2, bytes.Length);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void TryWrite_RefusesANullClip()
        {
            Assert.IsFalse(WavWriter.TryWrite(null, Path.Combine(Dir, "none.wav"), out string error));
            StringAssert.Contains("no clip", error);
        }
    }
}
