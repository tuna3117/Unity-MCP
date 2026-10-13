using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Pota = the 1:1 port of Pota's puzzle; Candy = A "Şeker patlaması"; Shatter = B "Gerçek kırılma".</summary>
    public enum PuzzleLook { Pota, Candy, Shatter }

    /// <summary>
    /// How special balls are born, idle, fire and land (spec 2026-10-01): Pota = today's feedback 1:1; Royal = 1 "Royal Match";
    /// Court = 2 "Basketbol". Normal block bursts keep the Pota look under all three.
    /// </summary>
    public enum SpecialLook { Pota, Royal, Court }

    /// <summary>
    /// How a resting special ball idles (spec 2026-10-07, Tuna: Build 68's Royal idle "çok dikkat dağıtıyor"): Look = the special look's
    /// own idle, exactly as before; A Breath, B Glint, C Rim, D Twinkle, E Sway = the calm alternatives (<see cref="SpecialIdleFx"/>).
    /// </summary>
    public enum SpecialIdle { Look, Breath, Glint, Rim, Twinkle, Sway }

    /// <summary>Every tunable number of the colour-hunt scene. Edit in the Inspector; nothing is hard-coded elsewhere.</summary>
    [CreateAssetMenu(menuName = "Project/Color Hunt/Settings", fileName = "ColorHuntSettings")]
    public class ColorHuntSettings : ScriptableObject
    {
        [Header("Look (spec 2026-09-30 studio polish: Pota, A = Candy, B = Shatter)")]
        [Tooltip("Pota keeps today's burst and HUD; Candy and Shatter add their burst and the studio polish.")]
        public PuzzleLook Look = PuzzleLook.Pota;
        [Tooltip("Stüdyo uçuşu: kırılan eşya zıplayıp kavisle hedefe uçar, varışta esneme, sayaç yuvarlanması ve ✓ damgası. Kapalıyken Pota görünümü Pota'nın düz uçuşunu kullanır; A/B'de hep açık.")]
        public bool StudioFlight = true;

        [Header("Special balls (spec 2026-10-01: Pota renkAvi.ts çizgi / bomba / altın)")]
        [Tooltip("Which specials are born (renkAvi.ts ozellikler). All off = today's level 20: same boards, same rng.")]
        public Features Specials;
        [Tooltip("Özel top görünümü: Pota (bugünkü, birebir), Royal (1 · Royal Match), Court (2 · Basketbol). Normal patlamalar hep Pota.")]
        public SpecialLook SpecialLook = SpecialLook.Pota;
        [Tooltip("1 · Royal: every number of the Royal special look (timing, birth, idle, rockets, bomb, 2+ specials, golden landing, ink).")]
        public RoyalTuning Royal = new RoyalTuning();
        [Tooltip("Özel top beklemesi (spec 2026-10-07): Görünüm = seçili özel görünümün kendi beklemesi (bugünkü, birebir); A Sakin nefes, " +
                 "B Parıltı süpürmesi, C Kenar nabzı, D Tek yıldızcık, E Hafif sallantı. Specials sahnesinde düğmelerle değişir.")]
        public SpecialIdle SpecialIdle = SpecialIdle.Look;
        [Tooltip("Every number of the five calm idles A–E (game seconds, board cells).")]
        public IdleTuning Idle = new IdleTuning();

        [Header("Pass ball (Pota spec 2026-10-11: 2×2 kare → pas topu; two looks, Tuna picks)")]
        [Tooltip("Pas topu uçuşu: A Pas (topun kendi bloğu zıplar, alçak kavisle uçar, hız çizgileri) · B Pervane (Blender topu + dönen pervane, " +
                 "yükselir, yüksek kavis, asılı kalıp dalar, toz halkası). Varsayılan B (Tuna 2026-10-12 seçti; A referans). Specials sahnesinde düğmelerle değişir.")]
        public PassLook PassLook = PassLook.B;
        [Tooltip("A Pas — Pota src/ozelPlan.ts OZEL_ZAMANLAMA pas* (:16-17) + src/ozelAyar.ts OZEL_AN.pasTopu.a (:51); flight renkAviFaz.ts pasIlerlet.")]
        public PassTuningA PassLookA = new PassTuningA();
        [Tooltip("B Pervane (Tuna's pick, the default) — Pota src/ozelPlan.ts OZEL_ZAMANLAMA pas* (:20) + src/ozelAyar.ts OZEL_AN.pasTopu (:58); flight renkAviFaz.ts pasUcur / pasBoyu, tilt patlamaEfekt.ts PAS_EGIM.")]
        public PassTuningB PassLookB = new PassTuningB();
        [Tooltip("The pass ball's sticker: Pota public/assets/ozel-pasTopu-a.png (candidate a2, 256², padded) → Art/Specials/pas-topu-a.png.")]
        public Texture2D PassSprite;
        [Tooltip("B: the Blender pass_ball delivery (ai_import_asset) — Basketball (960 tris) + Propeller (96 tris, its origin on the ball's up axis).")]
        public GameObject PassModel;

        [Header("Combo (Pota spec 2026-10-13: two specials side by side merge on a tap; two choreographies, Tuna picks)")]
        [Tooltip("Kombo koreografisi: A Hızlı-sert · B Sinematik (Pota OZEL_AN.kombo.a/.b; aynı sayılar). Specials sahnesinde düğmelerle değişir.")]
        public ComboLook ComboLook = ComboLook.A;
        [Tooltip("A \"Hızlı-sert\" — Pota src/ozelAyar.ts OZEL_AN.kombo.a.")]
        public ComboTuning ComboLookA = ComboTuning.A();
        [Tooltip("B \"Sinematik\" — Pota src/ozelAyar.ts OZEL_AN.kombo.b.")]
        public ComboTuning ComboLookB = ComboTuning.B();
        [Tooltip("What both share: lettering widths, the big bomb, the carry, the three-pass fan, the lean, the chunks.")]
        public ComboShared Combo = new ComboShared();
        [Tooltip("Yaslanma (K6): kombo yapabilen çift, tahta boşken ara ara birbirine yaslanır. Specials sahnesinde var/yok.")]
        public bool ComboLean = true;
        [Tooltip("vfx_flipbook lettering (combo / slam / swish styles), 1024×512 stills: CROSSOVER!, SHOWTIME!, MEGA SLAM!, ALLEY-OOP!, FAST BREAK!.")]
        public Texture2D LetterCrossover, LetterShowtime, LetterMegaSlam, LetterAlleyOop, LetterFastBreak;
        [Tooltip("The combo's ring: vfx_flipbook ring_toon (white toon ring, 4×4 frames, 0.4 s; plan P2 instead of the shock ring).")]
        public Texture2D ComboRing;
        [Tooltip("The combo's chunks: vfx_fracture pillow (vfx-pillow-shatter, ten pieces of the 0.9 block; the first three fly, plan P6).")]
        public GameObject ComboPieces;

        [Header("Feel layer (spec 2026-10-03: level intro, ammo tension, leftover finale, words, hint, bounce, clip, sky, lose)")]
        [Tooltip("Off = today's behaviour exactly (for the Pota | before | after recordings).")]
        public FeelTuning Feel = new FeelTuning();
        [Tooltip("Shown on the level-intro ribbon (\"BÖLÜM 20\"); Pota level 20 is the calibration board.")]
        public int LevelNumber = 20;

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
        [Tooltip("ses.ts special-ball samples through Pota's effect chain: bomba (patlama), cizgi (cizgi), altin-dogum (ozelDogdu), altin-inis (altinInis).")]
        public AudioClip SfxBomb, SfxLine, SfxGoldenBorn, SfxGoldenLand;
        [Tooltip("Court look, same chain: smac-vurus (the dunk's hit, Pota's smac music layer), file-swish (swish), kalabalik (crowd).")]
        public AudioClip SfxSlam, SfxSwish, SfxCrowd;
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
        [Tooltip("Project/PotaPiece: B's lit pieces with the sticker.")]
        public Material PieceTemplate;
        [Tooltip("Project/PotaShockRing: B's heat-haze ring.")]
        public Material ShockTemplate;
        [Tooltip("Core Project/Toon: the special looks' Blender meshes with their embedded skin as base map (SpecialMeshes.Toon).")]
        public Material ToonTemplate;
        [Tooltip("Project/PotaStencilMask: F7's invisible stencil quad over the frame's inside (BoardClip).")]
        public Material StencilMaskTemplate;
        [Tooltip("Project/PotaGlint: the calm idles' overlays (B glint, C rim, D twinkle; spec 2026-10-07).")]
        public Material GlintTemplate;

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

        [Header("Special looks (Blender hand-offs, ai_studio_import; plan 2026-10-01 Tasks 7–9)")]
        [Tooltip("1 Royal: vfx_flipbook fire_toon, inked cartoon fireball, 4×4 frames of 256 px, 0.45 s one-shot; ends in grey smoke.")]
        public Texture2D RoyalFire;
        [Tooltip("1 + 2: vfx_flipbook flame_jet, LOOP of 4×2 frames of 256 px, one cycle 0.3 s; nozzle at frame (0.5, 0.9), flame toward −y.")]
        public Texture2D FlameJet;
        [Tooltip("2 Court: vfx_flipbook dust_ring, inked court-tan ring laid face-on, 4×4 frames of 256 px, 0.4 s; open centre.")]
        public Texture2D CourtDust;
        [Tooltip("2 Court: vfx_flipbook speed_lines, horizontal streaks trailing to −x behind a ball at the right edge, 4×2 of 256 px, 0.25 s.")]
        public Texture2D SpeedLines;
        [Tooltip("2 Court: vfx_flipbook floor_crack, one 512 px still (dark star crack, pale chipped edge); fade by alpha.")]
        public Texture2D FloorCrack;
        [Tooltip("2 Court: vfx_flipbook lettering, 512×256 stills in Lilita One with a navy outline: draw on a 2:1 quad or size3D.")]
        public Texture2D LetterSlam, LetterSwish, LetterCombo;
        [Tooltip("1 Royal: vfx_mesh ball_halves. Children HalfA, HalfB: nose along local +X, flat cap at x = 0 facing −X, origin at the " +
                 "cap centre; the whole ball = HalfB turned 180° about y against HalfA. Import offsets y ±0.3 are for the preview only.")]
        public GameObject RoyalHalves;
        [Tooltip("1 Royal: vfx_fracture sphere, a 0.45 m bomb in eight shell pieces piece_00…07 (origin = piece centre).")]
        public GameObject RoyalShell;
        [Tooltip("1 Royal: vfx_mesh coin, a 0.30 m gold coin with a raised ball, faces along ±z.")]
        public GameObject RoyalCoin;
        [Tooltip("2 Court: vfx_mesh basketball, a 0.45 m low-poly ball in Pota's classic skin.")]
        public GameObject CourtBall;

        [Header("Calm idles (Blender hand-offs, ai_studio_import; spec 2026-10-07)")]
        [Tooltip("B: vfx_flipbook glint_streak, a 256² still: a thin white horizontal streak with soft ends, its core 0.10 of the frame high.")]
        public Texture2D IdleStreak;
        [Tooltip("C: vfx_flipbook rim_ring, a 256² still: a thin soft white ring, centre line at 0.40 of the frame, core 0.043 wide.")]
        public Texture2D IdleRing;
        [Tooltip("D: vfx_flipbook twinkle_star, a 256² still: four rays tapering to needle tips over a small soft core.")]
        public Texture2D IdleStar;

        [Header("Feel layer props (Blender hand-offs, ai_studio_import; spec 2026-10-03 F8, F11)")]
        [Tooltip("F11: vfx_flipbook lettering style miss, \"KAÇTI!\" 512×256 still (red, navy outline).")]
        public Texture2D LetterMiss;
        [Tooltip("F8: recipe cloud_set — Cloud_0..2, white, flat-bottomed; SkyLife dresses them in Project/Toon.")]
        public GameObject Clouds;
        [Tooltip("F8: recipe bird_flock — one skinned V of five birds on an armature.")]
        public GameObject Birds;
        [Tooltip("F8: the flock's looping 0.5 s flap clip (played through a PlayableGraph).")]
        public AnimationClip BirdFlap;
        [Tooltip("F8 (U27): recipe city_band — a 180° arc of low-poly buildings, wall + glass materials.")]
        public GameObject CityBand;

        [Header("Court special look (spec 2026-10-01 §2 · 2 Basketbol): game seconds, board cells")]
        [Tooltip("Every number of the Court look; the timing fields also drive the trigger plan (CourtSpecials.TimingFor).")]
        public CourtTuning Court = new CourtTuning();

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
            return new LevelDesign { Size = Size, ColorCount = ColorCount, HpMin = HpMin, HpMax = HpMax, Targets = t, Features = Specials };
        }
    }
}
