using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// A Pota button: a 3D lip under a gradient face with the label, the press sinking the face to 0.94 and the lip to 2 css px
    /// (Pota's translateY(4px)), the release springing to 1.04 and resting (M1). Feel off = Pota's instant 4-px press. Real time.
    /// </summary>
    public class MetaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public enum Kind { Big, Small, Chip, Round }
        public enum Tone { Green, Orange, Blue, Navy, Grey, Red }

        private MetaFeelTuning _feel;
        private RectTransform _face;
        private Image _lip;
        private float _press, _release = -1f, _lipHome;
        private bool _down;

        public RectTransform Rect => (RectTransform)transform;
        public RectTransform Face => _face;
        public Text Label { get; private set; }
        public Image Icon { get; private set; }
        public Button Button { get; private set; }
        public bool Pressed => _down;
        public float Scale => _face != null ? _face.localScale.x : 1f;
        public bool Interactable { get => Button.interactable; set => Button.interactable = value; }
        public event Action Clicked;

        public static (Color light, Color mid, Color dark, Color lip) Colors(Tone tone)
        {
            switch (tone)
            {
                case Tone.Orange: return (MetaStyle.OrangeLight, MetaStyle.Orange, MetaStyle.Orange2, MetaStyle.OrangeShadow);
                case Tone.Blue: return (MetaStyle.BlueLight, MetaStyle.Blue, MetaStyle.BlueDark, MetaStyle.BlueShadow);
                case Tone.Navy: return (MetaStyle.PanelTop, MetaStyle.Navy, MetaStyle.Navy, MetaStyle.Gold2);
                case Tone.Grey: return (UnityEngine.Color.Lerp(MetaStyle.Lock, Color.white, 0.2f), MetaStyle.Lock, MetaStyle.Lock, UnityEngine.Color.Lerp(MetaStyle.Lock, MetaStyle.Navy, 0.3f));
                case Tone.Red: return (MetaStyle.Red, MetaStyle.Red, MetaStyle.RedDark, MetaStyle.RedShadow);
                default: return (MetaStyle.GreenLight, MetaStyle.Green, MetaStyle.GreenDark, MetaStyle.Green2);
            }
        }

        /// <summary>Builds a button of <paramref name="sizeCss"/> under <paramref name="parent"/>; the label uses <paramref name="font"/>.</summary>
        public static MetaButton Create(Transform parent, string name, Kind kind, Tone tone, string label, Vector2 sizeCss, Font font, float fontCss, MetaFeelTuning feel)
        {
            var (light, mid, dark, lipColor) = Colors(tone);
            var go = new GameObject(name, typeof(RectTransform), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeCss * MetaStyle.Css;
            var b = go.AddComponent<MetaButton>();
            b._feel = feel;
            b.Button = go.GetComponent<Button>();
            b.Button.transition = Selectable.Transition.None;
            int lipCss = kind == Kind.Big ? 6 : 3, radiusCss = kind == Kind.Big ? 22 : kind == Kind.Round ? 999 : 12;
            // soft drop shadow, the lip, then the face (Pota's three box-shadows)
            var soft = Layer(rt, "Soft", MetaStyle.Shadow(kind == Kind.Big ? 22 : 12, kind == Kind.Big ? 18 : 10), new Color(0f, 0f, 0f, 0.3f), new Vector2(0f, -(lipCss + 4) * MetaStyle.Css), 6f * MetaStyle.Css);
            soft.raycastTarget = false;
            b._lip = Layer(rt, "Lip", kind == Kind.Round ? MetaStyle.Circle(Mathf.RoundToInt(sizeCss.x), lipColor, lipColor, lipColor, 0) : MetaStyle.Lip(radiusCss, lipColor), Color.white, new Vector2(0f, -lipCss * MetaStyle.Css), 0f);
            b._lip.raycastTarget = false;
            b._lipHome = -lipCss * MetaStyle.Css;
            var faceSprite = kind == Kind.Big ? MetaStyle.BigButton(light, mid, dark)
                : kind == Kind.Round ? MetaStyle.Circle(Mathf.RoundToInt(sizeCss.x), light, dark, Color.white, 3)
                : MetaStyle.SmallButton(light, dark);
            var face = Layer(rt, "Face", faceSprite, Color.white, Vector2.zero, 0f);
            face.raycastTarget = true;
            b._face = face.rectTransform;
            b._face.pivot = new Vector2(0.5f, 0.5f);
            b.Button.targetGraphic = face;
            if (!string.IsNullOrEmpty(label))
            {
                var t = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
                t.transform.SetParent(b._face, false);
                var trt = (RectTransform)t.transform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                var text = t.GetComponent<Text>();
                text.font = font;
                text.fontSize = Mathf.RoundToInt(fontCss * MetaStyle.Css);
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = label;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                var o = t.GetComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.2f);
                o.effectDistance = new Vector2(0.5f, -0.5f) * MetaStyle.Css;
                Shadow sh = null;
                foreach (var s in t.GetComponents<Shadow>()) if (s.GetType() == typeof(Shadow)) sh = s;
                sh.effectColor = new Color(0f, 0f, 0f, 0.3f);
                sh.effectDistance = new Vector2(0f, -2f) * MetaStyle.Css;
                b.Label = text;
            }
            return b;
        }

        /// <summary>Adds an icon (Pota .dugmeIkon 30 css) left of the label; the label shifts right.</summary>
        public MetaButton WithIcon(Sprite sprite, float sizeCss = 30f)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_face, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(16f * MetaStyle.Css, 0f);
            rt.sizeDelta = Vector2.one * (sizeCss * MetaStyle.Css);
            Icon = go.GetComponent<Image>();
            Icon.sprite = sprite;
            Icon.preserveAspect = true;
            Icon.raycastTarget = false;
            if (Label != null) Label.rectTransform.offsetMin = new Vector2((sizeCss + 16f) * MetaStyle.Css, 0f);
            return this;
        }

        public void SetTone(Tone tone)
        {
            var (light, mid, dark, lipColor) = Colors(tone);
            var face = _face.GetComponent<Image>();
            face.sprite = face.sprite != null && face.sprite.rect.width == MetaStyle.R(46) ? MetaStyle.Circle(46, light, dark, Color.white, 3) : MetaStyle.BigButton(light, mid, dark);
            _lip.sprite = MetaStyle.Lip(22, lipColor);
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
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public void OnPointerDown(PointerEventData e) { if (!Button.interactable) return; _down = true; _release = -1f; }
        public void OnPointerUp(PointerEventData e) { if (!_down) return; _down = false; _release = 0f; }
        public void OnPointerClick(PointerEventData e) { if (Button.interactable) Clicked?.Invoke(); }

        /// <summary>Tests and the recorder: press, then release (the click fires).</summary>
        public void Tap()
        {
            if (!Button.interactable) return;
            _down = false; _release = 0f; _press = 1f;
            Clicked?.Invoke();
        }

        private void Update()
        {
            if (_face == null || _feel == null) return;
            float dt = Time.unscaledDeltaTime;
            float scale;
            float lip = _lipHome;
            if (!_feel.Enabled)
            {
                scale = 1f;
                _face.anchoredPosition = _down ? new Vector2(0f, -4f * MetaStyle.Css) : Vector2.zero;   // Pota's translateY(4px)
                _press = _down ? 1f : 0f; _release = -1f;
            }
            else if (_down)
            {
                _press = Mathf.Min(1f, _press + dt / Mathf.Max(0.01f, _feel.Press));
                scale = MetaStyle.PressScale(_press);
                lip = Mathf.Lerp(_lipHome, -2f * MetaStyle.Css, _press);
                _face.anchoredPosition = new Vector2(0f, (_lipHome - lip) * 0.5f);
            }
            else if (_release >= 0f)
            {
                _release += dt / Mathf.Max(0.01f, _feel.Release);
                scale = MetaStyle.ReleaseScale(Mathf.Min(1f, _release));
                _face.anchoredPosition = Vector2.zero;
                if (_release >= 1f) { _release = -1f; _press = 0f; scale = 1f; }
            }
            else scale = 1f;
            _face.localScale = Vector3.one * scale;
            _lip.rectTransform.offsetMin = new Vector2(0f, lip);
            _lip.rectTransform.offsetMax = new Vector2(0f, lip);
        }
    }
}
