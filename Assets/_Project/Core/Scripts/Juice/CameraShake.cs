using UnityEngine;

namespace Project.Juice
{
    /// <summary>
    /// Camera offset from two channels: <see cref="Kick"/> (basket: random sphere, exponential decay, real time) and
    /// <see cref="Add"/> (Pota puzzle `sars`: additive, capped at 0.5, linear 2.2/s on game time so dips freeze it, uniform ±s/2 in x/y).
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public const float LinearCap = 0.5f;
        public const float LinearDecay = 2.2f;

        public float Decay = 9f;
        public Vector3 Offset { get; private set; }
        public float Linear { get; private set; }
        private float _amplitude;

        public void Kick(float amplitude) => _amplitude = Mathf.Max(_amplitude, amplitude);

        public void Add(float amount) => Linear = Mathf.Min(LinearCap, Linear + Mathf.Max(0f, amount));

        public static float StepLinear(float amount, float dt) => Mathf.Max(0f, amount - dt * LinearDecay);

        private void Update()
        {
            var kick = Vector3.zero;
            if (_amplitude < 0.0005f) _amplitude = 0f;
            else
            {
                kick = Random.insideUnitSphere * _amplitude;
                _amplitude *= Mathf.Exp(-Decay * Time.unscaledDeltaTime);
            }
            var lin = Vector3.zero;
            if (Linear > 0f)
            {
                lin = new Vector3((Random.value - 0.5f) * Linear, (Random.value - 0.5f) * Linear, 0f);
                Linear = StepLinear(Linear, Time.deltaTime);
            }
            Offset = kick + lin;
        }
    }
}
