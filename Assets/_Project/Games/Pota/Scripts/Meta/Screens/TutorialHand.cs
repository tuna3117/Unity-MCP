using System;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// M11: the tutorial's hand (Blender hand_pointer still) glides to its target in 0.3 s, taps every second (a 0.9 squash with a
    /// ring wave), slides along an arc to the next target; Pota's cream bubble carries the text. Feel off = Pota's #parmak: a white
    /// ring blinking on the target, no hand. Real time, canvas space of the root.
    /// </summary>
    public class TutorialHand : MonoBehaviour
    {
        public enum Mode { Hidden, Gliding, Tapping, Arcing }

        public const float Glide = 0.3f, TapEvery = 1f, TapDip = 0.15f, ArcTime = 0.6f, RingWave = 0.4f, Blink = 1f;

        private MetaSettings _s;
        private RectTransform _root, _rt, _hand, _ring, _halo, _bubbleRt;
        private Image _handImage, _ringImage, _haloImage;
        private Vector2 _from, _to;
        private float _t, _tapT, _wave = -1f;
        private int _tapsWanted;
        private Action _then;
        private RectTransform _target;

        public Mode State { get; private set; } = Mode.Hidden;
        public bool Visible => gameObject.activeSelf;
        public bool Moving => State == Mode.Gliding;
        public bool Arcing => State == Mode.Arcing;
        public int TapCount { get; private set; }
        public Text Bubble { get; private set; }
        public bool HandShown => _hand.gameObject.activeSelf;
        public bool RingShown => _ring.gameObject.activeSelf;
        /// <summary>The fingertip (canvas px, root space).</summary>
        public Vector2 Position => _rt.anchoredPosition;
        public Vector2 Target => _target != null ? MetaUiKit.CanvasPoint(_root, _target) : _to;

        public static TutorialHand Create(RectTransform root, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("TutorialHand", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var h = go.AddComponent<TutorialHand>();
            h._s = s; h._root = root;
            h._rt = (RectTransform)go.transform;
            h._rt.anchorMin = h._rt.anchorMax = h._rt.pivot = new Vector2(0.5f, 0.5f);
            h._rt.sizeDelta = Vector2.zero;
            // Pota's ring on the target: 46 circle, white border 4, an amber halo
            h._haloImage = MetaUiKit.Picture(h._rt, "Halo", MetaStyle.Circle(58, PotaLook.Hex(0xffb43c), PotaLook.Hex(0xffb43c), PotaLook.Hex(0xffb43c), 0), 58f, new Vector2(0.5f, 0.5f), Vector2.zero);
            h._haloImage.color = new Color(1f, 1f, 1f, 0.55f);
            h._halo = h._haloImage.rectTransform;
            h._ringImage = MetaUiKit.Picture(h._rt, "Ring", MetaStyle.Ring(46, Color.white, 4), 46f, new Vector2(0.5f, 0.5f), Vector2.zero);
            h._ring = h._ringImage.rectTransform;
            // the hand: fingertip at the pivot, the glove hanging down-right of it
            h._handImage = MetaUiKit.Picture(h._rt, "Hand", s.HandSprite, 80f, new Vector2(0.5f, 0.5f), Vector2.zero);
            h._hand = h._handImage.rectTransform;
            h._hand.pivot = new Vector2(0.5f, 0.93f);
            h._hand.anchoredPosition = new Vector2(6f * css, 0f);
            h._hand.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var sh = h._handImage.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.3f); sh.effectDistance = new Vector2(0f, -4f * css);
            // the bubble
            var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
            bubble.transform.SetParent(h._rt, false);
            h._bubbleRt = (RectTransform)bubble.transform;
            h._bubbleRt.anchorMin = h._bubbleRt.anchorMax = new Vector2(0.5f, 0.5f); h._bubbleRt.pivot = new Vector2(0.5f, 0f);
            h._bubbleRt.anchoredPosition = new Vector2(0f, 40f * css); h._bubbleRt.sizeDelta = new Vector2(200f, 34f) * css;
            var bimg = bubble.GetComponent<Image>(); bimg.sprite = ColorHuntHud.PanelSprite(128, MetaStyle.R(12), MetaStyle.CreamTop, MetaStyle.Cream, MetaStyle.Gold, MetaStyle.R(2), 0f, 0);
            bimg.type = Image.Type.Sliced; bimg.raycastTarget = false;
            h.Bubble = MetaUiKit.Label(h._bubbleRt, "Text", s.FredokaBold != null ? s.FredokaBold : s.Lilita, "", 13f, MetaStyle.HintText);
            go.SetActive(false);
            return h;
        }

        /// <summary>Points at <paramref name="target"/> with <paramref name="text"/>; after <paramref name="taps"/> taps, <paramref name="then"/>.</summary>
        public void PointAt(RectTransform target, string text, int taps, Action then = null)
        {
            Begin(target, text, taps, then);
            if (!_s.Feel.Enabled) { Place(_to); State = Mode.Tapping; _tapT = 0f; Apply(); return; }
            _from = Visible && State != Mode.Hidden ? _rt.anchoredPosition : _to + new Vector2(60f, -120f) * MetaStyle.Css;
            State = Mode.Gliding; _t = 0f;
            gameObject.SetActive(true);
            Apply();
        }

        /// <summary>Slides along an arc (bulging up) to <paramref name="target"/>, then taps there.</summary>
        public void ArcMove(RectTransform target, string text, int taps, Action then = null)
        {
            Vector2 here = Visible ? _rt.anchoredPosition : Vector2.zero;
            Begin(target, text, taps, then);
            if (!_s.Feel.Enabled) { Place(_to); State = Mode.Tapping; _tapT = 0f; Apply(); return; }
            _from = here;
            State = Mode.Arcing; _t = 0f;
            Apply();
        }

        private void Begin(RectTransform target, string text, int taps, Action then)
        {
            _target = target; _to = MetaUiKit.CanvasPoint(_root, target);
            Bubble.text = text; _tapsWanted = taps; _then = then; TapCount = 0; _wave = -1f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            bool feel = _s.Feel.Enabled;
            _hand.gameObject.SetActive(feel);
            _ring.gameObject.SetActive(true);
            _halo.gameObject.SetActive(!feel);
        }

        public void Hide()
        {
            State = Mode.Hidden;
            _then = null;
            gameObject.SetActive(false);
        }

        private void Place(Vector2 p) { _rt.anchoredPosition = p; }

        private void Update()
        {
            if (State == Mode.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            bool feel = _s.Feel.Enabled;
            if (!feel && (State == Mode.Gliding || State == Mode.Arcing)) { State = Mode.Tapping; _tapT = 0f; Place(_to); }
            _hand.gameObject.SetActive(feel);
            _halo.gameObject.SetActive(!feel);
            if (_target != null) _to = MetaUiKit.CanvasPoint(_root, _target);
            Apply();
        }

        private void Apply()
        {
            float css = MetaStyle.Css;
            switch (State)
            {
                case Mode.Gliding:
                {
                    float u = Mathf.Clamp01(_t / Glide);
                    Place(Vector2.Lerp(_from, _to, StudioCurves.EaseOutCubic(u)));
                    if (u >= 1f) { State = Mode.Tapping; _tapT = 0f; }
                    break;
                }
                case Mode.Arcing:
                {
                    float u = Mathf.Clamp01(_t / ArcTime), e = PotaLook.Smoothstep(u);
                    var mid = (_from + _to) * 0.5f + new Vector2(0f, 90f * css);
                    Place(Vector2.Lerp(Vector2.Lerp(_from, mid, e), Vector2.Lerp(mid, _to, e), e));
                    if (u >= 1f) { State = Mode.Tapping; _tapT = 0f; }
                    break;
                }
                case Mode.Tapping:
                {
                    if (!_s.Feel.Enabled)
                    {
                        // Pota's parmak: the ring blinks once a second
                        float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_t / Blink * Mathf.PI));
                        _ringImage.color = new Color(1f, 1f, 1f, blink);
                        _ring.localScale = Vector3.one;
                        _haloImage.color = new Color(1f, 1f, 1f, 0.55f * blink);
                        break;
                    }
                    _tapT += Time.unscaledDeltaTime;
                    if (_tapT >= TapEvery)
                    {
                        _tapT -= TapEvery; TapCount++; _wave = 0f;
                        if (TapCount >= _tapsWanted) { var next = _then; _then = null; if (next != null) { next(); return; } }
                    }
                    float dip = _wave >= 0f && _wave < TapDip ? Mathf.Sin(_wave / TapDip * Mathf.PI) : 0f;
                    _hand.localScale = Vector3.one * (1f - 0.1f * dip);
                    _hand.anchoredPosition = new Vector2(6f * css, -6f * css * dip);
                    if (_wave >= 0f)
                    {
                        _wave += Time.unscaledDeltaTime;
                        float w = Mathf.Clamp01(_wave / RingWave);
                        _ring.localScale = Vector3.one * Mathf.Lerp(1f, 1.9f, StudioCurves.EaseOutCubic(w));
                        _ringImage.color = new Color(1f, 1f, 1f, 1f - w);
                        if (w >= 1f) { _wave = -1f; }
                    }
                    else { _ring.localScale = Vector3.one; _ringImage.color = Color.white; }
                    break;
                }
            }
        }
    }
}
