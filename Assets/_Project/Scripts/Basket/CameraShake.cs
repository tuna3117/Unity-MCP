using UnityEngine;

namespace Project.Basket
{
    /// <summary>Positional camera noise with exponential decay (real time, unaffected by slow-mo).</summary>
    public class CameraShake : MonoBehaviour
    {
        public float Decay = 9f;
        public Vector3 Offset { get; private set; }
        private float _amplitude;

        public void Kick(float amplitude) => _amplitude = Mathf.Max(_amplitude, amplitude);

        private void Update()
        {
            if (_amplitude < 0.0005f)
            {
                _amplitude = 0f;
                Offset = Vector3.zero;
                return;
            }
            Offset = Random.insideUnitSphere * _amplitude;
            _amplitude *= Mathf.Exp(-Decay * Time.unscaledDeltaTime);
        }
    }
}
