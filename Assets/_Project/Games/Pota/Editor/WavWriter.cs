using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// 16-bit PCM WAV files for the recorder's sounds: imported clips (Pota's samples, DecompressOnLoad + PCM) and clips synthesised at
    /// runtime (AudioClip.Create) alike, through AudioClip.GetData.
    /// </summary>
    public static class WavWriter
    {
        /// <summary>RIFF/WAVE, PCM 16-bit little-endian. <paramref name="interleaved"/>: samples in −1..1 (clamped), channels interleaved.</summary>
        public static byte[] Encode(float[] interleaved, int channels, int frequency)
        {
            int n = interleaved != null ? interleaved.Length : 0;
            using (var ms = new MemoryStream(44 + n * 2))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + n * 2);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)channels);
                w.Write(frequency);
                w.Write(frequency * channels * 2);
                w.Write((short)(channels * 2));
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(n * 2);
                for (int i = 0; i < n; i++) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(interleaved[i], -1f, 1f) * 32767f));
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Writes <paramref name="clip"/> as a WAV at <paramref name="path"/>; false with the reason when it cannot be read.</summary>
        public static bool TryWrite(AudioClip clip, string path, out string error)
        {
            error = null;
            if (clip == null) { error = "no clip"; return false; }
            try
            {
                if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData())
                {
                    error = $"{clip.name}: its audio data did not load (load type {clip.loadType})";
                    return false;
                }
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0))
                {
                    error = $"{clip.name}: GetData failed (load type {clip.loadType}; the recorder needs DecompressOnLoad)";
                    return false;
                }
                File.WriteAllBytes(path, Encode(data, clip.channels, clip.frequency));
                return true;
            }
            catch (Exception e)
            {
                error = $"{clip.name}: {e.Message}";
                return false;
            }
        }
    }
}
