using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Project.ColorHunt
{
    /// <summary>
    /// Every number of the Royal special look (spec 2026-10-01 §2 "1 · Royal Match"). Cells are board cells (× the grid scale on screen),
    /// times are game seconds. Edit them in ColorHuntSettings.Royal; the timing block feeds SpecialSchedule through RoyalSpecials.Timing.
    /// </summary>
    [Serializable]
    public class RoyalTuning
    {
        [Header("Timing (SpecialSchedule)")]
        [Tooltip("Line: the ball squashes and glows this long before the halves fly (s).")]
        public float LineLead = 0.06f;
        [Tooltip("Rocket speed in cells/s: 6 cells (a 7×7 row, edge to far edge) in 0.32 s.")]
        public float LineSpeed = 6f / 0.32f;
        [Tooltip("Bomb wind-up: swell, two red-white blinks, shiver, fuse sparks (s).")]
        public float BombLead = 0.18f;
        [Tooltip("Bomb wave: each further ring of the 3×3 bursts this much later (s).")]
        public float BombRing = 0.04f;
        [Tooltip("Birth: the group's blocks are pulled into the birth cell in this time (s).")]
        public float GatherTime = 0.18f;

        [Header("Birth")]
        [Tooltip("A gathered block's scale when it reaches the birth cell.")]
        public float GatherShrink = 0.35f;
        [Tooltip("A gathered block turns this far on its way (degrees, alternating sides).")]
        public float GatherSpin = 40f;
        [Tooltip("The special pops 0 → PopPeak → 1 in this time (s).")]
        public float PopTime = 0.25f;
        public float PopPeak = 1.2f;
        public float BirthFlashCells = 2.2f;
        public float BirthStarCells = 2.8f;
        [Tooltip("Twinkles around a birth (spec: 6–8).")]
        public int BirthTwinkles = 7;

        [Header("Idle")]
        public float FuseSparksPerSecond = 3f;
        [Tooltip("The bomb breathes ±this (3 %).")]
        public float BreathAmount = 0.03f;
        public float BreathHz = 1.1f;
        [Tooltip("The line ball's arrow glows at this rate (Hz) up to ArrowGlow (a flash floor).")]
        public float ArrowGlowHz = 1.5f;
        public float ArrowGlow = 0.14f;
        public float GoldenTwinklesPerSecond = 1f;

        [Header("Line: rockets")]
        [Tooltip("Diameter of the split ball and of the bomb's shell (cells).")]
        public float BallCells = 0.8f;
        public float SquashAxis = 0.72f;
        public float SquashSide = 1.18f;
        [Tooltip("The ball's base colour × this at the end of the squash (HDR: it blooms).")]
        public float LineGlow = 2.2f;
        [Tooltip("The flame jet's frame size (cells); the flame runs 0.9 of it behind the cap.")]
        public float JetCells = 1.8f;
        [Tooltip("A grey puff every this many cells (spec: half a cell).")]
        public float PuffEvery = 0.5f;
        public float SparkEvery = 0.2f;
        [Tooltip("Past its last block a rocket speeds up to the frame edge (cells/s²).")]
        public float ExitAccel = 60f;
        [Tooltip("After the exit flash the rocket shrinks away in this time (s).")]
        public float ExitTail = 0.08f;
        [Tooltip("Shake along the line's axis (Pota sars units).")]
        public float LineShake = 0.12f;

        [Header("Bomb")]
        public float BombSwell = 1.25f;
        public int BombBlinks = 2;
        public float CoreFlashCells = 2.6f;
        public float CoreFlashTime = 0.08f;
        [Tooltip("The inked fireball's width (cells); the flipbook fills 85 % of its frame.")]
        public float FireCells = 3.2f;
        public float FireTime = 0.45f;
        public float RingCells = 3.6f;
        [Tooltip("Grey puffs (spec: 4–6).")]
        public int BombPuffs = 5;
        public int BombSparks = 12;
        [Tooltip("ColorHuntFx point light (Pota's bomb flash uses 8).")]
        public float BombLight = 10f;
        public float BombLightTime = 0.3f;
        [Tooltip("The 3×3's blocks are pushed out this far as the bomb goes (cells).")]
        public float PushCells = 0.1f;
        public float BombShake = 0.2f;
        public float BombHitStop = 0.06f;

        [Header("2+ specials in one step")]
        public float ComboSlowScale = 0.5f;
        public float ComboSlowTime = 0.2f;
        public float ScreenFlashTime = 0.1f;
        public float ScreenFlashAlpha = 0.55f;

        [Header("Golden landing")]
        [Tooltip("The golden ball flies as a big spinning coin this wide (cells).")]
        public float GoldenCoinCells = 0.85f;
        [Tooltip("The flying coin's spin (degrees/s).")]
        public float TokenSpin = 540f;
        public float GlitterEvery = 0.025f;
        [Tooltip("Coins thrown from the rim (spec: 8–12).")]
        public int LandCoins = 10;
        public float CoinCells = 0.42f;
        public int LandStars = 10;

        [Header("Ink")]
        [Tooltip("Project/Toon outline in world units (A's candy chunks use 0.022).")]
        public float InkWidth = 0.022f;
    }

    /// <summary>
    /// 1 · Royal (spec 2026-10-01 §2 "1 · Royal Match"): glossy, inked, cartoon; everything pops in. Birth: the group's blocks are pulled
    /// into the birth cell (shrinking, turning) and the special pops 0 → 1.2 → 1 with a white core flash, an eight-ray star and twinkles.
    /// Idle: the bomb's fuse spits sparks and the bomb breathes, the line ball's arrow glows at 1.5 Hz, the golden ball twinkles. Line: the
    /// ball squashes and glows, splits into the two Blender halves that fly to both ends with a looping flame jet, grey puffs every half
    /// cell and sparks — the game bursts each block the moment a rocket is over it (the same formula as SpecialSchedule, Pace included) —
    /// and leave the frame with a small flash; the camera shakes along the axis. Bomb: a copy of the consumed block swells to 1.25, blinks
    /// red-white twice, shivers and spits fuse sparks for BombLead, then a white core flash, the inked fireball (Blender), a ring, eight shell
    /// pieces (Blender, ShatterDebris' deterministic motion), grey puffs, sparks, Pota's point light, a 0.1-cell push of the 3×3 and the
    /// game's ring wave. 2+ specials in a step: ×0.5 for 0.2 s and a 0.1 s white screen flash on the HUD canvas, at the chain moment.
    /// Golden: a big spinning coin drops through its gate leaving glitter, flashes gold there, arcs into the hoop and bursts into spinning
    /// coins and stars. Game time throughout; meshes live under this object (the game root's space), particles in world space.
    /// [DefaultExecutionOrder(100)] puts this Update after ColorHuntGame's, so a fire's effect runs on the step's own clock (see Fire).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class RoyalSpecials : MonoBehaviour, ISpecialStyle
    {
        // Pools: Royal's own live particles and pieces stay ≤ Cap; Pota's burst pools (BurstBudget.Pota) draw the normal blocks.
        public const int FlashMax = 16, StarMax = 8, TwinkleMax = 96, SparkMax = 96, PuffMax = 32, FireMax = 4, RingMax = 6;
        public const int RocketMax = 6, ProxyMax = 3, ShellMax = 24, CoinMax = 24, TokenMax = 4;
        /// <summary>A rocket counts twice: its half ball and its flame jet.</summary>
        public const int PerRocket = 2;
        public const int PoolCapacity = FlashMax + StarMax + TwinkleMax + SparkMax + PuffMax + FireMax + RingMax
                                        + RocketMax * PerRocket + ProxyMax + ShellMax + CoinMax + TokenMax;
        /// <summary>Spec §1.6: a look's live particles ≤ BurstBudget.Max together with the Pota burst pools (342).</summary>
        public const int Cap = BurstBudget.Max - BurstBudget.Pota;

        /// <summary>Task 8's meshes: the halves and the shell make a 0.45 m ball, the coin is 0.30 m across.</summary>
        public const float ModelBall = 0.45f, ModelCoin = 0.30f;
        /// <summary>vfx-fire-toon reaches ~85 % of its frame at its widest.</summary>
        public const float FireFill = 0.85f;
        /// <summary>vfx-flame-jet: 4×2 frames, one loop 0.3 s, nozzle at frame (0.5, 0.9), the flame toward frame −y.</summary>
        public const float JetLoop = 0.3f;
        public const int JetCols = 4, JetRows = 2, JetFrames = JetCols * JetRows;
        public static readonly Vector2 JetNozzle = new Vector2(0.5f, 0.9f);
        /// <summary>Pota's ozel-bomba-a.png: the fuse spark's centroid, u 0.79 and v 0.90 from the bottom (measured 2026-10-01).</summary>
        public const float FuseU = 0.79f, FuseV = 0.90f;
        /// <summary>The special sticker's quad (cells): RAKAM_DUZLEM × ozelOlcek; the sprite fills its middle 88 % ("payli").</summary>
        public const float StickerQuad = PotaLook.LabelQuad * PotaLook.SpecialScale;
        /// <summary>Rockets fly this far in front of the board plane (cells): over the blocks they pass, not through them.</summary>
        public const float RocketZ = PotaLook.FrontZ + 0.45f;
        /// <summary>The fireball's depth (cells toward the camera), and the shell's: the pieces start in front of the fireball and of the
        /// 3×3's pillows, so they are seen flying out of it (at 0.2 they started inside the neighbours and stayed hidden).</summary>
        public const float FireZ = PotaLook.FrontZ + 0.5f, ShellZ = FireZ + 0.3f;
        /// <summary>A flame is at most the halves' separation + this long (cells): at the split it never reaches past its twin.</summary>
        public const float JetGap = 0.1f;
        /// <summary>
        /// Sorting orders above the Pota burst pools (ColorHuntFx 5–9, which keep drawing the normal blocks): Royal's effects fly in
        /// front of the bursting blocks, and drawn before them the later additive flashes and shards washed the fireball and the flame
        /// jets out. Puffs and rings under the fireball and the flame jets (a jet over its own trail smoke), sparks, flashes, stars and
        /// twinkles over them.
        /// </summary>
        public const int PuffOrder = 10, RingOrder = 10, JetOrder = 11, FireOrder = 11, GlowOrder = 12;
        public const float RocketSoundTime = 0.42f, FuseSoundTime = 0.22f, CoinSoundTime = 0.35f;
        private const float SparkGravity = 7f, CoinGravity = 11f, SynthRate = 44100f;

        public static readonly Color BlinkRed = PotaLook.Hex(0xff3b2f);
        private static readonly Color Smoke = new Color(0.6f, 0.62f, 0.68f, 0.95f);       // CandyPuff × grey: cartoon smoke, its ink stays dark
        private static readonly Color SparkTone = new Color(1f, 0.78f, 0.38f, 1f);
        private static readonly Color FuseSpark = new Color(1f, 0.86f, 0.45f, 1f);
        private static readonly Color Gold = new Color(1f, 0.84f, 0.35f, 1f);
        private static readonly Color GoldWhite = new Color(1f, 0.93f, 0.62f, 1f);
        private static readonly Color StarWhite = new Color(1f, 0.97f, 0.88f, 1f);
        private static readonly Color FireGlow = PotaLook.Hex(0xffb347);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SheetStId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly RoyalTuning Defaults = new RoyalTuning();
        private static Mesh _jetQuad;

        private class ViewFx { public SpecialKind Kind; public float Phase, Next, Pop = -1f, PopScale = 1f, Breath = 1f; }
        private class GatherFx { public BlockView V, Home; public Vector2 From, To; public float Age, Duration, Spin; public Action<BlockView> Arrived; }
        private class Rocket
        {
            public Transform Root, Half, Jet;
            public MeshFilter HalfF;
            public MeshRenderer HalfR, JetR;
            public SpecialFire Fire;
            public SpecialTiming Timing;
            public Vector2 Dir;
            public float Age, Last, Edge, NextPuff, NextSpark, Exited = -1f;
            public bool Launched, Leads;
        }
        private class Proxy
        {
            public Transform Root, Item;
            public MeshFilter BodyF, ItemF;
            public MeshRenderer BodyR, ItemR;
            public Color Tone;
            /// <summary>The hidden view's own scale at the fire (its breath, a squash): the copy starts there, so the swap shows nothing.</summary>
            public float Base = 1f;
        }
        private class BombFx { public SpecialFire Fire; public Proxy Proxy; public float Age, Lead, NextSpark; }
        private class Piece { public Transform T; public MeshFilter F; public Debris D; public float Radius; }
        private class Coin { public Transform T; public Debris D; }
        private class Flight
        {
            public GoldenRoute Route;
            public float T, Spin, NextGlitter;
            public bool Passed;
            public Action PassedGate, Landed;
            public Transform Token;
        }

        private ColorHuntGame _game;
        private bool _built;
        private ParticleSystem _flash, _stars, _twinkles, _sparks, _puffs, _fire, _rings;
        private readonly List<ParticleSystem> _systems = new List<ParticleSystem>();
        private readonly List<Material> _owned = new List<Material>();
        private Material _halfMat, _shellMat, _coinMat, _jetMat;
        private Mesh _halfA, _halfB, _coinMesh;
        private List<SpecialMeshes.Part> _shellParts = new List<SpecialMeshes.Part>();
        private AudioClip _rocketClip, _fuseClip, _coinClip;
        private Image _screenFlash;
        private float _screenAge = -1f;
        private MaterialPropertyBlock _mpb, _itemMpb;
        private readonly Dictionary<BlockView, ViewFx> _fx = new Dictionary<BlockView, ViewFx>();
        private readonly List<BlockView> _dead = new List<BlockView>();
        private readonly List<GatherFx> _gathers = new List<GatherFx>();
        private readonly List<Rocket> _rockets = new List<Rocket>();
        private readonly Stack<Rocket> _rocketPool = new Stack<Rocket>();
        private readonly List<BombFx> _bombs = new List<BombFx>();
        private readonly Stack<Proxy> _proxyPool = new Stack<Proxy>();
        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly Stack<Piece> _piecePool = new Stack<Piece>();
        private readonly List<Coin> _coins = new List<Coin>();
        private readonly Stack<Coin> _coinPool = new Stack<Coin>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly Stack<Transform> _tokenPool = new Stack<Transform>();
        private readonly System.Random _rng = new System.Random(29);
        private SpecialTiming _timing;
        private float _time, _stepClock;
        private bool _stepLive, _comboPending;
        private int _gen;

        public bool Ready { get; private set; }
        public bool GathersBirthGroup => true;
        public bool ConsumesSpecialView => true;
        public SpecialTiming Timing => TimingOf(Tune);
        public int LiveCount => !_built ? 0
            : StudioParticles.Count(_flash, _stars, _twinkles, _sparks, _puffs, _fire, _rings)
              + _rockets.Count * PerRocket + ProxiesLive + _pieces.Count + _coins.Count + TokensLive;
        public int RocketsLive => _rockets.Count;
        public int ProxiesLive { get { int n = 0; foreach (var b in _bombs) if (b.Proxy != null) n++; return n; } }
        public int PiecesLive => _pieces.Count;
        public int CoinsLive => _coins.Count;
        public int TokensLive { get { int n = 0; foreach (var f in _flights) if (f.Token != null) n++; return n; } }
        public int GatherCount => _gathers.Count;
        /// <summary>The 2+ specials screen flash's alpha now (0 = off).</summary>
        public float ScreenFlash => _screenFlash != null && _screenFlash.enabled ? _screenFlash.color.a : 0f;

        private RoyalTuning Tune => _game != null && _game.S != null && _game.S.Royal != null ? _game.S.Royal : Defaults;

        // ---------- pure parts (EditMode tests) ----------

        /// <summary>The Blender hand-offs and the material templates are in place (no Shader.Find at runtime).</summary>
        public static bool AssetsReady(ColorHuntSettings s)
        {
            if (s == null || s.RoyalFire == null || s.FlameJet == null || s.CandyPuff == null || s.CandyRing == null) return false;
            if (s.SoftDisc == null || s.StarSprite == null) return false;
            if (s.ToonTemplate == null || s.FlipbookTemplate == null || s.AdditiveTemplate == null) return false;
            var halves = SpecialMeshes.Parts(s.RoyalHalves);
            if (halves.Count != 2 || halves[0].Name != "HalfA" || halves[1].Name != "HalfB") return false;
            return SpecialMeshes.Parts(s.RoyalShell).Count >= 4 && SpecialMeshes.Parts(s.RoyalCoin).Count == 1;
        }

        public static SpecialTiming TimingOf(RoyalTuning t) => new SpecialTiming
        {
            LineSpeed = t.LineSpeed, LineLead = t.LineLead, BombLead = t.BombLead, BombRing = t.BombRing, GatherTime = t.GatherTime,
        };

        /// <summary>Step time when a line fired as <paramref name="f"/> is over the cell <paramref name="cells"/> away: SpecialSchedule's
        /// burst moment of that cell (Delay = LineLead + cells/LineSpeed, scaled by the plan's Pace).</summary>
        public static float RocketReach(SpecialFire f, SpecialTiming t, int cells) =>
            f.Time + (t.LineLead + (t.LineSpeed > 0f ? cells / t.LineSpeed : 0f)) * PaceOf(f);

        /// <summary>Cells a rocket has travelled <paramref name="age"/> s after its fire (0 while the ball squashes); the inverse of RocketReach.</summary>
        public static float RocketCells(SpecialFire f, SpecialTiming t, float age) =>
            Mathf.Max(0f, age / PaceOf(f) - t.LineLead) * t.LineSpeed;

        private static float PaceOf(SpecialFire f) => f.Pace > 0f ? f.Pace : 1f;

        /// <summary>The frame's middle line, in cells from the board centre: where a rocket leaves with its flash.</summary>
        public static float EdgeCells(int n) => (PotaLook.PanoSize * 0.5f + PotaLook.FrameWidth * 0.5f) / PotaLook.GridScale(n);

        /// <summary>The bomb sticker's fuse tip in cells from the block centre (block scale 1, sticker unturned).</summary>
        public static Vector2 FuseTip =>
            new Vector2((0.06f + 0.88f * FuseU - 0.5f) * StickerQuad, (0.06f + 0.88f * FuseV - 0.5f) * StickerQuad);

        /// <summary>The birth pop: 0 → <paramref name="peak"/> by 55 % (ease out), then down to 1 (smoothstep).</summary>
        public static float PopScale(float u, float peak)
        {
            u = Mathf.Clamp01(u);
            const float rise = 0.55f;
            if (u < rise) return peak * StudioCurves.EaseOutCubic(u / rise);
            return Mathf.Lerp(peak, 1f, PotaLook.Smoothstep((u - rise) / (1f - rise)));
        }

        /// <summary>The flame jet's _BaseMap_ST for its loop at <paramref name="age"/> s (frames row-major from the sheet's top-left).</summary>
        public static Vector4 JetFrame(float age)
        {
            int f = Mathf.FloorToInt(age / JetLoop * JetFrames) % JetFrames;
            if (f < 0) f += JetFrames;
            int col = f % JetCols, row = f / JetCols;
            return new Vector4(1f / JetCols, 1f / JetRows, col / (float)JetCols, 1f - (row + 1) / (float)JetRows);
        }

        /// <summary>One flame-jet frame as a 1 × 1 quad pivoted on the nozzle (0.5, 0.9): the flame runs down local −y; white colours.</summary>
        public static Mesh JetQuad()
        {
            if (_jetQuad != null) return _jetQuad;
            float x0 = -JetNozzle.x, x1 = 1f - JetNozzle.x, y0 = -JetNozzle.y, y1 = 1f - JetNozzle.y;
            _jetQuad = new Mesh
            {
                name = "RoyalJetQuad",
                vertices = new[] { new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f), new Vector3(x0, y1, 0f), new Vector3(x1, y1, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
            };
            _jetQuad.RecalculateBounds();
            return _jetQuad;
        }

        /// <summary>
        /// The flame jet's length factor <paramref name="d"/> cells into a rocket's flight: the flame (JetNozzle.y of a
        /// <paramref name="jetCells"/> frame behind the cap) is never longer than the two halves' separation 2d + <see cref="JetGap"/>,
        /// so right after the split it does not stick out past the twin half (it read as a flame in front of the nose); full from
        /// ~0.76 cells on.
        /// </summary>
        public static float JetGrow(float d, float jetCells) =>
            jetCells <= 0f ? 1f : Mathf.Clamp01((2f * Mathf.Max(0f, d) + JetGap) / (JetNozzle.y * jetCells));

        /// <summary>A landing coin: thrown from the rim, gravity, its own spin; no walls (the hoop hangs below the frame).</summary>
        public static void CoinStep(ref Debris d, float dt, float gravity)
        {
            d.V.y -= gravity * dt;
            d.P += d.V * dt;
            d.R = Quaternion.Euler(d.W * dt) * d.R;
            d.Age += dt;
        }

        // ---------- ISpecialStyle ----------

        public void Build(ColorHuntGame game)
        {
            _game = game;
            if (_built) return;
            var s = game != null ? game.S : null;
            Ready = AssetsReady(s);
            if (!Ready) { gameObject.SetActive(false); return; }
            _built = true;
            _mpb = new MaterialPropertyBlock();
            _itemMpb = new MaterialPropertyBlock();
            _timing = Timing;
            _flash = Sys("RoyalFlash", Keep(StudioParticles.Additive(s, s.SoftDisc, 2f)), ParticleSystemRenderMode.Billboard, FlashMax, GlowOrder);
            StudioParticles.SizeOverLife(_flash, u => Mathf.Lerp(0.55f, 1f, StudioCurves.EaseOutCubic(u)));
            StudioParticles.AlphaOverLife(_flash, u => 1f - u);
            _stars = Sys("RoyalStars", Keep(StudioParticles.Additive(s, StudioTextures.RayStar(), 1.8f)), ParticleSystemRenderMode.Billboard, StarMax, GlowOrder);
            StudioParticles.SizeOverLife(_stars, u => Mathf.Lerp(0.4f, 1.1f, StudioCurves.EaseOutCubic(Mathf.Min(1f, u * 1.6f))));
            StudioParticles.AlphaOverLife(_stars, u => 1f - u);
            _twinkles = Sys("RoyalTwinkles", Keep(StudioParticles.Additive(s, s.StarSprite, 2f)), ParticleSystemRenderMode.Billboard, TwinkleMax, GlowOrder);
            StudioParticles.SizeOverLife(_twinkles, u => Mathf.Sin(u * Mathf.PI) * (0.75f + 0.25f * Mathf.Sin(u * 25f)));
            _sparks = Sys("RoyalSparks", Keep(StudioParticles.Additive(s, s.SoftDisc, 2f)), ParticleSystemRenderMode.Stretch, SparkMax, GlowOrder);
            var sr = _sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.05f;
            sr.lengthScale = 1.4f;
            StudioParticles.SizeOverLife(_sparks, u => 1f - u);
            _puffs = Sys("RoyalPuffs", Keep(StudioParticles.Flipbook(s, s.CandyPuff)), ParticleSystemRenderMode.Billboard, PuffMax, PuffOrder);
            StudioParticles.Sheet(_puffs, 4, 4);
            _fire = Sys("RoyalFire", Keep(StudioParticles.Flipbook(s, s.RoyalFire)), ParticleSystemRenderMode.Billboard, FireMax, FireOrder);
            StudioParticles.Sheet(_fire, 4, 4);
            _rings = Sys("RoyalRings", Keep(StudioParticles.Flipbook(s, s.CandyRing)), ParticleSystemRenderMode.Billboard, RingMax, RingOrder);
            StudioParticles.Sheet(_rings, 4, 4);
            var halves = SpecialMeshes.Parts(s.RoyalHalves);                 // sorted: HalfA, HalfB (AssetsReady checked the names)
            _halfA = halves[0].Mesh;
            _halfB = halves[1].Mesh;
            _shellParts = SpecialMeshes.Parts(s.RoyalShell);
            _coinMesh = SpecialMeshes.Parts(s.RoyalCoin)[0].Mesh;
            float ink = Tune.InkWidth;
            _halfMat = Keep(SpecialMeshes.Toon(s, s.RoyalHalves, "RoyalHalves", ink));
            _shellMat = Keep(SpecialMeshes.Toon(s, s.RoyalShell, "RoyalShell", ink));
            _coinMat = Keep(SpecialMeshes.Toon(s, s.RoyalCoin, "RoyalCoin", ink * 0.7f));
            _jetMat = Keep(StudioParticles.Flipbook(s, s.FlameJet));
            _rocketClip = MakeRocket();
            _fuseClip = MakeFuse();
            _coinClip = MakeCoin();
            BuildScreenFlash();
            Activate(false);
        }

        public void Activate(bool on)
        {
            if (!on) Clear();
            gameObject.SetActive(on && _built);
        }

        public void Clear()
        {
            _gen++;
            _gathers.Clear();
            foreach (var r in _rockets) HideRocket(r);
            _rockets.Clear();
            foreach (var b in _bombs) if (b.Proxy != null) HideProxy(b.Proxy);
            _bombs.Clear();
            foreach (var p in _pieces) HidePiece(p);
            _pieces.Clear();
            foreach (var c in _coins) HideCoin(c);
            _coins.Clear();
            foreach (var f in _flights) HideToken(f);
            _flights.Clear();
            foreach (var kv in _fx) if (Alive(kv.Key, kv.Value.Kind)) kv.Key.Scale = 1f;   // NewView resets a recycled view to 1 anyway
            _fx.Clear();
            foreach (var ps in _systems) if (ps != null) ps.Clear();
            _screenAge = -1f;
            if (_screenFlash != null) _screenFlash.enabled = false;
            _comboPending = false;
            _stepLive = false;
        }

        /// <summary>
        /// Each frame for every resting special in play: the bomb's fuse and breath, the line's arrow glow, the golden twinkles. Before
        /// Begin (a staged board: PotaFlow's BoardReveal) and during the studio intro the entrance owns BlockView.Scale, so Royal waits.
        /// </summary>
        public void Idle(BlockView v, float dt)
        {
            if (!_built || v == null || _game.Intro || _game.Current != ColorHuntGame.State.Playing) return;
            var st = State(v);
            st.Kind = v.Block.Special;
            var tune = Tune;
            float t = _time + st.Phase;
            st.Breath = 1f;
            switch (st.Kind)
            {
                case SpecialKind.Bomb:
                    st.Breath = 1f + tune.BreathAmount * Mathf.Sin(2f * Mathf.PI * tune.BreathHz * t);
                    st.Next -= dt;
                    if (st.Next <= 0f) { st.Next = Interval(tune.FuseSparksPerSecond); FuseSparks(FuseWorld(v), 2, 1f); }
                    break;
                case SpecialKind.LineV:
                case SpecialKind.LineH:
                    v.Pulse = Mathf.Max(v.Pulse, tune.ArrowGlow * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * tune.ArrowGlowHz * t)));
                    break;
                case SpecialKind.Golden:
                    st.Next -= dt;
                    if (st.Next <= 0f) { st.Next = Interval(tune.GoldenTwinklesPerSecond); GoldTwinkle(v); }
                    break;
            }
            v.Scale = st.PopScale * st.Breath;
        }

        /// <summary>A birth group's block is pulled into the birth cell (ease in), shrinking and turning; arrived once, on arrival.</summary>
        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived)
        {
            if (v == null) return;
            if (!_built || _game == null) { arrived?.Invoke(v); return; }
            _gathers.Add(new GatherFx
            {
                V = v, Home = HomeView(to), From = v.Pos, To = to, Duration = Mathf.Max(0.01f, duration), Arrived = arrived,
                Spin = (v.Cell % 2 == 0 ? 1f : -1f) * Tune.GatherSpin,
            });
        }

        /// <summary>The special pops 0 → 1.2 → 1 with a white core flash, an eight-ray star, twinkles, Pota's sound and Pota's text.</summary>
        public void Birth(BlockView v, SpecialKind kind)
        {
            if (!_built || _game == null || v == null) return;
            var st = State(v);
            st.Kind = kind;
            st.Pop = 0f;
            st.PopScale = 0f;
            st.Breath = 1f;
            v.Scale = 0f;
            v.Flash = 1f;
            var tune = Tune;
            float s = Scale;
            bool golden = kind == SpecialKind.Golden;
            StudioParticles.Emit(_flash, World(v.Pos, PotaLook.FrontZ + 0.35f), tune.BirthFlashCells * s, 0.12f, Color.white);
            StudioParticles.Emit(_stars, World(v.Pos, PotaLook.FrontZ + 0.4f), tune.BirthStarCells * s, tune.PopTime, golden ? Gold : Color.white,
                Vector3.zero, R() * 45f);
            int n = Mathf.Max(0, tune.BirthTwinkles);
            for (int i = 0; i < n; i++)
            {
                float a = (i + R() * 0.7f) / Mathf.Max(1, n) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                StudioParticles.Emit(_twinkles, World(v.Pos + dir * (0.45f + 0.25f * R()), PotaLook.FrontZ + 0.45f), (0.22f + 0.14f * R()) * s,
                    0.45f + 0.25f * R(), golden ? GoldWhite : StarWhite, Vel(dir * (1f + 0.6f * R())), R() * 360f);
            }
            var sounds = Sounds();
            if (sounds != null)
            {
                if (golden) sounds.PlayClip("ozelDogdu", _game.S.SfxGoldenBorn);
                else sounds.Combo(2);
            }
            var popups = _game.Juice != null ? _game.Juice.Popups : null;
            if (popups != null)
                popups.Show(_game.BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.6f, -PotaLook.LabelZ)), SpecialTexts.Born(kind),
                    PotaLook.HudGold, 72, false, 1.2f);
        }

        /// <summary>A trigger step starts: the style's step clock restarts (Fire's reference) and 2+ specials arm the combo moment.</summary>
        public void TriggerStep(int specials, Vector2 centre)
        {
            _stepClock = 0f;
            _stepLive = true;
            _timing = Timing;
            _comboPending = specials >= 2;
        }

        /// <summary>
        /// One special fires. The game calls this from its Update the frame its step clock passes <see cref="SpecialFire.Time"/>; this
        /// Update runs after the game's with the same clamped dt, so an effect started at <c>_stepClock − Time</c> (≤ 0 now) is, after this
        /// frame's tick, exactly the step's overshoot old: a rocket is over each cell when the plan bursts it.
        /// </summary>
        public void Fire(SpecialFire f)
        {
            if (!_built || _game == null || _game.Board == null) return;
            float age = _stepLive ? Mathf.Clamp(_stepClock - f.Time, -0.05f, 0f) : 0f;
            if (_comboPending && f.Order >= 1)
            {
                _comboPending = false;
                Combo();
            }
            if (f.Kind == SpecialKind.LineV || f.Kind == SpecialKind.LineH) FireLine(f, age);
            else if (f.Kind == SpecialKind.Bomb) FireBomb(f, age);
        }

        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
        {
            if (!_built) { passedGate?.Invoke(); landed?.Invoke(); return; }
            _flights.Add(new Flight { Route = route, PassedGate = passedGate, Landed = landed, Token = TakeToken() });
        }

        // ---------- frame ----------

        private void Update()
        {
            if (!_built || _game == null || _game.BoardRoot == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);           // ColorHuntGame's own clamp: one clock for the plan and the rockets
            _time += dt;
            _stepClock += dt;
            int gen = _gen;
            TickGathers(dt, gen);
            if (gen != _gen) return;
            TickPops(dt);
            TickRockets(dt);
            TickBombs(dt);
            TickPieces(dt);
            TickCoins(dt);
            TickFlights(dt, gen);
            if (gen != _gen) return;
            TickScreenFlash(dt);
        }

        // ---------- birth and idle ----------

        private ViewFx State(BlockView v)
        {
            if (_fx.TryGetValue(v, out var st)) return st;
            st = new ViewFx { Kind = v.Block.Special, Phase = R() * 10f, Next = R() * 0.5f };
            _fx[v] = st;
            return st;
        }

        private bool Alive(BlockView v, SpecialKind kind) =>
            v != null && v.gameObject.activeInHierarchy && v.Block.Special == kind && _game.ViewAt(v.Cell) == v;

        /// <summary>Runs the birth pops (also while the special falls with its column) and forgets views that left the board.</summary>
        private void TickPops(float dt)
        {
            _dead.Clear();
            float time = Mathf.Max(1e-3f, Tune.PopTime), peak = Tune.PopPeak;
            foreach (var kv in _fx)
            {
                var v = kv.Key;
                var st = kv.Value;
                if (!Alive(v, st.Kind)) { _dead.Add(v); continue; }
                if (st.Pop < 0f) continue;
                st.Pop += dt;
                float u = st.Pop / time;
                st.PopScale = u >= 1f ? 1f : PopScale(u, peak);
                if (u >= 1f) st.Pop = -1f;
                v.Scale = st.PopScale * st.Breath;
            }
            foreach (var v in _dead) _fx.Remove(v);
        }

        private void TickGathers(float dt, int gen)
        {
            var tune = Tune;
            for (int i = _gathers.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _gathers.Count) return;
                var g = _gathers[i];
                g.Age += dt;
                float u = Mathf.Clamp01(g.Age / g.Duration), e = u * u;                 // pulled in: slow, then fast
                g.V.Pos = Vector2.LerpUnclamped(g.From, g.To, e);
                g.V.Scale = Mathf.Lerp(1f, tune.GatherShrink, e);
                g.V.Wiggle = g.Spin * e;
                g.V.Flash = Mathf.Max(g.V.Flash, 0.5f * e);
                if (u < 1f) continue;
                _gathers.RemoveAt(i);
                if (g.Home != null && g.Home.isActiveAndEnabled)                     // the birth cell gulps it
                {
                    g.Home.Flash = Mathf.Max(g.Home.Flash, 0.7f);
                    g.Home.Squash = Mathf.Max(g.Home.Squash, 0.6f);
                }
                StudioParticles.Emit(_twinkles, World(g.To, PotaLook.FrontZ + 0.4f), 0.3f * Scale, 0.3f, StarWhite, Vector3.zero, R() * 360f);
                g.Arrived?.Invoke(g.V);
            }
        }

        private BlockView HomeView(Vector2 to)
        {
            var board = _game.Board;
            if (board == null) return null;
            int n = board.Size;
            int col = Mathf.RoundToInt(to.x + (n - 1) * 0.5f), row = Mathf.RoundToInt((n - 1) * 0.5f - to.y);
            if (row < 0 || row >= n || col < 0 || col >= n) return null;
            return _game.ViewAt(ColorHuntRules.Index(n, row, col));
        }

        /// <summary>The bomb sticker's fuse tip in world space, following the view's scale, bob and label angle.</summary>
        private Vector3 FuseWorld(BlockView v)
        {
            float k = v.transform.localScale.x, a = v.LabelAngle * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            var tip = FuseTip * k;
            var at = new Vector2(v.Pos.x, v.Pos.y + v.Bob) + new Vector2(tip.x * cs - tip.y * sn, tip.x * sn + tip.y * cs);
            return World(at, PotaLook.LabelZ * Mathf.Max(1f, k));
        }

        private void FuseSparks(Vector3 at, int count, float speed)
        {
            float s = Scale;
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            for (int k = 0; k < count; k++)
            {
                float a = Mathf.PI * (0.15f + 0.7f * R());                        // up and to the sides
                var v = Vel(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ((1.4f + 1.2f * R()) * speed), 0.4f * R());
                StudioParticles.Emit(_sparks, at, (0.06f + 0.03f * R()) * s, 0.25f + 0.2f * R(), FuseSpark, v);
            }
        }

        private void GoldTwinkle(BlockView v)
        {
            float s = Scale, a = R() * Mathf.PI * 2f, r = 0.22f + 0.16f * R();
            var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            StudioParticles.Emit(_twinkles, World(new Vector2(v.Pos.x, v.Pos.y + v.Bob) + off, PotaLook.LabelZ + 0.05f), (0.2f + 0.12f * R()) * s,
                0.5f + 0.2f * R(), GoldWhite, Vel(new Vector2(0f, 0.3f)), R() * 360f);
        }

        // ---------- line: the split ball's rockets ----------

        private void FireLine(SpecialFire f, float age)
        {
            int n = _game.Board.Size;
            bool vertical = f.Kind == SpecialKind.LineV;                              // LineV clears the column: up and down
            var axis = vertical ? Vector2.up : Vector2.right;
            float coord = vertical ? f.At.y : f.At.x, half = (n - 1) * 0.5f, edge = EdgeCells(n);
            var q = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;   // HalfA's nose (+X) along +axis
            for (int side = 0; side < 2; side++)
            {
                var r = TakeRocket();
                if (r == null) break;                                                 // pool full: the blocks still burst on time
                float sign = side == 0 ? 1f : -1f;
                r.Fire = f;
                r.Timing = _timing;
                r.Dir = axis * sign;
                r.Age = age;
                r.Launched = false;
                r.Leads = side == 0;
                r.Exited = -1f;
                r.Last = half - sign * coord;                                        // cells to the last block on this side
                r.Edge = edge - sign * coord;                                        // cells to the frame edge
                r.NextPuff = Mathf.Max(0.05f, Tune.PuffEvery) * 0.5f;
                r.NextSpark = 0f;
                r.HalfF.sharedMesh = side == 0 ? _halfA : _halfB;
                r.Half.localRotation = side == 0 ? q : q * Quaternion.Euler(0f, 180f, 0f);   // noses out, caps together = the whole ball
                r.Jet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-r.Dir.y, -r.Dir.x) * Mathf.Rad2Deg + 90f);   // flame (−y) backward
                r.Jet.gameObject.SetActive(false);
                PoseRocket(r);
                r.Root.gameObject.SetActive(true);
                _rockets.Add(r);
            }
            StudioParticles.Emit(_flash, World(f.At, RocketZ + 0.1f), 1.6f * Scale, 0.1f, Color.white);
            Sounds()?.PlayClip("cizgi", _game.S.SfxLine);
        }

        /// <summary>The halves separate: jets on; the leading rocket of the pair shakes the camera along the axis and whooshes.</summary>
        private void Launch(Rocket r)
        {
            r.Launched = true;
            r.Jet.gameObject.SetActive(true);
            if (!r.Leads) return;
            var axis = _game.BoardRoot.TransformVector(new Vector3(r.Dir.x, r.Dir.y, 0f));
            var cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            if (cam != null) cam.AxisShake(new Vector2(axis.x, axis.y), Tune.LineShake);
            else _game.Juice?.Shake?.Add(Tune.LineShake);
            Sounds()?.PlayClip("roket", _rocketClip, 0.9f);
            float s = Scale;
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f * R()) / 6f * Mathf.PI * 2f;
                StudioParticles.Emit(_sparks, World(r.Fire.At, RocketZ), 0.08f * s, 0.25f + 0.1f * R(), SparkTone,
                    Vel(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (3f + 2f * R()), 0.5f));
            }
        }

        /// <summary>Cells travelled: the schedule's sweep inside the board, accelerating past the last block to the frame edge.</summary>
        private float Travel(Rocket r)
        {
            float d = RocketCells(r.Fire, r.Timing, r.Age);
            if (d <= r.Last || r.Timing.LineSpeed <= 0f) return d;
            float x = d - r.Last, tau = x / r.Timing.LineSpeed;
            return r.Last + x + 0.5f * Tune.ExitAccel * tau * tau;
        }

        private void TickRockets(float dt)
        {
            if (_rockets.Count == 0) return;
            float tail = Mathf.Max(1e-3f, Tune.ExitTail);
            for (int i = _rockets.Count - 1; i >= 0; i--)
            {
                var r = _rockets[i];
                r.Age += dt;
                if (!r.Launched && r.Age >= r.Timing.LineLead * PaceOf(r.Fire)) Launch(r);
                if (r.Launched)
                {
                    float d = Travel(r);
                    Trail(r, Mathf.Min(d, r.Edge));
                    if (r.Exited < 0f)
                    {
                        if (d >= r.Edge) { r.Exited = 0f; ExitFlash(r); }
                    }
                    else r.Exited += dt;
                    if (r.Exited >= tail) { HideRocket(r); _rockets.RemoveAt(i); continue; }
                }
                PoseRocket(r);
            }
        }

        private void PoseRocket(Rocket r)
        {
            var tune = Tune;
            float lead = Mathf.Max(1e-4f, r.Timing.LineLead * PaceOf(r.Fire)), rs = RootScale, ball = tune.BallCells / ModelBall * rs;
            Vector3 squash;
            float glow, d = 0f;
            if (!r.Launched)
            {
                float u = StudioCurves.EaseOutCubic(Mathf.Clamp01(r.Age / lead));      // "ezilip parlar"
                squash = new Vector3(Mathf.Lerp(1f, tune.SquashAxis, u), Mathf.Lerp(1f, tune.SquashSide, u), Mathf.Lerp(1f, tune.SquashSide, u));
                glow = Mathf.Lerp(1f, tune.LineGlow, u);
            }
            else
            {
                d = Travel(r);
                float since = r.Age - lead, k = Mathf.Clamp01(since / 0.08f);           // springs out of the squash into a speed stretch
                squash = new Vector3(Mathf.Lerp(tune.SquashAxis, 1.1f, k), Mathf.Lerp(tune.SquashSide, 0.94f, k), Mathf.Lerp(tune.SquashSide, 0.94f, k));
                glow = Mathf.Lerp(tune.LineGlow, 1f, Mathf.Clamp01(since / 0.12f));
            }
            float fade = r.Exited >= 0f ? Mathf.Clamp01(1f - r.Exited / Mathf.Max(1e-3f, tune.ExitTail)) : 1f;
            r.Root.localPosition = Local(r.Fire.At + r.Dir * d, RocketZ);
            r.Half.localScale = squash * (ball * fade);
            r.Jet.localPosition = new Vector3(0f, 0f, 0.05f * rs);                    // just behind the cap
            float jet = tune.JetCells * rs * fade;
            r.Jet.localScale = new Vector3(jet, jet * JetGrow(d, tune.JetCells), jet);     // the flame lengthens as the halves part
            _mpb.Clear();
            _mpb.SetVector(BaseColorId, new Vector4(glow, glow, glow, 1f));            // linear, HDR: the toon skin × glow
            r.HalfR.SetPropertyBlock(_mpb);
            if (!r.Launched) return;
            _mpb.Clear();
            _mpb.SetVector(SheetStId, JetFrame(r.Age - lead));
            r.JetR.SetPropertyBlock(_mpb);
        }

        /// <summary>Grey inked puffs every PuffEvery cells and warm sparks every SparkEvery cells behind the rocket, up to the frame edge.</summary>
        private void Trail(Rocket r, float upTo)
        {
            var tune = Tune;
            float s = Scale, puffEvery = Mathf.Max(0.05f, tune.PuffEvery), sparkEvery = Mathf.Max(0.05f, tune.SparkEvery);
            var side = new Vector2(-r.Dir.y, r.Dir.x);
            while (r.NextPuff <= upTo)
            {
                var at = r.Fire.At + r.Dir * (r.NextPuff - 0.3f) + side * ((R() - 0.5f) * 0.25f);
                StudioParticles.Emit(_puffs, World(at, RocketZ - 0.15f), (0.7f + 0.3f * R()) * s, 0.45f + 0.15f * R(), Smoke,
                    Vel(-r.Dir * 0.4f + side * ((R() - 0.5f) * 0.6f)), R() * 360f);
                r.NextPuff += puffEvery;
            }
            if (r.NextSpark > upTo) return;
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            while (r.NextSpark <= upTo)
            {
                var at = r.Fire.At + r.Dir * (r.NextSpark - 0.35f);
                StudioParticles.Emit(_sparks, World(at, RocketZ), (0.07f + 0.03f * R()) * s, 0.25f + 0.15f * R(), SparkTone,
                    Vel(-r.Dir * (2.5f + 2f * R()) + side * ((R() - 0.5f) * 3f), 0.5f + R()));
                r.NextSpark += sparkEvery;
            }
        }

        /// <summary>"Kenarda roket küçük bir flaşla çerçeveden çıkar": a small flash and star on the frame's middle line.</summary>
        private void ExitFlash(Rocket r)
        {
            float s = Scale;
            var at = World(r.Fire.At + r.Dir * r.Edge, RocketZ + 0.1f);
            StudioParticles.Emit(_flash, at, 1.1f * s, 0.12f, new Color(1f, 0.95f, 0.8f, 1f));
            StudioParticles.Emit(_stars, at, 1.5f * s, 0.15f, Color.white, Vector3.zero, R() * 45f);
            for (int k = 0; k < 4; k++)
                StudioParticles.Emit(_sparks, at, 0.08f * s, 0.2f + 0.1f * R(), SparkTone,
                    Vel(r.Dir * (2f + 2f * R()) + new Vector2(-r.Dir.y, r.Dir.x) * ((R() - 0.5f) * 4f), 0.5f));
        }

        // ---------- bomb: wind-up on a copy of the block, then the fireball ----------

        private void FireBomb(SpecialFire f, float age)
        {
            var b = new BombFx { Fire = f, Age = age, Lead = Mathf.Max(0f, _timing.BombLead * PaceOf(f)) };
            var p = TakeProxy();
            if (p != null && Dress(p, _game.ViewAt(f.Cell))) b.Proxy = p;
            else if (p != null) _proxyPool.Push(p);
            _bombs.Add(b);
            if (b.Proxy != null) PoseBomb(b);
            Sounds()?.PlayClip("fitil", _fuseClip, 0.8f);
        }

        /// <summary>The hidden (consumed) view's own pillow mesh, body material, tone and bomb sticker on a pooled copy.</summary>
        private bool Dress(Proxy p, BlockView v)
        {
            if (v == null) return false;
            var bodyF = v.GetComponent<MeshFilter>();
            var bodyR = v.GetComponent<MeshRenderer>();
            var item = v.transform.Find("Item");
            var itemF = item != null ? item.GetComponent<MeshFilter>() : null;
            var itemR = item != null ? item.GetComponent<MeshRenderer>() : null;
            if (bodyF == null || bodyR == null || itemF == null || itemR == null) return false;
            p.BodyF.sharedMesh = bodyF.sharedMesh;
            p.BodyR.sharedMaterial = bodyR.sharedMaterial;
            p.ItemF.sharedMesh = itemF.sharedMesh;
            p.ItemR.sharedMaterial = itemR.sharedMaterial;
            p.Item.localPosition = new Vector3(0f, 0f, -PotaLook.LabelZ);
            p.Item.localRotation = Quaternion.Euler(0f, 0f, v.LabelAngle);
            p.Item.localScale = Vector3.one * StickerQuad;
            _itemMpb.Clear();
            _itemMpb.SetVector(UvRectId, LabelAtlas.UvRect(LabelAtlas.SpecialCell(SpecialKind.Bomb)));
            _itemMpb.SetVector(ColorId, Color.white);
            p.ItemR.SetPropertyBlock(_itemMpb);
            p.Tone = v.Tone;
            p.Base = Mathf.Max(0.01f, v.transform.localScale.x);
            p.Root.gameObject.SetActive(true);
            return true;
        }

        private void BombPose(BombFx b, out float k, out float shiver)
        {
            float u = b.Lead > 0f ? Mathf.Clamp01(b.Age / b.Lead) : 1f;
            k = (b.Proxy != null ? b.Proxy.Base : 1f) * (1f + (Tune.BombSwell - 1f) * StudioCurves.EaseOutCubic(u));
            shiver = Mathf.Sin(b.Age * 70f) * 0.05f * u;
        }

        private void PoseBomb(BombFx b)
        {
            BombPose(b, out float k, out float shiver);
            float u = b.Lead > 0f ? Mathf.Clamp01(b.Age / b.Lead) : 1f;
            float h = Mathf.Sin(Mathf.PI * Mathf.Max(1, Tune.BombBlinks) * u);
            h *= h;                                                                       // BombBlinks humps: red → white-hot → back
            var p = b.Proxy;
            p.Root.localPosition = Local(b.Fire.At + new Vector2(shiver, 0f), 0f);
            p.Root.localScale = Vector3.one * (RootScale * k);
            _mpb.Clear();
            _mpb.SetVector(BaseColorId, PotaLook.BlockColor(Color.Lerp(p.Tone, BlinkRed, h), 0.9f * h * h, false));
            p.BodyR.SetPropertyBlock(_mpb);
        }

        private Vector3 FuseAt(BombFx b)
        {
            BombPose(b, out float k, out float shiver);
            return World(b.Fire.At + new Vector2(shiver, 0f) + FuseTip * k, PotaLook.LabelZ * k);
        }

        private void TickBombs(float dt)
        {
            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                var b = _bombs[i];
                b.Age += dt;
                if (b.Age >= b.Lead) { _bombs.RemoveAt(i); Explode(b); continue; }
                if (b.Proxy != null) PoseBomb(b);
                b.NextSpark -= dt;
                if (b.NextSpark > 0f) continue;
                b.NextSpark += 0.03f;                                                    // "fitil kıvılcım saçar"
                FuseSparks(FuseAt(b), 2, 1.6f);
            }
        }

        private void Explode(BombFx b)
        {
            if (b.Proxy != null) { HideProxy(b.Proxy); b.Proxy = null; }
            var f = b.Fire;
            var tune = Tune;
            float s = Scale;
            StudioParticles.Emit(_flash, World(f.At, PotaLook.FrontZ + 0.6f), tune.CoreFlashCells * s, tune.CoreFlashTime, Color.white);
            StudioParticles.Emit(_fire, World(f.At, FireZ), tune.FireCells / FireFill * s, tune.FireTime, Color.white,
                Vector3.zero, (R() - 0.5f) * 30f);
            StudioParticles.Emit(_rings, World(f.At, PotaLook.FrontZ + 0.3f), tune.RingCells * s, 0.4f, new Color(1f, 0.93f, 0.82f, 1f));
            LaunchShell(f.At);
            int puffs = Mathf.Max(0, tune.BombPuffs);
            for (int k = 0; k < puffs; k++)
            {
                float a = (k + 0.5f * R()) / Mathf.Max(1, puffs) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                StudioParticles.Emit(_puffs, World(f.At + dir * (0.6f + 0.4f * R()), PotaLook.FrontZ + 0.35f), (1f + 0.4f * R()) * s,
                    0.6f + 0.2f * R(), Smoke, Vel(dir * 0.8f + new Vector2(0f, 0.4f)), R() * 360f);
            }
            StudioParticles.Gravity(_sparks, SparkGravity * s);
            for (int k = 0; k < tune.BombSparks; k++)
            {
                float a = R() * Mathf.PI * 2f;
                StudioParticles.Emit(_sparks, World(f.At, FireZ), (0.08f + 0.04f * R()) * s, 0.35f + 0.2f * R(), SparkTone,
                    Vel(new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.3f) * (5f + 3f * R()), 0.8f * R()));
            }
            _game.Fx?.Glow(f.At, FireGlow.linear, 0.9f, 0f, tune.BombLight, tune.BombLightTime);   // the strong point light (Pota's)
            Push(f);
            _game.Juice?.Shake?.Add(tune.BombShake);
            _game.Juice?.TimeCtl?.Dip(_game.S.HitStopTapScale, tune.BombHitStop);
            Sounds()?.PlayClip("patlama", _game.S.SfxBomb);
        }

        /// <summary>"3×3 içindeki bloklar 0,1 hücre dışa itilir": only this step's bursting views (recycled at the drops).</summary>
        private void Push(SpecialFire f)
        {
            int n = _game.Board.Size;
            foreach (int c in ColorHuntRules.SingleArea(n, f.Cell, SpecialKind.Bomb))
            {
                if (c == f.Cell) continue;
                var v = _game.ViewAt(c);
                if (v == null || !v.Exploding || !v.gameObject.activeSelf) continue;
                var away = v.Home - f.At;
                if (away.sqrMagnitude < 1e-6f) continue;
                v.Pos = v.Home + away.normalized * Tune.PushCells;
                v.Flash = Mathf.Max(v.Flash, 0.5f);
            }
        }

        /// <summary>The Blender bomb's eight shell pieces fly out, spinning, with ShatterDebris' deterministic motion (game-root space).</summary>
        private void LaunchShell(Vector2 at)
        {
            float bs = RootScale, ms = Tune.BallCells / ModelBall * bs;
            var centre = Local(at, ShellZ);
            foreach (var part in _shellParts)
            {
                var p = TakePiece();
                if (p == null) break;
                p.F.sharedMesh = part.Mesh;
                p.D = ShatterDebris.Launch(centre, part.Local * (ms / bs), bs, _rng);     // P = centre + Local·ms; speed ∝ the grid scale
                p.D.Size = ms;
                p.Radius = part.Mesh.bounds.extents.magnitude * ms;
                ShatterDebris.Step(ref p.D, 0f, p.Radius);
                PosePiece(p);
                p.T.gameObject.SetActive(true);
                _pieces.Add(p);
            }
        }

        private void TickPieces(float dt)
        {
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                var p = _pieces[i];
                ShatterDebris.Step(ref p.D, dt, p.Radius);
                if (p.D.Age >= p.D.Life) { HidePiece(p); _pieces.RemoveAt(i); continue; }
                PosePiece(p);
            }
        }

        private static void PosePiece(Piece p)
        {
            p.T.localPosition = p.D.P;
            p.T.localRotation = p.D.R;
            p.T.localScale = Vector3.one * (p.D.Size * ShatterDebris.Shrink(p.D.Age, p.D.Life));
        }

        // ---------- 2+ specials ----------

        /// <summary>
        /// The 2+ specials flash: one full-screen white Image under the HUD canvas (first sibling: the counters stay on top). Cheaper than a
        /// camera quad (no material template, no near-plane fit, no sorting against the particles); the frame recorder already moves the
        /// HUD canvas into its captures.
        /// </summary>
        private void BuildScreenFlash()
        {
            var hud = _game != null ? _game.Hud : null;
            if (hud == null) return;
            var go = new GameObject("RoyalScreenFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _screenFlash = go.GetComponent<Image>();
            _screenFlash.raycastTarget = false;
            _screenFlash.color = new Color(1f, 1f, 1f, 0f);
            _screenFlash.enabled = false;
        }

        private void Combo()
        {
            var tune = Tune;
            _game.Juice?.TimeCtl?.SlowMo(tune.ComboSlowScale, tune.ComboSlowTime);
            if (_screenFlash == null) return;
            _screenAge = 0f;
            _screenFlash.enabled = true;
            _screenFlash.color = new Color(1f, 1f, 1f, tune.ScreenFlashAlpha);
        }

        private void TickScreenFlash(float dt)
        {
            if (_screenAge < 0f || _screenFlash == null) return;
            _screenAge += dt;
            float u = _screenAge / Mathf.Max(1e-3f, Tune.ScreenFlashTime);
            if (u >= 1f) { _screenAge = -1f; _screenFlash.enabled = false; return; }
            _screenFlash.color = new Color(1f, 1f, 1f, Tune.ScreenFlashAlpha * (1f - u) * (1f - u));
        }

        // ---------- golden: coin flight, gate, coin burst ----------

        private void TickFlights(float dt, int gen)
        {
            var tune = Tune;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _flights.Count) return;                       // a callback dealt a new board
                var f = _flights[i];
                f.T += dt;
                float t = f.T - f.Route.Delay;
                if (t < 0f) continue;                                                // the step's next ball leaves 0.12 s later
                float rs = f.Route.Scale > 0f ? f.Route.Scale : 1f, s = Scale;
                var pos = GoldenPath.At(f.Route, t) + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * rs);   // Pota's cizToplar z
                f.Spin += dt * tune.TokenSpin;
                if (f.Token != null)
                {
                    f.Token.gameObject.SetActive(true);
                    f.Token.localPosition = pos;
                    f.Token.localRotation = Quaternion.Euler(0f, f.Spin, 0f);
                    f.Token.localScale = Vector3.one * (tune.GoldenCoinCells / ModelCoin * rs);
                }
                float every = Mathf.Max(0.005f, tune.GlitterEvery);
                f.NextGlitter -= dt;
                while (f.NextGlitter <= 0f)
                {
                    f.NextGlitter += every;
                    Glitter(pos, s);
                }
                if (!f.Passed && GoldenPath.PastGate(t))
                {
                    f.Passed = true;
                    var gates = _game.Gates;
                    if (gates != null) gates.Flash(f.Route.Col);                       // Pota's ring flash (#ffe27a), as PotaSpecials
                    GateBurst(pos, s);
                    f.PassedGate?.Invoke();
                    if (gen != _gen) return;
                }
                if (!GoldenPath.Landed(t)) continue;
                _flights.RemoveAt(i);
                HideToken(f);
                Land(f.Route);
                f.Landed?.Invoke();
                if (gen != _gen) return;
            }
        }

        private void Glitter(Vector3 local, float s)
        {
            var jitter = new Vector3((R() - 0.5f) * 0.35f, (R() - 0.5f) * 0.35f, 0f) * s;
            StudioParticles.Emit(_twinkles, transform.TransformPoint(local) + jitter, (0.16f + 0.14f * R()) * s, 0.35f + 0.2f * R(), GoldWhite,
                new Vector3(0f, -0.4f * s, 0f), R() * 360f);
        }

        private void GateBurst(Vector3 local, float s)
        {
            var at = transform.TransformPoint(local);
            StudioParticles.Emit(_stars, at, 1.6f * s, 0.18f, Gold, Vector3.zero, R() * 45f);
            for (int k = 0; k < 6; k++)
            {
                float a = (k + 0.5f * R()) / 6f * Mathf.PI * 2f;
                StudioParticles.Emit(_twinkles, at, (0.2f + 0.12f * R()) * s, 0.4f + 0.2f * R(), GoldWhite,
                    transform.TransformVector(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f)) * (1.2f * s), R() * 360f);
            }
        }

        /// <summary>At the rim: Pota's gold glow, spinning coins thrown up, a gold star and twinkles, altin-inis + the coin clink, the net swings.</summary>
        private void Land(GoldenRoute r)
        {
            var tune = Tune;
            float rs = r.Scale > 0f ? r.Scale : 1f, s = Scale;
            var rim = r.Hoop + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * rs);
            var p = _game.BoardRoot.InverseTransformPoint(_game.transform.TransformPoint(r.Hoop));
            _game.Fx?.Glow(new Vector2(p.x, p.y), PotaLook.Hex(PotaSpecials.GoldenFlash).linear, 1.8f / rs, 4.2f / rs, 5f, 0.22f, -p.z);
            LaunchCoins(rim, rs);
            var at = transform.TransformPoint(rim);
            StudioParticles.Emit(_stars, at, 2.4f * s, 0.25f, Gold, Vector3.zero, R() * 45f);
            int stars = Mathf.Max(0, tune.LandStars);
            for (int k = 0; k < stars; k++)
            {
                float a = (k + 0.6f * R()) / Mathf.Max(1, stars) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.7f + 0.5f, -0.3f);
                StudioParticles.Emit(_twinkles, at, (0.26f + 0.16f * R()) * s, 0.55f + 0.25f * R(), GoldWhite,
                    transform.TransformVector(dir) * ((2f + 1.5f * R()) * rs), R() * 360f);
            }
            var sounds = Sounds();
            sounds?.PlayClip("altinInis", _game.S.SfxGoldenLand);
            sounds?.PlayClip("para", _coinClip, 0.85f);
            var hoop = _game.Hoop;
            if (hoop != null) hoop.Swing();
        }

        private void LaunchCoins(Vector3 rim, float rs)
        {
            var tune = Tune;
            int n = Mathf.Max(0, tune.LandCoins);
            float size = tune.CoinCells / ModelCoin * rs;
            for (int i = 0; i < n; i++)
            {
                var c = TakeCoin();
                if (c == null) break;
                float a = (i + 0.5f * R()) / Mathf.Max(1, n) * Mathf.PI * 2f;
                var v = new Vector3(Mathf.Cos(a) * (1.6f + 1.4f * R()), 3.2f + 2.2f * R() + Mathf.Sin(a) * 0.8f, -(0.8f + 1.4f * R())) * rs;
                var w = new Vector3((R() - 0.5f) * 360f, 540f + 360f * R(), (R() - 0.5f) * 180f);   // mostly a spin about y: the coin flips
                var turn = Quaternion.Euler(R() * 360f, R() * 360f, 0f);
                float life = 0.9f + 0.4f * R();
                c.D = new Debris { P = rim, V = v, W = w, R = turn, Age = 0f, Life = life, Size = size };
                PoseCoin(c);
                c.T.gameObject.SetActive(true);
                _coins.Add(c);
            }
        }

        private void TickCoins(float dt)
        {
            float g = CoinGravity * RootScale;
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                var c = _coins[i];
                CoinStep(ref c.D, dt, g);
                if (c.D.Age >= c.D.Life) { HideCoin(c); _coins.RemoveAt(i); continue; }
                PoseCoin(c);
            }
        }

        private static void PoseCoin(Coin c)
        {
            c.T.localPosition = c.D.P;
            c.T.localRotation = c.D.R;
            c.T.localScale = Vector3.one * (c.D.Size * ShatterDebris.Shrink(c.D.Age, c.D.Life));
        }

        // ---------- pools (each capped; all under this object = the game root's space) ----------

        private Rocket TakeRocket()
        {
            if (_rockets.Count >= RocketMax) return null;
            if (_rocketPool.Count > 0) return _rocketPool.Pop();
            var root = new GameObject("Rocket").transform;
            root.SetParent(transform, false);
            var r = new Rocket { Root = root };
            var half = new GameObject("Half");
            half.transform.SetParent(root, false);
            r.Half = half.transform;
            r.HalfF = half.AddComponent<MeshFilter>();
            r.HalfR = half.AddComponent<MeshRenderer>();
            r.HalfR.sharedMaterial = _halfMat;
            r.HalfR.shadowCastingMode = ShadowCastingMode.On;
            var jet = new GameObject("Jet");
            jet.transform.SetParent(root, false);
            r.Jet = jet.transform;
            jet.AddComponent<MeshFilter>().sharedMesh = JetQuad();
            r.JetR = jet.AddComponent<MeshRenderer>();
            r.JetR.sharedMaterial = _jetMat;
            r.JetR.sortingOrder = JetOrder;
            r.JetR.shadowCastingMode = ShadowCastingMode.Off;
            r.JetR.receiveShadows = false;
            root.gameObject.SetActive(false);
            return r;
        }

        private void HideRocket(Rocket r)
        {
            r.Root.gameObject.SetActive(false);
            _rocketPool.Push(r);
        }

        private Proxy TakeProxy()
        {
            if (ProxiesLive >= ProxyMax) return null;
            if (_proxyPool.Count > 0) return _proxyPool.Pop();
            var root = new GameObject("BombCopy").transform;
            root.SetParent(transform, false);
            var p = new Proxy { Root = root, BodyF = root.gameObject.AddComponent<MeshFilter>(), BodyR = root.gameObject.AddComponent<MeshRenderer>() };
            p.BodyR.shadowCastingMode = ShadowCastingMode.On;                          // as BlockView's body
            p.BodyR.receiveShadows = true;
            var item = new GameObject("Item");
            item.transform.SetParent(root, false);
            p.Item = item.transform;
            p.ItemF = item.AddComponent<MeshFilter>();
            p.ItemR = item.AddComponent<MeshRenderer>();
            p.ItemR.shadowCastingMode = ShadowCastingMode.Off;
            p.ItemR.receiveShadows = false;
            p.ItemR.lightProbeUsage = LightProbeUsage.Off;
            p.ItemR.reflectionProbeUsage = ReflectionProbeUsage.Off;
            p.ItemR.sortingOrder = 2;                                                   // BlockView's sticker order
            root.gameObject.SetActive(false);
            return p;
        }

        private void HideProxy(Proxy p)
        {
            p.Root.gameObject.SetActive(false);
            _proxyPool.Push(p);
        }

        private Piece TakePiece()
        {
            if (_pieces.Count >= ShellMax) return null;
            if (_piecePool.Count > 0) return _piecePool.Pop();
            var go = new GameObject("ShellPiece");
            go.transform.SetParent(transform, false);
            var p = new Piece { T = go.transform, F = go.AddComponent<MeshFilter>() };
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _shellMat;
            r.shadowCastingMode = ShadowCastingMode.On;
            go.SetActive(false);
            return p;
        }

        private void HidePiece(Piece p)
        {
            p.T.gameObject.SetActive(false);
            _piecePool.Push(p);
        }

        private Coin TakeCoin()
        {
            if (_coins.Count >= CoinMax) return null;
            if (_coinPool.Count > 0) return _coinPool.Pop();
            var go = new GameObject("Coin");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _coinMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _coinMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return new Coin { T = go.transform };
        }

        private void HideCoin(Coin c)
        {
            c.T.gameObject.SetActive(false);
            _coinPool.Push(c);
        }

        private Transform TakeToken()
        {
            if (TokensLive >= TokenMax) return null;
            if (_tokenPool.Count > 0) return _tokenPool.Pop();
            var go = new GameObject("GoldenCoin");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _coinMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _coinMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        private void HideToken(Flight f)
        {
            if (f.Token == null) return;
            f.Token.gameObject.SetActive(false);
            _tokenPool.Push(f.Token);
            f.Token = null;
        }

        // ---------- helpers ----------

        private ParticleSystem Sys(string name, Material m, ParticleSystemRenderMode mode, int max, int order)
        {
            var ps = StudioParticles.System(transform, name, m, mode, max, order);
            _systems.Add(ps);
            return ps;
        }

        private Material Keep(Material m)
        {
            _owned.Add(m);
            return m;
        }

        /// <summary>World size of one board cell (particles).</summary>
        private float Scale => _game.BoardRoot.lossyScale.x;
        /// <summary>Game-root units per board cell (meshes under this object, which sits at the game root's origin).</summary>
        private float RootScale => _game.BoardRoot.localScale.x;
        /// <summary>Board cell → world; <paramref name="z"/> in cells toward the camera (Pota's +z).</summary>
        private Vector3 World(Vector2 p, float z) => _game.BoardRoot.TransformPoint(new Vector3(p.x, p.y, -z));
        private Vector3 Local(Vector2 p, float z) => transform.InverseTransformPoint(World(p, z));
        /// <summary>A velocity in cells/s (plus toward the camera) → world units/s.</summary>
        private Vector3 Vel(Vector2 d, float towardCamera = 0f) => _game.BoardRoot.TransformVector(new Vector3(d.x, d.y, -towardCamera));
        private float R() => (float)_rng.NextDouble();
        private float Interval(float rate) => rate > 0f ? (0.7f + 0.6f * R()) / rate : 1e6f;
        private PotaSounds Sounds() => _game != null && _game.Juice != null ? _game.Juice.Sounds : null;

        private void OnDestroy()
        {
            foreach (var m in _owned) if (m != null) Destroy(m);
            foreach (var c in new[] { _rocketClip, _fuseClip, _coinClip }) if (c != null) Destroy(c);
            if (_screenFlash != null) Destroy(_screenFlash.gameObject);
        }

        // ---------- synth sounds (spec "Sesler": rocket "vınn", fuse hiss, coin clink) through PotaSounds.Synth ----------

        /// <summary>The rocket's "vınn": noise through a lowpass opening 900 → 3600 → 900 Hz, under a 260 → 720 Hz tone, 0.42 s.</summary>
        public static AudioClip MakeRocket()
        {
            float lp = 0f, phase = 0f;
            return PotaSounds.Synth("roket", RocketSoundTime, t =>
            {
                int i = Mathf.RoundToInt(t * SynthRate);
                float u = t / RocketSoundTime;
                float cutoff = Mathf.Lerp(900f, 3600f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.3f)));
                lp += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SynthRate)) * (Noise(i) - lp);
                float f = Mathf.Lerp(260f, 720f, Mathf.Min(1f, u * 2.2f)) * (1f - 0.25f * Mathf.Max(0f, u - 0.45f));
                phase += 2f * Mathf.PI * f / SynthRate;
                float env = Mathf.Min(1f, t / 0.025f) * Mathf.Pow(Mathf.Max(0f, 1f - u), 1.6f);
                return env * (0.55f * lp + 0.2f * Mathf.Sin(phase) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 32f * t)));
            });
        }

        /// <summary>The fuse's hiss: high-passed noise with sparse crackles, 0.22 s (covers the 0.18 s wind-up).</summary>
        public static AudioClip MakeFuse()
        {
            float lp = 0f;
            return PotaSounds.Synth("fitil", FuseSoundTime, t =>
            {
                int i = Mathf.RoundToInt(t * SynthRate);
                float n = Noise(i);
                lp += 0.12f * (n - lp);
                float hiss = n - lp;
                float crackle = Noise(i / 220 + 7919) > 0.8f ? Noise(i * 3 + 1) * Mathf.Exp(-(i % 220) / 30f) : 0f;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Clamp01((FuseSoundTime - t) / 0.05f);
                return env * (0.2f * hiss + 0.3f * crackle);
            });
        }

        /// <summary>The coin clink: two metallic strikes 0.07 s apart (C7/G7/E8, then D7/A7/F#8 partials), 0.35 s.</summary>
        public static AudioClip MakeCoin() => PotaSounds.Synth("para", CoinSoundTime, t =>
            0.16f * (Partial(t, 0f, 2093f, 18f) + 0.6f * Partial(t, 0f, 3136f, 24f) + 0.35f * Partial(t, 0f, 5274f, 30f)
                   + 0.8f * Partial(t, 0.07f, 2349f, 18f) + 0.5f * Partial(t, 0.07f, 3520f, 24f) + 0.3f * Partial(t, 0.07f, 5920f, 30f)));

        private static float Partial(float t, float t0, float f, float decay)
        {
            float u = t - t0;
            return u < 0f ? 0f : Mathf.Min(1f, u / 0.002f) * Mathf.Exp(-decay * u) * Mathf.Sin(2f * Mathf.PI * f * u);
        }

        /// <summary>Hash noise in [−1, 1] for sample i (PCG): the synth clips are identical every run, so recordings repeat.</summary>
        private static float Noise(int i)
        {
            unchecked
            {
                uint x = (uint)i * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return x / 4294967295f * 2f - 1f;
            }
        }
    }
}
