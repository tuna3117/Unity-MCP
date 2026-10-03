using Project.ColorHunt;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #ligPencere: the league badge (96) with its name and trophies, then the five steps top-down (Şampiyonlar first) as white
    /// cards — threshold, ✓ for passed, the "SEN" pill on yours. M8: opened after a promotion, the SEN pill and the highlight slide
    /// from the old step to the new in 0.5 s. Feel off = Pota's still table.
    /// </summary>
    public class LeagueWindow : MonoBehaviour
    {
        public class Row { public int League; public RectTransform Rt; public Image Card; public Text Name, Right; }

        public const float SlideTime = 0.5f, RowCss = 44f, RowGapCss = 6f;
        private static readonly Color Highlight = PotaLook.Hex(0xfff6dc);

        private MetaSettings _s;
        private Image _icon;
        private Text _name, _trophies;
        private RectTransform _sen;
        private int _from, _to;
        private float _slide = -1f;

        public MetaWindow Window { get; private set; }
        public Row[] Rows { get; private set; }
        public bool SenMoving => _slide >= 0f;
        /// <summary>The league the SEN pill rests on (the new one once the slide ends).</summary>
        public int SenRow => _slide >= 0f ? _from : _to;

        public static LeagueWindow Create(Transform parent, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var w = MetaWindow.Create(parent, "LeagueWindow", "Lig", s.Lilita, s.Feel, true);
            var lw = w.gameObject.AddComponent<LeagueWindow>();
            lw._s = s; lw.Window = w;
            Font bold = s.FredokaBold != null ? s.FredokaBold : s.Lilita;
            var head = MetaUiKit.Row(w.Content, "Head", 150f);
            lw._icon = MetaUiKit.Picture(head, "Icon", s.LeagueBadges[0], 96f, new Vector2(0.5f, 1f), Vector2.zero);
            lw._name = MetaUiKit.Label(head, "Name", s.Lilita, "", 26f, MetaStyle.Navy, TextAnchor.LowerCenter);
            lw._name.rectTransform.offsetMin = new Vector2(0f, 22f * css);
            lw._trophies = MetaUiKit.Label(head, "Trophies", bold, "", 14f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f), TextAnchor.LowerCenter);
            int n = s.LeagueNames.Length;
            var table = MetaUiKit.Row(w.Content, "Table", n * RowCss + (n - 1) * RowGapCss);
            lw.Rows = new Row[n];
            for (int i = 0; i < n; i++)
            {
                int league = n - 1 - i;                                   // top row = the top league
                var r = new Row { League = league };
                var go = new GameObject("Row" + league, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(table, false);
                r.Rt = (RectTransform)go.transform;
                r.Rt.anchorMin = new Vector2(0f, 1f); r.Rt.anchorMax = new Vector2(1f, 1f); r.Rt.pivot = new Vector2(0.5f, 1f);
                r.Rt.anchoredPosition = new Vector2(0f, -i * (RowCss + RowGapCss) * css); r.Rt.sizeDelta = new Vector2(0f, RowCss * css);
                r.Card = go.GetComponent<Image>();
                r.Card.sprite = ColorHuntHud.PanelSprite(128, MetaStyle.R(14), Color.white, Color.white, MetaStyle.Gold, MetaStyle.R(2), 0f, 0);
                r.Card.type = Image.Type.Sliced; r.Card.raycastTarget = false;
                MetaUiKit.Picture(r.Rt, "Icon", s.LeagueBadges[Mathf.Min(league, s.LeagueBadges.Length - 1)], 40f, new Vector2(0f, 0.5f), new Vector2(8f, 0f));
                r.Name = MetaUiKit.Label(r.Rt, "Name", bold, s.LeagueNames[league], 15f, MetaStyle.Navy, TextAnchor.MiddleLeft);
                r.Name.rectTransform.offsetMin = new Vector2(56f * css, 0f);
                r.Right = MetaUiKit.Label(r.Rt, "Right", bold, "", 13f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f), TextAnchor.MiddleRight);
                r.Right.rectTransform.offsetMax = new Vector2(-12f * css, 0f);
                lw.Rows[i] = r;
            }
            // the SEN pill, moved between rows
            var sen = new GameObject("Sen", typeof(RectTransform), typeof(Image));
            sen.transform.SetParent(table, false);
            lw._sen = (RectTransform)sen.transform;
            lw._sen.anchorMin = lw._sen.anchorMax = new Vector2(1f, 1f); lw._sen.pivot = new Vector2(1f, 0.5f);
            lw._sen.sizeDelta = new Vector2(48f, 24f) * css;
            var simg = sen.GetComponent<Image>(); simg.sprite = ColorHuntHud.PanelSprite(64, MetaStyle.R(12), MetaStyle.OrangeLight, MetaStyle.Orange2, MetaStyle.Orange, 0, 0f, 0);
            simg.type = Image.Type.Sliced; simg.raycastTarget = false;
            MetaUiKit.Label(lw._sen, "Text", s.Lilita, "SEN", 12f, Color.white);
            w.Fit();
            return lw;
        }

        private Row RowOf(int league) { foreach (var r in Rows) if (r.League == league) return r; return Rows[Rows.Length - 1]; }
        private float RowY(int league) => RowOf(league).Rt.anchoredPosition.y - RowCss * 0.5f * MetaStyle.Css;

        /// <summary>Shows the table for <paramref name="trophies"/>; with <paramref name="fromLeague"/> different from the current one, the SEN pill slides (M8).</summary>
        public void Open(int trophies, int fromLeague = -1)
        {
            int league = _s.LeagueOf(trophies);
            _to = league; _from = fromLeague >= 0 ? fromLeague : league;
            _icon.sprite = _s.LeagueBadges[Mathf.Min(league, _s.LeagueBadges.Length - 1)];
            _name.text = _s.LeagueNames[league];
            _trophies.text = trophies + " kupa";
            foreach (var r in Rows)
            {
                r.Right.text = r.League < league ? "✓" : r.League == league ? "" : _s.LeagueThresholds[r.League] + " kupa";
                r.Right.color = r.League < league ? MetaStyle.Green : new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f);
            }
            bool slide = _s.Feel.Enabled && _from != _to;
            _slide = slide ? 0f : -1f;
            if (slide) RowOf(_from).Right.text = "";
            Apply(slide ? 0f : 1f);
            Window.Show();
        }

        private void Apply(float u)
        {
            float css = MetaStyle.Css;
            float y = Mathf.Lerp(RowY(_from), RowY(_to), u);
            _sen.anchoredPosition = new Vector2(-12f * css, y);
            foreach (var r in Rows)
            {
                float k = r.League == _to ? u : r.League == _from ? 1f - u : 0f;
                r.Card.color = Color.Lerp(Color.white, Highlight, k);
            }
        }

        private void Update()
        {
            if (_slide < 0f) return;
            if (!_s.Feel.Enabled) { _slide = -1f; Apply(1f); return; }
            _slide += Time.unscaledDeltaTime / SlideTime;
            float u = PotaLook.Smoothstep(Mathf.Min(1f, _slide));
            Apply(u);
            if (_slide >= 1f) { _slide = -1f; Apply(1f); }
        }
    }
}
