using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// A Pota popup (.pencere + .kutu): full-screen backdrop, cream box with gold border and white inner line, the orange ribbon title
    /// hanging over the top edge, an optional red × close. Open: backdrop fades, box 0.85 → 1.05 → 1, ribbon drops in (M1). Close: box
    /// shrinks to 0.9 and fades. Feel off = instant. Content goes under <see cref="Content"/> (vertical layout, gap 12 css).
    /// </summary>
    public class MetaWindow : MonoBehaviour
    {
        public enum Phase { Closed, Opening, Open, Closing }

        private MetaFeelTuning _feel;
        private Image _backdrop;
        private RectTransform _box, _ribbon;
        private CanvasGroup _group;
        private Vector2 _ribbonHome;
        private float _t = -1f;
        private Action _onClosed;

        public Phase State { get; private set; } = Phase.Closed;
        public bool IsOpen => State == Phase.Open || State == Phase.Opening;
        public RectTransform Content { get; private set; }
        public Text Title { get; private set; }
        public MetaButton Close { get; private set; }
        public RectTransform Box => _box;
        public float BoxScale => _box != null ? _box.localScale.x : 1f;
        public float BackdropAlpha => _backdrop != null ? _backdrop.color.a : 0f;
        public event Action Opened, Closed;

        public static MetaWindow Create(Transform parent, string name, string title, Font titleFont, MetaFeelTuning feel, bool closeButton, float widthCss = 343f)
        {
            float css = MetaStyle.Css;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var w = go.AddComponent<MetaWindow>();
            w._feel = feel;
            // backdrop
            var back = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(rt, false);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            w._backdrop = back.GetComponent<Image>();
            w._backdrop.color = MetaStyle.Backdrop;
            // box
            var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasGroup));
            box.transform.SetParent(rt, false);
            w._box = (RectTransform)box.transform;
            w._box.anchorMin = w._box.anchorMax = new Vector2(0.5f, 0.5f);
            w._box.pivot = new Vector2(0.5f, 0.5f);
            w._box.sizeDelta = new Vector2(widthCss * css, 400f * css);
            w._group = box.GetComponent<CanvasGroup>();
            var soft = Layer(w._box, "Soft", MetaStyle.Shadow(30, 22), new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -22f * css), 10f * css);
            soft.raycastTarget = false;
            Layer(w._box, "Lip", MetaStyle.Lip(30, MetaStyle.Gold2), Color.white, new Vector2(0f, -8f * css), 0f).raycastTarget = false;
            Layer(w._box, "Face", MetaStyle.Box(), Color.white, Vector2.zero, 0f);
            Layer(w._box, "Inner", MetaStyle.BoxInner(), Color.white, Vector2.zero, -4f * css).raycastTarget = false;
            // content (vertical layout)
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(w._box, false);
            w.Content = (RectTransform)content.transform;
            w.Content.anchorMin = new Vector2(0f, 1f); w.Content.anchorMax = new Vector2(1f, 1f);
            w.Content.pivot = new Vector2(0.5f, 1f);
            w.Content.offsetMin = new Vector2(20f * css, 0f); w.Content.offsetMax = new Vector2(-20f * css, -16f * css);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f * css;
            layout.padding = new RectOffset(0, 0, Mathf.RoundToInt(10f * css), Mathf.RoundToInt(26f * css));
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // ribbon over the top edge
            var ribbon = new GameObject("Ribbon", typeof(RectTransform), typeof(Image));
            ribbon.transform.SetParent(w._box, false);
            w._ribbon = (RectTransform)ribbon.transform;
            w._ribbon.anchorMin = w._ribbon.anchorMax = new Vector2(0.5f, 1f);
            w._ribbon.pivot = new Vector2(0.5f, 0.5f);
            w._ribbon.sizeDelta = new Vector2((widthCss + 24f) * css, 54f * css);
            w._ribbonHome = new Vector2(0f, 2f * css);
            w._ribbon.anchoredPosition = w._ribbonHome;
            var rimg = ribbon.GetComponent<Image>();
            rimg.sprite = MetaStyle.Ribbon(); rimg.type = Image.Type.Sliced;
            Layer(w._ribbon, "Lip", MetaStyle.Lip(22, MetaStyle.OrangeShadow), Color.white, new Vector2(0f, -5f * css), 0f).transform.SetAsFirstSibling();
            var tgo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(Shadow));
            tgo.transform.SetParent(w._ribbon, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            w.Title = tgo.GetComponent<Text>();
            w.Title.font = titleFont; w.Title.fontSize = Mathf.RoundToInt(28f * css); w.Title.alignment = TextAnchor.MiddleCenter;
            w.Title.color = Color.white; w.Title.text = title; w.Title.raycastTarget = false;
            w.Title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tsh = tgo.GetComponent<Shadow>(); tsh.effectColor = new Color(0f, 0f, 0f, 0.3f); tsh.effectDistance = new Vector2(0f, -2f * css);
            if (closeButton)
            {
                w.Close = MetaButton.Create(w._box, "Close", MetaButton.Kind.Round, MetaButton.Tone.Red, "×", new Vector2(46f, 46f), titleFont, 24f, feel);
                var crt = w.Close.Rect;
                crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(16f * css, 18f * css);
                w.Close.Clicked += () => w.Hide();
            }
            w.gameObject.SetActive(false);
            return w;
        }

        private static Image Layer(RectTransform parent, string name, Sprite sprite, Color color, Vector2 offset, float grow)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offset - Vector2.one * grow;
            rt.offsetMax = offset + Vector2.one * grow;
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Sliced; img.color = color;
            return img;
        }

        /// <summary>Sizes the box to its content (call after filling Content).</summary>
        public void Fit()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            _box.sizeDelta = new Vector2(_box.sizeDelta.x, Content.rect.height + 16f * MetaStyle.Css);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            State = Phase.Opening;
            _t = 0f;
            Apply();
        }

        public void Hide(Action onClosed = null)
        {
            if (State == Phase.Closed) { onClosed?.Invoke(); return; }
            _onClosed = onClosed;
            State = Phase.Closing;
            _t = 0f;
            Apply();
        }

        private void Update()
        {
            if (State == Phase.Opening || State == Phase.Closing) { _t += Time.unscaledDeltaTime; Apply(); }
        }

        private void Apply()
        {
            if (State == Phase.Opening)
            {
                float open = _feel.T(_feel.WindowOpen), back = _feel.T(_feel.Backdrop), drop = _feel.T(_feel.RibbonDrop), delay = _feel.T(_feel.RibbonDelay);
                float u = open > 0f ? Mathf.Clamp01(_t / open) : 1f;
                float ub = back > 0f ? Mathf.Clamp01(_t / back) : 1f;
                float ur = drop > 0f ? Mathf.Clamp01((_t - delay) / drop) : 1f;
                _box.localScale = Vector3.one * MetaStyle.WindowOpen(u);
                _group.alpha = Mathf.Clamp01(u * 4f);
                var c = MetaStyle.Backdrop; c.a = MetaStyle.BackdropAlpha(ub); _backdrop.color = c;
                _ribbon.anchoredPosition = _ribbonHome + new Vector2(0f, -MetaStyle.RibbonDropCss(ur) * MetaStyle.Css);
                if (_t >= Mathf.Max(open, back, delay + drop)) { State = Phase.Open; _box.localScale = Vector3.one; _group.alpha = 1f; _ribbon.anchoredPosition = _ribbonHome; Opened?.Invoke(); }
            }
            else if (State == Phase.Closing)
            {
                float close = _feel.T(_feel.WindowClose);
                float u = close > 0f ? Mathf.Clamp01(_t / close) : 1f;
                _box.localScale = Vector3.one * MetaStyle.WindowClose(u);
                _group.alpha = 1f - u;
                var c = MetaStyle.Backdrop; c.a = MetaStyle.BackdropAlpha(1f - u); _backdrop.color = c;
                if (u >= 1f)
                {
                    State = Phase.Closed;
                    gameObject.SetActive(false);
                    var cb = _onClosed; _onClosed = null;
                    Closed?.Invoke();
                    cb?.Invoke();
                }
            }
        }
    }
}
