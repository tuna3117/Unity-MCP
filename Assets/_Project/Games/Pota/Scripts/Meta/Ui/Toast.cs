using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>Pota's #bildirim: a cream pill near the bottom that rises in, holds and sinks out (M1). Feel off = Pota's keyframe-less show/hide.</summary>
    public class Toast : MonoBehaviour
    {
        private MetaFeelTuning _feel;
        private RectTransform _rt;
        private CanvasGroup _group;
        private Text _text;
        private Vector2 _home;
        private float _t = -1f;

        public bool Showing => _t >= 0f;
        public string Message => _text != null ? _text.text : "";

        public static Toast Create(Transform parent, Font font, MetaFeelTuning feel)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Toast>();
            t._feel = feel;
            t._rt = (RectTransform)go.transform;
            t._rt.anchorMin = t._rt.anchorMax = new Vector2(0.5f, 0f);
            t._rt.pivot = new Vector2(0.5f, 0.5f);
            t._home = new Vector2(0f, (10f + 150f + 20f) * css);
            t._rt.anchoredPosition = t._home;
            t._rt.sizeDelta = new Vector2(300f, 40f) * css;
            t._group = go.GetComponent<CanvasGroup>();
            t._group.blocksRaycasts = false;
            var img = go.GetComponent<Image>();
            img.sprite = MetaStyle.Panel(128, MetaStyle.R(20), MetaStyle.Cream, MetaStyle.Cream, MetaStyle.Cream, 0, 0f, 0);
            img.type = Image.Type.Sliced; img.raycastTarget = false;
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(t._rt, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(18f * css, 0f); trt.offsetMax = new Vector2(-18f * css, 0f);
            t._text = tgo.GetComponent<Text>();
            t._text.font = font; t._text.fontSize = Mathf.RoundToInt(15f * css); t._text.alignment = TextAnchor.MiddleCenter;
            t._text.color = MetaStyle.Navy; t._text.horizontalOverflow = HorizontalWrapMode.Overflow; t._text.raycastTarget = false;
            go.SetActive(false);
            return t;
        }

        public void Show(string message)
        {
            _text.text = message;
            float w = _text.preferredWidth + 36f * MetaStyle.Css;
            _rt.sizeDelta = new Vector2(Mathf.Max(w, 120f * MetaStyle.Css), 40f * MetaStyle.Css);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _t = 0f;
            Apply();
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            float tin = _feel.T(_feel.ToastIn), hold = _feel.Enabled ? _feel.ToastHold : 1.6f, tout = _feel.T(_feel.ToastOut);
            float a; float y;
            if (_t < tin) { float u = _t / tin; a = u; y = Mathf.Lerp(-16f, 0f, StudioCurves.EaseOutCubic(u)); }
            else if (_t < tin + hold) { a = 1f; y = 0f; }
            else if (_t < tin + hold + tout) { float u = (_t - tin - hold) / tout; a = 1f - u; y = Mathf.Lerp(0f, -12f, u); }
            else { _t = -1f; gameObject.SetActive(false); return; }
            _group.alpha = a;
            _rt.anchoredPosition = _home + new Vector2(0f, y * MetaStyle.Css);
        }

        private static class StudioCurves
        {
            public static float EaseOutCubic(float u) => ColorHunt.StudioCurves.EaseOutCubic(u);
        }
    }
}
