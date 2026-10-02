using UnityEngine;

namespace Project.Basket
{
    /// <summary>
    /// Shows the player while aiming, plays the shot and hides afterwards. With an Animator (Humanoid prefab) the shot
    /// plays the "Shoot" state; without one it falls back to a squash/jump on the transform.
    /// </summary>
    public class PlayerActor : MonoBehaviour
    {
        private static readonly int ShootState = Animator.StringToHash("Shoot");
        private static readonly int IdleState = Animator.StringToHash("Idle");

        private BasketRound _round;
        private Animator _animator;
        private Vector3 _baseScale, _basePos;
        private float _animT = -1f;
        private float _hideAt = -1f;
        /// <summary>Stage pieces (terrace, rail) that hide and show with the player: the camera descends through them otherwise.</summary>
        public GameObject[] AlsoHide;

        private void Show(bool on)
        {
            gameObject.SetActive(on);
            if (AlsoHide == null) return;
            foreach (var go in AlsoHide) if (go != null) go.SetActive(on);
        }

        public void Bind(BasketRound round)
        {
            Unbind();
            _round = round;
            _baseScale = transform.localScale;
            _basePos = transform.position;
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController == null) _animator = null;
            round.ShotFired += OnShot;
            round.StateChanged += OnState;
            Show(round.Current == BasketRound.State.Aim);
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
            if (s == BasketRound.State.Aim) Reveal();
        }

        /// <summary>Shows the player and its stage pieces at the base pose, idling (aim start; PotaFlow's transition).</summary>
        public void Reveal()
        {
            Show(true);
            transform.localScale = _baseScale;
            transform.position = _basePos;
            _hideAt = -1f;
            if (_animator != null) _animator.Play(IdleState, 0, 0f);
        }

        /// <summary>Plays the shot once, where the hero stands, without the round's hide timer (PotaFlow candidate 1: the big shot into the giant hoop).</summary>
        public void PlayShoot()
        {
            _hideAt = -1f;
            if (_animator != null)
            {
                _animator.Play(ShootState, 0, 0f);
                return;
            }
            _animT = 0f;
        }

        private void OnShot(AimPoint aim)
        {
            if (this == null || _round == null) return;
            _hideAt = Time.time + _round.Layout.BallCount * _round.Settings.BallSpacing + 0.35f;
            if (_animator != null)
            {
                // Start inside the clip so the hands reach the release point right after the balls leave.
                var clip = System.Array.Find(_animator.runtimeAnimatorController.animationClips, c => c.name.StartsWith("player_shoot"));
                float len = clip != null ? clip.length : 1f;
                _animator.Play(ShootState, 0, Mathf.Clamp01(_round.Settings.PlayerShootClipStart / len));
                return;
            }
            _animT = 0f;
        }

        private void Update()
        {
            if (_hideAt > 0f && Time.time >= _hideAt) { Show(false); _hideAt = -1f; return; }
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
