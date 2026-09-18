using UnityEngine;

namespace Project.Basket
{
    /// <summary>Shows the player while aiming, plays a squash/jump on the shot and hides afterwards.</summary>
    public class PlayerActor : MonoBehaviour
    {
        private BasketRound _round;
        private Vector3 _baseScale, _basePos;
        private float _animT = -1f;
        private float _hideAt = -1f;

        public void Bind(BasketRound round)
        {
            Unbind();
            _round = round;
            _baseScale = transform.localScale;
            _basePos = transform.position;
            round.ShotFired += OnShot;
            round.StateChanged += OnState;
            gameObject.SetActive(round.Current == BasketRound.State.Aim);
        }

        private void Unbind()
        {
            if (_round == null) return;
            _round.ShotFired -= OnShot;
            _round.StateChanged -= OnState;
            _round = null;
        }

        private void OnDestroy() => Unbind();

        private void OnState(BasketRound.State s)
        {
            if (this == null) return;
            if (s == BasketRound.State.Aim)
            {
                gameObject.SetActive(true);
                transform.localScale = _baseScale;
                transform.position = _basePos;
                _hideAt = -1f;
            }
        }

        private void OnShot(AimPoint aim)
        {
            if (this == null || _round == null) return;
            _animT = 0f;
            _hideAt = Time.time + _round.Layout.BallCount * _round.Settings.BallSpacing + 0.35f;
        }

        private void Update()
        {
            if (_hideAt > 0f && Time.time >= _hideAt) { gameObject.SetActive(false); _hideAt = -1f; return; }
            if (_animT < 0f) return;
            _animT += Time.deltaTime;
            float t = _animT;
            float sy = 1f, rise = 0f;
            if (t < 0.1f) sy = 1f - t * 1.4f;
            else if (t < 0.35f) { float u = (t - 0.1f) / 0.25f; sy = 0.86f + Mathf.Sin(u * Mathf.PI) * 0.26f; rise = Mathf.Sin(u * Mathf.PI) * 0.35f; }
            else if (t < 0.55f) sy = 1f;
            else { _animT = -1f; sy = 1f; }
            transform.localScale = new Vector3(_baseScale.x * (2f - sy), _baseScale.y * sy, _baseScale.z * (2f - sy));
            transform.position = _basePos + Vector3.up * rise;
        }
    }
}
