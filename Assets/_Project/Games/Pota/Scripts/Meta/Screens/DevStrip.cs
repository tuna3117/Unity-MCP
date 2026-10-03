using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>The showcase's developer strip (not part of Pota): Kazan / Kaybet / Terfi / Bonus / Öğretici / Feel toggle, small navy chips at the very top.</summary>
    public class DevStrip : MonoBehaviour
    {
        private MetaDirector _d;
        private MetaSettings _s;
        private MetaButton _feel;
        public MetaButton Win, Lose, Promotion, Bonus, Tutorial, Feel;

        public static DevStrip Create(Transform parent, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var go = new GameObject("DevStrip", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -78f * css); rt.sizeDelta = new Vector2(0f, 26f * css);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 4f * css; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = false; h.childControlHeight = false; h.childForceExpandWidth = false;
            h.padding = new RectOffset(Mathf.RoundToInt(8f * css), Mathf.RoundToInt(8f * css), 0, 0);
            var d = go.AddComponent<DevStrip>();
            d._s = s;
            MetaButton Chip(string name, string label) => MetaButton.Create(rt, name, MetaButton.Kind.Small, MetaButton.Tone.Navy, label, new Vector2(56f, 24f), s.Lilita, 11f, s.Feel);
            d.Win = Chip("DevWin", "Kazan"); d.Lose = Chip("DevLose", "Kaybet"); d.Promotion = Chip("DevPromo", "Terfi");
            d.Bonus = Chip("DevBonus", "Bonus"); d.Tutorial = Chip("DevTutorial", "Öğretici"); d.Feel = Chip("DevFeel", "His: açık");
            return d;
        }

        public void Bind(MetaDirector d)
        {
            _d = d;
            Feel.Clicked -= ToggleFeel; Feel.Clicked += ToggleFeel;
            Feel.Label.text = _s.Feel.Enabled ? "His: açık" : "His: kapalı";
        }

        private void ToggleFeel()
        {
            _s.Feel.Enabled = !_s.Feel.Enabled;
            Feel.Label.text = _s.Feel.Enabled ? "His: açık" : "His: kapalı";
            _d?.Toast.Show(_s.Feel.Enabled ? "His katmanı açık" : "His katmanı kapalı (Pota)");
        }
    }
}
