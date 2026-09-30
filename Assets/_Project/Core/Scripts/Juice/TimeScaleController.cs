using UnityEngine;

namespace Project.Juice
{
    /// <summary>Slow-motion and hit-stop by scaling Time.timeScale (and fixedDeltaTime with it).</summary>
    public class TimeScaleController : MonoBehaviour
    {
        public const float HitStopScale = 0.05f;

        private float _baseFixed = -1f;
        private float _slowUntil = -1f, _slowScale = 1f, _stopUntil = -1f;

        private void Awake()
        {
            // A domain reload during slow-mo/hit-stop can leave the editor's time scale stuck; always start clean.
            Time.timeScale = 1f;
        }

        private void Start() => _baseFixed = Time.fixedDeltaTime;

        public void SlowMo(float scale, float seconds)
        {
            _slowScale = scale;
            _slowUntil = Time.unscaledTime + seconds;
            Apply();
        }

        /// <summary>Cancels any slow motion or hit-stop and restores normal time now (phase changes, e.g. PotaFlow's transition).</summary>
        public void Clear()
        {
            _slowUntil = -1f;
            _stopUntil = -1f;
            _slowScale = 1f;
            _dipUntil = -1f;
            _dipScale = 1f;
            Time.timeScale = 1f;
            if (_baseFixed > 0f) Time.fixedDeltaTime = _baseFixed;
        }

        private float _dipUntil = -1f, _dipScale = 1f;

        /// <summary>
        /// Pota renkAviFaz.ts zamanDusur: game time runs at <paramref name="scale"/> for <paramref name="seconds"/> of real time.
        /// While a dip runs, the smaller scale and the later end win. Slow motion still wins over a dip.
        /// </summary>
        public void Dip(float scale, float seconds)
        {
            float now = Time.unscaledTime;
            _dipScale = now < _dipUntil ? Mathf.Min(_dipScale, scale) : scale;
            _dipUntil = Mathf.Max(_dipUntil, now + seconds);
            Apply();
        }

        public void HitStop(float seconds)
        {
            _stopUntil = Mathf.Max(_stopUntil, Time.unscaledTime + seconds);
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            if (_baseFixed < 0f) return;
            float now = Time.unscaledTime;
            float scale = 1f;
            if (now < _slowUntil) scale = _slowScale;
            else if (now < _stopUntil) scale = HitStopScale;
            else if (now < _dipUntil) scale = _dipScale;
            if (Mathf.Abs(Time.timeScale - scale) > 1e-4f)
            {
                Time.timeScale = scale;
                Time.fixedDeltaTime = _baseFixed * scale;
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_baseFixed > 0f) Time.fixedDeltaTime = _baseFixed;
        }
    }
}
