using Project.ColorHunt;
using Project.Flow;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>
    /// F13 count feel (spec 2026-10-03): the HUD's basket number rolls instead of popping, the pop shrinks as landings come faster
    /// (FeelCurves.CountPop), and when the round ends an "N TOP" badge stamps over the cage (FlowBadge, PotaFlow's stamp curve).
    /// Only the standalone basket scene stamps (PotaFlow shows its own badge in the transition).
    /// </summary>
    public class BasketCountFeel : MonoBehaviour
    {
        public BasketRound Round;
        public BasketHud Hud;

        private FlowBadge _badge;
        private float _stamp = -1f, _lastLanding = -10f, _rate;
        private int _count;
        private bool _bound;

        public FlowBadge Badge => _badge;
        public float BadgeScale { get; private set; } = 1f;
        /// <summary>Landings per 0.1 s, smoothed; 1 = the fastest the pop shrinks for.</summary>
        public float LandingRate => _rate;

        private void Start() => Bind();

        private void Bind()
        {
            if (_bound) return;
            if (Round == null) Round = GetComponent<BasketRound>() ?? FindAnyObjectByType<BasketRound>();
            if (Hud == null) Hud = FindAnyObjectByType<BasketHud>();
            if (Round == null || Hud == null) return;
            Round.BallLanded += OnLanded;
            Round.RoundEnded += OnEnded;
            Round.StateChanged += s => { if (s == BasketRound.State.Aim) HideBadge(); };
            _bound = true;
        }

        private void OnDestroy()
        {
            if (!_bound || Round == null) return;
            Round.BallLanded -= OnLanded;
            Round.RoundEnded -= OnEnded;
        }

        private bool Feel => Round != null && Round.Settings != null && Round.Settings.Feel != null && Round.Settings.Feel.Enabled;

        private void OnLanded(Ball ball)
        {
            float now = Time.unscaledTime;
            float gap = Mathf.Max(0.02f, now - _lastLanding);
            _lastLanding = now;
            _rate = Mathf.Lerp(_rate, Mathf.Clamp01(0.1f / gap), 0.5f);
        }

        private void OnEnded(int count)
        {
            if (Feel && Hud.ShowResultPanel) Stamp(count);
        }

        /// <summary>Stamps "N TOP" over the cage: 1 → StampPeak → StampRest over StampTime, held 1.5 s.</summary>
        public void Stamp(int count)
        {
            if (!_bound) Bind();
            if (Hud == null) return;
            if (_badge == null)
                _badge = FlowBadge.Create(Hud.transform.parent, Hud.BasketText != null ? Hud.BasketText.font : null, new Color(1f, 0.78f, 0.24f), new Color(0.078f, 0.157f, 0.314f));
            _count = count;
            _badge.Show(count);
            _stamp = 0f;
            BadgeScale = 1f;
            Place();
        }

        private void HideBadge()
        {
            _stamp = -1f;
            _badge?.Hide();
        }

        private void Place()
        {
            var cam = Camera.main;
            Vector2 vp = new Vector2(0.5f, 0.6f);
            if (cam != null && Round != null && Round.Column != null && Round.Column.Cage != null)
            {
                var cage = Round.Column.Cage;
                var p = cam.WorldToViewportPoint(new Vector3(0f, cage.TopY + 0.6f, 0f));
                if (p.z > 0f) vp = new Vector2(Mathf.Clamp(p.x, 0.2f, 0.8f), Mathf.Clamp(p.y, 0.2f, 0.85f));
            }
            _badge.Place(vp, BadgeScale, 1f);
        }

        private void Update()
        {
            if (!_bound) Bind();
            if (!_bound) return;
            _rate = Mathf.MoveTowards(_rate, 0f, Time.unscaledDeltaTime * 0.8f);
            Hud.Roll = Feel;
            Hud.PopAmount = FeelCurves.CountPop(_rate);
            if (_stamp < 0f) return;
            var f = Round.Settings.Feel;
            _stamp += Time.unscaledDeltaTime;
            BadgeScale = FeelCurves.Stamp(_stamp / Mathf.Max(0.01f, f.StampTime), f.StampPeak, f.StampRest);
            Place();
            if (_stamp >= f.StampTime + 1.5f) HideBadge();
        }
    }
}
