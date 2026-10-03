using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>M3 on a coin badge: a 1.25 squash on every arrival, a gold glint on the last coin, the icon spinning while a spend drops. Real time; the director calls these only with the feel layer on.</summary>
    public class CoinBadgeFx : MonoBehaviour
    {
        public const float PunchTime = 0.18f, GlintTime = 0.3f;

        private RectTransform _rt, _icon;
        private Image _flash;
        private float _punch = -1f, _glint = -1f, _spin = -1f, _spinTime = 1f;

        public float Scale => _rt != null ? _rt.localScale.x : 1f;
        /// <summary>The glint's strength 0..1 (0 when idle).</summary>
        public float Glint => _glint >= 0f ? 1f - Mathf.Clamp01(_glint / GlintTime) : 0f;

        public static CoinBadgeFx Attach(MetaButton badge)
        {
            var fx = badge.gameObject.AddComponent<CoinBadgeFx>();
            fx._rt = badge.Rect;
            fx._icon = badge.Icon != null ? badge.Icon.rectTransform : null;
            var go = new GameObject("Glint", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(badge.Face, false);
            var grt = (RectTransform)go.transform;
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = grt.offsetMax = Vector2.zero;
            fx._flash = go.GetComponent<Image>();
            fx._flash.sprite = MetaStyle.SmallButton(MetaStyle.GoldLight, MetaStyle.Gold); fx._flash.type = Image.Type.Sliced;
            fx._flash.color = new Color(1f, 1f, 1f, 0f); fx._flash.raycastTarget = false;
            return fx;
        }

        public void Punch() => _punch = 0f;
        public void GlintNow() => _glint = 0f;
        public void Spin(float seconds) { _spin = 0f; _spinTime = Mathf.Max(0.05f, seconds); }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_punch >= 0f)
            {
                _punch += dt;
                float u = Mathf.Clamp01(_punch / PunchTime);
                _rt.localScale = Vector3.one * FeelCurves.Stamp(u, 1.25f, 1f);
                if (_punch >= PunchTime) { _punch = -1f; _rt.localScale = Vector3.one; }
            }
            if (_glint >= 0f)
            {
                _glint += dt;
                _flash.color = new Color(1f, 1f, 1f, 0.85f * Glint);
                if (_glint >= GlintTime) { _glint = -1f; _flash.color = new Color(1f, 1f, 1f, 0f); }
            }
            if (_spin >= 0f && _icon != null)
            {
                _spin += dt;
                float u = Mathf.Clamp01(_spin / _spinTime);
                _icon.localRotation = Quaternion.Euler(0f, 0f, -360f * u);
                if (_spin >= _spinTime) { _spin = -1f; _icon.localRotation = Quaternion.identity; }
            }
        }
    }
}
