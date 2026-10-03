using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>Small builders the windows share: fixed-height rows for a MetaWindow's vertical layout, labels, Pota's .panel card, images.</summary>
    public static class MetaUiKit
    {
        /// <summary>A row of <paramref name="heightCss"/> under a vertical layout (the layout sets the width, keeps this height).</summary>
        public static RectTransform Row(Transform parent, string name, float heightCss)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(0f, heightCss * MetaStyle.Css);
            return rt;
        }

        /// <summary>A text filling <paramref name="parent"/> (anchors 0..1) unless <paramref name="fill"/> is false.</summary>
        public static Text Label(Transform parent, string name, Font font, string text, float sizeCss, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool fill = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (fill) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = Mathf.RoundToInt(sizeCss * MetaStyle.Css); t.alignment = align; t.color = color; t.text = text;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Pota's gold display text (h1 "Harika!"): gold fill, brown outline, 5 css drop shadow.</summary>
        public static Text GoldText(Transform parent, string name, Font font, string text, float sizeCss)
        {
            var t = Label(parent, name, font, text, sizeCss, MetaStyle.Gold);
            var o = t.gameObject.AddComponent<Outline>(); o.effectColor = MetaStyle.Gold2; o.effectDistance = new Vector2(2f, -2f) * MetaStyle.Css;
            var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = MetaStyle.Gold2; sh.effectDistance = new Vector2(0f, -5f * MetaStyle.Css);
            return t;
        }

        /// <summary>An image of <paramref name="sizeCss"/> anchored at <paramref name="anchor"/> with <paramref name="offsetCss"/>.</summary>
        public static Image Picture(Transform parent, string name, Sprite sprite, float sizeCss, Vector2 anchor, Vector2 offsetCss)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = offsetCss * MetaStyle.Css;
            rt.sizeDelta = Vector2.one * (sizeCss * MetaStyle.Css);
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            return img;
        }

        /// <summary>Pota's .panel: a white rounded card (radius 14) with navy text.</summary>
        public static Text Panel(Transform parent, string name, Font font, string text, float heightCss, float fontCss = 14f)
        {
            var row = Row(parent, name, heightCss);
            var img = row.gameObject.AddComponent<Image>();
            img.sprite = ColorHunt.ColorHuntHud.PanelSprite(128, MetaStyle.R(14), Color.white, Color.white, Color.white, 0, 0f, 0);
            img.type = Image.Type.Sliced; img.raycastTarget = false;
            return Label(row, "Text", font, text, fontCss, MetaStyle.Navy);
        }

        /// <summary>A rounded card (radius <paramref name="radiusCss"/>) of <paramref name="color"/> filling its parent.</summary>
        public static Image Card(Transform parent, string name, Color color, int radiusCss = 14)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = ColorHunt.ColorHuntHud.PanelSprite(128, MetaStyle.R(radiusCss), color, color, color, 0, 0f, 0);
            img.type = Image.Type.Sliced; img.raycastTarget = false;
            return img;
        }

        /// <summary>A canvas point (root space, centre origin) of a rect's centre.</summary>
        public static Vector2 CanvasPoint(RectTransform root, RectTransform t) => root.InverseTransformPoint(t.TransformPoint(t.rect.center));
    }
}
