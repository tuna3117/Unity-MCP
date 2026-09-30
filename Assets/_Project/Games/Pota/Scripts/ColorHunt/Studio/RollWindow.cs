using UnityEngine;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// A clip window for a rolling HUD number: the text moves into a RectMask2D the size of its own rect (padded sideways), so the old
    /// number slides out of its slot instead of over the panel's label. The caller enables the mask only under studio looks.
    /// </summary>
    internal static class RollWindow
    {
        public static RectMask2D Wrap(Text text, float padXCss)
        {
            var rt = text.rectTransform;
            var go = new GameObject(text.name + "Window", typeof(RectTransform), typeof(RectMask2D));
            var w = (RectTransform)go.transform;
            w.SetParent(rt.parent, false);
            w.SetSiblingIndex(rt.GetSiblingIndex());
            w.anchorMin = rt.anchorMin;
            w.anchorMax = rt.anchorMax;
            w.pivot = rt.pivot;
            var pad = new Vector2(padXCss * ColorHuntHud.Css, 0f);
            w.sizeDelta = rt.sizeDelta + 2f * pad;
            w.anchoredPosition = rt.anchoredPosition + Vector2.Scale(2f * rt.pivot - Vector2.one, pad);
            rt.SetParent(w, true);
            return go.GetComponent<RectMask2D>();
        }
    }
}
