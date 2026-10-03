using System;
using System.Collections.Generic;
using Project.Basket.Visual;
using Project.Flow;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Round state machine and animation queue (Pota renkAviFaz.ts pattern): the rules resolve a tap in one go,
    /// the visuals replay the resulting steps — salvo → explosion → drops (with new blocks from above) → chains,
    /// and rotation. Nothing here decides rules; that is <see cref="ColorHuntRules"/>.
    /// </summary>
    public class ColorHuntGame : MonoBehaviour
    {
        public enum State { Idle, Playing, Ended }

        public ColorHuntSettings S;
        public ColorHuntFx Fx;
        public BallSalvo Salvo;
        public ColorHuntJuice Juice;
        /// <summary>F3: the HUD's leftover finale (set by ColorHuntHud.Bind); the win disperses after it.</summary>
        [NonSerialized] public LeftoverFinale Finale;
        /// <summary>F9: the HUD's lose moment (set by ColorHuntHud.Bind); the card waits Feel.LoseHold instead of LoseWait.</summary>
        [NonSerialized] public LoseMoment Lose;
        /// <summary>F7: the board clip on this object (set by BoardClip.Awake); the views take its layer.</summary>
        [NonSerialized] public BoardClip Clip;
        public ColorHuntHud Hud;
        public Camera Cam;
        [Tooltip("Deal a board on Start. PotaFlow turns this off and stages the board itself (Prepare → Begin).")]
        public bool AutoStart = true;

        public State Current { get; private set; } = State.Idle;
        public Board Board { get; private set; }
        public Transform BoardRoot { get; private set; }
        /// <summary>The golden ball's gate rings (yapbozSahne.ts gecisGoster), on the game root: they never turn with the board.</summary>
        public GateRings Gates { get; private set; }
        /// <summary>Taps wait: a job, the salvo, a loose swell, the intro, a golden ball or a gathered block on its way (until it arrives).</summary>
        public bool Busy => _jobs.Count > 0 || (Salvo != null && Salvo.Busy) || _swells.AnyLoose || _introOn || _goldens.Count > 0
                            || _gathering.Count > 0;

        private float _intro = -1f, _idle, _hintT = -1f;
        private float _introSpeed = 1f;
        private bool _introOn;
        private int _hintCell = -1;

        /// <summary>Studio board intro running (spec §3.3): the board is busy, taps wait.</summary>
        public bool Intro => _introOn;
        /// <summary>F1: the level intro began (the HUD plays its ribbon and goal chips).</summary>
        public event Action IntroStarted;
        /// <summary>F1: a tap compressed the intro (the HUD compresses too).</summary>
        public event Action IntroSkipped;
        /// <summary>The block the idle hint is wiggling, −1 when none (spec §3.4).</summary>
        public int HintCell => _hintT >= 0f ? _hintCell : -1;
        public int DisplayedAmmo { get; private set; }
        public bool Won { get; private set; }
        public int Rotations { get; private set; }
        public int LongestChain { get; private set; }
        public uint Seed { get; private set; }

        public event Action AmmoChanged;
        public event Action TargetsChanged;
        public event Action<bool, string> RoundEnded;
        public event Action RoundStarted;

        /// <summary>Listeners (HUD) must not be able to abort the animation queue: a throwing listener is logged, not propagated.</summary>
        private static void Raise(Action a)
        {
            try { a?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private abstract class Job { public float T; }
        private class SalvoJob : Job { public int Cell, Cost, AmmoBefore; public List<Step> Steps; public bool Started; public int Arrived; }
        private class StepJob : Job
        {
            public Step Step; public bool Dropping; public float DropTime; public bool Started; public int Depth;
            /// <summary>The tapped special of a trigger step (−1: none).</summary>
            public int Origin = -1;
            /// <summary>When the drops start: ChainTime, a trigger plan's End, a gathering look's GatherTime.</summary>
            public float SwellEnd;
            public TriggerPlan Plan;
            public int NextFire;
            public Action Peak;
            public readonly List<PendingSwell> Pending = new List<PendingSwell>();
            public readonly HashSet<int> Consumed = new HashSet<int>();
        }
        private class PendingSwell { public int Cell; public BlockView View; public float At; }
        private class RotateJob : Job { public float Angle; public Board Rotated; }
        private class WaitJob : Job { public float Duration; }

        private readonly List<Job> _jobs = new List<Job>();
        private readonly Dictionary<int, BlockView> _views = new Dictionary<int, BlockView>();
        private readonly Stack<BlockView> _pool = new Stack<BlockView>();
        private readonly Dictionary<BlockView, DropState> _drops = new Dictionary<BlockView, DropState>();
        private readonly SwellSet<BlockView> _swells = new SwellSet<BlockView>();
        private readonly List<BlockView> _loose = new List<BlockView>();      // out of their cell, still swelling (Pota sonenler)
        private readonly List<BlockView> _finished = new List<BlockView>();
        private float _disperse = -1f;
        /// <summary>The views' clock (s of game time): the golden pulse's phase, shared by every golden ball like Pota's `simdi`.</summary>
        private float _clock;
        private class DropState { public float StartY, Distance, T; public bool Landed; }

        private Mesh _blockMesh;
        private Material _blockMat, _labelMat;
        private readonly Dictionary<int, int> _shown = new Dictionary<int, int>();
        private int _flights, _flightGen;
        private bool _targetsPending;
        private Transform _blocksRoot;
        private bool _checkNeeded;
        private int _selected = -1;
        private GameObject _selectionRing;
        private float _resultTimer = -1f;
        private bool _staged;

        // ---------- lifecycle ----------

        private void Start()
        {
            BoardRoot = new GameObject("Board").transform;
            BoardRoot.SetParent(transform, false);
            BoardRoot.localScale = Vector3.one * PotaLook.GridScale(S.Size);
            _blocksRoot = new GameObject("Blocks").transform;
            _blocksRoot.SetParent(BoardRoot, false);

            _blockMesh = PillowBlockMesh.Create();
            _blockMat = BlockView.BodyMaterial();
            _labelMat = BlockView.LabelMaterial(S.LabelTemplate, S.LabelAtlas);


            _selectionRing = RimMeshBuilder.MeshObject("SelectionRing", BoardRoot, RimMeshBuilder.Torus(0.46f, 0.055f, 26, 8), BlockView.RingMaterial(S.SelectionTemplate));
            _selectionRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _selectionRing.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _selectionRing.SetActive(false);

            Gates = new GameObject("GateRings").AddComponent<GateRings>();
            Gates.transform.SetParent(transform, false);
            Gates.Build(this);

            if (Fx != null) { Fx.Build(); Fx.SetBoard(BoardRoot); }
            if (Salvo != null) Salvo.Board = BoardRoot;
            SetLook(S != null ? S.Look : PuzzleLook.Pota);
            SetSpecialLook(S != null ? S.SpecialLook : SpecialLook.Pota);
            if (AutoStart) NewRound();
        }

        public void NewRound() => NewRound(S.Ammo);

        public void NewRound(int ammo)
        {
            Prepare(ammo);
            StartIntro();
            Begin();
        }

        public void Prepare(int ammo) => Prepare(ammo, 0, null);

        /// <summary>Deals a board: <paramref name="seed"/> 0 = the settings' seed (random if 0 there); <paramref name="design"/> null = the settings' level.</summary>
        public void Prepare(int ammo, uint seed, LevelDesign design)
        {
            ClearViews();
            Seed = seed != 0 ? seed : S.Seed != 0 ? S.Seed : (uint)UnityEngine.Random.Range(1, int.MaxValue);
            Deal(ColorHuntRules.Create(design ?? S.ToDesign(), ammo, Seed));
        }

        /// <summary>
        /// Deals a given board (demo presets, rule fixtures) instead of a fresh one: a copy goes on the table with the same reset and the
        /// same events as <see cref="Prepare(int, uint, LevelDesign)"/>, its gates show; <see cref="Seed"/> is 0. Call Begin to play.
        /// </summary>
        public void Stage(Board board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            ClearViews();
            Seed = 0;
            Deal(board.Clone());
        }

        /// <summary>Puts a board on the table: views, grid scale, effects and styles cleared, gate rings, counters; then the round events.</summary>
        private void Deal(Board board)
        {
            Board = board;
            BuildViews(Board);
            BoardRoot.localScale = Vector3.one * PotaLook.GridScale(Board.Size);
            Fx?.SetBoard(BoardRoot);
            Fx?.Clear();
            _style?.Clear();
            ResetSpecials();                            // the special style, golden balls and gate rings start over (shows Board.Gates)
            DisplayedAmmo = Board.Ammo;
            Current = State.Idle;
            _staged = true;
            Won = false;
            Rotations = 0; LongestChain = 0;
            _checkNeeded = true;
            _resultTimer = -1f;
            BoardRoot.localRotation = Quaternion.identity;
            ResetShownTargets(false);                   // before RoundStarted: the HUD builds its rows from the new counts, not the last round's
            Raise(RoundStarted);
            Raise(AmmoChanged);
            Raise(TargetsChanged);
        }

        /// <summary>Shows a different number on the ammo counter while the board is staged (transition count-up).</summary>
        public void ShowAmmo(int value)
        {
            DisplayedAmmo = value;
            Raise(AmmoChanged);
        }

        /// <summary>Starts play on the staged board; the counter snaps to the real ammo.</summary>
        public void Begin()
        {
            if (Board == null) return;
            _staged = false;
            Current = State.Playing;
            DisplayedAmmo = Board.Ammo;
            Raise(AmmoChanged);
        }

        // ---------- public actions ----------

        public Vector3 CellWorld(int cell)
        {
            var p = BoardBuilder.CellPosition(Board.Size, ColorHuntRules.Row(Board.Size, cell), ColorHuntRules.Col(Board.Size, cell));
            return BoardRoot.TransformPoint(new Vector3(p.x, p.y, 0f));
        }

        public int Selected => _selected;
        public event Action SelectionChanged;
        public BlockView ViewAt(int cell) => _views.TryGetValue(cell, out var v) ? v : null;
        /// <summary>Every live block view (F7 re-layers them when the feel switch changes).</summary>
        public IEnumerable<BlockView> Views => _views.Values;

        public void Select(int cell)
        {
            if (cell == _selected) return;
            if (_selected >= 0 && _views.TryGetValue(_selected, out var previous)) previous.Pressed = false;
            _selected = cell;
            ShowRing(cell);
            if (cell >= 0 && _views.TryGetValue(cell, out var v))
            {
                v.Squash = Mathf.Max(v.Squash, 0.35f);
                Juice?.Select();
                if (Studio) v.Pressed = true;
            }
            Raise(SelectionChanged);
        }

        /// <summary>yapbozSahne.ts secimGoster: the white ring on a cell (−1 hides it).</summary>
        private void ShowRing(int cell)
        {
            if (cell >= 0 && _views.TryGetValue(cell, out var v))
            {
                _selectionRing.SetActive(true);
                _selectionRing.transform.localPosition = new Vector3(v.Home.x, v.Home.y, -(PotaLook.FrontZ + 0.08f));
            }
            else _selectionRing.SetActive(false);
        }

        /// <summary>Tap a cell. Returns "broken", "insufficient", "golden" (a golden ball leaves only through a gate) or "locked".</summary>
        public string Tap(int cell)
        {
            if (Current != State.Playing || Busy) return "locked";
            Select(-1);
            int ammoBefore = Board.Ammo;
            var r = ColorHuntRules.Break(Board, cell);
            if (r.Outcome == BreakOutcome.Golden)
            {
                var at = CellWorld(cell);
                if (_views.TryGetValue(cell, out var gv))
                {
                    gv.Shiver = S.InsufficientShake;                                                  // titrem
                    at = BoardRoot.TransformPoint(new Vector3(0f, gv.Pos.y + 0.55f, -PotaLook.LabelZ));   // dokun "altin": board centre, y + 0.55
                }
                Juice?.GoldenTap(at);
                return "golden";
            }
            if (!r.Broken)
            {
                var at = CellWorld(cell);
                if (_views.TryGetValue(cell, out var v))
                {
                    v.Shiver = S.InsufficientShake;
                    at = BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.55f, -PotaLook.LabelZ));   // dokunmaReddi: y + 0.55
                }
                Juice?.Insufficient(at, r.Cost);
                return "insufficient";
            }
            bool special = Board.Cells[cell].IsSpecial;
            Board = r.Board;
            _checkNeeded = true;
            _targetsPending = true;
            if (special)
            {
                QueueSteps(r.Steps, false, cell);       // dokun: özel top — free, no salvo, its blast plays as depth 1
                return "broken";
            }
            _jobs.Add(new SalvoJob { Cell = cell, Cost = r.Cost, AmmoBefore = ammoBefore, Steps = r.Steps });
            return "broken";
        }

        /// <summary>dondurmeUygula: turn (free) and settle (dondurVeOtur) — a golden ball turned onto an open gate drops after the turn.</summary>
        public bool Rotate(Rotation dir)
        {
            if (Current != State.Playing || Busy) return false;
            Select(-1);
            var rr = ColorHuntRules.RotateAndSettle(Board, dir);
            Board = rr.Board;
            Rotations++;
            _checkNeeded = true;
            if (rr.Steps.Count > 0) _targetsPending = true;
            _jobs.Add(new RotateJob { Angle = dir == Rotation.Clockwise ? -90f : 90f, Rotated = rr.Rotated });
            QueueSteps(rr.Steps, false, -1);            // they play once the views are rebuilt from the turned board
            Juice?.Rotate();
            return true;
        }

        // ---------- frame ----------

        private void Update()
        {
            if (Current == State.Idle)
            {
                if (_staged) TickViews(Mathf.Min(Time.deltaTime, 0.05f));
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Salvo?.Tick(dt);
            AdvanceJobs(dt);
            TickGoldens(dt);
            TickGathers(dt);
            AdvanceIntro(dt);
            AdvanceViews(dt);
            TickHint(Time.unscaledDeltaTime);
            if (Current == State.Playing && !Busy && _checkNeeded) AfterSettle();
            if (_targetsPending && !Busy && _flights == 0) SyncTargetsIfSettled();
            if (_resultTimer >= 0f)
            {
                _resultTimer -= Time.unscaledDeltaTime;
                if (_resultTimer < 0f) { _resultTimer = -1f; RoundEnded?.Invoke(Won, _endReason); }
            }
            TickViews(dt);
        }

        /// <summary>
        /// Ticks every view. renkAviFaz.ts gorselleriIlerlet: a golden ball that neither drops nor bursts pulses flas ≥ 0.08·(1 + sin 5t)
        /// (the game owns the clock so all golden balls share the phase); every resting special gets the look's idle life.
        /// </summary>
        private void TickViews(float dt)
        {
            _clock += dt;
            float pulse = PotaLook.GoldenPulse(_clock);
            var specials = Specials;
            foreach (var v in _views.Values)
            {
                bool resting = !v.Exploding && !_drops.ContainsKey(v);
                v.Pulse = v.Block.Special == SpecialKind.Golden && resting ? pulse : 0f;
                if (v.Block.IsSpecial && resting) specials.Idle(v, dt);
                v.Tick(dt);
            }
            foreach (var v in _loose)
            {
                v.Pulse = 0f;
                v.Tick(dt);
            }
            foreach (var v in _gathering.Keys)
            {
                v.Pulse = 0f;
                v.Tick(dt);
            }
        }

        private string _endReason = "";

        private void AfterSettle()
        {
            _checkNeeded = false;
            if (ColorHuntRules.Won(Board)) { End(true, "target"); return; }
            if (!ColorHuntRules.HasMove(Board)) { End(false, Board.Ammo <= 0 ? "ammo" : "insufficient"); return; }
        }

        private void End(bool won, string reason)
        {
            Current = State.Ended;
            Won = won;
            _endReason = reason;
            Select(-1);
            Juice?.Result(won);
            if (won && Feel && Finale != null && Board.Ammo > 0)
            {
                // F3: the leftover balls burst blocks into coins first; the board disperses and the card waits after that
                Finale.Play(Board.Ammo, () => { if (Current == State.Ended && Won) { Disperse(); _resultTimer = S.WinWait; } });
                return;
            }
            if (won) Disperse();
            if (!won && Feel && Lose != null) { Lose.Play(reason); _resultTimer = S.Feel.LoseHold; return; }
            _resultTimer = won ? S.WinWait : S.LoseWait;
        }

        /// <summary>F3: a live cell chosen for the finale's next ball; −1 when the board is empty.</summary>
        public int RandomLiveCell(System.Random rng)
        {
            if (_views.Count == 0) return -1;
            int pick = rng.Next(_views.Count), i = 0;
            foreach (var cell in _views.Keys) if (i++ == pick) return cell;
            return -1;
        }

        /// <summary>
        /// F3: bursts a live block for show only — the rules are untouched (no item counted, no cell event): the view leaves its cell,
        /// swells to the tap peak and plays the Pota burst, like FinishSalvo without the bookkeeping.
        /// </summary>
        public void BurstCell(int cell)
        {
            var view = ViewAt(cell);
            if (view == null) return;
            _views.Remove(cell);
            _loose.Add(view);
            view.Dim = false;
            var tone = BurstTone(view.Block);
            _swells.Start(view, S.TapSwellTime, S.TapSwell, true, () => { Juice?.Break(); Style.TapBurst(view, tone); });
        }

        /// <summary>
        /// renkAviFaz.ts tahtayiDagit: every block gives six hot points and swells to 1.24 over 0.5 s; the views go after 0.52 s. The paper
        /// confetti comes too (Pota drops it at the dunk, which is a later step; the spec keeps it at the win).
        /// </summary>
        private void Disperse()
        {
            foreach (var v in _views.Values)
            {
                Fx?.Particles(v.Pos, BurstTone(v.Block), 6, 3.5f, 0.1f);
                _swells.Start(v, 0.5f, S.ChainSwell, false, null);
            }
            Fx?.Confetti(Board.Size, S.ConfettiCount);
            _disperse = 0.52f;
        }

        // ---------- jobs ----------

        private void AdvanceJobs(float dt)
        {
            if (_jobs.Count == 0) return;
            var job = _jobs[0];
            switch (job)
            {
                case WaitJob w:
                    w.T += dt;
                    if (w.T >= w.Duration) _jobs.RemoveAt(0);
                    break;
                case RotateJob r:
                    r.T += dt;
                    float u = Mathf.Min(1f, r.T / S.RotateTime);
                    float angle = r.Angle * PotaLook.Smoothstep(u);                         // RENK_AVI_HIS.donus, smoothstep
                    BoardRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
                    BoardRoot.localScale = Vector3.one * (PotaLook.GridScale(Board.Size) * PotaLook.RotationFit(Board.Size, angle * Mathf.Deg2Rad));
                    float labels = PotaLook.LabelCounterAngle(angle, u);                     // ride the board, stand up after 70 %
                    foreach (var v in _views.Values) v.LabelAngle = labels;
                    if (u >= 1f)
                    {
                        BoardRoot.localRotation = Quaternion.identity;
                        BoardRoot.localScale = Vector3.one * PotaLook.GridScale(Board.Size);
                        _jobs.RemoveAt(0);
                        var pending = new List<Job>(_jobs);
                        ClearViews();
                        _jobs.AddRange(pending);
                        BuildViews(r.Rotated);
                    }
                    break;
                case SalvoJob s:
                    if (!s.Started) StartSalvo(s);
                    s.T += dt;
                    DisplayedAmmo = Mathf.Max(Board.Ammo, s.AmmoBefore - Mathf.RoundToInt(s.Cost * Mathf.Min(1f, s.T / S.SalvoDuration)));
                    Raise(AmmoChanged);
                    if (s.T >= S.SalvoDuration) FinishSalvo(s);
                    break;
                case StepJob st:
                    if (!st.Started) StartStep(st);
                    st.T += dt;
                    if (!st.Dropping)
                    {
                        TickPlan(st, dt);
                        if (st.T >= st.SwellEnd) StartDrops(st);
                        break;
                    }
                    if (st.T >= st.DropTime)
                    {
                        foreach (var kv in _drops) { kv.Key.Pos = kv.Key.Home; }
                        _drops.Clear();
                        _jobs.RemoveAt(0);
                        if (_jobs.Count > 0) _jobs.Insert(0, new WaitJob { Duration = S.ChainTime });
                    }
                    break;
            }
        }

        private void StartSalvo(SalvoJob s)
        {
            s.Started = true;
            var view = ViewAt(s.Cell);
            var target = view != null ? view.Home : BoardBuilder.CellPosition(Board.Size, ColorHuntRules.Row(Board.Size, s.Cell), ColorHuntRules.Col(Board.Size, s.Cell));
            Salvo?.Fire(PotaLook.SalvoSource(Board.Size), target, s.Cost, S.SalvoDuration, hit =>
            {
                s.Arrived++;
                var v = ViewAt(s.Cell);
                if (v != null)
                {
                    v.Flash = Mathf.Max(v.Flash, 0.55f);
                    v.Squash = Mathf.Max(v.Squash, 0.5f);
                    if (s.Arrived % 3 == 1) Fx?.Particles(hit, S.PaletteColor(v.Block.Color), 3, 1.5f, 0.07f);
                }
                Juice?.BallHit();
            });
            if (view != null) { view.Flash = Mathf.Max(view.Flash, 0.4f); view.Squash = 0.35f; view.Exploding = true; }   // never soluk (i !== salvoHucresi)
            ShowRing(s.Cell);
            Juice?.SalvoStart();
        }

        /// <summary>
        /// renkAviFaz.ts salvoBitir: the tapped block leaves its cell and swells to 1.18 in 0.11 s; at the peak — shake, ×0.12 dip,
        /// kirildi, puff 1.15, 14 hot points, the burst, the sticker. Its four neighbours flinch now. The steps queue behind it.
        /// </summary>
        private void FinishSalvo(SalvoJob s)
        {
            Salvo?.Clear();
            ShowRing(-1);
            var view = ViewAt(s.Cell);
            if (view != null)
            {
                _views.Remove(s.Cell);
                _loose.Add(view);
                view.Dim = false;                                   // sonenler are drawn lit (soluk: false)
                var tone = S.PaletteColor(view.Block.Color);
                int cell = s.Cell;
                _swells.Start(view, S.TapSwellTime, S.TapSwell, true, () =>
                {
                    Juice?.Break();
                    Style.TapBurst(view, tone);
                    CountItem(view, BoardRoot.TransformPoint(new Vector3(view.Pos.x, view.Pos.y, -PotaLook.FrontZ)));
                    RaiseCellBurst(cell);
                });
                RippleNeighbours(s.Cell);
            }
            else Juice?.Break();
            DisplayedAmmo = Board.Ammo;
            Raise(AmmoChanged);
            _jobs.RemoveAt(0);
            QueueSteps(s.Steps, true, -1);
        }

        public PuzzleLook Look { get; private set; } = PuzzleLook.Pota;
        /// <summary>The studio polish runs for every look but Pota (spec §3).</summary>
        public bool Studio => Look != PuzzleLook.Pota;
        /// <summary>The studio polish that the feel layer opens to the Pota look too: board intro, idle hint, ammo pulse and roll, chain words.</summary>
        public bool Polish => Studio || (S != null && S.Feel != null && S.Feel.Enabled);
        /// <summary>The feel layer's switch (spec 2026-10-03); false in A/B and when the asset turns it off.</summary>
        public bool Feel => S != null && S.Feel != null && S.Feel.Enabled;

        /// <summary>Blocks the current ammo covers (F2: one left → the hint points at it at once).</summary>
        public int AffordableCount
        {
            get
            {
                if (Board == null) return 0;
                int n = 0;
                foreach (var c in Board.Cells) if (c.Hp <= Board.Ammo) n++;
                return n;
            }
        }
        /// <summary>The studio sticker flight: always under A/B, under the Pota look while <see cref="ColorHuntSettings.StudioFlight"/> is on.</summary>
        public bool StudioFlight => Studio || (S != null && S.StudioFlight);
        public IBurstStyle Style => _style ?? (_style = Resolve(Look));
        public event Action LookChanged;

        private IBurstStyle _style;
        private readonly Dictionary<PuzzleLook, IBurstStyle> _styles = new Dictionary<PuzzleLook, IBurstStyle>();

        /// <summary>Switches the look (the record tool and designers do it at runtime): the old style is cleared and deactivated.</summary>
        public void SetLook(PuzzleLook look)
        {
            if (_style != null && look == Look) return;
            if (_style != null) { _style.Clear(); _style.Activate(false); }
            Look = look;
            _style = Resolve(look);
            _style.Activate(true);
            Raise(LookChanged);
        }

        /// <summary>Registers (and builds) the style a look uses; later looks and tests plug in here.</summary>
        public void UseStyle(PuzzleLook look, IBurstStyle style)
        {
            style.Build(this);
            _styles[look] = style;
        }

        private IBurstStyle Resolve(PuzzleLook look)
        {
            if (!_styles.TryGetValue(look, out var style)) UseStyle(look, style = CreateStyle(look));
            if (style.Ready) return style;
            Debug.LogWarning($"Color Hunt: the {look} look is missing assets (run ai_studio_import); Pota's burst plays instead.");
            return Resolve(PuzzleLook.Pota);
        }

        private IBurstStyle CreateStyle(PuzzleLook look)
        {
            switch (look)
            {
                case PuzzleLook.Pota: return new PotaBurstStyle();
                case PuzzleLook.Candy: return AddStyle<CandyBurst>("CandyBurst");
                case PuzzleLook.Shatter: return AddStyle<ShatterBurst>("ShatterBurst");
                default: return new NoStyle();
            }
        }

        /// <summary>A MonoBehaviour style on its own child of the game root (so it lives in the hoop's space, not the board's).</summary>
        private T AddStyle<T>(string name) where T : MonoBehaviour, IBurstStyle
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        // ---------- special balls (spec 2026-10-01) ----------

        public SpecialLook SpecialLook { get; private set; } = SpecialLook.Pota;
        /// <summary>What draws the special balls; a look whose assets are missing falls back to Pota's with one warning.</summary>
        public ISpecialStyle Specials => _special ?? (_special = ResolveSpecial(SpecialLook));
        public event Action SpecialLookChanged;
        /// <summary>A block burst — its swell peak, a consumed special's fire, a gathered block's arrival — with the cell it had at its step's start.</summary>
        public event Action<int> CellBurst;
        /// <summary>A special ball was born (its kind and cell), right after the look drew it — the recorder's "born" marker.</summary>
        public event Action<SpecialKind, int> SpecialBorn;
        /// <summary>A special fired, right after the look's Fire — the recorder's "fire" marker.</summary>
        public event Action<SpecialFire> SpecialFired;
        /// <summary>Golden balls that reached the hoop since the deal (no HUD for them yet: U25).</summary>
        public int GoldenLanded { get; private set; }
        /// <summary>Golden balls on their way to the hoop; the board is locked meanwhile (Pota kilitliMi).</summary>
        public int GoldenInFlight => _goldens.Count;
        /// <summary>The giant hoop around the board (PuzzleStage builds it under this root), or null.</summary>
        public GiantHoop Hoop => _hoop != null ? _hoop : (_hoop = GetComponentInChildren<GiantHoop>(true));
        /// <summary>A style's flight must land within this many seconds after Pota's would; otherwise the game lands it (one warning).</summary>
        public const float GoldenGrace = 3f;
        /// <summary>A gathered block must arrive within this many seconds after its gather time; otherwise the game settles it (one warning).</summary>
        public const float GatherGrace = 3f;

        private ISpecialStyle _special;
        private readonly Dictionary<SpecialLook, ISpecialStyle> _specialStyles = new Dictionary<SpecialLook, ISpecialStyle>();
        private class GoldenState { public int Col; public bool Passed; public float Age, Life; }
        /// <summary>One gather's identity: a late "arrived" checks it owns the view's current ticket before touching it (a pooled view
        /// the grace settled may since have been handed to a fresh gather).</summary>
        private class GatherTicket { public float TimeLeft; }
        private readonly List<GoldenState> _goldens = new List<GoldenState>();
        /// <summary>Pota gorunenGecisler: the deal's gates, + opened ones at a step's drops, − one per golden ball that passed.</summary>
        private readonly List<int> _shownGates = new List<int>();
        /// <summary>
        /// Birth-group blocks travelling into their birth cell (gathering looks) → game seconds left before the game settles them itself
        /// (GatherTime + <see cref="GatherGrace"/>); the style moves them, the game ticks them.
        /// </summary>
        private readonly Dictionary<BlockView, GatherTicket> _gathering = new Dictionary<BlockView, GatherTicket>();
        private readonly List<BlockView> _gatherKeys = new List<BlockView>(), _gatherDue = new List<BlockView>();
        private int _specialGen;
        private GiantHoop _hoop;

        /// <summary>Switches the special look (the demo's buttons, the record tool): the old style is cleared and deactivated.</summary>
        public void SetSpecialLook(SpecialLook look)
        {
            if (_special != null && look == SpecialLook) return;
            if (_special != null)
            {
                SettleSpecialWork();
                _special.Clear();
                _special.Activate(false);
            }
            SpecialLook = look;
            _special = ResolveSpecial(look);
            _special.Activate(true);
            ReplanUnfiredSpecials();
            Raise(SpecialLookChanged);
        }

        /// <summary>
        /// A look switch while a trigger step runs: the specials still to fire follow the new look (fires already done stay done). One the
        /// old look drew itself but the new one does not goes back to the pending swells at its planned start (it swells and bursts as in
        /// Pota); one the new look draws is hidden at its fire — its pending swell is dropped, a running one cancelled before its peak (one
        /// past its peak has burst: it stays Pota's). Each cell still bursts once: by its swell or at its fire.
        /// </summary>
        private void ReplanUnfiredSpecials()
        {
            if (_jobs.Count == 0 || !(_jobs[0] is StepJob st) || !st.Started || st.Dropping || st.Plan == null) return;
            bool consume = _special.ConsumesSpecialView;
            for (int k = st.NextFire; k < st.Plan.Fires.Count; k++)
            {
                int cell = st.Plan.Fires[k].Cell;
                var v = ViewAt(cell);
                if (v == null || consume == st.Consumed.Contains(cell)) continue;
                if (!consume)
                {
                    st.Consumed.Remove(cell);
                    float at = st.Plan.Start.TryGetValue(cell, out float planned) ? planned : 0f;
                    st.Pending.Add(new PendingSwell { Cell = cell, View = v, At = at });
                    continue;
                }
                int pending = st.Pending.FindIndex(p => p.Cell == cell);
                if (pending >= 0) st.Pending.RemoveAt(pending);
                else if (_swells.TryCancel(v)) v.Swell = -1f;
                else continue;
                st.Consumed.Add(cell);
            }
        }

        /// <summary>Registers (and builds) the style a special look uses; later looks and tests plug in here.</summary>
        public void UseSpecialStyle(SpecialLook look, ISpecialStyle style)
        {
            style.Build(this);
            _specialStyles[look] = style;
        }

        private ISpecialStyle ResolveSpecial(SpecialLook look)
        {
            if (!_specialStyles.TryGetValue(look, out var style)) UseSpecialStyle(look, style = CreateSpecialStyle(look));
            if (style.Ready) return style;
            Debug.LogWarning($"Color Hunt: the {look} special look is missing assets (run ai_studio_import); Pota's specials play instead.");
            return ResolveSpecial(SpecialLook.Pota);
        }

        private ISpecialStyle CreateSpecialStyle(SpecialLook look)
        {
            switch (look)
            {
                case SpecialLook.Pota: return AddSpecialStyle<PotaSpecials>("PotaSpecials");
                case SpecialLook.Royal: return AddSpecialStyle<RoyalSpecials>("RoyalSpecials");
                case SpecialLook.Court: return AddSpecialStyle<CourtSpecials>("CourtSpecials");
                default: return new NoSpecialStyle();
            }
        }

        /// <summary>A MonoBehaviour special style on its own child of the game root (game-root space: the hoop's, not the board's).</summary>
        private T AddSpecialStyle<T>(string name) where T : MonoBehaviour, ISpecialStyle
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// renkAviFaz.ts adimlariSiraya: a salvo's first step is depth 0 (its block already burst); a special tap's and a rotation's steps
        /// start from 0 and go one deeper at every step with something to burst, so a special's blast plays at depth 1. A pure basket step
        /// keeps the depth (it is no chain).
        /// </summary>
        private void QueueSteps(List<Step> steps, bool salvo, int origin)
        {
            int depth = salvo ? -1 : 0;
            for (int k = 0; k < steps.Count; k++)
            {
                var step = steps[k];
                if (step.Exploded.Count > 0 || (k == 0 && salvo)) depth++;
                _jobs.Add(new StepJob { Step = step, Depth = depth, Origin = k == 0 ? origin : -1 });
            }
            if (depth > LongestChain) LongestChain = depth;
        }

        /// <summary>Pota blokHex: a normal block bursts in its palette tone, a special in OZEL_RENK_HEX (its body is the darker OZEL_BLOK_TONU).</summary>
        private Color BurstTone(Block b) => b.IsSpecial ? PotaLook.Hex(PotaLook.SpecialHex(b.Special)) : S.PaletteColor(b.Color);

        private void RaiseCellBurst(int cell)
        {
            try { CellBurst?.Invoke(cell); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>A new board: no callback of the old one counts, nothing flies, the rings show the board's gates.</summary>
        private void ResetSpecials()
        {
            _specialGen++;
            _goldens.Clear();
            GoldenLanded = 0;
            _special?.Clear();
            _shownGates.Clear();
            if (Board != null && Board.Gates != null) _shownGates.AddRange(Board.Gates);
            Gates?.Show(_shownGates);
        }

        /// <summary>
        /// A look switch mid-effect: golden balls in the air land now (counted, no effect); gathered blocks arrive now — counted, burst
        /// (<see cref="CellBurst"/> with their step-start cell, which a gathered view keeps: it left the board before any drop) and
        /// recycled — exactly as their arrival would have done.
        /// </summary>
        private void SettleSpecialWork()
        {
            int gen = ++_specialGen;
            foreach (var g in _goldens)
            {
                if (!g.Passed) CloseGate(g.Col);
                GoldenLanded++;
            }
            _goldens.Clear();
            var gathered = new List<BlockView>(_gathering.Keys);   // a CellBurst listener may deal a new board (Deal bumps the gen)
            _gathering.Clear();
            foreach (var v in gathered)
            {
                if (gen == _specialGen)
                {
                    CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                    RaiseCellBurst(v.Cell);
                }
                Recycle(v);
            }
        }

        private void OpenGates(List<int> opened)
        {
            if (opened == null || opened.Count == 0) return;
            _shownGates.AddRange(opened);
            Gates?.Show(_shownGates);
            Juice?.Sounds?.Zip();
        }

        /// <summary>altinlariIlerlet: the ring closes when its ball passes — one of them (the same step may have opened the column again).</summary>
        private void CloseGate(int col)
        {
            int k = _shownGates.IndexOf(col);
            if (k < 0) return;
            _shownGates.RemoveAt(k);
            Gates?.Show(_shownGates);
        }

        private void LaunchGolden(Vector2 cellPos, int col, float delay)
        {
            int n = Board.Size;
            var route = new GoldenRoute
            {
                From = transform.InverseTransformPoint(BoardRoot.TransformPoint(new Vector3(cellPos.x, cellPos.y, 0f))),
                Gate = GateRings.RingAt(n, col),
                Hoop = GoldenPath.HoopLocal,
                Delay = delay,
                Col = col,
                Scale = PotaLook.GridScale(n),
            };
            var state = new GoldenState { Col = col, Life = delay + GoldenPath.Total + GoldenGrace };
            _goldens.Add(state);
            int gen = _specialGen;
            Specials.GoldenFlight(route,
                () =>
                {
                    if (gen != _specialGen || state.Passed || !_goldens.Contains(state)) return;
                    state.Passed = true;
                    CloseGate(col);
                },
                () =>
                {
                    if (gen != _specialGen || !_goldens.Remove(state)) return;
                    if (!state.Passed) { state.Passed = true; CloseGate(col); }
                    GoldenLanded++;
                });
        }

        /// <summary>Lands a golden ball whose style never called back (a broken look must not lock the board for good).</summary>
        private void TickGoldens(float dt)
        {
            for (int i = _goldens.Count - 1; i >= 0; i--)
            {
                var g = _goldens[i];
                g.Age += dt;
                if (g.Age < g.Life) continue;
                Debug.LogWarning($"Color Hunt: the {SpecialLook} special look never landed the golden ball of column {g.Col}; landed now.");
                _goldens.RemoveAt(i);
                if (!g.Passed) CloseGate(g.Col);
                GoldenLanded++;
            }
        }

        /// <summary>
        /// Settles a gathered block whose style never called arrived within GatherTime + <see cref="GatherGrace"/> (a broken look must not
        /// lock the board for good): one warning, then counted, burst (<see cref="CellBurst"/>, its step-start cell) and recycled, exactly as
        /// its arrival would have done; a late arrival afterwards finds it gone and does nothing.
        /// </summary>
        private void TickGathers(float dt)
        {
            if (_gathering.Count == 0) return;
            _gatherKeys.Clear();
            _gatherKeys.AddRange(_gathering.Keys);
            _gatherDue.Clear();
            foreach (var v in _gatherKeys)
            {
                var ticket = _gathering[v];
                ticket.TimeLeft -= dt;
                if (ticket.TimeLeft > 0f) continue;
                _gathering.Remove(v);
                _gatherDue.Add(v);
            }
            if (_gatherDue.Count == 0) return;
            var due = _gatherDue.ToArray();                        // a CellBurst listener may deal a new board (and gather again)
            int gen = _specialGen;
            foreach (var v in due)
            {
                Debug.LogWarning($"Color Hunt: the {SpecialLook} special look never brought the gathered block of cell {v.Cell} into its birth cell; settled now.");
                if (gen == _specialGen)                            // a CellBurst listener may deal a new board
                {
                    CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                    RaiseCellBurst(v.Cell);
                }
                Recycle(v);
            }
        }

        /// <summary>Gathering looks: a birth group's other blocks travel into the birth cell instead of bursting (their stickers fly on arrival).</summary>
        private HashSet<int> GatherBirthGroups(StepJob st)
        {
            var gathered = new HashSet<int>();
            if (st.Step.Born.Count == 0 || !Specials.GathersBirthGroup) return gathered;
            var exploded = new HashSet<int>(st.Step.Exploded);
            int n = Board.Size, gen = _specialGen;
            float time = GatherDuration();
            foreach (var b in st.Step.Born)
            {
                if (b.Group == null) continue;
                var home = ViewAt(b.Cell);
                var to = home != null ? home.Home : BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, b.Cell), ColorHuntRules.Col(n, b.Cell));
                foreach (int g in b.Group)
                {
                    if (!exploded.Contains(g) || gathered.Contains(g)) continue;
                    var v = ViewAt(g);
                    if (v == null) continue;
                    gathered.Add(g);
                    _views.Remove(g);
                    var ticket = new GatherTicket { TimeLeft = time + GatherGrace };
                    _gathering[v] = ticket;
                    v.Exploding = true;
                    int cell = g;
                    Specials.Gather(v, to, time, arrived =>
                    {
                        if (gen != _specialGen || !_gathering.TryGetValue(arrived, out var current) || current != ticket) return;
                        _gathering.Remove(arrived);
                        CountItem(arrived, BoardRoot.TransformPoint(new Vector3(arrived.Pos.x, arrived.Pos.y, -PotaLook.FrontZ)));
                        RaiseCellBurst(cell);
                        Recycle(arrived);
                    });
                }
            }
            return gathered;
        }

        private float GatherDuration() => Specials.Timing.GatherTime > 0f ? Specials.Timing.GatherTime : S.ChainTime;

        private void StartBurstSwell(StepJob st, int cell, BlockView v, float elapsed)
        {
            int d = st.Depth;
            var tone = BurstTone(v.Block);
            _swells.Start(v, S.ChainTime, S.ChainSwell, false, () =>
            {
                st.Peak?.Invoke();
                Style.StepBurst(v, tone, d);
                CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                RaiseCellBurst(cell);
            }, elapsed);
        }

        /// <summary>Starts the swells whose time has come (credited with the part of the frame they missed) and fires the specials that are due.</summary>
        private void TickPlan(StepJob st, float dt)
        {
            for (int k = 0; k < st.Pending.Count;)
            {
                var p = st.Pending[k];
                if (p.At > st.T) { k++; continue; }
                st.Pending.RemoveAt(k);
                StartBurstSwell(st, p.Cell, p.View, st.T - dt - p.At);
            }
            if (st.Plan == null) return;
            while (st.NextFire < st.Plan.Fires.Count && st.Plan.Fires[st.NextFire].Time <= st.T) FireSpecial(st, st.Plan.Fires[st.NextFire++]);
        }

        /// <summary>The drops start: every special that has not fired fires, every swell that has not started bursts now (End covers both; a long frame may not).</summary>
        private void FlushPlan(StepJob st)
        {
            if (st.Plan != null)
                while (st.NextFire < st.Plan.Fires.Count) FireSpecial(st, st.Plan.Fires[st.NextFire++]);
            foreach (var p in st.Pending) StartBurstSwell(st, p.Cell, p.View, 0f);   // StartDrops' End() bursts them at once
            st.Pending.Clear();
        }

        private void FireSpecial(StepJob st, SpecialFire f)
        {
            if (st.Consumed.Contains(f.Cell))
            {
                var v = ViewAt(f.Cell);
                if (v != null) v.gameObject.SetActive(false);      // the look draws it now; the drops recycle the view
                st.Peak?.Invoke();
                RaiseCellBurst(f.Cell);
            }
            Specials.Fire(f);
            try { SpecialFired?.Invoke(f); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public int ItemsInFlight => _flights;

        /// <summary>
        /// The target count the HUD shows (renkAviFaz.ts hedefAzalt / hedefHudYaz): one less per sticker that lands, never below the
        /// rules, and equal to the rules once the board has settled with no sticker in the air.
        /// </summary>
        public int ShownTarget(int color) => _shown.TryGetValue(color, out int v) ? v : Remaining(color);

        private int Remaining(int color)
        {
            if (Board != null) foreach (var t in Board.Targets) if (t.Color == color) return Mathf.Max(0, t.Remaining);
            return 0;
        }

        private void ResetShownTargets(bool raise = true)
        {
            _shown.Clear();
            _flights = 0;
            _flightGen++;                              // a sticker still flying from the old board changes nothing
            _targetsPending = false;
            if (Board != null) foreach (var t in Board.Targets) _shown[t.Color] = Mathf.Max(0, t.Remaining);
            if (raise) Raise(TargetsChanged);
        }

        /// <summary>A burst block of a target colour: its sticker flies to the HUD and lowers the count on arrival (at once if it cannot fly).</summary>
        private void CountItem(BlockView v, Vector3 world)
        {
            int color = v.Block.Color;
            if (!_shown.ContainsKey(color)) return;
            int gen = _flightGen;
            if (Hud != null && Hud.FlyItem(color, world, () =>
                {
                    if (gen != _flightGen) return;
                    _flights--;
                    LowerShown(color);
                    SyncTargetsIfSettled();
                }))
                _flights++;
            else LowerShown(color);
        }

        private void LowerShown(int color)
        {
            _shown[color] = Mathf.Max(Remaining(color), _shown[color] - 1);
            Raise(TargetsChanged);
        }

        private void SyncTargetsIfSettled()
        {
            if (Busy || _flights > 0 || Board == null) return;
            _targetsPending = false;
            bool changed = false;
            foreach (var t in Board.Targets)
            {
                int r = Mathf.Max(0, t.Remaining);
                if (_shown.TryGetValue(t.Color, out int s) && s == r) continue;
                _shown[t.Color] = r;
                changed = true;
            }
            if (changed) Raise(TargetsChanged);
        }

        private void RippleNeighbours(int cell)
        {
            int n = Board.Size, r = ColorHuntRules.Row(n, cell), c = ColorHuntRules.Col(n, cell);
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int rr = r + dr, cc = c + dc;
                if (rr < 0 || rr >= n || cc < 0 || cc >= n) continue;
                var v = ViewAt(ColorHuntRules.Index(n, rr, cc));
                if (v != null) v.Squash = Mathf.Max(v.Squash, PotaLook.NeighbourSquash);
            }
        }

        /// <summary>
        /// renkAviFaz.ts adimBaslat: depth 0 and a pure basket step drop at once; deeper steps swell every block to 1.24 over ChainTime. The
        /// first peak carries the step's feedback (shake, ×0.06 dip, kombo, "×d"); each block's peak its own puff, points, burst and sticker.
        /// A trigger step (a special was tapped) follows the look's plan: each cell's swell starts at its planned time, each special fires at
        /// its own (Pota: all at once, every fire at the first peak, before the step's own feedback), the drops wait for the plan's end. A
        /// gathering look sends a birth group into its birth cell instead.
        /// </summary>
        private void StartStep(StepJob st)
        {
            st.Started = true;
            st.SwellEnd = S.ChainTime;
            if (st.Depth == 0 || st.Step.Exploded.Count == 0) { StartDrops(st); return; }
            int d = st.Depth;
            bool peaked = false;
            BlockView first = null;
            Vector2 centre = Vector2.zero;
            int count = 0;
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                centre += v.Pos;
                count++;
            }
            if (count > 0) centre /= count;
            st.Peak = () =>
            {
                if (peaked) return;
                peaked = true;
                var at = first != null ? first.Pos : Vector2.zero;
                Juice?.Chain(d, BoardRoot.TransformPoint(new Vector3(at.x, at.y + 0.5f, -PotaLook.LabelZ)), Style.ShowsChainNumber);
                Style.ChainPeak(d, centre, first != null ? BurstTone(first.Block) : Color.white);
            };
            var gathered = GatherBirthGroups(st);
            if (gathered.Count > 0) st.SwellEnd = Mathf.Max(st.SwellEnd, GatherDuration());
            if (st.Step.Triggered.Count > 0)
            {
                st.Plan = SpecialSchedule.Plan(Board.Size, st.Step, st.Origin, Specials.Timing, S.ChainTime);
                st.SwellEnd = Mathf.Max(st.SwellEnd, st.Plan.End);
                if (Specials.ConsumesSpecialView) foreach (var f in st.Plan.Fires) st.Consumed.Add(f.Cell);
                Specials.TriggerStep(st.Plan.Fires.Count, centre);
            }
            foreach (int i in st.Step.Exploded)
            {
                if (gathered.Contains(i)) continue;
                var v = ViewAt(i);
                if (v == null) continue;
                if (first == null) first = v;
                v.Exploding = true;
                if (st.Consumed.Contains(i)) continue;              // a firing special the look draws itself
                float at = st.Plan != null && st.Plan.Start.TryGetValue(i, out float planned) ? planned : 0f;
                st.Pending.Add(new PendingSwell { Cell = i, View = v, At = at });
            }
            TickPlan(st, 0f);                                      // the swells due at 0 start now, as before the plan existed
            if (first == null) st.Peak();
        }

        private void StartDrops(StepJob st)
        {
            FlushPlan(st);
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                _swells.End(v);                 // gorselSil: a burst that has not peaked yet runs now
                Recycle(v);
                _views.Remove(i);
            }
            int n = Board.Size;
            // dususleriBaslat: born specials take their cell's view (it then falls with its column), golden balls leave for the hoop
            // 0.12 s apart, newly opened gates light up — all before the columns fall.
            foreach (var b in st.Step.Born)
            {
                var v = ViewAt(b.Cell);
                if (v == null) continue;
                v.SetBlock(Block.MakeSpecial(b.Kind), b.Cell);
                Specials.Birth(v, b.Kind);
                try { SpecialBorn?.Invoke(b.Kind, b.Cell); }
                catch (Exception e) { Debug.LogException(e); }
            }
            for (int k = 0; k < st.Step.ToBasket.Count; k++)
            {
                var bd = st.Step.ToBasket[k];
                var v = ViewAt(bd.Cell);
                var at = v != null ? v.Pos : BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, bd.Cell), ColorHuntRules.Col(n, bd.Cell));
                if (v != null) { _views.Remove(bd.Cell); Recycle(v); }
                LaunchGolden(at, bd.Col, k * GoldenPath.Gap);
            }
            OpenGates(st.Step.Opened);
            float longest = 0f;
            var moved = new Dictionary<int, BlockView>();
            foreach (var d in st.Step.Drops)
            {
                var v = ViewAt(d.From);
                if (v == null) continue;
                _views.Remove(d.From);
                int dist = ColorHuntRules.Row(n, d.To) - ColorHuntRules.Row(n, d.From);
                v.Home = BoardBuilder.CellPosition(n, ColorHuntRules.Row(n, d.To), ColorHuntRules.Col(n, d.To));
                v.Cell = d.To;
                _drops[v] = new DropState { StartY = v.Pos.y, Distance = Mathf.Max(0, dist) };
                longest = Mathf.Max(longest, dist);
                moved[d.To] = v;
            }
            foreach (var sp in st.Step.Spawns)
            {
                int row = ColorHuntRules.Row(n, sp.To), col = ColorHuntRules.Col(n, sp.To);
                var v = NewView(sp.Block, sp.To, BoardBuilder.CellPosition(n, row, col));
                var start = BoardBuilder.CellPosition(n, row - sp.Distance, col);
                v.Pos = start;
                _drops[v] = new DropState { StartY = start.y, Distance = sp.Distance };
                longest = Mathf.Max(longest, sp.Distance);
                moved[sp.To] = v;
            }
            foreach (var kv in moved) _views[kv.Key] = kv.Value;
            st.Dropping = true;
            st.T = 0f;
            st.DropTime = PotaLook.StepDropTime(longest, S.DropTimePerCell);
        }

        private void AdvanceViews(float dt)
        {
            _finished.Clear();
            _swells.Advance(dt, (v, scale, o) => { v.Swell = scale; v.Flash = Mathf.Max(v.Flash, o * o); }, _finished);
            foreach (var v in _finished) { _loose.Remove(v); Recycle(v); }
            if (_disperse >= 0f)
            {
                _disperse -= dt;
                if (_disperse < 0f) { foreach (var v in _views.Values) Recycle(v); _views.Clear(); _swells.Clear(); }
            }
            foreach (var kv in _drops)
            {
                var v = kv.Key; var d = kv.Value;
                d.T += dt;
                float travelled = Mathf.Min(d.Distance, PotaLook.DropTravel(d.T, S.DropTimePerCell));
                v.Pos = new Vector2(v.Home.x, d.StartY - travelled);
                if (!d.Landed && travelled >= d.Distance)
                {
                    d.Landed = true;
                    if (d.Distance > 0f)
                    {
                        if (Feel) v.Bounce = 0f; else v.Squash = 1f;      // F6: directional bounce instead of the uniform 0.88
                        Juice?.Landed(d.Distance);
                        if (d.Distance >= 1f) Fx?.Puff(new Vector2(v.Home.x, v.Home.y - 0.45f), 0.4f + 0.08f * d.Distance);
                    }
                }
            }
            foreach (var v in _views.Values)
            {
                bool ammoCovers = v.Block.Hp <= DisplayedAmmo;
                v.Dim = Current == State.Playing && !ammoCovers && !v.Exploding && !v.Block.IsSpecial;   // soluk: … && !g.blok.ozel
            }
        }

        // ---------- views ----------

        private void BuildViews(Board b)
        {
            for (int i = 0; i < b.Cells.Length; i++)
            {
                var p = BoardBuilder.CellPosition(b.Size, ColorHuntRules.Row(b.Size, i), ColorHuntRules.Col(b.Size, i));
                _views[i] = NewView(b.Cells[i], i, p);
            }
        }

        private BlockView NewView(Block block, int cell, Vector2 home)
        {
            BlockView v;
            if (_pool.Count > 0) { v = _pool.Pop(); v.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("Block");
                go.transform.SetParent(_blocksRoot, false);
                v = go.AddComponent<BlockView>();
                v.Init(S, _blockMesh, _blockMat, _labelMat);
            }
            Clip?.Assign(v.gameObject);                                // F7: the board layer while clipping is on (pooled views too)
            v.Home = home; v.Pos = home; v.Scale = 1f; v.Flash = 0f; v.Squash = 0f; v.Shiver = 0f; v.Exploding = false; v.Dim = false; v.Swell = -1f; v.LabelAngle = 0f; v.Pulse = 0f;
            v.ResetStudio();
            v.SetBlock(block, cell);
            v.transform.localPosition = new Vector3(home.x, home.y, 0f);
            v.transform.localScale = Vector3.one;
            return v;
        }

        private void Recycle(BlockView v)
        {
            v.gameObject.SetActive(false);
            _pool.Push(v);
        }

        /// <summary>Studio board intro (a no-op under Pota): call between Prepare and Begin, as NewRound does.</summary>
        public void StartIntro()
        {
            if (!Polish) return;
            _intro = Feel ? -S.Feel.IntroBoardAt : 0f;      // the feel layer's ribbon and goals come first
            _introSpeed = 1f;
            _introOn = true;
            foreach (var v in _views.Values) v.Scale = 0f;
            Raise(IntroStarted);
        }

        /// <summary>F1: a tap during the intro compresses what is left into Feel.IntroSkipTime.</summary>
        public void SkipIntro()
        {
            if (!_introOn || Board == null) return;
            float remaining = Mathf.Max(0.01f, StudioCurves.IntroTotal(Board.Size) - _intro);
            float skip = Feel ? S.Feel.IntroSkipTime : 0.3f;
            _introSpeed = Mathf.Max(_introSpeed, remaining / Mathf.Max(0.01f, skip));
            Raise(IntroSkipped);
        }

        /// <summary>Rows pop in bottom first, 0.03 s apart, each 0 → 1.15 → 1 in 0.3 s.</summary>
        private void AdvanceIntro(float dt)
        {
            if (!_introOn || Board == null) return;
            _intro += dt * _introSpeed;
            int n = Board.Size;
            foreach (var kv in _views)
            {
                int fromBottom = n - 1 - ColorHuntRules.Row(n, kv.Key);
                kv.Value.Scale = StudioCurves.IntroScale((_intro - StudioCurves.IntroDelay(fromBottom)) / StudioCurves.IntroTime);
            }
            if (_intro < StudioCurves.IntroTotal(n)) return;
            _intro = -1f;
            _introOn = false;
            _introSpeed = 1f;
            foreach (var v in _views.Values) v.Scale = 1f;
        }

        /// <summary>After 5 s without a touch the best move wiggles (±4°, twice) and glows; again every 5 s; any touch or job stops it.</summary>
        private void TickHint(float dt)
        {
            if (!Polish || Current != State.Playing || Busy || _selected >= 0) { _idle = 0f; StopHint(); return; }
            if (_hintT >= 0f)
            {
                _hintT += dt;
                float u = _hintT / StudioCurves.HintTime;
                if (_views.TryGetValue(_hintCell, out var v))
                {
                    v.Wiggle = StudioCurves.HintWiggle(u);
                    v.Flash = Mathf.Max(v.Flash, StudioCurves.HintGlow(u));
                }
                if (u >= 1f) { StopHint(); _idle = 0f; }
                return;
            }
            _idle += dt;
            bool tension = Feel && StudioCurves.LowAmmo(Board.Ammo, ColorHuntRules.Cheapest(Board));
            if (_idle < FeelCurves.HintDelay(AffordableCount, tension)) return;
            _hintCell = ColorHuntHint.Best(Board);
            if (_hintCell >= 0) _hintT = 0f;
            else _idle = 0f;
        }

        private void StopHint()
        {
            if (_hintCell >= 0 && _views.TryGetValue(_hintCell, out var v)) v.Wiggle = 0f;
            _hintCell = -1;
            _hintT = -1f;
        }

        private void ClearViews()
        {
            foreach (var v in _views.Values) Recycle(v);
            _views.Clear();
            _drops.Clear();
            _swells.Clear();
            foreach (var v in _loose) Recycle(v);
            _loose.Clear();
            foreach (var v in _gathering.Keys) Recycle(v);
            _gathering.Clear();
            _disperse = -1f;
            _intro = -1f; _introOn = false; _introSpeed = 1f; StopHint(); _idle = 0f;
            _jobs.Clear();
            Salvo?.Clear();
            Select(-1);
        }
    }
}
