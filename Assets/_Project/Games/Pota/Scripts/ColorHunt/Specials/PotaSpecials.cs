using System;
using System.Collections.Generic;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// The Pota look for special balls, renkAviFaz.ts 1:1 (spec 2026-10-01 §2 "Pota"). Birth (dususleriBaslat): flash 1.0, squash 0.6, a
    /// light flash (golden #ffe27a, else white; 1.6 / 3.4 / 5 / 0.2 s), altin-dogum or kombo(2), "ALTIN TOP!" / "BOMBA!" / "ÇİZGİ TOPU!"
    /// in gold. Idle: nothing here (the game drives the golden pulse). Fire (adimTepe, at the step's first peak): a bomb — #ffd28a
    /// 2.6 / 6.5 / 8 / 0.25 s, shake 0.2, bomba; a line — three #ffe0b0 1.2 / 2.2 / 3 / 0.15 s flashes at the line's ends and centre,
    /// shake 0.12, cizgi; and the ×0.12 dip for 0.06 s. Golden flight (altinlariIlerlet): the salvo ball tinted #ffc93c, ×1.25, spinning
    /// 9 rad/s, drops to its gate (the ring flashes), arcs into the hoop; on arrival a #ffe27a flash at the rim (1.8 / 4.2 / 5 / 0.22 s,
    /// sized in game-root units), altin-inis and the net swing. Every cell of a trigger step swells at once.
    /// </summary>
    public class PotaSpecials : MonoBehaviour, ISpecialStyle
    {
        // renkAviFaz.ts RENK_AVI_HIS.ozel / altinInis and the flasGoster arguments; yapbozSahne.ts cizToplar (TOP_TABAN).
        public const float LineShake = 0.12f, BombShake = 0.2f, HitStop = 0.06f, BornFlash = 1f, BornSquash = 0.6f;
        public const uint BombFlash = 0xffd28a, LineFlash = 0xffe0b0, GoldenFlash = 0xffe27a, White = 0xffffff;
        public const uint BallGold = 0xffc93c, BallBase = 0xf07d1d;
        public const float BallScale = 1.25f, BallRadius = 0.19f, BallSpin = 9f;

        private class Flight
        {
            public GoldenRoute Route;
            public float T, Spin;
            public bool Passed;
            public Action PassedGate, Landed;
            public Transform Ball;
        }

        private ColorHuntGame _game;
        private readonly List<Flight> _flights = new List<Flight>();
        private readonly Stack<Transform> _balls = new Stack<Transform>();
        private Material _ballMat;
        private int _gen;

        public bool Ready => true;
        public int LiveCount => _flights.Count;
        public SpecialTiming Timing => SpecialTiming.Pota;
        public bool GathersBirthGroup => false;
        public bool ConsumesSpecialView => false;

        public void Build(ColorHuntGame game) => _game = game;

        public void Activate(bool on)
        {
            if (!on) Clear();
        }

        public void Clear()
        {
            _gen++;
            foreach (var f in _flights) Hide(f);
            _flights.Clear();
        }

        public void Idle(BlockView v, float dt) { }

        public void Gather(BlockView v, Vector2 to, float duration, Action<BlockView> arrived) => arrived?.Invoke(v);

        public void Birth(BlockView v, SpecialKind kind)
        {
            if (_game == null) return;
            v.Flash = BornFlash;
            v.Squash = BornSquash;
            bool golden = kind == SpecialKind.Golden;
            _game.Fx?.Glow(v.Pos, PotaLook.Hex(golden ? GoldenFlash : White).linear, 1.6f, 3.4f, 5f, 0.2f);
            var sounds = Sounds();
            if (sounds != null)
            {
                if (golden) sounds.PlayClip("ozelDogdu", _game.S.SfxGoldenBorn);
                else sounds.Combo(2);
            }
            var popups = _game.Juice != null ? _game.Juice.Popups : null;
            if (popups != null)
                popups.Show(_game.BoardRoot.TransformPoint(new Vector3(v.Pos.x, v.Pos.y + 0.6f, -PotaLook.LabelZ)), SpecialTexts.Born(kind),
                    PotaLook.HudGold, 72, false, 1.2f);                     // .patlama.kombo.dogum: gold 44 px, 1.2 s
        }

        public void TriggerStep(int specials, Vector2 centre) { }

        public void Fire(SpecialFire f)
        {
            if (_game == null) return;
            var fx = _game.Fx;
            var juice = _game.Juice;
            if (f.Kind == SpecialKind.Bomb)
            {
                fx?.Glow(f.At, PotaLook.Hex(BombFlash).linear, 2.6f, 6.5f, 8f, 0.25f);
                juice?.Shake?.Add(BombShake);
                Sounds()?.PlayClip("patlama", _game.S.SfxBomb);
            }
            else if (f.Kind == SpecialKind.LineV || f.Kind == SpecialKind.LineH)
            {
                float half = (_game.Board.Size - 1) * 0.5f;                  // the line's two ends and its centre
                foreach (float u in new[] { -half, 0f, half })
                    fx?.Glow(f.Kind == SpecialKind.LineV ? new Vector2(f.At.x, u) : new Vector2(u, f.At.y), PotaLook.Hex(LineFlash).linear, 1.2f, 2.2f, 3f, 0.15f);
                juice?.Shake?.Add(LineShake);
                Sounds()?.PlayClip("cizgi", _game.S.SfxLine);
            }
            juice?.TimeCtl?.Dip(_game.S.HitStopTapScale, HitStop);           // zamanDusur(HIS.hitStop.olcek, ozel.hitStop)
        }

        public void GoldenFlight(GoldenRoute route, Action passedGate, Action landed)
        {
            var f = new Flight { Route = route, PassedGate = passedGate, Landed = landed, Ball = TakeBall() };
            _flights.Add(f);
        }

        private void Update()
        {
            if (_flights.Count == 0 || _game == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            int gen = _gen;
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                if (gen != _gen || i >= _flights.Count) return;              // a callback dealt a new board
                var f = _flights[i];
                f.T += dt;
                float t = f.T - f.Route.Delay;
                if (t < 0f) continue;                                         // the step's next ball leaves 0.12 s later
                f.Spin += dt * BallSpin;
                float s = f.Route.Scale > 0f ? f.Route.Scale : 1f;
                if (f.Ball != null)
                {
                    f.Ball.gameObject.SetActive(true);
                    f.Ball.localPosition = GoldenPath.At(f.Route, t) + new Vector3(0f, 0f, -(PotaLook.FrontZ + 0.2f) * s);   // cizToplar z
                    f.Ball.localScale = Vector3.one * (2f * BallRadius * BallScale * s);
                    f.Ball.localRotation = Quaternion.Euler(new Vector3(f.Spin, f.Spin * 0.6f, f.Spin * 0.3f) * Mathf.Rad2Deg);
                }
                if (!f.Passed && GoldenPath.PastGate(t))
                {
                    f.Passed = true;
                    var gates = _game.Gates;
                    if (gates != null) gates.Flash(f.Route.Col);                 // flasGoster(bx, halkaY, #ffe27a, 0.9, 2.0)
                    f.PassedGate?.Invoke();
                    if (gen != _gen) return;
                }
                if (!GoldenPath.Landed(t)) continue;
                _flights.RemoveAt(i);
                Hide(f);
                Land(f.Route);
                f.Landed?.Invoke();
                if (gen != _gen) return;
            }
        }

        /// <summary>altinlariIlerlet arrival: flash at the rim (sizes ÷ s: they are game-root units), altinInis, fileSalla.</summary>
        private void Land(GoldenRoute r)
        {
            float s = r.Scale > 0f ? r.Scale : 1f;
            var p = _game.BoardRoot.InverseTransformPoint(_game.transform.TransformPoint(r.Hoop));
            _game.Fx?.Glow(new Vector2(p.x, p.y), PotaLook.Hex(GoldenFlash).linear, 1.8f / s, 4.2f / s, 5f, 0.22f, -p.z);
            Sounds()?.PlayClip("altinInis", _game.S.SfxGoldenLand);
            var hoop = _game.Hoop;
            if (hoop != null) hoop.Swing();
        }

        /// <summary>The salvo ball (sphere r 0.19, S.SalvoBall) tinted so its base orange becomes #ffc93c (ton = renk / TOP_TABAN, linear).</summary>
        private Transform TakeBall()
        {
            if (_balls.Count > 0) return _balls.Pop();
            var template = _game != null && _game.S != null ? _game.S.SalvoBall : null;
            if (template == null) return null;                                // not imported: the flight still runs, unseen
            if (_ballMat == null)
            {
                var gold = PotaLook.Hex(BallGold).linear;
                var basis = PotaLook.Hex(BallBase).linear;
                _ballMat = RuntimeMaterial.From(template, "Universal Render Pipeline/Lit", "PotaGoldenBall");   // PotaBall.mat is URP Lit
                _ballMat.SetColor("_BaseColor", new Color(gold.r / basis.r, gold.g / basis.g, gold.b / basis.b, 1f).gamma);   // SetColor linearises
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "GoldenBall";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);                           // this object sits at the game root's origin
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _ballMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go.transform;
        }

        private void Hide(Flight f)
        {
            if (f.Ball == null) return;
            f.Ball.gameObject.SetActive(false);
            _balls.Push(f.Ball);
            f.Ball = null;
        }

        private PotaSounds Sounds() => _game != null && _game.Juice != null ? _game.Juice.Sounds : null;

        private void OnDestroy()
        {
            if (_ballMat != null) Destroy(_ballMat);
        }
    }
}
