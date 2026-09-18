using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>Procedural sound effects generated at start (no audio files), with per-id rate limits.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class SynthSfx : MonoBehaviour
    {
        private const int Rate = 44100;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _minInterval = new Dictionary<string, float>
        {
            { "rim", 0.07f }, { "multiply", 0.045f }, { "add", 0.06f }, { "land", 0.04f }, { "swish", 0.05f }, { "shot", 0.05f }, { "miss", 0.3f },
        };
        private readonly Dictionary<string, float> _lastPlay = new Dictionary<string, float>();
        private AudioSource _source;
        private System.Random _rng = new System.Random(1);

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _clips["shot"] = Make("shot", 0.35f, t => (Noise() * 0.5f * Env(t, 0.35f, 8f)) + 0.35f * Mathf.Sin(Phase(t, 240f, 420f, 0.35f)) * Env(t, 0.3f, 6f));
            _clips["rim"] = Make("rim", 0.14f, t => 0.6f * Mathf.Sin(Phase(t, 1900f, 1500f, 0.13f)) * Env(t, 0.13f, 18f) + 0.3f * Mathf.Sin(2f * Mathf.PI * 2850f * t) * Env(t, 0.09f, 24f));
            _clips["swish"] = Make("swish", 0.28f, t => 0.35f * NoiseHp() * Env(t, 0.25f, 10f) + 0.3f * Tri(Phase(t, 880f, 1320f, 0.14f)) * Env(t, 0.14f, 12f) + 0.25f * Tri(Phase(Mathf.Max(0f, t - 0.12f), 1320f, 1760f, 0.14f)) * (t > 0.12f ? Env(t - 0.12f, 0.14f, 12f) : 0f));
            _clips["multiply"] = Make("multiply", 0.08f, t => 0.6f * Mathf.Sin(Phase(t, 520f, 980f, 0.06f)) * Env(t, 0.06f, 30f));
            _clips["add"] = Make("add", 0.26f, t => Arp(t, 659f, 784f, 988f, 0.06f));
            _clips["land"] = Make("land", 0.08f, t => 0.7f * Mathf.Sin(Phase(t, 190f, 110f, 0.07f)) * Env(t, 0.07f, 30f));
            _clips["miss"] = Make("miss", 0.32f, t => 0.4f * Saw(Phase(t, 420f, 120f, 0.3f)) * Env(t, 0.3f, 8f) + 0.15f * Noise() * Env(t, 0.2f, 12f));
        }

        public void Play(string id, float pitch = 1f, float volume = 1f)
        {
            if (!_clips.TryGetValue(id, out var clip)) return;
            float now = Time.unscaledTime;
            if (_minInterval.TryGetValue(id, out float min) && _lastPlay.TryGetValue(id, out float last) && now - last < min) return;
            _lastPlay[id] = now;
            _source.pitch = pitch;
            _source.PlayOneShot(clip, volume);
        }

        // ---- synthesis helpers ----
        private float _n1;
        private float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);
        private float NoiseHp() { float n = Noise(); float hp = n - _n1; _n1 = n; return hp * 0.7f; }
        private static float Env(float t, float dur, float decay) => t < 0f || t > dur ? 0f : Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-decay * t);
        private static float Phase(float t, float f0, float f1, float dur) { float u = Mathf.Clamp01(t / dur); float f = Mathf.Lerp(f0, f1, u); return 2f * Mathf.PI * (f0 * t + (f1 - f0) * t * u * 0.5f) + 0f * f; }
        private static float Tri(float phase) => 2f * Mathf.Abs(2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f))) - 1f;
        private static float Saw(float phase) => 2f * (phase / (2f * Mathf.PI) - Mathf.Floor(phase / (2f * Mathf.PI) + 0.5f));
        private static float Arp(float t, float a, float b, float c, float step)
        {
            float s = 0f;
            float[] f = { a, b, c };
            for (int i = 0; i < 3; i++)
            {
                float lt = t - i * step;
                if (lt >= 0f) s += 0.4f * Mathf.Sin(2f * Mathf.PI * f[i] * lt) * Env(lt, 0.2f, 14f);
            }
            return s;
        }

        private AudioClip Make(string name, float seconds, Func<float, float> fn)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(fn(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
