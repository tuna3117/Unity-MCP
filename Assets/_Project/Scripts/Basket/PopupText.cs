using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Basket
{
    /// <summary>World-anchored score popups on an overlay canvas: rise 60 px and fade over their life.</summary>
    public class PopupText : MonoBehaviour
    {
        public Canvas Canvas;
        public Font Font;
        public Camera Cam;

        private class Item { public Text Text; public RectTransform Rect; public float T, Life; public Vector2 Start; public Color Color; }
        private readonly List<Item> _items = new List<Item>();
        private RectTransform _canvasRect;

        private void Awake()
        {
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Canvas == null)
            {
                var go = new GameObject("Popups", typeof(Canvas), typeof(CanvasScaler));
                go.transform.SetParent(transform, false);
                Canvas = go.GetComponent<Canvas>();
                Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Canvas.sortingOrder = 5;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            _canvasRect = Canvas.GetComponent<RectTransform>();
        }

        public void Show(Vector3 world, string text, Color color, int size, bool center = false, float life = 0.9f)
        {
            if (Cam == null) Cam = Camera.main;
            var go = new GameObject("Popup", typeof(Text), typeof(Outline));
            go.transform.SetParent(Canvas.transform, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.16f, 0.31f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, size * 1.6f);

            Vector2 start;
            if (center || Cam == null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.58f);
                start = Vector2.zero;
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                var screen = Cam.WorldToScreenPoint(world);
                if (screen.z < 0f) { Destroy(go); return; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local);
                start = local;
            }
            rt.anchoredPosition = start;
            _items.Add(new Item { Text = t, Rect = rt, Life = life, Start = start, Color = color });
        }

        private void Update()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                it.T += Time.unscaledDeltaTime;
                float u = it.T / it.Life;
                if (u >= 1f)
                {
                    Destroy(it.Text.gameObject);
                    _items.RemoveAt(i);
                    continue;
                }
                it.Rect.anchoredPosition = it.Start + Vector2.up * (60f * u);
                float scale = u < 0.15f ? Mathf.Lerp(0.6f, 1.15f, u / 0.15f) : Mathf.Lerp(1.15f, 1f, (u - 0.15f) / 0.85f);
                it.Rect.localScale = Vector3.one * scale;
                var c = it.Color; c.a = 1f - u * u;
                it.Text.color = c;
            }
        }
    }
}
