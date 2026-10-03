using System;
using UnityEngine;

namespace Project.Meta
{
    /// <summary>
    /// M2: a full-screen page (map, market) slides up from the bottom over the menu and slides back down; a CanvasGroup fades with it.
    /// Feel off = Pota's instant show/hide. Real time.
    /// </summary>
    public class ScreenSlide : MonoBehaviour
    {
        private MetaFeelTuning _feel;
        private RectTransform _rt;
        private CanvasGroup _group;
        private float _t = -1f;
        private bool _showing;
        private Action _done;

        public bool Visible => gameObject.activeSelf;
        public bool Moving => _t >= 0f;
        public float Offset => _rt != null ? _rt.anchoredPosition.y : 0f;

        public static ScreenSlide Attach(RectTransform page, MetaFeelTuning feel)
        {
            var s = page.gameObject.AddComponent<ScreenSlide>();
            s._feel = feel;
            s._rt = page;
            s._group = page.GetComponent<CanvasGroup>() ?? page.gameObject.AddComponent<CanvasGroup>();
            page.gameObject.SetActive(false);
            return s;
        }

        public void Show(Action done = null)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _showing = true; _t = 0f; _done = done;
            Apply();
        }

        public void Hide(Action done = null)
        {
            if (!gameObject.activeSelf) { done?.Invoke(); return; }
            _showing = false; _t = 0f; _done = done;
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
            float slide = _feel.T(_feel.Slide);
            float u = slide > 0f ? Mathf.Clamp01(_t / slide) : 1f;
            float k = _showing ? ColorHunt.StudioCurves.EaseOutCubic(u) : 1f - ColorHunt.StudioCurves.EaseOutCubic(u);
            float h = _rt.rect.height > 0f ? _rt.rect.height : 1920f;
            _rt.anchoredPosition = new Vector2(0f, -(1f - k) * h * 0.35f);
            _group.alpha = k;
            if (u >= 1f)
            {
                _t = -1f;
                if (!_showing) gameObject.SetActive(false);
                var d = _done; _done = null; d?.Invoke();
            }
        }
    }
}
