using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota-style HUD (navy panels, gold numbers): ammo counter, target panel (swatch + remaining, ✓ when done),
    /// two round rotate buttons, hint, result overlay with retry. Built entirely in code (uGUI, generated sprites).
    /// </summary>
    public class ColorHuntHud : MonoBehaviour
    {
        public ColorHuntGame Game;
        public ColorHuntSettings S;

        private Text _ammo;
        private Text _hint;
        private Text _resultTitle;
        private GameObject _resultPanel;
        private Transform _targets;
        private readonly List<(int color, Image swatch, Text count, Text tick)> _targetRows = new List<(int, Image, Text, Text)>();
        private float _ammoPop;
        private readonly Dictionary<Text, float> _pops = new Dictionary<Text, float>();
        private Font _font;

        public void Bind(ColorHuntGame game)
        {
            Game = game;
            game.AmmoChanged += OnAmmo;
            game.TargetsChanged += OnTargets;
            game.RoundEnded += OnEnded;
            game.RoundStarted += OnStarted;
        }

        private void Start()
        {
            if (Game == null) Game = FindAnyObjectByType<ColorHuntGame>();
            if (Game != null && Game.Board != null) { OnStarted(); }
        }

        private void OnStarted()
        {
            _resultPanel.SetActive(false);
            _hint.enabled = true;
            BuildTargets();
            OnAmmo();
        }

        private void OnAmmo()
        {
            if (Game == null) return;
            string s = Game.DisplayedAmmo.ToString();
            if (_ammo.text != s) { _ammo.text = s; }
        }

        private void OnTargets()
        {
            if (Game == null || Game.Board == null) return;
            foreach (var row in _targetRows)
            {
                int remaining = 0;
                foreach (var t in Game.Board.Targets) if (t.Color == row.color) remaining = Mathf.Max(0, t.Remaining);
                string s = remaining.ToString();
                if (row.count.text != s) { row.count.text = s; _pops[row.count] = 1f; }
                bool done = remaining == 0;
                row.count.color = done ? S.GreenColor : S.GoldColor;
                row.tick.enabled = done;
            }
        }

        private void OnEnded(bool won, string reason)
        {
            _resultTitle.text = won ? "Hedef tamam!" : reason == "ammo" ? "Top bitti" : "Hamle kalmadı";
            _resultTitle.color = won ? S.GreenColor : S.RedColor;
            _resultPanel.SetActive(true);
        }

        private void Update()
        {
            if (Game != null && Game.Busy) _hint.enabled = false;
            if (_iconPops.Count > 0)
            {
                var ikeys = new List<Transform>(_iconPops.Keys);
                foreach (var t in ikeys)
                {
                    float p = Mathf.Max(0f, _iconPops[t] - Time.unscaledDeltaTime * 4f);
                    t.localScale = Vector3.one * (1f + 0.5f * Mathf.Sin(p * Mathf.PI));
                    if (p <= 0f) _iconPops.Remove(t); else _iconPops[t] = p;
                }
            }
            if (_pops.Count == 0) return;
            var keys = new List<Text>(_pops.Keys);
            foreach (var t in keys)
            {
                float p = Mathf.Max(0f, _pops[t] - Time.unscaledDeltaTime * 5f);
                t.transform.localScale = Vector3.one * (1f + 0.35f * p);
                if (p <= 0f) _pops.Remove(t); else _pops[t] = p;
            }
        }

        private void BuildTargets()
        {
            foreach (Transform c in _targets) Destroy(c.gameObject);
            _targetRows.Clear();
            if (Game == null || Game.Board == null) return;
            float x = 0f;
            foreach (var t in Game.Board.Targets)
            {
                var swatchGo = new GameObject("Swatch", typeof(Image));
                swatchGo.transform.SetParent(_targets, false);
                var swatch = swatchGo.GetComponent<Image>();
                var icon = S.ItemIcon(t.Color);
                if (icon != null)
                {
                    swatch.sprite = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f));
                    swatch.preserveAspect = true;
                    var shadow = swatchGo.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.35f); shadow.effectDistance = new Vector2(0f, -3f);
                }
                else
                {
                    swatch.sprite = RoundedRect(64, 16, Color.white, new Color(1f, 1f, 1f, 0.9f), 6);
                    swatch.type = Image.Type.Sliced;
                    swatch.color = S.PaletteColor(t.Color);
                }
                var srt = swatchGo.GetComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f); srt.pivot = new Vector2(0f, 0.5f);
                srt.anchoredPosition = new Vector2(x, -2f); srt.sizeDelta = icon != null ? new Vector2(60f, 60f) : new Vector2(44f, 44f);
                x += icon != null ? 68f : 52f;
                var count = MakeText(_targets, "Count", 56, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(90f, 70f), TextAnchor.MiddleLeft, S.GoldColor);
                count.text = t.Remaining.ToString();
                var tick = MakeText(_targets, "Tick", 40, new Vector2(0f, 0.5f), new Vector2(x + 70f, 2f), new Vector2(50f, 70f), TextAnchor.MiddleLeft, S.GreenColor);
                tick.text = "✓";
                tick.enabled = false;
                x += 118f;
                _targetRows.Add((t.Color, swatch, count, tick));
            }
            var trt = _targets.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(x + 16f, 70f);
            OnTargets();
        }

        /// <summary>Screen-space centre of a target's icon (for the flying item), false if that colour has no box.</summary>
        public bool TargetScreenPoint(int color, out Vector2 screen)
        {
            screen = default;
            foreach (var row in _targetRows)
            {
                if (row.color != color) continue;
                var rt = row.swatch.rectTransform;
                screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                return true;
            }
            return false;
        }

        /// <summary>The flying item arrived: bounce the icon and its count.</summary>
        public void PopTarget(int color)
        {
            foreach (var row in _targetRows)
            {
                if (row.color != color) continue;
                _pops[row.count] = 1f;
                _iconPops[row.swatch.transform] = 1f;
            }
        }

        private readonly Dictionary<Transform, float> _iconPops = new Dictionary<Transform, float>();

        // ---------- construction ----------

        public static ColorHuntHud Create(Transform parent, ColorHuntSettings s)
        {
            var font = s.Font != null ? s.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("ColorHuntHud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.AddComponent<ColorHuntHud>();
            hud.S = s;
            hud._font = font;
            var root = canvasGo.transform;

            var panelSprite = RoundedRect(96, 26, s.NavyColor, s.GoldColor, 6);
            // Ammo panel (top-left).
            var ammoPanel = Panel(root, "AmmoPanel", panelSprite, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(250f, 130f));
            var ammoLabel = hud.MakeText(ammoPanel.transform, "Label", 26, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(240f, 34f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f));
            ammoLabel.text = "CEPHANE";
            hud._ammo = hud.MakeText(ammoPanel.transform, "Ammo", 64, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(240f, 80f), TextAnchor.MiddleCenter, s.GoldColor);
            hud._ammo.text = "0";

            // Target panel (top-right).
            var targetPanel = Panel(root, "TargetPanel", panelSprite, new Vector2(1f, 1f), new Vector2(-30f, -70f), new Vector2(430f, 130f));
            var targetLabel = hud.MakeText(targetPanel.transform, "Label", 26, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(400f, 34f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f));
            targetLabel.text = "HEDEF";
            var targetsGo = new GameObject("Targets", typeof(RectTransform));
            targetsGo.transform.SetParent(targetPanel.transform, false);
            var trt = targetsGo.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f); trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(-20f, 14f); trt.sizeDelta = new Vector2(360f, 70f);
            hud._targets = targetsGo.transform;

            // Rotate buttons (bottom corners).
            var circle = Circle(128, s.NavyColor, s.GoldColor, 8);
            hud.MakeRotateButton(root, "RotateLeft", circle, s, new Vector2(0f, 0f), new Vector2(90f, 110f), true);
            hud.MakeRotateButton(root, "RotateRight", circle, s, new Vector2(1f, 0f), new Vector2(-90f, 110f), false);

            hud._hint = hud.MakeText(root, "Hint", 30, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(600f, 80f), TextAnchor.MiddleCenter, new Color(0.06f, 0.19f, 0.33f, 0.9f));
            hud._hint.text = "Bloğa dokun: canı kadar top harcanır.\nOklar tahtayı çevirir, bedava.";

            // Result overlay.
            var overlay = new GameObject("ResultPanel", typeof(Image));
            overlay.transform.SetParent(root, false);
            var ort = overlay.GetComponent<RectTransform>();
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = ort.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.14f, 0.55f);
            var card = Panel(overlay.transform, "Card", panelSprite, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(720f, 420f));
            hud._resultTitle = hud.MakeText(card.transform, "Title", 76, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 120f), TextAnchor.MiddleCenter, s.GoldColor);
            var retry = new GameObject("Retry", typeof(Image), typeof(Button));
            retry.transform.SetParent(card.transform, false);
            var rrt = retry.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f); rrt.anchoredPosition = new Vector2(0f, 70f); rrt.sizeDelta = new Vector2(380f, 110f);
            var rimg = retry.GetComponent<Image>();
            rimg.sprite = RoundedRect(96, 30, s.GoldColor, new Color(0.85f, 0.6f, 0.12f), 6); rimg.type = Image.Type.Sliced;
            var rtext = hud.MakeText(retry.transform, "Label", 46, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 110f), TextAnchor.MiddleCenter, s.NavyColor);
            rtext.text = "Yeni tahta";
            retry.GetComponent<Button>().onClick.AddListener(() => { if (hud.Game != null) hud.Game.NewRound(); });
            hud._resultPanel = overlay;
            overlay.SetActive(false);
            return hud;
        }

        private void MakeRotateButton(Transform root, string name, Sprite circle, ColorHuntSettings s, Vector2 anchor, Vector2 pos, bool left)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(150f, 150f);
            go.GetComponent<Image>().sprite = circle;
            var icon = new GameObject("Icon", typeof(Image));
            icon.transform.SetParent(go.transform, false);
            var irt = icon.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(84f, 88f);
            irt.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
            var iimg = icon.GetComponent<Image>();
            iimg.raycastTarget = false;
            if (s.ArrowIcon != null) iimg.sprite = Sprite.Create(s.ArrowIcon, new Rect(0, 0, s.ArrowIcon.width, s.ArrowIcon.height), new Vector2(0.5f, 0.5f));
            else { iimg.enabled = false; var t = MakeText(go.transform, "Glyph", 70, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f), TextAnchor.MiddleCenter, s.GoldColor); t.text = left ? "↺" : "↻"; }
            var button = go.GetComponent<Button>();
            var colors = button.colors; colors.pressedColor = new Color(1f, 0.75f, 0.45f); colors.fadeDuration = 0.05f; button.colors = colors;
            button.onClick.AddListener(() => { if (Game != null) Game.Rotate(left ? Rotation.Counter : Rotation.Clockwise); });
        }

        private static GameObject Panel(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.type = Image.Type.Sliced; img.raycastTarget = false;
            return go;
        }

        private Text MakeText(Transform parent, string name, int size, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.alignment = align; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0.02f, 0.06f, 0.16f, 0.6f); o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        // ---------- generated sprites ----------

        public static Sprite RoundedRect(int size, int radius, Color fill, Color border, int borderWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                    float aa = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var c = Color.Lerp(border, fill, inner);
                    c.a = aa;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            float b = radius + borderWidth + 2f;
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

        public static Sprite Circle(int size, Color fill, Color border, int borderWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) - r;
                    float aa = Mathf.Clamp01(0.5f - d);
                    float inner = Mathf.Clamp01(0.5f - (d + borderWidth));
                    var col = Color.Lerp(border, fill, inner);
                    // Subtle vertical gradient on the fill.
                    col = Color.Lerp(col, Color.white, inner * 0.12f * (y / (float)size));
                    col.a = aa;
                    tex.SetPixel(x, y, col);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
