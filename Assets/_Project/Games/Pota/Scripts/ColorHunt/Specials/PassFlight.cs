using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>A "Pas" (OZEL_AN.pasTopu.a, src/ozelAyar.ts:51): the ball's own block hops toward the camera turning, then a low arc.</summary>
    [Serializable]
    public class PassTuningA
    {
        public PassTiming Timing = PassTiming.A;
        [Tooltip("zipla: cells toward the camera during the takeoff (sin ease), back to 0 at the hit.")] public float Hop = 0.18f;
        [Tooltip("donus: the block's turn in the board plane (rad/s).")] public float Spin = 14f;
        [Tooltip("kavis: the arc's height (cells toward the camera, sin(π·o)).")] public float Arc = 0.6f;
        [Tooltip("iz: speed lines behind the ball every Trail cells (the line ball's aralik 0.5).")] public bool Trail = true;
        [Tooltip("ozelAn.cizgi.aralik: cells between two speed-line sheets.")] public float TrailEvery = 0.5f;
        [Tooltip("carpmaFlas: the white flash at the hit.")] public float ImpactFlash = 1.6f;
        [Tooltip("sars: the board shake at the hit.")] public float Shake = 0.1f;
    }

    /// <summary>B "Pervane" (OZEL_AN.pasTopu.b, src/ozelAyar.ts:52): the Blender ball rises with its propeller spinning up, a high arc, hangs, dives.</summary>
    [Serializable]
    public class PassTuningB
    {
        public PassTiming Timing = PassTiming.B;
        [Tooltip("yuksel: cells toward the camera reached during the takeoff (linear).")] public float Rise = 0.55f;
        [Tooltip("donus: rad/s; the ball turns about its own up axis at 0.2 × this (Pota pasTopuGoster(…, donme·0.2, …)).")] public float Spin = 9f;
        [Tooltip("pervane: the propeller's spin (rad/s).")] public float Propeller = 40f;
        [Tooltip("kavis: the arc over the rise (cells, sin(π·o)).")] public float Arc = 1.6f;
        [Tooltip("dalis: in OZEL_AN but unused by Pota's B flight (the dive is the hang: rise → 0 over pasAsili); kept for the port.")] public float Dive = 0.35f;
        [Tooltip("carpmaFlas: the white flash at the hit.")] public float ImpactFlash = 1.9f;
        [Tooltip("sars: the board shake at the hit.")] public float Shake = 0.14f;
        [Tooltip("toz: the dust ring's size at the hit (cells).")] public float Dust = 2.6f;
        [Tooltip("The GLB ball is 0.45 m: × 2 = the block's 0.9 cell (Pota olcek 2).")] public float ModelScale = 2f;
        [Tooltip("Unity's own (not in Pota's Task 8 sketch): the ball's top leans this many degrees toward the camera, so the propeller reads " +
                 "as a turning disc like the icon's; at 0 the camera sees it edge-on (a flickering line).")] public float Tilt = 35f;
    }

    /// <summary>
    /// The pass ball's flight (Pota renkAviFaz.ts pasIlerlet / pasIlerletB / pasVurdu): from its cell to its one target, in one of two looks.
    /// The game owns the rules' replay (the plan bursts the target the frame the ball lands, both on the step's clock) and hands A the ball's
    /// own block; B hides that block and flies the Blender ball (Basketball + Propeller, the propeller turning about its own up axis). The
    /// game ticks this after its own jobs with the same clamped game dt, so hit-stops and slow motion slow the flight too. Positions are
    /// board-local cells (BoardRoot), Unity −z toward the camera.
    /// </summary>
    public class PassFlight : MonoBehaviour
    {
        public const int ModelPool = 4;
        /// <summary>yapbozSahne.ts pufPatlat at a target-less landing (OZEL_AN.altinVurus.carpmaPuf).</summary>
        public const float EmptyPuff = 0.9f;
        /// <summary>patlamaEfekt.ts hizCizgisi: the speed-line sheet's length and width (cells), z over the block front, life (s).</summary>
        public const float LineLength = 2.6f, LineWidth = 0.9f, LineZ = 0.32f, LineLife = 0.25f;
        /// <summary>patlamaEfekt.ts tozHalkasi: z over the block front, life (s).</summary>
        public const float DustZ = 0.33f, DustLife = 0.4f;
        /// <summary>The front of the blocks' particle layers (PotaFx 5–9): the lines and the dust read over them.</summary>
        public const int Order = 12;

        private sealed class Model { public Transform Root, Ball, Propeller; }
        private sealed class Flight
        {
            public PassLook Look;
            public BlockView View;
            public Model Model;
            public Vector2 From, To, Pos;
            public bool HasTarget;
            public float T, Duration, Turn, Trail, Lift;
            public Action<BlockView> Landed;
        }

        private ColorHuntGame _game;
        private ColorHuntSettings _s;
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly Stack<Model> _models = new Stack<Model>();
        private ParticleSystem _lines, _dust;
        private Mesh _ball, _propeller;
        private Material[] _ballMats, _propMats;
        private Vector3 _propellerLocal;
        private AudioClip _whoosh;
        private bool _built;

        /// <summary>Flights in the air (the board is busy meanwhile).</summary>
        public int InFlight => _flights.Count;
        /// <summary>B's model and its pieces are in place.</summary>
        public bool ModelReady => _ball != null;
        /// <summary>Live speed lines and dust (BurstBudget).</summary>
        public int LiveCount => _flights.Count + StudioParticles.Count(_lines, _dust);
        /// <summary>The A flight's lift of a flight (tests): cells toward the camera of the first flight, −1 when none.</summary>
        public float FirstLift => _flights.Count > 0 ? _flights[0].Lift : -1f;
        public Vector2 FirstPos => _flights.Count > 0 ? _flights[0].Pos : Vector2.zero;

        public void Build(ColorHuntGame game)
        {
            _game = game;
            _s = game != null ? game.S : null;
            if (_built || _s == null) return;
            _built = true;
            if (_s.SpeedLines != null)
            {
                _lines = StudioParticles.System(transform, "PassSpeedLines", StudioParticles.Flipbook(_s, _s.SpeedLines), ParticleSystemRenderMode.Mesh, 48, Order);
                _lines.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");   // faces −z, the camera
                StudioParticles.Sheet(_lines, 4, 2);
            }
            if (_s.CourtDust != null)
            {
                _dust = StudioParticles.System(transform, "PassDust", StudioParticles.Flipbook(_s, _s.CourtDust), ParticleSystemRenderMode.Billboard, 8, Order);
                StudioParticles.Sheet(_dust, 4, 4);
            }
            BuildModel(_s.PassModel);
            _whoosh = CourtSpecials.MakePass();                  // ses.ts "pas": the Court pass "vıııt" (Pota's own copy of MakePass)
        }

        /// <summary>B's pieces: the Basketball (toon, its skin) and the Propeller (toon, the GLB's colours), at the GLB's own offsets.</summary>
        private void BuildModel(GameObject model)
        {
            if (model == null) return;
            foreach (var p in SpecialMeshes.Parts(model))
            {
                var renderer = FindRenderer(model, p.Name);
                if (p.Name.StartsWith("Basketball")) { _ball = p.Mesh; _ballMats = ToonMats(model, renderer, "PassBall"); }
                else if (p.Name.StartsWith("Propeller")) { _propeller = p.Mesh; _propMats = ToonMats(model, renderer, "PassPropeller"); _propellerLocal = p.Local; }
            }
            if (_ball == null) { _propeller = null; return; }
        }

        private static Renderer FindRenderer(GameObject model, string name)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) if (r.name == name) return r;
            return null;
        }

        /// <summary>One Project/Toon per GLB material: the skin where it has one, else the material's base colour on white.</summary>
        private Material[] ToonMats(GameObject model, Renderer source, string name)
        {
            var mats = source != null ? source.sharedMaterials : new Material[1];
            var result = new Material[Mathf.Max(1, mats.Length)];
            for (int i = 0; i < result.Length; i++)
            {
                var src = i < mats.Length ? mats[i] : null;
                var m = SpecialMeshes.Toon(_s, null, $"{name}{i}", 0.006f);
                Texture skin = null;
                var color = Color.white;
                if (src != null)
                {
                    foreach (string prop in src.GetTexturePropertyNames())
                        if ((prop.IndexOf("baseColor", StringComparison.OrdinalIgnoreCase) >= 0 || prop == "_BaseMap" || prop == "_MainTex") && src.GetTexture(prop) != null)
                        { skin = src.GetTexture(prop); break; }
                    if (src.HasProperty("baseColorFactor")) color = src.GetColor("baseColorFactor");
                    else if (src.HasProperty("_BaseColor")) color = src.GetColor("_BaseColor");
                }
                if (skin != null) m.SetTexture("_BaseMap", skin);
                m.SetColor("_BaseColor", color);
                result[i] = m;
            }
            return result;
        }

        private Model TakeModel()
        {
            if (_ball == null) return null;
            if (_models.Count > 0) { var pooled = _models.Pop(); pooled.Root.gameObject.SetActive(true); return pooled; }
            var root = new GameObject("PassBall").transform;
            var ball = Piece(root, "Basketball", _ball, _ballMats);
            var prop = _propeller != null ? Piece(root, "Propeller", _propeller, _propMats) : null;
            if (prop != null) prop.localPosition = _propellerLocal;
            return new Model { Root = root, Ball = ball, Propeller = prop };
        }

        private static Transform Piece(Transform root, string name, Mesh mesh, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        private void HideModel(Model m)
        {
            if (m == null) return;
            m.Root.gameObject.SetActive(false);
            _models.Push(m);
        }

        /// <summary>
        /// A pass ball leaves (Pota pasAtesle): <paramref name="from"/> its cell, <paramref name="to"/> its target's (null: no target — it
        /// hops in place for the takeoff and puffs), <paramref name="duration"/> = PassTiming.Arrival (the takeoff without a target),
        /// <paramref name="age"/> = how far the step's clock already is past its fire. A flies <paramref name="view"/> (the ball's block); B
        /// hides it and flies the model. <paramref name="landed"/> gets the view back once, at the hit (or at the puff).
        /// </summary>
        public void Fly(PassLook look, Vector2 from, Vector2? to, float duration, float age, BlockView view, Action<BlockView> landed)
        {
            if (look == PassLook.B && _ball == null) look = PassLook.A;   // no model: A's flight rather than nothing
            var f = new Flight
            {
                Look = look, View = view, From = from, To = to ?? from, Pos = from, HasTarget = to.HasValue,
                Duration = Mathf.Max(1e-3f, duration), T = Mathf.Max(0f, age), Landed = landed,
            };
            if (view != null)
            {
                view.Exploding = true;
                view.Pressed = false;
                if (look == PassLook.B) view.gameObject.SetActive(false);
            }
            if (look == PassLook.B)
            {
                f.Model = TakeModel();
                if (f.Model != null)
                {
                    f.Model.Root.SetParent(_game != null && _game.BoardRoot != null ? _game.BoardRoot : transform, false);
                    f.Model.Root.localScale = Vector3.one * _s.PassLookB.ModelScale;
                }
            }
            _flights.Add(f);
            Place(f);
            _game?.Juice?.Sounds?.PlayClip("pas", _whoosh);
        }

        /// <summary>A new board or a look switch: every flight ends now, silently; the game recycles the views it handed over.</summary>
        public void Clear()
        {
            foreach (var f in _flights) HideModel(f.Model);
            _flights.Clear();
            if (_lines != null) _lines.Clear();
            if (_dust != null) _dust.Clear();
        }

        /// <summary>The game calls this each frame before its jobs, with its own clamped dt (see ColorHuntGame.Update).</summary>
        public void Tick(float dt)
        {
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (i >= _flights.Count) continue;                   // a landing callback may have cleared the list
                var f = _flights[i];
                float before = f.T;
                f.T += dt;
                if (f.Look == PassLook.A) StepA(f, before); else StepB(f);
                Place(f);
                if (f.T < f.Duration) continue;
                _flights.RemoveAt(i);
                Land(f);
            }
        }

        /// <summary>renkAviFaz.ts pasIlerlet (A): the hop (sin ease) during the takeoff, then the straight path with zipla·(1 − o) + sin(πo)·kavis, speed lines on the way.</summary>
        private void StepA(Flight f, float before)
        {
            var a = _s.PassLookA;
            float takeoff = a.Timing.Takeoff;
            f.Turn += (f.T - before) * a.Spin;
            if (!f.HasTarget || f.T < takeoff) { f.Lift = Mathf.Sin(Mathf.PI * 0.5f * Mathf.Min(1f, f.T / takeoff)) * a.Hop; return; }
            float o = Mathf.Min(1f, (f.T - takeoff) / Mathf.Max(1e-3f, f.Duration - takeoff));
            var d = f.To - f.From;
            f.Pos = f.From + d * o;
            f.Lift = a.Hop * (1f - o) + Mathf.Sin(Mathf.PI * o) * a.Arc;
            float path = d.magnitude;
            if (!a.Trail || _lines == null || path < 1e-3f) return;
            bool x = Mathf.Abs(d.x) >= Mathf.Abs(d.y);
            for (int k = 0; k < 4 && o * path - f.Trail >= a.TrailEvery; k++)
            {
                f.Trail += a.TrailEvery;
                SpeedLine(f.From + d * (f.Trail / path), x, x ? d.x : d.y);
            }
        }

        /// <summary>renkAviFaz.ts pasIlerletB: rise linearly over the takeoff, the arc over the rise, then over the target the hang dives to 0.</summary>
        private void StepB(Flight f)
        {
            var b = _s.PassLookB;
            float takeoff = b.Timing.Takeoff, end = f.Duration - b.Timing.Hang;
            f.Turn = f.T * b.Spin;
            if (!f.HasTarget || f.T < takeoff) f.Lift = b.Rise * Mathf.Min(1f, f.T / takeoff);
            else if (f.T < end)
            {
                float o = (f.T - takeoff) / Mathf.Max(1e-3f, end - takeoff);
                f.Pos = f.From + (f.To - f.From) * o;
                f.Lift = b.Rise + Mathf.Sin(Mathf.PI * o) * b.Arc;
            }
            else
            {
                f.Pos = f.To;
                f.Lift = b.Rise * (1f - Mathf.Min(1f, (f.T - end) / Mathf.Max(1e-3f, b.Timing.Hang)));
            }
        }

        /// <summary>Writes the flight onto its block (A) or its model (B): position, lift toward the camera, turn.</summary>
        private void Place(Flight f)
        {
            if (f.Look == PassLook.A)
            {
                if (f.View == null) return;
                f.View.Pos = f.Pos;
                f.View.Lift = f.Lift;
                f.View.Roll = f.Turn * Mathf.Rad2Deg;
                return;
            }
            if (f.Model == null) return;
            f.Model.Root.localPosition = new Vector3(f.Pos.x, f.Pos.y, -f.Lift);
            f.Model.Root.localRotation = Quaternion.Euler(-_s.PassLookB.Tilt, 0f, 0f) * Quaternion.Euler(0f, f.Turn * 0.2f * Mathf.Rad2Deg, 0f);
            if (f.Model.Propeller != null) f.Model.Propeller.localRotation = Quaternion.Euler(0f, f.T * _s.PassLookB.Propeller * Mathf.Rad2Deg, 0f);
        }

        /// <summary>renkAviFaz.ts pasBitti / pasVurdu: a white flash, the shake and carpma at the target (B: + the dust ring); no target: a puff.</summary>
        private void Land(Flight f)
        {
            HideModel(f.Model);
            f.Model = null;
            var fx = _game != null ? _game.Fx : null;
            var juice = _game != null ? _game.Juice : null;
            if (!f.HasTarget) fx?.Puff(f.Pos, EmptyPuff);
            else
            {
                float flash = f.Look == PassLook.A ? _s.PassLookA.ImpactFlash : _s.PassLookB.ImpactFlash;
                float shake = f.Look == PassLook.A ? _s.PassLookA.Shake : _s.PassLookB.Shake;
                fx?.Glow(f.To, Color.white.linear, flash, 2.4f, 3f, 0.15f);       // flasGoster(x, y, 0xffffff, carpmaFlas, 2.4, 3, 0.15)
                juice?.Shake?.Add(shake);
                var hits = _s.SfxHit;
                if (juice?.Sounds != null && hits != null && hits.Length > 0) juice.Sounds.PlayClip("carpma", hits[UnityEngine.Random.Range(0, hits.Length)], 0.8f);
                if (f.Look == PassLook.B && _dust != null)
                    StudioParticles.Emit(_dust, World(f.To, PotaLook.FrontZ + DustZ), _s.PassLookB.Dust * Scale, DustLife, Color.white);
            }
            if (f.View != null)
            {
                f.View.Lift = 0f;
                f.View.Roll = 0f;
            }
            try { f.Landed?.Invoke(f.View); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>patlamaEfekt.ts hizCizgisi: a length × width sheet behind the ball along the path's main axis (the ball at the sheet's right edge).</summary>
        private void SpeedLine(Vector2 at, bool alongX, float sign)
        {
            float s = Mathf.Sign(sign);
            float angle = alongX ? (s > 0f ? 0f : 180f) : (s > 0f ? 90f : -90f);
            var back = alongX ? new Vector2(s, 0f) : new Vector2(0f, s);
            var centre = at - back * (LineLength * 0.45f);
            float k = Scale;
            StudioParticles.Emit3D(_lines, World(centre, PotaLook.FrontZ + LineZ), Vector3.zero, new Vector3(LineLength * k, LineWidth * k, 1f),
                new Vector3(0f, 0f, angle + BoardAngle), Vector3.zero, LineLife, Color.white);
        }

        private float Scale => _game != null && _game.BoardRoot != null ? _game.BoardRoot.lossyScale.x : 1f;
        private float BoardAngle => _game != null && _game.BoardRoot != null ? _game.BoardRoot.eulerAngles.z : 0f;

        /// <summary>Board-local cell + Pota depth (toward the camera) → world.</summary>
        private Vector3 World(Vector2 cell, float z)
        {
            var root = _game != null ? _game.BoardRoot : null;
            var local = new Vector3(cell.x, cell.y, -z);
            return root != null ? root.TransformPoint(local) : local;
        }
    }
}
