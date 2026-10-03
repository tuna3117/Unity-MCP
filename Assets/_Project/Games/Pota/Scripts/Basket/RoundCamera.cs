using Project.Juice;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>
    /// Camera poses for the round: aim (above the rim line so the ring reads as an ellipse),
    /// flight (follows the first ball, never below rim height), drop (follows the balls' centre of mass,
    /// only ever downward, clamped above the pile) and basket. Blends in real time.
    /// </summary>
    public class RoundCamera : MonoBehaviour
    {
        public BasketRound Round;
        public BasketSettings S;

        private CameraShake _shake;
        private float _camY = float.MaxValue;
        private Vector3 _pos, _look;

        private void Awake() => _shake = GetComponent<CameraShake>();

        /// <summary>The smoothed look-at point (PotaFlow starts its flight from here).</summary>
        public Vector3 LookPoint => _look;

        /// <summary>F10: the slingshot's pull-back (set each frame by SlingshotFeel; zero at rest).</summary>
        [System.NonSerialized] public Vector3 PullOffset;
        /// <summary>F10: the slingshot's pitch down in degrees.</summary>
        [System.NonSerialized] public float PullPitch;
        /// <summary>F11: the miss sag (set by MissFeel; zero at rest).</summary>
        [System.NonSerialized] public Vector3 SagOffset;

        // Resume from wherever the camera is now (PotaFlow flies it back to the aim pose): no jump to a stale pose.
        private void OnEnable()
        {
            _pos = transform.position;
            _look = transform.position + transform.forward * 10f;
            _camY = float.MaxValue;
        }

        private void Start()
        {
            if (Round == null) Round = FindAnyObjectByType<BasketRound>();
            if (S == null && Round != null) S = Round.Settings;
        }

        private void LateUpdate()
        {
            if (Round == null || S == null || Round.Column == null) return;
            Vector3 targetPos, targetLook;
            float k;
            var cage = Round.Column.Cage;

            switch (Round.Current)
            {
                case BasketRound.State.Aim:
                    targetPos = S.AimCamPos;
                    targetLook = S.AimLookAt;
                    k = S.AimSmoothing;
                    _camY = float.MaxValue;
                    break;

                case BasketRound.State.Flight:
                {
                    var f = Round.FirstBall != null ? Round.FirstBall.transform.position : S.AimLookAt;
                    float y = Mathf.Max(S.AimCamPos.y, f.y * 0.6f + 1.5f);
                    targetPos = new Vector3(f.x * 0.2f, y, S.AimCamPos.z);
                    targetLook = new Vector3(f.x * 0.3f, Mathf.Max(f.y, S.AimLookAt.y), 0f);
                    k = S.AimSmoothing;
                    break;
                }

                case BasketRound.State.Drop:
                {
                    var mean = Round.MeanActivePosition;
                    float targetY = mean.y + 0.3f;
                    if (_camY == float.MaxValue) _camY = Mathf.Max(S.AimCamPos.y, targetY);
                    _camY = Mathf.Min(_camY, targetY);                 // only ever moves down
                    _camY = Mathf.Max(_camY, cage.PileTopY + 1.8f);    // never dips into the pile
                    targetPos = new Vector3(mean.x * 0.15f, _camY, S.DropCamZ);
                    targetLook = new Vector3(0f, _camY - 0.8f, 0f);
                    k = S.DropSmoothing;
                    break;
                }

                default: // Settle, Done
                {
                    float floor = cage.FloorY;
                    float y = Mathf.Max(floor + 2.2f, (cage.PileTopY + floor) * 0.5f + 1.2f);
                    targetPos = new Vector3(0f, y + 0.8f, S.BasketCamZ);
                    targetLook = new Vector3(0f, y - 0.6f, 0f);
                    k = S.BasketSmoothing;
                    break;
                }
            }

            float a = 1f - Mathf.Exp(-k * Time.unscaledDeltaTime);
            _pos = Vector3.Lerp(_pos, targetPos, a);
            _look = Vector3.Lerp(_look, targetLook, a);
            transform.position = _pos + PullOffset + SagOffset + (_shake != null ? _shake.Offset : Vector3.zero);
            transform.LookAt(_look);
            if (PullPitch != 0f) transform.Rotate(PullPitch, 0f, 0f, Space.Self);
        }
    }
}
