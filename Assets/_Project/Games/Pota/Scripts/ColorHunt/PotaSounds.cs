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
            { "carpma", 0.06f }, { "eksi", 0.06f }, { "kirildi", 0.05f }, { "kombo", 0.08f }, { "dustu", 0.045f },
        };

        /// <summary>ses.ts ORNEK: the events that play an mp3 sample (rate jitter 0.97–1.03); the rest are synthesised.</summary>
        private static readonly HashSet<string> Samples = new HashSet<string>
        {
            "dokun", "kuleAt", "kirildi", "dustu", "dondur", "eksi", "kazandi", "kaybetti",
        };

        public static bool IsSample(string id) => Samples.Contains(id);

        public AudioClip LastClip { get; private set; }
        public float LastVolume { get; private set; }

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
        }

        private void Play(string id, AudioClip clip, float volume)
        {
            if (clip == null || !Allow(_last, id, Time.unscaledTime)) return;
            LastClip = clip;
            LastVolume = volume;
            if (_voices == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.pitch = IsSample(id) ? Random.Range(0.97f, 1.03f) : 1f;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }
    }
}
