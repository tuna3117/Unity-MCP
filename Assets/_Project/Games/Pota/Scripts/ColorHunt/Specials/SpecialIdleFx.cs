using System;
using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// Every number of the calm idles A–E (spec 2026-10-07 "Özel top bekleme alternatifleri"). Cells are board cells (× the grid scale on
    /// screen), times are game seconds. Measure: the eye is back on the board within a second, nothing spills into a neighbour cell, no
    /// steady stream of particles.
    /// </summary>
    [Serializable]
    public class IdleTuning
    {
        [Header("A · Sakin nefes (scale breath only)")]
        [Tooltip("The ball breathes ± this (2 %).")]
        public float BreathAmount = 0.02f;
        public float BreathHz = 0.8f;

        [Header("B · Parıltı süpürmesi (a shop-window glint across the sticker)")]
        [Tooltip("One sweep across the ball (s).")]
        public float GlintTime = 0.35f;
        [Tooltip("The streak's soft core across (cells).")]
        public float GlintWidth = 0.12f;
        [Tooltip("The streak leans this far from vertical, like '/' (degrees); it sweeps across it, left to right.")]
        public float GlintAngle = 35f;
        [Tooltip("Additive strength of the streak's core (only where the sticker is drawn).")]
        public float GlintBrightness = 0.8f;
        [Tooltip("A sweep starts every GlintEvery ± GlintJitter seconds.")]
        public float GlintEvery = 2.5f;
        public float GlintJitter = 0.4f;

        [Header("C · Kenar nabzı (a thin soft ring at the ball's edge)")]
        [Tooltip("Ring radius (cells): the ball's own edge.")]
        public float RimRadius = 0.36f;
        [Tooltip("The ring's soft core across (cells).")]
        public float RimThickness = 0.05f;
        [Tooltip("Peak opacity (additive strength) at the top of each pulse.")]
        public float RimPeak = 0.5f;
        public float RimHz = 1f;
        public Color RimBomb = new Color(1f, 0.58f, 0.2f);
        public Color RimLine = new Color(0.55f, 0.84f, 1f);
        public Color RimGolden = new Color(1f, 0.85f, 0.3f);

        [Header("D · Tek yıldızcık (one four-ray star at the ball's upper-right edge)")]
        [Tooltip("The star's size (cells).")]
        public float TwinkleSize = 0.22f;
        [Tooltip("Appear, grow, fade (s).")]
        public float TwinkleLife = 0.35f;
        [Tooltip("A star starts every TwinkleEvery ± TwinkleJitter seconds; nothing in between.")]
        public float TwinkleEvery = 1.8f;
        public float TwinkleJitter = 0.5f;
        [Tooltip("Its place on the edge: a random angle from the right, counter-clockwise (degrees).")]
        public float TwinkleAngleMin = 20f;
        public float TwinkleAngleMax = 70f;
        [Tooltip("Distance from the ball's centre (cells): its edge.")]
        public float TwinkleRadius = 0.36f;
        public float TwinkleBrightness = 1f;
        public Color TwinkleColor = Color.white;
        public Color TwinkleGolden = new Color(1f, 0.93f, 0.66f);

        [Header("E · Hafif sallantı (the sticker rocks, the ball barely breathes; no hop)")]
        public float SwayHz = 0.5f;
        [Tooltip("The sticker rocks ± this (degrees).")]
        public float SwayDegrees = 5f;
        [Tooltip("And breathes ± this (1 %).")]
        public float SwayBreath = 0.01f;
    }

    /// <summary>The calm idles' curves, pure (EditMode tests pin them).</summary>
    public static class IdleCurves
    {
        /// <summary>The glint's band starts and ends this far from the sticker's centre (sticker quad units): wholly off the ball.</summary>
        public const float GlintReach = 0.75f;

        private static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>A cell's phase in [0, 2π): golden-ratio steps, so neighbouring specials never breathe or flash together.</summary>
        public static float Phase(int cell) => Frac(cell * 0.618034f + 0.137f) * 2f * Mathf.PI;

        /// <summary>A: 1 ± amount, <paramref name="hz"/> breaths a second.</summary>
        public static float Breath(float t, float phase, float amount, float hz) => 1f + amount * Mathf.Sin(2f * Mathf.PI * hz * t + phase);

        /// <summary>E: the sticker's rock in degrees, ± <paramref name="degrees"/>.</summary>
        public static float Sway(float t, float phase, float degrees, float hz) => degrees * Mathf.Sin(2f * Mathf.PI * hz * t + phase);

        /// <summary>B: the band's centre across the sticker (quad units from its centre) at u = age / GlintTime, eased in and out.</summary>
        public static float GlintSweep(float u)
        {
            u = Mathf.Clamp01(u);
            return Mathf.Lerp(-GlintReach, GlintReach, u * u * (3f - 2f * u));
        }

        /// <summary>C: the ring's opacity, 0 → peak → 0 once per 1/hz s.</summary>
        public static float RimAlpha(float t, float phase, float peak, float hz) => peak * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * hz * t + phase));

        /// <summary>B, D: seconds to the next start, every ± jitter (never under 0.05 s).</summary>
        public static float NextInterval(System.Random r, float every, float jitter) =>
            Mathf.Max(0.05f, every + jitter * (2f * (float)r.NextDouble() - 1f));

        /// <summary>B, D: the first start, spread over one interval by the cell's phase so the balls never start together.</summary>
        public static float FirstDelay(int cell, float every) => 0.25f + Phase(cell) / (2f * Mathf.PI) * Mathf.Max(0f, every);

        /// <summary>D: the star's angle on the ball's edge (degrees from the right, counter-clockwise), uniform in [min, max].</summary>
        public static float TwinkleAngle(System.Random r, float min, float max) => Mathf.Lerp(min, max, (float)r.NextDouble());

        /// <summary>D: the star's size over its life (u = age / life): grows 0.35 → 1, fast first.</summary>
        public static float TwinkleScale(float u)
        {
            u = Mathf.Clamp01(u);
            return 0.35f + 0.65f * (1f - (1f - u) * (1f - u));
        }

        /// <summary>D: its strength over its life: sin(πu), 0 at both ends.</summary>
        public static float TwinkleAlpha(float u) => u <= 0f || u >= 1f ? 0f : Mathf.Sin(Mathf.PI * u);
    }

    /// <summary>
    /// The calm idles A–E for resting special balls (spec 2026-10-07), chosen by ColorHuntSettings.SpecialIdle / ColorHuntGame.SetSpecialIdle
    /// in place of the special look's own idle. A and E write only <see cref="BlockView.IdleScale"/> (and E <see cref="BlockView.StickerSpin"/>);
    /// B, C and D draw one pooled additive quad per ball (Project/PotaGlint, white Blender stills tinted here) just in front of its
    /// sticker, hidden whenever the ball is not resting. Look writes nothing. Golden balls: A, B, C (yellow), D; E leaves them still.
    /// </summary>
    public class SpecialIdleFx : MonoBehaviour
    {
        /// <summary>The overlays sit this far (cells) in front of the sticker plane.</summary>
        public const float Lift = 0.02f;
        /// <summary>Blender's glint_streak: its soft core (alpha ≥ ½) is this share of the frame's height.</summary>
        public const float StreakCore = 0.10f;
        /// <summary>The streak texture's width in sticker quad units: its soft ends fall just outside the ball.</summary>
        public const float StreakLength = 1.25f;
        /// <summary>Blender's rim_ring: its centre line's radius and its soft core's width, in frame units.</summary>
        public const float RingRadius = 0.40f, RingCore = 0.043f;
        /// <summary>The rim's quad (cells): just inside the cell, so nothing can spill into a neighbour.</summary>
        public const float RimQuad = 0.98f;
        /// <summary>Sorting order: after the sticker (2) and the number (3).</summary>
        public const int Order = 4;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int UvRectId = Shader.PropertyToID("_UvRect");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private static readonly int AngleId = Shader.PropertyToID("_Angle");
        private static readonly int SpanId = Shader.PropertyToID("_Span");
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int HalfId = Shader.PropertyToID("_Half");
        private static readonly int TexRadiusId = Shader.PropertyToID("_TexRadius");
        private static readonly int TexHalfId = Shader.PropertyToID("_TexHalf");
        private static Mesh _quad;

        private class State
        {
            public SpecialKind Kind;
            public int Cell;
            public SpecialIdle Mode;
            public float Phase, T, Next, Age = -1f, Angle;
            public int Frame;
            public bool Spun;
            public MeshRenderer Quad;
        }

        private ColorHuntGame _game;
        private ColorHuntSettings _s;
        private Material _glintMat, _rimMat, _starMat;
        private bool _built, _warned;
        private readonly Dictionary<BlockView, State> _states = new Dictionary<BlockView, State>();
        private readonly Stack<MeshRenderer> _pool = new Stack<MeshRenderer>();
        private readonly List<BlockView> _stale = new List<BlockView>();
        private readonly List<Material> _owned = new List<Material>();
        private MaterialPropertyBlock _mpb;
        private System.Random _rng = new System.Random(20261007);

        /// <summary>Overlays drawn this frame (tests, the cost check).</summary>
        public int VisibleCount
        {
            get
            {
                int n = 0;
                foreach (var st in _states.Values) if (st.Quad != null && st.Quad.enabled) n++;
                return n;
            }
        }

        /// <summary>Resting specials this idle tracks.</summary>
        public int Tracked => _states.Count;

        private IdleTuning Tune => _s != null && _s.Idle != null ? _s.Idle : (_fallback ?? (_fallback = new IdleTuning()));
        private IdleTuning _fallback;

        private static Mesh Quad => _quad != null ? _quad : (_quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx"));

        /// <summary>The materials (one per drawn idle) from the settings' Project/PotaGlint template and the Blender stills.</summary>
        public void Build(ColorHuntGame game)
        {
            _game = game;
            _s = game != null ? game.S : null;
            if (_built || _s == null) return;
            _glintMat = Make("IdleGlint", 1f, _s.LabelAtlas, _s.IdleStreak);
            _rimMat = Make("IdleRim", 2f, _s.IdleRing, null);
            _starMat = Make("IdleTwinkle", 0f, _s.IdleStar, null);
            _mpb = new MaterialPropertyBlock();
            _built = true;
        }

        private Material Make(string name, float mode, Texture main, Texture streak)
        {
            var m = RuntimeMaterial.From(_s.GlintTemplate, "Project/PotaGlint", name);
            m.SetFloat("_Mode", mode);
            if (main != null) m.SetTexture("_BaseMap", main);
            if (streak != null) m.SetTexture("_StreakTex", streak);
            _owned.Add(m);
            return m;
        }

        private bool Has(SpecialIdle mode)
        {
            bool ok = mode == SpecialIdle.Glint ? _s.LabelAtlas != null && _s.IdleStreak != null
                : mode == SpecialIdle.Rim ? _s.IdleRing != null
                : mode == SpecialIdle.Twinkle ? _s.IdleStar != null : true;
            if (!ok && !_warned)
            {
                _warned = true;
                Debug.LogWarning($"Color Hunt: the {mode} idle is missing its Blender still (run ai_studio_import); it draws nothing.");
            }
            return ok;
        }

        /// <summary>
        /// Each frame for every resting special while a calm idle is chosen. <paramref name="mode"/> Look returns at once and writes
        /// nothing (the game calls the look's own idle instead).
        /// </summary>
        public void Idle(BlockView v, float dt, SpecialKind kind, SpecialIdle mode)
        {
            if (mode == SpecialIdle.Look || v == null || kind == SpecialKind.None) return;
            if (!_states.TryGetValue(v, out var st)) _states[v] = st = new State { Cell = -1 };
            if (st.Cell != v.Cell || st.Kind != kind || st.Mode != mode) Restart(v, st, kind, mode);
            st.Frame = Time.frameCount;
            st.T += dt;
            var tune = Tune;
            float t = st.T;
            bool golden = kind == SpecialKind.Golden;
            switch (mode)
            {
                case SpecialIdle.Breath:
                    v.IdleScale = IdleCurves.Breath(t, st.Phase, tune.BreathAmount, tune.BreathHz);
                    break;
                case SpecialIdle.Sway:
                    if (golden) break;                                         // spec: the golden ball gets A/B/C/D only
                    v.IdleScale = IdleCurves.Breath(t, st.Phase, tune.SwayBreath, tune.SwayHz);
                    v.StickerSpin = IdleCurves.Sway(t, st.Phase, tune.SwayDegrees, tune.SwayHz);
                    st.Spun = true;
                    break;
                case SpecialIdle.Glint:
                    Event(st, dt, tune.GlintEvery, tune.GlintJitter, tune.GlintTime);
                    if (st.Age >= 0f) DrawGlint(v, st, kind, st.Age / Mathf.Max(1e-3f, tune.GlintTime));
                    else Hide(st);
                    break;
                case SpecialIdle.Rim:
                    DrawRim(v, st, kind, IdleCurves.RimAlpha(t, st.Phase, tune.RimPeak, tune.RimHz));
                    break;
                case SpecialIdle.Twinkle:
                    if (Event(st, dt, tune.TwinkleEvery, tune.TwinkleJitter, tune.TwinkleLife))
                        st.Angle = IdleCurves.TwinkleAngle(_rng, tune.TwinkleAngleMin, tune.TwinkleAngleMax);
                    if (st.Age >= 0f) DrawTwinkle(v, st, golden, st.Age / Mathf.Max(1e-3f, tune.TwinkleLife));
                    else Hide(st);
                    break;
            }
        }

        /// <summary>The ball stopped resting (it drops, bursts or left the board): its overlay hides and the fields this idle wrote rest.</summary>
        public void Release(BlockView v)
        {
            if (v == null || !_states.TryGetValue(v, out var st)) return;
            Forget(v, st);
            _states.Remove(v);
        }

        /// <summary>Drops every overlay and puts back every field written (a mode switch, a look switch, a new board).</summary>
        public void Clear()
        {
            foreach (var kv in _states) Forget(kv.Key, kv.Value);
            _states.Clear();
        }

        private void Restart(BlockView v, State st, SpecialKind kind, SpecialIdle mode)
        {
            Forget(v, st);
            st.Kind = kind;
            st.Cell = v.Cell;
            st.Mode = mode;
            st.Phase = IdleCurves.Phase(v.Cell);
            st.T = 0f;
            st.Age = -1f;
            var tune = Tune;
            st.Next = mode == SpecialIdle.Glint ? IdleCurves.FirstDelay(v.Cell, tune.GlintEvery)
                : mode == SpecialIdle.Twinkle ? IdleCurves.FirstDelay(v.Cell, tune.TwinkleEvery) : 0f;
        }

        /// <summary>Hides the overlay and resets what this idle wrote on <paramref name="v"/> (a recycled view is reset by the game anyway).</summary>
        private void Forget(BlockView v, State st)
        {
            if (st.Quad != null)
            {
                st.Quad.enabled = false;
                _pool.Push(st.Quad);
                st.Quad = null;
            }
            if (v == null) return;
            v.IdleScale = 1f;
            if (st.Spun) v.StickerSpin = 0f;
            st.Spun = false;
        }

        /// <summary>B, D: counts down to the next start (start to start) and ages a running one; true on the frame one starts.</summary>
        private bool Event(State st, float dt, float every, float jitter, float life)
        {
            bool started = false;
            st.Next -= dt;
            if (st.Next <= 0f)
            {
                st.Next += IdleCurves.NextInterval(_rng, every, jitter);
                if (st.Next <= 0f) st.Next = IdleCurves.NextInterval(_rng, every, jitter);
                st.Age = 0f;
                started = true;
            }
            else if (st.Age >= 0f)
            {
                st.Age += dt;
                if (st.Age >= life) st.Age = -1f;
            }
            return started;
        }

        private void Hide(State st)
        {
            if (st.Quad != null) st.Quad.enabled = false;
        }

        private MeshRenderer Show(BlockView v, State st, Material mat)
        {
            if (st.Quad == null)
            {
                st.Quad = _pool.Count > 0 ? _pool.Pop() : NewQuad();
            }
            var q = st.Quad;
            if (q.sharedMaterial != mat) q.sharedMaterial = mat;
            if (q.gameObject.layer != v.gameObject.layer) q.gameObject.layer = v.gameObject.layer;   // F7: the board layer's pass, after the sticker
            q.enabled = true;
            return q;
        }

        private MeshRenderer NewQuad()
        {
            var go = new GameObject("IdleOverlay");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortingOrder = Order;
            r.enabled = false;
            return r;
        }

        /// <summary>World units per board cell and the step toward the camera.</summary>
        private bool Frame(out float cell, out Vector3 lift)
        {
            cell = 1f;
            lift = Vector3.zero;
            if (_game == null || _game.BoardRoot == null) return false;
            var root = _game.BoardRoot;
            cell = root.lossyScale.x;
            lift = -root.forward * (Lift * root.lossyScale.z);
            return true;
        }

        private void Place(Transform q, Vector3 world, Quaternion rotation, float sizeWorld)
        {
            q.SetPositionAndRotation(world, rotation);
            var parent = transform.lossyScale;
            q.localScale = new Vector3(sizeWorld / Mathf.Max(1e-6f, parent.x), sizeWorld / Mathf.Max(1e-6f, parent.y), 1f);
        }

        /// <summary>B: a quad on the sticker (same place, turn and size) that samples the sticker's own atlas cell as the streak's mask.</summary>
        private void DrawGlint(BlockView v, State st, SpecialKind kind, float u)
        {
            if (!_built || !Has(SpecialIdle.Glint) || !Frame(out float cell, out var lift)) { Hide(st); return; }
            var tune = Tune;
            var sticker = v.Sticker;
            var q = Show(v, st, _glintMat);
            float side = sticker.lossyScale.x;                                  // the sticker quad's world width
            Place(q.transform, sticker.position + lift, sticker.rotation, side);
            float stickerCells = side / Mathf.Max(1e-6f, cell);
            float turn = sticker.rotation.eulerAngles.z;                         // board turn + label angle + the line's 90° + any spin
            q.GetPropertyBlock(_mpb);
            _mpb.SetVector(UvRectId, LabelAtlas.UvRect(LabelAtlas.SpecialCell(kind)));
            _mpb.SetVector(ColorId, new Vector4(1f, 1f, 1f, tune.GlintBrightness));
            _mpb.SetFloat(SweepId, IdleCurves.GlintSweep(u));
            _mpb.SetFloat(AngleId, (-tune.GlintAngle - turn) * Mathf.Deg2Rad);    // across a '/' streak, fixed on screen
            _mpb.SetFloat(SpanId, tune.GlintWidth / Mathf.Max(1e-4f, stickerCells) / StreakCore);
            _mpb.SetFloat(LengthId, StreakLength);
            q.SetPropertyBlock(_mpb);
        }

        /// <summary>C: a ring at the ball's edge, its own colour per kind, on a quad just inside the cell.</summary>
        private void DrawRim(BlockView v, State st, SpecialKind kind, float alpha)
        {
            if (!_built || !Has(SpecialIdle.Rim) || !Frame(out float cell, out var lift)) { Hide(st); return; }
            var tune = Tune;
            var q = Show(v, st, _rimMat);
            float k = v.transform.localScale.x;                                  // the block's own squash and swell
            Place(q.transform, v.Sticker.position + lift, Quaternion.identity, RimQuad * k * cell);
            var c = (kind == SpecialKind.Bomb ? tune.RimBomb : kind == SpecialKind.Golden ? tune.RimGolden : tune.RimLine).linear;
            q.GetPropertyBlock(_mpb);
            _mpb.SetVector(ColorId, new Vector4(c.r, c.g, c.b, alpha));
            _mpb.SetFloat(RadiusId, tune.RimRadius / RimQuad);
            _mpb.SetFloat(HalfId, 0.5f * tune.RimThickness / RimQuad);
            _mpb.SetFloat(TexRadiusId, RingRadius);
            _mpb.SetFloat(TexHalfId, 0.5f * RingCore);
            q.SetPropertyBlock(_mpb);
        }

        /// <summary>D: one star at the ball's upper-right edge (screen axes), growing and fading over its life.</summary>
        private void DrawTwinkle(BlockView v, State st, bool golden, float u)
        {
            if (!_built || !Has(SpecialIdle.Twinkle) || !Frame(out float cell, out var lift)) { Hide(st); return; }
            var tune = Tune;
            var q = Show(v, st, _starMat);
            float k = v.transform.localScale.x, a = st.Angle * Mathf.Deg2Rad;
            var edge = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (tune.TwinkleRadius * k * cell);
            Place(q.transform, v.Sticker.position + lift * 1.5f + edge, Quaternion.Euler(0f, 0f, 15f * u),
                tune.TwinkleSize * IdleCurves.TwinkleScale(u) * cell);
            var c = (golden ? tune.TwinkleGolden : tune.TwinkleColor).linear;
            q.GetPropertyBlock(_mpb);
            _mpb.SetVector(ColorId, new Vector4(c.r, c.g, c.b, tune.TwinkleBrightness * IdleCurves.TwinkleAlpha(u)));
            q.SetPropertyBlock(_mpb);
        }

        /// <summary>A ball the game stopped idling this frame without a Release (it left the board's views): hide and forget it.</summary>
        private void LateUpdate()
        {
            if (_states.Count == 0) return;
            int frame = Time.frameCount;
            _stale.Clear();
            foreach (var kv in _states)
                if (kv.Value.Frame != frame || kv.Key == null || !kv.Key.isActiveAndEnabled) _stale.Add(kv.Key);
            foreach (var v in _stale)
            {
                Forget(v, _states[v]);
                _states.Remove(v);
            }
        }

        private void OnDestroy()
        {
            foreach (var m in _owned) if (m != null) Destroy(m);
            _owned.Clear();
        }
    }
}
