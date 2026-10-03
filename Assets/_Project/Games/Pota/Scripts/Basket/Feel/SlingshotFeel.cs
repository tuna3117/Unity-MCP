using Project.ColorHunt;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>
    /// F10 slingshot feel (spec 2026-10-03): while the finger pulls, the hero leans back and crouches with the pull, the trajectory
    /// thickens and brightens, the aimed top hoop glows, the camera eases back and down. Everything returns on release or shot.
    /// Off (or the feel layer off) = nothing moves.
    /// </summary>
    public class SlingshotFeel : MonoBehaviour
    {
        public BasketRound Round;
        public ShotInput Input;
        public AimPreview Preview;
        public RoundCamera Cam;

        private Hoop _glowing;
        private bool _bound;

        /// <summary>The current pull 0..1 (FeelCurves.PullFraction of the aim's power); 0 when nothing is pulled.</summary>
        public float Pull { get; private set; }

        private void Start() => Bind();

        private void Bind()
        {
            if (_bound) return;
            if (Round == null) Round = GetComponent<BasketRound>() ?? FindAnyObjectByType<BasketRound>();
            if (Input == null) Input = GetComponent<ShotInput>() ?? FindAnyObjectByType<ShotInput>();
            if (Preview == null && Input != null) Preview = Input.Preview;
            if (Cam == null) Cam = FindAnyObjectByType<RoundCamera>();
            if (Round == null || Input == null) return;
            Input.Pulled += OnPulled;
            Input.Released += Release;
            Round.ShotFired += _ => Release();
            Round.StateChanged += _ => Release();
            _bound = true;
        }

        private void OnDestroy()
        {
            if (!_bound) return;
            Input.Pulled -= OnPulled;
            Input.Released -= Release;
        }

        private bool Feel => Round != null && Round.Settings != null && Round.Settings.Feel != null && Round.Settings.Feel.Enabled;

        private void OnPulled(float pull, AimPoint aim, bool valid)
        {
            if (!Feel) { Release(); return; }
            Pull = valid ? pull : 0f;
            var f = Round.Settings.Feel;
            float k = FeelCurves.Lean(Pull);
            var actor = Round.Column != null ? Round.Column.Player : null;
            if (actor != null) actor.SetLean(Pull);
            Preview?.SetPower(Pull);
            if (Cam != null)
            {
                Cam.PullOffset = Vector3.back * (-f.CamBackM * k);          // the camera looks down −z toward the hoops; back = +z
                Cam.PullPitch = f.CamDownDeg * k;
            }
            var hoop = valid ? Round.TargetHoopFor(aim) : null;
            if (hoop != _glowing)
            {
                _glowing?.SetGlow(0f);
                _glowing = hoop;
            }
            _glowing?.SetGlow(1f);
        }

        /// <summary>Everything back to rest (release, shot, state change, feel off).</summary>
        public void Release()
        {
            Pull = 0f;
            var actor = Round != null && Round.Column != null ? Round.Column.Player : null;
            if (actor != null) actor.SetLean(0f);
            Preview?.SetPower(0f);
            if (Cam != null) { Cam.PullOffset = Vector3.zero; Cam.PullPitch = 0f; }
            _glowing?.SetGlow(0f);
            _glowing = null;
        }

        private void Update()
        {
            if (!_bound) Bind();
            if (!Feel && Pull > 0f) Release();
        }
    }
}
