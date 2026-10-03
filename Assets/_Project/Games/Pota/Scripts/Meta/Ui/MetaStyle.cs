using Project.ColorHunt;
using UnityEngine;

namespace Project.Meta
{
    /// <summary>
    /// Pota's UI palette, sizes and the kit's pure curves (docs/reference/2026-10-03-pota-meta-ui-style.md §0). Canvas units are
    /// 1080-wide pixels; a CSS px of the 390-px phone is <see cref="Css"/> of them, like ColorHuntHud.
    /// </summary>
    public static class MetaStyle
    {
        public const float Css = 1080f / 390f;

        // :root
        public static readonly Color Navy = PotaLook.Hex(0x142850), Navy2 = PotaLook.Hex(0x1f3a6e), PanelTop = PotaLook.Hex(0x24407a);
        public static readonly Color Orange = PotaLook.Hex(0xff7a1a), Orange2 = PotaLook.Hex(0xd95a05), OrangeLight = PotaLook.Hex(0xffb46b), OrangeShadow = PotaLook.Hex(0x8f3a00);
        public static readonly Color Gold = PotaLook.Hex(0xffc83d), Gold2 = PotaLook.Hex(0xb3600a), GoldLight = PotaLook.Hex(0xffd66b), GoldDark = PotaLook.Hex(0xe0a12a);
        public static readonly Color Green = PotaLook.Hex(0x2fb36b), Green2 = PotaLook.Hex(0x176b3e), GreenLight = PotaLook.Hex(0x7ae3a0), GreenDark = PotaLook.Hex(0x219a58);
        public static readonly Color Blue = PotaLook.Hex(0x2b55c7), BlueLight = PotaLook.Hex(0x5b86e0), BlueDark = PotaLook.Hex(0x1d3f8f), BlueShadow = PotaLook.Hex(0x122a63);
        public static readonly Color Cream = PotaLook.Hex(0xfff4e0), CreamTop = PotaLook.Hex(0xfff8ea), Sky = PotaLook.Hex(0x8ec9f2);
        public static readonly Color Red = PotaLook.Hex(0xff6b6b), RedDark = PotaLook.Hex(0xd92b36), RedShadow = PotaLook.Hex(0x7a1219);
        public static readonly Color Grey = PotaLook.Hex(0x7d8aa3), GreyDark = PotaLook.Hex(0x4e5a70), Lock = PotaLook.Hex(0xc9d2e3);
        public static readonly Color Backdrop = new Color(8f / 255f, 16f / 255f, 40f / 255f, 0.62f);
        public static readonly Color HintText = PotaLook.Hex(0x103055);

        // ---------- M1 curves (u = 0..1) ----------
        /// <summary>A pressed button sinks to 0.94.</summary>
        public static float PressScale(float u) => Mathf.Lerp(1f, 0.94f, StudioCurves.EaseOutCubic(Mathf.Clamp01(u)));

        /// <summary>Release: 0.94 → 1.04 (at 45 %) → 1.</summary>
        public static float ReleaseScale(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.45f) return Mathf.Lerp(0.94f, 1.04f, StudioCurves.EaseOutCubic(u / 0.45f));
            return Mathf.Lerp(1.04f, 1f, PotaLook.Smoothstep((u - 0.45f) / 0.55f));
        }

        /// <summary>A window opens 0.85 → 1.05 (at 60 %) → 1.</summary>
        public static float WindowOpen(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.6f) return Mathf.Lerp(0.85f, 1.05f, StudioCurves.EaseOutCubic(u / 0.6f));
            return Mathf.Lerp(1.05f, 1f, PotaLook.Smoothstep((u - 0.6f) / 0.4f));
        }

        /// <summary>A window closes 1 → 0.9 (alpha goes with the caller).</summary>
        public static float WindowClose(float u) => Mathf.Lerp(1f, 0.9f, Mathf.Clamp01(u));

        /// <summary>The ribbon title falls from −30 css px to home with a 10 % overshoot.</summary>
        public static float RibbonDropCss(float u)
        {
            u = Mathf.Clamp01(u);
            if (u < 0.7f) return Mathf.Lerp(-30f, 3f, StudioCurves.EaseOutCubic(u / 0.7f));
            return Mathf.Lerp(3f, 0f, PotaLook.Smoothstep((u - 0.7f) / 0.3f));
        }

        /// <summary>The overlay darkens to Pota's rgba(8,16,40,.62).</summary>
        public static float BackdropAlpha(float u) => Mathf.Lerp(0f, 0.62f, Mathf.Clamp01(u));

        /// <summary>Pota kupaKisa: ≥ 10 000 → "13,5K" (one decimal, Turkish comma), ≥ 100 000 → "154K".</summary>
        public static string KupaKisa(int trophies)
        {
            if (trophies < 10000) return trophies.ToString();
            if (trophies < 100000) return (trophies / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "K";
            return (trophies / 1000) + "K";
        }

        // ---------- sprites (ColorHuntHud's generators, Pota's numbers) ----------
        public static Sprite Box() => ColorHuntHud.PanelSprite(128, R(30), CreamTop, Cream, Gold, R(4), 0f, 0);
        public static Sprite BoxInner() => ColorHuntHud.PanelSprite(128, R(27), Color.clear, Color.clear, Color.white, R(3), 0f, 0);
        public static Sprite Ribbon() => ColorHuntHud.PanelSprite(128, R(22), PotaLook.Hex(0xffb35c), Orange2, Color.white, R(3), 0.35f, R(3));
        public static Sprite Card(int radiusCss = 18) => ColorHuntHud.PanelSprite(128, R(radiusCss), Color.white, Color.white, Color.white, 0, 0f, 0);
        public static Sprite Shadow(int radiusCss = 30, int blurCss = 22) => ColorHuntHud.ShadowSprite(128, R(radiusCss), R(blurCss));
        public static Sprite Lip(int radiusCss, Color color) => ColorHuntHud.PanelSprite(128, R(radiusCss), color, color, color, 0, 0f, 0);
        public static Sprite BigButton(Color light, Color mid, Color dark) => ColorHuntHud.PanelSprite(128, R(22), light, dark, new Color(1f, 1f, 1f, 0.55f), R(3), 0.35f, R(3));
        public static Sprite SmallButton(Color light, Color dark) => ColorHuntHud.PanelSprite(128, R(12), light, dark, light, 0, 0f, 0);
        public static Sprite Pill() => ColorHuntHud.PanelSprite(128, R(18), PanelTop, Navy, Gold, R(3), 0.18f, R(2));
        public static Sprite Circle(int sizeCss, Color top, Color bottom, Color border, int borderCss) => ColorHuntHud.CircleSprite(R(sizeCss), top, bottom, border, R(borderCss));
        public static Sprite CloseButton() => ColorHuntHud.CircleSprite(R(46), Red, RedDark, Color.white, R(3));

        public static int R(float css) => Mathf.Max(1, Mathf.RoundToInt(css * Css));
    }
}
