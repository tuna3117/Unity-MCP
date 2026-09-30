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

        private void LateUpdate()
        {
            transform.position = FitFor(_cam.fieldOfView, _cam.aspect) + (_shake != null ? _shake.Offset : Vector3.zero);
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
