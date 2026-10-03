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
        public enum Screen { Menu, Map, Market, Level, Win, Lose, League, Promotion, Bonus, Settings }

        public MetaSettings S;
        [SerializeField] private RectTransform _root;
        [SerializeField] private MenuScreen _menu;
        [SerializeField] private TabBar _tabs;
        [SerializeField] private RectTransform _mapPage, _marketPage;
        [SerializeField] private MetaWindow _settings;
        [SerializeField] private Toast _toast;
        [SerializeField] private CoinFlight _coins;
        [SerializeField] private DevStrip _dev;

        private SampleSave _save;
        private ScreenSlide _mapSlide, _marketSlide;

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
        public event Action<Screen> ScreenChanged;

        /// <summary>The scene tool hands over the canvas; the screens are built on Play (Awake).</summary>
        public void SetRoot(RectTransform root) => _root = root;

        /// <summary>Builds every screen under <paramref name="root"/> at runtime.</summary>
        public void Build(RectTransform root)
        {
            _root = root;
            _menu = MenuScreen.Create(root, S);
            _mapPage = Page(root, "MapPage", S.MapBackground, new Color(126f / 255f, 195f / 255f, 240f / 255f, 0.3f));
            _marketPage = Page(root, "MarketPage", null, new Color(20f / 255f, 40f / 255f, 80f / 255f, 0.82f));
            _tabs = TabBar.Create(root, S);
            _settings = SettingsWindow.Create(root, S);
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
            _menu.Refresh(_save, false);
            _menu.Enter();
            _dev?.Bind(this);
        }

        /// <summary>The sample save again (tests, the recorder).</summary>
        public void ResetSave()
        {
            _save = MetaRules.Copy(S.Save);
            _menu.Refresh(_save, false);
        }

        private void OnTab(TabBar.Tab tab)
        {
            switch (tab)
            {
                case TabBar.Tab.Map: _marketSlide.Hide(); _mapSlide.Show(); Set(Screen.Map); break;
                case TabBar.Tab.Market: _mapSlide.Hide(); _marketSlide.Show(); Set(Screen.Market); break;
                default: _mapSlide.Hide(); _marketSlide.Hide(); Set(Screen.Menu); break;
            }
        }

        private void Set(Screen s)
        {
            Current = s;
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
