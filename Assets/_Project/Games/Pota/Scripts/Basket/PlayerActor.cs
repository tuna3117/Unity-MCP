using Project.ColorHunt;
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

        // ---------- F10 / F11 feel: lean, crouch, slump on the Humanoid bones (after the Animator), the ball in hand ----------
        private float _leanTarget, _leanNow, _slump = -1f, _slumpDeg, _slumpTime;
        private Transform _handBall;

        /// <summary>The hero's lean 0..1 as drawn (FeelCurves.Lean of the pull, eased in over 0.15 s).</summary>
        public float Lean => _leanNow;
        public bool HandBallVisible => _handBall != null && _handBall.gameObject.activeInHierarchy && gameObject.activeInHierarchy;

        private bool Feel => _round != null && _round.Settings != null && _round.Settings.Feel != null && _round.Settings.Feel.Enabled;

        /// <summary>F10: the slingshot pull 0..1; the hero leans back LeanDeg and crouches CrouchM at a full pull.</summary>
        public void SetLean(float pull) => _leanTarget = Feel ? FeelCurves.Lean(pull) : 0f;

        /// <summary>F11: shoulders drop forward by <paramref name="deg"/> and recover over <paramref name="seconds"/>.</summary>
        public void SetSlump(float deg, float seconds)
        {
            _slumpDeg = deg;
            _slumpTime = Mathf.Max(0.1f, seconds);
            _slump = 0f;
        }

        private void EnsureHandBall()
        {
            if (_handBall != null || _animator == null || _round == null) return;
            var hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HandBall";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(hand, false);
            float r = _round.Settings.BallRadius;
            go.transform.localPosition = new Vector3(0f, r * 0.9f, r * 0.6f);
            go.transform.localScale = Vector3.one * (r * 2f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _round.Settings.BallVisualMaterial != null ? _round.Settings.BallVisualMaterial : Project.Basket.Visual.RimMeshBuilder.UrpLit(new Color(0.85f, 0.42f, 0.14f), 0.3f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _handBall = go.transform;
        }

        private void ShowHandBall(bool on)
        {
            if (on) EnsureHandBall();
            if (_handBall != null && _handBall.gameObject.activeSelf != on) _handBall.gameObject.SetActive(on);
        }

        private void LateUpdate()
        {
            bool feel = Feel;
            if (!feel) { _leanTarget = 0f; _leanNow = 0f; _slump = -1f; ShowHandBall(false); return; }
            if (_round != null && _round.Current == BasketRound.State.Aim && _hideAt < 0f) ShowHandBall(true);   // F10: the ball in hand while aiming
            _leanNow = Mathf.MoveTowards(_leanNow, _leanTarget, Time.unscaledDeltaTime / 0.15f);
            float slump = 0f;
            if (_slump >= 0f)
            {
                _slump += Time.unscaledDeltaTime;
                float u = _slump / _slumpTime;
                slump = u >= 1f ? 0f : Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                if (u >= 1f) _slump = -1f;
            }
            if (_animator == null || (_leanNow <= 0f && slump <= 0f)) return;
            var f = _round.Settings.Feel;
            float back = f.LeanDeg * _leanNow - f.SlumpDeg * slump;          // + leans back toward the camera, − slumps forward
            var hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            var spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            var head = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips != null) hips.position += Vector3.down * (f.CrouchM * _leanNow);
            if (spine != null) spine.Rotate(Vector3.right, back * 0.5f, Space.World);
            if (chest != null) chest.Rotate(Vector3.right, back * 0.5f, Space.World);
            if (head != null) head.Rotate(Vector3.right, -back * 0.6f + (slump > 0f ? -f.SlumpDeg * slump * 1.2f : 0f), Space.World);
        }

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
            ShowHandBall(s == BasketRound.State.Aim && Feel);
        }

        /// <summary>Shows the player and its stage pieces at the base pose, idling (aim start; PotaFlow's transition).</summary>
        public void Reveal()
        {
            Show(true);
            transform.localScale = _baseScale;
            transform.position = _basePos;
            _hideAt = -1f;
            if (_animator != null) _animator.Play(IdleState, 0, 0f);
            ShowHandBall(Feel && (_round == null || _round.Current == BasketRound.State.Aim));
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
            ShowHandBall(false);
            _leanTarget = 0f;
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
