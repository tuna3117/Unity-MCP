using UnityEngine;

namespace Project.Basket
{
    /// <summary>Slow-motion and hit-stop by scaling Time.timeScale (and fixedDeltaTime with it).</summary>
    public class TimeScaleController : MonoBehaviour
    {
        public const float HitStopScale = 0.05f;

        private float _baseFixed = -1f;
        private float _slowUntil = -1f, _slowScale = 1f, _stopUntil = -1f;

        private void Start() => _baseFixed = Time.fixedDeltaTime;

        public void SlowMo(float scale, float seconds)
        {
            _slowScale = scale;
            _slowUntil = Time.unscaledTime + seconds;
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
