using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Every tunable number of the colour-hunt scene. Edit in the Inspector; nothing is hard-coded elsewhere.</summary>
    [CreateAssetMenu(menuName = "Project/Color Hunt/Settings", fileName = "ColorHuntSettings")]
    public class ColorHuntSettings : ScriptableObject
    {
        [Header("Level (Pota level 20 calibration)")]
        public int Size = 7;
        public int ColorCount = 5;
        public int HpMin = 3;
        public int HpMax = 14;
        public int Ammo = 158;
        [Tooltip("Target: colour index and count. 0 = red, 1 = blue.")]
        public Vector2Int[] Targets = { new Vector2Int(0, 22), new Vector2Int(1, 16) };
        [Tooltip("0 = new random board every round; otherwise the same board every time.")]
        public uint Seed = 0;

        [Header("Palette (Pota RENKLER order: red, blue, green, yellow, purple)")]
        public Color[] Palette =
        {
            new Color(0.910f, 0.271f, 0.235f),
            new Color(0.184f, 0.498f, 0.847f),
            new Color(0.235f, 0.729f, 0.431f),
            new Color(1.000f, 0.690f, 0.227f),
            new Color(0.659f, 0.392f, 0.847f),
        };
        public Color FrameColor = new Color(1f, 0.541f, 0.165f);
        public Color PlateColor = new Color(0.106f, 0.208f, 0.373f);
        public Color SkyTop = new Color(0.62f, 0.80f, 0.93f);
        public Color SkyBottom = new Color(0.83f, 0.91f, 0.96f);
        public Color NavyColor = new Color(0.106f, 0.208f, 0.373f);
        public Color GoldColor = new Color(1f, 0.78f, 0.24f);
        public Color GreenColor = new Color(0.30f, 0.85f, 0.45f);
        public Color RedColor = new Color(1f, 0.42f, 0.42f);

        [Header("Block")]
        public float BlockSize = 0.92f;
        public float BlockDepth = 0.55f;
        public float BlockRadius = 0.16f;
        public Texture2D BlockFace;
        public Font Font;
        public Texture2D ArrowIcon;
        public Material BallMaterial;
        [Tooltip("Ammo does not cover this block: colour fades toward grey by this amount.")]
        [Range(0f, 1f)] public float DimAmount = 0.55f;

        [Header("Timing (seconds)")]
        public float SalvoDuration = 0.5f;
        [Range(0f, 1f)] public float SalvoLaunchFraction = 0.55f;
        [Range(0f, 1f)] public float SalvoFlightFraction = 0.42f;
        public int SalvoMaxBalls = 24;
        public float SalvoSpread = 1.1f;
        public float SalvoScatter = 0.46f;
        public float DropTimePerCell = 0.25f;
        public float ChainTime = 0.15f;
        public float RotateTime = 0.34f;
        public float WinWait = 0.85f;
        public float LoseWait = 0.45f;
        public float InsufficientShake = 0.3f;

        [Header("Juice")]
        public float HitStopTap = 0.05f;
        public float HitStopChainBase = 0.09f;
        public float HitStopChainPerDepth = 0.02f;
        public float HitStopChainMax = 0.18f;
        public float ShakeBallHit = 0.015f;
        public float ShakeTap = 0.08f;
        public float ShakeChain = 0.07f;
        public float ShakeChainPerDepth = 0.03f;
        public float ShakeWin = 0.14f;
        public float NeighbourRipple = 0.16f;

        [Header("Explosion")]
        public int ShardsBase = 14;
        public int ShardsPerDepth = 5;
        public int ShardsMax = 34;
        public int SparksBase = 16;
        public float LightFlashIntensity = 6f;
        public float LightFlashTime = 0.18f;

        public Color PaletteColor(int index)
        {
            if (Palette == null || Palette.Length == 0) return Color.white;
            return Palette[Mathf.Clamp(index, 0, Palette.Length - 1)];
        }

        public LevelDesign ToDesign()
        {
            var t = new (int, int)[Targets.Length];
            for (int i = 0; i < t.Length; i++) t[i] = (Targets[i].x, Targets[i].y);
            return new LevelDesign { Size = Size, ColorCount = ColorCount, HpMin = HpMin, HpMax = HpMax, Targets = t };
        }
    }
}
