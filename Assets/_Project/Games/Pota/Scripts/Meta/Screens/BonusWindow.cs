using System;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #bonus with M9: n reels × 3 cells, the spin on Pota's curve, each reel squashing as it stops and its cells popping;
    /// winning cells glow gold (the rest dim), the winnings' coins fly to the result card (M3), "Bu sefer olmadı" shakes the card.
    /// Winnings wait for the level (Pota). Feel off = Pota's spin only.
    /// </summary>
    public class BonusWindow : MonoBehaviour
    {
        private class Reel { public RectTransform Column, Strip; public RectTransform[] Cells; public Image[] Faces; }

        public const float CellGapCss = 4f, CardShake = 0.3f;

        private MetaSettings _s;
        private RectTransform _root, _grid;
        private Reel[] _reels;
        private Text _result, _note;
        private RectTransform _card;
        private BonusRules.State _state;
        private BonusRules.Winnings _added;
        private ReelSpin _spin;
        private float _t = -1f, _cellCss, _glowT = -1f, _shakeT = -1f;
        private bool[] _winning;
        private System.Random _filler = new System.Random(3);

        public MetaWindow Window { get; private set; }
        public MetaButton Play { get; private set; }
        public MetaButton Close { get; private set; }
        public bool Spinning => _t >= 0f;
        public int StoppedColumns { get; private set; }
        public BonusRules.Winnings Winnings => _state != null ? _state.Winnings : default;
        public int GlowingCells { get; private set; }
        public bool CoinsFlew { get; private set; }
        public string ResultText => _result.text;
        public float CardShakePeak { get; private set; }
        /// <summary>Set by the director: coins fly from a canvas point to another (no counter), then <c>done</c>.</summary>
        public Action<int, Vector2, Vector2, Action> FlyCoins;

        public static BonusWindow Create(Transform parent, MetaSettings s, RectTransform root)
        {
            float css = MetaStyle.Css;
            var w = MetaWindow.Create(parent, "BonusWindow", "BONUS", s.Lilita, s.Feel, false, 360f);
            var bw = w.gameObject.AddComponent<BonusWindow>();
            bw._s = s; bw._root = root; bw.Window = w;
            Font bold = s.FredokaBold != null ? s.FredokaBold : s.Lilita;
            int cols = Mathf.Max(1, s.BonusColumns);
            bw._cellCss = Mathf.Min(56f, (320f - (cols - 1) * CellGapCss) / cols);
            bw._grid = MetaUiKit.Row(w.Content, "Grid", bw._cellCss * 3f + CellGapCss * 2f);
            bw._reels = new Reel[cols];
            float totalW = cols * bw._cellCss + (cols - 1) * CellGapCss;
            for (int c = 0; c < cols; c++)
            {
                var reel = new Reel();
                var col = new GameObject("Reel" + c, typeof(RectTransform), typeof(RectMask2D));
                col.transform.SetParent(bw._grid, false);
                reel.Column = (RectTransform)col.transform;
                reel.Column.anchorMin = reel.Column.anchorMax = new Vector2(0.5f, 0.5f); reel.Column.pivot = new Vector2(0.5f, 0.5f);
                reel.Column.anchoredPosition = new Vector2((-totalW / 2f + bw._cellCss / 2f + c * (bw._cellCss + CellGapCss)) * css, 0f);
                reel.Column.sizeDelta = new Vector2(bw._cellCss, bw._cellCss * 3f + CellGapCss * 2f) * css;
                var strip = new GameObject("Strip", typeof(RectTransform));
                strip.transform.SetParent(reel.Column, false);
                reel.Strip = (RectTransform)strip.transform;
                reel.Strip.anchorMin = new Vector2(0f, 1f); reel.Strip.anchorMax = new Vector2(1f, 1f); reel.Strip.pivot = new Vector2(0.5f, 1f);
                int n = ReelSpin.TravelRows + 3;
                reel.Strip.sizeDelta = new Vector2(0f, (n * bw._cellCss + (n - 1) * CellGapCss) * css);
                reel.Cells = new RectTransform[n]; reel.Faces = new Image[n];
                for (int i = 0; i < n; i++)
                {
                    var cell = new GameObject("Cell" + i, typeof(RectTransform), typeof(Image));
                    cell.transform.SetParent(reel.Strip, false);
                    var crt = (RectTransform)cell.transform;
                    crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 0.5f);
                    crt.anchoredPosition = new Vector2(0f, -(i * (bw._cellCss + CellGapCss) + bw._cellCss / 2f) * css);
                    crt.sizeDelta = new Vector2(0f, bw._cellCss * css);
                    var img = cell.GetComponent<Image>();
                    img.sprite = ColorHuntHud.PanelSprite(128, MetaStyle.R(12), Color.white, Color.white, MetaStyle.Gold, MetaStyle.R(2), 0f, 0);
                    img.type = Image.Type.Sliced; img.raycastTarget = false;
                    MetaUiKit.Picture(crt, "Icon", null, bw._cellCss * 0.7f, new Vector2(0.5f, 0.5f), Vector2.zero);
                    var t = MetaUiKit.Label(crt, "Text", s.Lilita, "", bw._cellCss * 0.4f, MetaStyle.Orange2);
                    t.gameObject.SetActive(false);
                    reel.Cells[i] = crt; reel.Faces[i] = img;
                }
                bw._reels[c] = reel;
            }
            bw._result = MetaUiKit.Panel(w.Content, "Result", bold, "Butona bas, şeritler dönsün", 40f, 14f);
            bw._card = (RectTransform)bw._result.transform.parent;
            bw._note = MetaUiKit.Label(MetaUiKit.Row(w.Content, "Note", 16f), "Text", bold, "Kazanç beklemede: bölümü geçince alınır", 11f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.6f));
            bw.Play = LevelWindow.Big(w.Content, "Play", MetaButton.Tone.Orange, "BONUS OYNA · 3", s);
            bw.Play.Clicked += bw.Spin;
            bw.Close = LevelWindow.Big(w.Content, "Close", MetaButton.Tone.Green, "Kapat", s);
            bw.Close.Clicked += () => w.Hide();
            w.Fit();
            return bw;
        }

        public void Open(BonusRules.State state)
        {
            _state = state;
            _t = -1f; _glowT = -1f; _shakeT = -1f; CoinsFlew = false; GlowingCells = 0; StoppedColumns = 0; CardShakePeak = 0f;
            for (int c = 0; c < _reels.Length; c++)
            {
                for (int i = 0; i < _reels[c].Cells.Length; i++) Fill(_reels[c], i, i >= ReelSpin.TravelRows ? state.Cells[c * state.Rows + i - ReelSpin.TravelRows] : RandomCell(), 1f, Color.white);
                _reels[c].Strip.anchoredPosition = new Vector2(0f, Travel());
                _reels[c].Column.localScale = Vector3.one;
            }
            _result.text = "Butona bas, şeritler dönsün";
            _card.anchoredPosition = new Vector2(0f, _card.anchoredPosition.y);
            Labels();
            Window.Show();
        }

        private float Travel() => ReelSpin.TravelRows * (_cellCss + CellGapCss) * MetaStyle.Css;

        private BonusRules.Cell RandomCell()
        {
            double v = _filler.NextDouble();
            return v < 0.12 ? new BonusRules.Cell { Kind = BonusRules.Kind.Coin } : v < 0.2 ? new BonusRules.Cell { Kind = BonusRules.Kind.Clock }
                : v < 0.28 ? new BonusRules.Cell { Kind = BonusRules.Kind.Right } : new BonusRules.Cell { Kind = BonusRules.Kind.Item, Colour = _filler.Next(BonusRules.Colours) };
        }

        private void Fill(Reel reel, int i, BonusRules.Cell cell, float alpha, Color tint)
        {
            var icon = reel.Cells[i].Find("Icon").GetComponent<Image>();
            var text = reel.Cells[i].Find("Text").GetComponent<Text>();
            icon.gameObject.SetActive(cell.Kind != BonusRules.Kind.Right);
            text.gameObject.SetActive(cell.Kind == BonusRules.Kind.Right);
            switch (cell.Kind)
            {
                case BonusRules.Kind.Item: icon.sprite = _s.Items.Length > cell.Colour ? _s.Items[cell.Colour] : null; icon.color = Color.white; break;
                case BonusRules.Kind.Coin: icon.sprite = _s.Coin; icon.color = Color.white; break;
                case BonusRules.Kind.Clock: icon.sprite = _s.Heart; icon.color = Color.white; break;
                case BonusRules.Kind.Right: text.text = "+" + BonusRules.RightExtra; break;
            }
            reel.Faces[i].color = new Color(tint.r, tint.g, tint.b, alpha);
            reel.Cells[i].localScale = Vector3.one;
        }

        private void Labels()
        {
            Play.Label.text = "BONUS OYNA · " + _state.Rights;
            Play.Interactable = !BonusRules.Over(_state) && !Spinning;
            Play.SetTone(Play.Interactable ? MetaButton.Tone.Orange : MetaButton.Tone.Grey);
        }

        /// <summary>One spin: the rules decide the result, the reels show it.</summary>
        public void Spin()
        {
            if (_state == null || Spinning || BonusRules.Over(_state)) return;
            _state = BonusRules.Spin(_state, out _added);
            _winning = BonusRules.Winning(_state.Cells);
            for (int c = 0; c < _reels.Length; c++)
            {
                for (int i = 0; i < _reels[c].Cells.Length; i++) Fill(_reels[c], i, i >= ReelSpin.TravelRows ? _state.Cells[c * _state.Rows + i - ReelSpin.TravelRows] : RandomCell(), 1f, Color.white);
                _reels[c].Strip.anchoredPosition = Vector2.zero;
            }
            _spin = new ReelSpin(_s.Feel, _reels.Length);
            _t = 0f; StoppedColumns = 0; GlowingCells = 0; _glowT = -1f; _shakeT = -1f;
            _result.text = "";
            Labels();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_t >= 0f)
            {
                _t += dt;
                int stopped = 0;
                for (int c = 0; c < _reels.Length; c++)
                {
                    var r = _reels[c];
                    r.Strip.anchoredPosition = new Vector2(0f, _spin.Offset(c, _t) * Travel());
                    r.Column.localScale = new Vector3(1f, _spin.Squash(c, _t), 1f);
                    float pop = _spin.Pop(c, _t);
                    for (int i = ReelSpin.TravelRows; i < r.Cells.Length; i++) r.Cells[i].localScale = Vector3.one * pop;
                    if (_spin.Stopped(c, _t)) stopped++;
                }
                StoppedColumns = stopped;
                if (_t >= _spin.Done) Finish();
            }
            if (_glowT >= 0f)
            {
                _glowT += dt;
                float pulse = 1f + 0.1f * Mathf.Abs(Mathf.Sin(_glowT * Mathf.PI * 2f));     // pop ×2 over a second
                for (int c = 0; c < _reels.Length; c++)
                    for (int i = 0; i < _state.Rows; i++)
                        if (_winning[c * _state.Rows + i]) _reels[c].Cells[ReelSpin.TravelRows + i].localScale = Vector3.one * (_glowT < 1f ? pulse : 1f);
                if (_glowT >= 1f) _glowT = -1f;
            }
            if (_shakeT >= 0f)
            {
                _shakeT += dt;
                float off = _s.Feel.Enabled ? LoseWindow.Shake(_shakeT / CardShake) : 0f;
                CardShakePeak = Mathf.Max(CardShakePeak, Mathf.Abs(off));
                _card.anchoredPosition = new Vector2(off * MetaStyle.Css, _card.anchoredPosition.y);
                if (_shakeT >= CardShake) { _shakeT = -1f; _card.anchoredPosition = new Vector2(0f, _card.anchoredPosition.y); }
            }
        }

        private void Finish()
        {
            _t = -1f;
            bool feel = _s.Feel.Enabled;
            for (int c = 0; c < _reels.Length; c++)
            {
                _reels[c].Column.localScale = Vector3.one;
                _reels[c].Strip.anchoredPosition = new Vector2(0f, Travel());
                for (int i = 0; i < _state.Rows; i++)
                {
                    bool win = _winning[c * _state.Rows + i];
                    var face = _reels[c].Faces[ReelSpin.TravelRows + i];
                    face.color = win ? MetaStyle.GoldLight : new Color(1f, 1f, 1f, _added.Any || _added.ExtraRights > 0 ? 0.4f : 1f);
                    _reels[c].Cells[ReelSpin.TravelRows + i].localScale = Vector3.one;
                }
            }
            GlowingCells = 0; foreach (var w in _winning) if (w) GlowingCells++;
            if (GlowingCells > 0) _glowT = 0f;
            _result.text = BonusRules.Text(_added);
            Labels();
            if (_added.Coins > 0 && feel && FlyCoins != null)
            {
                Vector2 from = Vector2.zero; int found = 0;
                for (int c = 0; c < _reels.Length && found == 0; c++)
                    for (int i = 0; i < _state.Rows; i++)
                        if (_state.Cells[c * _state.Rows + i].Kind == BonusRules.Kind.Coin) { from = MetaUiKit.CanvasPoint(_root, _reels[c].Cells[ReelSpin.TravelRows + i]); found++; break; }
                CoinsFlew = true;
                _result.text = "";                                   // the card reads the winnings when the coins land
                FlyCoins(_added.Coins, from, MetaUiKit.CanvasPoint(_root, _card), () => _result.text = BonusRules.Text(_state.Winnings));
            }
            else _result.text = _state.Spins > 0 && !_added.Any && _added.ExtraRights == 0 ? "Bu sefer olmadı" : BonusRules.Text(_state.Winnings);
            if (!_added.Any && _added.ExtraRights == 0 && feel) { _shakeT = 0f; CardShakePeak = 0f; }
        }
    }
}
