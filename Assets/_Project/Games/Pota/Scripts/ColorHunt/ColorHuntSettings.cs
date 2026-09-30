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

        [Header("Palette (Pota SEKER_TONU: red, blue, green, yellow, purple)")]
        public Color[] Palette = (Color[])PotaLook.Tones.Clone();
        public Color NavyColor = new Color(0.106f, 0.208f, 0.373f);
        public Color GoldColor = new Color(1f, 0.78f, 0.24f);
        public Color GreenColor = new Color(0.30f, 0.85f, 0.45f);
        public Color RedColor = new Color(1f, 0.42f, 0.42f);

        [Header("Block")]
        public Font Font;
        public Texture2D ArrowIcon;
        [Tooltip("Item sprite per colour (Pota ESYALAR order: forma, ayakkabı, şapka, çorap, bileklik). Empty = number only.")]
        public Texture2D[] ItemIcons;
        public Material BallMaterial;

        [Header("Pota look assets (Tools/potalook/run_export.sh + ai_potalook_import)")]
        [Tooltip("yapbozSahne.ts EtiketAtlasi: 2048², 11 columns × 186 px; numbers 1–99 = cells 0–98, star 99, items 100–104.")]
        public Texture2D LabelAtlas;
        [Tooltip("patlamaEfekt.ts yildizKivilcim(128): four-point star + soft core.")]
        public Texture2D StarSprite;
        [Tooltip("patlamaEfekt.ts izDokusu(64): soft across, fading toward the tail.")]
        public Texture2D TrailSprite;
        [Tooltip("patlamaEfekt.ts yumusakDaire(128): the flash sprite.")]
        public Texture2D SoftDisc;
        [Tooltip("patlamaEfekt.ts yumusakDaire(128, 0.62, 0.98): the ring sprite.")]
        public Texture2D SoftRing;
        [Tooltip("public/assets/pano-a.png: the giant hoop's glass backboard.")]
        public Texture2D Backboard;
        [Tooltip("Salvo ball: efekt.ts topKureDokusu() skin, yapbozSahne.ts roughness 0.55, emissive #2a1200.")]
        public Material SalvoBall;
        [Tooltip("efekt.ts SonIslem: Neutral ×1.12, bloom 0.28 / 0.45 / 0.86, vignette 0.32 / 0.55.")]
        public UnityEngine.Rendering.VolumeProfile PuzzleProfile;
        [Tooltip("ses.ts samples (kirildi, kuleAt, dustu, dondur, eksi, kazandi, kaybetti) rendered through Pota's effect chain.")]
        public AudioClip SfxBreak, SfxSalvo, SfxDrop, SfxRotate, SfxMinus, SfxWin, SfxLose;
        [Tooltip("ses.ts kombo: MERDIVEN[0..10], index = chain depth.")]
        public AudioClip[] SfxCombo;
        [Tooltip("ses.ts carpma (guc 0.8) at r = 0.92, 1.00, 1.08.")]
        public AudioClip[] SfxHit;
        [Tooltip("ses.ts dokun: a block gets selected under the finger.")]
        public AudioClip SfxSelect;
        [Tooltip("three.js RoomEnvironment baked by ai_potalook_import (PotaRoomBaker): the blocks' reflections, intensity 0.45.")]
        public Cubemap RoomReflection;

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
        [Tooltip("renkAviFaz.ts HIS.titrem.sure: shiver of a block the ammo does not cover.")]
        public float InsufficientShake = 0.3f;
        [Tooltip("renkAviFaz.ts RENK_AVI_HIS.esyaUcus: sticker flight to the HUD, real time.")]
        public float ItemFlyTime = 0.55f;

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

        public Texture2D ItemIcon(int color) => ItemIcons != null && color >= 0 && color < ItemIcons.Length ? ItemIcons[color] : null;

        public LevelDesign ToDesign()
        {
            var t = new (int, int)[Targets.Length];
            for (int i = 0; i < t.Length; i++) t[i] = (Targets[i].x, Targets[i].y);
            return new LevelDesign { Size = Size, ColorCount = ColorCount, HpMin = HpMin, HpMax = HpMax, Targets = t };
        }
    }
}
