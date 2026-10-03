using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>Pota's #ayarlar: three green toggles (Müzik, Ses, Titreşim), the language row and two version lines, in a MetaWindow.</summary>
    public static class SettingsWindow
    {
        public static MetaWindow Create(Transform parent, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var w = MetaWindow.Create(parent, "Settings", "Ayarlar", s.Lilita, s.Feel, true);
            var row = new GameObject("Toggles", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(w.Content, false);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 14f * css; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false;
            row.GetComponent<LayoutElement>().preferredHeight = 90f * css;
            foreach (var label in new[] { "Müzik", "Ses", "Titreşim" })
            {
                var cell = new GameObject(label, typeof(RectTransform));
                cell.transform.SetParent(row.transform, false);
                ((RectTransform)cell.transform).sizeDelta = new Vector2(70f, 90f) * css;
                var b = MetaButton.Create(cell.transform, "Toggle", MetaButton.Kind.Small, MetaButton.Tone.Green, "♪", new Vector2(62f, 62f), s.Lilita, 26f, s.Feel);
                b.Rect.anchorMin = b.Rect.anchorMax = new Vector2(0.5f, 1f); b.Rect.pivot = new Vector2(0.5f, 1f); b.Rect.anchoredPosition = Vector2.zero;
                bool on = true;
                b.Clicked += () => { on = !on; b.SetTone(on ? MetaButton.Tone.Green : MetaButton.Tone.Grey); };
                var t = Text(cell.transform, s, label, 14f, MetaStyle.Navy, new Vector2(0f, -70f * css));
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1f); t.rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            var lang = new GameObject("Language", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            lang.transform.SetParent(w.Content, false);
            var lh = lang.GetComponent<HorizontalLayoutGroup>(); lh.spacing = 8f * css; lh.childAlignment = TextAnchor.MiddleCenter; lh.childControlWidth = false; lh.childControlHeight = false; lh.childForceExpandWidth = false;
            lang.GetComponent<LayoutElement>().preferredHeight = 40f * css;
            var lt = Text(lang.transform, s, "Dil", 16f, MetaStyle.Navy, Vector2.zero);
            lt.rectTransform.sizeDelta = new Vector2(50f, 36f) * css;
            MetaButton.Create(lang.transform, "Turkish", MetaButton.Kind.Small, MetaButton.Tone.Green, "Türkçe", new Vector2(84f, 36f), s.FredokaBold != null ? s.FredokaBold : s.Lilita, 14f, s.Feel);
            MetaButton.Create(lang.transform, "English", MetaButton.Kind.Small, MetaButton.Tone.Grey, "English", new Vector2(84f, 36f), s.FredokaBold != null ? s.FredokaBold : s.Lilita, 14f, s.Feel);
            var v1 = Text(w.Content, s, "Swish & Smash · Unity vitrini", 12f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.6f), Vector2.zero);
            v1.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f * css;
            var v2 = Text(w.Content, s, "© 2026 Aethrode Studios", 12f, new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.5f), Vector2.zero);
            v2.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f * css;
            w.Fit();
            return w;
        }

        public static Text Text(Transform parent, MetaSettings s, string text, float sizeCss, Color color, Vector2 pos)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(300f, 24f) * MetaStyle.Css;
            var t = go.GetComponent<Text>();
            t.font = s.FredokaBold != null ? s.FredokaBold : s.Lilita; t.fontSize = Mathf.RoundToInt(sizeCss * MetaStyle.Css);
            t.alignment = TextAnchor.MiddleCenter; t.color = color; t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
