using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #sekmeler: navy gradient bar with a gold top edge and three tabs (Market, Ana, Harita); the selected tab's icon lifts
    /// 14 css px and grows 1.25 over a gold card (M2: eased over TabLift, with a 1.08 overshoot on the card; Feel off = instant).
    /// </summary>
    public class TabBar : MonoBehaviour
    {
        public enum Tab { Market, Home, Map }

        private class Entry { public Tab Tab; public RectTransform Root, Icon, Card; public Text Label; public MetaButton Button; public float U; }

        private MetaFeelTuning _feel;
        private readonly Entry[] _entries = new Entry[3];
        public Tab Current { get; private set; } = Tab.Home;
        public event Action<Tab> Selected;
        public RectTransform Rect => (RectTransform)transform;
        public RectTransform RectOf(Tab tab) => _entries[(int)tab].Root;

        public const float HeightCss = 86f;

        public static TabBar Create(Transform parent, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("TabBar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, (HeightCss + 14f) * css);
            var bg = go.GetComponent<Image>();
            bg.sprite = ColorHunt.ColorHuntHud.PanelSprite(128, 1, MetaStyle.PanelTop, MetaStyle.Navy, MetaStyle.PanelTop, 0, 0f, 0);
            bg.type = Image.Type.Sliced;
            var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(rt, false);
            var ert = (RectTransform)edge.transform;
            ert.anchorMin = new Vector2(0f, 1f); ert.anchorMax = new Vector2(1f, 1f); ert.pivot = new Vector2(0.5f, 1f);
            ert.sizeDelta = new Vector2(0f, 4f * css);
            edge.GetComponent<Image>().color = MetaStyle.Gold;
            var bar = go.AddComponent<TabBar>();
            bar._feel = s.Feel;
            string[] labels = { "Market", "Ana", "Harita" };
            for (int i = 0; i < 3; i++)
            {
                var e = new Entry { Tab = (Tab)i };
                var root = new GameObject(labels[i], typeof(RectTransform));
                root.transform.SetParent(rt, false);
                e.Root = (RectTransform)root.transform;
                e.Root.anchorMin = new Vector2(i / 3f, 0f); e.Root.anchorMax = new Vector2((i + 1) / 3f, 1f);
                e.Root.offsetMin = e.Root.offsetMax = Vector2.zero;
                var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
                card.transform.SetParent(e.Root, false);
                e.Card = (RectTransform)card.transform;
                e.Card.anchorMin = new Vector2(0.1f, 0f); e.Card.anchorMax = new Vector2(0.9f, 1f);
                e.Card.offsetMin = new Vector2(0f, 12f * css); e.Card.offsetMax = new Vector2(0f, 16f * css);
                var cimg = card.GetComponent<Image>();
                cimg.sprite = ColorHunt.ColorHuntHud.PanelSprite(128, MetaStyle.R(14), MetaStyle.GoldLight, MetaStyle.GoldDark, Color.white, MetaStyle.R(3), 0f, 0);
                cimg.type = Image.Type.Sliced; cimg.raycastTarget = false;
                e.Button = MetaButton.Create(e.Root, "Hit", MetaButton.Kind.Small, MetaButton.Tone.Navy, "", new Vector2(100f, 80f), s.Lilita, 13f, s.Feel);
                foreach (var img in e.Button.GetComponentsInChildren<Image>()) { img.color = new Color(1f, 1f, 1f, 0f); }
                e.Button.Rect.anchorMin = Vector2.zero; e.Button.Rect.anchorMax = Vector2.one; e.Button.Rect.offsetMin = e.Button.Rect.offsetMax = Vector2.zero;
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Shadow));
                icon.transform.SetParent(e.Root, false);
                e.Icon = (RectTransform)icon.transform;
                e.Icon.anchorMin = e.Icon.anchorMax = new Vector2(0.5f, 1f); e.Icon.pivot = new Vector2(0.5f, 1f);
                e.Icon.anchoredPosition = new Vector2(0f, -10f * css);
                e.Icon.sizeDelta = Vector2.one * (50f * css);
                var iimg = icon.GetComponent<Image>();
                iimg.sprite = s.TabIcons != null && i < s.TabIcons.Length ? s.TabIcons[i] : null;
                iimg.preserveAspect = true; iimg.raycastTarget = false;
                var sh = icon.GetComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.35f); sh.effectDistance = new Vector2(0f, -4f * css);
                var lgo = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Shadow));
                lgo.transform.SetParent(e.Root, false);
                var lrt = (RectTransform)lgo.transform;
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
                lrt.anchoredPosition = new Vector2(0f, 18f * css); lrt.sizeDelta = new Vector2(0f, 18f * css);
                e.Label = lgo.GetComponent<Text>();
                e.Label.font = s.Lilita; e.Label.fontSize = Mathf.RoundToInt(13f * css); e.Label.alignment = TextAnchor.MiddleCenter;
                e.Label.color = Color.white; e.Label.text = labels[i]; e.Label.raycastTarget = false;
                var lsh = lgo.GetComponent<Shadow>(); lsh.effectColor = new Color(0f, 0f, 0f, 0.35f); lsh.effectDistance = new Vector2(0f, -2f * css);
                var tab = e.Tab;
                e.Button.Clicked += () => bar.Select(tab);
                bar._entries[i] = e;
            }
            bar.Apply(true);
            return bar;
        }

        public void Select(Tab tab)
        {
            if (tab == Current) { Selected?.Invoke(tab); return; }
            Current = tab;
            if (!_feel.Enabled) Apply(true);
            Selected?.Invoke(tab);
        }

        private void Update()
        {
            if (_feel.Enabled) Apply(false);
        }

        private void Apply(bool instant)
        {
            float css = MetaStyle.Css;
            float dt = Time.unscaledDeltaTime / Mathf.Max(0.01f, _feel.TabLift);
            foreach (var e in _entries)
            {
                if (e == null) continue;
                float target = e.Tab == Current ? 1f : 0f;
                e.U = instant ? target : Mathf.MoveTowards(e.U, target, dt);
                float k = ColorHunt.StudioCurves.EaseOutCubic(e.U);
                e.Icon.anchoredPosition = new Vector2(0f, (-10f + 14f * k) * css);
                e.Icon.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, k);
                float card = e.U < 0.6f ? Mathf.Lerp(0f, 1.08f, ColorHunt.StudioCurves.EaseOutCubic(e.U / 0.6f)) : Mathf.Lerp(1.08f, 1f, (e.U - 0.6f) / 0.4f);
                e.Card.localScale = new Vector3(card, card, 1f);
                e.Card.gameObject.SetActive(e.U > 0.001f);
                e.Label.color = Color.Lerp(new Color(1f, 1f, 1f, 0.8f), MetaStyle.Navy, k);
                e.Label.rectTransform.anchoredPosition = new Vector2(0f, (18f - 6f * k) * css);
                e.Icon.SetAsLastSibling(); e.Label.transform.SetAsLastSibling();
            }
        }
    }
}
