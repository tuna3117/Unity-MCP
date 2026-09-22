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

        public State Current { get; private set; } = State.Idle;
        public Board Board { get; private set; }
        public Transform BoardRoot { get; private set; }
        public bool Busy => _jobs.Count > 0 || (Salvo != null && Salvo.Busy);
        public int DisplayedAmmo { get; private set; }
        public bool Won { get; private set; }
        public int Rotations { get; private set; }
        public int LongestChain { get; private set; }
        public uint Seed { get; private set; }

        public event Action AmmoChanged;
        public event Action TargetsChanged;
        public event Action<bool, string> RoundEnded;
        public event Action RoundStarted;

        private abstract class Job { public float T; }
        private class SalvoJob : Job { public int Cell, Cost, AmmoBefore; public List<Step> Steps; public bool Started; public int Arrived; }
        private class StepJob : Job { public Step Step; public bool Dropping; public float DropTime; public bool Started; public int Depth; }
        private class RotateJob : Job { public float Angle; public Board Rotated; }
        private class WaitJob : Job { public float Duration; }

        private readonly List<Job> _jobs = new List<Job>();
        private readonly Dictionary<int, BlockView> _views = new Dictionary<int, BlockView>();
        private readonly Stack<BlockView> _pool = new Stack<BlockView>();
        private readonly Dictionary<BlockView, DropState> _drops = new Dictionary<BlockView, DropState>();
        private readonly Dictionary<BlockView, float> _explosions = new Dictionary<BlockView, float>();
        private class DropState { public float StartY, Distance, T; public bool Landed; }

        private Mesh _blockMesh, _shardMesh;
        private Material _blockMat, _textMat, _shadowMat, _itemMat;
        private Font _font;
        private Transform _blocksRoot;
        private bool _checkNeeded;
        private float _accel;
        private int _selected = -1;
        private GameObject _selectionRing;
        private Vector3 _launcher;
        private float _resultTimer = -1f;

        // ---------- lifecycle ----------

        private void Start()
        {
            BoardRoot = new GameObject("Board").transform;
            BoardRoot.SetParent(transform, false);
            _blocksRoot = new GameObject("Blocks").transform;
            _blocksRoot.SetParent(BoardRoot, false);

            _font = S.Font != null ? S.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _textMat = DepthTextMaterial.Create(_font);
            _textMat.renderQueue = 3002;
            _shadowMat = DepthTextMaterial.Create(_font);
            _shadowMat.renderQueue = 3001;
            _font.RequestCharactersInTexture("0123456789", 96, FontStyle.Bold);
            _itemMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _itemMat.SetFloat("_Surface", 1f); _itemMat.SetFloat("_Blend", 0f); _itemMat.SetFloat("_ZWrite", 0f);
            _itemMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _itemMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _itemMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _itemMat.renderQueue = 3000;
            if (Fx != null) Fx.ItemMaterial = _itemMat;
            _blockMesh = RoundedBoxMesh.Create(new Vector3(S.BlockSize, S.BlockSize, S.BlockDepth), S.BlockRadius, 6);
            _shardMesh = RoundedBoxMesh.Create(Vector3.one, 0.22f, 3);
            _blockMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (S.BlockFace != null) _blockMat.SetTexture("_BaseMap", S.BlockFace);
            _blockMat.SetFloat("_Smoothness", 0.55f);
            _blockMat.EnableKeyword("_EMISSION");
            _blockMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            BoardBuilder.Build(transform, S, S.Size);
            _launcher = new Vector3(0f, -(S.Size * 0.5f + BoardBuilder.Margin + 0.55f), -0.1f);
            _accel = 2f / (S.DropTimePerCell * S.DropTimePerCell);

            var ringMat = RimMeshBuilder.UrpLit(S.GoldColor, 0.7f);
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", S.GoldColor * 0.6f);
            _selectionRing = RimMeshBuilder.MeshObject("SelectionRing", BoardRoot, RimMeshBuilder.Torus(0.56f, 0.06f, 32, 8), ringMat);
            _selectionRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _selectionRing.SetActive(false);

            if (Fx != null) Fx.Build(_shardMesh);
            NewRound();
        }

        public void NewRound()
        {
            ClearViews();
            Seed = S.Seed != 0 ? S.Seed : (uint)UnityEngine.Random.Range(1, int.MaxValue);
            Board = ColorHuntRules.Create(S.ToDesign(), S.Ammo, Seed);
            BuildViews(Board);
            DisplayedAmmo = Board.Ammo;
            Current = State.Playing;
            Won = false;
            Rotations = 0; LongestChain = 0;
            _checkNeeded = true;
            _resultTimer = -1f;
            BoardRoot.localRotation = Quaternion.identity;
            RoundStarted?.Invoke();
            AmmoChanged?.Invoke();
            TargetsChanged?.Invoke();
        }

        // ---------- public actions ----------

        public Vector3 CellWorld(int cell)
        {
            var p = BoardBuilder.CellPosition(Board.Size, ColorHuntRules.Row(Board.Size, cell), ColorHuntRules.Col(Board.Size, cell));
            return BoardRoot.TransformPoint(new Vector3(p.x, p.y, 0f));
        }

        public BlockView ViewAt(int cell) => _views.TryGetValue(cell, out var v) ? v : null;

        public void Select(int cell)
        {
            if (cell == _selected) return;
            _selected = cell;
            if (cell >= 0 && _views.TryGetValue(cell, out var v))
            {
                _selectionRing.SetActive(true);
                _selectionRing.transform.localPosition = new Vector3(v.Home.x, v.Home.y, -S.BlockDepth * 0.5f);
                v.Squash = Mathf.Max(v.Squash, 0.35f);
                Juice?.Select();
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
                if (_views.TryGetValue(cell, out var v)) { v.Shiver = S.InsufficientShake; v.FlashRedLabel(); }
                Juice?.Insufficient(CellWorld(cell), r.Cost);
                return "insufficient";
            }
            Board = r.Board;
            _checkNeeded = true;
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
            if (Current == State.Idle) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Salvo?.Tick(dt);
            AdvanceJobs(dt);
            AdvanceViews(dt);
            if (Current == State.Playing && !Busy && _checkNeeded) AfterSettle();
            if (_resultTimer >= 0f)
            {
                _resultTimer -= Time.unscaledDeltaTime;
                if (_resultTimer < 0f) { _resultTimer = -1f; RoundEnded?.Invoke(Won, _endReason); }
            }
            float comp = -BoardRoot.localEulerAngles.z;
            foreach (var v in _views.Values) v.Tick(dt, comp);
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
            if (won && Fx != null)
            {
                var top = new Vector3(0f, S.Size * 0.5f + BoardBuilder.Margin + 1.2f, -0.8f);
                Fx.Confetti(top, S.Palette, 260);
            }
            _resultTimer = won ? S.WinWait : S.LoseWait;
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
                    BoardRoot.localRotation = Quaternion.Euler(0f, 0f, r.Angle * (1f - Mathf.Pow(1f - u, 3f)));
                    if (u >= 1f)
                    {
                        BoardRoot.localRotation = Quaternion.identity;
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
                    AmmoChanged?.Invoke();
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
            var target = CellWorld(s.Cell);
            var source = transform.TransformPoint(_launcher);
            var view = ViewAt(s.Cell);
            var color = view != null ? S.PaletteColor(view.Block.Color) : Color.white;
            Salvo?.Fire(source, target, s.Cost, S.SalvoDuration, hit =>
            {
                s.Arrived++;
                var v = ViewAt(s.Cell);
                if (v != null)
                {
                    v.Flash = Mathf.Max(v.Flash, 0.55f);
                    v.Squash = Mathf.Max(v.Squash, 0.5f);
                    if (s.Arrived % 3 == 1) Fx?.Sparkle(hit, color);
                }
                Juice?.BallHit();
            });
            if (view != null) { view.Flash = Mathf.Max(view.Flash, 0.4f); view.Squash = 0.35f; }
        }

        private void FinishSalvo(SalvoJob s)
        {
            Salvo?.Clear();
            var view = ViewAt(s.Cell);
            var pos = CellWorld(s.Cell);
            if (view != null)
            {
                Fx?.Explode(pos, S.PaletteColor(view.Block.Color), 0);
                FlyItem(view, pos);
                RippleNeighbours(s.Cell);
                Recycle(view);
                _views.Remove(s.Cell);
            }
            Juice?.Tap(pos, s.Cost);
            DisplayedAmmo = Board.Ammo;
            AmmoChanged?.Invoke();
            TargetsChanged?.Invoke();
            _jobs.RemoveAt(0);
            int depth = -1;
            foreach (var step in s.Steps)
            {
                depth++;
                _jobs.Add(new StepJob { Step = step, Depth = depth });
            }
            if (depth > LongestChain) LongestChain = depth;
        }

        /// <summary>A counted block's item sprite flies to its target box in the HUD and pops it (Unity extra).</summary>
        private void FlyItem(BlockView v, Vector3 from)
        {
            if (Fx == null || Hud == null || Cam == null) return;
            int color = v.Block.Color;
            bool counted = false;
            foreach (var t in Board.Targets) if (t.Color == color) counted = true;
            if (!counted) return;
            var icon = v.ItemIcon;
            if (icon == null) return;
            if (!Hud.TargetScreenPoint(color, out var screen)) return;
            float depth = Mathf.Abs(Cam.transform.position.z - from.z) - 1.5f;
            var to = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            Fx.FlyItem(icon, from + Vector3.back * 0.6f, to, 0.55f, () => Hud.PopTarget(color));
        }

        private void RippleNeighbours(int cell)
        {
            int n = Board.Size, r = ColorHuntRules.Row(n, cell), c = ColorHuntRules.Col(n, cell);
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                int rr = r + dr, cc = c + dc;
                if (rr < 0 || rr >= n || cc < 0 || cc >= n) continue;
                var v = ViewAt(ColorHuntRules.Index(n, rr, cc));
                if (v != null) v.Squash = Mathf.Max(v.Squash, S.NeighbourRipple * 3f);
            }
        }

        private void StartStep(StepJob st)
        {
            st.Started = true;
            if (st.Depth == 0 || st.Step.Exploded.Count == 0) { StartDrops(st); return; }
            Vector3 center = Vector3.zero;
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                _explosions[v] = 0f;
                v.Exploding = true;
                var p = CellWorld(i);
                center += p;
                Fx?.Explode(p, S.PaletteColor(v.Block.Color), st.Depth);
                FlyItem(v, p);
            }
            center /= Mathf.Max(1, st.Step.Exploded.Count);
            Juice?.Chain(st.Depth, center);
            TargetsChanged?.Invoke();
        }

        private void StartDrops(StepJob st)
        {
            foreach (int i in st.Step.Exploded)
            {
                var v = ViewAt(i);
                if (v == null) continue;
                _explosions.Remove(v);
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
            st.DropTime = longest > 0f ? S.DropTimePerCell * Mathf.Sqrt(longest) + 0.06f : 0f;
        }

        private void AdvanceViews(float dt)
        {
            var explodingKeys = new List<BlockView>(_explosions.Keys);
            foreach (var v in explodingKeys)
            {
                float t = _explosions[v] + dt;
                _explosions[v] = t;
                float u = Mathf.Min(1f, t / S.ChainTime);
                v.Scale = Mathf.Max(0.01f, 1f + 0.28f * u - 1.28f * u * u);
                v.Flash = Mathf.Max(v.Flash, u);
            }
            foreach (var kv in _drops)
            {
                var v = kv.Key; var d = kv.Value;
                d.T += dt;
                float travelled = Mathf.Min(d.Distance, 0.5f * _accel * d.T * d.T);
                v.Pos = new Vector2(v.Home.x, d.StartY - travelled);
                if (!d.Landed && travelled >= d.Distance)
                {
                    d.Landed = true;
                    if (d.Distance > 0f)
                    {
                        v.Squash = 1f;
                        Juice?.Landed(d.Distance);
                        Fx?.Puff(BoardRoot.TransformPoint(new Vector3(v.Home.x, v.Home.y - S.BlockSize * 0.5f, 0f)), Mathf.Min(1f, d.Distance / 4f));
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
                v.Init(S, _blockMesh, _blockMat, _font, _textMat, _shadowMat, _itemMat);
            }
            v.Home = home; v.Pos = home; v.Scale = 1f; v.Flash = 0f; v.Squash = 0f; v.Shiver = 0f; v.Exploding = false; v.Dim = false;
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

        private void ClearViews()
        {
            foreach (var v in _views.Values) Recycle(v);
            _views.Clear();
            _drops.Clear();
            _explosions.Clear();
            _jobs.Clear();
            Salvo?.Clear();
            Select(-1);
        }
    }
}
