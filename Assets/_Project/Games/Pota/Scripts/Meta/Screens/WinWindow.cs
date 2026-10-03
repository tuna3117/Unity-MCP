using System;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #kazandi driven by a <see cref="WinSequence"/> (M4): "Harika!" stamp, score count-up with hops, the medal escalating
    /// with flashes, "Kupa +N", the league bar filling (glowing at the brim on a promotion), the "+15" coins leaving for the strip
    /// (M3, through the director), the reward card, Devam breathing. Confetti is Pota's. Real time.
    /// </summary>
    public class WinWindow : MonoBehaviour
    {
        private static readonly string[] MedalNames = { "Bronz", "Gümüş", "Altın" };

        private MetaSettings _s;
        private RectTransform _root;
        private Confetti _confetti;
        private Text _harika, _score, _lines, _medalName, _trophy, _leagueName, _next, _plus, _rewardCard;
        private Image _medal, _medalFlash, _leagueIcon, _barFill, _coin;
        private CanvasGroup _linesGroup, _rewardGroup;
        private float _flipT = -1f, _coinT = -1f;
        private bool _coinsSent;
        private Action<int, Vector2> _gainCoins;
        private int _leftover, _leftoverCoins;
        private bool _firstTry;

        public MetaWindow Window { get; private set; }
        public MetaButton Continue { get; private set; }
        public WinSequence Sequence { get; private set; }
        public float Elapsed { get; private set; }
        public int MedalShown { get; private set; } = -1;
        public string ScoreText => _score.text;
        public bool ContinuePulsing => Continue.Pulsing;
        public float BarFill => _barFill.rectTransform.anchorMax.x;
        public event Action Done;

        public static WinWindow Create(Transform parent, MetaSettings s, RectTransform root, Confetti confetti)
        {
            float css = MetaStyle.Css;
            var w = MetaWindow.Create(parent, "WinWindow", "Bölüm bitti", s.Lilita, s.Feel, false);
            w.Content.GetComponent<VerticalLayoutGroup>().spacing = 8f * css;
            var ww = w.gameObject.AddComponent<WinWindow>();
            ww._s = s; ww._root = root; ww._confetti = confetti; ww.Window = w;
            Font bold = s.FredokaBold != null ? s.FredokaBold : s.Lilita;
            var navy70 = new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f);
            // Harika!
            var hr = MetaUiKit.Row(w.Content, "Harika", 50f);
            ww._harika = MetaUiKit.GoldText(hr, "Text", s.Lilita, "Harika!", 44f);
            ww._harika.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            // score
            var sr = MetaUiKit.Row(w.Content, "Score", 64f);
            var sl = MetaUiKit.Label(sr, "Label", bold, "SKOR", 11f, navy70, TextAnchor.UpperCenter);
            sl.rectTransform.offsetMax = new Vector2(0f, -2f * css);
            ww._score = MetaUiKit.Label(sr, "Value", s.Lilita, "0", 44f, MetaStyle.Orange2, TextAnchor.LowerCenter);
            ww._score.rectTransform.anchorMin = new Vector2(0.5f, 0f); ww._score.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            ww._score.rectTransform.pivot = new Vector2(0.5f, 0.5f); ww._score.rectTransform.anchoredPosition = new Vector2(0f, 24f * css);
            ww._score.rectTransform.sizeDelta = new Vector2(200f, 48f) * css; ww._score.alignment = TextAnchor.MiddleCenter;
            // lines
            var lr = MetaUiKit.Row(w.Content, "Lines", 16f);
            ww._linesGroup = lr.gameObject.AddComponent<CanvasGroup>();
            ww._lines = MetaUiKit.Label(lr, "Text", bold, "", 11f, navy70);
            // medal
            var mr = MetaUiKit.Row(w.Content, "Medal", 44f);
            ww._medal = MetaUiKit.Picture(mr, "Image", s.Medals.Length > 0 ? s.Medals[0] : null, 40f, new Vector2(0.5f, 0.5f), new Vector2(-50f, 0f));
            ww._medalFlash = MetaUiKit.Picture(ww._medal.rectTransform, "Flash", null, 40f, new Vector2(0.5f, 0.5f), Vector2.zero);
            ww._medalFlash.sprite = MetaStyle.Circle(40, Color.white, Color.white, Color.white, 0); ww._medalFlash.color = new Color(1f, 1f, 1f, 0f);
            ww._medalName = MetaUiKit.Label(mr, "Name", bold, "", 18f, MetaStyle.Navy, TextAnchor.MiddleLeft);
            ww._medalName.rectTransform.offsetMin = new Vector2(0.5f * mr.rect.width - 22f * css, 0f);
            ww._medalName.rectTransform.anchorMin = new Vector2(0.5f, 0f); ww._medalName.rectTransform.offsetMin = new Vector2(-22f * css, 0f);
            // trophies
            ww._trophy = MetaUiKit.Panel(w.Content, "Trophy", bold, "Kupa +0", 36f, 15f);
            // league bar card
            var lc = MetaUiKit.Row(w.Content, "League", 72f);
            MetaUiKit.Card(lc, "Card", Color.white);
            ww._leagueIcon = MetaUiKit.Picture(lc, "Icon", s.LeagueBadges.Length > 0 ? s.LeagueBadges[0] : null, 44f, new Vector2(0f, 0.5f), new Vector2(12f, 0f));
            ww._leagueName = MetaUiKit.Label(lc, "Name", bold, "", 14f, MetaStyle.Navy, TextAnchor.UpperLeft);
            ww._leagueName.rectTransform.offsetMin = new Vector2(66f * css, 0f); ww._leagueName.rectTransform.offsetMax = new Vector2(-12f * css, -10f * css);
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(lc, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(0f, 0.5f);
            brt.offsetMin = new Vector2(66f * css, -5f * css + 2f * css); brt.offsetMax = new Vector2(-12f * css, 5f * css + 2f * css);
            var bimg = bar.GetComponent<Image>(); bimg.sprite = MetaStyle.Lip(5, PotaLook.Hex(0xe3e9f4)); bimg.type = Image.Type.Sliced; bimg.raycastTarget = false;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(brt, false);
            ww._barFill = fill.GetComponent<Image>();
            ww._barFill.rectTransform.anchorMin = Vector2.zero; ww._barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            ww._barFill.rectTransform.offsetMin = ww._barFill.rectTransform.offsetMax = Vector2.zero;
            ww._barFill.sprite = MetaStyle.Panel(64, MetaStyle.R(5), MetaStyle.Gold, MetaStyle.Orange, MetaStyle.Gold, 0, 0f, 0);
            ww._barFill.type = Image.Type.Sliced; ww._barFill.raycastTarget = false;
            ww._next = MetaUiKit.Label(lc, "Next", bold, "", 11f, navy70, TextAnchor.LowerLeft);
            ww._next.rectTransform.offsetMin = new Vector2(66f * css, 8f * css); ww._next.rectTransform.offsetMax = new Vector2(-12f * css, 0f);
            // reward
            var rr = MetaUiKit.Row(w.Content, "Reward", 64f);
            ww._coin = MetaUiKit.Picture(rr, "Coin", s.Coin, 64f, new Vector2(0.5f, 0.5f), new Vector2(-52f, 0f));
            ww._plus = MetaUiKit.GoldText(rr, "Plus", s.Lilita, "+15", 44f);
            ww._plus.alignment = TextAnchor.MiddleLeft; ww._plus.rectTransform.anchorMin = new Vector2(0.5f, 0f); ww._plus.rectTransform.offsetMin = new Vector2(-10f * css, 0f);
            ww._rewardCard = MetaUiKit.Panel(w.Content, "RewardCard", bold, "", 36f, 13f);
            ww._rewardGroup = ww._rewardCard.transform.parent.gameObject.AddComponent<CanvasGroup>();
            ww.Continue = LevelWindow.Big(w.Content, "Continue", MetaButton.Tone.Green, "Devam", s);
            ww.Continue.Clicked += () => ww.Done?.Invoke();
            w.Fit();
            return ww;
        }

        /// <summary>Opens over <paramref name="before"/> with the numbers of <paramref name="r"/>; <paramref name="gainCoins"/> sends the reward to the strip when its moment comes.</summary>
        public void Open(WinSequence seq, WinResult r, SampleSave before, Action<int, Vector2> gainCoins)
        {
            Sequence = seq; _gainCoins = gainCoins;
            _leftover = before.LeftoverBalls; _leftoverCoins = r.LeftoverCoins; _firstTry = before.FirstTry;
            Elapsed = 0f; _coinsSent = false; _flipT = -1f; _coinT = -1f; MedalShown = -1;
            Window.Title.text = "Bölüm " + before.Level + " bitti";
            _lines.text = $"{before.ScoreBlocks} blok · {before.ScoreChain} zincir · {before.ScoreSpecial} özel · {before.ScoreGold} altın · {before.ScoreLeft} kalan top" + (before.FirstTry ? " · ilk deneme ×1.2" : "");
            int league = _s.LeagueOf(before.Trophies);
            _leagueIcon.sprite = _s.LeagueBadges[Mathf.Min(league, _s.LeagueBadges.Length - 1)];
            _leagueName.text = _s.LeagueNames[Mathf.Min(league, _s.LeagueNames.Length - 1)];
            _plus.text = "+" + r.CoinsGained;
            _rewardCard.text = $"{_leftover} top arttı · +{_leftoverCoins} ödül" + (_firstTry ? " · ilk denemede!" : "");
            Window.Show();
            _confetti?.Burst();
            Apply(seq.Stage(0f));
        }

        private void Update()
        {
            if (Sequence == null || !Window.IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            Elapsed += dt;
            Apply(Sequence.Stage(Elapsed));
            bool feel = _s.Feel.Enabled;
            if (_flipT >= 0f)
            {
                _flipT = feel ? _flipT + dt / 0.15f : 1f;
                float u = Mathf.Min(1f, _flipT);
                _medal.rectTransform.localScale = Vector3.one * (u < 0.5f ? Mathf.Lerp(1f, 1.2f, StudioCurves.EaseOutCubic(u * 2f)) : Mathf.Lerp(1.2f, 1f, (u - 0.5f) * 2f));
                _medalFlash.color = new Color(1f, 1f, 1f, 0.9f * (1f - u));
                if (_flipT >= 1f) { _flipT = -1f; _medal.rectTransform.localScale = Vector3.one; _medalFlash.color = new Color(1f, 1f, 1f, 0f); }
            }
            if (_coinT >= 0f)
            {
                _coinT = feel ? _coinT + dt / 0.3f : 1f;
                _coin.rectTransform.localScale = Vector3.one * FeelCurves.Stamp(Mathf.Min(1f, _coinT), 1.2f, 1f);
                if (_coinT >= 1f) { _coinT = -1f; _coin.rectTransform.localScale = Vector3.one; }
            }
        }

        private void Apply(WinSequence.Frame f)
        {
            bool feel = _s.Feel.Enabled;
            _harika.gameObject.SetActive(f.Stamp > 0f);
            _harika.rectTransform.localScale = Vector3.one * f.Stamp;
            _score.text = f.Score.ToString();
            _score.rectTransform.localScale = Vector3.one * f.ScoreScale;
            _linesGroup.alpha = f.LinesAlpha;
            if (f.Medal != MedalShown)
            {
                if (MedalShown >= 0 && feel) _flipT = 0f;
                MedalShown = f.Medal;
                _medal.gameObject.SetActive(f.Medal >= 0);
                _medalName.gameObject.SetActive(f.Medal >= 0);
                if (f.Medal >= 0)
                {
                    _medal.sprite = _s.Medals.Length > f.Medal ? _s.Medals[f.Medal] : null;
                    _medalName.text = MedalNames[f.Medal] + " madalya";
                }
            }
            int gain = Sequence.TrophiesAfter - Sequence.TrophiesBefore;
            _trophy.text = gain > 0 ? "Kupa +" + f.TrophyGainShown : "En iyi: " + Sequence.Score;      // Pota: no gain without a new best
            if (_flipT < 0f) _medal.rectTransform.localScale = Vector3.one * f.MedalPop;
            _barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(f.Bar), 1f);
            _barFill.color = f.BarOverflow ? Color.Lerp(Color.white, MetaStyle.Gold, 0.5f + 0.5f * Mathf.Sin(Elapsed * 8f)) : Color.white;
            int toNext = MetaRules.ToNextLeague(_s, f.TrophiesShown);
            _next.text = toNext > 0 ? "Sonraki lige " + toNext + " kupa" : "En üst lig";
            if (f.CoinsFlying && !_coinsSent)
            {
                _coinsSent = true;
                if (feel) _coinT = 0f;
                _gainCoins?.Invoke(Sequence.CoinsGained, MetaUiKit.CanvasPoint(_root, _coin.rectTransform));
            }
            _rewardGroup.alpha = f.RewardAlpha;
            Continue.Pulse(f.ContinuePulse);
        }
    }
}
