using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Pota = the 1:1 port of Pota's puzzle; Candy = A "Şeker patlaması"; Shatter = B "Gerçek kırılma".</summary>
    public enum PuzzleLook { Pota, Candy, Shatter }

    /// <summary>Every tunable number of the colour-hunt scene. Edit in the Inspector; nothing is hard-coded elsewhere.</summary>
    [CreateAssetMenu(menuName = "Project/Color Hunt/Settings", fileName = "ColorHuntSettings")]
    public class ColorHuntSettings : ScriptableObject
    {
        [Header("Look (spec 2026-09-30 studio polish: Pota, A = Candy, B = Shatter)")]
        [Tooltip("Pota keeps today's burst and HUD; Candy and Shatter add their burst and the studio polish.")]
        public PuzzleLook Look = PuzzleLook.Pota;

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

        [Header("Material templates (ai_potalook_import): a player build keeps only the shaders an asset references")]
        [Tooltip("Project/PotaLabel, GPU instancing on.")]
        public Material LabelTemplate;
        [Tooltip("Project/PotaAdditive: flashes, ring, stars, points, puff, salvo trail.")]
        public Material AdditiveTemplate;
        [Tooltip("URP Particles/Lit: glossy shards and paper confetti.")]
        public Material ParticleLitTemplate;
        [Tooltip("URP Unlit, transparent: the selection ring.")]
        public Material SelectionTemplate;
        [Tooltip("Project/PotaFlipbook: the studio flipbooks (puff, ring, smoke).")]
        public Material FlipbookTemplate;
        [Tooltip("Project/CandyShard: A's inked candy chunks.")]
        public Material CandyShardTemplate;

        [Header("Studio looks (Blender hand-offs, ai_studio_import)")]
        [Tooltip("A: vfx_flipbook puff_toon, 4×4 frames of 256 px.")]
        public Texture2D CandyPuff;
        [Tooltip("A: vfx_flipbook ring_toon.")]
        public Texture2D CandyRing;
        [Tooltip("B: vfx_flipbook smoke_lit.")]
        public Texture2D ShatterSmoke;
        [Tooltip("B: vfx_flipbook shock_ring (linear screen normals).")]
        public Texture2D ShockRing;
        [Tooltip("B: vfx_fracture pillow, one child mesh per piece (origin = piece centre, UV1.x = front flag).")]
        public GameObject ShatterPieces;

        [Header("Timing (seconds)")]
        public float SalvoDuration = 0.5f;
        [Range(0f, 1f)] public float SalvoLaunchFraction = 0.55f;
        [Range(0f, 1f)] public float SalvoFlightFraction = 0.42f;
        public int SalvoMaxBalls = 24;
        public float SalvoSpread = 1.1f;
        public float SalvoScatter = 0.46f;
        public float DropTimePerCell = 0.25f;
        public float ChainTime = 0.15f;
        [Tooltip("renkAviFaz.ts RENK_AVI_HIS.donus: smoothstep turn.")]
        public float RotateTime = 0.42f;
        public float WinWait = 0.85f;
        public float LoseWait = 0.45f;
        [Tooltip("renkAviFaz.ts HIS.titrem.sure: shiver of a block the ammo does not cover.")]
        public float InsufficientShake = 0.3f;
        [Tooltip("renkAviFaz.ts RENK_AVI_HIS.esyaUcus: sticker flight to the HUD, real time.")]
        public float ItemFlyTime = 0.55f;
        [Tooltip("RENK_AVI_HIS.balon: the tapped block swells to 1.18 in 0.11 s and bursts at 55 %; chain blocks to 1.24 in ChainTime.")]
        public float TapSwellTime = 0.11f;
        public float TapSwell = 1.18f;
        public float ChainSwell = 1.24f;

        [Header("Juice (renkAviFaz.ts HIS)")]
        [Tooltip("hitStop: game time ×0.12 for 0.05 s at the tapped block's swell peak.")]
        public float HitStopTapScale = 0.12f;
        public float HitStopTap = 0.05f;
        [Tooltip("zincirStop: ×0.06 for min(0.18, 0.09 + 0.02·depth) at a chain step's peak.")]
        public float HitStopChainScale = 0.06f;
        public float HitStopChainBase = 0.09f;
        public float HitStopChainPerDepth = 0.02f;
        public float HitStopChainMax = 0.18f;
        [Tooltip("sarsinti: additive, capped at 0.5, decays 2.2/s.")]
        public float ShakeTap = 0.08f;
        public float ShakeChain = 0.07f;
        public float ShakeChainPerDepth = 0.03f;
        public float ShakeWin = 0.14f;
        public float ShakeRotate = 0.05f;

        [Header("Explosion")]
        public int ShardsBase = 14;
        public int ShardsPerDepth = 5;
        public int ShardsMax = 34;
        public float LightFlashIntensity = 6f;
        public float LightFlashTime = 0.18f;
        [Tooltip("RENK_AVI_HIS.flas: flash size 1.3 (+0.15 per chain depth).")]
        public float FlashSize = 1.3f;
        public float FlashSizePerDepth = 0.15f;
        [Tooltip("three's 1/d^1.6 light (÷π) on URP's 1/d² at the block plane (≈ 1.9·1.2 m): 2.28^0.4 / π ≈ 0.44. Tuned side by side.")]
        public float LightIntensityScale = 0.44f;
        [Tooltip("HIS.parcacik: hot points per chain burst, 10 + 4 per depth, at most 26.")]
        public int PointsBase = 10;
        public int PointsPerDepth = 4;
        public int PointsMax = 26;
        [Tooltip("RENK_AVI_HIS.konfeti.")]
        public int ConfettiCount = 220;

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
