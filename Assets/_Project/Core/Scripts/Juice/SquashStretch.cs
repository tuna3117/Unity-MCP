using UnityEngine;

namespace Project.Juice
{
    /// <summary>
    /// Volume-keeping squash and stretch on a wrapper transform above a character (pivot at the feet): y = 1 + s,
    /// x = z = 1/√y. <see cref="Pulse"/> sets s and a damped spring brings it back with a little overshoot (jump take-off,
    /// landing, hit); with <c>fromVelocity</c> the vertical speed of the target stretches it while airborne and a landing
    /// squashes it by the impact speed. Works under Humanoid and Generic rigs alike because it scales the whole model.
    /// </summary>
    public class SquashStretch : MonoBehaviour
    {
        [Tooltip("Transform to scale (default: this one). Put the model under it with the pivot at the feet.")]
        [SerializeField] private Transform target;
        [Tooltip("Spring frequency (Hz) of the return to rest.")]
        [SerializeField, Min(0.1f)] private float frequency = 4.5f;
        [Tooltip("Damping ratio: 1 = no overshoot, lower = bouncier.")]
        [SerializeField, Range(0.05f, 1f)] private float damping = 0.35f;
        [Tooltip("Stretch the model by its own vertical speed and squash it on landing.")]
        [SerializeField] private bool fromVelocity;
        [Tooltip("Stretch per m/s of vertical speed while airborne.")]
        [SerializeField, Min(0f)] private float velocityGain = 0.04f;
        [Tooltip("Squash per m/s of landing speed.")]
        [SerializeField, Min(0f)] private float landingGain = 0.05f;
        [Tooltip("Largest stretch/squash (0.35 = y between 0.65 and 1.35).")]
        [SerializeField, Range(0.05f, 0.8f)] private float limit = 0.35f;
        [SerializeField] private bool useUnscaledTime;

        private float _s, _v, _velocityTerm, _lastY;
        private bool _hasLastY;

        public float Stretch => Mathf.Clamp(_s + _velocityTerm, -limit, limit);
        private Transform Target => target != null ? target : transform;

        /// <summary>Starts a squash (negative) or stretch (positive) that springs back to rest.</summary>
        public void Pulse(float amount)
        {
            _s = Mathf.Clamp(amount, -limit, limit);
            _v = 0f;
            Apply();
        }

        /// <summary>A landing at <paramref name="impactSpeed"/> m/s squashes in proportion.</summary>
        public void Land(float impactSpeed) => Pulse(-Mathf.Abs(impactSpeed) * landingGain);

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            // Semi-implicit spring step, stable for game frame times.
            float w = 2f * Mathf.PI * frequency;
            _v += (-w * w * _s - 2f * damping * w * _v) * dt;
            _s += _v * dt;
            if (fromVelocity)
            {
                float y = Target.position.y;
                if (_hasLastY)
                {
                    float vy = (y - _lastY) / dt;
                    bool airborne = Mathf.Abs(vy) > 0.2f;
                    if (!airborne && Mathf.Abs(_velocityTerm) > 0.02f && _velocityTerm < 0f) Land(-_velocityTerm / Mathf.Max(velocityGain, 1e-4f));
                    _velocityTerm = airborne ? Mathf.Clamp(Mathf.Abs(vy) * velocityGain, 0f, limit) * Mathf.Sign(vy) : 0f;
                }
                _lastY = y;
                _hasLastY = true;
            }
            Apply();
        }

        private void LateUpdate() => Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        private void Apply()
        {
            float y = 1f + Stretch;
            float xz = 1f / Mathf.Sqrt(Mathf.Max(0.05f, y));
            Target.localScale = new Vector3(xz, y, xz);
        }

        private void OnDisable()
        {
            _s = _v = _velocityTerm = 0f;
            _hasLastY = false;
            if (Target != null) Target.localScale = Vector3.one;
        }
    }
}
