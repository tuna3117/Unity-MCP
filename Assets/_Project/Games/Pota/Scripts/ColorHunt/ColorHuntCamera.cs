using Project.Juice;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's puzzle camera (yapbozSahne.ts boyutla, kasasız): straight at the board; the frame's outer width fills 98.5 % of the screen
    /// width unless frame + ball strip do not fit the band between the HUD and the buttons (then the band decides). Applies the shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ColorHuntCamera : MonoBehaviour
    {
        public ColorHuntSettings S;
        [Tooltip("World position of the board's game root (PotaFlow places the board away from the origin).")]
        public Vector3 Anchor;
        [Tooltip("HUD band top in Pota CSS px; < 0 = the counters' bottom + 10 under the safe area (renkAviFaz.ts boyutla).")]
        public float TopCss = -1f;
        [Tooltip("Button band under the board in Pota CSS px (renkAviFaz.ts dugmePayi).")]
        public float BottomCss = PotaLook.ButtonBandCss;

        private Camera _cam;
        private CameraShake _shake;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _shake = GetComponent<CameraShake>();
        }

        /// <summary>Close-up for recordings: 1 = the Pota fit; 2 = half the distance, centred halfway to <see cref="ZoomFocus"/>.</summary>
        public float Zoom = 1f;
        /// <summary>F9: metres the camera sits back from its fit (the lose moment pulls it; 0 = the fit).</summary>
        public float BackOffset;
        public Vector3 ZoomFocus;
        private Vector3 _pushTarget, _pushFrom;
        private float _pushAmount, _pushFromAmount, _pushTime = -1f, _pushDuration = 1f;
        /// <summary>Royal's rockets: the axis shake swings at this rate (Hz).</summary>
        public const float AxisShakeHz = 18f;
        private Vector2 _axis;
        private float _axisAmount, _axisT;

        /// <summary>The axis shake's current size (Pota sars units; 0 = still).</summary>
        public float AxisShakeAmount => _axisAmount;

        /// <summary>
        /// Spec 2026-10-01 Royal "eksen boyunca sarsıntı": a shake along one screen axis, ±amount/2 like CameraShake.Add, swinging at
        /// <see cref="AxisShakeHz"/> and fading 2.2/s of game time (dips freeze it, as Pota's sars). A stronger call wins; capped at
        /// CameraShake.LinearCap. Adds to the camera's other shake.
        /// </summary>
        public void AxisShake(Vector2 axis, float amount)
        {
            if (axis.sqrMagnitude < 1e-8f || amount <= 0f) return;
            _axis = axis.normalized;
            _axisAmount = Mathf.Min(CameraShake.LinearCap, Mathf.Max(_axisAmount, amount));
            _axisT = 0f;
        }

        public static Vector2 AxisShakeOffset(Vector2 axis, float amount, float t) =>
            axis.normalized * (0.5f * amount * Mathf.Sin(2f * Mathf.PI * AxisShakeHz * t));

        public bool Pushing => _pushTime >= 0f;

        /// <summary>A short push toward <paramref name="target"/>: <paramref name="amount"/> of the way in and back out (sin), real time.</summary>
        /// <summary>
        /// The kick toward <paramref name="target"/>: <paramref name="amount"/> of the way, sin in and out over <paramref name="seconds"/>.
        /// A push still running (the combo's merge push → its fire kick) hands over from where it is: the new kick starts at the old one's
        /// weight and spot (<see cref="KickWeight"/>), so the camera never jumps back in one frame (Pota yapbozSahne.ts itme, Task 12).
        /// With nothing running it is the plain sin push.
        /// </summary>
        public void Push(Vector3 target, float amount, float seconds)
        {
            if (_pushTime >= 0f)
            {
                float u = _pushTime / _pushDuration;
                var from = KickSpot(u, _pushFrom, _pushTarget);
                _pushFromAmount = KickWeight(u, _pushFromAmount, _pushAmount);
                _pushFrom = from;
            }
            else { _pushFromAmount = 0f; _pushFrom = target; }
            _pushTarget = target;
            _pushAmount = amount;
            _pushDuration = Mathf.Max(0.01f, seconds);
            _pushTime = 0f;
        }

        /// <summary>
        /// A kick's weight at u: the first half eases from <paramref name="from"/> (the weight it took over) to <paramref name="amount"/>
        /// (from + (amount − from)·sin πu), the second half is amount·sin πu. from = 0 → amount × <see cref="PushWeight"/>.
        /// </summary>
        public static float KickWeight(float u, float from, float amount)
            => u <= 0f ? from : u >= 1f ? 0f : u < 0.5f ? from + (amount - from) * Mathf.Sin(u * Mathf.PI) : amount * Mathf.Sin(u * Mathf.PI);

        /// <summary>The kick's spot at u: slides from the handed-over spot to the target over the first half (sin), then the target.</summary>
        public static Vector3 KickSpot(float u, Vector3 from, Vector3 target)
            => u > 0f && u < 0.5f ? Vector3.LerpUnclamped(from, target, Mathf.Sin(u * Mathf.PI)) : u <= 0f ? from : target;

        public static Vector3 Zoomed(Vector3 fit, Vector3 focus, float zoom)
        {
            if (zoom <= 1f) return fit;
            float k = 1f - 1f / zoom;
            return new Vector3(Mathf.Lerp(fit.x, focus.x, k), Mathf.Lerp(fit.y, focus.y, k), focus.z + (fit.z - focus.z) / zoom);
        }

        public static float PushWeight(float u) => u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);

        private void LateUpdate()
        {
            var pos = Zoomed(FitFor(_cam.fieldOfView, _cam.aspect), ZoomFocus, Zoom) + Vector3.back * BackOffset;
            if (_pushTime >= 0f)
            {
                _pushTime += Time.unscaledDeltaTime;
                float u = _pushTime / _pushDuration;
                pos += (KickSpot(u, _pushFrom, _pushTarget) - pos) * KickWeight(u, _pushFromAmount, _pushAmount);
                if (u >= 1f) _pushTime = -1f;
            }
            Vector3 axisOffset = Vector3.zero;
            if (_axisAmount > 0f)
            {
                _axisT += Time.deltaTime;
                axisOffset = AxisShakeOffset(_axis, _axisAmount, _axisT);
                _axisAmount = CameraShake.StepLinear(_axisAmount, Time.deltaTime);
            }
            transform.position = pos + (_shake != null ? _shake.Offset : Vector3.zero) + axisOffset;
            transform.rotation = Quaternion.identity;
        }

        /// <summary>The HUD band's top in CSS px now (safe area included).</summary>
        public float TopCssNow => TopCss >= 0f ? TopCss : PotaLook.HudTopCss(SafeTopCss());

        /// <summary>This camera's fit (valid while the component is disabled: PotaFlow flies to it).</summary>
        public Vector3 FitFor(float fov, float aspect) => FitPosition(fov, aspect, Anchor, TopCssNow, BottomCss);

        public static Vector3 FitPosition(float fov, float aspect, Vector3 anchor) =>
            FitPosition(fov, aspect, anchor, PotaLook.HudTopCss(0f), PotaLook.ButtonBandCss);

        public static Vector3 FitPosition(float fov, float aspect, Vector3 anchor, float topCss, float bottomCss) =>
            PotaLook.CameraPosition(anchor, PotaLook.CameraFit(aspect, topCss, bottomCss, fov));

        /// <summary>The screen's top safe inset in Pota CSS px (the phone is 390 CSS px wide).</summary>
        public static float SafeTopCss() =>
            Screen.width <= 0 ? 0f : (Screen.height - Screen.safeArea.yMax) * PotaLook.PhoneCssWidth / Screen.width;
    }
}
