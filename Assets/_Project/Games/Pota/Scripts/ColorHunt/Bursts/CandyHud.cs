using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>A's chain words (İyi!, Süper!, Harika!, Muhteşem!) and the screen-edge glow, on the HUD canvas, real time.</summary>
    public class CandyHud : MonoBehaviour
    {
        private class WordFx { public Text T; public float Age = -1f; public Vector2 Home; }

        private ColorHuntHud _hud;
        private readonly WordFx[] _words = new WordFx[3];
        private int _next;
        private Image _edge;
        private float _edgeAge = -1f, _edgePeak;
        private Color _edgeColor;

        public string LastWord { get; private set; }
        public float EdgeAlpha => _edge != null && _edge.enabled ? _edge.color.a : 0f;

        public static string WordFor(int depth) =>
            depth >= 5 ? "Muhteşem!" : depth == 4 ? "Harika!" : depth == 3 ? "Süper!" : depth == 2 ? "İyi!" : null;

        public static Color WordColor(int depth) =>
            depth >= 5 ? PotaLook.Hex(0xb35cff) : depth == 4 ? PotaLook.Hex(0xff4d6d) : depth == 3 ? PotaLook.HudOrange : PotaLook.HudGold;

        public static CandyHud Attach(ColorHuntGame game)
        {
            if (game.Hud == null) return null;
            var go = new GameObject("CandyHud", typeof(RectTransform));
            go.transform.SetParent(game.Hud.transform, false);
            go.transform.SetAsFirstSibling();                       // under the HUD panels
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var hud = go.AddComponent<CandyHud>();
            hud.Init(game.Hud);
            return hud;
        }

        private void Init(ColorHuntHud hud)
        {
            _hud = hud;
            var edgeGo = new GameObject("EdgeGlow", typeof(RectTransform), typeof(Image));
            edgeGo.transform.SetParent(transform, false);
            _edge = edgeGo.GetComponent<Image>();
            _edge.sprite = StudioTextures.EdgeGlowSprite();
            _edge.raycastTarget = false;
            _edge.enabled = false;
            var ert = _edge.rectTransform;
            ert.anchorMin = Vector2.zero;
            ert.anchorMax = Vector2.one;
            ert.offsetMin = ert.offsetMax = Vector2.zero;
            for (int i = 0; i < _words.Length; i++)
            {
                var go = new GameObject("Word", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
                go.transform.SetParent(transform, false);
                var t = go.GetComponent<Text>();
                t.font = hud.HudFont;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                var outline = go.GetComponent<Outline>();
                outline.effectColor = PotaLook.HudNavy;
                outline.effectDistance = new Vector2(3f, -3f) * ColorHuntHud.Css;
                Shadow shadow = null;                                   // GetComponent<Shadow> would return the Outline (a Shadow)
                foreach (var sh in go.GetComponents<Shadow>()) if (sh.GetType() == typeof(Shadow)) shadow = sh;
                shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
                shadow.effectDistance = new Vector2(0f, -5f) * ColorHuntHud.Css;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(600f, 200f);
                go.SetActive(false);
                _words[i] = new WordFx { T = t };
            }
        }

        public void Word(int depth, Vector3 world)
        {
            string s = WordFor(depth);
            if (s == null || _hud == null || !_hud.CanvasPoint(world, out var p)) return;
            var w = _words[_next];
            _next = (_next + 1) % _words.Length;
            w.T.text = s;
            w.T.color = WordColor(depth);
            w.T.fontSize = Mathf.RoundToInt(StudioCurves.WordSizeCss(depth) * ColorHuntHud.Css);
            w.Home = p;
            w.Age = 0f;
            w.T.rectTransform.anchoredPosition = p;
            w.T.rectTransform.localScale = Vector3.one * StudioCurves.WordScale(0f);
            w.T.gameObject.SetActive(true);
            LastWord = s;
        }

        public void EdgeGlow(Color tone, int depth)
        {
            if (_edge == null) return;
            _edgeColor = tone;
            _edgePeak = StudioCurves.EdgeGlowAlpha(depth);
            _edgeAge = 0f;
            _edge.enabled = true;
            var c = tone;
            c.a = _edgePeak;
            _edge.color = c;
        }

        public void Clear()
        {
            foreach (var w in _words) { if (w == null) continue; w.Age = -1f; w.T.gameObject.SetActive(false); }
            _edgeAge = -1f;
            if (_edge != null) _edge.enabled = false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var w in _words)
            {
                if (w == null || w.Age < 0f) continue;
                w.Age += dt;
                float u = w.Age / StudioCurves.WordTime;
                if (u >= 1f) { w.Age = -1f; w.T.gameObject.SetActive(false); continue; }
                w.T.rectTransform.anchoredPosition = w.Home + new Vector2(0f, StudioCurves.WordRiseCss * ColorHuntHud.Css * u);
                w.T.rectTransform.localScale = Vector3.one * StudioCurves.WordScale(u);
                var c = w.T.color;
                c.a = StudioCurves.WordAlpha(u);
                w.T.color = c;
            }
            if (_edgeAge < 0f) return;
            _edgeAge += dt;
            float e = _edgeAge / StudioCurves.EdgeTime;
            if (e >= 1f) { _edgeAge = -1f; _edge.enabled = false; return; }
            var ec = _edgeColor;
            ec.a = _edgePeak * (1f - e) * (1f - e);
            _edge.color = ec;
        }
    }
}
