using System;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #market (style guide §9): ribbon, coin badge, four category pills, the showcase (spotlight, podium, the hero breathing
    /// with the worn jersey's colour through the mask), the slot column, the cream try-on panel with five chips and the big button,
    /// cards for boosters and coin packs. M10: a purchase sends coins from the badge to the chip (M3 spend), the chip overshoots and
    /// gets its stamp, ring + 12 stars, the hero jumps (Pota kutla); trying a chip hops the hero and flashes the new part; too little
    /// money shakes the big button and blinks the badge red. Feel off = Pota's instant market.
    /// </summary>
    public class MarketScreen : MonoBehaviour
    {
        private class Chip { public MarketCatalog.Item Item; public RectTransform Rt; public Image Card, Icon; public Text Label; public MetaButton Hit; }

        public const float HeroBottom = 380f;
        public const float Breath = 3.2f, Hop = 0.28f, Celebrate = 0.5f, Flash = 0.15f, ShakeTime = 0.3f, BlinkTime = 0.6f;

        private MetaSettings _s;
        private RectTransform _root, _hero, _ring, _panel;
        private Image _jersey, _coinBadgeFace;
        private Image[] _stars;
        private MetaButton[] _tabs;
        private RectTransform _chipsRow, _cardsRow;
        private Chip[] _chips;
        private Text[] _cardTitles;
        private MarketCatalog.Category _category = MarketCatalog.Category.Clothes;
        private readonly System.Collections.Generic.HashSet<string> _owned = new System.Collections.Generic.HashSet<string> { "forma-mavi", "top-klasik" };
        private string _wornJersey = "forma-mavi", _wornBall = "top-klasik";
        private int _tried = -1;
        private float _t, _hopT = -1f, _hopTime, _flashT = -1f, _shakeT = -1f, _blinkT = -1f, _burstT = -1f;
        private int _burstChip = -1;
        private Action<int, Vector2, Action> _spend;
        private Action<string> _toast;
        private Func<int> _coinsOf = () => 0;                      // the director's save is the only source of the coin count

        public MetaButton Big { get; private set; }
        public MetaButton CoinsBadge { get; private set; }
        public RollingNumber Coins { get; private set; }
        public CoinBadgeFx CoinsFx { get; private set; }
        public int Tried => _tried;
        public bool Purchasing { get; private set; }
        public float HeroHopPeak { get; private set; }
        public float ShakePeak { get; private set; }
        public float HeroScale => _hero.localScale.x;
        public Vector2 CoinsPoint => MetaUiKit.CanvasPoint(_root, CoinsBadge.Rect);
        public string ChipLabel(int i) => _chips[i].Label.text;
        public bool Owned(string id) => _owned.Contains(id);

        public static MarketScreen Create(RectTransform page, MetaSettings s, RectTransform root)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("MarketScreen", typeof(RectTransform));
            go.transform.SetParent(page, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var m = go.AddComponent<MarketScreen>();
            m._s = s; m._root = root;
            Font bold = s.FredokaBold != null ? s.FredokaBold : s.Lilita;
            var ground = MetaUiKit.Card(rt, "Ground", MetaStyle.Navy, 1);
            ground.sprite = MetaStyle.Panel(128, 1, MetaStyle.PanelTop, MetaStyle.Navy, MetaStyle.Navy, 0, 0f, 0);
            // ribbon + coin badge
            var ribbon = new GameObject("Ribbon", typeof(RectTransform), typeof(Image));
            ribbon.transform.SetParent(rt, false);
            var rrt = (RectTransform)ribbon.transform; rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1f); rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(-40f * css, -16f * css); rrt.sizeDelta = new Vector2(200f, 48f) * css;
            var rimg = ribbon.GetComponent<Image>(); rimg.sprite = MetaStyle.Ribbon(); rimg.type = Image.Type.Sliced; rimg.raycastTarget = false;
            MetaUiKit.Label(rrt, "Title", s.Lilita, "Market", 26f, Color.white);
            m.CoinsBadge = MetaButton.Create(rt, "CoinsBadge", MetaButton.Kind.Small, MetaButton.Tone.Navy, s.Save.Coins.ToString(), new Vector2(84f, 32f), s.Lilita, 15f, s.Feel);
            m.CoinsBadge.Rect.anchorMin = m.CoinsBadge.Rect.anchorMax = new Vector2(1f, 1f); m.CoinsBadge.Rect.pivot = new Vector2(1f, 1f);
            m.CoinsBadge.Rect.anchoredPosition = new Vector2(-12f * css, -24f * css);
            m.CoinsBadge.WithIcon(s.Coin, 20f);
            m.CoinsBadge.Label.rectTransform.offsetMin = new Vector2(28f * css, 0f);
            m._coinBadgeFace = m.CoinsBadge.Face.GetComponent<Image>();
            m.Coins = RollingNumber.Attach(m.CoinsBadge.Label, s.Feel, 6f);
            m.CoinsFx = CoinBadgeFx.Attach(m.CoinsBadge);
            // category pills
            string[] names = { "Giysiler", "Toplar", "Güçlendirme", "Paketler" };
            m._tabs = new MetaButton[4];
            for (int i = 0; i < 4; i++)
            {
                var tab = MetaButton.Create(rt, "Tab" + i, MetaButton.Kind.Small, MetaButton.Tone.Navy, names[i], new Vector2(88f, 44f), s.Lilita, 11f, s.Feel);
                tab.Rect.anchorMin = tab.Rect.anchorMax = new Vector2(0.5f, 1f); tab.Rect.pivot = new Vector2(0.5f, 1f);
                tab.Rect.anchoredPosition = new Vector2((-135f + i * 90f) * css, -118f * css);
                tab.WithIcon(s.CategoryIcons.Length > i ? s.CategoryIcons[i] : null, 26f);
                tab.Label.color = PotaLook.Hex(0xcfe0ff); tab.Label.rectTransform.offsetMin = new Vector2(34f * css, 0f);
                int cat = i;
                tab.Clicked += () => m.Select((MarketCatalog.Category)cat);
                m._tabs[i] = tab;
            }
            // showcase: spotlight, podium, hero
            var spot = MetaUiKit.Picture(rt, "Spot", MetaStyle.Shadow(120, 60), 240f, new Vector2(0.5f, 1f), new Vector2(0f, -270f));
            spot.color = new Color(1f, 1f, 1f, 0.35f); spot.preserveAspect = false; spot.rectTransform.localScale = new Vector3(1f, 1.25f, 1f);
            var podium = MetaUiKit.Picture(rt, "Podium", MetaStyle.Circle(200, MetaStyle.Navy2, MetaStyle.Navy, MetaStyle.Gold, 4), 220f, new Vector2(0.5f, 1f), new Vector2(0f, -372f));
            podium.rectTransform.localScale = new Vector3(1f, 0.28f, 1f); podium.preserveAspect = false;
            var hero = new GameObject("Hero", typeof(RectTransform));
            hero.transform.SetParent(rt, false);
            m._hero = (RectTransform)hero.transform;
            m._hero.anchorMin = m._hero.anchorMax = new Vector2(0.5f, 1f); m._hero.pivot = new Vector2(0.5f, 0f);
            m._hero.anchoredPosition = new Vector2(0f, -HeroBottom * css); m._hero.sizeDelta = new Vector2(200f * 553f / 784f, 200f) * css;
            var himg = MetaUiKit.Picture(m._hero, "Image", s.Hero, 200f, new Vector2(0.5f, 0f), Vector2.zero);
            himg.rectTransform.anchorMin = Vector2.zero; himg.rectTransform.anchorMax = Vector2.one; himg.rectTransform.offsetMin = himg.rectTransform.offsetMax = Vector2.zero; himg.preserveAspect = true;
            m._jersey = MetaUiKit.Picture(m._hero, "Jersey", JerseySprite(s.HeroMask), 200f, new Vector2(0.5f, 0f), Vector2.zero);
            m._jersey.gameObject.SetActive(m._jersey.sprite != null);
            m._jersey.rectTransform.anchorMin = Vector2.zero; m._jersey.rectTransform.anchorMax = Vector2.one; m._jersey.rectTransform.offsetMin = m._jersey.rectTransform.offsetMax = Vector2.zero; m._jersey.preserveAspect = true;
            // slot column
            for (int i = 0; i < 4; i++)
            {
                var slot = MetaUiKit.Picture(rt, "Slot" + i, MetaStyle.Panel(64, MetaStyle.R(12), Color.white, Color.white, MetaStyle.Gold, MetaStyle.R(2), 0f, 0), 44f, new Vector2(0f, 1f), new Vector2(14f, -(182f + i * 52f)));
                slot.type = Image.Type.Sliced; slot.preserveAspect = false;
                var icon = MetaUiKit.Picture(slot.rectTransform, "Icon", s.Clothes.Length > i ? s.Clothes[i] : null, 32f, new Vector2(0.5f, 0.5f), Vector2.zero);
                icon.color = i == 0 ? MarketCatalog.Jerseys[0].Colour : new Color(0.5f, 0.55f, 0.65f);
            }
            // try-on panel
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(rt, false);
            m._panel = (RectTransform)panel.transform;
            m._panel.anchorMin = new Vector2(0f, 0f); m._panel.anchorMax = new Vector2(1f, 0f); m._panel.pivot = new Vector2(0.5f, 0f);
            m._panel.anchoredPosition = new Vector2(0f, (TabBar.HeightCss + 14f) * css); m._panel.sizeDelta = new Vector2(0f, 200f * css);
            var pimg = panel.GetComponent<Image>(); pimg.sprite = MetaStyle.Panel(128, MetaStyle.R(28), MetaStyle.CreamTop, MetaStyle.Cream, MetaStyle.Gold, MetaStyle.R(4), 0f, 0);
            pimg.type = Image.Type.Sliced; pimg.raycastTarget = true;
            var hint = MetaUiKit.Label(m._panel, "Hint", bold, "Dokun: üstünde görürsün", 12f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f), TextAnchor.UpperCenter);
            hint.rectTransform.offsetMax = new Vector2(0f, -12f * css);
            m._chipsRow = new GameObject("Chips", typeof(RectTransform)).GetComponent<RectTransform>();
            m._chipsRow.SetParent(m._panel, false);
            m._chipsRow.anchorMin = new Vector2(0f, 1f); m._chipsRow.anchorMax = new Vector2(1f, 1f); m._chipsRow.pivot = new Vector2(0.5f, 1f);
            m._chipsRow.anchoredPosition = new Vector2(0f, -30f * css); m._chipsRow.sizeDelta = new Vector2(0f, 84f * css);
            m._chips = new Chip[5];
            for (int i = 0; i < 5; i++) m._chips[i] = m.MakeChip(i, s);
            m._cardsRow = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
            m._cardsRow.SetParent(m._panel, false);
            m._cardsRow.anchorMin = new Vector2(0f, 1f); m._cardsRow.anchorMax = new Vector2(1f, 1f); m._cardsRow.pivot = new Vector2(0.5f, 1f);
            m._cardsRow.anchoredPosition = new Vector2(0f, -30f * css); m._cardsRow.sizeDelta = new Vector2(0f, 84f * css);
            m._cardTitles = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = new GameObject("Card" + i, typeof(RectTransform), typeof(Image));
                card.transform.SetParent(m._cardsRow, false);
                var crt = (RectTransform)card.transform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f); crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = new Vector2((-110f + i * 110f) * css, 0f); crt.sizeDelta = new Vector2(100f, 84f) * css;
                var cimg = card.GetComponent<Image>(); cimg.sprite = MetaStyle.Card(18); cimg.type = Image.Type.Sliced; cimg.raycastTarget = false;
                MetaUiKit.Picture(crt, "Icon", s.CoinPile, 40f, new Vector2(0.5f, 1f), new Vector2(0f, -6f));
                m._cardTitles[i] = MetaUiKit.Label(crt, "Title", bold, "", 12f, MetaStyle.Navy, TextAnchor.LowerCenter);
                m._cardTitles[i].rectTransform.offsetMin = new Vector2(0f, 18f * css);
                var note = MetaUiKit.Label(crt, "Note", bold, "", 10f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.6f), TextAnchor.LowerCenter);
                note.rectTransform.offsetMin = new Vector2(0f, 4f * css);
            }
            m._cardsRow.gameObject.SetActive(false);
            m.Big = MetaButton.Create(m._panel, "Big", MetaButton.Kind.Big, MetaButton.Tone.Green, "Giyili", new Vector2(303f, 56f), s.Lilita, 20f, s.Feel);
            m.Big.Rect.anchorMin = m.Big.Rect.anchorMax = new Vector2(0.5f, 0f); m.Big.Rect.pivot = new Vector2(0.5f, 0f);
            m.Big.Rect.anchoredPosition = new Vector2(0f, 12f * css);
            m.Big.Clicked += m.Buy;
            // celebration burst
            var burst = new GameObject("Burst", typeof(RectTransform));
            burst.transform.SetParent(rt, false);
            m._ring = (RectTransform)burst.transform;
            m._ring.anchorMin = m._ring.anchorMax = new Vector2(0.5f, 0.5f); m._ring.sizeDelta = Vector2.one * (60f * css);
            MetaUiKit.Picture(m._ring, "Ring", MetaStyle.Ring(70, MetaStyle.Gold, 4), 70f, new Vector2(0.5f, 0.5f), Vector2.zero);
            m._stars = new Image[12];
            for (int i = 0; i < 12; i++) m._stars[i] = MetaUiKit.Picture(m._ring, "Star" + i, MetaStyle.Circle(8, MetaStyle.GoldLight, MetaStyle.Gold, MetaStyle.Gold, 0), 8f, new Vector2(0.5f, 0.5f), Vector2.zero);
            burst.SetActive(false);
            m.Select(MarketCatalog.Category.Clothes);
            return m;
        }

        private static Sprite _jerseySprite;
        public const int JerseyLevel = 36;       // kahraman-maske.png: one grey level per part (jersey 36, shorts 72, hair 144, ball 216)

        /// <summary>Pota's kahraman-maske.png marks each part with a grey level; uGUI tints by alpha, so the jersey's level becomes a white sprite with alpha.</summary>
        public static Sprite JerseySprite(Sprite mask)
        {
            if (_jerseySprite != null) return _jerseySprite;
            if (mask == null || !mask.texture.isReadable) return null;
            var src = mask.texture;
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++) { byte a = (byte)(Mathf.Abs(px[i].r - JerseyLevel) <= 8 ? 255 : 0); px[i] = new Color32(255, 255, 255, a); }
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px); tex.Apply();
            _jerseySprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _jerseySprite;
        }

        private Chip MakeChip(int i, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var chip = new Chip();
            var go = new GameObject("Chip" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_chipsRow, false);
            chip.Rt = (RectTransform)go.transform;
            chip.Rt.anchorMin = chip.Rt.anchorMax = new Vector2(0.5f, 1f); chip.Rt.pivot = new Vector2(0.5f, 0.5f);
            chip.Rt.anchoredPosition = new Vector2((-134f + i * 67f) * css, -42f * css); chip.Rt.sizeDelta = new Vector2(62f, 84f) * css;
            chip.Card = go.GetComponent<Image>();
            chip.Card.sprite = MetaStyle.Panel(128, MetaStyle.R(14), Color.white, Color.white, MetaStyle.Gold, MetaStyle.R(3), 0f, 0);
            chip.Card.type = Image.Type.Sliced;
            chip.Icon = MetaUiKit.Picture(chip.Rt, "Icon", null, 50f, new Vector2(0.5f, 1f), new Vector2(0f, -4f));
            chip.Label = MetaUiKit.Label(chip.Rt, "Label", s.Lilita, "", 12f, MetaStyle.Navy, TextAnchor.LowerCenter);
            chip.Label.rectTransform.offsetMin = new Vector2(0f, 6f * css);
            chip.Hit = MetaButton.Create(chip.Rt, "Hit", MetaButton.Kind.Small, MetaButton.Tone.Navy, "", new Vector2(62f, 84f), s.Lilita, 12f, s.Feel);
            foreach (var img in chip.Hit.GetComponentsInChildren<Image>()) img.color = new Color(1f, 1f, 1f, 0f);
            chip.Hit.Rect.anchorMin = Vector2.zero; chip.Hit.Rect.anchorMax = Vector2.one; chip.Hit.Rect.offsetMin = chip.Hit.Rect.offsetMax = Vector2.zero;
            int index = i;
            chip.Hit.Clicked += () => TapChip(index);
            return chip;
        }

        /// <summary>The director wires spending (coins fly from the badge to a canvas point, counters roll down) and toasts.</summary>
        public void Bind(Action<int, Vector2, Action> spend, Action<string> toast, Func<int> coins) { _spend = spend; _toast = toast; _coinsOf = coins ?? _coinsOf; }

        public void Refresh(SampleSave save)
        {
            Coins.Set(save.Coins, false);
            Purchasing = false; HeroHopPeak = 0f; ShakePeak = 0f;
            Select(_category);
        }

        /// <summary>The badge and the chips read the save again (after a win elsewhere, on showing the page).</summary>
        public void Sync(int coins)
        {
            if (!Coins.Rolling) Coins.Set(coins, false);
            RefreshChips();
        }

        public void Select(MarketCatalog.Category category)
        {
            _category = category;
            for (int i = 0; i < _tabs.Length; i++) _tabs[i].SetTone(i == (int)category ? MetaButton.Tone.Orange : MetaButton.Tone.Navy);
            bool chips = category == MarketCatalog.Category.Clothes || category == MarketCatalog.Category.Balls;
            _chipsRow.gameObject.SetActive(chips);
            _cardsRow.gameObject.SetActive(!chips);
            Big.gameObject.SetActive(chips);
            if (chips)
            {
                var items = category == MarketCatalog.Category.Clothes ? MarketCatalog.Jerseys : MarketCatalog.Balls;
                for (int i = 0; i < _chips.Length; i++)
                {
                    _chips[i].Item = items[i];
                    _chips[i].Icon.sprite = category == MarketCatalog.Category.Clothes ? (_s.Clothes.Length > 0 ? _s.Clothes[0] : null) : _s.Ball;
                    _chips[i].Icon.color = category == MarketCatalog.Category.Clothes ? items[i].Colour : Color.Lerp(items[i].Colour, Color.white, 0.3f);
                }
                _tried = Array.FindIndex(items, it => it.Id == (category == MarketCatalog.Category.Clothes ? _wornJersey : _wornBall));
                RefreshChips();
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var t = _cardTitles[i];
                    if (category == MarketCatalog.Category.Boosters) { t.text = MarketCatalog.Boosters[i].name; t.transform.parent.Find("Note").GetComponent<Text>().text = MarketCatalog.Boosters[i].price + " para"; t.transform.parent.Find("Icon").GetComponent<Image>().sprite = _s.Boosters.Length > i ? _s.Boosters[i] : null; }
                    else { t.text = MarketCatalog.CoinPacks[i].name; t.transform.parent.Find("Note").GetComponent<Text>().text = MarketCatalog.CoinPacks[i].price; t.transform.parent.Find("Icon").GetComponent<Image>().sprite = i < 2 ? _s.CoinPile : _s.Heart; }
                }
            }
        }

        private bool Worn(MarketCatalog.Item it) => it.Id == _wornJersey || it.Id == _wornBall;

        private void RefreshChips()
        {
            for (int i = 0; i < _chips.Length; i++)
            {
                var c = _chips[i];
                c.Label.text = Worn(c.Item) ? "Giyili" : _owned.Contains(c.Item.Id) ? "Senin" : c.Item.Price.ToString();
                c.Card.color = i == _tried ? PotaLook.Hex(0xfff6dc) : Color.white;
                c.Card.sprite = MetaStyle.Panel(128, MetaStyle.R(14), Color.white, Color.white, i == _tried ? MetaStyle.Gold : Color.white, MetaStyle.R(3), 0f, 0);
            }
            if (_tried >= 0)
            {
                var it = _chips[_tried].Item;
                Big.Label.text = Worn(it) ? "Giyili" : _owned.Contains(it.Id) ? "Giy" : it.Name + " · " + it.Price;
                Big.SetTone(Worn(it) ? MetaButton.Tone.Grey : _owned.Contains(it.Id) ? MetaButton.Tone.Green : MetaButton.Tone.Orange);
            }
            _jersey.color = _category == MarketCatalog.Category.Clothes && _tried >= 0 ? Tint(_chips[_tried].Item.Colour) : Tint(JerseyColour(_wornJersey));
        }

        private static Color Tint(Color c) => new Color(c.r, c.g, c.b, 0.85f);
        private static Color JerseyColour(string id) { foreach (var j in MarketCatalog.Jerseys) if (j.Id == id) return j.Colour; return MarketCatalog.Jerseys[0].Colour; }

        /// <summary>Trying a chip: the hero shows it and hops 0.28 s, the part flashes 0.15 s (feel).</summary>
        public void TapChip(int i)
        {
            if (Purchasing || i < 0 || i >= _chips.Length) return;
            _tried = i;
            RefreshChips();
            _hopT = 0f; _hopTime = Hop;                              // Pota's zipla on a try-on
            if (_s.Feel.Enabled) _flashT = 0f;
        }

        /// <summary>The big button: wear what you own, buy what you can, shake when you cannot.</summary>
        public void Buy()
        {
            if (Purchasing || _tried < 0) return;
            var it = _chips[_tried].Item;
            if (Worn(it)) return;
            if (_owned.Contains(it.Id)) { Wear(it); RefreshChips(); _hopT = 0f; _hopTime = Celebrate; return; }      // Pota's kutla on a wear
            if (_coinsOf() < it.Price)
            {
                _shakeT = 0f; ShakePeak = 0f;                     // Pota shakes the button (yetmez)
                if (_s.Feel.Enabled) _blinkT = 0f;
                _toast?.Invoke("Para yetmiyor");
                return;
            }
            Purchasing = true;
            int chip = _tried;
            Action done = () =>
            {
                _owned.Add(it.Id);
                Wear(it);
                RefreshChips();
                Purchasing = false;
                _hopT = 0f; _hopTime = Celebrate;                 // Pota's kutla
                if (_s.Feel.Enabled) { _burstT = 0f; _burstChip = chip; _ring.anchoredPosition = MetaUiKit.CanvasPoint(_root, _chips[chip].Rt) - MetaUiKit.CanvasPoint(_root, (RectTransform)transform); }
            };
            if (_spend != null) _spend(it.Price, MetaUiKit.CanvasPoint(_root, _chips[chip].Rt), done);
            else done();
        }

        private void Wear(MarketCatalog.Item it) { if (it.Category == MarketCatalog.Category.Clothes) _wornJersey = it.Id; else _wornBall = it.Id; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime, css = MetaStyle.Css;
            _t += dt;
            bool feel = _s.Feel.Enabled;
            // breath (Pota vitrinNefes) and hops
            float hop = 0f, scale = 1f + 0.02f * (0.5f - 0.5f * Mathf.Cos(_t / Breath * 2f * Mathf.PI));
            if (_hopT >= 0f)
            {
                _hopT += dt;
                float u = Mathf.Clamp01(_hopT / Mathf.Max(0.01f, _hopTime));
                hop = Mathf.Sin(u * Mathf.PI) * (_hopTime > Hop ? 28f : 14f);
                HeroHopPeak = Mathf.Max(HeroHopPeak, hop);
                if (_hopT >= _hopTime) _hopT = -1f;
            }
            _hero.localScale = Vector3.one * scale;
            _hero.anchoredPosition = new Vector2(0f, (-HeroBottom + hop) * css);
            if (_flashT >= 0f)
            {
                _flashT += dt;
                float u = Mathf.Clamp01(_flashT / Flash);
                var c = _jersey.color; _jersey.color = Color.Lerp(Color.white, c, u);
                if (_flashT >= Flash) { _flashT = -1f; RefreshChips(); }
            }
            if (_shakeT >= 0f)
            {
                _shakeT += dt;
                float off = LoseWindow.Shake(_shakeT / ShakeTime);
                ShakePeak = Mathf.Max(ShakePeak, Mathf.Abs(off));
                Big.Rect.anchoredPosition = new Vector2(off * css, 12f * css);
                if (_shakeT >= ShakeTime) { _shakeT = -1f; Big.Rect.anchoredPosition = new Vector2(0f, 12f * css); }
            }
            if (_blinkT >= 0f)
            {
                _blinkT += dt;
                bool red = feel && ((int)(_blinkT / (BlinkTime / 4f)) % 2 == 0);
                _coinBadgeFace.color = red ? MetaStyle.Red : Color.white;
                if (_blinkT >= BlinkTime) { _blinkT = -1f; _coinBadgeFace.color = Color.white; }
            }
            if (_burstT >= 0f)
            {
                _burstT += dt;
                float u = Mathf.Clamp01(_burstT / 0.45f);
                _ring.gameObject.SetActive(u < 1f);
                var chip = _chips[_burstChip].Rt;
                chip.localScale = Vector3.one * (u < 0.5f ? Mathf.Lerp(1f, 1.2f, StudioCurves.EaseOutCubic(u * 2f)) : Mathf.Lerp(1.2f, 1f, (u - 0.5f) * 2f));
                var ring = _ring.GetChild(0) as RectTransform;
                ring.localScale = Vector3.one * StudioCurves.RingScale(u);
                ring.GetComponent<Image>().color = new Color(1f, 1f, 1f, StudioCurves.RingAlpha(u));
                for (int i = 0; i < _stars.Length; i++)
                {
                    float ang = i / 12f * Mathf.PI * 2f, r = Mathf.Lerp(16f, 60f, StudioCurves.EaseOutCubic(u)) * css;
                    _stars[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                    _stars[i].color = new Color(1f, 1f, 1f, 1f - u);
                }
                if (u >= 1f) { _burstT = -1f; chip.localScale = Vector3.one; }
            }
        }
    }
}
