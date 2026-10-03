using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #bolumPencere: ribbon "Bölüm N", the HEDEF card (item + count), the CEPHANE and ÖDÜL cards, the best/medal line,
    /// two booster chips, "Oyna · N top" (orange) and "Basket at" (blue, ❤1). Opening/closing is the MetaWindow's (M1).
    /// </summary>
    public class LevelWindow : MonoBehaviour
    {
        private MetaSettings _s;
        private Text _goalCount, _ammo, _reward, _best;
        private Image _goalItem;

        public MetaWindow Window { get; private set; }
        public MetaButton Play { get; private set; }
        public MetaButton Basket { get; private set; }

        public static LevelWindow Create(Transform parent, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var w = MetaWindow.Create(parent, "LevelWindow", "Bölüm", s.Lilita, s.Feel, true);
            var lw = w.gameObject.AddComponent<LevelWindow>();
            lw._s = s; lw.Window = w;
            Font bold = s.FredokaBold != null ? s.FredokaBold : s.Lilita;
            var navy70 = new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f);
            // HEDEF card
            var goal = MetaUiKit.Row(w.Content, "Goal", 76f);
            MetaUiKit.Card(goal, "Card", Color.white);
            var gl = MetaUiKit.Label(goal, "Label", bold, "HEDEF", 11f, navy70, TextAnchor.UpperCenter);
            gl.rectTransform.offsetMax = new Vector2(0f, -8f * css);
            lw._goalItem = MetaUiKit.Picture(goal, "Item", s.Items.Length > 0 ? s.Items[0] : null, 34f, new Vector2(0.5f, 0f), new Vector2(-24f, 10f));
            lw._goalCount = MetaUiKit.Label(goal, "Count", s.Lilita, "12", 24f, MetaStyle.Orange2, TextAnchor.MiddleLeft, false);
            lw._goalCount.rectTransform.anchorMin = lw._goalCount.rectTransform.anchorMax = new Vector2(0.5f, 0f); lw._goalCount.rectTransform.pivot = new Vector2(0f, 0.5f);
            lw._goalCount.rectTransform.anchoredPosition = new Vector2(2f * css, 27f * css); lw._goalCount.rectTransform.sizeDelta = new Vector2(80f, 34f) * css;
            // CEPHANE / ÖDÜL cards
            var info = MetaUiKit.Row(w.Content, "Info", 70f);
            lw._ammo = Info(info, s, bold, 0f, 0.485f, s.Ball, "158", "CEPHANE");
            lw._reward = Info(info, s, bold, 0.515f, 1f, s.Coin, "+" + s.LevelReward, "ÖDÜL");
            // best / medal line
            var best = MetaUiKit.Row(w.Content, "Best", 20f);
            lw._best = MetaUiKit.Label(best, "Text", bold, "", 12f, navy70);
            // boosters
            var boosters = MetaUiKit.Row(w.Content, "Boosters", 56f);
            for (int i = 0; i < 2; i++)
            {
                var chip = new GameObject("Chip" + i, typeof(RectTransform), typeof(Image));
                chip.transform.SetParent(boosters, false);
                var crt = (RectTransform)chip.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2((i == 0 ? -34f : 34f) * css, 0f); crt.sizeDelta = Vector2.one * (52f * css);
                var cimg = chip.GetComponent<Image>();
                cimg.sprite = MetaStyle.Panel(128, MetaStyle.R(14), Color.white, Color.white, MetaStyle.Lock, MetaStyle.R(2), 0f, 0);
                cimg.type = Image.Type.Sliced; cimg.raycastTarget = false;
                MetaUiKit.Picture(crt, "Icon", s.Boosters.Length > i ? s.Boosters[i] : null, 36f, new Vector2(0.5f, 0.5f), Vector2.zero);
            }
            // buttons
            lw.Play = Big(w.Content, "Play", MetaButton.Tone.Orange, "Oyna · 158 top", s);
            lw.Basket = Big(w.Content, "Basket", MetaButton.Tone.Blue, "Basket at", s).WithIcon(s.Basket, 30f);
            var heart = new GameObject("Heart", typeof(RectTransform), typeof(Image));
            heart.transform.SetParent(lw.Basket.Face, false);
            var hrt = (RectTransform)heart.transform; hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0.5f); hrt.pivot = new Vector2(1f, 0.5f);
            hrt.anchoredPosition = new Vector2(-12f * css, 0f); hrt.sizeDelta = new Vector2(44f, 24f) * css;
            var himg = heart.GetComponent<Image>(); himg.sprite = MetaStyle.Panel(64, MetaStyle.R(12), MetaStyle.Red, MetaStyle.RedDark, MetaStyle.Red, 0, 0f, 0);
            himg.type = Image.Type.Sliced; himg.raycastTarget = false;
            MetaUiKit.Picture(hrt, "Icon", s.Heart, 16f, new Vector2(0f, 0.5f), new Vector2(5f, 0f));
            var ht = MetaUiKit.Label(hrt, "Text", bold, "1", 13f, Color.white, TextAnchor.MiddleRight); ht.rectTransform.offsetMax = new Vector2(-7f * css, 0f);
            w.Fit();
            return lw;
        }

        private static Text Info(RectTransform row, MetaSettings s, Font bold, float x0, float x1, Sprite icon, string value, string caption)
        {
            float css = MetaStyle.Css;
            var go = new GameObject(caption, typeof(RectTransform));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, 0f); rt.anchorMax = new Vector2(x1, 1f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            MetaUiKit.Card(rt, "Card", Color.white);
            MetaUiKit.Picture(rt, "Icon", icon, 38f, new Vector2(0f, 0.5f), new Vector2(12f, 0f));
            var v = MetaUiKit.Label(rt, "Value", s.Lilita, value, 22f, MetaStyle.Orange2, TextAnchor.LowerLeft);
            v.rectTransform.offsetMin = new Vector2(58f * css, 32f * css);
            var c = MetaUiKit.Label(rt, "Caption", bold, caption, 11f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f), TextAnchor.UpperLeft);
            c.rectTransform.offsetMin = new Vector2(58f * css, 0f); c.rectTransform.offsetMax = new Vector2(0f, -38f * css);
            return v;
        }

        /// <summary>A full-width big button in its own row.</summary>
        public static MetaButton Big(Transform content, string name, MetaButton.Tone tone, string label, MetaSettings s)
        {
            var row = MetaUiKit.Row(content, name + "Row", 56f);
            var b = MetaButton.Create(row, name, MetaButton.Kind.Big, tone, label, new Vector2(303f, 56f), s.Lilita, 20f, s.Feel);
            b.Rect.anchorMin = Vector2.zero; b.Rect.anchorMax = Vector2.one; b.Rect.offsetMin = b.Rect.offsetMax = Vector2.zero;
            return b;
        }

        public void Open(SampleSave save)
        {
            Window.Title.text = "Bölüm " + save.Level;
            _goalItem.sprite = _s.Items.Length > save.GoalItem && save.GoalItem >= 0 ? _s.Items[save.GoalItem] : null;
            _goalCount.text = save.GoalCount.ToString();
            _ammo.text = save.Ammo.ToString();
            _reward.text = "+" + (_s.LevelReward + (save.FirstTry ? _s.FirstTryReward : 0));
            _best.text = (save.BestScore > 0 ? "En iyi: " + save.BestScore : "En iyi: —") + "   ·   Gümüş " + save.MedalSilver + " · Altın " + save.MedalGold;
            Play.Label.text = "Oyna · " + save.Ammo + " top";
            Window.Show();
        }
    }
}
