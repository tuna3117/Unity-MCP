using System.Collections.Generic;
using Project.VFX;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>Turns round events into feel: slow-mo, shake, hit-stop, popups, particles, sounds, net wobble.</summary>
    public class BasketJuice : MonoBehaviour
    {
        public BasketRound Round;
        public BasketSettings S;
        public CameraShake Shake;
        public TimeScaleController TimeCtl;
        public PopupText Popups;
        public SynthSfx Sfx;

        private static readonly Color Gold = new Color(1f, 0.78f, 0.24f);
        private static readonly Color Green = new Color(0.35f, 0.9f, 0.6f);
        private static readonly Color Red = new Color(1f, 0.42f, 0.37f);
        private readonly Dictionary<Hoop, float> _lastPopup = new Dictionary<Hoop, float>();
        private float _lastRimVfx = -1f;

        private void Start()
        {
            if (Round == null) Round = FindFirstObjectByType<BasketRound>();
            if (Round == null) return;
            if (S == null) S = Round.Settings;
            if (Shake == null && Camera.main != null) Shake = Camera.main.GetComponent<CameraShake>();
            if (TimeCtl == null) TimeCtl = GetComponent<TimeScaleController>();
            if (Popups == null) Popups = GetComponent<PopupText>();
            if (Sfx == null) Sfx = GetComponent<SynthSfx>();

            Round.ShotFired += OnShotFired;
            Round.HoopPassed += OnHoopPassed;
            Round.RimHit += OnRimHit;
            Round.BallLanded += OnBallLanded;
            Round.MissedDeclared += OnMissed;
            Round.RoundEnded += OnEnded;
        }

        private void OnShotFired(AimPoint aim) => Sfx?.Play("shot");

        private void OnHoopPassed(Hoop hoop, Ball ball, bool firstTopSwish)
        {
            string label = hoop.Spec.Kind == HoopEffectKind.Multiply ? $"x{hoop.Spec.Amount}" : $"+{hoop.Spec.Amount}";
            var color = hoop.Spec.Kind == HoopEffectKind.Multiply ? Gold : Green;
            hoop.Wobble();

            if (firstTopSwish)
            {
                TimeCtl?.SlowMo(S.SwishSlowMoScale, S.SwishSlowMoSeconds);
                Shake?.Kick(S.SwishShake);
                Popups?.Show(hoop.Center, label, Gold, 64, center: true, life: 1.1f);
                Sfx?.Play("swish");
                VfxService.Play("swish", hoop.Center + Vector3.down * 0.15f, Quaternion.identity);
                return;
            }

            float now = Time.unscaledTime;
            if (_lastPopup.TryGetValue(hoop, out float last) && now - last < S.PopupMinInterval) return;
            _lastPopup[hoop] = now;
            Popups?.Show(hoop.Center + Vector3.up * 0.35f, label, color, 38, false, S.PopupSeconds);
            VfxService.Play(hoop.Spec.Kind == HoopEffectKind.Multiply ? "multiply" : "add", hoop.Center + Vector3.down * 0.25f, Quaternion.identity);
            Sfx?.Play(hoop.Spec.Kind == HoopEffectKind.Multiply ? "multiply" : "add");
            TimeCtl?.HitStop(S.HitStopSeconds);
        }

        private void OnRimHit(Hoop hoop, Ball ball)
        {
            if (hoop.Spec.IsTop) Sfx?.Play("rim", 0.95f + Random.value * 0.1f, 0.7f);
            if (Time.unscaledTime - _lastRimVfx > 0.07f)
            {
                _lastRimVfx = Time.unscaledTime;
                VfxService.Play("rim_hit", ball.transform.position, Quaternion.identity);
            }
        }

        private void OnBallLanded(Ball ball) => Sfx?.Play("land", 0.9f + Random.value * 0.2f, 0.6f);

        private void OnMissed()
        {
            Popups?.Show(Vector3.zero, "Kaçtı", Red, 64, center: true, life: 1.2f);
            Sfx?.Play("miss");
        }

        private void OnEnded(int count) => Shake?.Kick(0.02f);
    }
}
