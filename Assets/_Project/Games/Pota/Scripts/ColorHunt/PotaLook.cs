using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's current puzzle (Renk Avı, "balon" look, ~/projects/Pota c742a6a) as numbers and pure curves; every value names its
    /// source file. Units: world = the game root (Pota's pota group, 1 = 1 m; DEV_POTA sizes), cell = one board cell before the
    /// grid scale. Pota's board faces +z, Unity's faces −z (the camera is at −z). Colours are sRGB like Pota's hex values;
    /// <see cref="BlockColor"/> returns linear because three.js mixes them in linear space.
    /// </summary>
    public static class PotaLook
    {
        // sahaYerlesim.ts DEV_POTA, IZGARA_PAYI
        public const float PanoSize = 8.8f;
        public const float FrameWidth = 0.35f;
        public const float FrameOuter = PanoSize + 2f * FrameWidth;
        public const float GridPad = 0.4f;
        public const float RimRadius = 1.6f;
        public const float RimY = -5.6f;
        public const float NetDepth = 1.8f;

        // yapbozSahne.ts
        public const float Block = 0.9f;                // BLOK
        public const float FrontZ = 0.45f;              // Z_ON
        public const float Fov = 38f;                   // GORUS_ACISI
        public const float WidthFill = 0.985f;          // DOLGU
        public const float BallPad = 0.6f;              // TOP_PAYI
        public const float Edge = 0.22f;                // KENAR
        public const float LabelQuad = 0.92f;           // RAKAM_DUZLEM
        public const float LabelZ = FrontZ + 0.03f + 0.1f;   // etiketleriYaz zTaban (front + pillow bulge)
        public const float ItemScale = 0.72f / 0.88f;   // esyaOlcek: 72 % on screen, the atlas sticker fills 88 % of its cell
        public const float NumberScale = 0.44f;
        public const float ItemOffsetY = 0.06f;
        public static readonly Vector2 NumberOffset = new Vector2(0.26f, -0.28f);

        // yastik.ts YASTIK
        public const float PillowRadius = 0.22f;
        public const float PillowBulge = 0.1f;

        // renkAviFaz.ts RENK_AVI_HIS / HIS, ayar2
        public const float SwellPeakAt = 0.55f;         // balon.tepe
        public const float ChainSwell = 1.24f;          // balon.sisme
        public const float TapSwell = 1.18f;            // balon.dokunusSisme
        public const float TapSwellTime = 0.11f;        // balon.dokunusSure
        public const float ChainTime = 0.15f;           // ayar2.sure.zincir
        public const float DropTimePerCell = 0.25f;     // ayar2.sure.dusus
        public const float RotateTime = 0.42f;          // RENK_AVI_HIS.donus
        public const float RotateUpright = 0.7f;        // donusDiklesme
        public const float FlashDecay = 4.5f;           // gorselleriIlerlet
        public const float SquashDecay = 6f;
        public const float LandSquash = 0.12f;          // olcek = 1 − 0.12·carpma
        public const float NeighbourSquash = 0.5f;      // komsuDalga
        public const float ShiverAmount = 0.075f;       // HIS.titrem.genlik (time: ColorHuntSettings.InsufficientShake = titrem.sure)

        // index.html / renkAviFaz.ts boyutla: Pota's CSS is laid out for a 390-px-wide phone.
        public const float PhoneCssWidth = 390f;
        public const float HudCounterCss = 63f;         // .sayac: border 3 + pad 6 + small 13.2 + b 31.5 + pad 6 + border 3
        public const float HudTopPadCss = 14f;          // --ust: max(14px, safe-area-inset-top)
        public const float HudGapCss = 10f;             // boyutla: ust = counter bottom + 10
        public const float ButtonBandCss = 160f;        // dugmePayi
        public const float ButtonGapCss = 34f;          // dugmeBoslugu

        /// <summary>yapbozSahne.ts SEKER_TONU: red, blue, green, yellow, purple (the Unity palette order).</summary>
        public static readonly Color[] Tones = { Hex(0xe8352e), Hex(0x2a82e8), Hex(0x46bf36), Hex(0xf8c22a), Hex(0xa246db) };
        /// <summary>yapbozSahne.ts RENKLER: the confetti colours.</summary>
        public static readonly Color[] Confetti = { Hex(0xe8453c), Hex(0x2f7fd8), Hex(0x3cba6e), Hex(0xffb03a), Hex(0xa864d8) };
        public static readonly Color ShortGrey = Hex(0x8792a6);     // kul
        public static readonly Color NumberShort = Hex(0xff7a72);   // soluk rakam
        public static readonly Color ItemShort = Hex(0x7a8494);     // soluk eşya
        public static readonly Color Sky = Hex(0x8ec9f2);           // saha.ts scene.background
        public static readonly Color Fog = Hex(0x9ed3f5);           // saha.ts fog (30–130)
        public const float FogStart = 30f, FogEnd = 130f;

        /// <summary>three.js lights → URP: three's Lambert divides by π, URP's does not, so direct intensities are ÷π (sun 2.1 → 0.668).</summary>
        public const float ThreeToUnityLight = 1f / Mathf.PI;
        public static readonly Color SunColor = Hex(0xfff4e0);      // saha.ts güneş, 2.1
        public const float SunIntensity = 2.1f;
        /// <summary>saha.ts sun (−24, 26, 24) → (3, 2, −4) in Pota axes; Unity mirrors z.</summary>
        public static readonly Vector3 SunDirection = new Vector3(3f + 24f, 2f - 26f, 4f + 24f);
        /// <summary>
        /// Ambient for URP's trilight: saha.ts HemisphereLight #dcefff / #8a7a66 × 0.8 (÷π like every three light) plus the room
        /// environment's diffuse share (environment intensity 0.55 × the room's mean radiance ≈ 0.8, estimated from RoomEnvironment.js'
        /// light; tuned side by side in Task 11).
        /// </summary>
        public const float RoomDiffuse = 0.55f * 0.8f;
        public static Color AmbientSky => Hex(0xdcefff).linear * (0.8f * ThreeToUnityLight) + Grey(RoomDiffuse);
        public static Color AmbientGround => Hex(0x8a7a66).linear * (0.8f * ThreeToUnityLight) + Grey(RoomDiffuse);
        public static Color AmbientEquator => Color.Lerp(AmbientSky, AmbientGround, 0.5f);
        private static Color Grey(float v) => new Color(v, v, v, 0f);

        public static Color Hex(uint rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);

        /// <summary>sahaYerlesim.ts izgaraOlcegiHesapla: the grid fills the backboard (8.4 wide at every size).</summary>
        public static float GridScale(int n) => (PanoSize - GridPad) / Mathf.Max(1, n);

        /// <summary>sahaYerlesim.ts donusSigdirma: a rotating grid shrinks to stay inside the frame (≈ 0.8 at 45°).</summary>
        public static float RotationFit(int n, float angleRad) =>
            Mathf.Min(1f, FrameOuter / (n * GridScale(n) * (Mathf.Abs(Mathf.Cos(angleRad)) + Mathf.Abs(Mathf.Sin(angleRad)))));

        public struct Fit { public float Distance; public float TargetY; }

        /// <summary>
        /// yapbozSahne.ts boyutla (kasasız): the frame's outer width fills <see cref="WidthFill"/> of the screen width, and frame + ball
        /// strip fit the band between the HUD (<paramref name="topCss"/>) and the button band (<paramref name="bottomCss"/>); the farther
        /// distance wins. Distances from the front plane (z = FrontZ); TargetY = where the camera looks (the band's centre on the board).
        /// </summary>
        public static Fit CameraFit(float aspect, float topCss, float bottomCss, float fov = Fov)
        {
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float h = PhoneCssWidth / aspect;
            float band = Mathf.Max(120f, h - topCss - bottomCss);
            float byWidth = FrameOuter / (2f * tan * aspect * WidthFill);            // panoUzakligi
            float byHeight = (FrameOuter + BallPad) * h / (band * 2f * tan);          // takimBoyu = panoBoy + 2·cerceve + TOP_PAYI
            float dist = Mathf.Max(byWidth, byHeight);
            float pxPerUnit = PhoneCssWidth / (2f * dist * tan * aspect);             // olcek
            float bandMid = (topCss + (h - bottomCss)) * 0.5f;                        // bantOrtaPx (from the top)
            return new Fit { Distance = dist, TargetY = -BallPad * 0.5f + (bandMid - h * 0.5f) / pxPerUnit };
        }

        /// <summary>The camera looks straight at (0, TargetY, front plane) from Distance in front of it (Unity: toward −z).</summary>
        public static Vector3 CameraPosition(Vector3 anchor, Fit f) => anchor + new Vector3(0f, f.TargetY, -(FrontZ + f.Distance));

        /// <summary>renkAviFaz.ts boyutla: the counters' bottom + 10 px (Pota's 118 fallback is this on a notched phone).</summary>
        public static float HudTopCss(float safeTopCss) => Mathf.Max(HudTopPadCss, safeTopCss) + HudCounterCss + HudGapCss;

        /// <summary>renkAviFaz.ts patlamaIlerlet: linear to <paramref name="peak"/> at 55 %, then peak·(1 − u)^1.5.</summary>
        public static float Swell(float o, float peak)
        {
            o = Mathf.Clamp01(o);
            return o < SwellPeakAt ? 1f + (peak - 1f) * (o / SwellPeakAt) : peak * Mathf.Pow(1f - (o - SwellPeakAt) / (1f - SwellPeakAt), 1.5f);
        }

        public static float Smoothstep(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        /// <summary>renkAviFaz.ts dönüş: labels ride the board for 70 %, then turn upright (degrees, relative to the board).</summary>
        public static float LabelCounterAngle(float boardAngle, float u) => -boardAngle * Smoothstep((u - RotateUpright) / (1f - RotateUpright));

        /// <summary>renkAviFaz.ts IVME = 2 / dusus²: cells travelled after t seconds.</summary>
        public static float DropTravel(float t, float perCell = DropTimePerCell) => 0.5f * (2f / (perCell * perCell)) * t * t;

        /// <summary>Step length for the longest drop (renkAviFaz.ts dususleriBaslat).</summary>
        public static float StepDropTime(float cells, float perCell = DropTimePerCell) => cells > 0f ? perCell * Mathf.Sqrt(cells) + 0.06f : 0f;

        /// <summary>yapbozSahne.ts ornekYaz, in linear: lerp(tone, white, min(1, f))·(1 + 1.4f); short of ammo: lerp(kul, 0.5)·0.62 last.</summary>
        public static Color BlockColor(Color tone, float flash, bool shortOfAmmo)
        {
            var c = Color.Lerp(tone.linear, Color.white, Mathf.Min(1f, flash)) * (1f + flash * 1.4f);
            if (shortOfAmmo) c = Color.Lerp(c, ShortGrey.linear, 0.5f) * 0.62f;
            c.a = 1f;
            return c;
        }

        // index.html :root and HUD rules
        public static readonly Color HudNavy = Hex(0x142850), HudNavy2 = Hex(0x1f3a6e), HudPanelTop = Hex(0x24407a), HudButtonTop = Hex(0x2b4d91),
            HudGold = Hex(0xffc83d), HudGold2 = Hex(0xb3600a), HudGreen = Hex(0x2fb36b), HudRed = Hex(0xff6b5e),
            HudOrange = Hex(0xff7a1a), HudOrangeTop = Hex(0xffb46b), HudHint = Hex(0x103055);

        public struct FlightPose { public float Along, Lift, Scale; }

        /// <summary>
        /// renkAviFaz.ts esyaUcur (WAAPI): keyframes 0 → scale 0.9; 0.3 → 30 % of the way, 40 CSS px up, scale 1.35; 1 → there, 0.5;
        /// the whole flight eased by cubic-bezier(.45, 0, .55, 1). <paramref name="t"/> = elapsed / esyaUcus.
        /// </summary>
        public static FlightPose ItemFlight(float t)
        {
            float p = CubicBezier(0.45f, 0f, 0.55f, 1f, Mathf.Clamp01(t));
            if (p < 0.3f)
            {
                float u = p / 0.3f;
                return new FlightPose { Along = 0.3f * u, Lift = 40f * u, Scale = Mathf.Lerp(0.9f, 1.35f, u) };
            }
            float w = (p - 0.3f) / 0.7f;
            return new FlightPose { Along = 0.3f + 0.7f * w, Lift = 40f * (1f - w), Scale = Mathf.Lerp(1.35f, 0.5f, w) };
        }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) at time x: bisection on the x curve (24 steps ≈ 6e-8).</summary>
        public static float CubicBezier(float x1, float y1, float x2, float y2, float x)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 24; i++)
            {
                float s = (lo + hi) * 0.5f;
                if (Bezier(x1, x2, s) < x) lo = s; else hi = s;
            }
            return Bezier(y1, y2, (lo + hi) * 0.5f);
        }

        private static float Bezier(float a, float b, float s)
        {
            float u = 1f - s;
            return 3f * u * u * s * a + 3f * u * s * s * b + s * s * s;
        }

        // renkAviFaz.ts RENK_AVI_HIS.iz, salvo; patlamaEfekt.ts cizIzler
        public const float SalvoArc = 0.9f;             // iz.kavis
        public const float SalvoSpin = 14f;             // iz.donus (rad/s)
        public const float TrailSpacing = 0.14f;        // iz.aralik (cells)
        public const int TrailPoints = 10;              // iz.adet
        public const float TrailWidth = 0.34f;
        public const float TrailBrightness = 1.35f;
        public static readonly Color TrailColor = Hex(0xffa040);

        /// <summary>toplariIlerlet: smoothstep path in the board plane, bowing sin(πo)·0.9 toward the camera (z, Pota axes).</summary>
        public static Vector3 SalvoPoint(Vector2 from, Vector2 to, float o)
        {
            var p = Vector2.LerpUnclamped(from, to, Smoothstep(o));
            return new Vector3(p.x, p.y, Mathf.Sin(Mathf.PI * Mathf.Clamp01(o)) * SalvoArc);
        }

        /// <summary>toplariIlerlet: 0.7 + 0.5·sin(π·min(1, 1.15o)).</summary>
        public static float SalvoScale(float o) => 0.7f + 0.5f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, Mathf.Clamp01(o) * 1.15f));

        /// <summary>yapbozSahne.ts topKaynagi: (0, altKenar − TOP_PAYI·0.75), altKenar = −n/2.</summary>
        public static Vector2 SalvoSource(int n) => new Vector2(0f, -n * 0.5f - BallPad * 0.75f);
    }
}
