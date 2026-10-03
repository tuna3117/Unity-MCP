using Project.VFX;
using UnityEngine;

namespace Project.Basket
{
    /// <summary>
    /// F12 clean pass (spec 2026-10-03): a ball that goes through a top hoop without touching its rim is a swish — the SWISH! lettering
    /// pops over the hoop with the gold swish burst; a rim-in keeps today's feedback. The first top pass keeps its slow motion and flash.
    /// </summary>
    public class SwishFeel : MonoBehaviour
    {
        public BasketRound Round;

        private LetterPop _letter;
        private bool _bound;

        public bool LastClean { get; private set; }
        public bool LetterShown => _letter != null && _letter.Shown;

        private void Start() => Bind();

        private void Bind()
        {
            if (_bound) return;
            if (Round == null) Round = GetComponent<BasketRound>() ?? FindAnyObjectByType<BasketRound>();
            if (Round == null) return;
            Round.HoopPassed += OnPassed;
            _bound = true;
        }

        private void OnDestroy()
        {
            if (_bound && Round != null) Round.HoopPassed -= OnPassed;
        }

        private bool Feel => Round != null && Round.Settings != null && Round.Settings.Feel != null && Round.Settings.Feel.Enabled;

        private void OnPassed(Hoop hoop, Ball ball, bool firstTopSwish)
        {
            if (Feel) Pass(hoop, ball, firstTopSwish);
        }

        /// <summary>Judges a pass through <paramref name="hoop"/>: clean (no rim contact on a top hoop) shows SWISH!.</summary>
        public void Pass(Hoop hoop, Ball ball, bool firstTopSwish = false)
        {
            if (!_bound) Bind();
            if (hoop == null || ball == null || !hoop.Spec.IsTop) { LastClean = false; return; }
            LastClean = !ball.TouchedRim;
            if (!LastClean || Round == null || Round.Settings == null) return;
            var s = Round.Settings;
            if (_letter == null) _letter = new LetterPop(transform, "SwishLetter", s.LetterTemplate, s.LetterSwish, 16);
            var cam = Camera.main;
            _letter.Show(hoop.Center + new Vector3(0f, 0.75f, 0.3f), 1.5f, 0.9f, cam);
            if (!firstTopSwish) VfxService.Play("swish", hoop.Center + Vector3.down * 0.15f, Quaternion.identity);
        }

        private void Update()
        {
            if (!_bound) Bind();
            _letter?.Tick(Time.unscaledDeltaTime);
        }
    }
}
