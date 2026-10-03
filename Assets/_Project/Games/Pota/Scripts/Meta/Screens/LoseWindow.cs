using UnityEngine;
using UnityEngine.UI;

namespace Project.Meta
{
    /// <summary>
    /// Pota's #kaybetti (M5): the box shakes ±4 css for LoseShake as it opens, the remaining count stamps in (0 → 1.3 → 1), a red
    /// "Kıl payı!" tag when ≤ 3 remained, the recommended (orange) button breathes after LosePulseAt. Feel off = Pota's still window.
    /// </summary>
    public class LoseWindow : MonoBehaviour
    {
        public const int NearMissAtMost = 3;
        private const float StampTime = 0.3f;

        private MetaSettings _s;
        private Text _remaining, _nearMiss;
        private float _t = -1f;

        public MetaWindow Window { get; private set; }
        public MetaButton Recommended { get; private set; }
        public MetaButton Basket { get; private set; }
        public MetaButton More { get; private set; }
        public MetaButton Video { get; private set; }
        public string RemainingText => _remaining.text;
        public bool NearMissShown => _nearMiss.transform.parent.gameObject.activeSelf;
        /// <summary>The box's current shake offset in css px.</summary>
        public float ShakeOffset { get; private set; }
        /// <summary>The largest shake offset applied since the window opened (css px).</summary>
        public float ShakePeak { get; private set; }

        public static bool NearMiss(int remaining) => remaining > 0 && remaining <= NearMissAtMost;

        /// <summary>Pota's salla keyframes: 0 → −4 → 4 → −4 → 4 → 0 over the shake time.</summary>
        public static float Shake(float u) => u <= 0f || u >= 1f ? 0f : 4f * Mathf.Sin(u * 4f * Mathf.PI) * -1f;

        public static LoseWindow Create(Transform parent, MetaSettings s)
        {
            float css = MetaStyle.Css;
            var w = MetaWindow.Create(parent, "LoseWindow", "Cephane bitti", s.Lilita, s.Feel, true);
            var lw = w.gameObject.AddComponent<LoseWindow>();
            lw._s = s; lw.Window = w;
            Font bold = s.FredokaBold != null ? s.FredokaBold : s.Lilita;
            var navy70 = new Color(MetaStyle.Navy.r, MetaStyle.Navy.g, MetaStyle.Navy.b, 0.7f);
            var panel = MetaUiKit.Row(w.Content, "Remaining", 100f);
            var card = panel.gameObject.AddComponent<Image>();
            card.sprite = ColorHunt.ColorHuntHud.PanelSprite(128, MetaStyle.R(14), Color.white, Color.white, Color.white, 0, 0f, 0);
            card.type = Image.Type.Sliced; card.raycastTarget = false;
            lw._remaining = MetaUiKit.Label(panel, "Count", s.Lilita, "0", 52f, MetaStyle.Orange2, TextAnchor.MiddleCenter, false);
            lw._remaining.rectTransform.anchorMin = lw._remaining.rectTransform.anchorMax = new Vector2(0.5f, 1f); lw._remaining.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            lw._remaining.rectTransform.anchoredPosition = new Vector2(0f, -36f * css); lw._remaining.rectTransform.sizeDelta = new Vector2(200f, 60f) * css;
            var sub = MetaUiKit.Label(panel, "Sub", bold, "hedef tamamlanmadı", 13f, navy70, TextAnchor.LowerCenter);
            sub.rectTransform.offsetMin = new Vector2(0f, 10f * css);
            var tag = new GameObject("NearMiss", typeof(RectTransform), typeof(Image));
            tag.transform.SetParent(panel, false);
            var nrt = (RectTransform)tag.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 1f); nrt.pivot = new Vector2(0.5f, 0.5f);
            nrt.anchoredPosition = new Vector2(70f * css, -14f * css); nrt.sizeDelta = new Vector2(96f, 28f) * css; nrt.localRotation = Quaternion.Euler(0f, 0f, 6f);
            var tbimg = tag.GetComponent<Image>(); tbimg.sprite = MetaStyle.Lip(10, MetaStyle.Red); tbimg.type = Image.Type.Sliced; tbimg.raycastTarget = false;
            lw._nearMiss = MetaUiKit.Label(nrt, "Text", s.Lilita, "Kıl payı!", 18f, Color.white);
            var outline = lw._nearMiss.gameObject.AddComponent<Outline>(); outline.effectColor = MetaStyle.RedDark; outline.effectDistance = new Vector2(1.5f, -1.5f) * css;
            lw.Recommended = LevelWindow.Big(w.Content, "Retry", MetaButton.Tone.Orange, "Yeniden dene", s);
            Heart(lw.Recommended, s, bold);
            lw.Basket = LevelWindow.Big(w.Content, "Basket", MetaButton.Tone.Blue, "Basket at", s).WithIcon(s.Basket, 30f);
            lw.More = LevelWindow.Big(w.Content, "More", MetaButton.Tone.Green, "+16 top · 100", s).WithIcon(s.Coin, 26f);
            lw.Video = LevelWindow.Big(w.Content, "Video", MetaButton.Tone.Blue, "▶ Video izle · +16 top", s);
            w.Fit();
            return lw;
        }

        private static void Heart(MetaButton b, MetaSettings s, Font bold)
        {
            float css = MetaStyle.Css;
            var heart = new GameObject("Heart", typeof(RectTransform), typeof(Image));
            heart.transform.SetParent(b.Face, false);
            var hrt = (RectTransform)heart.transform; hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0.5f); hrt.pivot = new Vector2(1f, 0.5f);
            hrt.anchoredPosition = new Vector2(-12f * css, 0f); hrt.sizeDelta = new Vector2(44f, 24f) * css;
            var himg = heart.GetComponent<Image>(); himg.sprite = ColorHunt.ColorHuntHud.PanelSprite(64, MetaStyle.R(12), MetaStyle.Red, MetaStyle.RedDark, MetaStyle.Red, 0, 0f, 0);
            himg.type = Image.Type.Sliced; himg.raycastTarget = false;
            MetaUiKit.Picture(hrt, "Icon", s.Heart, 16f, new Vector2(0f, 0.5f), new Vector2(5f, 0f));
            var ht = MetaUiKit.Label(hrt, "Text", bold, "1", 13f, Color.white, TextAnchor.MiddleRight); ht.rectTransform.offsetMax = new Vector2(-7f * css, 0f);
        }

        public void Open(int remaining)
        {
            _remaining.text = remaining.ToString();
            _nearMiss.transform.parent.gameObject.SetActive(NearMiss(remaining));
            _t = 0f; ShakeOffset = ShakePeak = 0f;
            Recommended.Pulse(false);
            Window.Show();
            Apply();
        }

        private void Update()
        {
            if (_t < 0f) return;
            if (!Window.IsOpen) { _t = -1f; Window.Box.anchoredPosition = Vector2.zero; Recommended.Pulse(false); return; }
            _t += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            var feel = _s.Feel;
            if (!feel.Enabled)
            {
                ShakeOffset = 0f; Window.Box.anchoredPosition = Vector2.zero; _remaining.rectTransform.localScale = Vector3.one; Recommended.Pulse(false);
                return;
            }
            ShakeOffset = Shake(_t / Mathf.Max(0.01f, feel.LoseShake));
            ShakePeak = Mathf.Max(ShakePeak, Mathf.Abs(ShakeOffset));
            Window.Box.anchoredPosition = new Vector2(ShakeOffset * MetaStyle.Css, 0f);
            float us = Mathf.Clamp01(_t / StampTime);
            _remaining.rectTransform.localScale = Vector3.one * (us < 0.4f ? Mathf.Lerp(0f, 1.3f, ColorHunt.StudioCurves.EaseOutCubic(us / 0.4f)) : Mathf.Lerp(1.3f, 1f, ColorHunt.PotaLook.Smoothstep((us - 0.4f) / 0.6f)));
            Recommended.Pulse(_t >= feel.LosePulseAt);
        }
    }
}
