using System.Collections.Generic;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #harita 1:1 (style guide §3): the city ground, a "Harita" ribbon under a black fade, 50 nodes on the snake path with
    /// white dots between them, passed nodes green with a medal, the current one orange and breathing (Pota nabiz), locked ones
    /// grey with a lock, the "Sen" pin beside the current node. <see cref="Play"/> runs the M6 progression from a <see cref="MapProgress"/>.
    /// Scrolls by drag (ScrollRect); the progression drives the scroll itself.
    /// </summary>
    public class MapScreen : MonoBehaviour
    {
        public enum NodeState { Locked, Current, Passed }

        private class Node
        {
            public int N; public NodeState State; public int MedalIndex;
            public Vector2 Pos; public RectTransform Rt; public Image Face, FaceTo, Medal; public Text Number; public RectTransform Lock;
        }

        public const float Width = 390f, NodeCss = 72f, PinCss = 58f, HeaderCss = 56f, Centre = 0.55f, PulsePeriod = 1.6f;

        private MetaSettings _s;
        private RectTransform _viewport, _content, _pin, _pinBody, _ring;
        private ScrollRect _scroll;
        private Node[] _nodes;
        private Image[][] _dots;                       // per segment n → n+1
        private Image[] _stars;
        private Sprite _faceGreen, _faceOrange, _faceGrey, _dotSprite, _softSprite, _lipSprite, _shackleSprite, _bodySprite;
        private int _level, _last;
        private float _t, _centre = -1f;
        private bool _centreDirty;
        // progression
        private MapProgress _progress;
        private float _progT;
        private int _from, _to, _medal;
        private Vector2 _pinFrom, _pinTo;

        public bool Progressing => _progress != null;
        public MapProgress Progress => _progress;
        public RectTransform Content => _content;

        public static MapScreen Create(RectTransform page, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("MapScreen", typeof(RectTransform));
            go.transform.SetParent(page, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var m = go.AddComponent<MapScreen>();
            m._s = s; m._last = s.MapLevels;
            m._faceGreen = MetaStyle.Circle(Mathf.RoundToInt(NodeCss), MetaStyle.GreenLight, MetaStyle.GreenDark, Color.white, 4);
            m._faceOrange = MetaStyle.Circle(Mathf.RoundToInt(NodeCss), MetaStyle.OrangeLight, MetaStyle.Orange2, Color.white, 4);
            m._faceGrey = MetaStyle.Circle(Mathf.RoundToInt(NodeCss), MetaStyle.Grey, MetaStyle.GreyDark, Color.white, 4);
            // one texture per part: 50 nodes and ~300 dots overlapping along the snake with distinct textures broke the canvas's depth sort (the header drew under them)
            m._dotSprite = MetaStyle.Circle(13, Color.white, Color.white, Color.white, 0);
            m._softSprite = MetaStyle.Shadow(36, 16);
            m._lipSprite = MetaStyle.Circle(Mathf.RoundToInt(NodeCss), new Color(0f, 0f, 0f, 0.28f), new Color(0f, 0f, 0f, 0.28f), Color.clear, 0);
            m._shackleSprite = MetaStyle.Ring(14, Color.white, 3);
            m._bodySprite = MetaStyle.Lip(3, Color.white);
            // viewport (between the header and the tab bar) + content
            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            vp.transform.SetParent(rt, false);
            m._viewport = (RectTransform)vp.transform;
            m._viewport.anchorMin = Vector2.zero; m._viewport.anchorMax = Vector2.one;
            m._viewport.offsetMin = new Vector2(0f, (TabBar.HeightCss + 14f) * css); m._viewport.offsetMax = new Vector2(0f, 0f);
            vp.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(m._viewport, false);
            m._content = (RectTransform)content.transform;
            m._content.anchorMin = new Vector2(0f, 1f); m._content.anchorMax = new Vector2(1f, 1f); m._content.pivot = new Vector2(0.5f, 1f);
            m._content.sizeDelta = new Vector2(0f, MapLayout.ContentHeight(m._last) * css);
            m._content.anchoredPosition = Vector2.zero;
            content.AddComponent<Canvas>();                 // nested canvas: the path is one block in the parent's draw order, under the header
            m._scroll = go.AddComponent<ScrollRect>();
            m._scroll.viewport = m._viewport; m._scroll.content = m._content; m._scroll.horizontal = false; m._scroll.vertical = true;
            m._scroll.movementType = ScrollRect.MovementType.Clamped; m._scroll.inertia = true; m._scroll.scrollSensitivity = 30f;
            // dots, then nodes (nodes over dots)
            m._dots = new Image[m._last][];
            for (int n = 1; n < m._last; n++)
            {
                var pts = MapLayout.Dots(MapLayout.Node(n, Width, m._last), MapLayout.Node(n + 1, Width, m._last));
                m._dots[n] = new Image[pts.Length];
                for (int i = 0; i < pts.Length; i++) m._dots[n][i] = m.Dot(pts[i]);
            }
            m._nodes = new Node[m._last + 1];
            for (int n = 1; n <= m._last; n++) m._nodes[n] = m.MakeNode(n);
            // unlock burst (ring + stars) under the pin
            m._ring = m.Burst();
            m._pin = m.MakePin();
            // header: black fade + ribbon
            var fade = new GameObject("TopFade", typeof(RectTransform), typeof(Image));
            fade.transform.SetParent(rt, false);
            var frt = (RectTransform)fade.transform; frt.anchorMin = new Vector2(0f, 1f); frt.anchorMax = new Vector2(1f, 1f); frt.pivot = new Vector2(0.5f, 1f);
            frt.sizeDelta = new Vector2(0f, HeaderCss * 1.6f * css);
            var fimg = fade.GetComponent<Image>(); fimg.sprite = ColorHuntHud.PanelSprite(64, 1, new Color(0f, 0f, 0f, 0.8f), new Color(0f, 0f, 0f, 0f), Color.clear, 0, 0f, 0);
            fimg.type = Image.Type.Simple; fimg.raycastTarget = false;        // sliced, the stretched centre of this 64 px gradient drew nothing
            var ribbon = new GameObject("Ribbon", typeof(RectTransform), typeof(Image));
            ribbon.transform.SetParent(rt, false);
            var rrt = (RectTransform)ribbon.transform; rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f); rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -16f * css); rrt.sizeDelta = new Vector2(200f, 48f) * css;
            var rimg = ribbon.GetComponent<Image>(); rimg.sprite = MetaStyle.Ribbon(); rimg.type = Image.Type.Sliced; rimg.raycastTarget = false;
            MetaUiKit.Label(rrt, "Title", s.Lilita, "Harita", 26f, Color.white);
            return m;
        }

        private Image Dot(Vector2 p)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image), typeof(Shadow));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((p.x - Width / 2f) * css, -p.y * css);
            rt.sizeDelta = Vector2.one * (13f * css);
            var img = go.GetComponent<Image>(); img.sprite = _dotSprite; img.raycastTarget = false;
            var sh = go.GetComponent<Shadow>(); sh.effectColor = new Color(20f / 255f, 40f / 255f, 80f / 255f, 0.45f); sh.effectDistance = new Vector2(0f, -3f * css);
            return img;
        }

        private Node MakeNode(int n)
        {
            float css = MetaStyle.Css;
            var node = new Node { N = n, Pos = MapLayout.Node(n, Width, _last) };
            var go = new GameObject("Node" + n, typeof(RectTransform));
            go.transform.SetParent(_content, false);
            node.Rt = (RectTransform)go.transform;
            node.Rt.anchorMin = node.Rt.anchorMax = new Vector2(0.5f, 1f); node.Rt.pivot = new Vector2(0.5f, 0.5f);
            node.Rt.anchoredPosition = new Vector2((node.Pos.x - Width / 2f) * css, -node.Pos.y * css);
            node.Rt.sizeDelta = Vector2.one * (NodeCss * css);
            var soft = MetaUiKit.Picture(node.Rt, "Soft", _softSprite, NodeCss + 24f, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f));
            soft.color = new Color(0f, 0f, 0f, 0.25f); soft.preserveAspect = false;
            var lip = MetaUiKit.Picture(node.Rt, "Lip", _lipSprite, NodeCss, new Vector2(0.5f, 0.5f), new Vector2(0f, -6f));
            node.Face = MetaUiKit.Picture(node.Rt, "Face", _faceGrey, NodeCss, new Vector2(0.5f, 0.5f), Vector2.zero);
            node.FaceTo = MetaUiKit.Picture(node.Rt, "FaceTo", _faceGreen, NodeCss, new Vector2(0.5f, 0.5f), Vector2.zero);
            node.FaceTo.color = new Color(1f, 1f, 1f, 0f);
            node.Number = MetaUiKit.Label(node.Rt, "Number", _s.Lilita, n.ToString(), 27f, Color.white);
            var nsh = node.Number.gameObject.AddComponent<Shadow>(); nsh.effectColor = new Color(0f, 0f, 0f, 0.3f); nsh.effectDistance = new Vector2(0f, -2f * css);
            node.Medal = MetaUiKit.Picture(node.Rt, "Medal", null, 30f, new Vector2(1f, 0f), new Vector2(10f, -10f));
            node.Medal.gameObject.SetActive(false);
            // lock: a white body and a ring shackle
            var lockGo = new GameObject("Lock", typeof(RectTransform));
            lockGo.transform.SetParent(node.Rt, false);
            node.Lock = (RectTransform)lockGo.transform;
            node.Lock.anchorMin = node.Lock.anchorMax = node.Lock.pivot = new Vector2(0.5f, 0.5f);
            node.Lock.sizeDelta = new Vector2(22f, 22f) * css;
            var shackle = MetaUiKit.Picture(node.Lock, "Shackle", _shackleSprite, 14f, new Vector2(0.5f, 1f), new Vector2(0f, 1f));
            var body = new GameObject("Body", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(node.Lock, false);
            var brt = (RectTransform)body.transform; brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f); brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(-2f * css, 12f * css);
            var bimg = body.GetComponent<Image>(); bimg.sprite = _bodySprite; bimg.type = Image.Type.Sliced; bimg.raycastTarget = false;
            // tapping a node is Pota's replay mode (not in the showcase): only the current node opens the level window later
            return node;
        }

        private RectTransform Burst()
        {
            float css = MetaStyle.Css;
            var go = new GameObject("Burst", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * (NodeCss * css);
            var ring = MetaUiKit.Picture(rt, "Ring", MetaStyle.Ring(80, MetaStyle.Gold, 4), 80f, new Vector2(0.5f, 0.5f), Vector2.zero);
            ring.name = "Ring";
            _stars = new Image[8];
            for (int i = 0; i < 8; i++) _stars[i] = MetaUiKit.Picture(rt, "Star" + i, MetaStyle.Circle(8, MetaStyle.GoldLight, MetaStyle.Gold, MetaStyle.Gold, 0), 8f, new Vector2(0.5f, 0.5f), Vector2.zero);
            go.SetActive(false);
            return rt;
        }

        private RectTransform MakePin()
        {
            float css = MetaStyle.Css;
            var go = new GameObject("Sen", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(MapLayout.SenWidth, MapLayout.SenHeight) * css;
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(rt, false);
            _pinBody = (RectTransform)body.transform;
            _pinBody.anchorMin = _pinBody.anchorMax = new Vector2(0.5f, 0f); _pinBody.pivot = new Vector2(0.5f, 0f);
            _pinBody.sizeDelta = rt.sizeDelta;
            var sh = MetaUiKit.Picture(_pinBody, "Soft", MetaStyle.Shadow(32, 12), PinCss + 16f, new Vector2(0.5f, 1f), new Vector2(0f, -4f)); sh.color = new Color(0f, 0f, 0f, 0.3f); sh.preserveAspect = false;
            var circle = MetaUiKit.Picture(_pinBody, "Circle", MetaStyle.Circle(Mathf.RoundToInt(PinCss) + 6, MetaStyle.Gold, MetaStyle.Gold, MetaStyle.Gold, 0), PinCss + 6f, new Vector2(0.5f, 1f), Vector2.zero);
            var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(_pinBody, false);
            var mrt = (RectTransform)maskGo.transform; mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1f); mrt.pivot = new Vector2(0.5f, 1f);
            mrt.anchoredPosition = new Vector2(0f, -3f * css); mrt.sizeDelta = Vector2.one * (PinCss * css);
            var mimg = maskGo.GetComponent<Image>(); mimg.sprite = MetaStyle.Circle(Mathf.RoundToInt(PinCss), MetaStyle.Sky, MetaStyle.Sky, MetaStyle.Sky, 0);
            maskGo.GetComponent<Mask>().showMaskGraphic = true;
            var hero = new GameObject("Hero", typeof(RectTransform), typeof(Image));
            hero.transform.SetParent(mrt, false);
            var hrt = (RectTransform)hero.transform; hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
            float heroW = PinCss * 2.1f, heroH = heroW * 784f / 553f;      // the head (top ~22 % of kahraman.png) fills the circle
            hrt.anchoredPosition = new Vector2(0f, (0.22f * heroH - PinCss * 0.5f) * css); hrt.sizeDelta = new Vector2(heroW, heroH) * css;
            var himg = hero.GetComponent<Image>(); himg.sprite = _s.Hero; himg.raycastTarget = false;
            var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            pill.transform.SetParent(_pinBody, false);
            var prt = (RectTransform)pill.transform; prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f); prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = Vector2.zero; prt.sizeDelta = new Vector2(40f, 18f) * css;
            var pimg = pill.GetComponent<Image>(); pimg.sprite = MetaStyle.Pill(); pimg.type = Image.Type.Sliced; pimg.raycastTarget = false;
            MetaUiKit.Label(prt, "Text", _s.Lilita, "Sen", 11f, Color.white);
            return rt;
        }

        // ---------- state ----------

        /// <summary>The whole map from the save: nodes under <c>Level</c> passed (medals of the sample), <c>Level</c> current, the rest locked; the pin beside the current node; the scroll centred on it.</summary>
        public void Refresh(SampleSave save)
        {
            _progress = null;
            _level = Mathf.Clamp(save.Level, 1, _last);
            for (int n = 1; n <= _last; n++)
            {
                var node = _nodes[n];
                node.State = n < _level ? NodeState.Passed : n == _level ? NodeState.Current : NodeState.Locked;
                node.MedalIndex = n < _level ? SampleMedal(n) : -1;
                ApplyNode(node, 1f);
                node.Rt.localScale = Vector3.one;
                if (n < _last) foreach (var d in _dots[n]) { d.color = Color.white; d.rectTransform.localScale = Vector3.one; }
            }
            PlacePin(SenYeri(_level), 0f, 1f);
            _ring.gameObject.SetActive(false);
            _centre = _level; _centreDirty = true;
            _scroll.enabled = true;
        }

        /// <summary>The sample's medal for an already passed level (Pota keeps one per level; the showcase has no history).</summary>
        public static int SampleMedal(int n) => (n * 7 + 1) % 3;

        public NodeState StateOf(int n) => _nodes[n].State;
        public int MedalOf(int n) => _nodes[n].MedalIndex;
        /// <summary>Node centre in css px (Pota coordinates, y down).</summary>
        public Vector2 NodePosition(int n) => _nodes[n].Pos;
        public Vector2 PinPosition => new Vector2(_pin.anchoredPosition.x / MetaStyle.Css + Width / 2f, -_pin.anchoredPosition.y / MetaStyle.Css);
        public float NodeScale(int n) => _nodes[n].Rt.localScale.x;
        /// <summary>The node nearest the viewport's centre line (Pota: 55 % from the top).</summary>
        public int CentredNode
        {
            get
            {
                float line = (_content.anchoredPosition.y + _viewport.rect.height * Centre) / MetaStyle.Css;
                int best = 1; float bestD = float.MaxValue;
                for (int n = 1; n <= _last; n++) { float d = Mathf.Abs(_nodes[n].Pos.y - line); if (d < bestD) { bestD = d; best = n; } }
                return best;
            }
        }

        private Vector2 SenYeri(int n)
        {
            var k = MapLayout.Node(n, Width, _last);
            Vector2? prev = n > 1 ? MapLayout.Node(n - 1, Width, _last) : (Vector2?)null;
            Vector2? next = n < _last ? MapLayout.Node(n + 1, Width, _last) : (Vector2?)null;
            return MapLayout.SenYeri(k, prev, next, Width);
        }

        private void ApplyNode(Node node, float number)
        {
            node.Face.sprite = node.State == NodeState.Passed ? _faceGreen : node.State == NodeState.Current ? _faceOrange : _faceGrey;
            node.FaceTo.color = new Color(1f, 1f, 1f, 0f);
            node.Number.gameObject.SetActive(node.State != NodeState.Locked);
            node.Number.rectTransform.localScale = Vector3.one * number;
            node.Lock.gameObject.SetActive(node.State == NodeState.Locked);
            node.Lock.localScale = Vector3.one;
            node.Medal.gameObject.SetActive(node.State == NodeState.Passed && node.MedalIndex >= 0 && _s.Medals.Length > node.MedalIndex);
            if (node.Medal.gameObject.activeSelf) node.Medal.sprite = _s.Medals[node.MedalIndex];
            node.Medal.rectTransform.localScale = Vector3.one;
        }

        private void PlacePin(Vector2 css, float heightCss, float squash)
        {
            _pin.anchoredPosition = new Vector2((css.x - Width / 2f) * MetaStyle.Css, -(css.y - heightCss) * MetaStyle.Css);
            _pinBody.localScale = new Vector3(1f / squash, squash, 1f);
        }

        /// <summary>Scrolls so node <paramref name="n"/> (fractional between nodes) sits at 55 % of the viewport; clamped to the content.</summary>
        private void Scroll(float n)
        {
            float vh = _viewport.rect.height;
            if (vh <= 0f) { _centreDirty = true; return; }
            float lo = Mathf.FloorToInt(n), frac = n - lo;
            float y = Mathf.Lerp(MapLayout.Node(Mathf.Clamp((int)lo, 1, _last), Width, _last).y, MapLayout.Node(Mathf.Clamp((int)lo + 1, 1, _last), Width, _last).y, frac);
            float offset = Mathf.Clamp(y * MetaStyle.Css - vh * Centre, 0f, Mathf.Max(0f, _content.rect.height - vh));
            _content.anchoredPosition = new Vector2(0f, offset);
            _centreDirty = false;
        }

        // ---------- progression (M6) ----------

        /// <summary>After a win: node <paramref name="from"/> turns green with <paramref name="medal"/>, the pin hops to <paramref name="to"/>, which unlocks.</summary>
        public void Play(int from, int to, int medal)
        {
            from = Mathf.Clamp(from, 1, _last); to = Mathf.Clamp(to, 1, _last);
            _from = from; _to = to; _medal = medal;
            _nodes[from].State = NodeState.Current; _nodes[from].MedalIndex = -1; ApplyNode(_nodes[from], 1f);
            _nodes[to].State = NodeState.Locked; _nodes[to].MedalIndex = -1; ApplyNode(_nodes[to], 1f);
            _nodes[from].FaceTo.sprite = _faceGreen; _nodes[to].FaceTo.sprite = _faceOrange;
            _pinFrom = SenYeri(from); _pinTo = SenYeri(to);
            int dots = from < _last && to == from + 1 ? _dots[from].Length : 0;
            _progress = new MapProgress(_s.Feel, dots);
            _progT = 0f;
            _ring.anchoredPosition = _nodes[to].Rt.anchoredPosition;
            _scroll.enabled = false;
            Apply(_progress.Stage(0f));
        }

        private void Apply(MapProgress.Frame f)
        {
            float css = MetaStyle.Css;
            var a = _nodes[_from]; var b = _nodes[_to];
            Scroll(_from - 1 + f.Scroll);
            // the passed node
            a.FaceTo.color = new Color(1f, 1f, 1f, f.Green);
            a.Rt.localScale = Vector3.one * f.GreenScale;
            if (f.Green >= 1f && a.State != NodeState.Passed) { a.State = NodeState.Passed; a.MedalIndex = _medal; ApplyNode(a, 1f); }
            bool medalUp = f.Medal > 0f && _s.Medals.Length > _medal && _medal >= 0;
            a.Medal.gameObject.SetActive(medalUp);
            if (medalUp) { a.Medal.sprite = _s.Medals[_medal]; a.Medal.rectTransform.localScale = Vector3.one * f.Medal; }
            // the path
            if (_from < _last && _to == _from + 1)
            {
                var dots = _dots[_from];
                for (int i = 0; i < dots.Length; i++)
                {
                    bool lit = i < f.DotsLit;
                    dots[i].color = lit ? MetaStyle.Gold : Color.white;
                    dots[i].rectTransform.localScale = Vector3.one * (lit && i == f.DotsLit - 1 && f.DotsLit < dots.Length ? 1.4f : 1f);
                }
            }
            // the pin
            PlacePin(Vector2.Lerp(_pinFrom, _pinTo, f.Pin), f.PinHeight, f.PinSquash);
            // the new node
            b.Lock.localScale = Vector3.one * f.Lock;
            b.Lock.gameObject.SetActive(f.Lock > 0f);
            b.FaceTo.color = new Color(1f, 1f, 1f, f.Unlocked ? 1f : 0f);
            b.Number.gameObject.SetActive(f.Number > 0f);
            b.Number.rectTransform.localScale = Vector3.one * f.Number;
            if (f.Unlocked && b.State != NodeState.Current) { b.State = NodeState.Current; ApplyNode(b, f.Number); b.Number.gameObject.SetActive(f.Number > 0f); }
            _ring.gameObject.SetActive(f.Ring > 0f);
            if (f.Ring > 0f)
            {
                var ring = _ring.GetChild(0) as RectTransform;
                ring.localScale = Vector3.one * StudioCurves.RingScale(f.Ring);
                ring.GetComponent<Image>().color = new Color(1f, 1f, 1f, StudioCurves.RingAlpha(f.Ring));
                for (int i = 0; i < _stars.Length; i++)
                {
                    float ang = i / 8f * Mathf.PI * 2f, r = Mathf.Lerp(20f, 70f, StudioCurves.EaseOutCubic(f.Stars)) * css;
                    _stars[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                    _stars[i].color = new Color(1f, 1f, 1f, 1f - f.Stars);
                }
            }
            if (_progT >= _progress.Done)
            {
                _progress = null;
                a.Rt.localScale = Vector3.one; a.FaceTo.color = new Color(1f, 1f, 1f, 0f);
                b.FaceTo.color = new Color(1f, 1f, 1f, 0f);
                ApplyNode(a, 1f); ApplyNode(b, 1f);
                _level = _to; _centre = _to; _centreDirty = false;
                _scroll.enabled = true;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            if (_progress != null)
            {
                _progT += dt;
                Apply(_progress.Stage(_progT));
            }
            else
            {
                if (_centreDirty) Scroll(_centre);
                // Pota nabiz on the current node
                var cur = _nodes[Mathf.Clamp(_level, 1, _last)];
                if (cur.State == NodeState.Current) cur.Rt.localScale = Vector3.one * (1f + 0.05f * (0.5f - 0.5f * Mathf.Cos(_t / PulsePeriod * 2f * Mathf.PI)));
            }
        }
    }
}
