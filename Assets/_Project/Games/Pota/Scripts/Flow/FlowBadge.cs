using UnityEngine;
using UnityEngine.UI;

namespace Project.Flow
{
    /// <summary>
    /// The transition's "N TOP" label on its own overlay canvas (above both HUDs): pops at the cage, docks while the camera
    /// flies, then flies into the ammo panel. The text is set once per transition (no per-frame allocation).
    /// </summary>
    public class FlowBadge : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _rt;
        private Text _text;
        private Color _color;

        public string Label => _text.text;
        public bool Visible => _canvas.enabled;
        public Canvas Canvas => _canvas;

        public static FlowBadge Create(Transform parent, Font font, Color color, Color outline)
        {
            var go = new GameObject("FlowBadge", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            label.transform.SetParent(go.transform, false);
            var rt = label.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(900f, 200f);
            var text = label.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 120;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            var o = label.GetComponent<Outline>();
            o.effectColor = outline;
            o.effectDistance = new Vector2(6f, -6f);

            var badge = go.AddComponent<FlowBadge>();
            badge._canvas = canvas;
            badge._rt = rt;
            badge._text = text;
            badge._color = color;
            canvas.enabled = false;
            return badge;
        }

        public void Show(int count)
        {
            _text.text = count + " TOP";
            _canvas.enabled = true;
        }

        public void Hide() => _canvas.enabled = false;

        /// <summary>
        /// Viewport position (0..1, bottom-left origin), uniform scale, alpha 0..1. Placed by anchors, not by converting screen
        /// pixels through the canvas transform: that transform is stale while a recording moves the canvas onto a camera.
        /// </summary>
        public void Place(Vector2 viewport, float scale, float alpha)
        {
            _rt.anchorMin = _rt.anchorMax = viewport;
            _rt.anchoredPosition = Vector2.zero;
            _rt.localScale = Vector3.one * scale;
            var c = _color;
            c.a = _color.a * Mathf.Clamp01(alpha);
            _text.color = c;
        }
    }
}
