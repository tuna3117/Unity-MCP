using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// ses.ts on Unity: the clips ai_potalook_import rendered from Pota's own graph (samples wet through its room send, synth kombo /
    /// carpma), 2D, with Pota's volumes, sample rate jitter 0.97–1.03 (ornekCal) and rate limits (SINIR). Eight voices.
    /// </summary>
    public class PotaSounds : MonoBehaviour
    {
        public ColorHuntSettings S;

        /// <summary>ses.ts SINIR (seconds).</summary>
        private static readonly Dictionary<string, float> Limits = new Dictionary<string, float>
        {
            { "carpma", 0.06f }, { "eksi", 0.06f }, { "kirildi", 0.05f }, { "kombo", 0.08f }, { "dustu", 0.045f }, { "topla", 0.03f },
        };

        /// <summary>ses.ts ORNEK: the events that play an mp3 sample (rate jitter 0.97–1.03); the rest are synthesised.</summary>
        private static readonly HashSet<string> Samples = new HashSet<string>
        {
            "dokun", "kuleAt", "kirildi", "dustu", "dondur", "eksi", "kazandi", "kaybetti",
        };

        public static bool IsSample(string id) => Samples.Contains(id);

        public AudioClip LastClip { get; private set; }
        public float LastVolume { get; private set; }

        private const int Rate = 44100;
        private AudioClip _collect, _done;

        /// <summary>"topla": two bright blips (1320 then 1980 Hz) — a sticker lands in its box.</summary>
        public static AudioClip MakeCollect() => Synth("topla", 0.14f,
            t => 0.5f * Mathf.Sin(2f * Mathf.PI * 1320f * t) * Env(t, 0.09f, 30f)
               + 0.35f * Mathf.Sin(2f * Mathf.PI * 1980f * t) * Env(t - 0.03f, 0.1f, 34f));

        /// <summary>"tamam": a rising triangle arpeggio G5–B5–E6 — a target is done.</summary>
        public static AudioClip MakeDone() => Synth("tamam", 0.42f, t =>
        {
            float s = 0f;
            float[] f = { 784f, 988f, 1319f };
            for (int k = 0; k < f.Length; k++) { float tk = t - 0.06f * k; s += Tri(f[k] * tk) * Env(tk, 0.28f, 12f); }
            return 0.35f * s;
        });

        public static AudioClip Synth(string name, float seconds, System.Func<float, float> fn)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(fn(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Env(float t, float dur, float decay) => t < 0f || t > dur ? 0f : Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-decay * t);

        private static float Tri(float cycles)
        {
            float f = cycles - Mathf.Floor(cycles);
            return 4f * Mathf.Abs(f - 0.5f) - 1f;
        }

        public void Collect(int streak) => Play("topla", _collect, 0.8f, StudioCurves.CollectPitch(streak));
        public void TargetDone() => Play("tamam", _done, 0.9f, 1f);

        private readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        private AudioSource[] _voices;
        private int _next;

        public static bool Allow(IDictionary<string, float> last, string id, float now)
        {
            if (Limits.TryGetValue(id, out float min) && last.TryGetValue(id, out float t) && now - t < min) return false;
            last[id] = now;
            return true;
        }

        public static float DropVolume(float guc) => Mathf.Min(1f, 0.35f + guc * 0.4f);
        public static int ComboIndex(int depth) => Mathf.Clamp(depth, 0, 10);

        public void Select() => Play("dokun", S.SfxSelect, 1f);
        public void Salvo() => Play("kuleAt", S.SfxSalvo, 1f);
        public void Hit() => Play("carpma", S.SfxHit != null && S.SfxHit.Length > 0 ? S.SfxHit[Random.Range(0, S.SfxHit.Length)] : null, 1f);
        public void Break() => Play("kirildi", S.SfxBreak, 1f);
        public void Combo(int depth) => Play("kombo", S.SfxCombo != null && S.SfxCombo.Length == 11 ? S.SfxCombo[ComboIndex(depth)] : null, 1f);
        public void Drop(float guc) => Play("dustu", S.SfxDrop, DropVolume(guc));
        public void Rotate() => Play("dondur", S.SfxRotate, 1f);
        public void Minus() => Play("eksi", S.SfxMinus, 1f);
        public void Win() => Play("kazandi", S.SfxWin, 1f);
        public void Lose() => Play("kaybetti", S.SfxLose, 1f);

        private void Awake()
        {
            _voices = new AudioSource[8];
            for (int i = 0; i < _voices.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _voices[i] = src;
            }
            _collect = MakeCollect();
            _done = MakeDone();
        }

        private void Play(string id, AudioClip clip, float volume, float pitch = -1f)
        {
            if (clip == null || !Allow(_last, id, Time.unscaledTime)) return;
            LastClip = clip;
            LastVolume = volume;
            if (_voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = pitch > 0f ? pitch : IsSample(id) ? Random.Range(0.97f, 1.03f) : 1f;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }
    }
}
