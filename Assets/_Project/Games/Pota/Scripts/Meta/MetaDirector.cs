using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// The showcase's flow over the sample save (spec 2026-10-03 meta feel §3): Menu ↔ Map / Market, the level window, a simulated
    /// round result from the dev strip, the win and lose windows, league/promotion, bonus, settings, toast. Every screen is built by
    /// <see cref="Build"/> (the scene tool calls it once in Edit Mode) and bound again at runtime in <see cref="Bind"/>.
    /// </summary>
    public class MetaDirector : MonoBehaviour
    {
        public enum Screen { Menu, Map, Market, Level, Game, Win, Lose, League, Promotion, Bonus, Settings }

        public MetaSettings S;
        [SerializeField] private RectTransform _root;
        [SerializeField] private MenuScreen _menu;
        [SerializeField] private TabBar _tabs;
        [SerializeField] private RectTransform _mapPage, _marketPage;
        [SerializeField] private MetaWindow _settings;
        [SerializeField] private Toast _toast;
        [SerializeField] private CoinFlight _coins;
        [SerializeField] private DevStrip _dev;
        [SerializeField] private LevelWindow _level;
        [SerializeField] private WinWindow _win;
        [SerializeField] private LoseWindow _lose;
        [SerializeField] private Confetti _confetti;
        [SerializeField] private Image _fade;
        [SerializeField] private MapScreen _map;
        [SerializeField] private MarketScreen _market;
        [SerializeField] private LeagueWindow _league;
        [SerializeField] private PromotionWindow _promotion;
        [SerializeField] private BonusWindow _bonus;
        private int _leagueSeen;
        private Action _afterPromotion;

        private SampleSave _save;
        private ScreenSlide _mapSlide, _marketSlide;
        private WinResult _lastWin;
        private float _fadeT = -1f;

        public Screen Current { get; private set; } = Screen.Menu;
        public SampleSave Save => _save;
        public RectTransform Root => _root;
        public MenuScreen Menu => _menu;
        public TabBar Tabs => _tabs;
        public ScreenSlide MapPage => _mapSlide;
        public ScreenSlide MarketPage => _marketSlide;
        public MetaWindow Settings => _settings;
        public Toast Toast => _toast;
        public CoinFlight Coins => _coins;
        public DevStrip Dev => _dev;
        public LevelWindow Level => _level;
        public WinWindow Win => _win;
        public LoseWindow Lose => _lose;
        public Confetti Confetti => _confetti;
        public WinResult LastWin => _lastWin;
        public MapScreen Map => _map;
        public MarketScreen Market => _market;
        public LeagueWindow League => _league;
        public PromotionWindow Promotion => _promotion;
        public BonusWindow Bonus => _bonus;
        public event Action<Screen> ScreenChanged;

        /// <summary>The scene tool hands over the canvas; the screens are built on Play (Awake).</summary>
        public void SetRoot(RectTransform root) => _root = root;

        /// <summary>Builds every screen under <paramref name="root"/> at runtime.</summary>
        public void Build(RectTransform root)
        {
            _root = root;
            _menu = MenuScreen.Create(root, S);
            _mapPage = Page(root, "MapPage", S.MapBackground, new Color(126f / 255f, 195f / 255f, 240f / 255f, 0.3f));
            _map = MapScreen.Create(_mapPage, S);
            _marketPage = Page(root, "MarketPage", null, new Color(20f / 255f, 40f / 255f, 80f / 255f, 0.82f));
            _market = MarketScreen.Create(_marketPage, S, root);
            _tabs = TabBar.Create(root, S);
            _settings = SettingsWindow.Create(root, S);
            _level = LevelWindow.Create(root, S);
            _confetti = Confetti.Create(root);
            _win = WinWindow.Create(root, S, root, _confetti);
            _lose = LoseWindow.Create(root, S);
            _league = LeagueWindow.Create(root, S);
            _promotion = PromotionWindow.Create(root, S, _confetti);
            _bonus = BonusWindow.Create(root, S, root);
            _fade = Page(root, "Fade", null, Color.black).GetComponent<Image>();
            _fade.gameObject.SetActive(false);
            _toast = Toast.Create(root, S.Lilita, S.Feel);
            _coins = CoinFlight.Create(root, S.Coin, S.Feel);
            _dev = DevStrip.Create(root, S);
            _tabs.transform.SetAsLastSibling();
            _dev.transform.SetAsLastSibling();
        }

        private static RectTransform Page(RectTransform root, string name, Sprite background, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = background; img.color = background != null ? Color.white : tint; img.raycastTarget = true;
            if (background != null)
            {
                var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image));
                shade.transform.SetParent(rt, false);
                var srt = (RectTransform)shade.transform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
                shade.GetComponent<Image>().color = tint; shade.GetComponent<Image>().raycastTarget = false;
            }
            return rt;
        }

        private void Awake()
        {
            if (_menu == null && _root != null && S != null) Build(_root);
            Bind();
        }

        /// <summary>Runtime wiring (editor-added listeners are not saved with the scene).</summary>
        public void Bind()
        {
            if (S == null || _menu == null) return;
            _save = MetaRules.Copy(S.Save);
            _mapSlide = _mapPage.GetComponent<ScreenSlide>() ?? ScreenSlide.Attach(_mapPage, S.Feel);
            _marketSlide = _marketPage.GetComponent<ScreenSlide>() ?? ScreenSlide.Attach(_marketPage, S.Feel);
            _tabs.Selected -= OnTab; _tabs.Selected += OnTab;
            _menu.Gear.Clicked -= OpenSettings; _menu.Gear.Clicked += OpenSettings;
            _menu.Play.Clicked -= OpenLevel; _menu.Play.Clicked += OpenLevel;
            _level.Play.Clicked -= StartGame; _level.Play.Clicked += StartGame;
            _level.Window.Closed -= OnLevelClosed; _level.Window.Closed += OnLevelClosed;
            _win.Done -= OnWinDone; _win.Done += OnWinDone;
            _lose.Window.Closed -= OnLoseClosed; _lose.Window.Closed += OnLoseClosed;
            _lose.Recommended.Clicked -= RetryLevel; _lose.Recommended.Clicked += RetryLevel;
            foreach (var b in new[] { _lose.Basket, _lose.More, _lose.Video }) { b.Clicked -= CloseLose; b.Clicked += CloseLose; }
            _menu.LeagueBadge.Clicked -= OpenLeagueSeen; _menu.LeagueBadge.Clicked += OpenLeagueSeen;
            _promotion.Done -= OnPromotionDone; _promotion.Done += OnPromotionDone;
            _bonus.FlyCoins = (amount, from, to, done) => _coins.Fly(from, to, amount, null, done);
            _bonus.Window.Closed -= OnBonusClosed; _bonus.Window.Closed += OnBonusClosed;
            _market.Bind(SpendCoins, msg => _toast.Show(msg));
            _leagueSeen = S.LeagueOf(_save.Trophies);
            _menu.Refresh(_save, false);
            _map.Refresh(_save);
            _market.Refresh(_save);
            _menu.Enter();
            _dev?.Bind(this);
            if (_dev != null)
            {
                _dev.Win.Clicked -= ShowWin; _dev.Win.Clicked += ShowWin;
                _dev.Lose.Clicked -= ShowLose; _dev.Lose.Clicked += ShowLose;
                _dev.Promotion.Clicked -= ShowPromotion; _dev.Promotion.Clicked += ShowPromotion;
                _dev.Bonus.Clicked -= ShowBonus; _dev.Bonus.Clicked += ShowBonus;
            }
        }

        // ---------- league, promotion, bonus, market ----------

        /// <summary>The league table; <paramref name="fromLeague"/> ≥ 0 and different slides the SEN row (M8).</summary>
        public void OpenLeague(int fromLeague = -1)
        {
            _league.Open(_save.Trophies, fromLeague);
            _leagueSeen = S.LeagueOf(_save.Trophies);
            Set(Screen.League);
            _league.Window.Closed -= OnSettingsClosed; _league.Window.Closed += OnSettingsClosed;
        }

        private void OpenLeagueSeen() => OpenLeague(_leagueSeen);

        /// <summary>The dev strip's "Terfi": the trophies jump to the next league's floor and the promotion window plays.</summary>
        public void ShowPromotion()
        {
            int l = S.LeagueOf(_save.Trophies);
            if (l >= S.LeagueThresholds.Length - 1) { _toast.Show("En üst lig"); return; }
            _save.Trophies = S.LeagueThresholds[l + 1];
            _afterPromotion = null;
            _promotion.Open(l, l + 1);
            Set(Screen.Promotion);
        }

        private void OnPromotionDone()
        {
            _promotion.Window.Hide(() =>
            {
                _menu.Refresh(_save, true);
                var next = _afterPromotion; _afterPromotion = null;
                if (next != null) next();
                else OnSettingsClosed();
            });
        }

        public void ShowBonus()
        {
            _bonus.Open(BonusRules.Start(S.BonusColumns, S.BonusSeed >= 0 ? S.BonusSeed : BonusRules.ShowcaseSeed));
            Set(Screen.Bonus);
        }

        private void OnBonusClosed() => OnSettingsClosed();

        /// <summary>Coins spent: they fly from the market's badge to <paramref name="to"/>, both counters rolling down (M3 spend).</summary>
        public void SpendCoins(int amount, Vector2 to, Action done)
        {
            int target = _save.Coins - amount, shown = _save.Coins, count = CoinPlan.Count(amount);
            _save.Coins = target;
            if (count == 0 || !S.Feel.Enabled) { _menu.Coins.Set(target, false); _market.Coins.Set(target, false); done?.Invoke(); return; }
            int step = Mathf.Max(1, amount / count), arrived = 0;
            _coins.Fly(_market.CoinsPoint, to, amount,
                () => { arrived++; shown = arrived >= count ? target : Mathf.Max(target, shown - step); _market.Coins.Set(shown, true); _menu.Coins.Set(shown, true); },
                () => { _market.Coins.Set(target, true); _menu.Coins.Set(target, true); done?.Invoke(); });
        }

        // ---------- level → "game" → result ----------

        public void OpenLevel()
        {
            _level.Open(_save);
            Set(Screen.Level);
        }

        private void OnLevelClosed() { if (Current == Screen.Level) Set(Screen.Menu); }

        /// <summary>"Oyna": the box shrinks, the screen fades to black and back (M2) — the showcase has no round; the dev strip gives the result.</summary>
        private void StartGame()
        {
            Set(Screen.Game);
            _level.Window.Hide();
            if (S.Feel.Enabled) { _fadeT = 0f; _fade.gameObject.SetActive(true); _fade.color = new Color(0f, 0f, 0f, 0f); }
            else EndGameFade();
        }

        private void EndGameFade()
        {
            _fadeT = -1f;
            _fade.gameObject.SetActive(false);
            Set(Screen.Menu);
            _toast.Show("Oyun burada oynanır — sonucu şeritten seç: Kazan / Kaybet");
        }

        private void Update()
        {
            if (_fadeT < 0f) return;
            _fadeT += Time.unscaledDeltaTime;
            float f = Mathf.Max(0.01f, S.Feel.PlayFade), hold = 0.25f;
            float a = _fadeT < f ? _fadeT / f : _fadeT < f + hold ? 1f : 1f - (_fadeT - f - hold) / f;
            _fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
            if (_fadeT >= 2f * f + hold || !S.Feel.Enabled) EndGameFade();
        }

        /// <summary>The dev strip's "Kazan": Pota's win over the current save, shown by the win window's sequence.</summary>
        public void ShowWin()
        {
            var before = _save;
            int score = MetaRules.Score(before);
            var r = MetaRules.Win(S, before, score, before.LeftoverBalls);
            var seq = new WinSequence(S, score, before.MedalSilver, before.MedalGold, before.Trophies, r.Save.Trophies, r.CoinsGained);
            _lastWin = r;
            _save = r.Save;
            _save.Coins = before.Coins;                     // the flight adds the coins (GainCoins)
            _win.Open(seq, r, before, (amount, from) => GainCoins(amount, from));
            Set(Screen.Win);
        }

        /// <summary>Devam: with the feel layer the map slides in and plays the progression (M6); without it Pota goes home.</summary>
        private void OnWinDone()
        {
            _win.Window.Hide(() =>
            {
                _menu.Refresh(_save, true);
                if (_lastWin.Promoted)
                {
                    _afterPromotion = AfterWin;                 // M4: the promotion window comes after Devam, then the map
                    _promotion.Open(_lastWin.LeagueBefore, _lastWin.LeagueAfter);
                    Set(Screen.Promotion);
                }
                else AfterWin();
            });
        }

        private void AfterWin()
        {
            if (S.Feel.Enabled)
            {
                _map.Refresh(_save);
                _tabs.Select(TabBar.Tab.Map);
                _map.Play(_lastWin.Save.Level - 1, _lastWin.Save.Level, _lastWin.MedalIndex);
            }
            else
            {
                _map.Refresh(_save);
                _tabs.Select(TabBar.Tab.Home);
                Set(Screen.Menu);
            }
        }

        /// <summary>The dev strip's "Kaybet": a life goes, the lose window shows what remained.</summary>
        public void ShowLose()
        {
            _save = MetaRules.Lose(_save);
            _lose.Open(_save.RemainingOnLoss);
            Set(Screen.Lose);
        }

        private void OnLoseClosed()
        {
            _menu.Refresh(_save, true);
            if (Current == Screen.Lose) Set(Screen.Menu);
        }

        private void RetryLevel() => _lose.Window.Hide(OpenLevel);
        private void CloseLose() => _lose.Window.Hide();

        /// <summary>The sample save again (tests, the recorder).</summary>
        public void ResetSave()
        {
            _save = MetaRules.Copy(S.Save);
            _leagueSeen = S.LeagueOf(_save.Trophies);
            _menu.Refresh(_save, false);
            _map.Refresh(_save);
            _market.Refresh(_save);
        }

        private void OnTab(TabBar.Tab tab)
        {
            switch (tab)
            {
                case TabBar.Tab.Map: _marketSlide.Hide(); _mapSlide.Show(); Set(Screen.Map); break;
                case TabBar.Tab.Market: _mapSlide.Hide(); _marketSlide.Show(); Set(Screen.Market); break;
                default: _mapSlide.Hide(); _marketSlide.Hide(); Set(Screen.Menu); break;
            }
            _tabs.transform.SetAsLastSibling();             // a shown page moves to the top; the bar stays over the pages
            _dev?.transform.SetAsLastSibling();
        }

        private void Set(Screen s)
        {
            Current = s;
            _dev?.transform.SetAsLastSibling();             // the strip stays above every window
            ScreenChanged?.Invoke(s);
        }

        public void OpenSettings()
        {
            _settings.Show();
            Set(Screen.Settings);
            _settings.Closed -= OnSettingsClosed; _settings.Closed += OnSettingsClosed;
        }

        private void OnSettingsClosed() => Set(_tabs.Current == TabBar.Tab.Home ? Screen.Menu : _tabs.Current == TabBar.Tab.Map ? Screen.Map : Screen.Market);

        /// <summary>Coins gained: fly from a canvas point to the strip's coin badge, the counter stepping on every arrival.</summary>
        public void GainCoins(int amount, Vector2 fromCanvas, Action done = null)
        {
            int target = _save.Coins + amount, shown = _save.Coins, count = CoinPlan.Count(amount);
            _save.Coins = target;
            if (count == 0) { _menu.Coins.Set(target, true); done?.Invoke(); return; }
            int step = Mathf.Max(1, amount / count), arrived = 0;
            _coins.Fly(fromCanvas, _menu.CoinsPoint, amount,
                () => { arrived++; shown = arrived >= count ? target : Mathf.Min(target, shown + step); _menu.Coins.Set(shown, true); },
                () => { _menu.Coins.Set(target, true); done?.Invoke(); });
        }
    }
}
