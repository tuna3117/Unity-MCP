using System;
using System.Collections.Generic;
using Project.Flow;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// Every number of the Court special look (spec 2026-10-01 §2 "2 · Basketbol"), in ColorHuntSettings.Court so it can be tuned in the
    /// Inspector. Times are game seconds; lengths are board cells (the grid scale applies) unless they say metres. The timing fields
    /// feed the trigger plan through <see cref="CourtSpecials.TimingFor"/>, so the balls, the dunk and the bursts share one clock.
    /// </summary>
    [Serializable]
    public class CourtTuning
    {
        [Header("Birth: the group bounces into the birth cell, the special lands")]
        [Tooltip("Doğuş: the group's blocks bounce into the birth cell in this time (SpecialTiming.GatherTime).")]
        public float GatherTime = 0.22f;
        [Tooltip("First hop of the bounce pass (cells; + 0.1 per cell travelled), the second hop, where the bounce is (0–1), shrink on arrival.")]
        public float GatherHop = 0.5f, GatherBounce = 0.18f, GatherBounceAt = 0.62f, GatherShrink = 0.45f;
        [Tooltip("On the way the block also lifts toward the camera, sin(πu)·GatherLift cells, so it passes in front of the blocks it crosses " +
                 "(at the board plane their bodies and labels cut into it).")]
        public float GatherLift = 0.8f;
        [Tooltip("The new special drops from LandDrop cells onto its cell in LandTime, then squashes (Pota çarpma 0.6) and flashes.")]
        public float LandDrop = 0.45f, LandTime = 0.1f, BornSquash = 0.6f, BornFlash = 0.7f;
        [Tooltip("White court-line ring under the landing: from → to (cells) in time.")]
        public float CourtRingFrom = 0.9f, CourtRingTo = 2.4f, CourtRingTime = 0.35f;
        [Tooltip("Light dust at the landing (cells); the dust flipbook's life (vfx-dust-ring: 0.4 s).")]
        public float BirthDust = 1.6f, DustTime = 0.4f;

        [Header("Idle: dribbling specials, a spinning golden ball")]
        [Tooltip("Bounces per second (dripling 2 Hz).")]
        public float IdleHz = 2f;
        [Tooltip("Hop height (cells) and the squash at the bottom (BlockView.Squash: 1 − 0.12·squash).")]
        public float IdleHop = 0.06f, IdleSquash = 0.5f;
        [Tooltip("Golden ball's sticker spin (degrees per second); it glows with the game's golden pulse.")]
        public float GoldenSpin = 40f;

        [Header("Line: a hard dribble, two balls on fire to both ends")]
        [Tooltip("The hard dribble before the balls leave (SpecialTiming.LineLead) and its squash.")]
        public float DribbleTime = 0.08f;
        [Tooltip("How flat the dribble squashes the stand-in ball (0–1).")]
        public float DribbleSquash = 0.4f;
        [Tooltip("A ball reaches the far edge of a 7×7 (6 cells) in LineSweep: LineSpeed = SweepCells / LineSweep (cells per second).")]
        public float LineSweep = 0.3f, SweepCells = 6f;
        [Tooltip("Ball sizes (cells): the stand-in on the cell, the flying balls; their spin (degrees per second).")]
        public float StandInCells = 0.82f, BallCells = 0.72f, BallSpin = 720f;
        [Tooltip("A ball leaves over ExitCells past the last cell's outer edge, shrinking.")]
        public float ExitCells = 0.7f;
        [Tooltip("A passed block hops LineHop cells over LineHopTime, starting when the ball is KickLead cells away (it bursts on arrival).")]
        public float KickLead = 0.6f, LineHop = 0.15f, LineHopTime = 0.12f;
        [Tooltip("The flame tail (vfx-flame-jet): square quad size (cells), one loop (s).")]
        public float FlameCells = 1.8f, FlameLoop = 0.3f;
        [Tooltip("The flame tail is this many times wider across the path than the sheet draws it (the sheet's flame is a thin jet).")]
        public float FlameWide = 1.7f;
        [Tooltip("Flame tint (linear multiply, may exceed 1): a little more yellow than the sheet, so the tail reads apart from the orange ball.")]
        [ColorUsage(false, true)]
        public Color FlameTint = new Color(1f, 1.35f, 0.7f, 1f);
        [Tooltip("Speed lines (vfx-speed-lines, one-shot): longest stretch along the path, height (cells), play time (s).")]
        public float SpeedLength = 2.6f, SpeedHeight = 0.9f, SpeedTime = 0.25f;
        [Tooltip("White swish ring where a ball meets the frame: from → to (cells) in time; file-swish volume.")]
        public float SwishRingFrom = 0.5f, SwishRingTo = 0.9f, SwishRingTime = 0.25f, SwishVolume = 0.7f;
        [Tooltip("The pass whoosh (synth court-pas) volume.")]
        public float PassVolume = 0.8f;

        [Header("Bomb: the dunk")]
        [Tooltip("Hop toward the camera (s).")]
        public float HopTime = 0.14f;
        [Tooltip("At the top of the hop: this scale, this high and this far toward the camera (cells).")]
        public float HopScale = 1.6f, HopUp = 0.35f, HopToward = 2f;
        [Tooltip("Hang in the air (s), slam down (s) to SlamScale; impact = Hop + Hang + Slam = SpecialTiming.BombLead.")]
        public float HangTime = 0.08f, SlamTime = 0.07f, SlamScale = 1.25f;
        [Tooltip("The ball lies flat on the floor for ImpactHold s, then is gone.")]
        public float ImpactHold = 0.05f, ImpactSquash = 0.55f;
        [Tooltip("Seconds between the impact and each further ring of the 3×3 (SpecialTiming.BombRing).")]
        public float BombRing = 0.03f;
        [Tooltip("Floor crack decal (cells) fading out in CrackFade s; the dust ring (cells).")]
        public float CrackCells = 2.6f, CrackFade = 0.6f, DustCells = 3.4f;
        [Tooltip("The 3×3 blocks hop this high (cells) over this time, bursting on the way up.")]
        public float BlockHop = 0.25f, BlockHopTime = 0.1f;
        [Tooltip("Lettering (SLAM!, COMBO!, SWISH!): life, pop 0 → peak → 1, widths in cells (the stills are 2:1).")]
        public float LetterTime = 0.5f, LetterPeak = 1.15f, SlamLetterCells = 4f, ComboLetterCells = 4.4f, SwishLetterCells = 3.6f;
        [Tooltip("Flashbulbs on the frame edges: how many (min–max).")]
        public int FlashbulbsMin = 6, FlashbulbsMax = 10;
        [Tooltip("Flashbulbs: within this window after the impact (s), size (cells), life (s).")]
        public float FlashbulbWindow = 0.4f, FlashbulbCells = 1.1f, FlashbulbLife = 0.14f;
        [Tooltip("Impact shake (additive) and the camera kick toward the impact (fraction of the way, real seconds).")]
        public float SlamShake = 0.24f, KickAmount = 0.08f, KickTime = 0.22f;

        [Header("2+ specials in one step")]
        [Tooltip("Slow motion (scale, real seconds); the short crowd: the first CrowdCut s of kalabalik, faded over CrowdFade s.")]
        public float ComboSlowScale = 0.5f;
        public float ComboSlowTime = 0.2f, CrowdCut = 1.2f, CrowdFade = 0.35f, CrowdVolume = 0.8f;

        [Header("Golden ball: rolls to the gate, swish into the hoop")]
        [Tooltip("Ball size (cells), roll spin (rad/s), SWISH! height above the rim (metres).")]
        public float GoldenBallCells = 0.7f;
        public float GoldenRoll = 12f, SwishLift = 1.4f;
        [Tooltip("SWISH! stands this far behind the hoop's centre (metres), so the landing ball and the gold confetti fly in front of it.")]
        public float SwishBack = 0.3f;
        [Tooltip("Gold confetti pieces per landing.")]
        public int ConfettiCount = 28;
        [Tooltip("The six net rings flex in turn: amount, each ring's time (s), the gap between rings (s).")]
        public float NetRingAmount = 0.2f, NetRingTime = 0.25f, NetRingGap = 0.05f;
    }

    /// <summary>
    /// 2 · Court (spec 2026-10-01 §2 "Basketbol"): Pota's own world — speed lines, the court, the dunk, the net. Birth: the group's blocks
    /// bounce into the birth cell like bounce passes and the special lands with a squash, a white court-line ring and light dust. Idle:
    /// specials dribble at 2 Hz (a squash at the bottom), the golden ball spins its sticker and glows (the game's pulse). Line: one hard
    /// dribble, then two basketballs on fire (Blender ball, flame-jet tail, speed lines) fly to both ends; passed blocks hop and burst in
    /// order (the trigger plan's times, Pace included); a white swish ring where they meet the frame. Bomb = a dunk: the ball hops toward
    /// the camera (1 → 1.6), hangs, slams; the impact (at BombLead) cracks the floor, throws a dust ring, hops the 3×3 that then bursts,
    /// "SLAM!", flashbulbs on the frame, shake and a camera kick. 2+ specials in a step: slow motion, "COMBO!", a short crowd. Golden: the
    /// gold ball rolls down through its gate and arcs into the hoop; the net swings ring by ring, "SWISH!", gold confetti. Everything is
    /// pooled and on game time, in the game root's space (this object sits at its origin); materials come from the settings' templates.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after ColorHuntGame: the step clock equals the game's step time at the end of every frame
    public class CourtSpecials : MonoBehaviour, ISpecialStyle
    {
        public const int BallMax = 8, GoldenMax = 4, FlameMax = 8, SpeedMax = 8, RingMax = 12, CrackMax = 4, LetterMax = 4,
            DustMax = 16, FlashMax = 24, ConfettiMax = 60;
        /// <summary>Every pooled piece and particle this look can have alive at once.</summary>
        public const int PoolTotal = BallMax + GoldenMax + FlameMax + SpeedMax + RingMax + CrackMax + LetterMax + DustMax + FlashMax + ConfettiMax;
        /// <summary>Spec §1.6: this look's own live count stays under what the Pota burst pools (ColorHuntFx) leave of the budget.</summary>
        public const int Cap = BurstBudget.Max - BurstBudget.Pota;

        // Depths in Pota board cells toward the camera (block fronts 0.45, pillow bulge + 0.1, labels 0.58, a 1.24× swell's bulge 0.68).
        public const float StandInZ = 0.2f, BallZ = PotaLook.FrontZ + 0.55f, RingZ = PotaLook.FrontZ + 0.15f, DecalZ = PotaLook.FrontZ + 0.3f,
            FlashZ = PotaLook.FrontZ + 0.5f, LetterZ = PotaLook.FrontZ + 2.6f;
        // Transparent sorting: the birth's court ring under the block labels (2/3); the impact's crack and dust ring, the trails, the swish
        // ring and the flashbulbs over Pota's burst pools (ColorHuntFx 5–9: drawn under them, the 3×3's flashes washed the crack and the
        // dust out within 0.1 s of the impact); the lettering over everything. Pota's shards and confetti are opaque: depth still hides
        // the crack behind a shard that flies in front of it.
        public const int RingOrder = 1, CrackOrder = 10, DustOrder = 11, SpeedOrder = 12, FlameOrder = 13, SwishOrder = 14, FlashOrder = 15,
            LetterOrder = 30;
        /// <summary>Toon outline of the balls in world metres (Project/Toon extrudes along the world normal).</summary>
        public const float Outline = 0.02f;

        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Vector4 Whole = new Vector4(0f, 0f, 1f, 1f);
        private static readonly CourtTuning Defaults = new CourtTuning();
        private static readonly Color[] Gold = { PotaLook.Hex(0xffc93c), PotaLook.Hex(0xffe27a), PotaLook.Hex(0xeeb900), PotaLook.Hex(0xfff1b0) };
        private static Texture2D _ringTex;

        private enum Kind { Ring, Crack, Letter, Flame, Speed }
        private enum Mode { Line, Dunk }

        private sealed class Quad
        {
            public Transform T;
            public MeshRenderer R;
            public Kind Kind;
            public bool Live;
            public float Age, Life, From, To, Aspect = 1f, Turn;
            public Vector3 At;
        }

        private sealed class Ball
        {
            public Transform T;
            public bool Live;
            public Mode Mode;
            public float Age;
            public SpecialFire Fire;
            public Vector2 Dir;
            public float Edge;
            public int[] Cells;
            public int NextKick;
            public bool Lead, Whooshed, Edged, Impacted, SpeedDone;
            public Quad Flame, Speed;
        }

        private sealed class Flight
        {
            public GoldenRoute Route;
            public Action PassedGate, Landed;
            public Transform Ball;
            public float Age;
            public bool Passed;
        }

        private sealed class Hop { public BlockView V; public float Age, Height, Time; }
        private sealed class Gathering { public BlockView V; public Vector2 From, To; public float Age, Time; public bool Bounced; public Action<BlockView> Arrived; }
        private sealed class Flashbulb { public float Wait; public Vector2 At; }

        private ColorHuntGame _game;
        private bool _built, _crowdMade;
        private Mesh _ballMesh, _quadMesh;
        private float _ballSize = 0.45f;
        private Vector3 _ballCentre;
        private Material _ballMat, _goldMat, _ringMat, _crackMat, _slamMat, _swishMat, _comboMat, _flameMat, _speedMat;
        private readonly List<Material> _materials = new List<Material>();
        private MaterialPropertyBlock _mpb;
        private ParticleSystem _dust, _birthDust, _flash, _confetti;
        private AudioClip _pass, _crowd;
        private readonly Stack<Ball> _freeBalls = new Stack<Ball>();
        private readonly Stack<Transform> _freeGolden = new Stack<Transform>();
        private readonly Dictionary<Kind, Stack<Quad>> _freeQuads = new Dictionary<Kind, Stack<Quad>>();
        private readonly List<Ball> _balls = new List<Ball>();
        private readonly List<Quad> _quads = new List<Quad>();
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly List<Hop> _hops = new List<Hop>();
        private readonly List<Gathering> _gathers = new List<Gathering>();
        private readonly List<Flashbulb> _bulbs = new List<Flashbulb>();
        private readonly Dictionary<BlockView, float> _landing = new Dictionary<BlockView, float>();
        private readonly List<BlockView> _keys = new List<BlockView>();
        private readonly System.Random _rng = new System.Random(23);
        private float _clock, _stepClock, _netT = -1f;
        private bool _stepActive, _comboArmed;
        private Vector2 _comboCentre;
        private GiantHoop _hoop;
        private int _gen;

        public bool Ready { get; private set; }
        public int LiveCount => _balls.Count + _flights.Count + _quads.Count + StudioParticles.Count(_dust, _birthDust, _flash, _confetti);
        public SpecialTiming Timing => TimingFor(Tuning);
        public bool GathersBirthGroup => true;
        public bool ConsumesSpecialView => true;
        /// <summary>The settings' tuning (ColorHuntSettings.Court), or the defaults.</summary>
        public CourtTuning Tuning => _game != null && _game.S != null && _game.S.Court != null ? _game.S.Court : Defaults;
        /// <summary>Basketballs on screen: line balls, dunk balls, golden flights.</summary>
        public int BallsLive => _balls.Count + _flights.Count;
        public int LettersLive
        {
            get
            {
                int n = 0;
                foreach (var q in _quads) if (q.Kind == Kind.Letter) n++;
                return n;
            }
        }
        /// <summary>The live lettering quads (SLAM!, COMBO!, SWISH!): unit quads scaled in the game root.</summary>
        public IEnumerable<Transform> Letters { get { foreach (var q in _quads) if (q.Kind == Kind.Letter) yield return q.T; } }
        /// <summary>The live swish-ring quads (a line ball reaching the frame): unit quads scaled in the game root.</summary>
        public IEnumerable<Transform> Rings { get { foreach (var q in _quads) if (q.Kind == Kind.Ring) yield return q.T; } }
        /// <summary>GiantHoop.Swing calls made by golden landings.</summary>
        public int NetSwings { get; private set; }
        public int Landings { get; private set; }

        // ---------- pure timing and shapes (CourtTests) ----------

        public static bool AssetsReady(ColorHuntSettings s) =>
            s != null && s.CourtBall != null && s.CourtDust != null && s.SpeedLines != null && s.FlameJet != null && s.FloorCrack != null
            && s.LetterSlam != null && s.LetterSwish != null && s.LetterCombo != null && SpecialMeshes.Parts(s.CourtBall).Count > 0;

        /// <summary>The trigger plan's timing for this look: the balls, the dunk and SpecialSchedule share it.</summary>
        public static SpecialTiming TimingFor(CourtTuning t) => new SpecialTiming
        {
            LineSpeed = t.SweepCells / Mathf.Max(1e-3f, t.LineSweep),
            LineLead = t.DribbleTime,
            BombLead = t.HopTime + t.HangTime + t.SlamTime,
            BombRing = t.BombRing,
            GatherTime = t.GatherTime,
        };

        private static float PaceOf(SpecialFire f) => f.Pace > 0f ? f.Pace : 1f;

        /// <summary>Cells a line ball has covered <paramref name="tau"/> s after its fire (0 while it dribbles): SpecialSchedule's Delay inverted.</summary>
        public static float LineDistance(SpecialFire f, CourtTuning t, float tau)
        {
            var timing = TimingFor(t);
            return Mathf.Max(0f, (tau / PaceOf(f) - timing.LineLead) * timing.LineSpeed);
        }

        /// <summary>Seconds into the step when a line ball reaches the cell <paramref name="cells"/> away: that cell's burst in the plan.</summary>
        public static float ReachTime(SpecialFire f, CourtTuning t, float cells)
        {
            var timing = TimingFor(t);
            return f.Time + (timing.LineLead + cells / timing.LineSpeed) * PaceOf(f);
        }

        /// <summary>Seconds into the step when the dunk hits the floor.</summary>
        public static float ImpactTime(SpecialFire f, CourtTuning t) => f.Time + TimingFor(t).BombLead * PaceOf(f);

        /// <summary>Seconds into the step when ring <paramref name="ring"/> (Chebyshev distance) of the bomb's 3×3 bursts.</summary>
        public static float BombBurstTime(SpecialFire f, CourtTuning t, int ring) =>
            f.Time + (TimingFor(t).BombLead + ring * t.BombRing) * PaceOf(f);

        public struct DunkPose
        {
            /// <summary>Cells up from the bomb's cell; cells toward the camera.</summary>
            public float Up, Toward;
            /// <summary>Size × the stand-in's; Squash = height ÷ width (1 = round); Spin in degrees.</summary>
            public float Scale, Squash, Spin;
            public bool Impact;
        }

        /// <summary>The dunk <paramref name="tau"/> s after the bomb's fire: hop (ease-out) to 1.6 toward the camera, hang, slam (ease-in), flat at BombLead·pace.</summary>
        public static DunkPose Dunk(float tau, float pace, CourtTuning t)
        {
            pace = pace > 0f ? pace : 1f;
            float lead = TimingFor(t).BombLead;
            float impact = lead * pace, hop = t.HopTime * pace, hung = (t.HopTime + t.HangTime) * pace;
            var p = new DunkPose { Scale = 1f, Squash = 1f };
            if (tau <= 0f) return p;
            if (tau >= impact)
            {
                p.Scale = t.SlamScale;
                p.Squash = t.ImpactSquash;
                p.Spin = 720f;
                p.Impact = true;
                return p;
            }
            if (tau < hop)
            {
                float u = tau / hop, e = 1f - (1f - u) * (1f - u);
                p.Up = t.HopUp * e;
                p.Toward = t.HopToward * e;
                p.Scale = 1f + (t.HopScale - 1f) * e;
                p.Spin = 360f * e;
            }
            else if (tau < hung)
            {
                float u = (tau - hop) / Mathf.Max(1e-5f, hung - hop);
                p.Up = t.HopUp + 0.04f * Mathf.Sin(Mathf.PI * u);
                p.Toward = t.HopToward;
                p.Scale = t.HopScale;
                p.Spin = 360f + 60f * u;                                                // turns slowly while it hangs
            }
            else
            {
                float u = (tau - hung) / Mathf.Max(1e-5f, impact - hung), e = u * u;
                p.Up = t.HopUp * (1f - e);
                p.Toward = t.HopToward * (1f - e);
                p.Scale = Mathf.Lerp(t.HopScale, t.SlamScale, e);
                p.Spin = 420f + 300f * u;
            }
            return p;
        }

        /// <summary>Lettering size: 0 → peak in 0.1 s (ease-out), back to 1 by 0.2 s.</summary>
        public static float LetterScale(float age, CourtTuning t)
        {
            if (age <= 0f) return 0f;
            if (age < 0.1f)
            {
                float u = age / 0.1f;
                return t.LetterPeak * (1f - (1f - u) * (1f - u));
            }
            if (age < 0.2f) return Mathf.Lerp(t.LetterPeak, 1f, (age - 0.1f) / 0.1f);
            return 1f;
        }

        /// <summary>Lettering opacity: full for 75 % of its life, then out by LetterTime.</summary>
        public static float LetterAlpha(float age, CourtTuning t) => Mathf.Clamp01((t.LetterTime - age) / (0.25f * Mathf.Max(1e-3f, t.LetterTime)));

        /// <summary>Net ring <paramref name="ring"/> (0 = under the rim) widens 1 + amount·sin(πu) over its time, NetRingGap after the one above.</summary>
        public static float NetRing(float time, int ring, CourtTuning t)
        {
            float u = (time - ring * t.NetRingGap) / Mathf.Max(1e-3f, t.NetRingTime);
            return u <= 0f || u >= 1f ? 1f : 1f + t.NetRingAmount * Mathf.Sin(Mathf.PI * u);
        }

        public static float NetWaveTime(CourtTuning t, int rings) => Mathf.Max(0, rings - 1) * t.NetRingGap + t.NetRingTime;

        /// <summary>A bounce pass in the board plane: straight across, two parabolic hops in +y (the bounce at GatherBounceAt).</summary>
        public static Vector2 GatherPos(Vector2 from, Vector2 to, float u, CourtTuning t)
        {
            u = Mathf.Clamp01(u);
            var p = Vector2.LerpUnclamped(from, to, u);
            float at = Mathf.Clamp(t.GatherBounceAt, 0.05f, 0.95f), d = Vector2.Distance(from, to), h;
            if (u < at)
            {
                float a = u / at;
                h = (t.GatherHop + 0.1f * d) * 4f * a * (1f - a);
            }
            else
            {
                float b = (u - at) / (1f - at);
                h = t.GatherBounce * 4f * b * (1f - b);
            }
            return new Vector2(p.x, p.y + h);
        }

        /// <summary>
        /// The flame tail's length factor <paramref name="d"/> cells into a line ball's flight. The two balls of a line part 2d apart and
        /// each tail streams back toward the twin: a full tail (0.9 of FlameCells behind the nozzle) right after the split stuck out past
        /// the twin's front and read as a flame shooting ahead of it. So the tail reaches at most into the twin's half (hidden behind it).
        /// </summary>
        public static float FlameGrow(float d, CourtTuning t) =>
            Mathf.Clamp01((2f * Mathf.Max(0f, d) + 0.5f * t.BallCells) / Mathf.Max(1e-3f, 0.9f * t.FlameCells));

        /// <summary>The dribble: |sin| hops, IdleHz bounces a second, 0 at each bottom.</summary>
        public static float IdleHop(float time, CourtTuning t) => t.IdleHop * Mathf.Sin(Mathf.PI * Frac(time * t.IdleHz));

        /// <summary>UV rect (u, v, w, h; v from the bottom) of frame <paramref name="f"/> of a cols × rows sheet, row by row from the top-left.</summary>
        public static Vector4 Frame(int f, int cols, int rows)
        {
            int c = f % cols, r = f / cols;
            return new Vector4(c / (float)cols, 1f - (r + 1) / (float)rows, 1f / cols, 1f / rows);
        }

        /// <summary>A thin white court-line ring (radius 0.86–0.97 of the half size, anti-aliased): the births' ring and the swish ring.</summary>
        public static Texture2D RingTexture(int size = 128)
        {
            if (_ringTex != null && _ringTex.width == size) return _ringTex;
            var px = new Color32[size * size];
            float edge = 2f / size;                                                     // one pixel in the −1…1 square
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.86f) / edge + 0.5f) * Mathf.Clamp01((0.97f - r) / edge + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            _ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "CourtLineRing", wrapMode = TextureWrapMode.Clamp };
            _ringTex.SetPixels32(px);
            _ringTex.Apply(false, false);
            return _ringTex;
        }

        /// <summary>The pass "vıııt" (synth court-pas, 0.24 s): white noise through a one-pole band sweeping up and back, under a 320 → 960 Hz glide.</summary>
        public static AudioClip MakePass()
        {
            uint seed = 0x9E3779B9u;
            float low = 0f, lower = 0f;
            return PotaSounds.Synth("court-pas", 0.24f, t =>
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;                // xorshift: the same whoosh every time
                float noise = (seed & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
                float sweep = t < 0.12f ? t / 0.12f : 1f - (t - 0.12f) / 0.12f;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * (500f + 3500f * Mathf.Clamp01(sweep)) / 44100f);
                low += a * (noise - low);
                lower += a * 0.35f * (low - lower);
                float env = Mathf.Min(1f, t / 0.03f) * Mathf.Clamp01((0.24f - t) / 0.12f);
                float glide = Mathf.Sin(2f * Mathf.PI * (320f * t + 0.5f * (960f - 320f) / 0.24f * t * t));
                return env * (0.55f * (low - lower) + 0.12f * glide);
            });
        }

        /// <summary>"kalabalik-kisa": the first <paramref name="seconds"/> of the crowd sample, faded out over <paramref name="fade"/> s; the whole clip if its data cannot be read.</summary>
        public static AudioClip ShortCrowd(AudioClip crowd, float seconds, float fade)
        {
            if (crowd == null) return null;
            if (crowd.loadState != AudioDataLoadState.Loaded) crowd.LoadAudioData();
            int ch = Mathf.Max(1, crowd.channels), rate = crowd.frequency;
            int n = Mathf.Min(crowd.samples, Mathf.CeilToInt(seconds * rate));
            var data = new float[Mathf.Max(0, n) * ch];
            if (n <= 0 || !crowd.GetData(data, 0)) return crowd;
            int fadeN = Mathf.Clamp(Mathf.RoundToInt(fade * rate), 1, n);
            for (int i = n - fadeN; i < n; i++)
            {
                float g = (n - 1 - i) / (float)fadeN;
                for (int c = 0; c < ch; c++) data[i * ch + c] *= g;
            }
            var clip = AudioClip.Create("kalabalik-kisa", n, ch, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        // ---------- ISpecialStyle ----------

        public void Build(ColorHuntGame game)
        {
            _game = game;
            var s = game != null ? game.S : null;
            Ready = AssetsReady(s);
            if (!Ready) { gameObject.SetActive(false); return; }
            if (!_built)
            {
                _built = true;
                var part = SpecialMeshes.Parts(s.CourtBall)[0];
                _ballMesh = part.Mesh;
                _ballSize = Mathf.Max(1e-3f, _ballMesh.bounds.size.x);
                _ballCentre = _ballMesh.bounds.center;
                _quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");             // faces −z, the camera, like the block labels
                _ballMat = Keep(SpecialMeshes.Toon(s, s.CourtBall, "CourtBall", Outline));
                _goldMat = Keep(SpecialMeshes.Toon(s, s.CourtBall, "CourtGoldBall", Outline));
                var gold = PotaLook.Hex(PotaSpecials.BallGold).linear;
                var basis = PotaLook.Hex(PotaSpecials.BallBase).linear;                 // the skin's #f07d1d becomes #ffc93c
                _goldMat.SetColor("_BaseColor", new Color(gold.r / basis.r, gold.g / basis.g, gold.b / basis.b, 1f).gamma);   // SetColor linearises
                _ringMat = QuadMaterial(s, RingTexture(), "CourtLineRing");
                _crackMat = QuadMaterial(s, s.FloorCrack, "CourtCrack");
                _slamMat = QuadMaterial(s, s.LetterSlam, "CourtSlam");
                _swishMat = QuadMaterial(s, s.LetterSwish, "CourtSwish");
                _comboMat = QuadMaterial(s, s.LetterCombo, "CourtCombo");
                _flameMat = QuadMaterial(s, s.FlameJet, "CourtFlame");
                _speedMat = QuadMaterial(s, s.SpeedLines, "CourtSpeedLines");
                _mpb = new MaterialPropertyBlock();
                for (int i = 0; i < BallMax; i++) _freeBalls.Push(new Ball { T = NewBall("CourtBall" + i, _ballMat) });
                for (int i = 0; i < GoldenMax; i++) _freeGolden.Push(NewBall("CourtGoldBall" + i, _goldMat));
                Pool(Kind.Ring, RingMax);
                Pool(Kind.Crack, CrackMax);
                Pool(Kind.Letter, LetterMax);
                Pool(Kind.Flame, FlameMax);
                Pool(Kind.Speed, SpeedMax);
                // The dunk's dust ring over the bursts; a birth's light dust under the block labels (with the court ring), so the newborn
                // special's sticker stays readable through it. The two share DustMax.
                var dustMat = Keep(StudioParticles.Flipbook(s, s.CourtDust));
                _dust = StudioParticles.System(transform, "CourtDust", dustMat, ParticleSystemRenderMode.Billboard, DustMax / 2, DustOrder);
                StudioParticles.Sheet(_dust, 4, 4);
                _birthDust = StudioParticles.System(transform, "CourtBirthDust", dustMat, ParticleSystemRenderMode.Billboard, DustMax - DustMax / 2, RingOrder);
                StudioParticles.Sheet(_birthDust, 4, 4);
                _flash = StudioParticles.System(transform, "CourtFlashbulbs", Keep(StudioParticles.Additive(s, StudioTextures.RayStar(), 2f)),
                    ParticleSystemRenderMode.Billboard, FlashMax, FlashOrder);
                StudioParticles.SizeOverLife(_flash, u => u < 0.25f ? Mathf.Lerp(0.35f, 1f, u / 0.25f) : Mathf.Lerp(1f, 0.6f, (u - 0.25f) / 0.75f));
                StudioParticles.AlphaOverLife(_flash, u => 1f - u);
                _confetti = StudioParticles.System(transform, "CourtGoldConfetti", Keep(ColorHuntFx.LitMaterial(s.ParticleLitTemplate, 0.6f, 0.4f, 1f)),
                    ParticleSystemRenderMode.Mesh, ConfettiMax, 7, true);
                _confetti.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                StudioParticles.Gravity(_confetti, 6f);
                _pass = MakePass();
                _crowd = ShortCrowd(s.SfxCrowd, Tuning.CrowdCut, Tuning.CrowdFade);
                _crowdMade = _crowd != null && _crowd != s.SfxCrowd;
            }
            Activate(false);
        }

        public void Activate(bool on)
        {
            if (!on) Clear();
            gameObject.SetActive(on);
        }

        public void Clear()
        {
            _gen++;
            foreach (var q in _quads.ToArray()) RecycleQuad(q);
            foreach (var b in _balls)
            {
                b.Flame = null;
                b.Speed = null;
                HideBall(b);
            }
            _balls.Clear();
            foreach (var f in _flights) HideGolden(f);
            _flights.Clear();
            foreach (var h in _hops) if (h.V != null && h.V.Exploding) h.V.Bob = 0f;
            _hops.Clear();
            foreach (var kv in _landing) if (kv.Key != null) kv.Key.Bob = 0f;
            _landing.Clear();
            _gathers.Clear();                                                           // the game settles its gathered blocks itself
            _bulbs.Clear();
            foreach (var ps in new[] { _dust, _birthDust, _flash, _confetti }) if (ps != null) ps.Clear();
            ResetNet();
            ResetIdle();
            _stepActive = false;
            _comboArmed = false;
        }

        /// <summary>Non-golden specials dribble (|sin| hop, a squash at each bottom); the golden ball spins its sticker (it glows with the game's pulse).</summary>
        public void Idle(BlockView v, float dt)
        {
            if (!Ready || v == null || _landing.ContainsKey(v)) return;              // a landing special is the landing's
            var t = Tuning;
            if (v.Block.Special == SpecialKind.Golden)
            {
                v.Bob = 0f;
                v.StickerSpin = -Mathf.Repeat(_clock * t.GoldenSpin, 360f);
                return;
            }
            float time = _clock + 0.07f * (v.Cell % 4);                                 // a little out of step with each other
            v.Bob = IdleHop(time, t);
            float phase = Frac(time * t.IdleHz);
            if (phase < 0.15f) v.Squash = Mathf.Max(v.Squash, t.IdleSquash * (1f - phase / 0.15f));
            v.StickerSpin = 0f;
        }

        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived)
        {
            if (v == null) return;
            if (!Ready || !isActiveAndEnabled) { v.Pos = to; arrived?.Invoke(v); return; }
            _gathers.Add(new Gathering { V = v, From = v.Pos, To = to, Time = Mathf.Max(0.01f, duration), Arrived = arrived });
        }

        /// <summary>The special drops onto its cell (then squash, court ring, dust), with Pota's birth text and sound.</summary>
        public void Birth(BlockView v, SpecialKind kind)
        {
            if (_game == null || v == null) return;
            var t = Tuning;
            v.Flash = Mathf.Max(v.Flash, t.BornFlash);
            if (Ready && isActiveAndEnabled)
            {
                _landing[v] = 0f;
                v.Bob = t.LandDrop;
            }
            else v.Squash = Mathf.Max(v.Squash, t.BornSquash);
            var sounds = Sounds();
            if (sounds != null)
            {
                if (kind == SpecialKind.Golden) sounds.PlayClip("ozelDogdu", _game.S.SfxGoldenBorn);
                else sounds.Combo(2);
            }
            var popups = _game.Juice != null ? _game.Juice.Popups : null;
            if (popups != null)
                popups.Show(_game.BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.6f, -PotaLook.LabelZ)), SpecialTexts.Born(kind),
                    PotaLook.HudGold, 72, false, 1.2f);
        }

        /// <summary>The step starts: the step clock restarts with the game's (st.T = 0 here); 2+ specials arm the combo.</summary>
        public void TriggerStep(int specials, Vector2 centre)
        {
            _stepClock = 0f;
            _stepActive = true;
            _comboArmed = specials >= 2;
            _comboCentre = centre;
        }

        /// <summary>
        /// A special fires (its view is already hidden: ConsumesSpecialView). It started <c>stepClock − f.Time</c> s ago (≤ 0: planned
        /// between two frames); this frame's Update adds the frame, so its age equals the game's step time minus the planned fire.
        /// </summary>
        public void Fire(SpecialFire f)
        {
            if (!Ready || _game == null || _game.BoardRoot == null) return;
            float age = _stepActive ? Mathf.Clamp(_stepClock - f.Time, -0.05f, 0.5f) : 0f;
            if (f.Order == 1 && _comboArmed)
            {
                _comboArmed = false;
                Combo();
            }
            if (f.Kind == SpecialKind.Bomb) FireDunk(f, age);
            else if (f.Kind == SpecialKind.LineV || f.Kind == SpecialKind.LineH) FireLine(f, age);
        }

        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
        {
            _flights.Add(new Flight
            {
                Route = route, PassedGate = passedGate, Landed = landed, Ball = _freeGolden.Count > 0 ? _freeGolden.Pop() : null,
            });
        }

        // ---------- frame ----------

        private void Update()
        {
            if (!Ready || _game == null || _game.BoardRoot == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);                                // ColorHuntGame's own step
            var t = Tuning;
            _clock += dt;
            _stepClock += dt;
            int gen = _gen;
            TickGathers(dt, t);
            if (gen != _gen) return;
            TickLandings(dt, t);
            TickHops(dt);
            TickBalls(dt, t);
            TickFlights(dt, t);
            if (gen != _gen) return;
            TickQuads(dt, t);
            TickBulbs(dt, t);
            TickNet(dt, t);
        }

        private void TickGathers(float dt, CourtTuning t)
        {
            int gen = _gen;
            for (int i = _gathers.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _gathers.Count) return;
                var g = _gathers[i];
                g.Age += dt;
                float u = Mathf.Clamp01(g.Age / g.Time);
                bool alive = g.V != null && g.V.gameObject.activeSelf;
                if (alive)
                {
                    g.V.Pos = GatherPos(g.From, g.To, u, t);
                    g.V.Scale = 1f - t.GatherShrink * u * u;
                    // In front of the blocks it crosses: written after the game's own Tick this frame (execution order 100), which puts z 0.
                    g.V.transform.localPosition = new Vector3(g.V.Pos.x, g.V.Pos.y + g.V.Bob, -t.GatherLift * Mathf.Sin(Mathf.PI * u));
                    if (!g.Bounced && u >= t.GatherBounceAt)
                    {
                        g.Bounced = true;
                        g.V.Squash = Mathf.Max(g.V.Squash, 1f);
                    }
                }
                if (u < 1f && alive) continue;
                _gathers.RemoveAt(i);
                Call(() => g.Arrived?.Invoke(g.V));                                     // exactly once (the game ignores a view it let go)
            }
        }

        private void TickLandings(float dt, CourtTuning t)
        {
            if (_landing.Count == 0) return;
            _keys.Clear();
            _keys.AddRange(_landing.Keys);
            foreach (var v in _keys)
            {
                if (v == null || !v.gameObject.activeSelf || !v.Block.IsSpecial) { _landing.Remove(v); continue; }
                float age = _landing[v] + dt;
                if (age < t.LandTime)
                {
                    _landing[v] = age;
                    float u = age / Mathf.Max(1e-3f, t.LandTime);
                    v.Bob = t.LandDrop * (1f - u * u);
                    continue;
                }
                _landing.Remove(v);
                v.Bob = 0f;
                v.Squash = Mathf.Max(v.Squash, t.BornSquash);
                Ring(v.Pos, t.CourtRingFrom, t.CourtRingTo, t.CourtRingTime, RingOrder, RingZ);
                StudioParticles.Emit(_birthDust, World(v.Pos, DecalZ), t.BirthDust * BoardScale, t.DustTime, new Color(1f, 1f, 1f, 0.8f), Vector3.zero, R() * 360f);
            }
        }

        private void TickHops(float dt)
        {
            for (int i = _hops.Count - 1; i >= 0; i--)
            {
                var h = _hops[i];
                if (h.V == null || !h.V.Exploding || !h.V.gameObject.activeSelf) { _hops.RemoveAt(i); continue; }   // recycled: NewView resets Bob
                h.Age += dt;
                if (h.Age >= h.Time) { h.V.Bob = 0f; _hops.RemoveAt(i); continue; }
                h.V.Bob = h.Height * Mathf.Sin(Mathf.PI * h.Age / h.Time);
            }
        }

        private void TickBalls(float dt, CourtTuning t)
        {
            for (int i = _balls.Count - 1; i >= 0; i--)
            {
                var b = _balls[i];
                b.Age += dt;
                bool alive = b.Mode == Mode.Line ? StepLine(b, t) : StepDunk(b, t);
                if (alive) continue;
                _balls.RemoveAt(i);
                RecycleQuad(b.Flame);
                RecycleQuad(b.Speed);
                b.Flame = null;
                b.Speed = null;
                HideBall(b);
            }
        }

        private void FireLine(SpecialFire f, float age)
        {
            var t = Tuning;
            int n = BoardSize, row = ColorHuntRules.Row(n, f.Cell), col = ColorHuntRules.Col(n, f.Cell);
            bool column = f.Kind == SpecialKind.LineV;                                  // Pota cizgiDikey: the column
            for (int side = 0; side < 2; side++)
            {
                if (_freeBalls.Count == 0) return;                                      // ruling 10: the plan still bursts the cells
                var dir = column ? new Vector2(0f, side == 0 ? 1f : -1f) : new Vector2(side == 0 ? 1f : -1f, 0f);
                int cells = column ? (side == 0 ? row : n - 1 - row) : (side == 0 ? n - 1 - col : col);
                var b = TakeBall();
                b.Mode = Mode.Line;
                b.Fire = f;
                b.Age = age;
                b.Dir = dir;
                b.Edge = cells + 0.5f;                                                  // the last cell's outer edge, inside the frame bar
                b.Cells = LineCells(n, row, col, dir, cells);
                b.NextKick = 0;
                b.Lead = side == 0;                                                     // the first ball also plays the dribble
                b.Whooshed = side == 1;                                                 // one whoosh per fire
                b.Edged = false;
                b.Impacted = false;
                b.SpeedDone = false;
                b.Flame = null;
                b.Speed = null;
            }
        }

        /// <summary>The cells a ball passes, nearest first (row 0 is the top: +y is row − k).</summary>
        private static int[] LineCells(int n, int row, int col, Vector2 dir, int count)
        {
            var cells = new int[count];
            for (int k = 1; k <= count; k++)
                cells[k - 1] = ColorHuntRules.Index(n, row - Mathf.RoundToInt(dir.y) * k, col + Mathf.RoundToInt(dir.x) * k);
            return cells;
        }

        private bool StepLine(Ball b, CourtTuning t)
        {
            var f = b.Fire;
            float pace = PaceOf(f), lead = t.DribbleTime * pace;
            if (b.Age < lead)
            {
                bool show = b.Lead && b.Age >= 0f;                                     // the hard dribble on the special's cell
                b.T.gameObject.SetActive(show);
                if (show)
                {
                    float s = Mathf.Sin(Mathf.PI * Mathf.Clamp01(b.Age / Mathf.Max(1e-4f, lead)));
                    PlaceBall(b.T, f.At + new Vector2(0f, -0.12f * s), StandInZ, t.StandInCells, 1f + 0.5f * t.DribbleSquash * s,
                        1f - t.DribbleSquash * s, Quaternion.identity);
                }
                return true;
            }
            if (!b.Whooshed)
            {
                b.Whooshed = true;
                Sounds()?.PlayClip("courtPas", _pass, t.PassVolume);
            }
            float d = LineDistance(f, t, b.Age);
            while (b.NextKick < b.Cells.Length && d >= b.NextKick + 1 - t.KickLead) Kick(b.Cells[b.NextKick++], t.LineHop, t.LineHopTime);
            if (!b.Edged && d >= b.Edge)
            {
                b.Edged = true;
                Ring(f.At + b.Dir * b.Edge, t.SwishRingFrom, t.SwishRingTo, t.SwishRingTime, SwishOrder, DecalZ);
                Sounds()?.PlayClip("swish", _game.S.SfxSwish, t.SwishVolume);
            }
            if (d >= b.Edge + t.ExitCells) return false;
            float shrink = d > b.Edge ? 1f - (d - b.Edge) / Mathf.Max(1e-3f, t.ExitCells) : 1f;
            float flown = b.Age - lead, rise = Mathf.Clamp01(d / 0.6f);
            var at = f.At + b.Dir * d;
            float z = Mathf.Lerp(StandInZ, BallZ, rise);
            b.T.gameObject.SetActive(true);
            PlaceBall(b.T, at, z, Mathf.Lerp(t.StandInCells, t.BallCells, rise) * shrink, 1f, 1f,
                Quaternion.AngleAxis(flown * t.BallSpin, new Vector3(-b.Dir.y, b.Dir.x, 0f)));
            float bs = BoardScale, angle = Mathf.Atan2(b.Dir.y, b.Dir.x) * Mathf.Rad2Deg;
            // The flame tail: the sheet's nozzle (frame 0.5, 0.9) sits on the ball, its flame (frame −y) streams back along −dir.
            if (b.Flame == null) b.Flame = TakeQuad(Kind.Flame, _flameMat, FlameOrder);
            if (b.Flame != null)
            {
                float size = t.FlameCells * shrink, length = size * FlameGrow(d, t);
                b.Flame.Age = flown;
                b.Flame.T.localPosition = Local(at - b.Dir * (0.4f * length), z - 0.05f);      // the nozzle stays on the ball
                b.Flame.T.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
                b.Flame.T.localScale = new Vector3(size * bs * t.FlameWide, length * bs, 1f);
                int frame = Mathf.FloorToInt(Frac(flown / Mathf.Max(1e-3f, t.FlameLoop)) * 8f) % 8;
                Draw(b.Flame, Frame(frame, 4, 2), t.FlameTint, 1f);
            }
            // The speed lines: the sheet's ball (u 0.95) on the ball, the streaks behind it, stretched over the path so far.
            if (b.Speed == null && !b.SpeedDone) b.Speed = TakeQuad(Kind.Speed, _speedMat, SpeedOrder);
            if (b.Speed != null)
            {
                if (flown >= t.SpeedTime)
                {
                    RecycleQuad(b.Speed);
                    b.Speed = null;
                    b.SpeedDone = true;
                }
                else
                {
                    float w = Mathf.Clamp(d + 0.6f, 1.2f, t.SpeedLength), h = t.SpeedHeight;
                    b.Speed.Age = flown;
                    b.Speed.T.localPosition = Local(at - b.Dir * (0.45f * w), z - 0.1f);
                    b.Speed.T.localRotation = Quaternion.Euler(0f, 0f, angle);
                    b.Speed.T.localScale = new Vector3(w * bs, h * bs, 1f);
                    int frame = Mathf.Min(7, Mathf.FloorToInt(flown / Mathf.Max(1e-3f, t.SpeedTime) * 8f));
                    Draw(b.Speed, Frame(frame, 4, 2), Color.white, 1f);
                }
            }
            return true;
        }

        private void FireDunk(SpecialFire f, float age)
        {
            if (_freeBalls.Count == 0) return;                                          // ruling 10
            var b = TakeBall();
            b.Mode = Mode.Dunk;
            b.Fire = f;
            b.Age = age;
            b.Impacted = false;
            b.Flame = null;
            b.Speed = null;
        }

        private bool StepDunk(Ball b, CourtTuning t)
        {
            var f = b.Fire;
            float pace = PaceOf(f);
            if (b.Age < 0f) { b.T.gameObject.SetActive(false); return true; }
            var pose = Dunk(b.Age, pace, t);
            if (pose.Impact)
            {
                if (!b.Impacted)
                {
                    b.Impacted = true;
                    Impact(f, t);
                }
                if (b.Age - TimingFor(t).BombLead * pace >= t.ImpactHold) return false;
            }
            b.T.gameObject.SetActive(true);
            var rot = pose.Impact ? Quaternion.identity : Quaternion.Euler(pose.Spin * 0.35f, pose.Spin, 0f);
            float wide = pose.Impact ? 1f + 0.6f * (1f - pose.Squash) : 1f;
            PlaceBall(b.T, f.At + new Vector2(0f, pose.Up - (pose.Impact ? 0.15f : 0f)), StandInZ + pose.Toward, t.StandInCells * pose.Scale,
                wide, pose.Squash, rot);
            return true;
        }

        /// <summary>The dunk hits the floor: crack, dust ring, the 3×3 hops (they burst BombRing·Pace later), SLAM!, flashbulbs, shake, kick, smac.</summary>
        private void Impact(SpecialFire f, CourtTuning t)
        {
            int n = BoardSize;
            float bs = BoardScale;
            var crack = TakeQuad(Kind.Crack, _crackMat, CrackOrder);
            if (crack != null)
            {
                crack.At = Local(f.At, DecalZ);
                crack.From = t.CrackCells * bs;
                crack.Life = t.CrackFade;
                crack.Turn = R() * 360f;
            }
            StudioParticles.Emit(_dust, World(f.At, DecalZ), t.DustCells * bs, t.DustTime, Color.white, Vector3.zero, R() * 360f);
            foreach (int c in ColorHuntRules.SingleArea(n, f.Cell, SpecialKind.Bomb)) if (c != f.Cell) Kick(c, t.BlockHop, t.BlockHopTime);
            Letter(_slamMat, f.At + new Vector2(0f, 1.1f), t.SlamLetterCells);
            int bulbs = t.FlashbulbsMin + _rng.Next(Mathf.Max(0, t.FlashbulbsMax - t.FlashbulbsMin) + 1);
            for (int i = 0; i < bulbs; i++) _bulbs.Add(new Flashbulb { Wait = R() * t.FlashbulbWindow, At = EdgePoint(n) });
            _game.Juice?.Shake?.Add(t.SlamShake);
            var cam = _game.Cam != null ? _game.Cam.GetComponent<ColorHuntCamera>() : null;
            if (cam != null) cam.Push(World(f.At, PotaLook.FrontZ), t.KickAmount, t.KickTime);
            Sounds()?.PlayClip("smac", _game.S.SfxSlam);
        }

        /// <summary>2+ specials in one step, at the second fire: slow motion, COMBO! under the step's centre, a short crowd.</summary>
        private void Combo()
        {
            var t = Tuning;
            _game.Juice?.TimeCtl?.SlowMo(t.ComboSlowScale, t.ComboSlowTime);
            Letter(_comboMat, _comboCentre + new Vector2(0f, -1.4f), t.ComboLetterCells);
            Sounds()?.PlayClip("kalabalik", _crowd, t.CrowdVolume);
        }

        private void TickFlights(float dt, CourtTuning t)
        {
            int gen = _gen;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _flights.Count) return;
                var f = _flights[i];
                f.Age += dt;
                float tt = f.Age - f.Route.Delay;
                if (tt < 0f) continue;                                                  // the step's next ball leaves 0.12 s later
                float s = f.Route.Scale > 0f ? f.Route.Scale : 1f;
                if (f.Ball != null)
                {
                    f.Ball.gameObject.SetActive(true);
                    f.Ball.localPosition = GoldenPath.At(f.Route, tt) + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * s);
                    float roll = tt * t.GoldenRoll * Mathf.Rad2Deg;                     // rolls down the column, keeps spinning in the arc
                    f.Ball.localRotation = Quaternion.Euler(roll, 0f, roll * 0.3f);
                    f.Ball.localScale = Vector3.one * (t.GoldenBallCells * s / _ballSize);
                }
                if (!f.Passed && GoldenPath.PastGate(tt))
                {
                    f.Passed = true;
                    var gates = _game.Gates;
                    if (gates != null) gates.Flash(f.Route.Col);
                    Call(f.PassedGate);
                    if (gen != _gen) return;
                }
                if (!GoldenPath.Landed(tt)) continue;
                _flights.RemoveAt(i);
                HideGolden(f);
                Land(f.Route, t);
                Call(f.Landed);
                if (gen != _gen) return;
            }
        }

        /// <summary>Swish: the net swings and its six rings flex in turn, SWISH! over the rim, gold confetti, file-swish + altin-inis.</summary>
        private void Land(GoldenRoute r, CourtTuning t)
        {
            Landings++;
            float s = r.Scale > 0f ? r.Scale : 1f;
            var hoop = _game.Hoop;
            if (hoop != null)
            {
                hoop.Swing();
                NetSwings++;
                if (_hoop != null && _hoop != hoop) ResetNet();
                _hoop = hoop;
                _netT = 0f;
            }
            ShowLetter(_swishMat, r.Hoop + new Vector3(0f, t.SwishLift, t.SwishBack), t.SwishLetterCells * s);
            var at = transform.TransformPoint(r.Hoop + new Vector3(0f, 0.2f, 0f));
            for (int i = 0; i < t.ConfettiCount; i++)
            {
                float a = R() * Mathf.PI * 2f;
                var c = Gold[i % Gold.Length].linear;
                var v = new Vector3(Mathf.Cos(a) * (0.8f + 1.6f * R()), 3.2f + 2.4f * R(), -(0.4f + 1.2f * R()));
                StudioParticles.Emit3D(_confetti, at, transform.TransformVector(v), new Vector3(0.2f, 0.13f, 0.03f),
                    new Vector3(R(), R(), R()) * 360f, new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 900f, 1.4f + 0.6f * R(), c);
            }
            var sounds = Sounds();
            if (sounds == null) return;
            sounds.PlayClip("swish", _game.S.SfxSwish);
            sounds.PlayClip("altinInis", _game.S.SfxGoldenLand);
        }

        private void TickQuads(float dt, CourtTuning t)
        {
            for (int i = _quads.Count - 1; i >= 0; i--)
            {
                if (i >= _quads.Count) continue;
                var q = _quads[i];
                if (q.Kind == Kind.Flame || q.Kind == Kind.Speed) continue;            // their ball places them
                q.Age += dt;
                if (q.Age >= q.Life) { RecycleQuad(q); continue; }
                float u = q.Age / q.Life, size, alpha;
                switch (q.Kind)
                {
                    case Kind.Ring:
                        size = Mathf.Lerp(q.From, q.To, 1f - (1f - u) * (1f - u));
                        alpha = 0.95f * Mathf.Pow(1f - u, 1.5f);
                        break;
                    case Kind.Crack:
                        size = q.From * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(q.Age / 0.05f));
                        float fade = Mathf.Clamp01((q.Age - 0.1f) / Mathf.Max(1e-3f, q.Life - 0.1f));
                        alpha = (1f - fade) * (1f - fade);
                        break;
                    default:                                                            // lettering, 2:1
                        size = q.From * LetterScale(q.Age, t);
                        alpha = LetterAlpha(q.Age, t);
                        break;
                }
                q.T.localPosition = q.At;
                q.T.localRotation = Quaternion.Euler(0f, 0f, q.Turn);
                q.T.localScale = new Vector3(Mathf.Max(1e-4f, size), Mathf.Max(1e-4f, size / q.Aspect), 1f);
                Draw(q, Whole, Color.white, alpha);
            }
        }

        private void TickBulbs(float dt, CourtTuning t)
        {
            for (int i = _bulbs.Count - 1; i >= 0; i--)
            {
                var b = _bulbs[i];
                b.Wait -= dt;
                if (b.Wait > 0f) continue;
                _bulbs.RemoveAt(i);
                StudioParticles.Emit(_flash, World(b.At, FlashZ), t.FlashbulbCells * BoardScale, t.FlashbulbLife, new Color(1f, 0.97f, 0.9f, 1f),
                    Vector3.zero, R() * 45f);
            }
        }

        private void TickNet(float dt, CourtTuning t)
        {
            if (_netT < 0f) return;
            if (_hoop == null || _hoop.Net == null) { _netT = -1f; return; }
            _netT += dt;
            var net = _hoop.Net;
            for (int k = 0; k < net.childCount; k++)
            {
                float r = NetRing(_netT, k, t);
                net.GetChild(k).localScale = new Vector3(r, 1f, r);                    // the rings lie in xz under the rim
            }
            if (_netT >= NetWaveTime(t, net.childCount)) ResetNet();
        }

        // ---------- pieces ----------

        private void Kick(int cell, float height, float time)
        {
            var v = _game.ViewAt(cell);
            if (v == null || !v.Exploding || !v.gameObject.activeSelf) return;         // only blocks this step bursts (never a golden ball)
            _hops.Add(new Hop { V = v, Height = height, Time = Mathf.Max(0.01f, time) });
        }

        private void Ring(Vector2 cell, float from, float to, float time, int order, float z)
        {
            var q = TakeQuad(Kind.Ring, _ringMat, order);
            if (q == null) return;
            float bs = BoardScale;
            q.At = Local(cell, z);
            q.From = from * bs;
            q.To = to * bs;
            q.Life = time;
        }

        /// <summary>A 2:1 lettering quad <paramref name="widthCells"/> wide near <paramref name="at"/>, kept inside the board so it is never cut.</summary>
        private void Letter(Material mat, Vector2 at, float widthCells)
        {
            int n = BoardSize;
            float w = widthCells, h = widthCells * 0.5f;
            float mx = Mathf.Max(0f, n * 0.5f - 0.4f - 0.6f * w), my = Mathf.Max(0f, n * 0.5f - 0.4f - 0.6f * h);   // 0.6: the 1.15 pop + perspective
            ShowLetter(mat, Local(new Vector2(Mathf.Clamp(at.x, -mx, mx), Mathf.Clamp(at.y, -my, my)), LetterZ), w * BoardScale);
        }

        /// <summary>
        /// One word at a time: while the same lettering is still up (the second golden ball of a volley lands 0.12 s after the first,
        /// a second dunk in a chain) it pops again from its peak at the new spot, instead of a second copy stacked half over it
        /// (SWISH! always re-pops at the same spot, the hoop; a second SLAM! moves to its own dunk).
        /// </summary>
        private void ShowLetter(Material mat, Vector3 local, float width)
        {
            foreach (var live in _quads)
                if (live.Kind == Kind.Letter && live.R.sharedMaterial == mat)
                {
                    live.Age = Mathf.Min(live.Age, 0.1f);
                    live.At = local;
                    return;
                }
            var q = TakeQuad(Kind.Letter, mat, LetterOrder);
            if (q == null) return;
            q.At = local;
            q.From = width;
            q.Aspect = 2f;
            q.Life = Tuning.LetterTime;
        }

        /// <summary>A random point on the frame (board units): half the backboard plus half the frame bar, over the grid scale.</summary>
        private Vector2 EdgePoint(int n)
        {
            float half = (PotaLook.PanoSize + PotaLook.FrameWidth) * 0.5f / PotaLook.GridScale(n);
            float u = (R() * 2f - 1f) * half;
            switch (_rng.Next(4))
            {
                case 0: return new Vector2(u, half);
                case 1: return new Vector2(u, -half);
                case 2: return new Vector2(-half, u);
                default: return new Vector2(half, u);
            }
        }

        private void PlaceBall(Transform ball, Vector2 cell, float potaZ, float cells, float wide, float tall, Quaternion rot)
        {
            float k = cells * BoardScale / _ballSize;
            ball.localPosition = Local(cell, potaZ);
            ball.localRotation = rot;
            ball.localScale = new Vector3(wide * k, tall * k, wide * k);
        }

        private void Draw(Quad q, Vector4 uv, Color tint, float alpha)
        {
            _mpb.Clear();
            _mpb.SetVector(UvRectId, uv);
            _mpb.SetVector(ColorId, new Vector4(tint.r, tint.g, tint.b, tint.a * alpha));   // PotaLabel: linear tint × alpha
            q.R.SetPropertyBlock(_mpb);
        }

        private Ball TakeBall()
        {
            var b = _freeBalls.Pop();
            b.Live = true;
            _balls.Add(b);
            return b;
        }

        private void HideBall(Ball b)
        {
            if (!b.Live) return;
            b.Live = false;
            b.T.gameObject.SetActive(false);
            _freeBalls.Push(b);
        }

        private void HideGolden(Flight f)
        {
            if (f.Ball == null) return;
            f.Ball.gameObject.SetActive(false);
            _freeGolden.Push(f.Ball);
            f.Ball = null;
        }

        private Quad TakeQuad(Kind kind, Material mat, int order)
        {
            var free = _freeQuads[kind];
            if (free.Count == 0) return null;
            var q = free.Pop();
            q.Live = true;
            q.Age = 0f;
            q.Life = 1f;
            q.From = q.To = 1f;
            q.Aspect = 1f;
            q.Turn = 0f;
            q.R.sharedMaterial = mat;
            q.R.sortingOrder = order;
            q.T.localRotation = Quaternion.identity;
            q.T.gameObject.SetActive(true);
            _quads.Add(q);
            return q;
        }

        private void RecycleQuad(Quad q)
        {
            if (q == null || !q.Live) return;
            q.Live = false;
            q.T.gameObject.SetActive(false);
            _quads.Remove(q);
            _freeQuads[q.Kind].Push(q);
        }

        private void ResetNet()
        {
            if (_hoop != null && _hoop.Net != null)
                for (int k = 0; k < _hoop.Net.childCount; k++) _hoop.Net.GetChild(k).localScale = Vector3.one;
            _netT = -1f;
        }

        /// <summary>The board's resting specials lose this look's dribble and spin (a look switch, a new board).</summary>
        private void ResetIdle()
        {
            var board = _game != null ? _game.Board : null;
            if (board == null) return;
            for (int i = 0; i < board.Cells.Length; i++)
            {
                var v = _game.ViewAt(i);
                if (v == null || !v.Block.IsSpecial) continue;
                v.Bob = 0f;
                v.StickerSpin = 0f;
            }
        }

        private void Pool(Kind kind, int count)
        {
            var stack = new Stack<Quad>();
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Court" + kind + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.SetActive(false);
                stack.Push(new Quad { T = go.transform, R = r, Kind = kind });
            }
            _freeQuads[kind] = stack;
        }

        /// <summary>A pooled basketball: a pivot at the ball's centre, the mesh (Project/Toon with its embedded skin) under it.</summary>
        private Transform NewBall(string name, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            var mesh = new GameObject("Mesh");
            mesh.transform.SetParent(pivot, false);
            mesh.transform.localPosition = -_ballCentre;
            mesh.AddComponent<MeshFilter>().sharedMesh = _ballMesh;
            var r = mesh.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            pivot.gameObject.SetActive(false);
            return pivot;
        }

        /// <summary>Quads use the block labels' Project/PotaLabel (LabelTemplate): straight alpha, _UvRect frame, _Color tint × alpha per renderer.</summary>
        private Material QuadMaterial(ColorHuntSettings s, Texture tex, string name)
        {
            var m = RuntimeMaterial.From(s.LabelTemplate, "Project/PotaLabel", name);
            m.enableInstancing = true;
            m.SetTexture("_BaseMap", tex);
            return Keep(m);
        }

        private Material Keep(Material m)
        {
            _materials.Add(m);
            return m;
        }

        private int BoardSize => _game != null && _game.Board != null ? _game.Board.Size : _game != null && _game.S != null ? _game.S.Size : 7;

        /// <summary>Game-root units per board cell (the board's grid scale; the game root is never scaled).</summary>
        private float BoardScale => _game != null && _game.BoardRoot != null
            ? _game.BoardRoot.lossyScale.x / Mathf.Max(1e-4f, transform.lossyScale.x)
            : PotaLook.GridScale(BoardSize);

        /// <summary>Board cell + Pota depth (toward the camera) → world.</summary>
        private Vector3 World(Vector2 cell, float potaZ) => _game.BoardRoot.TransformPoint(new Vector3(cell.x, cell.y, -potaZ));

        /// <summary>Board cell + Pota depth → this object's space (the game root's).</summary>
        private Vector3 Local(Vector2 cell, float potaZ) => transform.InverseTransformPoint(World(cell, potaZ));

        private float R() => (float)_rng.NextDouble();

        private PotaSounds Sounds() => _game != null && _game.Juice != null ? _game.Juice.Sounds : null;

        private static void Call(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnDestroy()
        {
            foreach (var m in _materials) if (m != null) Destroy(m);
            if (_pass != null) Destroy(_pass);
            if (_crowdMade && _crowd != null) Destroy(_crowd);
        }
    }
}
