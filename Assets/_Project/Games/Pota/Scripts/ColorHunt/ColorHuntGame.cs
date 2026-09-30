using System;
using System.Collections.Generic;
using Project.Basket.Visual;
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
        public ColorHuntHud Hud;
        public Camera Cam;
        [Tooltip("Deal a board on Start. PotaFlow turns this off and stages the board itself (Prepare → Begin).")]
        public bool AutoStart = true;

        public State Current { get; private set; } = State.Idle;
        public Board Board { get; private set; }
        public Transform BoardRoot { get; private set; }
        public bool Busy => _jobs.Count > 0 || (Salvo != null && Salvo.Busy) || _swells.AnyLoose || _intro >= 0f;

        private float _intro = -1f, _idle, _hintT = -1f;
        private int _hintCell = -1;

        /// <summary>Studio board intro running (spec §3.3): the board is busy, taps wait.</summary>
        public bool Intro => _intro >= 0f;
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
        private class StepJob : Job { public Step Step; public bool Dropping; public float DropTime; public bool Started; public int Depth; }
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

            if (Fx != null) { Fx.Build(); Fx.SetBoard(BoardRoot); }
            if (Salvo != null) Salvo.Board = BoardRoot;
            SetLook(S != null ? S.Look : PuzzleLook.Pota);
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
            Board = ColorHuntRules.Create(design ?? S.ToDesign(), ammo, Seed);
            BuildViews(Board);
            BoardRoot.localScale = Vector3.one * PotaLook.GridScale(Board.Size);
            Fx?.SetBoard(BoardRoot);
            Fx?.Clear();
            _style?.Clear();
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

        /// <summary>Tap a cell. Returns "broken", "insufficient" or "locked".</summary>
        public string Tap(int cell)
        {
            if (Current != State.Playing || Busy) return "locked";
            Select(-1);
            int ammoBefore = Board.Ammo;
            var r = ColorHuntRules.Break(Board, cell);
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
            Board = r.Board;
            _checkNeeded = true;
            _targetsPending = true;
            _jobs.Add(new SalvoJob { Cell = cell, Cost = r.Cost, AmmoBefore = ammoBefore, Steps = r.Steps });
            return "broken";
        }

        public bool Rotate(Rotation dir)
        {
            if (Current != State.Playing || Busy) return false;
            Select(-1);
            Board = ColorHuntRules.Rotate(Board, dir);
            Rotations++;
            _checkNeeded = true;
            _jobs.Add(new RotateJob { Angle = dir == Rotation.Clockwise ? -90f : 90f, Rotated = Board });
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

        private void TickViews(float dt)
        {
            foreach (var v in _views.Values) v.Tick(dt);
            foreach (var v in _loose) v.Tick(dt);
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
            if (won) Disperse();
            _resultTimer = won ? S.WinWait : S.LoseWait;
        }

        /// <summary>
        /// renkAviFaz.ts tahtayiDagit: every block gives six hot points and swells to 1.24 over 0.5 s; the views go after 0.52 s. The paper
        /// confetti comes too (Pota drops it at the dunk, which is a later step; the spec keeps it at the win).
        /// </summary>
        private void Disperse()
        {
            foreach (var v in _views.Values)
            {
                Fx?.Particles(v.Pos, S.PaletteColor(v.Block.Color), 6, 3.5f, 0.1f);
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
                        if (st.T >= S.ChainTime) StartDrops(st);
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
                _swells.Start(view, S.TapSwellTime, S.TapSwell, true, () =>
                {
                    Juice?.Break();
                    Style.TapBurst(view, tone);
                    CountItem(view, BoardRoot.TransformPoint(new Vector3(view.Pos.x, view.Pos.y, -PotaLook.FrontZ)));
                });
                RippleNeighbours(s.Cell);
            }
            else Juice?.Break();
            DisplayedAmmo = Board.Ammo;
            Raise(AmmoChanged);
            _jobs.RemoveAt(0);
            int depth = -1;
            foreach (var step in s.Steps)
            {
                depth++;
                _jobs.Add(new StepJob { Step = step, Depth = depth });
            }
            if (depth > LongestChain) LongestChain = depth;
        }

        public PuzzleLook Look { get; private set; } = PuzzleLook.Pota;
        /// <summary>The studio polish runs for every look but Pota (spec §3).</summary>
        public bool Studio => Look != PuzzleLook.Pota;
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
        /// renkAviFaz.ts adimBaslat: depth 0 drops at once; deeper steps swell every matched block to 1.24 over ChainTime. The first peak
        /// carries the step's feedback (shake, ×0.06 dip, kombo, "×d"); each block's peak its own puff, points, burst and sticker.
        /// </summary>
        private void StartStep(StepJob st)
        {
            st.Started = true;
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
            void StepPeak()
            {
                if (peaked) return;
                peaked = true;
                var at = first != null ? first.Pos : Vector2.zero;
                Juice?.Chain(d, BoardRoot.TransformPoint(new Vector3(at.x, at.y + 0.5f, -PotaLook.LabelZ)), Style.ShowsChainNumber);
                Style.ChainPeak(d, centre, first != null ? S.PaletteColor(first.Block.Color) : Color.white);
            }
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                if (first == null) first = v;
                v.Exploding = true;
                var tone = S.PaletteColor(v.Block.Color);
                _swells.Start(v, S.ChainTime, S.ChainSwell, false, () =>
                {
                    StepPeak();
                    Style.StepBurst(v, tone, d);
                    CountItem(v, BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y, -PotaLook.FrontZ)));
                });
            }
            if (first == null) StepPeak();
        }

        private void StartDrops(StepJob st)
        {
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                _swells.End(v);                 // gorselSil: a burst that has not peaked yet runs now
                Recycle(v);
                _views.Remove(i);
            }
            int n = Board.Size;
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
                        v.Squash = 1f;
                        Juice?.Landed(d.Distance);
                        if (d.Distance >= 1f) Fx?.Puff(new Vector2(v.Home.x, v.Home.y - 0.45f), 0.4f + 0.08f * d.Distance);
                    }
                }
            }
            foreach (var v in _views.Values)
            {
                bool ammoCovers = v.Block.Hp <= DisplayedAmmo;
                v.Dim = Current == State.Playing && !ammoCovers && !v.Exploding;
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
            v.Home = home; v.Pos = home; v.Scale = 1f; v.Flash = 0f; v.Squash = 0f; v.Shiver = 0f; v.Exploding = false; v.Dim = false; v.Swell = -1f; v.LabelAngle = 0f;
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
            if (!Studio) return;
            _intro = 0f;
            foreach (var v in _views.Values) v.Scale = 0f;
        }

        /// <summary>Rows pop in bottom first, 0.03 s apart, each 0 → 1.15 → 1 in 0.3 s.</summary>
        private void AdvanceIntro(float dt)
        {
            if (_intro < 0f || Board == null) return;
            _intro += dt;
            int n = Board.Size;
            foreach (var kv in _views)
            {
                int fromBottom = n - 1 - ColorHuntRules.Row(n, kv.Key);
                kv.Value.Scale = StudioCurves.IntroScale((_intro - StudioCurves.IntroDelay(fromBottom)) / StudioCurves.IntroTime);
            }
            if (_intro < StudioCurves.IntroTotal(n)) return;
            _intro = -1f;
            foreach (var v in _views.Values) v.Scale = 1f;
        }

        /// <summary>After 5 s without a touch the best move wiggles (±4°, twice) and glows; again every 5 s; any touch or job stops it.</summary>
        private void TickHint(float dt)
        {
            if (!Studio || Current != State.Playing || Busy || _selected >= 0) { _idle = 0f; StopHint(); return; }
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
            if (_idle < StudioCurves.HintDelay) return;
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
            _disperse = -1f;
            _intro = -1f; StopHint(); _idle = 0f;
            _jobs.Clear();
            Salvo?.Clear();
            Select(-1);
        }
    }
}
