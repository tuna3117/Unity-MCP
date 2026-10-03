using Project.ColorHunt;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>
    /// F11 miss feel (spec 2026-10-03): on "Kaçtı" the KAÇTI! lettering pops over the hoops and wobbles, the top rims blink red for
    /// RimBlinkTime, the hero slumps (if he is still on stage) and the camera sags MissCamSag and comes back. Replaces today's HUD
    /// "Kaçtı" text while the feel layer is on. Real time.
    /// </summary>
    public class MissFeel : MonoBehaviour
    {
        public BasketRound Round;
        public RoundCamera Cam;

        private LetterPop _letter;
        private float _t = -1f;
        private bool _bound;

        public bool Playing => _t >= 0f;
        public bool LetterShown => _letter != null && _letter.Shown;

        private void Start() => Bind();

        private void Bind()
        {
            if (_bound) return;
            if (Round == null) Round = GetComponent<BasketRound>() ?? FindAnyObjectByType<BasketRound>();
            if (Cam == null) Cam = FindAnyObjectByType<RoundCamera>();
            if (Round == null) return;
            Round.MissedDeclared += OnMissed;
            Round.StateChanged += s => { if (s == BasketRound.State.Aim) Stop(); };
            _bound = true;
        }

        private void OnDestroy()
        {
            if (_bound && Round != null) Round.MissedDeclared -= OnMissed;
        }

        private bool Feel => Round != null && Round.Settings != null && Round.Settings.Feel != null && Round.Settings.Feel.Enabled;

        private void OnMissed()
        {
            if (Feel) Play();
        }

        public void Play()
        {
            if (!_bound) Bind();
            if (Round == null || Round.Settings == null) return;
            var s = Round.Settings;
            var f = s.Feel;
            if (_letter == null) _letter = new LetterPop(transform, "MissLetter", s.LetterTemplate, s.LetterMiss, 16);
            var cam = Cam != null ? Cam.GetComponent<Camera>() : Camera.main;
            var at = new Vector3(s.AimLookAt.x, s.AimLookAt.y + 1.4f, 0.6f);
            _letter.Show(at, 2.8f, f.MissTime, cam, 4f);
            if (Round.Column != null)
            {
                foreach (var h in Round.Column.Hoops) if (h.Spec.IsTop) h.Blink(f.RimBlinkTime);
                var actor = Round.Column.Player;
                if (actor != null && actor.gameObject.activeInHierarchy) actor.SetSlump(f.SlumpDeg, f.SlumpTime);
            }
            _t = 0f;
        }

        private void Stop()
        {
            _t = -1f;
            _letter?.Hide();
            if (Cam != null) Cam.SagOffset = Vector3.zero;
        }

        private void Update()
        {
            if (!_bound) Bind();
            float dt = Time.unscaledDeltaTime;
            _letter?.Tick(dt);
            if (_t < 0f) return;
            if (!Feel) { Stop(); return; }
            var f = Round.Settings.Feel;
            _t += dt;
            // sag: down in 0.15 s, back over 0.5 s
            float sag = _t < 0.15f ? FeelCurves.Lean(_t / 0.15f) : 1f - FeelCurves.Lean((_t - 0.15f) / 0.5f);
            if (Cam != null) Cam.SagOffset = Vector3.down * (f.MissCamSag * Mathf.Clamp01(sag));
            if (_t >= Mathf.Max(f.MissTime, 0.65f)) Stop();
        }
    }
}
