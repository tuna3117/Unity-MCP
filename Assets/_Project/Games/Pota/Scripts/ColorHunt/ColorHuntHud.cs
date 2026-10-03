using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's puzzle HUD (index.html #hud2 / #yAlt, renkAviFaz.ts): navy gradient counters with a gold border and lip — CEPHANE
    /// top-left, HEDEF top-right (sticker + count, green ✓ when done) — the rotate arrows stacked on the right 34 px under the
    /// backboard, stickers flying from a burst block to their box (the count drops as each lands), the result overlay with retry.
    /// Built in code; sizes are Pota's CSS px on a 390-px-wide phone, <see cref="Css"/> canvas units each.
    /// </summary>
    public class ColorHuntHud : MonoBehaviour
    {
        /// <summary>Canvas units per Pota CSS px: the canvas is 1080 wide (width-matched), Pota's phone 390.</summary>
        public const float Css = 1080f / PotaLook.PhoneCssWidth;

        public ColorHuntGame Game;
        public ColorHuntSettings S;
        /// <summary>When set, the result panel's button calls this instead of dealing a new board (PotaFlow flies back to the basket).</summary>
        public Action RetryOverride;
        [Tooltip("Show the how-to-play hint (PotaFlow hides it: the giant hoop's net sits there).")]
        public bool ShowHint = true;

        // Serialized: the HUD is built once by the scene tool in Edit Mode and must survive scene load / Play Mode.
        [SerializeField] private Text _ammo;
        [SerializeField] private Text _hint;
        [SerializeField] private Image _hintBack;
        [SerializeField] private Text _resultTitle;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private RectTransform _targets;
        [SerializeField] private RectTransform _ammoPanel, _targetPanel, _right, _left, _flyers;
        [SerializeField] private Font _font;

        private class TargetRow { public int Color; public Image Icon; public Text Count, Tick; }
        private class Flyer { public RectTransform T; public Image Img; public Vector2 From, To; public float T0; public Action Done; public TargetRow Row; }

        private readonly List<TargetRow> _rows = new List<TargetRow>();
        private readonly Dictionary<Transform, float> _pops = new Dictionary<Transform, float>();
        private readonly List<Flyer> _flying = new List<Flyer>();
        private readonly Stack<Flyer> _flyerPool = new Stack<Flyer>();
        private float _introAmmo = 1f, _introRest = 1f;
        private Color _hintColor;
        private bool _hintCached;

        /// <summary>The studio sticker flight (looks other than Pota).</summary>
        public CollectFlight Collect { get; private set; }
        public AmmoCounter AmmoCounter { get; private set; }
        public AmmoTension Tension { get; private set; }
        public LevelIntro Intro { get; private set; }
        public LeftoverFinale Finale { get; private set; }
        public int FlyingCount => _flying.Count + (Collect != null ? Collect.Flying : 0);

        internal RectTransform FlyersRoot => _flyers;
        internal RectTransform TargetPanel => _targetPanel;
        internal RectTransform AmmoPanel => _ammoPanel;
        internal Text AmmoText => _ammo;
        internal Font HudFont => _font;

        /// <summary>A world point on the canvas (centre origin, canvas units); false when behind the camera.</summary>
        internal bool CanvasPoint(Vector3 world, out Vector2 point)
        {
            point = Vector2.zero;
            var cam = Game != null ? Game.Cam : null;
            if (cam == null) return false;
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0f) return false;
            var rect = ((RectTransform)transform).rect;
            point = new Vector2((vp.x - 0.5f) * rect.width, (vp.y - 0.5f) * rect.height);
            return true;
        }

        internal Vector2 CanvasPointOf(RectTransform t) =>
            ((RectTransform)transform).InverseTransformPoint(t.TransformPoint(t.rect.center));

        internal bool TryTarget(int color, out Image icon, out Text count, out Text tick)
        {
            foreach (var r in _rows)
                if (r.Color == color) { icon = r.Icon; count = r.Count; tick = r.Tick; return true; }
            icon = null; count = null; tick = null;
            return false;
        }

        public void Bind(ColorHuntGame game)
        {
            Game = game;
            game.AmmoChanged += OnAmmo;
            game.TargetsChanged += OnTargets;
            game.RoundEnded += OnEnded;
            game.RoundStarted += OnStarted;
            WireButtons();
            Collect = GetComponent<CollectFlight>();
            if (Collect == null) Collect = gameObject.AddComponent<CollectFlight>();
            Collect.Init(this);
            AmmoCounter = GetComponent<AmmoCounter>();
            if (AmmoCounter == null) AmmoCounter = gameObject.AddComponent<AmmoCounter>();
            AmmoCounter.Init(this);
            Tension = GetComponent<AmmoTension>();
            if (Tension == null) Tension = gameObject.AddComponent<AmmoTension>();
            Tension.Init(this);
            Intro = GetComponent<LevelIntro>();
            if (Intro == null) Intro = gameObject.AddComponent<LevelIntro>();
            Intro.Init(this);
            Finale = GetComponent<LeftoverFinale>();
            if (Finale == null) Finale = gameObject.AddComponent<LeftoverFinale>();
            Finale.Init(this, game);
            game.Finale = Finale;
        }

        /// <summary>A .sayac-style counter panel (face, lip, soft shadow) for the feel layer's badges.</summary>
        internal RectTransform NewCounter(string name, Vector2 anchor, Vector2 sizeCss)
        {
            var face = PanelSprite(128, Mathf.RoundToInt(18f * Css), PotaLook.HudPanelTop, PotaLook.HudNavy, PotaLook.HudGold, Mathf.RoundToInt(3f * Css), 0.18f, Mathf.RoundToInt(2f * Css));
            var lip = PanelSprite(128, Mathf.RoundToInt(18f * Css), PotaLook.HudGold2, PotaLook.HudGold2, PotaLook.HudGold2, 0, 0f, 0);
            var soft = ShadowSprite(128, Mathf.RoundToInt(18f * Css), Mathf.RoundToInt(12f * Css));
            return Counter(transform, name, anchor, sizeCss, face, lip, soft);
        }

        /// <summary>A HUD text in the HUD font (sizes in CSS px), centred under <paramref name="anchor"/>.</summary>
        internal Text MakeLabel(RectTransform parent, string name, float sizeCss, Vector2 anchor, Vector2 pos, Vector2 size, Color color, string text) =>
            MakeText(parent, name, sizeCss, anchor, new Vector2(0.5f, 0.5f), pos, size, TextAnchor.MiddleCenter, color, text);

        /// <summary>F1: a target row's icon, count and tick are hidden until its goal chip arrives (the tick only when the goal is done).</summary>
        internal void SetRowShown(int color, bool on)
        {
            foreach (var r in _rows)
            {
                if (r.Color != color) continue;
                r.Icon.enabled = on && r.Icon.sprite != null;
                r.Count.enabled = on;
                r.Tick.enabled = on && Game != null && Game.ShownTarget(color) == 0;
            }
        }

        /// <summary>index.html @keyframes pop on a target row's icon and count.</summary>
        internal void PopRow(int color)
        {
            foreach (var r in _rows)
                if (r.Color == color) { _pops[r.Icon.transform] = 0f; _pops[r.Count.transform] = 0f; }
        }

        // Listeners added while the scene tool builds the HUD in Edit Mode are not saved with the scene: wire them here.
        private void WireButtons()
        {
            Wire("RotateLeft", () => { if (Game != null) Game.Rotate(Rotation.Counter); });
            Wire("RotateRight", () => { if (Game != null) Game.Rotate(Rotation.Clockwise); });
            Wire("ResultPanel/Card/Retry", Retry);
        }

        private void Wire(string path, UnityEngine.Events.UnityAction action)
        {
            var t = transform.Find(path);
            var button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) { Debug.LogWarning($"[ColorHuntHud] no button at {path}"); return; }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void Retry()
        {
            if (RetryOverride != null) { RetryOverride(); return; }
            if (Game != null) Game.NewRound();
        }

        public void SetRetryLabel(string text)
        {
            var t = transform.Find("ResultPanel/Card/Retry/Label");
            if (t != null) t.GetComponent<Text>().text = text;
        }

        public void HideResult()
        {
            if (_resultPanel != null) _resultPanel.SetActive(false);
        }

        /// <summary>Slides the HUD in (PotaFlow): p = 0 panels off-screen and hint hidden, p = 1 exactly home. Ease-out.</summary>
        public void SetIntro(float p) => SetIntro(p, p);

        /// <summary>Slides the ammo panel by <paramref name="ammo"/> and the rest (targets, arrows, hint) by <paramref name="rest"/>.</summary>
        public void SetIntro(float ammo, float rest)
        {
            _introAmmo = ammo;
            _introRest = rest;
            Layout();
        }

        private static float EaseOut(float p)
        {
            p = Mathf.Clamp01(p);
            return 1f - (1f - p) * (1f - p) * (1f - p);
        }

        /// <summary>
        /// Counters under the safe area 14 px in from the sides (#hud2), arrows on the right 34 px under the backboard but never lower than
        /// 150 px above the bottom (#yAlt), hint in the middle column; plus the intro offsets.
        /// </summary>
        private void Layout()
        {
            if (_ammoPanel == null) return;
            float top = Mathf.Max(PotaLook.HudTopPadCss, ColorHuntCamera.SafeTopCss()) * Css;
            float ea = EaseOut(_introAmmo), er = EaseOut(_introRest);
            _ammoPanel.anchoredPosition = new Vector2(14f * Css - 420f * (1f - ea), -top);
            _targetPanel.anchoredPosition = new Vector2(-14f * Css, -top + 320f * (1f - er));
            // Fully out (er = 0): far below the screen, whatever the flight camera projects the backboard to (it used to peek in at the top).
            float arrows = ArrowsTopCss() * Css, drop = 340f * (1f - er) + (er <= 0f ? 4000f : 0f);
            _right.anchoredPosition = new Vector2(-14f * Css, -arrows - drop);
            _left.anchoredPosition = new Vector2(-14f * Css, -arrows - 72f * Css - drop);
            if (_hint == null) return;
            if (!_hintCached) { _hintColor = _hint.color; _hintCached = true; }
            _hint.rectTransform.anchoredPosition = new Vector2(0f, -arrows - 6f * Css);
            var c = _hintColor;
            c.a *= ShowHint ? er : 0f;
            _hint.color = c;
            if (_hintBack == null) return;
            _hintBack.rectTransform.anchoredPosition = _hint.rectTransform.anchoredPosition + new Vector2(0f, 5f * Css);
            _hintBack.enabled = _hint.enabled;
            _hintBack.color = new Color(1f, 0.973f, 0.91f, 0.86f * (ShowHint ? er : 0f));
        }

        /// <summary>index.html #yAlt top: min(backboard's outer bottom on screen + 34, screen − 10 − 140), CSS px.</summary>
        private float ArrowsTopCss()
        {
            float cssH = ((RectTransform)transform).rect.height / Css;
            float limit = cssH - 10f - 140f;
            var cam = Game != null ? Game.Cam : null;
            if (cam == null) return limit;
            var world = Game.transform.TransformPoint(new Vector3(0f, -(PotaLook.PanoSize * 0.5f + PotaLook.FrameWidth), -PotaLook.FrontZ));
            float vy;
            var fit = cam.GetComponent<ColorHuntCamera>();
            if (fit != null && fit.enabled)
            {
                // The camera's rest pose, not the shaken one: Pota places the buttons per layout (dugmeleriYerlestir), they never shake.
                var eye = fit.FitFor(cam.fieldOfView, cam.aspect);
                float depth = world.z - eye.z;
                if (depth <= 0f) return limit;
                vy = 0.5f + (world.y - eye.y) / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth);
            }
            else
            {
                var vp = cam.WorldToViewportPoint(world);          // PotaFlow's flight: the live camera
                if (vp.z <= 0f) return limit;
                vy = vp.y;
            }
            return Mathf.Min((1f - vy) * cssH + PotaLook.ButtonGapCss, limit);
        }

        /// <summary>The ammo number's position as a 0..1 fraction of the HUD canvas (bottom-left origin), for PotaFlow's badge.</summary>
        public Vector2 AmmoViewportPoint
        {
            get
            {
                if (_ammo == null) return new Vector2(0.15f, 0.93f);
                var canvasRt = (RectTransform)transform;
                Vector2 local = canvasRt.InverseTransformPoint(_ammo.rectTransform.position);
                var r = canvasRt.rect;
                return new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            }
        }

        /// <summary>index.html @keyframes pop on the ammo number (transition arrival).</summary>
        public void PopAmmo()
        {
            if (_ammo != null) _pops[_ammo.transform] = 0f;
        }

        public string TargetText(int color)
        {
            foreach (var row in _rows) if (row.Color == color) return row.Count.text;
            return null;
        }

        /// <summary>
        /// renkAviFaz.ts esyaUcur: the colour's sticker (56 px) flies from the block's screen point to its target icon in
        /// <see cref="ColorHuntSettings.ItemFlyTime"/> of real time; on arrival the icon and the count pop and <paramref name="onArrive"/> runs.
        /// False (nothing flies) when the colour has no box or the block is behind the camera.
        /// </summary>
        public bool FlyItem(int color, Vector3 world, Action onArrive)
        {
            if (Game != null && Game.StudioFlight && Collect != null) return Collect.Fly(color, world, onArrive);
            TargetRow row = null;
            foreach (var r in _rows) if (r.Color == color) row = r;
            if (row == null || row.Icon.sprite == null || _flyers == null) return false;
            if (!CanvasPoint(world, out var from)) return false;
            var f = _flyerPool.Count > 0 ? _flyerPool.Pop() : NewFlyer();
            f.From = from;
            f.To = CanvasPointOf(row.Icon.rectTransform);
            f.T0 = Time.unscaledTime;
            f.Done = onArrive;
            f.Row = row;
            f.Img.sprite = row.Icon.sprite;
            f.T.anchoredPosition = f.From;
            f.T.localScale = Vector3.one * 0.9f;
            f.T.gameObject.SetActive(true);
            _flying.Add(f);
            return true;
        }

        private Flyer NewFlyer()
        {
            var go = new GameObject("Sticker", typeof(RectTransform), typeof(Image), typeof(Shadow));
            go.transform.SetParent(_flyers, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * (56f * Css);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            var sh = go.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.4f);
            sh.effectDistance = new Vector2(0f, -3f * Css);
            return new Flyer { T = rt, Img = img };
        }

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<ColorHuntGame>();
            if (Game != null && Game.Board != null) OnStarted();
        }

        private void OnStarted()
        {
            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_hint != null) _hint.enabled = true;
            foreach (var f in _flying) { f.T.gameObject.SetActive(false); _flyerPool.Push(f); }
            _flying.Clear();
            Collect?.ClearAll();
            BuildTargets();
            OnAmmo();
        }

        private void OnAmmo()
        {
            if (Game == null || _ammo == null) return;
            string s = Game.DisplayedAmmo.ToString();
            if (_ammo.text != s) _ammo.text = s;
        }

        private void OnTargets()
        {
            if (Game == null || Game.Board == null) return;
            bool studio = Game.StudioFlight && Collect != null;
            foreach (var row in _rows)
            {
                int shown = Game.ShownTarget(row.Color);
                string s = shown.ToString();
                bool done = shown == 0;
                string old = row.Count.text;
                if (old != s)
                {
                    row.Count.text = s;
                    if (studio) Collect.CountChanged(row.Color, old, done);
                    else _pops[row.Count.transform] = 0f;
                }
                row.Count.color = done ? PotaLook.HudGreen : PotaLook.HudGold;
                bool stamped = done && !row.Tick.enabled;
                row.Tick.enabled = done && row.Count.enabled;              // a row hidden by the level intro stays hidden
                if (stamped && studio) Collect.StampTick(row.Color);
            }
        }

        private void OnEnded(bool won, string reason)
        {
            if (_resultTitle != null)
            {
                _resultTitle.text = won ? "Hedef tamam!" : reason == "ammo" ? "Top bitti" : "Hamle kalmadı";
                _resultTitle.color = won ? PotaLook.HudGreen : PotaLook.HudRed;
            }
            if (_resultPanel != null) _resultPanel.SetActive(true);
        }

        private void Update()
        {
            if (Game != null && Game.Busy && !Game.Intro && _hint != null) _hint.enabled = false;   // the card waits for the first tap, not the intro
            float dt = Time.unscaledDeltaTime;
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                float t = (Time.unscaledTime - f.T0) / Mathf.Max(0.01f, S.ItemFlyTime);
                if (t >= 1f)
                {
                    f.T.gameObject.SetActive(false);
                    _flying.RemoveAt(i);
                    _flyerPool.Push(f);
                    _pops[f.Row.Icon.transform] = 0f;
                    f.Done?.Invoke();
                    continue;
                }
                var pose = PotaLook.ItemFlight(t);
                f.T.anchoredPosition = Vector2.Lerp(f.From, f.To, pose.Along) + new Vector2(0f, pose.Lift * Css);
                f.T.localScale = Vector3.one * pose.Scale;
            }
            if (_pops.Count == 0) return;
            var keys = new List<Transform>(_pops.Keys);
            foreach (var t in keys)
            {
                if (t == null) { _pops.Remove(t); continue; }                     // a new round rebuilt the rows under a running pop
                float p = _pops[t] + dt / 0.18f;                                   // @keyframes pop: 0.18 s, 1.25 at 40 %
                float k = p < 0.4f ? p / 0.4f : Mathf.Max(0f, (1f - p) / 0.6f);
                t.localScale = Vector3.one * (1f + 0.25f * k);
                if (p >= 1f) { t.localScale = Vector3.one; _pops.Remove(t); } else _pops[t] = p;
            }
        }

        private void LateUpdate() => Layout();

        /// <summary>#yHedefler: sticker 30 px + 5 + count 26 px (min 30), 10 px between targets; the panel is at least 120 wide.</summary>
        private void BuildTargets()
        {
            _rows.Clear();
            if (_targets == null || Game == null || Game.Board == null) return;
            foreach (Transform c in _targets) Destroy(c.gameObject);
            float x = 0f;
            foreach (var t in Game.Board.Targets)
            {
                var row = new TargetRow { Color = t.Color };
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Shadow));
                iconGo.transform.SetParent(_targets, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2((x + 15f) * Css, 0f);
                irt.sizeDelta = Vector2.one * (30f * Css);
                row.Icon = iconGo.GetComponent<Image>();
                row.Icon.preserveAspect = true;
                row.Icon.raycastTarget = false;
                var tex = S.ItemIcon(t.Color);
                if (tex != null) row.Icon.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                var sh = iconGo.GetComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
                sh.effectDistance = new Vector2(0f, -2f * Css);
                row.Count = MakeText(_targets, "Count", 26f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2((x + 35f) * Css, 0f),
                    new Vector2(40f * Css, 30f * Css), TextAnchor.MiddleLeft, PotaLook.HudGold, Game.ShownTarget(t.Color).ToString());
                row.Tick = MakeText(_targets, "Tick", 18f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2((x + 35f + 30f) * Css, 0f),
                    new Vector2(20f * Css, 30f * Css), TextAnchor.MiddleLeft, PotaLook.HudGreen, "✓");
                row.Tick.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");       // Lilita One has no ✓
                row.Tick.enabled = false;
                x += 65f + 10f;
                _rows.Add(row);
            }
            float content = Mathf.Max(120f, x - 10f);
            _targets.sizeDelta = new Vector2(content * Css, 32f * Css);
            _targetPanel.sizeDelta = new Vector2((content + 28f + 6f) * Css, 63.2f * Css);
            OnTargets();
        }

        // ---------- construction ----------

        public static ColorHuntHud Create(Transform parent, ColorHuntSettings s)
        {
            var font = s.Font != null ? s.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("ColorHuntHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;                     // Pota's CSS is width-bound
            var hud = canvasGo.AddComponent<ColorHuntHud>();
            hud.S = s;
            hud._font = font;
            var root = canvasGo.transform;

            // .sayac: gradient #24407a → #142850, 3 px gold border, radius 18, inset top highlight, 3 px #b3600a lip, soft shadow.
            var face = PanelSprite(128, Mathf.RoundToInt(18f * Css), PotaLook.HudPanelTop, PotaLook.HudNavy, PotaLook.HudGold, Mathf.RoundToInt(3f * Css), 0.18f, Mathf.RoundToInt(2f * Css));
            var lip = PanelSprite(128, Mathf.RoundToInt(18f * Css), PotaLook.HudGold2, PotaLook.HudGold2, PotaLook.HudGold2, 0, 0f, 0);
            var soft = ShadowSprite(128, Mathf.RoundToInt(18f * Css), Mathf.RoundToInt(12f * Css));

            hud._ammoPanel = Counter(root, "AmmoPanel", new Vector2(0f, 1f), new Vector2(98f, 63f), face, lip, soft);
            hud.MakeText(hud._ammoPanel, "Label", 11f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15.6f * Css),
                new Vector2(90f * Css, 14f * Css), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f), "CEPHANE");
            hud._ammo = hud.MakeText(hud._ammoPanel, "Ammo", 30f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -37.95f * Css),
                new Vector2(90f * Css, 32f * Css), TextAnchor.MiddleCenter, PotaLook.HudGold, "0");

            hud._targetPanel = Counter(root, "TargetPanel", new Vector2(1f, 1f), new Vector2(154f, 63.2f), face, lip, soft);
            hud.MakeText(hud._targetPanel, "Label", 11f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15.6f * Css),
                new Vector2(140f * Css, 14f * Css), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f), "HEDEF");
            var targetsGo = new GameObject("Targets", typeof(RectTransform));
            targetsGo.transform.SetParent(hud._targetPanel, false);
            hud._targets = (RectTransform)targetsGo.transform;
            hud._targets.anchorMin = hud._targets.anchorMax = new Vector2(0.5f, 1f);
            hud._targets.pivot = new Vector2(0.5f, 0.5f);
            hud._targets.anchoredPosition = new Vector2(0f, -39.2f * Css);
            hud._targets.sizeDelta = new Vector2(120f * Css, 32f * Css);

            // .dondur: 64 px circle, 4 px gold border, gradient #2b4d91 → #1f3a6e, 5 px #b3600a lip; pressed: #ffb46b → #ff7a1a.
            int d = Mathf.RoundToInt(64f * Css), bw = Mathf.RoundToInt(4f * Css);
            var circle = CircleSprite(d, PotaLook.HudButtonTop, PotaLook.HudNavy2, PotaLook.HudGold, bw);
            var pressed = CircleSprite(d, PotaLook.HudOrangeTop, PotaLook.HudOrange, PotaLook.HudGold, bw);
            var circleLip = CircleSprite(d, PotaLook.HudGold2, PotaLook.HudGold2, PotaLook.HudGold2, 0);
            hud._right = hud.ArrowButton(root, "RotateRight", circle, pressed, circleLip, s, false);
            hud._left = hud.ArrowButton(root, "RotateLeft", circle, pressed, circleLip, s, true);

            // #yIpucu: 13 px bold #103055 on a cream card (rgba(255,248,232,.86), radius 12, padding 5 × 11) in the middle column.
            var back = new GameObject("HintBack", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(root, false);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(232f * Css, 46f * Css);
            hud._hintBack = back.GetComponent<Image>();
            hud._hintBack.sprite = PanelSprite(128, Mathf.RoundToInt(12f * Css), Color.white, Color.white, Color.white, 0, 0f, 0);
            hud._hintBack.type = Image.Type.Sliced;
            hud._hintBack.raycastTarget = false;
            hud._hint = hud.MakeText(root, "Hint", 13f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(210f * Css, 40f * Css),
                TextAnchor.UpperCenter, PotaLook.HudHint, "Bloğa dokun: canı kadar top harcanır.\nOklar tahtayı çevirir, bedava.");
            hud._hint.horizontalOverflow = HorizontalWrapMode.Wrap;

            var flyers = new GameObject("Flyers", typeof(RectTransform));
            flyers.transform.SetParent(root, false);
            hud._flyers = (RectTransform)flyers.transform;
            hud._flyers.anchorMin = Vector2.zero;
            hud._flyers.anchorMax = Vector2.one;
            hud._flyers.offsetMin = hud._flyers.offsetMax = Vector2.zero;

            // Result overlay (Pota's result screens are a later step): dim + a counter-style card with the retry button.
            var overlay = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(root, false);
            var ort = (RectTransform)overlay.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = ort.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var card = Counter(overlay.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(260f, 150f), face, lip, soft);
            hud._resultTitle = hud.MakeText(card, "Title", 28f, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f * Css),
                new Vector2(250f * Css, 40f * Css), TextAnchor.MiddleCenter, PotaLook.HudGold, "");
            var retry = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button));
            retry.transform.SetParent(card, false);
            var rrt = (RectTransform)retry.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 40f * Css);
            rrt.sizeDelta = new Vector2(150f * Css, 44f * Css);
            var rimg = retry.GetComponent<Image>();
            rimg.sprite = PanelSprite(128, Mathf.RoundToInt(14f * Css), PotaLook.HudOrangeTop, PotaLook.HudOrange, PotaLook.HudGold2, Mathf.RoundToInt(3f * Css), 0f, 0);
            rimg.type = Image.Type.Sliced;
            hud.MakeText(rrt, "Label", 20f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, rrt.sizeDelta, TextAnchor.MiddleCenter, Color.white, "Yeni tahta");
            hud._resultPanel = overlay;
            overlay.SetActive(false);
            return hud;
        }

        /// <summary>A .sayac-style box: soft shadow (0 6px 12px), lip (0 3px 0 #b3600a), face. Size in CSS px.</summary>
        private static RectTransform Counter(Transform parent, string name, Vector2 anchor, Vector2 sizeCss, Sprite face, Sprite lip, Sprite soft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = sizeCss * Css;
            Layer(rt, "Shadow", soft, new Color(0f, 0f, 0f, 0.25f), new Vector2(0f, -6f * Css), 12f * Css);
            Layer(rt, "Lip", lip, Color.white, new Vector2(0f, -3f * Css), 0f);
            Layer(rt, "Face", face, Color.white, Vector2.zero, 0f);
            return rt;
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
            img.raycastTarget = false;
            return img;
        }

        private RectTransform ArrowButton(Transform root, string name, Sprite face, Sprite pressed, Sprite lip, ColorHuntSettings s, bool left)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Button));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = Vector2.one * (64f * Css);
            Layer(rt, "Shadow", ShadowSprite(128, 60, 20), new Color(0f, 0f, 0f, 0.3f), new Vector2(0f, -10f * Css), 9f * Css).type = Image.Type.Simple;
            Layer(rt, "Lip", lip, Color.white, new Vector2(0f, -5f * Css), 0f).type = Image.Type.Simple;
            var faceImg = Layer(rt, "Face", face, Color.white, Vector2.zero, 0f);
            faceImg.type = Image.Type.Simple;
            faceImg.raycastTarget = true;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Shadow));
            icon.transform.SetParent(rt, false);
            var irt = (RectTransform)icon.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = Vector2.one * (40f * Css);
            irt.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);                 // #dondurSol img { scaleX(-1) }
            var iimg = icon.GetComponent<Image>();
            iimg.raycastTarget = false;
            iimg.preserveAspect = true;
            if (s.ArrowIcon != null) iimg.sprite = Sprite.Create(s.ArrowIcon, new Rect(0, 0, s.ArrowIcon.width, s.ArrowIcon.height), new Vector2(0.5f, 0.5f));
            var ish = icon.GetComponent<Shadow>();
            ish.effectColor = new Color(0f, 0f, 0f, 0.25f);
            ish.effectDistance = new Vector2(0f, -2f * Css);
            var button = go.GetComponent<Button>();
            button.targetGraphic = faceImg;
            button.transition = Selectable.Transition.SpriteSwap;
            var ss = button.spriteState;
            ss.pressedSprite = pressed;
            button.spriteState = ss;
            return rt;
        }

        private Text MakeText(Transform parent, string name, float sizeCss, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, TextAnchor align, Color color, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = Mathf.RoundToInt(sizeCss * Css);
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ---------- generated sprites (canvas units = pixels) ----------

        /// <summary>Rounded rectangle: vertical gradient, border, optional inset highlight under the top border; 9-sliced.</summary>
        public static Sprite PanelSprite(int size, int radius, Color top, Color bottom, Color border, int borderWidth, float highlight, int highlightWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                    float aa = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var fill = Color.Lerp(bottom, top, y / (size - 1f));
                    if (highlight > 0f && y >= size - borderWidth - highlightWidth) fill = Color.Lerp(fill, Color.white, highlight);
                    var c = Color.Lerp(border, fill, inner);
                    c.a = aa * Mathf.Lerp(border.a, fill.a, inner);      // a clear fill leaves only the border (the ✓ glow)
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            float b = radius + borderWidth + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>Soft rounded shadow (black, alpha fades over <paramref name="blur"/>), 9-sliced; tinted by the Image colour.</summary>
        public static Sprite ShadowSprite(int size, int radius, int blur)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius + blur) + blur;
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + blur) / (2f * blur)));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            float b = radius + 2f * blur + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static float RoundedDistance(float x, float y, int size, int radius)
        {
            float hx = size * 0.5f - radius, hy = size * 0.5f - radius;
            float dx = Mathf.Abs(x - size * 0.5f) - hx, dy = Mathf.Abs(y - size * 0.5f) - hy;
            float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside + inside - radius;
        }

        /// <summary>Circle with a vertical gradient and a border.</summary>
        public static Sprite CircleSprite(int size, Color top, Color bottom, Color border, int borderWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) - r;
                    float aa = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var col = Color.Lerp(border, Color.Lerp(bottom, top, y / (size - 1f)), inner);
                    col.a = aa;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
