using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #menu: the level's key art, the top strip (avatar + level, league + trophies, coins, lives, gear) and the Play button
    /// over the tab bar. M7: the art breathes (1 → 1.02, 3.2 s, pivoted on the hero), the strip badges drop in one after another at
    /// entry, the Play button pulses (Pota nabizDugme) and a shine sweeps it every ShineEvery seconds; two cloud sprites drift.
    /// Feel off = Pota: a still image and the pulse only.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        private MetaSettings _s;
        private RectTransform _art;
        private RectTransform[] _badges;
        private Vector2[] _badgeHomes;
        private RectTransform _shine;
        private RectTransform[] _clouds;
        private float _t, _entered = -1f;

        public Image Background { get; private set; }
        public MetaButton Play { get; private set; }
        public MetaButton Gear { get; private set; }
        public MetaButton CoinsBadge { get; private set; }
        public RollingNumber Coins { get; private set; }
        public RollingNumber Trophies { get; private set; }
        public RollingNumber Lives { get; private set; }
        public Text LevelPill { get; private set; }
        public Image LeagueIcon { get; private set; }
        public MetaButton LeagueBadge { get; private set; }
        public string PlayLabel => Play != null && Play.Label != null ? Play.Label.text : "";
        public string CoinsText => Coins != null ? Coins.Text.text : "";
        public string TrophiesText => Trophies != null ? Trophies.Text.text : "";
        public string LivesText => Lives != null ? Lives.Text.text : "";
        public float HeroScale => _art != null ? _art.localScale.x : 1f;
        public bool ShineVisible => _shine != null && _shine.gameObject.activeSelf;
        /// <summary>The coin badge's centre on the root canvas (coin flights land here).</summary>
        public Vector2 CoinsPoint => CanvasPoint(CoinsBadge.Rect);
        /// <summary>A cloud's x (canvas px); the clouds drift right and wrap (M7).</summary>
        public float CloudX(int i) => _clouds != null && i < _clouds.Length && _clouds[i] != null ? _clouds[i].anchoredPosition.x : 0f;
        public Vector2 TrophiesPoint => CanvasPoint(_trophiesRect);
        private RectTransform _trophiesRect;

        public static MenuScreen Create(Transform parent, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("Menu", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var m = go.AddComponent<MenuScreen>();
            m._s = s;
            // key art, pivoted where the hero's chest sits (left-centre) so the breath moves him, not the sky
            var art = new GameObject("Art", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(rt, false);
            m._art = (RectTransform)art.transform;
            m._art.anchorMin = Vector2.zero; m._art.anchorMax = Vector2.one; m._art.offsetMin = m._art.offsetMax = Vector2.zero;
            m._art.pivot = new Vector2(0.3f, 0.45f);
            m.Background = art.GetComponent<Image>();
            m.Background.sprite = s.MenuBackgroundFor(s.Save.Level);
            m.Background.preserveAspect = false;
            m.Background.raycastTarget = false;
            // Pota's overlay gradient: a touch of navy at the top, more at the bottom
            var top = Overlay(rt, "ShadeTop", new Vector2(0f, 0.78f), new Vector2(1f, 1f), new Color(20f / 255f, 40f / 255f, 80f / 255f, 0.12f), true);
            var bottom = Overlay(rt, "ShadeBottom", new Vector2(0f, 0f), new Vector2(1f, 0.22f), new Color(20f / 255f, 40f / 255f, 80f / 255f, 0.45f), false);
            // clouds (M7): two soft sprites far up, drifting
            m._clouds = new RectTransform[2];
            for (int i = 0; i < 2; i++)
            {
                var c = new GameObject("Cloud" + i, typeof(RectTransform), typeof(Image));
                c.transform.SetParent(rt, false);
                var crt = (RectTransform)c.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
                crt.sizeDelta = new Vector2(220f + 60f * i, 110f + 30f * i) * css;
                crt.anchoredPosition = new Vector2((-120f + 240f * i) * css, -(150f + 70f * i) * css);
                var ci = c.GetComponent<Image>();
                ci.sprite = s.CloudSprite; ci.color = new Color(1f, 1f, 1f, 0.55f); ci.preserveAspect = true; ci.raycastTarget = false;
                c.SetActive(s.CloudSprite != null);
                m._clouds[i] = crt;
            }
            // top strip
            var strip = new GameObject("Strip", typeof(RectTransform));
            strip.transform.SetParent(rt, false);
            var srt = (RectTransform)strip.transform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -14f * css); srt.sizeDelta = new Vector2(0f, 58f * css);
            var avatar = MetaButton.Create(srt, "Profile", MetaButton.Kind.Round, MetaButton.Tone.Navy, "", new Vector2(50f, 50f), s.Lilita, 12f, s.Feel);
            Place(avatar.Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f * css, 0f));
            var face = new GameObject("Face", typeof(RectTransform), typeof(Image), typeof(Mask));
            face.transform.SetParent(avatar.Face, false);
            var frt = (RectTransform)face.transform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = Vector2.one * (3f * css); frt.offsetMax = -Vector2.one * (3f * css);
            face.GetComponent<Image>().sprite = MetaStyle.Circle(44, Color.white, Color.white, Color.white, 0);
            face.GetComponent<Mask>().showMaskGraphic = false;
            var hero = new GameObject("Hero", typeof(RectTransform), typeof(Image));
            hero.transform.SetParent(frt, false);
            var hrt = (RectTransform)hero.transform; hrt.anchorMin = new Vector2(0.5f, 1f); hrt.anchorMax = new Vector2(0.5f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
            hrt.sizeDelta = new Vector2(44f, 62f) * css; hrt.anchoredPosition = new Vector2(0f, 2f * css);
            var hi = hero.GetComponent<Image>(); hi.sprite = s.Hero; hi.preserveAspect = true; hi.raycastTarget = false;
            m.LevelPill = Pill(avatar.Rect, s, "LevelPill", s.Save.Level.ToString(), new Vector2(24f, -44f) * css, MetaStyle.Orange);
            // the right group: a row, right-aligned, gap 4 (Pota #menuUst), each badge as wide as its label needs
            var right = new GameObject("Right", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            right.transform.SetParent(srt, false);
            var rrt = (RectTransform)right.transform;
            rrt.anchorMin = new Vector2(1f, 0.5f); rrt.anchorMax = new Vector2(1f, 0.5f); rrt.pivot = new Vector2(1f, 0.5f);
            rrt.anchoredPosition = new Vector2(-14f * css, 0f); rrt.sizeDelta = new Vector2(320f, 50f) * css;
            var rh = right.GetComponent<HorizontalLayoutGroup>();
            rh.spacing = 4f * css; rh.childAlignment = TextAnchor.MiddleRight; rh.childControlWidth = false; rh.childControlHeight = false; rh.childForceExpandWidth = false; rh.childForceExpandHeight = false;
            var league = Badge(rrt, s, "LeagueBadge", s.LeagueBadges[s.LeagueOf(s.Save.Trophies)], MetaStyle.KupaKisa(s.Save.Trophies), Vector2.zero, 26f);
            m.LeagueIcon = league.Icon;
            m.LeagueBadge = league;
            var coins = Badge(rrt, s, "CoinsBadge", s.Coin, s.Save.Coins.ToString(), Vector2.zero, 20f);
            m.CoinsBadge = coins;
            var lives = Badge(rrt, s, "LivesBadge", s.Heart, s.Save.Lives >= s.Save.MaxLives ? "TAM" : s.Save.Lives.ToString(), Vector2.zero, 20f);
            m.Gear = MetaButton.Create(rrt, "Gear", MetaButton.Kind.Round, MetaButton.Tone.Navy, "", new Vector2(44f, 44f), s.Lilita, 12f, s.Feel);
            var gi = new GameObject("Icon", typeof(RectTransform), typeof(Image)); gi.transform.SetParent(m.Gear.Face, false);
            var girt = (RectTransform)gi.transform; girt.anchorMin = girt.anchorMax = new Vector2(0.5f, 0.5f); girt.sizeDelta = Vector2.one * (26f * css);
            var gimg = gi.GetComponent<Image>(); gimg.sprite = s.Gear; gimg.preserveAspect = true; gimg.raycastTarget = false;
            // the entrance moves the avatar and the whole right group (its badges are placed by the layout group every frame)
            m._badges = new[] { avatar.Rect, rrt };
            m._badgeHomes = new Vector2[m._badges.Length];
            for (int i = 0; i < m._badges.Length; i++) m._badgeHomes[i] = m._badges[i].anchoredPosition;
            m._trophiesRect = league.Rect;
            m.Trophies = RollingNumber.Attach(league.Label, s.Feel, 6f);
            m.Coins = RollingNumber.Attach(coins.Label, s.Feel, 6f);
            m.Lives = RollingNumber.Attach(lives.Label, s.Feel, 6f);
            // Play
            m.Play = MetaButton.Create(rt, "Play", MetaButton.Kind.Big, MetaButton.Tone.Green, "Bölüm " + s.Save.Level, new Vector2(226f, 72f), s.Lilita, 34f, s.Feel);
            Place(m.Play.Rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, (TabBar.HeightCss + 10f + 18f) * css));
            m._shine = Shine(m.Play.Face, css);
            return m;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos;
        }

        private static Image Overlay(RectTransform parent, string name, Vector2 min, Vector2 max, Color color, bool fadeDown)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            var clear = color; clear.a = 0f;
            img.sprite = ColorHunt.ColorHuntHud.PanelSprite(16, 1, fadeDown ? color : clear, fadeDown ? clear : color, clear, 0, 0f, 0);
            img.type = Image.Type.Sliced; img.raycastTarget = false;
            return img;
        }

        /// <summary>A .rozet pill: icon + Lilita 15 css label, 32 css tall, anchored at the strip's right.</summary>
        private static MetaButton Badge(RectTransform strip, MetaSettings s, string name, Sprite icon, string label, Vector2 rightCss, float iconCss)
        {
            float css = MetaStyle.Css;
            var b = MetaButton.Create(strip, name, MetaButton.Kind.Small, MetaButton.Tone.Navy, label, new Vector2(iconCss + 14f + label.Length * 10f, 32f), s.Lilita, 15f, s.Feel);
            b.gameObject.AddComponent<LayoutElement>().preferredWidth = b.Rect.sizeDelta.x;
            b.Face.GetComponent<Image>().sprite = MetaStyle.Pill();
            b.WithIcon(icon, iconCss);
            b.Icon.rectTransform.anchoredPosition = new Vector2(4f * css, 0f);
            b.Label.rectTransform.offsetMin = new Vector2((iconCss + 8f) * css, 0f);
            b.Label.rectTransform.offsetMax = new Vector2(-6f * css, 0f);
            b.Label.color = Color.white;
            return b;
        }

        private static Text Pill(RectTransform parent, MetaSettings s, string name, string text, Vector2 pos, Color color)
        {
            float css = MetaStyle.Css;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(30f, 18f) * css;
            var img = go.GetComponent<Image>();
            img.sprite = ColorHunt.ColorHuntHud.PanelSprite(64, MetaStyle.R(9), color, color, MetaStyle.Gold, MetaStyle.R(2), 0f, 0);
            img.type = Image.Type.Sliced; img.raycastTarget = false;
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(rt, false);
            var trt = (RectTransform)tgo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.GetComponent<Text>(); t.font = s.FredokaBold != null ? s.FredokaBold : s.Lilita; t.fontSize = Mathf.RoundToInt(12f * css);
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.text = text; t.raycastTarget = false;
            return t;
        }

        /// <summary>A white slanted strip clipped to the button face (M7 shine).</summary>
        private static RectTransform Shine(RectTransform face, float css)
        {
            var mask = new GameObject("ShineMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            mask.transform.SetParent(face, false);
            var mrt = (RectTransform)mask.transform; mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.offsetMin = Vector2.one * (4f * css); mrt.offsetMax = -Vector2.one * (4f * css);
            mask.GetComponent<Image>().sprite = MetaStyle.Card(18); mask.GetComponent<Image>().type = Image.Type.Sliced;
            mask.GetComponent<Mask>().showMaskGraphic = false;
            mask.GetComponent<Image>().raycastTarget = false;
            var go = new GameObject("Shine", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(mrt, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(40f * css, 140f * css); rt.localRotation = Quaternion.Euler(0f, 0f, 20f);
            var img = go.GetComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0.35f); img.raycastTarget = false;
            img.sprite = ColorHunt.ColorHuntHud.PanelSprite(32, 1, Color.white, Color.white, Color.white, 0, 0f, 0); img.type = Image.Type.Sliced;
            go.SetActive(false);
            mask.transform.SetSiblingIndex(0);
            return rt;
        }

        /// <summary>Refreshes the texts from the save (rolling when asked).</summary>
        public void Refresh(SampleSave save, bool roll)
        {
            Background.sprite = _s.MenuBackgroundFor(save.Level);
            if (Play.Label != null) Play.Label.text = "Bölüm " + save.Level;
            LevelPill.text = save.Level.ToString();
            Coins.Set(save.Coins, roll);
            Trophies.Text.text = MetaStyle.KupaKisa(save.Trophies);
            Lives.Text.text = save.Lives >= save.MaxLives ? "TAM" : save.Lives.ToString();
            LeagueIcon.sprite = _s.LeagueBadges[_s.LeagueOf(save.Trophies)];
        }

        /// <summary>The strip's entrance: badges drop in from above, 0.05 s apart (M7).</summary>
        public void Enter() => _entered = _s.Feel.Enabled ? 0f : -1f;

        private Vector2 CanvasPoint(RectTransform t)
        {
            var root = (RectTransform)transform;
            return root.InverseTransformPoint(t.TransformPoint(t.rect.center));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            var feel = _s.Feel;
            if (!feel.Enabled)
            {
                _art.localScale = Vector3.one;
                Play.Rect.localScale = Vector3.one * (1f + 0.035f * (0.5f + 0.5f * Mathf.Sin(_t / 1.8f * 2f * Mathf.PI)));   // Pota nabizDugme
                _shine.gameObject.SetActive(false);
                return;
            }
            _art.localScale = Vector3.one * MetaCurves.Breath(_t, feel.Breath);
            _art.anchoredPosition = new Vector2(Mathf.Sin(_t * 0.7f) * 2f * MetaStyle.Css, 0f);
            Play.Rect.localScale = Vector3.one * (1f + 0.035f * (0.5f + 0.5f * Mathf.Sin(_t / 1.8f * 2f * Mathf.PI)));
            float shine = MetaCurves.Shine(_t, feel.ShineEvery, feel.ShineTime);
            bool on = shine > -1f;
            if (_shine.gameObject.activeSelf != on) _shine.gameObject.SetActive(on);
            if (on) _shine.anchoredPosition = new Vector2(shine * Play.Face.rect.width * 0.6f, 0f);
            if (_entered >= 0f)
            {
                _entered += dt;
                for (int i = 0; i < _badges.Length; i++)
                {
                    float u = Mathf.Clamp01((_entered - i * 0.05f) / 0.25f);
                    _badges[i].anchoredPosition = _badgeHomes[i] + new Vector2(0f, (1f - ColorHunt.StudioCurves.EaseOutCubic(u)) * 40f * MetaStyle.Css);
                }
                if (_entered > 0.25f + 0.05f * _badges.Length) _entered = -1f;
            }
            for (int i = 0; i < _clouds.Length; i++)
            {
                if (_clouds[i] == null || !_clouds[i].gameObject.activeSelf) continue;
                var p = _clouds[i].anchoredPosition;
                p.x += (6f + 4f * i) * MetaStyle.Css * dt;
                if (p.x > 320f * MetaStyle.Css) p.x = -320f * MetaStyle.Css;
                _clouds[i].anchoredPosition = p;
            }
        }
    }
}
